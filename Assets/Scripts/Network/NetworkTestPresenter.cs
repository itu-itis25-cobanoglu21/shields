using UnityEngine;
using Shields.Match;
using Shields.Players;
using Shields.Presentation;
using Shields.Session;
using Shields.Table;

namespace Shields.Network
{
    /// <summary>
    /// Builds the network test view on whichever machine it runs on (TASK 008 sections 9
    /// and 10).
    ///
    /// The seating ring is deterministic for a given player count, so nothing about the
    /// table, the bodies or their positions is replicated. Each machine builds the same
    /// layout locally from the shared target player count and then differs in exactly one
    /// respect: which seat it owns, which it reads from the replicated
    /// <see cref="NetworkSeatRegistry"/>.
    ///
    /// No cards are dealt and no ShieldsMatch is created. This is connectivity and
    /// ownership only.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkTestPresenter : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] TableLayoutController tableLayout;
        [SerializeField] GameplayCamera gameplayCamera;
        [SerializeField] CrosshairInteraction crosshair;

        [Tooltip("Ended before the network test builds, so the offline match never overlaps it.")]
        [SerializeField] MatchBootstrap offlineBootstrap;

        [Header("First Person View")]
        [Tooltip("Where the local view rests: 0 aims at the deck, 1 at the player's own seat.")]
        [Range(0f, 1f)]
        [SerializeField] float restAimTowardOwnCards = 0.08f;

        NetworkSeatRegistry _seatRegistry;
        NetworkHeadPoseBridge _headPoseBridge;

        int _builtForSeatCount = -1;
        int _builtForLocalSeat = NetworkSeatRegistry.NoSeat;

        /// <summary>True while the network test layout is on screen.</summary>
        public bool TestLayoutActive { get; private set; }

        public int LocalSeatIndex
        {
            get { return _seatRegistry != null ? _seatRegistry.LocalSeatIndex : NetworkSeatRegistry.NoSeat; }
        }

        public void Bind(NetworkSeatRegistry seatRegistry, NetworkHeadPoseBridge headPoseBridge)
        {
            Unbind();

            _seatRegistry = seatRegistry;
            _headPoseBridge = headPoseBridge;

            if (_seatRegistry != null)
            {
                _seatRegistry.SeatsChanged += OnSeatsChanged;
                _seatRegistry.NetworkTestStarted += OnNetworkTestStarted;
            }
        }

        public void Unbind()
        {
            if (_seatRegistry == null) return;

            _seatRegistry.SeatsChanged -= OnSeatsChanged;
            _seatRegistry.NetworkTestStarted -= OnNetworkTestStarted;
            _seatRegistry = null;
        }

        void OnDestroy()
        {
            Unbind();
        }

        // ------------------------------------------------------------------ reacting

        void OnNetworkTestStarted()
        {
            RebuildIfNeeded();
        }

        /// <summary>
        /// Seats changed. While the test is up this matters, because a participant that
        /// joined or left changes which bodies are remote humans.
        /// </summary>
        void OnSeatsChanged()
        {
            if (_seatRegistry == null) return;

            if (!_seatRegistry.TestStarted)
            {
                if (TestLayoutActive) TearDown();
                return;
            }

            RebuildIfNeeded();
        }

        void RebuildIfNeeded()
        {
            if (_seatRegistry == null || !_seatRegistry.TestStarted) return;

            int seatCount = _seatRegistry.TargetPlayerCount;
            int localSeat = _seatRegistry.LocalSeatIndex;

            // A client can receive the start flag before its own seat has replicated.
            // Building then would park the camera at no seat, so wait for the seat.
            if (localSeat == NetworkSeatRegistry.NoSeat) return;

            bool sameLayout = TestLayoutActive
                && seatCount == _builtForSeatCount
                && localSeat == _builtForLocalSeat;

            if (sameLayout)
            {
                // Same seats, but occupancy may have changed. Refresh roles only.
                ApplySeatRoles(localSeat);
                return;
            }

            Build(seatCount, localSeat);
        }

        // ------------------------------------------------------------------ building

