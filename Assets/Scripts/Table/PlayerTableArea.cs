using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
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

        readonly List<Transform> _slots = new List<Transform>();
        readonly List<CardView> _cardViews = new List<CardView>();

        public int PlayerIndex { get { return playerIndex; } }

        public Transform VerticalCardSlotLeft { get { return verticalCardSlotLeft; } }
        public Transform VerticalCardSlotRight { get { return verticalCardSlotRight; } }
        public Transform HorizontalCardSlot { get { return horizontalCardSlot; } }

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

            if (healthCards == null) return;

            for (int i = 0; i < healthCards.Count; i++)
            {
                CardView view = FindView(healthCards[i]);
                if (view == null) continue;

                view.PlaceAt(i == 0 ? verticalCardSlotLeft : verticalCardSlotRight, false);
            }
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
