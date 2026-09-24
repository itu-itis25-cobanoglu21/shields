using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Shields.Bots;
using Shields.Game;
using Shields.Online;
using Shields.Players;
using Shields.Presentation;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// Drives turn play for an offline match. The local human picks an action with 1/2/3
    /// and a target with the crosshair; bots ask <see cref="RandomBotPolicy"/>. Both only
    /// build a <see cref="TurnActionRequest"/> and submit it to the same
    /// <see cref="ShieldsMatch.Submit"/>; every outcome comes back from the engine and is
    /// handed to <see cref="MatchPresenter"/>.
    ///
    /// Pacing lives here, never in the engine: bots wait a short think time, and every
    /// result stays readable for a moment before the next bot acts.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchTurnController : MonoBehaviour
    {
        [Header("Bot Pacing")]
        [Tooltip("Pause after any resolved action before the next bot starts thinking.")]
        [SerializeField] float postActionDelay = 2.5f;

        [Tooltip("How long a bot visibly thinks on its own turn before acting.")]
        [SerializeField] float botThinkDelay = 2.5f;

        [Header("Feedback")]
        [Tooltip("How long world-space action text and revealed cards stay visible.")]
        [SerializeField] float feedbackSeconds = 1.8f;

        [Tooltip("How long the latest action stays in the screen-space HUD panel.")]
        [SerializeField] float hudFeedbackSeconds = 3f;

        enum PendingAction { None, Attack, ReplaceShield, ChargedAttack }

        ShieldsMatch _match;
        MatchPresenter _presenter;
        RandomBotPolicy _botPolicy;
        TableLayoutController _table;
        CrosshairInteraction _crosshair;
        int _localPlayerIndex;

        PendingAction _pending;
        int _hoveredTarget = TurnActionRequest.NoTarget;

        // Every accepted action bumps this; it identifies the turn a bot action belongs
        // to, so a delayed bot action can never fire for a later turn.
        int _resolvedCount;
        uint _snapshotRevision;
        readonly BotTurnGate _botGate = new BotTurnGate();
        float _feedbackUntil;

        // The HUD and the table are both driven from public state, exactly as online.
        MatchHud _hud;
        PublicMatchSnapshot _lastSnapshot;

        public ShieldsMatch Match { get { return _match; } }

        public bool IsRunning { get { return _match != null; } }

        /// <summary>Takes over a freshly started match and draws the table from it.</summary>
        public void Begin(ShieldsMatch match, int localPlayerIndex, TableLayoutController table,
            CrosshairInteraction crosshair, CardView cardViewPrefab, DeckView deckView, int? seed)
        {
            End();

            _match = match;
            _localPlayerIndex = localPlayerIndex;
            _table = table;
            _crosshair = crosshair;

            // Its own stream, separate from the shuffle, bot Shield choice and turn order.
            _botPolicy = new RandomBotPolicy(seed.HasValue ? new System.Random(seed.Value ^ 0x2B07) : new System.Random());
            _presenter = new MatchPresenter(table, cardViewPrefab, deckView, localPlayerIndex, feedbackSeconds);
            _hud = new MatchHud(hudFeedbackSeconds);

            _pending = PendingAction.None;
            _resolvedCount = 0;
            _snapshotRevision = 0;
            _botGate.Cancel();
            _feedbackUntil = 0f;
            _hud.ShowFeedback(
                MatchHud.PlayerName(match.StartingPlayerIndex, localPlayerIndex, false) + " STARTS",
                match.Direction == TurnDirection.Clockwise ? "Turns go clockwise" : "Turns go counter-clockwise");

            AddTargetColliders();
            SyncPresenter();
        }

        /// <summary>Stops turn play and removes every match visual this controller made.</summary>
        public void End()
        {
            if (_presenter != null) _presenter.Clear();

            _match = null;
            _presenter = null;
            _botPolicy = null;
            _botGate.Cancel();
            _pending = PendingAction.None;
            _hoveredTarget = TurnActionRequest.NoTarget;
        }

        void OnDisable()
        {
            End();
        }

        void Update()
        {
            if (_match == null) return;

            if (_match.IsMatchOver)
            {
                _botGate.Cancel();
                ClearPending();
                return;
            }

            // Control comes from runtime match state: the local seat index and IsBot.
            // The local human's turn has no timer and waits for input indefinitely.
            if (BotTurnGate.ControlOf(_match, _localPlayerIndex) == SeatControl.LocalHuman)
            {
                _botGate.Cancel();
                UpdateLocalTurn();
            }
            else
            {
                UpdateBotTurn();
            }
        }

        // ------------------------------------------------------------------ local human

        /// <summary>True while it is the living local player's turn and input may act.</summary>
        public bool LocalInputAllowed
        {
            get
            {
                return _match != null && !_match.IsMatchOver && _match.CurrentPlayerIndex == _localPlayerIndex &&
                    _match.Players[_localPlayerIndex].IsAlive;
            }
        }

        /// <summary>The valid target currently under the crosshair, or NoTarget.</summary>
        public int HoveredTarget { get { return _hoveredTarget; } }

        void UpdateLocalTurn()
        {
            if (!LocalInputAllowed) return;

            for (int key = 1; key <= 3; key++)
                if (KeyPressed(key)) { PressActionKey(key); break; }

            if (_pending == PendingAction.None || _match == null || _match.CurrentPlayerIndex != _localPlayerIndex) return;

            int target = TargetUnderCrosshair();
            _hoveredTarget = IsSelectableTarget(target) ? target : TurnActionRequest.NoTarget;
            _presenter.SetTargetHighlight(_hoveredTarget);

            if (_hoveredTarget != TurnActionRequest.NoTarget && _crosshair != null && _crosshair.PrimaryPressedThisFrame())
                ConfirmTarget(_hoveredTarget);
        }

        /// <summary>
        /// The 1/2/3 keys route here. 1 and 2 enter targeting; 3 commits a first Charge at
        /// once or enters Charged Attack targeting. Switching modes never draws a card.
        /// Returns false when it is not the local player's turn.
        /// </summary>
        public bool PressActionKey(int key)
        {
            if (!LocalInputAllowed) return false;

            switch (key)
            {
                case 1: _pending = PendingAction.Attack; return true;
                case 2: _pending = PendingAction.ReplaceShield; return true;
                case 3:
                    // A first Charge needs no target, so it is committed immediately.
                    if (_match.Players[_localPlayerIndex].HasCharge)
                    {
                        _pending = PendingAction.ChargedAttack;
                        return true;
                    }

                    return Submit(TurnActionRequest.Charge(_localPlayerIndex));
                default:
                    return false;
            }
        }

        /// <summary>
        /// The crosshair click routes here with the aimed player. Submits the pending
        /// action only for a selectable target; the turn is consumed only if the engine
        /// accepts it.
        /// </summary>
        public bool ConfirmTarget(int target)
        {
            if (!LocalInputAllowed || _pending == PendingAction.None || !IsSelectableTarget(target)) return false;

            switch (_pending)
            {
                case PendingAction.Attack: return Submit(TurnActionRequest.Attack(_localPlayerIndex, target));
                case PendingAction.ReplaceShield: return Submit(TurnActionRequest.ReplaceShield(_localPlayerIndex, target));
                default: return Submit(TurnActionRequest.Charge(_localPlayerIndex, target));
            }
        }

        /// <summary>
        /// The player the crosshair names: another player's body, or any player's Shield
        /// card. The Shield card is how the local player aims at themselves.
        /// </summary>
        public int TargetUnderCrosshair()
        {
            RaycastHit hit;
            if (_crosshair == null || !_crosshair.TryGetHit(out hit)) return TurnActionRequest.NoTarget;

            PlayerCharacter character = hit.collider.GetComponentInParent<PlayerCharacter>();
            if (character != null) return character.PlayerIndex;

            CardView card = hit.collider.GetComponentInParent<CardView>();
            if (card != null && _table != null)
            {
                PlayerTableArea area = _table.GetAreaForPlayer(card.OwnerPlayerIndex);
                if (area != null && area.ShieldView == card) return card.OwnerPlayerIndex;
            }

            return TurnActionRequest.NoTarget;
        }

        /// <summary>
        /// Highlight filter only, from public state. The engine still validates every
        /// request and is the only authority on legality.
        /// </summary>
        bool IsSelectableTarget(int target)
        {
            if (target < 0 || target >= _match.PlayerCount || !_match.Players[target].IsAlive) return false;

            return _pending == PendingAction.ReplaceShield || target != _localPlayerIndex;
        }

        void ClearPending()
        {
            _pending = PendingAction.None;
            _hoveredTarget = TurnActionRequest.NoTarget;
            if (_presenter != null) _presenter.SetTargetHighlight(TurnActionRequest.NoTarget);
        }

        // ------------------------------------------------------------------ bots

        /// <summary>
        /// Schedules at most one action for the current bot and submits it when due. The
        /// gate re-verifies right before submitting that the same bot still holds the same
        /// turn; otherwise nothing happens. A rejected submit simply reschedules.
        /// </summary>
        void UpdateBotTurn()
        {
            double dueAt = Mathf.Max(Time.time, _feedbackUntil) + botThinkDelay;

            int botIndex;
            if (!_botGate.TryTakeDueBot(_match, _localPlayerIndex, _resolvedCount, Time.time, dueAt, out botIndex))
                return;

            Submit(_botPolicy.ChooseAction(_match.Players, botIndex));
        }

        // ------------------------------------------------------------------ submit

        bool Submit(TurnActionRequest request)
        {
            TurnActionResult result = _match.Submit(request);

            if (!result.Accepted)
            {
                Debug.LogWarning("Rejected " + result, this);
                return false;
            }

            _resolvedCount++;
            ClearPending();

            // Offline projects its own engine into the same public state the host sends
            // online, so one presentation path and one HUD serve both modes.
            PublicActionResult publicResult = PublicStateBuilder.BuildActionResult(result);

            SyncPresenter();
            _presenter.ShowResult(publicResult);
            _feedbackUntil = Time.time + postActionDelay;
            _hud.ShowResult(publicResult, _localPlayerIndex);

            Debug.Log(result.ToString(), this);
            if (result.MatchOver) Debug.Log("Match over. Winner: player " + result.WinnerIndex, this);

            return true;
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>Projects the local engine into a public snapshot and draws it.</summary>
        void SyncPresenter()
        {
            if (_presenter == null || _match == null) return;

            _snapshotRevision++;
            _lastSnapshot = PublicStateBuilder.BuildMatchSnapshot(_match, _snapshotRevision,
                _match.IsMatchOver ? OnlineMatchPhase.MatchOver : OnlineMatchPhase.Playing);

            _presenter.Sync(_lastSnapshot);
        }

        // ------------------------------------------------------------------ setup helpers

        /// <summary>
        /// Gives every other player's body a collider so the crosshair can aim at them.
        /// The prefab's primitives carry none. The local body gets none: it cannot be a
        /// target of its own attacks, and self Shield targeting uses the Shield card.
        /// </summary>
        void AddTargetColliders()
        {
            if (_table == null) return;

            foreach (PlayerCharacter character in _table.Players)
            {
                if (character.PlayerIndex == _localPlayerIndex || character.GetComponent<Collider>() != null) continue;

                Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                // Built from each renderer's own local bounds, not world AABBs, so seats
                // rotated around the table get a tight box that does not cover a neighbour.
                Transform root = character.transform;
                Bounds local = new Bounds(root.InverseTransformPoint(renderers[0].transform.TransformPoint(renderers[0].localBounds.center)), Vector3.zero);

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

        // ------------------------------------------------------------------ input

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

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (_match == null || _hud == null) return;

            _hud.Draw(_lastSnapshot, _localPlayerIndex, PendingForHud(), _hoveredTarget);
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
    }
}
