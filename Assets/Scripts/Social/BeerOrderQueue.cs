using System;
using System.Collections.Generic;
using Shields.Session;

namespace Shields.Social
{
    /// <summary>
    /// The one authoritative beer order queue. Only the authority owns an instance: the
    /// offline match on the local machine, or the host online. Clients never touch it;
    /// they send intent and receive a <see cref="BeerOrderReceipt"/>.
    ///
    /// Beer is social and cosmetic, so this deliberately lives outside the Shields rules
    /// engine. It validates who may order and for whom, rate limits each requester, and
    /// keeps accepted orders strictly first in, first out.
    ///
    /// Delivery is a two step hand off for whatever presents it (TASK 025's waiter):
    /// <see cref="TryBeginDelivery"/> moves the oldest order into the single active slot,
    /// and <see cref="CompleteDelivery"/> clears that slot once the delivery has fully
    /// finished. While an order is active no other order can begin, so the next order
    /// only becomes eligible after the previous delivery is done.
    ///
    /// Plain C# with an explicit clock, so validation, FIFO, cooldown and capacity are
    /// all unit testable without Unity or a network.
    /// </summary>
    public sealed class BeerOrderQueue
    {
        /// <summary>Order id meaning "none".</summary>
        public const int NoOrder = 0;

        public const double DefaultCooldownSeconds = 20.0;
        public const int DefaultMaxOrders = 5;

        readonly List<BeerOrder> _pending = new List<BeerOrder>();
        readonly Dictionary<int, double> _lastAcceptedAt = new Dictionary<int, double>();
        BeerOrder _active;
        int _nextOrderId = 1;

        public BeerOrderQueue(double cooldownSeconds = DefaultCooldownSeconds, int maxOrders = DefaultMaxOrders)
        {
            if (cooldownSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(cooldownSeconds));
            if (maxOrders < 1) throw new ArgumentOutOfRangeException(nameof(maxOrders));

            CooldownSeconds = cooldownSeconds;
            MaxOrders = maxOrders;
        }

        /// <summary>Per requester seat: minimum time between two accepted orders.</summary>
        public double CooldownSeconds { get; }

        /// <summary>
        /// Most undelivered orders at once, counting the one in delivery. Bounds how long
        /// the waiter can ever be busy.
        /// </summary>
        public int MaxOrders { get; }

        /// <summary>Bumped on every change, so a consumer can cheaply notice one.</summary>
        public int Revision { get; private set; }

        /// <summary>Accepted orders waiting for delivery, oldest first. Excludes the active one.</summary>
        public IReadOnlyList<BeerOrder> PendingOrders { get { return _pending; } }

        /// <summary>The order currently being delivered, or null.</summary>
        public BeerOrder ActiveOrder { get { return _active; } }

        public bool HasActiveDelivery { get { return _active != null; } }

        /// <summary>Every undelivered order: pending plus the active one.</summary>
        public int Count { get { return _pending.Count + (_active != null ? 1 : 0); } }

        public bool IsFull { get { return Count >= MaxOrders; } }

        // ------------------------------------------------------------------ submit

        /// <summary>
        /// Validates and, when legal, enqueues one order. The requester seat must already
        /// be authoritative: offline it is the local seat, online it comes from
        /// <see cref="SubmitFromParticipant"/>. <paramref name="roster"/> is the current
        /// state of every seat, indexed by seat.
        /// </summary>
        public BeerOrderReceipt Submit(int requesterSeat, IReadOnlyList<int> recipientSeats,
            IReadOnlyList<BeerSeat> roster, double now)
        {
            if (roster == null || roster.Count == 0) return BeerOrderReceipt.Refused(BeerOrderRejection.NotAvailable);

            if (requesterSeat < 0 || requesterSeat >= roster.Count || !roster[requesterSeat].IsPresent)
                return BeerOrderReceipt.Refused(BeerOrderRejection.InvalidRequester);

            if (!roster[requesterSeat].CanOrder)
                return BeerOrderReceipt.Refused(BeerOrderRejection.RequesterUnavailable);

            if (recipientSeats == null || recipientSeats.Count == 0)
                return BeerOrderReceipt.Refused(BeerOrderRejection.NoRecipients);

            List<int> recipients = new List<int>(recipientSeats.Count);
            for (int i = 0; i < recipientSeats.Count; i++)
            {
                int seat = recipientSeats[i];

                if (seat < 0 || seat >= roster.Count || !roster[seat].CanReceive)
                    return BeerOrderReceipt.Refused(BeerOrderRejection.RecipientUnavailable);

                if (recipients.Contains(seat))
                    return BeerOrderReceipt.Refused(BeerOrderRejection.DuplicateRecipient);

                recipients.Add(seat);
            }

            double cooldown = CooldownRemaining(requesterSeat, now);
            if (cooldown > 0.0) return BeerOrderReceipt.Refused(BeerOrderRejection.Cooldown, (float)cooldown);

            if (IsFull) return BeerOrderReceipt.Refused(BeerOrderRejection.QueueFull);

            // Canonical order, so the same selection always produces the same order.
            recipients.Sort();

            int ahead = Count;
            BeerOrder order = new BeerOrder(_nextOrderId++, requesterSeat, recipients, now);
            _pending.Add(order);
            _lastAcceptedAt[requesterSeat] = now;
            Revision++;

            return new BeerOrderReceipt(BeerOrderRejection.None, order.OrderId, order.BeerCount, ahead,
                (float)CooldownSeconds);
        }

