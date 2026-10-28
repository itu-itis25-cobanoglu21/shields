using System;
using System.Collections.Generic;

namespace Shields.Social
{
    /// <summary>What one scheduler tick decided.</summary>
    public enum WaiterTickResult
    {
        /// <summary>Nothing to do this tick.</summary>
        None = 0,

        /// <summary>A delivery began: the order is now the queue's active order.</summary>
        Began = 1,

        /// <summary>
        /// The front order had no valid recipient left when its delay ran out. It was
        /// begun and completed at once, so no empty waiter trip happens.
        /// </summary>
        SkippedEmpty = 2,

        /// <summary>
        /// The active order disappeared from the queue (match ended, queue cleared).
        /// Presentation must abort; no queue operation is made for it.
        /// </summary>
        LostActive = 3
    }

    /// <summary>The delivery plan the authority hands to presentation when a delivery begins.</summary>
    public readonly struct WaiterDeliveryStart
    {
        public WaiterDeliveryStart(BeerOrder order, int[] validRecipients)
        {
            Order = order;
            ValidRecipients = validRecipients;
        }

        public BeerOrder Order { get; }

        /// <summary>Recipients still valid at delivery start, ascending. One mug each.</summary>
        public int[] ValidRecipients { get; }
    }

    /// <summary>
    /// The authority's waiter timeline on top of <see cref="BeerOrderQueue"/>. Only the
    /// offline match or the online host runs one; it is the only consumer of the queue.
    ///
    /// Idle → (front order seen) Waiting with one delay chosen once → (delay over)
    /// revalidate recipients, then <see cref="BeerOrderQueue.TryBeginDelivery"/> →
    /// Delivering → (waiter fully outside) <see cref="NotifyWaiterExited"/> completes the
    /// order → Idle, so the next order gets a fresh delay of its own.
    ///
    /// The delay belongs here, not in the queue. Plain C# with an explicit clock and an
    /// injectable random source, so the whole lifecycle is unit testable.
    /// </summary>
    public sealed class WaiterDeliveryScheduler
    {
        public const double DefaultDelayMin = 5.0;
        public const double DefaultDelayMax = 10.0;

        enum State { Idle, Waiting, Delivering }

        readonly double _delayMin;
        readonly double _delayMax;
        readonly Func<double> _random01;

        State _state = State.Idle;
        int _orderId = BeerOrderQueue.NoOrder;
        double _readyAt;

        public WaiterDeliveryScheduler(double delayMin = DefaultDelayMin, double delayMax = DefaultDelayMax,
            Func<double> random01 = null)
        {
            if (delayMin < 0.0 || delayMax < delayMin) throw new ArgumentOutOfRangeException(nameof(delayMin));

            _delayMin = delayMin;
            _delayMax = delayMax;

            if (random01 == null)
            {
                Random random = new Random();
                random01 = random.NextDouble;
            }

            _random01 = random01;
        }

        public bool IsIdle { get { return _state == State.Idle; } }

        public bool IsWaiting { get { return _state == State.Waiting; } }

        public bool IsDelivering { get { return _state == State.Delivering; } }

        /// <summary>The order being waited for or delivered, or <see cref="BeerOrderQueue.NoOrder"/>.</summary>
        public int CurrentOrderId { get { return _orderId; } }

        /// <summary>When the waited-for order may begin. Chosen once per order.</summary>
        public double ReadyAt { get { return _readyAt; } }

        /// <summary>Forgets everything. Match or session end; never touches a queue.</summary>
        public void Reset()
        {
            _state = State.Idle;
            _orderId = BeerOrderQueue.NoOrder;
            _readyAt = 0.0;
        }

        /// <summary>
        /// Advances the timeline. <paramref name="roster"/> is the current authoritative
        /// seat state; when it is null nothing may begin this tick.
        /// </summary>
        public WaiterTickResult Tick(BeerOrderQueue queue, IReadOnlyList<BeerSeat> roster, double now,
            out WaiterDeliveryStart start)
        {
            start = default;

            if (queue == null)
            {
                bool wasDelivering = _state == State.Delivering;
                Reset();
                return wasDelivering ? WaiterTickResult.LostActive : WaiterTickResult.None;
            }

            if (_state == State.Delivering)
            {
                if (queue.ActiveOrder != null && queue.ActiveOrder.OrderId == _orderId) return WaiterTickResult.None;

                Reset();
                return WaiterTickResult.LostActive;
            }

            BeerOrder next = queue.PeekNext();

            if (next == null || queue.HasActiveDelivery)
            {
                // Nothing to wait for, or somebody else's delivery is active.
                if (_state == State.Waiting) Reset();
                return WaiterTickResult.None;
            }

            if (_state == State.Idle || _orderId != next.OrderId)
            {
                _state = State.Waiting;
                _orderId = next.OrderId;
                _readyAt = now + _delayMin + (_delayMax - _delayMin) * Clamp01(_random01());
                return WaiterTickResult.None;
            }

            if (now < _readyAt || roster == null) return WaiterTickResult.None;

            // Revalidate first, then begin: the order may be several seconds old.
            int[] valid = ValidRecipients(next, roster);

            BeerOrder begun;
            if (!queue.TryBeginDelivery(out begun)) return WaiterTickResult.None;

            if (valid.Length == 0)
            {
                queue.CompleteDelivery(begun.OrderId);
                Reset();
                return WaiterTickResult.SkippedEmpty;
            }

            _state = State.Delivering;
            _orderId = begun.OrderId;
            start = new WaiterDeliveryStart(begun, valid);
            return WaiterTickResult.Began;
        }

        /// <summary>
        /// The waiter of <paramref name="orderId"/> is fully outside again: completes the
        /// order. Ignored, with no queue call, unless it is exactly the delivery in progress.
        /// </summary>
        public bool NotifyWaiterExited(BeerOrderQueue queue, int orderId)
        {
            if (_state != State.Delivering || orderId != _orderId) return false;

            bool completed = queue != null && queue.CompleteDelivery(orderId);
            Reset();
            return completed;
        }

        /// <summary>A recipient is valid while their seat is present and alive. Bots included.</summary>
        public static bool IsValidRecipient(IReadOnlyList<BeerSeat> roster, int seat)
        {
            return roster != null && seat >= 0 && seat < roster.Count && roster[seat].CanReceive;
        }

        /// <summary>The order's recipients that are still valid now, ascending.</summary>
        public static int[] ValidRecipients(BeerOrder order, IReadOnlyList<BeerSeat> roster)
        {
            List<int> valid = new List<int>();
            if (order == null) return valid.ToArray();

            for (int i = 0; i < order.RecipientSeats.Count; i++)
                if (IsValidRecipient(roster, order.RecipientSeats[i])) valid.Add(order.RecipientSeats[i]);

            return valid.ToArray();
        }

        static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }
    }
}
