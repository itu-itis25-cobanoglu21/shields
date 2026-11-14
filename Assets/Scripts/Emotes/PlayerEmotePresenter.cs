using System;
using System.Collections.Generic;
using UnityEngine;
using Shields.Match;
using Shields.Players;
using Shields.Social;
using Shields.Table;
using Shields.Tavern;

namespace Shields.Emotes
{
    /// <summary>
    /// Plays hand emotes on this machine, for every seat, from emote state it is told about:
    /// the local seat's own input at once, other seats' from the authority. It decides nothing.
    ///
    ///   Middle Finger - left hand raised in front of the shoulder, back of the hand toward
    ///                   where the player looks, middle finger up
    ///   Point         - right hand out along the player's head look (never locked onto a
    ///                   player), flat, palm down, index straight; reach is pushed out by the wheel
    ///   Offer         - right hand out toward the table, upright, open, thumb up
    ///   Clasp         - a matched handshake: both right hands meet beside each other at the
    ///                   table centre, close, shake a few times and open again
    ///
    /// Only finger shapes come from <see cref="HandPoses"/> (the Emote layer of
    /// <see cref="HandRig"/>); where the floating hand goes is computed here, in world space
    /// from the seat, its head pose and the table, so it holds for every seat and every table
    /// size and needs no transform from the network.
    ///
    /// Gameplay always wins. While a gameplay presenter owns a hand (cards, Charge, weapon,
    /// beer) this neither moves it nor poses its fingers; when the hand is free again an emote
    /// still held blends back in from rest. Every hand is moved from its true rest pose, which
    /// is handed to the gameplay presenters before it is first moved, so their own animations
    /// always return to the right place.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1200)]
    public class PlayerEmotePresenter : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        [SerializeField] float raiseDuration = 0.24f;
        [SerializeField] float lowerDuration = 0.26f;
        [SerializeField] float fingerBlend = 0.16f;

        [Tooltip("Fingers let go this fast when gameplay takes the hand.")]
        [SerializeField] float yieldBlend = 0.08f;

        [Header("Middle Finger (left hand)")]
        [Tooltip("Palm position in the look frame: x right, y up from the seat's floor, z along the horizontal head look.")]
        [SerializeField] Vector3 middleFingerPalm = new Vector3(-0.14f, 1.13f, 0.32f);

        [Tooltip("How far the raised finger leans toward the look direction, degrees. 0 = straight up (the finger then stands within ~2 degrees of vertical).")]
        [SerializeField] float middleFingerLean = 0f;

        [Tooltip("Extra turn of the hand about the raised finger, degrees (+ turns the back toward the body's centre line). 0 = back of the hand square to where the player looks.")]
        [SerializeField] float middleFingerTurn = 0f;

        [Header("Point (right hand)")]
        [Tooltip("Where the pointing arm starts in the seat frame: x right, y up from the floor, z toward the table.")]
        [SerializeField] Vector3 pointShoulder = new Vector3(0.17f, 1.10f, 0.06f);

        [Tooltip("Normal distance of the palm from the shoulder along the aim.")]
        [SerializeField] float pointBaseReach = 0.36f;

        [Tooltip("Extra distance the mouse wheel can push the hand out, at full reach.")]
        [SerializeField] float pointExtraReach = 0.28f;

        [Tooltip("Roll of the pointing hand about the aim, degrees (+ turns the thumb up). 0 = flat, palm down.")]
        [SerializeField] float pointRoll = 0f;

        [Tooltip("Correction so the index finger, not the middle of the hand, lies on the aim: yaw, pitch (degrees). Measured: +2 yaw puts the index on the aim; no pitch keeps the hand flat (the index then sits ~8 degrees above it).")]
        [SerializeField] Vector2 pointIndexCorrection = new Vector2(2f, 0f);

        [Tooltip("How quickly the pointing direction follows the look.")]
        [SerializeField] float pointAimSmoothing = 14f;

        [Tooltip("How quickly the hand follows a reach change.")]
        [SerializeField] float reachSmoothing = 12f;

