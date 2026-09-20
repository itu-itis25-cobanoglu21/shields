using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shields.Cards;

namespace Shields.Cards.Tests
{
    /// <summary>
    /// EditMode tests for the logical card and deck foundation. These cover the
    /// validation list in TASK 003: deck generation, card identity, draw, discard and
    /// reshuffle on exhaustion.
    /// </summary>
    public class CardDeckTests
    {
        static readonly CardSuit[] AllSuits =
        {
            CardSuit.Clubs, CardSuit.Diamonds, CardSuit.Hearts, CardSuit.Spades
        };

        const int TestSeed = 12345;

        static List<PlayingCard> AllCardsOf(CardDeck deck)
        {
            // A freshly built deck holds every card in its draw pile.
            return deck.DrawPile.ToList();
        }

        // ---------------------------------------------------------------- one deck

        [Test]
        public void OneDeck_CreatesExactly52Cards()
        {
            CardDeck deck = new CardDeck(1);

            Assert.AreEqual(52, deck.TotalCardCount);
            Assert.AreEqual(52, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void OneDeck_EverySuitHasAll13Ranks()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(1));

            foreach (CardSuit suit in AllSuits)
            {
                List<CardRank> ranks = cards.Where(c => c.Suit == suit).Select(c => c.Rank).ToList();

                Assert.AreEqual(13, ranks.Count, "Suit {0} should have 13 cards.", suit);
                Assert.AreEqual(13, ranks.Distinct().Count(), "Suit {0} should have 13 distinct ranks.", suit);
            }
        }

        [Test]
        public void OneDeck_ValuesRunAceOneThroughKingThirteen()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(1));

            Assert.AreEqual(1, cards.First(c => c.Rank == CardRank.Ace).Value);
            Assert.AreEqual(11, cards.First(c => c.Rank == CardRank.Jack).Value);
            Assert.AreEqual(12, cards.First(c => c.Rank == CardRank.Queen).Value);
            Assert.AreEqual(13, cards.First(c => c.Rank == CardRank.King).Value);

