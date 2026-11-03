using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Shields.Match;
using Shields.Players;
using Shields.Social;

namespace Shields.Tavern
{
    /// <summary>
    /// The small lower left beer order panel: "ORDER! (O)", recipient toggles, the derived
    /// beer count, and a one line receipt.
    ///
    /// Opening it borrows the mouse: the cursor is freed and the local head look is
    /// suppressed (never disabled, so head pose replication keeps running). Nothing else
    /// stops: the match, the bots and the network carry on. Closing gives the exact
    /// previous cursor state back, except when a targeting action is what closed it, in
    /// which case the cursor is locked because the crosshair needs it.
    ///
    /// Cursor policy: the panel refuses to open while the local player is choosing an
    /// Attack, Replace Shield or Charged Attack target, and closes itself the moment
    /// such targeting begins. Beer UI and the crosshair never own the mouse at once.
    ///
    /// IMGUI like the rest of the prototype HUD, so it adds no canvas or event system.
    /// Presentation only: it reads the roster from <see cref="BeerOrderCoordinator"/>
    /// and only ever sends intent through it.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeerOrderUI : MonoBehaviour
    {
        [SerializeField] BeerOrderCoordinator coordinator;

        [Tooltip("How long an accepted/refused line stays visible.")]
        [SerializeField] float feedbackSeconds = 3f;

        [Tooltip("How long to wait for the host's answer before saying so.")]
        [SerializeField] float replyTimeoutSeconds = 5f;

        static readonly Color AccentColor = new Color(1f, 0.82f, 0.45f);
        static readonly Color AcceptColor = new Color(0.55f, 1f, 0.6f);
        static readonly Color RefuseColor = new Color(1f, 0.5f, 0.45f);

        readonly List<BeerRecipient> _recipients = new List<BeerRecipient>();
        readonly HashSet<int> _selected = new HashSet<int>();
        readonly List<int> _submitBuffer = new List<int>();

        bool _open;
        SeatedHeadLook _look;
        CursorLockMode _previousLockState;
        bool _previousCursorVisible;
        Vector2 _scroll;

        /// <summary>What kind of line the feedback is, so a replacement HUD can colour it its own way.</summary>
        public enum FeedbackTone { Info, Accepted, Refused }

        string _feedback = string.Empty;
        FeedbackTone _feedbackTone;
        float _feedbackShownAt = -100f;
        float _awaitingReplySince = -1f;

        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        GUIStyle _toggleStyle;
        GUIStyle _buttonStyle;

        public bool IsOpen { get { return _open; } }

        // ------------------------------------------------------------------ replacement HUD access
        // A replacement HUD (HudOverride.BeerPanel) draws the panel itself and drives it
        // through these. Ordering, cursor and validation rules stay here.

        /// <summary>Who can be ordered for, refreshed every frame while the panel is open.</summary>
        public IReadOnlyList<BeerRecipient> Recipients { get { return _recipients; } }

        public bool IsSelected(int seat) { return _selected.Contains(seat); }

        public void SetSelected(int seat, bool on)
        {
            if (on) _selected.Add(seat);
            else _selected.Remove(seat);
        }

        public int SelectedCount { get { return _selected.Count; } }

        public float CooldownRemaining { get { return coordinator != null ? coordinator.LocalCooldownRemaining : 0f; } }

        public bool AwaitingReply { get { return _awaitingReplySince >= 0f; } }

        /// <summary>Same as pressing O.</summary>
        public void RequestOpen()
        {
            if (!_open && coordinator != null && coordinator.OrderingAvailable) TryOpen();
        }

        public void Order() { if (_open) SubmitOrder(); }

        public void Cancel() { Close(false); }

        /// <summary>The current feedback line and its 0..1 visibility; false when nothing is shown.</summary>
        public bool TryGetFeedback(out string text, out FeedbackTone tone, out float alpha)
        {
            text = _feedback;
            tone = _feedbackTone;
            float age = Time.unscaledTime - _feedbackShownAt;
            bool waiting = _awaitingReplySince >= 0f;
            alpha = waiting ? 1f : Mathf.Clamp01((feedbackSeconds - age) / 0.5f);
            return !string.IsNullOrEmpty(_feedback) && (age < feedbackSeconds || waiting);
        }

        // ------------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            if (coordinator != null) coordinator.ReceiptReceived += OnReceipt;
        }

        void OnDisable()
        {
            if (coordinator != null) coordinator.ReceiptReceived -= OnReceipt;

            Close(false);
        }

