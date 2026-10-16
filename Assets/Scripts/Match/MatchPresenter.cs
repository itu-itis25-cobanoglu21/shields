using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
using Shields.Game;
using Shields.Online;
using Shields.Players;
using Shields.Presentation;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// Mirrors the authoritative <see cref="ShieldsMatch"/> on the table. It redraws every
    /// card area from match state, keeps a status label above each player (turn marker,
    /// Health, elimination) and shows a resolved <see cref="TurnActionResult"/> as text
    /// and temporary revealed cards.
    ///
    /// It never computes a gameplay outcome. Everything it shows was already decided by
    /// the engine, and nothing it creates is read back into gameplay.
    ///
    /// A targeted action (Attack, Replace Shield, Charged Attack) is shown in order: its
    /// drawn card travels face down and is revealed when it lands. Until then the table
    /// keeps showing the public state from before the action, so neither a new Shield
    /// face, nor the action text, nor a Health change gives the card away early. The
    /// state is only held back, never changed: it is shown as received once the result
    /// plays out, or at once when anything newer arrives.
    /// </summary>
    public sealed class MatchPresenter
    {
        // Labels float between a player's head and their cards, so they stay inside the
        // seated resting view instead of drifting above the top of the screen.
        const float StatusHeight = 0.02f;
        const float ActionHeight = 0.2f;

        static readonly Color StatusColor = new Color(0.85f, 0.85f, 0.85f);
        static readonly Color TurnColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color EliminatedColor = new Color(0.9f, 0.3f, 0.3f);

        readonly TableLayoutController _table;
        readonly CardView _cardViewPrefab;
        readonly DeckView _deckView;
        readonly DiscardPileView _discardView;
        readonly PlayerActionPresenter _actions;
        readonly int _localPlayerIndex;
        readonly float _feedbackSeconds;

        readonly Dictionary<int, WorldLabel> _statusLabels = new Dictionary<int, WorldLabel>();
        readonly List<GameObject> _revealedCards = new List<GameObject>();

        CardView _highlightedShield;

        // What the table shows, and the snapshot shown before it.
        PublicMatchSnapshot _displayed;
        PublicMatchSnapshot _previous;

        // The snapshot a targeted action produced, waiting for its card to be revealed.
        PublicMatchSnapshot _held;
        bool _hasHeld;
        bool _holding;
        int _holdToken;

        // The host publishes an action's snapshot right before its result, and offline
        // does the same: a result arriving after a sync belongs to that sync.
        bool _syncedSinceResult;

        /// <param name="actions">Animates actions on the table. Optional: without it every
        /// result simply appears in place.</param>
        public MatchPresenter(TableLayoutController table, CardView cardViewPrefab, DeckView deckView,
            DiscardPileView discardView, PlayerActionPresenter actions, int localPlayerIndex, float feedbackSeconds)
        {
            _table = table;
            _cardViewPrefab = cardViewPrefab;
            _deckView = deckView;
            _discardView = discardView;
            _actions = actions;
            _localPlayerIndex = localPlayerIndex;
            _feedbackSeconds = feedbackSeconds;

            if (_actions != null) _actions.Bind(table, deckView, cardViewPrefab, discardView);
        }

        /// <summary>The public state currently on the table. Lags the latest one while an action is presented.</summary>
        public PublicMatchSnapshot DisplayedSnapshot { get { return _displayed; } }

        /// <summary>True while a targeted action is still being presented. Local input waits for it.</summary>
        public bool IsHoldingResult { get { return _holding; } }

        // ------------------------------------------------------------------ state sync

        /// <summary>
        /// Redraws cards, status labels and the draw and discard counts from public state.
        ///
        /// It takes a snapshot rather than the engine so the same presenter serves both
        /// modes: offline projects its local match into one of these, online receives one
        /// from the host. Either way the presenter only ever sees public information.
        /// </summary>
        public void Sync(PublicMatchSnapshot match)
        {
            if (!match.IsValid || _table == null) return;

            // Newer state overtakes an action still being presented: its state shows now.
            ReleaseHold();
            Show(match);
            _syncedSinceResult = true;
        }

        void Show(PublicMatchSnapshot match)
        {
            _previous = _displayed;
            _displayed = match;
            Draw(match, false);
        }

        /// <param name="restoring">Redrawing an earlier state while an action is presented:
        /// a Charge card that comes back was already seen stored, so it shows at once.</param>
        void Draw(PublicMatchSnapshot match, bool restoring)
        {
            _highlightedShield = null;

            for (int i = 0; i < match.PlayerCount; i++)
            {
                PublicPlayerState player = match.Players[i];

                // A newly stored Charge card is handed to the action presenter, which shows it
                // when its draw animation lands, or directly if no action explains it.
                PlayerTableArea area = _table.GetAreaForPlayer(i);
                if (area != null && area.ShowMatchState(player, _cardViewPrefab) && _actions != null)
                {
                    if (restoring) _actions.RestoreChargeCard(i, area.ChargeView);
                    else _actions.OnChargeCardCreated(i, area.ChargeView);
                }

                UpdateStatusLabel(match, player);
            }

            if (_deckView != null) _deckView.Refresh();

            // Only the count: the pile is face down and no discarded identity is public.
            if (_discardView != null) _discardView.ShowCount(match.DiscardCount);
        }

        void UpdateStatusLabel(PublicMatchSnapshot match, PublicPlayerState player)
        {
            // The local player cannot see above their own head; the HUD covers them.
            if (player.PlayerIndex == _localPlayerIndex) return;

            WorldLabel label = StatusLabelFor(player.PlayerIndex);
            if (label == null) return;

            string name = "PLAYER " + player.PlayerIndex + (player.IsBot ? " (BOT)" : "");

            if (!player.IsAlive)
            {
                label.Text = name + "\nELIMINATED";
                label.Color = EliminatedColor;
            }
            else if (player.PlayerIndex == match.CurrentPlayerIndex)
            {
                label.Text = "▶ " + name + " ◀\n" + player.Health + " HP";
                label.Color = TurnColor;
            }
            else
            {
                label.Text = name + "\n" + player.Health + " HP";
                label.Color = StatusColor;
            }
        }

        WorldLabel StatusLabelFor(int playerIndex)
        {
            WorldLabel label;
            if (_statusLabels.TryGetValue(playerIndex, out label) && label != null) return label;

            PlayerCharacter character = _table.GetPlayer(playerIndex);
            if (character == null) return null;

            label = WorldLabel.Create("StatusLabel", character.transform, LabelOffset(character, StatusHeight),
                StatusColor, 0.009f);
            _statusLabels[playerIndex] = label;
            return label;
        }

        // ------------------------------------------------------------------ results

        /// <summary>
        /// Shows a resolved action: white text above the actor and the revealed cards. A
        /// targeted action first plays its draw and throw, and shows all of that only when
        /// the card lands. <paramref name="onRevealed"/> runs at that same moment (the
        /// caller's HUD line), or at once for anything else.
        /// </summary>
        public void ShowResult(PublicActionResult result, System.Action onRevealed = null)
        {
            if (!result.Accepted || _table == null) return;

            bool synced = _syncedSinceResult;
            _syncedSinceResult = false;
            ReleaseHold();

            // Attack and Charged Attack: the actor's weapon locks onto the public target and
            // fires; only a published positive Damage makes it a hit, and its size.
            bool shot = result.ActionType == TurnActionType.Attack || result.IsChargedAttack;
            bool targeted = result.TargetIndex >= 0 && !result.StoredHiddenCharge &&
                (shot || result.ActionType == TurnActionType.ReplaceShield);

            if (targeted && _actions != null)
            {
                // Keep the table on the state from before this action until its card lands.
                _holding = true;
                int token = ++_holdToken;
                if (synced && _previous.IsValid)
                {
                    _held = _displayed;
                    _hasHeld = true;
                    _displayed = _previous;
                    Draw(_displayed, true);
                }

                _actions.PlayTargetedAction(result.ActorIndex, result.TargetIndex, DrawnCard(result),
                    result.ActionType == TurnActionType.ReplaceShield, shot, result.Damage, ShotSeed(result),
                    () =>
                    {
                        RevealResult(result);
                        if (onRevealed != null) onRevealed();
                    },
                    () => ReleaseHold(token));
                return;
            }

            // The flag is all a first Charge publishes, and all the animation needs.
            if (result.StoredHiddenCharge && _actions != null) _actions.PlayFirstCharge(result.ActorIndex);

            if (shot && result.TargetIndex >= 0 && _actions != null)
                _actions.PlayAttack(result.ActorIndex, result.TargetIndex, result.Damage, ShotSeed(result));

            RevealResult(result);
            if (onRevealed != null) onRevealed();
        }

        /// <summary>The public reveal of a result: the action text, and the cards it turned face up.</summary>
        void RevealResult(PublicActionResult result)
        {
            if (_table == null) return;

            PlayerCharacter actor = _table.GetPlayer(result.ActorIndex);
            if (actor != null)
            {
                WorldLabel label = WorldLabel.Create("ActionLabel", actor.transform, LabelOffset(actor, ActionHeight),
                    Color.white, 0.011f);
                label.Text = Headline(result) + (string.IsNullOrEmpty(Detail(result)) ? "" : "\n" + Detail(result));
                label.Expire(_feedbackSeconds);
            }

            // Attack cards and completed Charge cards are public once revealed. A new
            // Shield is already visible on the table, and a first Charge reveals nothing.
            if (result.ActionType != TurnActionType.ReplaceShield) ShowRevealedCards(result.RevealedCards);

            // Both Charge cards are revealed together: the stored one turns face up in its
            // slot as the second lands. It leaves with the Charge when the state catches up.
            if (result.IsChargedAttack && result.RevealedCardCount == 2)
            {
                PlayerTableArea area = _table.GetAreaForPlayer(result.ActorIndex);
                if (area != null && area.ChargeView != null)
                    area.ChargeView.Bind(result.RevealedCards[0].ToPlayingCard(), result.ActorIndex);
            }
        }

        /// <summary>The card this action drew now: the Attack card, the new Shield, or the second Charge card.</summary>
        static PlayingCard DrawnCard(PublicActionResult result)
        {
            if (result.ActionType == TurnActionType.ReplaceShield) return result.NewShieldCard.ToPlayingCard();
            if (result.RevealedCardCount == 0) return PlayingCard.Nothing;

            return result.RevealedCards[result.RevealedCardCount - 1].ToPlayingCard();
        }

        /// <summary>The presented action has played out (or was overtaken): show its state.</summary>
        void ReleaseHold(int token)
        {
            if (token == _holdToken) ReleaseHold();
        }

        void ReleaseHold()
        {
            if (!_holding) return;

            _holding = false;
            if (!_hasHeld) return;

            _hasHeld = false;
            Show(_held);
        }

        void ShowRevealedCards(IReadOnlyList<PublicCard> cards)
        {
            ClearRevealedCards();
            if (cards == null || cards.Count == 0 || _cardViewPrefab == null) return;

            Camera view = Camera.main;
            if (view == null) return;

            Vector3 center = _table.DeckAimPoint + Vector3.up * 0.2f;
            Vector3 toCamera = view.transform.position - center;
            Vector3 right = Vector3.Cross(Vector3.up, new Vector3(toCamera.x, 0f, toCamera.z)).normalized;

            // Stood upright and turned toward the local camera so the faces are readable.
            Quaternion facing = Quaternion.LookRotation(Vector3.up, toCamera);
            float spacing = 0.11f;

            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = Object.Instantiate(_cardViewPrefab);
                card.Bind(cards[i].ToPlayingCard(), -1);
                card.transform.SetPositionAndRotation(
                    center - right * ((i - (cards.Count - 1) * 0.5f) * spacing), facing);
                card.transform.localScale = Vector3.one * 1.5f;

                // Presentation only: never a crosshair target.
                foreach (Collider collider in card.GetComponentsInChildren<Collider>()) collider.enabled = false;

                Object.Destroy(card.gameObject, _feedbackSeconds);
                _revealedCards.Add(card.gameObject);
            }
        }

        void ClearRevealedCards()
        {
            for (int i = 0; i < _revealedCards.Count; i++)
                if (_revealedCards[i] != null) Object.Destroy(_revealedCards[i]);

            _revealedCards.Clear();
        }

        /// <summary>First line of the action text, e.g. "ATTACK → PLAYER 3".</summary>
        public static string Headline(PublicActionResult result)
        {
            switch (result.ActionType)
            {
                case TurnActionType.Attack:
                    return "ATTACK → PLAYER " + result.TargetIndex;
                case TurnActionType.ReplaceShield:
                    return "SHIELD → PLAYER " + result.TargetIndex;
                default:
                    return result.IsChargedAttack ? "CHARGED ATTACK → PLAYER " + result.TargetIndex : "CHARGE";
            }
        }

        /// <summary>
        /// Second line with the already resolved numbers, or empty. A first Charge has
        /// none, because its card is hidden.
        /// </summary>
        public static string Detail(PublicActionResult result)
        {
            if (result.StoredHiddenCharge) return string.Empty;

            if (result.ActionType == TurnActionType.ReplaceShield)
                return Face(result.OldShieldCard) + " → " + Face(result.NewShieldCard);

            string power;
            if (result.IsChargedAttack && result.RevealedCardCount == 2)
                power = Face(result.RevealedCards[0]) + " + " + Face(result.RevealedCards[1]) + " = " + result.AttackPower;
            else if (result.RevealedCardCount == 1)
                power = FaceWithValue(result.RevealedCards[0]);
            else
                power = result.AttackPower.ToString();

            string outcome = result.Damage > 0 ? result.Damage + " DAMAGE" : "BLOCKED";
            if (result.TargetChargeInterrupted) outcome += ", CHARGE BROKEN";
            if (result.TargetEliminated) outcome += ", ELIMINATED";

            return power + " vs Shield " + result.TargetShieldValue + " — " + outcome;
        }

        /// <summary>
        /// A seed for a shot's random visual details, built only from the public result,
        /// so every machine that receives the result plays the identical effect.
        /// </summary>
        public static int ShotSeed(PublicActionResult result)
        {
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + result.ActorIndex;
                seed = seed * 31 + result.TargetIndex;
                seed = seed * 31 + result.AttackPower;
                seed = seed * 31 + result.Damage;
                seed = seed * 31 + result.TargetHealthBefore;

                for (int i = 0; i < result.RevealedCardCount; i++)
                    seed = seed * 31 + result.RevealedCards[i].CardId;

                return seed;
            }
        }

        static string Face(PublicCard card)
        {
            return card.IsValid ? CardView.RankSymbol(card.Rank) : "?";
        }

        static string FaceWithValue(PublicCard card)
        {
            string face = Face(card);
            return face == card.Value.ToString() ? face : face + " = " + card.Value;
        }

        // ------------------------------------------------------------------ weapon

        /// <summary>Local Attack or Charged Attack targeting began: draw and aim with the view.</summary>
        public void BeginWeaponAim(int seat)
        {
            if (_actions != null) _actions.BeginWeaponAim(seat, seat == _localPlayerIndex);
        }

        /// <summary>Targeting ended without a confirmed action: holster.</summary>
        public void EndWeaponAim(int seat)
        {
            if (_actions != null) _actions.EndWeaponAim(seat);
        }

        /// <summary>The local player clicked a target; the host has not answered yet.</summary>
        public void AnticipateWeaponLock(int seat, int target)
        {
            if (_actions != null) _actions.LockWeapon(seat, target, false);
        }

        // ------------------------------------------------------------------ targeting pose

        /// <summary>Targeted action selected: lean in and rest the left hand on the Draw Pile.</summary>
        public void BeginTargeting(int seat)
        {
            if (_actions != null) _actions.BeginTargeting(seat);
        }

        /// <summary>Targeting ended without a chosen target: back to idle.</summary>
        public void EndTargeting(int seat)
        {
            if (_actions != null) _actions.EndTargeting(seat);
        }

        /// <summary>The local player clicked a target; the hand waits on the deck for the host.</summary>
        public void AnticipateTarget(int seat)
        {
            if (_actions != null) _actions.ChooseThrowTarget(seat);
        }

        // ------------------------------------------------------------------ targeting

        /// <summary>Highlights the target's Shield card, or clears it with a negative index.</summary>
        public void SetTargetHighlight(int playerIndex)
        {
            CardView shield = null;
            if (playerIndex >= 0 && _table != null)
            {
                PlayerTableArea area = _table.GetAreaForPlayer(playerIndex);
                if (area != null) shield = area.ShieldView;
            }

            if (shield == _highlightedShield) return;

            if (_highlightedShield != null) _highlightedShield.SetHighlighted(false);
            _highlightedShield = shield;
            if (_highlightedShield != null) _highlightedShield.SetHighlighted(true);
        }

        // ------------------------------------------------------------------ teardown

        public void Clear()
        {
            _holding = false;
            _hasHeld = false;
            _holdToken++;

            SetTargetHighlight(-1);
            ClearRevealedCards();

            if (_actions != null) _actions.Clear();

            if (_discardView != null) _discardView.Clear();

            foreach (WorldLabel label in _statusLabels.Values)
                if (label != null) Object.Destroy(label.gameObject);

            _statusLabels.Clear();
        }

        /// <summary>World offset from the character root to a point midway between head and cards.</summary>
        Vector3 LabelOffset(PlayerCharacter character, float height)
        {
            Transform head = character.HeadPivot != null ? character.HeadPivot : character.transform;
            PlayerTableArea area = _table.GetAreaForPlayer(character.PlayerIndex);
            Vector3 anchor = area != null ? Vector3.Lerp(area.transform.position, head.position, 0.5f) : head.position;
            return anchor - character.transform.position + Vector3.up * height;
        }
    }
}
