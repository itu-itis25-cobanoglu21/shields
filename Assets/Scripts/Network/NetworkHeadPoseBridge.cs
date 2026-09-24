using Unity.Netcode;
using UnityEngine;
using Shields.Players;
using Shields.Table;

namespace Shields.Network
{
    /// <summary>
    /// Sends the local player's head look to everyone else and drives the remote heads
    /// with what comes back (TASK 008 section 11).
    ///
    /// Only two angles per player travel, never a transform: seats are placed
    /// deterministically from the shared config, so bodies need no replication at all. The
    /// stream is throttled, dead banded and sent unreliably, because a dropped head angle
    /// is replaced by the next one a few milliseconds later and is not worth a resend.
    ///
    /// Two rules keep it from fighting local input (section 20):
    /// a client's pose is accepted only for the seat the host says that client owns, and
    /// every machine ignores the broadcast for its own seat.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkHeadPoseBridge : NetworkBehaviour
    {
        [Tooltip("Seat ownership. Assigned by the network bootstrap.")]
        [SerializeField] NetworkSeatRegistry seatRegistry;

        [Tooltip("Supplies the PlayerCharacter for a seat. Assigned by the network bootstrap.")]
        [SerializeField] TableLayoutController tableLayout;

        [Header("Bandwidth")]
        [Tooltip("How many pose updates a client sends per second at most.")]
        [Range(5f, 60f)]
        [SerializeField] float sendsPerSecond = 20f;

        [Tooltip("Smallest change in degrees worth sending. Stops idle players from sending at all.")]
        [Range(0f, 2f)]
        [SerializeField] float minimumDegreesToSend = 0.2f;

        [Header("Remote Smoothing")]
        [Tooltip("How quickly a remote head catches up to the latest received angles.")]
        [Range(0f, 40f)]
        [SerializeField] float remoteSmoothing = 18f;

        /// <summary>Latest angles received per seat, and whether that seat has any yet.</summary>
        Vector2[] _targetAngles = new Vector2[0];
        bool[] _hasTarget = new bool[0];
        Vector2[] _currentAngles = new Vector2[0];

        float _nextSendTime;
        Vector2 _lastSentAngles;
        bool _hasSentOnce;

        public void Bind(NetworkSeatRegistry registry, TableLayoutController layout)
        {
            seatRegistry = registry;
            tableLayout = layout;
        }

        public override void OnNetworkSpawn()
        {
            ResizeBuffers();
        }

        void ResizeBuffers()
        {
            int seats = seatRegistry != null ? seatRegistry.SeatCount : 0;
            if (seats == _targetAngles.Length) return;

            _targetAngles = new Vector2[seats];
            _hasTarget = new bool[seats];
            _currentAngles = new Vector2[seats];
        }

        void Update()
        {
            if (seatRegistry == null || !IsSpawned) return;

            ResizeBuffers();
            SendLocalPose();
        }

        /// <summary>
        /// Remote heads are driven after every local Update has run, so the local head look
        /// has already written its own pivot this frame and the two never interleave.
        /// </summary>
        void LateUpdate()
        {
            if (seatRegistry == null || tableLayout == null || !IsSpawned) return;

            ApplyRemotePoses();
        }

        // ------------------------------------------------------------------ sending

        void SendLocalPose()
        {
            int localSeat = seatRegistry.LocalSeatIndex;
            if (localSeat == NetworkSeatRegistry.NoSeat) return;

            SeatedHeadLook look = LocalHeadLook(localSeat);
            if (look == null) return;

            if (Time.unscaledTime < _nextSendTime) return;

            Vector2 angles = look.PivotAngles;

            // Nothing moved enough to be worth a packet.
            if (_hasSentOnce &&
                Mathf.Abs(Mathf.DeltaAngle(angles.x, _lastSentAngles.x)) < minimumDegreesToSend &&
                Mathf.Abs(Mathf.DeltaAngle(angles.y, _lastSentAngles.y)) < minimumDegreesToSend)
            {
                return;
            }

            _nextSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, sendsPerSecond);
            _lastSentAngles = angles;
            _hasSentOnce = true;

            SubmitHeadPoseRpc(angles.x, angles.y);
        }

        SeatedHeadLook LocalHeadLook(int localSeat)
        {
            if (tableLayout == null) return null;

            PlayerCharacter character = tableLayout.GetPlayer(localSeat);
            if (character == null) return null;

            SeatedHeadLook look = character.GetComponent<SeatedHeadLook>();

            // A disabled look means this machine does not drive that seat, so it has no
            // pose of its own to report.
            return look != null && look.enabled ? look : null;
        }

        // ------------------------------------------------------------------ rpcs

        /// <summary>
        /// Client to host. The host decides which seat this pose belongs to; the sender
        /// never names a seat, so a client cannot move another player's head.
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void SubmitHeadPoseRpc(float yaw, float pitch, RpcParams rpcParams = default)
        {
            if (seatRegistry == null) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            int seat = seatRegistry.SeatOfParticipant(sender);

            // Ownership validation: no seat, no broadcast.
            if (seat == NetworkSeatRegistry.NoSeat) return;

            BroadcastHeadPoseRpc(seat, yaw, pitch);
        }

        /// <summary>Host to everyone, including itself, so all machines run the same path.</summary>
        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
        void BroadcastHeadPoseRpc(int seat, float yaw, float pitch)
        {
            ResizeBuffers();

            if (seat < 0 || seat >= _targetAngles.Length) return;

            // Never apply our own pose back onto ourselves: that is the feedback loop.
            if (seat == seatRegistry.LocalSeatIndex) return;

            _targetAngles[seat] = new Vector2(yaw, pitch);

            if (!_hasTarget[seat])
            {
                _hasTarget[seat] = true;
                _currentAngles[seat] = _targetAngles[seat];
            }
        }

        // ------------------------------------------------------------------ applying

        void ApplyRemotePoses()
        {
            int localSeat = seatRegistry.LocalSeatIndex;
            float t = remoteSmoothing > 0f
                ? 1f - Mathf.Exp(-remoteSmoothing * Time.unscaledDeltaTime)
                : 1f;

            for (int seat = 0; seat < _targetAngles.Length; seat++)
            {
                if (seat == localSeat || !_hasTarget[seat]) continue;
                if (!seatRegistry.OccupantAt(seat).IsHuman) continue;

                PlayerCharacter character = tableLayout.GetPlayer(seat);
                if (character == null || character.HeadPivot == null) continue;

                Vector2 target = _targetAngles[seat];
                Vector2 current = _currentAngles[seat];

                // DeltaAngle keeps the interpolation on the short way round.
                current.x += Mathf.DeltaAngle(current.x, target.x) * t;
                current.y += Mathf.DeltaAngle(current.y, target.y) * t;
                _currentAngles[seat] = current;

                SeatedHeadLook.ApplyPivotAngles(character.HeadPivot, current.x, current.y);
            }
        }

        /// <summary>Drops every stored remote pose, for when the layout is rebuilt.</summary>
        public void ClearRemotePoses()
        {
            for (int i = 0; i < _hasTarget.Length; i++) _hasTarget[i] = false;

            _hasSentOnce = false;
            _nextSendTime = 0f;
        }
    }
}
