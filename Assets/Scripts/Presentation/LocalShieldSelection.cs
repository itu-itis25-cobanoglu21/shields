using System;
using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;

namespace Shields.Presentation
{
    /// <summary>
    /// Lets the local player pick their Shield by looking at one of their three dealt
    /// cards and pressing the primary button. Aiming goes through the centre screen
    /// crosshair, so the seated head look stays on and the cursor stays locked for the
    /// whole choice.
    ///
    /// This only reports which card was chosen. Whether that choice is legal is decided
    /// by the authoritative match state.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalShieldSelection : MonoBehaviour
    {
        readonly List<CardView> _selectable = new List<CardView>();

        CrosshairInteraction _interaction;
        Action<PlayingCard> _onChosen;
        CardView _hovered;

        public bool IsActive { get; private set; }

        /// <summary>The selectable card currently under the crosshair, or null.</summary>
        public CardView HoveredCard { get { return _hovered; } }

        /// <summary>
        /// Starts selection over the given card views. Returns false when there is
        /// nothing to aim with or nothing to pick, in which case the caller should
        /// resolve the choice itself.
        /// </summary>
        public bool Begin(CrosshairInteraction interaction, IReadOnlyList<CardView> selectableCards,
            Action<PlayingCard> onChosen)
        {
            if (IsActive) End();

            _selectable.Clear();
            if (selectableCards != null)
            {
                for (int i = 0; i < selectableCards.Count; i++)
                    if (selectableCards[i] != null) _selectable.Add(selectableCards[i]);
            }

            if (_selectable.Count == 0 || interaction == null || onChosen == null) return false;

            _interaction = interaction;
            _onChosen = onChosen;

            IsActive = true;
            return true;
        }

        /// <summary>Stops selection and clears any highlight.</summary>
        public void End()
        {
            if (!IsActive) return;

            SetHovered(null);
            _selectable.Clear();
            _onChosen = null;
            _interaction = null;
            IsActive = false;
        }

        void OnDisable()
        {
            if (IsActive) End();
        }

        void Update()
        {
            if (!IsActive) return;

            CardView targeted = TargetedCard();
            SetHovered(targeted);

            if (targeted != null && _interaction.PrimaryPressedThisFrame()) Choose(targeted);
        }

        /// <summary>
        /// Completes selection with one of the selectable cards. The crosshair click
        /// routes here, and so can any other input source or an automated check.
        /// </summary>
        public bool Choose(CardView view)
        {
            if (!IsActive || view == null || !_selectable.Contains(view)) return false;

            PlayingCard chosen = view.Card;
            Action<PlayingCard> callback = _onChosen;

            End();

            if (callback != null) callback(chosen);
            return true;
        }

        /// <summary>
        /// The selectable card the crosshair is on, or null. Any other card, including
        /// every card on another player's area, is ignored.
        /// </summary>
        public CardView TargetedCard()
        {
            if (_interaction == null) return null;

            CardView view = _interaction.Target<CardView>();
            return view != null && _selectable.Contains(view) ? view : null;
        }

        void SetHovered(CardView view)
        {
            if (_hovered == view) return;

            if (_hovered != null) _hovered.SetHighlighted(false);
            _hovered = view;
            if (_hovered != null) _hovered.SetHighlighted(true);
        }
    }
}
