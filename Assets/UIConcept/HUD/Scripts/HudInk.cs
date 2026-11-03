using Shields.Match;
using Shields.Online;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// HUD 1 — INK. The main menu's own language carried into the match: frameless bone
    /// type on local darkening, red ink dashes, hand-lettered Knewave labels, wax-seal
    /// keys. Turn banner top centre, the latest event under it, the action keys bottom
    /// centre, the table bottom right, beer bottom left.
    /// </summary>
    public sealed class HudInk : HudConcept
    {
        public override string Code { get { return "Ink"; } }

        // Banner
        TextMeshProUGUI _kicker, _title, _line;
        Image _titleDash;

        // Event
        RectTransform _event;
        TextMeshProUGUI _eventHead, _eventDetail;
        Image _eventDash;
        HudNote _shownNote;

        // Actions
        RectTransform _actions;
        readonly TextMeshProUGUI[] _actLabels = new TextMeshProUGUI[3];
        readonly Image[] _actSeals = new Image[3];
        readonly Image[] _actDashes = new Image[3];
        float[] _actSel = new float[3];

        // Table
        RectTransform _table;
        TextMeshProUGUI _tableMeta;
        const int MaxRows = 8;
        readonly RectTransform[] _rows = new RectTransform[MaxRows];
        readonly TextMeshProUGUI[] _rowName = new TextMeshProUGUI[MaxRows], _rowHp = new TextMeshProUGUI[MaxRows], _rowShield = new TextMeshProUGUI[MaxRows];
        readonly Image[] _rowTurn = new Image[MaxRows], _rowStrike = new Image[MaxRows], _rowCharge = new Image[MaxRows], _rowHeart = new Image[MaxRows], _rowShieldIcon = new Image[MaxRows];

        // Nameplates
        readonly RectTransform[] _plates = new RectTransform[MaxRows];
        readonly TextMeshProUGUI[] _plateName = new TextMeshProUGUI[MaxRows], _plateHp = new TextMeshProUGUI[MaxRows];
        readonly Image[] _plateDash = new Image[MaxRows];

        // Beer + end
        TextMeshProUGUI _orderText, _drinkText;
        Image _orderSeal, _drinkSeal;
        RectTransform _end;
        TextMeshProUGUI _endKicker, _endTitle, _endLine;

        protected override void Build()
        {
            // Local darkening only where text sits: a soft band at the top, pools in the bottom corners.
            Image topBand = Img(Root, "TopBand", HudSprites.FadeDown(), new Color(0, 0, 0, 0.6f));
            topBand.rectTransform.anchorMin = new Vector2(0, 1);
            topBand.rectTransform.anchorMax = new Vector2(1, 1);
            topBand.rectTransform.pivot = new Vector2(0.5f, 1);
            topBand.rectTransform.anchoredPosition = Vector2.zero;
            topBand.rectTransform.sizeDelta = new Vector2(0, 330);
            Img(Root, "TablePool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.75f)).rectTransform.At(1, 0, 0.5f, 0.5f, -150, 130, 1100, 760);
            Img(Root, "BeerPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.5f)).rectTransform.At(0, 0, 0.5f, 0.5f, 60, 60, 700, 360);

            BuildBanner();
            BuildEvent();
            BuildActions();
            BuildTable();
            BuildPlates();
            BuildBeer();
            BuildEnd();
        }

        // ------------------------------------------------------------------ banner

        void BuildBanner()
        {
            RectTransform b = UiKit.Node(Root, "Banner").At(0.5f, 1, 0.5f, 1, 0, -26, 1200, 150);
            _kicker = K(b, "Kicker", "", 16, Red, 10);
            _kicker.rectTransform.TL(0, 0, 1200, 26);
            _title = T(b, "Title", "", 52, Bone, TextAlignmentOptions.Center, true);
            _title.rectTransform.TL(0, 24, 1200, 66);
            _titleDash = TavernUI.Line(b, "Dash", Red);
            _titleDash.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -96, 120, Thk(3));
            _line = T(b, "Line", "", 16, Dim);
            _line.rectTransform.TL(0, 108, 1200, 24);
            _line.characterSpacing = Sp(6);
        }

        void TickBanner(HudModel m)
        {
            string kicker = "", title = "", line = "";
            bool hot = false;

            if (m.ChoosingShield)
            {
                kicker = "BEFORE THE FIRST TURN"; title = "CHOOSE YOUR SHIELD"; hot = true;
                line = "LOOK AT ONE OF YOUR THREE CARDS AND CLICK IT  ·  THE OTHER TWO BECOME YOUR HEALTH";
            }
            else if (m.WaitingForSetup) { kicker = "BEFORE THE FIRST TURN"; title = "THE TABLE IS CHOOSING"; line = "EVERYONE PICKS A SHIELD"; }
            else if (m.InMatch && !m.MatchOver)
            {
                if (!m.LocalAlive) { kicker = "YOU ARE OUT"; title = m.Possessive(m.Current) + " TURN"; line = "WATCHING THE TABLE"; }
                else if (!m.MyTurn) { kicker = "WAITING"; title = m.Possessive(m.Current) + " TURN"; }
                else if (m.Pending == HudPendingAction.None) { kicker = "YOUR MOVE"; title = "YOUR TURN"; hot = true; line = "CHOOSE AN ACTION"; }
                else
                {
                    kicker = "YOUR TURN"; title = m.ActionName(m.Pending); hot = true;
                    line = m.Hovered < 0 ? AimHint(m.Pending) : ConfirmHint(m);
                }
            }

            _kicker.text = kicker;
            _title.text = title;
            _line.text = line;
            _title.fontSize = hot ? 52 : 36;
            _title.color = hot ? Bone : Dim;
            float w = Mathf.Min(_title.GetPreferredValues(title).x, 900f);
            _titleDash.rectTransform.sizeDelta = new Vector2(hot && title.Length > 0 ? w * 0.35f : 0f, Thk(3));
        }

        public static string AimHint(HudPendingAction p)
        {
            return p == HudPendingAction.ReplaceShield
                ? "AIM AT A PLAYER OR A SHIELD  ·  YOUR OWN WORKS TOO"
                : "AIM AT ANOTHER PLAYER OR THEIR SHIELD";
        }

        public static string ConfirmHint(HudModel m)
        {
            string who = m.Hovered == m.Local ? "YOURSELF" : m.Name(m.Hovered);
            return "CLICK TO " + m.ActionName(m.Pending) + "  " + who;
        }

        // ------------------------------------------------------------------ event

        void BuildEvent()
        {
            _event = UiKit.Node(Root, "Event").At(0.5f, 1, 0.5f, 1, 0, -190, 1200, 90);
            _eventDash = TavernUI.Line(_event, "Dash", Red);
            _eventDash.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -6, 60, Thk(2));
            _eventHead = T(_event, "Head", "", 30, Bone, TextAlignmentOptions.Center, true);
            _eventHead.rectTransform.TL(0, 14, 1200, 40);
            _eventDetail = T(_event, "Detail", "", 18, Dim);
            _eventDetail.rectTransform.TL(0, 56, 1200, 26);
            _eventDetail.characterSpacing = Sp(5);
        }

        void TickEvent(HudModel m)
        {
            HudNote note = m.Notes.Count > 0 ? m.Notes[m.Notes.Count - 1] : null;
            float a = note != null ? Life(note.Time, note.Kind == NoteKind.Big ? 5f : 4f) : 0f;
            if (!m.InMatch) a = 0f;
            Show(_event, a > 0.001f);
            if (a <= 0.001f) return;

            if (note != _shownNote) _shownNote = note;
            _eventHead.text = note.Headline;
            _eventDetail.text = note.Detail;
            Color head = note.Kind == NoteKind.Bad ? UiKit.Hex("#E5503C") : note.Kind == NoteKind.Big ? Red : Bone;
            _eventHead.color = head.A(a);
            _eventHead.fontSize = note.Kind == NoteKind.Big ? 40 : 30;
            _eventDetail.color = Dim.A(a);
            _eventDash.color = Red.A(a);
            float rise = (1f - Mathf.Clamp01((Time.unscaledTime - note.Time) / 0.25f)) * 12f;
            _event.anchoredPosition = new Vector2(0, (m.MyTurn ? -244 : -176) - rise);
        }

        // ------------------------------------------------------------------ actions

        void BuildActions()
        {
            // Inline under the banner: the bottom centre belongs to your own cards on the table.
            _actions = UiKit.Node(Root, "Actions").At(0.5f, 1, 0.5f, 1, 0, -170, 960, 52);
            string[] labels = { "ATTACK", "REPLACE SHIELD", "CHARGE" };
            float[] xs = { -300, 0, 300 };
            for (int i = 0; i < 3; i++)
            {
                RectTransform a = UiKit.Node(_actions, "Action" + i).At(0.5f, 0.5f, 0.5f, 0.5f, xs[i], 0, 300, 52);
                _actSeals[i] = Img(a, "Seal", UiSprites.WaxSeal(), Red);
                _actSeals[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 34, 0, 36, 36);
                TextMeshProUGUI key = T(_actSeals[i].transform, "Key", (i + 1).ToString(), 20, Bone, TextAlignmentOptions.Center, true);
                key.rectTransform.Fill(0, 0, 0, 2);
                _actLabels[i] = T(a, "Label", labels[i], 22, Dim, TextAlignmentOptions.MidlineLeft);
                _actLabels[i].rectTransform.TL(62, 0, 240, 52);
                _actDashes[i] = TavernUI.Line(a, "Dash", Red);
                _actDashes[i].rectTransform.At(0, 0, 0, 0.5f, 62, 4, 0, Thk(3));
            }
        }

        void TickActions(HudModel m)
        {
            bool on = m.MyTurn;
            Show(_actions, on);
            if (!on) return;
            _actLabels[2].text = m.ThirdAction;
            for (int i = 0; i < 3; i++)
            {
                bool sel = (int)m.Pending == i + 1 || (i == 2 && m.Pending == HudPendingAction.ChargedAttack);
                _actSel[i] = Mathf.MoveTowards(_actSel[i], sel ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                float s = UiFx.Ease(_actSel[i]);
                _actLabels[i].color = UiKit.Lerp(m.Pending == HudPendingAction.None ? Bone : Muted, Bone, s);
                _actLabels[i].fontSize = 22 + 2 * s;
                float w = _actLabels[i].GetPreferredValues(_actLabels[i].text).x;
                _actDashes[i].rectTransform.sizeDelta = new Vector2(w * s, Thk(3));
                _actSeals[i].rectTransform.localScale = Vector3.one * (1f + 0.15f * s);
            }
        }

        // ------------------------------------------------------------------ table

        void BuildTable()
        {
            _table = UiKit.Node(Root, "Table").At(1, 0, 1, 0, -48, 40, 440, 60 + MaxRows * 40);
            TextMeshProUGUI kicker = K(_table, "Kicker", "THE TABLE", 16, Red, 10, TextAlignmentOptions.MidlineLeft);
            kicker.rectTransform.TL(0, 0, 200, 26);
            _tableMeta = T(_table, "Meta", "", 14, Muted, TextAlignmentOptions.MidlineRight);
            _tableMeta.rectTransform.TL(140, 0, 300, 26);
            _tableMeta.characterSpacing = Sp(6);

            for (int i = 0; i < MaxRows; i++)
            {
                RectTransform row = UiKit.Node(_table, "Row" + i).TL(0, 36 + i * 40, 440, 38);
                _rows[i] = row;
                _rowTurn[i] = TavernUI.Line(row, "Turn", Red);
                _rowTurn[i].rectTransform.At(0, 0.5f, 1, 0.5f, 18, 0, 26, Thk(4));
                _rowName[i] = T(row, "Name", "", 22, Dim, TextAlignmentOptions.MidlineLeft);
                _rowName[i].rectTransform.TL(26, 0, 220, 38);
                _rowStrike[i] = TavernUI.Line(row, "Strike", Muted);
                _rowStrike[i].rectTransform.At(0, 0.5f, 0, 0.5f, 22, 0, 150, Thk(2));

                _rowHeart[i] = Img(row, "Heart", HudSprites.Heart(), Red);
                _rowHeart[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 270, 0, 18, 18);
                _rowHp[i] = T(row, "Hp", "", 22, Bone, TextAlignmentOptions.MidlineLeft);
                _rowHp[i].rectTransform.TL(284, 0, 60, 38);
                _rowShieldIcon[i] = Img(row, "ShieldIcon", HudSprites.Shield(), Dim);
                _rowShieldIcon[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 352, 0, 18, 18);
                _rowShield[i] = T(row, "Shield", "", 22, Bone, TextAlignmentOptions.MidlineLeft);
                _rowShield[i].rectTransform.TL(366, 0, 50, 38);
                _rowCharge[i] = Img(row, "Charge", HudSprites.Bolt(), UiKit.Hex("#E2B04A"));
                _rowCharge[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 428, 0, 18, 18);
            }
        }

        void TickTable(HudModel m)
        {
            bool on = m.InMatch;
            Show(_table, on);
            if (!on) return;

            int n = Mathf.Min(m.PlayerCount, MaxRows);
            _table.sizeDelta = new Vector2(440, 40 + n * 40);
            _tableMeta.text = "DECK " + m.Snap.RemainingDrawCount + "   ·   " + DirectionText(m);
            for (int i = 0; i < MaxRows; i++)
            {
                Show(_rows[i], i < n);
                if (i >= n) continue;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                bool you = i == m.Local;
                _rowName[i].text = m.Name(i) + (p.IsBot ? "  ·  BOT" : "");
                _rowName[i].color = !p.IsAlive ? Muted : turn ? Bone : you ? Bone.A(0.9f) : Dim;
                _rowTurn[i].rectTransform.sizeDelta = new Vector2(turn ? 26 : 0, Thk(4));
                _rowName[i].rectTransform.anchoredPosition = new Vector2(turn ? 32 : 26, 0);
                Show(_rowStrike[i], !p.IsAlive);
                if (!p.IsAlive) _rowStrike[i].rectTransform.sizeDelta = new Vector2(_rowName[i].GetPreferredValues(_rowName[i].text).x + 8, Thk(2));

                _rowHp[i].text = p.IsAlive ? p.Health.ToString() : "—";
                _rowShield[i].text = p.IsAlive ? p.ShieldValue.ToString() : "—";
                _rowHp[i].color = _rowShield[i].color = p.IsAlive ? Bone : Muted;
                _rowHeart[i].color = p.IsAlive ? Red : Muted;
                Show(_rowCharge[i], p.IsAlive && p.HasCharge);
            }
        }

        // ------------------------------------------------------------------ nameplates

        void BuildPlates()
        {
            for (int i = 0; i < MaxRows; i++)
            {
                RectTransform p = UiKit.Node(Root, "Plate" + i).At(0.5f, 0.5f, 0.5f, 1, 0, 0, 260, 70);
                _plates[i] = p;
                _plateName[i] = T(p, "Name", "", 20, Bone, TextAlignmentOptions.Center, true);
                _plateName[i].rectTransform.TL(0, 0, 260, 28);
                _plateHp[i] = T(p, "Hp", "", 15, Dim);
                _plateHp[i].rectTransform.TL(0, 28, 260, 22);
                _plateHp[i].characterSpacing = Sp(5);
                _plateDash[i] = TavernUI.Line(p, "Dash", Red);
                _plateDash[i].rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -58, 40, Thk(3));
                p.SetAsFirstSibling();
            }
        }

        void TickPlates(HudModel m)
        {
            for (int i = 0; i < MaxRows; i++)
            {
                bool on = m.InMatch && i < m.PlayerCount && i != m.Local && Project(i, out Vector2 pos);
                Show(_plates[i], on);
                if (!on) continue;
                Project(i, out Vector2 at);
                _plates[i].anchoredPosition = at;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                _plateName[i].text = m.Name(i);
                _plateName[i].color = p.IsAlive ? (turn ? Bone : Dim) : Muted;
                _plateHp[i].text = p.IsAlive ? p.Health + " HEALTH" : "OUT";
                _plateDash[i].rectTransform.sizeDelta = new Vector2(turn ? 40 : 0, Thk(3));
            }
        }

        // ------------------------------------------------------------------ beer + end

        void BuildBeer()
        {
            _orderSeal = Img(Root, "OrderKey", HudSprites.RingSprite(5), Dim);
            _orderSeal.rectTransform.At(0, 0, 0.5f, 0.5f, 66, 60, 34, 34);
            T(_orderSeal.transform, "Key", "O", 16, Bone, TextAlignmentOptions.Center, true).rectTransform.Fill(0, 0, 0, 1);
            _orderText = T(Root, "Order", "ORDER A ROUND", 18, Dim, TextAlignmentOptions.MidlineLeft);
            _orderText.rectTransform.At(0, 0, 0, 0.5f, 92, 60, 260, 30);
            _orderText.characterSpacing = Sp(5);

            _drinkSeal = Img(Root, "DrinkKey", HudSprites.RingSprite(5), Dim);
            _drinkSeal.rectTransform.At(0, 0, 0.5f, 0.5f, 66, 104, 34, 34);
            T(_drinkSeal.transform, "Key", "B", 16, Bone, TextAlignmentOptions.Center, true).rectTransform.Fill(0, 0, 0, 1);
            _drinkText = T(Root, "Drink", "DRINK", 18, Dim, TextAlignmentOptions.MidlineLeft);
            _drinkText.rectTransform.At(0, 0, 0, 0.5f, 92, 104, 260, 30);
            _drinkText.characterSpacing = Sp(5);
        }

        void TickBeer(HudModel m)
        {
            bool order = m.OrderAvailable && !m.OrderOpen;
            Show(_orderSeal, order); Show(_orderText, order);
            bool drink = m.CanDrink || m.Drinking;
            Show(_drinkSeal, drink); Show(_drinkText, drink);
            _drinkText.text = m.Drinking ? "DRINKING…" : "HOLD TO DRINK";
            _drinkSeal.color = m.Drinking ? Red : Dim;
        }

        void BuildEnd()
        {
            _end = UiKit.Node(Root, "End").At(0.5f, 0.5f, 0.5f, 0.5f, 0, 80, 1200, 260);
            Image shade = Img(_end, "Shade", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.75f));
            shade.rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 1500, 520);
            _endKicker = K(_end, "Kicker", "", 18, Red, 12);
            _endKicker.rectTransform.TL(0, 20, 1200, 30);
            _endTitle = T(_end, "Title", "", 96, Bone, TextAlignmentOptions.Center, true);
            _endTitle.rectTransform.TL(0, 50, 1200, 120);
            Image dash = TavernUI.Line(_end, "Dash", Red);
            dash.rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -180, 140, Thk(4));
            _endLine = T(_end, "Line", "", 20, Dim);
            _endLine.rectTransform.TL(0, 200, 1200, 30);
            _endLine.characterSpacing = Sp(6);
        }

        void TickEnd(HudModel m)
        {
            Show(_end, m.InMatch && m.MatchOver);
            if (!m.MatchOver) return;
            bool won = m.Winner == m.Local;
            _endKicker.text = won ? "THE LAST ONE STANDING" : "THE TABLE HAS A WINNER";
            _endTitle.text = won ? "YOU WIN" : m.Name(m.Winner) + " WINS";
            _endLine.text = m.RematchVisible ? "R   REMATCH          ESC   FREE THE CURSOR" : "";
        }

        // ------------------------------------------------------------------ frame

        public override void Tick(HudModel m)
        {
            TickBanner(m);
            TickEvent(m);
            TickActions(m);
            TickTable(m);
            TickPlates(m);
            TickBeer(m);
            TickEnd(m);
        }
    }
}
