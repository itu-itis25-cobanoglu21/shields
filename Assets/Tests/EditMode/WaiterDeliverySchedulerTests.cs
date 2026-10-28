using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shields.Social;

namespace Shields.Game.Tests
{
    /// <summary>
    /// The waiter timeline on top of the beer order queue (TASK 025): one delay chosen
    /// once per front order, revalidation before the start, one delivery at a time,
    /// completion only on the waiter's exit, and a fresh delay for the next order.
    /// Plus the walking-order planner, which may reorder visits but never recipients.
    /// </summary>
    public class WaiterDeliverySchedulerTests
    {
        static BeerSeat[] Roster(int seats)
        {
            BeerSeat[] roster = new BeerSeat[seats];
            for (int i = 0; i < seats; i++) roster[i] = new BeerSeat(true, true, i >= 2);
            return roster;
        }

        static BeerOrderQueue QueueWith(params int[][] orders)
        {
            BeerOrderQueue queue = new BeerOrderQueue(0.0, 8);
            for (int i = 0; i < orders.Length; i++)
                Assert.IsTrue(queue.Submit(i % 2, orders[i], Roster(8), i).Accepted);

            return queue;
        }

        /// <summary>A scheduler whose random delay is always min + fraction × (max − min).</summary>
        static WaiterDeliveryScheduler Scheduler(double fraction = 0.5)
        {
            return new WaiterDeliveryScheduler(5.0, 10.0, () => fraction);
        }

        static WaiterTickResult Tick(WaiterDeliveryScheduler s, BeerOrderQueue q, double now, out WaiterDeliveryStart start,
            BeerSeat[] roster = null)
        {
            return s.Tick(q, roster ?? Roster(8), now, out start);
        }

        // ================================================================ delay

        [Test]
        public void DelayIsChosenOnceAndHonoured()
        {
            BeerOrderQueue queue = QueueWith(new[] { 0, 2 });
            WaiterDeliveryScheduler scheduler = Scheduler(0.4);
            WaiterDeliveryStart start;

            Assert.AreEqual(WaiterTickResult.None, Tick(scheduler, queue, 100.0, out start));
            Assert.IsTrue(scheduler.IsWaiting);
            Assert.AreEqual(107.0, scheduler.ReadyAt, 1e-9);

            for (double t = 100.5; t < 107.0; t += 0.5)
            {
                Assert.AreEqual(WaiterTickResult.None, Tick(scheduler, queue, t, out start));
                Assert.AreEqual(107.0, scheduler.ReadyAt, 1e-9, "The delay is never rerolled.");
            }

            Assert.IsNull(queue.ActiveOrder, "Nothing begins before the delay is over.");
            Assert.AreEqual(WaiterTickResult.Began, Tick(scheduler, queue, 107.0, out start));
            CollectionAssert.AreEqual(new[] { 0, 2 }, start.ValidRecipients);
            Assert.AreSame(start.Order, queue.ActiveOrder);
        }

        [Test]
        public void DelayStaysWithinTheConfiguredRange()
        {
            foreach (double fraction in new[] { 0.0, 0.25, 1.0 })
            {
                WaiterDeliveryScheduler scheduler = Scheduler(fraction);
                WaiterDeliveryStart start;
                Tick(scheduler, QueueWith(new[] { 1 }), 0.0, out start);

                Assert.GreaterOrEqual(scheduler.ReadyAt, 5.0);
                Assert.LessOrEqual(scheduler.ReadyAt, 10.0);
            }
        }

        // ================================================================ sequencing