        [Header("Handshake offer (right hand)")]
        [Tooltip("Palm position in the seat frame: x right, y up from the floor, z toward the table.")]
        [SerializeField] Vector3 offerPalm = new Vector3(0.10f, 1.00f, 0.50f);

        [Tooltip("Fingers dip this many degrees below level.")]
        [SerializeField] float offerDip = 12f;

        [Tooltip("Roll about the fingers, degrees (+ tips the thumb outward).")]
        [SerializeField] float offerRoll = 10f;

        [Header("Matched handshake")]
        [Tooltip("Height of the shared handshake point above the centre deck's resting point.")]
        [SerializeField] float claspHeight = 0.14f;

        [Tooltip("Each palm sits this far to its own hand's right of the shared point, so the hands meet instead of overlapping.")]
        [SerializeField] float claspPalmOffset = 0.022f;

        [Tooltip("Each hand sits this far back toward its own player along the line between them.")]
        [SerializeField] float claspPullBack = 0.03f;

        [Tooltip("Roll of the clasped hands about the line between the players, degrees.")]
        [SerializeField] float claspRoll = 12f;

        [Tooltip("Fingers dip this many degrees while clasped.")]
        [SerializeField] float claspDip = 8f;

        [SerializeField] float shakeAmplitude = 0.04f;
        [SerializeField] float shakeCycles = 2.5f;

        enum HandEmote { None, MiddleFinger, Point, Offer, Clasp }

        sealed class Hand
        {
            public Transform Anchor;
            public Transform Palm;
            public HandRig Rig;
            public bool Left;

            public Pose Rest;
            public bool HasRest;

            public HandEmote Kind;
            public bool Writing;
            public Pose From;
            public float Blend = 1f;
            public float BlendDuration = 0.2f;

            public Vector3 Aim;
            public bool HasAim;
            public float Reach;
        }

        sealed class Clasp
        {
            public HandshakePair Pair;
            public int Partner;
            public float StartedAt;
            public int FingerStage;
            public bool Interrupted;
        }

        sealed class Seat
        {
            public int Index;
            public PlayerCharacter Player;
            public EmoteState State = EmoteState.Idle;
            public Hand LeftHand = new Hand { Left = true };
            public Hand RightHand = new Hand();
            public Clasp Clasp;
        }

        TableLayoutController _table;
        PlayerActionPresenter _actions;
        BeerDrinkPresenter _beer;

        readonly Dictionary<int, Seat> _seats = new Dictionary<int, Seat>();
        readonly List<int> _keys = new List<int>();

        /// <summary>
        /// Raised when gameplay took a seat's right hand in the middle of a matched handshake,
        /// with the seat and the pair. The seat's own machine reports it to the authority.
        /// </summary>
        public event Action<int, HandshakePair> HandshakeInterrupted;

        public void Bind(TableLayoutController table, PlayerActionPresenter actions, BeerDrinkPresenter beer)
        {
            _table = table;
            _actions = actions;
            _beer = beer;
        }

        // ------------------------------------------------------------------ queries

        public EmoteState StateOf(int seat)
        {
            Seat s;
            return _seats.TryGetValue(seat, out s) ? s.State : EmoteState.Idle;
        }

        /// <summary>True while a gameplay presenter owns the seat's left hand.</summary>
        public bool LeftHandBusy(int seat)
        {
            return (_actions != null && _actions.IsLeftHandBusy(seat)) || (_beer != null && _beer.IsBusy(seat));
        }

        /// <summary>True while a gameplay presenter owns the seat's right hand.</summary>
        public bool RightHandBusy(int seat)
        {
            return _actions != null && _actions.IsRightHandBusy(seat);
        }

        public bool InHandshake(int seat)
        {
            Seat s;
            return _seats.TryGetValue(seat, out s) && s.Clasp != null;
        }

        // ------------------------------------------------------------------ input from the coordinator

        /// <summary>What a seat shows now. The fingers and hands blend there on their own.</summary>
        public void SetState(int seat, EmoteState state)
        {
            SeatOf(seat).State = state;
        }

