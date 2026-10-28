using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shields.Cards;

namespace Shields.Game.Tests
{
    /// <summary>
    /// The clockwise-only rule (TASK 026, SHIELDS_RULES.md section 7), checked against the
    /// real seat geometry: <see cref="TableGeometry"/> is what the table layout itself uses
    /// to place every seat, so the engine's turn order is verified to move clockwise on
    /// the actual table as seen from above, not just to add one to an index.
    /// </summary>
    public class ClockwiseTurnTests
    {
        /// <summary>
        /// Angle of a floor point seen from above, in the usual maths sense (x right, z up
        /// the screen, counter-clockwise positive). A clockwise step makes it decrease.
        /// </summary>
        static double TopViewAngle(int seat, int seats)
        {
            double x, z;
            TableGeometry.SeatFloorPosition(seat, seats, 1.0, out x, out z);
            return Math.Atan2(z, x) * 180.0 / Math.PI;
        }

        static double SignedStep(double from, double to)
        {
            double delta = (to - from) % 360.0;
            if (delta > 180.0) delta -= 360.0;
            if (delta <= -180.0) delta += 360.0;
            return delta;
        }

        [Test]
        public void NextSeatIsClockwiseOnTheRealTable([Values(3, 4, 5, 6, 7, 8)] int seats)
        {
            for (int seat = 0; seat < seats; seat++)
            {
                int next = TableGeometry.NextClockwise(seat, seats);
                double step = SignedStep(TopViewAngle(seat, seats), TopViewAngle(next, seats));

                Assert.Less(step, 0.0, "Seat {0} -> {1} of {2} must turn clockwise seen from above.", seat, next, seats);
                Assert.AreEqual(-360.0 / seats, step, 1e-9, "One even seat step.");
            }
        }

        [Test]
        public void SeatZeroSitsAtSixOClockAndTheNextAtItsLeft()
        {
            double x0, z0, x1, z1;
            TableGeometry.SeatFloorPosition(0, 4, 1.0, out x0, out z0);
            TableGeometry.SeatFloorPosition(1, 4, 1.0, out x1, out z1);

            Assert.AreEqual(0.0, x0, 1e-9);
            Assert.AreEqual(-1.0, z0, 1e-9, "Seat 0 is nearest the viewer (south).");
            Assert.AreEqual(-1.0, x1, 1e-9, "Seat 1 is west: 6 o'clock to 9 o'clock is clockwise.");
            Assert.AreEqual(0.0, z1, 1e-9);
        }

        // ================================================================ engine

        static ShieldsMatch Match(int seats, int start, out CardDeck deck)
        {
            deck = new CardDeck(1);
            deck.Shuffle();
            PlayerSetupState[] players = new PlayerSetupState[seats];

            for (int seat = 0; seat < seats; seat++)
            {
                List<PlayingCard> cards = new List<PlayingCard>();
                for (int i = 0; i < 3; i++)
                {
                    PlayingCard card;
                    Assert.IsTrue(deck.TryDraw(out card));
                    cards.Add(card);
                }

                players[seat] = new PlayerSetupState(seat, seat != 0, cards);
                players[seat].SelectShieldAt(2);
            }

            return new ShieldsMatch(deck, players, start);
        }

        static List<int> PlayTurns(ShieldsMatch match, int turns)
        {
            List<int> order = new List<int>();
            for (int i = 0; i < turns; i++)
            {
                int actor = match.CurrentPlayerIndex;
                order.Add(actor);
                Assert.IsTrue(match.Submit(TurnActionRequest.ReplaceShield(actor, actor)).Accepted);
            }

            return order;
        }

        [Test]
        public void EngineTurnOrderFollowsTheTableClockwise([Values(3, 6, 8)] int seats, [Values(0, 2)] int start)
        {
            CardDeck deck;
            ShieldsMatch match = Match(seats, start, out deck);

            List<int> order = PlayTurns(match, seats + 2);

            for (int i = 1; i < order.Count; i++)
            {
                Assert.AreEqual(TableGeometry.NextClockwise(order[i - 1], seats), order[i]);
                Assert.Less(SignedStep(TopViewAngle(order[i - 1], seats), TopViewAngle(order[i], seats)), 0.0,
                    "{0} players: turn {1} must move clockwise on the table.", seats, i);
            }

            Assert.AreEqual(TurnDirection.Clockwise, match.Direction);
        }

        [Test]
        public void DeadPlayersAreSkippedWhileStayingClockwise()
        {
            CardDeck deck;
            ShieldsMatch match = Match(6, 0, out deck);

            // Eliminate seats 1 and 4 through the engine's internal state (rules for how
            // they died are covered elsewhere; this is only about who plays next).
            match.Players[1].IsAlive = false;
            match.Players[4].IsAlive = false;

            CollectionAssert.AreEqual(new[] { 0, 2, 3, 5, 0, 2, 3, 5 }, PlayTurns(match, 8));
        }

        [Test]
        public void RandomStartIsIndependentOfDirection([Values(3, 6, 8)] int seats)
        {
            HashSet<int> starts = new HashSet<int>();
            Random random = new Random(seats * 31);

            for (int i = 0; i < 200; i++)
            {
                CardDeck deck = new CardDeck(1);
                deck.Shuffle();
                PlayerSetupState[] players = new PlayerSetupState[seats];
                for (int seat = 0; seat < seats; seat++)
                {
                    List<PlayingCard> cards = new List<PlayingCard>();
                    for (int c = 0; c < 3; c++) { PlayingCard card; deck.TryDraw(out card); cards.Add(card); }
                    players[seat] = new PlayerSetupState(seat, true, cards);
                    players[seat].SelectShieldAt(0);
                }

                ShieldsMatch match = new ShieldsMatch(deck, players, random);
                starts.Add(match.StartingPlayerIndex);
                Assert.AreEqual(TurnDirection.Clockwise, match.Direction);
            }

            Assert.AreEqual(seats, starts.Count, "Every seat can start; only the direction is fixed.");
        }
    }
}
