using System;
using System.Collections.Generic;
using Shields.Cards;

namespace Shields.Game
{
    /// <summary>
    /// Drives the initial setup of a Shields match: build and shuffle the deck, deal
    /// three cards to every seat, let bots pick their Shield at random, and report when
    /// every player has finished choosing.
    ///
    /// Entirely plain C#. The same instance can later be driven by a host, a bot, or a
    /// test without a scene existing. Turn play is deliberately not part of this type.
    /// </summary>
    public sealed class MatchSetup
    {
        /// <summary>Cards dealt to each player at the start of a match.</summary>
        public const int CardsPerPlayer = PlayerSetupState.DealtCardCount;

        readonly PlayerSetupState[] _players;
        readonly Random _botRandom;

        /// <summary>
        /// Creates the deck, shuffles it and deals. Players are created with the local
        /// seat taken from the config and every other seat marked as a bot.
        /// </summary>
        public MatchSetup(MatchConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));

            Deck = new CardDeck(config.DeckCount, config.Seed);
            Deck.Shuffle();

            // A separate stream from the deck's, so bot choices and shuffles stay
            // independent while both remain reproducible under a seed.
            _botRandom = config.Seed.HasValue ? new Random(config.Seed.Value ^ 0x5B1E) : new Random();

            _players = new PlayerSetupState[config.PlayerCount];
            Deal();
        }

        public MatchConfig Config { get; }

        /// <summary>The authoritative draw and discard piles for the whole match.</summary>
        public CardDeck Deck { get; }

        public IReadOnlyList<PlayerSetupState> Players { get { return _players; } }

        public PlayerSetupState LocalPlayer { get { return _players[Config.LocalPlayerIndex]; } }

        /// <summary>
        /// True once every seat has two Health cards, one Shield card and a numeric
        /// starting Health. This is the state TASK 004 ends on.
        /// </summary>
        public bool InitialSetupComplete
        {
            get
            {
                for (int i = 0; i < _players.Length; i++)
                    if (!_players[i].IsSelectionComplete) return false;

                return true;
            }
        }

        public PlayerSetupState GetPlayer(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));

            return _players[playerIndex];
        }

        /// <summary>
        /// Makes every bot pick one of its own three cards as its Shield, at random.
        /// A bot never looks at another player's cards; the choice is intentionally blind.
        /// </summary>
        public void RunBotSelections()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerSetupState player = _players[i];
                if (!player.IsBot || player.IsSelectionComplete) continue;

                player.SelectShieldAt(_botRandom.Next(PlayerSetupState.DealtCardCount));
            }
        }

        /// <summary>Applies a chosen Shield for one seat, used by the local player's click.</summary>
        public void SelectShieldFor(int playerIndex, PlayingCard card)
        {
            GetPlayer(playerIndex).SelectShield(card);
        }

        /// <summary>One line summary of the finished setup, handy for logging.</summary>
        public string DescribeSetup()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("{0} | deck {1}/{2} remaining, {3} in play",
                Config, Deck.DrawCount, Deck.TotalCardCount, Deck.InPlayCount);

            for (int i = 0; i < _players.Length; i++)
                sb.Append("\n  ").Append(_players[i]);

            return sb.ToString();
        }

        // ------------------------------------------------------------------ internals

        /// <summary>
        /// Deals round robin, one card at a time, the way a real deal works. Every dealt
        /// card leaves the draw pile and is an active in play card from then on.
        /// </summary>
        void Deal()
        {
            List<PlayingCard>[] hands = new List<PlayingCard>[Config.PlayerCount];
            for (int i = 0; i < hands.Length; i++) hands[i] = new List<PlayingCard>(CardsPerPlayer);

            for (int round = 0; round < CardsPerPlayer; round++)
            {
                for (int seat = 0; seat < Config.PlayerCount; seat++)
                {
                    PlayingCard card;
                    if (!Deck.TryDraw(out card))
                    {
                        throw new InvalidOperationException(
                            "The deck ran out while dealing, which should be impossible for a standard match.");
                    }

                    hands[seat].Add(card);
                }
            }

            for (int seat = 0; seat < Config.PlayerCount; seat++)
                _players[seat] = new PlayerSetupState(seat, seat != Config.LocalPlayerIndex, hands[seat]);
        }
    }
}
