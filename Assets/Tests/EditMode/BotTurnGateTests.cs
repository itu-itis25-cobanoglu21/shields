using NUnit.Framework;
using Shields.Bots;
using Shields.Game;

namespace Shields.Game.Tests
{
    /// <summary>
    /// EditMode tests for the turn-controller decision logic: who controls the current
    /// seat, and that a delayed bot action can only ever fire for the bot and turn it was
    /// scheduled for. Time is passed in explicitly, so nothing here waits.
    /// </summary>
    public class BotTurnGateTests
    {
        const int Local = 0;
        const double Delay = 5.0;

        /// <summary>Four seats, seat 0 local, player <paramref name="start"/> starts clockwise.</summary>
        static ShieldsMatch Match(int start)
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(4, 1, Local, 31));
            setup.RunBotSelections();
            setup.SelectShieldFor(Local, setup.LocalPlayer.DealtCards[0]);
            return new ShieldsMatch(setup.Deck, setup.Players, start);
        }

        /// <summary>Resolves the current turn with a harmless legal action.</summary>
        static void PassTurn(ShieldsMatch match)
        {
            int current = match.CurrentPlayerIndex;
            Assert.IsTrue(match.Submit(TurnActionRequest.ReplaceShield(current, current)).Accepted);
        }

        [Test]
        public void LocalSeatIsClassifiedAsHuman()
        {
            Assert.AreEqual(SeatControl.LocalHuman, BotTurnGate.ControlOf(Match(Local), Local));
        }

        [Test]
        public void OtherSeatsAreClassifiedAsBots([Values(1, 2, 3)] int seat)
        {
            Assert.AreEqual(SeatControl.Bot, BotTurnGate.ControlOf(Match(seat), Local));
        }

        [Test]
        public void ClassificationFollowsTheConfiguredLocalSeatNotSeatZero()
        {
            MatchSetup setup = new MatchSetup(new MatchConfig(4, 1, 2, 31));
            setup.RunBotSelections();
            setup.SelectShieldFor(2, setup.LocalPlayer.DealtCards[0]);

            Assert.AreEqual(SeatControl.LocalHuman,
                BotTurnGate.ControlOf(new ShieldsMatch(setup.Deck, setup.Players, 2), 2));
            Assert.AreEqual(SeatControl.Bot,
                BotTurnGate.ControlOf(new ShieldsMatch(setup.Deck, setup.Players, 0), 2));
        }

        [Test]
        public void HumanTurnNeverYieldsABotActionNoMatterHowLongItLasts()
        {
            ShieldsMatch match = Match(Local);
            BotTurnGate gate = new BotTurnGate();

            for (double now = 0; now < 3600; now += 0.5)
            {
                int bot;
                Assert.IsFalse(gate.TryTakeDueBot(match, Local, 0, now, now + Delay, out bot),
                    "The bot policy must never be reached on the local human's turn.");
                Assert.IsFalse(gate.IsScheduled);
            }

            Assert.AreEqual(Local, match.CurrentPlayerIndex, "Nothing may consume the human turn.");
        }

        [Test]
        public void BotActionIsDueOnceAfterItsDelay()
        {
            ShieldsMatch match = Match(1);
            BotTurnGate gate = new BotTurnGate();
            int bot;

            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 0, 0, Delay, out bot), "First call only schedules.");
            Assert.IsTrue(gate.IsScheduled);
            Assert.AreEqual(1, gate.ScheduledPlayer);

            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 0, 4.9, 99, out bot), "Not due yet.");
            Assert.IsTrue(gate.TryTakeDueBot(match, Local, 0, 5.0, 99, out bot));
            Assert.AreEqual(1, bot);

            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 0, 5.0, 99, out bot), "Only one action per schedule.");
        }

        [Test]
        public void TurnChangingToTheHumanCancelsThePendingBotAction()
        {
            // Player 3 is the last bot before the local seat (clockwise).
            ShieldsMatch match = Match(3);
            BotTurnGate gate = new BotTurnGate();
            int bot;

            gate.TryTakeDueBot(match, Local, 0, 0, Delay, out bot);
            Assert.IsTrue(gate.IsScheduled);

            PassTurn(match); // now the human's turn
            Assert.AreEqual(Local, match.CurrentPlayerIndex);

            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 1, 100, 105, out bot));
            Assert.IsFalse(gate.IsScheduled, "A stale bot action must be dropped on the human turn.");
            Assert.AreEqual(Local, match.CurrentPlayerIndex);
        }

        [Test]
        public void StaleActionCannotFireForTheNextBotOrALaterTurn()
        {
            ShieldsMatch match = Match(1);
            BotTurnGate gate = new BotTurnGate();
            int bot;

            gate.TryTakeDueBot(match, Local, 0, 0, Delay, out bot); // scheduled for bot 1, turn 0
            PassTurn(match); // bot 2's turn, turn 1

            // At bot 1's due time the old schedule must not fire; bot 2 gets a fresh one.
            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 1, Delay, Delay + Delay, out bot));
            Assert.AreEqual(2, gate.ScheduledPlayer);

            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 1, Delay + 4.9, 999, out bot));
            Assert.IsTrue(gate.TryTakeDueBot(match, Local, 1, Delay + Delay, 999, out bot));
            Assert.AreEqual(2, bot);
        }

        [Test]
        public void SameSeatOnANewTurnIdIsRescheduledNotFired()
        {
            ShieldsMatch match = Match(1);
            BotTurnGate gate = new BotTurnGate();
            int bot;

            gate.TryTakeDueBot(match, Local, 0, 0, Delay, out bot);

            // A different turn id (e.g. a restarted match) must not reuse the old timer.
            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 7, 50, 55, out bot));
            Assert.IsFalse(gate.TryTakeDueBot(match, Local, 7, 54, 999, out bot));
            Assert.IsTrue(gate.TryTakeDueBot(match, Local, 7, 55, 999, out bot));
        }

        [Test]
        public void NoMatchMeansNoController()
        {
            Assert.AreEqual(SeatControl.None, BotTurnGate.ControlOf(null, Local));
        }
    }
}
