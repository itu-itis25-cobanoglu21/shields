using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// CONCEPT AC — CANDLELIGHT SHOWROOM.
    /// Candlelight's main menu, Join Room and Settings, with a darker oxblood logo.
    /// Customize is a showroom: the character stands centre stage, the categories are the
    /// main menu's own word list on the left, the chosen item gets a big title card on the
    /// right, and the items run along the foot of the screen as a strip.
    ///
    /// Original theme is concept 2 as approved; Hearth is its cozier "old" variant.
    /// </summary>
    public sealed class ConceptAC : ConceptA
    {
        public ConceptAC(CandleTheme theme) : base(theme) { }

        public override string Code { get { return "AC" + Th.Suffix; } }

        public override float PreviewYaw { get { return 0f; } }
        public override float PreviewTurn { get { return 10f; } }
        public override float PreviewLookHeight { get { return 0.6f; } }
        public override float PreviewFov { get { return 52f; } }

        protected override string LogoTop { get { return "#A9302A"; } }
        protected override string LogoBottom { get { return "#661316"; } }
        protected override Material LogoFace
        {
            get { return K.TextMat(D.Knewave, "LogoFaceOxblood", 0.16f, K.Hex("#E2CCA4"), default, 0, 0, 0, 0, 0.06f); }
        }

        const int MaxSlots = 15, VisibleSlots = 7;
        const float SlotStep = 154, SlotW = 140, SlotH = 196;
        const float StripW = VisibleSlots * SlotStep;

        protected override void BuildCustomize()
        {
            RectTransform scr = CustomizeScreen;

            Graphic hintIcon = null, hint = null;
            DragZone(scr, dragging =>
            {
                if (hint != null) hint.CrossFadeAlpha(dragging ? 0f : 1f, 0.2f, true);
                if (hintIcon != null) hintIcon.CrossFadeAlpha(dragging ? 0f : 1f, 0.2f, true);
            }, 660, 600);

            LeftShade(scr, 900, 0.85f);
            RightShade(scr, 1050, 0.92f);
            Image floor = K.Img(scr, "FloorShade", ConceptSprites.FadeUp(), ShadeColor(scr, 0.92f));
            floor.rectTransform.At(0, 0, 0, 0, 0, 0, 0, 380);
            floor.rectTransform.anchorMax = new Vector2(1, 0);
            floor.rectTransform.sizeDelta = new Vector2(0, 380);

            BackLink(scr, GoMenu);
            NicknameOverCharacter(scr, 960, 34, 44);
            DragHint(scr, 960, 252, out hintIcon, out hint);

            // Tabs above the category list.
            var tabs = new ConceptFx[2];
            tabs[0] = TabWord(scr, "WARDROBE", 142, 296, 26, () => SetTab(CustomizeTab.Wardrobe));
            float shopX = 142 + tabs[0].GetComponent<RectTransform>().sizeDelta.x + 30;
            tabs[1] = TabWord(scr, "SHOP", shopX, 296, 26, () => SetTab(CustomizeTab.Shop));
            Bind(() => { tabs[0].Selected = S.Tab == CustomizeTab.Wardrobe; tabs[1].Selected = S.Tab == CustomizeTab.Shop; });

            RectTransform wardrobe = K.Node(scr, "Wardrobe").Fill();
            RectTransform shop = K.Node(scr, "Shop").Fill();
            Bind(() => { K.SetActive(wardrobe, S.Tab == CustomizeTab.Wardrobe); K.SetActive(shop, S.Tab == CustomizeTab.Shop); });

            // ---- Categories: the main menu's own list.
            for (int i = 0; i < 3; i++)
            {
                var slot = (WardrobeSlot)i;
                Button b = MenuWord(wardrobe, UIConceptState.SlotName(slot), 46, () => SetSlot(slot), () => S.Slot == slot, out _);
                b.name = "Cat_" + slot;
                b.GetComponent<RectTransform>().TL(140, 390 + i * 70, 420, 62);
            }
            Line(wardrobe, "CatRule", Bone.A(0.22f)).rectTransform.TL(144, 368, 56, Thk(2));

            BuildTitleCard(wardrobe);

            BuildStrip(wardrobe);

            BuildShop(shop);
        }

        void BuildTitleCard(RectTransform root)
        {
            RectTransform card = K.Node(root, "TitleCard").At(1, 1, 1, 1, -110, -300, 560, 420);

            TextMeshProUGUI kicker = Kicker(card, "Kicker", "", 15, Red, 12);
            kicker.rectTransform.TL(2, 0, 560, 24);
            TextMeshProUGUI name = T(card, "Name", "", 66, Bone, TextAlignmentOptions.TopLeft, true);
            name.rectTransform.TL(0, 28, 560, 170);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -24;
            Image rule = Line(card, "Rule", Red);
            rule.rectTransform.TL(4, 120, 56, Thk(3));
            TextMeshProUGUI status = T(card, "Status", "", 16, Muted);
            status.rectTransform.TL(2, 136, 560, 24);
            status.characterSpacing = Sp(8);

            Button equip = Btn(card, "Equip", () => S.EquipSelected(), null, out ConceptFx eqFx);
            RectTransform eqRt = equip.GetComponent<RectTransform>().TL(0, 180, 360, 66);
            Image eqPool = HoverPool(eqRt);
            TextMeshProUGUI eqText = T(eqRt, "Label", "EQUIP", 46, Bone, TextAlignmentOptions.MidlineLeft, true);
            eqText.rectTransform.Fill();
            if (eqPool != null)
            {
                float w = eqText.GetPreferredValues("EQUIP").x;
                eqPool.rectTransform.At(0, 0.5f, 0.5f, 0.5f, 16 + w * 0.5f, 0, w + 184, 148);
            }
            Image eqBar = Line(eqRt, "Blade", Red);
            float eqBarH = Thk(5);
            eqBar.rectTransform.At(0, 0.5f, 1, 0.5f, -16, 0, 0, eqBarH);
            eqFx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                eqText.color = K.Lerp(Dim, Bone, 0.6f + 0.4f * h);
                eqText.rectTransform.offsetMin = eqText.rectTransform.offsetMax = new Vector2(16 * h, 0);
                eqBar.rectTransform.sizeDelta = new Vector2(30 * h, eqBarH);
                eqBar.color = Red.A(h);
                SetPool(eqPool, h, 7.3f);
            };

            RectTransform reset = TextLink(card, "ResetDefault", "RESET TO DEFAULT", 18, false, () => S.ResetSlotToDefault());
            reset.TL(2, 300, 300, 40);

            Bind(() =>
            {
                CosmeticItem sel = S.SelectedItem(S.Slot);
                if (sel == null) return;
                kicker.text = UIConceptState.SlotName(S.Slot) + "   ·   " + (sel.IsDefault ? "DEFAULT" : UIConceptState.RarityName(sel.Rarity));
                name.text = sel.Name;
                float h = Mathf.Min(name.GetPreferredValues(sel.Name, 560, 0).y, 160);
                float y = 28 + h + 14;
                rule.rectTransform.anchoredPosition = new Vector2(4, -y);
                bool eq = S.IsEquipped(sel);
                status.text = eq ? "EQUIPPED" : sel.Locked ? "LOCKED  ·  NOT YET AVAILABLE" : "PREVIEWING";
                status.color = eq ? Red : Muted;
                status.rectTransform.anchoredPosition = new Vector2(2, -(y + 16));
                bool canEquip = S.CanEquipSelected();
                K.SetActive(equip, canEquip);
                eqRt.anchoredPosition = new Vector2(0, -(y + 58));
                CosmeticItem def = S.DefaultFor(S.Slot);
                bool showReset = def != null && !(S.IsEquipped(def) && S.IsSelected(def));
                K.SetActive(reset, showReset);
                reset.anchoredPosition = new Vector2(2, -(y + (canEquip ? 134 : 62)));
            });
        }

        /// <summary>The item strip: up to seven visible, wheel/drag/arrow scrolling, a thin red scrollbar.</summary>
        void BuildStrip(RectTransform root)
        {
            K.ScrollParts scroll = K.Scroll(root, "Strip", false, Bone.A(0.12f), Red);
            scroll.Scroll.GetComponent<RectTransform>().At(0.5f, 0, 0.5f, 0, 0, 40, StripW, SlotH);
            scroll.Bar.GetComponent<RectTransform>().At(0.5f, 0, 0.5f, 1, 0, -6, StripW, 5);
            for (int i = 0; i < MaxSlots; i++) BuildSlot(scroll.Content, i);

            ConceptFx left = StripArrow(root, -1, scroll);
            ConceptFx right = StripArrow(root, 1, scroll);

            WardrobeSlot shown = S.Slot;
            Bind(() =>
            {
                int count = SlotItems(S.Slot).Count;
                scroll.Content.sizeDelta = new Vector2(Mathf.Max(count * SlotStep, StripW), 0);
                bool overflow = count * SlotStep > StripW + 1f;
                K.SetActive(left, overflow);
                K.SetActive(right, overflow);
                if (shown != S.Slot) { shown = S.Slot; scroll.Scroll.horizontalNormalizedPosition = 0f; }
            });
        }

        ConceptFx StripArrow(RectTransform root, int dir, K.ScrollParts scroll)
        {
            Button b = Btn(root, dir < 0 ? "StripLeft" : "StripRight", () =>
            {
                float range = scroll.Content.rect.width - scroll.Viewport.rect.width;
                if (range <= 0f) return;
                float step = 3 * SlotStep / range;
                scroll.Scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.Scroll.horizontalNormalizedPosition + dir * step);
            }, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().At(0.5f, 0, 0.5f, 0.5f, dir * (StripW * 0.5f + 40), 40 + SlotH - 62, 56, 80);
            Image chev = K.Img(rt, "Chevron", Chev, Dim);
            chev.rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 20, 30);
            if (dir < 0) chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                float pos = scroll.Scroll.horizontalNormalizedPosition;
                bool atEnd = dir < 0 ? pos <= 0.001f : pos >= 0.999f;
                chev.color = atEnd ? Muted.A(0.35f) : K.Lerp(Dim, Red, h);
                chev.rectTransform.anchoredPosition = new Vector2(dir * 5 * h, 0);
            };
            return fx;
        }

        void BuildSlot(RectTransform content, int index)
        {
            Button b = Btn(content, "Slot" + index, null, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().At(0, 0, 0.5f, 0, 0, 0, SlotW, SlotH);

            Image glow = K.Img(rt, "Glow", ConceptSprites.Glow(), Red.A(0));
            glow.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 190, 190);
            RawImage thumb = K.Raw(rt, "Thumb", null, Color.white);
            thumb.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 112, 112);
            Image lockIcon = K.Img(rt, "Lock", ConceptSprites.Lock(), Dim);
            lockIcon.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 28, 33);
            Image check = K.Img(rt, "Equipped", ConceptSprites.Check(), Red);
            check.rectTransform.At(1, 1, 0.5f, 0.5f, -16, -14, 18, 18);
            TextMeshProUGUI name = T(rt, "Name", "", 16, Dim, TextAlignmentOptions.Top);
            name.rectTransform.TL(0, 128, SlotW, 44);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -16;
            Image bar = Line(rt, "Underline", Red);
            float barH = Thk(4);
            bar.rectTransform.At(0.5f, 0, 0.5f, 0, 0, 18, 0, barH);

            CosmeticItem item = null;
            int count = 0;
            b.onClick.AddListener(() => { if (item != null) S.Select(item); });

            Bind(() =>
            {
                List<CosmeticItem> items = SlotItems(S.Slot);
                count = items.Count;
                item = index < count ? items[index] : null;
                K.SetActive(rt, item != null);
                if (item == null) return;
                // Centred when the row is short; laid out from the left when it scrolls.
                float offset = Mathf.Max(0f, (StripW - count * SlotStep) * 0.5f);
                rt.anchoredPosition = new Vector2(offset + (index + 0.5f) * SlotStep, 0);
                thumb.texture = item.Thumbnail;
                thumb.color = item.Locked ? new Color(0.35f, 0.33f, 0.31f, 0.65f) : Color.white;
                K.SetActive(lockIcon, item.Locked);
                K.SetActive(check, S.IsEquipped(item));
                name.text = item.Name;
                fx.Selected = S.IsSelected(item);
            });

            fx.Apply = f =>
            {
                float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                bool locked = item != null && item.Locked;
                glow.color = Red.A(0.22f * s);
                float sc = 1f + 0.1f * s + 0.05f * h * (1 - s) - 0.04f * f.Press;
                thumb.rectTransform.localScale = new Vector3(sc, sc, 1);
                name.color = K.Lerp(locked ? Muted : Dim, Bone, Mathf.Max(s, h * 0.7f));
                bar.rectTransform.sizeDelta = new Vector2(48 * Mathf.Max(s, h * 0.4f), barH);
                bar.color = Red.A(Mathf.Max(s, h * 0.5f));
            };
        }

        void BuildShop(RectTransform root)
        {
            RectTransform card = K.Node(root, "ShopCard").At(1, 1, 1, 1, -110, -300, 560, 360);
            TextMeshProUGUI kicker = Kicker(card, "Kicker", "THE SHOP", 15, Red, 12);
            kicker.rectTransform.TL(2, 0, 560, 24);
            TextMeshProUGUI big = T(card, "Title", "COMING\nSOON", 66, Bone, TextAlignmentOptions.TopLeft, true);
            big.rectTransform.TL(0, 28, 560, 170);
            big.lineSpacing = -24;
            float below = 28 + big.GetPreferredValues(big.text, 560, 0).y + 16;
            Line(card, "Rule", Red).rectTransform.TL(4, below, 56, Thk(3));
            TextMeshProUGUI line = T(card, "Line", "THE MERCHANT IS STILL ON THE ROAD.", 18, Dim);
            line.rectTransform.TL(2, below + 18, 560, 26);
            line.characterSpacing = Sp(4);

            // The strip, empty and locked.
            RectTransform strip = K.Node(root, "GhostStrip").At(0.5f, 0, 0.5f, 0, 0, 34, 6 * SlotStep, SlotH);
            for (int i = 0; i < 6; i++)
            {
                float a = 1f - Mathf.Abs(i - 2.5f) / 4f;
                RectTransform s = K.Node(strip, "Ghost" + i).At(0.5f, 0, 0.5f, 0, (i - 2.5f) * SlotStep, 0, SlotW, SlotH);
                K.Img(s, "Box", Th.Frame(10, 1), Bone.A(0.12f * a)).rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 104, 104);
                K.Img(s, "Lock", ConceptSprites.Lock(), Bone.A(0.2f * a)).rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -62, 24, 28);
                K.Img(s, "Name", Th.Fill(5), Bone.A(0.07f * a)).rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -142, 80, 10);
            }
        }
    }
}
