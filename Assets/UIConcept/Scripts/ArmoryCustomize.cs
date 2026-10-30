using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// The Armory Customize screen: chamfered iron panel, shield-badge category rail,
    /// item tiles, and a detail area spread down to the panel's foot. Built once per
    /// concept with a palette (brass for Armory, crimson for the hybrid). Prototype only.
    /// </summary>
    public static class ArmoryCustomize
    {
        const int MaxTiles = 8;
        const float TileW = 142, TileH = 168, TileGap = 16;
        const float GridX = 188;
        const float PanelH = 812;

        public static void Build(ConceptView host, RectTransform scr, ArmoryPalette p)
        {
            UIConceptState S = host.S;

            CanvasGroup hintGroup = null;
            host.DragZone(scr, dragging => { if (hintGroup != null) hintGroup.alpha = dragging ? 0.15f : 1f; });

            Image shade = K.Img(scr, "RightShade", ConceptSprites.FadeRight(), new Color(0, 0, 0, 0.6f));
            shade.rectTransform.At(1, 0.5f, 0.5f, 0.5f, -600, 0, 1200, 1080);
            shade.rectTransform.localScale = new Vector3(-1, 1, 1);

            ArmoryKit.BackPlate(host, p, scr, host.GoMenu);

            // ---- Nickname plaque.
            Plate plaque = ArmoryKit.MakePlate(scr, "NicknamePlaque", 16, p.Iron.A(0.9f), p.Accent.A(0.7f), 2);
            plaque.Rect.TL(70, 130, 530, 106);
            K.Img(plaque.Rect, "Emblem", ConceptSprites.ShieldShape(), p.ActiveFill).rectTransform.TL(22, 24, 48, 56);
            K.Img(plaque.Rect, "EmblemEdge", ConceptSprites.ShieldOutline(6), p.Accent).rectTransform.TL(22, 24, 48, 56);
            TextMeshProUGUI nl = ArmoryKit.T(p, host, plaque.Rect, "Label", "NICKNAME", 13, p.TextDim, TextAlignmentOptions.MidlineLeft, true);
            nl.rectTransform.TL(88, 16, 300, 20);
            nl.characterSpacing = 10;
            TMP_InputField nick = K.Input(plaque.Rect, "Nickname", host.D.Bowlby, 34, p.Text, "YOUR NAME", p.TextDim, TextAlignmentOptions.MidlineLeft,
                Vector4.zero, null, new Color(0, 0, 0, 0), p.Plate);
            nick.GetComponent<RectTransform>().TL(86, 36, 400, 58);
            host.SetupNickname(nick);
            Image pencil = K.Img(plaque.Rect, "Pencil", ConceptSprites.Pencil(), p.Accent.A(0.8f));
            pencil.rectTransform.At(0, 0.5f, 0.5f, 0.5f, 480, -4, 22, 22);
            var nickFx = nick.gameObject.AddComponent<ConceptFx>();
            host.Track(nickFx);
            nickFx.Apply = f =>
            {
                float h = Mathf.Max(ConceptFx.Ease(f.Hover), nick.isFocused ? 1 : 0);
                plaque.Edge.color = K.Lerp(p.Accent.A(0.7f), p.AccentHi, h);
                pencil.color = K.Lerp(p.Accent.A(0.8f), p.AccentHi, h);
                float w = Mathf.Min(nick.textComponent.GetPreferredValues(nick.text).x, 400f);
                pencil.rectTransform.anchoredPosition = new Vector2(86 + w + 20, -4);
            };

            RectTransform hint = K.Node(scr, "DragHint").At(0, 0, 0.5f, 0, 540, 30, 360, 70);
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            K.Img(hint, "Icon", ConceptSprites.RotateArrows(), p.Accent.A(0.85f)).rectTransform.At(0.5f, 1, 0.5f, 1, 0, 0, 110, 46);
            TextMeshProUGUI ht = ArmoryKit.T(p, host, hint, "Label", "DRAG TO ROTATE", 14, p.Text.A(0.7f), TextAlignmentOptions.Center);
            ht.rectTransform.At(0.5f, 0, 0.5f, 0, 0, 0, 360, 22);
            ht.characterSpacing = 10;

            // ---- Tabs + panel.
            RectTransform area = K.Node(scr, "ContentArea").At(1, 1, 1, 1, -70, -48, 830, 80 + PanelH);
            var tabs = new ConceptFx[2];
            tabs[0] = TabPlate(host, p, area, "WARDROBE", 0, 264, () => host.SetTab(CustomizeTab.Wardrobe));
            tabs[1] = TabPlate(host, p, area, "SHOP", 276, 170, () => host.SetTab(CustomizeTab.Shop));
            host.Bind(() => { tabs[0].Selected = S.Tab == CustomizeTab.Wardrobe; tabs[1].Selected = S.Tab == CustomizeTab.Shop; });

            Plate panel = ArmoryKit.MakePlate(area, "Panel", 26, p.Iron.A(0.94f), p.Accent.A(0.6f), 2);
            panel.Rect.TL(0, 80, 830, PanelH);
            K.Img(panel.Rect, "InnerEdge", ConceptSprites.ChamferOutline(20, 1), p.Accent.A(0.18f)).rectTransform.Fill(10, 10, 10, 10);

            RectTransform wardrobe = K.Node(panel.Rect, "Wardrobe").Fill();
            RectTransform shop = K.Node(panel.Rect, "Shop").Fill();
            host.Bind(() => { K.SetActive(wardrobe, S.Tab == CustomizeTab.Wardrobe); K.SetActive(shop, S.Tab == CustomizeTab.Shop); });
            BuildWardrobe(host, p, wardrobe);
            BuildShop(host, p, shop);
        }

        static ConceptFx TabPlate(ConceptView host, ArmoryPalette p, RectTransform area, string label, float x, float w, System.Action click)
        {
            Button b = host.Btn(area, label, click, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(x, 0, w, 68);
            Plate plate = ArmoryKit.MakePlate(rt, "Plate", 14, p.IronMid, p.Accent, 2);
            plate.Rect.Fill();
            TextMeshProUGUI t = ArmoryKit.T(p, host, rt, "Label", label, 28, p.TextDim, TextAlignmentOptions.Center);
            t.rectTransform.Fill(0, 0, 0, 3);
            fx.Apply = f =>
            {
                float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                plate.Fill.color = K.Lerp(K.Lerp(p.IronMid.A(0.92f), p.IronLight, h), p.Accent, s);
                plate.Edge.color = K.Lerp(p.Accent.A(0.45f + 0.4f * h), p.AccentHi, s);
                t.fontSharedMaterial = s > 0.5f ? p.OnAccentMaterial : p.Plate;
                t.color = K.Lerp(K.Lerp(p.TextDim, p.Text, h), p.OnAccent, s);
            };
            return fx;
        }

        static void BuildWardrobe(ConceptView host, ArmoryPalette p, RectTransform root)
        {
            UIConceptState S = host.S;

            // Shield badges spread down the full rail.
            var badges = new ConceptFx[3];
            float[] tops = { 56, 316, 576 };
            for (int i = 0; i < 3; i++)
            {
                var slot = (WardrobeSlot)i;
                Button b = host.Btn(root, "Cat_" + slot, () => host.SetSlot(slot), null, out ConceptFx fx);
                RectTransform rt = b.GetComponent<RectTransform>().TL(30, tops[i], 120, 160);
                Image glow = K.Img(rt, "Glow", ConceptSprites.Glow(), p.Glow.A(0));
                glow.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -58, 200, 200);
                Image shield = K.Img(rt, "Shield", ConceptSprites.ShieldShape(), p.IronMid);
                shield.rectTransform.At(0.5f, 1, 0.5f, 1, 0, 0, 104, 118);
                Image edge = K.Img(rt, "Edge", ConceptSprites.ShieldOutline(5), p.Accent);
                edge.rectTransform.At(0.5f, 1, 0.5f, 1, 0, 0, 104, 118);
                RawImage icon = K.Raw(rt, "Icon", host.SlotIcon(slot), Color.white);
                icon.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -52, 78, 78);
                TextMeshProUGUI t = ArmoryKit.T(p, host, rt, "Label", UIConceptState.SlotName(slot), 18, p.TextDim, TextAlignmentOptions.Center);
                t.rectTransform.At(0.5f, 0, 0.5f, 0, 0, 0, 140, 28);
                t.characterSpacing = 6;
                fx.Apply = f =>
                {
                    float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                    shield.color = K.Lerp(K.Lerp(p.IronMid, p.IronLight, h), p.ActiveFill, s);
                    edge.color = K.Lerp(p.Accent.A(0.5f + 0.4f * h), p.AccentHi, s);
                    glow.color = p.Glow.A(0.45f * s);
                    t.color = K.Lerp(K.Lerp(p.TextDim, p.Text, h), p.AccentHi, s);
                    icon.color = new Color(1, 1, 1, 0.65f + 0.35f * Mathf.Max(s, h));
                    float sc = 1 + 0.05f * Mathf.Max(s, h) - 0.04f * f.Press;
                    rt.localScale = new Vector3(sc, sc, 1);
                };
                badges[i] = fx;
            }
            host.Bind(() => { for (int i = 0; i < 3; i++) badges[i].Selected = (int)S.Slot == i; });
            K.Img(root, "RailRule", ConceptSprites.White(), p.Accent.A(0.2f)).rectTransform.TL(168, 40, 2, PanelH - 80);

            RectTransform grid = K.Node(root, "Grid").TL(GridX, 34, 4 * TileW + 3 * TileGap, 2 * TileH + TileGap);
            for (int i = 0; i < MaxTiles; i++) BuildTile(host, p, grid, i);

            // ---- Detail area, spread down to the foot of the panel.
            RectTransform pane = K.Node(root, "Detail").TL(GridX, 410, 616, PanelH - 440);
            ArmoryKit.Divider(pane, "Divider", 616, p.Accent.A(0.45f)).TL(0, 0, 616, 14);

            TextMeshProUGUI name = ArmoryKit.T(p, host, pane, "Name", "", 40, p.Text);
            name.rectTransform.TL(0, 34, 616, 52);

            Plate ribbon = ArmoryKit.MakePlate(pane, "Rarity", 8, p.IronLight, p.Accent.A(0.3f), 1);
            ribbon.Rect.TL(0, 98, 160, 32);
            TextMeshProUGUI rarity = ArmoryKit.T(p, host, ribbon.Rect, "Label", "", 14, p.Text, TextAlignmentOptions.Center, true);
            rarity.rectTransform.Fill(0, 0, 0, 1);
            rarity.characterSpacing = 6;
            TextMeshProUGUI state = ArmoryKit.T(p, host, pane, "State", "", 15, p.TextDim, TextAlignmentOptions.MidlineLeft, true);
            state.rectTransform.TL(176, 98, 440, 32);
            state.characterSpacing = 6;

            // What this slot is wearing right now, so "previewing" is never ambiguous.
            TextMeshProUGUI wearingLabel = ArmoryKit.T(p, host, pane, "WearingLabel", "EQUIPPED IN THIS SLOT", 13, p.TextDim, TextAlignmentOptions.MidlineLeft, true);
            wearingLabel.rectTransform.TL(0, 162, 400, 20);
            wearingLabel.characterSpacing = 8;
            Plate wearing = ArmoryKit.MakePlate(pane, "Wearing", 10, p.Slot.A(0.85f), p.Accent.A(0.3f), 1);
            wearing.Rect.TL(0, 190, 616, 66);
            RawImage wearingIcon = K.Raw(wearing.Rect, "Icon", null, Color.white);
            wearingIcon.rectTransform.TL(12, 5, 56, 56);
            TextMeshProUGUI wearingName = ArmoryKit.T(p, host, wearing.Rect, "Name", "", 22, p.Text);
            wearingName.rectTransform.TL(84, 0, 400, 66);
            Image wearingCheck = K.Img(wearing.Rect, "Check", ConceptSprites.Check(), p.AccentHi);
            wearingCheck.rectTransform.At(1, 0.5f, 0.5f, 0.5f, -30, 0, 22, 22);

            host.Bind(() =>
            {
                CosmeticItem sel = S.SelectedItem(S.Slot);
                CosmeticItem eq = S.EquippedItem(S.Slot);
                if (sel == null || eq == null) return;
                name.text = sel.Name;
                rarity.text = sel.IsDefault ? "DEFAULT" : UIConceptState.RarityName(sel.Rarity);
                float w = rarity.GetPreferredValues(rarity.text).x + 36;
                ribbon.Rect.sizeDelta = new Vector2(w, 32);
                state.rectTransform.anchoredPosition = new Vector2(w + 16, -98);
                ribbon.Fill.color = sel.IsDefault ? p.IronLight : sel.Rarity == ItemRarity.Legendary ? p.Legendary : sel.Rarity == ItemRarity.Rare ? p.Steel : p.IronLight;
                bool isEq = S.IsEquipped(sel);
                state.text = isEq ? "EQUIPPED" : sel.Locked ? "LOCKED" : "PREVIEWING  ·  NOT EQUIPPED";
                state.color = isEq ? p.AccentHi : sel.Locked ? p.Disabled : p.TextDim;
                wearingIcon.texture = eq.Thumbnail;
                wearingName.text = eq.Name;
            });

            ConceptFx equip = ArmoryKit.PlateButton(host, p, pane, "Equip", "EQUIP", 34, true, () => S.EquipSelected(), out RectTransform er, out TextMeshProUGUI eqText);
            er.TL(0, PanelH - 440 - 82, 300, 78);
            host.Bind(() => { eqText.text = host.EquipLabel(); equip.Interactable = S.CanEquipSelected(); eqText.fontSize = eqText.text.Length > 8 ? 26 : 34; });

            ConceptFx reset = ArmoryKit.PlateButton(host, p, pane, "ResetDefault", "RESET TO DEFAULT", 20, false, () => S.ResetSlotToDefault(), out RectTransform rr, out _);
            rr.TL(316, PanelH - 440 - 82, 300, 78);
            host.Bind(() =>
            {
                CosmeticItem def = S.DefaultFor(S.Slot);
                reset.Interactable = def != null && !(S.IsEquipped(def) && S.IsSelected(def));
            });
        }

        static void BuildTile(ConceptView host, ArmoryPalette p, RectTransform grid, int index)
        {
            UIConceptState S = host.S;
            int col = index % 4, row = index / 4;
            float cx = col * (TileW + TileGap) + TileW * 0.5f;
            float cy = row * (TileH + TileGap) + TileH * 0.5f;

            Button b = host.Btn(grid, "Tile" + index, null, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().At(0, 1, 0.5f, 0.5f, cx, -cy, TileW, TileH);
            Plate plate = ArmoryKit.MakePlate(rt, "Plate", 14, p.IronMid, p.Accent, 2);
            plate.Rect.Fill();
            Image glow = K.Img(rt, "Glow", ConceptSprites.Glow(), p.Glow.A(0));
            glow.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 150, 150);
            Image selectEdge = K.Img(rt, "SelectEdge", ConceptSprites.ChamferOutline(14, 4), p.AccentHi);
            selectEdge.rectTransform.Fill(-4, -4, -4, -4);
            RawImage thumb = K.Raw(rt, "Thumb", null, Color.white);
            thumb.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 104, 104);
            Image lockIcon = K.Img(rt, "Lock", ConceptSprites.Lock(), p.Accent);
            lockIcon.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 34, 40);
            TextMeshProUGUI name = ArmoryKit.T(p, host, rt, "Name", "", 14, p.Text, TextAlignmentOptions.Center, true);
            name.rectTransform.TL(8, 118, TileW - 16, 42);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -16;

            RectTransform badge = K.Node(rt, "Equipped").At(1, 1, 0.5f, 0.5f, -16, -18, 30, 34);
            K.Img(badge, "Shield", ConceptSprites.ShieldShape(), p.Accent).rectTransform.Fill();
            K.Img(badge, "Check", ConceptSprites.Check(), p.OnAccent).rectTransform.Fill(6, 6, 6, 9);

            Image defMark = K.Img(rt, "DefaultMark", ConceptSprites.NoneMark(), p.TextDim.A(0.6f));
            defMark.rectTransform.At(0, 1, 0.5f, 0.5f, 18, -18, 18, 18);

            CosmeticItem item = null;
            b.onClick.AddListener(() => { if (item != null) S.Select(item); });

            host.Bind(() =>
            {
                List<CosmeticItem> items = host.SlotItems(S.Slot);
                item = index < items.Count ? items[index] : null;
                K.SetActive(rt, item != null);
                if (item == null) return;
                thumb.texture = item.Thumbnail;
                thumb.color = item.Locked ? new Color(0.3f, 0.29f, 0.28f, 0.8f) : Color.white;
                K.SetActive(lockIcon, item.Locked);
                K.SetActive(badge, S.IsEquipped(item));
                K.SetActive(defMark, item.IsDefault);
                name.text = item.Name;
                name.color = item.Locked ? p.Disabled : p.Text;
                fx.Selected = S.IsSelected(item);
            });

            fx.Apply = f =>
            {
                float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                bool locked = item != null && item.Locked;
                plate.Fill.color = K.Lerp(locked ? p.Iron : p.IronMid, p.IronLight, Mathf.Max(h * 0.8f, s * 0.6f));
                plate.Edge.color = p.Accent.A(0.3f + 0.5f * h);
                selectEdge.color = p.AccentHi.A(s);
                glow.color = p.Glow.A(locked ? 0.15f * s : 0.4f * s);
                float sc = 1 + 0.04f * Mathf.Max(h, s) - 0.04f * f.Press;
                rt.localScale = new Vector3(sc, sc, 1);
            };
        }

        static void BuildShop(ConceptView host, ArmoryPalette p, RectTransform root)
        {
            // Dim, locked tiles behind the sign.
            for (int i = 0; i < 12; i++)
            {
                int col = i % 4, row = i / 4;
                Plate g = ArmoryKit.MakePlate(root, "Ghost" + i, 14, p.IronMid.A(0.45f), p.Accent.A(0.12f), 2);
                g.Rect.TL(46 + col * 188, 70 + row * 228, 172, 206);
                K.Img(g.Rect, "Lock", ConceptSprites.Lock(), p.Accent.A(0.15f)).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 10, 34, 40);
            }

            for (int side = -1; side <= 1; side += 2)
                for (int link = 0; link < 10; link++)
                {
                    Image l = K.Img(root, "Chain", ConceptSprites.RingShape(18), K.Hex("#7D7E86"));
                    l.rectTransform.At(0.5f, 1, 0.5f, 0.5f, side * 250, -26 - link * 30, link % 2 == 0 ? 20 : 12, 34);
                }

            RectTransform sign = K.Node(root, "Sign").At(0.5f, 1, 0.5f, 0.5f, 0, -390, 620, 170);
            sign.localEulerAngles = new Vector3(0, 0, -3.5f);
            Image shadow = K.Img(sign, "Shadow", ConceptSprites.ChamferRect(10), new Color(0, 0, 0, 0.55f));
            shadow.rectTransform.Fill(10, 14, -10, -14);
            Plate plank = ArmoryKit.MakePlate(sign, "Plank", 10, K.Hex("#5C3B22"), K.Hex("#2B1A0E"), 4);
            plank.Rect.Fill();
            for (int gr = 0; gr < 3; gr++)
                K.Img(sign, "Grain" + gr, ConceptSprites.White(), K.Hex("#6E4A2E", 0.7f)).rectTransform.TL(30 + gr * 40, 38 + gr * 44, 460 - gr * 70, 3);
            float[] nx = { 26, 594 };
            float[] ny = { 24, 146 };
            foreach (float x in nx)
                foreach (float yy in ny)
                {
                    K.Img(sign, "Nail", ConceptSprites.CircleShape(), K.Hex("#24252A")).rectTransform.At(0, 1, 0.5f, 0.5f, x, -yy, 16, 16);
                    K.Img(sign, "NailHi", ConceptSprites.CircleShape(), K.Hex("#8C8D95")).rectTransform.At(0, 1, 0.5f, 0.5f, x - 1.5f, -yy + 1.5f, 9, 9);
                }
            TextMeshProUGUI big = ArmoryKit.T(p, host, sign, "Title", "COMING SOON", 62, K.Hex("#F1E3C6"), TextAlignmentOptions.Center);
            big.rectTransform.Fill(0, 0, 0, 34);
            TextMeshProUGUI sub = ArmoryKit.T(p, host, sign, "Sub", "THE ARMORER IS STILL AT THE FORGE", 15, K.Hex("#E2C79A"), TextAlignmentOptions.Center, true);
            sub.rectTransform.At(0.5f, 0, 0.5f, 0, 0, 22, 600, 24);
            sub.characterSpacing = 6;
        }
    }
}
