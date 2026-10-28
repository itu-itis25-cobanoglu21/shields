using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
using Shields.Game;
using Shields.Online;
using Shields.Presentation;

namespace Shields.Table
{
    /// <summary>
    /// One player's card space on the tabletop. Shields players do not hold cards, so
    /// these slots live on the table layout rather than on the character body.
    ///
    /// The layout is two portrait Health cards nearest the player, with the landscape
    /// Shield card in front of them toward the table centre. This component only places
    /// card views; what each card is, and which one is the Shield, is decided by the
    /// authoritative match state.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerTableArea : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] int playerIndex = -1;

        [Header("Card Slots")]
        [Tooltip("Portrait Health card nearest the player, on their left.")]
        [SerializeField] Transform verticalCardSlotLeft;

        [Tooltip("Portrait Health card nearest the player, on their right.")]
        [SerializeField] Transform verticalCardSlotRight;

        [Tooltip("Landscape Shield card, in front of the Health cards toward the table centre.")]
        [SerializeField] Transform horizontalCardSlot;

        [Tooltip("Face-down stored Charge card, beside the Shield. Created at runtime when missing.")]
        [SerializeField] Transform chargeSlot;

        [Tooltip("Local position used when the Charge slot has to be created at runtime.")]
        [SerializeField] Vector3 chargeSlotLocalPosition = new Vector3(0.1f, 0f, 0.085f);

        [Tooltip("Where a drawn action card (Attack, Replace Shield, Charged Attack) lands and is revealed: beyond the Shield, toward the table centre. Presentation only. Created at runtime when missing.")]
        [SerializeField] Transform actionRevealAnchor;

        [Tooltip("Local position used when the action reveal anchor has to be created at runtime.")]
        [SerializeField] Vector3 actionRevealLocalPosition = new Vector3(0f, 0f, 0.17f);

        [Tooltip("Where delivered beer mugs rest on the table. Social presentation only; card redraws never touch it. Created at runtime when missing.")]
        [SerializeField] Transform drinkArea;

        readonly List<Transform> _slots = new List<Transform>();
        readonly List<CardView> _cardViews = new List<CardView>();

        // Kept apart from the redrawn cards: it lives exactly as long as the public
        // HasCharge flag, so a routine snapshot never recreates it (or anything on it).
        CardView _chargeView;

        public int PlayerIndex { get { return playerIndex; } }

        public Transform VerticalCardSlotLeft { get { return verticalCardSlotLeft; } }
        public Transform VerticalCardSlotRight { get { return verticalCardSlotRight; } }
        public Transform HorizontalCardSlot { get { return horizontalCardSlot; } }

        /// <summary>Where a stored Charge shows face-down. Created on first use if the prefab has none.</summary>
        public Transform ChargeSlot
        {
            get
            {
                if (chargeSlot == null)
                {
                    chargeSlot = new GameObject("ChargeSlot").transform;
                    chargeSlot.SetParent(transform, false);
                    chargeSlot.localPosition = chargeSlotLocalPosition;
                }

                return chargeSlot;
            }
        }

        /// <summary>
        /// Where a card drawn for an action aimed at this player lands face up, clear of
        /// the Health, Shield and Charge cards. Created on first use if the prefab has none.
        /// Only temporary presentation cards ever sit here.
        /// </summary>
        public Transform ActionRevealAnchor
        {
            get
            {
                if (actionRevealAnchor == null)
                {
                    actionRevealAnchor = new GameObject("ActionRevealAnchor").transform;
                    actionRevealAnchor.SetParent(transform, false);
                    actionRevealAnchor.localPosition = actionRevealLocalPosition;
                }

                return actionRevealAnchor;
            }
        }

        /// <summary>
        /// Holds this seat's delivered beer mugs. Sits at the area origin with the area's
        /// rotation but undoes the area's scale, so its local units are plain meters and
        /// a mug keeps its real size. Lives and dies with the area, so mugs go with a
        /// layout teardown and never leak into the next match.
        /// </summary>
        public Transform DrinkArea
        {
            get
            {
                if (drinkArea == null)
                {
                    drinkArea = new GameObject("DrinkArea").transform;
                    drinkArea.SetParent(transform, false);
                }

                Vector3 scale = transform.lossyScale;
                drinkArea.localScale = new Vector3(1f / Mathf.Max(0.0001f, scale.x), 1f / Mathf.Max(0.0001f, scale.y),
                    1f / Mathf.Max(0.0001f, scale.z));

                return drinkArea;
            }
        }

        /// <summary>The view drawing this player's Shield card, or null.</summary>
        public CardView ShieldView { get; private set; }

        /// <summary>The face-down stored Charge card while a Charge is stored, or null.</summary>
        public CardView ChargeView { get { return _chargeView; } }

        /// <summary>The card views currently on this area.</summary>
        public IReadOnlyList<CardView> CardViews { get { return _cardViews; } }

        /// <summary>The three card slots, in left, right, front order.</summary>
        public IReadOnlyList<Transform> CardSlots
        {
            get
            {
                _slots.Clear();
                if (verticalCardSlotLeft != null) _slots.Add(verticalCardSlotLeft);
                if (verticalCardSlotRight != null) _slots.Add(verticalCardSlotRight);
                if (horizontalCardSlot != null) _slots.Add(horizontalCardSlot);
                return _slots;
            }
        }

