using UnityEngine;

namespace Shields.Menu
{
    /// <summary>What the player chose on the main menu before GameScene loaded.</summary>
    public enum MainMenuIntent
    {
        None = 0,
        CreateRoom,
        JoinRoom
    }

    /// <summary>
    /// One-shot hand-off from the main menu to GameScene.
    ///
    /// All networking (NetworkManager, the session controller, the lobby) lives in
    /// GameScene, so the menu does not connect by itself. It records what the player asked
    /// for, loads GameScene, and the existing lobby there consumes the request once and
    /// runs its own create or join flow.
    /// </summary>
    public static class MainMenuLaunchRequest
    {
        public const string MainMenuSceneName = "MainMenuScene";
        public const string GameSceneName = "GameScene";

        static MainMenuIntent _intent;
        static string _joinCode = string.Empty;

        public static void Request(MainMenuIntent intent, string joinCode)
        {
            _intent = intent;
            _joinCode = joinCode ?? string.Empty;
        }

        /// <summary>Returns the pending request once and clears it.</summary>
        public static bool TryConsume(out MainMenuIntent intent, out string joinCode)
        {
            intent = _intent;
            joinCode = _joinCode;

            Clear();
            return intent != MainMenuIntent.None;
        }

        // Statics survive Play Mode restarts when domain reload is off, so start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        {
            _intent = MainMenuIntent.None;
            _joinCode = string.Empty;
        }
    }
}
