using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// CONCEPT A — CANDLELIGHT.
    /// Typographic and nearly frameless: the tavern is the backdrop, type does the work.
    /// Readability comes from local darkening gradients, not panels. Bone-white Bowlby
    /// on soft shadow, a single blood-red accent (blade bars, underlines, rules), and a
    /// clear size hierarchy between "play" actions and "meta" actions.
    ///
    /// Customize is built like the main menu itself: a left-aligned word list where the
    /// chosen category unfolds its items underneath (the same accordion as Join Room),
    /// with Equip offered inline on the chosen item. The character stands on the right,
    /// where the diorama sits on the main menu.
    /// </summary>
    public class ConceptA : ConceptView
    {
        public override string Code { get { return "A"; } }

        public override float PreviewYaw { get { return -17.5f; } }
        public override float PreviewTurn { get { return -18f; } }

        protected static readonly Color Bone = K.Hex("#EEE6D6");
        protected static readonly Color Dim = K.Hex("#A69E90");
        protected static readonly Color Muted = K.Hex("#6A635A");
        protected static readonly Color Red = K.Hex("#C9302F");

        // Logo tone for this concept: a deeper ember red than the original mark.
        protected virtual string LogoTop { get { return "#C63B2B"; } }
        protected virtual string LogoBottom { get { return "#86191C"; } }
        protected virtual Material LogoFace { get { return null; } }

        protected Material Soft;
        protected Material Strong;

        protected TextMeshProUGUI T(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool strong = false)
        {
            return K.Text(parent, name, text, Bowlby, size, color, align, strong ? Strong : Soft);
        }

        protected override void Build()
        {
            Soft = K.TextMat(Bowlby, "A_Soft", 0f, default, new Color(0, 0, 0, 0.55f), 0.5f, -0.7f, 0.45f, 0.05f);
            Strong = K.TextMat(Bowlby, "A_Strong", 0f, default, new Color(0, 0, 0, 0.75f), 0.8f, -1f, 0.25f, 0.15f);

            // Shared atmosphere: a gentle vignette across every screen.
            Image vig = K.Img(Root, "Vignette", ConceptSprites.Vignette(), new Color(0, 0, 0, 0.6f));
            vig.rectTransform.Fill();
            vig.transform.SetAsFirstSibling();

            BuildMenu();
            BuildCustomize();
            BuildSettings();
        }

        // ================================================================== MAIN MENU

        sealed class Entry
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float BaseY;
            public bool IsJoin;
        }

        void BuildMenu()
        {
            RectTransform scr = MenuScreen;
            LeftShade(scr, 1150, 0.85f);

            Logo(scr, "Logo", 128, TextAlignmentOptions.Left, LogoTop, LogoBottom, LogoFace).TL(132, 92, 640, 170);
            TextMeshProUGUI tag = T(scr, "Tagline", "3 – 8 PLAYERS  ·  ONE SURVIVOR", 16, Muted);
            tag.rectTransform.TL(142, 262, 600, 24);
            tag.characterSpacing = 9;

            var entries = new List<Entry>();
            TMP_InputField joinInput = null;

            float y = 370;
            AddEntry(scr, entries, "OFFLINE MODE", ref y, 46, 68, Offline, false);
            AddEntry(scr, entries, "JOIN ROOM", ref y, 46, 68, () => OpenJoin(joinInput), true);
            AddEntry(scr, entries, "CREATE ROOM", ref y, 46, 68, CreateRoom, false);

            // A short rule separates playing from everything else.
            var ruleEntry = new Entry { Rect = K.Node(scr, "Rule").TL(144, y + 14, 56, 2), BaseY = y + 14 };
            K.Img(ruleEntry.Rect, "Line", ConceptSprites.White(), Bone.A(0.22f)).rectTransform.Fill();
            ruleEntry.Group = ruleEntry.Rect.gameObject.AddComponent<CanvasGroup>();
            entries.Add(ruleEntry);
            y += 44;

            AddEntry(scr, entries, "CUSTOMIZE", ref y, 30, 50, GoCustomize, false);
            AddEntry(scr, entries, "SETTINGS", ref y, 30, 50, GoSettings, false);
            AddEntry(scr, entries, "EXIT", ref y, 30, 50, Exit, false);

            // ---- Join Room: an accordion that opens under its own entry.
            Entry join = entries.Find(e => e.IsJoin);
            RectTransform panel = K.Node(scr, "JoinPanel").TL(140, join.BaseY + 74, 640, 210);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            K.Img(panel, "Rule", ConceptSprites.White(), Red).rectTransform.TL(4, 4, 3, 196);
            TextMeshProUGUI codeLabel = T(panel, "CodeLabel", "ROOM CODE", 16, Red);
            codeLabel.rectTransform.TL(30, 0, 400, 24);
            codeLabel.characterSpacing = 10;

            joinInput = K.Input(panel, "CodeInput", Bowlby, 46, Bone, "ENTER CODE", Muted.A(0.6f), TextAlignmentOptions.MidlineLeft,
                new Vector4(0, 0, 0, 0), null, new Color(0, 0, 0, 0), Soft);
            joinInput.GetComponent<RectTransform>().TL(30, 30, 430, 66);
            joinInput.textComponent.characterSpacing = 14;
            ((TextMeshProUGUI)joinInput.placeholder).characterSpacing = 4;
            ((TextMeshProUGUI)joinInput.placeholder).fontSize = 34;
            SetupJoinInput(joinInput);
            Image underline = K.Img(panel, "Underline", ConceptSprites.White(), Bone.A(0.3f));
            underline.rectTransform.TL(30, 98, 430, 2);
            Bind(() => underline.color = joinInput.isFocused ? Bone.A(0.75f) : Bone.A(0.3f));

            TextMeshProUGUI message = T(panel, "Message", string.Empty, 16, Red);
            message.rectTransform.TL(30, 106, 430, 24);
            message.characterSpacing = 6;
            Bind(() => message.text = S.JoinMessage);

            TextLink(panel, "JoinButton", "JOIN", 40, true, () => ConfirmJoin(joinInput)).TL(28, 138, 190, 60);
            TextLink(panel, "BackButton", "BACK", 26, false, CloseJoin).TL(250, 146, 140, 44);

            Animator(scr, "JoinAnimator", () => S.JoinOpen, t =>
            {
                foreach (Entry e in entries)
                {
                    bool below = e.BaseY > join.BaseY;
                    e.Rect.anchoredPosition = new Vector2(e.Rect.anchoredPosition.x, -(e.BaseY + (below ? 228f * t : 0f)));
                    if (e.Group == null) continue;
                    e.Group.alpha = e.IsJoin ? 1f : 1f - 0.85f * t;
                    e.Group.interactable = e.Group.blocksRaycasts = t < 0.01f;
                }
                panelGroup.alpha = t;
                panelGroup.interactable = panelGroup.blocksRaycasts = t > 0.99f;
                panel.anchoredPosition = new Vector2(140 - 16 * (1 - t), -(join.BaseY + 74));
            });
        }

        void AddEntry(RectTransform parent, List<Entry> list, string label, ref float y, float size, float step, Action click, bool isJoin)
        {
            var e = new Entry { BaseY = y, IsJoin = isJoin };
            Button b = MenuWord(parent, label, size, click, () => isJoin && S.JoinOpen, out _);
            e.Rect = b.GetComponent<RectTransform>().TL(140, y, 620, size * 1.35f);
            e.Group = b.gameObject.AddComponent<CanvasGroup>();
            list.Add(e);
            y += step;
        }

        /// <summary>
        /// The main menu's entry: bone text that slides right on hover while a red blade
        /// grows in front of it. <paramref name="held"/> keeps it lit (active state).
        /// </summary>
        protected Button MenuWord(Transform parent, string label, float size, Action click, Func<bool> held, out ConceptFx fx)
        {
            Button b = Btn(parent, label, click, null, out fx);
            TextMeshProUGUI text = T(b.transform, "Label", label, size, Dim, TextAlignmentOptions.MidlineLeft, size > 40);
            text.rectTransform.Fill();
            Image bar = K.Img(b.transform, "Blade", ConceptSprites.White(), Red);
            bar.rectTransform.At(0, 0.5f, 1, 0.5f, -16, 0, 0, size > 40 ? 5 : 4);
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(Mathf.Max(f.Hover, held() ? 1f : 0f));
                text.color = K.Lerp(Dim, Bone, h);
                text.rectTransform.offsetMin = new Vector2(16 * h, 0);
                text.rectTransform.offsetMax = new Vector2(16 * h, 0);
                bar.rectTransform.sizeDelta = new Vector2(30 * h, bar.rectTransform.sizeDelta.y);
                bar.color = Red.A(h);
                float s = 1f - 0.03f * f.Press;
                b.transform.localScale = new Vector3(s, s, 1);
            };
            return b;
        }

        /// <summary>A plain text action with a chevron that slides in on hover.</summary>
        protected RectTransform TextLink(Transform parent, string name, string label, float size, bool primary, Action click)
        {
            Button b = Btn(parent, name, click, null, out ConceptFx fx);
            TextMeshProUGUI text = T(b.transform, "Label", label, size, primary ? Bone : Dim, TextAlignmentOptions.MidlineLeft, primary);
            text.rectTransform.Fill();
            Image chev = K.Img(b.transform, "Chevron", ConceptSprites.Chevron(), Red);
            float w = text.GetPreferredValues(label).x;
            chev.rectTransform.At(0, 0.5f, 0, 0.5f, w + 12, 0, size * 0.36f, size * 0.54f);
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                bool on = f.IsActive;
                text.color = !on ? Muted : primary ? Bone : K.Lerp(Dim, Bone, h);
                chev.color = Red.A(on ? (primary ? 0.6f + 0.4f * h : h) : 0f);
                chev.rectTransform.anchoredPosition = new Vector2(w + 12 + 8 * h, 0);
            };
            return b.GetComponent<RectTransform>();
        }

        protected void LeftShade(RectTransform parent, float width, float alpha)
        {
            Image shade = K.Img(parent, "LeftShade", ConceptSprites.FadeRight(), new Color(0, 0, 0, alpha));
            shade.rectTransform.At(0, 0.5f, 0, 0.5f, 0, 0, width, 1080);
            shade.rectTransform.anchorMin = new Vector2(0, 0);
            shade.rectTransform.anchorMax = new Vector2(0, 1);
            shade.rectTransform.sizeDelta = new Vector2(width, 0);
        }

        protected void RightShade(RectTransform parent, float width, float alpha)
        {
            Image shade = K.Img(parent, "RightShade", ConceptSprites.FadeRight(), new Color(0, 0, 0, alpha));
            shade.rectTransform.At(1, 0.5f, 0.5f, 0.5f, -width * 0.5f, 0, width, 1080);
            shade.rectTransform.localScale = new Vector3(-1, 1, 1);
        }

        protected RectTransform BackLink(Transform parent, Action click)
        {
            Button b = Btn(parent, "Back", click, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(76, 56, 200, 48);
            Image chev = K.Img(b.transform, "Chevron", ConceptSprites.Chevron(), Red);
            chev.rectTransform.At(0, 0.5f, 0, 0.5f, 0, 0, 14, 21);
            chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            TextMeshProUGUI text = T(b.transform, "Label", "BACK", 26, Dim);
            text.rectTransform.Fill(30, 0, 0, 0);
            text.characterSpacing = 4;
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                text.color = K.Lerp(Dim, Bone, h);
                chev.rectTransform.anchoredPosition = new Vector2(14 - 6 * h, 0);
            };
            return rt;
        }

        /// <summary>
        /// The nickname as a name-plate over the character, centred on <paramref name="centreX"/>:
        /// "NICKNAME" in red, the name itself as the input, an underline and a pencil that
        /// follow the text width.
        /// </summary>
        protected void NicknameOverCharacter(RectTransform scr, float centreX, float y, float size)
        {
            TextMeshProUGUI label = T(scr, "NicknameLabel", "NICKNAME", 15, Red, TextAlignmentOptions.Center);
            label.rectTransform.At(0, 1, 0.5f, 1, centreX, -y, 400, 22);
            label.characterSpacing = 10;
            float inputH = size * 1.35f;
            TMP_InputField nick = K.Input(scr, "Nickname", Bowlby, size, Bone, "YOUR NAME", Muted, TextAlignmentOptions.Center,
                Vector4.zero, null, new Color(0, 0, 0, 0), Strong);
            nick.GetComponent<RectTransform>().At(0, 1, 0.5f, 1, centreX, -(y + 20), 720, inputH);
            SetupNickname(nick);
            Image line = K.Img(scr, "NicknameLine", ConceptSprites.White(), Bone.A(0.18f));
            line.rectTransform.At(0, 1, 0.5f, 1, centreX, -(y + 22 + inputH), 400, 2);
            Image pencil = K.Img(scr, "Pencil", ConceptSprites.Pencil(), Muted);
            pencil.rectTransform.At(0, 1, 0.5f, 0.5f, centreX + 200, -(y + 20 + inputH * 0.5f), 24, 24);
            var fx = nick.gameObject.AddComponent<ConceptFx>();
            Track(fx);
            fx.Apply = f =>
            {
                float h = Mathf.Max(ConceptFx.Ease(f.Hover), nick.isFocused ? 1f : 0f);
                line.color = K.Lerp(Bone.A(0.18f), Red, h);
                pencil.color = K.Lerp(Muted, Bone, h);
                float w = Mathf.Min(nick.textComponent.GetPreferredValues(nick.text).x, 660f);
                pencil.rectTransform.anchoredPosition = new Vector2(centreX + w * 0.5f + 22, -(y + 20 + inputH * 0.5f));
                line.rectTransform.sizeDelta = new Vector2(w + 40, 2);
            };
        }

        /// <summary>A text tab with a red underline that draws in when selected.</summary>
        protected ConceptFx TabWord(RectTransform parent, string label, float x, float y, float size, Action click)
        {
            Button b = Btn(parent, label, click, null, out ConceptFx fx);
            TextMeshProUGUI text = T(b.transform, "Label", label, size, Muted, TextAlignmentOptions.MidlineLeft, size > 40);
            text.characterSpacing = size < 40 ? 4 : 0;
            float tw = text.GetPreferredValues(label).x;
            b.GetComponent<RectTransform>().TL(x, y, tw + 8, size * 1.6f);
            text.rectTransform.Fill();
            Image line = K.Img(b.transform, "Underline", ConceptSprites.White(), Red);
            line.rectTransform.At(0, 0, 0, 0.5f, 0, 2, tw, size < 40 ? 3 : 4);
            fx.Apply = f =>
            {
                float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                text.color = K.Lerp(K.Lerp(Muted, Dim, h), Bone, s);
                line.rectTransform.sizeDelta = new Vector2(tw * s, line.rectTransform.sizeDelta.y);
            };
            return fx;
        }

        protected void DragHint(RectTransform scr, float centreX, float bottom, out Graphic icon, out Graphic text)
        {
            Image i = K.Img(scr, "RotateIcon", ConceptSprites.RotateArrows(), Bone.A(0.45f));
            i.rectTransform.At(0, 0, 0.5f, 0, centreX, bottom + 30, 120, 50);
            TextMeshProUGUI t = T(scr, "DragHint", "DRAG TO ROTATE", 14, Bone.A(0.5f), TextAlignmentOptions.Center);
            t.rectTransform.At(0, 0, 0.5f, 0, centreX, bottom, 400, 24);
            t.characterSpacing = 10;
            icon = i;
            text = t;
        }

        // ================================================================== CUSTOMIZE

        const float ListX = 140, CatTop = 236, CatStep = 66;
        const float GridX = 30;                                // grid indent under its category
        const float Box = 150, BoxGap = 16, BoxStep = Box + BoxGap;
        const int GridCols = 3, VisibleRows = 3, MaxBoxes = 15;
        const float GridW = GridCols * Box + (GridCols - 1) * BoxGap;
        const float ActionH = 64;                              // the equip line under the grid

        protected virtual void BuildCustomize()
        {
            RectTransform scr = CustomizeScreen;

            Graphic hintIcon = null, hint = null;
            DragZone(scr, dragging =>
            {
                if (hint != null) hint.CrossFadeAlpha(dragging ? 0f : 1f, 0.2f, true);
                if (hintIcon != null) hintIcon.CrossFadeAlpha(dragging ? 0f : 1f, 0.2f, true);
            }, 960, 960);

            // Same light as the main menu: dark on the left for the list, open on the right.
            LeftShade(scr, 1250, 0.92f);
            LeftShade(scr, 900, 0.55f);

            BackLink(scr, GoMenu);
            NicknameOverCharacter(scr, 1382, 82, 48);
            DragHint(scr, 1382, 40, out hintIcon, out hint);

            var tabs = new ConceptFx[2];
            tabs[0] = TabWord(scr, "WARDROBE", 142, 140, 24, () => SetTab(CustomizeTab.Wardrobe));
            float shopX = 142 + tabs[0].GetComponent<RectTransform>().sizeDelta.x + 30;
            tabs[1] = TabWord(scr, "SHOP", shopX, 140, 24, () => SetTab(CustomizeTab.Shop));
            Bind(() => { tabs[0].Selected = S.Tab == CustomizeTab.Wardrobe; tabs[1].Selected = S.Tab == CustomizeTab.Shop; });
            K.Img(scr, "Rule", ConceptSprites.White(), Bone.A(0.22f)).rectTransform.TL(144, 206, 56, 2);

            RectTransform wardrobe = K.Node(scr, "Wardrobe").Fill();
            RectTransform shop = K.Node(scr, "Shop").Fill();
            Bind(() => { K.SetActive(wardrobe, S.Tab == CustomizeTab.Wardrobe); K.SetActive(shop, S.Tab == CustomizeTab.Shop); });

            BuildWardrobeList(wardrobe);
            BuildShopList(shop);
        }

        void BuildWardrobeList(RectTransform root)
        {
            // ---- Categories: the main menu's own entries.
            var cats = new RectTransform[3];
            var catY = new float[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = (WardrobeSlot)i;
                Button b = MenuWord(root, UIConceptState.SlotName(slot), 46, () => SetSlot(slot), () => S.Slot == slot, out _);
                b.name = "Cat_" + slot;
                cats[i] = b.GetComponent<RectTransform>().TL(ListX, CatTop + i * CatStep, 500, 62);
                catY[i] = CatTop + i * CatStep;
            }

            // ---- The unfolded box grid, with the Join Room's red rule.
            RectTransform block = K.Node(root, "Items").TL(ListX, 0, GridX + GridW + 40, VisibleRows * BoxStep + ActionH);
            var blockGroup = block.gameObject.AddComponent<CanvasGroup>();
            Image rule = K.Img(block, "Rule", ConceptSprites.White(), Red);
            rule.rectTransform.TL(4, 4, 3, 100);

            K.ScrollParts scroll = K.Scroll(block, "Grid", true, Bone.A(0.12f), Red);
            RectTransform barRt = scroll.Bar.GetComponent<RectTransform>();
            for (int i = 0; i < MaxBoxes; i++) BuildBox(scroll.Content, i);

            BuildEquipLine(block);

            float blockY = CatTop + CatStep;
            WardrobeSlot shownSlot = S.Slot;

            Bind(() =>
            {
                int rows = Mathf.CeilToInt(SlotItems(S.Slot).Count / (float)GridCols);
                scroll.Content.sizeDelta = new Vector2(0, Mathf.Max(0, rows * BoxStep - BoxGap));
            });

            // Smoothly re-stack the categories around the open grid every frame.
            Animator(root, "Accordion", () => true, _ =>
            {
                float gridH = GridHeight(SlotItems(S.Slot).Count);
                var gridRt = scroll.Scroll.GetComponent<RectTransform>();
                gridRt.TL(GridX, 0, GridW, gridH);
                barRt.TL(GridW + 12, 0, 10, gridH);
                block.sizeDelta = new Vector2(block.sizeDelta.x, gridH + ActionH);

                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
                float y = CatTop;
                float target = 0;
                for (int i = 0; i < 3; i++)
                {
                    catY[i] = Mathf.Lerp(catY[i], y, k);
                    cats[i].anchoredPosition = new Vector2(ListX, -catY[i]);
                    y += CatStep;
                    if (i == (int)S.Slot)
                    {
                        target = y + 2;
                        y += gridH + ActionH + 18;
                    }
                }
                if (shownSlot != S.Slot)
                {
                    shownSlot = S.Slot;
                    blockGroup.alpha = 0f;
                    blockY = target;
                    scroll.Scroll.verticalNormalizedPosition = 1f;
                }
                blockY = Mathf.Lerp(blockY, target, k);
                block.anchoredPosition = new Vector2(ListX, -blockY);
                blockGroup.alpha = Mathf.MoveTowards(blockGroup.alpha, 1f, Time.unscaledDeltaTime * 5f);
                rule.rectTransform.sizeDelta = new Vector2(3, gridH + ActionH - 10);
            });
        }

        static float GridHeight(int count)
        {
            int rows = Mathf.Clamp(Mathf.CeilToInt(count / (float)GridCols), 1, VisibleRows);
            return rows * BoxStep - BoxGap;
        }

        /// <summary>One item box: a square frame, the item inside, its name along the bottom edge.</summary>
        void BuildBox(RectTransform content, int index)
        {
            int col = index % GridCols, row = index / GridCols;
            Button b = Btn(content, "Box" + index, null, null, out ConceptFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(col * BoxStep, row * BoxStep, Box, Box);

            Image fill = K.Img(rt, "Fill", ConceptSprites.RoundRect(6), new Color(0, 0, 0, 0.35f));
            fill.rectTransform.Fill();
            Image frame = K.Img(rt, "Frame", ConceptSprites.RoundRectOutline(6, 2), Bone.A(0.32f));
            frame.rectTransform.Fill();
            Image select = K.Img(rt, "Select", ConceptSprites.RoundRectOutline(6, 3), Red);
            select.rectTransform.Fill();

            RawImage thumb = K.Raw(rt, "Thumb", null, Color.white);
            thumb.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -60, 100, 100);
            Image lockIcon = K.Img(rt, "Lock", ConceptSprites.Lock(), Dim);
            lockIcon.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -60, 26, 31);
            TextMeshProUGUI name = T(rt, "Name", "", 13, Dim, TextAlignmentOptions.Bottom);
            name.rectTransform.Fill(8, 0, 8, 10);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -14;

            Image check = K.Img(rt, "Equipped", ConceptSprites.Check(), Red);
            check.rectTransform.At(1, 1, 0.5f, 0.5f, -16, -16, 16, 16);
            Image defMark = K.Img(rt, "DefaultMark", ConceptSprites.NoneMark(), Muted);
            defMark.rectTransform.At(0, 1, 0.5f, 0.5f, 16, -16, 15, 15);

            CosmeticItem item = null;
            b.onClick.AddListener(() => { if (item != null) S.Select(item); });

            Bind(() =>
            {
                List<CosmeticItem> items = SlotItems(S.Slot);
                item = index < items.Count ? items[index] : null;
                K.SetActive(rt, item != null);
                if (item == null) return;
                thumb.texture = item.Thumbnail;
                thumb.color = item.Locked ? new Color(0.35f, 0.33f, 0.31f, 0.65f) : Color.white;
                K.SetActive(lockIcon, item.Locked);
                K.SetActive(check, S.IsEquipped(item));
                K.SetActive(defMark, item.IsDefault);
                name.text = item.Name;
                fx.Selected = S.IsSelected(item);
            });

            fx.Apply = f =>
            {
                float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                bool locked = item != null && item.Locked;
                fill.color = K.Lerp(new Color(0, 0, 0, 0.35f), Bone.A(0.08f), Mathf.Max(s, h * 0.6f));
                frame.color = Bone.A(0.32f + 0.4f * h);
                select.color = Red.A(s);
                name.color = K.Lerp(locked ? Muted : Dim, Bone, Mathf.Max(s, h * 0.7f));
                float sc = 1f + 0.06f * Mathf.Max(s, h) - 0.04f * f.Press;
                thumb.rectTransform.localScale = new Vector3(sc, sc, 1);
            };
        }

        /// <summary>Under the grid: the chosen item's name and state, and EQUIP when it can be equipped.</summary>
        void BuildEquipLine(RectTransform block)
        {
            RectTransform line = K.Node(block, "EquipLine");
            line.anchorMin = line.anchorMax = new Vector2(0, 0);
            line.pivot = new Vector2(0, 0);
            line.anchoredPosition = new Vector2(GridX, 0);
            line.sizeDelta = new Vector2(GridW + 24, ActionH - 12);

            TextMeshProUGUI name = T(line, "Name", "", 24, Bone, TextAlignmentOptions.TopLeft, true);
            name.rectTransform.TL(0, 0, GridW - 150, 30);
            TextMeshProUGUI state = T(line, "State", "", 13, Muted, TextAlignmentOptions.TopLeft);
            state.rectTransform.TL(2, 32, GridW - 150, 20);
            state.characterSpacing = 6;

            RectTransform equip = TextLink(line, "Equip", "EQUIP", 30, true, () => S.EquipSelected());
            equip.At(1, 1, 1, 1, -24, 0, 130, 46);

            Bind(() =>
            {
                CosmeticItem sel = S.SelectedItem(S.Slot);
                if (sel == null) return;
                name.text = sel.Name;
                bool eq = S.IsEquipped(sel);
                string rarity = sel.IsDefault ? "DEFAULT" : UIConceptState.RarityName(sel.Rarity);
                state.text = rarity + "   ·   " + (eq ? "EQUIPPED" : sel.Locked ? "LOCKED" : "PREVIEWING");
                state.color = eq ? Red : Muted;
                K.SetActive(equip, S.CanEquipSelected());
            });
        }

        void BuildShopList(RectTransform root)
        {
            TextMeshProUGUI big = T(root, "Title", "COMING SOON", 64, Bone, TextAlignmentOptions.MidlineLeft, true);
            big.rectTransform.TL(ListX, CatTop, 700, 84);
            K.Img(root, "Rule", ConceptSprites.White(), Red).rectTransform.TL(ListX + 4, CatTop + 94, 56, 3);
            TextMeshProUGUI line = T(root, "Line", "THE MERCHANT IS STILL ON THE ROAD.", 20, Dim);
            line.rectTransform.TL(ListX + 2, CatTop + 112, 700, 30);
            line.characterSpacing = 4;

            // Empty locked boxes in the wardrobe's shape, fading away.
            for (int i = 0; i < 6; i++)
            {
                int col = i % GridCols, row = i / GridCols;
                float a = row == 0 ? 1f : 0.45f;
                RectTransform box = K.Node(root, "Ghost" + i).TL(ListX + GridX + col * BoxStep, CatTop + 180 + row * BoxStep, Box, Box);
                K.Img(box, "Frame", ConceptSprites.RoundRectOutline(6, 2), Bone.A(0.14f * a)).rectTransform.Fill();
                K.Img(box, "Lock", ConceptSprites.Lock(), Bone.A(0.18f * a)).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 8, 26, 31);
                K.Img(box, "Name", ConceptSprites.RoundRect(4), Bone.A(0.07f * a)).rectTransform.At(0.5f, 0, 0.5f, 0, 0, 16, 80, 9);
            }
        }

        // ================================================================== SETTINGS

        void BuildSettings()
        {
            RectTransform scr = SettingsScreen;
            LeftShade(scr, 1500, 0.92f);
            LeftShade(scr, 1150, 0.7f);
            BackLink(scr, GoMenu);

            TextMeshProUGUI title = T(scr, "Title", "SETTINGS", 64, Bone, TextAlignmentOptions.MidlineLeft, true);
            title.rectTransform.TL(140, 118, 700, 80);

            float y = 236;
            Section(scr, "AUDIO", ref y);
            SliderRow(scr, "MASTER VOLUME", ref y, 0, 1, () => S.MasterVolume, v => S.MasterVolume = v, UIConceptState.Percent);
            SliderRow(scr, "MUSIC VOLUME", ref y, 0, 1, () => S.MusicVolume, v => S.MusicVolume = v, UIConceptState.Percent);
            SliderRow(scr, "EFFECTS VOLUME", ref y, 0, 1, () => S.EffectsVolume, v => S.EffectsVolume = v, UIConceptState.Percent);
            Section(scr, "CONTROLS", ref y);
            SliderRow(scr, "MOUSE SENSITIVITY", ref y, UIConceptState.MinSensitivity, UIConceptState.MaxSensitivity, () => S.MouseSensitivity, v => S.MouseSensitivity = v, UIConceptState.Sensitivity);
            Section(scr, "DISPLAY", ref y);
            SelectorRow(scr, "RESOLUTION", ref y);
            OptionRow(scr, "DISPLAY MODE", ref y, "FULLSCREEN", "WINDOWED", () => S.Fullscreen, v => S.Fullscreen = v);
            OptionRow(scr, "V-SYNC", ref y, "ON", "OFF", () => S.VSync, v => S.VSync = v);

            TextMeshProUGUI foot = T(scr, "Footnote", "CHANGES APPLY IMMEDIATELY", 13, Muted);
            foot.rectTransform.TL(142, y + 22, 600, 20);
            foot.characterSpacing = 8;
        }

        void Section(RectTransform parent, string label, ref float y)
        {
            TextMeshProUGUI t = T(parent, "Section_" + label, label, 15, Red);
            t.rectTransform.TL(142, y + 18, 300, 24);
            t.characterSpacing = 12;
            float w = t.GetPreferredValues(label).x;
            K.Img(parent, "SectionRule_" + label, ConceptSprites.White(), Bone.A(0.1f)).rectTransform.TL(142 + w + 18, y + 30, 730 - w - 18, 1);
            y += 52;
        }

        RectTransform Row(RectTransform parent, string label, ref float y, out TextMeshProUGUI labelText)
        {
            RectTransform row = K.Node(parent, "Row_" + label).TL(140, y, 740, 60);
            labelText = T(row, "Label", label, 24, Dim);
            labelText.rectTransform.TL(0, 0, 400, 60);
            y += 62;
            return row;
        }

        void SliderRow(RectTransform parent, string label, ref float y, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
        {
            RectTransform row = Row(parent, label, ref y, out TextMeshProUGUI labelText);
            TextMeshProUGUI value = T(row, "Value", "", 24, Bone, TextAlignmentOptions.MidlineRight);
            value.rectTransform.TL(640, 0, 90, 60);

            K.SliderParts sl = K.Slider(row, "Slider", min, max, get(), v => { set(v); S.Notify(); },
                ConceptSprites.White(), ConceptSprites.White(), D.MenuDiamond, 2, new Vector2(20, 20));
            sl.Slider.GetComponent<RectTransform>().TL(380, 10, 240, 40);
            sl.Track.color = Bone.A(0.16f);
            sl.Fill.color = Bone.A(0.8f);
            Track(sl.Fx);
            sl.Fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                sl.Handle.color = K.Lerp(Bone, Red, h);
                sl.Handle.rectTransform.localScale = Vector3.one * (1f + 0.3f * h);
                labelText.color = K.Lerp(Dim, Bone, h);
            };
            Bind(() => { sl.Slider.SetValueWithoutNotify(get()); value.text = format(get()); });
        }

        void SelectorRow(RectTransform parent, string label, ref float y)
        {
            RectTransform row = Row(parent, label, ref y, out _);
            TextMeshProUGUI value = T(row, "Value", "", 24, Bone, TextAlignmentOptions.Center);
            value.rectTransform.TL(410, 0, 200, 60);
            Bind(() => value.text = UIConceptState.Resolutions[S.ResolutionIndex]);
            Arrow(row, 376, -1);
            Arrow(row, 614, +1);
        }

        void Arrow(RectTransform row, float x, int dir)
        {
            Button b = Btn(row, dir < 0 ? "Prev" : "Next", () =>
            {
                int n = UIConceptState.Resolutions.Length;
                S.ResolutionIndex = (S.ResolutionIndex + dir + n) % n;
                S.Notify();
            }, null, out ConceptFx fx);
            b.GetComponent<RectTransform>().TL(x, 10, 40, 40);
            Image chev = K.Img(b.transform, "Chevron", ConceptSprites.Chevron(), Dim);
            chev.rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 14, 21);
            if (dir < 0) chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            fx.Apply = f =>
            {
                float h = ConceptFx.Ease(f.Hover);
                chev.color = K.Lerp(Dim, Red, h);
                chev.rectTransform.anchoredPosition = new Vector2(dir * 4 * h, 0);
            };
        }

        void OptionRow(RectTransform parent, string label, ref float y, string a, string b, Func<bool> get, Action<bool> set)
        {
            RectTransform row = Row(parent, label, ref y, out _);
            ConceptFx fa = OptionWord(row, a, 380, () => { set(true); S.Notify(); });
            ConceptFx fb = OptionWord(row, b, 570, () => { set(false); S.Notify(); });
            Bind(() => { fa.Selected = get(); fb.Selected = !get(); });
        }

        ConceptFx OptionWord(RectTransform row, string label, float x, Action click)
        {
            Button b = Btn(row, label, click, null, out ConceptFx fx);
            b.GetComponent<RectTransform>().TL(x, 6, 200, 48);
            TextMeshProUGUI text = T(b.transform, "Label", label, 22, Muted);
            text.rectTransform.Fill();
            text.characterSpacing = 3;
            float tw = text.GetPreferredValues(label).x;
            Image line = K.Img(b.transform, "Underline", ConceptSprites.White(), Red);
            line.rectTransform.At(0, 0, 0, 0.5f, 0, 4, tw, 3);
            fx.Apply = f =>
            {
                float s = ConceptFx.Ease(f.Select), h = ConceptFx.Ease(f.Hover);
                text.color = K.Lerp(K.Lerp(Muted, Dim, h), Bone, s);
                line.rectTransform.sizeDelta = new Vector2(tw * s, 3);
            };
            return fx;
        }
    }
}