        void Update()
        {
            if (coordinator == null) return;

            bool available = coordinator.OrderingAvailable;

            if (_open)
            {
                if (!available)
                {
                    // Match over, torn down, or the local player is out.
                    Close(false);
                    _selected.Clear();
                }
                else if (coordinator.LocalTargetingActive)
                {
                    // An action needs the crosshair now; give it a locked cursor.
                    Close(true);
                }
                else if (TogglePressed() || EscapePressed())
                {
                    Close(false);
                }
                else
                {
                    KeepCursorFree();
                    RefreshRecipients();
                }

                return;
            }

            if (!available)
            {
                _selected.Clear();
                _awaitingReplySince = -1f;
                return;
            }

            if (_awaitingReplySince >= 0f && Time.unscaledTime - _awaitingReplySince > replyTimeoutSeconds)
            {
                _awaitingReplySince = -1f;
                ShowFeedback("NO ANSWER FROM HOST", FeedbackTone.Refused);
            }

            if (TogglePressed()) TryOpen();
        }

        void TryOpen()
        {
            if (coordinator.LocalTargetingActive) ShowFeedback("CANNOT ORDER WHILE CHOOSING A TARGET", FeedbackTone.Refused);
            else Open();
        }

        // ------------------------------------------------------------------ open / close

        void Open()
        {
            _look = coordinator.LocalHeadLook();
            _previousLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            if (_look != null) _look.SetInputSuppressed(true);

            _open = true;
            KeepCursorFree();
            RefreshRecipients();
        }

        /// <summary>
        /// Closes the panel and hands the mouse back. <paramref name="lockForGameplay"/>
        /// locks the cursor for the crosshair; otherwise the state from before opening
        /// returns. If the head look went away meanwhile (match torn down), the cursor is
        /// left free for whatever menu is now on screen.
        /// </summary>
        void Close(bool lockForGameplay)
        {
            if (!_open) return;

            _open = false;

            bool lookStillOwnsMouse = _look != null && _look.isActiveAndEnabled;
            if (_look != null) _look.SetInputSuppressed(false);
            _look = null;

            if (!lookStillOwnsMouse) return;

            if (lockForGameplay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = _previousLockState;
                Cursor.visible = _previousCursorVisible;
            }
        }

