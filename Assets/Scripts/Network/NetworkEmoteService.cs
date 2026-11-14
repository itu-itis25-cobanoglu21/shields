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
    /// <summary>
    /// The online side of hand emotes (Middle Finger, Point, Handshake). The host owns the one
    /// <see cref="EmoteAuthority"/>; every machine, the host included, sends its own seat's
    /// emote state through the same server RPC, and the host broadcasts what each seat shows
    /// and every handshake pair it makes or ends.
    ///
    /// Low frequency and reliable: a client sends only when what it holds changes (press,
    /// release, a new handshake target, a pointing-reach step), never every frame. Where a
    /// hand points comes from the already replicated head pose, so no aim is sent at all.
    ///
    /// Cosmetic, kept apart from the match: it only reads the host's match for who is alive
    /// and the host's seat map for who the sender is. The sender never names its seat.
    /// Everything is tagged with the match generation and dropped when turn play ends.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkEmoteService : NetworkBehaviour
    {
        [SerializeField] NetworkSeatRegistry seatRegistry;
        [SerializeField] NetworkMatchController matchController;

        // Host only.
        EmoteAuthority _authority;
        readonly List<int> _cleared = new List<int>();
        readonly List<HandshakePair> _started = new List<HandshakePair>();
        readonly List<HandshakeEnd> _ended = new List<HandshakeEnd>();
        EmoteSeat[] _roster = new EmoteSeat[0];

        /// <summary>Raised on every machine when the host changed what a seat shows.</summary>
        public event Action<int, EmoteState> StateChanged;

        /// <summary>Raised on every machine when the host paired two seats for a handshake.</summary>
        public event Action<HandshakePair> HandshakeStarted;

        /// <summary>Raised on every machine when the host ended a handshake (played out or cancelled).</summary>
        public event Action<HandshakeEnd> HandshakeEnded;

        public void Bind(NetworkSeatRegistry registry, NetworkMatchController match)
        {
            seatRegistry = registry;
            matchController = match;
        }

        int HostGeneration { get { return matchController != null ? matchController.HostGeneration : 0; } }

        bool IsCurrentGeneration(int generation)
        {
            return matchController == null || generation == matchController.Generation;
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (matchController != null)
            {
                matchController.PhaseChanged += OnPhaseChanged;
                OnPhaseChanged(matchController.Phase);
            }

            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (matchController != null) matchController.PhaseChanged -= OnPhaseChanged;
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            _authority = null;
        }

        void OnPhaseChanged(OnlineMatchPhase phase)
        {
            if (!IsServer) return;

            // Emotes belong to one live match. Every machine drops its own presentation when
            // turn play ends, so nothing needs broadcasting for what is cut short here.
            if (phase == OnlineMatchPhase.Playing)
                EnsureAuthority();
            else
                _authority = null;
        }

        void EnsureAuthority()
        {
            ShieldsMatch match = matchController != null ? matchController.HostMatch : null;
            if (match == null) { _authority = null; return; }

            if (_authority == null || _authority.Generation != HostGeneration || _authority.SeatCount != match.PlayerCount)
                _authority = new EmoteAuthority(match.PlayerCount, HostGeneration);
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || matchController == null) return;
            if (matchController.Phase != OnlineMatchPhase.Playing) { _authority = null; return; }

            EnsureAuthority();
            if (_authority == null || !BuildRoster()) return;

            _cleared.Clear();
            _started.Clear();
            _ended.Clear();
            _authority.Tick(Time.unscaledTimeAsDouble, _roster, _cleared, _started, _ended);
            PublishChanges();
        }

        /// <summary>A participant left mid match: their emotes and handshake end at once.</summary>
        void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer || _authority == null || seatRegistry == null) return;

            SeatRegistry seats = seatRegistry.AuthoritativeSeats;
            int seat = seats != null ? seats.SeatOf(clientId) : SeatRegistry.NoSeat;
            if (seat == SeatRegistry.NoSeat) return;

            _cleared.Clear();
            _ended.Clear();
            _authority.RemoveSeat(seat, HandshakeEndReason.Left, _cleared, _ended);
            PublishChanges();
        }

        /// <summary>Who may emote right now, from the host's own match and seat map.</summary>
        bool BuildRoster()
        {
            ShieldsMatch match = matchController != null ? matchController.HostMatch : null;
            SeatRegistry seats = seatRegistry != null ? seatRegistry.AuthoritativeSeats : null;
            if (match == null || seats == null) return false;

            if (_roster.Length != match.PlayerCount) _roster = new EmoteSeat[match.PlayerCount];
            for (int i = 0; i < _roster.Length; i++)
            {
                MatchPlayerState player = match.Players[i];
                bool present = !player.IsBot && seats.OccupantAt(i).IsHuman;
                _roster[i] = new EmoteSeat(present, player.IsAlive && !match.IsMatchOver, player.IsBot);
            }

            return true;
        }

        void PublishChanges()
        {
            for (int i = 0; i < _cleared.Count; i++) BroadcastState(_cleared[i], EmoteState.Idle);
            for (int i = 0; i < _ended.Count; i++) BroadcastEnded(_ended[i]);
            for (int i = 0; i < _started.Count; i++)
            {
                HandshakePair pair = _started[i];
                Debug.Log("Emote: " + pair + " matched", this);
                HandshakeStartedRpc(pair.Id, (byte)pair.SeatA, (byte)pair.SeatB, HostGeneration);
            }

            _cleared.Clear();
            _started.Clear();
            _ended.Clear();
        }

        // ------------------------------------------------------------------ client intent

        /// <summary>Local intent: what this machine's own seat holds now. The host decides which seat that is.</summary>
        public bool RequestState(EmoteState state, int generation)
        {
            if (!IsSpawned) return false;

            sbyte target = (sbyte)Mathf.Clamp(state.Target, -1, SeatRegistry.MaxSeats - 1);
            SubmitEmoteRpc((byte)state.Flags, state.Reach, target, generation);
            return true;
        }

        /// <summary>Local: gameplay took this machine's right hand during a handshake.</summary>
        public bool RequestInterrupt(int generation)
        {
            if (!IsSpawned) return false;

            InterruptHandshakeRpc(generation);
            return true;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        void SubmitEmoteRpc(byte flags, byte reach, sbyte target, int generation, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            int seat = SenderSeat(rpcParams);
            if (seat == SeatRegistry.NoSeat || matchController == null || matchController.Phase != OnlineMatchPhase.Playing) return;

            EnsureAuthority();
            if (_authority == null || generation != HostGeneration || !BuildRoster()) return;

            EmoteState applied;
            EmoteSubmitResult result = _authority.Submit(seat, new EmoteState((EmoteFlags)flags, reach, target), generation,
                _roster, out applied);

            if (result == EmoteSubmitResult.Accepted) BroadcastState(seat, applied);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        void InterruptHandshakeRpc(int generation, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            int seat = SenderSeat(rpcParams);
            if (seat == SeatRegistry.NoSeat || _authority == null) return;

            _ended.Clear();
            if (_authority.Interrupt(seat, generation, _ended)) PublishChanges();
        }

        /// <summary>The sender's seat from the host's own participant map; never from the message.</summary>
        int SenderSeat(RpcParams rpcParams)
        {
            SeatRegistry seats = seatRegistry != null ? seatRegistry.AuthoritativeSeats : null;
            return seats != null ? seats.SeatOf(rpcParams.Receive.SenderClientId) : SeatRegistry.NoSeat;
        }

        // ------------------------------------------------------------------ host to everyone

        void BroadcastState(int seat, EmoteState state)
        {
            if (!IsSpawned) return;

            EmoteStateRpc((byte)seat, (byte)state.Flags, state.Reach, HostGeneration);
        }

        void BroadcastEnded(HandshakeEnd end)
        {
            if (!IsSpawned) return;

            Debug.Log("Emote: " + end, this);
            HandshakeEndedRpc(end.Pair.Id, (byte)end.Pair.SeatA, (byte)end.Pair.SeatB, (byte)end.Reason, HostGeneration);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void EmoteStateRpc(byte seat, byte flags, byte reach, int generation)
        {
            if (!IsCurrentGeneration(generation)) return;

            Action<int, EmoteState> changed = StateChanged;
            if (changed != null) changed(seat, new EmoteState((EmoteFlags)flags, reach, EmoteState.NoTarget));
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void HandshakeStartedRpc(int pairId, byte seatA, byte seatB, int generation)
        {
            if (!IsCurrentGeneration(generation)) return;

            Action<HandshakePair> started = HandshakeStarted;
            if (started != null) started(new HandshakePair(pairId, seatA, seatB, 0.0));
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void HandshakeEndedRpc(int pairId, byte seatA, byte seatB, byte reason, int generation)
        {
            if (!IsCurrentGeneration(generation)) return;

            Action<HandshakeEnd> ended = HandshakeEnded;
            if (ended != null) ended(new HandshakeEnd(new HandshakePair(pairId, seatA, seatB, 0.0), (HandshakeEndReason)reason));
        }
    }
}
