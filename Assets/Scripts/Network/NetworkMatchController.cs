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
    /// Every match in a session is a new generation (1, 2, 3 ...). Each one gets fresh
    /// random seating, a fresh deck and fresh state; its public messages carry the
    /// generation, and <see cref="MatchGenerationGate"/> orders them, so a rematch is a
    /// genuinely new match inside the same session and connection.
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

        // Rematch status after a match is over: who has voted yes, and how many
        // connected humans must. Bots never vote.
        readonly NetworkList<ulong> _rematchVoters = new NetworkList<ulong>(
            null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<byte> _rematchRequired = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        // Session long on the host: one generation per match, and the seating used last,
        // so the next match never repeats the exact same table.
        int _generation;
        MatchParticipant[] _lastSeating;
        int _lastPlayerCount;
        int _lastDeckCount = 1;
        readonly System.Random _seatRandom = new System.Random();
        readonly RematchVote _rematch = new RematchVote();
        bool _rematchStartPending;

        // ---------------- every machine ----------------

        readonly MatchGenerationGate _gate = new MatchGenerationGate();

        // Messages of a generation whose seating has not replicated here yet. Kept in
        // arrival order and applied once this machine knows its new seat, so nothing is
        // ever drawn from a stale seat mapping.
        readonly List<KeyValuePair<int, Action>> _deferred = new List<KeyValuePair<int, Action>>();

        PublicSetupSnapshot _setupSnapshot;
        PublicMatchSnapshot _matchSnapshot;

        /// <summary>
        /// Raised on every machine when the first message of a new match generation is
        /// applied, before that message's own event. Presentation tears the previous
        /// match down here and rebuilds for the new seating.
        /// </summary>
        public event Action<int> MatchGenerationStarted;

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

        /// <summary>The match generation this machine is presenting; 0 before the first match.</summary>
        public int Generation { get { return _gate.Generation; } }

        /// <summary>Host only: the generation of the match the host is running.</summary>
        public int HostGeneration { get { return _generation; } }

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

        // ---------------- rematch status, every machine ----------------

        public int RematchYesCount { get { return _rematchVoters.Count; } }

        public int RematchRequired { get { return _rematchRequired.Value; } }

        public bool LocalVotedRematch
        {
            get
            {
                if (NetworkManager == null) return false;

                ulong local = NetworkManager.LocalClientId;
                for (int i = 0; i < _rematchVoters.Count; i++)
                    if (_rematchVoters[i] == local) return true;

                return false;
            }
        }

        /// <summary>This machine's participant holds a seat and may vote for a rematch.</summary>
        public bool LocalCanVoteRematch
        {
            get { return Phase == OnlineMatchPhase.MatchOver && LocalSeatIndex != NetworkSeatRegistry.NoSeat; }
        }

        public void Bind(NetworkSeatRegistry registry)
        {
            seatRegistry = registry;
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            _phase.OnValueChanged += OnPhaseChanged;

            // A fresh connection is a fresh session: generations start again.
            ResetGenerationState();

            if (IsServer)
            {
                _generation = 0;
                _lastSeating = null;
                _rematch.Reset();
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            _phase.OnValueChanged -= OnPhaseChanged;

            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            // Host or session shutdown cancels any pending rematch cleanly.
            ClearHostState();
            _rematch.Reset();
            _rematchStartPending = false;
            ResetGenerationState();
        }

        void ResetGenerationState()
        {
            _gate.Reset();
            _deferred.Clear();
            _setupSnapshot = default;
            _matchSnapshot = default;
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

            // NGO lists a joining client before its connect callback seats it. Starting in
            // that gap would seat bots everywhere and leave the joiner without a seat.
            if (seatRegistry != null && seatRegistry.IsSpawned && seatRegistry.HasUnseatedParticipantAndFreeSeat())
                return "A player is still joining.";

            return DescribeConfigBlocker(targetPlayerCount, deckCount);
        }

        /// <summary>Everything about a start that does not depend on the current phase.</summary>
        string DescribeConfigBlocker(int targetPlayerCount, int deckCount)
        {
            if (NetworkManager == null || !NetworkManager.IsListening) return "The network connection is not active.";

            if (targetPlayerCount < MatchConfig.MinPlayers || targetPlayerCount > MatchConfig.MaxPlayers)
                return "Player count must be between " + MatchConfig.MinPlayers + " and " + MatchConfig.MaxPlayers + ".";

            if (deckCount < CardDeck.MinDeckCount || deckCount > CardDeck.MaxDeckCount)
                return "Deck count must be 1 or 2.";

            if (seatRegistry == null || !seatRegistry.IsSpawned) return "Seats are not ready yet.";

            int humans = ConnectedHumans(SeatOccupant.NoParticipant).Count;
            if (humans == 0) return "No connected players hold a seat.";
            if (humans > targetPlayerCount) return "More players are connected than the table seats.";

            return string.Empty;
        }

        public bool CanStartMatch(int targetPlayerCount, int deckCount)
        {
            return DescribeStartBlocker(targetPlayerCount, deckCount).Length == 0;
        }

        /// <summary>
        /// Host only. Starts the first match of a lobby. Every seat the connected humans do
        /// not take becomes a bot, and everyone is seated at random.
        /// </summary>
        public bool StartMatch(int targetPlayerCount, int deckCount, int? seed = null)
        {
            string blocker = DescribeStartBlocker(targetPlayerCount, deckCount);
            if (blocker.Length > 0)
            {
                Debug.LogWarning("Cannot start the online match: " + blocker, this);
                return false;
            }

            return BeginMatch(targetPlayerCount, deckCount, seed);
        }

        /// <summary>
        /// The one way a match begins, first match and rematch alike: new generation,
        /// fresh random seating published before anything is dealt, then a fresh deck,
        /// deal and Shield selection.
        /// </summary>
        bool BeginMatch(int targetPlayerCount, int deckCount, int? seed)
        {
            string blocker = DescribeConfigBlocker(targetPlayerCount, deckCount);
            if (blocker.Length > 0)
            {
                Debug.LogWarning("Cannot start the online match: " + blocker, this);
                return false;
            }

            ClearHostState();

            if (seatRegistry.SeatCount != targetPlayerCount) seatRegistry.SetTargetPlayerCount(targetPlayerCount);

            // Seating first: every participant, humans and bots, shuffled over every seat.
            int generation = _generation + 1;
            MatchParticipant[] roster = SeatShuffler.BuildRoster(ConnectedHumans(SeatOccupant.NoParticipant), targetPlayerCount);
            MatchParticipant[] previous = _lastSeating != null && _lastSeating.Length == targetPlayerCount ? _lastSeating : null;
            MatchParticipant[] seating = SeatShuffler.Shuffle(roster, previous, _seatRandom);

            if (!seatRegistry.ApplyMatchSeating(seating, generation))
            {
                Debug.LogWarning("Could not apply the new seating; the match did not start.\n" + seatRegistry.Describe(), this);
                return false;
            }

            _generation = generation;
            _lastSeating = seating;
            _lastPlayerCount = targetPlayerCount;
            _lastDeckCount = deckCount;
            _rematch.Reset();
            _rematchStartPending = false;

            // Occupancy comes from the new seating: humans where they now sit, bots elsewhere.
            bool[] seatIsBot = PublicStateBuilder.BuildSeatOccupancy(seatRegistry.AuthoritativeSeats, targetPlayerCount);

            int hostSeat = Mathf.Clamp(LocalSeatIndex, 0, targetPlayerCount - 1);
            MatchConfig config = new MatchConfig(targetPlayerCount, deckCount, hostSeat, seed);

            // The only deck in the whole session is created here, on the host, every match.
            _setup = new MatchSetup(config, seatIsBot);
            _setupRouter = new OnlineSetupRouter(seatRegistry.AuthoritativeSeats, _setup);
            _botPolicy = new RandomBotPolicy(seed.HasValue ? new System.Random(seed.Value ^ 0x2B07) : new System.Random());

            _match = null;
            _turnRouter = null;
            _revision = 0;
            _resolvedCount = 0;
            _botGate.Cancel();

            Debug.Log(string.Format("Online match generation {0} begins.\n{1}", _generation, seatRegistry.Describe()), this);

            // The deal is published in its own phase so clients can show the three cards
            // before anybody is asked to choose.
            SetPhase(OnlineMatchPhase.InitialDeal);
            PublishRematchState();
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
            _rematch.Reset();
            _rematchStartPending = false;
            SetPhase(finalPhase);
            PublishMatch();
            PublishRematchState();

            if (finalPhase == OnlineMatchPhase.SessionLobby && seatRegistry != null) seatRegistry.ReturnToLobbySeating();
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

            Debug.Log("Online turn play begins (generation " + _generation + "): player " + _match.StartingPlayerIndex +
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
            PublishActionResultRpc(publicResult, _generation);

            if (routed.Result.MatchOver)
            {
                SetPhase(OnlineMatchPhase.MatchOver);
                _botGate.Cancel();
                _rematch.Reset();
                PublishRematchState();
                Debug.Log("Online match over (generation " + _generation + "). Winner: player " + routed.Result.WinnerIndex, this);
            }
        }

        // ------------------------------------------------------------------ host: bots and rematch

        void Update()
        {
            if (!IsSpawned) return;

            FlushDeferred();

            if (!IsServer) return;

            if (_rematchStartPending)
            {
                _rematchStartPending = false;
                BeginRematch();
            }

            if (_turnRouter == null || _match == null) return;
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

        /// <summary>Client to host: "I want a rematch" (or no longer). The voter comes from the sender.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        void SubmitRematchVoteRpc(bool yes, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong sender = rpcParams.Receive.SenderClientId;

            if (Phase != OnlineMatchPhase.MatchOver || _rematch.Locked)
            {
                RefuseTo(sender, "A rematch can only be voted for after a match is over.");
                return;
            }

            List<ulong> humans = ConnectedHumans(SeatOccupant.NoParticipant);
            if (!_rematch.SetVote(sender, yes, humans))
            {
                RefuseTo(sender, "Only seated players vote for a rematch.");
                return;
            }

            PublishRematchState();
            TryLockRematch(humans);
        }

        /// <summary>
        /// The start-once guard. Locking happens here, the start itself on the host's
        /// next update: by then any disconnect that triggered this has left the seat map,
        /// so the new seating never includes somebody who is gone.
        /// </summary>
        void TryLockRematch(IReadOnlyCollection<ulong> humans)
        {
            if (!_rematch.TryLockForRestart(humans)) return;

            _rematchStartPending = true;
            Debug.Log("Rematch: every connected human agreed (" + humans.Count + "). Starting a new match.", this);
        }

        void BeginRematch()
        {
            if (Phase != OnlineMatchPhase.MatchOver) return;

            if (!BeginMatch(_lastPlayerCount, _lastDeckCount, null))
            {
                // Nothing started: unlock so the table can try again rather than hang.
                _rematch.Reset();
                PublishRematchState();
            }
        }

        /// <summary>Humans currently holding a seat, minus one that is leaving right now.</summary>
        List<ulong> ConnectedHumans(ulong leaving)
        {
            List<ulong> humans = new List<ulong>();
            SeatRegistry seats = seatRegistry != null ? seatRegistry.AuthoritativeSeats : null;
            if (seats == null || NetworkManager == null) return humans;

            IReadOnlyList<ulong> participants = seats.Participants();
            for (int i = 0; i < participants.Count; i++)
            {
                ulong participant = participants[i];
                if (participant == leaving) continue;
                if (!IsConnected(participant)) continue;

                humans.Add(participant);
            }

            return humans;
        }

        bool IsConnected(ulong participant)
        {
            IReadOnlyList<ulong> connected = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < connected.Count; i++)
                if (connected[i] == participant) return true;

            return false;
        }

        void PublishRematchState(IReadOnlyCollection<ulong> humans = null)
        {
            if (!IsServer) return;

            IReadOnlyList<ulong> yes = _rematch.YesVoters;

            if (_rematchVoters.Count != yes.Count || !SameVoters(yes))
            {
                _rematchVoters.Clear();
                for (int i = 0; i < yes.Count; i++) _rematchVoters.Add(yes[i]);
            }

            int required = Phase == OnlineMatchPhase.MatchOver
                ? (humans != null ? humans.Count : ConnectedHumans(SeatOccupant.NoParticipant).Count)
                : 0;

            byte requiredByte = (byte)Mathf.Clamp(required, 0, byte.MaxValue);
            if (_rematchRequired.Value != requiredByte) _rematchRequired.Value = requiredByte;
        }

        bool SameVoters(IReadOnlyList<ulong> yes)
        {
            for (int i = 0; i < yes.Count; i++)
                if (_rematchVoters[i] != yes[i]) return false;

            return true;
        }

        // ------------------------------------------------------------------ publishing

        void PublishSetup()
        {
            if (!IsServer) return;

            _revision++;
            PublicSetupSnapshot snapshot = PublicStateBuilder.BuildSetupSnapshot(_setup, _revision);
            snapshot.Generation = _generation;
            PublishSetupSnapshotRpc(snapshot);
        }

        void PublishMatch()
        {
            if (!IsServer) return;

            _revision++;
            PublicMatchSnapshot snapshot = PublicStateBuilder.BuildMatchSnapshot(_match, _revision, Phase);
            snapshot.Generation = _generation;
            PublishMatchSnapshotRpc(snapshot);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void PublishSetupSnapshotRpc(PublicSetupSnapshot snapshot)
        {
            Deliver(snapshot.Generation, () => ApplySetupSnapshot(snapshot));
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void PublishMatchSnapshotRpc(PublicMatchSnapshot snapshot)
        {
            Deliver(snapshot.Generation, () => ApplyMatchSnapshot(snapshot));
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        void PublishActionResultRpc(PublicActionResult result, int generation)
        {
            Deliver(generation, () =>
            {
                GateDecision decision = _gate.AcceptEvent(generation);
                if (decision == GateDecision.Stale) return;
                if (decision == GateDecision.AcceptedNewGeneration) StartGeneration(generation);

                Action<PublicActionResult> resolved = ActionResolved;
                if (resolved != null) resolved(result);
            });
        }

        void ApplySetupSnapshot(PublicSetupSnapshot snapshot)
        {
            // Never let an older snapshot, or one from an earlier match, overwrite a newer one.
            GateDecision decision = _gate.AcceptSnapshot(snapshot.Generation, snapshot.Revision, SnapshotChannel.Setup);
            if (decision == GateDecision.Stale) return;
            if (decision == GateDecision.AcceptedNewGeneration) StartGeneration(snapshot.Generation);

            _setupSnapshot = snapshot;

            Action<PublicSetupSnapshot> changed = SetupSnapshotChanged;
            if (changed != null) changed(snapshot);
        }

        void ApplyMatchSnapshot(PublicMatchSnapshot snapshot)
        {
            GateDecision decision = _gate.AcceptSnapshot(snapshot.Generation, snapshot.Revision, SnapshotChannel.Match);
            if (decision == GateDecision.Stale) return;
            if (decision == GateDecision.AcceptedNewGeneration) StartGeneration(snapshot.Generation);

            _matchSnapshot = snapshot;

            Action<PublicMatchSnapshot> changed = MatchSnapshotChanged;
            if (changed != null) changed(snapshot);
        }

        /// <summary>A new match reached this machine: forget the previous one's public state.</summary>
        void StartGeneration(int generation)
        {
            _setupSnapshot = default;
            _matchSnapshot = default;

            Debug.Log("Match generation " + generation + " reached this machine; local seat " + LocalSeatIndex + ".", this);

            Action<int> started = MatchGenerationStarted;
            if (started != null) started(generation);
        }

        /// <summary>
        /// Applies a message now, or holds it while this machine's replicated seating is
        /// still from an earlier generation. Seats and messages travel on different paths,
        /// so a client could otherwise draw a new match from its old seat.
        /// </summary>
        void Deliver(int generation, Action apply)
        {
            if (_deferred.Count == 0 && SeatingReady(generation))
            {
                apply();
                return;
            }

            _deferred.Add(new KeyValuePair<int, Action>(generation, apply));
        }

        void FlushDeferred()
        {
            while (_deferred.Count > 0 && SeatingReady(_deferred[0].Key))
            {
                Action apply = _deferred[0].Value;
                _deferred.RemoveAt(0);
                apply();
            }
        }

        bool SeatingReady(int generation)
        {
            return seatRegistry == null || generation <= seatRegistry.SeatingGeneration;
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

        /// <summary>Local human rematch vote. The host decides whether it counts.</summary>
        public void RequestRematchVote(bool yes)
        {
            if (!IsSpawned) return;

            SubmitRematchVoteRpc(yes);
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

                return _matchSnapshot.IsValid && IsCurrentSeating(_matchSnapshot.Generation) && _matchSnapshot.IsTurnOf(seat);
            }
        }

        /// <summary>
        /// The phase and the seats replicate separately from the snapshots, so for a
        /// moment at a rematch the local seat can be new while a snapshot is still the
        /// previous match's. Local input only trusts a snapshot of the current seating.
        /// </summary>
        bool IsCurrentSeating(int generation)
        {
            return seatRegistry == null || generation == seatRegistry.SeatingGeneration;
        }

        /// <summary>True while the local seat still owes an initial Shield choice.</summary>
        public bool LocalShieldChoicePending
        {
            get
            {
                if (Phase != OnlineMatchPhase.WaitingForShieldSelections) return false;

                int seat = LocalSeatIndex;
                if (seat == NetworkSeatRegistry.NoSeat || !_setupSnapshot.IsValid) return false;
                if (!IsCurrentSeating(_setupSnapshot.Generation)) return false;

                PublicSetupPlayer player = _setupSnapshot.GetPlayer(seat);
                return !player.IsBot && !player.SelectionComplete;
            }
        }

        // ------------------------------------------------------------------ disconnects

        /// <summary>
        /// A participant vanished. During a match TASK 009's policy stands: the match
        /// stops, because no substitute rule exists. After a match is over they simply
        /// stop counting toward the rematch vote, and the vote is re-evaluated.
        /// </summary>
        void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (Phase == OnlineMatchPhase.MatchOver)
            {
                List<ulong> humans = ConnectedHumans(clientId);
                _rematch.Prune(humans);
                PublishRematchState(humans);
                TryLockRematch(humans);
                return;
            }

            if (!MatchActive) return;

            // Whether they were mid setup or mid turn, the match cannot continue without
            // them and TASK 009 forbids inventing a substitute rule, so it stops.
            AbortMatch(clientId);
        }

        void AbortMatch(ulong leaving)
        {
            if (!IsServer) return;

            Debug.LogWarning("A participant disconnected during an active match; stopping it.", this);

            _botGate.Cancel();
            ClearHostState();
            _rematch.Reset();
            _rematchStartPending = false;
            SetPhase(OnlineMatchPhase.Aborted);
            PublishMatch();
            PublishRematchState();

            if (seatRegistry != null) seatRegistry.ReturnToLobbySeating(leaving);
        }

        // ------------------------------------------------------------------ diagnostics

        /// <summary>Development diagnostics. Never contains a hidden Charge identity.</summary>
        public string DescribeDiagnostics()
        {
            List<string> lines = new List<string>
            {
                "phase " + Phase,
                "local seat " + LocalSeatIndex,
                "gen " + _gate.Generation + " (match rev " + _gate.AppliedRevision(SnapshotChannel.Match) +
                    ", setup rev " + _gate.AppliedRevision(SnapshotChannel.Setup) + ")"
            };

            if (IsServer) lines.Add("host gen " + _generation + " rev " + _revision);
            if (_deferred.Count > 0) lines.Add(_deferred.Count + " waiting for seating");
            if (_matchSnapshot.IsValid) lines.Add("turn player " + _matchSnapshot.CurrentPlayerIndex);
            if (Phase == OnlineMatchPhase.MatchOver) lines.Add("rematch " + RematchYesCount + "/" + RematchRequired);

            return string.Join(" | ", lines);
        }
    }
}
