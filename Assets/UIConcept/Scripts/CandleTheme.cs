using System;
using TMPro;
using UnityEngine;

namespace Shields.UIConcept
{
    /// <summary>
    /// The look of the Candlelight family: colours, line and frame sprites, text materials
    /// and letter spacing. Layout never changes with the theme.
    ///
    /// Original (concepts 1 and 2) keeps the first palette and black shading with the
    /// hand-made Ink shapes. Ink and Hearth are the cozier "old" variants with warm
    /// parchment / brass colours over natural, near-neutral shadows.
    /// </summary>
    public sealed class CandleTheme
    {
        public string Suffix = string.Empty;

        // ---- colours
        public Color Bone, Dim, Muted, Accent;
        public Color Shade = Color.black;      // tint of the darkening gradients
        public float ShadeStrength = 1f;       // multiplies their alpha
        public Color VignetteTint = Color.black;
        public Color? CustomizeShade;          // a different shading on the Customize screen only (null: same as everywhere)
        public float CustomizeShadeStrength = 1f;
        public Color BoxRest = new Color(0, 0, 0, 0.35f);
        public float BoxHotAlpha = 0.08f;      // Bone at this alpha when a box is hovered / selected
        public Color? HandleRest;              // slider handle at rest (null: Bone)

        // ---- type
        public float Tracking = 1f;            // multiplies every letter spacing
        public Func<TMP_FontAsset, Material> SoftMaterial;
        public Func<TMP_FontAsset, Material> StrongMaterial;
        public bool KickerUsesKnewave;         // small accent labels hand-lettered in Knewave
        public float KickerScale = 1f;
        public Func<TMP_FontAsset, Material> KickerMaterial;

        // ---- strokes, frames and controls
        public Func<Sprite> Line = ConceptSprites.White;      // blades, rules, underlines
        public Func<Sprite> VLine = ConceptSprites.White;     // vertical rules
        public Func<Sprite> Hairline = ConceptSprites.White;  // long section rules
        public Func<Sprite> Chevron = ConceptSprites.Chevron;
        public Func<int, Sprite> Fill = ConceptSprites.RoundRect;
        public Func<int, int, Sprite> Frame = ConceptSprites.RoundRectOutline;
        public Func<Sprite> SliderTrack = ConceptSprites.White;
        public Func<Sprite> SliderFill = ConceptSprites.White;
        public Func<Sprite> SliderHandle;      // null: the production menu diamond
        public float TrackHeight = 2f;
        public Vector2 HandleSize = new Vector2(20, 20);
        public float LineScale = 1f, LineMin;

        // ---- warm light behind hovered entries (Hearth)
        public bool HoverGlow;
        public Color GlowColor;

        // ---- logo (null: the concept's own)
        public string LogoTop, LogoBottom;
        public Color? LogoInline;

        /// <summary>Line thickness for this theme: hand-drawn strokes need more height to read.</summary>
        public float Thick(float h) { return Mathf.Max(h * LineScale, LineMin); }

        // ------------------------------------------------------------------ themes

        /// <summary>
        /// The hand-made shapes approved on the Ink variant: brush-stroke dashes, rules and
        /// underlines with dry tails, pen chevrons, deckled paper tiles, wax-seal slider
        /// knobs, hand-lettered Knewave labels and tight letter spacing. Colour-free, so
        /// every theme keeps its own palette and shading.
        /// </summary>
        static CandleTheme InkShapes(CandleTheme t, Color shadow)
        {
            t.Tracking = 0.35f;
            t.KickerUsesKnewave = true;
            t.KickerScale = 1.3f;
            t.KickerMaterial = f => K.TextMat(f, "Kicker_" + ColorUtility.ToHtmlStringRGB(shadow), 0f, default, shadow.A(0.75f), 0.5f, -0.6f, 0.25f, 0.06f);
            t.Line = () => ConceptSprites.InkStroke(false);
            t.VLine = () => ConceptSprites.InkStroke(true);
            t.Hairline = ConceptSprites.InkHairline;
            t.Chevron = ConceptSprites.InkChevron;
            t.Fill = r => ConceptSprites.DeckledFill();
            t.Frame = (r, th) => ConceptSprites.DeckledFrame(th);
            t.SliderTrack = ConceptSprites.InkHairline;
            t.SliderFill = () => ConceptSprites.InkStroke(false);
            t.SliderHandle = ConceptSprites.WaxSeal;
            t.TrackHeight = 5f;
            t.HandleSize = new Vector2(30, 30);
            t.LineScale = 2.2f;
            t.LineMin = 4f;
            t.HandleRest = t.Accent;
            t.BoxHotAlpha = 0.13f;
            return t;
        }