        /// <summary>The authority paired two seats: both right hands go to the table centre now.</summary>
        public void BeginHandshake(HandshakePair pair)
        {
            StartClasp(pair.SeatA, pair);
            StartClasp(pair.SeatB, pair);
        }

        /// <summary>The authority ended a handshake (played out, cancelled, a participant gone).</summary>
        public void EndHandshake(HandshakeEnd end)
        {
            EndClasp(end.Pair.SeatA, end.Pair.Id);
            EndClasp(end.Pair.SeatB, end.Pair.Id);
        }

        /// <summary>Drops every emote at once and puts every hand it moved back to rest: match end, rematch, teardown.</summary>
        public void ClearAll()
        {
            foreach (Seat s in _seats.Values)
            {
                ReleaseNow(s.LeftHand);
                ReleaseNow(s.RightHand);
            }

            _seats.Clear();
        }

        void StartClasp(int seat, HandshakePair pair)
        {
            Seat s = SeatOf(seat);
            if (s.Clasp != null && s.Clasp.Pair.Id == pair.Id) return;

            s.Clasp = new Clasp { Pair = pair, Partner = pair.PartnerOf(seat), StartedAt = Time.time };
        }

        void EndClasp(int seat, int pairId)
        {
            Seat s;
            if (_seats.TryGetValue(seat, out s) && s.Clasp != null && s.Clasp.Pair.Id == pairId) s.Clasp = null;
        }

        Seat SeatOf(int seat)
        {
            Seat s;
            if (!_seats.TryGetValue(seat, out s))
            {
                s = new Seat { Index = seat };
                _seats[seat] = s;
            }

            return s;
        }

        // ------------------------------------------------------------------ frame

        void LateUpdate()
        {
            if (_table == null) return;

            _keys.Clear();
            _keys.AddRange(_seats.Keys);
            for (int i = 0; i < _keys.Count; i++) UpdateSeat(_seats[_keys[i]]);
        }

        void UpdateSeat(Seat s)
        {
            PlayerCharacter player = _table.GetPlayer(s.Index);
            if (player != s.Player)
            {
                // A rebuilt table: the old hands are gone, start clean on the new character.
                s.Player = player;
                s.LeftHand = new Hand { Left = true };
                s.RightHand = new Hand();
            }

            if (player == null) return;

            Bind(s.LeftHand, player.LeftHandAnchor, player.LeftHand);
            Bind(s.RightHand, player.RightHandAnchor, player.RightHand);

            // A handshake that has played out locally ends here even if the authority's word is late.
            if (s.Clasp != null && Time.time - s.Clasp.StartedAt >= (float)HandshakeTiming.Total) s.Clasp = null;

            HandEmote left = s.State.Has(EmoteFlags.MiddleFinger) ? HandEmote.MiddleFinger : HandEmote.None;

            HandEmote right = HandEmote.None;
            if (s.Clasp != null) right = HandEmote.Clasp;
            else if (s.State.Has(EmoteFlags.Point)) right = HandEmote.Point;
            else if (s.State.Has(EmoteFlags.HandshakeOffer)) right = HandEmote.Offer;

            bool rightBusy = RightHandBusy(s.Index);
            if (rightBusy && s.Clasp != null)
            {
                // Gameplay needs the hand mid handshake: let go now and tell the authority once.
                HandshakePair pair = s.Clasp.Pair;
                bool first = !s.Clasp.Interrupted;
                s.Clasp.Interrupted = true;
                s.Clasp = null;
                if (first && HandshakeInterrupted != null) HandshakeInterrupted(s.Index, pair);
            }

            Drive(s, s.LeftHand, left, LeftHandBusy(s.Index));
            Drive(s, s.RightHand, right, rightBusy);
        }

        static void Bind(Hand h, Transform anchor, HandRig rig)
        {
            if (h.Anchor == anchor && h.Rig == rig) return;

            h.Anchor = anchor;
            h.Rig = rig;
            h.Palm = anchor != null ? anchor.Find("PalmContact") : null;
            h.HasRest = false;
            h.Writing = false;
            h.Kind = HandEmote.None;
        }

