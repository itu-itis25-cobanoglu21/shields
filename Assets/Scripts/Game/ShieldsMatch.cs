using System;
using System.Collections.Generic;
using Shields.Cards;

namespace Shields.Game
{
    /// <summary>
    /// The authoritative Shields turn engine. It takes over from a finished
    /// <see cref="MatchSetup"/>, keeping the same <see cref="CardDeck"/> and the cards the
    /// players chose, and resolves Attack, Replace Shield and Charge until one player is
    /// left alive.
    ///
    /// Plain C# with no Unity dependency. Humans, bots and a future network host all submit
    /// the same <see cref="TurnActionRequest"/> through <see cref="Submit"/>; there is no
    /// separate rule path for any of them.
    /// </summary>
    public sealed class ShieldsMatch
    {
        /// <summary>Player index meaning "nobody", e.g. no winner yet.</summary>
        public const int NoPlayer = -1;

        const int MaxCardValue = 13;

        /// <summary>A Health slot showing a Virtual Health Marker, waiting for a real card.</summary>
        struct WaitingSlot
        {
            public int PlayerIndex;
            public HealthSlotSide Side;
        }

        readonly CardDeck _deck;
        readonly MatchPlayerState[] _players;

        // One FIFO queue per card value, indexed by value. Index 0 is unused.
        readonly List<WaitingSlot>[] _waiting;

        /// <summary>
        /// Starts turn play from a completed setup. The starting player and direction are
        /// random; under a config seed they are reproducible, for debugging and tests only.
        /// </summary>
        public ShieldsMatch(MatchSetup setup)
            : this(setup, CreateRandom(setup))
        {
        }

        /// <summary>Starts turn play from a completed setup using the given random source.</summary>
        public ShieldsMatch(MatchSetup setup, Random random)
            : this(RequireCompleteSetup(setup).Deck, setup.Players, random)
        {
        }

        /// <summary>
        /// Starts turn play from explicit players and an explicit deck, picking a random
        /// living starting player and a random direction.
        /// </summary>
        public ShieldsMatch(CardDeck deck, IReadOnlyList<PlayerSetupState> players, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _players = CreatePlayers(players);
            _waiting = CreateWaitingQueues();

            // Every player is alive at the start, so any seat is a living starting player.
            StartingPlayerIndex = random.Next(_players.Length);
            Direction = random.Next(2) == 0 ? TurnDirection.Clockwise : TurnDirection.CounterClockwise;
            CurrentPlayerIndex = StartingPlayerIndex;
            WinnerIndex = NoPlayer;
        }

        /// <summary>Starts turn play with a fixed starting player and direction.</summary>
        public ShieldsMatch(CardDeck deck, IReadOnlyList<PlayerSetupState> players,
            int startingPlayerIndex, TurnDirection direction)
        {
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _players = CreatePlayers(players);
            _waiting = CreateWaitingQueues();

            if (startingPlayerIndex < 0 || startingPlayerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(startingPlayerIndex));

            StartingPlayerIndex = startingPlayerIndex;
            Direction = direction;
            CurrentPlayerIndex = startingPlayerIndex;
            WinnerIndex = NoPlayer;
        }

        /// <summary>The same draw and discard piles the setup dealt from.</summary>
        public CardDeck Deck { get { return _deck; } }

        public IReadOnlyList<MatchPlayerState> Players { get { return _players; } }

        public int PlayerCount { get { return _players.Length; } }

        public int StartingPlayerIndex { get; }

        public TurnDirection Direction { get; }

        /// <summary>Whose turn it is, or <see cref="NoPlayer"/> once the match is over.</summary>
        public int CurrentPlayerIndex { get; private set; }

        public bool IsMatchOver { get; private set; }

        /// <summary>The last living player once the match is over, otherwise <see cref="NoPlayer"/>.</summary>
        public int WinnerIndex { get; private set; }

        public int LivingPlayerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _players.Length; i++)
                    if (_players[i].IsAlive) count++;