        [Test]
        public void SecondOrderWaitsForTheFirstWaiterToLeaveThenGetsAFreshDelay()
        {
            BeerOrderQueue queue = QueueWith(new[] { 0 });
            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;

            Tick(scheduler, queue, 0.0, out start);
            Assert.AreEqual(WaiterTickResult.Began, Tick(scheduler, queue, 5.0, out start));
            int first = start.Order.OrderId;

            // Order B arrives while the waiter is inside.
            Assert.IsTrue(queue.Submit(1, new[] { 3 }, Roster(8), 6.0).Accepted);

            for (double t = 6.0; t < 60.0; t += 1.0)
            {
                Assert.AreEqual(WaiterTickResult.None, Tick(scheduler, queue, t, out start));
                Assert.AreEqual(first, queue.ActiveOrder.OrderId, "B never starts while A is active.");
                Assert.AreEqual(1, queue.PendingOrders.Count);
            }

            Assert.IsTrue(scheduler.NotifyWaiterExited(queue, first));
            Assert.IsNull(queue.ActiveOrder);

            // Fresh delay for B, measured from now, not from when B was accepted.
            Assert.AreEqual(WaiterTickResult.None, Tick(scheduler, queue, 60.0, out start));
            Assert.AreEqual(65.0, scheduler.ReadyAt, 1e-9);
            Assert.AreEqual(WaiterTickResult.None, Tick(scheduler, queue, 64.9, out start));
            Assert.AreEqual(WaiterTickResult.Began, Tick(scheduler, queue, 65.0, out start));
            CollectionAssert.AreEqual(new[] { 3 }, start.ValidRecipients);
        }

        [Test]
        public void ExitOfAnyOtherOrderIsIgnored()
        {
            BeerOrderQueue queue = QueueWith(new[] { 0 }, new[] { 1 });
            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;
            Tick(scheduler, queue, 0.0, out start);
            Tick(scheduler, queue, 5.0, out start);

            Assert.IsFalse(scheduler.NotifyWaiterExited(queue, start.Order.OrderId + 1));
            Assert.IsTrue(queue.HasActiveDelivery);
            Assert.IsTrue(scheduler.IsDelivering);
        }

        // ================================================================ revalidation

        [Test]
        public void RecipientsWhoLeftBeforeTheStartAreSkipped()
        {
            BeerOrderQueue queue = QueueWith(new[] { 1, 3, 5 });
            BeerSeat[] roster = Roster(8);
            roster[3] = new BeerSeat(true, false, true);   // died while queued
            roster[5] = new BeerSeat(false, true, false);  // left while queued

            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;
            Tick(scheduler, queue, 0.0, out start, roster);

            Assert.AreEqual(WaiterTickResult.Began, Tick(scheduler, queue, 5.0, out start, roster));
            CollectionAssert.AreEqual(new[] { 1 }, start.ValidRecipients);
            CollectionAssert.AreEqual(new[] { 1, 3, 5 }, start.Order.RecipientSeats, "The order itself is never rewritten.");
        }

        [Test]
        public void OrderWithNobodyLeftIsCompletedWithoutATrip()
        {
            BeerOrderQueue queue = QueueWith(new[] { 4 }, new[] { 1 });
            BeerSeat[] roster = Roster(8);
            roster[4] = new BeerSeat(true, false, true);

            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;
            Tick(scheduler, queue, 0.0, out start, roster);

            Assert.AreEqual(WaiterTickResult.SkippedEmpty, Tick(scheduler, queue, 5.0, out start, roster));
            Assert.IsNull(queue.ActiveOrder);
            Assert.AreEqual(1, queue.Count, "Only the next order remains.");
            Assert.IsTrue(scheduler.IsIdle);

            // The next order gets its own fresh delay.
            Tick(scheduler, queue, 5.0, out start, roster);
            Assert.AreEqual(10.0, scheduler.ReadyAt, 1e-9);
        }

        [Test]
        public void NoStartWithoutAnAuthoritativeRoster()
        {
            BeerOrderQueue queue = QueueWith(new[] { 0 });
            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;
            scheduler.Tick(queue, null, 0.0, out start);

            Assert.AreEqual(WaiterTickResult.None, scheduler.Tick(queue, null, 50.0, out start));
            Assert.IsNull(queue.ActiveOrder);
        }

