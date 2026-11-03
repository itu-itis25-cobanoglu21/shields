using Shields.Match;
using Shields.Online;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// HUD 4 — CARD TABLE BACKUP: an untouched copy of the original CARD TABLE. Everything speaks the game's own object, the playing card:
    /// players are a fanned row of cards bottom right (Health and Shield on the face, a
    /// stored Charge peeking out behind, face down once eliminated); the actions are a
    /// small hand bottom left; events arrive as cards top right; a dealer chip marks the turn.
    /// </summary>
    public sealed class HudCardsBackup : HudConcept
    {
        public override string Code { get { return "CardsBackup"; } }

        static readonly Color Face = UiKit.Hex("#F3EBDA");
        static readonly Color FaceEdge = UiKit.Hex("#2B2119", 0.55f);
        static readonly Color InkDark = UiKit.Hex("#231A14");
        static readonly Color SuitRed = UiKit.Hex("#B3261E");
        static readonly Color BackRed = UiKit.Hex("#6E1A18");
        static readonly Color ChipGold = UiKit.Hex("#E2B04A");

        struct Card { public RectTransform Rect; public Image Fill, Edge; }

        Card MakeCard(Transform parent, string name, float w, float h)
        {
            var c = new Card { Rect = UiKit.Node(parent, name) };
            c.Rect.sizeDelta = new Vector2(w, h);
            Img(c.Rect, "Shadow", UiSprites.RoundRect(12), new Color(0, 0, 0, 0.35f)).rectTransform.Fill(-3, 4, -6, -8);
            c.Fill = Img(c.Rect, "Fill", UiSprites.RoundRect(12), Face);
            c.Fill.rectTransform.Fill();
            c.Edge = Img(c.Rect, "Edge", HudSprites.RoundOutline(12, 2), FaceEdge);
            c.Edge.rectTransform.Fill();
            return c;
        }

        Image Chip(Transform parent, string name, float size, Color color, string text, Color textColor)
        {
            Image chip = Img(parent, name, HudSprites.Circle(), color);
            chip.rectTransform.sizeDelta = new Vector2(size, size);
            Img(chip.transform, "Ring", HudSprites.RingSprite(4), new Color(1, 1, 1, 0.85f)).rectTransform.Fill(size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);
            TextMeshProUGUI t = UiKit.Text(chip.transform, "Text", text, UI.Bowlby, size * 0.32f, textColor, TextAlignmentOptions.Center);
            t.rectTransform.Fill(0, 0, 0, 1);
            return chip;
        }

        // Banner
        RectTransform _banner;
        Image _bannerChip;
        TextMeshProUGUI _title, _sub;

        // Event cards
        const int EventCards = 3;
        readonly Card[] _events = new Card[EventCards];
        readonly Image[] _eventStripe = new Image[EventCards];
        readonly TextMeshProUGUI[] _eventHead = new TextMeshProUGUI[EventCards], _eventDetail = new TextMeshProUGUI[EventCards];
        readonly CanvasGroup[] _eventGroups = new CanvasGroup[EventCards];

        // Hand
        RectTransform _hand;
        readonly Card[] _handCards = new Card[3];
        readonly TextMeshProUGUI[] _handLabels = new TextMeshProUGUI[3];
        readonly float[] _handSel = new float[3];

        // Players
        const int Max = 8;
        RectTransform _fan;
        TextMeshProUGUI _fanMeta;
        readonly Card[] _pc = new Card[Max];
        readonly RectTransform[] _pcFace = new RectTransform[Max];
        readonly Image[] _pcBack = new Image[Max], _pcCharge = new Image[Max], _pcGlow = new Image[Max], _pcChip = new Image[Max];
        readonly TextMeshProUGUI[] _pcCorner = new TextMeshProUGUI[Max], _pcBot = new TextMeshProUGUI[Max], _pcHp = new TextMeshProUGUI[Max],
            _pcShield = new TextMeshProUGUI[Max], _pcOut = new TextMeshProUGUI[Max], _pcCorner2 = new TextMeshProUGUI[Max];
        readonly float[] _pcLift = new float[Max];

        // Tags
        readonly RectTransform[] _tags = new RectTransform[Max];
        readonly Image[] _tagEdge = new Image[Max];
        readonly TextMeshProUGUI[] _tagText = new TextMeshProUGUI[Max], _tagHp = new TextMeshProUGUI[Max];

        // Beer + end
        RectTransform _order, _drink;
        TextMeshProUGUI _drinkText;
        RectTransform _end;
        TextMeshProUGUI _endTitle, _endKicker;
        RectTransform _endRematch;

        protected override void Build()
        {
            Img(Root, "FanPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.55f)).rectTransform.At(1, 0, 0.5f, 0.5f, -260, 60, 1100, 520);
            Img(Root, "HandPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.45f)).rectTransform.At(0, 0, 0.5f, 0.5f, 180, 40, 760, 440);
            Img(Root, "TopPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.5f)).rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -40, 1300, 300);
            BuildTags();
            BuildBanner();
            BuildEvents();
            BuildHand();
            BuildFan();
            BuildBeer();
            BuildEnd();
        }

        // ------------------------------------------------------------------ banner

        void BuildBanner()
        {
            _banner = UiKit.Node(Root, "Banner").At(0.5f, 1, 0.5f, 1, 0, -28, 1100, 100);
            _bannerChip = Chip(_banner, "Chip", 64, SuitRed, "", Color.white);
            _title = T(_banner, "Title", "", 42, Face, TextAlignmentOptions.MidlineLeft, true);
            _sub = T(_banner, "Sub", "", 15, Dim, TextAlignmentOptions.MidlineLeft);
            _sub.characterSpacing = Sp(6);
        }

        void TickBanner(HudModel m)
        {
            string title = "", sub = "", chip = "";
            bool hot = false;
            if (m.ChoosingShield) { title = "CHOOSE YOUR SHIELD"; sub = "CLICK ONE OF YOUR THREE CARDS · THE OTHER TWO ARE YOUR HEALTH"; chip = "YOU"; hot = true; }
            else if (m.WaitingForSetup) { title = "THE TABLE IS CHOOSING"; sub = "EVERYONE PICKS A SHIELD"; chip = "…"; }
            else if (m.InMatch && !m.MatchOver)
            {
                chip = m.Current == m.Local ? "YOU" : "P" + (m.Current + 1);
                if (!m.LocalAlive) { title = m.Possessive(m.Current) + " TURN"; sub = "YOU ARE OUT · WATCHING THE TABLE"; }
                else if (!m.MyTurn) { title = m.Possessive(m.Current) + " TURN"; sub = m.Player(m.Current).IsBot ? "THE BOT IS THINKING" : "WAITING FOR THEIR MOVE"; }
                else if (m.Pending == HudPendingAction.None) { title = "YOUR TURN"; sub = "PLAY A CARD FROM YOUR HAND  ·  1  2  3"; hot = true; }
                else { title = m.ActionName(m.Pending); sub = m.Hovered < 0 ? HudInk.AimHint(m.Pending) : HudInk.ConfirmHint(m); hot = true; }
            }

            Show(_banner, title.Length > 0);
            _title.text = title;
            _sub.text = sub;
            _title.fontSize = hot ? 44 : 32;
            _title.color = hot ? Face : Face.A(0.75f);
            _bannerChip.GetComponentInChildren<TextMeshProUGUI>().text = chip;
            _bannerChip.color = hot ? SuitRed : InkDark;

            // Centre chip + text as one group.
            float w = Mathf.Max(_title.GetPreferredValues(title).x, _sub.GetPreferredValues(sub).x);
            float total = 64 + 20 + w;
            float left = -total * 0.5f;
            _bannerChip.rectTransform.At(0.5f, 1, 0, 0.5f, left, -46, 64, 64);
            _title.rectTransform.At(0.5f, 1, 0, 0.5f, left + 84, -32, w + 10, 52);
            _sub.rectTransform.At(0.5f, 1, 0, 0.5f, left + 86, -72, w + 10, 22);
            float pulse = hot ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
            _bannerChip.rectTransform.localScale = Vector3.one * pulse;
        }

        // ------------------------------------------------------------------ events

        void BuildEvents()
        {
            for (int i = 0; i < EventCards; i++)
            {
                _events[i] = MakeCard(Root, "Event" + i, 470, 78);
                _events[i].Rect.At(1, 1, 1, 1, -26, -(26 + i * 88), 470, 78);
                _eventGroups[i] = _events[i].Rect.gameObject.AddComponent<CanvasGroup>();
                _eventStripe[i] = Img(_events[i].Rect, "Stripe", UiSprites.RoundRect(4), SuitRed);
                _eventStripe[i].rectTransform.TL(10, 10, 8, 58);
                _eventHead[i] = UiKit.Text(_events[i].Rect, "Head", "", UI.Bowlby, 18, InkDark, TextAlignmentOptions.TopLeft);
                _eventHead[i].rectTransform.TL(30, 12, 430, 28);
                _eventDetail[i] = UiKit.Text(_events[i].Rect, "Detail", "", UI.Bowlby, 13, InkDark.A(0.65f), TextAlignmentOptions.TopLeft);
                _eventDetail[i].rectTransform.TL(30, 42, 430, 24);
                _eventDetail[i].characterSpacing = Sp(4);
            }
        }

        void TickEvents(HudModel m)
        {
            int count = m.InMatch ? m.Notes.Count : 0;
            for (int i = 0; i < EventCards; i++)
            {
                int index = count - 1 - i;
                HudNote note = index >= 0 ? m.Notes[index] : null;
                float a = note != null ? Life(note.Time, 9f, 0.2f, 1f) * (i == 0 ? 1f : 0.8f - 0.2f * i) : 0f;
                Show(_events[i].Rect, a > 0.01f);
                if (a <= 0.01f) continue;
                _eventGroups[i].alpha = a;
                _eventHead[i].text = note.Headline;
                _eventDetail[i].text = note.Detail;
                bool red = note.Kind == NoteKind.Bad || note.Kind == NoteKind.Big;
                _eventStripe[i].color = red ? SuitRed : note.Kind == NoteKind.Good ? ChipGold : InkDark;
                _eventHead[i].color = red ? SuitRed : InkDark;
                float slide = (1f - UiFx.Ease(Mathf.Clamp01((Time.unscaledTime - note.Time) / 0.3f))) * 500f;
                _events[i].Rect.anchoredPosition = new Vector2(-26 + (i == 0 ? slide : 0f), -(26 + i * 88));
            }
        }

        // ------------------------------------------------------------------ hand

        void BuildHand()
        {
            _hand = UiKit.Node(Root, "Hand").At(0, 0, 0, 0, 40, -22, 480, 260);
            Sprite[] icons = { HudSprites.Sword(), HudSprites.Shield(), HudSprites.Bolt() };
            string[] labels = { "ATTACK", "REPLACE\nSHIELD", "CHARGE" };
            for (int i = 0; i < 3; i++)
            {
                Card c = MakeCard(_hand, "Card" + i, 140, 196);
                c.Rect.pivot = new Vector2(0.5f, 0);
                c.Rect.anchorMin = c.Rect.anchorMax = new Vector2(0, 0);
                TextMeshProUGUI key = UiKit.Text(c.Rect, "Key", (i + 1).ToString(), UI.Bowlby, 30, SuitRed, TextAlignmentOptions.TopLeft);
                key.rectTransform.TL(12, 6, 40, 40);
                Img(c.Rect, "Icon", icons[i], InkDark).rectTransform.At(0.5f, 1, 0.5f, 0.5f, 0, -82, 54, 54);
                _handLabels[i] = UiKit.Text(c.Rect, "Label", labels[i], UI.Bowlby, 17, InkDark, TextAlignmentOptions.Top);
                _handLabels[i].rectTransform.TL(0, 120, 140, 50);
                _handLabels[i].lineSpacing = -18;
                _handCards[i] = c;
            }
        }

        void TickHand(HudModel m)
        {
            Show(_hand, m.MyTurn);
            if (!m.MyTurn) return;
            _handLabels[2].text = m.LocalHasCharge ? "CHARGED\nATTACK" : "CHARGE";
            for (int i = 0; i < 3; i++)
            {
                bool sel = (int)m.Pending == i + 1 || (i == 2 && m.Pending == HudPendingAction.ChargedAttack);
                _handSel[i] = Mathf.MoveTowards(_handSel[i], sel ? 1f : 0f, Time.unscaledDeltaTime * 7f);
                float s = UiFx.Ease(_handSel[i]);
                bool dimmed = m.Pending != HudPendingAction.None && !sel;
                Card c = _handCards[i];
                float x = 80 + i * 118, rot = (1 - i) * 8f;
                c.Rect.anchoredPosition = new Vector2(x, Mathf.Abs(1 - i) * -10 + 70 * s);
                c.Rect.localEulerAngles = new Vector3(0, 0, rot * (1 - s));
                c.Rect.localScale = Vector3.one * (1f + 0.08f * s);
                c.Fill.color = dimmed ? Face.A(0.55f) : Face;
                c.Edge.color = s > 0.5f ? SuitRed : FaceEdge;
                if (sel) c.Rect.SetAsLastSibling();
            }
        }

        // ------------------------------------------------------------------ player fan

        void BuildFan()
        {
            _fan = UiKit.Node(Root, "Fan").At(1, 0, 1, 0, -30, 22, 760, 262);
            _fanMeta = T(_fan, "Meta", "", 14, Dim, TextAlignmentOptions.MidlineRight);
            _fanMeta.rectTransform.At(1, 1, 1, 1, 0, 0, 700, 24);
            _fanMeta.characterSpacing = Sp(6);

            for (int i = 0; i < Max; i++)
            {
                RectTransform slot = UiKit.Node(_fan, "Player" + i);
                slot.anchorMin = slot.anchorMax = new Vector2(1, 0);
                slot.pivot = new Vector2(0.5f, 0);
                slot.sizeDelta = new Vector2(104, 146);

                _pcGlow[i] = Img(slot, "Glow", HudSprites.SoftDisc(), SuitRed.A(0));
                _pcGlow[i].rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 220, 260);
                _pcCharge[i] = Img(slot, "Charge", HudSprites.CardBack(), BackRed);
                _pcCharge[i].rectTransform.At(1, 1, 0.5f, 0.5f, -6, -10, 44, 60);
                _pcCharge[i].rectTransform.localEulerAngles = new Vector3(0, 0, -16);

                Card c = MakeCard(slot, "Card", 104, 146);
                c.Rect.Fill();
                _pc[i] = c;

                RectTransform face = UiKit.Node(c.Rect, "Face").Fill();
                _pcFace[i] = face;
                _pcCorner[i] = UiKit.Text(face, "Corner", "", UI.Bowlby, 20, InkDark, TextAlignmentOptions.TopLeft);
                _pcCorner[i].rectTransform.TL(9, 5, 90, 26);
                _pcCorner2[i] = UiKit.Text(face, "Corner2", "", UI.Bowlby, 13, InkDark, TextAlignmentOptions.TopLeft);
                _pcCorner2[i].rectTransform.At(1, 0, 0, 1, -9, 5, 60, 18);
                _pcCorner2[i].rectTransform.localEulerAngles = new Vector3(0, 0, 180);
                _pcBot[i] = UiKit.Text(face, "Bot", "BOT", UI.Bowlby, 10, InkDark.A(0.5f), TextAlignmentOptions.TopLeft);
                _pcBot[i].rectTransform.TL(10, 28, 60, 14);

                Img(face, "Heart", HudSprites.Heart(), SuitRed).rectTransform.At(0.5f, 1, 0.5f, 0.5f, -22, -60, 26, 26);
                _pcHp[i] = UiKit.Text(face, "Hp", "", UI.Bowlby, 32, InkDark, TextAlignmentOptions.MidlineLeft);
                _pcHp[i].rectTransform.At(0.5f, 1, 0, 0.5f, -6, -60, 70, 40);
                Img(face, "ShieldIcon", HudSprites.Shield(), InkDark.A(0.8f)).rectTransform.At(0.5f, 1, 0.5f, 0.5f, -22, -100, 20, 20);
                _pcShield[i] = UiKit.Text(face, "Shield", "", UI.Bowlby, 22, InkDark, TextAlignmentOptions.MidlineLeft);
                _pcShield[i].rectTransform.At(0.5f, 1, 0, 0.5f, -6, -100, 70, 30);

                _pcBack[i] = Img(c.Rect, "Back", HudSprites.CardBack(), BackRed);
                _pcBack[i].rectTransform.Fill();
                _pcOut[i] = UiKit.Text(c.Rect, "Out", "OUT", UI.Bowlby, 22, Face, TextAlignmentOptions.Center);
                _pcOut[i].rectTransform.Fill();

                _pcChip[i] = Chip(slot, "TurnChip", 38, SuitRed, "", Color.white);
                _pcChip[i].rectTransform.At(0.5f, 1, 0.5f, 0, 0, 6, 38, 38);
            }
        }

        void TickFan(HudModel m)
        {
            Show(_fan, m.InMatch);
            if (!m.InMatch) return;
            int n = Mathf.Min(m.PlayerCount, Max);
            const float step = 90f;
            _fanMeta.text = "DECK " + m.Snap.RemainingDrawCount + "   ·   " + DirectionText(m);
            _fanMeta.rectTransform.anchoredPosition = new Vector2(0, 0);

            for (int i = 0; i < Max; i++)
            {
                RectTransform slot = (RectTransform)_pc[i].Rect.parent;
                Show(slot, i < n);
                if (i >= n) continue;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                _pcLift[i] = Mathf.MoveTowards(_pcLift[i], turn ? 1f : 0f, Time.unscaledDeltaTime * 6f);
                float s = UiFx.Ease(_pcLift[i]);

                float x = -(n - 1 - i) * step - 60;
                float centre = (n - 1) * 0.5f;
                float rot = (centre - i) * 2.2f;
                slot.anchoredPosition = new Vector2(x, 18 * Mathf.Abs(i - centre) * -0.6f + 30 * s);
                slot.localEulerAngles = new Vector3(0, 0, rot * (1 - s));
                slot.localScale = Vector3.one * (1f + 0.07f * s);
                if (turn) slot.SetAsLastSibling();

                bool you = i == m.Local;
                _pcCorner[i].text = you ? "YOU" : "P" + (i + 1);
                _pcCorner2[i].text = _pcCorner[i].text;
                _pcCorner[i].color = _pcCorner2[i].color = you ? SuitRed : InkDark;
                Show(_pcBot[i], p.IsBot);
                _pcHp[i].text = p.Health.ToString();
                _pcShield[i].text = p.ShieldValue.ToString();

                Show(_pcFace[i], p.IsAlive);
                Show(_pcBack[i], !p.IsAlive);
                Show(_pcOut[i], !p.IsAlive);
                Show(_pcCharge[i], p.IsAlive && p.HasCharge);
                _pc[i].Edge.color = turn ? SuitRed : FaceEdge;
                _pcGlow[i].color = SuitRed.A(0.45f * s);
                Show(_pcChip[i], turn);
            }
        }

        // ------------------------------------------------------------------ tags

        void BuildTags()
        {
            for (int i = 0; i < Max; i++)
            {
                RectTransform tag = UiKit.Node(Root, "Tag" + i).At(0.5f, 0.5f, 0.5f, 1, 0, 0, 150, 36);
                _tags[i] = tag;
                Img(tag, "Fill", UiSprites.RoundRect(10), Face.A(0.92f)).rectTransform.Fill();
                _tagEdge[i] = Img(tag, "Edge", HudSprites.RoundOutline(10, 2), FaceEdge);
                _tagEdge[i].rectTransform.Fill();
                _tagText[i] = UiKit.Text(tag, "Text", "", UI.Bowlby, 16, InkDark, TextAlignmentOptions.MidlineRight);
                _tagText[i].rectTransform.At(0.5f, 0.5f, 1, 0.5f, -6, 1, 70, 30);
                Img(tag, "Heart", HudSprites.Heart(), SuitRed).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 10, 0, 16, 16);
                _tagHp[i] = UiKit.Text(tag, "Hp", "", UI.Bowlby, 16, InkDark, TextAlignmentOptions.MidlineLeft);
                _tagHp[i].rectTransform.At(0.5f, 0.5f, 0, 0.5f, 22, 1, 60, 30);
            }
        }

        void TickTags(HudModel m)
        {
            for (int i = 0; i < Max; i++)
            {
                bool on = m.InMatch && i < m.PlayerCount && i != m.Local && Project(i, out _);
                Show(_tags[i], on);
                if (!on) continue;
                Project(i, out Vector2 at);
                _tags[i].anchoredPosition = at;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                _tagText[i].text = "P" + (i + 1);
                _tagHp[i].text = p.IsAlive ? p.Health.ToString() : "OUT";
                _tagEdge[i].color = turn ? SuitRed : FaceEdge;
                _tags[i].localScale = Vector3.one * (turn ? 1.12f : 1f);
            }
        }

        // ------------------------------------------------------------------ beer + end

        RectTransform ChipButton(string name, string key, string label, float y, out TextMeshProUGUI text)
        {
            RectTransform r = UiKit.Node(Root, name).At(0, 1, 0, 1, 26, y, 300, 46);
            Image chip = Chip(r, "Chip", 46, InkDark, key, Face);
            chip.rectTransform.At(0, 0.5f, 0, 0.5f, 0, 0, 46, 46);
            text = T(r, "Label", label, 17, Face, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.TL(58, 0, 240, 46);
            text.characterSpacing = Sp(5);
            return r;
        }

        void BuildBeer()
        {
            Img(Root, "BeerPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.45f)).rectTransform.At(0, 1, 0.5f, 0.5f, 60, -60, 640, 260);
            _order = ChipButton("Order", "O", "ORDER A ROUND", -26, out _);
            _drink = ChipButton("Drink", "B", "HOLD TO DRINK", -80, out _drinkText);
        }

        void TickBeer(HudModel m)
        {
            Show(_order, m.OrderAvailable && !m.OrderOpen);
            Show(_drink, m.CanDrink || m.Drinking);
            _drinkText.text = m.Drinking ? "DRINKING…" : "HOLD TO DRINK";
        }

        void BuildEnd()
        {
            _end = UiKit.Node(Root, "End").At(0.5f, 0.5f, 0.5f, 0.5f, 0, 40, 380, 520);
            Img(_end, "Pool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.7f)).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 1200, 900);
            Card c = MakeCard(_end, "Card", 320, 440);
            c.Rect.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 30, 320, 440);
            c.Edge.color = SuitRed;
            Img(c.Rect, "Heart", HudSprites.Heart(), SuitRed).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 60, 120, 120);
            _endKicker = UiKit.Text(c.Rect, "Kicker", "", UI.Bowlby, 18, SuitRed, TextAlignmentOptions.Center);
            _endKicker.rectTransform.TL(0, 24, 320, 30);
            _endKicker.characterSpacing = Sp(10);
            _endTitle = UiKit.Text(c.Rect, "Title", "", UI.Bowlby, 40, InkDark, TextAlignmentOptions.Center);
            _endTitle.rectTransform.TL(10, 300, 300, 110);
            _endTitle.textWrappingMode = TextWrappingModes.Normal;
            _endTitle.lineSpacing = -20;
            _endRematch = UiKit.Node(_end, "Rematch").At(0.5f, 0, 0.5f, 0, 0, 0, 300, 50);
            Chip(_endRematch, "Chip", 46, SuitRed, "R", Color.white).rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, -70, 0, 46, 46);
            T(_endRematch, "Label", "REMATCH", 22, Face, TextAlignmentOptions.MidlineLeft).rectTransform.At(0.5f, 0.5f, 0, 0.5f, -36, 0, 200, 40);
        }

        void TickEnd(HudModel m)
        {
            Show(_end, m.InMatch && m.MatchOver);
            if (!m.MatchOver) return;
            bool won = m.Winner == m.Local;
            _endKicker.text = "THE LAST CARD";
            _endTitle.text = won ? "YOU WIN" : m.Name(m.Winner) + " WINS";
            Show(_endRematch, m.RematchVisible);
        }

        // ------------------------------------------------------------------ frame

        public override void Tick(HudModel m)
        {
            TickBanner(m);
            TickEvents(m);
            TickHand(m);
            TickFan(m);
            TickTags(m);
            TickBeer(m);
            TickEnd(m);
        }
    }
}