        void Drive(Seat s, Hand h, HandEmote wanted, bool busy)
        {
            if (h.Anchor == null) return;

            if (busy)
            {
                // Gameplay owns the hand: stop moving it and give the fingers up.
                if (h.Kind != HandEmote.None || h.Writing)
                {
                    if (h.Rig != null) h.Rig.ClearPose(HandPoseLayer.Emote, yieldBlend);
                    h.Kind = HandEmote.None;
                    h.Writing = false;
                    h.HasAim = false;
                }

                return;
            }

            if (wanted != h.Kind)
            {
                if (wanted != HandEmote.None && !h.Writing) CaptureRest(h);

                h.From = new Pose(h.Anchor.localPosition, h.Anchor.localRotation);
                h.Kind = wanted;
                h.Blend = 0f;
                h.BlendDuration = wanted == HandEmote.None ? lowerDuration
                    : wanted == HandEmote.Clasp ? (float)HandshakeTiming.Approach
                    : raiseDuration;
                if (wanted != HandEmote.None) h.Writing = true;
                if (wanted != HandEmote.Point) h.HasAim = false;

                PoseFingers(h, wanted);
                if (wanted == HandEmote.Clasp && s.Clasp != null) s.Clasp.FingerStage = 0;
            }

            if (!h.Writing) return;

            float dt = Time.deltaTime;
            h.Blend = h.BlendDuration > 0f ? Mathf.Min(1f, h.Blend + dt / h.BlendDuration) : 1f;
            float e = EaseInOut(h.Blend);

            if (wanted == HandEmote.None)
            {
                Pose rest = h.Rest;
                h.Anchor.localPosition = Vector3.LerpUnclamped(h.From.position, rest.position, e);
                h.Anchor.localRotation = Quaternion.Slerp(h.From.rotation, rest.rotation, e);

                if (h.Blend >= 1f)
                {
                    h.Anchor.localPosition = rest.position;
                    h.Anchor.localRotation = rest.rotation;
                    h.Writing = false;
                }

                return;
            }

            if (wanted == HandEmote.Clasp) StageClaspFingers(s, h);

            Vector3 palmWorld;
            Quaternion world;
            if (!Target(s, h, wanted, dt, out palmWorld, out world)) return;

            Transform parent = h.Anchor.parent;
            Quaternion local = parent != null ? Quaternion.Inverse(parent.rotation) * world : world;
            Vector3 palmLocal = h.Palm != null ? h.Palm.localPosition : Vector3.zero;
            Vector3 anchorWorld = palmWorld - world * palmLocal;
            Vector3 position = parent != null ? parent.InverseTransformPoint(anchorWorld) : anchorWorld;

            h.Anchor.localPosition = Vector3.LerpUnclamped(h.From.position, position, e);
            h.Anchor.localRotation = Quaternion.Slerp(h.From.rotation, local, e);
        }

        /// <summary>
        /// Remembers the hand's rest pose the first time it is moved (it is at rest then: no
        /// gameplay owns it) and hands that pose to the gameplay presenters, so an action that
        /// starts while the hand is raised still returns it to the true rest.
        /// </summary>
        void CaptureRest(Hand h)
        {
            if (!h.HasRest)
            {
                h.Rest = new Pose(h.Anchor.localPosition, h.Anchor.localRotation);
                h.HasRest = true;
            }

            if (_actions != null) _actions.SeedRestPose(h.Anchor, h.Rest);
            if (_beer != null && h.Left) _beer.SeedHandRest(h.Anchor, h.Rest);
        }

        void ReleaseNow(Hand h)
        {
            if (h.Rig != null && h.Kind != HandEmote.None) h.Rig.ClearPose(HandPoseLayer.Emote, 0f);

            if (h.Writing && h.HasRest && h.Anchor != null)
            {
                h.Anchor.localPosition = h.Rest.position;
                h.Anchor.localRotation = h.Rest.rotation;
            }

            h.Writing = false;
            h.Kind = HandEmote.None;
        }

