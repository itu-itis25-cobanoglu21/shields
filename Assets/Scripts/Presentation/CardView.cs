using UnityEngine;
using Shields.Cards;

namespace Shields.Presentation
{
    /// <summary>
    /// Presentation for a single playing card. This renders a <see cref="PlayingCard"/>
    /// and nothing more.
    ///
    /// It never decides a card's rank, suit, value or identity, and it holds no
    /// gameplay state. The card it was handed is kept only so the view can draw itself
    /// and so a click can report which card was clicked; the authority stays in the
    /// plain C# match state, which validates anything a click claims.
    /// </summary>
    [DisallowMultipleComponent]
    public class CardView : MonoBehaviour
    {
        [Header("Face")]
        [SerializeField] TextMesh centerLabel;
        [SerializeField] TextMesh cornerLabel;

        [Header("Feedback")]
        [SerializeField] Transform highlight;

        static readonly Color RedInk = new Color(0.72f, 0.10f, 0.12f);
        static readonly Color BlackInk = new Color(0.10f, 0.10f, 0.12f);

        /// <summary>The card this view was told to draw. Presentation data, not authority.</summary>
        public PlayingCard Card { get; private set; }

        /// <summary>Which seat owns the card on the table. Used to ignore foreign clicks.</summary>
        public int OwnerPlayerIndex { get; private set; }

        public bool IsBound { get { return Card.IsValid; } }

        /// <summary>Points this view at a card and redraws its face.</summary>
        public void Bind(PlayingCard card, int ownerPlayerIndex)
        {
            Card = card;
            OwnerPlayerIndex = ownerPlayerIndex;

            name = card.IsValid
                ? string.Format("Card_{0}{1}", RankSymbol(card.Rank), SuitSymbol(card.Suit))
                : "Card_Unbound";

            Redraw();
        }

        public void SetHighlighted(bool on)
        {
            if (highlight != null) highlight.gameObject.SetActive(on);
        }

        /// <summary>Places this card at a slot, portrait or turned sideways for a Shield.</summary>
        public void PlaceAt(Transform slot, bool landscape)
        {
            if (slot == null) return;

            transform.SetParent(slot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = landscape ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        void Redraw()
        {
            string face = Card.IsValid
                ? RankSymbol(Card.Rank) + SuitSymbol(Card.Suit)
                : string.Empty;

            Color ink = Card.IsValid ? InkColor(Card.Suit) : BlackInk;

            if (centerLabel != null)
            {
                centerLabel.text = face;
                centerLabel.color = ink;
            }

            if (cornerLabel != null)
            {
                cornerLabel.text = face;
                cornerLabel.color = ink;
            }

            SetHighlighted(false);
        }

        // ---------------------------------------------------------------- symbols

        /// <summary>Short rank label: A, 2 through 10, J, Q, K.</summary>
        public static string RankSymbol(CardRank rank)
        {
            switch (rank)
            {
                case CardRank.Ace: return "A";
                case CardRank.Jack: return "J";
                case CardRank.Queen: return "Q";
                case CardRank.King: return "K";
                default: return ((int)rank).ToString();
            }
        }

        /// <summary>Suit glyph. The built in font carries all four.</summary>
        public static string SuitSymbol(CardSuit suit)
        {
            switch (suit)
            {
                case CardSuit.Clubs: return "♣";
                case CardSuit.Diamonds: return "♦";
                case CardSuit.Hearts: return "♥";
                default: return "♠";
            }
        }

        /// <summary>Standard card colouring, so the suits stay distinguishable at a glance.</summary>
        public static Color InkColor(CardSuit suit)
        {
            return suit == CardSuit.Hearts || suit == CardSuit.Diamonds ? RedInk : BlackInk;
        }

        /// <summary>Wires the prefab parts from code, so the prefab can be built headlessly.</summary>
        public void BindParts(TextMesh center, TextMesh corner, Transform highlightTransform)
        {
            centerLabel = center;
            cornerLabel = corner;
            highlight = highlightTransform;
        }
    }
}
