using System.Collections;
using Shields.Players;
using Shields.Settings;
using Shields.Social;
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

        [Header("Body outfits")]
        [Tooltip("The exported torso outfits, shared with the gameplay PlayerCharacter.")]
        [SerializeField] BodyOutfitLibrary bodyOutfits;

        // Where the Customize camera puts the character: camera yaw offset from looking
        // straight at it, the character's resting turn, aim height and field of view.
        const float PreviewYaw = -17.5f, PreviewTurn = -18f, PreviewLookHeight = 0.84f, PreviewFov = 44f;

        // FACE close-up: camera distance from the head centre, its height over it, how far
        // below the head centre it aims (lifts the head clear of the name plate), field of
        // view, and the seconds the camera takes to move in or out.
        const float FaceDistance = 1.2f, FaceRise = 0.0f, FaceAimDrop = 0.07f, FaceFov = 40f, FaceBlendSeconds = 0.38f;

        static readonly Color TavernSky = new Color(0.11f, 0.115f, 0.15f), TavernEq = new Color(0.12f, 0.095f, 0.08f), TavernGround = new Color(0.08f, 0.06f, 0.045f);

        public MainMenuState State { get; private set; }
        public CharacterTurntable Turntable { get { return turntable; } }

        MainMenuView _view;
        CharacterCosmetics _cosmetics;
        Transform _headVisual;
        Renderer _headMesh, _bodyMesh;
        Renderer[] _hands;
        CharacterFace _face;

        // The Customize camera's normal framing, and how far it is blended toward the FACE close-up.
        Vector3 _camRestPosition;
        Quaternion _camRestRotation;
        float _faceYaw, _faceBlend, _faceBlendVelocity;

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

            // BODY: Bare and the exported outfits as cards; the base colours go on the colour bar.
            foreach (BodyOutfit o in BodyCatalog.Outfits)
            {
                Add(o.Id, o.Name, WardrobeSlot.Body, ItemRarity.Common, o.IsDefault, false, null, null);
                State.Items[State.Items.Count - 1].Outfit = bodyOutfits != null ? bodyOutfits.PrefabFor(o.Id) : null;
                if (!o.IsDefault && State.Items[State.Items.Count - 1].Outfit == null)
                    Debug.LogError("Body outfit model missing from the BodyOutfitLibrary: " + o.SourceAsset, this);
            }
            foreach (BodyColor c in BodyCatalog.Colors)
            {
                var color = new Color(c.R, c.G, c.B);
                Material material = c.IsDefault ? null : Tint(c.Metal ? metalMaterial : seatMaterial, c.Name, color);
                Add(c.Id, c.Name, WardrobeSlot.BodyColor, (ItemRarity)(int)c.Rarity, c.IsDefault, c.Locked, null, material);
                State.Items[State.Items.Count - 1].Swatch = c.IsDefault && seatMaterial != null && seatMaterial.HasProperty("_BaseColor") ? seatMaterial.GetColor("_BaseColor") : color;
            }

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

            // The face parts live on the character (FaceAnchor); the item is just its id.
            foreach (FaceCosmetic f in FaceCatalog.All)
                Add(f.Id, f.Name, FaceWardrobeSlot(f.Slot), f.IsFacialHair ? ItemRarity.Rare : ItemRarity.Common, f.IsDefault, false, null, null);
        }

        static WardrobeSlot FaceWardrobeSlot(FaceSlot slot)
        {
            switch (slot)
            {
                case FaceSlot.Eyes: return WardrobeSlot.Eyes;
                case FaceSlot.Eyebrows: return WardrobeSlot.Eyebrows;
                default: return WardrobeSlot.Mouth;
            }
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
            // The hand meshes are skinned renderers inside the rigged-hand visuals under Hand_L / Hand_R.
            _hands = new[] { HandRenderer("Hand_L"), HandRenderer("Hand_R") };
            _face = _headVisual != null ? _headVisual.GetComponentInChildren<CharacterFace>(true) : null;

            _cosmetics = previewCharacter.GetComponent<CharacterCosmetics>();
            if (_cosmetics == null) _cosmetics = previewCharacter.gameObject.AddComponent<CharacterCosmetics>();
            _cosmetics.Configure(_headVisual, _headMesh, _bodyMesh, _hands, _face, BodyCosmeticAnchor.On(Find(previewCharacter, "Body")));
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
                        item.Thumbnail = studio.Render(OutfitModel(item.Outfit), 192, 205f, 6f, 1.4f);
                        break;
                    case WardrobeSlot.BodyColor:
                        break;   // shown as colour swatches, not cards
                    case WardrobeSlot.Eyes:
                    case WardrobeSlot.Eyebrows:
                    case WardrobeSlot.Mouth:
                        item.Thumbnail = studio.Render(FaceModel(item), 192, 0f, 3f, 1.5f);
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

        /// <summary>
        /// The preview character's body and head wearing one outfit (null: Bare), in the
        /// default body colour, without head props or the floating hands, so the outfit's
        /// silhouette fills the card.
        /// </summary>
        GameObject OutfitModel(GameObject outfit)
        {
            GameObject m = Stripped(previewCharacter.gameObject);
            var drop = new System.Collections.Generic.List<GameObject>();
            foreach (Transform t in m.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("HeadProp_") || t.name == "LeftShoulderAnchor" || t.name == "RightShoulderAnchor") drop.Add(t.gameObject);
            foreach (GameObject go in drop) DestroyImmediate(go);
            Transform bodyMesh = Find(m.transform, "BodyMesh");
            if (bodyMesh != null && _cosmetics != null && _cosmetics.DefaultBody != null) bodyMesh.GetComponent<Renderer>().sharedMaterial = _cosmetics.DefaultBody;
            BodyCosmeticAnchor anchor = BodyCosmeticAnchor.On(Find(m.transform, "Body"));
            if (anchor != null) anchor.Show(outfit != null ? outfit.name : null, outfit);
            return m;
        }

        /// <summary>
        /// The bare head wearing only this face part, like the authoring catalog. Turned
        /// around so the face looks into the studio's key light, toward a yaw-0 camera.
        /// </summary>
        GameObject FaceModel(CosmeticItem item)
        {
            GameObject head = Stripped(_headVisual.gameObject);
            head.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            FaceSlot slot = item.Slot == WardrobeSlot.Eyes ? FaceSlot.Eyes : item.Slot == WardrobeSlot.Eyebrows ? FaceSlot.Eyebrows : FaceSlot.Mouth;
            CharacterFace.ShowOnly(Find(head.transform, CharacterFace.AnchorName), slot, item.Id);
            return head;
        }

        Renderer HandRenderer(string hand)
        {
            Transform t = Find(previewCharacter, hand);
            return t != null ? t.GetComponentInChildren<Renderer>(true) : null;
        }

        /// <summary>The right hand for its thumbnail: the whole rigged visual (mesh and bones), posed as on the preview.</summary>
        GameObject HandModel(Material material)
        {
            Transform handR = Find(previewCharacter, "Hand_R");
            Transform visual = handR != null && handR.childCount > 0 ? handR.GetChild(0) : _hands[1].transform;
            GameObject h = Stripped(visual.gameObject);
            if (material != null)
                foreach (Renderer r in h.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
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
            float facing = Quaternion.LookRotation(toCam).eulerAngles.y;
            if (turntable.transform.parent != null) facing -= turntable.transform.parent.eulerAngles.y;
            _faceYaw = facing;
            turntable.SetRestYaw(facing + PreviewTurn, true);

            _camRestPosition = cam.position;
            _camRestRotation = cam.rotation;
        }

        /// <summary>
        /// Blends the Customize camera between its normal framing and a FACE close-up: the
        /// same line toward the character, much closer, aimed at the head and turned by the
        /// same yaw offset so the face sits in the open area right of the list.
        /// </summary>
        void UpdateFaceCamera()
        {
            if (customizeCamera == null || _headMesh == null) return;
            float target = State.FaceFocus ? 1f : 0f;
            if (_faceBlend == target && _faceBlendVelocity == 0f) return;

            _faceBlend = Mathf.SmoothDamp(_faceBlend, target, ref _faceBlendVelocity, FaceBlendSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(_faceBlend - target) < 0.0005f) { _faceBlend = target; _faceBlendVelocity = 0f; }

            Vector3 head = _headMesh.bounds.center;
            Vector3 away = _camRestPosition - head;
            away.y = 0f;
            Vector3 facePosition = head + away.normalized * FaceDistance + Vector3.up * FaceRise;
            Quaternion faceRotation = Quaternion.AngleAxis(PreviewYaw, Vector3.up) * Quaternion.LookRotation(head + Vector3.down * FaceAimDrop - facePosition);

            float k = Mathf.SmoothStep(0f, 1f, _faceBlend);
            Transform cam = customizeCamera.transform;
            cam.position = Vector3.Lerp(_camRestPosition, facePosition, k);
            cam.rotation = Quaternion.Slerp(_camRestRotation, faceRotation, k);
            customizeCamera.fieldOfView = Mathf.Lerp(PreviewFov, FaceFov, k);
        }

        void RefreshPreview()
        {
            if (_cosmetics == null) return;
            // The preview tries on the selection; outside the wardrobe it shows what is equipped.
            bool tryOn = State.Screen == MenuScreen.Customize && State.Tab == CustomizeTab.Wardrobe;
            CosmeticItem Pick(WardrobeSlot slot) { return tryOn ? State.SelectedItem(slot) : State.EquippedItem(slot); }
            _cosmetics.Show(Pick(WardrobeSlot.Head), Pick(WardrobeSlot.Body), Pick(WardrobeSlot.BodyColor), Pick(WardrobeSlot.Hands));
            _cosmetics.ShowFace(Pick(WardrobeSlot.Eyes), Pick(WardrobeSlot.Eyebrows), Pick(WardrobeSlot.Mouth));
        }

        // ------------------------------------------------------------------ state

        void OnStateChanged()
        {
            _wantCustomizeEnvironment = State.Screen == MenuScreen.Customize;
            if (_wantCustomizeEnvironment != _customizeEnvironment && _fadePhase == 0) _fadePhase = 1;
            RefreshPreview();
            if (turntable != null) turntable.Focus(State.FaceFocus, _faceYaw);
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
        public void SetSlot(WardrobeSlot slot)
        {
            State.Slot = slot;
            if (MainMenuState.IsFace(slot)) State.FaceSlot = slot;
            State.Notify();
        }

        /// <summary>A top-level wardrobe entry; FACE reopens the face subcategory last used.</summary>
        public void SetCategory(WardrobeCategory category)
        {
            switch (category)
            {
                case WardrobeCategory.Head: SetSlot(WardrobeSlot.Head); break;
                case WardrobeCategory.Face: SetSlot(State.FaceSlot); break;
                case WardrobeCategory.Body: SetSlot(WardrobeSlot.Body); break;
                default: SetSlot(WardrobeSlot.Hands); break;
            }
        }

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

        void LateUpdate() { UpdateFaceCamera(); }

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
