using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Shields.Session;

namespace Shields.Network
{
    /// <summary>
    /// One replicated seat. Unmanaged so it can live in a <see cref="NetworkList{T}"/>.
    /// </summary>
    public struct SeatSlot : INetworkSerializable, IEquatable<SeatSlot>
    {
        public byte Kind;
        public ulong ParticipantId;

        public static SeatSlot From(SeatOccupant occupant)
        {
            return new SeatSlot
            {
                Kind = (byte)occupant.Kind,
                ParticipantId = occupant.ParticipantId
            };
        }

        public SeatOccupant ToOccupant()
        {
            return new SeatOccupant((SeatOccupantKind)Kind, ParticipantId);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref ParticipantId);
        }

        public bool Equals(SeatSlot other)
        {
            return Kind == other.Kind && ParticipantId == other.ParticipantId;
        }

        public override bool Equals(object obj)
        {
            return obj is SeatSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() ^ ParticipantId.GetHashCode();
        }
    }

    /// <summary>
    /// The authoritative participant to seat mapping, owned by the host and replicated to
    /// every client (TASK 008 sections 7 and 12).
    ///
    /// The host is the only writer. Clients never choose a seat; they read the replicated
    /// table and work out which seat is theirs by matching their own NGO client id. That
    /// is why <see cref="LocalSeatIndex"/> is derived rather than sent per client: the same
    /// replicated data reads correctly on every machine, and nothing anywhere assumes the
    /// local player is seat 0.
    ///
    /// The seating rules themselves live in the pure <see cref="SeatRegistry"/>, which is
    /// unit tested without any networking. This class is only the replication shell.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkSeatRegistry : NetworkBehaviour
    {
        /// <summary>Returned when the local participant holds no seat.</summary>
        public const int NoSeat = SeatRegistry.NoSeat;

        readonly NetworkList<SeatSlot> _seats = new NetworkList<SeatSlot>(
            null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> _targetPlayerCount = new NetworkVariable<int>(
            6, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> _deckCount = new NetworkVariable<int>(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<bool> _networkTestStarted = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // The match generation the current seating belongs to. Written together with the
        // seats, so a client knows its seat table is the new match's before it shows it.
        readonly NetworkVariable<int> _seatingGeneration = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Host side authority. Null on clients, which only mirror the snapshot.</summary>
        SeatRegistry _authoritative;

        /// <summary>
        /// The host's authoritative seat map, or null on a client. Gameplay routers use it
        /// to turn a sender's client id into the seat the host says they own.
        /// </summary>
        public SeatRegistry AuthoritativeSeats { get { return _authoritative; } }

        /// <summary>Raised on every machine when the seating or the shared config changes.</summary>
        public event Action SeatsChanged;

        /// <summary>Raised on every machine when the host starts the network test.</summary>
        public event Action NetworkTestStarted;

        public int TargetPlayerCount { get { return _targetPlayerCount.Value; } }

        public int DeckCount { get { return _deckCount.Value; } }

        public bool TestStarted { get { return _networkTestStarted.Value; } }

        /// <summary>The match generation whose seating this table currently shows.</summary>
        public int SeatingGeneration { get { return _seatingGeneration.Value; } }

        public int SeatCount { get { return _seats.Count; } }

        /// <summary>
        /// The seat this machine owns, or <see cref="NoSeat"/>. Derived from the replicated
        /// table and the local client id, so each client naturally resolves a different seat.
        /// </summary>
        public int LocalSeatIndex
        {
            get
            {
                if (NetworkManager == null) return NoSeat;

                return SeatOfParticipant(NetworkManager.LocalClientId);
            }
        }

        public int HumanCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _seats.Count; i++)
                    if (_seats[i].Kind == (byte)SeatOccupantKind.Human) count++;

                return count;
            }
        }

        public SeatOccupant OccupantAt(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _seats.Count) return SeatOccupant.Empty;

            return _seats[seatIndex].ToOccupant();
        }

        public int SeatOfParticipant(ulong participantId)
        {
            for (int i = 0; i < _seats.Count; i++)
            {
                SeatSlot slot = _seats[i];
                if (slot.Kind == (byte)SeatOccupantKind.Human && slot.ParticipantId == participantId) return i;
            }

            return NoSeat;
        }

        /// <summary>True when that seat is a human this machine does not control.</summary>
        public bool IsRemoteHumanSeat(int seatIndex)
        {
            return OccupantAt(seatIndex).IsHuman && seatIndex != LocalSeatIndex;
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            _seats.OnListChanged += OnSeatListChanged;
            _targetPlayerCount.OnValueChanged += OnConfigChanged;
            _deckCount.OnValueChanged += OnConfigChanged;
            _networkTestStarted.OnValueChanged += OnTestStartedChanged;

            if (IsServer)
            {
                _authoritative = new SeatRegistry(Mathf.Clamp(_targetPlayerCount.Value,
                    SeatRegistry.MinSeats, SeatRegistry.MaxSeats));

                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

                // The host occupies a seat like anyone else, and any client that finished
                // connecting before this object spawned is seated here too.
                AssignSeat(NetworkManager.LocalClientId);
                foreach (ulong clientId in NetworkManager.ConnectedClientsIds) AssignSeat(clientId);

                PublishSnapshot();
            }

            RaiseSeatsChanged();
        }

        public override void OnNetworkDespawn()
        {
            _seats.OnListChanged -= OnSeatListChanged;
            _targetPlayerCount.OnValueChanged -= OnConfigChanged;
            _deckCount.OnValueChanged -= OnConfigChanged;
            _networkTestStarted.OnValueChanged -= OnTestStartedChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            _authoritative = null;
        }

        // ------------------------------------------------------------------ host writes

        /// <summary>
        /// Host only. Resizes the table, keeping everyone already seated in their existing
        /// order so a connected client never silently loses its seat.
        /// </summary>
        public void SetTargetPlayerCount(int playerCount)
        {
            if (!IsServer || _authoritative == null) return;
            if (_networkTestStarted.Value) return;

            int clamped = Mathf.Clamp(playerCount, SeatRegistry.MinSeats, SeatRegistry.MaxSeats);
            if (clamped == _authoritative.SeatCount) return;

            IReadOnlyList<ulong> seated = _authoritative.Participants();

            _authoritative.Reset(clamped);
            for (int i = 0; i < seated.Count; i++) _authoritative.AssignSeat(seated[i]);

            _targetPlayerCount.Value = clamped;
            PublishSnapshot();
        }

        /// <summary>
        /// Host only. Seats a new match: the shuffled seating, bots included, tagged with
        /// the match's generation. Returns false, changing nothing, when the seating does
        /// not hold exactly the humans currently seated.
        /// </summary>
        public bool ApplyMatchSeating(IReadOnlyList<MatchParticipant> seating, int generation)
        {
            if (!IsServer || _authoritative == null) return false;
            if (!_authoritative.ApplyMatchSeating(seating)) return false;

            PublishSnapshot();
            _seatingGeneration.Value = generation;
            return true;
        }

        /// <summary>
        /// Host only. Back to the lobby: bot seats are freed, humans keep theirs, and any
        /// connected participant who has no seat (joined while the table was full of a
        /// match's bots) takes a free one.
        /// </summary>
        public void ReturnToLobbySeating(ulong leaving = SeatOccupant.NoParticipant)
        {
            if (!IsServer || _authoritative == null) return;

            _authoritative.ClearBots();

            // A participant who is disconnecting right now may still be listed as
            // connected inside the disconnect callback; never seat them again.
            if (leaving != SeatOccupant.NoParticipant) _authoritative.ReleaseParticipant(leaving);

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                if (clientId != leaving && _authoritative.SeatOf(clientId) == SeatRegistry.NoSeat) AssignSeat(clientId);

            PublishSnapshot();
        }

        /// <summary>Host only. A connected participant has no seat yet although one is free.</summary>
        public bool HasUnseatedParticipantAndFreeSeat()
        {
            if (!IsServer || _authoritative == null || NetworkManager == null) return false;
            if (_authoritative.OccupiedSeatCount >= _authoritative.SeatCount) return false;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                if (_authoritative.SeatOf(clientId) == SeatRegistry.NoSeat) return true;

            return false;
        }

        /// <summary>Host only. Deck count is shared now and used once gameplay is networked.</summary>
        public void SetDeckCount(int deckCount)
        {
            if (!IsServer) return;
            if (_networkTestStarted.Value) return;

            _deckCount.Value = Mathf.Clamp(deckCount, 1, 2);
        }

        /// <summary>Host only. Tells every client to build the shared network test layout.</summary>
        public void StartNetworkTest()
        {
            if (!IsServer) return;

            _networkTestStarted.Value = true;
        }

        /// <summary>Host only. Returns everyone to the pre test state.</summary>
        public void StopNetworkTest()
        {
            if (!IsServer) return;

            _networkTestStarted.Value = false;
        }

        // ------------------------------------------------------------------ host seating

        void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            AssignSeat(clientId);
            PublishSnapshot();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer || _authoritative == null) return;

            // Pre match disconnect: the seat goes straight back into the pool.
            if (_authoritative.ReleaseParticipant(clientId) == SeatRegistry.NoSeat) return;

            PublishSnapshot();
        }

        void AssignSeat(ulong clientId)
        {
            if (_authoritative == null) return;

            if (_authoritative.AssignSeat(clientId) == SeatRegistry.NoSeat)
            {
                Debug.LogWarning(string.Format(
                    "Client {0} connected but every seat is taken; it has no seat.", clientId), this);
            }
        }

        /// <summary>Pushes the authoritative table into the replicated list.</summary>
        void PublishSnapshot()
        {
            if (!IsServer || _authoritative == null) return;

            SeatOccupant[] snapshot = _authoritative.Snapshot();

            while (_seats.Count > snapshot.Length) _seats.RemoveAt(_seats.Count - 1);

            for (int i = 0; i < snapshot.Length; i++)
            {
                SeatSlot slot = SeatSlot.From(snapshot[i]);

                if (i < _seats.Count)
                {
                    if (!_seats[i].Equals(slot)) _seats[i] = slot;
                }
                else
                {
                    _seats.Add(slot);
                }
            }
        }

        // ------------------------------------------------------------------ replication

        void OnSeatListChanged(NetworkListEvent<SeatSlot> change)
        {
            RaiseSeatsChanged();
        }

        void OnConfigChanged(int previous, int current)
        {
            RaiseSeatsChanged();
        }

        void OnTestStartedChanged(bool previous, bool current)
        {
            RaiseSeatsChanged();

            if (!current) return;

            Action started = NetworkTestStarted;
            if (started != null) started();
        }

        void RaiseSeatsChanged()
        {
            Action changed = SeatsChanged;
            if (changed != null) changed();
        }

        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("{0} seats, {1} human(s), local seat {2}", SeatCount, HumanCount, LocalSeatIndex);

            for (int i = 0; i < _seats.Count; i++)
                sb.AppendFormat("\n  seat {0}: {1}", i, _seats[i].ToOccupant());

            return sb.ToString();
        }
    }
}
