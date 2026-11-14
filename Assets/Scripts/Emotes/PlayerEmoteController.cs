using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Shields.Match;
using Shields.Network;
using Shields.Players;
using Shields.Social;
using Shields.Table;
using Shields.Tavern;

namespace Shields.Emotes
{
    /// <summary>
    /// The local player's hand emotes, for whichever match is running.
    ///
    /// Input (all held): Q = Middle Finger (left hand), W = Point (right hand), right mouse
    /// button = offer a handshake (right hand). Point and Handshake share the right hand, so
    /// whichever was pressed first keeps it. While pointing, the mouse wheel pushes the hand
    /// out or back within a small range; it resets when W is let go. Nothing here touches the
    /// camera, the cursor or the head look.
    ///
    /// Gameplay always wins: an emote is only held while its hand is free of cards, Charge,
    /// weapon and beer, and while no menu owns the mouse. A key still held when the hand comes
    /// free again resumes its emote.
    ///
    /// Authority: offline this machine owns the <see cref="EmoteAuthority"/>; online the host's
    /// <see cref="NetworkEmoteService"/> does and this only sends what the local seat holds,
    /// when it changes. Every machine shows the local seat at once and other seats from the
    /// authority's broadcasts, through one <see cref="PlayerEmotePresenter"/>. Bots never emote.
    /// Emotes never reach the Shields match.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerEmotePresenter))]
    public class PlayerEmoteController : MonoBehaviour
    {
        [Tooltip("Tells this which match is running, the local seat and who is alive (the beer coordinator already knows).")]
        [SerializeField] BeerOrderCoordinator matchContext;

        [SerializeField] NetworkEmoteService onlineEmotes;

        [Header("Point reach")]
        [Tooltip("Reach gained per mouse wheel notch, as a fraction of the full extra reach.")]
        [Range(0.02f, 1f)]
        [SerializeField] float reachPerNotch = 0.2f;

        [Tooltip("Fewest seconds between two reach updates sent to the authority while scrolling.")]
        [Range(0f, 1f)]
        [SerializeField] float reachSendInterval = 0.12f;

        [Header("Handshake")]
        [Tooltip("A living human within this many degrees of the crosshair is the one a handshake is offered to.")]
        [Range(2f, 30f)]
        [SerializeField] float handshakeTargetCone = 12f;

        enum RightUse { None, Point, Offer }

        PlayerEmotePresenter _presenter;
        PlayerActionPresenter _actions;
        BeerOrderUI _orderUI;
        bool _subscribed;

        bool _turnPlay;
        int _generation = int.MinValue;
        int _localSeat = -1;

        EmoteAuthority _offline;
        EmoteSeat[] _roster = new EmoteSeat[0];
        readonly List<int> _cleared = new List<int>();
        readonly List<HandshakePair> _started = new List<HandshakePair>();
        readonly List<HandshakeEnd> _ended = new List<HandshakeEnd>();

        RightUse _right;
        float _reach;
        EmoteState _shown = EmoteState.Idle;
        EmoteState _sent = EmoteState.Idle;
        float _nextReachSend;

        EmoteFlags _externalHeld;
        float _externalNotches;

        public PlayerEmotePresenter Presenter { get { return _presenter; } }

        /// <summary>What the local seat shows right now.</summary>
        public EmoteState LocalState { get { return _shown; } }

        /// <summary>
        /// Holds emotes from an input source other than the keys and mouse (a future gamepad
        /// binding, or validation). Combined with the real input: either one holds.
        /// </summary>
        public void SetHeld(EmoteFlags held) { _externalHeld = held; }

        /// <summary>Mouse wheel notches from another input source: positive pushes the pointing hand out.</summary>
        public void AddReachNotches(float notches) { _externalNotches += notches; }

        void Awake()
        {
            _presenter = GetComponent<PlayerEmotePresenter>();
        }

        void OnEnable() { Subscribe(); }

        void OnDisable()
        {
            Unsubscribe();
            if (_presenter != null) _presenter.ClearAll();
            ResetLocal();
            _offline = null;
            _turnPlay = false;
            _generation = int.MinValue;
        }

