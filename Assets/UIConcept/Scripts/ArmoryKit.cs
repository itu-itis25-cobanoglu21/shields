using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// Colours for the Armory language. Brass is Concept C; Crimson is the same panel
    /// recoloured for the Candlelight + Armory hybrid.
    /// </summary>
    public sealed class ArmoryPalette
    {
        public Color Iron, IronMid, IronLight, Slot;
        public Color Accent, AccentHi, Glow, ActiveFill;
        public Color Text, TextDim, Disabled;
        public Color OnAccent;
        public Color Steel, Legendary;
        public Material Plate;  // outlined, for text on dark plates
        public Material Flat;   // plain, for dark text on light plates
        public bool DarkTextOnAccent;

        public Material OnAccentMaterial { get { return DarkTextOnAccent ? Flat : Plate; } }

        static ArmoryPalette Base(TMP_FontAsset bowlby)
        {
            return new ArmoryPalette
            {
                Iron = K.Hex("#17181B"),
                IronMid = K.Hex("#25262B"),
                IronLight = K.Hex("#363840"),
                Slot = K.Hex("#0D0E10"),
                Steel = K.Hex("#2F5B8A"),
                Legendary = K.Hex("#E3672D"),
                Plate = K.TextMat(bowlby, "C_Plate", 0.08f, new Color(0.03f, 0.03f, 0.035f, 1f), new Color(0, 0, 0, 0.7f), 0.5f, -0.9f, 0.1f, 0.1f),
                Flat = K.TextMat(bowlby, "C_Flat", 0f),
            };
        }

        public static ArmoryPalette Brass(TMP_FontAsset bowlby)
        {
            ArmoryPalette p = Base(bowlby);
            p.Accent = K.Hex("#C38F48");
            p.AccentHi = K.Hex("#F2C77F");
            p.Glow = K.Hex("#E3672D");
            p.ActiveFill = K.Hex("#5B2A18");
            p.Text = K.Hex("#F1E3C6");
            p.TextDim = K.Hex("#A99D88");
            p.Disabled = K.Hex("#5C564E");
            p.OnAccent = p.Iron;
            p.DarkTextOnAccent = true;
            return p;
        }

        public static ArmoryPalette Crimson(TMP_FontAsset bowlby)
        {
            ArmoryPalette p = Base(bowlby);
            p.Accent = K.Hex("#A52A2C");
            p.AccentHi = K.Hex("#E4483F");
            p.Glow = K.Hex("#D3302C");
            p.ActiveFill = K.Hex("#4C1215");
            p.Text = K.Hex("#EEE6D6");
            p.TextDim = K.Hex("#A69E90");
            p.Disabled = K.Hex("#5A554E");
            p.OnAccent = K.Hex("#F4EBDC");
            p.DarkTextOnAccent = false;
            return p;
        }
    }

    public struct Plate
    {
        public RectTransform Rect;
        public Image Fill;
        public Image Edge;
    }

    /// <summary>Building blocks shared by the Armory concept and the hybrid.</summary>
    public static class ArmoryKit
    {
        public static Plate MakePlate(Transform parent, string name, int cut, Color fill, Color edge, int thickness = 2)
        {
            var p = new Plate { Rect = K.Node(parent, name) };
            p.Fill = K.Img(p.Rect, "Fill", ConceptSprites.ChamferRect(cut), fill);
            p.Fill.rectTransform.Fill();
            p.Edge = K.Img(p.Rect, "Edge", ConceptSprites.ChamferOutline(cut, thickness), edge);
            p.Edge.rectTransform.Fill();
            return p;
        }

        /// <summary>Line — diamond — line.</summary>
        public static RectTransform Divider(Transform parent, string name, float width, Color color)
        {
            RectTransform d = K.Node(parent, name);
            d.sizeDelta = new Vector2(width, 14);
            K.Img(d, "L", ConceptSprites.White(), color).rectTransform.At(0.5f, 0.5f, 1, 0.5f, -14, 0, width * 0.5f - 14, 2);
            K.Img(d, "R", ConceptSprites.White(), color).rectTransform.At(0.5f, 0.5f, 0, 0.5f, 14, 0, width * 0.5f - 14, 2);
            K.Img(d, "D", ConceptSprites.DiamondShape(), color).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 14, 14);
            return d;
        }

        public static TextMeshProUGUI T(ArmoryPalette p, ConceptView host, Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool flat = false)
        {
            return K.Text(parent, name, text, host.D.Bowlby, size, color, align, flat ? p.Flat : p.Plate);
        }

        /// <summary>Chamfered plate button. Primary plates are filled with the accent.</summary>
        public static ConceptFx PlateButton(ConceptView host, ArmoryPalette p, Transform parent, string name, string label, float size, bool primary,
            Action click, out RectTransform rect, out TextMeshProUGUI text)
        {
            Button b = host.Btn(parent, name, click, null, out ConceptFx fx);
            rect = b.GetComponent<RectTransform>();
            Plate plate = MakePlate(rect, "Plate", 12, primary ? p.Accent : p.IronMid, p.Accent, 2);
            plate.Rect.Fill();
            TextMeshProUGUI t = K.Text(rect, "Label", label, host.D.Bowlby, size, primary ? p.OnAccent : p.Text, TextAlignmentOptions.Center,
                primary ? p.OnAccentMaterial : p.Plate);
            t.rectTransform.Fill(0, 0, 0, 3);
            text = t;
            RectTransform r = rect;
            fx.Apply = f =>
            {
                bool on = f.IsActive;
                float h = on ? ConceptFx.Ease(f.Hover) : 0f;
                if (primary)
                {
                    plate.Fill.color = on ? K.Lerp(p.Accent, p.AccentHi, h * 0.8f) : p.IronMid;
                    plate.Edge.color = on ? p.AccentHi.A(0.6f + 0.4f * h) : p.Accent.A(0.2f);
                    t.color = on ? p.OnAccent : p.Disabled;
                    t.fontSharedMaterial = on ? p.OnAccentMaterial : p.Plate;
                }
                else
                {
                    plate.Fill.color = on ? K.Lerp(p.IronMid, p.IronLight, h) : p.IronMid.A(0.6f);
                    plate.Edge.color = on ? K.Lerp(p.Accent.A(0.55f), p.AccentHi, h) : p.Accent.A(0.15f);
                    t.color = on ? K.Lerp(p.Text, p.AccentHi, h) : p.Disabled;
                }
                float s = 1 - 0.03f * f.Press;
                r.localScale = new Vector3(s, s, 1);
            };
            return fx;
        }

        public static RectTransform BackPlate(ConceptView host, ArmoryPalette p, Transform parent, Action click, float x = 70, float y = 48)
        {
            Button b = host.Btn(parent, "Back", click, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(x, y, 186, 62);
            Plate plate = MakePlate(rt, "Plate", 12, p.IronMid.A(0.95f), p.Accent.A(0.6f), 2);
            plate.Rect.Fill();
            Image chev = K.Img(rt, "Chevron", ConceptSprites.Chevron(), p.Accent);
            chev.rectTransform.At(0, 0.5f, 0.5f, 0.5f, 34, 0, 13, 20);
            chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            TextMeshProUGUI t = T(p, host, rt, "Label", "BACK", 26, p.Text);
            t.rectTransform.Fill(60, 0, 0, 3);
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                plate.Fill.color = K.Lerp(p.IronMid.A(0.95f), p.IronLight, h);
                plate.Edge.color = K.Lerp(p.Accent.A(0.6f), p.AccentHi, h);
                t.color = K.Lerp(p.Text, p.AccentHi, h);
                chev.color = K.Lerp(p.Accent, p.AccentHi, h);
                chev.rectTransform.anchoredPosition = new Vector2(34 - 5 * h, 0);
            };
            return rt;
        }
    }
}
