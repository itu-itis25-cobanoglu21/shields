using System.Collections.Generic;
using UnityEngine;
using Shields.Cards;
using Shields.Game;
using Shields.Players;
using Shields.Presentation;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// Starts and stops a prototype match. This is the only place that decides which
    /// seat the local human owns and which seats are bots, so no gameplay or camera code
    /// has to assume player 0 or assume a single human.
    ///
    /// It also runs initial setup: build and shuffle the deck, deal three cards to every
    /// seat, let bots choose their Shield, and let the local player aim at theirs. The
    /// authoritative state lives in the plain C# <see cref="MatchSetup"/>; the table only
    /// shows it. Once every seat has chosen, the same setup becomes one authoritative
    /// <see cref="ShieldsMatch"/> and <see cref="MatchTurnController"/> runs the turns.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchBootstrap : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] TableLayoutController tableLayout;
        [SerializeField] GameplayCamera gameplayCamera;
        [SerializeField] CrosshairInteraction crosshair;
        [SerializeField] LocalShieldSelection shieldSelection;
        [SerializeField] DeckView deckView;

        [Tooltip("Runs turn play once setup completes. Found or added on this object when empty.")]
        [SerializeField] MatchTurnController turnController;

        [Header("Card Presentation")]
        [SerializeField] CardView cardViewPrefab;

        [Header("Match Setup")]
        [Tooltip("Total seats for the match, humans and bots together.")]
        [Range(MatchConfig.MinPlayers, MatchConfig.MaxPlayers)]
        [SerializeField] int targetPlayerCount = 6;

        [Tooltip("How many complete 52 card decks the match is played with.")]
        [Range(CardDeck.MinDeckCount, CardDeck.MaxDeckCount)]
        [SerializeField] int deckCount = 1;

        [Tooltip("Which seat the local human owns in this offline prototype.")]
        [SerializeField] int localPlayerIndex = 0;

        [Tooltip("Start a match immediately instead of waiting for the setup UI.")]
        [SerializeField] bool autoStartOnPlay = false;

        [Header("Determinism")]
        [Tooltip("Use a fixed seed so the shuffle and bot choices repeat. Debugging only.")]
        [SerializeField] bool useFixedSeed = false;

        [SerializeField] int fixedSeed = 12345;

        [Header("First Person View")]
        [Tooltip("Where the local view rests: 0 aims at the deck, 1 at the player's own cards.")]
        [Range(0f, 1f)]
        [SerializeField] float restAimTowardOwnCards = 0.08f;

        public int TargetPlayerCount
        {
            get { return targetPlayerCount; }
            set { targetPlayerCount = Mathf.Clamp(value, MatchConfig.MinPlayers, MatchConfig.MaxPlayers); }
        }

        public int DeckCount
        {
            get { return deckCount; }
            set { deckCount = Mathf.Clamp(value, CardDeck.MinDeckCount, CardDeck.MaxDeckCount); }
        }

        public int LocalPlayerIndex { get { return localPlayerIndex; } }
        public bool MatchRunning { get; private set; }

        public TableLayoutController TableLayout { get { return tableLayout; } }

        /// <summary>Authoritative setup state for the running match, or null.</summary>
        public MatchSetup Setup { get; private set; }

        /// <summary>The authoritative turn engine once setup has completed, or null.</summary>
        public ShieldsMatch Match { get { return turnController != null ? turnController.Match : null; } }

        /// <summary>True once every seat has two Health cards, one Shield and numeric Health.</summary>
        public bool InitialSetupComplete { get { return Setup != null && Setup.InitialSetupComplete; } }

        /// <summary>True while the local player still has to click one of their three cards.</summary>
        public bool AwaitingLocalShieldChoice
        {
            get { return shieldSelection != null && shieldSelection.IsActive; }
        }

        public IReadOnlyList<PlayerCharacter> Players
        {
            get { return tableLayout != null ? tableLayout.Players : new List<PlayerCharacter>(); }
        }

        public PlayerCharacter LocalPlayer { get; private set; }

        void Awake()
        {
            if (crosshair == null && gameplayCamera != null)
                crosshair = gameplayCamera.GetComponent<CrosshairInteraction>();

            if (turnController == null) turnController = GetComponent<MatchTurnController>();
            if (turnController == null) turnController = gameObject.AddComponent<MatchTurnController>();

            // No crosshair until a seat owns the camera.
            if (crosshair != null) crosshair.enabled = false;
        }

        void Start()
        {
            if (autoStartOnPlay) StartMatch(targetPlayerCount, deckCount);
        }

        // ------------------------------------------------------------------ match

        /// <summary>Starts a match with the currently configured deck count.</summary>
        public void StartMatch(int playerCount)
        {
            StartMatch(playerCount, deckCount);
        }

        /// <summary>Seats the players, deals the opening three cards and begins setup.</summary>
        public void StartMatch(int playerCount, int decks)
        {
            if (tableLayout == null)
            {
                Debug.LogError("MatchBootstrap has no TableLayoutController assigned.", this);
                return;
            }

            TargetPlayerCount = playerCount;
            DeckCount = decks;
            localPlayerIndex = Mathf.Clamp(localPlayerIndex, 0, targetPlayerCount - 1);

            // Park the camera first. It rides under a player's head, so rebuilding the
            // layout while it is still attached would destroy it with the outgoing seat.
            if (turnController != null) turnController.End();
            if (gameplayCamera != null) gameplayCamera.Detach();
            if (shieldSelection != null) shieldSelection.End();

            tableLayout.BuildLayout(targetPlayerCount);
            AssignRoles();
            AttachLocalCamera();

            MatchRunning = true;

            BeginInitialSetup();
        }

        /// <summary>Tears the match down and parks the camera so a new count can be chosen.</summary>
        public void EndMatch()
        {
            if (turnController != null) turnController.End();
            if (shieldSelection != null) shieldSelection.End();
            if (gameplayCamera != null) gameplayCamera.Detach();
            if (crosshair != null) crosshair.enabled = false;
            if (deckView != null) deckView.Clear();

            Setup = null;
            LocalPlayer = null;

            if (tableLayout != null) tableLayout.ClearLayout();

            MatchRunning = false;
        }

        public void RestartMatch()
        {
            EndMatch();
            StartMatch(targetPlayerCount, deckCount);
        }

        // ------------------------------------------------------------------ setup

        /// <summary>
        /// Creates the authoritative setup state, deals, resolves every bot, and hands
        /// the local player their three cards to choose from.
        /// </summary>
        void BeginInitialSetup()
        {
            int? seed = useFixedSeed ? fixedSeed : (int?)null;
            Setup = new MatchSetup(new MatchConfig(targetPlayerCount, deckCount, localPlayerIndex, seed));

            // Bots choose blindly and immediately; only the human needs input.
            Setup.RunBotSelections();

            PresentAllSeats();

            if (deckView != null) deckView.Bind(Setup.Deck);

            BeginLocalShieldChoice();
        }

        void PresentAllSeats()
        {
            for (int i = 0; i < Setup.Players.Count; i++) PresentSeat(Setup.Players[i]);
        }

        /// <summary>
        /// Draws one seat's cards. Before a choice all three sit portrait in the slots;
        /// afterwards the Health cards take the vertical slots and the Shield turns
        /// sideways in the front slot.
        /// </summary>
        void PresentSeat(PlayerSetupState player)
        {
            PlayerTableArea area = tableLayout.GetAreaForPlayer(player.PlayerIndex);
            if (area == null || cardViewPrefab == null) return;

            area.ShowDealtCards(player.DealtCards, cardViewPrefab);

            if (player.IsSelectionComplete)
                area.ArrangeRoles(player.HealthCards, player.ShieldCard);
        }

        void BeginLocalShieldChoice()
        {
            PlayerSetupState local = Setup.LocalPlayer;
            if (local.IsSelectionComplete)
            {
                ReportSetupState();
                return;
            }

            // Only this seat's own dealt cards are offered, so no other card can be picked.
            PlayerTableArea area = tableLayout.GetAreaForPlayer(local.PlayerIndex);

            bool started = shieldSelection != null && area != null &&
                shieldSelection.Begin(crosshair, area.CardViews, OnLocalShieldChosen);

            if (!started)
            {
                // Nothing to click with, so fall back to the first dealt card rather than
                // leaving setup stuck forever.
                Debug.LogWarning("Shield selection could not start; defaulting the local Shield.", this);
                OnLocalShieldChosen(local.DealtCards[0]);
            }
        }

        void OnLocalShieldChosen(PlayingCard card)
        {
            PlayerSetupState local = Setup.LocalPlayer;
            if (local.IsSelectionComplete) return;

            // The authoritative state validates the choice, not the view that reported it.
            Setup.SelectShieldFor(local.PlayerIndex, card);

            PlayerTableArea area = tableLayout.GetAreaForPlayer(local.PlayerIndex);
            if (area != null) area.ArrangeRoles(local.HealthCards, local.ShieldCard);

            if (deckView != null) deckView.Refresh();

            ReportSetupState();
        }

        void ReportSetupState()
        {
            if (!InitialSetupComplete) return;

            Debug.Log("InitialSetupComplete\n" + Setup.DescribeSetup(), this);

            StartTurnPlay();
        }

        /// <summary>
        /// Hands the finished setup to the turn engine. Same deck, same cards: nothing is
        /// redealt or recreated.
        /// </summary>
        void StartTurnPlay()
        {
            if (turnController == null || turnController.IsRunning) return;

            ShieldsMatch match = new ShieldsMatch(Setup);
            Debug.Log(string.Format("Turn play begins: player {0} starts, {1}.",
                match.StartingPlayerIndex, match.Direction), this);

            turnController.Begin(match, localPlayerIndex, tableLayout, crosshair, cardViewPrefab, deckView,
                useFixedSeed ? fixedSeed : (int?)null);
        }

        // ------------------------------------------------------------------ roles

        /// <summary>
        /// Splits the spawned seats into one local human and the rest as bots. Control
        /// and identity are assigned here; the characters themselves stay agnostic.
        /// </summary>
        void AssignRoles()
        {
            IReadOnlyList<PlayerCharacter> players = tableLayout.Players;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerCharacter player = players[i];
                bool isLocal = player.PlayerIndex == localPlayerIndex;

                player.AssignIdentity(player.PlayerIndex, isLocal, !isLocal);

                SeatedHeadLook look = player.GetComponent<SeatedHeadLook>();
                if (look != null)
                {
                    look.Bind(player.HeadPivot);
                    // Only the local seat reads input. Bots keep the component but idle.
                    look.enabled = isLocal;

                    if (isLocal) RestLookOnTable(player, look);
                }

                BotController bot = player.GetComponent<BotController>();
                if (bot != null) bot.enabled = !isLocal;

                if (isLocal) LocalPlayer = player;
            }
        }

        /// <summary>
        /// Rests the local view between the deck and the player's own cards, so both are
        /// in frame. Derived from geometry rather than a fixed angle.
        /// </summary>
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

        void AttachLocalCamera()
        {
            if (gameplayCamera == null) return;

            bool attached = LocalPlayer != null && LocalPlayer.CameraAnchor != null;

            if (attached) gameplayCamera.AttachTo(LocalPlayer.CameraAnchor);
            else gameplayCamera.Detach();

            if (crosshair != null) crosshair.enabled = attached;
        }
    }
}
