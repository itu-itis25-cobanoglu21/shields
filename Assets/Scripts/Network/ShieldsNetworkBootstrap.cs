using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using Shields.Match;
using Shields.Table;

namespace Shields.Network
{
    /// <summary>
    /// Wires the networking pieces together and gives the prototype UI one place to call.
    ///
    /// It owns no rules and no state of its own. The session lives in
    /// <see cref="ShieldsSessionController"/>, seat ownership in
    /// <see cref="NetworkSeatRegistry"/>, the view in <see cref="NetworkTestPresenter"/>.
    /// This just connects them and makes sure the offline prototype is stood down before
    /// anything networked takes over the table.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShieldsNetworkBootstrap : MonoBehaviour
    {
        [Header("Networking")]
        [SerializeField] ShieldsSessionController sessionController;

        [Tooltip("Scene NetworkObject carrying the seat registry and the head pose bridge.")]
        [SerializeField] NetworkSeatRegistry seatRegistry;

        [SerializeField] NetworkHeadPoseBridge headPoseBridge;

        [Tooltip("Authoritative online gameplay. Host owns the match; clients send intent.")]
        [SerializeField] NetworkMatchController matchController;

        [Header("Presentation")]
        [SerializeField] NetworkTestPresenter presenter;
        [SerializeField] OnlineMatchDriver matchDriver;
        [SerializeField] TableLayoutController tableLayout;

        [Tooltip("The existing offline prototype. Stood down whenever an online session starts.")]
        [SerializeField] MatchBootstrap offlineBootstrap;

        public ShieldsSessionController SessionController { get { return sessionController; } }

        public NetworkSeatRegistry SeatRegistry { get { return seatRegistry; } }

        public NetworkTestPresenter Presenter { get { return presenter; } }

        public NetworkMatchController MatchController { get { return matchController; } }

        /// <summary>True once the seat registry has spawned and replicated.</summary>
        public bool SeatRegistryReady
        {
            get { return seatRegistry != null && seatRegistry.IsSpawned; }
        }

        /// <summary>The seat this machine owns, or <see cref="NetworkSeatRegistry.NoSeat"/>.</summary>
        public int LocalSeatIndex
        {
            get { return SeatRegistryReady ? seatRegistry.LocalSeatIndex : NetworkSeatRegistry.NoSeat; }
        }

        void Awake()
        {
            if (headPoseBridge != null) headPoseBridge.Bind(seatRegistry, tableLayout);
            if (presenter != null) presenter.Bind(seatRegistry, headPoseBridge);
            if (matchController != null) matchController.Bind(seatRegistry);

            if (sessionController != null) sessionController.SessionEnded += OnSessionEnded;
        }

        /// <summary>
        /// Host only. Locks the session against late joins, then creates the one
        /// authoritative match. The lock is attempted first so a match never starts while
        /// somebody is still mid join.
        /// </summary>
        public async Task<bool> StartOnlineMatchAsync(int targetPlayerCount, int deckCount)
        {
            if (matchController == null || !matchController.IsSpawned) return false;
            if (!matchController.CanStartMatch(targetPlayerCount, deckCount)) return false;

            if (sessionController != null) await sessionController.SetJoinableAsync(false);

            return matchController.StartMatch(targetPlayerCount, deckCount);
        }

        /// <summary>Host only. Ends the match and reopens the session to joins.</summary>
        public async Task EndOnlineMatchAsync()
        {
            if (matchController != null) matchController.EndMatch();
            if (matchDriver != null) matchDriver.TearDown();

            if (sessionController != null) await sessionController.SetJoinableAsync(true);
        }

        void OnDestroy()
        {
            if (sessionController != null) sessionController.SessionEnded -= OnSessionEnded;
            if (presenter != null) presenter.Unbind();
        }

        // ------------------------------------------------------------------ operations

        /// <summary>
        /// Creates a session. The offline match is ended first: it owns the same table and
        /// the same single camera, so the two must never run at once.
        /// </summary>
        public async Task<bool> HostAsync(int targetPlayerCount, int deckCount)
        {
            StandDownOffline();

            if (sessionController == null) return false;

            bool created = await sessionController.HostAsync(targetPlayerCount);
            if (!created) return false;

            // The registry spawns with the host's NetworkManager, so it is only safe to
            // push the chosen configuration once it is actually spawned.
            await WaitForSeatRegistryAsync();

            if (seatRegistry != null && seatRegistry.IsSpawned)
            {
                seatRegistry.SetTargetPlayerCount(targetPlayerCount);
                seatRegistry.SetDeckCount(deckCount);
            }

            return true;
        }

        public async Task<bool> JoinAsync(string joinCode)
        {
            StandDownOffline();

            if (sessionController == null) return false;

            bool joined = await sessionController.JoinAsync(joinCode);
            if (!joined) return false;

            await WaitForSeatRegistryAsync();
            return true;
        }

        /// <summary>Host only. Broadcasts the shared configuration and builds the layout everywhere.</summary>
        public void StartNetworkTest()
        {
            if (seatRegistry == null || !seatRegistry.IsSpawned) return;

            seatRegistry.StartNetworkTest();
        }

        public void StopNetworkTest()
        {
            if (seatRegistry == null || !seatRegistry.IsSpawned) return;

            seatRegistry.StopNetworkTest();
        }

        public async Task LeaveAsync()
        {
            if (presenter != null) presenter.TearDown();

            if (sessionController != null) await sessionController.LeaveAsync();
        }

        // ------------------------------------------------------------------ internals

        void StandDownOffline()
        {
            if (offlineBootstrap != null && offlineBootstrap.MatchRunning) offlineBootstrap.EndMatch();
        }

        void OnSessionEnded()
        {
            // The host went away. Drop the match and the table, then go back to the menu.
            if (matchDriver != null) matchDriver.TearDown();
            if (presenter != null) presenter.TearDown();
        }

        /// <summary>
        /// Waits for the scene NetworkObject to finish spawning, with a bounded number of
        /// frames so a failure surfaces instead of hanging the UI forever.
        /// </summary>
        async Task WaitForSeatRegistryAsync()
        {
            const int maxFrames = 600;

            for (int frame = 0; frame < maxFrames; frame++)
            {
                if (seatRegistry != null && seatRegistry.IsSpawned) return;

                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsListening) return;

                await Task.Yield();
            }

            Debug.LogWarning("The seat registry did not spawn in time.", this);
        }
    }
}
