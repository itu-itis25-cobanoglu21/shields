using System;
using System.Collections.Generic;
using UnityEngine;
using Shields.Match;
using Shields.Players;
using Shields.Social;
using Shields.Table;

namespace Shields.Tavern
{
    /// <summary>
    /// Plays beer drinking on this machine, for any seat, from authoritative drink events.
    /// It decides nothing: the authority says when a drink started (start fill, rate,
    /// lead-in) and when it stopped (final fill); everything in between is animated
    /// locally and deterministically, so no transform is ever sent over the network.
    ///
    /// Sequence, with the drinker's LEFT hand and the very same mug throughout:
    ///   Reach  - hand from rest to the mug's grip anchor on the table
    ///   Settle - a brief squeeze, then the mug is attached to the hand
    ///   Bring  - mug up to the mouth; the visual head starts to tilt up
    ///   Drink  - rim at the mouth while the drink lasts; the emptier the mug, the more
    ///            the mug and the head tilt
    ///   Return - mug back into its own DrinkSlot, head and mug level out
    ///   Home   - hand back to rest
    ///
    /// The head tilt is written to the visual head only (a sibling of the camera anchor),
    /// so the local camera never moves; for the local drinker vertical look is frozen
    /// while the pose is on, yaw keeps working.
    ///
    /// Gameplay always wins: the moment a targeted action needs that seat's left hand, or
    /// the drinker dies, the drink lets go at once (the mug glides home on its own, the
    /// head and look are restored) and <see cref="Interrupted"/> tells the authority.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public class BeerDrinkPresenter : MonoBehaviour
    {
        public const string MouthAnchorName = "DrinkMouthAnchor";

        [Header("Timing (seconds)")]
        [SerializeField] float reachDuration = 0.25f;
        [SerializeField] float gripSettleDuration = 0.10f;
        [SerializeField] float bringDuration = 0.35f;
        [SerializeField] float returnDuration = 0.40f;
        [SerializeField] float handHomeDuration = 0.25f;

        [Tooltip("How long the empty mug stays at the mouth before it goes back.")]
        [SerializeField] float emptyFinishDuration = 0.3f;

        [Tooltip("Gameplay took the hand: the mug glides home this fast on its own.")]
        [SerializeField] float emergencyReturnDuration = 0.18f;

        [Tooltip("A local drink the authority never answers is abandoned after this long.")]
        [SerializeField] float confirmTimeout = 1.5f;

        [Header("Tilt (degrees) by how empty the mug is")]
        [Tooltip("Upward head pitch with a full mug.")]
        [SerializeField] float headTiltFull = 4f;

        [Tooltip("Upward head pitch with a nearly empty mug.")]
        [SerializeField] float headTiltEmpty = 12f;

        [Tooltip("Mug tilt from vertical with a full mug.")]
        [SerializeField] float mugTiltFull = 35f;

        [Tooltip("Mug tilt from vertical with a nearly empty mug.")]
        [SerializeField] float mugTiltEmpty = 70f;

        [Tooltip("x: how empty (0 full, 1 empty); y: how far from the full tilt toward the empty tilt.")]
        [SerializeField] AnimationCurve tiltByEmptiness = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 1.2f), new Keyframe(1f, 1f, 0.8f, 0f));

        [Header("Contact")]
        [Tooltip("The rim sits this far into the lips, so it reads as touching the mouth.")]
        [SerializeField] float lipOverlap = 0.006f;

        [Tooltip("Inward squeeze of the hand while it settles on the mug.")]
        [SerializeField] float gripSqueeze = 0.004f;

        [Tooltip("Mouth position in the visual head's space, used when the prefab has no DrinkMouthAnchor.")]
        [SerializeField] Vector3 fallbackMouthLocal = new Vector3(0f, 0.022f, 0.104f);

        [Tooltip("How fast the shown fill settles onto an authoritative final fill, per second.")]
        [SerializeField] float reconcileSpeed = 2f;

        enum Phase { Reach, Settle, Bring, Drink, Return, Home, Emergency }

        sealed class Motion
        {
            public int Seat;
            public BeerId Beer;
            public BeerMugVisual Mug;
            public PlayerCharacter Player;
            public Transform Hand;
            public Pose HandRest;
            public Transform Head;
            public Quaternion HeadRest;
            public Transform Mouth;
            public bool IsLocal;
            public Vector3 MugScale;

            public Phase Phase;
            public float PhaseStart;
            public float CreatedAt;

            public bool Confirmed;
            public float ConfirmedAt;
            public float StartFill;
            public float Rate;
            public float LeadIn;

            public bool Stopping;
            public bool HasFinal;
            public float FinalFill;
            public float EmptySince = -1f;

            public bool Attached;
            public bool LookLocked;
            public float HeadWeight;
            public float HeadWeightFrom;

            public Vector3 FromPosition;
            public Quaternion FromRotation;
        }

        readonly Dictionary<int, Motion> _motions = new Dictionary<int, Motion>();
        readonly Dictionary<Transform, Pose> _handRest = new Dictionary<Transform, Pose>();
        readonly List<int> _seats = new List<int>();

        TableLayoutController _table;
        PlayerActionPresenter _actions;
        Func<SeatedHeadLook> _localLook;

        /// <summary>A drink was cut short on this machine (gameplay took the hand, or the drinker died).</summary>
        public event Action<int, BeerId> Interrupted;

        /// <summary>A seat's whole drink presentation finished (hand back home or let go).</summary>
        public event Action<int> Finished;

        public void Bind(TableLayoutController table, Func<SeatedHeadLook> localLook)
        {
            _table = table;
            _localLook = localLook;
        }

        void OnDisable()
        {
            ClearAll();
            if (_actions != null) _actions.LeftHandClaimed -= OnLeftHandClaimed;
            _actions = null;
        }

        void Update()
        {
            // The one action presenter of the scene; found once, lazily, since it is shared
            // by the offline and online match drivers.
            if (_actions == null)
            {
                _actions = FindAnyObjectByType<PlayerActionPresenter>();
                if (_actions != null) _actions.LeftHandClaimed += OnLeftHandClaimed;
            }
        }

        // ------------------------------------------------------------------ queries

        /// <summary>True while any part of a drink (reach to hand home) is playing for the seat.</summary>
        public bool IsBusy(int seat) { return _motions.ContainsKey(seat); }

        /// <summary>True while the seat's mug is at, or on its way to, the mouth.</summary>
        public bool IsDrinking(int seat)
        {
            Motion m;
            return _motions.TryGetValue(seat, out m) && !m.Stopping && m.Phase != Phase.Emergency;
        }

        /// <summary>
        /// The seat's oldest delivered mug that still has beer and rests in its slot. Lowest
        /// order id first, so every machine picks the same mug.
        /// </summary>
        public BeerMugVisual FindOldestDrinkable(int seat)
        {
            BeerMugVisual best = null;
            IReadOnlyList<BeerMugVisual> mugs = BeerMugVisual.Active;

            for (int i = 0; i < mugs.Count; i++)
            {
                BeerMugVisual mug = mugs[i];
                if (mug == null || !mug.IsDelivered || mug.Beer.Seat != seat || !mug.IsHome || mug.Fill <= 0.001f) continue;
                if (best != null && mug.Beer.CompareAge(best.Beer) >= 0) continue;

                best = mug;
            }

            return best;
        }

        public static BeerMugVisual FindMug(BeerId beer)
        {
            IReadOnlyList<BeerMugVisual> mugs = BeerMugVisual.Active;
            for (int i = 0; i < mugs.Count; i++)
                if (mugs[i] != null && mugs[i].IsDelivered && mugs[i].Beer == beer) return mugs[i];

            return null;
        }

        // ------------------------------------------------------------------ events in

        /// <summary>
        /// Cosmetic: the local drinker pressed the key, so the hand reaches at once. Nothing
        /// drains until the authority confirms; a refusal or no answer brings the hand back.
        /// </summary>
        public bool BeginLocal(int seat, BeerMugVisual mug)
        {
            if (_motions.ContainsKey(seat) || mug == null) return false;

            return Create(seat, mug, true) != null;
        }

        /// <summary>The authority accepted a drink. Starts it here too if this machine was not already reaching.</summary>
        public void Confirm(BeerDrinkStart start, bool isLocalSeat)
        {
            int seat = start.Beer.Seat;
            Motion m;

            if (_motions.TryGetValue(seat, out m) && (m.Beer != start.Beer || m.Stopping || m.Confirmed))
            {
                // An earlier drink of this seat is still settling here: finish it at once.
                FinishNow(m);
                m = null;
            }

            BeerMugVisual mug = FindMug(start.Beer);
            if (mug == null) return;

            if (m == null)
            {
                if (!mug.IsHome) mug.SettleHome(mug.transform.localScale);
                m = Create(seat, mug, isLocalSeat);
                if (m == null) return;
            }

            m.Confirmed = true;
            m.ConfirmedAt = Time.unscaledTime;
            m.StartFill = start.StartFill;
            m.Rate = start.RatePerSecond;
            m.LeadIn = start.LeadInSeconds;
            mug.Fill = start.StartFill;
        }

        /// <summary>The authority refused this machine's drink: bring the hand back.</summary>
        public void Refuse(BeerId beer)
        {
            Motion m;
            if (_motions.TryGetValue(beer.Seat, out m) && m.Beer == beer && !m.Confirmed) m.Stopping = true;
        }

        /// <summary>The local drinker let go of the key: stop draining here now and go back.</summary>
        public void ReleaseLocal(int seat)
        {
            Motion m;
            if (!_motions.TryGetValue(seat, out m) || m.Stopping) return;

            m.Stopping = true;
        }

        /// <summary>The authority's final word: reconcile the fill and make sure the mug goes back.</summary>
        public void Stop(BeerDrinkStop stop)
        {
            Motion m;
            if (_motions.TryGetValue(stop.Beer.Seat, out m) && m.Beer == stop.Beer)
            {
                m.Stopping = true;
                m.HasFinal = true;
                m.FinalFill = stop.FinalFill;
                return;
            }

            BeerMugVisual mug = FindMug(stop.Beer);
            if (mug != null) mug.Fill = stop.FinalFill;
        }

        /// <summary>
        /// The drinker died or left: stop draining now and put the mug back with the usual
        /// quick return (nothing else needs the hand). Tells the authority.
        /// </summary>
        public void Interrupt(int seat)
        {
            Motion m;
            if (!_motions.TryGetValue(seat, out m) || m.Stopping) return;

            m.Stopping = true;
            RaiseInterrupted(m);
        }

        void RaiseInterrupted(Motion m)
        {
            Action<int, BeerId> interrupted = Interrupted;
            if (interrupted != null) interrupted(m.Seat, m.Beer);
        }

        /// <summary>Drops every drink immediately: match end, rematch, teardown.</summary>
        public void ClearAll()
        {
            _seats.Clear();
            _seats.AddRange(_motions.Keys);
            for (int i = 0; i < _seats.Count; i++)
            {
                Motion m;
                if (_motions.TryGetValue(_seats[i], out m)) FinishNow(m);
            }

            _motions.Clear();
        }

        void OnLeftHandClaimed(int seat)
        {
            Motion m;
            if (!_motions.TryGetValue(seat, out m)) return;

            // Hand the gameplay presenter the true rest pose before it reads its own.
            if (_actions != null && m.Hand != null) _actions.SeedRestPose(m.Hand, m.HandRest);

            if (m.Phase == Phase.Home)
            {
                // The mug is already home; gameplay simply takes the hand from where it is.
                RestoreHead(m);
                Remove(m);
                return;
            }

            if (m.Phase == Phase.Emergency) return;

            // Gameplay wins: stop draining, give the hand up this instant, mug glides home.
            bool wasDrinking = !m.Stopping;
            LetGo(m);
            if (wasDrinking) RaiseInterrupted(m);
        }

        // ------------------------------------------------------------------ motion lifecycle

        Motion Create(int seat, BeerMugVisual mug, bool isLocal)
        {
            PlayerCharacter player = _table != null ? _table.GetPlayer(seat) : null;
            Transform hand = player != null ? player.LeftHandAnchor : null;
            if (player == null || hand == null || mug.LeftHandGripAnchor == null) return null;

            Pose rest;
            if (!_handRest.TryGetValue(hand, out rest))
            {
                rest = new Pose(hand.localPosition, hand.localRotation);
                _handRest[hand] = rest;
            }

            if (_actions != null) _actions.SeedRestPose(hand, rest);

            Motion m = new Motion
            {
                Seat = seat,
                Beer = mug.Beer,
                Mug = mug,
                Player = player,
                Hand = hand,
                HandRest = rest,
                Head = player.HeadVisual,
                HeadRest = player.HeadVisual != null ? player.HeadVisual.localRotation : Quaternion.identity,
                Mouth = MouthAnchor(player),
                IsLocal = isLocal,
                MugScale = mug.transform.localScale,
                CreatedAt = Time.unscaledTime
            };

            Enter(m, Phase.Reach);
            _motions[seat] = m;
            return m;
        }

        Transform MouthAnchor(PlayerCharacter player)
        {
            Transform head = player.HeadVisual;
            if (head == null) return null;

            Transform mouth = head.Find(MouthAnchorName);
            if (mouth != null) return mouth;

            mouth = new GameObject(MouthAnchorName).transform;
            mouth.SetParent(head, false);
            mouth.localPosition = fallbackMouthLocal;
            return mouth;
        }

        void Enter(Motion m, Phase phase)
        {
            m.Phase = phase;
            m.PhaseStart = Time.unscaledTime;
            m.HeadWeightFrom = m.HeadWeight;

            if (m.Hand != null)
            {
                m.FromPosition = phase == Phase.Return || phase == Phase.Emergency
                    ? m.Mug.transform.position
                    : m.Hand.position;
                m.FromRotation = phase == Phase.Return || phase == Phase.Emergency
                    ? m.Mug.transform.rotation
                    : m.Hand.rotation;
            }

            if (phase == Phase.Home && m.Hand != null)
            {
                m.FromPosition = m.Hand.localPosition;
                m.FromRotation = m.Hand.localRotation;
            }
        }

        /// <summary>Releases the hand to gameplay now; the mug glides home on its own.</summary>
        void LetGo(Motion m)
        {
            m.Stopping = true;
            SetLookLocked(m, false);

            if (m.Mug == null) { Remove(m); return; }

            if (!m.Attached && m.Mug.IsHome)
            {
                // Never picked up: nothing to put back.
                RestoreHead(m);
                Remove(m);
                return;
            }

            // Keep the mug exactly where it is in the world, now under its own slot.
            if (m.Mug.HomeSlot != null) m.Mug.transform.SetParent(m.Mug.HomeSlot, true);
            m.Attached = false;
            Enter(m, Phase.Emergency);
        }

        /// <summary>Ends a motion this frame: mug home, hand at rest, head level, look free.</summary>
        void FinishNow(Motion m)
        {
            if (m.Mug != null && m.Mug.HomeSlot != null && !m.Mug.IsHome) m.Mug.SettleHome(m.MugScale);
            if (m.Mug != null && m.HasFinal) m.Mug.Fill = m.FinalFill;

            if (m.Hand != null && m.Phase != Phase.Emergency)
            {
                m.Hand.localPosition = m.HandRest.position;
                m.Hand.localRotation = m.HandRest.rotation;
            }

            RestoreHead(m);
            Remove(m);
        }

        void Remove(Motion m)
        {
            SetLookLocked(m, false);

            Motion current;
            if (_motions.TryGetValue(m.Seat, out current) && current == m) _motions.Remove(m.Seat);

            Action<int> finished = Finished;
            if (finished != null) finished(m.Seat);
        }

        void RestoreHead(Motion m)
        {
            m.HeadWeight = 0f;
            if (m.Head != null) m.Head.localRotation = m.HeadRest;
        }

        void SetLookLocked(Motion m, bool locked)
        {
            if (!m.IsLocal || m.LookLocked == locked) return;

            SeatedHeadLook look = _localLook != null ? _localLook() : null;
            if (look != null) look.SetPitchLocked(locked);
            m.LookLocked = locked;
        }

        // ------------------------------------------------------------------ per frame

        void LateUpdate()
        {
            if (_motions.Count == 0) return;

            _seats.Clear();
            _seats.AddRange(_motions.Keys);

            for (int i = 0; i < _seats.Count; i++)
            {
                Motion m;
                if (!_motions.TryGetValue(_seats[i], out m)) continue;

                if (m.Mug == null || m.Player == null || m.Hand == null || m.Mug.HomeSlot == null)
                {
                    // The table or the mug went away under the drink.
                    if (m.Head != null) m.Head.localRotation = m.HeadRest;
                    if (m.Hand != null && m.Phase != Phase.Emergency)
                    {
                        m.Hand.localPosition = m.HandRest.position;
                        m.Hand.localRotation = m.HandRest.rotation;
                    }

                    Remove(m);
                    continue;
                }

                UpdateFill(m);
                Step(m);
            }
        }

        void UpdateFill(Motion m)
        {
            if (m.HasFinal)
            {
                m.Mug.Fill = Mathf.MoveTowards(m.Mug.Fill, m.FinalFill, reconcileSpeed * Time.unscaledDeltaTime);
                return;
            }

            // Drains only while the drink lasts; a stopping mug keeps what it showed.
            if (!m.Confirmed || m.Stopping) return;

            m.Mug.Fill = BeerLedger.Deplete(m.StartFill, m.Rate, Time.unscaledTime - m.ConfirmedAt - m.LeadIn);
        }

        void Step(Motion m)
        {
            float elapsed = Time.unscaledTime - m.PhaseStart;

            switch (m.Phase)
            {
                case Phase.Reach:
                {
                    float t = Clamp01(elapsed / reachDuration);
                    Transform grip = m.Mug.LeftHandGripAnchor;
                    float e = EaseOutCubic(t);
                    m.Hand.SetPositionAndRotation(Vector3.LerpUnclamped(m.FromPosition, grip.position, e),
                        Quaternion.Slerp(m.FromRotation, grip.rotation, e));

                    if (m.Stopping) { Enter(m, Phase.Home); break; }
                    if (t >= 1f) Enter(m, Phase.Settle);
                    break;
                }

                case Phase.Settle:
                {
                    float t = Clamp01(elapsed / gripSettleDuration);
                    Transform grip = m.Mug.LeftHandGripAnchor;

                    // A small squeeze toward the mug's axis and back: the hand closes on it.
                    Vector3 axis = m.Mug.transform.TransformPoint(m.Mug.RimCenterLocal);
                    axis.y = grip.position.y;
                    Vector3 inward = (axis - grip.position).normalized;
                    m.Hand.SetPositionAndRotation(grip.position + inward * (gripSqueeze * Mathf.Sin(t * Mathf.PI)), grip.rotation);

                    if (m.Stopping) { Enter(m, Phase.Home); break; }
                    if (t < 1f) break;

                    Attach(m);
                    Enter(m, Phase.Bring);
                    SetLookLocked(m, true);
                    break;
                }

                case Phase.Bring:
                {
                    float t = Clamp01(elapsed / bringDuration);
                    float e = EaseInOutCubic(t);
                    m.HeadWeight = e;
                    ApplyHead(m);

                    Vector3 mouthPosition;
                    Quaternion mouthRotation;
                    MouthPose(m, out mouthPosition, out mouthRotation);

                    // Up first, then in to the mouth; the tilt comes in over the second half.
                    Vector3 control = m.Mug.HomeSlot.position + Vector3.up * ((mouthPosition.y - m.Mug.HomeSlot.position.y) * 0.85f);
                    Vector3 position = Bezier(m.Mug.HomeSlot.position, control, mouthPosition, e);
                    Quaternion rotation = Quaternion.Slerp(m.Mug.HomeSlot.rotation, mouthRotation, Smooth(Mathf.InverseLerp(0.25f, 1f, t)));
                    PlaceMug(m, position, rotation);

                    if (m.Stopping) { Enter(m, Phase.Return); break; }
                    if (t >= 1f) Enter(m, Phase.Drink);
                    break;
                }

                case Phase.Drink:
                {
                    m.HeadWeight = 1f;
                    ApplyHead(m);

                    Vector3 mouthPosition;
                    Quaternion mouthRotation;
                    MouthPose(m, out mouthPosition, out mouthRotation);
                    PlaceMug(m, mouthPosition, mouthRotation);

                    if (!m.Confirmed && Time.unscaledTime - m.CreatedAt > confirmTimeout) m.Stopping = true;

                    // Empty: hold the last sip for a moment, then put the mug down.
                    if (m.Mug.Fill <= 0f && m.EmptySince < 0f) m.EmptySince = Time.unscaledTime;
                    if (m.EmptySince >= 0f && Time.unscaledTime - m.EmptySince >= emptyFinishDuration) m.Stopping = true;

                    if (m.Stopping) Enter(m, Phase.Return);
                    break;
                }

                case Phase.Return:
                {
                    float t = Clamp01(elapsed / returnDuration);
                    float e = EaseInOutCubic(t);
                    m.HeadWeight = Mathf.Lerp(m.HeadWeightFrom, 0f, e);
                    ApplyHead(m);

                    Transform slot = m.Mug.HomeSlot;
                    Vector3 control = slot.position + Vector3.up * ((m.FromPosition.y - slot.position.y) * 0.85f);
                    Vector3 position = Bezier(m.FromPosition, control, slot.position, e);
                    Quaternion rotation = Quaternion.Slerp(m.FromRotation, slot.rotation, Smooth(Mathf.InverseLerp(0f, 0.7f, t)));
                    PlaceMug(m, position, rotation);

                    if (t < 1f) break;

                    // The very same mug, back in its own slot.
                    m.Mug.SettleHome(m.MugScale);
                    m.Attached = false;
                    RestoreHead(m);
                    SetLookLocked(m, false);
                    Enter(m, Phase.Home);
                    break;
                }

                case Phase.Home:
                {
                    float t = Clamp01(elapsed / handHomeDuration);
                    float e = EaseInOutCubic(t);
                    m.Hand.localPosition = Vector3.LerpUnclamped(m.FromPosition, m.HandRest.position, e);
                    m.Hand.localRotation = Quaternion.Slerp(m.FromRotation, m.HandRest.rotation, e);

                    if (m.HeadWeight > 0f)
                    {
                        m.HeadWeight = Mathf.Lerp(m.HeadWeightFrom, 0f, e);
                        ApplyHead(m);
                    }

                    if (t < 1f) break;

                    m.Hand.localPosition = m.HandRest.position;
                    m.Hand.localRotation = m.HandRest.rotation;
                    RestoreHead(m);
                    Remove(m);
                    break;
                }

                case Phase.Emergency:
                {
                    // The hand already belongs to gameplay; only the mug and head move here.
                    float t = Clamp01(elapsed / emergencyReturnDuration);
                    float e = EaseInOutCubic(t);
                    m.HeadWeight = Mathf.Lerp(m.HeadWeightFrom, 0f, e);
                    ApplyHead(m);

                    Transform slot = m.Mug.HomeSlot;
                    if (slot != null)
                    {
                        Vector3 position = Vector3.LerpUnclamped(m.FromPosition, slot.position, e) +
                            Vector3.up * (0.04f * Mathf.Sin(t * Mathf.PI));
                        m.Mug.transform.SetPositionAndRotation(position, Quaternion.Slerp(m.FromRotation, slot.rotation, e));
                    }

                    if (t < 1f) break;

                    m.Mug.SettleHome(m.MugScale);
                    if (m.HasFinal) m.Mug.Fill = m.FinalFill;
                    RestoreHead(m);
                    Remove(m);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ poses

        /// <summary>The same mug object moves into the hand at the exact grip offset.</summary>
        void Attach(Motion m)
        {
            Transform mug = m.Mug.transform;
            Transform grip = m.Mug.LeftHandGripAnchor;

            Quaternion inverse = Quaternion.Inverse(grip.localRotation);
            mug.SetParent(m.Hand, false);
            mug.localScale = m.MugScale;
            mug.localRotation = inverse;
            mug.localPosition = -(inverse * Vector3.Scale(m.MugScale, grip.localPosition));
            m.Attached = true;
        }

        /// <summary>Puts the mug at a world pose by moving the hand that holds it.</summary>
        void PlaceMug(Motion m, Vector3 mugPosition, Quaternion mugRotation)
        {
            if (!m.Attached)
            {
                m.Mug.transform.SetPositionAndRotation(mugPosition, mugRotation);
                return;
            }

            Transform grip = m.Mug.LeftHandGripAnchor;
            m.Hand.SetPositionAndRotation(mugPosition + mugRotation * Vector3.Scale(m.MugScale, grip.localPosition),
                mugRotation * grip.localRotation);
        }

        /// <summary>How far from the full tilt toward the empty tilt, from the remaining beer.</summary>
        float TiltAmount(float fill)
        {
            float emptiness = 1f - Clamp01(fill);
            return Clamp01(tiltByEmptiness != null && tiltByEmptiness.length > 0 ? tiltByEmptiness.Evaluate(emptiness) : emptiness);
        }

        /// <summary>
        /// Where the mug goes to drink: tilted toward the face around the drinker's own
        /// left-right axis, handle to their left, with the rim's near edge at the mouth.
        /// </summary>
        void MouthPose(Motion m, out Vector3 position, out Quaternion rotation)
        {
            Transform mouth = m.Mouth != null ? m.Mouth : m.Player.HeadPivot;
            Vector3 face = Vector3.ProjectOnPlane(m.Head != null ? m.Head.forward : m.Player.transform.forward, Vector3.up);
            if (face.sqrMagnitude < 0.0001f) face = m.Player.transform.forward;
            face.Normalize();

            float tilt = Mathf.Lerp(mugTiltFull, mugTiltEmpty, TiltAmount(m.Mug.Fill));
            rotation = Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(-tilt, 0f, 0f);

            Vector3 rimNear = m.Mug.RimCenterLocal + Vector3.back * m.Mug.RimOuterRadius;
            position = mouth.position - rotation * Vector3.Scale(m.MugScale, rimNear) - face * lipOverlap;
        }

        /// <summary>
        /// Visual head only: blends the head from wherever look points it to a small upward
        /// pitch that grows as the beer runs out. The camera anchor is a sibling, untouched.
        /// </summary>
        void ApplyHead(Motion m)
        {
            if (m.Head == null) return;

            Transform pivot = m.Player.HeadPivot;
            float lookPitch = pivot != null ? Mathf.DeltaAngle(0f, pivot.localEulerAngles.x) : 0f;
            float up = Mathf.Lerp(headTiltFull, headTiltEmpty, TiltAmount(m.Mug.Fill));

            float delta = m.HeadWeight * (-up - lookPitch);
            m.Head.localRotation = Quaternion.Euler(delta, 0f, 0f) * m.HeadRest;
        }

        // ------------------------------------------------------------------ maths

        static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        static float Clamp01(float t) { return t < 0f ? 0f : t > 1f ? 1f : t; }

        static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float EaseOutCubic(float t)
        {
            t = 1f - Clamp01(t);
            return 1f - t * t * t;
        }

        static float EaseInOutCubic(float t)
        {
            t = Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Diagnostics: <see cref="Describe"/> as a property, for editor tooling.</summary>
        public string Summary { get { return Describe(); } }

        /// <summary>Diagnostics: seat, beer, phase and fill of every drink in progress.</summary>
        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Motion m in _motions.Values)
                sb.AppendFormat("seat {0}: {1} {2} fill {3:0.00}{4}{5}\n", m.Seat, m.Beer, m.Phase,
                    m.Mug != null ? m.Mug.Fill : -1f, m.Confirmed ? " confirmed" : "", m.Stopping ? " stopping" : "");

            return sb.ToString();
        }
    }
}
