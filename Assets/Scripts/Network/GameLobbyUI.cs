using System.Collections;
using System.Collections.Generic;
using Shields.Match;
using Shields.Menu;
using Shields.Online;
using Shields.Players;
using Shields.Session;
using Shields.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shields.Network
{
    /// <summary>
    /// GameScene's side of the main menu. Consumes the <see cref="MainMenuLaunchRequest"/>
    /// once and acts on it: OFFLINE starts the offline match with the chosen table, CREATE
    /// ROOM hosts a session with it, JOIN ROOM joins with the code.
    ///
    /// While a room is open but its match is not on screen it shows the room lobby (the
    /// room code, who sits where, START MATCH for the host). During a match it stays out of
    /// the way; freeing the cursor (Esc) shows a small bar to end the match or leave. Every
    /// way out returns to the main menu. Drawn in the main menu's own style.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameLobbyUI : MonoBehaviour
    {
        enum Mode { None, Offline, Host, Join }

        [SerializeField] ShieldsNetworkBootstrap networkBootstrap;

        [Tooltip("In-match hints for the offline table. Enabled only for an offline match.")]
        [SerializeField] MatchSetupUI offlineSetupUI;

        [SerializeField] MatchBootstrap offlineBootstrap;

        [Header("Type")]
        [SerializeField] TMP_FontAsset bowlby;
        [SerializeField] TMP_FontAsset knewave;

        static readonly Color Bone = TavernUI.Bone, Dim = TavernUI.Dim, Muted = TavernUI.Muted, Red = TavernUI.Red;

        Mode _mode;
        int _players = 6, _decks = 1;
        string _joinCode = string.Empty;
        bool _busy;          // creating or joining
        bool _wasInSession;  // to notice the session ending under us
        bool _leaving;
        string _failure = string.Empty;

        TavernUI _ui;
        Canvas _canvas;
        RectTransform _lobby, _bar;
        Image _fade;
        float _fadeAlpha = 1f;

        readonly List<SeatedHeadLook> _suppressed = new List<SeatedHeadLook>();

        ShieldsSessionController Session { get { return networkBootstrap != null ? networkBootstrap.SessionController : null; } }
        NetworkSeatRegistry Seats { get { return networkBootstrap != null ? networkBootstrap.SeatRegistry : null; } }
        NetworkMatchController MatchController { get { return networkBootstrap != null ? networkBootstrap.MatchController : null; } }

        bool InSession { get { return Session != null && Session.InSession; } }
        bool IsHost { get { return _mode == Mode.Host; } }

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (offlineSetupUI != null) offlineSetupUI.enabled = false;
        }

        void Start()
        {
            if (!MainMenuLaunchRequest.TryConsume(out MainMenuIntent intent, out _joinCode, out _players, out _decks))
            {
                // GameScene always starts from the main menu.
                Debug.Log("GameScene opened without a main menu request; returning to the main menu.", this);
                SceneManager.LoadScene(MainMenuLaunchRequest.MainMenuSceneName);
                return;
            }

            TavernUI.EnsureEventSystem();
            BuildUI();

            switch (intent)
            {
                case MainMenuIntent.Offline:
                    _mode = Mode.Offline;
                    if (offlineSetupUI != null) offlineSetupUI.enabled = true;
                    if (offlineBootstrap != null) offlineBootstrap.StartMatch(_players, _decks);
                    break;

                case MainMenuIntent.CreateRoom:
                    _mode = Mode.Host;
                    RunHost();
                    break;

                case MainMenuIntent.JoinRoom:
                    _mode = Mode.Join;
                    RunJoin();
                    break;
            }
        }

        async void RunHost()
        {
            _busy = true;
            bool ok = networkBootstrap != null && await networkBootstrap.HostAsync(_players, _decks);
            _busy = false;
            if (!ok) Fail("COULD NOT OPEN THE ROOM");
        }

        async void RunJoin()
        {
            _busy = true;
            bool ok = networkBootstrap != null && await networkBootstrap.JoinAsync(_joinCode);
            _busy = false;
            if (!ok) Fail("COULD NOT JOIN " + _joinCode);
        }

        /// <summary>Shows the failure screen: a headline and the session's own reason, if any.</summary>
        void Fail(string headline)
        {
            string reason = Session != null ? Session.ErrorMessage : string.Empty;
            _failure = headline;
            _failureReason = string.IsNullOrEmpty(reason) ? "CHECK YOUR CONNECTION AND TRY AGAIN." : reason.ToUpperInvariant();
        }

        string _failureReason = string.Empty;

        // ------------------------------------------------------------------ flow

        /// <summary>True once the match owns the screen, so the lobby steps aside.</summary>
        bool GameplayOnScreen()
        {
            if (_mode == Mode.Offline) return offlineBootstrap != null && offlineBootstrap.MatchRunning;

            NetworkMatchController match = MatchController;
            if (match == null) return false;
            return match.Phase == OnlineMatchPhase.WaitingForShieldSelections
                || match.Phase == OnlineMatchPhase.Playing
                || match.Phase == OnlineMatchPhase.MatchOver;
        }

        bool LobbyVisible
        {
            get
            {
                if (_mode == Mode.None || _mode == Mode.Offline || _leaving) return false;
                if (_failure.Length > 0 || _busy || !InSession) return true;
                return !GameplayOnScreen();
            }
        }

        bool BarVisible
        {
            get
            {
                if (_mode == Mode.None || _leaving || LobbyVisible) return false;
                return GameplayOnScreen() && Cursor.lockState != CursorLockMode.Locked;
            }
        }

        void Update()
        {
            if (_canvas == null) return;

            // The session went away under us: the host ended it, or we were removed.
            if (_wasInSession && !InSession && !_leaving && _failure.Length == 0)
                Fail("THE ROOM WAS CLOSED");
            _wasInSession = InSession;

            bool lobby = LobbyVisible, bar = BarVisible;
            UiKit.SetActive(_lobby, lobby);
            UiKit.SetActive(_bar, bar);

            // The lobby owns the mouse; the bar borrows it while the pointer is over it, so
            // clicking it does not re-lock the cursor.
            bool ownMouse = lobby || (bar && PointerOver(_bar));
            if (lobby && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            SuppressLook(ownMouse);

            if (!_leaving) _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.unscaledDeltaTime / 0.4f);
            _fade.color = new Color(0, 0, 0, _fadeAlpha);
            _fade.raycastTarget = _fadeAlpha > 0.5f;

            _ui.Refresh();
        }

        static bool PointerOver(RectTransform rect)
        {
            Mouse mouse = Mouse.current;
            return mouse != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue(), null);
        }

        void SuppressLook(bool suppress)
        {
            if (suppress)
            {
                foreach (SeatedHeadLook look in FindObjectsByType<SeatedHeadLook>())
                {
                    if (!look.isActiveAndEnabled || look.InputSuppressed) continue;
                    look.SetInputSuppressed(true);
                    _suppressed.Add(look);
                }
                return;
            }

            if (_suppressed.Count == 0) return;
            foreach (SeatedHeadLook look in _suppressed) if (look != null) look.SetInputSuppressed(false);
            _suppressed.Clear();
        }

        void OnDisable() { SuppressLook(false); }

        // ------------------------------------------------------------------ actions

        void StartMatch()
        {
            if (!IsHost || networkBootstrap == null) return;
            _ = networkBootstrap.StartOnlineMatchAsync(_players, _decks);
        }

        void EndMatch()
        {
            if (!IsHost || networkBootstrap == null) return;
            _ = networkBootstrap.EndOnlineMatchAsync();
        }

        void CopyCode()
        {
            if (Session != null && Session.JoinCode.Length > 0) GUIUtility.systemCopyBuffer = Session.JoinCode;
            _copiedTime = Time.unscaledTime;
        }

        float _copiedTime = -10f;

        /// <summary>Leaves whatever is running and goes back to the main menu.</summary>
        void LeaveToMenu()
        {
            if (_leaving) return;
            _leaving = true;
            SuppressLook(false);
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            for (float t = _fadeAlpha; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                _fadeAlpha = t;
                _fade.color = new Color(0, 0, 0, t);
                yield return null;
            }
            _fade.color = Color.black;

            if (networkBootstrap != null && InSession)
            {
                var leave = networkBootstrap.LeaveAsync();
                while (!leave.IsCompleted) yield return null;
            }

            // NGO keeps the NetworkManager alive across scene loads. GameScene brings its own
            // next time, so drop this one rather than end up with two.
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                if (manager.IsListening) manager.Shutdown();
                Destroy(manager.gameObject);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(MainMenuLaunchRequest.MainMenuSceneName);
        }

        // ================================================================== UI

        void BuildUI()
        {
            _ui = new TavernUI(bowlby, knewave);
            _canvas = TavernUI.MakeCanvas(transform, "LobbyCanvas", 20);
            RectTransform root = (RectTransform)_canvas.transform;

            _lobby = UiKit.Node(root, "Lobby").Fill();
            BuildLobby(_lobby);

            // Bottom-left, clear of the HUD hint at the top-left and the rematch prompt in the middle.
            _bar = UiKit.Node(root, "SessionBar").At(0, 0, 0, 0, 0, 0, 620, 230);
            BuildBar(_bar);

            _fade = UiKit.Img(root, "Fade", UiSprites.White(), Color.black);
            _fade.rectTransform.Fill();
        }

        static float Sp(float spacing) { return TavernUI.Sp(spacing); }
        static float Thk(float h) { return TavernUI.Thk(h); }

        const float X = 140;
        const int MaxSeatRows = 8;

        void BuildLobby(RectTransform scr)
        {
            TavernUI.Shade(scr, "Shade", 1300, new Color(0, 0, 0, 0.9f));
            TavernUI.Shade(scr, "Shade2", 950, new Color(0, 0, 0, 0.5f));

            // ---- Title: the room code once it exists, otherwise what is happening.
            TextMeshProUGUI kicker = _ui.Kicker(scr, "Kicker", "", 16, Red, 10);
            kicker.rectTransform.TL(X + 2, 112, 600, 26);
            TextMeshProUGUI title = _ui.T(scr, "Title", "", 72, Bone, TextAlignmentOptions.TopLeft, true);
            title.rectTransform.TL(X, 140, 900, 200);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.lineSpacing = -20;

            RectTransform copy = _ui.TextLink(scr, "Copy", "COPY", 22, false, CopyCode);
            TextMeshProUGUI copyLabel = copy.Find("Label").GetComponent<TextMeshProUGUI>();

            Image rule = TavernUI.Line(scr, "Rule", Red);
            rule.rectTransform.TL(X + 4, 248, 56, Thk(3));

            // ---- Status and table.
            TextMeshProUGUI status = _ui.T(scr, "Status", "", 18, Dim, TextAlignmentOptions.TopLeft);
            status.rectTransform.TL(X + 2, 272, 820, 60);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.characterSpacing = Sp(5);

            RectTransform table = UiKit.Node(scr, "Table").TL(X, 330, 700, 420);
            _ui.Kicker(table, "TableLabel", "THE TABLE", 16, Red, 10).rectTransform.TL(2, 0, 400, 26);
            TextMeshProUGUI summary = _ui.T(table, "Summary", "", 16, Muted);
            summary.rectTransform.TL(2, 30, 700, 24);
            summary.characterSpacing = Sp(6);
            Image seatRule = TavernUI.VLine(table, "Rule", Red);
            seatRule.rectTransform.TL(4, 66, Thk(3), MaxSeatRows * 40 - 6);

            var seatNames = new TextMeshProUGUI[MaxSeatRows];
            var seatNumbers = new TextMeshProUGUI[MaxSeatRows];
            var seatDashes = new Image[MaxSeatRows];
            for (int i = 0; i < MaxSeatRows; i++)
            {
                float y = 66 + i * 40;
                seatNumbers[i] = _ui.T(table, "SeatNo" + i, "SEAT " + (i + 1), 16, Muted);
                seatNumbers[i].rectTransform.TL(30, y, 110, 36);
                seatNumbers[i].characterSpacing = Sp(6);
                seatNames[i] = _ui.T(table, "Seat" + i, "", 26, Dim);
                seatNames[i].rectTransform.TL(150, y, 500, 36);
                seatDashes[i] = TavernUI.Line(table, "YouDash" + i, Red);
                seatDashes[i].rectTransform.At(0, 1, 1, 0.5f, 142, -(y + 18), 26, Thk(4));
            }

            // ---- Actions.
            RectTransform start = _ui.TextLink(scr, "StartMatch", "START MATCH", 40, true, StartMatch, out UiFx startFx);
            RectTransform leave = _ui.TextLink(scr, "Leave", "LEAVE ROOM", 26, false, LeaveToMenu);
            TextMeshProUGUI leaveLabel = leave.Find("Label").GetComponent<TextMeshProUGUI>();
            Button startButton = start.GetComponent<Button>();

            _ui.Bind(() =>
            {
                bool failed = _failure.Length > 0;
                bool connecting = !failed && (_busy || !InSession);
                NetworkSeatRegistry seats = Seats;
                bool seatsReady = !failed && !connecting && networkBootstrap.SeatRegistryReady;

                // Title block.
                if (failed)
                {
                    kicker.text = IsHost ? "CREATE ROOM" : "JOIN ROOM";
                    title.text = _failure;
                    title.fontSize = 56;
                }
                else if (connecting)
                {
                    kicker.text = IsHost ? "CREATE ROOM" : "JOIN ROOM";
                    title.text = (IsHost ? "OPENING ROOM" : "JOINING ROOM") + new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
                    title.fontSize = 56;
                }
                else
                {
                    kicker.text = "ROOM CODE";
                    title.text = Session.JoinCode;
                    title.fontSize = 72;
                }
                title.characterSpacing = !failed && !connecting ? Sp(30) : 0;

                bool showCopy = !failed && !connecting && Session.JoinCode.Length > 0;
                UiKit.SetActive(copy, showCopy);
                if (showCopy)
                {
                    float w = title.GetPreferredValues(title.text).x;
                    copy.TL(X + w + 30, 166, 140, 40);
                    copyLabel.text = Time.unscaledTime - _copiedTime < 1.6f ? "COPIED" : "COPY";
                }

                // Status line.
                if (failed) status.text = _failureReason;
                else if (connecting) status.text = IsHost ? "SETTING UP THE TABLE FOR " + _players + " PLAYERS" : "CONNECTING TO ROOM " + _joinCode;
                else if (!seatsReady) status.text = "TAKING A SEAT...";
                else if (IsHost)
                {
                    string blocker = MatchController != null ? MatchController.DescribeStartBlocker(_players, _decks) : "THE MATCH IS NOT READY.";
                    status.text = blocker.Length > 0 ? blocker.ToUpperInvariant() : "SHARE THE CODE. START WHENEVER YOU ARE READY.";
                }
                else status.text = "WAITING FOR THE HOST TO START";
                status.color = failed ? Red : Dim;

                // Table.
                UiKit.SetActive(table, seatsReady);
                if (seatsReady)
                {
                    int count = Mathf.Min(seats.SeatCount, MaxSeatRows);
                    seatRule.rectTransform.sizeDelta = new Vector2(Thk(3), Mathf.Max(1, count) * 40 - 6);
                    int humans = seats.HumanCount;
                    summary.text = humans + " OF " + seats.SeatCount + " SEATS TAKEN   ·   EMPTY SEATS GO TO BOTS   ·   "
                                   + seats.DeckCount * MainMenuState.CardsPerDeck + " CARDS";
                    int local = seats.LocalSeatIndex;
                    for (int i = 0; i < MaxSeatRows; i++)
                    {
                        bool shown = i < count;
                        UiKit.SetActive(seatNumbers[i], shown);
                        UiKit.SetActive(seatNames[i], shown);
                        UiKit.SetActive(seatDashes[i], shown && i == local);
                        if (!shown) continue;
                        SeatOccupant occupant = seats.OccupantAt(i);
                        bool you = i == local;
                        seatNames[i].text = you ? "YOU" : occupant.IsHuman ? "PLAYER" : "BOT";
                        seatNames[i].color = you ? Bone : occupant.IsHuman ? Dim : Muted.A(0.8f);
                    }
                }

                // Actions.
                bool canStart = IsHost && seatsReady && MatchController != null && MatchController.DescribeStartBlocker(_players, _decks).Length == 0;
                UiKit.SetActive(start, IsHost && seatsReady);
                startButton.interactable = canStart;
                float actionsY = seatsReady ? 330 + 66 + Mathf.Min(seats.SeatCount, MaxSeatRows) * 40 + 30 : 350;
                start.TL(X - 2, actionsY, 340, 60);
                leaveLabel.text = failed ? "BACK TO MENU" : connecting ? "CANCEL" : "LEAVE ROOM";
                float leaveX = IsHost && seatsReady ? X - 2 + TavernUI.LinkWidth(start) + 72 : X;
                leave.TL(leaveX, actionsY + (IsHost && seatsReady ? 8 : 0), 300, 44);
            });
        }

        /// <summary>The in-match bar: shown only while the cursor is free.</summary>
        void BuildBar(RectTransform bar)
        {
            TavernUI.Shade(bar, "Shade", 620, new Color(0, 0, 0, 0.75f));

            TextMeshProUGUI kicker = _ui.Kicker(bar, "Kicker", "", 15, Red, 10);
            kicker.rectTransform.TL(40, 30, 500, 24);
            TextMeshProUGUI code = _ui.T(bar, "Code", "", 34, Bone, TextAlignmentOptions.MidlineLeft, true);
            code.rectTransform.TL(38, 54, 500, 48);
            code.characterSpacing = Sp(20);

            RectTransform end = _ui.TextLink(bar, "EndMatch", "END MATCH", 26, false, EndMatch);
            RectTransform leave = _ui.TextLink(bar, "Leave", "LEAVE TABLE", 26, false, LeaveToMenu);
            TextMeshProUGUI hint = _ui.T(bar, "Hint", "CLICK THE TABLE TO GO BACK", 13, Muted);
            hint.characterSpacing = Sp(8);

            _ui.Bind(() =>
            {
                bool online = _mode != Mode.Offline && InSession;
                UiKit.SetActive(kicker, online);
                UiKit.SetActive(code, online);
                if (online)
                {
                    kicker.text = Session.IsLocked ? "ROOM CODE  ·  LOCKED" : "ROOM CODE";
                    code.text = Session.JoinCode;
                }
                float y = online ? 116 : 40;
                UiKit.SetActive(end, IsHost && online);
                end.TL(40, y, 220, 44);
                leave.TL(IsHost && online ? 270 : 40, y, 260, 44);
                hint.rectTransform.TL(42, y + 52, 500, 20);
            });
        }
    }
}
