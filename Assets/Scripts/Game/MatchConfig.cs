using System;
using Shields.Cards;

namespace Shields.Game
{
    /// <summary>
    /// The settings a match is started with. Chosen in the prototype setup UI today and
    /// by a lobby later, but the type itself is plain C# with no Unity dependency.
    /// </summary>
    public sealed class MatchConfig
    {
        public const int MinPlayers = 3;
        public const int MaxPlayers = 8;

        public MatchConfig(int playerCount, int deckCount, int localPlayerIndex, int? seed = null)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount,
                    string.Format("Shields supports {0} to {1} players.", MinPlayers, MaxPlayers));
            }

            if (deckCount < CardDeck.MinDeckCount || deckCount > CardDeck.MaxDeckCount)
            {
                throw new ArgumentOutOfRangeException(nameof(deckCount), deckCount,
                    string.Format("Shields supports {0} to {1} decks.", CardDeck.MinDeckCount, CardDeck.MaxDeckCount));
            }

            if (localPlayerIndex < 0 || localPlayerIndex >= playerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerIndex), localPlayerIndex,
                    "The local seat must be one of the seats in the match.");
            }

            PlayerCount = playerCount;
            DeckCount = deckCount;
            LocalPlayerIndex = localPlayerIndex;
            Seed = seed;
        }

        /// <summary>Total seats, humans and bots together.</summary>
        public int PlayerCount { get; }

        /// <summary>How many complete 52 card decks the match is played with.</summary>
        public int DeckCount { get; }

        /// <summary>Which seat the local human owns in this offline prototype.</summary>
        public int LocalPlayerIndex { get; }

        /// <summary>
        /// Optional seed making the shuffle and the bot choices reproducible. For
        /// debugging and tests only, never a fairness mechanism.
        /// </summary>
        public int? Seed { get; }

        public override string ToString()
        {
            return string.Format("{0} players, {1} deck(s), local seat {2}{3}",
                PlayerCount, DeckCount, LocalPlayerIndex, Seed.HasValue ? ", seed " + Seed.Value : "");
        }
    }
}
