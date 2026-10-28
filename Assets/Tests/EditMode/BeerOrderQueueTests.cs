using System.Collections.Generic;
using NUnit.Framework;
using Shields.Session;
using Shields.Social;

namespace Shields.Game.Tests
{
    /// <summary>
    /// The authoritative beer order queue (TASK 024): who may order, for whom, FIFO,
    /// cooldown, capacity, and the delivery hand off a future waiter consumes. Plain C#
    /// with an explicit clock, so nothing here depends on frame timing.
    /// </summary>
    public class BeerOrderQueueTests
    {
        const ulong HostClient = 0;
        const ulong ClientA = 1;
        const ulong Stranger = 77;

        /// <summary>Seat 0 and 1 are humans, the rest bots, everyone alive and present.</summary>
        static BeerSeat[] Roster(int seats)
        {
            BeerSeat[] roster = new BeerSeat[seats];
            for (int i = 0; i < seats; i++) roster[i] = new BeerSeat(true, true, i >= 2);

            return roster;
        }

        static BeerOrderReceipt Order(BeerOrderQueue queue, int requester, double now, params int[] recipients)
        {
            return queue.Submit(requester, recipients, Roster(8), now);
        }

        // ================================================================ validation

        [Test]
        public void OneRecipientIsOneBeer()
        {
            BeerOrderQueue queue = new BeerOrderQueue();

            BeerOrderReceipt receipt = queue.Submit(0, new[] { 0, 2, 4 }, Roster(6), 0.0);

            Assert.IsTrue(receipt.Accepted);
            Assert.AreEqual(3, receipt.BeerCount);
            Assert.AreEqual(3, queue.PeekNext().BeerCount);
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, queue.PeekNext().RecipientSeats);
            Assert.AreEqual(0, queue.PeekNext().RequesterSeat);
        }

        [Test]
        public void SelfOnlyAndBotOnlyOrdersAreAllowed()
        {
            BeerOrderQueue queue = new BeerOrderQueue(0.0);

            Assert.IsTrue(queue.Submit(0, new[] { 0 }, Roster(3), 0.0).Accepted, "self");
            Assert.IsTrue(queue.Submit(0, new[] { 2 }, Roster(3), 1.0).Accepted, "bot");
        }

