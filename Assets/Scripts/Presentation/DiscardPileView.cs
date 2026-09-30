using UnityEngine;

namespace Shields.Presentation
{
    /// <summary>
    /// Presentation for the discard pile beside the draw pile: a face-down stack and a
    /// count, nothing else.
    ///
    /// It knows only how many cards are in the pile. No discarded card's identity is part
    /// of public state, so none is shown, stored or inferred here. The authoritative
    /// discard pile is the host's <see cref="Shields.Cards.CardDeck"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiscardPileView : MonoBehaviour
    {
        [SerializeField] Transform pileProp;
        [SerializeField] TextMesh countLabel;

        [Tooltip("Show \"Discard: N\" next to the pile.")]
        [SerializeField] bool showCount = true;

        int _count = -1;

        /// <summary>Shows a public discard count. Zero or less leaves the spot empty.</summary>
        public void ShowCount(int count)
        {
            _count = count;
            Refresh();
        }

        /// <summary>Hides the pile when no match is running.</summary>
        public void Clear()
        {
            _count = -1;
            Refresh();
        }

        void Refresh()
        {
            bool visible = _count > 0;

            if (pileProp != null) pileProp.gameObject.SetActive(visible);

            if (countLabel != null)
            {
                bool show = showCount && visible;
                countLabel.gameObject.SetActive(show);
                if (show) countLabel.text = "Discard: " + _count;
            }
        }

        void OnEnable()
        {
            Refresh();
        }
    }
}
