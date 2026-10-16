using UnityEngine;
using Shields.Cards;
using Shields.Game;
using Shields.Match;
using Shields.Menu;
using Shields.Online;
using Shields.Session;

namespace Shields.Network
{
    /// <summary>
    /// Scaffolding UI for the connection slice: Offline, Host Online, Join Online.
    ///
    /// IMGUI on purpose, exactly like the existing offline setup panel, so it adds no
    /// canvas, no event system and no prefabs, and so it is obvious this is throwaway
    /// prototype UI rather than the real menu. No session browser, no matchmaking: a join
    /// code is the whole story.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPrototypeUI : MonoBehaviour
    {
        enum Screen
        {
            ChooseMode,
            Offline,
            Host,
            Join
        }

        [SerializeField] ShieldsNetworkBootstrap networkBootstrap;

        [Tooltip("The existing offline panel. Enabled only while Offline mode is chosen.")]
        [SerializeField] MatchSetupUI offlineSetupUI;

        [SerializeField] MatchBootstrap offlineBootstrap;

        Screen _screen = Screen.ChooseMode;

        int _targetPlayerCount = 6;
        int _deckCount = 1;
        string _joinCodeInput = string.Empty;
        bool _busy;

        ShieldsSessionController Session
        {
            get { return networkBootstrap != null ? networkBootstrap.SessionController : null; }
        }

        NetworkSeatRegistry Seats
        {
            get { return networkBootstrap != null ? networkBootstrap.SeatRegistry : null; }
        }

        void Awake()
        {
            ApplyOfflineUIVisibility();
        }

        /// <summary>
        /// Arriving from the main menu skips the mode chooser: OFFLINE MODE opens the
        /// offline setup panel exactly as the chooser's OFFLINE button does, CREATE ROOM
        /// opens the host setup, JOIN ROOM fills in the code and starts joining straight away.
        /// </summary>
        void Start()
        {
            MainMenuIntent intent;
            string joinCode;
            if (!MainMenuLaunchRequest.TryConsume(out intent, out joinCode)) return;

            if (intent == MainMenuIntent.Offline)
            {
                SetScreen(Screen.Offline);
            }
            else if (intent == MainMenuIntent.CreateRoom)
            {
                SetScreen(Screen.Host);
            }
            else if (intent == MainMenuIntent.JoinRoom)
            {
                SetScreen(Screen.Join);
                _joinCodeInput = joinCode;
                JoinSession();
            }
        }

        void ApplyOfflineUIVisibility()
        {
            if (offlineSetupUI != null) offlineSetupUI.enabled = _screen == Screen.Offline;
        }

        void SetScreen(Screen screen)
        {
            _screen = screen;
            ApplyOfflineUIVisibility();
        }

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            switch (_screen)
            {
                case Screen.ChooseMode:
                    DrawModeChooser();
                    break;

                case Screen.Offline:
                    DrawOfflineBanner();
                    break;

                case Screen.Host:
                    DrawHostPanel();
                    break;

                case Screen.Join:
                    DrawJoinPanel();
                    break;
            }
        }

        static Rect CenteredPanel(float width, float height)
        {
            return new Rect((UnityEngine.Screen.width - width) * 0.5f,
                (UnityEngine.Screen.height - height) * 0.5f, width, height);
        }

