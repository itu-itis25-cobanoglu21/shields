using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Shields.Network;
using Shields.Online;

namespace Shields.Match
{
    /// <summary>
    /// The small post-match rematch control under the winner banner.
    ///
    /// Offline the single human decides: REMATCH starts the next match at once through
    /// <see cref="MatchBootstrap.RestartMatch"/>. Online it only sends a vote; the host
    /// counts connected humans, shows the same tally to everyone and starts the rematch
    /// when all of them agree. Bots never vote.
    ///
    /// R toggles the vote so nobody has to free the cursor; the button works too whenever
    /// the cursor is free. Presentation only: it never decides anything.
    /// </summary>
    [DisallowMultipleComponent]
    public class RematchUI : MonoBehaviour
    {
        [SerializeField] MatchBootstrap offlineBootstrap;
        [SerializeField] NetworkMatchController onlineMatch;

        GUIStyle _titleStyle;
        GUIStyle _lineStyle;
        GUIStyle _buttonStyle;

        bool Online { get { return onlineMatch != null && onlineMatch.IsSpawned; } }

        /// <summary>True while a finished match is on screen and this machine may ask for a rematch.</summary>
        public bool Visible
        {
            get
            {
                if (Online) return onlineMatch.LocalCanVoteRematch;

                return offlineBootstrap != null && offlineBootstrap.MatchRunning &&
                    offlineBootstrap.Match != null && offlineBootstrap.Match.IsMatchOver;
            }
        }

        bool LocalVoted { get { return Online && onlineMatch.LocalVotedRematch; } }

        void Update()
        {
            if (!Visible || !RematchKeyPressed()) return;

            Toggle();
        }

        void Toggle()
        {
            if (Online) onlineMatch.RequestRematchVote(!onlineMatch.LocalVotedRematch);
            else offlineBootstrap.RestartMatch();
        }

        void OnGUI()
        {
            if (!Visible) return;

            EnsureStyles();
            float scale = Screen.height / 1080f;
            _titleStyle.fontSize = Mathf.RoundToInt(30f * scale);
            _lineStyle.fontSize = Mathf.RoundToInt(22f * scale);
            _buttonStyle.fontSize = Mathf.RoundToInt(22f * scale);

            // Under the existing winner banner (drawn at 35% of the screen height).
            float width = 360f * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.35f + 110f * scale, width, 150f * scale);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            Rect title = new Rect(panel.x, panel.y + 8f * scale, panel.width, 40f * scale);
            Rect line = new Rect(panel.x, title.yMax, panel.width, 32f * scale);
            Rect button = new Rect(panel.x + 40f * scale, line.yMax + 8f * scale, panel.width - 80f * scale, 44f * scale);

            if (Online)
            {
                bool voted = LocalVoted;
                MatchHud.ShadowLabel(title, voted ? "REMATCH - YOU ARE READY" : "REMATCH?", _titleStyle,
                    voted ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.85f, 0.2f));
                MatchHud.ShadowLabel(line, string.Format("WAITING {0} / {1} PLAYERS", onlineMatch.RematchYesCount,
                    Mathf.Max(1, onlineMatch.RematchRequired)), _lineStyle, Color.white);

                if (GUI.Button(button, voted ? "CANCEL REMATCH (R)" : "REMATCH (R)", _buttonStyle)) Toggle();
            }
            else
            {
                MatchHud.ShadowLabel(title, "REMATCH?", _titleStyle, new Color(1f, 0.85f, 0.2f));
                MatchHud.ShadowLabel(line, "New seats, new deck, same table", _lineStyle, Color.white);

                if (GUI.Button(button, "REMATCH (R)", _buttonStyle)) Toggle();
            }
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _lineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }

        static bool RematchKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) return keyboard.rKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }
    }
}
