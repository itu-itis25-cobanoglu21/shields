using System.Collections;
using Shields.Settings;
using Shields.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shields.Menu
{
    /// <summary>
    /// Runs MainMenuScene. Builds the menu UI (<see cref="MainMenuView"/>), switches
    /// between the menu diorama and the tavern weapon corner used by Customize, and does
    /// what the menu asks: OFFLINE MODE, CREATE ROOM and JOIN ROOM record a
    /// <see cref="MainMenuLaunchRequest"/> and load GameScene, whose lobby starts the
    /// match, hosts or joins. Customize and Settings are saved on this machine.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Environments")]
        [Tooltip("Everything that belongs to the main menu diorama (environment, set dressing, lighting).")]
        [SerializeField] GameObject[] menuSet;
        [SerializeField] Camera menuCamera;
        [Tooltip("The tavern weapon corner shown behind Customize.")]
        [SerializeField] GameObject customizeSet;
        [SerializeField] Camera customizeCamera;

        [Header("Customize preview")]
        [SerializeField] CharacterTurntable turntable;
        [SerializeField] Transform previewCharacter;

        [Header("Type")]
        [SerializeField] TMP_FontAsset bowlby;
        [SerializeField] TMP_FontAsset knewave;
        [SerializeField] Material logoStroke;

        [Header("Placeholder cosmetics")]
        [SerializeField] GameObject helmetA;
        [SerializeField] GameObject helmetB;
        [SerializeField] GameObject helmetC;
        [SerializeField] GameObject tricorn;
        [SerializeField] GameObject aleMug;
        [SerializeField] Material skinMaterial;
        [SerializeField] Material metalMaterial;
        [SerializeField] Material darkMaterial;
        [SerializeField] Material seatMaterial;

        // Where the Customize camera puts the character: camera yaw offset from looking
        // straight at it, the character's resting turn, aim height and field of view.
        const float PreviewYaw = -17.5f, PreviewTurn = -18f, PreviewLookHeight = 0.84f, PreviewFov = 44f;

        static readonly Color TavernSky = new Color(0.11f, 0.115f, 0.15f), TavernEq = new Color(0.12f, 0.095f, 0.08f), TavernGround = new Color(0.08f, 0.06f, 0.045f);

        public MainMenuState State { get; private set; }
        public CharacterTurntable Turntable { get { return turntable; } }

        MainMenuView _view;
        CharacterCosmetics _cosmetics;
        Transform _headVisual;
        Renderer _headMesh, _bodyMesh;
        Renderer[] _hands;

        Color _menuSky, _menuEq, _menuGround;
        bool _customizeEnvironment, _wantCustomizeEnvironment;
        float _fadeAlpha;
        int _fadePhase; // 0 idle, 1 fading out, 2 fading in
        Image _fade;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            TavernUI.EnsureEventSystem();

            _menuSky = RenderSettings.ambientSkyColor;
            _menuEq = RenderSettings.ambientEquatorColor;
            _menuGround = RenderSettings.ambientGroundColor;

            State = new MainMenuState();
            BuildCatalog();
            State.LoadEquipped();
            SetupPreview();
            RenderThumbnails();
            ApplyFraming();

            _view = new MainMenuView(this, State, bowlby, knewave, logoStroke);
            BuildFade();

            State.Changed += OnStateChanged;
            ApplyEnvironment(false, true);
            OnStateChanged();
        }

        void OnDestroy()
        {
            if (State != null) State.Changed -= OnStateChanged;
        }

        void BuildFade()
        {
            Canvas overlay = TavernUI.MakeCanvas(transform, "MenuFade", 100);
            _fade = UiKit.Img(overlay.transform, "Fade", UiSprites.White(), new Color(0, 0, 0, 0));
            _fade.rectTransform.Fill();
        }

        void BuildCatalog()
        {
            Add("head_bare", "BARE HEAD", WardrobeSlot.Head, ItemRarity.Common, true, false, null, null);
            Add("head_iron", "IRON HELM", WardrobeSlot.Head, ItemRarity.Common, false, false, helmetB, null);
            Add("head_gilt", "GILT HELM", WardrobeSlot.Head, ItemRarity.Rare, false, false, helmetA, null);
            Add("head_great", "AZURE HELM", WardrobeSlot.Head, ItemRarity.Rare, false, false, helmetC, null);
            Add("head_tricorn", "TRICORN", WardrobeSlot.Head, ItemRarity.Rare, false, false, tricorn, null);
            Add("head_mug", "MUG OF HONOR", WardrobeSlot.Head, ItemRarity.Legendary, false, true, aleMug, null);
            Add("head_blacksteel", "BLACKSTEEL HELM", WardrobeSlot.Head, ItemRarity.Rare, false, false, helmetB, Tint(metalMaterial, "Blacksteel", new Color(0.16f, 0.16f, 0.18f)));
            Add("head_bronze", "BRONZE HELM", WardrobeSlot.Head, ItemRarity.Common, false, false, helmetA, Tint(metalMaterial, "Bronze", new Color(0.62f, 0.38f, 0.18f)));
            Add("head_bone", "BONE HELM", WardrobeSlot.Head, ItemRarity.Rare, false, true, helmetC, Tint(seatMaterial, "Bone", new Color(0.86f, 0.81f, 0.68f)));
            Add("head_captain", "CAPTAIN'S TRICORN", WardrobeSlot.Head, ItemRarity.Rare, false, false, tricorn, Tint(seatMaterial, "Captain", new Color(0.5f, 0.1f, 0.1f)));
            Add("head_goldhat", "GOLDEN TRICORN", WardrobeSlot.Head, ItemRarity.Legendary, false, true, tricorn, Tint(metalMaterial, "GoldHat", new Color(0.9f, 0.68f, 0.22f)));

            Add("body_seat", "SEAT COLORS", WardrobeSlot.Body, ItemRarity.Common, true, false, null, null);
            Add("body_crimson", "CRIMSON TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Crimson", new Color(0.55f, 0.11f, 0.12f)));
            Add("body_forest", "FOREST TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Forest", new Color(0.17f, 0.33f, 0.19f)));
            Add("body_midnight", "MIDNIGHT TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Midnight", new Color(0.14f, 0.18f, 0.36f)));
            Add("body_mustard", "MUSTARD TUNIC", WardrobeSlot.Body, ItemRarity.Rare, false, false, null, Tint(seatMaterial, "Mustard", new Color(0.78f, 0.56f, 0.16f)));
            Add("body_coal", "COAL TUNIC", WardrobeSlot.Body, ItemRarity.Rare, false, false, null, Tint(seatMaterial, "Coal", new Color(0.12f, 0.12f, 0.13f)));
            Add("body_motley", "JESTER MOTLEY", WardrobeSlot.Body, ItemRarity.Rare, false, true, null, Tint(seatMaterial, "Motley", new Color(0.42f, 0.16f, 0.46f)));
            Add("body_gilded", "GILDED VEST", WardrobeSlot.Body, ItemRarity.Legendary, false, true, null, Tint(metalMaterial, "Gilded", new Color(0.86f, 0.62f, 0.2f)));
            Add("body_plum", "PLUM TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Plum", new Color(0.36f, 0.14f, 0.3f)));
            Add("body_sky", "SKY TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Sky", new Color(0.3f, 0.5f, 0.7f)));
            Add("body_rose", "ROSE TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Rose", new Color(0.74f, 0.4f, 0.44f)));
            Add("body_ash", "ASH TUNIC", WardrobeSlot.Body, ItemRarity.Rare, false, true, null, Tint(seatMaterial, "Ash", new Color(0.48f, 0.47f, 0.45f)));

            Add("hands_bare", "BARE HANDS", WardrobeSlot.Hands, ItemRarity.Common, true, false, null, null);
            Add("hands_leather", "LEATHER GLOVES", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, Tint(skinMaterial, "Leather", new Color(0.36f, 0.22f, 0.13f)));
            Add("hands_black", "BLACK GLOVES", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, darkMaterial);
            Add("hands_iron", "IRON GAUNTLETS", WardrobeSlot.Hands, ItemRarity.Rare, false, false, null, metalMaterial);
            Add("hands_red", "RED MITTS", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, Tint(skinMaterial, "Red", new Color(0.6f, 0.12f, 0.12f)));
            Add("hands_gold", "GOLDEN KNUCKLES", WardrobeSlot.Hands, ItemRarity.Legendary, false, true, null, Tint(metalMaterial, "Gold", new Color(0.9f, 0.66f, 0.22f)));
            Add("hands_green", "GREEN MITTS", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, Tint(skinMaterial, "Green", new Color(0.2f, 0.38f, 0.2f)));
            Add("hands_blue", "BLUE GLOVES", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, Tint(skinMaterial, "Blue", new Color(0.2f, 0.28f, 0.55f)));
            Add("hands_white", "WHITE GLOVES", WardrobeSlot.Hands, ItemRarity.Rare, false, false, null, Tint(skinMaterial, "White", new Color(0.88f, 0.86f, 0.8f)));
            Add("hands_bronze", "BRONZE GAUNTLETS", WardrobeSlot.Hands, ItemRarity.Rare, false, true, null, Tint(metalMaterial, "BronzeHands", new Color(0.62f, 0.38f, 0.18f)));
        }

        void Add(string id, string name, WardrobeSlot slot, ItemRarity rarity, bool isDefault, bool locked, GameObject prefab, Material material)
        {
            State.Items.Add(new CosmeticItem { Id = id, Name = name, Slot = slot, Rarity = rarity, IsDefault = isDefault, Locked = locked, HeadPrefab = prefab, Material = material });
        }

        static Material Tint(Material source, string name, Color color)
        {
            if (source == null) return null;
            var m = new Material(source) { name = "Cosmetic_" + name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        // ------------------------------------------------------------------ preview

        void SetupPreview()
        {
            if (previewCharacter == null) return;
            _headVisual = Find(previewCharacter, "HeadVisual");
            _headMesh = Find(previewCharacter, "HeadMesh")?.GetComponent<Renderer>();
            _bodyMesh = Find(previewCharacter, "BodyMesh")?.GetComponent<Renderer>();
            _hands = new[] { Find(previewCharacter, "Hand_L")?.GetComponent<Renderer>(), Find(previewCharacter, "Hand_R")?.GetComponent<Renderer>() };

            _cosmetics = previewCharacter.GetComponent<CharacterCosmetics>();
            if (_cosmetics == null) _cosmetics = previewCharacter.gameObject.AddComponent<CharacterCosmetics>();
            _cosmetics.Configure(_headVisual, _headMesh, _bodyMesh, _hands);
            if (turntable != null) turntable.SetBreathingTarget(previewCharacter);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                Transform f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        void RenderThumbnails()
        {
            if (previewCharacter == null) return;
            var studio = new CosmeticThumbnails();
            foreach (CosmeticItem item in State.Items)
            {
                switch (item.Slot)
                {
                    case WardrobeSlot.Head:
                        item.Thumbnail = item.HeadPrefab != null
                            ? studio.Render(CharacterCosmetics.Recolour(Stripped(item.HeadPrefab), item.Material), 192, 205f, 14f)
                            : studio.Render(Stripped(_headVisual.gameObject), 192, 200f, 8f, 1.05f);
                        break;
                    case WardrobeSlot.Body:
                        item.Thumbnail = studio.Render(Mannequin(item.Material), 192, 205f, 8f, 1.08f);
                        break;
                    default:
                        item.Thumbnail = studio.Render(HandModel(item.Material), 192, 150f, 18f, 1.0f);
                        break;
                }
            }
            studio.Dispose();
        }

        static GameObject Stripped(GameObject source)
        {
            GameObject go = CharacterCosmetics.InstantiateStripped(source, null);
            go.transform.localScale = Vector3.one;
            return go;
        }

        GameObject Mannequin(Material body)
        {
            GameObject m = Stripped(previewCharacter.gameObject);
            foreach (Transform t in m.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("HeadProp_")) Destroy(t.gameObject);
            Transform bodyMesh = Find(m.transform, "BodyMesh");
            if (body != null && bodyMesh != null) bodyMesh.GetComponent<Renderer>().sharedMaterial = body;
            return m;
        }

        GameObject HandModel(Material material)
        {
            GameObject h = Stripped(_hands[1].gameObject);
            if (material != null) h.GetComponent<Renderer>().sharedMaterial = material;
            return h;
        }

        /// <summary>Aims the Customize camera and turns the character so it stands right of the list.</summary>
        void ApplyFraming()
        {
            if (customizeCamera == null || turntable == null) return;
            Transform cam = customizeCamera.transform;
            Vector3 pivot = turntable.transform.position;
            cam.LookAt(pivot + Vector3.up * PreviewLookHeight);
            cam.Rotate(0f, PreviewYaw, 0f, Space.World);
            customizeCamera.fieldOfView = PreviewFov;

            Vector3 toCam = cam.position - pivot;
            toCam.y = 0f;
            float facing = Quaternion.LookRotation(toCam).eulerAngles.y + PreviewTurn;
            if (turntable.transform.parent != null) facing -= turntable.transform.parent.eulerAngles.y;
            turntable.SetRestYaw(facing, true);
        }

        void RefreshPreview()
        {
            if (_cosmetics == null) return;
            // The preview tries on the selection; outside the wardrobe it shows what is equipped.
            bool tryOn = State.Screen == MenuScreen.Customize && State.Tab == CustomizeTab.Wardrobe;
            CosmeticItem Pick(WardrobeSlot slot) { return tryOn ? State.SelectedItem(slot) : State.EquippedItem(slot); }
            _cosmetics.Show(Pick(WardrobeSlot.Head), Pick(WardrobeSlot.Body), Pick(WardrobeSlot.Hands));
        }

        // ------------------------------------------------------------------ state

        void OnStateChanged()
        {
            _wantCustomizeEnvironment = State.Screen == MenuScreen.Customize;
            if (_wantCustomizeEnvironment != _customizeEnvironment && _fadePhase == 0) _fadePhase = 1;
            RefreshPreview();
            _view.Refresh();
        }

        public void GoMenu() { State.Screen = MenuScreen.MainMenu; State.Panel = MenuPanel.None; State.Notify(); }
        public void GoSettings() { State.Screen = MenuScreen.Settings; State.Panel = MenuPanel.None; State.Notify(); }

        public void GoCustomize()
        {
            State.ClearTryOn();
            State.Screen = MenuScreen.Customize;
            State.Panel = MenuPanel.None;
            State.Notify();
        }

        public void SetTab(CustomizeTab tab) { State.Tab = tab; State.Notify(); }
        public void SetSlot(WardrobeSlot slot) { State.Slot = slot; State.Notify(); }

        public void OpenPanel(MenuPanel panel)
        {
            if (State.Panel == panel) return;
            State.Panel = panel;
            State.Message = string.Empty;
            State.Notify();
        }

        public void ClosePanel()
        {
            State.Panel = MenuPanel.None;
            State.Message = string.Empty;
            State.Notify();
        }

        public void OpenJoin(TMP_InputField input)
        {
            if (State.Panel == MenuPanel.Join)
            {
                if (input != null) input.ActivateInputField();
                return;
            }
            OpenPanel(MenuPanel.Join);
            if (input == null) return;
            input.text = string.Empty;
            input.Select();
            input.ActivateInputField();
        }

        /// <summary>Esc: close the open panel, or go back to the main menu.</summary>
        public void Back()
        {
            if (State.Busy) return;
            if (State.Panel != MenuPanel.None) { ClosePanel(); return; }
            if (State.Screen != MenuScreen.MainMenu) GoMenu();
        }

        // ------------------------------------------------------------------ play

        public void ConfirmOffline()
        {
            MainMenuLaunchRequest.Request(MainMenuIntent.Offline, string.Empty, State.TablePlayers, State.TableDecks);
            LoadGameScene();
        }

        public void ConfirmCreate()
        {
            MainMenuLaunchRequest.Request(MainMenuIntent.CreateRoom, string.Empty, State.TablePlayers, State.TableDecks);
            LoadGameScene();
        }

        public void ConfirmJoin(string rawCode)
        {
            // Codes are case-insensitive letters and digits; spaces from copy-paste go.
            string code = (rawCode ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                State.Message = "ENTER A ROOM CODE";
                State.Notify();
                return;
            }
            MainMenuLaunchRequest.Request(MainMenuIntent.JoinRoom, code);
            LoadGameScene();
        }

        public void Exit()
        {
            if (State.Busy) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void LoadGameScene()
        {
            if (State.Busy) return;
            if (!Application.CanStreamedLevelBeLoaded(MainMenuLaunchRequest.GameSceneName))
            {
                MainMenuLaunchRequest.TryConsume(out _, out _, out _, out _);
                Debug.LogError("GameScene is not in the Build Profile scene list, so the menu cannot open it.", this);
                State.Message = "COULD NOT OPEN THE GAME";
                State.Notify();
                return;
            }

            State.Busy = true;
            State.Notify();
            StartCoroutine(FadeAndLoad());
        }

        IEnumerator FadeAndLoad()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(MainMenuLaunchRequest.GameSceneName);
            load.allowSceneActivation = false;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                _fade.color = new Color(0, 0, 0, Mathf.Max(_fadeAlpha, t));
                yield return null;
            }
            _fade.color = Color.black;
            _fade.raycastTarget = true;
            load.allowSceneActivation = true;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
            if (!State.Busy) UpdateFade();
        }

        void UpdateFade()
        {
            const float outSeconds = 0.16f, inSeconds = 0.3f;
            if (_fadePhase == 1)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, Time.unscaledDeltaTime / outSeconds);
                if (_fadeAlpha >= 1f)
                {
                    ApplyEnvironment(_wantCustomizeEnvironment, false);
                    _fadePhase = 2;
                }
            }
            else if (_fadePhase == 2)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.unscaledDeltaTime / inSeconds);
                if (_fadeAlpha <= 0f)
                {
                    _fadePhase = 0;
                    if (_wantCustomizeEnvironment != _customizeEnvironment) _fadePhase = 1;
                }
            }
            _fade.color = new Color(0, 0, 0, _fadeAlpha);
            _fade.raycastTarget = _fadeAlpha > 0.5f;
        }

        void ApplyEnvironment(bool customize, bool force)
        {
            if (!force && customize == _customizeEnvironment) return;
            _customizeEnvironment = customize;
            if (menuSet != null) foreach (GameObject go in menuSet) if (go != null) go.SetActive(!customize);
            if (customizeSet != null) customizeSet.SetActive(customize);
            if (menuCamera != null) menuCamera.enabled = !customize;
            if (customizeCamera != null) customizeCamera.enabled = customize;
            RenderSettings.ambientSkyColor = customize ? TavernSky : _menuSky;
            RenderSettings.ambientEquatorColor = customize ? TavernEq : _menuEq;
            RenderSettings.ambientGroundColor = customize ? TavernGround : _menuGround;
        }
    }
}
