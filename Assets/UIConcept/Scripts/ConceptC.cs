using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// CONCEPT C — ARMORY.
    /// Heavy, heraldic and faceted, echoing the low-poly art. A battle-worn crimson banner
    /// carries the main menu, Join Room and Settings; Customize uses chamfered iron plates
    /// with brass edges and shield-shaped category badges. The logo is struck in aged
    /// gold so it sits warmly on the cloth instead of shouting red-on-red.
    /// </summary>
    public sealed class ConceptC : ConceptView
    {
        public override string Code { get { return "C"; } }

        // Armory stays exactly as approved: its fixed 4×2 grid shows the original items only.
        public override bool ShowExtraItems { get { return false; } }

        static readonly Color BannerCloth = K.Hex("#7E1D21");
        static readonly Color Groove = K.Hex("#1C090A");

        ArmoryPalette _p;

        TextMeshProUGUI T(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool flat = false)
        {
            return ArmoryKit.T(_p, this, parent, name, text, size, color, align, flat);
        }

        protected override void Build()
        {
            _p = ArmoryPalette.Brass(Bowlby);

            Image vig = K.Img(Root, "Vignette", ConceptSprites.Vignette(), new Color(0, 0, 0, 0.55f));
            vig.rectTransform.Fill();
            vig.transform.SetAsFirstSibling();

            BuildMenu();
            ArmoryCustomize.Build(this, CustomizeScreen, _p);
            BuildSettings();
        }

        // ------------------------------------------------------------------ banner

        /// <summary>A torn banner hanging from a brass rod, at its top-left corner.</summary>
        RectTransform HangBanner(RectTransform parent, string name, float x, int width, int height, int seed)
        {
            RectTransform banner = K.Node(parent, name).TL(x, -6, width, height);
            // No drop shadow: through the tears it would read as black spikes.
            Sprite cloth = ConceptSprites.TornBanner(width, height, seed, BannerCloth, _p.Accent);
            K.Img(banner, "Cloth", cloth, Color.white).rectTransform.Fill();
            Plate rod = ArmoryKit.MakePlate(banner, "Rod", 8, _p.Accent, _p.AccentHi.A(0.6f));
            rod.Rect.TL(-34, 14, width + 68, 24);
            K.Img(banner, "FinialL", ConceptSprites.DiamondShape(), _p.AccentHi).rectTransform.At(0, 1, 0.5f, 0.5f, -44, -26, 30, 30);
            K.Img(banner, "FinialR", ConceptSprites.DiamondShape(), _p.AccentHi).rectTransform.At(1, 1, 0.5f, 0.5f, 44, -26, 30, 30);
            return banner;
        }

        RectTransform GoldLogo(Transform parent, float size)
        {
            Material face = K.TextMat(D.Knewave, "LogoFaceGold", 0.15f, K.Hex("#2A130A"), default, 0, 0, 0, 0, 0.06f);
            Material stroke = K.TextMat(D.Knewave, "LogoStrokeGold", 0.36f, K.Hex("#170A06"),
                new Color(0.07f, 0.03f, 0.02f, 0.9f), 0.7f, -1f, 0f, 0.36f, 0.18f);
            return Logo(parent, "Logo", size, TextAlignmentOptions.Center, "#E2BC7C", "#9C6A30", face, stroke);
        }

        // ================================================================== MAIN MENU

        const int BannerW = 540;

        void BuildMenu()
        {
            RectTransform scr = MenuScreen;
            Image shade = K.Img(scr, "LeftShade", ConceptSprites.FadeRight(), new Color(0, 0, 0, 0.55f));
            shade.rectTransform.At(0, 0, 0, 0, 0, 0, 1000, 0);
            shade.rectTransform.anchorMax = new Vector2(0, 1);

            RectTransform banner = HangBanner(scr, "Banner", 110, BannerW, 1000, 3);
            GoldLogo(banner, 104).TL(0, 74, BannerW, 140);
            ArmoryKit.Divider(banner, "LogoDivider", 260, _p.Accent.A(0.8f)).At(0.5f, 1, 0.5f, 0.5f, 0, -236, 260, 14);

            // ---- Menu items.
            RectTransform items = K.Node(banner, "Items").Fill();
            var itemsGroup = items.gameObject.AddComponent<CanvasGroup>();
            TMP_InputField joinInput = null;

            float y = 280;
            BannerItem(items, "OFFLINE MODE", ref y, 38, 68, Offline);
            BannerItem(items, "JOIN ROOM", ref y, 38, 68, () => OpenJoin(joinInput));
            BannerItem(items, "CREATE ROOM", ref y, 38, 68, CreateRoom);
            ArmoryKit.Divider(items, "GroupDivider", 140, _p.Accent.A(0.5f)).At(0.5f, 1, 0.5f, 0.5f, 0, -(y + 18), 140, 14);
            y += 40;
            BannerItem(items, "CUSTOMIZE", ref y, 29, 56, GoCustomize);
            BannerItem(items, "SETTINGS", ref y, 29, 56, GoSettings);
            BannerItem(items, "EXIT", ref y, 29, 56, Exit);

            // ---- Join Room replaces the items on the banner.
            RectTransform join = K.Node(banner, "JoinRoom").Fill();
            var joinGroup = join.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI title = T(join, "Title", "JOIN ROOM", 44, _p.Text, TextAlignmentOptions.Center);
            title.rectTransform.TL(0, 284, BannerW, 60);
            TextMeshProUGUI codeLabel = T(join, "CodeLabel", "ROOM CODE", 16, _p.TextDim, TextAlignmentOptions.Center, true);
            codeLabel.rectTransform.TL(0, 366, BannerW, 24);
            codeLabel.characterSpacing = 12;

            Plate slot = ArmoryKit.MakePlate(join, "CodeSlot", 14, _p.Slot, _p.Accent.A(0.85f), 3);
            slot.Rect.TL(60, 398, BannerW - 120, 86);
            joinInput = K.Input(slot.Rect, "CodeInput", Bowlby, 46, _p.Text, "ENTER CODE", _p.TextDim.A(0.45f), TextAlignmentOptions.Center,
                new Vector4(20, 6, 20, 6), null, new Color(0, 0, 0, 0), _p.Flat);
            joinInput.GetComponent<RectTransform>().Fill();
            joinInput.textComponent.characterSpacing = 16;
            ((TextMeshProUGUI)joinInput.placeholder).fontSize = 30;
            ((TextMeshProUGUI)joinInput.placeholder).characterSpacing = 6;
            SetupJoinInput(joinInput);
            Bind(() => slot.Edge.color = joinInput.isFocused ? _p.AccentHi : _p.Accent.A(0.85f));

            TextMeshProUGUI message = T(join, "Message", string.Empty, 16, _p.Glow, TextAlignmentOptions.Center, true);
            message.rectTransform.TL(0, 492, BannerW, 24);
            message.characterSpacing = 6;
            Bind(() => message.text = S.JoinMessage);

            ArmoryKit.PlateButton(this, _p, join, "JoinButton", "JOIN", 34, true, () => ConfirmJoin(joinInput), out RectTransform jr, out _);
            jr.TL(120, 528, BannerW - 240, 72);
            ArmoryKit.PlateButton(this, _p, join, "BackButton", "BACK", 24, false, CloseJoin, out RectTransform br, out _);
            br.TL(180, 616, BannerW - 360, 56);

            Animator(scr, "JoinAnimator", () => S.JoinOpen, t =>
            {
                itemsGroup.alpha = 1 - t;
                itemsGroup.interactable = itemsGroup.blocksRaycasts = t < 0.01f;
                joinGroup.alpha = t;
                joinGroup.interactable = joinGroup.blocksRaycasts = t > 0.99f;
                items.anchoredPosition = new Vector2(0, 20 * t);
                join.anchoredPosition = new Vector2(0, -20 * (1 - t));
            });
        }

        void BannerItem(RectTransform parent, string label, ref float y, float size, float step, Action click)
        {
            float width = BannerW - 100;
            Button b = Btn(parent, label, click, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(50, y, width, size * 1.5f);
            Plate plate = ArmoryKit.MakePlate(rt, "Plate", 12, _p.Iron.A(0.85f), _p.Accent, 2);
            plate.Rect.Fill();
            Image dl = K.Img(rt, "DiamondL", ConceptSprites.DiamondShape(), _p.AccentHi);
            dl.rectTransform.At(0, 0.5f, 0.5f, 0.5f, 0, 0, 14, 14);
            Image dr = K.Img(rt, "DiamondR", ConceptSprites.DiamondShape(), _p.AccentHi);
            dr.rectTransform.At(1, 0.5f, 0.5f, 0.5f, 0, 0, 14, 14);
            TextMeshProUGUI t = T(rt, "Label", label, size, _p.Text, TextAlignmentOptions.Center);
            t.rectTransform.Fill(0, 0, 0, 3);
            float tw = t.GetPreferredValues(label).x;
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                plate.Fill.color = _p.Iron.A(0.85f * h);
                plate.Edge.color = _p.Accent.A(h);
                plate.Rect.localScale = new Vector3(0.88f + 0.12f * h, 1, 1);
                t.color = K.Lerp(_p.Text, _p.AccentHi, h);
                float edge = tw * 0.5f + 26 + 12 * (1 - h);
                dl.rectTransform.anchoredPosition = new Vector2(width * 0.5f - edge, 0);
                dr.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + edge, 0);
                dl.color = dr.color = _p.AccentHi.A(h);
                float s = 1 - 0.03f * f.Press;
                rt.localScale = new Vector3(s, s, 1);
            };
            y += step;
        }

        // ================================================================== SETTINGS
        // A wide war banner with every setting on it: no tabs, nothing hidden, and the
        // same cloth-and-brass object as the main menu, so Settings feels like the menu
        // banner was swapped for a bigger one.

        const int SetW = 820;
        const float RowX = 74, CtrlX = 360, CtrlW = 290, ValueX = 668;

        void BuildSettings()
        {
            RectTransform scr = SettingsScreen;
            Image shade = K.Img(scr, "LeftShade", ConceptSprites.FadeRight(), new Color(0, 0, 0, 0.6f));
            shade.rectTransform.At(0, 0, 0, 0, 0, 0, 1300, 0);
            shade.rectTransform.anchorMax = new Vector2(0, 1);

            RectTransform banner = HangBanner(scr, "SettingsBanner", 110, SetW, 1030, 8);

            TextMeshProUGUI title = T(banner, "Title", "SETTINGS", 48, _p.Text, TextAlignmentOptions.Center);
            title.rectTransform.TL(0, 56, SetW, 62);
            ArmoryKit.Divider(banner, "TitleDivider", 300, _p.Accent.A(0.8f)).At(0.5f, 1, 0.5f, 0.5f, 0, -134, 300, 14);

            float y = 160;
            Heading(banner, "AUDIO", ref y);
            SliderRow(banner, "MASTER VOLUME", ref y, 0, 1, () => S.MasterVolume, v => S.MasterVolume = v, UIConceptState.Percent);
            SliderRow(banner, "MUSIC VOLUME", ref y, 0, 1, () => S.MusicVolume, v => S.MusicVolume = v, UIConceptState.Percent);
            SliderRow(banner, "EFFECTS VOLUME", ref y, 0, 1, () => S.EffectsVolume, v => S.EffectsVolume = v, UIConceptState.Percent);
            Heading(banner, "CONTROLS", ref y);
            SliderRow(banner, "MOUSE SENSITIVITY", ref y, UIConceptState.MinSensitivity, UIConceptState.MaxSensitivity, () => S.MouseSensitivity, v => S.MouseSensitivity = v, UIConceptState.Sensitivity);
            Heading(banner, "DISPLAY", ref y);
            SelectorRow(banner, "RESOLUTION", ref y);
            LeverRow(banner, "DISPLAY MODE", ref y, "FULLSCREEN", "WINDOWED", () => S.Fullscreen, v => S.Fullscreen = v);
            LeverRow(banner, "V-SYNC", ref y, "ON", "OFF", () => S.VSync, v => S.VSync = v);

            ArmoryKit.Divider(banner, "FootDivider", 300, _p.Accent.A(0.6f)).At(0.5f, 1, 0.5f, 0.5f, 0, -(y + 18), 300, 14);
            ArmoryKit.PlateButton(this, _p, banner, "Back", "BACK", 26, false, GoMenu, out RectTransform back, out _);
            back.TL(SetW * 0.5f - 110, y + 44, 220, 60);
            TextMeshProUGUI foot = T(banner, "Footnote", "CHANGES APPLY IMMEDIATELY", 13, _p.TextDim, TextAlignmentOptions.Center, true);
            foot.rectTransform.TL(0, y + 118, SetW, 20);
            foot.characterSpacing = 8;
        }

        void Heading(RectTransform banner, string label, ref float y)
        {
            TextMeshProUGUI t = T(banner, "Heading_" + label, label, 16, _p.AccentHi, TextAlignmentOptions.Center, true);
            t.rectTransform.TL(0, y, SetW, 26);
            t.characterSpacing = 14;
            float w = t.GetPreferredValues(label).x;
            float line = 230;
            K.Img(banner, "HeadL_" + label, ConceptSprites.White(), _p.Accent.A(0.45f)).rectTransform.TL(SetW * 0.5f - w * 0.5f - 18 - line, y + 12, line, 2);
            K.Img(banner, "HeadR_" + label, ConceptSprites.White(), _p.Accent.A(0.45f)).rectTransform.TL(SetW * 0.5f + w * 0.5f + 18, y + 12, line, 2);
            y += 36;
        }

        RectTransform Row(RectTransform banner, string label, ref float y, out TextMeshProUGUI text)
        {
            RectTransform row = K.Node(banner, "Row_" + label).TL(0, y, SetW, 54);
            text = T(row, "Label", label, 22, _p.Text);
            text.rectTransform.TL(RowX, 0, 290, 54);
            y += 56;
            return row;
        }

        void SliderRow(RectTransform banner, string label, ref float y, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
        {
            RectTransform row = Row(banner, label, ref y, out TextMeshProUGUI labelText);
            Plate valuePlate = ArmoryKit.MakePlate(row, "ValuePlate", 8, Groove.A(0.9f), _p.Accent.A(0.45f), 1);
            valuePlate.Rect.TL(ValueX, 9, 78, 36);
            TextMeshProUGUI value = T(valuePlate.Rect, "Value", "", 20, _p.Text, TextAlignmentOptions.Center, true);
            value.rectTransform.Fill(0, 0, 0, 2);

            K.SliderParts sl = K.Slider(row, "Slider", min, max, get(), v => { set(v); S.Notify(); },
                ConceptSprites.ChamferRect(6), ConceptSprites.ChamferRect(4), ConceptSprites.ShieldShape(), 16, new Vector2(28, 32), 4);
            sl.Slider.GetComponent<RectTransform>().TL(CtrlX, 7, CtrlW, 40);
            sl.Track.color = Groove.A(0.9f);
            K.Img(sl.Track.transform, "Edge", ConceptSprites.ChamferOutline(6, 1), _p.Accent.A(0.4f)).rectTransform.Fill();
            sl.Fill.color = _p.Accent;
            sl.Handle.color = _p.AccentHi;
            Image knobEdge = K.Img(sl.Handle.transform, "Edge", ConceptSprites.ShieldOutline(8), K.Hex("#2A130A"));
            knobEdge.rectTransform.Fill();
            Track(sl.Fx);
            sl.Fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                sl.Handle.rectTransform.localScale = Vector3.one * (1 + 0.15f * h);
                sl.Fill.color = K.Lerp(_p.Accent, _p.AccentHi, h * 0.6f);
                labelText.color = K.Lerp(_p.Text, _p.AccentHi, h);
            };
            Bind(() => { sl.Slider.SetValueWithoutNotify(get()); value.text = format(get()); });
        }

        void SelectorRow(RectTransform banner, string label, ref float y)
        {
            RectTransform row = Row(banner, label, ref y, out _);
            Plate valuePlate = ArmoryKit.MakePlate(row, "ValuePlate", 8, Groove.A(0.9f), _p.Accent.A(0.45f), 1);
            valuePlate.Rect.TL(CtrlX + 54, 7, (ValueX + 78 - 46 - 8) - (CtrlX + 54), 40);
            TextMeshProUGUI value = T(valuePlate.Rect, "Value", "", 21, _p.Text, TextAlignmentOptions.Center, true);
            value.rectTransform.Fill(0, 0, 0, 2);
            Bind(() => value.text = UIConceptState.Resolutions[S.ResolutionIndex]);
            ArrowPlate(row, CtrlX, -1);
            ArrowPlate(row, ValueX + 78 - 46, 1);
        }

        void ArrowPlate(RectTransform row, float x, int dir)
        {
            Button b = Btn(row, dir < 0 ? "Prev" : "Next", () =>
            {
                int n = UIConceptState.Resolutions.Length;
                S.ResolutionIndex = (S.ResolutionIndex + dir + n) % n;
                S.Notify();
            }, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(x, 7, 46, 40);
            Plate plate = ArmoryKit.MakePlate(rt, "Plate", 8, Groove.A(0.9f), _p.Accent.A(0.5f), 2);
            plate.Rect.Fill();
            Image chev = K.Img(rt, "Chevron", ConceptSprites.Chevron(), _p.Accent);
            chev.rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, dir * 1.5f, 0, 12, 18);
            if (dir < 0) chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                plate.Edge.color = K.Lerp(_p.Accent.A(0.5f), _p.AccentHi, h);
                chev.color = K.Lerp(_p.Accent, _p.AccentHi, h);
                float s = 1 - 0.06f * f.Press;
                rt.localScale = new Vector3(s, s, 1);
            };
        }

        /// <summary>Two-position lever: a brass block slides under the chosen option.</summary>
        void LeverRow(RectTransform banner, string label, ref float y, string a, string b, Func<bool> get, Action<bool> set)
        {
            RectTransform row = Row(banner, label, ref y, out _);
            float w = ValueX + 78 - CtrlX, half = w * 0.5f;
            Plate track = ArmoryKit.MakePlate(row, "LeverTrack", 10, Groove.A(0.9f), _p.Accent.A(0.45f), 1);
            track.Rect.TL(CtrlX, 5, w, 44);
            Plate block = ArmoryKit.MakePlate(track.Rect, "Block", 8, _p.Accent, _p.AccentHi.A(0.7f), 2);
            block.Rect.TL(4, 4, half - 8, 36);

            TextMeshProUGUI la = T(track.Rect, "A", a, a.Length > 6 ? 17 : 20, _p.OnAccent, TextAlignmentOptions.Center, true);
            la.rectTransform.TL(0, 0, half, 44);
            TextMeshProUGUI lb = T(track.Rect, "B", b, b.Length > 6 ? 17 : 20, _p.TextDim, TextAlignmentOptions.Center, true);
            lb.rectTransform.TL(half, 0, half, 44);

            Btn(track.Rect, "HitA", () => { set(true); S.Notify(); }, null, out ConceptFx fa).GetComponent<RectTransform>().TL(0, 0, half, 44);
            Btn(track.Rect, "HitB", () => { set(false); S.Notify(); }, null, out ConceptFx fb).GetComponent<RectTransform>().TL(half, 0, half, 44);

            Animator(row, "Lever", get, t =>
            {
                block.Rect.anchoredPosition = new Vector2(4 + (1 - t) * half, -4);
                float ha = ConceptFx.Ease(fa.Hover), hb = ConceptFx.Ease(fb.Hover);
                la.color = K.Lerp(K.Lerp(_p.TextDim, _p.Text, ha), _p.OnAccent, t);
                lb.color = K.Lerp(K.Lerp(_p.TextDim, _p.Text, hb), _p.OnAccent, 1 - t);
                track.Edge.color = _p.Accent.A(0.45f + 0.4f * Mathf.Max(ha, hb));
            }, 9f);
        }
    }
}
