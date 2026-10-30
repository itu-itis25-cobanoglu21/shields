using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UI
{
    /// <summary>
    /// Small helpers for building the front-end UI from code: rect placement, images,
    /// text, cached text materials, and the few controls the menus need.
    /// </summary>
    public static class UiKit
    {
        // ------------------------------------------------------------------ rects

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor at one point (0..1 in the parent), place by pivot.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float px, float py, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(px, py);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Top-left anchored, positioned from the parent's top-left corner (y grows downward).</summary>
        public static RectTransform TL(this RectTransform rt, float x, float y, float w, float h)
        {
            return rt.At(0, 1, 0, 1, x, -y, w, h);
        }

        public static RectTransform Fill(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // ------------------------------------------------------------------ graphics

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static RawImage Raw(Transform parent, string name, Texture tex, Color color)
        {
            RectTransform rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Material material = null)
        {
            RectTransform rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        // ------------------------------------------------------------------ text materials

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>
        /// A cached copy of the font's material with outline / underlay settings.
        /// Pass alpha 0 colours to leave an effect off.
        /// </summary>
        public static Material TextMat(TMP_FontAsset font, string key,
            float outline = 0f, Color outlineColor = default,
            Color underlay = default, float underlayX = 0f, float underlayY = 0f, float underlaySoft = 0f, float underlayDilate = 0f,
            float faceDilate = 0f)
        {
            string id = font.name + "/" + key;
            if (Materials.TryGetValue(id, out Material cached) && cached != null) return cached;

            var m = new Material(font.material) { name = font.name + " " + key };
            m.SetFloat("_FaceDilate", faceDilate);
            if (outline > 0f)
            {
                m.EnableKeyword("OUTLINE_ON");
                m.SetFloat("_OutlineWidth", outline);
                m.SetColor("_OutlineColor", outlineColor);
            }
            if (underlay.a > 0f)
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", underlay);
                m.SetFloat("_UnderlayOffsetX", underlayX);
                m.SetFloat("_UnderlayOffsetY", underlayY);
                m.SetFloat("_UnderlaySoftness", underlaySoft);
                m.SetFloat("_UnderlayDilate", underlayDilate);
            }
            Materials[id] = m;
            return m;
        }

        // ------------------------------------------------------------------ controls

        /// <summary>A clickable rect: transparent raycast image, Button, and a <see cref="UiFx"/>.</summary>
        public static Button Button(Transform parent, string name, Action onClick, out UiFx fx)
        {
            RectTransform rt = Node(parent, name);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            fx = rt.gameObject.AddComponent<UiFx>();
            return button;
        }

        public struct SliderParts
        {
            public Slider Slider;
            public Image Track;
            public Image Fill;
            public Image Handle;
            public UiFx Fx;
        }

        public static SliderParts Slider(Transform parent, string name, float min, float max, float value, Action<float> onChange,
            Sprite track, Sprite fill, Sprite handle, float trackHeight, Vector2 handleSize)
        {
            RectTransform root = Node(parent, name);
            var parts = new SliderParts();

            parts.Track = Img(root, "Track", track, Color.white, true);
            parts.Track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            parts.Track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            parts.Track.rectTransform.sizeDelta = new Vector2(0, trackHeight);

            RectTransform fillArea = Node(root, "FillArea");
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, trackHeight);
            parts.Fill = Img(fillArea, "Fill", fill, Color.white);
            parts.Fill.rectTransform.Fill();

            RectTransform handleArea = Node(root, "HandleArea");
            handleArea.Fill();
            parts.Handle = Img(handleArea, "Handle", handle, Color.white, true);
            parts.Handle.rectTransform.anchorMin = new Vector2(0, 0.5f);
            parts.Handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
            parts.Handle.rectTransform.sizeDelta = handleSize;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;
            slider.fillRect = parts.Fill.rectTransform;
            slider.handleRect = parts.Handle.rectTransform;
            slider.targetGraphic = parts.Handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            parts.Slider = slider;
            parts.Fx = root.gameObject.AddComponent<UiFx>();
            return parts;
        }

        public struct ScrollParts
        {
            public ScrollRect Scroll;
            public RectTransform Viewport;
            public RectTransform Content;
            public Scrollbar Bar;
        }

        /// <summary>
        /// A clipped, wheel- and drag-scrollable vertical area. The root is sized by the
        /// caller; the scrollbar is a separate child the caller positions. Content is
        /// anchored to the top-left; the caller sets its height.
        /// </summary>
        public static ScrollParts VerticalScroll(Transform parent, string name, Color trackColor, Color handleColor)
        {
            var parts = new ScrollParts();
            RectTransform root = Node(parent, name);

            parts.Viewport = Node(root, "Viewport");
            parts.Viewport.Fill();
            parts.Viewport.gameObject.AddComponent<RectMask2D>();
            var hit = parts.Viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            parts.Content = Node(parts.Viewport, "Content");
            parts.Content.anchorMin = new Vector2(0, 1);
            parts.Content.anchorMax = new Vector2(1, 1);
            parts.Content.pivot = new Vector2(0, 1);
            parts.Content.anchoredPosition = Vector2.zero;
            parts.Content.sizeDelta = Vector2.zero;

            // The bar's own rect is an invisible hit area; a thin track runs down its middle.
            RectTransform barRt = Node(root, "Scrollbar");
            barRt.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Image track = Img(barRt, "Track", UiSprites.White(), trackColor);
            track.rectTransform.anchorMin = new Vector2(0.5f, 0);
            track.rectTransform.anchorMax = new Vector2(0.5f, 1);
            track.rectTransform.sizeDelta = new Vector2(2, 0);
            RectTransform area = Node(barRt, "SlidingArea");
            area.Fill();
            Image handle = Img(area, "Handle", UiSprites.RoundRect(2), handleColor, true);
            handle.rectTransform.Fill();
            parts.Bar = barRt.gameObject.AddComponent<Scrollbar>();
            parts.Bar.handleRect = handle.rectTransform;
            parts.Bar.targetGraphic = handle;
            parts.Bar.transition = Selectable.Transition.None;
            var nav = parts.Bar.navigation; nav.mode = Navigation.Mode.None; parts.Bar.navigation = nav;
            parts.Bar.direction = Scrollbar.Direction.BottomToTop;

            parts.Scroll = root.gameObject.AddComponent<ScrollRect>();
            parts.Scroll.viewport = parts.Viewport;
            parts.Scroll.content = parts.Content;
            parts.Scroll.vertical = true;
            parts.Scroll.horizontal = false;
            parts.Scroll.movementType = ScrollRect.MovementType.Clamped;
            parts.Scroll.inertia = true;
            parts.Scroll.decelerationRate = 0.08f;
            parts.Scroll.scrollSensitivity = 40f;
            parts.Scroll.verticalScrollbar = parts.Bar;
            parts.Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return parts;
        }

        public static TMP_InputField Input(Transform parent, string name, TMP_FontAsset font, float size, Color textColor,
            string placeholder, Color placeholderColor, TextAlignmentOptions align, Material textMaterial = null)
        {
            RectTransform root = Node(parent, name);
            root.gameObject.SetActive(false);

            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);

            RectTransform area = Node(root, "TextArea");
            area.Fill();
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI ph = Text(area, "Placeholder", placeholder, font, size, placeholderColor, align, textMaterial);
            ph.rectTransform.Fill();
            TextMeshProUGUI txt = Text(area, "Text", string.Empty, font, size, textColor, align, textMaterial);
            txt.rectTransform.Fill();

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.transition = Selectable.Transition.None;
            var nav = input.navigation; nav.mode = Navigation.Mode.None; input.navigation = nav;
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = font;
            input.pointSize = size;
            input.customCaretColor = true;
            input.caretColor = textColor;
            input.caretWidth = 3;
            input.selectionColor = new Color(textColor.r, textColor.g, textColor.b, 0.25f);
            input.richText = false;
            input.lineType = TMP_InputField.LineType.SingleLine;

            root.gameObject.SetActive(true);
            return input;
        }

        // ------------------------------------------------------------------ misc

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c);
            c.a = alpha;
            return c;
        }

        public static Color A(this Color c, float alpha) { c.a = alpha; return c; }

        public static Color Lerp(Color a, Color b, float t) { return Color.LerpUnclamped(a, b, t); }

        public static void SetActive(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }
    }
}
