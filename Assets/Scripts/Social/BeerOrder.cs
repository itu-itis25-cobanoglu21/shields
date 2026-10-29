using System;
using System.Collections.Generic;

namespace Shields.Social
{
    /// <summary>
    /// Why a beer order was refused. Beer is cosmetic, so none of these ever touch the
    /// Shields match; they only explain to the requester why nothing was queued.
    /// </summary>
    public enum BeerOrderRejection : byte
    {
        None = 0,

        /// <summary>No live match to order in, or the authority is not ready.</summary>
        NotAvailable = 1,

        /// <summary>The sender maps to no seat at this table.</summary>
        InvalidRequester = 2,

        /// <summary>The requester's seat may not order: eliminated, gone, or a bot.</summary>
        RequesterUnavailable = 3,

        NoRecipients = 4,

        /// <summary>A recipient seat is out of range, eliminated or no longer present.</summary>
        RecipientUnavailable = 5,

        DuplicateRecipient = 6,

        /// <summary>The requester ordered too recently.</summary>
        Cooldown = 7,

        /// <summary>The queue already holds the maximum number of undelivered orders.</summary>
        QueueFull = 8
    }

    /// <summary>
    /// What the authority knows about one seat when it validates an order. Built fresh
    /// from authoritative match and session state at the moment of submission, never from
    /// presentation objects.
    /// </summary>
    public readonly struct BeerSeat
    {
        public BeerSeat(bool isPresent, bool isAlive, bool isBot)
        {
            IsPresent = isPresent;
            IsAlive = isAlive;
            IsBot = isBot;
        }

        /// <summary>The seat is part of the match and, for a human, still connected.</summary>
        public bool IsPresent { get; }

        public bool IsAlive { get; }

        public bool IsBot { get; }

        /// <summary>Only living, present humans place orders. Bots never order.</summary>
        public bool CanOrder { get { return IsPresent && IsAlive && !IsBot; } }

        /// <summary>Any living, present seat may be bought a beer, bots included.</summary>
        public bool CanReceive { get { return IsPresent && IsAlive; } }

        /// <summary>Only living, present humans drink their beers. Bots never drink.</summary>
        public bool CanDrink { get { return IsPresent && IsAlive && !IsBot; } }
    }

    /// <summary>
    /// One accepted beer order. Immutable once accepted: the delivery consumer reads it,
    /// it never rewrites it.
    ///
    /// Identity is seats, never GameObjects or network client ids. Recipient seats are
    /// kept even if a recipient later dies or disconnects; whoever delivers revalidates
    /// each seat at delivery time. One recipient is one beer, so there is no separate
    /// quantity to disagree with the recipient list.
    /// </summary>
    public sealed class BeerOrder
    {
        readonly int[] _recipients;

        public BeerOrder(int orderId, int requesterSeat, IReadOnlyList<int> recipientSeats, double acceptedAt)
        {
            if (recipientSeats == null) throw new ArgumentNullException(nameof(recipientSeats));

            OrderId = orderId;
            RequesterSeat = requesterSeat;
            AcceptedAt = acceptedAt;

            _recipients = new int[recipientSeats.Count];
            for (int i = 0; i < _recipients.Length; i++) _recipients[i] = recipientSeats[i];
        }

        /// <summary>Unique and increasing for the lifetime of the queue that accepted it.</summary>
        public int OrderId { get; }

        public int RequesterSeat { get; }

        /// <summary>Distinct recipient seats in ascending seat order.</summary>
        public IReadOnlyList<int> RecipientSeats { get { return _recipients; } }

        /// <summary>One mug per recipient.</summary>
        public int BeerCount { get { return _recipients.Length; } }

        /// <summary>Authority clock, in seconds, when the order was accepted.</summary>
        public double AcceptedAt { get; }

        public bool IncludesRecipient(int seat)
        {
            return Array.IndexOf(_recipients, seat) >= 0;
        }

        public override string ToString()
        {
            return string.Format("Beer order #{0}: seat {1} -> [{2}] ({3} beer{4})", OrderId, RequesterSeat,
                string.Join(",", _recipients), BeerCount, BeerCount == 1 ? "" : "s");
        }
    }

    /// <summary>
    /// The answer the requester gets for one submission. Plain data so the same value can
    /// come straight from a local queue offline or be rebuilt from a host reply online.
    /// </summary>
    public readonly struct BeerOrderReceipt
    {
        public BeerOrderReceipt(BeerOrderRejection rejection, int orderId, int beerCount, int ordersAhead,
            float cooldownRemaining)
        {
            Rejection = rejection;
            OrderId = orderId;
            BeerCount = beerCount;
            OrdersAhead = ordersAhead;
            CooldownRemaining = cooldownRemaining;
        }

        public static BeerOrderReceipt Refused(BeerOrderRejection rejection, float cooldownRemaining = 0f)
        {
            return new BeerOrderReceipt(rejection, BeerOrderQueue.NoOrder, 0, 0, cooldownRemaining);
        }

        public bool Accepted { get { return Rejection == BeerOrderRejection.None; } }

        public BeerOrderRejection Rejection { get; }

        public int OrderId { get; }

        public int BeerCount { get; }

        /// <summary>Accepted orders still ahead of this one, including one in delivery.</summary>
        public int OrdersAhead { get; }

        /// <summary>
        /// Seconds before the requester may order again. After an acceptance this is the
        /// full cooldown; after a cooldown refusal it is what is left of it.
        /// </summary>
        public float CooldownRemaining { get; }

        public override string ToString()
        {
            return Accepted
                ? string.Format("accepted #{0}, {1} beer(s), {2} ahead", OrderId, BeerCount, OrdersAhead)
                : "refused: " + Rejection;
        }
    }
}
