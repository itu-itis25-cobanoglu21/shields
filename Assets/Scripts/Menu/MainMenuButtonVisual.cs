using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shields.Menu
{
    /// <summary>
    /// Hover and press feedback for a text-only main menu button: the label brightens,
    /// gains a faint warm halo and scales up a touch; pressing dips it slightly. An
    /// optional marker (the small diamond left of the label) fades and slides in with
    /// the highlight.
    ///
    /// Scale and colour are always rebuilt from absolute targets and move with
    /// MoveTowards, so they land exactly on 1.00 / the normal colour and never drift.
    /// A non-interactable button shows its disabled colour and ignores the pointer.
    ///
    /// Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public class MainMenuButtonVisual : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] TMP_Text label;

        [Header("Colours")]
        [SerializeField] Color normalColor = new Color32(0xC7, 0xC4, 0xBC, 0xFF);
        [SerializeField] Color hoverColor = new Color32(0xFF, 0xFD, 0xF6, 0xFF);
        [SerializeField] Color disabledColor = new Color32(0x6E, 0x6B, 0x66, 0xFF);

        [Header("Scale")]
        [SerializeField] float hoverScale = 1.04f;
        [SerializeField] float pressedScale = 0.99f;

        [Tooltip("Seconds to go from normal to hover.")]
        [SerializeField] float transitionSeconds = 0.12f;

        [Header("Glow")]
        [Tooltip("Halo colour behind the label on hover. Alpha is the strongest it gets.")]
        [SerializeField] Color glowColor = new Color(1f, 0.78f, 0.5f, 0.35f);

        [Header("Hover Marker")]
        [Tooltip("Shown beside the label while highlighted, in the hover colour. Leave empty for none.")]
        [SerializeField] Graphic hoverMarker;

        [Tooltip("How far right of its resting place the marker starts as it fades in.")]
        [SerializeField] float markerSlide = 6f;

        static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");

        Selectable _selectable;
        Material _labelMaterial;
        bool _hovered;
        bool _pressed;
        float _highlight;
        float _scale = 1f;
        Vector2 _markerRest;

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();

            if (hoverMarker != null)
            {
                hoverMarker.raycastTarget = false;
                _markerRest = hoverMarker.rectTransform.anchoredPosition;
            }

            // Own material instance, so only this label's halo changes.
            if (label != null && label.fontSharedMaterial != null
                && label.fontSharedMaterial.HasProperty(UnderlayColorId))
                _labelMaterial = label.fontMaterial;

            Apply();
        }

        void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            _highlight = 0f;
            _scale = 1f;
            Apply();
        }

        void OnDestroy()
        {
            if (_labelMaterial != null) Destroy(_labelMaterial);
        }

        bool Interactable { get { return _selectable != null && _selectable.IsInteractable(); } }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; }

        public void OnPointerExit(PointerEventData eventData) { _hovered = false; _pressed = false; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData) { _pressed = false; }

        void Update()
        {
            bool active = Interactable;
            float targetHighlight = active && (_hovered || _pressed) ? 1f : 0f;
            float targetScale = !active ? 1f : _pressed ? pressedScale : _hovered ? hoverScale : 1f;

            float duration = Mathf.Max(0.01f, transitionSeconds);
            float dt = Time.unscaledDeltaTime;

            _highlight = Mathf.MoveTowards(_highlight, targetHighlight, dt / duration);

            // Covers the full press-to-hover distance in one transition time.
            float scaleSpeed = Mathf.Max(hoverScale - 1f, 1f - pressedScale, hoverScale - pressedScale) / duration;
            _scale = Mathf.MoveTowards(_scale, targetScale, scaleSpeed * dt);

            Apply();
        }

        void Apply()
        {
            transform.localScale = new Vector3(_scale, _scale, 1f);

            bool active = _selectable == null || _selectable.IsInteractable();
            ApplyMarker(active ? _highlight : 0f);

            if (label == null) return;

            label.color = active ? Color.Lerp(normalColor, hoverColor, _highlight) : disabledColor;

            if (_labelMaterial != null)
            {
                Color glow = glowColor;
                glow.a *= active ? _highlight : 0f;
                _labelMaterial.SetColor(UnderlayColorId, glow);
            }
        }

        // Rebuilt from the resting position every frame, like scale, so it cannot drift.
        void ApplyMarker(float amount)
        {
            if (hoverMarker == null) return;

            Color color = hoverColor;
            color.a *= amount;
            hoverMarker.color = color;
            hoverMarker.enabled = amount > 0f;
            hoverMarker.rectTransform.anchoredPosition = _markerRest + new Vector2(markerSlide * (1f - amount), 0f);
        }
    }
}
