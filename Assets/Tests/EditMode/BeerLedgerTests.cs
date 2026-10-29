using System.Collections.Generic;
using NUnit.Framework;
using Shields.Session;
using Shields.Social;

namespace Shields.Game.Tests
{
    /// <summary>
    /// The authoritative beer drinking state (TASK 027): identity, ownership, remaining
    /// amount, one drink per player, start/stop fill math, generation safety and reset.
    /// Plain C# with an explicit clock; no transforms or animation.
    /// </summary>
    public class BeerLedgerTests
    {
        const int Gen = 3;
        const ulong HostClient = 0;
        const ulong ClientA = 1;
        const ulong Stranger = 77;

        // 5 s per beer, 0.5 s lead-in, so the numbers are easy to check by hand.
        static BeerLedger Ledger() { return new BeerLedger(Gen, 5f, 0.5f); }

        /// <summary>Seats 0 and 1 are humans, the rest bots, everyone alive and present.</summary>
        static BeerSeat[] Roster(int seats)
        {
            BeerSeat[] roster = new BeerSeat[seats];
            for (int i = 0; i < seats; i++) roster[i] = new BeerSeat(true, true, i >= 2);
            return roster;
        }

        static BeerDrinkStart Start(BeerLedger ledger, int seat, BeerId beer, double now)
        {
            return ledger.TryStart(seat, beer, Gen, Roster(4), now);
        }

        // ================================================================ identity and ownership

        [Test]
        public void BeerIdComesFromOrderAndSeat()
        {
            Assert.AreEqual(new BeerId(4, 2), new BeerId(4, 2));
            Assert.AreNotEqual(new BeerId(4, 2), new BeerId(4, 1));
            Assert.AreNotEqual(new BeerId(4, 2), new BeerId(5, 2));
            Assert.IsFalse(new BeerId(BeerOrderQueue.NoOrder, 1).IsValid);
        }

        [Test]
        public void DeliveredBeerStartsFullAndRegistersOnce()
        {
            BeerLedger ledger = Ledger();

            Assert.IsTrue(ledger.Register(new BeerId(1, 0)));
            Assert.IsFalse(ledger.Register(new BeerId(1, 0)), "duplicate");
            Assert.IsFalse(ledger.Register(new BeerId(BeerOrderQueue.NoOrder, 0)), "invalid");

            Assert.AreEqual(1f, ledger.FillAt(new BeerId(1, 0), 100.0));
            Assert.AreEqual(0f, ledger.FillAt(new BeerId(9, 0), 100.0), "unknown beer reads empty");
        }

        [Test]
        public void OnlyTheOwnerSeatMayDrink()
        {
            BeerLedger ledger = Ledger();
            ledger.Register(new BeerId(1, 0));

            Assert.AreEqual(BeerDrinkRejection.NotOwner, Start(ledger, 1, new BeerId(1, 0), 0.0).Rejection);
            Assert.AreEqual(BeerDrinkRejection.UnknownBeer, Start(ledger, 0, new BeerId(2, 0), 0.0).Rejection);
            Assert.IsTrue(Start(ledger, 0, new BeerId(1, 0), 0.0).Accepted);
        }

        [Test]
        public void DrinkerComesFromTheSendersSeatNeverFromTheClientId()
        {
            // Client 1 sits in seat 0 and the host in seat 1, so clientId != seat.
            SeatRegistry seats = new SeatRegistry(4);
            seats.AssignSeat(ClientA);
            seats.AssignSeat(HostClient);

            BeerLedger ledger = Ledger();
            ledger.Register(new BeerId(1, 0));
            ledger.Register(new BeerId(1, 1));

            // Client 1 cannot drink seat 1's beer, even though its id is 1.
            Assert.AreEqual(BeerDrinkRejection.NotOwner,
                ledger.TryStartFromParticipant(seats, ClientA, new BeerId(1, 1), Gen, Roster(4), 0.0).Rejection);

            Assert.IsTrue(ledger.TryStartFromParticipant(seats, ClientA, new BeerId(1, 0), Gen, Roster(4), 0.0).Accepted);

            Assert.AreEqual(BeerDrinkRejection.InvalidDrinker,
                ledger.TryStartFromParticipant(seats, Stranger, new BeerId(1, 1), Gen, Roster(4), 0.0).Rejection);
            Assert.AreEqual(BeerDrinkRejection.InvalidDrinker,
                ledger.TryStartFromParticipant(null, HostClient, new BeerId(1, 1), Gen, Roster(4), 0.0).Rejection);

            // And nobody may stop another seat's drink.
            BeerDrinkStop stop;
            Assert.IsFalse(ledger.TryStopFromParticipant(seats, HostClient, new BeerId(1, 0), Gen, 1.0,
                BeerDrinkStopReason.Released, out stop));
            Assert.IsTrue(ledger.IsDrinking(0));
        }

