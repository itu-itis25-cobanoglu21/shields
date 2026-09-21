using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shields.Cards;
using Shields.Game;

namespace Shields.Game.Tests
{
    /// <summary>
    /// EditMode tests for the pure initial setup logic of TASK 004: dealing, Shield
    /// selection, and random bot selection. No Unity objects are involved.
    /// </summary>
    public class MatchSetupTests
    {
        const int TestSeed = 4242;

        static MatchConfig Config(int players, int decks, int seed = TestSeed, int localSeat = 0)
        {
            return new MatchConfig(players, decks, localSeat, seed);
        }

        static List<PlayingCard> ThreeCards()
        {
            return new List<PlayingCard>
            {
                new PlayingCard(0, 0, CardSuit.Spades, CardRank.King),   // 13
                new PlayingCard(1, 0, CardSuit.Hearts, CardRank.Jack),   // 11
                new PlayingCard(2, 0, CardSuit.Clubs, CardRank.Five)     // 5
            };
        }

        // ---------------------------------------------------------------- deal

        [Test]
        public void Deal_EveryPlayerCountGetsExactlyThreeCardsEach([Values(3, 4, 5, 6, 7, 8)] int playerCount)
        {
            MatchSetup setup = new MatchSetup(Config(playerCount, 1));

            Assert.AreEqual(playerCount, setup.Players.Count);

            foreach (PlayerSetupState player in setup.Players)
                Assert.AreEqual(3, player.DealtCards.Count, "Seat {0} should hold 3 cards.", player.PlayerIndex);
        }

        [Test]
        public void Deal_NoCardIdentityAppearsInTwoPlayersHands([Values(3, 4, 5, 6, 7, 8)] int playerCount)
        {
            MatchSetup setup = new MatchSetup(Config(playerCount, 1));

            List<int> allIds = setup.Players.SelectMany(p => p.DealtCards).Select(c => c.CardId).ToList();

            Assert.AreEqual(playerCount * 3, allIds.Count);
            Assert.AreEqual(allIds.Count, allIds.Distinct().Count(), "A card was dealt to two players.");
        }

        [Test]
        public void Deal_DrawPileDropsByThreePerPlayer([Values(3, 4, 5, 6, 7, 8)] int playerCount)
        {
            MatchSetup setup = new MatchSetup(Config(playerCount, 1));

            int dealt = playerCount * 3;

            Assert.AreEqual(52, setup.Deck.TotalCardCount);
            Assert.AreEqual(52 - dealt, setup.Deck.DrawCount);
            Assert.AreEqual(0, setup.Deck.DiscardCount, "Dealing must not discard anything.");
            Assert.AreEqual(dealt, setup.Deck.InPlayCount, "Dealt cards are active in play cards.");
        }

        [Test]
        public void Deal_DealtCardsAreNoLongerInTheDrawPile()
        {
            MatchSetup setup = new MatchSetup(Config(6, 1));

            HashSet<int> dealtIds = new HashSet<int>(
                setup.Players.SelectMany(p => p.DealtCards).Select(c => c.CardId));

            foreach (PlayingCard card in setup.Deck.DrawPile)
                Assert.IsFalse(dealtIds.Contains(card.CardId), "{0} was dealt but is still drawable.", card);
        }

        [Test]
        public void Deal_TwoDeckMatchUses104CardsAndStillDealsUniqueIdentities()
        {
            MatchSetup setup = new MatchSetup(Config(8, 2));

            Assert.AreEqual(104, setup.Deck.TotalCardCount);
            Assert.AreEqual(104 - 24, setup.Deck.DrawCount);

            List<PlayingCard> dealt = setup.Players.SelectMany(p => p.DealtCards).ToList();

            Assert.AreEqual(24, dealt.Count);
            Assert.AreEqual(24, dealt.Select(c => c.CardId).Distinct().Count());
            Assert.IsTrue(dealt.All(c => c.DeckIndex == 0 || c.DeckIndex == 1));
        }

        [Test]
        public void Deal_LocalSeatIsHumanAndEveryOtherSeatIsABot()
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(6, 1, 0, TestSeed));

