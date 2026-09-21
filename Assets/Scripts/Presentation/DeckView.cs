using UnityEngine;
using Shields.Cards;

namespace Shields.Presentation
{
    /// <summary>
    /// Presentation for the draw pile sitting at the centre of the table. It shows a
    /// single deck prop and, for debugging, how many cards remain.
    ///
    /// It never owns cards. The authoritative draw pile is the plain C#
    /// <see cref="CardDeck"/>, and no GameObject is created per remaining card.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeckView : MonoBehaviour
    {
        [SerializeField] Transform deckProp;
        [SerializeField] TextMesh remainingLabel;

        [Tooltip("Show the remaining draw count next to the deck. Debug aid only.")]
        [SerializeField] bool showRemainingCount = true;

        CardDeck _deck;

        /// <summary>Points the view at the deck it should report on.</summary>
        public void Bind(CardDeck deck)
        {
            _deck = deck;
            Refresh();
        }

        /// <summary>Clears the readout when no match is running.</summary>
        public void Clear()
        {
            _deck = null;
            Refresh();
        }

        /// <summary>Re-reads the deck. Call after anything that changes the draw pile.</summary>
        public void Refresh()
        {
            if (remainingLabel != null)
            {
                bool show = showRemainingCount && _deck != null;
                remainingLabel.gameObject.SetActive(show);
                if (show) remainingLabel.text = _deck.DrawCount.ToString();
            }

            if (deckProp != null) deckProp.gameObject.SetActive(_deck == null || _deck.DrawCount > 0);
        }

        void OnEnable()
        {
            Refresh();
        }

        /// <summary>Wires the parts from code, so the scene object can be built headlessly.</summary>
        public void BindParts(Transform prop, TextMesh label)
        {
            deckProp = prop;
            remainingLabel = label;
        }
    }
}
