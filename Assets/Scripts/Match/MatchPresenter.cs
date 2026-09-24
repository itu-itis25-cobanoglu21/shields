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
        readonly int _localPlayerIndex;
        readonly float _feedbackSeconds;

        readonly Dictionary<int, WorldLabel> _statusLabels = new Dictionary<int, WorldLabel>();
        readonly List<GameObject> _revealedCards = new List<GameObject>();

        CardView _highlightedShield;

        public MatchPresenter(TableLayoutController table, CardView cardViewPrefab, DeckView deckView,
            int localPlayerIndex, float feedbackSeconds)
        {
            _table = table;
            _cardViewPrefab = cardViewPrefab;
            _deckView = deckView;
            _localPlayerIndex = localPlayerIndex;
            _feedbackSeconds = feedbackSeconds;
        }

        // ------------------------------------------------------------------ state sync

        /// <summary>
        /// Redraws cards, status labels and the deck count from public state.
        ///
        /// It takes a snapshot rather than the engine so the same presenter serves both
        /// modes: offline projects its local match into one of these, online receives one
        /// from the host. Either way the presenter only ever sees public information.
        /// </summary>
        public void Sync(PublicMatchSnapshot match)
        {
            if (!match.IsValid || _table == null) return;

            _highlightedShield = null;

            for (int i = 0; i < match.PlayerCount; i++)
            {
                PublicPlayerState player = match.Players[i];

                PlayerTableArea area = _table.GetAreaForPlayer(i);
                if (area != null) area.ShowMatchState(player, _cardViewPrefab);

                UpdateStatusLabel(match, player);
            }

            if (_deckView != null) _deckView.Refresh();
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

        /// <summary>Shows a resolved action: white text above the actor and the revealed cards.</summary>
        public void ShowResult(PublicActionResult result)
        {
            if (!result.Accepted || _table == null) return;

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

        static string Face(PublicCard card)
        {
            return card.IsValid ? CardView.RankSymbol(card.Rank) : "?";
        }

        static string FaceWithValue(PublicCard card)
        {
            string face = Face(card);
            return face == card.Value.ToString() ? face : face + " = " + card.Value;
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
            SetTargetHighlight(-1);
            ClearRevealedCards();

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
