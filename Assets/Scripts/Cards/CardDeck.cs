using System;
using System.Collections.Generic;

namespace Shields.Cards
{
    /// <summary>
    /// The authoritative draw pile and discard pile for a match, built from one or two
    /// complete standard 52 card decks. Plain C# with no Unity dependency, so the same
    /// instance can serve local play, bots, an authoritative host, or a test.
    ///
    /// A new deck is created in a known order and is NOT shuffled. Call
    /// <see cref="Shuffle"/> once before dealing.
    ///
    /// The deck owns only two locations: the draw pile and the discard pile. A card that
    /// has been drawn belongs to whatever gameplay zone took it and is tracked there,
    /// not here. It returns to the deck only when the caller explicitly discards it.
    /// </summary>
    public sealed class CardDeck
    {
        public const int SuitCount = 4;
        public const int RanksPerSuit = 13;
        public const int CardsPerStandardDeck = SuitCount * RanksPerSuit;

        public const int MinDeckCount = 1;
        public const int MaxDeckCount = 2;

        static readonly CardSuit[] AllSuits =
        {
            CardSuit.Clubs, CardSuit.Diamonds, CardSuit.Hearts, CardSuit.Spades
        };

        readonly List<PlayingCard> _drawPile;
        readonly List<PlayingCard> _discardPile;
        readonly Random _random;

        /// <summary>Creates a deck with a non reproducible shuffle order.</summary>
        public CardDeck(int deckCount)
            : this(deckCount, null)
        {
        }

        /// <summary>
        /// Creates a deck. Supplying a seed makes the shuffle order reproducible, which
        /// is for debugging and tests only. It is not a security or fairness mechanism.
        /// </summary>
        public CardDeck(int deckCount, int? seed)
        {
            if (deckCount < MinDeckCount || deckCount > MaxDeckCount)
            {
                throw new ArgumentOutOfRangeException(nameof(deckCount), deckCount,
                    string.Format("Shields supports {0} to {1} decks.", MinDeckCount, MaxDeckCount));
            }

            DeckCount = deckCount;
            Seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();

            _drawPile = new List<PlayingCard>(TotalCardCount);
            _discardPile = new List<PlayingCard>(TotalCardCount);

            BuildCards();
        }

        /// <summary>How many complete standard decks are in play, 1 or 2.</summary>
        public int DeckCount { get; }

        /// <summary>The seed this deck shuffles with, or null when unseeded.</summary>
        public int? Seed { get; }

        /// <summary>Total cards this deck created, 52 per standard deck.</summary>
        public int TotalCardCount { get { return DeckCount * CardsPerStandardDeck; } }

        /// <summary>Cards still available to draw.</summary>
        public int DrawCount { get { return _drawPile.Count; } }

        /// <summary>Cards sent to the discard pile.</summary>
        public int DiscardCount { get { return _discardPile.Count; } }

        /// <summary>
        /// Cards neither in the draw pile nor the discard pile, so they are sitting in
        /// some active gameplay zone. Useful for accounting checks.
        /// </summary>
        public int InPlayCount { get { return TotalCardCount - DrawCount - DiscardCount; } }

        /// <summary>How many times an empty draw pile has been refilled from the discards.</summary>
        public int ReshuffleCount { get; private set; }

        /// <summary>Read only view of the draw pile. The last entry is the top card.</summary>
        public IReadOnlyList<PlayingCard> DrawPile { get { return _drawPile; } }

        /// <summary>Read only view of the discard pile.</summary>
        public IReadOnlyList<PlayingCard> DiscardPile { get { return _discardPile; } }

        /// <summary>Fisher-Yates shuffle of the draw pile.</summary>
        public void Shuffle()
        {
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);

