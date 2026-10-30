using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shields.UI
{
    /// <summary>
    /// Smoothed hover / press / selected values for one UI element. Each widget passes
    /// its own <see cref="Apply"/> callback that turns these values into a look. Values
    /// move toward absolute targets every frame, so nothing drifts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiFx : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Action<UiFx> Apply;

        [NonSerialized] public float Hover;
        [NonSerialized] public float Press;
        [NonSerialized] public float Select;

        public bool Selected;
        public bool Interactable = true;
        public float Speed = 9f;

        bool _hovered;
        bool _pressed;
        Selectable _selectable;

        public bool IsActive { get { return Interactable && (_selectable == null || _selectable.IsInteractable()); } }

        void Awake() { _selectable = GetComponent<Selectable>(); }

        void OnEnable() { Snap(); }

        void OnDisable() { _hovered = false; _pressed = false; }

        public void OnPointerEnter(PointerEventData e) { _hovered = true; }
        public void OnPointerExit(PointerEventData e) { _hovered = false; _pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _pressed = true; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }

        /// <summary>Jump to the current targets, for screens that just opened.</summary>
        public void Snap()
        {
            Hover = IsActive && _hovered ? 1f : 0f;
            Press = 0f;
            Select = Selected ? 1f : 0f;
            Apply?.Invoke(this);
        }

        void Update()
        {
            bool active = IsActive;
            float dt = Time.unscaledDeltaTime * Speed;
            Hover = Mathf.MoveTowards(Hover, active && _hovered ? 1f : 0f, dt);
            Press = Mathf.MoveTowards(Press, active && _pressed ? 1f : 0f, dt * 2f);
            Select = Mathf.MoveTowards(Select, Selected ? 1f : 0f, dt);
            Apply?.Invoke(this);
        }

        /// <summary>Smooth 0..1 ease for the raw values.</summary>
        public static float Ease(float t) { return t * t * (3f - 2f * t); }
    }
}
