using System.Collections.Generic;
using UnityEngine;

namespace Shields.Table
{
    /// <summary>
    /// One player's card space on the tabletop. Shields players do not hold cards, so
    /// these slots live on the table layout rather than on the character body.
    ///
    /// The layout is two portrait cards side by side nearest the player, with one
    /// landscape card in front of them toward the table center. No card state or rules
    /// live here, only the positions future gameplay will place cards into.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerTableArea : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] int playerIndex = -1;

        [Header("Card Slots")]
        [Tooltip("Portrait card nearest the player, on their left.")]
        [SerializeField] Transform verticalCardSlotLeft;

        [Tooltip("Portrait card nearest the player, on their right.")]
        [SerializeField] Transform verticalCardSlotRight;

        [Tooltip("Landscape card in front of the two portrait cards, toward the table center.")]
        [SerializeField] Transform horizontalCardSlot;

        readonly List<Transform> _slots = new List<Transform>();

        public int PlayerIndex { get { return playerIndex; } }

        public Transform VerticalCardSlotLeft { get { return verticalCardSlotLeft; } }
        public Transform VerticalCardSlotRight { get { return verticalCardSlotRight; } }
        public Transform HorizontalCardSlot { get { return horizontalCardSlot; } }

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
        /// Shows or hides the placeholder cards sitting in the slots. They exist only to
        /// validate position and first person readability and can be turned off freely.
        /// </summary>
        public void SetPlaceholderCardsVisible(bool visible)
        {
            IReadOnlyList<Transform> slots = CardSlots;
            for (int i = 0; i < slots.Count; i++)
                for (int c = 0; c < slots[i].childCount; c++)
                    slots[i].GetChild(c).gameObject.SetActive(visible);
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
