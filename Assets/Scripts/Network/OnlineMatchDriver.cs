using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Shields.Cards;
using Shields.Game;
using Shields.Match;
using Shields.Online;
using Shields.Players;
using Shields.Presentation;
using Shields.Table;

namespace Shields.Network
{
    /// <summary>
    /// The online match as this machine sees and plays it.
    ///
    /// It owns no gameplay state. It turns the host's public snapshots into the same
    /// table, labels and HUD the offline prototype draws, and turns this machine's input
    /// into intent for the host. Every outcome on screen was decided by the host's
    /// <see cref="ShieldsMatch"/>; nothing here predicts, and nothing here draws a card.
    ///
    /// Presentation is reused rather than rebuilt: the seating comes from
    /// <see cref="NetworkTestPresenter"/>, the table and labels from
    /// <see cref="MatchPresenter"/>, the screen HUD from <see cref="MatchHud"/>, and the
    /// Shield choice from the existing crosshair <see cref="LocalShieldSelection"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class OnlineMatchDriver : MonoBehaviour
    {
        [Header("Network")]
        [SerializeField] NetworkMatchController matchController;
        [SerializeField] NetworkSeatRegistry seatRegistry;
        [SerializeField] NetworkTestPresenter layoutPresenter;

        [Header("Presentation")]
        [SerializeField] TableLayoutController tableLayout;
        [SerializeField] CardView cardViewPrefab;
        [SerializeField] DeckView deckView;
        [SerializeField] DiscardPileView discardView;
        [SerializeField] CrosshairInteraction crosshair;
        [SerializeField] LocalShieldSelection shieldSelection;

        [Tooltip("Animates resolved actions on the table. Optional.")]
        [SerializeField] PlayerActionPresenter actionPresenter;

        [Header("Feedback")]
        [Tooltip("How long world-space action text and revealed cards stay visible.")]
        [SerializeField] float feedbackSeconds = 1.8f;

        [Tooltip("How long the latest action stays in the screen-space HUD panel.")]
        [SerializeField] float hudFeedbackSeconds = 3f;

        [Tooltip("Show the development network diagnostics line.")]
        [SerializeField] bool showDiagnostics = true;

        enum PendingAction { None, Attack, ReplaceShield, ChargedAttack }

        MatchPresenter _presenter;
        MatchHud _hud;

        PendingAction _pending;
        int _hoveredTarget = TurnActionRequest.NoTarget;
        bool _layoutReady;
        bool _shieldChoiceStarted;
        string _refusal = string.Empty;
        float _refusalShownAt = -100f;

        int LocalSeat
        {
            get { return matchController != null ? matchController.LocalSeatIndex : NetworkSeatRegistry.NoSeat; }
        }

        /// <summary>
        /// True while this machine's player is choosing an Attack, Replace Shield or
        /// Charged Attack target with the crosshair. Social menus read it to stay off the mouse.
        /// </summary>
        public bool LocalTargetingActive { get { return _pending != PendingAction.None; } }

        void Awake()
        {
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            if (matchController == null) return;

            matchController.PhaseChanged += OnPhaseChanged;
            matchController.MatchGenerationStarted += OnMatchGenerationStarted;
            matchController.SetupSnapshotChanged += OnSetupSnapshot;
            matchController.MatchSnapshotChanged += OnMatchSnapshot;
            matchController.ActionResolved += OnActionResolved;
            matchController.RequestRefused += OnRequestRefused;
        }

        void Unsubscribe()
        {
            if (matchController == null) return;

            matchController.PhaseChanged -= OnPhaseChanged;
            matchController.MatchGenerationStarted -= OnMatchGenerationStarted;
            matchController.SetupSnapshotChanged -= OnSetupSnapshot;
            matchController.MatchSnapshotChanged -= OnMatchSnapshot;
            matchController.ActionResolved -= OnActionResolved;
            matchController.RequestRefused -= OnRequestRefused;
        }

        // ------------------------------------------------------------------ phases

        void OnPhaseChanged(OnlineMatchPhase phase)
        {
            switch (phase)
            {
                case OnlineMatchPhase.SessionLobby:
                case OnlineMatchPhase.Aborted:
                    TearDown();
                    break;

                case OnlineMatchPhase.MatchOver:
                    // Stop accepting input, but leave the final table on screen.
                    EndShieldChoice();
                    ClearPending();
                    break;

                case OnlineMatchPhase.WaitingForShieldSelections:
                    // The phase and the setup snapshot travel separately; whichever lands
                    // second starts the local Shield choice.
                    if (matchController.SetupSnapshot.IsValid) TryBeginLocalShieldChoice(matchController.SetupSnapshot);
                    break;
            }
        }

        /// <summary>
        /// A new match (first match or rematch) reached this machine. Everything of the
        /// previous match goes, the seat layout included: seating is new every match, so
        /// characters, camera, look and roles are rebuilt from the new seat mapping by the
        /// snapshot that follows.
        /// </summary>
        void OnMatchGenerationStarted(int generation)
        {
            TearDown();
            if (layoutPresenter != null) layoutPresenter.TearDown();

            _refusal = string.Empty;
        }

        /// <summary>
        /// Draws the public initial deal. Every dealt card is public by the rules, so all
        /// three are shown for every seat until that seat has chosen.
        /// </summary>
        void OnSetupSnapshot(PublicSetupSnapshot setup)
        {
            if (!EnsureLayout(setup.PlayerCount)) return;

            EnsurePresenter();

            for (int seat = 0; seat < setup.PlayerCount; seat++)
            {
                PublicSetupPlayer player = setup.GetPlayer(seat);

                PlayerTableArea area = tableLayout.GetAreaForPlayer(seat);
                if (area == null) continue;

                area.ShowDealtCards(ToPlayingCards(player.DealtCards), cardViewPrefab);

                if (player.SelectionComplete)
                    area.ArrangeRoles(ToPlayingCards(player.HealthCards), player.ShieldCard.ToPlayingCard());
            }

            TryBeginLocalShieldChoice(setup);
        }

        void OnMatchSnapshot(PublicMatchSnapshot snapshot)
        {
            if (!EnsureLayout(snapshot.PlayerCount)) return;

            EnsurePresenter();
            EndShieldChoice();

            _presenter.Sync(snapshot);

            // Only the count travels; a client never holds a deck.
            if (deckView != null) deckView.ShowRemainingCount(snapshot.RemainingDrawCount);
        }

        void OnActionResolved(PublicActionResult result)
        {
            if (_presenter == null || !result.Accepted) return;

            // The HUD line names the drawn card, so it appears when the card lands.
            MatchHud hud = _hud;
            int localSeat = LocalSeat;
            _presenter.ShowResult(result, () => hud.ShowResult(result, localSeat));
            ClearPending();
        }

        void OnRequestRefused(string reason)
        {
            _refusal = reason;
            _refusalShownAt = Time.unscaledTime;
            ClearPending();

            if (actionPresenter != null) actionPresenter.CancelAnticipation(LocalSeat);
        }

        // ------------------------------------------------------------------ setup

        bool EnsureLayout(int seatCount)
        {
            if (layoutPresenter == null || seatCount <= 0) return false;

            _layoutReady = layoutPresenter.EnsureLayout(seatCount);
            return _layoutReady;
        }

        void EnsurePresenter()
        {
            if (_presenter != null) return;

            _presenter = new MatchPresenter(tableLayout, cardViewPrefab, deckView, discardView, actionPresenter,
                LocalSeat, feedbackSeconds);
            _hud = new MatchHud(hudFeedbackSeconds);

            AddTargetColliders();
        }

        /// <summary>
        /// Offers this machine's own three cards to the crosshair. The click only reports
        /// which card was aimed at; the host decides whether that choice is legal.
        /// </summary>
        void TryBeginLocalShieldChoice(PublicSetupSnapshot setup)
        {
            if (_shieldChoiceStarted || shieldSelection == null) return;
            if (matchController == null || !matchController.LocalShieldChoicePending) return;

            int seat = LocalSeat;
            PlayerTableArea area = tableLayout != null ? tableLayout.GetAreaForPlayer(seat) : null;
            if (area == null || area.CardViews.Count == 0) return;

            _shieldChoiceStarted = shieldSelection.Begin(crosshair, area.CardViews, OnLocalShieldChosen);
        }

        void OnLocalShieldChosen(PlayingCard card)
        {
            _shieldChoiceStarted = false;

            // Intent only: the card's identity, never a decision about setup state.
            if (matchController != null) matchController.RequestShieldChoice(card.CardId);
        }

        void EndShieldChoice()
        {
            if (!_shieldChoiceStarted) return;

            if (shieldSelection != null) shieldSelection.End();
            _shieldChoiceStarted = false;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (matchController == null || !_layoutReady) return;
            if (!LocalInputAllowed)
            {
                if (_pending != PendingAction.None) ClearPending();
                return;
            }

            for (int key = 1; key <= 3; key++)
                if (KeyPressed(key)) { PressActionKey(key); break; }

            if (_pending == PendingAction.None) return;

            int target = TargetUnderCrosshair();
            _hoveredTarget = IsSelectableTarget(target) ? target : TurnActionRequest.NoTarget;
            if (_presenter != null) _presenter.SetTargetHighlight(_hoveredTarget);

            if (_hoveredTarget != TurnActionRequest.NoTarget && crosshair != null && crosshair.PrimaryPressedThisFrame())
                ConfirmTarget(_hoveredTarget);
        }

        /// <summary>
        /// 1 and 2 enter targeting; 3 sends a first Charge immediately or enters Charged
        /// Attack targeting. Whether this seat holds a Charge comes from public state.
        /// </summary>
        public bool PressActionKey(int key)
        {
            if (!LocalInputAllowed) return false;

            switch (key)
            {
                case 1: SetPending(PendingAction.Attack); return true;
                case 2: SetPending(PendingAction.ReplaceShield); return true;
                case 3:
                    if (matchController.MatchSnapshot.GetPlayer(LocalSeat).HasCharge)
                    {
                        SetPending(PendingAction.ChargedAttack);
                        return true;
                    }

                    matchController.RequestAction(TurnActionType.Charge, TurnActionRequest.NoTarget);

                    // Cosmetic only: the hand starts reaching now and goes no further than
                    // the deck until the host's result confirms the Charge.
                    if (actionPresenter != null && _presenter != null) actionPresenter.AnticipateFirstCharge(LocalSeat);
                    return true;

                default:
                    return false;
            }
        }

        public bool ConfirmTarget(int target)
        {
            if (matchController == null || _pending == PendingAction.None || !IsSelectableTarget(target)) return false;

            // Cosmetic and reversible: the weapon locks on the clicked player now and the
            // left hand keeps waiting on the deck; the host's result confirms both, a
            // refusal puts them away.
            if (_presenter != null && _pending != PendingAction.ReplaceShield) _presenter.AnticipateWeaponLock(LocalSeat, target);
            if (_presenter != null) _presenter.AnticipateTarget(LocalSeat);

            switch (_pending)
            {
                case PendingAction.Attack:
                    matchController.RequestAction(TurnActionType.Attack, target);
                    break;

                case PendingAction.ReplaceShield:
                    matchController.RequestAction(TurnActionType.ReplaceShield, target);
                    break;

                default:
                    matchController.RequestAction(TurnActionType.Charge, target);
                    break;
            }

            return true;
        }

        /// <summary>
        /// The host says it is this seat's turn, and the table has finished presenting the
        /// previous action. UX only: the host still validates every request.
        /// </summary>
        bool LocalInputAllowed
        {
            get
            {
                return matchController != null && matchController.LocalInputAllowed &&
                    (_presenter == null || !_presenter.IsHoldingResult);
            }
        }

        int TargetUnderCrosshair()
        {
            RaycastHit hit;
            if (crosshair == null || !crosshair.TryGetHit(out hit)) return TurnActionRequest.NoTarget;

            PlayerCharacter character = hit.collider.GetComponentInParent<PlayerCharacter>();
            if (character != null) return character.PlayerIndex;

            CardView card = hit.collider.GetComponentInParent<CardView>();
            if (card != null && tableLayout != null)
            {
                PlayerTableArea area = tableLayout.GetAreaForPlayer(card.OwnerPlayerIndex);
                if (area != null && area.ShieldView == card) return card.OwnerPlayerIndex;
            }

            return TurnActionRequest.NoTarget;
        }

        /// <summary>Highlight filter only, from public state. The host still validates.</summary>
        bool IsSelectableTarget(int target)
        {
            PublicMatchSnapshot snapshot = matchController.MatchSnapshot;

            if (target < 0 || target >= snapshot.PlayerCount) return false;
            if (!snapshot.GetPlayer(target).IsAlive) return false;

            return _pending == PendingAction.ReplaceShield || target != LocalSeat;
        }

        void ClearPending()
        {
            SetPending(PendingAction.None);
            _hoveredTarget = TurnActionRequest.NoTarget;
            if (_presenter != null) _presenter.SetTargetHighlight(TurnActionRequest.NoTarget);
        }

        /// <summary>
        /// Changes the pending action and keeps the pose in step: every targeted action
        /// leans in with the left hand on the Draw Pile, and Attack and Charged Attack
        /// targeting also draw the weapon. Anything else returns to idle, unless a target
        /// was already clicked or confirmed by the host. Presentation only.
        /// </summary>
        void SetPending(PendingAction pending)
        {
            _pending = pending;
            if (_presenter == null) return;

            if (pending == PendingAction.Attack || pending == PendingAction.ChargedAttack)
                _presenter.BeginWeaponAim(LocalSeat);
            else
                _presenter.EndWeaponAim(LocalSeat);

            if (pending != PendingAction.None) _presenter.BeginTargeting(LocalSeat);
            else _presenter.EndTargeting(LocalSeat);
        }

        // ------------------------------------------------------------------ teardown

        public void TearDown()
        {
            EndShieldChoice();
            ClearPending();

            if (_presenter != null) _presenter.Clear();
            _presenter = null;
            _hud = null;

            if (deckView != null) deckView.Clear();
            _layoutReady = false;
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (matchController == null) return;

            OnlineMatchPhase phase = matchController.Phase;

            if (_hud != null && (phase == OnlineMatchPhase.Playing || phase == OnlineMatchPhase.MatchOver))
            {
                // The table's state, which waits for an action's card to land like the table does.
                PublicMatchSnapshot shown = _presenter != null && _presenter.DisplayedSnapshot.IsValid
                    ? _presenter.DisplayedSnapshot
                    : matchController.MatchSnapshot;

                _hud.Draw(shown, LocalSeat, PendingForHud(), _hoveredTarget);
            }
            else if (phase == OnlineMatchPhase.WaitingForShieldSelections)
                DrawSetupHint();

            DrawRefusal();

            if (showDiagnostics) DrawDiagnostics();
        }

        HudPendingAction PendingForHud()
        {
            switch (_pending)
            {
                case PendingAction.Attack: return HudPendingAction.Attack;
                case PendingAction.ReplaceShield: return HudPendingAction.ReplaceShield;
                case PendingAction.ChargedAttack: return HudPendingAction.ChargedAttack;
                default: return HudPendingAction.None;
            }
        }

        void DrawSetupHint()
        {
            float scale = Screen.height / 1080f;
            Rect line = new Rect(0f, 120f * scale, Screen.width, 40f * scale);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(26f * scale)
            };

            string text = matchController.LocalShieldChoicePending
                ? "Look at one of your three cards and click to make it your Shield."
                : "Waiting for the other players to choose their Shields...";

            MatchHud.ShadowLabel(line, text, style, Color.white);
        }

