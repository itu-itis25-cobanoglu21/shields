using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// A low-poly flame for the HUD: three nested faceted tongues (ember red, orange,
    /// pale gold), each a fan of flat-shaded triangles whose points drift on smooth noise
    /// so the fire flickers without ever looking like a sprite loop. Vector geometry, so it
    /// stays crisp at any size. Fills its rect: the base sits on the bottom edge, the tip
    /// reaches the top. Presentation only.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudFlame : MaskableGraphic
    {
        // Outline of one tongue in a unit box (x -0.5..0.5, y 0..1), counter-clockwise from
        // the bottom centre. Every point drifts a little; the upper ones drift more.
        static readonly Vector2[] Outline =
        {
            new Vector2(0.00f, 0.00f), new Vector2(0.30f, 0.04f), new Vector2(0.44f, 0.24f),
            new Vector2(0.38f, 0.50f), new Vector2(0.22f, 0.68f), new Vector2(0.06f, 1.00f),
            new Vector2(-0.12f, 0.72f), new Vector2(-0.34f, 0.54f), new Vector2(-0.44f, 0.28f),
            new Vector2(-0.30f, 0.05f)
        };

        static readonly Color Ember = new Color(0.62f, 0.12f, 0.07f);
        static readonly Color Orange = new Color(0.93f, 0.40f, 0.10f);
        static readonly Color Gold = new Color(1.00f, 0.80f, 0.36f);

        [Range(0f, 1f)] public float Intensity = 1f;
        public float Speed = 1f;

        float _seed;

        protected override void Awake()
        {
            base.Awake();
            _seed = Random.value * 50f;
            raycastTarget = false;
        }

        void Update()
        {
            if (Intensity > 0.001f) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Intensity <= 0.001f) return;

            Rect r = GetPixelAdjustedRect();
            float t = (Application.isPlaying ? Time.unscaledTime : 0f) * Speed;

            // Outer to inner: each smaller, lifted a little, and swaying on its own phase.
            Tongue(vh, r, t, 0.0f, 1.00f, 1.00f, 0.00f, Ember, 1.00f);
            Tongue(vh, r, t, 3.1f, 0.70f, 0.80f, 0.05f, Orange, 1.15f);
            Tongue(vh, r, t, 6.7f, 0.40f, 0.52f, 0.08f, Gold, 1.35f);
        }

        void Tongue(VertexHelper vh, Rect r, float t, float phase, float width, float height, float lift, Color baseColor, float sway)
        {
            int n = Outline.Length;
            var pts = new Vector2[n];
            float heightPulse = 1f + 0.08f * (Mathf.PerlinNoise(_seed + phase, t * 2.2f) - 0.5f) * 2f;
            float lean = (Mathf.PerlinNoise(_seed + phase + 9f, t * 1.3f) - 0.5f) * 0.22f * sway;

            for (int i = 0; i < n; i++)
            {
                Vector2 p = Outline[i];
                float up = p.y;
                float jx = (Mathf.PerlinNoise(_seed + phase + i * 1.7f, t * 3.1f) - 0.5f) * 0.10f * (0.3f + up);
                float jy = (Mathf.PerlinNoise(_seed + phase + i * 2.3f + 40f, t * 2.7f) - 0.5f) * 0.08f * up;
                float x = (p.x + jx + lean * up * up) * width;
                float y = lift + (p.y + jy) * height * heightPulse * Intensity;
                pts[i] = new Vector2(r.xMin + (x + 0.5f) * r.width, r.yMin + Mathf.Clamp01(y) * r.height);
            }

            // Fan from a point inside the tongue; each facet gets its own shade so the
            // flame reads as cut geometry rather than a flat shape.
            Vector2 centre = new Vector2(r.xMin + (0.5f + lean * 0.3f * width) * r.width, r.yMin + (lift + 0.30f * height * Intensity) * r.height);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                float facing = Mathf.Sin(i * 2.1f + phase) * 0.5f + 0.5f;
                float flick = Mathf.PerlinNoise(_seed + i * 0.9f + phase, t * 4f);
                Color c = Color.Lerp(baseColor * 0.82f, Color.Lerp(baseColor, Color.white, 0.18f), facing * 0.7f + flick * 0.3f);
                c.a = baseColor.a * color.a;
                c.r *= color.r; c.g *= color.g; c.b *= color.b;
                int start = vh.currentVertCount;
                vh.AddVert(centre, c, Vector2.zero);
                vh.AddVert(a, c, Vector2.zero);
                vh.AddVert(b, c, Vector2.zero);
                vh.AddTriangle(start, start + 2, start + 1);
            }
        }
    }
}
