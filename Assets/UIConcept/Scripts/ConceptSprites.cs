using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shields.UIConcept
{
    /// <summary>
    /// Procedural, anti-aliased sprites for the UI concepts, drawn from signed distance
    /// functions at startup. Nothing is written to disk; every sprite is cached by key.
    /// White shapes, so Image.color tints them.
    /// </summary>
    public static class ConceptSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // Distance in pixels, negative inside.
        delegate float Sdf(float x, float y);

        static Sprite Get(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            sprite = make();
            Cache[key] = sprite;
            return sprite;
        }

        static Sprite Paint(string name, int w, int h, Vector4 border, Sdf sdf, float feather = 1f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = sdf(x + 0.5f, y + 0.5f);
                    float a = Mathf.Clamp01(0.5f - d / feather);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        // ------------------------------------------------------------------ primitives

        static float Box(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px - cx) - (hx - r);
            float qy = Mathf.Abs(py - cy) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        // Rectangle with 45° cut corners of size c.
        static float Chamfer(float px, float py, float cx, float cy, float hx, float hy, float c)
        {
            float dx = Mathf.Abs(px - cx), dy = Mathf.Abs(py - cy);
            float box = Mathf.Max(dx - hx, dy - hy);
            float cut = (dx + dy - (hx + hy - c)) * 0.70710678f;
            return Mathf.Max(box, cut);
        }

        static float Circle(float px, float py, float cx, float cy, float r)
        {
            float dx = px - cx, dy = py - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float Segment(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float Union(float a, float b) { return Mathf.Min(a, b); }
        static float Ring(float d, float thickness) { return Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f; }

        // ------------------------------------------------------------------ sliced panels

        public static Sprite RoundRect(int radius)
        {
            return Get("rr" + radius, () =>
            {
                int s = radius * 2 + 4;
                return Paint("RoundRect", s, s, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1),
                    (x, y) => Box(x, y, s * 0.5f, s * 0.5f, s * 0.5f - 1f, s * 0.5f - 1f, radius));
            });
        }

        public static Sprite RoundRectOutline(int radius, int thickness)
        {
            return Get("rro" + radius + "_" + thickness, () =>
            {
                int s = radius * 2 + 4;
                return Paint("RoundRectOutline", s, s, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1),
                    (x, y) => Ring(Box(x, y, s * 0.5f, s * 0.5f, s * 0.5f - 1f, s * 0.5f - 1f, radius), thickness));
            });
        }

        public static Sprite ChamferRect(int cut)
        {
            return Get("ch" + cut, () =>
            {
                int s = cut * 2 + 6;
                return Paint("Chamfer", s, s, new Vector4(cut + 2, cut + 2, cut + 2, cut + 2),
                    (x, y) => Chamfer(x, y, s * 0.5f, s * 0.5f, s * 0.5f - 1f, s * 0.5f - 1f, cut));
            });
        }

        public static Sprite ChamferOutline(int cut, int thickness)
        {
            return Get("cho" + cut + "_" + thickness, () =>
            {
                int s = cut * 2 + 6;
                return Paint("ChamferOutline", s, s, new Vector4(cut + 2, cut + 2, cut + 2, cut + 2),
                    (x, y) => Ring(Chamfer(x, y, s * 0.5f, s * 0.5f, s * 0.5f - 1f, s * 0.5f - 1f, cut), thickness));
            });
        }

        // ------------------------------------------------------------------ fills and fades

        public static Sprite White()
        {
            return Get("white", () => Paint("White", 4, 4, Vector4.zero, (x, y) => -10f));
        }

        /// <summary>Horizontal fade: opaque on the left, transparent on the right.</summary>
        public static Sprite FadeRight()
        {
            return Get("fadeR", () =>
            {
                const int w = 256;
                var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < w; x++)
                {
                    float t = x / (w - 1f);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, t);
                    tex.SetPixel(x, 0, new Color(1, 1, 1, a));
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>Vertical fade: opaque at the bottom, transparent at the top.</summary>
        public static Sprite FadeUp()
        {
            return Get("fadeU", () =>
            {
                const int h = 256;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                {
                    float t = y / (h - 1f);
                    tex.SetPixel(0, y, new Color(1, 1, 1, 1f - Mathf.SmoothStep(0f, 1f, t)));
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>Transparent centre, dark edges.</summary>
        public static Sprite Vignette()
        {
            return Get("vig", () =>
            {
                const int s = 256;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x / (s - 1f) - 0.5f) * 2f, dy = (y / (s - 1f) - 0.5f) * 2f;
                        float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.55f, 1.45f, d)));
                    }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>Soft round glow, brightest in the middle.</summary>
        public static Sprite Glow()
        {
            return Get("glow", () =>
            {
                const int s = 128;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x / (s - 1f) - 0.5f) * 2f, dy = (y / (s - 1f) - 0.5f) * 2f;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = (1f - d) * (1f - d);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ------------------------------------------------------------------ icons

        public static Sprite CircleShape()
        {
            return Get("circle", () => Paint("Circle", 128, 128, Vector4.zero, (x, y) => Circle(x, y, 64, 64, 62)));
        }

        public static Sprite RingShape(int thickness)
        {
            return Get("ring" + thickness, () => Paint("Ring", 128, 128, Vector4.zero, (x, y) => Ring(Circle(x, y, 64, 64, 62), thickness)));
        }

        public static Sprite DiamondShape()
        {
            return Get("diamond", () => Paint("Diamond", 64, 64, Vector4.zero,
                (x, y) => (Mathf.Abs(x - 32) + Mathf.Abs(y - 32) - 30f) * 0.7071f));
        }

        /// <summary>Heater shield: flat top, curved sides meeting in a point.</summary>
        public static Sprite ShieldShape()
        {
            return Get("shield", () => Paint("Shield", 112, 128, Vector4.zero, ShieldSdf));
        }

        public static Sprite ShieldOutline(int thickness)
        {
            return Get("shieldo" + thickness, () => Paint("ShieldOutline", 112, 128, Vector4.zero, (x, y) => Ring(ShieldSdf(x, y), thickness)));
        }

        static float ShieldSdf(float x, float y)
        {
            // Top box part plus a pointed bottom made of two large circles.
            float top = Box(x, y, 56, 92, 52, 34, 6);
            float left = Circle(x, y, 108, 92, 102);
            float right = Circle(x, y, 4, 92, 102);
            float bottom = Mathf.Max(Mathf.Max(left, right), y - 92);
            return Mathf.Min(top, bottom);
        }

        public static Sprite Lock()
        {
            return Get("lock", () => Paint("Lock", 96, 112, Vector4.zero, (x, y) =>
            {
                float body = Box(x, y, 48, 36, 38, 32, 8);
                float shackle = Ring(Box(x, y, 48, 76, 26, 30, 24), 10);
                shackle = Mathf.Max(shackle, 60 - y);
                float hole = Union(Circle(x, y, 48, 42, 8), Box(x, y, 48, 30, 3.5f, 10, 1));
                return Mathf.Max(Union(body, shackle), -hole);
            }));
        }

        public static Sprite Check()
        {
            return Get("check", () => Paint("Check", 96, 96, Vector4.zero, (x, y) =>
                Union(Segment(x, y, 14, 50, 38, 24, 8), Segment(x, y, 38, 24, 84, 74, 8))));
        }

        public static Sprite Cross()
        {
            return Get("cross", () => Paint("Cross", 96, 96, Vector4.zero, (x, y) =>
                Union(Segment(x, y, 20, 20, 76, 76, 8), Segment(x, y, 20, 76, 76, 20, 8))));
        }

        /// <summary>Chevron pointing right.</summary>
        public static Sprite Chevron()
        {
            return Get("chev", () => Paint("Chevron", 64, 96, Vector4.zero, (x, y) =>
                Union(Segment(x, y, 16, 14, 48, 48, 7), Segment(x, y, 48, 48, 16, 82, 7))));
        }

        /// <summary>Slashed circle, the "nothing equipped" mark.</summary>
        public static Sprite NoneMark()
        {
            return Get("none", () => Paint("None", 112, 112, Vector4.zero, (x, y) =>
                Union(Ring(Circle(x, y, 56, 56, 46), 9), Segment(x, y, 24, 24, 88, 88, 4.5f))));
        }

        /// <summary>Small pencil, for editable fields.</summary>
        public static Sprite Pencil()
        {
            return Get("pencil", () => Paint("Pencil", 96, 96, Vector4.zero, (x, y) =>
                Union(Segment(x, y, 30, 30, 78, 78, 10), Segment(x, y, 14, 14, 24, 24, 4))));
        }

        // ------------------------------------------------------------------ worn cloth

        static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f * 0.5f;
        }

        static float Noise1(float t, int seed)
        {
            int i = Mathf.FloorToInt(t);
            float f = t - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i + seed * 131), Hash(i + 1 + seed * 131), f);
        }

        static float Fbm1(float t, int seed)
        {
            return Noise1(t, seed) * 0.55f + Noise1(t * 2.7f, seed + 7) * 0.3f + Noise1(t * 7.3f, seed + 13) * 0.15f;
        }

        static float Noise2(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix + iy * 57 + seed * 977), b = Hash(ix + 1 + iy * 57 + seed * 977);
            float c = Hash(ix + (iy + 1) * 57 + seed * 977), d = Hash(ix + 1 + (iy + 1) * 57 + seed * 977);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Bite(float y, float y0, float depth, float half)
        {
            return depth * Mathf.Max(0f, 1f - Mathf.Abs(y - y0) / half);
        }

        /// <summary>
        /// A battle-worn hanging banner baked in colour (use Image colour white): ragged
        /// frayed edges, torn bites in the sides, a slashed swallowtail, two holes, scorched
        /// darkening along every edge, cloth folds, grime, and a brass trim that the tears cut
        /// through. Width/height are in canvas units; the texture is baked at a higher density.
        /// </summary>
        public static Sprite TornBanner(int width, int height, int seed, Color cloth, Color trim)
        {
            return Get("torn" + width + "x" + height + "_" + seed, () =>
            {
                const float density = 1.25f;
                int w = Mathf.RoundToInt(width * density), h = Mathf.RoundToInt(height * density);
                float s = density;
                float notch = 150f * s;
                var px = new Color32[w * h];
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TornBanner", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

                // Hole centres and slit, in texture pixels measured from the top.
                Vector2 hole1 = new Vector2(w * 0.78f, h * 0.30f), hole2 = new Vector2(w * 0.2f, h * 0.71f);
                float r1 = 13f * s, r2 = 9f * s;
                float slitX = w * 0.74f, slitTop = h - notch * 0.25f - 170f * s;

                for (int py = 0; py < h; py++)
                {
                    float y = h - 1 - py + 0.5f; // distance from the top
                    float leftEdge = 2f * s + Fbm1(y / (26f * s), seed) * 9f * s
                                     + Bite(y, h * 0.58f, 46f * s, 38f * s) + Bite(y, h * 0.83f, 22f * s, 18f * s);
                    float rightEdge = w - 2f * s - Fbm1(y / (24f * s), seed + 3) * 9f * s
                                      - Bite(y, h * 0.41f, 34f * s, 30f * s) - Bite(y, h * 0.69f, 58f * s, 26f * s);
                    for (int px_ = 0; px_ < w; px_++)
                    {
                        float x = px_ + 0.5f;
                        float centre = Mathf.Abs(x / w * 2f - 1f);
                        float bottom = h - notch * (1f - centre) - Fbm1(x / (14f * s), seed + 5) * 26f * s
                                       - Bite(x, w * 0.12f, 70f * s, w * 0.07f) - Bite(x, w * 0.88f, 40f * s, w * 0.05f);
                        float dl = x - leftEdge, dr = rightEdge - x, db = bottom - y, dt = y + 4f;
                        float edge = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));

                        // Holes with ragged rims.
                        float a1 = Mathf.Atan2(y - hole1.y, x - hole1.x), a2 = Mathf.Atan2(y - hole2.y, x - hole2.x);
                        float h1 = Vector2.Distance(new Vector2(x, y), hole1) - r1 * (0.75f + 0.5f * Noise1(a1 * 2.5f + 10f, seed + 9));
                        float h2 = Vector2.Distance(new Vector2(x, y), hole2) - r2 * (0.75f + 0.5f * Noise1(a2 * 2.5f + 10f, seed + 11));
                        edge = Mathf.Min(edge, Mathf.Min(h1, h2));

                        // A slash up from the right tail.
                        if (y > slitTop)
                        {
                            float wobble = (Noise1(y / (10f * s), seed + 17) - 0.5f) * 6f * s;
                            float open = 2.5f * s * Mathf.Clamp01((y - slitTop) / (60f * s));
                            edge = Mathf.Min(edge, Mathf.Abs(x - slitX - wobble) - open);
                        }

                        float alpha = Mathf.Clamp01(edge + 0.5f);
                        if (alpha <= 0f) { px[py * w + px_] = new Color32(0, 0, 0, 0); continue; }

                        // Cloth shading: folds, grime, a darker sleeve at the top.
                        float folds = 1f + 0.07f * Mathf.Sin(x / w * Mathf.PI * 6.5f + Noise1(y / (90f * s), seed + 21) * 2.5f);
                        float grime = 0.82f + 0.22f * Noise2(x / (60f * s), y / (60f * s), seed + 23) + 0.06f * Noise2(x / (9f * s), y / (9f * s), seed + 29);
                        float sleeve = y < 46f * s ? 0.72f : 1f;
                        Color c = cloth * (folds * grime * sleeve);

                        // Brass trim, cut wherever the cloth is torn away.
                        float tx = Mathf.Min(Mathf.Abs(x - 22f * s), Mathf.Abs(x - (w - 22f * s)));
                        if (tx < 2.4f * s && y > 50f * s && y < h - notch - 40f * s)
                            c = trim * (0.8f + 0.25f * Noise2(x / (6f * s), y / (30f * s), seed + 31));

                        // Scorched, frayed edges.
                        float burn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / (16f * s)));
                        c = Color.Lerp(c * 0.35f, c, burn);
                        c.a = alpha;
                        px[py * w + px_] = c;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f * s);
            });
        }

        /// <summary>Turntable glyph: the front half of a floor ellipse with an arrow head at each end.</summary>
        public static Sprite RotateArrows()
        {
            return Get("rot", () => Paint("Rotate", 192, 80, Vector4.zero, (x, y) =>
            {
                float ex = (x - 96) / 80f, ey = (y - 44) / 30f;
                float ring = Mathf.Abs((Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 30f) - 3.5f;
                ring = Mathf.Max(ring, y - 44f);
                float triL = Mathf.Max(40f - y, (Mathf.Abs(x - 16f) - (66f - y) * 0.55f) * 0.88f);
                float triR = Mathf.Max(40f - y, (Mathf.Abs(x - 176f) - (66f - y) * 0.55f) * 0.88f);
                return Union(ring, Union(triL, triR));
            }));
        }
    }
}