        /// <summary>
        /// Concepts 1 and 2: the original colours and plain black shading, with the
        /// hand-made Ink shapes and lettering.
        /// </summary>
        public static CandleTheme Original()
        {
            return InkShapes(new CandleTheme
            {
                Bone = K.Hex("#EEE6D6"),
                Dim = K.Hex("#A69E90"),
                Muted = K.Hex("#6A635A"),
                Accent = K.Hex("#C9302F"),
                SoftMaterial = f => K.TextMat(f, "A_Soft", 0f, default, new Color(0, 0, 0, 0.55f), 0.5f, -0.7f, 0.45f, 0.05f),
                StrongMaterial = f => K.TextMat(f, "A_Strong", 0f, default, new Color(0, 0, 0, 0.75f), 0.8f, -1f, 0.25f, 0.15f),
            }, Color.black);
        }

        /// <summary>
        /// MAIN: Candlelight (concept 1) with the Candlelight Showroom oxblood logo, and the
        /// Showroom Old cool night shadow on the Customize screen only, at full strength so
        /// the room reads a little darker there.
        /// </summary>
        public static CandleTheme Main()
        {
            CandleTheme t = Original();
            t.Suffix = "_MAIN";
            t.LogoTop = "#A9302A";
            t.LogoBottom = "#661316";
            t.LogoInline = K.Hex("#E2CCA4");
            t.CustomizeShade = K.Hex("#05070A");
            t.CustomizeShadeStrength = 1f;
            return t;
        }

        /// <summary>
        /// INK &amp; PARCHMENT. The menu as a tavern ledger: parchment text, sepia inks, a
        /// dried-rust accent and the hand-made Ink shapes. Shadows are near-black with only
        /// a trace of warmth and a little lighter than the originals, so the tavern keeps
        /// its own colour instead of reading through a sepia filter.
        /// </summary>
        public static CandleTheme Ink()
        {
            var ink = new Color(0.07f, 0.05f, 0.035f);
            return InkShapes(new CandleTheme
            {
                Suffix = "_OLD",
                Bone = K.Hex("#ECE2CE"),
                Dim = K.Hex("#B1A592"),
                Muted = K.Hex("#766A5D"),
                Accent = K.Hex("#A9472D"),
                Shade = K.Hex("#0A0806"),
                ShadeStrength = 0.9f,
                VignetteTint = K.Hex("#0A0806"),
                BoxRest = new Color(0.035f, 0.028f, 0.022f, 0.45f),
                SoftMaterial = f => K.TextMat(f, "Ink_Soft", 0f, default, ink.A(0.8f), 0.6f, -0.8f, 0.2f, 0.08f, 0.01f),
                StrongMaterial = f => K.TextMat(f, "Ink_Strong", 0f, default, ink.A(0.9f), 1.1f, -1.3f, 0.08f, 0.22f, 0.02f),
                LogoTop = "#BE4A2E",
                LogoBottom = "#7E2A18",
            }, ink);
        }

        /// <summary>
        /// HEARTH &amp; BRASS. Firelight instead of ink: candle-cream text, brass accents,
        /// tapered engraved rules, picture-frame tiles with rounded-in corners, and a soft
        /// flickering warm pool behind what you hover. Shadows are cool night blue-black
        /// and lighter, the way a candlelit room really looks: warmth only near the light.
        /// </summary>
        public static CandleTheme Hearth()
        {
            var soot = new Color(0.04f, 0.04f, 0.05f);
            return new CandleTheme
            {
                Suffix = "_OLD",
                Bone = K.Hex("#F0E4CC"),
                Dim = K.Hex("#B9A88F"),
                Muted = K.Hex("#7A6D5E"),
                Accent = K.Hex("#C28D4E"),
                Shade = K.Hex("#06080B"),
                ShadeStrength = 0.85f,
                VignetteTint = K.Hex("#05070A"),
                BoxRest = new Color(0.03f, 0.032f, 0.04f, 0.45f),
                BoxHotAlpha = 0.1f,
                HandleRest = K.Hex("#D2A766"),
                Tracking = 0.45f,
                SoftMaterial = f => K.TextMat(f, "Hearth_Soft", 0f, default, soot.A(0.75f), 0.4f, -0.6f, 0.6f, 0.15f),
                StrongMaterial = f => K.TextMat(f, "Hearth_Strong", 0f, default, soot.A(0.85f), 0.6f, -0.9f, 0.5f, 0.3f),
                Line = () => ConceptSprites.TaperLine(false),
                VLine = () => ConceptSprites.TaperLine(true),
                Hairline = () => ConceptSprites.TaperLine(false),
                Chevron = ConceptSprites.LeafArrow,
                Fill = r => ConceptSprites.FrameFill(Mathf.Max(8, r + 4)),
                Frame = (r, t) => ConceptSprites.FrameOutline(Mathf.Max(8, r + 4), Mathf.Max(1, t)),
                SliderTrack = () => ConceptSprites.TaperLine(false),
                SliderFill = () => ConceptSprites.RoundRect(2),
                TrackHeight = 4f,
                HandleSize = new Vector2(24, 24),
                LineScale = 1.8f,
                LineMin = 3f,
                HoverGlow = true,
                GlowColor = K.Hex("#FF9A40"),
                LogoInline = K.Hex("#EBC77F"),
            };
        }
    }
}
