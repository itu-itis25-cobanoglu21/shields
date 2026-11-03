using Shields.Online;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// Base for one in-game HUD direction. A concept builds its own canvas once and then
    /// mirrors the <see cref="HudModel"/> every frame. Prototype only.
    /// </summary>
    public abstract class HudConcept
    {
        public abstract string Code { get; }
        public Canvas Canvas { get; private set; }

        protected HudConceptDirector D;
        protected TavernUI UI;
        protected RectTransform Root;

        protected static readonly Color Bone = TavernUI.Bone, Dim = TavernUI.Dim, Muted = TavernUI.Muted, Red = TavernUI.Red;

        public void Init(HudConceptDirector director, Canvas canvas, TavernUI ui)
        {
            D = director;
            Canvas = canvas;
            UI = ui;
            Root = (RectTransform)canvas.transform;
            Build();
        }

        protected abstract void Build();
        public abstract void Tick(HudModel m);

        /// <summary>The table started presenting an action. Only actor, target and type are public yet.</summary>
        public virtual void ActionStarted(PublicActionResult r, int local) { }

        /// <summary>The action's card was revealed on the table: the whole result is public now.</summary>
        public virtual void ActionRevealed(PublicActionResult r, int local) { }

        // ------------------------------------------------------------------ helpers

        protected static Image Img(Transform parent, string name, Sprite sprite, Color color) { return UiKit.Img(parent, name, sprite, color); }

        protected TextMeshProUGUI T(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool strong = false)
        {
            return UI.T(parent, name, text, size, color, align, strong);
        }

        protected TextMeshProUGUI K(Transform parent, string name, string text, float size, Color color, float spacing,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            return UI.Kicker(parent, name, text, size, color, spacing, align);
        }

        protected static void Show(Component c, bool on) { UiKit.SetActive(c, on); }

        /// <summary>Where a seat's head is on this canvas, if it is in front of the camera.</summary>
        protected bool Project(int seat, out Vector2 local) { return D.ProjectHead(seat, Root, out local); }

        /// <summary>0..1 visibility for something shown at <paramref name="shownAt"/> for <paramref name="life"/> seconds.</summary>
        protected static float Life(float shownAt, float life, float fadeIn = 0.2f, float fadeOut = 0.6f)
        {
            float age = Time.unscaledTime - shownAt;
            if (age < 0f || age > life) return 0f;
            return Mathf.Min(Mathf.Clamp01(age / fadeIn), Mathf.Clamp01((life - age) / fadeOut));
        }

        protected static float Sp(float spacing) { return TavernUI.Sp(spacing); }
        protected static float Thk(float h) { return TavernUI.Thk(h); }

        /// <summary>Glyph for the turn direction.</summary>
        protected static string DirectionText(HudModel m)
        {
            return m.Snap.Direction == Shields.Game.TurnDirection.Clockwise ? "CLOCKWISE" : "COUNTER-CLOCKWISE";
        }
    }
}