        [Test]
        public void DeadAbsentAndBotSeatsCannotDrink()
        {
            BeerLedger ledger = Ledger();
            ledger.Register(new BeerId(1, 0));
            ledger.Register(new BeerId(1, 2));

            BeerSeat[] roster = Roster(4);
            roster[0] = new BeerSeat(true, false, false);
            Assert.AreEqual(BeerDrinkRejection.DrinkerUnavailable, ledger.TryStart(0, new BeerId(1, 0), Gen, roster, 0.0).Rejection);

            roster[0] = new BeerSeat(false, true, false);
            Assert.AreEqual(BeerDrinkRejection.DrinkerUnavailable, ledger.TryStart(0, new BeerId(1, 0), Gen, roster, 0.0).Rejection);

            Assert.AreEqual(BeerDrinkRejection.DrinkerUnavailable, Start(ledger, 2, new BeerId(1, 2), 0.0).Rejection, "bot");
        }

        // ================================================================ amounts

        [Test]
        public void StartStopComputesTheAuthoritativeFill()
        {
            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);

            BeerDrinkStart start = Start(ledger, 0, beer, 10.0);
            Assert.IsTrue(start.Accepted);
            Assert.AreEqual(1f, start.StartFill);
            Assert.AreEqual(10.0, start.StartTime);
            Assert.AreEqual(0.5f, start.LeadInSeconds);
            Assert.AreEqual(0.2f, start.RatePerSecond, 1e-6f);

            // Nothing drains during the reach.
            Assert.AreEqual(1f, ledger.FillAt(beer, 10.4), 1e-5f);

            // 10.5 lead-in end, then 2 s of drinking at 0.2 per second.
            BeerDrinkStop stop;
            Assert.IsTrue(ledger.TryStop(0, beer, Gen, 12.5, BeerDrinkStopReason.Released, out stop));
            Assert.AreEqual(0.6f, stop.FinalFill, 1e-5f);
            Assert.AreEqual(BeerDrinkStopReason.Released, stop.Reason);
            Assert.AreEqual(0.6f, start.FillAt(12.5), 1e-5f, "clients derive the same curve");

            // Remaining beer persists between drinks.
            Assert.AreEqual(0.6f, ledger.FillAt(beer, 1000.0), 1e-5f);
            Assert.IsFalse(ledger.IsDrinking(0));
        }

        [Test]
        public void PartialDrinkResumesFromTheRemainingAmount()
        {
            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);

            BeerDrinkStop stop;
            Start(ledger, 0, beer, 0.0);
            ledger.TryStop(0, beer, Gen, 1.5, BeerDrinkStopReason.Released, out stop); // 1 s of drinking

            BeerDrinkStart again = Start(ledger, 0, beer, 20.0);
            Assert.IsTrue(again.Accepted);
            Assert.AreEqual(0.8f, again.StartFill, 1e-5f);

