using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shields.Players;
using Shields.Presentation;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// Procedural action presentation. For now only the first Charge: the right hand
    /// reaches the deck, pulls the face-down card to the Charge slot and returns, then a
    /// purple flame starts burning on the card and keeps burning while the Charge exists.
    ///
    /// It decides nothing. It is told that an action already resolved, from the same
    /// public result every machine receives, and it only moves visuals that public state
    /// already created. The card it animates is the stored Charge card that
    /// <see cref="PlayerTableArea"/> made from the public HasCharge flag: it is held
    /// hidden until the animation hands it over, so there is never a second card, and it
    /// is a plain card back with no card identity behind it.
    ///
    /// The flame is parented to that card, so it lives exactly as long as the card, which
    /// lives exactly as long as the authoritative stored Charge.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerActionPresenter : MonoBehaviour
    {
        [Header("First Charge Timing (seconds)")]
        [SerializeField] float handReachDuration = 0.25f;
        [SerializeField] float deckHoldDuration = 0.175f;
        [SerializeField] float cardPullDuration = 0.25f;
        [SerializeField] float handReturnDuration = 0.25f;

        [Tooltip("Delay after the card settles in the Charge slot before the flame starts.")]
        [SerializeField] float flameStartDelay = 0.075f;

        [Header("First Charge Motion")]
        [Tooltip("Gap between the palm and the top of the deck while grabbing.")]
        [SerializeField] float palmClearance = 0.03f;

        [Tooltip("Peak height of the card's arc on its way to the Charge slot.")]
        [SerializeField] float cardArcHeight = 0.05f;

        [Tooltip("How far the hand trails the card while carrying it, as a fraction of the pull.")]
        [Range(0f, 0.3f)]
        [SerializeField] float handTrail = 0.08f;

        [Header("Charge Flame")]
        [SerializeField] GameObject chargeFlamePrefab;

        [Tooltip("World scale applied to the flame prefab on the card.")]
        [SerializeField] float flameWorldScale = 0.1f;

        [Tooltip("Startup skipped when a Charge is shown as already stored rather than animated.")]
        [SerializeField] float flameSettledTime = 1f;

        [Header("Robustness")]
        [Tooltip("How long a newly stored Charge card waits for its action result before it is shown directly.")]
        [SerializeField] float revealGrace = 0.35f;

        [Tooltip("How long an anticipated local reach waits at the deck for the host's confirmation.")]
        [SerializeField] float confirmTimeout = 1.5f;

        const string FlameName = "ChargeFlameVFX";

        // Top of the card face above the card root (Face sits at 0.001 and is 0.002 thick).
        const float CardFaceTop = 0.002f;

        sealed class HandMotion
        {
            public int Seat;
            public Transform Hand;
            public Transform Palm;
            public Vector3 RestPosition;
            public Quaternion RestRotation;
            public Coroutine Routine;
            public bool Confirmed;
            public bool Cancelled;
            public CardView Card;
        }

        struct PendingCard
        {
            public CardView Card;
            public float Since;
        }

        TableLayoutController _table;
        DeckView _deck;

        readonly Dictionary<int, HandMotion> _motions = new Dictionary<int, HandMotion>();
        readonly Dictionary<int, PendingCard> _pendingCards = new Dictionary<int, PendingCard>();
        readonly Dictionary<Transform, Pose> _restPoses = new Dictionary<Transform, Pose>();
        readonly List<int> _due = new List<int>();

        public void Bind(TableLayoutController table, DeckView deck)
        {
            _table = table;
            _deck = deck;
        }

        // ------------------------------------------------------------------ events

        /// <summary>
        /// Public state has just gained a stored Charge for this seat. The card stays
        /// hidden until an animation hands it over, or is shown directly after a short
        /// grace when no action result explains it (a late or restored state).
        /// </summary>
        public void OnChargeCardCreated(int seat, CardView card)
        {
            if (card == null) return;

            card.gameObject.SetActive(false);
            _pendingCards[seat] = new PendingCard { Card = card, Since = Time.time };
        }

        /// <summary>A first Charge resolved for this seat. Plays the full presentation.</summary>
        public void PlayFirstCharge(int seat)
        {
            HandMotion motion;
            if (_motions.TryGetValue(seat, out motion) && !motion.Confirmed && !motion.Cancelled)
            {
                // The local reach already started; carry on from where the hand is.
                motion.Confirmed = true;
                return;
            }

            StartFirstCharge(seat, true);
        }

        /// <summary>
        /// Cosmetic only: starts the local player's reach before the host answers, so the
        /// key press feels immediate. Nothing moves past the deck until
        /// <see cref="PlayFirstCharge"/> confirms it; otherwise the hand simply returns.
        /// </summary>
        public void AnticipateFirstCharge(int seat)
        {
            if (_motions.ContainsKey(seat)) return;

            StartFirstCharge(seat, false);
        }

        /// <summary>The host refused the anticipated action: bring the hand back.</summary>
        public void CancelAnticipation(int seat)
        {
            HandMotion motion;
            if (_motions.TryGetValue(seat, out motion) && !motion.Confirmed) motion.Cancelled = true;
        }

        /// <summary>Stops every animation, restores every hand and shows any held card.</summary>
        public void Clear()
        {
            foreach (int seat in new List<int>(_motions.Keys)) StopMotion(seat);
            foreach (int seat in new List<int>(_pendingCards.Keys)) RevealPending(seat, true);

            _motions.Clear();
            _pendingCards.Clear();
            _restPoses.Clear();
        }

        void Update()
        {
            if (_pendingCards.Count == 0) return;

            _due.Clear();
            foreach (KeyValuePair<int, PendingCard> pending in _pendingCards)
            {
                if (pending.Value.Card == null) { _due.Add(pending.Key); continue; }

                // An animation for this seat will hand the card over itself.
                if (_motions.ContainsKey(pending.Key)) continue;

                if (Time.time - pending.Value.Since >= revealGrace) _due.Add(pending.Key);
            }

            for (int i = 0; i < _due.Count; i++) RevealPending(_due[i], true);
        }

        // ------------------------------------------------------------------ first Charge

        void StartFirstCharge(int seat, bool confirmed)
        {
            StopMotion(seat);

            PlayerCharacter player = _table != null ? _table.GetPlayer(seat) : null;
            Transform hand = player != null ? player.RightHandAnchor : null;

            if (hand == null || _deck == null)
            {
                // Nothing to animate with: show the result directly.
                if (confirmed) RevealPending(seat, false);
                return;
            }

            Pose rest = RestPoseOf(hand);

            HandMotion motion = new HandMotion
            {
                Seat = seat,
                Hand = hand,
                Palm = hand.Find("PalmContact"),
                RestPosition = rest.position,
                RestRotation = rest.rotation,
                Confirmed = confirmed
            };

            _motions[seat] = motion;
            motion.Routine = StartCoroutine(FirstChargeRoutine(motion));
        }

        IEnumerator FirstChargeRoutine(HandMotion m)
        {
            // Reach: fast out of rest, easing into the grab point above the deck.
            Vector3 from = m.Hand.localPosition;
            for (float elapsed = 0f; elapsed < handReachDuration; elapsed += Time.deltaTime)
            {
                if (m.Hand == null) { Finish(m); yield break; }

                m.Hand.localPosition = Vector3.LerpUnclamped(from, GrabLocal(m), EaseOutCubic(elapsed / handReachDuration));
                yield return null;
            }

            // Hold: "grab card". Also waits here for the host when the reach was anticipated.
            float held = 0f;
            while (held < deckHoldDuration || (!m.Confirmed && !m.Cancelled && held < deckHoldDuration + confirmTimeout))
            {
                if (m.Hand == null) { Finish(m); yield break; }

                m.Hand.localPosition = GrabLocal(m);
                held += Time.deltaTime;
                yield return null;
            }

            if (m.Confirmed)
            {
                // The card normally exists already: the snapshot lands before the result.
                for (float waited = 0f; (m.Card = TakePending(m.Seat)) == null && waited < revealGrace; waited += Time.deltaTime)
                    yield return null;

                yield return PullCard(m);
            }

            yield return ReturnHand(m);

            Finish(m);
        }

        IEnumerator PullCard(HandMotion m)
        {
            Transform slot = m.Card != null ? m.Card.transform.parent : null;
            Vector3 start = _deck.TopCenter;
            Quaternion startRotation = _deck.transform.rotation;

            if (m.Card != null) m.Card.gameObject.SetActive(true);

            for (float elapsed = 0f; elapsed < cardPullDuration; elapsed += Time.deltaTime)
            {
                if (m.Hand == null) yield break;

                float t = elapsed / cardPullDuration;

                if (m.Card != null && slot != null)
                {
                    float e = Smooth(t);
                    m.Card.transform.SetPositionAndRotation(
                        CardPath(start, slot.position, e),
                        Quaternion.Slerp(startRotation, slot.rotation, e));
                }

                // The hand carries the card, a touch behind it.
                Vector3 end = slot != null ? slot.position : start;
                Vector3 carried = CardPath(start, end, Smooth(Mathf.Clamp01(t - handTrail)));
                m.Hand.localPosition = HandLocalForPalm(m, carried + Vector3.up * palmClearance);

                yield return null;
            }

            // Settle exactly in the slot the public state uses.
            if (m.Card != null && slot != null) m.Card.PlaceAt(slot, false);
        }

        IEnumerator ReturnHand(HandMotion m)
        {
            if (m.Hand == null) yield break;

            bool flameStarted = m.Card == null;
            float sinceSettled = 0f;

            Vector3 from = m.Hand.localPosition;
            for (float elapsed = 0f; elapsed < handReturnDuration; elapsed += Time.deltaTime)
            {
                if (m.Hand == null) yield break;

                m.Hand.localPosition = Vector3.LerpUnclamped(from, m.RestPosition, EaseInOutCubic(elapsed / handReturnDuration));

                if (!flameStarted && sinceSettled >= flameStartDelay) flameStarted = StartFlame(m.Card, false);
                sinceSettled += Time.deltaTime;

                yield return null;
            }

            RestoreRest(m);

            // A start delay longer than the return still waits its full time.
            for (; !flameStarted && sinceSettled < flameStartDelay; sinceSettled += Time.deltaTime)
                yield return null;

            if (!flameStarted) StartFlame(m.Card, false);
        }

        void StopMotion(int seat)
        {
            HandMotion motion;
            if (!_motions.TryGetValue(seat, out motion)) return;

            if (motion.Routine != null) StopCoroutine(motion.Routine);
            RestoreRest(motion);

            // A card already in the air goes straight to its slot, burning.
            if (motion.Card != null)
            {
                Transform slot = motion.Card.transform.parent;
                if (slot != null) motion.Card.PlaceAt(slot, false);
                motion.Card.gameObject.SetActive(true);
                StartFlame(motion.Card, true);
            }

            _motions.Remove(seat);
        }

        void Finish(HandMotion m)
        {
            RestoreRest(m);

            HandMotion current;
            if (_motions.TryGetValue(m.Seat, out current) && current == m) _motions.Remove(m.Seat);
        }

        static void RestoreRest(HandMotion m)
        {
            if (m.Hand == null) return;

            m.Hand.localPosition = m.RestPosition;
            m.Hand.localRotation = m.RestRotation;
        }

        /// <summary>
        /// The prefab pose, captured the first time this hand is used. Nothing else moves
        /// the hand and every animation restores it, so repeats can never drift.
        /// </summary>
        Pose RestPoseOf(Transform hand)
        {
            Pose rest;
            if (!_restPoses.TryGetValue(hand, out rest))
            {
                rest = new Pose(hand.localPosition, hand.localRotation);
                _restPoses[hand] = rest;
            }

            return rest;
        }

        // ------------------------------------------------------------------ cards and flame

        CardView TakePending(int seat)
        {
            PendingCard pending;
            if (!_pendingCards.TryGetValue(seat, out pending)) return null;

            _pendingCards.Remove(seat);
            return pending.Card;
        }

        void RevealPending(int seat, bool settled)
        {
            CardView card = TakePending(seat);
            if (card == null) return;

            card.gameObject.SetActive(true);
            StartFlame(card, settled);
        }

        /// <summary>
        /// Lights the flame on the card's centre, parented to the card so it follows it and
        /// is destroyed with it. Settled skips the startup, for a Charge nobody saw stored.
        /// </summary>
        bool StartFlame(CardView card, bool settled)
        {
            if (card == null || chargeFlamePrefab == null) return true;
            if (card.transform.Find(FlameName) != null) return true;

            Transform cardTransform = card.transform;
            GameObject flame = Instantiate(chargeFlamePrefab, cardTransform, false);
            flame.name = FlameName;

            flame.transform.localPosition = Vector3.up * CardFaceTop;
            flame.transform.localRotation = Quaternion.identity;
            flame.transform.localScale = Vector3.one * (flameWorldScale / Mathf.Max(0.0001f, cardTransform.lossyScale.y));

            ParticleSystem system = flame.GetComponent<ParticleSystem>();
            if (system != null)
            {
                if (settled) system.Simulate(flameSettledTime, true, true);
                system.Play(true);
            }

            return true;
        }

        // ------------------------------------------------------------------ geometry

        Vector3 GrabLocal(HandMotion m)
        {
            return HandLocalForPalm(m, _deck.TopCenter + Vector3.up * palmClearance);
        }

        /// <summary>The hand's local position that puts its palm on a world point.</summary>
        static Vector3 HandLocalForPalm(HandMotion m, Vector3 palmWorld)
        {
            Vector3 palmOffset = m.Palm != null ? m.Palm.position - m.Hand.position : Vector3.zero;
            Vector3 handWorld = palmWorld - palmOffset;

            return m.Hand.parent != null ? m.Hand.parent.InverseTransformPoint(handWorld) : handWorld;
        }

        Vector3 CardPath(Vector3 from, Vector3 to, float t)
        {
            return Vector3.LerpUnclamped(from, to, t) + Vector3.up * (cardArcHeight * Mathf.Sin(t * Mathf.PI));
        }

        // ------------------------------------------------------------------ easing

        static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