        /// <summary>
        /// Online entry point. The requester is derived from the authenticated sender
        /// through the host's seat registry; the payload never names it, so nobody can
        /// order as somebody else.
        /// </summary>
        public BeerOrderReceipt SubmitFromParticipant(SeatRegistry seats, ulong senderParticipantId,
            IReadOnlyList<int> recipientSeats, IReadOnlyList<BeerSeat> roster, double now)
        {
            int seat = seats != null ? seats.SeatOf(senderParticipantId) : SeatRegistry.NoSeat;
            if (seat == SeatRegistry.NoSeat) return BeerOrderReceipt.Refused(BeerOrderRejection.InvalidRequester);

            return Submit(seat, recipientSeats, roster, now);
        }

        /// <summary>Seconds before this seat may order again; zero when it may order now.</summary>
        public double CooldownRemaining(int requesterSeat, double now)
        {
            double last;
            if (!_lastAcceptedAt.TryGetValue(requesterSeat, out last)) return 0.0;

            return Math.Max(0.0, last + CooldownSeconds - now);
        }

        // ------------------------------------------------------------------ delivery (consumer API)

        /// <summary>The oldest order not yet in delivery, or null. Changes nothing.</summary>
        public BeerOrder PeekNext()
        {
            return _pending.Count > 0 ? _pending[0] : null;
        }

        /// <summary>
        /// Moves the oldest pending order into delivery. Refused while another delivery is
        /// still active, so deliveries never overlap and never reorder.
        /// </summary>
        public bool TryBeginDelivery(out BeerOrder order)
        {
            order = null;
            if (_active != null || _pending.Count == 0) return false;

            order = _pending[0];
            _pending.RemoveAt(0);
            _active = order;
            Revision++;
            return true;
        }

        /// <summary>
        /// Ends the active delivery. Only the active order's id is accepted, so a stale
        /// completion can never remove a different order.
        /// </summary>
        public bool CompleteDelivery(int orderId)
        {
            if (_active == null || _active.OrderId != orderId) return false;

            _active = null;
            Revision++;
            return true;
        }

        /// <summary>Forgets every order and every cooldown. Match or session end.</summary>
        public void Clear()
        {
            if (_pending.Count == 0 && _active == null && _lastAcceptedAt.Count == 0) return;

            _pending.Clear();
            _lastAcceptedAt.Clear();
            _active = null;
            Revision++;
        }

        // ------------------------------------------------------------------ text

        /// <summary>A short reason for the requester. Beer orders carry no hidden state.</summary>
        public static string DescribeRejection(BeerOrderRejection rejection)
        {
            switch (rejection)
            {
                case BeerOrderRejection.None: return string.Empty;
                case BeerOrderRejection.NoRecipients: return "SELECT A PLAYER";
                case BeerOrderRejection.Cooldown: return "ORDER COOLDOWN";
                case BeerOrderRejection.QueueFull: return "ORDER QUEUE FULL";
                case BeerOrderRejection.RecipientUnavailable: return "PLAYER NO LONGER AVAILABLE";
                case BeerOrderRejection.DuplicateRecipient: return "PLAYER SELECTED TWICE";
                default: return "CANNOT ORDER RIGHT NOW";
            }
        }

        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("{0}/{1} beer order(s)", Count, MaxOrders);
            if (_active != null) sb.Append("\n  active  ").Append(_active);
            for (int i = 0; i < _pending.Count; i++) sb.AppendFormat("\n  {0}. {1}", i + 1, _pending[i]);

            return sb.ToString();
        }
    }
}