                PlayingCard swap = _drawPile[i];
                _drawPile[i] = _drawPile[j];
                _drawPile[j] = swap;
            }
        }

        /// <summary>
        /// Takes the top card off the draw pile. If the draw pile is empty, the discard
        /// pile is shuffled back in first. Returns false and yields
        /// <see cref="PlayingCard.Nothing"/> when both piles are empty, rather than
        /// inventing a card.
        /// </summary>
        public bool TryDraw(out PlayingCard card)
        {
            if (_drawPile.Count == 0) RecycleDiscardPile();

            if (_drawPile.Count == 0)
            {
                card = PlayingCard.Nothing;
                return false;
            }

            int top = _drawPile.Count - 1;
            card = _drawPile[top];
            _drawPile.RemoveAt(top);
            return true;
        }

        /// <summary>
        /// Sends one card to the discard pile. The deck trusts the caller about where the
        /// card currently is; only cards passed here are ever considered discarded.
        /// </summary>
        public void Discard(PlayingCard card)
        {
            if (!card.IsValid)
                throw new ArgumentException("Cannot discard a card that is not a real card.", nameof(card));

            _discardPile.Add(card);
        }

        /// <summary>Sends several cards to the discard pile, in the order given.</summary>
        public void Discard(IEnumerable<PlayingCard> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            foreach (PlayingCard card in cards) Discard(card);
        }

        /// <summary>
        /// Removes one card of the given value from the discard pile, searching from the
        /// most recently discarded card down. Returns false and yields
        /// <see cref="PlayingCard.Nothing"/> when no card of that value is there.
        /// </summary>
        public bool TryTakeFromDiscardPile(int value, out PlayingCard card)
        {
            return TryTakeValue(_discardPile, value, out card);
        }

        /// <summary>
        /// Removes one specific card of the given value from the draw pile, searching from
        /// the top down. Every other card keeps its relative order; nothing is shuffled.
        /// Returns false and yields <see cref="PlayingCard.Nothing"/> when there is none.
        /// </summary>
        public bool TryTakeFromDrawPile(int value, out PlayingCard card)
        {
            return TryTakeValue(_drawPile, value, out card);
        }

        /// <summary>
        /// Test support only: moves a card that is already in the draw pile onto the top,
        /// so the next draw is known. Every other card keeps its relative order.
        /// </summary>
        internal void MoveToTopOfDrawPile(PlayingCard card)
        {
            int index = _drawPile.IndexOf(card);
            if (index < 0) throw new ArgumentException("That card is not in the draw pile.", nameof(card));

            _drawPile.RemoveAt(index);
            _drawPile.Add(card);
        }

        // ------------------------------------------------------------------ internals

        static bool TryTakeValue(List<PlayingCard> pile, int value, out PlayingCard card)
        {
            for (int i = pile.Count - 1; i >= 0; i--)
            {
                if (pile[i].Value != value) continue;

                card = pile[i];
                pile.RemoveAt(i);
                return true;
            }

            card = PlayingCard.Nothing;
            return false;
        }

        /// <summary>
        /// Creates every card once per deck, in deck then suit then rank order, giving
        /// each a sequential id. Two decks therefore produce two King of Spades cards
        /// with different ids and different deck indices.
        /// </summary>
        void BuildCards()
        {
            int nextCardId = 0;

            for (int deckIndex = 0; deckIndex < DeckCount; deckIndex++)
            {
                for (int s = 0; s < AllSuits.Length; s++)
                {
                    for (int value = 1; value <= RanksPerSuit; value++)
                    {
                        _drawPile.Add(new PlayingCard(nextCardId, deckIndex, AllSuits[s], (CardRank)value));
                        nextCardId++;
                    }
                }
            }
        }

        /// <summary>
        /// Moves the whole discard pile into the draw pile and shuffles it. Cards sitting
        /// in active gameplay zones are untouched, so they are never restored by this.
        /// </summary>
        void RecycleDiscardPile()
        {
            if (_discardPile.Count == 0) return;

            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();

            Shuffle();
            ReshuffleCount++;
        }
    }
}
