using Shields.Match;
using Shields.Online;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// HUD 2 — TAVERN BOARD. Physical and readable: dark wooden plaques with brass trim,
    /// keycap tiles for the three actions, a scoreboard plaque for the table, and the
    /// table's events pinned top left as parchment slips (a short running log, newest on
    /// top). Name tags over heads are small plaques too.
    /// </summary>
    public sealed class HudBoard : HudConcept
    {
        public override string Code { get { return "Board"; } }

        static readonly Color Wood = UiKit.Hex("#1B110A", 0.92f);
        static readonly Color WoodLight = UiKit.Hex("#2C1C11", 0.95f);
        static readonly Color Brass = UiKit.Hex("#C0904A");
        static readonly Color BrassHi = UiKit.Hex("#EBC27C");
        static readonly Color Cream = UiKit.Hex("#F0E3C6");
        static readonly Color Parchment = UiKit.Hex("#E6D5B0", 0.96f);
        static readonly Color Ink = UiKit.Hex("#2A1A0F");
        static readonly Color InkRed = UiKit.Hex("#9B2A20");

        struct Plaque { public RectTransform Rect; public Image Fill, Edge; }

        Plaque MakePlaque(Transform parent, string name, Color fill, Color edge)
        {
            var p = new Plaque { Rect = UiKit.Node(parent, name) };
            p.Fill = Img(p.Rect, "Fill", UiSprites.RoundRect(12), fill);
            p.Fill.rectTransform.Fill();
            p.Edge = Img(p.Rect, "Edge", HudSprites.RoundOutline(12, 2), edge);
            p.Edge.rectTransform.Fill();
            Img(p.Rect, "Inner", HudSprites.RoundOutline(9, 1), edge.A(edge.a * 0.3f)).rectTransform.Fill(5, 5, 5, 5);
            return p;
        }

        Image Keycap(Transform parent, string name, string key, float size, Color fill, Color text)
        {
            Image cap = Img(parent, name, UiSprites.RoundRect(6), fill);
            cap.rectTransform.sizeDelta = new Vector2(size, size);
            TextMeshProUGUI t = UiKit.Text(cap.transform, "Key", key, UI.Bowlby, size * 0.55f, text, TextAlignmentOptions.Center);
            t.rectTransform.Fill(0, 0, 0, 2);
            return cap;
        }

        // Banner
        Plaque _banner;
        TextMeshProUGUI _title, _sub;

        // Actions
        RectTransform _actions;
        readonly Plaque[] _tiles = new Plaque[3];
        readonly TextMeshProUGUI[] _tileLabels = new TextMeshProUGUI[3];
        readonly Image[] _tileIcons = new Image[3], _tileKeys = new Image[3];

        // Log
        const int Slips = 4;
        readonly RectTransform[] _slips = new RectTransform[Slips];
        readonly TextMeshProUGUI[] _slipHead = new TextMeshProUGUI[Slips], _slipDetail = new TextMeshProUGUI[Slips];
        readonly CanvasGroup[] _slipGroups = new CanvasGroup[Slips];

        // Scoreboard
        const int MaxRows = 8;
        Plaque _board;
        TextMeshProUGUI _footer;
        readonly RectTransform[] _rows = new RectTransform[MaxRows];
        readonly Image[] _rowBg = new Image[MaxRows], _rowArrow = new Image[MaxRows], _rowCharge = new Image[MaxRows];
        readonly TextMeshProUGUI[] _rowName = new TextMeshProUGUI[MaxRows], _rowHp = new TextMeshProUGUI[MaxRows], _rowShield = new TextMeshProUGUI[MaxRows];

        // Tags
        readonly Plaque[] _tags = new Plaque[MaxRows];
        readonly TextMeshProUGUI[] _tagName = new TextMeshProUGUI[MaxRows], _tagHp = new TextMeshProUGUI[MaxRows];
        readonly Image[] _tagArrow = new Image[MaxRows];

        // Beer + end
        Plaque _order, _drink;
        TextMeshProUGUI _drinkText;
        Plaque _end;
        TextMeshProUGUI _endKicker, _endTitle;
        RectTransform _endRematch;

        protected override void Build()
        {
            BuildTags();
            BuildBanner();
            BuildLog();
            BuildActions();
            BuildBoard();
            BuildBeer();
            BuildEnd();
        }

        // ------------------------------------------------------------------ banner

        void BuildBanner()
        {
            _banner = MakePlaque(Root, "Banner", Wood, Brass);
            _banner.Rect.At(0.5f, 1, 0.5f, 1, 0, -22, 640, 96);
            _title = T(_banner.Rect, "Title", "", 38, Cream, TextAlignmentOptions.Center, true);
            _title.rectTransform.TL(0, 10, 640, 48);
            _sub = T(_banner.Rect, "Sub", "", 14, Brass);
            _sub.rectTransform.TL(0, 60, 640, 24);
            _sub.characterSpacing = Sp(6);
        }

        void TickBanner(HudModel m)
        {
            string title = "", sub = "";
            bool hot = false;
            if (m.ChoosingShield) { title = "CHOOSE YOUR SHIELD"; sub = "CLICK ONE OF YOUR THREE CARDS · THE OTHER TWO ARE YOUR HEALTH"; hot = true; }
            else if (m.WaitingForSetup) { title = "THE TABLE IS CHOOSING"; sub = "EVERYONE PICKS A SHIELD"; }
            else if (m.InMatch && !m.MatchOver)
            {
                if (!m.LocalAlive) { title = m.Possessive(m.Current) + " TURN"; sub = "YOU ARE OUT · WATCHING THE TABLE"; }
                else if (!m.MyTurn) { title = m.Possessive(m.Current) + " TURN"; sub = m.Player(m.Current).IsBot ? "THE BOT IS THINKING" : "WAITING FOR THEIR MOVE"; }
                else if (m.Pending == HudPendingAction.None) { title = "YOUR TURN"; sub = "PICK AN ACTION  ·  1  2  3"; hot = true; }
                else { title = m.ActionName(m.Pending); sub = m.Hovered < 0 ? HudInk.AimHint(m.Pending) : HudInk.ConfirmHint(m); hot = true; }
            }

            Show(_banner.Rect, title.Length > 0);
            _title.text = title;
            _sub.text = sub;
            _title.fontSize = hot ? 38 : 30;
            _title.color = hot ? Cream : Cream.A(0.75f);
            _banner.Edge.color = hot ? BrassHi : Brass.A(0.6f);
            float w = Mathf.Max(_title.GetPreferredValues(title).x, _sub.GetPreferredValues(sub).x) + 90;
            _banner.Rect.sizeDelta = new Vector2(Mathf.Clamp(w, 420, 1100), hot ? 96 : 86);
            _title.rectTransform.sizeDelta = _sub.rectTransform.sizeDelta = new Vector2(_banner.Rect.sizeDelta.x, _title.rectTransform.sizeDelta.y);
            _sub.rectTransform.sizeDelta = new Vector2(_banner.Rect.sizeDelta.x, 24);
        }

        // ------------------------------------------------------------------ log

        void BuildLog()
        {
            for (int i = 0; i < Slips; i++)
            {
                RectTransform slip = UiKit.Node(Root, "Slip" + i).TL(26, 26 + i * 78, 560, 70);
                _slips[i] = slip;
                _slipGroups[i] = slip.gameObject.AddComponent<CanvasGroup>();
                Img(slip, "Paper", UiSprites.DeckledFill(), Parchment).rectTransform.Fill();
                Img(slip, "Pin", HudSprites.Circle(), InkRed).rectTransform.At(0, 1, 0.5f, 0.5f, 18, -16, 12, 12);
                _slipHead[i] = UiKit.Text(slip, "Head", "", UI.Bowlby, 19, Ink, TextAlignmentOptions.TopLeft);
                _slipHead[i].rectTransform.TL(34, 9, 510, 28);
                _slipDetail[i] = UiKit.Text(slip, "Detail", "", UI.Bowlby, 13, Ink.A(0.7f), TextAlignmentOptions.TopLeft);
                _slipDetail[i].rectTransform.TL(34, 38, 510, 22);
                _slipDetail[i].characterSpacing = Sp(4);
            }
        }

        void TickLog(HudModel m)
        {
            int count = m.InMatch ? m.Notes.Count : 0;
            for (int i = 0; i < Slips; i++)
            {
                int index = count - 1 - i;
                HudNote note = index >= 0 ? m.Notes[index] : null;
                float a = note != null ? Life(note.Time, 14f, 0.25f, 1.2f) * (i == 0 ? 1f : 0.85f - 0.15f * i) : 0f;
                Show(_slips[i], a > 0.01f);
                if (a <= 0.01f) continue;
                _slipGroups[i].alpha = a;
                _slipHead[i].text = note.Headline;
                _slipDetail[i].text = note.Detail;
                _slipHead[i].color = note.Kind == NoteKind.Bad || note.Kind == NoteKind.Big ? InkRed : Ink;
                float slide = (1f - Mathf.Clamp01((Time.unscaledTime - note.Time) / 0.3f)) * -40f;
                _slips[i].anchoredPosition = new Vector2(26 + slide, -(26 + i * 78));
                _slips[i].localEulerAngles = new Vector3(0, 0, i % 2 == 0 ? 0.6f : -0.5f);
            }
        }

        // ------------------------------------------------------------------ actions

        void BuildActions()
        {
            // A column at the left edge: the bottom centre belongs to your own cards on the table.
            _actions = UiKit.Node(Root, "Actions").At(0, 0.5f, 0, 0.5f, 26, -40, 280, 260);
            Sprite[] icons = { HudSprites.Sword(), HudSprites.Shield(), HudSprites.Bolt() };
            string[] labels = { "ATTACK", "REPLACE SHIELD", "CHARGE" };
            for (int i = 0; i < 3; i++)
            {
                _tiles[i] = MakePlaque(_actions, "Tile" + i, Wood, Brass.A(0.7f));
                _tiles[i].Rect.At(0, 0.5f, 0, 0.5f, 0, (1 - i) * 86, 268, 76);
                _tileKeys[i] = Keycap(_tiles[i].Rect, "Key", (i + 1).ToString(), 34, Brass, Ink);
                _tileKeys[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 34, 0, 34, 34);
                _tileIcons[i] = Img(_tiles[i].Rect, "Icon", icons[i], Cream);
                _tileIcons[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 78, 0, 30, 30);
                _tileLabels[i] = T(_tiles[i].Rect, "Label", labels[i], 21, Cream, TextAlignmentOptions.MidlineLeft);
                _tileLabels[i].rectTransform.TL(100, 0, 160, 76);
            }
        }

        void TickActions(HudModel m)
        {
            Show(_actions, m.MyTurn);
            if (!m.MyTurn) return;
            _tileLabels[2].text = m.ThirdAction;
            _tileLabels[2].fontSize = m.LocalHasCharge ? 18 : 21;
            for (int i = 0; i < 3; i++)
            {
                bool sel = (int)m.Pending == i + 1 || (i == 2 && m.Pending == HudPendingAction.ChargedAttack);
                bool dimmed = m.Pending != HudPendingAction.None && !sel;
                _tiles[i].Fill.color = sel ? Brass : Wood;
                _tiles[i].Edge.color = sel ? BrassHi : Brass.A(dimmed ? 0.35f : 0.7f);
                _tileLabels[i].color = sel ? Ink : Cream.A(dimmed ? 0.5f : 1f);
                _tileIcons[i].color = sel ? Ink : Cream.A(dimmed ? 0.5f : 1f);
                _tileKeys[i].color = sel ? Ink : Brass.A(dimmed ? 0.5f : 1f);
                _tileKeys[i].GetComponentInChildren<TextMeshProUGUI>().color = sel ? BrassHi : Ink;
                float nudge = sel ? 14f : 0f;
                _tiles[i].Rect.anchoredPosition = new Vector2(nudge, (1 - i) * 86);
            }
        }

        // ------------------------------------------------------------------ scoreboard

        void BuildBoard()
        {
            _board = MakePlaque(Root, "Board", Wood, Brass);
            _board.Rect.At(1, 0, 1, 0, -26, 26, 470, 400);
            K(_board.Rect, "Title", "THE TABLE", 17, BrassHi, 8, TextAlignmentOptions.MidlineLeft).rectTransform.TL(24, 12, 200, 30);
            Img(_board.Rect, "HeartHead", HudSprites.Heart(), Brass).rectTransform.At(0, 1, 0.5f, 0.5f, 336, -28, 18, 18);
            Img(_board.Rect, "ShieldHead", HudSprites.Shield(), Brass).rectTransform.At(0, 1, 0.5f, 0.5f, 404, -28, 18, 18);
            Img(_board.Rect, "Rule", UiSprites.White(), Brass.A(0.35f)).rectTransform.TL(20, 48, 430, 1);

            for (int i = 0; i < MaxRows; i++)
            {
                RectTransform row = UiKit.Node(_board.Rect, "Row" + i).TL(12, 54 + i * 40, 446, 38);
                _rows[i] = row;
                _rowBg[i] = Img(row, "Bg", UiSprites.RoundRect(6), Brass.A(0.18f));
                _rowBg[i].rectTransform.Fill();
                _rowArrow[i] = Img(row, "Arrow", HudSprites.Arrow(), BrassHi);
                _rowArrow[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 18, 0, 16, 16);
                _rowName[i] = T(row, "Name", "", 20, Cream, TextAlignmentOptions.MidlineLeft);
                _rowName[i].rectTransform.TL(34, 0, 250, 38);
                _rowHp[i] = T(row, "Hp", "", 22, Cream, TextAlignmentOptions.Center, true);
                _rowHp[i].rectTransform.TL(300, 0, 50, 38);
                _rowShield[i] = T(row, "Shield", "", 22, Cream, TextAlignmentOptions.Center, true);
                _rowShield[i].rectTransform.TL(368, 0, 50, 38);
                _rowCharge[i] = Img(row, "Charge", HudSprites.Bolt(), BrassHi);
                _rowCharge[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 430, 0, 16, 16);
            }
            _footer = T(_board.Rect, "Footer", "", 13, Brass, TextAlignmentOptions.MidlineLeft);
            _footer.characterSpacing = Sp(6);
        }

        void TickBoard(HudModel m)
        {
            Show(_board.Rect, m.InMatch);
            if (!m.InMatch) return;
            int n = Mathf.Min(m.PlayerCount, MaxRows);
            _board.Rect.sizeDelta = new Vector2(470, 54 + n * 40 + 44);
            _footer.rectTransform.TL(24, 54 + n * 40 + 6, 420, 30);
            _footer.text = "DECK " + m.Snap.RemainingDrawCount + "   ·   TURNS GO " + DirectionText(m);

            for (int i = 0; i < MaxRows; i++)
            {
                Show(_rows[i], i < n);
                if (i >= n) continue;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                bool you = i == m.Local;
                Show(_rowBg[i], turn || you);
                _rowBg[i].color = turn ? Brass.A(0.32f) : Cream.A(0.06f);
                Show(_rowArrow[i], turn);
                _rowName[i].text = m.Name(i) + (p.IsBot ? "  (BOT)" : "");
                _rowName[i].color = p.IsAlive ? (turn ? BrassHi : Cream) : Cream.A(0.35f);
                _rowHp[i].text = p.IsAlive ? p.Health.ToString() : "OUT";
                _rowHp[i].fontSize = p.IsAlive ? 22 : 14;
                _rowShield[i].text = p.IsAlive ? p.ShieldValue.ToString() : "";
                _rowHp[i].color = _rowShield[i].color = p.IsAlive ? Cream : Cream.A(0.35f);
                Show(_rowCharge[i], p.IsAlive && p.HasCharge);
            }
        }

        // ------------------------------------------------------------------ tags

        void BuildTags()
        {
            for (int i = 0; i < MaxRows; i++)
            {
                _tags[i] = MakePlaque(Root, "Tag" + i, Wood, Brass.A(0.6f));
                _tags[i].Rect.At(0.5f, 0.5f, 0.5f, 1, 0, 0, 170, 54);
                _tagName[i] = T(_tags[i].Rect, "Name", "", 16, Cream, TextAlignmentOptions.Center, true);
                _tagName[i].rectTransform.TL(0, 6, 170, 22);
                Image heart = Img(_tags[i].Rect, "Heart", HudSprites.Heart(), InkRed);
                heart.rectTransform.At(0.5f, 1, 0.5f, 0.5f, -16, -38, 14, 14);
                _tagHp[i] = T(_tags[i].Rect, "Hp", "", 15, Cream, TextAlignmentOptions.MidlineLeft, true);
                _tagHp[i].rectTransform.At(0.5f, 1, 0, 0.5f, -4, -38, 80, 20);
                _tagArrow[i] = Img(_tags[i].Rect, "Arrow", HudSprites.Arrow(), BrassHi);
                _tagArrow[i].rectTransform.At(0.5f, 0, 0.5f, 0.5f, 0, -14, 18, 18);
                _tagArrow[i].rectTransform.localEulerAngles = new Vector3(0, 0, -90);
            }
        }

        void TickTags(HudModel m)
        {
            for (int i = 0; i < MaxRows; i++)
            {
                bool on = m.InMatch && i < m.PlayerCount && i != m.Local && Project(i, out _);
                Show(_tags[i].Rect, on);
                if (!on) continue;
                Project(i, out Vector2 at);
                _tags[i].Rect.anchoredPosition = at;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                _tagName[i].text = m.Name(i);
                _tagHp[i].text = p.IsAlive ? p.Health.ToString() : "OUT";
                _tags[i].Edge.color = turn ? BrassHi : Brass.A(0.5f);
                _tags[i].Fill.color = p.IsAlive ? Wood : Wood.A(0.5f);
                Show(_tagArrow[i], turn);
            }
        }

        // ------------------------------------------------------------------ beer + end

        void BuildBeer()
        {
            _order = MakePlaque(Root, "Order", Wood, Brass.A(0.6f));
            _order.Rect.At(0, 0, 0, 0, 26, 26, 236, 50);
            Keycap(_order.Rect, "Key", "O", 28, Brass, Ink).rectTransform.At(0, 0.5f, 0.5f, 0.5f, 28, 0, 28, 28);
            T(_order.Rect, "Label", "ORDER A ROUND", 17, Cream, TextAlignmentOptions.MidlineLeft).rectTransform.TL(52, 0, 180, 50);

            _drink = MakePlaque(Root, "Drink", Wood, Brass.A(0.6f));
            _drink.Rect.At(0, 0, 0, 0, 26, 84, 236, 50);
            Keycap(_drink.Rect, "Key", "B", 28, Brass, Ink).rectTransform.At(0, 0.5f, 0.5f, 0.5f, 28, 0, 28, 28);
            _drinkText = T(_drink.Rect, "Label", "HOLD TO DRINK", 17, Cream, TextAlignmentOptions.MidlineLeft);
            _drinkText.rectTransform.TL(52, 0, 180, 50);
        }

        void TickBeer(HudModel m)
        {
            Show(_order.Rect, m.OrderAvailable && !m.OrderOpen);
            bool drink = m.CanDrink || m.Drinking;
            Show(_drink.Rect, drink);
            _drinkText.text = m.Drinking ? "DRINKING…" : "HOLD TO DRINK";
            _drink.Edge.color = m.Drinking ? BrassHi : Brass.A(0.6f);
        }

        void BuildEnd()
        {
            _end = MakePlaque(Root, "End", Wood, BrassHi);
            _end.Rect.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 60, 820, 250);
            _endKicker = K(_end.Rect, "Kicker", "", 20, BrassHi, 10);
            _endKicker.rectTransform.TL(0, 20, 820, 34);
            _endTitle = T(_end.Rect, "Title", "", 84, Cream, TextAlignmentOptions.Center, true);
            _endTitle.rectTransform.TL(0, 54, 820, 104);
            _endRematch = UiKit.Node(_end.Rect, "Rematch").TL(0, 176, 820, 50);
            Keycap(_endRematch, "Key", "R", 34, Brass, Ink).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, -80, 0, 34, 34);
            T(_endRematch, "Label", "REMATCH", 24, Cream, TextAlignmentOptions.MidlineLeft).rectTransform.At(0.5f, 0.5f, 0, 0.5f, -50, 0, 200, 40);
        }

        void TickEnd(HudModel m)
        {
            Show(_end.Rect, m.InMatch && m.MatchOver);
            if (!m.MatchOver) return;
            bool won = m.Winner == m.Local;
            _endKicker.text = won ? "VICTORY" : "THE LAST ONE STANDING";
            _endTitle.text = won ? "YOU WIN" : m.Name(m.Winner) + " WINS";
            Show(_endRematch, m.RematchVisible);
        }

        // ------------------------------------------------------------------ frame

        public override void Tick(HudModel m)
        {
            TickBanner(m);
            TickLog(m);
            TickActions(m);
            TickBoard(m);
            TickTags(m);
            TickBeer(m);
            TickEnd(m);
        }
    }
}
