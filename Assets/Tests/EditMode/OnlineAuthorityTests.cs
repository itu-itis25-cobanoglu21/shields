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
    /// Who is allowed to ask for what (TASK 009 sections 20, 42 and 43).
    ///
    /// The routers are plain C#, so the whole authority path is provable here without a
    /// Relay connection: the actor always comes from the sender's seat, never from the
    /// payload, and a refused request reaches neither the deck nor the engine.
    /// </summary>
    public class OnlineAuthorityTests
    {
        const ulong HostClient = 0;
        const ulong ClientA = 1;
        const ulong ClientB = 2;
        const ulong Stranger = 77;

        // ================================================================ helpers

        static PlayingCard Take(CardDeck deck, CardRank rank)
        {
            PlayingCard card;
            Assert.IsTrue(deck.TryTakeFromDrawPile((int)rank, out card), "No {0} left.", rank);
            return card;
        }

        static PlayerSetupState Seat(CardDeck deck, int index, bool isBot, CardRank left, CardRank right, CardRank shield)
        {
            List<PlayingCard> cards = new List<PlayingCard> { Take(deck, left), Take(deck, right), Take(deck, shield) };
            PlayerSetupState seat = new PlayerSetupState(index, isBot, cards);
            seat.SelectShieldAt(2);
            return seat;
        }

        static List<PlayingCard> Stack(CardDeck deck, params CardRank[] drawOrder)
        {
            List<PlayingCard> stacked = new List<PlayingCard>();

            for (int i = drawOrder.Length - 1; i >= 0; i--)
            {
                CardRank rank = drawOrder[i];
                PlayingCard card = deck.DrawPile.First(c => c.Rank == rank && !stacked.Contains(c));
                deck.MoveToTopOfDrawPile(card);
                stacked.Add(card);
            }

            stacked.Reverse();
            return stacked;
        }

        /// <summary>Seats 0 and 1 are humans, seat 2 is a bot. Seat 0 starts.</summary>
        static ShieldsMatch Table(CardDeck deck, int startingPlayer = 0)
        {
            return new ShieldsMatch(deck, new[]
            {
                Seat(deck, 0, false, CardRank.King, CardRank.Jack, CardRank.Five),
                Seat(deck, 1, false, CardRank.Ten, CardRank.Nine, CardRank.Four),
                Seat(deck, 2, true, CardRank.Eight, CardRank.Seven, CardRank.Three)
            }, startingPlayer);
        }

        /// <summary>Host holds seat 0, one client holds seat 1. Seat 2 is nobody's.</summary>
        static SeatRegistry Seats()
        {
            SeatRegistry seats = new SeatRegistry(3);
            seats.AssignSeat(HostClient);
            seats.AssignSeat(ClientA);
            return seats;
        }

        static void AssertNothingHappened(ShieldsMatch match, RoutedAction routed, int drawBefore, int currentBefore)
        {
            Assert.That(routed.Accepted, Is.False);
            Assert.That(match.Deck.DrawCount, Is.EqualTo(drawBefore), "A refused request must draw no card.");
            Assert.That(match.CurrentPlayerIndex, Is.EqualTo(currentBefore), "A refused request must not pass the turn.");
        }

        // ================================================================ sender identity

        [Test]
        public void Actor_IsDerivedFromTheSenderNotThePayload()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 1);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            Stack(deck, CardRank.King);

            // ClientA holds seat 1 and it is seat 1's turn.
            RoutedAction routed = router.SubmitFromParticipant(ClientA, TurnActionType.Attack, 0);

            Assert.That(routed.Accepted, Is.True, "Rejected: {0}", routed.Rejection);
            Assert.That(routed.ActorIndex, Is.EqualTo(1));
            Assert.That(routed.Result.ActorIndex, Is.EqualTo(1));
        }

        [Test]
        public void AClientCannotActAsAnotherHumanSeat()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            int draw = match.Deck.DrawCount;

            // It is seat 0's turn (the host). ClientA owns seat 1 and may not act now,
            // and there is no way for it to name seat 0 at all.
            RoutedAction routed = router.SubmitFromParticipant(ClientA, TurnActionType.Attack, 2);

            Assert.That(routed.ActorIndex, Is.EqualTo(1), "The sender is always resolved to its own seat.");
            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.NotYourTurn));
            AssertNothingHappened(match, routed, draw, 0);
        }

        [Test]
        public void AClientCannotActForABotSeat()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 2);

            // Give the stranger the bot's seat index by seating them there, which the
            // real registry would never do; the router must still refuse.
            SeatRegistry seats = new SeatRegistry(3);
            seats.AssignSeat(HostClient);
            seats.AssignSeat(ClientA);
            seats.AssignSeat(ClientB); // lands on seat 2, which the match says is a bot

            OnlineTurnRouter router = new OnlineTurnRouter(seats, match);
            int draw = match.Deck.DrawCount;

            RoutedAction routed = router.SubmitFromParticipant(ClientB, TurnActionType.Attack, 0);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.InvalidPlayer));
            AssertNothingHappened(match, routed, draw, 2);
            Assert.That(router.SubmitCount, Is.Zero, "A bot seat request must never reach the engine.");
        }

        [Test]
        public void AParticipantWithNoSeatIsRefused()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            int draw = match.Deck.DrawCount;
            RoutedAction routed = router.SubmitFromParticipant(Stranger, TurnActionType.Attack, 1);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.InvalidPlayer));
            Assert.That(routed.ActorIndex, Is.EqualTo(SeatRegistry.NoSeat));
            AssertNothingHappened(match, routed, draw, 0);
            Assert.That(router.SubmitCount, Is.Zero);
        }

        [Test]
        public void OutOfTurnSenderIsRefusedWithoutDisconnectingAnyone()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            int draw = match.Deck.DrawCount;
            RoutedAction routed = router.SubmitFromParticipant(ClientA, TurnActionType.Charge, -1);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.NotYourTurn));
            AssertNothingHappened(match, routed, draw, 0);

            // An ordinary stale request: the same client may act normally afterwards.
            Stack(deck, CardRank.Two);
            Assert.That(router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 1).Accepted, Is.True);
        }

        [Test]
        public void EliminatedSenderIsRefused()
        {
            CardDeck deck = new CardDeck(1, 7);

            // Seat 1 is built fragile on purpose: Health 3 behind a Shield of 1, so a
            // single King attack removes it outright.
            ShieldsMatch match = new ShieldsMatch(deck, new[]
            {
                Seat(deck, 0, false, CardRank.King, CardRank.Jack, CardRank.Five),
                Seat(deck, 1, false, CardRank.Ace, CardRank.Two, CardRank.Ace),
                Seat(deck, 2, true, CardRank.Eight, CardRank.Seven, CardRank.Three)
            }, 0);

            Stack(deck, CardRank.King);
            match.Submit(TurnActionRequest.Attack(0, 1));

            Assert.That(match.Players[1].IsAlive, Is.False, "Seat 1 should be eliminated by that attack.");

            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);
            RoutedAction routed = router.SubmitFromParticipant(ClientA, TurnActionType.Attack, 0);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.PlayerEliminated));
            Assert.That(router.SubmitCount, Is.Zero);
        }

        // ================================================================ exactly once

        [Test]
        public void AValidRequestReachesTheEngineExactlyOnce()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 1);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            // A Two cannot beat seat 0's Shield of Five, so the attack is blocked. That
            // keeps exactly one card leaving the draw pile: a successful hit would also
            // pull a physical Health card to represent the new total (rules 15 and 16).
            Stack(deck, CardRank.Two);
            int draw = match.Deck.DrawCount;

            RoutedAction routed = router.SubmitFromParticipant(ClientA, TurnActionType.Attack, 0);

            Assert.That(routed.Accepted, Is.True);
            Assert.That(routed.Result.Damage, Is.Zero, "This attack is meant to be blocked.");
            Assert.That(router.SubmitCount, Is.EqualTo(1), "One request must produce exactly one submit.");
            Assert.That(match.Deck.DrawCount, Is.EqualTo(draw - 1), "Exactly one card should have been drawn.");
        }

        [Test]
        public void AHostLocalHumanActionAlsoResolvesExactlyOnce()
        {
            // The host is both server and client, so this is the path most at risk of
            // being applied twice.
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            // Blocked by seat 1's Shield of Four, so exactly one card moves.
            Stack(deck, CardRank.Three);
            int draw = match.Deck.DrawCount;

            RoutedAction routed = router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 1);

            Assert.That(routed.Accepted, Is.True);
            Assert.That(routed.ActorIndex, Is.EqualTo(0));
            Assert.That(routed.Result.Damage, Is.Zero, "This attack is meant to be blocked.");
            Assert.That(router.SubmitCount, Is.EqualTo(1));
            Assert.That(match.Deck.DrawCount, Is.EqualTo(draw - 1));
        }

        [Test]
        public void ARepeatedRequestForTheSameTurnIsRefusedTheSecondTime()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            Stack(deck, CardRank.King);
            Assert.That(router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 1).Accepted, Is.True);

            int draw = match.Deck.DrawCount;
            int current = match.CurrentPlayerIndex;

            // A duplicate arriving after the turn moved on changes nothing.
            RoutedAction second = router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 1);

            Assert.That(second.Rejection, Is.EqualTo(ActionRejection.NotYourTurn));
            AssertNothingHappened(match, second, draw, current);
            Assert.That(router.SubmitCount, Is.EqualTo(1));
        }

        // ================================================================ targets

        [Test]
        public void AnInvalidTargetIsRefusedByTheEngineAndChangesNothing()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            int draw = match.Deck.DrawCount;
            RoutedAction routed = router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 99);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.InvalidTarget));
            AssertNothingHappened(match, routed, draw, 0);

            // It did reach the engine, which is the authority on targets.
            Assert.That(router.SubmitCount, Is.EqualTo(1));
        }

        [Test]
        public void SelfAttackIsRefusedAndChangesNothing()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            int draw = match.Deck.DrawCount;
            RoutedAction routed = router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 0);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.SelfTargetNotAllowed));
            AssertNothingHappened(match, routed, draw, 0);
        }

        // ================================================================ bots

        [Test]
        public void BotsGoThroughTheSameRouterAndReachTheSameEngine()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 2);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            Stack(deck, CardRank.King);
            RoutedAction routed = router.SubmitBot(TurnActionRequest.Attack(2, 0));

            Assert.That(routed.Accepted, Is.True, "Rejected: {0}", routed.Rejection);
            Assert.That(routed.ActorIndex, Is.EqualTo(2));
            Assert.That(router.SubmitCount, Is.EqualTo(1));
        }

        [Test]
        public void TheHostCannotDriveAHumanSeatAsIfItWereABot()
        {
            CardDeck deck = new CardDeck(1, 7);
            ShieldsMatch match = Table(deck, 0);
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), match);

            int draw = match.Deck.DrawCount;
            RoutedAction routed = router.SubmitBot(TurnActionRequest.Attack(0, 1));

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.InvalidPlayer));
            AssertNothingHappened(match, routed, draw, 0);
            Assert.That(router.SubmitCount, Is.Zero);
        }

        // ================================================================ no match

        [Test]
        public void RequestsAreRefusedWhenNoMatchIsRunning()
        {
            OnlineTurnRouter router = new OnlineTurnRouter(Seats(), null);

            RoutedAction routed = router.SubmitFromParticipant(HostClient, TurnActionType.Attack, 1);

            Assert.That(routed.Rejection, Is.EqualTo(ActionRejection.MatchOver));
            Assert.That(router.SubmitCount, Is.Zero);
        }

        [Test]
        public void EveryRejectionHasAReasonThatRevealsNoHiddenState()
        {
            foreach (ActionRejection rejection in System.Enum.GetValues(typeof(ActionRejection)))
            {
                string text = OnlineTurnRouter.DescribeRejection(rejection);

                if (rejection == ActionRejection.None) Assert.That(text, Is.Empty);
                else Assert.That(text, Is.Not.Empty, "{0} needs a reason.", rejection);

                Assert.That(text.ToLowerInvariant(), Does.Not.Contain("charge"),
                    "A rejection reason must not mention hidden Charge state.");
            }
        }
    }
}
