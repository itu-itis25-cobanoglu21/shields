using Shields.Cards;
using Shields.Game;
using UnityEngine;

namespace Shields.Menu
{
    /// <summary>What the player chose on the main menu before GameScene loaded.</summary>
    public enum MainMenuIntent
    {
        None = 0,
        CreateRoom,
        JoinRoom,
        /// <summary>One human plus bots, no services.</summary>
        Offline
    }

    /// <summary>
    /// One-shot hand-off from the main menu to GameScene.
    ///
    /// All networking (NetworkManager, the session controller, the lobby) lives in
    /// GameScene, so the menu does not connect by itself. It records what the player asked
    /// for (and the table setup for Offline / Create Room), loads GameScene, and the lobby
    /// there consumes the request once and starts the match, hosts or joins.
    /// </summary>
    public static class MainMenuLaunchRequest
    {
        public const string MainMenuSceneName = "MainMenuScene";
        public const string GameSceneName = "GameScene";

        static MainMenuIntent _intent;
        static string _joinCode = string.Empty;
        static int _players = 6;
        static int _decks = 1;

        public static void Request(MainMenuIntent intent, string joinCode, int players = 6, int decks = 1)
        {
            _intent = intent;
            _joinCode = joinCode ?? string.Empty;
            _players = Mathf.Clamp(players, MatchConfig.MinPlayers, MatchConfig.MaxPlayers);
            _decks = Mathf.Clamp(decks, CardDeck.MinDeckCount, CardDeck.MaxDeckCount);
        }

        /// <summary>Returns the pending request once and clears it.</summary>
        public static bool TryConsume(out MainMenuIntent intent, out string joinCode, out int players, out int decks)
        {
            intent = _intent;
            joinCode = _joinCode;
            players = _players;
            decks = _decks;

            Clear();
            return intent != MainMenuIntent.None;
        }

        // Statics survive Play Mode restarts when domain reload is off, so start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        {
            _intent = MainMenuIntent.None;
            _joinCode = string.Empty;
            _players = 6;
            _decks = 1;
        }
    }
}