        [Test]
        public void EmptySelectionIsRefused()
        {
            BeerOrderQueue queue = new BeerOrderQueue();

            Assert.AreEqual(BeerOrderRejection.NoRecipients, queue.Submit(0, new int[0], Roster(3), 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.NoRecipients, queue.Submit(0, null, Roster(3), 0.0).Rejection);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void DuplicateRecipientIsRefusedAndNothingQueued()
        {
            BeerOrderQueue queue = new BeerOrderQueue();

            BeerOrderReceipt receipt = queue.Submit(0, new[] { 1, 2, 1 }, Roster(4), 0.0);

            Assert.AreEqual(BeerOrderRejection.DuplicateRecipient, receipt.Rejection);
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0.0, queue.CooldownRemaining(0, 0.0), "A refusal starts no cooldown.");
        }

        [Test]
        public void DeadOrMissingRecipientsAreRefused()
        {
            BeerOrderQueue queue = new BeerOrderQueue();
            BeerSeat[] roster = Roster(4);
            roster[2] = new BeerSeat(true, false, true);   // eliminated bot
            roster[3] = new BeerSeat(false, true, false);  // human who left

            Assert.AreEqual(BeerOrderRejection.RecipientUnavailable, queue.Submit(0, new[] { 2 }, roster, 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.RecipientUnavailable, queue.Submit(0, new[] { 3 }, roster, 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.RecipientUnavailable, queue.Submit(0, new[] { 9 }, roster, 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.RecipientUnavailable, queue.Submit(0, new[] { -1 }, roster, 0.0).Rejection);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void DeadRequestersAndBotsCannotOrder()
        {
            BeerOrderQueue queue = new BeerOrderQueue();
            BeerSeat[] roster = Roster(4);
            roster[1] = new BeerSeat(true, false, false);

            Assert.AreEqual(BeerOrderRejection.RequesterUnavailable, queue.Submit(1, new[] { 0 }, roster, 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.RequesterUnavailable, queue.Submit(2, new[] { 0 }, roster, 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.InvalidRequester, queue.Submit(7, new[] { 0 }, roster, 0.0).Rejection);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void EveryLivingPlayerAtAnEightSeatTableIsServedExactlyOnce()
        {
            BeerOrderQueue queue = new BeerOrderQueue();

            BeerOrderReceipt receipt = queue.Submit(1, new[] { 7, 6, 5, 4, 3, 2, 1, 0 }, Roster(8), 0.0);

            Assert.IsTrue(receipt.Accepted);
            Assert.AreEqual(8, receipt.BeerCount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, queue.PeekNext().RecipientSeats,
                "Recipients are distinct and in canonical seat order.");
        }

        // ================================================================ participant mapping

        [Test]
        public void RequesterComesFromTheSendersSeatNeverFromTheClientId()
        {
            // Client 1 deliberately sits in seat 0 here, so clientId != seat.
            SeatRegistry seats = new SeatRegistry(4);
            seats.AssignSeat(ClientA);
            seats.AssignSeat(HostClient);

            BeerOrderQueue queue = new BeerOrderQueue();
            BeerOrderReceipt receipt = queue.SubmitFromParticipant(seats, ClientA, new[] { 1, 3 }, Roster(4), 0.0);

            Assert.IsTrue(receipt.Accepted);
            Assert.AreEqual(0, queue.PeekNext().RequesterSeat);
            CollectionAssert.AreEqual(new[] { 1, 3 }, queue.PeekNext().RecipientSeats);
        }

        [Test]
        public void SenderWithoutASeatIsRefused()
        {
            SeatRegistry seats = new SeatRegistry(3);
            seats.AssignSeat(HostClient);

            BeerOrderQueue queue = new BeerOrderQueue();

            Assert.AreEqual(BeerOrderRejection.InvalidRequester,
                queue.SubmitFromParticipant(seats, Stranger, new[] { 0 }, Roster(3), 0.0).Rejection);
            Assert.AreEqual(BeerOrderRejection.InvalidRequester,
                queue.SubmitFromParticipant(null, HostClient, new[] { 0 }, Roster(3), 0.0).Rejection);
            Assert.AreEqual(0, queue.Count);
        }

        // ================================================================ FIFO

        [Test]
        public void OrdersStayFirstInFirstOutWithStableIds()
        {
            BeerOrderQueue queue = new BeerOrderQueue();

            BeerOrderReceipt first = Order(queue, 1, 0.0, 0, 2, 4);
            BeerOrderReceipt second = Order(queue, 0, 0.5, 3);
            BeerOrderReceipt third = queue.Submit(1, new[] { 1, 5 }, Roster(8), 30.0);

            Assert.IsTrue(first.Accepted && second.Accepted && third.Accepted);
            Assert.AreEqual(0, first.OrdersAhead);
            Assert.AreEqual(1, second.OrdersAhead);
            Assert.AreEqual(2, third.OrdersAhead);
            Assert.Less(first.OrderId, second.OrderId);
            Assert.Less(second.OrderId, third.OrderId);

            IReadOnlyList<BeerOrder> pending = queue.PendingOrders;
            Assert.AreEqual(3, pending.Count);
            Assert.AreEqual(first.OrderId, pending[0].OrderId);
            Assert.AreEqual(1, pending[0].RequesterSeat);
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, pending[0].RecipientSeats);
            Assert.AreEqual(second.OrderId, pending[1].OrderId);
            Assert.AreEqual(0, pending[1].RequesterSeat);
            Assert.AreEqual(third.OrderId, pending[2].OrderId);
        }

        // ================================================================ cooldown

        [Test]
        public void CooldownIsPerRequesterAndExpires()
        {
            BeerOrderQueue queue = new BeerOrderQueue(20.0, 10);

            Assert.IsTrue(Order(queue, 0, 100.0, 1).Accepted);

            BeerOrderReceipt tooSoon = Order(queue, 0, 105.0, 1);
            Assert.AreEqual(BeerOrderRejection.Cooldown, tooSoon.Rejection);
            Assert.AreEqual(15f, tooSoon.CooldownRemaining, 0.001f);

            Assert.IsTrue(Order(queue, 1, 105.0, 0).Accepted, "Another requester is not blocked.");
            Assert.IsTrue(Order(queue, 0, 120.0, 1).Accepted, "Cooldown over after exactly 20 seconds.");
            Assert.AreEqual(3, queue.Count);
        }

        [Test]
        public void AcceptedReceiptCarriesTheFullCooldown()
        {
            BeerOrderQueue queue = new BeerOrderQueue(20.0);

            Assert.AreEqual(20f, Order(queue, 0, 0.0, 1).CooldownRemaining, 0.001f);
            Assert.AreEqual(20.0, queue.CooldownRemaining(0, 0.0), 0.001);
            Assert.AreEqual(0.0, queue.CooldownRemaining(1, 0.0));
        }

        // ================================================================ capacity

        [Test]
        public void FullQueueRefusesWithoutDroppingAcceptedOrders()
        {
            BeerOrderQueue queue = new BeerOrderQueue(0.0, 5);
            for (int i = 0; i < 5; i++) Assert.IsTrue(Order(queue, 0, i, 1).Accepted);

            BeerOrderReceipt overflow = Order(queue, 1, 10.0, 0);

            Assert.AreEqual(BeerOrderRejection.QueueFull, overflow.Rejection);
            Assert.AreEqual(5, queue.Count);
            Assert.AreEqual(0.0, queue.CooldownRemaining(1, 10.0), "A refused order starts no cooldown.");
        }

        [Test]
        public void TheOrderInDeliveryCountsTowardCapacity()
        {
            BeerOrderQueue queue = new BeerOrderQueue(0.0, 2);
            Order(queue, 0, 0.0, 1);
            Order(queue, 0, 1.0, 1);

            BeerOrder active;
            Assert.IsTrue(queue.TryBeginDelivery(out active));
            Assert.AreEqual(BeerOrderRejection.QueueFull, Order(queue, 0, 2.0, 1).Rejection);

            Assert.IsTrue(queue.CompleteDelivery(active.OrderId));
            Assert.IsTrue(Order(queue, 0, 3.0, 1).Accepted);
        }

        // ================================================================ delivery hand off

        [Test]
        public void OnlyOneDeliveryRunsAndTheNextWaitsForCompletion()
        {
            BeerOrderQueue queue = new BeerOrderQueue(0.0);
            int first = Order(queue, 0, 0.0, 1).OrderId;
            int second = Order(queue, 1, 1.0, 0, 1).OrderId;

            BeerOrder active;
            Assert.IsTrue(queue.TryBeginDelivery(out active));
            Assert.AreEqual(first, active.OrderId);
            Assert.AreSame(active, queue.ActiveOrder);
            Assert.AreEqual(second, queue.PeekNext().OrderId, "Peek never consumes.");

            BeerOrder blocked;
            Assert.IsFalse(queue.TryBeginDelivery(out blocked), "No overlap while one is active.");
            Assert.IsNull(blocked);

            Assert.AreEqual(2, Order(queue, 0, 2.0, 1).OrdersAhead,
                "A new order counts the active delivery and the pending one as ahead of it.");

            Assert.IsFalse(queue.CompleteDelivery(second), "Only the active order can complete.");
            Assert.IsTrue(queue.CompleteDelivery(first));
            Assert.IsFalse(queue.HasActiveDelivery);

            Assert.IsTrue(queue.TryBeginDelivery(out active));
            Assert.AreEqual(second, active.OrderId);
        }

        [Test]
        public void ClearForgetsOrdersAndCooldowns()
        {
            BeerOrderQueue queue = new BeerOrderQueue(20.0);
            Order(queue, 0, 0.0, 1);
            BeerOrder active;
            queue.TryBeginDelivery(out active);
            Order(queue, 1, 0.0, 0);

            int revision = queue.Revision;
            queue.Clear();

            Assert.AreEqual(0, queue.Count);
            Assert.IsNull(queue.ActiveOrder);
            Assert.AreEqual(0.0, queue.CooldownRemaining(0, 1.0));
            Assert.Greater(queue.Revision, revision);
            Assert.IsTrue(Order(queue, 0, 1.0, 1).Accepted);
        }

        [Test]
        public void RecipientsAreKeptEvenIfTheyDieLater()
        {
            BeerOrderQueue queue = new BeerOrderQueue();
            BeerSeat[] roster = Roster(3);
            queue.Submit(0, new[] { 1, 2 }, roster, 0.0);

            // Later the recipient dies; the accepted order is untouched and the delivery
            // consumer revalidates each seat itself.
            roster[2] = new BeerSeat(true, false, true);

            CollectionAssert.AreEqual(new[] { 1, 2 }, queue.PeekNext().RecipientSeats);
            Assert.IsTrue(queue.PeekNext().IncludesRecipient(2));
        }
    }
}