        // ------------------------------------------------------------------ fingers

        void PoseFingers(Hand h, HandEmote kind)
        {
            if (h.Rig == null) return;

            switch (kind)
            {
                case HandEmote.MiddleFinger: h.Rig.SetPose(HandPoseLayer.Emote, HandPoseId.MiddleFinger, fingerBlend); break;
                case HandEmote.Point: h.Rig.SetPose(HandPoseLayer.Emote, HandPoseId.Point, fingerBlend); break;
                case HandEmote.Offer:
                case HandEmote.Clasp: h.Rig.SetPose(HandPoseLayer.Emote, HandPoseId.HandshakeOffer, fingerBlend); break;
                default: h.Rig.ClearPose(HandPoseLayer.Emote, lowerDuration); break;
            }
        }

        /// <summary>Open on the way in, close round the other hand on contact, open again before parting.</summary>
        void StageClaspFingers(Seat s, Hand h)
        {
            if (s.Clasp == null || h.Rig == null) return;

            float t = Time.time - s.Clasp.StartedAt;
            int stage = t < HandshakeTiming.Approach ? 0
                : t < HandshakeTiming.Approach + HandshakeTiming.Grip + HandshakeTiming.Shake ? 1
                : 2;
            if (stage == s.Clasp.FingerStage) return;

            s.Clasp.FingerStage = stage;
            if (stage == 1) h.Rig.SetPose(HandPoseLayer.Emote, HandPoseId.HandshakeGrip, (float)HandshakeTiming.Grip);
            else if (stage == 2) h.Rig.SetPose(HandPoseLayer.Emote, HandPoseId.HandshakeOffer, (float)HandshakeTiming.Release);
        }

        // ------------------------------------------------------------------ targets (world space)

        bool Target(Seat s, Hand h, HandEmote kind, float dt, out Vector3 palm, out Quaternion rotation)
        {
            PlayerCharacter p = s.Player;
            palm = Vector3.zero;
            rotation = Quaternion.identity;

            Vector3 up = Vector3.up;
            Vector3 toTable = Flat(_table.TableCenter - p.transform.position, p.transform.forward);
            Vector3 right = Vector3.Cross(up, toTable).normalized;
            Vector3 origin = p.transform.position;

            switch (kind)
            {
                case HandEmote.MiddleFinger:
                {
                    Vector3 look = HeadLookFlat(p, toTable);
                    Vector3 lookRight = Vector3.Cross(up, look).normalized;
                    palm = origin + lookRight * middleFingerPalm.x + up * middleFingerPalm.y + look * middleFingerPalm.z;

                    Vector3 fingers = Quaternion.AngleAxis(middleFingerLean, lookRight) * up;
                    Vector3 back = Vector3.ProjectOnPlane(look, fingers).normalized;
                    back = Quaternion.AngleAxis(middleFingerTurn, fingers) * back;
                    return HandRotation(h, fingers, back, 0f, Vector2.zero, out rotation);
                }

                case HandEmote.Point:
                {
                    Vector3 shoulder = origin + right * pointShoulder.x + up * pointShoulder.y + toTable * pointShoulder.z;
                    Vector3 aim = PointAim(s, shoulder);

                    float k = 1f - Mathf.Exp(-pointAimSmoothing * dt);
                    h.Aim = h.HasAim ? Vector3.Slerp(h.Aim, aim, k).normalized : aim;
                    h.HasAim = true;

                    float reach = s.State.ReachFraction;
                    h.Reach = Mathf.Lerp(h.Reach, reach, 1f - Mathf.Exp(-reachSmoothing * dt));

                    palm = shoulder + h.Aim * (pointBaseReach + pointExtraReach * h.Reach);
                    Vector3 back = Vector3.ProjectOnPlane(up, h.Aim);
                    if (back.sqrMagnitude < 1e-4f) back = Vector3.ProjectOnPlane(-toTable, h.Aim);
                    return HandRotation(h, h.Aim, back.normalized, pointRoll, pointIndexCorrection, out rotation);
                }

                case HandEmote.Offer:
                {
                    palm = origin + right * offerPalm.x + up * offerPalm.y + toTable * offerPalm.z;
                    Vector3 fingers = Quaternion.AngleAxis(offerDip, right) * toTable;
                    return HandRotation(h, fingers, right, offerRoll, Vector2.zero, out rotation);
                }

                case HandEmote.Clasp:
                    return ClaspTarget(s, h, out palm, out rotation);
            }

            return false;
        }

