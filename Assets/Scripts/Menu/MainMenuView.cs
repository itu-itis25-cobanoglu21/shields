using System;
using System.Collections.Generic;
using Shields.Settings;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.Menu
{
    /// <summary>
    /// Builds the main menu UI from code: the main menu with its Offline / Join Room /
    /// Create Room panels, Customize (wardrobe + shop) and Settings. Typographic and
    /// nearly frameless over the live tavern. Reads <see cref="MainMenuState"/> through
    /// bind callbacks and calls back into <see cref="MainMenuController"/> for actions.
    /// </summary>
    public sealed class MainMenuView
    {
        static readonly Color Bone = TavernUI.Bone, Dim = TavernUI.Dim, Muted = TavernUI.Muted, Red = TavernUI.Red;

        readonly MainMenuController _c;
        readonly MainMenuState S;
        readonly TavernUI _ui;
        readonly CanvasGroup _rootGroup;

        public readonly Canvas Canvas;
        RectTransform _menu, _customize, _settings;

        public MainMenuView(MainMenuController controller, MainMenuState state, TMP_FontAsset bowlby, TMP_FontAsset knewave, Material logoStroke)
        {
            _c = controller;
            S = state;
            _ui = new TavernUI(bowlby, knewave);
            _logoStroke = logoStroke;

            Canvas = TavernUI.MakeCanvas(controller.transform, "MainMenuCanvas", 10);
            RectTransform root = (RectTransform)Canvas.transform;
            _rootGroup = Canvas.gameObject.AddComponent<CanvasGroup>();

            // Shared atmosphere: a gentle vignette, cool night blue on Customize.
            Image vig = UiKit.Img(root, "Vignette", UiSprites.Vignette(), new Color(0, 0, 0, 0.6f));
            vig.rectTransform.Fill();
            Bind(() => vig.color = (S.Screen == MenuScreen.Customize ? TavernUI.NightShade : Color.black).A(0.6f));

            _menu = UiKit.Node(root, "MainMenu").Fill();
            _customize = UiKit.Node(root, "Customize").Fill();
            _settings = UiKit.Node(root, "Settings").Fill();

            BuildMenu();
            BuildCustomize();
            BuildSettings();
        }

        void Bind(Action bind) { _ui.Bind(bind); }

        public void Refresh()
        {
            UiKit.SetActive(_menu, S.Screen == MenuScreen.MainMenu);
            UiKit.SetActive(_customize, S.Screen == MenuScreen.Customize);
            UiKit.SetActive(_settings, S.Screen == MenuScreen.Settings);
            _rootGroup.interactable = !S.Busy;
            _ui.Refresh();
        }

        TextMeshProUGUI T(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool strong = false)
        {
            return _ui.T(parent, name, text, size, color, align, strong);
        }

        static float Sp(float spacing) { return TavernUI.Sp(spacing); }
        static float Thk(float h) { return TavernUI.Thk(h); }

        void Shade(RectTransform screen, float width, float alpha)
        {
            Color tint = screen == _customize ? TavernUI.NightShade : Color.black;
            TavernUI.Shade(screen, "Shade", width, tint.A(alpha));
        }

        // ------------------------------------------------------------------ logo

        readonly Material _logoStroke;

        /// <summary>The SHIELDS! word mark: an oxblood gradient face with a parchment inline over a dark stroke.</summary>
        RectTransform Logo(Transform parent, float size)
        {
            RectTransform root = UiKit.Node(parent, "Logo");
            Material back = _logoStroke != null ? _logoStroke
                : UiKit.TextMat(_ui.Knewave, "LogoBack", 0.42f, new Color(0.09f, 0.03f, 0.03f, 1f), new Color(0.05f, 0.015f, 0.015f, 0.95f), 0.9f, -1.1f, 0f, 0.42f, 0.18f);
            TextMeshProUGUI stroke = UiKit.Text(root, "Stroke", "SHIELDS!", _ui.Knewave, size, new Color(0.09f, 0.03f, 0.03f), TextAlignmentOptions.Left, back);
            stroke.rectTransform.Fill();
            Material faceMat = UiKit.TextMat(_ui.Knewave, "LogoFaceOxblood", 0.16f, UiKit.Hex("#E2CCA4"), default, 0, 0, 0, 0, 0.06f);
            TextMeshProUGUI face = UiKit.Text(root, "Face", "SHIELDS!", _ui.Knewave, size, Color.white, TextAlignmentOptions.Left, faceMat);
            face.rectTransform.Fill();
            face.enableVertexGradient = true;
            Color top = UiKit.Hex("#A9302A"), bottom = UiKit.Hex("#661316");
            face.colorGradient = new VertexGradient(top, top, bottom, bottom);
            stroke.characterSpacing = face.characterSpacing = -2f;
            return root;
        }

        // ================================================================== MAIN MENU

        sealed class Entry
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float BaseY;
            public MenuPanel Panel;
        }

        /// <summary>A panel that unfolds under its menu entry and pushes the entries below it down.</summary>
        sealed class Fold
        {
            public MenuPanel Kind;
            public Entry Entry;
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Shift;
            public float T;
        }

        const float MenuX = 140, FoldGap = 74;

        TMP_InputField _joinInput;

        void BuildMenu()
        {
            RectTransform scr = _menu;
            Shade(scr, 1150, 0.85f);

            Logo(scr, 128).TL(132, 92, 640, 170);
            TextMeshProUGUI tag = T(scr, "Tagline", MainMenuState.MinPlayers + " – " + MainMenuState.MaxPlayers + " PLAYERS  ·  ONE SURVIVOR", 16, Muted);
            tag.rectTransform.TL(142, 262, 600, 24);
            tag.characterSpacing = Sp(9);

            var entries = new List<Entry>();
            float y = 370;
            AddEntry(scr, entries, "OFFLINE MODE", ref y, 46, 68, () => _c.OpenPanel(MenuPanel.Offline), MenuPanel.Offline);
            AddEntry(scr, entries, "JOIN ROOM", ref y, 46, 68, () => _c.OpenJoin(_joinInput), MenuPanel.Join);
            AddEntry(scr, entries, "CREATE ROOM", ref y, 46, 68, () => _c.OpenPanel(MenuPanel.Create), MenuPanel.Create);

            // A short rule separates playing from everything else.
            var ruleEntry = new Entry { Rect = UiKit.Node(scr, "Rule").TL(144, y + 14, 56, Thk(2)), BaseY = y + 14 };
            TavernUI.Line(ruleEntry.Rect, "Line", Bone.A(0.22f)).rectTransform.Fill();
            ruleEntry.Group = ruleEntry.Rect.gameObject.AddComponent<CanvasGroup>();
            entries.Add(ruleEntry);
            y += 44;

            AddEntry(scr, entries, "CUSTOMIZE", ref y, 30, 50, _c.GoCustomize, MenuPanel.None);
            AddEntry(scr, entries, "SETTINGS", ref y, 30, 50, _c.GoSettings, MenuPanel.None);
            AddEntry(scr, entries, "EXIT", ref y, 30, 50, _c.Exit, MenuPanel.None);

            var folds = new List<Fold>
            {
                BuildTablePanel(scr, EntryFor(entries, MenuPanel.Offline), MenuPanel.Offline, "START", _c.ConfirmOffline, () =>
                    "YOU + " + (S.TablePlayers - 1) + " BOTS   ·   " + S.TableDecks * MainMenuState.CardsPerDeck + " CARDS"),
                BuildJoinPanel(scr, EntryFor(entries, MenuPanel.Join)),
                BuildTablePanel(scr, EntryFor(entries, MenuPanel.Create), MenuPanel.Create, "CREATE", _c.ConfirmCreate, () =>
                    "EMPTY SEATS GO TO BOTS   ·   " + S.TableDecks * MainMenuState.CardsPerDeck + " CARDS"),
            };

            foreach (Fold fold in folds)
            {
                Fold f = fold;
                TavernUI.Animator(scr, f.Kind + "Animator", () => S.Panel == f.Kind, t => { f.T = t; LayoutMenu(entries, folds); });
            }
        }

        static Entry EntryFor(List<Entry> entries, MenuPanel panel) { return entries.Find(e => e.Panel == panel); }

        static void LayoutMenu(List<Entry> entries, List<Fold> folds)
        {
            float open = 0f;
            foreach (Fold f in folds) open = Mathf.Max(open, f.T);

            foreach (Entry e in entries)
            {
                float shift = 0f, own = 0f;
                foreach (Fold f in folds)
                {
                    if (e.BaseY > f.Entry.BaseY) shift += f.Shift * f.T;
                    if (f.Entry == e) own = f.T;
                }
                e.Rect.anchoredPosition = new Vector2(e.Rect.anchoredPosition.x, -(e.BaseY + shift));
                if (e.Group == null) continue;
                e.Group.alpha = 1f - 0.85f * Mathf.Max(0f, open - own);
                e.Group.interactable = e.Group.blocksRaycasts = open < 0.01f;
            }

            foreach (Fold f in folds)
            {
                f.Group.alpha = f.T;
                f.Group.interactable = f.Group.blocksRaycasts = f.T > 0.99f;
                f.Rect.anchoredPosition = new Vector2(MenuX - 16 * (1 - f.T), -(f.Entry.BaseY + FoldGap));
            }
        }

        void AddEntry(RectTransform parent, List<Entry> list, string label, ref float y, float size, float step, Action click, MenuPanel panel)
        {
            var e = new Entry { BaseY = y, Panel = panel };
            Button b = _ui.MenuWord(parent, label, size, click, () => panel != MenuPanel.None && S.Panel == panel, out _);
            e.Rect = b.GetComponent<RectTransform>().TL(MenuX, y, 620, size * 1.35f);
            e.Group = b.gameObject.AddComponent<CanvasGroup>();
            list.Add(e);
            y += step;
        }

        // ---- Join Room: an accordion that opens under its own entry.
        Fold BuildJoinPanel(RectTransform scr, Entry join)
        {
            RectTransform panel = UiKit.Node(scr, "JoinPanel").TL(MenuX, join.BaseY + FoldGap, 640, 210);
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            TavernUI.VLine(panel, "Rule", Red).rectTransform.TL(4, 4, Thk(3), 196);
            _ui.Kicker(panel, "CodeLabel", "ROOM CODE", 16, Red, 10).rectTransform.TL(30, 0, 400, 24);

            TMP_InputField input = UiKit.Input(panel, "CodeInput", _ui.Bowlby, 46, Bone, "ENTER CODE", Muted.A(0.6f), TextAlignmentOptions.MidlineLeft, _ui.Soft);
            input.GetComponent<RectTransform>().TL(30, 30, 430, 66);
            input.textComponent.characterSpacing = Sp(14);
            ((TextMeshProUGUI)input.placeholder).characterSpacing = Sp(4);
            ((TextMeshProUGUI)input.placeholder).fontSize = 34;
            input.characterLimit = 16;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            input.onValueChanged.AddListener(v =>
            {
                string up = v.ToUpperInvariant();
                if (up != v) input.SetTextWithoutNotify(up);
                if (S.Message.Length > 0) { S.Message = string.Empty; S.Notify(); }
            });
            input.onSubmit.AddListener(_ => _c.ConfirmJoin(input.text));
            _joinInput = input;

            Image underline = TavernUI.Line(panel, "Underline", Bone.A(0.3f));
            underline.rectTransform.TL(30, 98, 430, Thk(2));
            Bind(() => underline.color = input.isFocused ? Bone.A(0.75f) : Bone.A(0.3f));

            TextMeshProUGUI message = T(panel, "Message", string.Empty, 16, Red);
            message.rectTransform.TL(30, 106, 430, 24);
            message.characterSpacing = Sp(6);
            Bind(() => message.text = S.Panel == MenuPanel.Join ? S.Message : string.Empty);

            _ui.TextLink(panel, "JoinButton", "JOIN", 40, true, () => _c.ConfirmJoin(input.text)).TL(28, 138, 190, 60);
            _ui.TextLink(panel, "BackButton", "BACK", 26, false, _c.ClosePanel).TL(250, 146, 140, 44);

            return new Fold { Kind = MenuPanel.Join, Entry = join, Rect = panel, Group = group, Shift = 228f };
        }

        // ---- Offline Mode / Create Room: the table setup, in the Join Room's shape.
        const float TableH = 216, ChoiceX = 190;

        Fold BuildTablePanel(RectTransform scr, Entry entry, MenuPanel kind, string confirm, Action confirmClick, Func<string> summary)
        {
            RectTransform panel = UiKit.Node(scr, kind + "Panel").TL(MenuX, entry.BaseY + FoldGap, 640, TableH);
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            TavernUI.VLine(panel, "Rule", Red).rectTransform.TL(4, 4, Thk(3), TableH - 14);

            // Players at the table.
            _ui.Kicker(panel, "PlayersLabel", "PLAYERS", 16, Red, 10).rectTransform.TL(30, 4, 150, 54);
            int count = MainMenuState.MaxPlayers - MainMenuState.MinPlayers + 1;
            var players = new UiFx[count];
            for (int i = 0; i < count; i++)
            {
                int n = MainMenuState.MinPlayers + i;
                players[i] = _ui.TabWord(panel, n.ToString(), ChoiceX + i * 58, 4, 34, () => { S.TablePlayers = n; S.Notify(); });
            }

            // Decks in play.
            _ui.Kicker(panel, "DecksLabel", "DECKS", 16, Red, 10).rectTransform.TL(30, 66, 150, 42);
            int deckOptions = MainMenuState.MaxDecks - MainMenuState.MinDecks + 1;
            var decks = new UiFx[deckOptions];
            float dx = ChoiceX;
            for (int i = 0; i < deckOptions; i++)
            {
                int n = MainMenuState.MinDecks + i;
                decks[i] = _ui.TabWord(panel, n == 1 ? "1 DECK" : n + " DECKS", dx, 66, 26, () => { S.TableDecks = n; S.Notify(); });
                dx += decks[i].GetComponent<RectTransform>().sizeDelta.x + 40;
            }

            Bind(() =>
            {
                for (int i = 0; i < players.Length; i++) players[i].Selected = S.TablePlayers == MainMenuState.MinPlayers + i;
                for (int i = 0; i < decks.Length; i++) decks[i].Selected = S.TableDecks == MainMenuState.MinDecks + i;
            });

            // Summary, or the reason the game could not open.
            TextMeshProUGUI line = T(panel, "Summary", string.Empty, 16, Muted);
            line.rectTransform.TL(30, 118, 600, 24);
            line.characterSpacing = Sp(6);
            Bind(() =>
            {
                bool error = S.Panel == kind && S.Message.Length > 0;
                line.text = error ? S.Message : summary();
                line.color = error ? Red : Muted;
            });

            RectTransform go = _ui.TextLink(panel, "ConfirmButton", confirm, 40, true, confirmClick).TL(28, 150, 240, 60);
            _ui.TextLink(panel, "BackButton", "BACK", 26, false, _c.ClosePanel).TL(Mathf.Max(250, 28 + TavernUI.LinkWidth(go) + 72), 158, 140, 44);

            return new Fold { Kind = kind, Entry = entry, Rect = panel, Group = group, Shift = TableH + 18 };
        }

        // ================================================================== CUSTOMIZE

        const float ListX = 140, CatTop = 236, CatStep = 66;
        const float GridX = 30;                                // grid indent under its category
        const float Box = 150, BoxGap = 16, BoxStep = Box + BoxGap;
        const int GridCols = 3, VisibleRows = 3;
        const float GridW = GridCols * Box + (GridCols - 1) * BoxGap;
        const float ActionH = 64;                              // the equip line under the grid

        void BuildCustomize()
        {
            RectTransform scr = _customize;

            Graphic hintIcon = null, hint = null;
            DragZone(scr, dragging =>
            {
                if (hint != null) hint.CrossFadeAlpha(dragging ? 0f : 1f, 0.2f, true);
                if (hintIcon != null) hintIcon.CrossFadeAlpha(dragging ? 0f : 1f, 0.2f, true);
            }, 960, 960);

            // Dark on the left for the list, open on the right for the character.
            Shade(scr, 1250, 0.92f);
            Shade(scr, 900, 0.55f);

            _ui.BackLink(scr, _c.GoMenu);
            Nickname(scr, 1382, 82, 48);
            DragHint(scr, 1382, 40, out hintIcon, out hint);

            var tabs = new UiFx[2];
            tabs[0] = _ui.TabWord(scr, "WARDROBE", 142, 140, 24, () => _c.SetTab(CustomizeTab.Wardrobe));
            float shopX = 142 + tabs[0].GetComponent<RectTransform>().sizeDelta.x + 30;
            tabs[1] = _ui.TabWord(scr, "SHOP", shopX, 140, 24, () => _c.SetTab(CustomizeTab.Shop));
            Bind(() => { tabs[0].Selected = S.Tab == CustomizeTab.Wardrobe; tabs[1].Selected = S.Tab == CustomizeTab.Shop; });
            TavernUI.Line(scr, "Rule", Bone.A(0.22f)).rectTransform.TL(144, 206, 56, Thk(2));

            RectTransform wardrobe = UiKit.Node(scr, "Wardrobe").Fill();
            RectTransform shop = UiKit.Node(scr, "Shop").Fill();
            Bind(() => { UiKit.SetActive(wardrobe, S.Tab == CustomizeTab.Wardrobe); UiKit.SetActive(shop, S.Tab == CustomizeTab.Shop); });

            BuildWardrobe(wardrobe);
            BuildShop(shop);
        }

        /// <summary>Invisible drag surface over the character, behind the rest of the UI.</summary>
        void DragZone(RectTransform parent, Action<bool> dragChanged, float x, float width)
        {
            Image img = UiKit.Img(parent, "PreviewDragZone", null, new Color(0, 0, 0, 0), true);
            img.rectTransform.anchorMin = new Vector2(0, 0);
            img.rectTransform.anchorMax = new Vector2(0, 1);
            img.rectTransform.pivot = new Vector2(0, 0.5f);
            img.rectTransform.anchoredPosition = new Vector2(x, 0);
            img.rectTransform.sizeDelta = new Vector2(width, 0);
            var zone = img.gameObject.AddComponent<CharacterDragZone>();
            zone.Turntable = _c.Turntable;
            zone.DragChanged = dragChanged;
        }

        void DragHint(RectTransform scr, float centreX, float bottom, out Graphic icon, out Graphic text)
        {
            Image i = UiKit.Img(scr, "RotateIcon", UiSprites.RotateArrows(), Bone.A(0.45f));
            i.rectTransform.At(0, 0, 0.5f, 0, centreX, bottom + 30, 120, 50);
            TextMeshProUGUI t = T(scr, "DragHint", "DRAG TO ROTATE", 14, Bone.A(0.5f), TextAlignmentOptions.Center);
            t.rectTransform.At(0, 0, 0.5f, 0, centreX, bottom, 400, 24);
            t.characterSpacing = Sp(10);
            icon = i;
            text = t;
        }

        /// <summary>The nickname as a name-plate over the character, with an underline and a pencil that follow the text.</summary>
        void Nickname(RectTransform scr, float centreX, float y, float size)
        {
            _ui.Kicker(scr, "NicknameLabel", "NICKNAME", 15, Red, 10, TextAlignmentOptions.Center).rectTransform.At(0, 1, 0.5f, 1, centreX, -y, 400, 22);
            float inputH = size * 1.35f;
            TMP_InputField nick = UiKit.Input(scr, "Nickname", _ui.Bowlby, size, Bone, "YOUR NAME", Muted, TextAlignmentOptions.Center, _ui.Strong);
            nick.GetComponent<RectTransform>().At(0, 1, 0.5f, 1, centreX, -(y + 20), 720, inputH);
            nick.characterLimit = PlayerProfile.MaxNicknameLength;
            nick.text = PlayerProfile.Nickname;
            nick.onValueChanged.AddListener(v =>
            {
                string up = v.ToUpperInvariant();
                if (up != v) nick.SetTextWithoutNotify(up);
            });
            nick.onEndEdit.AddListener(v =>
            {
                PlayerProfile.Nickname = v;
                nick.SetTextWithoutNotify(PlayerProfile.Nickname);
            });

            Image line = TavernUI.Line(scr, "NicknameLine", Bone.A(0.18f));
            float lineH = Thk(2);
            line.rectTransform.At(0, 1, 0.5f, 1, centreX, -(y + 22 + inputH), 400, lineH);
            Image pencil = UiKit.Img(scr, "Pencil", UiSprites.Pencil(), Muted);
            pencil.rectTransform.At(0, 1, 0.5f, 0.5f, centreX + 200, -(y + 20 + inputH * 0.5f), 24, 24);
            var fx = nick.gameObject.AddComponent<UiFx>();
            fx.Apply = f =>
            {
                float h = Mathf.Max(UiFx.Ease(f.Hover), nick.isFocused ? 1f : 0f);
                line.color = UiKit.Lerp(Bone.A(0.18f), Red, h);
                pencil.color = UiKit.Lerp(Muted, Bone, h);
                float w = Mathf.Min(nick.textComponent.GetPreferredValues(nick.text).x, 660f);
                pencil.rectTransform.anchoredPosition = new Vector2(centreX + w * 0.5f + 22, -(y + 20 + inputH * 0.5f));
                line.rectTransform.sizeDelta = new Vector2(w + 40, lineH);
            };
        }

        void BuildWardrobe(RectTransform root)
        {
            // ---- Categories: the main menu's own entries.
            var cats = new RectTransform[3];
            var catY = new float[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = (WardrobeSlot)i;
                Button b = _ui.MenuWord(root, MainMenuState.SlotName(slot), 46, () => _c.SetSlot(slot), () => S.Slot == slot, out _);
                b.name = "Cat_" + slot;
                cats[i] = b.GetComponent<RectTransform>().TL(ListX, CatTop + i * CatStep, 500, 62);
                catY[i] = CatTop + i * CatStep;
            }

            // ---- The unfolded box grid, with the Join Room's red rule.
            RectTransform block = UiKit.Node(root, "Items").TL(ListX, 0, GridX + GridW + 40, VisibleRows * BoxStep + ActionH);
            var blockGroup = block.gameObject.AddComponent<CanvasGroup>();
            Image rule = TavernUI.VLine(block, "Rule", Red);
            float ruleW = Thk(3);
            rule.rectTransform.TL(4, 4, ruleW, 100);

            UiKit.ScrollParts scroll = UiKit.VerticalScroll(block, "Grid", Bone.A(0.12f), Red);
            RectTransform barRt = scroll.Bar.GetComponent<RectTransform>();
            int maxBoxes = 0;
            foreach (WardrobeSlot slot in Enum.GetValues(typeof(WardrobeSlot))) maxBoxes = Mathf.Max(maxBoxes, S.ItemsIn(slot).Count);
            for (int i = 0; i < maxBoxes; i++) BuildBox(scroll.Content, i);

            BuildEquipLine(block);

            float blockY = CatTop + CatStep;
            WardrobeSlot shownSlot = S.Slot;

            Bind(() =>
            {
                int rows = Mathf.CeilToInt(S.ItemsIn(S.Slot).Count / (float)GridCols);
                scroll.Content.sizeDelta = new Vector2(0, Mathf.Max(0, rows * BoxStep - BoxGap));
            });

            // Smoothly re-stack the categories around the open grid every frame.
            TavernUI.Animator(root, "Accordion", () => true, _ =>
            {
                float gridH = GridHeight(S.ItemsIn(S.Slot).Count);
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
                rule.rectTransform.sizeDelta = new Vector2(ruleW, gridH + ActionH - 10);
            });
        }

        static float GridHeight(int count)
        {
            int rows = Mathf.Clamp(Mathf.CeilToInt(count / (float)GridCols), 1, VisibleRows);
            return rows * BoxStep - BoxGap;
        }

        /// <summary>One item tile: a deckled paper scrap, the item inside, its name along the bottom.</summary>
        void BuildBox(RectTransform content, int index)
        {
            int col = index % GridCols, row = index / GridCols;
            Button b = TavernUI.Btn(content, "Box" + index, null, out UiFx fx);
            RectTransform rt = b.GetComponent<RectTransform>().TL(col * BoxStep, row * BoxStep, Box, Box);

            Image fill = UiKit.Img(rt, "Fill", UiSprites.DeckledFill(), TavernUI.BoxRest);
            fill.rectTransform.Fill();
            Image frame = UiKit.Img(rt, "Frame", UiSprites.DeckledFrame(2), Bone.A(0.32f));
            frame.rectTransform.Fill();
            Image select = UiKit.Img(rt, "Select", UiSprites.DeckledFrame(3), Red);
            select.rectTransform.Fill();

            RawImage thumb = UiKit.Raw(rt, "Thumb", null, Color.white);
            thumb.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -60, 100, 100);
            Image lockIcon = UiKit.Img(rt, "Lock", UiSprites.Lock(), Dim);
            lockIcon.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -60, 26, 31);
            TextMeshProUGUI name = T(rt, "Name", "", 13, Dim, TextAlignmentOptions.Bottom);
            name.rectTransform.Fill(8, 0, 8, 10);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -14;

            Image check = UiKit.Img(rt, "Equipped", UiSprites.Check(), Red);
            check.rectTransform.At(1, 1, 0.5f, 0.5f, -16, -16, 16, 16);
            Image defMark = UiKit.Img(rt, "DefaultMark", UiSprites.NoneMark(), Muted);
            defMark.rectTransform.At(0, 1, 0.5f, 0.5f, 16, -16, 15, 15);

            CosmeticItem item = null;
            b.onClick.AddListener(() => { if (item != null) S.Select(item); });

            Bind(() =>
            {
                List<CosmeticItem> items = S.ItemsIn(S.Slot);
                item = index < items.Count ? items[index] : null;
                UiKit.SetActive(rt, item != null);
                if (item == null) return;
                thumb.texture = item.Thumbnail;
                thumb.color = item.Locked ? new Color(0.35f, 0.33f, 0.31f, 0.65f) : Color.white;
                UiKit.SetActive(lockIcon, item.Locked);
                UiKit.SetActive(check, S.IsEquipped(item));
                UiKit.SetActive(defMark, item.IsDefault);
                name.text = item.Name;
                fx.Selected = S.IsSelected(item);
            });

            fx.Apply = f =>
            {
                float s = UiFx.Ease(f.Select), h = UiFx.Ease(f.Hover);
                bool locked = item != null && item.Locked;
                fill.color = UiKit.Lerp(TavernUI.BoxRest, Bone.A(TavernUI.BoxHotAlpha), Mathf.Max(s, h * 0.6f));
                frame.color = Bone.A(0.32f + 0.4f * h);
                select.color = Red.A(s);
                name.color = UiKit.Lerp(locked ? Muted : Dim, Bone, Mathf.Max(s, h * 0.7f));
                float sc = 1f + 0.06f * Mathf.Max(s, h) - 0.04f * f.Press;
                thumb.rectTransform.localScale = new Vector3(sc, sc, 1);
            };
        }

        /// <summary>Under the grid: the chosen item's name and state, and EQUIP when it can be equipped.</summary>
        void BuildEquipLine(RectTransform block)
        {
            RectTransform line = UiKit.Node(block, "EquipLine");
            line.anchorMin = line.anchorMax = new Vector2(0, 0);
            line.pivot = new Vector2(0, 0);
            line.anchoredPosition = new Vector2(GridX, 0);
            line.sizeDelta = new Vector2(GridW + 24, ActionH - 12);

            TextMeshProUGUI name = T(line, "Name", "", 24, Bone, TextAlignmentOptions.TopLeft, true);
            name.rectTransform.TL(0, 0, GridW - 150, 30);
            TextMeshProUGUI state = T(line, "State", "", 13, Muted, TextAlignmentOptions.TopLeft);
            state.rectTransform.TL(2, 32, GridW - 150, 20);
            state.characterSpacing = Sp(6);

            RectTransform equip = _ui.TextLink(line, "Equip", "EQUIP", 30, true, () => S.EquipSelected());
            equip.At(1, 1, 1, 1, -24, 0, 130, 46);

            Bind(() =>
            {
                CosmeticItem sel = S.SelectedItem(S.Slot);
                if (sel == null) return;
                name.text = sel.Name;
                bool eq = S.IsEquipped(sel);
                string rarity = sel.IsDefault ? "DEFAULT" : MainMenuState.RarityName(sel.Rarity);
                state.text = rarity + "   ·   " + (eq ? "EQUIPPED" : sel.Locked ? "LOCKED" : "PREVIEWING");
                state.color = eq ? Red : Muted;
                UiKit.SetActive(equip, S.CanEquipSelected());
            });
        }

        void BuildShop(RectTransform root)
        {
            TextMeshProUGUI big = T(root, "Title", "COMING SOON", 64, Bone, TextAlignmentOptions.MidlineLeft, true);
            big.rectTransform.TL(ListX, CatTop, 700, 84);
            TavernUI.Line(root, "Rule", Red).rectTransform.TL(ListX + 4, CatTop + 94, 56, Thk(3));
            TextMeshProUGUI line = T(root, "Line", "THE MERCHANT IS STILL ON THE ROAD.", 20, Dim);
            line.rectTransform.TL(ListX + 2, CatTop + 112, 700, 30);
            line.characterSpacing = Sp(4);

            // Empty locked tiles in the wardrobe's shape, fading away.
            for (int i = 0; i < 6; i++)
            {
                int col = i % GridCols, row = i / GridCols;
                float a = row == 0 ? 1f : 0.45f;
                RectTransform box = UiKit.Node(root, "Ghost" + i).TL(ListX + GridX + col * BoxStep, CatTop + 180 + row * BoxStep, Box, Box);
                UiKit.Img(box, "Frame", UiSprites.DeckledFrame(2), Bone.A(0.14f * a)).rectTransform.Fill();
                UiKit.Img(box, "Lock", UiSprites.Lock(), Bone.A(0.18f * a)).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 8, 26, 31);
                UiKit.Img(box, "Name", UiSprites.DeckledFill(), Bone.A(0.07f * a)).rectTransform.At(0.5f, 0, 0.5f, 0, 0, 16, 80, 9);
            }
        }

        // ================================================================== SETTINGS

        bool _fullscreen;

        void BuildSettings()
        {
            RectTransform scr = _settings;
            Shade(scr, 1500, 0.92f);
            Shade(scr, 1150, 0.7f);
            _ui.BackLink(scr, _c.GoMenu);

            TextMeshProUGUI title = T(scr, "Title", "SETTINGS", 64, Bone, TextAlignmentOptions.MidlineLeft, true);
            title.rectTransform.TL(140, 118, 700, 80);

            _fullscreen = GameSettings.Fullscreen;

            float y = 236;
            Section(scr, "AUDIO", ref y);
            SliderRow(scr, "MASTER VOLUME", ref y, 0, 1, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, MainMenuState.Percent);
            SliderRow(scr, "MUSIC VOLUME", ref y, 0, 1, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, MainMenuState.Percent);
            SliderRow(scr, "EFFECTS VOLUME", ref y, 0, 1, () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v, MainMenuState.Percent);
            Section(scr, "CONTROLS", ref y);
            SliderRow(scr, "MOUSE SENSITIVITY", ref y, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, MainMenuState.Sensitivity);
            Section(scr, "DISPLAY", ref y);
            SelectorRow(scr, "RESOLUTION", ref y);
            OptionRow(scr, "DISPLAY MODE", ref y, "FULLSCREEN", "WINDOWED", () => _fullscreen, v => { _fullscreen = v; GameSettings.Fullscreen = v; });
            OptionRow(scr, "V-SYNC", ref y, "ON", "OFF", () => GameSettings.VSync, v => GameSettings.VSync = v);

            TextMeshProUGUI foot = T(scr, "Footnote", "CHANGES APPLY IMMEDIATELY", 13, Muted);
            foot.rectTransform.TL(142, y + 22, 600, 20);
            foot.characterSpacing = Sp(8);
        }

        void Section(RectTransform parent, string label, ref float y)
        {
            TextMeshProUGUI t = _ui.Kicker(parent, "Section_" + label, label, 15, Red, 12);
            t.rectTransform.TL(142, y + 18, 300, 24);
            float w = t.GetPreferredValues(label).x;
            float h = Thk(1);
            TavernUI.Hairline(parent, "SectionRule_" + label, Bone.A(0.1f)).rectTransform.TL(142 + w + 18, y + 30 - (h - 1) * 0.5f, 730 - w - 18, h);
            y += 52;
        }

        RectTransform Row(RectTransform parent, string label, ref float y, out TextMeshProUGUI labelText)
        {
            RectTransform row = UiKit.Node(parent, "Row_" + label).TL(140, y, 740, 60);
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

            UiKit.SliderParts sl = UiKit.Slider(row, "Slider", min, max, get(), v => { set(v); S.Notify(); },
                UiSprites.InkHairline(), UiSprites.InkStroke(false), UiSprites.WaxSeal(), 5f, new Vector2(30, 30));
            sl.Slider.GetComponent<RectTransform>().TL(380, 10, 240, 40);
            sl.Track.color = Bone.A(0.16f);
            sl.Fill.color = Bone.A(0.8f);
            sl.Fx.Apply = f =>
            {
                float h = UiFx.Ease(f.Hover);
                sl.Handle.color = UiKit.Lerp(Red, Bone, h);
                sl.Handle.rectTransform.localScale = Vector3.one * (1f + 0.3f * h);
                labelText.color = UiKit.Lerp(Dim, Bone, h);
            };
            Bind(() => { sl.Slider.SetValueWithoutNotify(get()); value.text = format(get()); });
        }

        void SelectorRow(RectTransform parent, string label, ref float y)
        {
            RectTransform row = Row(parent, label, ref y, out _);
            TextMeshProUGUI value = T(row, "Value", "", 24, Bone, TextAlignmentOptions.Center);
            value.rectTransform.TL(410, 0, 200, 60);
            Bind(() =>
            {
                Vector2Int r = GameSettings.CurrentResolution;
                value.text = r.x + " × " + r.y;
            });
            Arrow(row, 376, -1);
            Arrow(row, 614, +1);
        }

        void Arrow(RectTransform row, float x, int dir)
        {
            Button b = TavernUI.Btn(row, dir < 0 ? "Prev" : "Next", () =>
            {
                GameSettings.ResolutionIndex = GameSettings.ResolutionIndex + dir;
                S.Notify();
            }, out UiFx fx);
            b.GetComponent<RectTransform>().TL(x, 10, 40, 40);
            Image chev = UiKit.Img(b.transform, "Chevron", TavernUI.Chev, Dim);
            chev.rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 14, 21);
            if (dir < 0) chev.rectTransform.localScale = new Vector3(-1, 1, 1);
            fx.Apply = f =>
            {
                float h = UiFx.Ease(f.Hover);
                chev.color = UiKit.Lerp(Dim, Red, h);
                chev.rectTransform.anchoredPosition = new Vector2(dir * 4 * h, 0);
            };
        }

        void OptionRow(RectTransform parent, string label, ref float y, string a, string b, Func<bool> get, Action<bool> set)
        {
            RectTransform row = Row(parent, label, ref y, out _);
            UiFx fa = OptionWord(row, a, 380, () => { set(true); S.Notify(); });
            UiFx fb = OptionWord(row, b, 570, () => { set(false); S.Notify(); });
            Bind(() => { fa.Selected = get(); fb.Selected = !get(); });
        }

        UiFx OptionWord(RectTransform row, string label, float x, Action click)
        {
            Button b = TavernUI.Btn(row, label, click, out UiFx fx);
            b.GetComponent<RectTransform>().TL(x, 6, 200, 48);
            TextMeshProUGUI text = T(b.transform, "Label", label, 22, Muted);
            text.rectTransform.Fill();
            text.characterSpacing = Sp(3);
            float tw = text.GetPreferredValues(label).x;
            Image line = TavernUI.Line(b.transform, "Underline", Red);
            float lineH = Thk(3);
            line.rectTransform.At(0, 0, 0, 0.5f, 0, 4, tw, lineH);
            fx.Apply = f =>
            {
                float s = UiFx.Ease(f.Select), h = UiFx.Ease(f.Hover);
                text.color = UiKit.Lerp(UiKit.Lerp(Muted, Dim, h), Bone, s);
                line.rectTransform.sizeDelta = new Vector2(tw * s, lineH);
            };
            return fx;
        }
    }
}
