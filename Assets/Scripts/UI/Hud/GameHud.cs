using System.Collections.Generic;
using Shields.Match;
using Shields.Network;
using Shields.Online;
using Shields.Players;
using Shields.Table;
using Shields.Tavern;
using TMPro;
using UnityEngine;

namespace Shields.UI
{
    /// <summary>
    /// The in-match screen HUD, offline and online. While enabled it switches the IMGUI
    /// prototype HUD off through <see cref="HudOverride"/> (the beer order panel too) and
    /// draws <see cref="GameHudView"/> instead.
    ///
    /// Every frame it rebuilds a <see cref="HudModel"/> from public information only: the
    /// snapshot the match HUD was asked to draw, resolved actions as the table reveals
    /// them, setup / beer / rematch state from their own components. Presentation only:
    /// it never decides anything and only sends intent through the beer order panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset bowlby;
        [SerializeField] TMP_FontAsset knewave;

        readonly HudModel _model = new HudModel();
        Canvas _canvas;
        GameHudView _view;

        MatchBootstrap _boot;
        NetworkMatchController _online;
        OnlineMatchDriver _onlineDriver;
        TableLayoutController _table;
        BeerOrderCoordinator _orders;
        BeerOrderUI _orderUI;
        BeerDrinkCoordinator _drink;
        RematchUI _rematch;

        /// <summary>The beer order panel's logic; the view draws the panel and drives it through this.</summary>
        public BeerOrderUI OrderUI { get { return _orderUI; } }

        /// <summary>For hosts that add the HUD at runtime instead of from a scene.</summary>
        public void SetFonts(TMP_FontAsset bowlbyFont, TMP_FontAsset knewaveFont)
        {
            bowlby = bowlbyFont;
            knewave = knewaveFont;
        }

        void OnEnable()
        {
            HudOverride.Active = true;
            HudOverride.BeerPanel = true;
            HudOverride.Result += OnResult;
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            HudOverride.Result -= OnResult;
            HudOverride.Active = false;
            HudOverride.BeerPanel = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void Start()
        {
            _boot = FindAnyObjectByType<MatchBootstrap>();
            _online = FindAnyObjectByType<NetworkMatchController>();
            _onlineDriver = FindAnyObjectByType<OnlineMatchDriver>();
            _table = FindAnyObjectByType<TableLayoutController>();
            _orders = FindAnyObjectByType<BeerOrderCoordinator>();
            _orderUI = FindAnyObjectByType<BeerOrderUI>();
            _drink = FindAnyObjectByType<BeerDrinkCoordinator>();
            _rematch = FindAnyObjectByType<RematchUI>();

            TavernUI.EnsureEventSystem();
            _canvas = TavernUI.MakeCanvas(transform, "GameHudCanvas", 5);
            _view = new GameHudView(this, _canvas, new TavernUI(bowlby, knewave));
        }

        void OnResult(PublicActionResult result, int localSeat)
        {
            _model.AddResult(result, localSeat);
        }

        void Update()
        {
            if (_view == null) return;
            BuildModel();
            _view.Tick(_model);
        }

        // ------------------------------------------------------------------ model

        bool Online { get { return _online != null && _online.IsSpawned; } }

        void BuildModel()
        {
            bool online = Online;
            OnlineMatchPhase phase = online ? _online.Phase : OnlineMatchPhase.SessionLobby;

            if (HudOverride.HasCurrentFrame)
            {
                HudOverride.Frame f = HudOverride.Latest;
                _model.SetMatch(f.Snapshot, f.LocalSeat, f.Pending, f.HoveredTarget);
            }
            else
            {
                // A missed draw frame keeps the table on screen while a match is still running.
                bool offlineRunning = _boot != null && _boot.MatchRunning && _boot.Match != null;
                bool onlineRunning = online && (phase == OnlineMatchPhase.Playing || phase == OnlineMatchPhase.MatchOver);
                if (!(_model.InMatch && (offlineRunning || onlineRunning))) _model.ClearMatch();
            }

            bool offlineSetup = _boot != null && _boot.MatchRunning && _boot.Match == null;
            bool onlineSetup = online && phase == OnlineMatchPhase.WaitingForShieldSelections;
            bool mustChoose = offlineSetup ? _boot.AwaitingLocalShieldChoice : onlineSetup && _online.LocalShieldChoicePending;
            _model.ChoosingShield = (offlineSetup || onlineSetup) && mustChoose;
            _model.WaitingForSetup = (offlineSetup || onlineSetup) && !mustChoose;

            _model.OrderAvailable = _orders != null && _orders.OrderingAvailable;
            _model.OrderOpen = _orderUI != null && _orderUI.IsOpen;
            _model.CanDrink = _drink != null && _drink.LocalCanDrink;
            _model.Drinking = _drink != null && _drink.LocalDrinking;

            _model.RematchVisible = _rematch != null && _rematch.Visible;
            _model.RematchOnline = online;
            _model.RematchVoted = online && _online.LocalVotedRematch;
            _model.RematchYes = online ? _online.RematchYesCount : 0;
            _model.RematchNeeded = online ? Mathf.Max(1, _online.RematchRequired) : 0;

            string refusal = null;
            float refusalAlpha = 0f;
            if (online && _onlineDriver != null && _onlineDriver.TryGetRefusal(out string text, out float alpha))
            {
                refusal = text;
                refusalAlpha = alpha;
            }
            _model.Refusal = refusal;
            _model.RefusalAlpha = refusalAlpha;
        }

        // ------------------------------------------------------------------ projection

        /// <summary>
        /// A point fixed in the air just above a seat's head, on <paramref name="rect"/>. Never
        /// clamped: look away and it leaves the screen like the head does. False when the
        /// point is behind the camera. <paramref name="distance"/> is metres from the camera.
        /// </summary>
        public bool ProjectAboveHead(int seat, RectTransform rect, float clearance, out Vector2 local, out float distance)
        {
            local = Vector2.zero;
            distance = 0f;
            Camera cam = Camera.main;
            if (cam == null || _table == null) return false;
            PlayerCharacter character = _table.GetPlayer(seat);
            if (character == null) return false;

            Vector3 top = HeadTop(character) + Vector3.up * clearance;
            Vector3 screen = cam.WorldToScreenPoint(top);
            if (screen.z <= 0.05f) return false;
            distance = screen.z;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out local);
        }

        static readonly List<Renderer> HeadRenderers = new List<Renderer>();

        /// <summary>The top centre of a character's head (hats and hair included), in world space.</summary>
        static Vector3 HeadTop(PlayerCharacter character)
        {
            Transform head = character.HeadVisual != null ? character.HeadVisual : character.HeadPivot;
            if (head == null) return character.transform.position + Vector3.up * 1.4f;

            head.GetComponentsInChildren(false, HeadRenderers);
            bool any = false;
            Bounds b = default;
            foreach (Renderer r in HeadRenderers)
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return head.position + Vector3.up * 0.2f;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }
    }
}