        [Test]
        public void HandoffRevalidationReadsTheCurrentRoster()
        {
            BeerSeat[] roster = Roster(4);
            Assert.IsTrue(WaiterDeliveryScheduler.IsValidRecipient(roster, 2), "Bots are valid recipients.");

            roster[2] = new BeerSeat(true, false, true);
            Assert.IsFalse(WaiterDeliveryScheduler.IsValidRecipient(roster, 2));
            Assert.IsFalse(WaiterDeliveryScheduler.IsValidRecipient(roster, 9));
            Assert.IsFalse(WaiterDeliveryScheduler.IsValidRecipient(null, 0));
        }

        // ================================================================ teardown

        [Test]
        public void ClearedQueueMidDeliveryReportsLostActiveAndNeverCompletes()
        {
            BeerOrderQueue queue = QueueWith(new[] { 0 });
            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;
            Tick(scheduler, queue, 0.0, out start);
            Tick(scheduler, queue, 5.0, out start);
            int orderId = start.Order.OrderId;

            queue.Clear();

            Assert.AreEqual(WaiterTickResult.LostActive, Tick(scheduler, queue, 6.0, out start));
            Assert.IsTrue(scheduler.IsIdle);
            Assert.IsFalse(scheduler.NotifyWaiterExited(queue, orderId), "A late exit makes no queue call.");
        }

        [Test]
        public void NullQueueResetsTheTimeline()
        {
            BeerOrderQueue queue = QueueWith(new[] { 0 });
            WaiterDeliveryScheduler scheduler = Scheduler(0.0);
            WaiterDeliveryStart start;
            Tick(scheduler, queue, 0.0, out start);
            Tick(scheduler, queue, 5.0, out start);

            Assert.AreEqual(WaiterTickResult.LostActive, scheduler.Tick(null, null, 6.0, out start));
            Assert.IsTrue(scheduler.IsIdle);
        }

        // ================================================================ route planner

        [Test]
        public void NearbyStopsOnOneSideAreVisitedOutAndBack()
        {
            WaiterRoutePlan plan = WaiterRoutePlanner.Plan(new[] { 80.0, 20.0, 50.0 });

            Assert.AreEqual(WaiterRouteMode.ForwardAndBack, plan.Mode);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, plan.VisitOrder);
        }

        [Test]
        public void StopsJustBehindTheDoorAreVisitedBackwards()
        {
            WaiterRoutePlan plan = WaiterRoutePlanner.Plan(new[] { 300.0, 338.0 });

            Assert.AreEqual(WaiterRouteMode.BackwardAndBack, plan.Mode);
            CollectionAssert.AreEqual(new[] { 1, 0 }, plan.VisitOrder);
        }

        [Test]
        public void StopsAllRoundTheTableUseOneLoop()
        {
            double[] angles = { 338.0, 23.0, 68.0, 113.0, 158.0, 203.0, 248.0, 293.0 };
            WaiterRoutePlan plan = WaiterRoutePlanner.Plan(angles);

            Assert.AreEqual(WaiterRouteMode.FullLoop, plan.Mode);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 0 }, plan.VisitOrder);
            Assert.AreEqual(360.0, WaiterRoutePlanner.RingDegrees(angles, plan));
        }

        [Test]
        public void PlannerOnlyReordersNeverChangesWhoIsServed()
        {
            System.Random random = new System.Random(7);
            for (int trial = 0; trial < 200; trial++)
            {
                int count = random.Next(1, 9);
                double[] angles = Enumerable.Range(0, count).Select(_ => random.NextDouble() * 360.0).ToArray();
                WaiterRoutePlan plan = WaiterRoutePlanner.Plan(angles);

                CollectionAssert.AreEquivalent(Enumerable.Range(0, count), plan.VisitOrder);

                // Visits are monotonic in the walking direction, so the ring is never doubled back mid-route.
                List<double> visited = plan.VisitOrder.Select(i => angles[i]).ToList();
                if (plan.Mode == WaiterRouteMode.BackwardAndBack) visited.Reverse();
                CollectionAssert.IsOrdered(visited);

                double shortest = new[] { 2.0 * angles.Max(), 2.0 * (360.0 - angles.Min()), 360.0 }.Min();
                Assert.AreEqual(shortest, WaiterRoutePlanner.RingDegrees(angles, plan), 1e-9);
            }
        }
    }
}