            foreach (PlayingCard card in cards)
            {
                Assert.AreEqual((int)card.Rank, card.Value, "Value must equal the rank for {0}.", card);
                Assert.That(card.Value, Is.InRange(1, 13));
            }
        }

        [Test]
        public void OneDeck_AllCardIdsAreUnique()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(1));

            Assert.AreEqual(52, cards.Select(c => c.CardId).Distinct().Count());
        }

        [Test]
        public void OneDeck_AllCardsReportDeckIndexZero()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(1));

            Assert.IsTrue(cards.All(c => c.DeckIndex == 0));
        }

        // ---------------------------------------------------------------- two decks

        [Test]
        public void TwoDecks_CreatesExactly104Cards()
        {
            CardDeck deck = new CardDeck(2);

            Assert.AreEqual(104, deck.TotalCardCount);
            Assert.AreEqual(104, deck.DrawCount);
        }

        [Test]
        public void TwoDecks_EverySuitRankCombinationAppearsTwice()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(2));

            foreach (CardSuit suit in AllSuits)
            {
                for (int value = 1; value <= 13; value++)
                {
                    CardRank rank = (CardRank)value;
                    int count = cards.Count(c => c.Suit == suit && c.Rank == rank);

                    Assert.AreEqual(2, count, "{0} of {1} should appear twice.", rank, suit);
                }
            }
        }

        [Test]
        public void TwoDecks_AllCardIdsAreStillUnique()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(2));

            Assert.AreEqual(104, cards.Select(c => c.CardId).Distinct().Count());
        }

        [Test]
        public void TwoDecks_RecordSourceDeckIndex()
        {
            List<PlayingCard> cards = AllCardsOf(new CardDeck(2));

            Assert.AreEqual(52, cards.Count(c => c.DeckIndex == 0));
            Assert.AreEqual(52, cards.Count(c => c.DeckIndex == 1));
            Assert.IsTrue(cards.All(c => c.DeckIndex == 0 || c.DeckIndex == 1));
        }

        [Test]
        public void TwoDecks_SameFaceFromDifferentDecksAreDifferentInstances()
        {
            List<PlayingCard> kingsOfSpades = AllCardsOf(new CardDeck(2))
                .Where(c => c.Suit == CardSuit.Spades && c.Rank == CardRank.King)
                .ToList();

            Assert.AreEqual(2, kingsOfSpades.Count);

            PlayingCard fromDeck0 = kingsOfSpades[0];
            PlayingCard fromDeck1 = kingsOfSpades[1];

            Assert.IsTrue(fromDeck0.HasSameFace(fromDeck1), "Both should be the same face.");
            Assert.AreEqual(fromDeck0.Value, fromDeck1.Value);
            Assert.AreNotEqual(fromDeck0.CardId, fromDeck1.CardId, "Ids must differ.");
            Assert.AreNotEqual(fromDeck0.DeckIndex, fromDeck1.DeckIndex, "Deck indices must differ.");
            Assert.AreNotEqual(fromDeck0, fromDeck1, "They must not be equal as instances.");
        }

        [Test]
        public void DeckCountOutsideOneOrTwoIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CardDeck(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CardDeck(3));
        }

        // ---------------------------------------------------------------- shuffle

        [Test]
        public void Shuffle_KeepsExactlyTheSameCards()
        {
            CardDeck deck = new CardDeck(2, TestSeed);
            List<int> before = deck.DrawPile.Select(c => c.CardId).OrderBy(id => id).ToList();

            deck.Shuffle();

            List<int> after = deck.DrawPile.Select(c => c.CardId).OrderBy(id => id).ToList();

            Assert.AreEqual(104, deck.DrawCount);
            CollectionAssert.AreEqual(before, after);
        }

        [Test]
        public void Shuffle_ChangesOrder()
        {
            CardDeck deck = new CardDeck(1, TestSeed);
            List<int> ordered = deck.DrawPile.Select(c => c.CardId).ToList();

            deck.Shuffle();

            List<int> shuffled = deck.DrawPile.Select(c => c.CardId).ToList();

            CollectionAssert.AreNotEqual(ordered, shuffled);
        }

        [Test]
        public void Shuffle_IsReproducibleForTheSameSeed()
        {
            CardDeck a = new CardDeck(2, TestSeed);
            CardDeck b = new CardDeck(2, TestSeed);

            a.Shuffle();
            b.Shuffle();

            CollectionAssert.AreEqual(
                a.DrawPile.Select(c => c.CardId).ToList(),
                b.DrawPile.Select(c => c.CardId).ToList());
        }

        // ---------------------------------------------------------------- draw

        [Test]
        public void Draw_DecreasesDrawCountAndRemovesThatCard()
        {
            CardDeck deck = new CardDeck(1, TestSeed);
            deck.Shuffle();

            int before = deck.DrawCount;

            PlayingCard drawn;
            Assert.IsTrue(deck.TryDraw(out drawn));

            Assert.IsTrue(drawn.IsValid);
            Assert.AreEqual(before - 1, deck.DrawCount);
            Assert.IsFalse(deck.DrawPile.Contains(drawn), "Drawn card must leave the draw pile.");
            Assert.IsFalse(deck.DiscardPile.Contains(drawn), "Drawing must not discard the card.");
        }

        [Test]
        public void Draw_EveryCardOnceGivesTheWholeDeckWithNoRepeats()
        {
            CardDeck deck = new CardDeck(2, TestSeed);
            deck.Shuffle();

            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < 104; i++)
            {
                PlayingCard card;
                Assert.IsTrue(deck.TryDraw(out card), "Draw {0} should succeed.", i);
                Assert.IsTrue(seen.Add(card.CardId), "Card id {0} was drawn twice.", card.CardId);
            }

            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(104, seen.Count);
        }

        [Test]
        public void Draw_FailsGracefullyWhenBothPilesAreEmpty()
        {
            CardDeck deck = new CardDeck(1, TestSeed);

            for (int i = 0; i < 52; i++)
            {
                PlayingCard ignored;
                Assert.IsTrue(deck.TryDraw(out ignored));
            }

            PlayingCard card;
            bool drew = deck.TryDraw(out card);

            Assert.IsFalse(drew, "An exhausted deck must not produce a card.");
            Assert.IsFalse(card.IsValid);
            Assert.AreEqual(PlayingCard.Nothing, card);
            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        // ---------------------------------------------------------------- discard

        [Test]
        public void Discard_IncreasesDiscardCountAndKeepsIdentity()
        {
            CardDeck deck = new CardDeck(1, TestSeed);
            deck.Shuffle();

            PlayingCard drawn;
            deck.TryDraw(out drawn);

            int discardBefore = deck.DiscardCount;
            deck.Discard(drawn);

            Assert.AreEqual(discardBefore + 1, deck.DiscardCount);

            PlayingCard inDiscard = deck.DiscardPile[deck.DiscardCount - 1];

            Assert.AreEqual(drawn, inDiscard);
            Assert.AreEqual(drawn.CardId, inDiscard.CardId);
            Assert.AreEqual(drawn.DeckIndex, inDiscard.DeckIndex);
            Assert.AreEqual(drawn.Suit, inDiscard.Suit);
            Assert.AreEqual(drawn.Rank, inDiscard.Rank);
            Assert.AreEqual(drawn.Value, inDiscard.Value);
        }

        [Test]
        public void Discard_RejectsANonCard()
        {
            CardDeck deck = new CardDeck(1);

            Assert.Throws<System.ArgumentException>(() => deck.Discard(PlayingCard.Nothing));
        }

        [Test]
        public void CardsHeldInPlayAreNeitherDrawableNorDiscarded()
        {
            CardDeck deck = new CardDeck(1, TestSeed);
            deck.Shuffle();

            List<PlayingCard> held = new List<PlayingCard>();
            for (int i = 0; i < 5; i++)
            {
                PlayingCard card;
                deck.TryDraw(out card);
                held.Add(card);
            }

            Assert.AreEqual(47, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
            Assert.AreEqual(5, deck.InPlayCount);

            foreach (PlayingCard card in held)
            {
                Assert.IsFalse(deck.DrawPile.Contains(card));
                Assert.IsFalse(deck.DiscardPile.Contains(card));
            }
        }

        // ---------------------------------------------------------------- reshuffle

        [Test]
        public void Reshuffle_RefillsDrawPileFromDiscardsWhenEmpty()
        {
            CardDeck deck = new CardDeck(1, TestSeed);
            deck.Shuffle();

            // Draw the whole deck, discarding everything as it comes out.
            List<PlayingCard> drawnCards = new List<PlayingCard>();
            for (int i = 0; i < 52; i++)
            {
                PlayingCard card;
                deck.TryDraw(out card);
                drawnCards.Add(card);
                deck.Discard(card);
            }

            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(52, deck.DiscardCount);
            Assert.AreEqual(0, deck.ReshuffleCount);

            PlayingCard next;
            Assert.IsTrue(deck.TryDraw(out next), "The discard pile should refill the draw pile.");

            Assert.AreEqual(1, deck.ReshuffleCount);
            Assert.AreEqual(51, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
            Assert.IsTrue(next.IsValid);

            // The refilled pile must be exactly the discarded cards, nothing invented.
            List<int> expected = drawnCards.Select(c => c.CardId).OrderBy(id => id).ToList();
            List<int> actual = deck.DrawPile.Select(c => c.CardId).Concat(new[] { next.CardId })
                .OrderBy(id => id).ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Reshuffle_DoesNotRestoreCardsStillInPlay()
        {
            CardDeck deck = new CardDeck(1, TestSeed);
            deck.Shuffle();

            List<PlayingCard> held = new List<PlayingCard>();
            List<PlayingCard> discarded = new List<PlayingCard>();

            for (int i = 0; i < 52; i++)
            {
                PlayingCard card;
                deck.TryDraw(out card);

                // Keep the first 10 cards in an active gameplay zone.
                if (i < 10) held.Add(card);
                else { discarded.Add(card); deck.Discard(card); }
            }

            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(42, deck.DiscardCount);
            Assert.AreEqual(10, deck.InPlayCount);

            PlayingCard next;
            Assert.IsTrue(deck.TryDraw(out next));

            Assert.AreEqual(1, deck.ReshuffleCount);
            Assert.AreEqual(41, deck.DrawCount, "Only the 42 discards come back, minus the one drawn.");
            Assert.AreEqual(0, deck.DiscardCount);

            // 10 still held plus the card just drawn are all outside both piles.
            Assert.AreEqual(11, deck.InPlayCount, "Held cards must stay out of the deck.");

            HashSet<int> heldIds = new HashSet<int>(held.Select(c => c.CardId));
            foreach (PlayingCard card in deck.DrawPile)
                Assert.IsFalse(heldIds.Contains(card.CardId), "{0} was still in play and must not be restored.", card);

            Assert.IsFalse(heldIds.Contains(next.CardId));
        }

        [Test]
        public void Reshuffle_ShufflesRatherThanReversingTheDiscardPile()
        {
            CardDeck deck = new CardDeck(2, TestSeed);
            deck.Shuffle();

            List<int> discardOrder = new List<int>();
            for (int i = 0; i < 104; i++)
            {
                PlayingCard card;
                deck.TryDraw(out card);
                deck.Discard(card);
                discardOrder.Add(card.CardId);
            }

            PlayingCard first;
            deck.TryDraw(out first);

            List<int> afterReshuffle = new List<int> { first.CardId };
            afterReshuffle.AddRange(deck.DrawPile.Reverse().Select(c => c.CardId));

            CollectionAssert.AreNotEqual(discardOrder, afterReshuffle,
                "A reshuffle must not simply flip the discard pile over.");
            CollectionAssert.AreEquivalent(discardOrder, afterReshuffle);
        }

        // ---------------------------------------------------------------- accounting

        [Test]
        public void EveryCardIsAlwaysAccountedFor()
        {
            CardDeck deck = new CardDeck(2, TestSeed);
            deck.Shuffle();

            int inPlay = 0;

            for (int i = 0; i < 40; i++)
            {
                PlayingCard card;
                Assert.IsTrue(deck.TryDraw(out card));

                if (i % 2 == 0) deck.Discard(card);
                else inPlay++;

                Assert.AreEqual(104, deck.DrawCount + deck.DiscardCount + inPlay);
                Assert.AreEqual(inPlay, deck.InPlayCount);
            }
        }
    }
}
