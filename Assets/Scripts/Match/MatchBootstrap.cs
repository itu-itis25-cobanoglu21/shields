using System.Collections.Generic;
using UnityEngine;
using Shields.Players;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// Starts and stops a prototype match. This is the only place that decides which
    /// seat the local human owns and which seats are bots, so no gameplay or camera code
    /// has to assume player 0 or assume a single human.
    ///
    /// Offline prototype rules: exactly one seat is the local human, every other seat is
    /// a bot placeholder with no AI. A future networking layer would replace this class
    /// without touching placement, characters or the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchBootstrap : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] TableLayoutController tableLayout;
        [SerializeField] GameplayCamera gameplayCamera;

        [Header("Match Setup")]
        [Tooltip("Total seats for the match, humans and bots together.")]
        [Range(SeatLayout.MinPlayers, SeatLayout.MaxPlayers)]
        [SerializeField] int targetPlayerCount = 6;

        [Tooltip("Which seat the local human owns in this offline prototype.")]
        [SerializeField] int localPlayerIndex = 0;

        [Tooltip("Start a match immediately instead of waiting for the setup UI.")]
        [SerializeField] bool autoStartOnPlay = false;

        public int TargetPlayerCount
        {
            get { return targetPlayerCount; }
            set { targetPlayerCount = SeatLayout.ClampPlayerCount(value); }
        }

        public int LocalPlayerIndex { get { return localPlayerIndex; } }
        public bool MatchRunning { get; private set; }

        public TableLayoutController TableLayout { get { return tableLayout; } }

        public IReadOnlyList<PlayerCharacter> Players
        {
            get { return tableLayout != null ? tableLayout.Players : new List<PlayerCharacter>(); }
        }

        public PlayerCharacter LocalPlayer { get; private set; }

        void Start()
        {
            if (autoStartOnPlay) StartMatch(targetPlayerCount);
        }

        /// <summary>Builds the seating for the given count and hands the local seat its camera.</summary>
        public void StartMatch(int playerCount)
        {
            if (tableLayout == null)
            {
                Debug.LogError("MatchBootstrap has no TableLayoutController assigned.", this);
                return;
            }

            targetPlayerCount = SeatLayout.ClampPlayerCount(playerCount);
            localPlayerIndex = Mathf.Clamp(localPlayerIndex, 0, targetPlayerCount - 1);

            // Park the camera first. It rides under a player's head, so rebuilding the
            // layout while it is still attached would destroy it with the outgoing seat.
            if (gameplayCamera != null) gameplayCamera.Detach();

            tableLayout.BuildLayout(targetPlayerCount);
            AssignRoles();
            AttachLocalCamera();

            MatchRunning = true;
        }

        /// <summary>Tears the match down and parks the camera so a new count can be chosen.</summary>
        public void EndMatch()
        {
            if (gameplayCamera != null) gameplayCamera.Detach();

            LocalPlayer = null;

            if (tableLayout != null) tableLayout.ClearLayout();

            MatchRunning = false;
        }

        public void RestartMatch()
        {
            EndMatch();
            StartMatch(targetPlayerCount);
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

        /// <summary>Rests the local view on the deck, derived from geometry rather than a fixed angle.</summary>
        void RestLookOnTable(PlayerCharacter player, SeatedHeadLook look)
        {
            if (player.CameraAnchor == null || player.HeadPivot == null) return;

            look.restPitch = SeatedHeadLook.NeutralPitchToward(
                player.HeadPivot, player.CameraAnchor.position, tableLayout.DeckAimPoint);

            look.ResetLook();
        }

        void AttachLocalCamera()
        {
            if (gameplayCamera == null) return;

            if (LocalPlayer != null && LocalPlayer.CameraAnchor != null)
                gameplayCamera.AttachTo(LocalPlayer.CameraAnchor);
            else
                gameplayCamera.Detach();
        }
    }
}