        void DrawRefusal()
        {
            if (string.IsNullOrEmpty(_refusal) || Time.unscaledTime - _refusalShownAt > 3f) return;

            float scale = Screen.height / 1080f;
            Rect line = new Rect(0f, Screen.height - 120f * scale, Screen.width, 40f * scale);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(24f * scale)
            };

            MatchHud.ShadowLabel(line, _refusal, style, new Color(1f, 0.5f, 0.45f));
        }

        /// <summary>Development diagnostics. Never contains a hidden Charge identity.</summary>
        void DrawDiagnostics()
        {
            float scale = Screen.height / 1080f;
            Rect line = new Rect(10f, Screen.height - 28f * scale, Screen.width - 20f, 24f * scale);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(16f * scale)
            };

            MatchHud.ShadowLabel(line, matchController.DescribeDiagnostics(), style, new Color(0.7f, 0.8f, 1f, 0.8f));
        }

        // ------------------------------------------------------------------ helpers

        static PlayingCard[] ToPlayingCards(PublicCard[] cards)
        {
            if (cards == null) return new PlayingCard[0];

            PlayingCard[] result = new PlayingCard[cards.Length];
            for (int i = 0; i < cards.Length; i++) result[i] = cards[i].ToPlayingCard();

            return result;
        }

        /// <summary>
        /// Gives every other seat a collider so the crosshair can aim at them. The local
        /// body gets none: it can never be the target of its own attack, and self Shield
        /// targeting uses the Shield card.
        /// </summary>
        void AddTargetColliders()
        {
            if (tableLayout == null) return;

            int localSeat = LocalSeat;

            foreach (PlayerCharacter character in tableLayout.Players)
            {
                if (character.PlayerIndex == localSeat || character.GetComponent<Collider>() != null) continue;

                Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                Transform root = character.transform;
                Bounds local = new Bounds(
                    root.InverseTransformPoint(renderers[0].transform.TransformPoint(renderers[0].localBounds.center)),
                    Vector3.zero);

                foreach (Renderer r in renderers)
                {
                    Bounds b = r.localBounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = b.center + Vector3.Scale(b.extents, new Vector3(
                            (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                        local.Encapsulate(root.InverseTransformPoint(r.transform.TransformPoint(point)));
                    }
                }

                BoxCollider box = character.gameObject.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = local.size;
            }
        }

        static bool KeyPressed(int number)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                switch (number)
                {
                    case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                    case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                    case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            switch (number)
            {
                case 1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case 2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case 3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            }
#endif
            return false;
        }
    }
}
