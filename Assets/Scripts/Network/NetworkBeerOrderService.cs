using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Shields.Game;
using Shields.Online;
using Shields.Session;
using Shields.Social;

namespace Shields.Network
{
    /// <summary>A beer order request on the wire: recipient seats only, never the requester.</summary>
    public struct BeerOrderRequestMessage : INetworkSerializable
    {
        public byte[] RecipientSeats;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int count = RecipientSeats != null ? RecipientSeats.Length : 0;
            serializer.SerializeValue(ref count);

            // Never trust a length off the wire beyond the biggest table.
            if (serializer.IsReader) RecipientSeats = new byte[Mathf.Clamp(count, 0, SeatRegistry.MaxSeats)];

            for (int i = 0; i < RecipientSeats.Length; i++) serializer.SerializeValue(ref RecipientSeats[i]);
        }
    }

    /// <summary>The host's answer to one beer order, sent only to its requester.</summary>
    public struct BeerOrderReplyMessage : INetworkSerializable
    {
        public byte RejectionCode;
        public int OrderId;
        public byte BeerCount;
        public byte OrdersAhead;
        public float CooldownRemaining;

        public static BeerOrderReplyMessage From(BeerOrderReceipt receipt)
        {
            return new BeerOrderReplyMessage
            {
                RejectionCode = (byte)receipt.Rejection,
                OrderId = receipt.OrderId,
                BeerCount = (byte)Mathf.Clamp(receipt.BeerCount, 0, byte.MaxValue),
                OrdersAhead = (byte)Mathf.Clamp(receipt.OrdersAhead, 0, byte.MaxValue),
                CooldownRemaining = receipt.CooldownRemaining
            };
        }

        public BeerOrderReceipt ToReceipt()
        {
            return new BeerOrderReceipt((BeerOrderRejection)RejectionCode, OrderId, BeerCount, OrdersAhead, CooldownRemaining);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RejectionCode);
            serializer.SerializeValue(ref OrderId);
            serializer.SerializeValue(ref BeerCount);
            serializer.SerializeValue(ref OrdersAhead);
            serializer.SerializeValue(ref CooldownRemaining);
        }
    }

    /// <summary>
    /// A waiter delivery as every machine presents it: order, seats in visiting order,
    /// route direction. Public social data only; never any card state.
    /// </summary>
    public struct WaiterPlanMessage : INetworkSerializable
    {
        public int OrderId;
        public byte RequesterSeat;
        public byte ModeCode;
        public byte[] VisitSeats;

        public static WaiterPlanMessage From(WaiterDeliveryPlan plan)
        {
            byte[] seats = new byte[Mathf.Min(plan.VisitSeats.Length, SeatRegistry.MaxSeats)];
            for (int i = 0; i < seats.Length; i++) seats[i] = (byte)Mathf.Clamp(plan.VisitSeats[i], 0, byte.MaxValue);

            return new WaiterPlanMessage
            {
                OrderId = plan.OrderId,
                RequesterSeat = (byte)Mathf.Clamp(plan.RequesterSeat, 0, byte.MaxValue),
                ModeCode = (byte)plan.Mode,
                VisitSeats = seats
            };
        }

        public WaiterDeliveryPlan ToPlan()
        {
            int[] seats = new int[VisitSeats != null ? VisitSeats.Length : 0];
            for (int i = 0; i < seats.Length; i++) seats[i] = VisitSeats[i];

            return new WaiterDeliveryPlan(OrderId, RequesterSeat, seats, (WaiterRouteMode)ModeCode);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OrderId);
            serializer.SerializeValue(ref RequesterSeat);
            serializer.SerializeValue(ref ModeCode);

            int count = VisitSeats != null ? VisitSeats.Length : 0;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader) VisitSeats = new byte[Mathf.Clamp(count, 0, SeatRegistry.MaxSeats)];

            for (int i = 0; i < VisitSeats.Length; i++) serializer.SerializeValue(ref VisitSeats[i]);
        }
    }

    /// <summary>
    /// The online side of beer ordering. The host owns the one <see cref="BeerOrderQueue"/>;
    /// every machine, the host included, asks for an order through the same server RPC,
    /// exactly like turn actions do through <see cref="NetworkMatchController"/>.
    ///
    /// Kept apart from the match controller on purpose: beer is social and never reaches
    /// <see cref="ShieldsMatch"/>. It only reads the host's match for who is alive, and
    /// the host's seat registry for who the sender is. Its queue is not rebuilt by match
    /// snapshots; it lives while the match is Playing and is cleared when it stops.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkBeerOrderService : NetworkBehaviour
    {
        [SerializeField] NetworkSeatRegistry seatRegistry;
        [SerializeField] NetworkMatchController matchController;

        [Header("Tuning")]
        [Tooltip("Seconds between two accepted orders from the same requester. Host authoritative.")]
        [Min(0f)]
        [SerializeField] float orderCooldownSeconds = (float)BeerOrderQueue.DefaultCooldownSeconds;

        [Tooltip("Most undelivered orders at once, counting the one being delivered.")]
        [Min(1)]
        [SerializeField] int maxQueuedOrders = BeerOrderQueue.DefaultMaxOrders;

        // Host only.
        BeerOrderQueue _queue;

        /// <summary>Raised on the requesting machine when the host answers its order.</summary>
        public event Action<BeerOrderReceipt> ReceiptReceived;

        /// <summary>Raised on every machine, the host included, when a waiter delivery starts.</summary>
        public event Action<WaiterDeliveryPlan> WaiterDeliveryStarted;

        /// <summary>Raised on every machine for each stop: order id, seat, whether the mug is handed over.</summary>
        public event Action<int, int, bool> WaiterHandoffDecided;

        /// <summary>Raised on every machine when a delivery ends: order id, aborted.</summary>
        public event Action<int, bool> WaiterDeliveryEnded;

        /// <summary>
        /// The authoritative queue, on the host only and only while a match is Playing.
        /// Null on clients. This is what a host side delivery system consumes.
        /// </summary>
        public BeerOrderQueue HostQueue { get { return _queue; } }

        public float OrderCooldownSeconds { get { return orderCooldownSeconds; } }

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            if (!IsServer || matchController == null) return;

            matchController.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(matchController.Phase);
        }

        public override void OnNetworkDespawn()
        {
            if (matchController != null) matchController.PhaseChanged -= OnPhaseChanged;

            ClearQueue();
        }

        void OnPhaseChanged(OnlineMatchPhase phase)
        {
            if (!IsServer) return;

            // Orders belong to one live match. Anything else, including MatchOver, ends them.
            if (phase == OnlineMatchPhase.Playing)
            {
                if (_queue == null) _queue = new BeerOrderQueue(orderCooldownSeconds, maxQueuedOrders);
            }
            else
            {
                ClearQueue();
            }
        }

        void ClearQueue()
        {
            if (_queue != null) _queue.Clear();
            _queue = null;
        }

        // ------------------------------------------------------------------ client intent

        /// <summary>Local intent. Sends only recipient seats; the host decides who is asking.</summary>
        public bool RequestOrder(IReadOnlyList<int> recipientSeats)
        {
            if (!IsSpawned || recipientSeats == null) return false;

            byte[] seats = new byte[Mathf.Min(recipientSeats.Count, SeatRegistry.MaxSeats)];
            for (int i = 0; i < seats.Length; i++) seats[i] = (byte)Mathf.Clamp(recipientSeats[i], 0, byte.MaxValue);

            SubmitBeerOrderRpc(new BeerOrderRequestMessage { RecipientSeats = seats });
            return true;
        }

        // ------------------------------------------------------------------ host

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        void SubmitBeerOrderRpc(BeerOrderRequestMessage request, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            BeerOrderReceipt receipt;

            ShieldsMatch match = matchController != null ? matchController.HostMatch : null;
            SeatRegistry seats = seatRegistry != null ? seatRegistry.AuthoritativeSeats : null;

            if (_queue == null || match == null || seats == null ||
                matchController.Phase != OnlineMatchPhase.Playing || match.IsMatchOver)
            {
                receipt = BeerOrderReceipt.Refused(BeerOrderRejection.NotAvailable);
            }
            else
            {
                int[] recipients = new int[request.RecipientSeats != null ? request.RecipientSeats.Length : 0];
                for (int i = 0; i < recipients.Length; i++) recipients[i] = request.RecipientSeats[i];

                receipt = _queue.SubmitFromParticipant(seats, sender, recipients, BuildRoster(match, seats),
                    Time.unscaledTimeAsDouble);

                if (receipt.Accepted)
                    Debug.Log("Beer order accepted from client " + sender + "\n" + _queue.Describe(), this);
            }

            BeerOrderReplyRpc(BeerOrderReplyMessage.From(receipt), RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        void BeerOrderReplyRpc(BeerOrderReplyMessage reply, RpcParams rpcParams)
        {
            Action<BeerOrderReceipt> received = ReceiptReceived;
            if (received != null) received(reply.ToReceipt());
        }

        // ------------------------------------------------------------------ waiter presentation (host to all)

        /// <summary>
        /// Host only: who may be served right now, for the waiter's revalidation. Null
        /// when no match is Playing.
        /// </summary>
        public BeerSeat[] BuildHostRoster()
        {
            if (!IsServer || matchController == null || seatRegistry == null) return null;

            ShieldsMatch match = matchController.HostMatch;
            SeatRegistry seats = seatRegistry.AuthoritativeSeats;
            if (match == null || seats == null || matchController.Phase != OnlineMatchPhase.Playing) return null;

            return BuildRoster(match, seats);
        }

        /// <summary>Host only. Every machine starts the same waiter from this plan.</summary>
        public void BroadcastWaiterStarted(WaiterDeliveryPlan plan)
        {
            if (!IsServer || !IsSpawned) return;

            WaiterStartedRpc(WaiterPlanMessage.From(plan), HostGeneration);
        }

        int HostGeneration { get { return matchController != null ? matchController.HostGeneration : 0; } }

        /// <summary>
        /// Waiter messages belong to one match. Order ids restart every match, so a late
        /// message from an earlier match must never touch the current one's waiter.
        /// </summary>
        bool IsCurrentGeneration(int generation)
        {
            return matchController == null || generation == matchController.Generation;
        }

        /// <summary>Host only. The host's decision at one stop, so nobody disagrees on who got a beer.</summary>
        public void BroadcastWaiterHandoff(int orderId, int seat, bool delivered)
        {
            if (!IsServer || !IsSpawned) return;

            WaiterHandoffRpc(orderId, (byte)Mathf.Clamp(seat, 0, byte.MaxValue), delivered, HostGeneration);
        }

        /// <summary>Host only. The delivery is over (waiter outside) or was aborted.</summary>
        public void BroadcastWaiterEnded(int orderId, bool aborted)
        {
            if (!IsServer || !IsSpawned) return;

            WaiterEndedRpc(orderId, aborted, HostGeneration);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void WaiterStartedRpc(WaiterPlanMessage plan, int generation)
        {
            if (!IsCurrentGeneration(generation)) return;

            Action<WaiterDeliveryPlan> started = WaiterDeliveryStarted;
            if (started != null) started(plan.ToPlan());
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void WaiterHandoffRpc(int orderId, byte seat, bool delivered, int generation)
        {
            if (!IsCurrentGeneration(generation)) return;

            Action<int, int, bool> decided = WaiterHandoffDecided;
            if (decided != null) decided(orderId, seat, delivered);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void WaiterEndedRpc(int orderId, bool aborted, int generation)
        {
            // Strict even for aborts: order ids restart every match, so an earlier match's
            // "abort #1" must not hit this match's #1. That earlier waiter was already
            // removed locally when its turn play ended.
            if (!IsCurrentGeneration(generation)) return;

            Action<int, bool> ended = WaiterDeliveryEnded;
            if (ended != null) ended(orderId, aborted);
        }

        /// <summary>
        /// Who may order and be served right now, from the host's own match and seat map.
        /// A human seat whose participant has left is not present.
        /// </summary>
        static BeerSeat[] BuildRoster(ShieldsMatch match, SeatRegistry seats)
        {
            BeerSeat[] roster = new BeerSeat[match.PlayerCount];

            for (int i = 0; i < roster.Length; i++)
            {
                MatchPlayerState player = match.Players[i];
                bool present = player.IsBot || seats.OccupantAt(i).IsHuman;

                roster[i] = new BeerSeat(present, player.IsAlive, player.IsBot);
            }

            return roster;
        }
    }
}
