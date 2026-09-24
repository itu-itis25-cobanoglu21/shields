using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Shields.Bots;
using Shields.Cards;
using Shields.Game;
using Shields.Online;
using Shields.Session;

namespace Shields.Network
{
    /// <summary>
    /// The authoritative online match, owned entirely by the host.
    ///
    /// Only the host holds a <see cref="MatchSetup"/>, a <see cref="CardDeck"/> and a
    /// <see cref="ShieldsMatch"/>. Clients hold none of those: they send intent, and they
    /// render the public snapshots the host sends back. Bots are host side decision makers
    /// that reach the very same engine through the very same router, so a bot turn and a
    /// human turn are the same code path from the engine's point of view.
    ///
    /// Nothing here implements a Shields rule. Every outcome comes from
    /// <see cref="ShieldsMatch"/>; this class decides only who is allowed to ask.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkMatchController : NetworkBehaviour
    {
        [SerializeField] NetworkSeatRegistry seatRegistry;

        [Header("Pacing")]
        [Tooltip("Pause after any resolved action before the next bot starts thinking.")]
        [SerializeField] float postActionDelay = 2.5f;

        [Tooltip("How long a bot visibly thinks on its own turn before acting.")]
        [SerializeField] float botThinkDelay = 2.5f;

        // ---------------- replicated ----------------

        readonly NetworkVariable<byte> _phase = new NetworkVariable<byte>(
            (byte)OnlineMatchPhase.SessionLobby,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ---------------- host only ----------------

        MatchSetup _setup;
        ShieldsMatch _match;
        OnlineSetupRouter _setupRouter;
        OnlineTurnRouter _turnRouter;
        RandomBotPolicy _botPolicy;
        readonly BotTurnGate _botGate = new BotTurnGate();

        uint _revision;
        int _resolvedCount;
        float _feedbackUntil;

        // ---------------- every machine ----------------

        PublicSetupSnapshot _setupSnapshot;
        PublicMatchSnapshot _matchSnapshot;
        uint _appliedSetupRevision;
        uint _appliedMatchRevision;

        /// <summary>Raised on every machine when a newer public setup snapshot is applied.</summary>
        public event Action<PublicSetupSnapshot> SetupSnapshotChanged;

        /// <summary>Raised on every machine when a newer public match snapshot is applied.</summary>
        public event Action<PublicMatchSnapshot> MatchSnapshotChanged;

        /// <summary>Raised on every machine for each resolved action, for feedback.</summary>
        public event Action<PublicActionResult> ActionResolved;

        /// <summary>Raised on the machine whose request was refused. Local UX only.</summary>
        public event Action<string> RequestRefused;

        public event Action<OnlineMatchPhase> PhaseChanged;

        public OnlineMatchPhase Phase { get { return (OnlineMatchPhase)_phase.Value; } }

        public PublicSetupSnapshot SetupSnapshot { get { return _setupSnapshot; } }

        public PublicMatchSnapshot MatchSnapshot { get { return _matchSnapshot; } }

        public int LocalSeatIndex
        {
            get { return seatRegistry != null ? seatRegistry.LocalSeatIndex : NetworkSeatRegistry.NoSeat; }
        }

        public bool MatchActive
        {
            get { return Phase == OnlineMatchPhase.InitialDeal
                || Phase == OnlineMatchPhase.WaitingForShieldSelections
                || Phase == OnlineMatchPhase.Playing; }
        }

        /// <summary>Host only, and for diagnostics: the authoritative engine.</summary>
        public ShieldsMatch HostMatch { get { return _match; } }

        public void Bind(NetworkSeatRegistry registry)
        {
            seatRegistry = registry;
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            _phase.OnValueChanged += OnPhaseChanged;

            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            _phase.OnValueChanged -= OnPhaseChanged;

            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            ClearHostState();
        }

        void OnPhaseChanged(byte previous, byte current)
        {
            Action<OnlineMatchPhase> changed = PhaseChanged;
            if (changed != null) changed((OnlineMatchPhase)current);
        }

        // ------------------------------------------------------------------ host: start

        /// <summary>
        /// Why the host cannot start right now, or empty when it can. Checked before the
        /// deck exists so a refused start creates nothing.
        /// </summary>
        public string DescribeStartBlocker(int targetPlayerCount, int deckCount)
        {
            if (!IsServer) return "Only the host can start the match.";
            if (MatchActive || Phase == OnlineMatchPhase.MatchOver) return "A match is already running.";
            if (NetworkManager == null || !NetworkManager.IsListening) return "The network connection is not active.";

            if (targetPlayerCount < MatchConfig.MinPlayers || targetPlayerCount > MatchConfig.MaxPlayers)
                return "Player count must be between " + MatchConfig.MinPlayers + " and " + MatchConfig.MaxPlayers + ".";

            if (deckCount < CardDeck.MinDeckCount || deckCount > CardDeck.MaxDeckCount)
                return "Deck count must be 1 or 2.";

            if (seatRegistry == null || !seatRegistry.IsSpawned) return "Seats are not ready yet.";

            int humans = 0;
            for (int seat = 0; seat < seatRegistry.SeatCount; seat++)
            {
                if (!seatRegistry.OccupantAt(seat).IsHuman) continue;

                humans++;

                // A human outside the gameplay table would have no seat to play.
                if (seat >= targetPlayerCount)
                    return "A connected player holds seat " + seat + ", beyond the " + targetPlayerCount + " seat table.";
            }

            if (humans == 0) return "No connected players hold a seat.";
            if (humans > targetPlayerCount) return "More players are connected than the table seats.";

            return string.Empty;
        }

        public bool CanStartMatch(int targetPlayerCount, int deckCount)
        {
            return DescribeStartBlocker(targetPlayerCount, deckCount).Length == 0;
        }

        /// <summary>
        /// Host only. Finalises occupancy, deals once, and publishes the public setup.
        /// Every seat the connected humans do not hold becomes a bot.
        /// </summary>
        public bool StartMatch(int targetPlayerCount, int deckCount, int? seed = null)
        {
            string blocker = DescribeStartBlocker(targetPlayerCount, deckCount);
            if (blocker.Length > 0)
            {
                Debug.LogWarning("Cannot start the online match: " + blocker, this);
                return false;
            }

            // Occupancy is frozen here: humans keep their seats, the rest become bots.
            bool[] seatIsBot = PublicStateBuilder.BuildSeatOccupancy(seatRegistry.AuthoritativeSeats, targetPlayerCount);

            int hostSeat = Mathf.Clamp(LocalSeatIndex, 0, targetPlayerCount - 1);
            MatchConfig config = new MatchConfig(targetPlayerCount, deckCount, hostSeat, seed);

            // The only deck in the whole session is created here, on the host.
            _setup = new MatchSetup(config, seatIsBot);
            _setupRouter = new OnlineSetupRouter(seatRegistry.AuthoritativeSeats, _setup);
            _botPolicy = new RandomBotPolicy(seed.HasValue ? new System.Random(seed.Value ^ 0x2B07) : new System.Random());

            _match = null;
            _turnRouter = null;
            _revision = 0;
            _resolvedCount = 0;
            _botGate.Cancel();

            // The deal is published in its own phase so clients can show the three cards
            // before anybody is asked to choose.
            SetPhase(OnlineMatchPhase.InitialDeal);
            PublishSetup();

            // Bots choose blindly and immediately, on the host only.
            _setupRouter.RunBotSelections();

            SetPhase(OnlineMatchPhase.WaitingForShieldSelections);
            PublishSetup();
            TryBeginTurnPlay();

            return true;
        }

        /// <summary>Host only. Ends the match and returns everyone to the lobby state.</summary>
        public void EndMatch(OnlineMatchPhase finalPhase = OnlineMatchPhase.SessionLobby)
        {
            if (!IsServer) return;

            ClearHostState();
            SetPhase(finalPhase);
            PublishMatch();
        }

        void ClearHostState()
        {
            _setup = null;
            _match = null;
            _setupRouter = null;
            _turnRouter = null;
            _botPolicy = null;
            _botGate.Cancel();
        }

        void SetPhase(OnlineMatchPhase phase)
        {
            if (!IsServer) return;

            _phase.Value = (byte)phase;
        }

        // ------------------------------------------------------------------ host: setup

        /// <summary>Client to host: "make this card my Shield". The seat comes from the sender.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        public void SubmitShieldChoiceRpc(int cardId, RpcParams rpcParams = default)
        {
            if (!IsServer || _setupRouter == null) return;
            if (Phase != OnlineMatchPhase.WaitingForShieldSelections) return;

            int seat;
            SetupRejection rejection = _setupRouter.SelectShieldFromParticipant(
                rpcParams.Receive.SenderClientId, cardId, out seat);

            if (rejection != SetupRejection.None)
            {
                RefuseTo(rpcParams.Receive.SenderClientId, OnlineSetupRouter.DescribeRejection(rejection));
                return;
            }

            PublishSetup();
            TryBeginTurnPlay();
        }

        /// <summary>
        /// Moves into turn play once every seat has chosen. The same
        /// <see cref="MatchSetup"/> becomes the one <see cref="ShieldsMatch"/>: the deck
        /// and the dealt cards carry over untouched, exactly as offline.
        /// </summary>
        void TryBeginTurnPlay()
        {
            if (!IsServer || _setup == null || _match != null) return;
            if (!_setup.InitialSetupComplete) return;

            _match = new ShieldsMatch(_setup);
            _turnRouter = new OnlineTurnRouter(seatRegistry.AuthoritativeSeats, _match);
            _resolvedCount = 0;
            _feedbackUntil = 0f;

            SetPhase(OnlineMatchPhase.Playing);
            PublishMatch();

            Debug.Log("Online turn play begins: player " + _match.StartingPlayerIndex +
                " starts, " + _match.Direction + ".", this);
        }

        // ------------------------------------------------------------------ host: turns

        /// <summary>
        /// Client to host: an action and, when needed, a target. The actor is never part
        /// of the payload; the host derives it from the authenticated sender.
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        public void SubmitTurnActionRpc(byte actionTypeCode, sbyte targetIndex, RpcParams rpcParams = default)
        {
            if (!IsServer || _turnRouter == null) return;
            if (Phase != OnlineMatchPhase.Playing) return;

            RoutedAction routed = _turnRouter.SubmitFromParticipant(
                rpcParams.Receive.SenderClientId, (TurnActionType)actionTypeCode, targetIndex);

            HandleRouted(routed, rpcParams.Receive.SenderClientId);
        }

        void HandleRouted(RoutedAction routed, ulong senderClientId)
        {
            if (!routed.Accepted)
            {
                // A stale or illegal request changes nothing and never disconnects anyone.
                RefuseTo(senderClientId, OnlineTurnRouter.DescribeRejection(routed.Rejection));
                return;
            }

            _resolvedCount++;
            _feedbackUntil = Time.time + postActionDelay;

            PublicActionResult publicResult = PublicStateBuilder.BuildActionResult(routed.Result);

            PublishMatch();
            PublishActionResultRpc(publicResult);

            if (routed.Result.MatchOver)
            {
                SetPhase(OnlineMatchPhase.MatchOver);
                _botGate.Cancel();
                Debug.Log("Online match over. Winner: player " + routed.Result.WinnerIndex, this);
            }
        }

        // ------------------------------------------------------------------ host: bots

        void Update()
        {
            if (!IsServer || _turnRouter == null || _match == null) return;
            if (Phase != OnlineMatchPhase.Playing || _match.IsMatchOver) return;

            // Passing NoSeat means no seat counts as a local human here: the gate then
            // reports Bot for exactly the bot seats, which is what the host drives. Human
            // seats simply wait for their own client's request.
            double dueAt = Mathf.Max(Time.time, _feedbackUntil) + botThinkDelay;

            int botIndex;
            if (!_botGate.TryTakeDueBot(_match, NetworkSeatRegistry.NoSeat, _resolvedCount, Time.time, dueAt, out botIndex))
                return;

            // Bot decisions never travel: only the host runs the policy, and its chosen
            // request goes straight into the same engine a human request would reach.
            RoutedAction routed = _turnRouter.SubmitBot(_botPolicy.ChooseAction(_match.Players, botIndex));

            if (routed.Accepted) HandleRouted(routed, NetworkManager.LocalClientId);
            else Debug.LogWarning("Bot action refused: " + routed.Rejection, this);
        }

        // ------------------------------------------------------------------ publishing

        void PublishSetup()
        {
            if (!IsServer) return;

            _revision++;
            PublishSetupSnapshotRpc(PublicStateBuilder.BuildSetupSnapshot(_setup, _revision));
        }

        void PublishMatch()
        {
            if (!IsServer) return;

            _revision++;
            PublishMatchSnapshotRpc(PublicStateBuilder.BuildMatchSnapshot(_match, _revision, Phase));
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void PublishSetupSnapshotRpc(PublicSetupSnapshot snapshot)
        {
            // Never let an older snapshot overwrite a newer one.
            if (snapshot.Revision <= _appliedSetupRevision) return;

            _appliedSetupRevision = snapshot.Revision;
            _setupSnapshot = snapshot;

            Action<PublicSetupSnapshot> changed = SetupSnapshotChanged;
            if (changed != null) changed(snapshot);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void PublishMatchSnapshotRpc(PublicMatchSnapshot snapshot)
        {
            if (snapshot.Revision <= _appliedMatchRevision) return;

            _appliedMatchRevision = snapshot.Revision;
            _matchSnapshot = snapshot;

            Action<PublicMatchSnapshot> changed = MatchSnapshotChanged;
            if (changed != null) changed(snapshot);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void PublishActionResultRpc(PublicActionResult result)
        {
            Action<PublicActionResult> resolved = ActionResolved;
            if (resolved != null) resolved(result);
        }

        void RefuseTo(ulong clientId, string reason)
        {
            RefusalRpc(reason, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        void RefusalRpc(string reason, RpcParams rpcParams)
        {
            Action<string> refused = RequestRefused;
            if (refused != null) refused(reason);
        }

        // ------------------------------------------------------------------ client intent

        /// <summary>Local human intent. Sends only the action and target; never a card or result.</summary>
        public void RequestAction(TurnActionType actionType, int targetIndex)
        {
            if (!IsSpawned) return;

            SubmitTurnActionRpc((byte)actionType, (sbyte)targetIndex);
        }

        /// <summary>Local human initial Shield choice, named by the clicked card's identity.</summary>
        public void RequestShieldChoice(int cardId)
        {
            if (!IsSpawned) return;

            SubmitShieldChoiceRpc(cardId);
        }

        /// <summary>
        /// Whether local input should offer an action. UX only: the host validates
        /// independently and is the sole authority.
        /// </summary>
        public bool LocalInputAllowed
        {
            get
            {
                if (Phase != OnlineMatchPhase.Playing) return false;

                int seat = LocalSeatIndex;
                if (seat == NetworkSeatRegistry.NoSeat) return false;

                return _matchSnapshot.IsValid && _matchSnapshot.IsTurnOf(seat);
            }
        }

        /// <summary>True while the local seat still owes an initial Shield choice.</summary>
        public bool LocalShieldChoicePending
        {
            get
            {
                if (Phase != OnlineMatchPhase.WaitingForShieldSelections) return false;

                int seat = LocalSeatIndex;
                if (seat == NetworkSeatRegistry.NoSeat || !_setupSnapshot.IsValid) return false;

                PublicSetupPlayer player = _setupSnapshot.GetPlayer(seat);
                return !player.IsBot && !player.SelectionComplete;
            }
        }

        // ------------------------------------------------------------------ disconnects

        /// <summary>
        /// A participant vanished. TASK 009 deliberately invents no rule for continuing
        /// without them, so an active match stops and everyone is told plainly. Bot
        /// takeover stays a later product decision.
        /// </summary>
        void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            if (!MatchActive) return;

            // Whether they were mid setup or mid turn, the match cannot continue without
            // them and TASK 009 forbids inventing a substitute rule, so it stops.
            AbortMatch();
        }

        void AbortMatch()
        {
            if (!IsServer) return;

            Debug.LogWarning("A participant disconnected during an active match; stopping it.", this);

            _botGate.Cancel();
            ClearHostState();
            SetPhase(OnlineMatchPhase.Aborted);
            PublishMatch();
        }

        // ------------------------------------------------------------------ diagnostics

        /// <summary>Development diagnostics. Never contains a hidden Charge identity.</summary>
        public string DescribeDiagnostics()
        {
            List<string> lines = new List<string>
            {
                "phase " + Phase,
                "local seat " + LocalSeatIndex,
                "applied match rev " + _appliedMatchRevision + ", setup rev " + _appliedSetupRevision
            };

            if (IsServer) lines.Add("authoritative rev " + _revision);
            if (_matchSnapshot.IsValid) lines.Add("turn player " + _matchSnapshot.CurrentPlayerIndex);

            return string.Join(" | ", lines);
        }
    }
}