            Assert.AreEqual(1, setup.Players.Count(p => !p.IsBot));
            Assert.AreEqual(5, setup.Players.Count(p => p.IsBot));
            Assert.AreEqual(0, setup.LocalPlayer.PlayerIndex);
            Assert.IsFalse(setup.LocalPlayer.IsBot);
        }

        // ---------------------------------------------------------------- selection

        [Test]
        public void Selection_ChosenCardBecomesShieldAndTheRestBecomeHealth()
        {
            List<PlayingCard> cards = ThreeCards();
            PlayerSetupState player = new PlayerSetupState(0, false, cards);

            Assert.IsFalse(player.IsSelectionComplete);

            player.SelectShield(cards[2]); // the Five

            Assert.IsTrue(player.IsSelectionComplete);
            Assert.AreEqual(cards[2], player.ShieldCard);
            Assert.AreEqual(5, player.ShieldStrength);

            CollectionAssert.AreEquivalent(new[] { cards[0], cards[1] }, player.HealthCards);
        }

        [Test]
        public void Selection_StartingHealthIsTheSumOfTheTwoHealthCards()
        {
            List<PlayingCard> cards = ThreeCards();
            PlayerSetupState player = new PlayerSetupState(0, false, cards);

            player.SelectShield(cards[2]); // Five is the Shield, King + Jack are Health

            Assert.AreEqual(24, player.StartingHealth, "King 13 + Jack 11 = 24.");
        }

        [Test]
        public void Selection_AnyOfTheThreeCanBeTheShield([Values(0, 1, 2)] int shieldIndex)
        {
            List<PlayingCard> cards = ThreeCards();
            PlayerSetupState player = new PlayerSetupState(0, false, cards);

            player.SelectShieldAt(shieldIndex);

            int expectedHealth = 0;
            for (int i = 0; i < cards.Count; i++)
                if (i != shieldIndex) expectedHealth += cards[i].Value;

            Assert.AreEqual(cards[shieldIndex], player.ShieldCard);
            Assert.AreEqual(2, player.HealthCards.Count);
            Assert.AreEqual(expectedHealth, player.StartingHealth);
        }

        [Test]
        public void Selection_PreservesAllThreeOriginalIdentities()
        {
            List<PlayingCard> cards = ThreeCards();
            PlayerSetupState player = new PlayerSetupState(0, false, cards);

            player.SelectShield(cards[1]);

            List<PlayingCard> afterwards = new List<PlayingCard>(player.HealthCards) { player.ShieldCard };

            CollectionAssert.AreEquivalent(cards, afterwards);
            CollectionAssert.AreEquivalent(
                cards.Select(c => c.CardId).ToList(),
                afterwards.Select(c => c.CardId).ToList());

            // The dealt list itself is untouched by choosing.
            CollectionAssert.AreEqual(cards, player.DealtCards);
        }

        [Test]
        public void Selection_RejectsACardThePlayerWasNotDealt()
        {
            PlayerSetupState player = new PlayerSetupState(0, false, ThreeCards());
            PlayingCard stranger = new PlayingCard(99, 1, CardSuit.Diamonds, CardRank.Two);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => player.SelectShield(stranger));
            Assert.IsFalse(player.IsSelectionComplete);
        }

        [Test]
        public void Selection_CannotBeMadeTwice()
        {
            List<PlayingCard> cards = ThreeCards();
            PlayerSetupState player = new PlayerSetupState(0, false, cards);

            player.SelectShield(cards[0]);

            Assert.Throws<System.InvalidOperationException>(() => player.SelectShield(cards[1]));
        }

        // ---------------------------------------------------------------- bots

        [Test]
        public void Bots_SelectExactlyOneOfTheirOwnThreeCards()
        {
            MatchSetup setup = new MatchSetup(Config(8, 1));

            setup.RunBotSelections();

            foreach (PlayerSetupState bot in setup.Players.Where(p => p.IsBot))
            {
                Assert.IsTrue(bot.IsSelectionComplete, "Seat {0} should have chosen.", bot.PlayerIndex);
                Assert.IsTrue(bot.WasDealt(bot.ShieldCard), "A bot must shield one of its own cards.");
                Assert.AreEqual(2, bot.HealthCards.Count);

                foreach (PlayingCard health in bot.HealthCards)
                {
                    Assert.IsTrue(bot.WasDealt(health));
                    Assert.AreNotEqual(bot.ShieldCard, health, "The Shield cannot also be a Health card.");
                }
            }
        }

        [Test]
        public void Bots_HealthEqualsTheSumOfTheirTwoRemainingCards()
        {
            MatchSetup setup = new MatchSetup(Config(8, 2));

            setup.RunBotSelections();

            foreach (PlayerSetupState bot in setup.Players.Where(p => p.IsBot))
            {
                int expected = bot.HealthCards.Sum(c => c.Value);

                Assert.AreEqual(expected, bot.StartingHealth);
                Assert.That(bot.StartingHealth, Is.InRange(2, 26));
            }
        }

        [Test]
        public void Bots_DoNotTouchTheLocalSeat()
        {
            MatchSetup setup = new MatchSetup(Config(6, 1));

            setup.RunBotSelections();

            Assert.IsFalse(setup.LocalPlayer.IsSelectionComplete, "The human must still choose.");
            Assert.IsFalse(setup.InitialSetupComplete);
        }

        [Test]
        public void Bots_SameSeedReproducesTheSameChoices()
        {
            MatchSetup a = new MatchSetup(Config(8, 1));
            MatchSetup b = new MatchSetup(Config(8, 1));

            a.RunBotSelections();
            b.RunBotSelections();

            for (int i = 0; i < a.Players.Count; i++)
            {
                Assert.AreEqual(a.Players[i].ShieldCard, b.Players[i].ShieldCard,
                    "Seat {0} should choose identically under the same seed.", i);
                Assert.AreEqual(a.Players[i].StartingHealth, b.Players[i].StartingHealth);
            }
        }

        [Test]
        public void Bots_DifferentSeedsCanProduceDifferentChoices()
        {
            MatchSetup a = new MatchSetup(Config(8, 1, seed: 1));
            MatchSetup b = new MatchSetup(Config(8, 1, seed: 2));

            a.RunBotSelections();
            b.RunBotSelections();

            List<int> aShields = a.Players.Select(p => p.ShieldCard.CardId).ToList();
            List<int> bShields = b.Players.Select(p => p.ShieldCard.CardId).ToList();

            CollectionAssert.AreNotEqual(aShields, bShields);
        }

        // ---------------------------------------------------------------- completion

        [Test]
        public void InitialSetupComplete_OnlyOnceEverySeatHasChosen()
        {
            MatchSetup setup = new MatchSetup(Config(5, 1));

            Assert.IsFalse(setup.InitialSetupComplete, "Nobody has chosen yet.");

            setup.RunBotSelections();
            Assert.IsFalse(setup.InitialSetupComplete, "The local seat is still outstanding.");

            setup.SelectShieldFor(setup.LocalPlayer.PlayerIndex, setup.LocalPlayer.DealtCards[1]);

            Assert.IsTrue(setup.InitialSetupComplete);

            foreach (PlayerSetupState player in setup.Players)
            {
                Assert.AreEqual(2, player.HealthCards.Count);
                Assert.IsTrue(player.ShieldCard.IsValid);
                Assert.Greater(player.StartingHealth, 0);
                Assert.IsTrue(player.IsSelectionComplete);
            }
        }

        [Test]
        public void SetupNeverLosesOrDuplicatesACard([Values(3, 6, 8)] int playerCount)
        {
            MatchSetup setup = new MatchSetup(Config(playerCount, playerCount == 8 ? 2 : 1));

            setup.RunBotSelections();
            setup.SelectShieldFor(setup.LocalPlayer.PlayerIndex, setup.LocalPlayer.DealtCards[0]);

            List<int> held = setup.Players
                .SelectMany(p => p.HealthCards.Concat(new[] { p.ShieldCard }))
                .Select(c => c.CardId)
                .ToList();

            Assert.AreEqual(playerCount * 3, held.Count);
            Assert.AreEqual(held.Count, held.Distinct().Count());

            // Held cards plus the draw pile plus the discard pile equal the whole deck.
            Assert.AreEqual(setup.Deck.TotalCardCount,
                held.Count + setup.Deck.DrawCount + setup.Deck.DiscardCount);
            Assert.AreEqual(held.Count, setup.Deck.InPlayCount);
        }

        // ---------------------------------------------------------------- config

        [Test]
        public void Config_RejectsUnsupportedValues()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchConfig(2, 1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchConfig(9, 1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchConfig(6, 0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchConfig(6, 3, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchConfig(6, 1, 6));
        }
    }
}
