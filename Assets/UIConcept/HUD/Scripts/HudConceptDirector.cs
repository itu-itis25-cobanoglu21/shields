using System.Collections.Generic;
using Shields.Match;
using Shields.Menu;
using Shields.Online;
using Shields.Players;
using Shields.Presentation;
using Shields.Tavern;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shields.UIConcept.Hud
{
    /// <summary>
    /// Runs HUDConceptScene, a copy of GameScene. Starts an offline match exactly as the
    /// main menu would, switches the prototype IMGUI HUD off through
    /// <see cref="HudOverride"/>, and draws one of three HUD directions over the live match
    /// instead. F1 / F2 / F3 switch directions (1 / 2 / 3 are the game's action keys),
    /// H hides the switcher. Design exploration only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudConceptDirector : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset bowlby;
        [SerializeField] TMP_FontAsset knewave;
        [SerializeField, Range(3, 8)] int players = 5;
        [SerializeField] int startConcept;

        readonly HudModel _model = new HudModel();
        readonly List<HudConcept> _concepts = new List<HudConcept>();
        int _active = -1;

        // F5: the game's own HUD (MAIN went to production), hosted here for comparison.
        GameHud _main;

        MatchBootstrap _boot;
        BeerOrderCoordinator _orders;
        BeerOrderUI _orderUI;
        BeerDrinkCoordinator _drink;
        RematchUI _rematch;

        GameObject _switchBar;
        readonly List<TextMeshProUGUI> _switchLabels = new List<TextMeshProUGUI>();
        readonly List<Image> _switchBacks = new List<Image>();

        void Awake()
        {
            // Exactly what the main menu's OFFLINE MODE does; GameLobbyUI consumes it in Start.
            MainMenuLaunchRequest.Request(MainMenuIntent.Offline, string.Empty, players, 1);
            HudOverride.Active = true;
            HudOverride.Result += OnResult;
            HudOverride.ActionStarted += OnActionStarted;
        }

        void OnDestroy()
        {
            HudOverride.Result -= OnResult;
            HudOverride.ActionStarted -= OnActionStarted;
            HudOverride.Active = false;
            HudOverride.BeerPanel = false;
        }


        void OnResult(PublicActionResult result, int localSeat)
        {
            _model.AddResult(result, localSeat);
            foreach (HudConcept c in _concepts) c.ActionRevealed(result, localSeat);
        }

        void OnActionStarted(PublicActionResult result, int localSeat)
        {
            foreach (HudConcept c in _concepts) c.ActionStarted(result, localSeat);
        }

        void Start()
        {
            _boot = FindAnyObjectByType<MatchBootstrap>();
            _orders = FindAnyObjectByType<BeerOrderCoordinator>();
            _orderUI = FindAnyObjectByType<BeerOrderUI>();
            _drink = FindAnyObjectByType<BeerDrinkCoordinator>();
            _rematch = FindAnyObjectByType<RematchUI>();

            TavernUI.EnsureEventSystem();
            _concepts.Add(new HudInk());
            _concepts.Add(new HudBoard());
            _concepts.Add(new HudCards());
            _concepts.Add(new HudCardsBackup());
            foreach (HudConcept c in _concepts)
            {
                Canvas canvas = TavernUI.MakeCanvas(transform, "Hud" + c.Code, 5);
                c.Init(this, canvas, new TavernUI(bowlby, knewave));
                canvas.gameObject.SetActive(false);
            }

            var main = new GameObject("GameHud");
            main.SetActive(false);
            main.transform.SetParent(transform, false);
            _main = main.AddComponent<GameHud>();
            _main.SetFonts(bowlby, knewave);

            BuildSwitcher();
            Show(Mathf.Clamp(startConcept, 0, _concepts.Count));
        }

        /// <summary>Index <c>_concepts.Count</c> is the game HUD (F5).</summary>
        void SetShown(int index, bool on)
        {
            if (index < _concepts.Count) _concepts[index].Canvas.gameObject.SetActive(on);
            else _main.gameObject.SetActive(on);
        }

        void Show(int index)
        {
            if (index == _active) return;
            if (_active >= 0) SetShown(_active, false);
            _active = index;
            SetShown(_active, true);
            // The game HUD switches the prototype HUD off itself; the concepts need it off too,
            // but leave the beer order panel to the prototype.
            if (_active < _concepts.Count)
            {
                HudOverride.Active = true;
                HudOverride.BeerPanel = false;
            }
            for (int i = 0; i < _switchLabels.Count; i++)
            {
                bool on = i == _active;
                _switchLabels[i].color = on ? new Color(0.1f, 0.09f, 0.08f) : new Color(1f, 1f, 1f, 0.7f);
                _switchBacks[i].color = on ? new Color(0.94f, 0.9f, 0.82f, 0.95f) : new Color(1f, 1f, 1f, 0.06f);
            }
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) Show(0);
                if (kb.f2Key.wasPressedThisFrame) Show(1);
                if (kb.f3Key.wasPressedThisFrame) Show(2);
                if (kb.f4Key.wasPressedThisFrame) Show(3);
                if (kb.f5Key.wasPressedThisFrame) Show(4);
                if (kb.hKey.wasPressedThisFrame && _switchBar != null) _switchBar.SetActive(!_switchBar.activeSelf);
            }

            BuildModel();
            HideWorldLabels();
            if (_active >= 0 && _active < _concepts.Count) _concepts[_active].Tick(_model);
        }

        void BuildModel()
        {
            if (HudOverride.HasCurrentFrame)
            {
                HudOverride.Frame f = HudOverride.Latest;
                _model.SetMatch(f.Snapshot, f.LocalSeat, f.Pending, f.HoveredTarget);
            }
            else if (!(_model.InMatch && _boot != null && _boot.MatchRunning && _boot.Match != null))
            {
                _model.ClearMatch();
            }

            bool setup = _boot != null && _boot.MatchRunning && _boot.Match == null;
            _model.ChoosingShield = setup && _boot.AwaitingLocalShieldChoice;
            _model.WaitingForSetup = setup && !_boot.AwaitingLocalShieldChoice;

            _model.OrderAvailable = _orders != null && _orders.OrderingAvailable;
            _model.OrderOpen = _orderUI != null && _orderUI.IsOpen;
            _model.CanDrink = _drink != null && _drink.LocalCanDrink;
            _model.Drinking = _drink != null && _drink.LocalDrinking;
            _model.RematchVisible = _rematch != null && _rematch.Visible;
        }

        /// <summary>The prototype names / HP and action texts above heads give way to the concept's own.</summary>
        void HideWorldLabels()
        {
            foreach (WorldLabel label in FindObjectsByType<WorldLabel>())
                foreach (Renderer r in label.GetComponentsInChildren<Renderer>())
                    if (r.enabled) r.enabled = false;
        }

        /// <summary>
        /// Where a seat's nameplate goes on <paramref name="rect"/>: just under the head (seated
        /// opponents' heads sit at the top edge of the view), kept inside the screen.
        /// </summary>
        public bool ProjectHead(int seat, RectTransform rect, out Vector2 local)
        {
            local = Vector2.zero;
            Camera cam = Camera.main;
            if (cam == null || _boot == null || _boot.TableLayout == null) return false;
            PlayerCharacter character = _boot.TableLayout.GetPlayer(seat);
            if (character == null) return false;
            Transform head = character.HeadPivot != null ? character.HeadPivot : character.transform;
            Vector3 screen = cam.WorldToScreenPoint(head.position + Vector3.down * 0.32f);
            if (screen.z <= 0f) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out local)) return false;
            Vector2 half = rect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + 120, half.x - 120);
            local.y = Mathf.Clamp(local.y, -half.y + 80, half.y - 260);
            return true;
        }

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
            if (cam == null || _boot == null || _boot.TableLayout == null) return false;
            PlayerCharacter character = _boot.TableLayout.GetPlayer(seat);
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

        // ------------------------------------------------------------------ switcher

        void BuildSwitcher()
        {
            Canvas overlay = TavernUI.MakeCanvas(transform, "HudConceptSwitcher", 90);
            RectTransform root = (RectTransform)overlay.transform;
            TMP_FontAsset ui = TMP_Settings.defaultFontAsset;

            string[] names = { "F1  INK", "F2  TAVERN BOARD", "F3  CARD TABLE", "F4  CARD TABLE BACKUP", "F5  MAIN" };
            float[] widths = { 96, 196, 170, 250, 100 };
            float w = 124 + 12;
            foreach (float x in widths) w += x + 6;
            RectTransform bar = UiKit.Node(root, "SwitcherBar").At(0.5f, 0, 0.5f, 0, 0, 10, w, 36);
            _switchBar = bar.gameObject;
            UiKit.Img(bar, "Back", UiSprites.RoundRect(10), new Color(0, 0, 0, 0.55f)).rectTransform.Fill();
            TextMeshProUGUI tag = UiKit.Text(bar, "Tag", "HUD CONCEPT", ui, 14, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.MidlineLeft);
            tag.rectTransform.TL(12, 0, 120, 36);
            float px = 124;
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Button b = UiKit.Button(bar, "Concept" + i, () => Show(index), out _);
                b.GetComponent<RectTransform>().TL(px, 5, widths[i], 26);
                Image back = UiKit.Img(b.transform, "Back", UiSprites.RoundRect(8), Color.white);
                back.rectTransform.Fill();
                TextMeshProUGUI label = UiKit.Text(b.transform, "Label", names[i], ui, 14, Color.white, TextAlignmentOptions.Center);
                label.rectTransform.Fill();
                label.fontStyle = FontStyles.Bold;
                _switchBacks.Add(back);
                _switchLabels.Add(label);
                px += widths[i] + 6;
            }
        }
    }
}