        void Subscribe()
        {
            if (_subscribed || _presenter == null) return;

            _presenter.HandshakeInterrupted += OnHandshakeInterrupted;
            if (onlineEmotes != null)
            {
                onlineEmotes.StateChanged += OnAuthorityState;
                onlineEmotes.HandshakeStarted += OnHandshakeStarted;
                onlineEmotes.HandshakeEnded += OnHandshakeEnded;
            }

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            _presenter.HandshakeInterrupted -= OnHandshakeInterrupted;
            if (onlineEmotes != null)
            {
                onlineEmotes.StateChanged -= OnAuthorityState;
                onlineEmotes.HandshakeStarted -= OnHandshakeStarted;
                onlineEmotes.HandshakeEnded -= OnHandshakeEnded;
            }

            _subscribed = false;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (matchContext == null || _presenter == null) return;

            BindPresenter();
            TrackLifecycle();
            if (!_turnPlay) return;

            if (!matchContext.IsOnline) TickOffline();
            HandleLocalInput();
        }

        void BindPresenter()
        {
            if (_actions == null) _actions = FindAnyObjectByType<PlayerActionPresenter>();
            if (_orderUI == null) _orderUI = FindAnyObjectByType<BeerOrderUI>();

            BeerDrinkPresenter beer = matchContext.Drinks != null ? matchContext.Drinks.Presenter : null;
            _presenter.Bind(matchContext.TableLayout, _actions, beer);
        }

        /// <summary>Emotes belong to one match's turn play; anything else drops them everywhere at once.</summary>
        void TrackLifecycle()
        {
            bool turnPlay = matchContext.TurnPlayActive;
            int generation = matchContext.CurrentGeneration;
            int seat = matchContext.LocalSeat;

            if (turnPlay == _turnPlay && generation == _generation && seat == _localSeat) return;

            _turnPlay = turnPlay;
            _generation = generation;
            _localSeat = seat;
            _presenter.ClearAll();
            ResetLocal();

            int seats = matchContext.TableLayout != null ? matchContext.TableLayout.Players.Count : 0;
            _offline = turnPlay && !matchContext.IsOnline ? new EmoteAuthority(seats, generation) : null;
        }

        void ResetLocal()
        {
            _right = RightUse.None;
            _reach = 0f;
            _shown = EmoteState.Idle;
            _sent = EmoteState.Idle;
            _externalNotches = 0f;
        }

        // ------------------------------------------------------------------ offline authority

        void TickOffline()
        {
            if (_offline == null) return;

            BuildOfflineRoster();
            _cleared.Clear();
            _started.Clear();
            _ended.Clear();
            _offline.Tick(Time.unscaledTimeAsDouble, _roster, _cleared, _started, _ended);
            ApplyAuthorityChanges();
        }

        void BuildOfflineRoster()
        {
            TableLayoutController table = matchContext.TableLayout;
            int seats = _offline.SeatCount;
            if (_roster.Length != seats) _roster = new EmoteSeat[seats];

            for (int i = 0; i < seats; i++)
            {
                PlayerCharacter player = table != null ? table.GetPlayer(i) : null;
                bool bot = player == null || player.IsBot;
                _roster[i] = new EmoteSeat(player != null, matchContext.IsSeatAlive(i), bot);
            }
        }

        void ApplyAuthorityChanges()
        {
            for (int i = 0; i < _cleared.Count; i++) OnAuthorityState(_cleared[i], EmoteState.Idle);
            for (int i = 0; i < _ended.Count; i++) OnHandshakeEnded(_ended[i]);
            for (int i = 0; i < _started.Count; i++) OnHandshakeStarted(_started[i]);

            _cleared.Clear();
            _started.Clear();
            _ended.Clear();
        }

        // ------------------------------------------------------------------ local input

