using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Shields.UI
{
    /// <summary>
    /// The SHIELDS front-end look and its widgets: bone-white Bowlby on soft shadow, one
    /// red accent, hand-made ink dashes and rules, hand-lettered Knewave labels, deckled
    /// paper tiles and wax-seal sliders. Readability comes from local darkening, never
    /// from opaque panels, so the tavern stays visible.
    ///
    /// One instance builds one canvas. It keeps the small "bind" callbacks that copy
    /// state onto widgets; call <see cref="Refresh"/> whenever that state changes.
    /// </summary>
    public sealed class TavernUI
    {
        public static readonly Color Bone = UiKit.Hex("#EEE6D6");
        public static readonly Color Dim = UiKit.Hex("#A69E90");
        public static readonly Color Muted = UiKit.Hex("#6A635A");
        public static readonly Color Red = UiKit.Hex("#C9302F");

        /// <summary>The cool night shadow used behind the Customize screen.</summary>
        public static readonly Color NightShade = UiKit.Hex("#05070A");

        public static readonly Color BoxRest = new Color(0, 0, 0, 0.35f);
        public const float BoxHotAlpha = 0.13f;

        const float Tracking = 0.35f;
        const float LineScale = 2.2f, LineMin = 4f;
        const float KickerScale = 1.3f;

        public readonly TMP_FontAsset Bowlby;
        public readonly TMP_FontAsset Knewave;
        public readonly Material Soft;
        public readonly Material Strong;
        readonly Material _kicker;

        readonly List<Action> _binds = new List<Action>();

        public TavernUI(TMP_FontAsset bowlby, TMP_FontAsset knewave)
        {
            Bowlby = bowlby;
            Knewave = knewave;
            Soft = UiKit.TextMat(bowlby, "Tavern_Soft", 0f, default, new Color(0, 0, 0, 0.55f), 0.5f, -0.7f, 0.45f, 0.05f);
            Strong = UiKit.TextMat(bowlby, "Tavern_Strong", 0f, default, new Color(0, 0, 0, 0.75f), 0.8f, -1f, 0.25f, 0.15f);
            _kicker = UiKit.TextMat(knewave, "Tavern_Kicker", 0f, default, new Color(0, 0, 0, 0.75f), 0.5f, -0.6f, 0.25f, 0.06f);
        }

        // ------------------------------------------------------------------ canvas and state

        /// <summary>A 1080p-referenced overlay canvas, scaled by height.</summary>
        public static Canvas MakeCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Makes sure the scene can click UI: adds an Input System event system if there is none.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetAsLastSibling();
        }

        public void Bind(Action bind) { _binds.Add(bind); }

        public void Refresh() { foreach (Action bind in _binds) bind(); }

        // ------------------------------------------------------------------ type and lines

        public static float Sp(float spacing) { return spacing * Tracking; }

        /// <summary>Hand-drawn strokes need more height than a crisp line to read.</summary>
        public static float Thk(float h) { return Mathf.Max(h * LineScale, LineMin); }

        public TextMeshProUGUI T(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool strong = false)
        {
            return UiKit.Text(parent, name, text, Bowlby, size, color, align, strong ? Strong : Soft);
        }

        /// <summary>A small hand-lettered accent label (ROOM CODE, NICKNAME, AUDIO ...).</summary>
        public TextMeshProUGUI Kicker(Transform parent, string name, string text, float size, Color color, float spacing,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            TextMeshProUGUI t = UiKit.Text(parent, name, text, Knewave, size * KickerScale, color, align, _kicker);
            t.characterSpacing = Sp(spacing);
            return t;
        }

        public static Image Line(Transform parent, string name, Color color) { return UiKit.Img(parent, name, UiSprites.InkStroke(false), color); }
        public static Image VLine(Transform parent, string name, Color color) { return UiKit.Img(parent, name, UiSprites.InkStroke(true), color); }
        public static Image Hairline(Transform parent, string name, Color color) { return UiKit.Img(parent, name, UiSprites.InkHairline(), color); }
        public static Sprite Chev { get { return UiSprites.InkChevron(); } }

        public static void Shade(RectTransform parent, string name, float width, Color color)
        {
            Image shade = UiKit.Img(parent, name, UiSprites.FadeRight(), color);
            shade.rectTransform.anchorMin = new Vector2(0, 0);
            shade.rectTransform.anchorMax = new Vector2(0, 1);
            shade.rectTransform.pivot = new Vector2(0, 0.5f);
            shade.rectTransform.anchoredPosition = Vector2.zero;
            shade.rectTransform.sizeDelta = new Vector2(width, 0);
        }

        // ------------------------------------------------------------------ behaviour

        /// <summary>A button whose look is entirely defined by <paramref name="apply"/>.</summary>
        public static Button Btn(Transform parent, string name, Action onClick, out UiFx fx)
        {
            return UiKit.Button(parent, name, onClick, out fx);
        }

        /// <summary>
        /// An invisible driver that runs <paramref name="tick"/> every frame with a smoothed
        /// 0..1 value following <paramref name="target"/>. Used for screen-level animation.
        /// </summary>
        public static UiFx Animator(Transform parent, string name, Func<bool> target, Action<float> tick, float speed = 7f)
        {
            RectTransform rt = UiKit.Node(parent, name);
            var fx = rt.gameObject.AddComponent<UiFx>();
            fx.Speed = speed;
            fx.Apply = f => { f.Selected = target(); tick(UiFx.Ease(f.Select)); };
            return fx;
        }

        // ------------------------------------------------------------------ widgets

        /// <summary>
        /// A menu entry: text that slides right on hover while an ink dash is drawn in front
        /// of it. <paramref name="held"/> keeps it lit (active state).
        /// </summary>
        public Button MenuWord(Transform parent, string label, float size, Action click, Func<bool> held, out UiFx fx)
        {
            Button b = Btn(parent, label, click, out fx);
            TextMeshProUGUI text = T(b.transform, "Label", label, size, Dim, TextAlignmentOptions.MidlineLeft, size > 40);
            text.rectTransform.Fill();
            Image bar = Line(b.transform, "Blade", Red);
            float barH = Thk(size > 40 ? 5 : 4);
            bar.rectTransform.At(0, 0.5f, 1, 0.5f, -16, 0, 0, barH);
            fx.Apply = f =>
            {
                float h = UiFx.Ease(Mathf.Max(f.Hover, held != null && held() ? 1f : 0f));
                text.color = UiKit.Lerp(Dim, Bone, h);
                text.rectTransform.offsetMin = new Vector2(16 * h, 0);
                text.rectTransform.offsetMax = new Vector2(16 * h, 0);
                bar.rectTransform.sizeDelta = new Vector2(30 * h, barH);
                bar.color = Red.A(h);
                float s = 1f - 0.03f * f.Press;
                b.transform.localScale = new Vector3(s, s, 1);
            };
            return b;
        }

        /// <summary>A plain text action with a chevron that slides in on hover. Dims when not interactable.</summary>
        public RectTransform TextLink(Transform parent, string name, string label, float size, bool primary, Action click, out UiFx fx)
        {
            Button b = Btn(parent, name, click, out fx);
            TextMeshProUGUI text = T(b.transform, "Label", label, size, primary ? Bone : Dim, TextAlignmentOptions.MidlineLeft, primary);
            text.rectTransform.Fill();
            Image chev = UiKit.Img(b.transform, "Chevron", Chev, Red);
            float w = text.GetPreferredValues(label).x;
            chev.rectTransform.At(0, 0.5f, 0, 0.5f, w + 12, 0, size * 0.36f, size * 0.54f);
            fx.Apply = f =>
            {
                float h = UiFx.Ease(f.Hover);
                bool on = f.IsActive;
                text.color = !on ? Muted : primary ? Bone : UiKit.Lerp(Dim, Bone, h);
                chev.color = Red.A(on ? (primary ? 0.6f + 0.4f * h : h) : 0f);
                chev.rectTransform.anchoredPosition = new Vector2(w + 12 + 8 * h, 0);
            };
            return b.GetComponent<RectTransform>();
        }

        public RectTransform TextLink(Transform parent, string name, string label, float size, bool primary, Action click)
        {
            return TextLink(parent, name, label, size, primary, click, out _);
        }

        /// <summary>Width of a text link's label, for placing what comes after it.</summary>
        public static float LinkWidth(RectTransform link)
        {
            var label = link.Find("Label").GetComponent<TextMeshProUGUI>();
            return label.GetPreferredValues(label.text).x;
        }

        /// <summary>A text tab / choice with an ink underline that draws in when selected.</summary>
        public UiFx TabWord(RectTransform parent, string label, float x, float y, float size, Action click)
        {
            Button b = Btn(parent, label, click, out UiFx fx);
            TextMeshProUGUI text = T(b.transform, "Label", label, size, Muted, TextAlignmentOptions.MidlineLeft, size > 40);
            text.characterSpacing = Sp(size < 40 ? 4 : 0);
            float tw = text.GetPreferredValues(label).x;
            b.GetComponent<RectTransform>().TL(x, y, tw + 8, size * 1.6f);
            text.rectTransform.Fill();
            Image line = Line(b.transform, "Underline", Red);
            float lineH = Thk(size < 40 ? 3 : 4);
            line.rectTransform.At(0, 0, 0, 0.5f, 0, 2, tw, lineH);
            fx.Apply = f =>
            {
                float s = UiFx.Ease(f.Select), h = UiFx.Ease(f.Hover);
                text.color = UiKit.Lerp(UiKit.Lerp(Muted, Dim, h), Bone, s);
                line.rectTransform.sizeDelta = new Vector2(tw * s, lineH);
            };
            return fx;
        }

        /// <summary>BACK with a reversed chevron, top-left of a screen.</summary>
        public RectTransform BackLink(Transform parent, Action click)
        {
            Button b = Btn(parent, "Back", click, out UiFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(76, 56, 200, 48);
            Image chev = UiKit.Img(b.transform, "Chevron", Chev, Red);
            chev.rectTransform.At(0, 0.5f, 0, 0.5f, 0, 0, 14, 21);
            chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            TextMeshProUGUI text = T(b.transform, "Label", "BACK", 26, Dim);
            text.rectTransform.Fill(30, 0, 0, 0);
            text.characterSpacing = Sp(4);
            fx.Apply = f =>
            {
                float h = UiFx.Ease(f.Hover);
                text.color = UiKit.Lerp(Dim, Bone, h);
                chev.rectTransform.anchoredPosition = new Vector2(14 - 6 * h, 0);
            };
            return rt;
        }
    }
}