            ledger.TryStop(0, beer, Gen, 21.5, BeerDrinkStopReason.Released, out stop);
            Assert.AreEqual(0.6f, stop.FinalFill, 1e-5f);
        }

        [Test]
        public void RemainingAmountIsClampedToZeroAndOne()
        {
            Assert.AreEqual(0f, BeerLedger.Deplete(0.3f, 0.2f, 100.0));
            Assert.AreEqual(1f, BeerLedger.Deplete(1.5f, 0.2f, 0.0));
            Assert.AreEqual(1f, BeerLedger.Deplete(1f, 0.2f, -3.0), "negative time drains nothing");
            Assert.AreEqual(0f, BeerLedger.Clamp01(float.NaN));

            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);
            Start(ledger, 0, beer, 0.0);

            BeerDrinkStop stop;
            ledger.TryStop(0, beer, Gen, 60.0, BeerDrinkStopReason.Released, out stop);
            Assert.AreEqual(0f, stop.FinalFill);
        }

        [Test]
        public void FullBeerEmptiesAfterTheConfiguredDuration()
        {
            BeerLedger ledger = new BeerLedger(Gen, 5f, 0.7f);
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);
            Start(ledger, 0, beer, 0.0);

            List<BeerDrinkStop> stopped = new List<BeerDrinkStop>();
            ledger.Tick(5.6, Roster(4), stopped);
            Assert.AreEqual(0, stopped.Count, "still drinking after 4.9 s of mouth time");

            ledger.Tick(5.71, Roster(4), stopped);
            Assert.AreEqual(1, stopped.Count);
            Assert.AreEqual(BeerDrinkStopReason.Empty, stopped[0].Reason);
            Assert.AreEqual(0f, stopped[0].FinalFill);
            Assert.IsFalse(ledger.IsDrinking(0));
        }

        [Test]
        public void EmptyBeerCannotStart()
        {
            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);

            BeerDrinkStop stop;
            Start(ledger, 0, beer, 0.0);
            ledger.TryStop(0, beer, Gen, 30.0, BeerDrinkStopReason.Released, out stop);

            Assert.AreEqual(BeerDrinkRejection.Empty, Start(ledger, 0, beer, 40.0).Rejection);
        }

        [Test]
        public void OnlyOneActiveDrinkPerPlayer()
        {
            BeerLedger ledger = Ledger();
            ledger.Register(new BeerId(1, 0));
            ledger.Register(new BeerId(2, 0));
            ledger.Register(new BeerId(2, 1));

            Assert.IsTrue(Start(ledger, 0, new BeerId(1, 0), 0.0).Accepted);
            Assert.AreEqual(BeerDrinkRejection.AlreadyDrinking, Start(ledger, 0, new BeerId(2, 0), 0.1).Rejection);
            Assert.AreEqual(BeerDrinkRejection.AlreadyDrinking, Start(ledger, 0, new BeerId(1, 0), 0.1).Rejection);

            // Another player drinks at the same time without trouble.
            Assert.IsTrue(Start(ledger, 1, new BeerId(2, 1), 0.2).Accepted);

            // A stop naming the wrong beer does nothing.
            BeerDrinkStop stop;
            Assert.IsFalse(ledger.TryStop(0, new BeerId(2, 0), Gen, 1.0, BeerDrinkStopReason.Released, out stop));
            Assert.IsTrue(ledger.IsDrinking(0));
        }

        [Test]
        public void OldestNonEmptyBeerIsChosenAndBeersStayIndependent()
        {
            BeerLedger ledger = Ledger();
            BeerId first = new BeerId(2, 0);
            BeerId second = new BeerId(5, 0);
            ledger.Register(second);
            ledger.Register(first);
            ledger.Register(new BeerId(1, 1));

            BeerId oldest;
            Assert.IsTrue(ledger.TryGetOldestDrinkable(0, 0.0, out oldest));
            Assert.AreEqual(first, oldest);

            // Drain the first completely.
            BeerDrinkStop stop;
            Start(ledger, 0, first, 0.0);
            ledger.TryStop(0, first, Gen, 10.0, BeerDrinkStopReason.Released, out stop);
            Assert.AreEqual(0f, ledger.FillAt(first, 10.0));
            Assert.AreEqual(1f, ledger.FillAt(second, 10.0), "the other mug is untouched");

            Assert.IsTrue(ledger.TryGetOldestDrinkable(0, 10.0, out oldest));
            Assert.AreEqual(second, oldest);

            Start(ledger, 0, second, 20.0);
            ledger.TryStop(0, second, Gen, 30.0, BeerDrinkStopReason.Released, out stop);
            Assert.IsFalse(ledger.TryGetOldestDrinkable(0, 30.0, out oldest), "all empty");
        }

        // ================================================================ lifecycle

        [Test]
        public void DeathOrDisconnectInterruptsTheDrink()
        {
            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);
            Start(ledger, 0, beer, 0.0);

            BeerSeat[] roster = Roster(4);
            roster[0] = new BeerSeat(true, false, false);

            List<BeerDrinkStop> stopped = new List<BeerDrinkStop>();
            ledger.Tick(1.5, roster, stopped);

            Assert.AreEqual(1, stopped.Count);
            Assert.AreEqual(BeerDrinkStopReason.Interrupted, stopped[0].Reason);
            Assert.AreEqual(0.8f, stopped[0].FinalFill, 1e-5f);
            Assert.IsFalse(ledger.IsDrinking(0));
        }

        [Test]
        public void OldMatchGenerationMessagesAreIgnored()
        {
            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);

            // Order ids restart every match, so beer #1/seat 0 exists in both: only the
            // generation tells them apart.
            Assert.AreEqual(BeerDrinkRejection.StaleGeneration, ledger.TryStart(0, beer, Gen - 1, Roster(4), 0.0).Rejection);
            Assert.IsFalse(ledger.IsDrinking(0));

            Assert.IsTrue(Start(ledger, 0, beer, 0.0).Accepted);

            BeerDrinkStop stop;
            Assert.IsFalse(ledger.TryStop(0, beer, Gen - 1, 1.0, BeerDrinkStopReason.Released, out stop),
                "a previous match's stop cannot end this match's drink");
            Assert.IsTrue(ledger.IsDrinking(0));
        }

        [Test]
        public void ClearForgetsEveryBeerAndDrink()
        {
            BeerLedger ledger = Ledger();
            BeerId beer = new BeerId(1, 0);
            ledger.Register(beer);
            ledger.Register(new BeerId(1, 1));
            Start(ledger, 0, beer, 0.0);

            ledger.Clear();

            Assert.AreEqual(0, ledger.Count);
            Assert.IsFalse(ledger.IsDrinking(0));
            Assert.IsFalse(ledger.Contains(beer));
            Assert.AreEqual(BeerDrinkRejection.UnknownBeer, Start(ledger, 0, beer, 1.0).Rejection);
        }
    }
}