        void Build(int seatCount, int localSeat)
        {
            if (tableLayout == null)
            {
                Debug.LogError("NetworkTestPresenter has no TableLayoutController assigned.", this);
                return;
            }

            // The offline match owns the same table and camera, so it must stand down first.
            if (offlineBootstrap != null && offlineBootstrap.MatchRunning) offlineBootstrap.EndMatch();

            // Detach before rebuilding: the camera rides under a seat that is about to be
            // destroyed along with the old layout.
            if (gameplayCamera != null) gameplayCamera.Detach();

            tableLayout.BuildLayout(seatCount);

            ApplySeatRoles(localSeat);
            AttachLocalCamera(localSeat);

            if (_headPoseBridge != null) _headPoseBridge.ClearRemotePoses();

            _builtForSeatCount = seatCount;
            _builtForLocalSeat = localSeat;
            TestLayoutActive = true;

            Debug.Log(string.Format("Network test layout built: {0} seats, local seat {1}.\n{2}",
                seatCount, localSeat, _seatRegistry.Describe()), this);
        }

        /// <summary>
        /// Decides, per seat, what this machine is looking at. Every answer comes from the
        /// replicated registry; nothing is inferred from prefabs or components.
        /// </summary>
        void ApplySeatRoles(int localSeat)
        {
            var players = tableLayout.Players;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerCharacter character = players[i];
                int seat = character.PlayerIndex;

                bool isLocal = seat == localSeat;
                SeatOccupant occupant = _seatRegistry.OccupantAt(seat);

                // Nobody is a bot during TASK 008. An unoccupied seat is a passive
                // placeholder body, which is not the same thing as a bot.
                character.AssignIdentity(seat, isLocal, false);

                SeatedHeadLook look = character.GetComponent<SeatedHeadLook>();
                if (look != null)
                {
                    look.Bind(character.HeadPivot);

                    // Only the seat this machine owns reads the mouse. Remote humans are
                    // driven by the head pose bridge; empty seats stay still.
                    look.enabled = isLocal;

                    if (isLocal) RestLookOnTable(character, look);
                }

                // Presentation marker only, and there are no bots yet, so it stays off.
                BotController bot = character.GetComponent<BotController>();
                if (bot != null) bot.enabled = false;

                if (!isLocal && !occupant.IsHuman) ResetPivot(character);
            }
        }

        static void ResetPivot(PlayerCharacter character)
        {
            SeatedHeadLook.ApplyPivotAngles(character.HeadPivot, 0f, 0f);
        }

        void AttachLocalCamera(int localSeat)
        {
            if (gameplayCamera == null) return;

            PlayerCharacter local = tableLayout.GetPlayer(localSeat);
            bool attached = local != null && local.CameraAnchor != null;

            if (attached) gameplayCamera.AttachTo(local.CameraAnchor);
            else gameplayCamera.Detach();

            if (crosshair != null) crosshair.enabled = attached;

            if (!attached)
            {
                Debug.LogWarning(string.Format(
                    "Seat {0} has no camera anchor; the local camera stayed parked.", localSeat), this);
            }
        }

        /// <summary>Rests the local view between the deck and this player's own seat.</summary>
        void RestLookOnTable(PlayerCharacter player, SeatedHeadLook look)
        {
            if (player.CameraAnchor == null || player.HeadPivot == null) return;

            Vector3 aim = tableLayout.DeckAimPoint;
            PlayerTableArea ownArea = tableLayout.GetAreaForPlayer(player.PlayerIndex);
            if (ownArea != null) aim = Vector3.Lerp(aim, ownArea.transform.position, restAimTowardOwnCards);

            look.restPitch = SeatedHeadLook.NeutralPitchToward(
                player.HeadPivot, player.CameraAnchor.position, aim);

            look.ResetLook();
        }

        // ------------------------------------------------------------------ teardown

        /// <summary>Clears the test layout and parks the camera, leaving the room untouched.</summary>
        public void TearDown()
        {
            if (gameplayCamera != null) gameplayCamera.Detach();
            if (crosshair != null) crosshair.enabled = false;
            if (tableLayout != null) tableLayout.ClearLayout();
            if (_headPoseBridge != null) _headPoseBridge.ClearRemotePoses();

            _builtForSeatCount = -1;
            _builtForLocalSeat = NetworkSeatRegistry.NoSeat;
            TestLayoutActive = false;
        }
    }
}