        void DrawModeChooser()
        {
            Rect panel = CenteredPanel(420f, 230f);

            GUI.Box(panel, "SHIELDS Prototype");
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 32f, panel.width - 32f, panel.height - 46f));

            GUILayout.Label("Choose how to play.");
            GUILayout.Space(6f);

            if (GUILayout.Button("OFFLINE  (one human plus bots)", GUILayout.Height(38f)))
                SetScreen(Screen.Offline);

            GUILayout.Space(4f);

            if (GUILayout.Button("HOST ONLINE", GUILayout.Height(38f)))
                SetScreen(Screen.Host);

            GUILayout.Space(4f);

            if (GUILayout.Button("JOIN ONLINE", GUILayout.Height(38f)))
                SetScreen(Screen.Join);

            GUILayout.EndArea();
        }

        void DrawOfflineBanner()
        {
            // The existing offline panel draws itself; this only offers a way back.
            bool matchRunning = offlineBootstrap != null && offlineBootstrap.MatchRunning;
            if (matchRunning) return;

            Rect back = new Rect(12f, UnityEngine.Screen.height - 46f, 180f, 30f);
            if (GUI.Button(back, "Back to mode select")) SetScreen(Screen.ChooseMode);
        }

        // ------------------------------------------------------------------ host

        void DrawHostPanel()
        {
            bool inSession = Session != null && Session.InSession;

            // Once the table is live the lobby must not sit on top of it.
            if (inSession && GameplayOnScreen()) { DrawCompactSessionBar(true); return; }

            Rect panel = CenteredPanel(460f, inSession ? 380f : 300f);
            GUI.Box(panel, inSession ? "Hosting" : "Host Online");
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 32f, panel.width - 32f, panel.height - 46f));

            if (!inSession) DrawHostSetup();
            else DrawHostSession();

            DrawStatusLine();

            GUILayout.EndArea();
        }

        void DrawHostSetup()
        {
            GUILayout.Label("Players at the table");
            GUILayout.BeginHorizontal();
            for (int count = MatchConfig.MinPlayers; count <= MatchConfig.MaxPlayers; count++)
            {
                bool selected = _targetPlayerCount == count;
                if (GUILayout.Button(selected ? "[" + count + "]" : count.ToString(), GUILayout.Height(26f)))
                    _targetPlayerCount = count;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Decks in play");
            GUILayout.BeginHorizontal();
            for (int decks = CardDeck.MinDeckCount; decks <= CardDeck.MaxDeckCount; decks++)
            {
                bool selected = _deckCount == decks;
                string label = string.Format("{0}{1} deck{2}{3}", selected ? "[" : "", decks,
                    decks == 1 ? "" : "s", selected ? "]" : "");

                if (GUILayout.Button(label, GUILayout.Height(26f))) _deckCount = decks;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            GUI.enabled = !_busy;
            if (GUILayout.Button(_busy ? "Creating session..." : "CREATE SESSION", GUILayout.Height(34f)))
                CreateSession();
            GUI.enabled = true;

            GUILayout.Space(4f);
            if (GUILayout.Button("Back", GUILayout.Height(24f))) SetScreen(Screen.ChooseMode);
        }

        void DrawHostSession()
        {
            GUILayout.Label("JOIN CODE");

            GUIStyle codeStyle = new GUIStyle(GUI.skin.textField);
            codeStyle.fontSize = 24;
            codeStyle.alignment = TextAnchor.MiddleCenter;

            // Selectable so the code can be copied out of the editor.
            GUILayout.TextField(Session.JoinCode, codeStyle, GUILayout.Height(38f));

            GUILayout.Space(6f);
            DrawSeatSummary();

            GUILayout.Space(8f);

            bool ready = networkBootstrap.SeatRegistryReady;
            bool testStarted = ready && Seats.TestStarted;
            NetworkMatchController match = networkBootstrap.MatchController;
            bool matchRunning = match != null && (match.MatchActive || match.Phase == OnlineMatchPhase.MatchOver);

            if (!matchRunning)
            {
                string blocker = match != null
                    ? match.DescribeStartBlocker(_targetPlayerCount, _deckCount)
                    : "Match controller missing.";

                GUI.enabled = blocker.Length == 0;
                if (GUILayout.Button("START MATCH", GUILayout.Height(34f))) StartMatch();
                GUI.enabled = true;

                if (blocker.Length > 0)
                {
                    GUIStyle warn = new GUIStyle(GUI.skin.label) { wordWrap = true };
                    warn.normal.textColor = new Color(1f, 0.8f, 0.45f);
                    GUILayout.Label(blocker, warn);
                }

                GUILayout.Space(4f);

                GUI.enabled = ready && !testStarted;
                if (GUILayout.Button("Start connection test only", GUILayout.Height(24f)))
                    networkBootstrap.StartNetworkTest();
                GUI.enabled = true;

                if (testStarted && GUILayout.Button("Stop connection test", GUILayout.Height(22f)))
                    networkBootstrap.StopNetworkTest();
            }
            else
            {
                GUILayout.Label("Match phase: " + match.Phase);

                if (GUILayout.Button("End match", GUILayout.Height(26f))) EndMatch();
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("End session", GUILayout.Height(24f))) LeaveSession();
        }

        void CreateSession()
        {
            _busy = true;
            RunHost();
        }

        async void RunHost()
        {
            await networkBootstrap.HostAsync(_targetPlayerCount, _deckCount);
            _busy = false;
        }

        // ------------------------------------------------------------------ join

        void DrawJoinPanel()
        {
            bool inSession = Session != null && Session.InSession;

            if (inSession && GameplayOnScreen()) { DrawCompactSessionBar(false); return; }

            Rect panel = CenteredPanel(460f, inSession ? 250f : 220f);
            GUI.Box(panel, inSession ? "Joined" : "Join Online");
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 32f, panel.width - 32f, panel.height - 46f));

            if (!inSession) DrawJoinSetup();
            else DrawJoinedSession();

            DrawStatusLine();

            GUILayout.EndArea();
        }

        void DrawJoinSetup()
        {
            GUILayout.Label("Join code from the host");

            GUIStyle codeStyle = new GUIStyle(GUI.skin.textField);
            codeStyle.fontSize = 20;
            codeStyle.alignment = TextAnchor.MiddleCenter;

            _joinCodeInput = GUILayout.TextField(_joinCodeInput, 16, codeStyle, GUILayout.Height(34f));

            GUILayout.Space(10f);

            GUI.enabled = !_busy;
            if (GUILayout.Button(_busy ? "Joining..." : "JOIN", GUILayout.Height(34f)))
                JoinSession();
            GUI.enabled = true;

            GUILayout.Space(4f);
            if (GUILayout.Button("Back", GUILayout.Height(24f))) SetScreen(Screen.ChooseMode);
        }

        void DrawJoinedSession()
        {
            DrawSeatSummary();

            GUILayout.Space(6f);

            NetworkMatchController match = networkBootstrap.MatchController;
            bool testStarted = networkBootstrap.SeatRegistryReady && Seats.TestStarted;

            if (match != null && match.Phase != OnlineMatchPhase.SessionLobby)
                GUILayout.Label("Match phase: " + match.Phase);
            else if (testStarted)
                GUILayout.Label("Connection test running.");
            else
                GUILayout.Label("Waiting for the host to start.");

            GUILayout.Space(6f);
            if (GUILayout.Button("Leave session", GUILayout.Height(24f))) LeaveSession();
        }

        void StartMatch()
        {
            RunStartMatch();
        }

        async void RunStartMatch()
        {
            await networkBootstrap.StartOnlineMatchAsync(_targetPlayerCount, _deckCount);
        }

        void EndMatch()
        {
            RunEndMatch();
        }

        async void RunEndMatch()
        {
            await networkBootstrap.EndOnlineMatchAsync();
        }

        void JoinSession()
        {
            _busy = true;
            RunJoin();
        }

        async void RunJoin()
        {
            await networkBootstrap.JoinAsync(_joinCodeInput);
            _busy = false;
        }

        // ------------------------------------------------------------------ shared

        void DrawSeatSummary()
        {
            if (Session == null) return;

            GUILayout.Label(string.Format("Connected humans: {0} of {1}",
                Session.ConnectedHumanCount, Session.MaxPlayers));

            if (!networkBootstrap.SeatRegistryReady)
            {
                GUILayout.Label("Assigned seat: waiting for the host...");
                return;
            }

            int seat = networkBootstrap.LocalSeatIndex;
            GUILayout.Label(seat == NetworkSeatRegistry.NoSeat
                ? "Assigned seat: none (table is full)"
                : string.Format("Assigned seat: {0} of {1}", seat, Seats.TargetPlayerCount));

            GUILayout.Label(DescribeSeats());
        }

        string DescribeSeats()
        {
            NetworkSeatRegistry seats = Seats;
            if (seats == null) return string.Empty;

            int local = seats.LocalSeatIndex;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Seats: ");

            for (int i = 0; i < seats.SeatCount; i++)
            {
                if (i > 0) sb.Append(' ');

                SeatOccupant occupant = seats.OccupantAt(i);

                if (!occupant.IsHuman) sb.Append('.');
                else if (i == local) sb.Append('@');
                else sb.Append('H');
            }

            sb.Append("   ( @ you, H other human, . empty )");
            return sb.ToString();
        }

        void DrawStatusLine()
        {
            if (Session == null) return;

            GUILayout.Space(6f);
            GUILayout.Label("Status: " + Session.Phase);

            if (string.IsNullOrEmpty(Session.ErrorMessage)) return;

            GUIStyle errorStyle = new GUIStyle(GUI.skin.label);
            errorStyle.normal.textColor = new Color(1f, 0.45f, 0.4f);
            errorStyle.wordWrap = true;

            GUILayout.Label(Session.ErrorMessage, errorStyle);
        }

        /// <summary>True once the match owns the screen, so the lobby steps aside.</summary>
        bool GameplayOnScreen()
        {
            NetworkMatchController match = networkBootstrap != null ? networkBootstrap.MatchController : null;
            if (match == null) return false;

            return match.Phase == OnlineMatchPhase.WaitingForShieldSelections
                || match.Phase == OnlineMatchPhase.Playing
                || match.Phase == OnlineMatchPhase.MatchOver;
        }

        /// <summary>A small corner bar so the session can still be left during a match.</summary>
        void DrawCompactSessionBar(bool isHost)
        {
            NetworkMatchController match = networkBootstrap.MatchController;

            Rect bar = new Rect(12f, 12f, 320f, isHost ? 104f : 80f);
            GUI.Box(bar, "Session");
            GUILayout.BeginArea(new Rect(bar.x + 10f, bar.y + 24f, bar.width - 20f, bar.height - 34f));

            GUILayout.Label("Seat " + networkBootstrap.LocalSeatIndex + "   |   " + match.Phase);
            if (isHost) GUILayout.Label("Code " + Session.JoinCode + (Session.IsLocked ? "  (locked)" : ""));

            GUILayout.BeginHorizontal();
            if (isHost && GUILayout.Button("End match", GUILayout.Height(22f))) EndMatch();
            if (GUILayout.Button("Leave", GUILayout.Height(22f))) LeaveSession();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void LeaveSession()
        {
            RunLeave();
        }

        async void RunLeave()
        {
            await networkBootstrap.LeaveAsync();
            SetScreen(Screen.ChooseMode);
        }
    }
}