        bool ClaspTarget(Seat s, Hand h, out Vector3 palm, out Quaternion rotation)
        {
            palm = Vector3.zero;
            rotation = Quaternion.identity;
            if (s.Clasp == null) return false;

            PlayerCharacter partner = _table.GetPlayer(s.Clasp.Partner);
            if (partner == null) return false;

            Vector3 up = Vector3.up;
            Vector3 center = _table.TableCenter;
            center.y = _table.DeckAimPoint.y + claspHeight;

            // Along the line between the two players, the same line on both sides.
            Vector3 along = Flat(partner.transform.position - s.Player.transform.position, s.Player.transform.forward);
            Vector3 right = Vector3.Cross(up, along).normalized;

            float t = Time.time - s.Clasp.StartedAt;
            float shakeStart = (float)(HandshakeTiming.Approach + HandshakeTiming.Grip);
            float shake = Mathf.Clamp01((t - shakeStart) / (float)HandshakeTiming.Shake);
            float envelope = Mathf.Sin(shake * Mathf.PI);
            float bob = shake > 0f && shake < 1f
                ? -Mathf.Sin(shake * shakeCycles * 2f * Mathf.PI) * shakeAmplitude * Mathf.Clamp01(envelope * 3f)
                : 0f;

            palm = center + right * claspPalmOffset - along * claspPullBack + up * bob;
            Vector3 fingers = Quaternion.AngleAxis(claspDip, right) * along;
            return HandRotation(h, fingers, right, claspRoll, Vector2.zero, out rotation);
        }

        /// <summary>
        /// Where a seat points: simply where it looks. No player is ever locked onto; the aim is
        /// the head's own line of sight, which every machine already has from the head pose, so
        /// remote players point the same way with nothing extra sent.
        /// </summary>
        static Vector3 PointAim(Seat s, Vector3 shoulder)
        {
            PlayerCharacter p = s.Player;
            Transform view = p.CameraAnchor != null ? p.CameraAnchor : p.HeadPivot;
            if (view == null) return p.transform.forward;

            // Toward a point well out on the line of sight, so the finger and the view agree.
            Vector3 far = view.position + view.forward * 3f;
            return (far - shoulder).normalized;
        }

        static Vector3 HeadLookFlat(PlayerCharacter p, Vector3 fallback)
        {
            Transform head = p.HeadPivot != null ? p.HeadPivot : p.transform;
            return Flat(head.forward, fallback);
        }

        /// <summary>
        /// World rotation of a hand whose own resting finger and back-of-hand axes (from its rig)
        /// end up along <paramref name="fingers"/> and <paramref name="back"/>, then rolled about
        /// the fingers and nudged by a yaw/pitch correction in that frame.
        /// </summary>
        static bool HandRotation(Hand h, Vector3 fingers, Vector3 back, float roll, Vector2 correction, out Quaternion rotation)
        {
            Vector3 restFingers, restBack;
            if (h.Rig == null || !h.Rig.RestAxes(h.Anchor, out restFingers, out restBack))
            {
                restFingers = Vector3.forward;
                restBack = Vector3.up;
            }

            // The rig's back-of-hand axis mirrors between hands; roll and yaw mean the same visually on both.
            float side = h.Left ? -1f : 1f;
            Quaternion frame = Quaternion.LookRotation(fingers, back) *
                Quaternion.Euler(correction.y, correction.x * side, roll * side);
            rotation = frame * Quaternion.Inverse(Quaternion.LookRotation(restFingers, restBack));
            return true;
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            if (v.sqrMagnitude < 1e-6f)
            {
                v = fallback;
                v.y = 0f;
            }

            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
