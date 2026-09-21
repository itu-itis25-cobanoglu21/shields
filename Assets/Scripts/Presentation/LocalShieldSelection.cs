using System;
using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
using Shields.Players;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Shields.Presentation
{
    /// <summary>
    /// Lets the local player pick their Shield by clicking one of their three dealt
    /// cards. While this is running the seated head look is switched off and the cursor
    /// is released, so camera look and card clicking never fight over the mouse. Normal
    /// first person control resumes the moment a card is chosen.
    ///
    /// This only reports which card was clicked. Whether that choice is legal is decided
    /// by the authoritative match state.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalShieldSelection : MonoBehaviour
    {
        [Tooltip("How far a click ray reaches, in meters.")]
        [SerializeField] float rayDistance = 5f;

        readonly List<CardView> _selectable = new List<CardView>();

        Camera _camera;
        SeatedHeadLook _look;
        Action<PlayingCard> _onChosen;
        CardView _hovered;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Starts selection over the given card views. Returns false when there is
        /// nothing to click, in which case the caller should resolve the choice itself.
        /// </summary>
        public bool Begin(Camera gameplayCamera, SeatedHeadLook localLook,
            IReadOnlyList<CardView> selectableCards, Action<PlayingCard> onChosen)
        {
            if (IsActive) End();

            _selectable.Clear();
            if (selectableCards != null)
            {
                for (int i = 0; i < selectableCards.Count; i++)
                    if (selectableCards[i] != null) _selectable.Add(selectableCards[i]);
            }

            if (_selectable.Count == 0 || gameplayCamera == null || onChosen == null) return false;

            _camera = gameplayCamera;
            _look = localLook;
            _onChosen = onChosen;

            // Hand the mouse over to the cards for the duration of the choice.
            if (_look != null) _look.enabled = false;
            SetCursorFree(true);

            IsActive = true;
            return true;
        }

        /// <summary>Stops selection and gives the mouse back to the seated camera.</summary>
        public void End()
        {
            if (!IsActive) return;

            SetHovered(null);
            _selectable.Clear();
            _onChosen = null;
            IsActive = false;

            SetCursorFree(false);
            if (_look != null) _look.enabled = true;

            _look = null;
            _camera = null;
        }

        void OnDisable()
        {
            if (IsActive) End();
        }

        void Update()
        {
            if (!IsActive) return;

            CardView under = CardUnderPointer();
            SetHovered(under);

            if (under != null && PointerClickedThisFrame()) Choose(under);
        }

        /// <summary>
        /// Completes selection with one of the selectable cards. Clicking routes here,
        /// and so can any other input source or an automated check.
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

        /// <summary>The selectable card currently under the pointer, or null.</summary>
        public CardView CardUnderPointer()
        {
            if (_camera == null) return null;

            Vector2 pointer;
            if (!TryGetPointerPosition(out pointer)) return null;

            return CardAtScreenPoint(pointer);
        }

        /// <summary>The selectable card at a screen position, or null. Pointer independent.</summary>
        public CardView CardAtScreenPoint(Vector2 screenPoint)
        {
            if (_camera == null) return null;

            Ray ray = _camera.ScreenPointToRay(screenPoint);

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, rayDistance)) return null;

            CardView view = hit.collider.GetComponentInParent<CardView>();
            if (view == null) return null;

            // Only this player's own dealt cards are clickable.
            return _selectable.Contains(view) ? view : null;
        }

        // ---------------------------------------------------------------- pointer

        void SetHovered(CardView view)
        {
            if (_hovered == view) return;

            if (_hovered != null) _hovered.SetHighlighted(false);
            _hovered = view;
            if (_hovered != null) _hovered.SetHighlighted(true);
        }

        static bool TryGetPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return true;
#else
            position = Vector2.zero;
            return false;
#endif
        }

        static bool PointerClickedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.leftButton.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
    }
}
