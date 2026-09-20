using System;

namespace Shields.Cards
{
    /// <summary>
    /// One physical playing card. This is the authoritative gameplay representation:
    /// a plain immutable value with no Unity dependency, safe to copy between zones,
    /// send over a wire, or reason about in tests and bots.
    ///
    /// Identity is <see cref="CardId"/>, not rank and suit. A two deck game holds two
    /// King of Spades cards that are equal in value but are different card instances,
    /// so equality and hashing use the id alone.
    /// </summary>
    public readonly struct PlayingCard : IEquatable<PlayingCard>
    {
        /// <summary>The <see cref="CardId"/> reported by <see cref="Nothing"/>.</summary>
        public const int NoCardId = -1;

        /// <summary>A card shaped nothing, returned when a draw cannot be satisfied.</summary>
        public static readonly PlayingCard Nothing = default;

        readonly int _cardIdPlusOne;

        public PlayingCard(int cardId, int deckIndex, CardSuit suit, CardRank rank)
        {
            if (cardId < 0) throw new ArgumentOutOfRangeException(nameof(cardId), cardId, "Card ids start at 0.");
            if (deckIndex < 0) throw new ArgumentOutOfRangeException(nameof(deckIndex), deckIndex, "Deck index starts at 0.");

            // Stored offset by one so that default(PlayingCard) reads as NoCardId
            // rather than as a real card with id 0.
            _cardIdPlusOne = cardId + 1;
            DeckIndex = deckIndex;
            Suit = suit;
            Rank = rank;
        }

        /// <summary>
        /// Unique identity of this physical card within the deck that created it.
        /// Assigned in creation order, so it is stable and reproducible rather than
        /// dependent on any GameObject or object reference.
        /// </summary>
        public int CardId { get { return _cardIdPlusOne - 1; } }

        /// <summary>Which of the standard decks this card was dealt from, 0 or 1.</summary>
        public int DeckIndex { get; }

        public CardSuit Suit { get; }

        public CardRank Rank { get; }

        /// <summary>Gameplay value, Ace = 1 through King = 13.</summary>
        public int Value { get { return (int)Rank; } }

        /// <summary>False for <see cref="Nothing"/>, true for any real card.</summary>
        public bool IsValid { get { return _cardIdPlusOne > 0; } }

        public bool Equals(PlayingCard other)
        {
            return _cardIdPlusOne == other._cardIdPlusOne;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayingCard other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _cardIdPlusOne;
        }

        public static bool operator ==(PlayingCard a, PlayingCard b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(PlayingCard a, PlayingCard b)
        {
            return !a.Equals(b);
        }

        /// <summary>True when two cards are the same rank and suit, ignoring identity.</summary>
        public bool HasSameFace(PlayingCard other)
        {
            return Rank == other.Rank && Suit == other.Suit;
        }

        public override string ToString()
        {
            if (!IsValid) return "NoCard";

            return string.Format("{0} of {1} (value {2}, deck {3}, id {4})",
                Rank, Suit, Value, DeckIndex, CardId);
        }
    }
}
