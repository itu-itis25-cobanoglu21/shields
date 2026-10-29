using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
using Shields.Players;
using Shields.Presentation;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// The presentation shared by every targeted action: Attack, Replace Shield and the
    /// second Charge (Charged Attack). The first Charge keeps its own right-hand sequence.
    ///
    ///   Anticipation - the body leans toward the table centre around its fixed base and
    ///                  the LEFT hand rests flat on the Draw Pile while a target is chosen.
    ///                  An Attack draws the weapon with the right hand at the same time.
    ///   Throw        - once the result is known, the left hand lifts a face-down card and
    ///                  sends it to the target's <see cref="PlayerTableArea.ActionRevealAnchor"/>.
    ///   Reveal       - only on landing does the card show its face.
    ///   Read         - a pause, so everyone can read the card.
    ///   Resolution   - the held weapon fires (Attack, Charged Attack), or the card slides
    ///                  into the target's Shield slot (Replace Shield).
    ///   Return       - lean and hand back to rest; an Attack card goes to the discard pile.
    ///
    /// Presentation only. The card that flies is a temporary view with no collider and no
    /// identity until it lands, and the face it then shows comes from the public action
    /// result. The authoritative cards stay with the public state; the caller is told
    /// when to reveal and when to let the result's state show.
    /// </summary>
    public partial class PlayerActionPresenter
    {
        [Header("Targeted Action: Attack / Replace Shield / Charged Attack")]
        [Tooltip("Left hand from rest onto the Draw Pile. The body leans in over the same time.")]
        [SerializeField] float leftHandReachDuration = 0.25f;

        [Tooltip("Forward lean toward the table centre while the action waits for its target, in degrees. The base stays put.")]
        [Range(0f, 10f)]
        [SerializeField] float targetingLeanAngle = 4f;

        [Tooltip("Shortest rest of the hand on the Draw Pile before the card is drawn, so a bot's or a remote player's action still reads.")]
        [SerializeField] float minimumDeckHold = 0.3f;

        [Tooltip("Drawn card from the Draw Pile to the target's ActionRevealAnchor.")]
        [SerializeField] float cardThrowDuration = 0.3f;

        [Tooltip("Pause on the revealed card before the result plays out (the shot, or the Shield handoff).")]
        [SerializeField] float resultReadDelay = 0.85f;

        [Tooltip("Left hand back to rest. The lean eases out over the same time.")]
        [SerializeField] float leftHandReturnDuration = 0.25f;

        [Tooltip("Revealed card from the ActionRevealAnchor into the target's Shield slot.")]
        [SerializeField] float shieldHandoffDuration = 0.25f;

        [Tooltip("How long a revealed Attack card stays after the shot before it goes to the discard pile.")]
        [SerializeField] float landedCardLinger = 0.6f;

        [SerializeField] float discardSlideDuration = 0.3f;

        [Tooltip("Fraction of the throw the hand carries the card before letting it fly on.")]
        [Range(0.1f, 1f)]
        [SerializeField] float throwHandFollow = 0.4f;

        [Tooltip("Peak height of the drawn card's arc.")]
        [SerializeField] float throwArcHeight = 0.12f;

        [Tooltip("A clicked target the host has not confirmed is dropped after this long.")]
        [SerializeField] float unconfirmedThrowTimeout = 3f;

        [Tooltip("Direction the left hand's fingers point in its resting local space (the right hand's, mirrored).")]
        [SerializeField] Vector3 leftHandFingerAxis = new Vector3(-0.365f, -0.930f, -0.039f);

        [Tooltip("Direction the back of the left hand faces in its resting local space. Turned straight up on the deck.")]
        [SerializeField] Vector3 leftHandBackAxis = new Vector3(-0.491f, 0.156f, 0.857f);

        sealed class ThrowMotion
        {
            public int Seat;
            public PlayerCharacter Player;
            public Transform Hand;
            public Transform Palm;
            public Pose Rest;
            public Quaternion FlatWorld;
            public float StartedAt;
            public Coroutine Routine;
            public Coroutine HandRoutine;

            // Anticipation: the local click before the host answers, or a cancel.
            public bool TargetChosen;
            public float TargetChosenAt;
            public bool Cancelled;

            // From the confirmed public result.
            public bool Confirmed;
            public int Target = -1;
            public PlayingCard Drawn;
            public bool BecomesShield;
            public bool Shot;
            public int Damage;
            public int Seed;
            public WeaponRig Rig;
            public Action OnReveal;
            public Action OnResolve;

            public CardView Card;
        }

        readonly Dictionary<int, ThrowMotion> _throws = new Dictionary<int, ThrowMotion>();

        /// <summary>
        /// Raised with the seat just before a targeted action takes that seat's left hand,
        /// before its rest pose is read. Cosmetic users of the hand (beer drinking) must give
        /// it up at once: gameplay presentation always wins.
        /// </summary>
        public event Action<int> LeftHandClaimed;

        /// <summary>True while a targeted action (anticipation, throw or return) owns the seat's left hand.</summary>
        public bool IsLeftHandBusy(int seat)
        {
            return _throws.ContainsKey(seat);
        }

        // ------------------------------------------------------------------ events

        /// <summary>
        /// The local player chose Attack, Replace Shield or Charged Attack and is now picking
        /// a target: lean in and rest the left hand on the Draw Pile. Idempotent, so
        /// switching between targeted actions keeps the pose.
        /// </summary>
        public void BeginTargeting(int seat)
        {
            ThrowMotion motion;
            if (_throws.TryGetValue(seat, out motion))
            {
                if (motion.Confirmed || !motion.Cancelled) return;

                // On its way home after a cancel: start over from where the hand is.
                StopThrow(motion, false);
            }

            StartThrow(seat);
        }

        /// <summary>
        /// Targeting ended without a chosen target (cancelled, switched to a first Charge):
        /// back to idle. A clicked or confirmed target finishes on its own.
        /// </summary>
        public void EndTargeting(int seat)
        {
            ThrowMotion motion;
            if (_throws.TryGetValue(seat, out motion) && !motion.Confirmed && !motion.TargetChosen) motion.Cancelled = true;
        }

        /// <summary>
        /// Cosmetic: the local player clicked a target and the host has not answered. The
        /// hand keeps waiting on the deck; a refusal or a timeout brings it back.
        /// </summary>
        public void ChooseThrowTarget(int seat)
        {
            ThrowMotion motion;
            if (!_throws.TryGetValue(seat, out motion) || motion.Confirmed || motion.Cancelled) return;

            motion.TargetChosen = true;
            motion.TargetChosenAt = Time.time;
        }

        void CancelThrowAnticipation(int seat)
        {
            ThrowMotion motion;
            if (_throws.TryGetValue(seat, out motion) && !motion.Confirmed) motion.Cancelled = true;
        }

        /// <summary>
        /// A targeted action resolved, from the public result. Plays the draw, the hidden
        /// throw and the landing reveal, then the result itself after the read delay.
        ///
        /// <paramref name="onReveal"/> runs the instant the card lands face up;
        /// <paramref name="onResolve"/> runs when the result plays out (the shot fires, or
        /// the card reaches the Shield slot). Both always run exactly once, unless the whole
        /// presentation is cleared.
        /// </summary>
        public void PlayTargetedAction(int seat, int target, PlayingCard drawn, bool becomesShield, bool shot,
            int damage, int seed, Action onReveal, Action onResolve)
        {
            ThrowMotion motion;
            if (_throws.TryGetValue(seat, out motion) && (motion.Confirmed || motion.Cancelled))
            {
                // An earlier action of this seat is still playing, or the hand is on its way
                // home from a cancel: finish that at once and start this one cleanly.
                StopThrow(motion, true);
                motion = null;
            }

            if (motion == null) motion = StartThrow(seat);

            if (motion == null)
            {
                // Nothing to animate with: everything shows at once.
                if (onReveal != null) onReveal();
                if (shot) PlayAttack(seat, target, damage, seed);
                if (onResolve != null) onResolve();
                return;
            }

            motion.Confirmed = true;
            motion.Target = target;
            motion.Drawn = drawn;
            motion.BecomesShield = becomesShield;
            motion.Shot = shot;
            motion.Damage = Mathf.Max(0, damage);
            motion.Seed = seed;
            motion.OnReveal = onReveal;
            motion.OnResolve = onResolve;

            if (!shot) return;

            // The weapon locks on now and stays there; it fires only once the card is read.
            motion.Rig = Lock(seat, target, true);
            if (motion.Rig == null) return;

            motion.Rig.Damage = motion.Damage;
            motion.Rig.Seed = seed;
            motion.Rig.HoldFire = true;
        }

        // ------------------------------------------------------------------ sequence

        ThrowMotion StartThrow(int seat)
        {
            PlayerCharacter player = _table != null ? _table.GetPlayer(seat) : null;
            Transform hand = player != null ? player.LeftHandAnchor : null;
            if (hand == null || _deck == null || _cardPrefab == null) return null;

            // A drink in progress lets go of the hand (and re-seeds its true rest) first.
            Action<int> claimed = LeftHandClaimed;
            if (claimed != null) claimed(seat);

            Pose rest = RestPoseOf(hand);

            ThrowMotion motion = new ThrowMotion
            {
                Seat = seat,
                Player = player,
                Hand = hand,
                Palm = hand.Find("PalmContact"),
                Rest = rest,
                FlatWorld = FlatHandWorldRotation(player, leftHandFingerAxis, leftHandBackAxis),
                StartedAt = Time.time
            };

            _throws[seat] = motion;
            motion.Routine = StartCoroutine(ThrowRoutine(motion));
            return motion;
        }

        IEnumerator ThrowRoutine(ThrowMotion m)
        {
            // Anticipation: lean in while the hand reaches the deck, then rest on it.
            SetLean(m.Player, TowardTableCentre(m.Player), targetingLeanAngle, leftHandReachDuration);

            Vector3 from = m.Hand.localPosition;
            Quaternion fromRotation = m.Hand.localRotation;
            for (float elapsed = 0f; elapsed < leftHandReachDuration; elapsed += Time.deltaTime)
            {
                if (m.Hand == null) { Abandon(m); yield break; }

                float e = EaseOutCubic(elapsed / leftHandReachDuration);
                Quaternion flat = FlatLocal(m);
                m.Hand.localRotation = Quaternion.Slerp(fromRotation, flat, e);
                m.Hand.localPosition = Vector3.LerpUnclamped(from, DeckLocal(m, flat), e);
                yield return null;
            }

            while (true)
            {
                if (m.Hand == null) { Abandon(m); yield break; }

                Quaternion flat = FlatLocal(m);
                m.Hand.localRotation = flat;
                m.Hand.localPosition = DeckLocal(m, flat);

                if (m.Cancelled) break;
                if (m.Confirmed && Time.time - m.StartedAt >= leftHandReachDuration + minimumDeckHold) break;

                if (!m.Confirmed && m.TargetChosen && Time.time - m.TargetChosenAt >= unconfirmedThrowTimeout)
                {
                    m.Cancelled = true;
                    break;
                }

                yield return null;
            }

            if (!m.Confirmed)
            {
                SetLean(m.Player, Vector3.zero, 0f, leftHandReturnDuration);
                yield return ReturnLeftHand(m);
                EndThrow(m);
                yield break;
            }

            // Throw: hidden while it travels, face up only once it lands.
            yield return ThrowCard(m);
            RevealCard(m);

            for (float waited = 0f; waited < resultReadDelay; waited += Time.deltaTime) yield return null;

            if (m.BecomesShield)
            {
                yield return HandOffToShield(m);
                Resolve(m);

                // The real Shield view, drawn from public state, takes over in this frame.
                DestroyCard(m);
                SetLean(m.Player, Vector3.zero, 0f, leftHandReturnDuration);
            }
            else
            {
                if (m.Shot) yield return FireHeldShot(m);
                Resolve(m);

                for (float waited = 0f; waited < landedCardLinger; waited += Time.deltaTime) yield return null;

                SetLean(m.Player, Vector3.zero, 0f, leftHandReturnDuration);
                yield return SlideToDiscard(m);
            }

            EndThrow(m);
        }

        IEnumerator ThrowCard(ThrowMotion m)
        {
            PlayerTableArea area = _table != null ? _table.GetAreaForPlayer(m.Target) : null;
            Transform anchor = area != null ? area.ActionRevealAnchor : null;

            m.Card = Instantiate(_cardPrefab);
            m.Card.ShowFaceDown(-1);
            m.Card.name = "ActionCard_Hidden";

            // Presentation only: never a crosshair target and never in anyone's way.
            foreach (Collider collider in m.Card.GetComponentsInChildren<Collider>()) collider.enabled = false;

            Vector3 start = _deck.TopCenter;
            Quaternion startRotation = _deck.transform.rotation;
            m.Card.transform.SetPositionAndRotation(start, startRotation);

            if (anchor == null)
            {
                m.HandRoutine = StartCoroutine(ReturnLeftHand(m));
                yield break;
            }

            bool released = false;
            for (float elapsed = 0f; elapsed < cardThrowDuration; elapsed += Time.deltaTime)
            {
                float t = elapsed / cardThrowDuration;
                float e = Smooth(t);

                if (m.Card != null)
                    m.Card.transform.SetPositionAndRotation(ThrowPath(start, anchor.position, e),
                        Quaternion.Slerp(startRotation, anchor.rotation, e));

                if (!released && m.Hand != null)
                {
                    if (t < throwHandFollow)
                    {
                        // The flat hand lifts and carries the card, a touch behind it.
                        Vector3 carried = ThrowPath(start, anchor.position, Smooth(Mathf.Clamp01(t - handTrail)));
                        Quaternion flat = FlatLocal(m);
                        m.Hand.localRotation = flat;
                        m.Hand.localPosition = HandLocalForPalm(m.Hand, m.Palm, carried + Vector3.up * palmClearance, flat);
                    }
                    else
                    {
                        // Let go: the card flies on, the hand heads home.
                        released = true;
                        m.HandRoutine = StartCoroutine(ReturnLeftHand(m));
                    }
                }

                yield return null;
            }

            if (!released) m.HandRoutine = StartCoroutine(ReturnLeftHand(m));

            if (m.Card != null) m.Card.PlaceAt(anchor, false);
        }

        /// <summary>The public reveal point: the landed card shows its face to everyone.</summary>
        void RevealCard(ThrowMotion m)
        {
            if (m.Card != null && m.Drawn.IsValid) m.Card.Bind(m.Drawn, -1);

            Action reveal = m.OnReveal;
            m.OnReveal = null;
            if (reveal != null) reveal();
        }

        void Resolve(ThrowMotion m)
        {
            Action resolve = m.OnResolve;
            m.OnResolve = null;
            if (resolve != null) resolve();
        }

        /// <summary>Lets the locked weapon fire, and waits for the shot (the existing fire and hit).</summary>
        IEnumerator FireHeldShot(ThrowMotion m)
        {
            WeaponRig rig = m.Rig;
            if (rig == null)
            {
                // No weapon to fire with: the hit still shows.
                if (m.Damage > 0) PlayHit(m.Target, m.Player.transform.position, m.Damage, m.Seed);
                yield break;
            }

            rig.HoldFire = false;

            // A confirmed shot always goes off; this only guards against a weapon that vanished.
            for (float waited = 0f; WeaponLive(rig) && !rig.Fired && waited < unconfirmedLockTimeout + 1f; waited += Time.deltaTime)
                yield return null;
        }

        bool WeaponLive(WeaponRig rig)
        {
            WeaponRig current;
            return rig != null && _weapons.TryGetValue(rig.Seat, out current) && current == rig;
        }

        /// <summary>The revealed card slides from the reveal spot into the target's Shield slot.</summary>
        IEnumerator HandOffToShield(ThrowMotion m)
        {
            PlayerTableArea area = _table != null ? _table.GetAreaForPlayer(m.Target) : null;
            Transform slot = area != null ? area.HorizontalCardSlot : null;
            if (slot == null || m.Card == null) yield break;

            Transform card = m.Card.transform;
            Vector3 from = card.position;
            Quaternion fromRotation = card.rotation;

            // Lands turned sideways, exactly as the Shield lies, a hair above the old one.
            Vector3 to = slot.position + Vector3.up * CardFaceTop;
            Quaternion toRotation = slot.rotation * Quaternion.Euler(0f, 90f, 0f);

            for (float elapsed = 0f; elapsed < shieldHandoffDuration; elapsed += Time.deltaTime)
            {
                if (m.Card == null) yield break;

                float e = EaseInOutCubic(elapsed / shieldHandoffDuration);
                card.SetPositionAndRotation(Vector3.LerpUnclamped(from, to, e) + Vector3.up * (cardArcHeight * 0.5f * Mathf.Sin(e * Mathf.PI)),
                    Quaternion.Slerp(fromRotation, toRotation, e));
                yield return null;
            }

            if (m.Card != null) card.SetPositionAndRotation(to, toRotation);
        }

        /// <summary>A spent action card goes onto the discard pile, which public state already counts.</summary>
        IEnumerator SlideToDiscard(ThrowMotion m)
        {
            if (m.Card == null) yield break;

            Transform card = m.Card.transform;
            card.SetParent(null, true);

            Vector3 from = card.position;
            Quaternion fromRotation = card.rotation;
            Vector3 to = (_discard != null ? _discard.transform.position : _deck.TopCenter) + Vector3.up * 0.02f;
            Quaternion toRotation = _discard != null ? _discard.transform.rotation : fromRotation;

            for (float elapsed = 0f; elapsed < discardSlideDuration; elapsed += Time.deltaTime)
            {
                if (m.Card == null) yield break;

                float e = EaseInOutCubic(elapsed / discardSlideDuration);
                card.SetPositionAndRotation(Vector3.LerpUnclamped(from, to, e) + Vector3.up * (cardArcHeight * Mathf.Sin(e * Mathf.PI)),
                    Quaternion.Slerp(fromRotation, toRotation, e));
                yield return null;
            }

            DestroyCard(m);
        }

        IEnumerator ReturnLeftHand(ThrowMotion m)
        {
            if (m.Hand == null) yield break;

            Vector3 from = m.Hand.localPosition;
            Quaternion fromRotation = m.Hand.localRotation;
            for (float elapsed = 0f; elapsed < leftHandReturnDuration; elapsed += Time.deltaTime)
            {
                if (m.Hand == null) yield break;

                float e = EaseInOutCubic(elapsed / leftHandReturnDuration);
                m.Hand.localPosition = Vector3.LerpUnclamped(from, m.Rest.position, e);
                m.Hand.localRotation = Quaternion.Slerp(fromRotation, m.Rest.rotation, e);
                yield return null;
            }

            RestoreLeftHand(m);
        }

        // ------------------------------------------------------------------ teardown

        /// <summary>
        /// Stops a sequence at once. Finishing still reveals and resolves it (an earlier
        /// action of the same seat overtaken by a new one); otherwise the callbacks are
        /// dropped (the whole table is being cleared).
        /// </summary>
        void StopThrow(ThrowMotion m, bool finish)
        {
            if (m.Routine != null) StopCoroutine(m.Routine);
            if (m.HandRoutine != null) StopCoroutine(m.HandRoutine);

            RestoreLeftHand(m);
            DestroyCard(m);

            if (finish && m.Confirmed)
            {
                if (m.OnReveal != null) RevealCard(m);
                if (WeaponLive(m.Rig)) m.Rig.HoldFire = false;
                Resolve(m);
            }

            m.OnReveal = null;
            m.OnResolve = null;

            if (finish) SetLean(m.Player, Vector3.zero, 0f, leftHandReturnDuration);

            ThrowMotion current;
            if (_throws.TryGetValue(m.Seat, out current) && current == m) _throws.Remove(m.Seat);
        }

        /// <summary>
        /// The hand vanished under a running sequence (its player was removed). A confirmed
        /// result is still revealed and resolved, so the caller is never left waiting.
        /// </summary>
        void Abandon(ThrowMotion m)
        {
            m.Routine = null;
            StopThrow(m, true);
        }

        void EndThrow(ThrowMotion m)
        {
            if (m.HandRoutine != null) StopCoroutine(m.HandRoutine);

            RestoreLeftHand(m);
            DestroyCard(m);

            ThrowMotion current;
            if (_throws.TryGetValue(m.Seat, out current) && current == m) _throws.Remove(m.Seat);
        }

        void ClearThrows()
        {
            foreach (ThrowMotion motion in new List<ThrowMotion>(_throws.Values)) StopThrow(motion, false);
            _throws.Clear();
        }

        static void RestoreLeftHand(ThrowMotion m)
        {
            if (m.Hand == null) return;

            m.Hand.localPosition = m.Rest.position;
            m.Hand.localRotation = m.Rest.rotation;
        }

        static void DestroyCard(ThrowMotion m)
        {
            if (m.Card != null) Destroy(m.Card.gameObject);
            m.Card = null;
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// The flat hand rotation in the hand's parent space, read live: the shoulder leans
        /// with the body, and the hand must still lie flat on the deck.
        /// </summary>
        static Quaternion FlatLocal(ThrowMotion m)
        {
            Quaternion parent = m.Hand.parent != null ? m.Hand.parent.rotation : Quaternion.identity;
            return Quaternion.Inverse(parent) * m.FlatWorld;
        }

        /// <summary>Where the hand goes so its palm rests on top of the Draw Pile.</summary>
        Vector3 DeckLocal(ThrowMotion m, Quaternion flat)
        {
            return HandLocalForPalm(m.Hand, m.Palm, _deck.TopCenter + Vector3.up * palmClearance, flat);
        }

        Vector3 TowardTableCentre(PlayerCharacter player)
        {
            Vector3 toward = _deck.TopCenter - player.transform.position;
            toward.y = 0f;
            return toward.sqrMagnitude > 0.0001f ? toward : player.transform.forward;
        }

        Vector3 ThrowPath(Vector3 from, Vector3 to, float t)
        {
            return Vector3.LerpUnclamped(from, to, t) + Vector3.up * (throwArcHeight * Mathf.Sin(t * Mathf.PI));
        }
    }
}