                return count;
            }
        }

        public MatchPlayerState GetPlayer(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));

            return _players[playerIndex];
        }

        /// <summary>How many Virtual Health Markers are waiting for a real card of this value.</summary>
        public int WaitingMarkerCount(int value)
        {
            if (value < 1 || value > MaxCardValue) return 0;
            return _waiting[value].Count;
        }

        /// <summary>
        /// Validates a committed request and, only if it is legal, draws and resolves it.
        /// A rejected request draws nothing, changes nothing, and does not pass the turn.
        /// </summary>
        public TurnActionResult Submit(TurnActionRequest request)
        {
            TurnActionResult result = new TurnActionResult(request);

            result.Rejection = Validate(request);
            if (!result.Accepted) return result;

            // The action and target are committed; only now is a card drawn.
            PlayingCard drawn;
            if (!_deck.TryDraw(out drawn))
            {
                result.Rejection = ActionRejection.DeckExhausted;
                return result;
            }

            MatchPlayerState actor = _players[request.PlayerIndex];

            switch (request.ActionType)
            {
                case TurnActionType.Attack:
                    ResolveAttack(result, _players[request.TargetIndex], drawn);
                    break;

                case TurnActionType.ReplaceShield:
                    ResolveReplaceShield(result, _players[request.TargetIndex], drawn);
                    break;

                case TurnActionType.Charge:
                    if (actor.HasCharge) ResolveChargedAttack(result, actor, _players[request.TargetIndex], drawn);
                    else ResolveFirstCharge(result, actor, drawn);
                    break;
            }

            if (LivingPlayerCount == 1)
            {
                IsMatchOver = true;
                WinnerIndex = FirstLivingPlayer();
                CurrentPlayerIndex = NoPlayer;
            }
            else
            {
                CurrentPlayerIndex = NextLivingPlayer(CurrentPlayerIndex);
            }

            result.MatchOver = IsMatchOver;
            result.WinnerIndex = WinnerIndex;
            result.NextPlayerIndex = CurrentPlayerIndex;
            return result;
        }

        /// <summary>Multi line summary of the public match state, handy for logging.</summary>
        public string DescribeState()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("{0} | draw {1}, discard {2}",
                IsMatchOver ? "Winner: player " + WinnerIndex : "Turn: player " + CurrentPlayerIndex + ", " + Direction,
                _deck.DrawCount, _deck.DiscardCount);

            for (int i = 0; i < _players.Length; i++)
                sb.Append("\n  ").Append(_players[i]);

            return sb.ToString();
        }

        // ------------------------------------------------------------------ validation

        ActionRejection Validate(TurnActionRequest request)
        {
            if (IsMatchOver) return ActionRejection.MatchOver;

            if (request.PlayerIndex < 0 || request.PlayerIndex >= _players.Length)
                return ActionRejection.InvalidPlayer;

            MatchPlayerState actor = _players[request.PlayerIndex];
            if (!actor.IsAlive) return ActionRejection.PlayerEliminated;
            if (request.PlayerIndex != CurrentPlayerIndex) return ActionRejection.NotYourTurn;

            switch (request.ActionType)
            {
                case TurnActionType.Attack:
                    return ValidateTarget(request, false);

                case TurnActionType.ReplaceShield:
                    return ValidateTarget(request, true);

                case TurnActionType.Charge:
                    // A first Charge needs no target. Completing one is an attack.
                    return actor.HasCharge ? ValidateTarget(request, false) : ActionRejection.None;

                default:
                    return ActionRejection.UnknownAction;
            }
        }

        ActionRejection ValidateTarget(TurnActionRequest request, bool allowSelf)
        {
            if (request.TargetIndex == TurnActionRequest.NoTarget) return ActionRejection.TargetRequired;

            if (request.TargetIndex < 0 || request.TargetIndex >= _players.Length)
                return ActionRejection.InvalidTarget;

            if (!allowSelf && request.TargetIndex == request.PlayerIndex)
                return ActionRejection.SelfTargetNotAllowed;

            if (!_players[request.TargetIndex].IsAlive) return ActionRejection.TargetEliminated;

            return ActionRejection.None;
        }

        // ------------------------------------------------------------------ actions

        void ResolveAttack(TurnActionResult result, MatchPlayerState target, PlayingCard attackCard)
        {
            result.TargetIndex = target.PlayerIndex;
            result.RevealedCards = new[] { attackCard };

            ResolveHit(result, target, attackCard.Value, result.RevealedCards);
        }

        void ResolveReplaceShield(TurnActionResult result, MatchPlayerState target, PlayingCard newShield)
        {
            PlayingCard oldShield = target.ShieldCard;

            // Mandatory, whether the new Shield is stronger or weaker. Charge is untouched.
            target.ShieldCard = newShield;
            DiscardCard(oldShield);

            result.TargetIndex = target.PlayerIndex;
            result.RevealedCards = new[] { newShield };
            result.OldShieldCard = oldShield;
            result.NewShieldCard = newShield;
            result.TargetShieldValue = newShield.Value;
            result.TargetHealthBefore = target.Health;
            result.TargetHealthAfter = target.Health;
        }

        void ResolveFirstCharge(TurnActionResult result, MatchPlayerState actor, PlayingCard chargeCard)
        {
            // Stored face-down. The result deliberately carries no trace of the card.
            actor.ChargeCard = chargeCard;
            result.StoredHiddenCharge = true;
        }

        void ResolveChargedAttack(TurnActionResult result, MatchPlayerState actor, MatchPlayerState target, PlayingCard secondCard)
        {
            PlayingCard firstCard = actor.ChargeCard;
            actor.ChargeCard = PlayingCard.Nothing;

            result.IsChargedAttack = true;
            result.TargetIndex = target.PlayerIndex;
            result.RevealedCards = new[] { firstCard, secondCard };

            // Both Charge cards are discarded whether the attack succeeds or fails.
            ResolveHit(result, target, firstCard.Value + secondCard.Value, result.RevealedCards);
        }

        /// <summary>
        /// Shared by normal and Charged Attacks: applies the Shield once to the attack power,
        /// discards the resolved cards, and on positive damage interrupts the target's
        /// Charge and changes their Health.
        /// </summary>
        void ResolveHit(TurnActionResult result, MatchPlayerState target, int attackPower,
            IReadOnlyList<PlayingCard> resolvedCards)
        {
            int shield = target.ShieldValue;
            int damage = attackPower > shield ? attackPower - shield : 0;

            result.AttackPower = attackPower;
            result.TargetShieldValue = shield;
            result.Damage = damage;
            result.TargetHealthBefore = target.Health;

            // Confirmed rule: resolved cards reach the discard pile before the target's
            // Health is redistributed, so the Health lookup may take them from there.
            for (int i = 0; i < resolvedCards.Count; i++) DiscardCard(resolvedCards[i]);

            if (damage > 0)
            {
                if (target.HasCharge)
                {
                    PlayingCard interrupted = target.ChargeCard;
                    target.ChargeCard = PlayingCard.Nothing;
                    DiscardCard(interrupted);
                    result.TargetChargeInterrupted = true;
                }

                target.Health -= damage;

                if (target.Health <= 0)
                {
                    Eliminate(target);
                    result.TargetEliminated = true;
                }
                else
                {
                    RedistributeHealth(target);
                }
            }

            result.TargetHealthAfter = target.Health;
        }

        // ------------------------------------------------------------------ Health

        /// <summary>
        /// Re-represents the player's new numeric Health. The lower physical card is kept
        /// if it still fits (equal cards: the RIGHT one is kept); physical cards are always
        /// preferred over a Virtual marker. The removed Health is discarded and the missing
        /// value is looked up. If no physical card can stay, everything is removed and the
        /// Health is shown as one value.
        /// </summary>
        void RedistributeHealth(MatchPlayerState player)
        {
            HealthSlot left = player.LeftHealth;
            HealthSlot right = player.RightHealth;

            bool hasKeep = true;
            HealthSlotSide keep;

            if (left.IsCard && right.IsCard) keep = left.Value < right.Value ? HealthSlotSide.Left : HealthSlotSide.Right;
            else if (left.IsCard) keep = HealthSlotSide.Left;
            else if (right.IsCard) keep = HealthSlotSide.Right;
            else { keep = HealthSlotSide.Left; hasKeep = false; }

            if (hasKeep && player.GetHealthSlot(keep).Value > player.Health) hasKeep = false;

            // Clear every slot that is not kept before discarding anything, so this
            // player's own markers are out of the queues when the cards go out.
            List<PlayingCard> released = new List<PlayingCard>(2);
            if (!hasKeep || keep != HealthSlotSide.Left) ReleaseSlot(player, HealthSlotSide.Left, released);
            if (!hasKeep || keep != HealthSlotSide.Right) ReleaseSlot(player, HealthSlotSide.Right, released);

            for (int i = 0; i < released.Count; i++) DiscardCard(released[i]);

            int kept = hasKeep ? player.GetHealthSlot(keep).Value : 0;
            int missing = player.Health - kept;

            if (missing > 0)
            {
                HealthSlotSide fill = hasKeep && keep == HealthSlotSide.Left ? HealthSlotSide.Right : HealthSlotSide.Left;
                FillHealthSlot(player, fill, missing);
            }
        }

        /// <summary>
        /// Puts a card of exactly <paramref name="value"/> in a Health slot: from the
        /// discard pile first, then from the draw pile, otherwise a Virtual Health Marker
        /// that joins the FIFO queue for that value.
        /// </summary>
        void FillHealthSlot(MatchPlayerState player, HealthSlotSide side, int value)
        {
            PlayingCard card;
            if (_deck.TryTakeFromDiscardPile(value, out card) || _deck.TryTakeFromDrawPile(value, out card))
            {
                player.SetHealthSlot(side, HealthSlot.FromCard(card));
                return;
            }

            player.SetHealthSlot(side, HealthSlot.VirtualMarker(value));
            _waiting[value].Add(new WaitingSlot { PlayerIndex = player.PlayerIndex, Side = side });
        }

        /// <summary>
        /// Empties a Health slot. A physical card is added to <paramref name="released"/>
        /// for the caller to discard; a Virtual marker simply leaves its waiting queue.
        /// </summary>
        void ReleaseSlot(MatchPlayerState player, HealthSlotSide side, List<PlayingCard> released)
        {
            HealthSlot slot = player.GetHealthSlot(side);

            if (slot.IsCard) released.Add(slot.Card);
            else if (slot.IsVirtual) RemoveWaiting(slot.Value, player.PlayerIndex, side);

            player.SetHealthSlot(side, HealthSlot.Empty);
        }

        void RemoveWaiting(int value, int playerIndex, HealthSlotSide side)
        {
            List<WaitingSlot> queue = _waiting[value];

            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].PlayerIndex != playerIndex || queue[i].Side != side) continue;

                queue.RemoveAt(i);
                return;
            }
        }

        // ------------------------------------------------------------------ cards leaving play

        /// <summary>
        /// The single path by which gameplay discards a card. If a Virtual Health Marker is
        /// waiting for this value, the oldest one takes the card instead and it never
        /// reaches the discard pile.
        /// </summary>
        void DiscardCard(PlayingCard card)
        {
            List<WaitingSlot> queue = _waiting[card.Value];

            if (queue.Count > 0)
            {
                WaitingSlot waiting = queue[0];
                queue.RemoveAt(0);
                _players[waiting.PlayerIndex].SetHealthSlot(waiting.Side, HealthSlot.FromCard(card));
                return;
            }

            _deck.Discard(card);
        }

        /// <summary>
        /// Removes a player from the match and releases every card they hold. Their own
        /// markers leave the queues first, so their cards can only serve other players.
        /// </summary>
        void Eliminate(MatchPlayerState player)
        {
            player.IsAlive = false;

            List<PlayingCard> released = new List<PlayingCard>(4);
            ReleaseSlot(player, HealthSlotSide.Left, released);
            ReleaseSlot(player, HealthSlotSide.Right, released);

            if (player.ShieldCard.IsValid) released.Add(player.ShieldCard);
            if (player.HasCharge) released.Add(player.ChargeCard);

            player.ShieldCard = PlayingCard.Nothing;
            player.ChargeCard = PlayingCard.Nothing;

            for (int i = 0; i < released.Count; i++) DiscardCard(released[i]);
        }

        // ------------------------------------------------------------------ turn order

        int NextLivingPlayer(int fromIndex)
        {
            int step = Direction == TurnDirection.Clockwise ? 1 : -1;
            int count = _players.Length;
            int index = fromIndex;

            for (int i = 0; i < count; i++)
            {
                index = ((index + step) % count + count) % count;
                if (_players[index].IsAlive) return index;
            }

            return NoPlayer;
        }

        int FirstLivingPlayer()
        {
            for (int i = 0; i < _players.Length; i++)
                if (_players[i].IsAlive) return i;

            return NoPlayer;
        }

        // ------------------------------------------------------------------ construction

        static MatchSetup RequireCompleteSetup(MatchSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (!setup.InitialSetupComplete)
                throw new InvalidOperationException("Every player must choose their Shield before turn play starts.");

            return setup;
        }

        static Random CreateRandom(MatchSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            // Its own stream, separate from the shuffle and the bot Shield choices.
            return setup.Config.Seed.HasValue ? new Random(setup.Config.Seed.Value ^ 0x7A3C) : new Random();
        }

        static MatchPlayerState[] CreatePlayers(IReadOnlyList<PlayerSetupState> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            if (players.Count < MatchConfig.MinPlayers || players.Count > MatchConfig.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players.Count,
                    string.Format("Shields supports {0} to {1} players.", MatchConfig.MinPlayers, MatchConfig.MaxPlayers));
            }

            MatchPlayerState[] result = new MatchPlayerState[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null || players[i].PlayerIndex != i)
                    throw new ArgumentException("Players must be listed in seat order.", nameof(players));

                result[i] = new MatchPlayerState(players[i]);
            }

            return result;
        }

        static List<WaitingSlot>[] CreateWaitingQueues()
        {
            List<WaitingSlot>[] queues = new List<WaitingSlot>[MaxCardValue + 1];
            for (int i = 0; i < queues.Length; i++) queues[i] = new List<WaitingSlot>();

            return queues;
        }
    }
}
