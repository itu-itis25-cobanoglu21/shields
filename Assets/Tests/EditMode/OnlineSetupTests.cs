using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shields.Cards;
using Shields.Game;
using Shields.Online;
using Shields.Session;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Host authority over the initial deal and the Shield choices (TASK 009 sections 43
    /// and 44), plus the card accounting guarantees that must survive going online.
    /// </summary>
    public class OnlineSetupTests
    {
        const ulong HostClient = 0;
        const ulong ClientA = 1;
        const ulong Stranger = 77;

        // ================================================================ helpers

        /// <summary>Host on seat 0, one client on seat 1; the rest are bots.</summary>
        static SeatRegistry Seats(int seatCount = 4)
        {
            SeatRegistry seats = new SeatRegistry(seatCount);
            seats.AssignSeat(HostClient);
            seats.AssignSeat(ClientA);
            return seats;
        }

        static MatchSetup Setup(int players = 4, int decks = 1, int seed = 42)
        {
            bool[] isBot = PublicStateBuilder.BuildSeatOccupancy(Seats(players), players);
            return new MatchSetup(new MatchConfig(players, decks, 0, seed), isBot);
        }

        static int CardIdOf(MatchSetup setup, int seat, int dealtIndex)
        {
            return setup.GetPlayer(seat).DealtCards[dealtIndex].CardId;
        }

        // ================================================================ occupancy

        [Test]
        public void Occupancy_ConnectedHumansKeepSeatsAndTheRestBecomeBots()
        {
            bool[] isBot = PublicStateBuilder.BuildSeatOccupancy(Seats(6), 6);

            Assert.That(isBot[0], Is.False, "The host holds seat 0.");
            Assert.That(isBot[1], Is.False, "The client holds seat 1.");

            for (int seat = 2; seat < 6; seat++)
                Assert.That(isBot[seat], Is.True, "Seat {0} was unoccupied and should be a bot.", seat);
        }

        [Test]
        public void Occupancy_MatchesTheTaskExample()
        {
            // TASK 009 section 5: three humans at a six seat table gives three bots.
            SeatRegistry seats = new SeatRegistry(6);
            seats.AssignSeat(10);
            seats.AssignSeat(11);
            seats.AssignSeat(12);

            bool[] isBot = PublicStateBuilder.BuildSeatOccupancy(seats, 6);

            Assert.That(isBot, Is.EqualTo(new[] { false, false, false, true, true, true }));
        }

        [Test]
        public void Occupancy_FlowsIntoTheDealtSeats([Values(3, 4, 6, 8)] int players)
        {
            MatchSetup setup = Setup(players);

            Assert.That(setup.GetPlayer(0).IsBot, Is.False);
            Assert.That(setup.GetPlayer(1).IsBot, Is.False);

            for (int seat = 2; seat < players; seat++)
                Assert.That(setup.GetPlayer(seat).IsBot, Is.True, "Seat {0} should be a bot.", seat);
        }

        [Test]
        public void Occupancy_OfflineBehaviourIsUnchangedWhenNoneIsGiven()
        {
            // The original constructor must still mean "local seat is the only human".
            MatchSetup setup = new MatchSetup(new MatchConfig(5, 1, 2, 7));

            for (int seat = 0; seat < 5; seat++)
                Assert.That(setup.GetPlayer(seat).IsBot, Is.EqualTo(seat != 2), "Seat {0}", seat);
        }

        [Test]
        public void Occupancy_MustCoverEverySeat()
        {
            Assert.Throws<System.ArgumentException>(
                () => new MatchSetup(new MatchConfig(4, 1, 0, 1), new[] { false, true }));
        }

        // ================================================================ dealing

        [Test]
        public void Host_DealsEachSeatThreeCardsFromOneDeck([Values(3, 4, 8)] int players)
        {
            MatchSetup setup = Setup(players);

            for (int seat = 0; seat < players; seat++)
                Assert.That(setup.GetPlayer(seat).DealtCards.Count, Is.EqualTo(3));

            Assert.That(setup.Deck.DrawCount, Is.EqualTo(52 - players * 3));
        }

        [Test]
        public void Host_DealsOnceAndTheDeckIsNeverDuplicatedForClients()
        {
            MatchSetup setup = Setup();

            // The public setup snapshot carries card data for presentation, but no deck.
            PublicSetupSnapshot snapshot = PublicStateBuilder.BuildSetupSnapshot(setup, 1);

            Assert.That(snapshot.PlayerCount, Is.EqualTo(4));
            Assert.That(typeof(PublicSetupSnapshot).GetFields()
                .Any(f => f.FieldType == typeof(CardDeck)), Is.False,
                "No deck may be replicated; only the host owns one.");
        }

        [Test]
        public void PublicMatchSnapshot_CarriesOnlyTheRemainingCountNotTheDrawPile()
        {
            Assert.That(typeof(PublicMatchSnapshot).GetFields()
                .Any(f => f.FieldType == typeof(CardDeck) || f.FieldType == typeof(PublicCard[])), Is.False,
                "The snapshot must not carry deck contents.");

            Assert.That(typeof(PublicMatchSnapshot).GetField("RemainingDrawCount"), Is.Not.Null);
        }

        // ================================================================ shield choice

        [Test]
        public void Human_ChoosesTheirOwnShieldThroughTheHost()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            int cardId = CardIdOf(setup, 1, 2);
            int seat;
            SetupRejection rejection = router.SelectShieldFromParticipant(ClientA, cardId, out seat);

            Assert.That(rejection, Is.EqualTo(SetupRejection.None));
            Assert.That(seat, Is.EqualTo(1));
            Assert.That(setup.GetPlayer(1).IsSelectionComplete, Is.True);
            Assert.That(setup.GetPlayer(1).ShieldCard.CardId, Is.EqualTo(cardId));
        }

        [Test]
        public void Human_CannotChooseAShieldForAnotherHumanSeat()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            // ClientA names a card that was dealt to the host's seat.
            int hostCardId = CardIdOf(setup, 0, 0);

            int seat;
            SetupRejection rejection = router.SelectShieldFromParticipant(ClientA, hostCardId, out seat);

            Assert.That(rejection, Is.EqualTo(SetupRejection.CardNotDealtToYou));
            Assert.That(seat, Is.EqualTo(1), "The sender is still resolved to its own seat.");
            Assert.That(setup.GetPlayer(0).IsSelectionComplete, Is.False, "The host's seat must be untouched.");
            Assert.That(setup.GetPlayer(1).IsSelectionComplete, Is.False, "A refused choice changes nothing.");
        }

        [Test]
        public void Human_CannotChooseACardThatDoesNotExist()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            int seat;
            Assert.That(router.SelectShieldFromParticipant(ClientA, 9999, out seat),
                Is.EqualTo(SetupRejection.CardNotDealtToYou));
            Assert.That(router.SelectShieldFromParticipant(ClientA, -1, out seat),
                Is.EqualTo(SetupRejection.CardNotDealtToYou));

            Assert.That(setup.GetPlayer(1).IsSelectionComplete, Is.False);
        }

        [Test]
        public void Human_CannotChooseTwice()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            int seat;
            router.SelectShieldFromParticipant(ClientA, CardIdOf(setup, 1, 0), out seat);
            PlayingCard chosen = setup.GetPlayer(1).ShieldCard;

            SetupRejection second = router.SelectShieldFromParticipant(ClientA, CardIdOf(setup, 1, 1), out seat);

            Assert.That(second, Is.EqualTo(SetupRejection.AlreadyChosen));
            Assert.That(setup.GetPlayer(1).ShieldCard, Is.EqualTo(chosen), "The first choice must stand.");
            Assert.That(router.AppliedCount, Is.EqualTo(1));
        }

        [Test]
        public void AParticipantWithNoSeatCannotChooseAnything()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            int seat;
            SetupRejection rejection = router.SelectShieldFromParticipant(Stranger, CardIdOf(setup, 0, 0), out seat);

            Assert.That(rejection, Is.EqualTo(SetupRejection.NoSeat));
            Assert.That(seat, Is.EqualTo(SeatRegistry.NoSeat));
            Assert.That(router.AppliedCount, Is.Zero);
        }

        [Test]
        public void AClientCannotChooseForABotSeat()
        {
            MatchSetup setup = Setup();

            // Seat a third participant on seat 2, which the match says is a bot.
            SeatRegistry seats = Seats();
            seats.AssignSeat(55);

            OnlineSetupRouter router = new OnlineSetupRouter(seats, setup);

            int seat;
            SetupRejection rejection = router.SelectShieldFromParticipant(55, CardIdOf(setup, 2, 0), out seat);

            Assert.That(rejection, Is.EqualTo(SetupRejection.BotSeat));
            Assert.That(setup.GetPlayer(2).IsSelectionComplete, Is.False);
        }

        // ================================================================ bots

        [Test]
        public void Bots_ChooseOnlyOnTheHostAndOnlyForThemselves()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            router.RunBotSelections();

            Assert.That(setup.GetPlayer(2).IsSelectionComplete, Is.True);
            Assert.That(setup.GetPlayer(3).IsSelectionComplete, Is.True);

            Assert.That(setup.GetPlayer(0).IsSelectionComplete, Is.False, "Humans still choose for themselves.");
            Assert.That(setup.GetPlayer(1).IsSelectionComplete, Is.False);
        }

        [Test]
        public void Bots_PickOneOfTheirOwnThreeCards()
        {
            MatchSetup setup = Setup();
            new OnlineSetupRouter(Seats(), setup).RunBotSelections();

            for (int seat = 2; seat < 4; seat++)
            {
                PlayerSetupState bot = setup.GetPlayer(seat);
                Assert.That(bot.WasDealt(bot.ShieldCard), Is.True, "Seat {0} chose a foreign card.", seat);
                Assert.That(bot.HealthCards.Count, Is.EqualTo(2));
                Assert.That(bot.StartingHealth,
                    Is.EqualTo(bot.HealthCards[0].Value + bot.HealthCards[1].Value));
            }
        }

        // ================================================================ match start

        [Test]
        public void TheMatchStartsOnlyAfterEverySeatHasChosen()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            router.RunBotSelections();
            Assert.That(setup.InitialSetupComplete, Is.False, "Two humans still owe a choice.");

            int seat;
            router.SelectShieldFromParticipant(HostClient, CardIdOf(setup, 0, 0), out seat);
            Assert.That(setup.InitialSetupComplete, Is.False);

            router.SelectShieldFromParticipant(ClientA, CardIdOf(setup, 1, 0), out seat);
            Assert.That(setup.InitialSetupComplete, Is.True);

            // Only now can the one authoritative match be created.
            ShieldsMatch match = new ShieldsMatch(setup);
            Assert.That(match.PlayerCount, Is.EqualTo(4));
            Assert.That(match.Players[0].IsBot, Is.False);
            Assert.That(match.Players[2].IsBot, Is.True);
        }

        [Test]
        public void StartingTheMatchKeepsTheSameDeckAndCards()
        {
            MatchSetup setup = Setup();
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(), setup);

            router.RunBotSelections();
            int seat;
            router.SelectShieldFromParticipant(HostClient, CardIdOf(setup, 0, 0), out seat);
            router.SelectShieldFromParticipant(ClientA, CardIdOf(setup, 1, 0), out seat);

            int drawBefore = setup.Deck.DrawCount;
            ShieldsMatch match = new ShieldsMatch(setup);

            Assert.That(match.Deck, Is.SameAs(setup.Deck), "Turn play continues the same deck.");
            Assert.That(match.Deck.DrawCount, Is.EqualTo(drawBefore), "Nothing is redealt.");
        }

        // ================================================================ card accounting

        [Test]
        public void EveryPhysicalCardIsAccountedForThroughSetup([Values(3, 6, 8)] int players)
        {
            MatchSetup setup = Setup(players);
            OnlineSetupRouter router = new OnlineSetupRouter(Seats(players), setup);

            router.RunBotSelections();

            AssertCardsAccountedFor(setup, players, 52);
        }

        [Test]
        public void TwoDecksStillAccountForEveryCard()
        {
            bool[] isBot = PublicStateBuilder.BuildSeatOccupancy(Seats(6), 6);
            MatchSetup setup = new MatchSetup(new MatchConfig(6, 2, 0, 5), isBot);

            new OnlineSetupRouter(Seats(6), setup).RunBotSelections();

            AssertCardsAccountedFor(setup, 6, 104);
        }

        [Test]
        public void ASnapshotCopiesCardDataWithoutTakingOwnership()
        {
            MatchSetup setup = Setup();

            int drawBefore = setup.Deck.DrawCount;
            int inPlayBefore = setup.Deck.InPlayCount;

            PublicStateBuilder.BuildSetupSnapshot(setup, 1);

            // Building a snapshot is a projection, never a move of a physical card.
            Assert.That(setup.Deck.DrawCount, Is.EqualTo(drawBefore));
            Assert.That(setup.Deck.InPlayCount, Is.EqualTo(inPlayBefore));
        }

        static void AssertCardsAccountedFor(MatchSetup setup, int players, int expectedTotal)
        {
            HashSet<int> seen = new HashSet<int>();

            foreach (PlayingCard card in setup.Deck.DrawPile)
                Assert.That(seen.Add(card.CardId), Is.True, "Card {0} appears twice.", card.CardId);

            foreach (PlayingCard card in setup.Deck.DiscardPile)
                Assert.That(seen.Add(card.CardId), Is.True, "Card {0} appears twice.", card.CardId);

            for (int seat = 0; seat < players; seat++)
            {
                foreach (PlayingCard card in setup.GetPlayer(seat).DealtCards)
                    Assert.That(seen.Add(card.CardId), Is.True, "Card {0} is in two places.", card.CardId);
            }

            Assert.That(seen.Count, Is.EqualTo(expectedTotal),
                "Expected {0} distinct physical cards, found {1}.", expectedTotal, seen.Count);
            Assert.That(setup.Deck.TotalCardCount, Is.EqualTo(expectedTotal));
        }
    }
}