        static void KeepCursorFree()
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }

        void RefreshRecipients()
        {
            coordinator.GetRecipients(_recipients);

            // Anyone who died or left since being ticked silently drops out.
            _selected.RemoveWhere(seat => !_recipients.Exists(r => r.Seat == seat));
        }

        // ------------------------------------------------------------------ submit

        void SubmitOrder()
        {
            _submitBuffer.Clear();
            foreach (BeerRecipient recipient in _recipients)
                if (_selected.Contains(recipient.Seat)) _submitBuffer.Add(recipient.Seat);

            _submitBuffer.Sort();

            if (_submitBuffer.Count == 0)
            {
                ShowFeedback(BeerOrderQueue.DescribeRejection(BeerOrderRejection.NoRecipients), FeedbackTone.Refused);
                return;
            }

            // Shown until the receipt replaces it; offline that happens inside Submit.
            ShowFeedback("ORDER SENT...", FeedbackTone.Info);
            _awaitingReplySince = Time.unscaledTime;

            if (!coordinator.Submit(_submitBuffer))
            {
                _awaitingReplySince = -1f;
                ShowFeedback(BeerOrderQueue.DescribeRejection(BeerOrderRejection.NotAvailable), FeedbackTone.Refused);
                return;
            }

            Close(false);
        }

        void OnReceipt(BeerOrderReceipt receipt)
        {
            _awaitingReplySince = -1f;

            if (receipt.Accepted)
            {
                _selected.Clear();

                string beers = receipt.BeerCount + (receipt.BeerCount == 1 ? " BEER" : " BEERS");
                ShowFeedback(receipt.OrdersAhead == 0
                    ? "ORDER ACCEPTED — " + beers
                    : "ORDER QUEUED — " + receipt.OrdersAhead + " AHEAD", FeedbackTone.Accepted);
                return;
            }

            string reason = BeerOrderQueue.DescribeRejection(receipt.Rejection);
            if (receipt.Rejection == BeerOrderRejection.Cooldown)
                reason += " — " + Mathf.CeilToInt(receipt.CooldownRemaining) + "s";

            ShowFeedback(reason, FeedbackTone.Refused);
        }

        void ShowFeedback(string text, FeedbackTone tone)
        {
            _feedback = text;
            _feedbackTone = tone;
            _feedbackShownAt = Time.unscaledTime;
        }

        static Color ToneColor(FeedbackTone tone)
        {
            switch (tone)
            {
                case FeedbackTone.Accepted: return AcceptColor;
                case FeedbackTone.Refused: return RefuseColor;
                default: return AccentColor;
            }
        }

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            if (coordinator == null || !coordinator.OrderingAvailable) return;
            // The replacement HUD draws the panel and the feedback line itself.
            if (HudOverride.Active && HudOverride.BeerPanel) return;

            EnsureStyles();

            float scale = Screen.height / 1080f;
            _titleStyle.fontSize = Mathf.RoundToInt(20f * scale);
            _labelStyle.fontSize = Mathf.RoundToInt(18f * scale);
            _toggleStyle.fontSize = Mathf.RoundToInt(18f * scale);
            _buttonStyle.fontSize = Mathf.RoundToInt(18f * scale);

            float margin = 16f * scale;

            // Sits above the online diagnostics line in the bottom left corner.
            float bottom = Screen.height - 40f * scale;
            // A replacement HUD draws its own entry prompt; the open panel is still drawn here.
            if (!_open && HudOverride.Active) return;

            float top = _open ? DrawPanel(margin, bottom, scale) : DrawEntryButton(margin, bottom, scale);

            DrawFeedbackLine(margin, top, scale);
        }

        float DrawEntryButton(float left, float bottom, float scale)
        {
            Rect rect = new Rect(left, bottom - 30f * scale, 150f * scale, 30f * scale);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;

            MatchHud.ShadowLabel(rect, "ORDER! (O)", _titleStyle, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.9f));
            return rect.y;
        }

        /// <summary>Draws the open panel upwards from <paramref name="bottom"/>; returns its top.</summary>
        float DrawPanel(float left, float bottom, float scale)
        {
            RefreshRecipients();

            float width = 240f * scale;
            float pad = 10f * scale;
            float rowHeight = 28f * scale;
            float headerHeight = 34f * scale;
            float footerHeight = 30f * scale + 38f * scale + pad;

            // The list scrolls rather than ever reaching the turn banner at the top.
            float maxListHeight = Mathf.Max(rowHeight * 3f, Screen.height * 0.6f - headerHeight - footerHeight);
            float contentHeight = rowHeight * _recipients.Count;
            float listHeight = Mathf.Min(contentHeight, maxListHeight);

            float height = headerHeight + listHeight + footerHeight + pad;
            Rect panel = new Rect(left, bottom - height, width, height);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            Rect title = new Rect(panel.x + pad, panel.y + pad * 0.5f, width - 2f * pad, headerHeight);
            MatchHud.ShadowLabel(title, "BEER ORDER", _titleStyle, AccentColor);

            Rect listView = new Rect(panel.x + pad, title.yMax, width - 2f * pad, listHeight);
            Rect listContent = new Rect(0f, 0f, listView.width - (contentHeight > listHeight ? 18f : 0f), contentHeight);

            _scroll = GUI.BeginScrollView(listView, _scroll, listContent);
            for (int i = 0; i < _recipients.Count; i++)
            {
                BeerRecipient recipient = _recipients[i];
                bool was = _selected.Contains(recipient.Seat);
                bool now = GUI.Toggle(new Rect(0f, i * rowHeight, listContent.width, rowHeight), was,
                    " " + recipient.Label, _toggleStyle);

                if (now && !was) _selected.Add(recipient.Seat);
                else if (!now && was) _selected.Remove(recipient.Seat);
            }
            GUI.EndScrollView();

            int beers = _selected.Count;
            Rect count = new Rect(panel.x + pad, listView.yMax + pad * 0.5f, width - 2f * pad, 30f * scale);
            MatchHud.ShadowLabel(count, "BEERS: " + beers, _labelStyle, Color.white);

            float buttonWidth = (width - 3f * pad) * 0.5f;
            Rect orderButton = new Rect(panel.x + pad, count.yMax, buttonWidth, 32f * scale);
            Rect cancelButton = new Rect(orderButton.xMax + pad, count.yMax, buttonWidth, 32f * scale);

            float cooldown = coordinator.LocalCooldownRemaining;
            bool waiting = _awaitingReplySince >= 0f;

            GUI.enabled = cooldown <= 0f && !waiting;
            string orderText = cooldown > 0f ? "WAIT " + Mathf.CeilToInt(cooldown) + "s" : "ORDER";
            if (GUI.Button(orderButton, orderText, _buttonStyle)) SubmitOrder();
            GUI.enabled = true;

            if (GUI.Button(cancelButton, "CANCEL", _buttonStyle)) Close(false);

            return panel.y;
        }

        void DrawFeedbackLine(float left, float top, float scale)
        {
            if (!TryGetFeedback(out string text, out FeedbackTone tone, out float alpha)) return;

            Rect line = new Rect(left, top - 30f * scale, 520f * scale, 28f * scale);
            Color color = ToneColor(tone);

            MatchHud.ShadowLabel(line, text, _labelStyle, new Color(color.r, color.g, color.b, alpha));
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            _toggleStyle = new GUIStyle(GUI.skin.toggle) { alignment = TextAnchor.MiddleLeft };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }

        // ------------------------------------------------------------------ input

        static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) return keyboard.oKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.O);
#else
            return false;
#endif
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) return keyboard.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
