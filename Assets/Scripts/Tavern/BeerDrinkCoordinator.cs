using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Shields.Match;
using Shields.Network;
using Shields.Players;
using Shields.Social;

namespace Shields.Tavern
{
    /// <summary>
    /// Connects beer drinking to whichever match is running, like
    /// <see cref="BeerOrderCoordinator"/> does for ordering. Created by it at runtime.
    ///
    /// Input: hold B to drink the local seat's oldest beer that still has something in it;
    /// let go to put it down. A small "DRINK (B)" prompt shows when that is possible.
    ///
    /// Authority: offline this machine owns the <see cref="BeerLedger"/> and answers its
    /// own requests; online the host's <see cref="NetworkBeerOrderService"/> owns it and
    /// this only forwards intent. Either way the same ledger decides, and every machine
    /// animates the authoritative start and stop events through one
    /// <see cref="BeerDrinkPresenter"/>. On the authority it also ends drinks that ran dry
    /// or whose drinker died or left, and turns local interruptions into authoritative
    /// stops. Beer never reaches the Shields match.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeerDrinkCoordinator : MonoBehaviour
    {
        static readonly Color AccentColor = new Color(1f, 0.82f, 0.45f);
        static readonly Color ActiveColor = new Color(1f, 0.93f, 0.62f);

        BeerOrderCoordinator _orders;
        BeerDrinkPresenter _presenter;
        NetworkBeerOrderService _service;
        BeerOrderUI _orderUI;
        PlayerActionPresenter _actions;

        readonly List<BeerDrinkStop> _stops = new List<BeerDrinkStop>();

        bool _turnPlay;
        int _generation = -1;

        // The local drink this machine asked for, while the key is held.
        bool _localHolding;
        BeerId _localBeer;
        bool _waitForRelease;

        GUIStyle _promptStyle;

        public BeerDrinkPresenter Presenter { get { return _presenter; } }

        /// <summary>Called by <see cref="BeerOrderCoordinator"/> when it creates this component.</summary>
        public void Bind(BeerOrderCoordinator orders)
        {
            _orders = orders;

            _presenter = GetComponent<BeerDrinkPresenter>();
            if (_presenter == null) _presenter = gameObject.AddComponent<BeerDrinkPresenter>();
            _presenter.Bind(orders.TableLayout, orders.LocalHeadLook);

            // Added at runtime, this component was enabled before it was bound.
            if (isActiveAndEnabled) Subscribe();
        }

        bool _subscribed;

        void OnEnable() { Subscribe(); }

        void OnDisable()
        {
            Unsubscribe();
            if (_presenter != null) _presenter.ClearAll();
            ResetLocalInput();
        }

        void Subscribe()
        {
            if (_subscribed || _orders == null || _presenter == null) return;

            _presenter.Interrupted += OnInterrupted;
            _presenter.Finished += OnFinished;

            _service = _orders.OnlineOrders;
            if (_service != null)
            {
                _service.DrinkStarted += OnDrinkStarted;
                _service.DrinkStopped += OnDrinkStopped;
                _service.DrinkRefused += OnDrinkRefused;
            }

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            _presenter.Interrupted -= OnInterrupted;
            _presenter.Finished -= OnFinished;

            if (_service != null)
            {
                _service.DrinkStarted -= OnDrinkStarted;
                _service.DrinkStopped -= OnDrinkStopped;
                _service.DrinkRefused -= OnDrinkRefused;
            }

            _subscribed = false;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (_orders == null || _presenter == null) return;

            if (_orderUI == null) _orderUI = FindAnyObjectByType<BeerOrderUI>();
            if (_actions == null) _actions = FindAnyObjectByType<PlayerActionPresenter>();

            TrackLifecycle();
            if (!_turnPlay) return;

            InterruptTheDead();
            TickAuthority();
            HandleLocalInput();
        }

        /// <summary>Drinks belong to one match's turn play; anything else drops them everywhere at once.</summary>
        void TrackLifecycle()
        {
            bool turnPlay = _orders.TurnPlayActive;
            int generation = _orders.CurrentGeneration;

            if (turnPlay == _turnPlay && generation == _generation) return;

            _turnPlay = turnPlay;
            _generation = generation;
            _presenter.ClearAll();
            ResetLocalInput();
        }

        /// <summary>A drinker who died is put down at once on this machine; the authority confirms the fill.</summary>
        void InterruptTheDead()
        {
            int seats = _orders.TableLayout != null ? _orders.TableLayout.Players.Count : 0;
            for (int seat = 0; seat < seats; seat++)
                if (_presenter.IsDrinking(seat) && !_orders.IsSeatAlive(seat)) _presenter.Interrupt(seat);
        }

        void TickAuthority()
        {
            BeerLedger ledger = _orders.AuthoritativeLedger;
            if (ledger == null) return;

            _stops.Clear();
            ledger.Tick(Time.unscaledTimeAsDouble, _orders.BuildAuthoritativeRoster(), _stops);
            for (int i = 0; i < _stops.Count; i++) PublishStop(_stops[i]);
        }

        // ------------------------------------------------------------------ local input

        /// <summary>
        /// Drinking is allowed for a living human during turn play, while no targeting,
        /// order panel or gameplay animation owns the left hand or the input.
        /// </summary>
        public bool LocalDrinkAllowed
        {
            get
            {
                if (_orders == null || !_orders.OrderingAvailable || _orders.LocalTargetingActive) return false;
                if (_orderUI != null && _orderUI.IsOpen) return false;

                int seat = _orders.LocalSeat;
                return _actions == null || !_actions.IsLeftHandBusy(seat);
            }
        }

        /// <summary>The mug a press would drink now, or null.</summary>
        BeerMugVisual LocalCandidate()
        {
            int seat = _orders.LocalSeat;
            if (seat < 0 || _presenter.IsBusy(seat) || !LocalDrinkAllowed) return null;

            return _presenter.FindOldestDrinkable(seat);
        }

        /// <summary>
        /// Holds or lets go of "drink" from an input source other than the B key (a future
        /// gamepad binding, or validation). Combined with the key: either one holds.
        /// </summary>
        public void SetDrinkHeld(bool held)
        {
            if (held && !_externalHeld) _externalPressed = true;
            _externalHeld = held;
        }

        bool _externalHeld;
        bool _externalPressed;

        void HandleLocalInput()
        {
            bool held = DrinkKeyHeld() || _externalHeld;
            bool pressed = DrinkKeyPressedThisFrame() || _externalPressed;
            _externalPressed = false;

            if (!held) _waitForRelease = false;

            if (_localHolding)
            {
                if (!held) StopLocal();
                return;
            }

            if (!held || _waitForRelease || !pressed) return;

            BeerMugVisual mug = LocalCandidate();
            if (mug == null) return;

            int seat = _orders.LocalSeat;
            if (!_presenter.BeginLocal(seat, mug)) return;

            _localHolding = true;
            _localBeer = mug.Beer;
            RequestStart(mug.Beer);
        }

        void StopLocal()
        {
            if (!_localHolding) return;

            _localHolding = false;
            _presenter.ReleaseLocal(_orders.LocalSeat);
            RequestStop(_localBeer);
        }

        void ResetLocalInput()
        {
            _localHolding = false;
            _waitForRelease = DrinkKeyHeld() || _externalHeld;
            _externalPressed = false;
        }

        // ------------------------------------------------------------------ requests

        void RequestStart(BeerId beer)
        {
            if (_orders.IsOnline)
            {
                if (_service == null || !_service.RequestDrink(beer, true, _orders.CurrentGeneration))
                    _presenter.Refuse(beer);
                return;
            }

            BeerLedger ledger = _orders.AuthoritativeLedger;
            BeerDrinkStart start = ledger != null
                ? ledger.TryStart(_orders.LocalSeat, beer, _orders.CurrentGeneration, _orders.BuildAuthoritativeRoster(),
                    Time.unscaledTimeAsDouble)
                : BeerDrinkStart.Refused(BeerDrinkRejection.NotAvailable, beer);

            if (start.Accepted) OnDrinkStarted(start);
            else OnDrinkRefused(beer, start.Rejection);
        }

        void RequestStop(BeerId beer)
        {
            if (_orders.IsOnline)
            {
                if (_service != null) _service.RequestDrink(beer, false, _orders.CurrentGeneration);
                return;
            }

            BeerLedger ledger = _orders.AuthoritativeLedger;
            BeerDrinkStop stop;
            if (ledger != null && ledger.TryStop(_orders.LocalSeat, beer, ledger.Generation, Time.unscaledTimeAsDouble,
                    BeerDrinkStopReason.Released, out stop))
                PublishStop(stop);
        }

        /// <summary>Authority only: tells every machine (offline: this one) that a drink ended.</summary>
        void PublishStop(BeerDrinkStop stop)
        {
            Debug.Log("Beer drink: " + stop, this);

            if (_orders.IsOnline)
            {
                if (_service != null) _service.BroadcastDrinkStopped(stop);
            }
            else
            {
                OnDrinkStopped(stop);
            }
        }

        // ------------------------------------------------------------------ authoritative events

        void OnDrinkStarted(BeerDrinkStart start)
        {
            if (!_turnPlay) return;

            bool local = start.Beer.Seat == _orders.LocalSeat;
            _presenter.Confirm(start, local);
        }

        void OnDrinkStopped(BeerDrinkStop stop)
        {
            _presenter.Stop(stop);
        }

        void OnDrinkRefused(BeerId beer, BeerDrinkRejection rejection)
        {
            _presenter.Refuse(beer);
            if (beer == _localBeer && _localHolding)
            {
                _localHolding = false;
                _waitForRelease = true;
            }

            Debug.Log("Beer drink refused: " + beer + " (" + rejection + ")", this);
        }

        /// <summary>
        /// Gameplay took the hand, or the drinker died, on this machine. The authority ends
        /// the drink at once; the owning client asks the host to.
        /// </summary>
        void OnInterrupted(int seat, BeerId beer)
        {
            BeerLedger ledger = _orders.AuthoritativeLedger;
            if (ledger != null)
            {
                BeerDrinkStop stop;
                if (ledger.TryStop(seat, beer, ledger.Generation, Time.unscaledTimeAsDouble,
                        BeerDrinkStopReason.Interrupted, out stop))
                    PublishStop(stop);
            }
            else if (seat == _orders.LocalSeat && _service != null)
            {
                _service.RequestDrink(beer, false, _orders.CurrentGeneration);
            }

            if (seat == _orders.LocalSeat && _localHolding && beer == _localBeer)
            {
                _localHolding = false;
                _waitForRelease = true;
            }
        }

        /// <summary>The local drink ended on its own (empty, refused, timed out): wait for the key to be let go.</summary>
        void OnFinished(int seat)
        {
            if (seat != _orders.LocalSeat || !_localHolding) return;

            _localHolding = false;
            _waitForRelease = true;

            // Make sure the authority is not left draining a mug nobody is holding (a no-op
            // when it already ended the drink itself, e.g. the beer ran out).
            RequestStop(_localBeer);
        }

        // ------------------------------------------------------------------ prompt

        void OnGUI()
        {
            if (_orders == null || _presenter == null || !_turnPlay) return;

            int seat = _orders.LocalSeat;
            bool drinking = seat >= 0 && _presenter.IsDrinking(seat);
            if (!drinking && LocalCandidate() == null) return;

            if (_promptStyle == null)
                _promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            float scale = Screen.height / 1080f;
            _promptStyle.fontSize = Mathf.RoundToInt(20f * scale);

            // Beside the ORDER! (O) button in the bottom left corner, same size and style.
            float margin = 16f * scale;
            float bottom = Screen.height - 40f * scale;
            Rect rect = new Rect(margin + 150f * scale + 8f * scale, bottom - 30f * scale, 130f * scale, 30f * scale);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, drinking ? 0.6f : 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;

            Color text = drinking ? ActiveColor : AccentColor;
            MatchHud.ShadowLabel(rect, "DRINK (B)", _promptStyle, new Color(text.r, text.g, text.b, drinking ? 1f : 0.9f));
        }

        // ------------------------------------------------------------------ input

        static bool DrinkKeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) return keyboard.bKey.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.B);
#else
            return false;
#endif
        }

        static bool DrinkKeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) return keyboard.bKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.B);
#else
            return false;
#endif
        }
    }
}
