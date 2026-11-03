using Shields.Match;
using Shields.Online;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// HUD 3 — CARD TABLE. Everything speaks the game's own object, the playing card:
    /// players are a vertical stack of wide cards bottom right, one per seat, so a long name,
    /// Health and Shield always fit and are always visible (a stored Charge peeks out
    /// behind, face down once eliminated); the actions are a small hand bottom left; events
    /// arrive as cards top right; a dealer chip marks the turn and that card steps forward.
    ///
    /// The cards act out the table: on a shot the shooter's card steps out and raises a
    /// pistol at the target, whose card steps out under a reticle; a hit shakes the target
    /// card and knocks the lost Health off it; a Shield change makes both cards hop.
    /// Everything is triggered by public information at the moment the table shows it.
    /// The original row version is kept as HudCardsBackup (F4).
    /// </summary>
    public sealed class HudCards : HudConcept
    {
        public override string Code { get { return "Cards"; } }

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

        // Players: one wide card per seat, stacked top to bottom in seat order.
        const int Max = 8;
        const float CardW = 360, CardH = 62, Step = 70;
        RectTransform _fan;
        TextMeshProUGUI _fanMeta;
        readonly RectTransform[] _slot = new RectTransform[Max];
        readonly Card[] _pc = new Card[Max];
        readonly RectTransform[] _pcFace = new RectTransform[Max];
        readonly Image[] _pcBack = new Image[Max], _pcCharge = new Image[Max], _pcGlow = new Image[Max], _pcChip = new Image[Max], _pcHit = new Image[Max];
        readonly TextMeshProUGUI[] _pcName = new TextMeshProUGUI[Max], _pcBot = new TextMeshProUGUI[Max], _pcHp = new TextMeshProUGUI[Max],
            _pcShield = new TextMeshProUGUI[Max], _pcOut = new TextMeshProUGUI[Max], _pcLoss = new TextMeshProUGUI[Max];
        readonly Image[] _pcReticle = new Image[Max], _pcPistol = new Image[Max], _pcPistolBack = new Image[Max], _pcFlash = new Image[Max];
        readonly float[] _pcLift = new float[Max], _pcAim = new float[Max], _pcArm = new float[Max];

        // Per-seat reactions, in unscaled time.
        readonly float[] _hitAt = new float[Max], _jumpAt = new float[Max], _recoilAt = new float[Max];
        readonly int[] _lossShown = new int[Max];
        readonly int[] _lastHealth = new int[Max];
        int _healthGeneration = -1;

        // The shot being acted out, if any. Started when the table starts the action (only
        // actor and target are public then); revealed when its card is turned up.
        struct Shot
        {
            public bool Active, Revealed, Fired;
            public int Actor, Target;
            public float StartedAt, EndAt;  // unscaled
            public float RevealedAt;        // scaled (table time)
            public uint RevealRevision;
        }
        Shot _shot;

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
            Img(Root, "FanPool", HudSprites.SoftDisc(), new Color(0, 0, 0, 0.55f)).rectTransform.At(1, 0, 0.5f, 0.5f, -200, 250, 820, 900);
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

        // ------------------------------------------------------------------ player stack

        void BuildFan()
        {
            _fan = UiKit.Node(Root, "Stack").At(1, 0, 1, 0, -40, 26, CardW, Max * Step);
            _fanMeta = T(_fan, "Meta", "", 14, Dim, TextAlignmentOptions.MidlineRight);
            _fanMeta.characterSpacing = Sp(6);

            for (int i = 0; i < Max; i++)
            {
                RectTransform slot = UiKit.Node(_fan, "Player" + i);
                slot.anchorMin = slot.anchorMax = new Vector2(1, 0);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(CardW, CardH);
                _slot[i] = slot;

                _pcGlow[i] = Img(slot, "Glow", HudSprites.SoftDisc(), SuitRed.A(0));
                _pcGlow[i].rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, CardW + 160, CardH + 120);

                // A stored Charge: a face-down card tucked behind, peeking out on the right.
                _pcCharge[i] = Img(slot, "Charge", HudSprites.CardBack(), BackRed);
                _pcCharge[i].rectTransform.At(1, 0.5f, 0.5f, 0.5f, 8, 2, 32, 46);
                _pcCharge[i].rectTransform.localEulerAngles = new Vector3(0, 0, -12);

                Card c = MakeCard(slot, "Card", CardW, CardH);
                c.Rect.Fill();
                _pc[i] = c;

                RectTransform face = UiKit.Node(c.Rect, "Face").Fill();
                _pcFace[i] = face;
                _pcName[i] = UiKit.Text(face, "Name", "", UI.Bowlby, 21, InkDark, TextAlignmentOptions.MidlineLeft);
                // Ellipsis hides a line that does not fit the box height, so the box is roomy.
                _pcName[i].rectTransform.TL(16, -2, 186, 46);
                _pcName[i].overflowMode = TextOverflowModes.Ellipsis;
                _pcBot[i] = UiKit.Text(face, "Bot", "BOT", UI.Bowlby, 11, InkDark.A(0.5f), TextAlignmentOptions.TopLeft);
                _pcBot[i].rectTransform.TL(17, 40, 80, 16);
                _pcBot[i].characterSpacing = Sp(6);

                Img(face, "Heart", HudSprites.Heart(), SuitRed).rectTransform.At(0, 0.5f, 0.5f, 0.5f, 222, 0, 24, 24);
                _pcHp[i] = UiKit.Text(face, "Hp", "", UI.Bowlby, 30, InkDark, TextAlignmentOptions.Midline);
                _pcHp[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 262, 1, 50, 40);
                Img(face, "ShieldIcon", HudSprites.Shield(), InkDark.A(0.8f)).rectTransform.At(0, 0.5f, 0.5f, 0.5f, 304, 0, 20, 20);
                _pcShield[i] = UiKit.Text(face, "Shield", "", UI.Bowlby, 22, InkDark, TextAlignmentOptions.Midline);
                _pcShield[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 334, 1, 40, 32);

                _pcBack[i] = Img(c.Rect, "Back", UiSprites.RoundRect(12), BackRed);
                _pcBack[i].rectTransform.Fill();
                _pcOut[i] = UiKit.Text(c.Rect, "Out", "", UI.Bowlby, 18, Face, TextAlignmentOptions.Center);
                _pcOut[i].rectTransform.Fill(12, 0, 12, 0);
                _pcOut[i].characterSpacing = Sp(5);
                _pcOut[i].overflowMode = TextOverflowModes.Ellipsis;

                // Hit flash over the whole card.
                _pcHit[i] = Img(c.Rect, "HitFlash", UiSprites.RoundRect(12), SuitRed.A(0));
                _pcHit[i].rectTransform.Fill();

                _pcChip[i] = Chip(slot, "TurnChip", 34, SuitRed, "", Color.white);
                _pcChip[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, -26, 0, 34, 34);

                // The pistol raised by a shooter, left of their card; the reticle over a target's Health.
                // A card-coloured token behind it keeps the dark pistol readable over the room.
                _pcPistolBack[i] = Img(slot, "PistolBack", HudSprites.Circle(), Face);
                _pcPistolBack[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, -40, 0, 60, 60);
                Img(_pcPistolBack[i].transform, "Edge", HudSprites.RingSprite(4), FaceEdge).rectTransform.Fill();
                _pcPistol[i] = Img(_pcPistolBack[i].transform, "Pistol", HudSprites.PistolIcon(), InkDark);
                _pcPistol[i].rectTransform.At(0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 46, 46);
                _pcFlash[i] = Img(_pcPistol[i].transform, "Flash", HudSprites.Circle(), ChipGold.A(0));
                _pcFlash[i].rectTransform.At(0.94f, 0.66f, 0.5f, 0.5f, 4, 0, 22, 22);

                _pcReticle[i] = Img(slot, "Reticle", HudSprites.Reticle(), SuitRed);
                _pcReticle[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 262, 0, 74, 74);

                _pcLoss[i] = UiKit.Text(slot, "Loss", "", UI.Bowlby, 26, SuitRed, TextAlignmentOptions.Center);
                _pcLoss[i].rectTransform.At(0, 0.5f, 0.5f, 0.5f, 262, 30, 90, 36);
            }
        }

        // ------------------------------------------------------------------ shot / reactions

        static bool IsShot(PublicActionResult r)
        {
            return r.TargetIndex >= 0 && r.TargetIndex != r.ActorIndex && !r.StoredHiddenCharge &&
                (r.ActionType == Shields.Game.TurnActionType.Attack || r.IsChargedAttack);
        }

        public override void ActionStarted(PublicActionResult r, int local)
        {
            // Only who shoots whom is used here: it is already public (the weapon locks on
            // the table at this moment). Cards and damage wait for the reveal.
            if (!IsShot(r)) return;
            _shot = new Shot { Active = true, Actor = r.ActorIndex, Target = r.TargetIndex, StartedAt = Time.unscaledTime, EndAt = Time.unscaledTime + 30f };
        }

        public override void ActionRevealed(PublicActionResult r, int local)
        {
            float now = Time.unscaledTime;
            if (r.ActionType == Shields.Game.TurnActionType.ReplaceShield)
            {
                Jump(r.ActorIndex, now);
                if (r.TargetIndex >= 0 && r.TargetIndex != r.ActorIndex) Jump(r.TargetIndex, now);
                return;
            }

            if (!IsShot(r)) return;
            if (!_shot.Active || _shot.Actor != r.ActorIndex || _shot.Target != r.TargetIndex)
                _shot = new Shot { Active = true, Actor = r.ActorIndex, Target = r.TargetIndex, StartedAt = now };

            _shot.Revealed = true;
            _shot.RevealedAt = Time.time;
            _shot.RevealRevision = HudOverride.Latest.Snapshot.Revision;
            _shot.EndAt = now + 30f;
        }

        void Jump(int seat, float now) { if (seat >= 0 && seat < Max) _jumpAt[seat] = now; }

        /// <summary>
        /// Follows the shown state: a seat whose Health drops on the table takes the hit now,
        /// which is when the table itself shows the damage. Also times the shot: it fires on
        /// that hit, or shortly after the reveal for a blocked shot, and lowers afterwards.
        /// </summary>
        void TrackHealth(HudModel m, int n)
        {
            float now = Time.unscaledTime;
            bool fresh = m.Snap.Generation != _healthGeneration;
            _healthGeneration = m.Snap.Generation;
            if (fresh) { _shot = default; for (int i = 0; i < Max; i++) { _hitAt[i] = _jumpAt[i] = _recoilAt[i] = -100f; } }

            for (int i = 0; i < n; i++)
            {
                int hp = m.Player(i).Health;
                if (!fresh && hp < _lastHealth[i])
                {
                    _hitAt[i] = now;
                    _lossShown[i] = _lastHealth[i] - hp;
                    if (_shot.Active && _shot.Target == i && !_shot.Fired)
                    {
                        _shot.Fired = true;
                        _recoilAt[_shot.Actor] = now;
                        _shot.EndAt = now + 0.9f;
                    }
                }
                _lastHealth[i] = hp;
            }

            if (!_shot.Active) return;

            // A blocked shot changes no Health: it fires once the table has moved on from
            // the revealed card (the presenter releases its held state after the shot).
            // The cap is in table time (scaled), like the presentation itself.
            if (_shot.Revealed && !_shot.Fired)
            {
                bool tableMovedOn = m.Snap.Revision != _shot.RevealRevision;
                if (tableMovedOn || Time.time - _shot.RevealedAt > 5f)
                {
                    _shot.Fired = true;
                    _recoilAt[_shot.Actor] = now;
                    _shot.EndAt = now + 0.8f;
                }
            }

            if (now > _shot.EndAt || m.MatchOver && _shot.Fired) _shot.Active = false;
        }

        void TickFan(HudModel m)
        {
            Show(_fan, m.InMatch);
            if (!m.InMatch) return;
            int n = Mathf.Min(m.PlayerCount, Max);
            float now = Time.unscaledTime;
            TrackHealth(m, n);

            _fanMeta.text = "DECK " + m.Snap.RemainingDrawCount + "   ·   " + DirectionText(m);
            _fanMeta.rectTransform.At(1, 0, 1, 0, 0, n * Step + 4, 700, 24);

            int front = -1;
            for (int i = 0; i < Max; i++)
            {
                RectTransform slot = _slot[i];
                Show(slot, i < n);
                if (i >= n) continue;
                PublicPlayerState p = m.Player(i);
                bool turn = !m.MatchOver && i == m.Current && p.IsAlive;
                bool armed = _shot.Active && _shot.Actor == i;
                bool aimed = _shot.Active && _shot.Target == i;

                float dt = Time.unscaledDeltaTime;
                _pcLift[i] = Mathf.MoveTowards(_pcLift[i], turn ? 1f : 0f, dt * 6f);
                _pcArm[i] = Mathf.MoveTowards(_pcArm[i], armed ? 1f : 0f, dt * 5f);
                _pcAim[i] = Mathf.MoveTowards(_pcAim[i], aimed ? 1f : 0f, dt * 6f);
                float s = UiFx.Ease(_pcLift[i]), arm = UiFx.Ease(_pcArm[i]), aim = UiFx.Ease(_pcAim[i]);

                // Reactions.
                float hitAge = now - _hitAt[i];
                float hit = hitAge >= 0f && hitAge < 0.55f ? 1f - hitAge / 0.55f : 0f;
                float shake = hit > 0f ? Mathf.Sin(hitAge * 70f) * 9f * hit * hit : 0f;
                float jumpAge = now - _jumpAt[i];
                float jump = jumpAge >= 0f && jumpAge < 0.42f ? Mathf.Sin(jumpAge / 0.42f * Mathf.PI) * 16f : 0f;

                // Seat 0 at the top. The turn card steps forward (left, towards the table)
                // and up a little; shooter and target step out too.
                float step = Mathf.Max(s, Mathf.Max(arm, aim));
                float x = -CardW * 0.5f - 34f * step + shake;
                float y = CardH * 0.5f + (n - 1 - i) * Step + 4f * s + jump;
                slot.anchoredPosition = new Vector2(x, y);
                slot.localScale = Vector3.one * (1f + 0.05f * step);
                if (turn && front < 0) front = i;

                bool you = i == m.Local;
                _pcName[i].text = m.Name(i);
                _pcName[i].color = you ? SuitRed : InkDark;
                Show(_pcBot[i], p.IsBot && p.IsAlive);
                _pcHp[i].text = p.Health.ToString();
                _pcShield[i].text = p.ShieldValue.ToString();

                Show(_pcFace[i], p.IsAlive);
                Show(_pcBack[i], !p.IsAlive);
                Show(_pcOut[i], !p.IsAlive);
                if (!p.IsAlive) _pcOut[i].text = m.Name(i) + "   ·   OUT";
                Show(_pcCharge[i], p.IsAlive && p.HasCharge);
                _pc[i].Edge.color = turn || aim > 0.5f ? SuitRed : FaceEdge;
                _pcGlow[i].color = SuitRed.A(0.45f * Mathf.Max(s, 0.8f * aim));
                Show(_pcChip[i], turn && arm < 0.5f);

                // Hit: red flash over the card, the Health number kicks, the loss floats off.
                _pcHit[i].color = SuitRed.A(0.45f * hit);
                float punch = hit > 0f ? 1f + 0.45f * hit * hit : 1f;
                _pcHp[i].rectTransform.localScale = Vector3.one * punch;
                _pcHp[i].color = UiKit.Lerp(InkDark, SuitRed, hit);
                float lossAge = now - _hitAt[i];
                bool loss = lossAge >= 0f && lossAge < 1.1f && _lossShown[i] > 0;
                Show(_pcLoss[i], loss);
                if (loss)
                {
                    float k = lossAge / 1.1f;
                    _pcLoss[i].text = "-" + _lossShown[i];
                    _pcLoss[i].rectTransform.anchoredPosition = new Vector2(262, 30 + 34 * UiFx.Ease(k));
                    _pcLoss[i].color = SuitRed.A(1f - k * k);
                }

                // Reticle: closes in from wide and slowly turns while the shot is on.
                Show(_pcReticle[i], aim > 0.01f);
                if (aim > 0.01f)
                {
                    _pcReticle[i].color = SuitRed.A(aim);
                    _pcReticle[i].rectTransform.localScale = Vector3.one * (1f + 0.8f * (1f - aim));
                    _pcReticle[i].rectTransform.localEulerAngles = new Vector3(0, 0, (now - _shot.StartedAt) * 40f);
                }

                // Pistol: rises from pointing at the floor to pointing at the target's card.
                RectTransform holder = _pcPistolBack[i].rectTransform;
                Show(holder, arm > 0.01f);
                if (arm > 0.01f)
                {
                    int target = _shot.Active ? _shot.Target : -1;
                    float aimAngle = 0f;
                    if (target >= 0 && target < n)
                    {
                        // From the pistol (left of this card) to the reticle on the target's card.
                        float dy = (i - target) * Step;
                        float dx = 40f + 262f;
                        aimAngle = Mathf.Clamp(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, -70f, 70f);
                    }
                    float recoilAge = now - _recoilAt[i];
                    float recoil = recoilAge >= 0f && recoilAge < 0.3f ? 1f - recoilAge / 0.3f : 0f;
                    holder.localScale = Vector3.one * (0.6f + 0.4f * arm);
                    _pcPistolBack[i].color = Face.A(arm);
                    _pcPistol[i].color = InkDark.A(arm);
                    _pcPistol[i].rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(-80f, aimAngle, arm) + 22f * recoil * recoil);
                    _pcFlash[i].color = ChipGold.A(recoil > 0.6f ? 1f : 0f);
                    _pcFlash[i].rectTransform.localScale = Vector3.one * (0.6f + recoil);
                }
            }

            // Draw order: the turn card, then the target, then the shooter in front.
            if (front >= 0) _slot[front].SetAsLastSibling();
            if (_shot.Active && _shot.Target < n) _slot[_shot.Target].SetAsLastSibling();
            if (_shot.Active && _shot.Actor < n) _slot[_shot.Actor].SetAsLastSibling();
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
