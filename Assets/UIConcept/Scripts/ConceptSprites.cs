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

        // ------------------------------------------------------------------ noise

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

        // ------------------------------------------------------------------ old-world strokes
        // Used by the cozy Candlelight themes. Thin, noisy strokes are drawn much larger
        // than they show, so these textures carry mipmaps to stay clean when scaled down.

        static Sprite Baked(string name, int w, int h, Func<float, float, float> alpha, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, mipMapBias = -0.4f
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static float TaperSegment(float px, float py, float ax, float ay, float bx, float by, float r0, float r1)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - Mathf.Lerp(r0, r1, h);
        }

        /// <summary>
        /// A single brush stroke: a loaded start, a wavering body with rough edges, and a dry
        /// tail where the bristles break into streaks. Horizontal, or vertical from the top.
        /// </summary>
        public static Sprite InkStroke(bool vertical)
        {
            return Get("inkStroke" + vertical, () =>
            {
                const int L = 256, T = 28;
                Func<float, float, float> stroke = (u, v) =>
                {
                    float t = u / L;
                    float centre = T * 0.5f + (Fbm1(u / 60f, 41) - 0.5f) * 4f;
                    float body = Mathf.Pow(Mathf.Sin(Mathf.PI * (t * 0.94f + 0.03f)), 0.35f);
                    float half = T * 0.36f * body * Mathf.Lerp(1.05f, 0.72f, t) * (0.86f + 0.28f * Noise1(u / 4f, 43));
                    float a = Mathf.Clamp01(half - Mathf.Abs(v - centre) + 0.5f);
                    float streak = Noise2(u / 26f, v / 1.8f, 47);
                    a *= Mathf.Lerp(1f, Mathf.SmoothStep(0.25f, 0.6f, streak), Mathf.SmoothStep(0.5f, 1f, t) * 0.9f);
                    return a * (0.86f + 0.14f * Noise2(u / 2.5f, v / 2.5f, 53));
                };
                return vertical
                    ? Baked("InkStrokeV", T, L, (x, y) => stroke(L - y, x))
                    : Baked("InkStroke", L, T, stroke);
            });
        }

        /// <summary>A thin, slightly broken pen line for long rules and slider tracks.</summary>
        public static Sprite InkHairline()
        {
            return Get("inkHair", () =>
            {
                const int L = 512, T = 16;
                return Baked("InkHairline", L, T, (x, y) =>
                {
                    float t = x / L;
                    float centre = T * 0.5f + (Fbm1(x / 90f, 61) - 0.5f) * 3f;
                    float half = T * 0.2f * (0.75f + 0.5f * Noise1(x / 7f, 67)) * Mathf.Pow(Mathf.Sin(Mathf.PI * (t * 0.96f + 0.02f)), 0.25f);
                    float a = Mathf.Clamp01(half - Mathf.Abs(y - centre) + 0.5f);
                    return a * Mathf.Lerp(0.55f, 1f, Noise2(x / 18f, y / 3f, 71));
                });
            });
        }

        /// <summary>Pen-drawn chevron pointing right: pressure swells at the corner.</summary>
        public static Sprite InkChevron()
        {
            return Get("inkChev", () => Paint("InkChevron", 64, 96, Vector4.zero, (x, y) =>
            {
                float wobble = (Noise2(x / 9f, y / 9f, 79) - 0.5f) * 2.4f;
                return Union(TaperSegment(x, y, 14, 14, 49, 49, 3.5f, 7.5f), TaperSegment(x, y, 49, 49, 18, 84, 7.5f, 3f)) + wobble;
            }));
        }

        static float Deckle(float x, float y)
        {
            float d = Box(x, y, 80, 80, 74, 74, 5);
            return d + (Noise2(x / 7f, y / 7f, 83) - 0.5f) * 4f + (Noise2(x / 2.5f, y / 2.5f, 86) - 0.5f) * 1.2f;
        }

        /// <summary>A scrap of old paper: deckled edges, mottled, a little darker at the rim.</summary>
        public static Sprite DeckledFill()
        {
            return Get("deckFill", () => Baked("DeckledFill", 160, 160, (x, y) =>
            {
                float d = Deckle(x, y);
                float a = Mathf.Clamp01(0.5f - d);
                float mottle = 0.7f + 0.3f * Noise2(x / 16f, y / 16f, 89);
                float rim = Mathf.Lerp(1.25f, 1f, Mathf.SmoothStep(0f, 14f, -d));
                return a * Mathf.Clamp01(mottle * rim);
            }));
        }

        /// <summary>The deckled edge drawn in ink.</summary>
        public static Sprite DeckledFrame(int thickness)
        {
            return Get("deckFrame" + thickness, () => Baked("DeckledFrame", 160, 160, (x, y) =>
            {
                float w = 1.6f + thickness * 1.1f;
                float ring = Mathf.Abs(Deckle(x, y) + w * 0.5f) - w * 0.5f;
                return Mathf.Clamp01(0.5f - ring) * (0.6f + 0.4f * Noise2(x / 4f, y / 4f, 97));
            }));
        }

        /// <summary>Wax seal: a blobby disc with a pressed ring.</summary>
        public static Sprite WaxSeal()
        {
            return Get("wax", () => Baked("WaxSeal", 96, 96, (x, y) =>
            {
                float dx = x - 48f, dy = y - 48f;
                float r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                float edge = 40f + (Noise1(ang * 2.2f + 20f, 101) - 0.5f) * 8f + (Noise1(ang * 7f + 5f, 103) - 0.5f) * 3f;
                float a = Mathf.Clamp01(edge - r + 0.5f);
                if (Mathf.Abs(r - 27f) < 2.5f) a *= 0.6f;
                else if (r < 24f) a *= 0.88f;
                return a;
            }));
        }

        /// <summary>Engraved line, swelling in the middle and tapering to points at both ends.</summary>
        public static Sprite TaperLine(bool vertical)
        {
            return Get("taper" + vertical, () =>
            {
                const int L = 256, T = 16;
                Func<float, float, float> line = (u, v) =>
                {
                    float t = u / L;
                    float half = T * 0.24f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
                    return Mathf.Clamp01(half - Mathf.Abs(v - T * 0.5f) + 0.5f);
                };
                return vertical
                    ? Baked("TaperLineV", T, L, (x, y) => line(L - y, x))
                    : Baked("TaperLine", L, T, line);
            });
        }

        /// <summary>Old engraved pointer: an arrowhead with a hollowed crescent back, pointing right.</summary>
        public static Sprite LeafArrow()
        {
            return Get("leafArrow", () => Paint("LeafArrow", 64, 96, Vector4.zero, (x, y) =>
            {
                // Counter-clockwise triangle (12,10) (54,48) (12,86); outward half-plane distances.
                float e1 = HalfPlane(x, y, 12, 10, 54, 48);
                float e2 = HalfPlane(x, y, 54, 48, 12, 86);
                float e3 = HalfPlane(x, y, 12, 86, 12, 10);
                float tri = Mathf.Max(e1, Mathf.Max(e2, e3));
                return Mathf.Max(tri, -Circle(x, y, 0, 48, 24));
            }));
        }

        static float HalfPlane(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, len = Mathf.Sqrt(dx * dx + dy * dy);
            return ((px - ax) * dy - (py - ay) * dx) / len;
        }

        // Square whose corners are rounded inwards, like an old picture frame or label.
        static float Notched(float x, float y, float s, float c)
        {
            float half = s * 0.5f - 1f, lo = s * 0.5f - half, hi = s * 0.5f + half;
            float box = Box(x, y, s * 0.5f, s * 0.5f, half, half, 0f);
            float corner = Mathf.Min(Mathf.Min(Circle(x, y, lo, lo, c), Circle(x, y, hi, lo, c)),
                                     Mathf.Min(Circle(x, y, lo, hi, c), Circle(x, y, hi, hi, c)));
            return Mathf.Max(box, -corner);
        }

        public static Sprite FrameFill(int corner)
        {
            return Get("frameFill" + corner, () =>
            {
                int s = corner * 2 + 8;
                return Paint("FrameFill", s, s, new Vector4(corner + 2, corner + 2, corner + 2, corner + 2), (x, y) => Notched(x, y, s, corner));
            });
        }

        public static Sprite FrameOutline(int corner, int thickness)
        {
            return Get("frameLine" + corner + "_" + thickness, () =>
            {
                int s = corner * 2 + 8;
                return Paint("FrameOutline", s, s, new Vector4(corner + 2, corner + 2, corner + 2, corner + 2), (x, y) => Ring(Notched(x, y, s, corner), thickness));
            });
        }
    }
}
