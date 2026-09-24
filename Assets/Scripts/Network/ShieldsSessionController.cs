using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Shields.Network
{
    /// <summary>Where the local process is in the connection flow.</summary>
    public enum NetworkPhase
    {
        /// <summary>No services touched. The offline prototype is free to run.</summary>
        Offline = 0,
        Initializing,
        /// <summary>Services are up and the player is signed in, but no session exists.</summary>
        Ready,
        Creating,
        Joining,
        InSession,
        Failed
    }

    /// <summary>
    /// Owns the Unity Gaming Services side of the prototype: initialise, sign in
    /// anonymously, and create or join one Relay backed Session.
    ///
    /// This is the current Multiplayer Services flow, not the old manual Lobby + Relay
    /// dance. <see cref="SessionOptions"/> with <c>WithRelayNetwork()</c> makes the SDK
    /// allocate the Relay, mint the join code, and start <see cref="NetworkManager"/> with
    /// the transport already configured. Nothing here allocates Relay by hand.
    ///
    /// It deliberately knows nothing about seats, tables or Shields rules. It answers one
    /// question: are we connected, and under what join code.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShieldsSessionController : MonoBehaviour
    {
        [Tooltip("Session name sent to the service. Cosmetic; the join code is what matters.")]
        [SerializeField] string sessionName = "Shields Prototype";

        ISession _session;

        /// <summary>Raised whenever anything the prototype UI displays has changed.</summary>
        public event Action StateChanged;

        /// <summary>Raised when the session ended from the outside (host left, kicked).</summary>
        public event Action SessionEnded;

        public NetworkPhase Phase { get; private set; } = NetworkPhase.Offline;

        /// <summary>Human readable reason for the last failure, or empty. Never a token.</summary>
        public string ErrorMessage { get; private set; } = string.Empty;

        public ISession Session { get { return _session; } }

        public bool InSession { get { return _session != null; } }

        public bool IsHost { get { return _session != null && _session.IsHost; } }

        /// <summary>The code a second participant types to join. Empty outside a session.</summary>
        public string JoinCode { get { return _session != null ? _session.Code : string.Empty; } }

        /// <summary>How many real people are in the session right now.</summary>
        public int ConnectedHumanCount { get { return _session != null ? _session.PlayerCount : 0; } }

        public int MaxPlayers { get { return _session != null ? _session.MaxPlayers : 0; } }

        /// <summary>True while an async service call is in flight, so the UI can disable buttons.</summary>
        public bool IsBusy
        {
            get
            {
                return Phase == NetworkPhase.Initializing
                    || Phase == NetworkPhase.Creating
                    || Phase == NetworkPhase.Joining;
            }
        }

        /// <summary>The signed in player id, for diagnostics only.</summary>
        public string LocalPlayerId
        {
            get
            {
                return AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                    ? AuthenticationService.Instance.PlayerId
                    : string.Empty;
            }
        }

        // ------------------------------------------------------------------ services

        /// <summary>
        /// Initialises Unity Services and signs in anonymously, doing nothing if that has
        /// already happened. Anonymous sign in needs no identity provider configuration,
        /// which is why the prototype uses it.
        /// </summary>
        public async Task<bool> EnsureSignedInAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    SetPhase(NetworkPhase.Initializing);
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    SetPhase(NetworkPhase.Initializing);
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                if (Phase != NetworkPhase.InSession) SetPhase(NetworkPhase.Ready);

                ErrorMessage = string.Empty;
                return true;
            }
            catch (ServicesInitializationException e)
            {
                // Almost always an unlinked cloud project or no network.
                Fail("Unity Services could not initialize. Check the project is linked to Unity Cloud.", e);
                return false;
            }
            catch (AuthenticationException e)
            {
                Fail("Anonymous sign in failed. Check Authentication is enabled for this project.", e);
                return false;
            }
            catch (Exception e)
            {
                Fail("Unity Services failed to start.", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ hosting

        /// <summary>
        /// Creates a private Relay backed session sized for the target player count and
        /// becomes its host. The SDK starts NGO as host as part of this call.
        /// </summary>
        public async Task<bool> HostAsync(int maxPlayers)
        {
            if (InSession) return true;
            if (!await EnsureSignedInAsync()) return false;
            if (!RequireNetworkManager()) return false;

            try
            {
                SetPhase(NetworkPhase.Creating);

                SessionOptions options = new SessionOptions
                {
                    Name = sessionName,
                    MaxPlayers = Mathf.Clamp(maxPlayers, 2, 8),
                    IsPrivate = true
                }.WithRelayNetwork();

                IHostSession hosted = await MultiplayerService.Instance.CreateSessionAsync(options);

                AdoptSession(hosted);
                SetPhase(NetworkPhase.InSession);
                return true;
            }
            catch (SessionException e)
            {
                Fail(DescribeSessionError(e), e);
                return false;
            }
            catch (Exception e)
            {
                Fail("Could not create the session.", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ joining

        /// <summary>Joins an existing session by its join code and starts NGO as a client.</summary>
        public async Task<bool> JoinAsync(string joinCode)
        {
            if (InSession) return true;

            string code = (joinCode ?? string.Empty).Trim();
            if (code.Length == 0)
            {
                Fail("Enter a join code first.", null);
                return false;
            }

            if (!await EnsureSignedInAsync()) return false;
            if (!RequireNetworkManager()) return false;

            try
            {
                SetPhase(NetworkPhase.Joining);

                ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

                AdoptSession(joined);
                SetPhase(NetworkPhase.InSession);
                return true;
            }
            catch (SessionException e)
            {
                Fail(DescribeSessionError(e), e);
                return false;
            }
            catch (Exception e)
            {
                Fail("Could not join that session.", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ leaving

        /// <summary>
        /// Leaves the session and shuts NGO down. Safe to call when not in a session, and
        /// never throws at the caller: leaving must always get you back to the menu.
        /// </summary>
        public async Task LeaveAsync()
        {
            ISession leaving = _session;
            ForgetSession();

            if (leaving != null)
            {
                try
                {
                    await leaving.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Leaving the session reported: " + e.Message, this);
                }
            }

            ShutdownNetworkManager();
            SetPhase(NetworkPhase.Ready);
        }

        void OnDestroy()
        {
            DetachSessionEvents(_session);
        }

        void OnApplicationQuit()
        {
            // Without this the session lingers server side until it times out.
            if (_session != null) _ = _session.LeaveAsync();
        }

        // ------------------------------------------------------------------ internals

        void AdoptSession(ISession session)
        {
            _session = session;
            AttachSessionEvents(session);
            ErrorMessage = string.Empty;
        }

        void AttachSessionEvents(ISession session)
        {
            if (session == null) return;

            session.PlayerJoined += OnPlayerJoined;
            session.PlayerLeaving += OnPlayerLeaving;
            session.Deleted += OnSessionGone;
            session.RemovedFromSession += OnSessionGone;
            session.Changed += RaiseStateChanged;
        }

        void DetachSessionEvents(ISession session)
        {
            if (session == null) return;

            session.PlayerJoined -= OnPlayerJoined;
            session.PlayerLeaving -= OnPlayerLeaving;
            session.Deleted -= OnSessionGone;
            session.RemovedFromSession -= OnSessionGone;
            session.Changed -= RaiseStateChanged;
        }

        void OnPlayerJoined(string playerId)
        {
            RaiseStateChanged();
        }

        void OnPlayerLeaving(string playerId)
        {
            RaiseStateChanged();
        }

        /// <summary>The host ended the session, or we were removed from it.</summary>
        void OnSessionGone()
        {
            ForgetSession();
            ShutdownNetworkManager();

            ErrorMessage = "The host ended the session.";
            SetPhase(NetworkPhase.Ready);

            Action ended = SessionEnded;
            if (ended != null) ended();
        }

        void ForgetSession()
        {
            DetachSessionEvents(_session);
            _session = null;
        }

        static void ShutdownNetworkManager()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening) manager.Shutdown();
        }

        bool RequireNetworkManager()
        {
            if (NetworkManager.Singleton != null) return true;

            Fail("No NetworkManager in the scene. Networking cannot start without one.", null);
            return false;
        }

        void SetPhase(NetworkPhase phase)
        {
            if (Phase == phase) return;

            Phase = phase;
            RaiseStateChanged();
        }

        void Fail(string message, Exception e)
        {
            ErrorMessage = message;
            Phase = NetworkPhase.Failed;

            // The exception message is logged, never surfaced raw in the UI, and no token
            // or credential is ever part of these types.
            if (e != null) Debug.LogError(message + "\n" + e.GetType().Name + ": " + e.Message, this);
            else Debug.LogError(message, this);

            RaiseStateChanged();
        }

        void RaiseStateChanged()
        {
            Action changed = StateChanged;
            if (changed != null) changed();
        }

        /// <summary>Turns a service error code into something a tester can act on.</summary>
        static string DescribeSessionError(SessionException e)
        {
            switch (e.Error)
            {
                case SessionError.SessionNotFound:
                    return "No session exists for that join code.";
                case SessionError.InvalidSessionIdentifier:
                case SessionError.InvalidParameter:
                    return "That join code is not valid.";
                case SessionError.SessionDeleted:
                    return "That session has already been closed.";
                case SessionError.NotAuthorized:
                case SessionError.Forbidden:
                    return "Not authorized. Check this project's multiplayer services are enabled.";
                case SessionError.RateLimitExceeded:
                    return "Too many attempts in a row. Wait a moment and try again.";
                case SessionError.NetworkManagerNotInitialized:
                case SessionError.NetworkManagerStartFailed:
                case SessionError.NetworkSetupFailed:
                    return "Netcode failed to start for this session.";
                case SessionError.TransportComponentMissing:
                case SessionError.TransportInvalid:
                    return "The NetworkManager has no valid UnityTransport component.";
                default:
                    return "Session error: " + e.Error;
            }
        }
    }
}
