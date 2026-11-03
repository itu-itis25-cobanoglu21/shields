using System.Collections.Generic;
using Shields.Match;
using Shields.Online;
using Shields.Tavern;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UI
{
    /// <summary>
    /// The in-match HUD's widgets, built once on <see cref="GameHud"/>'s canvas and then
    /// mirroring the <see cref="HudModel"/> every frame:
    ///   banner (choose your Shield / your turn / targeting) — top centre
    ///   action keys — the key (1 / 2 / 3) on a red wax seal, under the banner on your turn
    ///   refusal — the host's "not allowed" line, under the action keys (online)
    ///   event feed — parchment slips top left. Attacks read as a red attack card, an
    ///                arrow and the target's green Shield card
    ///   order a round — a wooden plaque bottom left, which opens into the order panel
    ///   table list — bottom right; your own name in gold
    ///   names — fixed in the air above each head; they leave the screen when you look away
    ///   drink prompt, winner + rematch
    /// Presentation only.
    /// </summary>
    public sealed class GameHudView
    {
        readonly GameHud _host;
        readonly RectTransform Root;
        readonly TavernUI UI;

        static readonly Color Bone = TavernUI.Bone, Dim = TavernUI.Dim, Muted = TavernUI.Muted, Red = TavernUI.Red;

        // Tavern Board palette, for the slips and the order plaque.
        static readonly Color Wood = UiKit.Hex("#1B110A", 0.92f);
        static readonly Color Brass = UiKit.Hex("#C0904A");
        static readonly Color Cream = UiKit.Hex("#F0E3C6");
        static readonly Color Parchment = UiKit.Hex("#E6D5B0", 0.96f);
        static readonly Color Ink = UiKit.Hex("#2A1A0F");
        static readonly Color InkRed = UiKit.Hex("#9B2A20");

        /// <summary>Your own name, wherever the HUD lists the table.</summary>
        static readonly Color Gold = UiKit.Hex("#F2C14E");

        // Mini cards on the event slips: the attack card and the Shield it meets.
        static readonly Color AttackCard = UiKit.Hex("#B3322A");
        static readonly Color AttackCardBack = UiKit.Hex("#7E1F19");
        static readonly Color ShieldCard = UiKit.Hex("#3D7A3A");

        // Order panel feedback, readable on the dark corner.
        static readonly Color Accepted = UiKit.Hex("#9CD67C");
        static readonly Color Refused = UiKit.Hex("#E2584B");

        const int MaxRows = 8;

        // Banner
        TextMeshProUGUI _kicker, _title, _line;

        // Actions
        RectTransform _actions;
        readonly TextMeshProUGUI[] _actLabels = new TextMeshProUGUI[3];
        readonly RectTransform[] _actKeys = new RectTransform[3];
        readonly Image[] _actSeals = new Image[3];
        readonly TextMeshProUGUI[] _actKeyText = new TextMeshProUGUI[3];
        readonly float[] _actSel = new float[3];

        // Refusal (online)
        TextMeshProUGUI _refusal;

        // Slips
        const int Slips = 4;
        const float SlipW = 440, SlipH = 76, SlipStep = 84;
        readonly RectTransform[] _slips = new RectTransform[Slips];
        readonly TextMeshProUGUI[] _slipHead = new TextMeshProUGUI[Slips], _slipDetail = new TextMeshProUGUI[Slips];
        readonly CanvasGroup[] _slipGroups = new CanvasGroup[Slips];
        // Attack line: [red card] → [green card]  3 DAMAGE
        readonly RectTransform[] _slipAttack = new RectTransform[Slips];
        readonly Image[] _slipPowerBack = new Image[Slips];
        readonly TextMeshProUGUI[] _slipPower = new TextMeshProUGUI[Slips], _slipShield = new TextMeshProUGUI[Slips], _slipOutcome = new TextMeshProUGUI[Slips];

        // Table
        RectTransform _table;
        readonly RectTransform[] _rows = new RectTransform[MaxRows];
        readonly TextMeshProUGUI[] _rowName = new TextMeshProUGUI[MaxRows], _rowHp = new TextMeshProUGUI[MaxRows], _rowShield = new TextMeshProUGUI[MaxRows];
        readonly Image[] _rowTurn = new Image[MaxRows], _rowStrike = new Image[MaxRows], _rowCharge = new Image[MaxRows], _rowHeart = new Image[MaxRows], _rowShieldIcon = new Image[MaxRows];

        // Names above heads
        readonly RectTransform[] _plates = new RectTransform[MaxRows];
        readonly TextMeshProUGUI[] _plateName = new TextMeshProUGUI[MaxRows], _plateHp = new TextMeshProUGUI[MaxRows];
        readonly Image[] _plateTurn = new Image[MaxRows];

        // Beer + end
        RectTransform _order;
        Image _orderEdge;
        TextMeshProUGUI _drinkText;
        Image _drinkSeal;

        // Order panel: the plaque opened up into a list of who drinks.
        const float PanelW = 340, PanelTop = 96, PanelRowH = 36, PanelFooter = 112;
        RectTransform _panel;
        readonly RectTransform[] _panelRows = new RectTransform[MaxRows];
        readonly TextMeshProUGUI[] _panelName = new TextMeshProUGUI[MaxRows], _panelBot = new TextMeshProUGUI[MaxRows];
        readonly Image[] _panelTick = new Image[MaxRows];
        readonly UiFx[] _panelRowFx = new UiFx[MaxRows];
        readonly int[] _panelSeat = new int[MaxRows];
        TextMeshProUGUI _panelCount, _panelOrderLabel;
        Button _panelOrder;
        TextMeshProUGUI _beerFeedback;
        RectTransform _end;
        TextMeshProUGUI _endKicker, _endTitle, _endLine;

        public GameHudView(GameHud host, Canvas canvas, TavernUI ui)
        {
            _host = host;
            Root = (RectTransform)canvas.transform;
            UI = ui;
            Build();
        }

        // ------------------------------------------------------------------ helpers

        static Image Img(Transform parent, string name, Sprite sprite, Color color) { return UiKit.Img(parent, name, sprite, color); }

        TextMeshProUGUI T(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool strong = false)
        {
            return UI.T(parent, name, text, size, color, align, strong);
        }

        TextMeshProUGUI K(Transform parent, string name, string text, float size, Color color, float spacing,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            return UI.Kicker(parent, name, text, size, color, spacing, align);
        }

        static void Show(Component c, bool on) { UiKit.SetActive(c, on); }

        static float Sp(float spacing) { return TavernUI.Sp(spacing); }

        /// <summary>0..1 visibility for something shown at <paramref name="shownAt"/> for <paramref name="life"/> seconds.</summary>
        static float Life(float shownAt, float life, float fadeIn = 0.2f, float fadeOut = 0.6f)
        {
            float age = Time.unscaledTime - shownAt;
            if (age < 0f || age > life) return 0f;
            return Mathf.Min(Mathf.Clamp01(age / fadeIn), Mathf.Clamp01((life - age) / fadeOut));
        }

        static string AimHint(HudPendingAction p)
        {
            return p == HudPendingAction.ReplaceShield
                ? "AIM AT A PLAYER OR A SHIELD  ·  YOUR OWN WORKS TOO"
                : "AIM AT ANOTHER PLAYER OR THEIR SHIELD";
        }

        static string ConfirmHint(HudModel m)
        {
            string who = m.Hovered == m.Local ? "YOURSELF" : m.Name(m.Hovered);
            return "CLICK TO " + m.ActionName(m.Pending) + "  " + who;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            // Ink's local darkening: a soft band at the top, pools in the bottom corners.
            Image topBand = Img(Root, "TopBand", HudSprites.FadeDown(), new Color(0, 0, 0, 0.6f));
            topBand.rectTransform.anchorMin = new Vector2(0, 1);
            topBand.rectTransform.anchorMax = new Vector2(1, 1);
            topBand.rectTransform.pivot = new Vector2(0.5f, 1);
            topBand.rectTransform.anchoredPosition = Vector2.zero;
            topBand.rectTransform.sizeDelta = new Vector2(0, 330);
            Img(Root, "TablePool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.75f)).rectTransform.At(1, 0, 0.5f, 0.5f, -150, 110, 1000, 640);
            Img(Root, "BeerPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.5f)).rectTransform.At(0, 0, 0.5f, 0.5f, 60, 60, 700, 360);

            BuildPlates();
            BuildBanner();
            BuildActions();
            BuildSlips();
            BuildTable();
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
            _line = T(b, "Line", "", 16, Dim);
            _line.rectTransform.TL(0, 96, 1200, 24);
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
        }

        // ------------------------------------------------------------------ actions

        void BuildActions()
        {
            _actions = UiKit.Node(Root, "Actions").At(0.5f, 1, 0.5f, 1, 0, -160, 960, 56);
            string[] labels = { "ATTACK", "REPLACE SHIELD", "CHARGE" };
            float[] xs = { -300, 0, 300 };
            for (int i = 0; i < 3; i++)
            {
                RectTransform a = UiKit.Node(_actions, "Action" + i).At(0.5f, 0.5f, 0.5f, 0.5f, xs[i], 0, 300, 56);
                _actKeys[i] = UiKit.Node(a, "Key").At(0, 0.5f, 0.5f, 0.5f, 34, 0, 40, 40);
                _actSeals[i] = Img(_actKeys[i], "Seal", UiSprites.WaxSeal(), Red);
                _actSeals[i].rectTransform.Fill();
                _actLabels[i] = T(a, "Label", labels[i], 22, Dim, TextAlignmentOptions.MidlineLeft);
                _actLabels[i].rectTransform.TL(64, 0, 240, 56);

                // The key to press, stamped on the seal in bone.
                _actKeyText[i] = T(_actKeys[i], "Key", (i + 1).ToString(), 22, Bone, TextAlignmentOptions.Center, true);
                _actKeyText[i].rectTransform.Fill(0, 0, 0, 2);
            }

            _refusal = T(Root, "Refusal", "", 20, Refused, TextAlignmentOptions.Center);
            _refusal.rectTransform.At(0.5f, 1, 0.5f, 1, 0, -224, 1200, 30);
            _refusal.characterSpacing = Sp(5);
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
                bool others = m.Pending != HudPendingAction.None && !sel;
                _actLabels[i].color = UiKit.Lerp(others ? Muted : Bone, Bone, s);
                _actLabels[i].fontSize = 22 + 2 * s;
                _actKeys[i].localScale = Vector3.one * (1f + 0.15f * s);
                _actSeals[i].color = others ? Red.A(0.55f) : Red;
                _actKeyText[i].color = others ? Bone.A(0.6f) : Bone;
            }
        }

        void TickRefusal(HudModel m)
        {
            bool on = m.InMatch && !string.IsNullOrEmpty(m.Refusal);
            Show(_refusal, on);
            if (!on) return;
            _refusal.text = m.Refusal.ToUpperInvariant();
            _refusal.color = Refused.A(m.RefusalAlpha);
        }

        // ------------------------------------------------------------------ event slips

        void BuildSlips()
        {
            for (int i = 0; i < Slips; i++)
            {
                RectTransform slip = UiKit.Node(Root, "Slip" + i).TL(26, 26 + i * SlipStep, SlipW, SlipH);
                _slips[i] = slip;
                _slipGroups[i] = slip.gameObject.AddComponent<CanvasGroup>();
                Img(slip, "Paper", UiSprites.DeckledFill(), Parchment).rectTransform.Fill();
                _slipHead[i] = UiKit.Text(slip, "Head", "", UI.Bowlby, 19, Ink, TextAlignmentOptions.TopLeft);
                _slipHead[i].rectTransform.TL(20, 9, SlipW - 40, 28);
                _slipHead[i].enableAutoSizing = true;
                _slipHead[i].fontSizeMin = 14;
                _slipHead[i].fontSizeMax = 19;
                _slipDetail[i] = UiKit.Text(slip, "Detail", "", UI.Bowlby, 13, Ink.A(0.7f), TextAlignmentOptions.TopLeft);
                _slipDetail[i].rectTransform.TL(20, 37, SlipW - 40, 22);
                _slipDetail[i].characterSpacing = Sp(4);
                _slipDetail[i].enableAutoSizing = true;
                _slipDetail[i].fontSizeMin = 10;
                _slipDetail[i].fontSizeMax = 13;

                BuildAttackLine(slip, i);
            }
        }

        /// <summary>[red attack card] → [green Shield card]  3 DAMAGE, under an attack headline.</summary>
        void BuildAttackLine(RectTransform slip, int i)
        {
            RectTransform line = UiKit.Node(slip, "Attack").TL(20, 38, SlipW - 40, 30);
            _slipAttack[i] = line;

            // A Charged Attack is two cards: the second one peeks out behind.
            _slipPowerBack[i] = Img(line, "PowerBack", UiSprites.RoundRect(4), AttackCardBack);
            _slipPowerBack[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 18, 1, 24, 30);
            _slipPowerBack[i].rectTransform.localEulerAngles = new Vector3(0, 0, -12);
            _slipPower[i] = MiniCard(line, "Power", 12, AttackCard);

            Img(line, "Shaft", UiSprites.White(), Ink.A(0.75f)).rectTransform.At(0, 0.5f, 0, 0.5f, 31, 0, 19, 3);
            Img(line, "Head", HudSprites.Arrow(), Ink.A(0.75f)).rectTransform.At(0, 0.5f, 0.5f, 0.5f, 52, 0, 14, 14);

            _slipShield[i] = MiniCard(line, "Shield", 74, ShieldCard);

            _slipOutcome[i] = UiKit.Text(line, "Outcome", "", UI.Bowlby, 16, Ink, TextAlignmentOptions.MidlineLeft);
            _slipOutcome[i].rectTransform.TL(98, 0, SlipW - 40 - 98, 30);
            _slipOutcome[i].characterSpacing = Sp(3);
            _slipOutcome[i].enableAutoSizing = true;
            _slipOutcome[i].fontSizeMin = 11;
            _slipOutcome[i].fontSizeMax = 16;
        }

        /// <summary>A small coloured card with a number on it and a soft drop shadow; returns the number.</summary>
        TextMeshProUGUI MiniCard(RectTransform parent, string name, float centreX, Color color)
        {
            Img(parent, name + "Shadow", UiSprites.RoundRect(4), Ink.A(0.3f)).rectTransform.At(0, 0.5f, 0.5f, 0.5f, centreX + 1.5f, -1.5f, 24, 30);
            Image card = Img(parent, name, UiSprites.RoundRect(4), color);
            card.rectTransform.At(0, 0.5f, 0.5f, 0.5f, centreX, 0, 24, 30);
            TextMeshProUGUI number = UiKit.Text(card.transform, "Number", "", UI.Bowlby, 16, Cream, TextAlignmentOptions.Center);
            number.rectTransform.Fill(1, 0, 1, 1);
            number.enableAutoSizing = true;
            number.fontSizeMin = 10;
            number.fontSizeMax = 16;
            return number;
        }

        static string AttackHeadline(HudModel m, HudNote n)
        {
            return m.Name(n.Actor) + (n.Charged ? " FIRED A CHARGE AT " : " ATTACKED ") + m.Name(n.Target);
        }

        void TickSlips(HudModel m)
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
                _slipHead[i].text = note.IsAttack ? AttackHeadline(m, note) : note.Headline;
                _slipDetail[i].text = note.Detail;
                Show(_slipDetail[i], !note.IsAttack);
                Show(_slipAttack[i], note.IsAttack);
                if (note.IsAttack)
                {
                    Show(_slipPowerBack[i], note.Charged);
                    _slipPower[i].text = note.Power.ToString();
                    _slipShield[i].text = note.Shield.ToString();
                    _slipOutcome[i].text = (note.Damage > 0 ? note.Damage + " DAMAGE" : "BLOCKED")
                        + (note.ChargeBroken ? "  ·  CHARGE BROKEN" : "");
                    _slipOutcome[i].color = note.Damage > 0 ? InkRed : Ink.A(0.7f);
                }
                _slipHead[i].color = note.Kind == NoteKind.Bad || note.Kind == NoteKind.Big ? InkRed : Ink;
                float slide = (1f - Mathf.Clamp01((Time.unscaledTime - note.Time) / 0.3f)) * -40f;
                _slips[i].anchoredPosition = new Vector2(26 + slide, -(26 + i * SlipStep));
                _slips[i].localEulerAngles = new Vector3(0, 0, i % 2 == 0 ? 0.6f : -0.5f);
            }
        }

        // ------------------------------------------------------------------ table

        void BuildTable()
        {
            _table = UiKit.Node(Root, "Table").At(1, 0, 1, 0, -48, 40, 440, MaxRows * 40);
            for (int i = 0; i < MaxRows; i++)
            {
                RectTransform row = UiKit.Node(_table, "Row" + i).TL(0, i * 40, 440, 38);
                _rows[i] = row;
                // The turn mark is a small bone arrow, not an ink stroke.
                _rowTurn[i] = Img(row, "Turn", HudSprites.Arrow(), Bone);
                _rowTurn[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 10, 0, 14, 14);
                _rowName[i] = T(row, "Name", "", 22, Dim, TextAlignmentOptions.MidlineLeft);
                _rowName[i].rectTransform.TL(26, 0, 236, 38);
                _rowName[i].overflowMode = TextOverflowModes.Ellipsis;
                _rowStrike[i] = Img(row, "Strike", UiSprites.White(), Muted);
                _rowStrike[i].rectTransform.At(0, 0.5f, 0, 0.5f, 22, 0, 150, 2);

                _rowHeart[i] = Img(row, "Heart", HudSprites.Heart(), Red);
                _rowHeart[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 278, 0, 18, 18);
                _rowHp[i] = T(row, "Hp", "", 22, Bone, TextAlignmentOptions.MidlineLeft);
                _rowHp[i].rectTransform.TL(292, 0, 56, 38);
                _rowShieldIcon[i] = Img(row, "ShieldIcon", HudSprites.Shield(), Dim);
                _rowShieldIcon[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 358, 0, 18, 18);
                _rowShield[i] = T(row, "Shield", "", 22, Bone, TextAlignmentOptions.MidlineLeft);
                _rowShield[i].rectTransform.TL(372, 0, 44, 38);
                _rowCharge[i] = Img(row, "Charge", HudSprites.Bolt(), UiKit.Hex("#E2B04A"));
                _rowCharge[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 430, 0, 18, 18);
            }
        }

        void TickTable(HudModel m)
        {
            bool on = m.InMatch;
            Show(_table, on);
            if (!on) return;

            int n = Mathf.Min(m.PlayerCount, MaxRows);
            _table.sizeDelta = new Vector2(440, n * 40);
            for (int i = 0; i < MaxRows; i++)
            {
                Show(_rows[i], i < n);
                if (i >= n) continue;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                bool you = i == m.Local;
                _rowName[i].text = m.Name(i) + (p.IsBot ? "  ·  BOT" : "");
                _rowName[i].color = !p.IsAlive ? Muted : you ? Gold : turn ? Bone : Dim;
                Show(_rowTurn[i], turn);
                _rowName[i].rectTransform.anchoredPosition = new Vector2(turn ? 30 : 26, 0);
                Show(_rowStrike[i], !p.IsAlive);
                if (!p.IsAlive) _rowStrike[i].rectTransform.sizeDelta = new Vector2(_rowName[i].GetPreferredValues(_rowName[i].text).x + 8, 2);

                _rowHp[i].text = p.IsAlive ? p.Health.ToString() : "—";
                _rowShield[i].text = p.IsAlive ? p.ShieldValue.ToString() : "—";
                _rowHp[i].color = _rowShield[i].color = p.IsAlive ? Bone : Muted;
                _rowHeart[i].color = p.IsAlive ? Red : Muted;
                Show(_rowCharge[i], p.IsAlive && p.HasCharge);
            }
        }

        // ------------------------------------------------------------------ names above heads

        void BuildPlates()
        {
            for (int i = 0; i < MaxRows; i++)
            {
                // Pivot at the bottom centre: the plate stands on the point above the head.
                RectTransform p = UiKit.Node(Root, "Plate" + i).At(0.5f, 0.5f, 0.5f, 0, 0, 0, 280, 44);
                _plates[i] = p;
                _plateTurn[i] = Img(p, "Turn", HudSprites.Arrow(), Bone);
                _plateTurn[i].rectTransform.At(0.5f, 1, 0.5f, 0, 0, 2, 14, 14);
                _plateTurn[i].rectTransform.localEulerAngles = new Vector3(0, 0, -90);
                _plateName[i] = T(p, "Name", "", 20, Bone, TextAlignmentOptions.Center, true);
                // Ellipsis hides a line that does not fit the box height, so the box is roomy.
                _plateName[i].rectTransform.TL(0, -8, 280, 42);
                _plateName[i].overflowMode = TextOverflowModes.Ellipsis;
                _plateHp[i] = T(p, "Hp", "", 15, Dim);
                _plateHp[i].rectTransform.TL(0, 24, 280, 20);
                _plateHp[i].characterSpacing = Sp(5);
            }
        }

        void TickPlates(HudModel m)
        {
            for (int i = 0; i < MaxRows; i++)
            {
                Vector2 at = Vector2.zero;
                float dist = 0f;
                bool on = m.InMatch && i < m.PlayerCount && i != m.Local && _host.ProjectAboveHead(i, Root, 0.02f, out at, out dist);
                Show(_plates[i], on);
                if (!on) continue;

                _plates[i].anchoredPosition = at;
                // Further heads get slightly smaller names, so they sit in the room rather than on the glass.
                _plates[i].localScale = Vector3.one * Mathf.Clamp(1.6f / Mathf.Max(dist, 0.1f), 0.72f, 1.05f);

                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                _plateName[i].text = m.Name(i);
                _plateName[i].color = p.IsAlive ? (turn ? Bone : Dim) : Muted;
                _plateHp[i].text = p.IsAlive ? p.Health + " HEALTH" : "OUT";
                Show(_plateTurn[i], turn);
            }
        }

        // ------------------------------------------------------------------ beer

        void BuildBeer()
        {
            // ORDER A ROUND: the Tavern Board plaque. Clickable while the cursor is free.
            Button plaque = UiKit.Button(Root, "Order", () => { if (_host.OrderUI != null) _host.OrderUI.RequestOpen(); }, out UiFx plaqueFx);
            _order = plaque.GetComponent<RectTransform>().At(0, 0, 0, 0, 26, 26, 236, 50);
            Plaque(_order, out _orderEdge);
            OrderKey(_order);
            T(_order, "Label", "ORDER A ROUND", 17, Cream, TextAlignmentOptions.MidlineLeft).rectTransform.TL(52, 0, 180, 50);
            plaqueFx.Apply = f => _orderEdge.color = Brass.A(0.6f + 0.4f * UiFx.Ease(f.Hover));

            BuildOrderPanel();

            // Order feedback (sent / accepted / refused) sits just above the plaque or panel.
            _beerFeedback = T(Root, "BeerFeedback", "", 16, Cream, TextAlignmentOptions.BottomLeft);
            _beerFeedback.rectTransform.At(0, 0, 0, 0, 30, 0, 520, 26);
            _beerFeedback.characterSpacing = Sp(4);

            // DRINK: Ink's ring key, lined up with the plaque's key.
            _drinkSeal = Img(Root, "DrinkKey", HudSprites.RingSprite(5), Dim);
            _drinkSeal.rectTransform.At(0, 0, 0.5f, 0.5f, 54, 104, 34, 34);
            T(_drinkSeal.transform, "Key", "B", 16, Bone, TextAlignmentOptions.Center, true).rectTransform.Fill(0, 0, 0, 1);
            _drinkText = T(Root, "Drink", "HOLD TO DRINK", 18, Dim, TextAlignmentOptions.MidlineLeft);
            _drinkText.rectTransform.At(0, 0, 0, 0.5f, 80, 104, 260, 30);
            _drinkText.characterSpacing = Sp(5);
        }

        /// <summary>Dark wood, a brass edge and a faint inner line: the Tavern Board plaque.</summary>
        static void Plaque(RectTransform r, out Image edge)
        {
            Img(r, "Fill", UiSprites.RoundRect(12), Wood).rectTransform.Fill();
            edge = Img(r, "Edge", HudSprites.RoundOutline(12, 2), Brass.A(0.6f));
            edge.rectTransform.Fill();
            Img(r, "Inner", HudSprites.RoundOutline(9, 1), Brass.A(0.18f)).rectTransform.Fill(5, 5, 5, 5);
        }

        /// <summary>The brass O keycap, top left of a plaque.</summary>
        void OrderKey(RectTransform parent)
        {
            Image cap = Img(parent, "Key", UiSprites.RoundRect(6), Brass);
            cap.rectTransform.TL(14, 11, 28, 28);
            UiKit.Text(cap.transform, "Key", "O", UI.Bowlby, 28 * 0.55f, Ink, TextAlignmentOptions.Center).rectTransform.Fill(0, 0, 0, 2);
        }

        void BuildOrderPanel()
        {
            // Grows up from where the plaque sits. Its height follows the number of seats.
            _panel = UiKit.Node(Root, "OrderPanel").At(0, 0, 0, 0, 26, 26, PanelW, 400);
            UiKit.Img(_panel, "Blocker", UiSprites.White(), new Color(0, 0, 0, 0), true).rectTransform.Fill();
            Plaque(_panel, out _);

            OrderKey(_panel);
            T(_panel, "Title", "ORDER A ROUND", 19, Cream, TextAlignmentOptions.MidlineLeft).rectTransform.TL(52, 0, PanelW - 64, 50);
            K(_panel, "Kicker", "WHO'S DRINKING?", 12, Brass, 6, TextAlignmentOptions.MidlineLeft).rectTransform.TL(22, 50, PanelW - 44, 28);
            Img(_panel, "TopRule", UiSprites.White(), Brass.A(0.3f)).rectTransform.TL(20, 84, PanelW - 40, 2);

            for (int i = 0; i < MaxRows; i++)
            {
                int index = i;
                Button b = UiKit.Button(_panel, "Row" + i, () => ToggleRow(index), out UiFx fx);
                RectTransform row = b.GetComponent<RectTransform>().TL(12, PanelTop + i * PanelRowH, PanelW - 24, PanelRowH - 2);
                _panelRows[i] = row;
                _panelRowFx[i] = fx;

                Image hover = Img(row, "Hover", UiSprites.RoundRect(8), Brass.A(0f));
                hover.rectTransform.Fill();
                Img(row, "Box", HudSprites.RoundOutline(6, 2), Brass.A(0.8f)).rectTransform.At(0, 0.5f, 0.5f, 0.5f, 22, 0, 22, 22);
                _panelTick[i] = Img(row, "Tick", UiSprites.RoundRect(6), Brass);
                _panelTick[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 22, 0, 22, 22);
                Img(_panelTick[i].transform, "Check", UiSprites.Check(), Ink).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 15, 15);

                _panelName[i] = T(row, "Name", "", 19, Cream, TextAlignmentOptions.MidlineLeft);
                _panelName[i].rectTransform.TL(46, 0, 190, PanelRowH - 2);
                _panelBot[i] = T(row, "Bot", "BOT", 13, Dim, TextAlignmentOptions.MidlineRight);
                _panelBot[i].rectTransform.TL(PanelW - 24 - 92, 0, 80, PanelRowH - 2);
                _panelBot[i].characterSpacing = Sp(5);

                fx.Apply = f => hover.color = Brass.A(0.14f * UiFx.Ease(f.Hover));
            }

            // Footer, anchored to the bottom edge.
            Img(_panel, "BottomRule", UiSprites.White(), Brass.A(0.3f)).rectTransform.At(0, 0, 0, 0, 20, PanelFooter - 8, PanelW - 40, 2);
            _panelCount = T(_panel, "Count", "", 18, Cream, TextAlignmentOptions.MidlineLeft);
            _panelCount.rectTransform.At(0, 0, 0, 0, 22, 64, PanelW - 44, 32);
            _panelCount.characterSpacing = Sp(4);

            _panelOrder = UiKit.Button(_panel, "OrderButton", () => { if (_host.OrderUI != null) _host.OrderUI.Order(); }, out UiFx orderFx);
            RectTransform order = _panelOrder.GetComponent<RectTransform>().At(0, 0, 0, 0, 20, 18, 172, 42);
            Image orderFill = Img(order, "Fill", UiSprites.RoundRect(10), Brass);
            orderFill.rectTransform.Fill();
            _panelOrderLabel = UiKit.Text(order, "Label", "ORDER", UI.Bowlby, 18, Ink, TextAlignmentOptions.Center);
            _panelOrderLabel.rectTransform.Fill(0, 0, 0, 2);
            _panelOrderLabel.characterSpacing = Sp(4);
            orderFx.Apply = f =>
            {
                bool on = f.IsActive;
                float h = UiFx.Ease(f.Hover);
                orderFill.color = on ? UiKit.Lerp(Brass, Cream, 0.3f * h) : Brass.A(0.3f);
                _panelOrderLabel.color = on ? Ink : Cream.A(0.55f);
                float s = 1f - 0.04f * f.Press;
                order.localScale = new Vector3(s, s, 1);
            };

            Button cancel = UiKit.Button(_panel, "CancelButton", () => { if (_host.OrderUI != null) _host.OrderUI.Cancel(); }, out UiFx cancelFx);
            RectTransform cancelRt = cancel.GetComponent<RectTransform>().At(0, 0, 0, 0, 204, 18, 116, 42);
            TextMeshProUGUI cancelLabel = T(cancelRt, "Label", "CANCEL", 17, Cream.A(0.6f), TextAlignmentOptions.Center);
            cancelLabel.rectTransform.Fill();
            cancelLabel.characterSpacing = Sp(4);
            cancelFx.Apply = f => cancelLabel.color = UiKit.Lerp(Cream.A(0.6f), Cream, UiFx.Ease(f.Hover));
        }

        void ToggleRow(int i)
        {
            BeerOrderUI ui = _host.OrderUI;
            if (ui == null) return;
            int seat = _panelSeat[i];
            ui.SetSelected(seat, !ui.IsSelected(seat));
        }

        /// <summary>Mirrors the open order panel; returns its height.</summary>
        float TickPanel(HudModel m, BeerOrderUI ui)
        {
            IReadOnlyList<BeerRecipient> list = ui.Recipients;
            int n = Mathf.Min(list.Count, MaxRows);
            for (int i = 0; i < MaxRows; i++)
            {
                Show(_panelRows[i], i < n);
                if (i >= n) continue;
                BeerRecipient r = list[i];
                _panelSeat[i] = r.Seat;
                bool on = ui.IsSelected(r.Seat);
                _panelName[i].text = r.IsLocal ? "YOU" : m.Name(r.Seat);
                _panelName[i].color = r.IsLocal ? Gold : on ? Cream : Cream.A(0.6f);
                Show(_panelTick[i], on);
                Show(_panelBot[i], r.IsBot);
            }

            float height = PanelTop + n * PanelRowH + 6 + PanelFooter;
            _panel.sizeDelta = new Vector2(PanelW, height);

            int beers = ui.SelectedCount;
            _panelCount.text = beers == 0 ? "NOBODY YET" : beers + (beers == 1 ? " BEER" : " BEERS");
            _panelCount.color = beers == 0 ? Cream.A(0.5f) : Cream;

            float cooldown = ui.CooldownRemaining;
            _panelOrder.interactable = beers > 0 && cooldown <= 0f && !ui.AwaitingReply;
            _panelOrderLabel.text = cooldown > 0f ? "WAIT " + Mathf.CeilToInt(cooldown) + "S" : ui.AwaitingReply ? "SENDING…" : "ORDER";
            return height;
        }

        void TickBeer(HudModel m)
        {
            BeerOrderUI ui = _host.OrderUI;
            bool open = m.OrderAvailable && m.OrderOpen && ui != null;
            Show(_order, m.OrderAvailable && !open);
            Show(_panel, open);
            float top = 26 + 50;
            if (open) top = 26 + TickPanel(m, ui);

            bool drink = (m.CanDrink || m.Drinking) && !open;
            Show(_drinkSeal, drink);
            Show(_drinkText, drink);
            _drinkText.text = m.Drinking ? "DRINKING…" : "HOLD TO DRINK";
            _drinkSeal.color = m.Drinking ? Bone : Dim;
            if (drink) top = 104 + 17;

            string text = null;
            BeerOrderUI.FeedbackTone tone = BeerOrderUI.FeedbackTone.Info;
            float alpha = 0f;
            bool feedback = ui != null && ui.TryGetFeedback(out text, out tone, out alpha);
            Show(_beerFeedback, feedback);
            if (!feedback) return;
            _beerFeedback.text = text;
            Color c = tone == BeerOrderUI.FeedbackTone.Accepted ? Accepted : tone == BeerOrderUI.FeedbackTone.Refused ? Refused : Brass;
            _beerFeedback.color = c.A(alpha);
            _beerFeedback.rectTransform.anchoredPosition = new Vector2(30, top + 10);
        }

        // ------------------------------------------------------------------ end

        void BuildEnd()
        {
            _end = UiKit.Node(Root, "End").At(0.5f, 0.5f, 0.5f, 0.5f, 0, 80, 1200, 260);
            Img(_end, "Shade", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.75f)).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 1500, 520);
            _endKicker = K(_end, "Kicker", "", 18, Red, 12);
            _endKicker.rectTransform.TL(0, 20, 1200, 30);
            _endTitle = T(_end, "Title", "", 96, Bone, TextAlignmentOptions.Center, true);
            _endTitle.rectTransform.TL(0, 50, 1200, 120);
            _endLine = T(_end, "Line", "", 20, Dim);
            _endLine.rectTransform.TL(0, 186, 1200, 30);
            _endLine.characterSpacing = Sp(6);
        }

        void TickEnd(HudModel m)
        {
            Show(_end, m.InMatch && m.MatchOver);
            if (!m.MatchOver) return;
            bool won = m.Winner == m.Local;
            _endKicker.text = won ? "THE LAST ONE STANDING" : "THE TABLE HAS A WINNER";
            _endTitle.text = won ? "YOU WIN" : m.Name(m.Winner) + " WINS";
            _endLine.text = m.RematchVisible ? RematchLine(m) + "          ESC   FREE THE CURSOR" : "";
        }

        /// <summary>Offline R restarts at once; online it toggles this player's vote.</summary>
        static string RematchLine(HudModel m)
        {
            if (!m.RematchOnline) return "R   REMATCH";
            return (m.RematchVoted ? "R   TAKE BACK VOTE" : "R   VOTE REMATCH") + "     " + m.RematchYes + " / " + m.RematchNeeded + " READY";
        }

        // ------------------------------------------------------------------ frame

        public void Tick(HudModel m)
        {
            TickBanner(m);
            TickActions(m);
            TickRefusal(m);
            TickSlips(m);
            TickTable(m);
            TickPlates(m);
            TickBeer(m);
            TickEnd(m);
        }
    }
}
