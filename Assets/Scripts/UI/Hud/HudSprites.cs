using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shields.UI
{
    /// <summary>
    /// Procedural icons for the in-game HUD: Health heart, Shield, Charge bolt, Attack
    /// sword, turn arrow, round outlines and chips. White, tinted by Image.color.
    /// </summary>
    public static class HudSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        delegate float Sdf(float x, float y);

        static Sprite Get(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            s = make();
            Cache[key] = s;
            return s;
        }

        static Sprite Paint(string name, int w, int h, Vector4 border, Sdf sdf)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static float Box(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px - cx) - (hx - r), qy = Mathf.Abs(py - cy) - (hy - r);
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

        static float HalfPlane(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, len = Mathf.Sqrt(dx * dx + dy * dy);
            return ((px - ax) * dy - (py - ay) * dx) / len;
        }

        static float Ring(float d, float t) { return Mathf.Abs(d + t * 0.5f) - t * 0.5f; }

        /// <summary>Counter-clockwise triangle a, b, c.</summary>
        static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            return Mathf.Max(Mathf.Max(HalfPlane(x, y, ax, ay, bx, by), HalfPlane(x, y, bx, by, cx, cy)), HalfPlane(x, y, cx, cy, ax, ay));
        }

        // ------------------------------------------------------------------ fades

        /// <summary>Vertical fade: opaque at the top, transparent at the bottom.</summary>
        public static Sprite FadeDown()
        {
            return Get("fadeD", () =>
            {
                const int h = 256;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++) tex.SetPixel(0, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, y / (h - 1f))));
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>Soft round pool, opaque centre fading to nothing: local darkening behind text.</summary>
        public static Sprite SoftDisc()
        {
            return Get("soft", () =>
            {
                const int s = 128;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x / (s - 1f) - 0.5f) * 2f, dy = (y / (s - 1f) - 0.5f) * 2f;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1f, 0f, d)));
                    }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ------------------------------------------------------------------ shapes

        public static Sprite Circle() { return Get("circle", () => Paint("Circle", 96, 96, Vector4.zero, (x, y) => Circle(x, y, 48, 48, 46))); }

        public static Sprite RingSprite(int thickness)
        {
            return Get("ring" + thickness, () => Paint("Ring", 96, 96, Vector4.zero, (x, y) => Ring(Circle(x, y, 48, 48, 46), thickness)));
        }

        /// <summary>Sliced rounded-rect outline.</summary>
        public static Sprite RoundOutline(int radius, int thickness)
        {
            return Get("rro" + radius + "_" + thickness, () =>
            {
                int s = radius * 2 + 4;
                return Paint("RoundOutline", s, s, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1),
                    (x, y) => Ring(Box(x, y, s * 0.5f, s * 0.5f, s * 0.5f - 1f, s * 0.5f - 1f, radius), thickness));
            });
        }

        // ------------------------------------------------------------------ icons

        /// <summary>Health.</summary>
        public static Sprite Heart()
        {
            return Get("heart", () => Paint("Heart", 96, 96, Vector4.zero, (x, y) =>
            {
                // Two circles and a point, unioned. Triangles are counter-clockwise.
                float l = Circle(x, y, 32, 58, 22), r = Circle(x, y, 64, 58, 22);
                float tri = Triangle(x, y, 48, 8, 88, 54, 8, 54);
                return Mathf.Min(Mathf.Min(l, r), tri);
            }));
        }

        static float ShieldSdf(float x, float y)
        {
            float top = Box(x, y, 48, 66, 40, 24, 5);
            float left = Circle(x, y, 92, 66, 84), right = Circle(x, y, 4, 66, 84);
            float bottom = Mathf.Max(Mathf.Max(left, right), y - 66);
            return Mathf.Min(top, bottom);
        }

        /// <summary>Shield (heater shape).</summary>
        public static Sprite Shield() { return Get("shield", () => Paint("Shield", 96, 96, Vector4.zero, ShieldSdf)); }

        public static Sprite ShieldOutline(int thickness)
        {
            return Get("shieldo" + thickness, () => Paint("ShieldOutline", 96, 96, Vector4.zero, (x, y) => Ring(ShieldSdf(x, y), thickness)));
        }

        /// <summary>Charge: a lightning bolt.</summary>
        public static Sprite Bolt()
        {
            return Get("bolt", () => Paint("Bolt", 96, 96, Vector4.zero, (x, y) =>
            {
                float upper = Triangle(x, y, 26, 44, 52, 50, 62, 92);
                float lower = Triangle(x, y, 70, 52, 44, 46, 38, 4);
                return Mathf.Min(upper, lower);
            }));
        }

        /// <summary>Attack: a sword, point up.</summary>
        public static Sprite Sword()
        {
            return Get("sword", () => Paint("Sword", 96, 96, Vector4.zero, (x, y) =>
            {
                float blade = Mathf.Min(Box(x, y, 48, 56, 6, 26, 1), Triangle(x, y, 42, 82, 54, 82, 48, 94));
                float guard = Box(x, y, 48, 30, 20, 4, 2);
                float grip = Box(x, y, 48, 18, 3.5f, 10, 1);
                float pommel = Circle(x, y, 48, 6, 5);
                return Mathf.Min(Mathf.Min(blade, guard), Mathf.Min(grip, pommel));
            }));
        }

        /// <summary>Filled triangle pointing right (turn marker).</summary>
        public static Sprite Arrow()
        {
            return Get("arrow", () => Paint("Arrow", 64, 64, Vector4.zero, (x, y) => Triangle(x, y, 14, 6, 58, 32, 14, 58)));
        }

        // ------------------------------------------------------------------ refined icons
        // Drawn larger and with mipmaps so they stay clean at 18-30 px (the older icons
        // above have no mips and alias when shrunk that far).

        static Sprite PaintMip(string name, int s, Sdf sdf)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Convex polygon, points counter-clockwise.</summary>
        static float Poly(float x, float y, params float[] p)
        {
            float d = float.MinValue;
            int n = p.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                d = Mathf.Max(d, HalfPlane(x, y, p[i * 2], p[i * 2 + 1], p[j * 2], p[j * 2 + 1]));
            }
            return d;
        }

        /// <summary>Attack: an arming sword, point up, tapered blade, flared guard.</summary>
        public static Sprite SwordIcon()
        {
            return Get("swordIcon", () => PaintMip("SwordIcon", 128, (x, y) =>
            {
                float blade = Poly(x, y, 57, 40, 71, 40, 69, 104, 64, 122, 59, 104);
                float guard = Mathf.Min(Box(x, y, 64, 36, 22, 4.5f, 2f), Mathf.Min(Circle(x, y, 41, 37.5f, 6.5f), Circle(x, y, 87, 37.5f, 6.5f)));
                float grip = Box(x, y, 64, 21, 4.5f, 12, 2f);
                float pommel = Circle(x, y, 64, 7.5f, 7);
                return Mathf.Min(Mathf.Min(blade, guard), Mathf.Min(grip, pommel));
            }));
        }

        static float HeaterSdf(float x, float y)
        {
            // A heater shield in a 128 box: flat top with rounded corners, sides curving to a point.
            float top = Box(x, y, 64, 92, 46, 26, 7);
            float left = Circle(x, y, 120, 92, 102), right = Circle(x, y, 8, 92, 102);
            float bottom = Mathf.Max(Mathf.Max(left, right), y - 92);
            return Mathf.Min(top, bottom);
        }

        /// <summary>Replace Shield: a heater shield with an inset rim.</summary>
        public static Sprite ShieldIcon()
        {
            return Get("shieldIcon", () => PaintMip("ShieldIcon", 128, (x, y) =>
            {
                float outer = HeaterSdf(x, y);
                float rimCut = Ring(outer + 13f, 7f);
                return Mathf.Max(outer, -rimCut);
            }));
        }

        /// <summary>The shot: a flintlock pistol, barrel to the right.</summary>
        public static Sprite PistolIcon()
        {
            return Get("pistolIcon", () => PaintMip("PistolIcon", 128, (x, y) =>
            {
                float barrel = Box(x, y, 80, 84, 38, 6.5f, 2f);
                float muzzle = Box(x, y, 116, 84, 4, 9, 2f);
                float lockPlate = Box(x, y, 44, 80, 18, 11, 3f);
                float hammer = Poly(x, y, 30, 90, 40, 90, 26, 106, 22, 103);
                float grip = Poly(x, y, 30, 72, 54, 72, 36, 26, 12, 34);
                float butt = Circle(x, y, 22, 30, 11);
                float guard = Mathf.Max(Ring(Circle(x, y, 56, 62, 10), 4f), y - 70);
                float trigger = Segment(x, y, 52, 70, 50, 58, 2.2f);
                return Mathf.Min(Mathf.Min(Mathf.Min(barrel, muzzle), Mathf.Min(lockPlate, hammer)),
                    Mathf.Min(Mathf.Min(grip, butt), Mathf.Min(guard, trigger)));
            }));
        }

        /// <summary>Aim: a ring with four ticks and a centre dot.</summary>
        public static Sprite Reticle()
        {
            return Get("reticle", () => PaintMip("Reticle", 128, (x, y) =>
            {
                float ring = Ring(Circle(x, y, 64, 64, 42), 5f);
                float ticks = Mathf.Min(Mathf.Min(Segment(x, y, 64, 4, 64, 34, 2.8f), Segment(x, y, 64, 94, 64, 124, 2.8f)),
                    Mathf.Min(Segment(x, y, 4, 64, 34, 64, 2.8f), Segment(x, y, 94, 64, 124, 64, 2.8f)));
                float dot = Circle(x, y, 64, 64, 4.5f);
                return Mathf.Min(Mathf.Min(ring, ticks), dot);
            }));
        }

        /// <summary>Card back: a rounded card with an inner border and a diamond lattice.</summary>
        public static Sprite CardBack()
        {
            return Get("cardback", () => Paint("CardBack", 96, 128, Vector4.zero, (x, y) =>
            {
                float card = Box(x, y, 48, 64, 46, 62, 8);
                float inner = Ring(Box(x, y, 48, 64, 38, 54, 4), 2.5f);
                float u = (x + y) / 14f, v = (x - y) / 14f;
                float lattice = Mathf.Min(Mathf.Abs(u - Mathf.Round(u)), Mathf.Abs(v - Mathf.Round(v))) * 14f - 1.2f;
                lattice = Mathf.Max(lattice, Box(x, y, 48, 64, 34, 50, 2));
                // The card is the fill; lines are cut out (drawn as holes).
                return Mathf.Max(card, -Mathf.Min(inner, lattice));
            }));
        }
    }
}