        /// <summary>Ties this area to a player index. Nothing looks areas up by name.</summary>
        public void AssignPlayer(int index)
        {
            playerIndex = index;
            name = string.Format("PlayerArea_{0:00}", index);
        }

        /// <summary>
        /// Spawns one card view per dealt card and lays all three out portrait in the
        /// three slots, which is how the cards look before a Shield is chosen.
        /// </summary>
        public IReadOnlyList<CardView> ShowDealtCards(IReadOnlyList<PlayingCard> cards, CardView cardViewPrefab)
        {
            ClearCards();

            if (cards == null || cardViewPrefab == null) return _cardViews;

            IReadOnlyList<Transform> slots = CardSlots;

            for (int i = 0; i < cards.Count && i < slots.Count; i++)
            {
                CardView view = Instantiate(cardViewPrefab);
                view.Bind(cards[i], playerIndex);
                view.PlaceAt(slots[i], false);

                _cardViews.Add(view);
            }

            return _cardViews;
        }

        /// <summary>
        /// Moves the already spawned cards into their final roles: the two Health cards
        /// portrait in the vertical slots, the Shield turned sideways in the front slot.
        /// </summary>
        public void ArrangeRoles(IReadOnlyList<PlayingCard> healthCards, PlayingCard shieldCard)
        {
            CardView shieldView = FindView(shieldCard);
            if (shieldView != null) shieldView.PlaceAt(horizontalCardSlot, true);
            ShieldView = shieldView;

            if (healthCards == null) return;

            for (int i = 0; i < healthCards.Count; i++)
            {
                CardView view = FindView(healthCards[i]);
                if (view == null) continue;

                view.PlaceAt(i == 0 ? verticalCardSlotLeft : verticalCardSlotRight, false);
            }
        }

        /// <summary>
        /// Draws one seat from public state. Offline builds this from the local engine and
        /// online receives it from the host, so both paths render through the same code.
        /// Each Health slot shows a real card, a Virtual Health Marker or nothing, then the
        /// Shield, then one face-down card while a Charge is stored. Nothing here is read
        /// back into gameplay.
        ///
        /// Returns true when this call created the stored Charge card, i.e. the Charge
        /// has just appeared in public state.
        /// </summary>
        public bool ShowMatchState(PublicPlayerState state, CardView cardViewPrefab)
        {
            ClearSlotCards();

            if (cardViewPrefab == null) return false;

            ShowHealthSlot(state.LeftHealth, verticalCardSlotLeft, cardViewPrefab);
            ShowHealthSlot(state.RightHealth, verticalCardSlotRight, cardViewPrefab);

            if (state.ShieldCard.IsValid)
            {
                ShieldView = Spawn(cardViewPrefab, horizontalCardSlot, true);
                ShieldView.Bind(state.ShieldCard.ToPlayingCard(), playerIndex);
            }

            if (!state.HasCharge)
            {
                ClearChargeCard();
                return false;
            }

            if (_chargeView != null) return false;

            // Only the fact that a Charge exists is public, and the public state carries
            // nothing else, so the view could not draw the card even by mistake.
            _chargeView = Instantiate(cardViewPrefab);
            _chargeView.PlaceAt(ChargeSlot, false);
            _chargeView.ShowFaceDown(playerIndex);

            // Never a crosshair target, so it cannot block aiming while it is moved.
            foreach (Collider collider in _chargeView.GetComponentsInChildren<Collider>()) collider.enabled = false;

            return true;
        }

        void ShowHealthSlot(PublicHealthSlot slot, Transform where, CardView cardViewPrefab)
        {
            if (slot.IsCard) Spawn(cardViewPrefab, where, false).Bind(slot.Card.ToPlayingCard(), playerIndex);
            else if (slot.IsVirtual) Spawn(cardViewPrefab, where, false).ShowVirtualMarker(slot.Value, playerIndex);
        }

        CardView Spawn(CardView cardViewPrefab, Transform slot, bool landscape)
        {
            CardView view = Instantiate(cardViewPrefab);
            view.PlaceAt(slot, landscape);
            _cardViews.Add(view);
            return view;
        }

        /// <summary>The view currently drawing a given card, or null.</summary>
        public CardView FindView(PlayingCard card)
        {
            for (int i = 0; i < _cardViews.Count; i++)
                if (_cardViews[i] != null && _cardViews[i].Card == card) return _cardViews[i];

            return null;
        }

        /// <summary>Removes every card view from this area, the stored Charge card included.</summary>
        public void ClearCards()
        {
            ClearSlotCards();
            ClearChargeCard();
        }

        void ClearSlotCards()
        {
            for (int i = 0; i < _cardViews.Count; i++)
                if (_cardViews[i] != null) DestroyView(_cardViews[i]);

            _cardViews.Clear();
            ShieldView = null;
        }

        /// <summary>Removes the stored Charge card and everything attached to it.</summary>
        void ClearChargeCard()
        {
            if (_chargeView != null) DestroyView(_chargeView);
            _chargeView = null;
        }

        static void DestroyView(CardView view)
        {
            if (Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }

        /// <summary>Editor convenience so the prefab can be wired from code.</summary>
        public void BindSlots(Transform left, Transform right, Transform front)
        {
            verticalCardSlotLeft = left;
            verticalCardSlotRight = right;
            horizontalCardSlot = front;
        }
    }
}
