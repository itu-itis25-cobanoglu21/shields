using UnityEngine;
using Shields.Network;
using Shields.Social;

namespace Shields.Tavern
{
    /// <summary>
    /// Drives waiter deliveries from the authoritative beer order queue.
    ///
    /// Authority (offline match, or the online host): a <see cref="WaiterDeliveryScheduler"/>
    /// waits one 5–10 second delay per front order, revalidates its recipients, begins
    /// it, plans the walking order, decides each handoff and completes the order only
    /// when the waiter is fully outside. It is the only consumer of the queue.
    ///
    /// Presentation (every machine): runs the same <see cref="WaiterPresenter"/> from the
    /// same plan. Offline the authority calls the presenter directly; online the host
    /// broadcasts start, per-stop decisions and end through
    /// <see cref="NetworkBeerOrderService"/>, and every machine, the host included,
    /// presents from those messages. Clients never touch the queue.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaiterDeliveryCoordinator : MonoBehaviour
    {
        [SerializeField] BeerOrderCoordinator beerOrders;
        [SerializeField] WaiterPresenter presenter;

        [Header("Delivery Delay (authority only)")]
        [Tooltip("Seconds an order waits at the front of the queue before the waiter sets off. Chosen once per order.")]
        [Min(0f)]
        [SerializeField] float deliveryDelayMin = (float)WaiterDeliveryScheduler.DefaultDelayMin;

        [Min(0f)]
        [SerializeField] float deliveryDelayMax = (float)WaiterDeliveryScheduler.DefaultDelayMax;

        [Tooltip("If the authority's own waiter never starts or vanishes, complete the order after this long so the queue cannot stall.")]
        [SerializeField] float presenterWatchdogSeconds = 3f;

        WaiterDeliveryScheduler _scheduler;
        BeerOrderQueue _queue;
        NetworkBeerOrderService _service;
        float _beganAt;
        bool _tableLive;
        bool _turnPlay;

        /// <summary>The scheduler, on the authority. Diagnostics and validation only.</summary>
        public WaiterDeliveryScheduler Scheduler { get { return _scheduler; } }

        void Awake()
        {
            _scheduler = new WaiterDeliveryScheduler(deliveryDelayMin, Mathf.Max(deliveryDelayMin, deliveryDelayMax));
        }

        void OnEnable()
        {
            if (presenter != null)
            {
                presenter.StopReached += OnStopReached;
                presenter.Exited += OnWaiterExited;
            }

            _service = beerOrders != null ? beerOrders.OnlineOrders : null;
            if (_service != null)
            {
                _service.WaiterDeliveryStarted += OnDeliveryStarted;
                _service.WaiterHandoffDecided += OnHandoffDecided;
                _service.WaiterDeliveryEnded += OnDeliveryEnded;
            }
        }

        void OnDisable()
        {
            if (presenter != null)
            {
                presenter.StopReached -= OnStopReached;
                presenter.Exited -= OnWaiterExited;
            }

            if (_service != null)
            {
                _service.WaiterDeliveryStarted -= OnDeliveryStarted;
                _service.WaiterHandoffDecided -= OnHandoffDecided;
                _service.WaiterDeliveryEnded -= OnDeliveryEnded;
            }

            _scheduler.Reset();
            _queue = null;
        }

        void Update()
        {
            if (beerOrders == null || presenter == null) return;

            // The authority's queue was replaced or cleared (match ended, new match):
            // forget the old timeline first, so nothing calls into a stale queue.
            BeerOrderQueue queue = beerOrders.AuthoritativeQueue;
            if (!ReferenceEquals(queue, _queue))
            {
                int lostOrder = _scheduler.IsDelivering ? _scheduler.CurrentOrderId : BeerOrderQueue.NoOrder;
                _scheduler.Reset();
                _queue = queue;

                if (lostOrder != BeerOrderQueue.NoOrder) PublishEnded(lostOrder, true);
            }

            // No turn play on this machine: no waiter may stay in the room.
            bool turnPlay = beerOrders.TurnPlayActive;
            if (!turnPlay && presenter.IsRunning) presenter.Abort();

            // Delivered beers belong to one match. The final table of a finished match
            // keeps them; a torn-down table, or a new match on a reused layout, does not.
            bool tableLive = beerOrders.MatchTableLive;
            if ((_tableLive && !tableLive) || (turnPlay && !_turnPlay)) presenter.ClearDeliveredMugs();
            _tableLive = tableLive;
            _turnPlay = turnPlay;

            if (_queue == null) return;

            TickAuthority();
        }

        // ------------------------------------------------------------------ authority

        void TickAuthority()
        {
            int orderBefore = _scheduler.CurrentOrderId;

            WaiterDeliveryStart start;
            WaiterTickResult result = _scheduler.Tick(_queue, beerOrders.BuildAuthoritativeRoster(),
                Time.unscaledTimeAsDouble, out start);

            switch (result)
            {
                case WaiterTickResult.Began:
                    BeginDelivery(start);
                    break;

                case WaiterTickResult.SkippedEmpty:
                    Debug.Log("Beer order skipped: no recipient is still at the table.", this);
                    break;

                case WaiterTickResult.LostActive:
                    PublishEnded(orderBefore, true);
                    break;
            }

            // Safety net: the authority's own waiter never started or disappeared.
            if (_scheduler.IsDelivering && presenter.ActiveOrderId != _scheduler.CurrentOrderId &&
                Time.unscaledTime - _beganAt > presenterWatchdogSeconds)
            {
                Debug.LogWarning("Waiter for beer order #" + _scheduler.CurrentOrderId + " is not running; completing it.", this);
                int orderId = _scheduler.CurrentOrderId;
                _scheduler.NotifyWaiterExited(_queue, orderId);
                PublishEnded(orderId, true);
            }
        }

        void BeginDelivery(WaiterDeliveryStart start)
        {
            int[] seats = start.ValidRecipients;

            // Only the walking order is chosen here; the recipients are exactly the order's.
            WaiterRoutePlan route = presenter.CanPresent
                ? WaiterRoutePlanner.Plan(presenter.StopAnglesFromDoor(seats))
                : new WaiterRoutePlan(Identity(seats.Length), WaiterRouteMode.ForwardAndBack);

            int[] visit = new int[seats.Length];
            for (int i = 0; i < visit.Length; i++) visit[i] = seats[route.VisitOrder[i]];

            WaiterDeliveryPlan plan = new WaiterDeliveryPlan(start.Order.OrderId, start.Order.RequesterSeat, visit, route.Mode);
            _beganAt = Time.unscaledTime;

            Debug.Log("Waiter sets off: " + plan + " (ordered for [" + string.Join(",", start.Order.RecipientSeats) +
                "], " + seats.Length + " still at the table)", this);

            if (beerOrders.IsOnline)
            {
                if (_service != null) _service.BroadcastWaiterStarted(plan);
            }
            else
            {
                presenter.Begin(plan);
            }
        }

        /// <summary>Revalidates the recipient right before the handoff and tells everyone.</summary>
        void OnStopReached(int orderId, int seat)
        {
            if (_queue == null || !_scheduler.IsDelivering || orderId != _scheduler.CurrentOrderId) return;

            bool deliver = WaiterDeliveryScheduler.IsValidRecipient(beerOrders.BuildAuthoritativeRoster(), seat);
            if (!deliver) Debug.Log("Beer for seat " + seat + " stays on the tray: no longer at the table.", this);

            // Handed over: from now on it is a drinkable beer, full, owned by that seat. A mug
            // that stays on the tray is never registered and leaves with the waiter.
            if (deliver) beerOrders.RegisterDeliveredBeer(new BeerId(orderId, seat));

            if (beerOrders.IsOnline)
            {
                if (_service != null) _service.BroadcastWaiterHandoff(orderId, seat, deliver);
            }
            else
            {
                presenter.SetHandoffDecision(orderId, seat, deliver);
            }
        }

        /// <summary>Only the authority's own waiter, fully outside, completes an order.</summary>
        void OnWaiterExited(int orderId, bool aborted)
        {
            if (_queue == null || !_scheduler.IsDelivering || orderId != _scheduler.CurrentOrderId) return;

            _scheduler.NotifyWaiterExited(_queue, orderId);
            Debug.Log("Beer order #" + orderId + (aborted ? " delivery aborted" : " delivered; waiter has left") + "\n" +
                _queue.Describe(), this);

            PublishEnded(orderId, aborted);
        }

        void PublishEnded(int orderId, bool aborted)
        {
            if (beerOrders.IsOnline)
            {
                if (_service != null) _service.BroadcastWaiterEnded(orderId, aborted);
            }
            else if (aborted && presenter.ActiveOrderId == orderId)
            {
                presenter.Abort();
            }
        }

        // ------------------------------------------------------------------ every machine (online)

        void OnDeliveryStarted(WaiterDeliveryPlan plan)
        {
            if (beerOrders.TurnPlayActive) presenter.Begin(plan);
        }

        void OnHandoffDecided(int orderId, int seat, bool deliver)
        {
            presenter.SetHandoffDecision(orderId, seat, deliver);
        }

        void OnDeliveryEnded(int orderId, bool aborted)
        {
            if (presenter.ActiveOrderId != orderId) return;

            if (aborted) presenter.Abort();
            else presenter.MarkEnded(orderId);
        }

        static int[] Identity(int count)
        {
            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            return order;
        }
    }
}