        void HandleLocalInput()
        {
            int seat = matchContext.LocalSeat;
            PlayerCharacter player = matchContext.TableLayout != null ? matchContext.TableLayout.GetPlayer(seat) : null;

            bool canEmote = seat >= 0 && player != null && !player.IsBot && matchContext.IsSeatAlive(seat) && !MenuOwnsInput(player);

            EmoteFlags held = canEmote ? KeysHeld() | _externalHeld : EmoteFlags.None;
            float notches = ReadScrollNotches() + _externalNotches;
            _externalNotches = 0f;

            bool middle = (held & EmoteFlags.MiddleFinger) != 0 && !_presenter.LeftHandBusy(seat);

            bool rightFree = !_presenter.RightHandBusy(seat);
            bool pointKey = (held & EmoteFlags.Point) != 0;
            bool offerKey = (held & EmoteFlags.HandshakeOffer) != 0;

            if (!rightFree) _right = RightUse.None;
            else
            {
                if (_right == RightUse.Point && !pointKey) _right = RightUse.None;
                if (_right == RightUse.Offer && !offerKey) _right = RightUse.None;
                if (_right == RightUse.None) _right = pointKey ? RightUse.Point : offerKey ? RightUse.Offer : RightUse.None;
            }

            if (_right == RightUse.Point) _reach = Mathf.Clamp01(_reach + notches * reachPerNotch);
            else _reach = 0f;

            int target = EmoteState.NoTarget;
            if (_right == RightUse.Offer)
                target = EmoteTargeting.FindLookTarget(matchContext.TableLayout, seat, handshakeTargetCone, CanShakeWith, out _);

            EmoteFlags flags = EmoteFlags.None;
            if (middle) flags |= EmoteFlags.MiddleFinger;
            if (_right == RightUse.Point) flags |= EmoteFlags.Point;
            if (_right == RightUse.Offer) flags |= EmoteFlags.HandshakeOffer;

            EmoteState state = new EmoteState(flags, (byte)Mathf.RoundToInt(_reach * 255f), target);

            if (state != _shown)
            {
                _shown = state;
                _presenter.SetState(seat, state);
            }

            if (state == _sent) return;

            // Presses, releases and target changes go at once; a wheel-only change waits its turn.
            bool reachOnly = state.Flags == _sent.Flags && state.Target == _sent.Target;
            if (reachOnly && Time.unscaledTime < _nextReachSend) return;

            _nextReachSend = Time.unscaledTime + reachSendInterval;
            _sent = state;
            Submit(seat, state);
        }

        /// <summary>A handshake is only offered to a living human other than this player.</summary>
        bool CanShakeWith(int seat)
        {
            PlayerCharacter other = matchContext.TableLayout != null ? matchContext.TableLayout.GetPlayer(seat) : null;
            return other != null && !other.IsBot && matchContext.IsSeatAlive(seat);
        }

        bool MenuOwnsInput(PlayerCharacter player)
        {
            if (_orderUI != null && _orderUI.IsOpen) return true;

            SeatedHeadLook look = player.GetComponent<SeatedHeadLook>();
            return look != null && look.InputSuppressed;
        }

        void Submit(int seat, EmoteState state)
        {
            if (matchContext.IsOnline)
            {
                if (onlineEmotes != null) onlineEmotes.RequestState(state, _generation);
                return;
            }

            if (_offline == null) return;

            BuildOfflineRoster();
            _offline.Submit(seat, state, _generation, _roster, out _);
        }

        // ------------------------------------------------------------------ authority events

        void OnAuthorityState(int seat, EmoteState state)
        {
            if (!_turnPlay) return;

            // This machine already shows its own seat from its own input.
            if (seat == matchContext.LocalSeat) return;

            _presenter.SetState(seat, state);
        }

        void OnHandshakeStarted(HandshakePair pair)
        {
            if (!_turnPlay) return;

            Debug.Log("Emote: " + pair + " starts here", this);
            _presenter.BeginHandshake(pair);
        }

        void OnHandshakeEnded(HandshakeEnd end)
        {
            _presenter.EndHandshake(end);
        }

        /// <summary>Gameplay took a right hand mid handshake. Only the seat's own machine tells the authority.</summary>
        void OnHandshakeInterrupted(int seat, HandshakePair pair)
        {
            if (seat != matchContext.LocalSeat) return;

            if (matchContext.IsOnline)
            {
                if (onlineEmotes != null) onlineEmotes.RequestInterrupt(_generation);
                return;
            }

            if (_offline == null) return;

            _ended.Clear();
            if (_offline.Interrupt(seat, _generation, _ended))
            {
                for (int i = 0; i < _ended.Count; i++) OnHandshakeEnded(_ended[i]);
            }

            _ended.Clear();
        }

        // ------------------------------------------------------------------ input

        static EmoteFlags KeysHeld()
        {
            EmoteFlags held = EmoteFlags.None;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.isPressed) held |= EmoteFlags.MiddleFinger;
                if (keyboard.wKey.isPressed) held |= EmoteFlags.Point;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed) held |= EmoteFlags.HandshakeOffer;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.Q)) held |= EmoteFlags.MiddleFinger;
            if (Input.GetKey(KeyCode.W)) held |= EmoteFlags.Point;
            if (Input.GetMouseButton(1)) held |= EmoteFlags.HandshakeOffer;
#endif
            return held;
        }

        /// <summary>Wheel notches this frame, positive away from the player. Read only, never consumed.</summary>
        static float ReadScrollNotches()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return 0f;

            // Some platforms report 120 per notch, others 1.
            float y = mouse.scroll.ReadValue().y;
            return Mathf.Abs(y) >= 20f ? y / 120f : y;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }
    }
}
