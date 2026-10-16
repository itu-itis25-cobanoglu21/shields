using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shields.Menu
{
    /// <summary>
    /// The main menu buttons. It starts no networking itself: CREATE ROOM and JOIN ROOM
    /// record a <see cref="MainMenuLaunchRequest"/> and load GameScene, whose existing
    /// lobby runs the actual Sessions + Relay host or join. EXIT quits the player, or stops
    /// Play Mode in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Main Buttons")]
        [SerializeField] Button joinRoomButton;
        [SerializeField] Button createRoomButton;
        [SerializeField] Button customizeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Tooltip("Dimmed and locked while the join panel is open.")]
        [SerializeField] CanvasGroup mainButtonsGroup;

        [Range(0f, 1f)]
        [SerializeField] float dimmedAlpha = 0.25f;

        [Header("Join Room Panel")]
        [SerializeField] GameObject joinRoomPanel;
        [SerializeField] TMP_InputField joinCodeInput;
        [SerializeField] Button joinConfirmButton;
        [SerializeField] Button joinBackButton;
        [SerializeField] TMP_Text joinMessage;

        [Tooltip("Longest code the field accepts. Session join codes are short.")]
        [SerializeField] int maxCodeLength = 16;

        bool _loading;

        void Awake()
        {
            // Visible but not available yet.
            if (customizeButton != null) customizeButton.interactable = false;
            if (settingsButton != null) settingsButton.interactable = false;

            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OpenJoinPanel);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(CreateRoom);
            if (exitButton != null) exitButton.onClick.AddListener(Exit);
            if (joinConfirmButton != null) joinConfirmButton.onClick.AddListener(ConfirmJoin);
            if (joinBackButton != null) joinBackButton.onClick.AddListener(CloseJoinPanel);

            if (joinCodeInput != null)
            {
                joinCodeInput.characterLimit = maxCodeLength;
                joinCodeInput.onSubmit.AddListener(_ => ConfirmJoin());
                joinCodeInput.onValueChanged.AddListener(_ => SetJoinMessage(string.Empty));
            }

            SetJoinPanelOpen(false);
        }

        // ------------------------------------------------------------------ create

        void CreateRoom()
        {
            if (_loading) return;

            MainMenuLaunchRequest.Request(MainMenuIntent.CreateRoom, string.Empty);
            LoadGameScene();
        }

        // ------------------------------------------------------------------ join

        void OpenJoinPanel()
        {
            if (_loading) return;

            SetJoinPanelOpen(true);
        }

        void CloseJoinPanel()
        {
            if (_loading) return;

            SetJoinPanelOpen(false);
        }

        void ConfirmJoin()
        {
            if (_loading || joinCodeInput == null) return;

            // Codes are case-insensitive letters and digits; spaces from copy-paste go.
            string code = joinCodeInput.text.Replace(" ", string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                SetJoinMessage("ENTER A ROOM CODE");
                return;
            }

            MainMenuLaunchRequest.Request(MainMenuIntent.JoinRoom, code);
            LoadGameScene();
        }

        void SetJoinPanelOpen(bool open)
        {
            if (joinRoomPanel != null) joinRoomPanel.SetActive(open);
            SetJoinMessage(string.Empty);

            if (mainButtonsGroup != null)
            {
                mainButtonsGroup.alpha = open ? dimmedAlpha : 1f;
                mainButtonsGroup.interactable = !open;
                mainButtonsGroup.blocksRaycasts = !open;
            }

            if (!open || joinCodeInput == null) return;

            joinCodeInput.text = string.Empty;
            joinCodeInput.Select();
            joinCodeInput.ActivateInputField();
        }

        void SetJoinMessage(string message)
        {
            if (joinMessage != null) joinMessage.text = message;
        }

        // ------------------------------------------------------------------ exit

        void Exit()
        {
            if (_loading) return;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ loading

        void LoadGameScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(MainMenuLaunchRequest.GameSceneName))
            {
                MainMenuLaunchRequest.TryConsume(out _, out _);
                Debug.LogError("GameScene is not in the Build Profile scene list, so the menu cannot open it.", this);
                SetJoinMessage("COULD NOT OPEN THE GAME");
                return;
            }

            _loading = true;
            SetInteractable(false);
            SceneManager.LoadSceneAsync(MainMenuLaunchRequest.GameSceneName);
        }

        void SetInteractable(bool interactable)
        {
            foreach (Button button in new[] { joinRoomButton, createRoomButton, exitButton, joinConfirmButton, joinBackButton })
                if (button != null) button.interactable = interactable;

            if (joinCodeInput != null) joinCodeInput.interactable = interactable;
        }
    }
}
