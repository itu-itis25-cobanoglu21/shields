using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
using Shields.Game;
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

        readonly List<Transform> _slots = new List<Transform>();
        readonly List<CardView> _cardViews = new List<CardView>();

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

        /// <summary>The view drawing this player's Shield card, or null.</summary>
        public CardView ShieldView { get; private set; }

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
        /// Redraws this area from the authoritative turn state: each Health slot as a
        /// real card, a Virtual Health Marker or nothing; the Shield; and one face-down
        /// card while a Charge is stored. Nothing here is read back into gameplay.
        /// </summary>
        public void ShowMatchState(MatchPlayerState state, CardView cardViewPrefab)
        {
            ClearCards();

            if (state == null || cardViewPrefab == null) return;

            ShowHealthSlot(state.LeftHealth, verticalCardSlotLeft, cardViewPrefab);
            ShowHealthSlot(state.RightHealth, verticalCardSlotRight, cardViewPrefab);

            if (state.ShieldCard.IsValid)
            {
                ShieldView = Spawn(cardViewPrefab, horizontalCardSlot, true);
                ShieldView.Bind(state.ShieldCard, playerIndex);
            }

            // Only the fact that a Charge exists is public. The view never gets the card.
            if (state.HasCharge) Spawn(cardViewPrefab, ChargeSlot, false).ShowFaceDown(playerIndex);
        }

        void ShowHealthSlot(HealthSlot slot, Transform where, CardView cardViewPrefab)
        {
            if (slot.IsCard) Spawn(cardViewPrefab, where, false).Bind(slot.Card, playerIndex);
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

        /// <summary>Removes every card view from this area.</summary>
        public void ClearCards()
        {
            for (int i = 0; i < _cardViews.Count; i++)
            {
                if (_cardViews[i] == null) continue;

                GameObject go = _cardViews[i].gameObject;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            _cardViews.Clear();
            ShieldView = null;
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
