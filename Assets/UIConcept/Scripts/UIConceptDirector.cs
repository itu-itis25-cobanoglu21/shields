using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// Runs UIConceptScene: builds the UI concepts from code, shares one prototype state
    /// between them, switches them with 1 – 5, and moves between the main menu
    /// diorama and the tavern weapon corner used for the Customize preview.
    ///
    /// Design exploration only. Nothing here is production UI, and nothing persists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIConceptDirector : MonoBehaviour
    {
        [Header("Environments")]
        [SerializeField] GameObject menuSet;
        [SerializeField] GameObject tavernSet;
        [SerializeField] Camera menuCamera;
        [SerializeField] Camera customizeCamera;

        [Header("Preview")]
        [SerializeField] PreviewTurntable turntable;
        [SerializeField] Transform previewCharacter;

        [Header("Type")]
        public TMP_FontAsset Bowlby;
        public TMP_FontAsset Knewave;
        public Material KnewaveLogo;
        public Material KnewaveLogoStroke;
        public Sprite MenuDiamond;

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

        [Header("Start")]
        [SerializeField] int startConcept;

        public UIConceptState State { get; private set; }
        public PreviewTurntable Turntable { get { return turntable; } }

        readonly List<ConceptView> _views = new List<ConceptView>();
        int _active = -1;
        PreviewCosmetics _cosmetics;

        // Overlay
        Canvas _overlay;
        Image _fade;
        TextMeshProUGUI _toast;
        CanvasGroup _toastGroup;
        float _toastTimer;
        readonly List<TextMeshProUGUI> _switchLabels = new List<TextMeshProUGUI>();
        readonly List<Image> _switchBacks = new List<Image>();
        GameObject _switchBar;

        // Environment fade
        bool _customizeEnvironment;
        bool _wantCustomizeEnvironment;
        float _fadeAlpha;
        int _fadePhase; // 0 idle, 1 fading out, 2 fading in

        static readonly Color MenuSky = new Color(0.09f, 0.10f, 0.15f), MenuEq = new Color(0.075f, 0.062f, 0.055f), MenuGround = new Color(0.045f, 0.038f, 0.034f);
        static readonly Color TavernSky = new Color(0.11f, 0.115f, 0.15f), TavernEq = new Color(0.12f, 0.095f, 0.08f), TavernGround = new Color(0.08f, 0.06f, 0.045f);

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            State = new UIConceptState();
            BuildCatalog();
            SetupPreview();
            RenderThumbnails();

            BuildOverlay();

            _views.Add(new ConceptA(CandleTheme.Original()));
            _views.Add(new ConceptAC(CandleTheme.Original()));
            _views.Add(new ConceptA(CandleTheme.Ink()));
            _views.Add(new ConceptAC(CandleTheme.Hearth()));
            _views.Add(new ConceptA(CandleTheme.Main()));
            for (int i = 0; i < _views.Count; i++)
            {
                Canvas canvas = MakeCanvas("Concept" + _views[i].Code, 10);
                _views[i].Init(this, canvas);
                canvas.gameObject.SetActive(false);
            }

            State.Changed += OnStateChanged;
            ApplyEnvironment(false, true);
            ShowConcept(Mathf.Clamp(startConcept, 0, _views.Count - 1));
            OnStateChanged();
        }

        void OnDestroy()
        {
            if (State != null) State.Changed -= OnStateChanged;
        }

        Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        void BuildCatalog()
        {
            var s = State;
            Add(s, "head_bare", "BARE HEAD", WardrobeSlot.Head, ItemRarity.Common, true, false, null, null);
            Add(s, "head_iron", "IRON HELM", WardrobeSlot.Head, ItemRarity.Common, false, false, helmetB, null);
            Add(s, "head_gilt", "GILT HELM", WardrobeSlot.Head, ItemRarity.Rare, false, false, helmetA, null);
            Add(s, "head_great", "AZURE HELM", WardrobeSlot.Head, ItemRarity.Rare, false, false, helmetC, null);
            Add(s, "head_tricorn", "TRICORN", WardrobeSlot.Head, ItemRarity.Rare, false, false, tricorn, null);
            Add(s, "head_mug", "MUG OF HONOR", WardrobeSlot.Head, ItemRarity.Legendary, false, true, aleMug, null);

            Add(s, "body_seat", "SEAT COLORS", WardrobeSlot.Body, ItemRarity.Common, true, false, null, null);
            Add(s, "body_crimson", "CRIMSON TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Crimson", new Color(0.55f, 0.11f, 0.12f)));
            Add(s, "body_forest", "FOREST TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Forest", new Color(0.17f, 0.33f, 0.19f)));
            Add(s, "body_midnight", "MIDNIGHT TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, false, null, Tint(seatMaterial, "Midnight", new Color(0.14f, 0.18f, 0.36f)));
            Add(s, "body_mustard", "MUSTARD TUNIC", WardrobeSlot.Body, ItemRarity.Rare, false, false, null, Tint(seatMaterial, "Mustard", new Color(0.78f, 0.56f, 0.16f)));
            Add(s, "body_coal", "COAL TUNIC", WardrobeSlot.Body, ItemRarity.Rare, false, false, null, Tint(seatMaterial, "Coal", new Color(0.12f, 0.12f, 0.13f)));
            Add(s, "body_motley", "JESTER MOTLEY", WardrobeSlot.Body, ItemRarity.Rare, false, true, null, Tint(seatMaterial, "Motley", new Color(0.42f, 0.16f, 0.46f)));
            Add(s, "body_gilded", "GILDED VEST", WardrobeSlot.Body, ItemRarity.Legendary, false, true, null, Tint(metalMaterial, "Gilded", new Color(0.86f, 0.62f, 0.2f)));

            Add(s, "hands_bare", "BARE HANDS", WardrobeSlot.Hands, ItemRarity.Common, true, false, null, null);
            Add(s, "hands_leather", "LEATHER GLOVES", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, Tint(skinMaterial, "Leather", new Color(0.36f, 0.22f, 0.13f)));
            Add(s, "hands_black", "BLACK GLOVES", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, darkMaterial);
            Add(s, "hands_iron", "IRON GAUNTLETS", WardrobeSlot.Hands, ItemRarity.Rare, false, false, null, metalMaterial);
            Add(s, "hands_red", "RED MITTS", WardrobeSlot.Hands, ItemRarity.Common, false, false, null, Tint(skinMaterial, "Red", new Color(0.6f, 0.12f, 0.12f)));
            Add(s, "hands_gold", "GOLDEN KNUCKLES", WardrobeSlot.Hands, ItemRarity.Legendary, false, true, null, Tint(metalMaterial, "Gold", new Color(0.9f, 0.66f, 0.22f)));

            // Extra placeholders (recoloured existing props and dyes) so every category
            // has more than nine items and the scrolling lists can be judged.
            Extra(s, "head_blacksteel", "BLACKSTEEL HELM", WardrobeSlot.Head, ItemRarity.Rare, false, helmetB, Tint(metalMaterial, "Blacksteel", new Color(0.16f, 0.16f, 0.18f)));
            Extra(s, "head_bronze", "BRONZE HELM", WardrobeSlot.Head, ItemRarity.Common, false, helmetA, Tint(metalMaterial, "Bronze", new Color(0.62f, 0.38f, 0.18f)));
            Extra(s, "head_bone", "BONE HELM", WardrobeSlot.Head, ItemRarity.Rare, true, helmetC, Tint(seatMaterial, "Bone", new Color(0.86f, 0.81f, 0.68f)));
            Extra(s, "head_captain", "CAPTAIN'S TRICORN", WardrobeSlot.Head, ItemRarity.Rare, false, tricorn, Tint(seatMaterial, "Captain", new Color(0.5f, 0.1f, 0.1f)));
            Extra(s, "head_goldhat", "GOLDEN TRICORN", WardrobeSlot.Head, ItemRarity.Legendary, true, tricorn, Tint(metalMaterial, "GoldHat", new Color(0.9f, 0.68f, 0.22f)));

            Extra(s, "body_plum", "PLUM TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, null, Tint(seatMaterial, "Plum", new Color(0.36f, 0.14f, 0.3f)));
            Extra(s, "body_sky", "SKY TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, null, Tint(seatMaterial, "Sky", new Color(0.3f, 0.5f, 0.7f)));
            Extra(s, "body_rose", "ROSE TUNIC", WardrobeSlot.Body, ItemRarity.Common, false, null, Tint(seatMaterial, "Rose", new Color(0.74f, 0.4f, 0.44f)));
            Extra(s, "body_ash", "ASH TUNIC", WardrobeSlot.Body, ItemRarity.Rare, true, null, Tint(seatMaterial, "Ash", new Color(0.48f, 0.47f, 0.45f)));

            Extra(s, "hands_green", "GREEN MITTS", WardrobeSlot.Hands, ItemRarity.Common, false, null, Tint(skinMaterial, "Green", new Color(0.2f, 0.38f, 0.2f)));
            Extra(s, "hands_blue", "BLUE GLOVES", WardrobeSlot.Hands, ItemRarity.Common, false, null, Tint(skinMaterial, "Blue", new Color(0.2f, 0.28f, 0.55f)));
            Extra(s, "hands_white", "WHITE GLOVES", WardrobeSlot.Hands, ItemRarity.Rare, false, null, Tint(skinMaterial, "White", new Color(0.88f, 0.86f, 0.8f)));
            Extra(s, "hands_bronze", "BRONZE GAUNTLETS", WardrobeSlot.Hands, ItemRarity.Rare, true, null, Tint(metalMaterial, "BronzeHands", new Color(0.62f, 0.38f, 0.18f)));

            // Start with every state visible at once: an equipped item, a different
            // selected (tried-on) item, a default, and locked items.
            s.Equipped[WardrobeSlot.Head] = "head_tricorn";
            s.Selected[WardrobeSlot.Head] = "head_great";
            s.Equipped[WardrobeSlot.Body] = "body_crimson";
            s.Selected[WardrobeSlot.Body] = "body_crimson";
            s.Equipped[WardrobeSlot.Hands] = "hands_bare";
            s.Selected[WardrobeSlot.Hands] = "hands_bare";
        }

        static void Add(UIConceptState s, string id, string name, WardrobeSlot slot, ItemRarity rarity, bool isDefault, bool locked, GameObject prefab, Material material)
        {
            s.Items.Add(new CosmeticItem { Id = id, Name = name, Slot = slot, Rarity = rarity, IsDefault = isDefault, Locked = locked, HeadPrefab = prefab, Material = material });
        }

        static void Extra(UIConceptState s, string id, string name, WardrobeSlot slot, ItemRarity rarity, bool locked, GameObject prefab, Material material)
        {
            Add(s, id, name, slot, rarity, false, locked, prefab, material);
            s.Items[s.Items.Count - 1].Extra = true;
        }

        static Material Tint(Material source, string name, Color color)
        {
            if (source == null) return null;
            var m = new Material(source) { name = "Concept_" + name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        // ------------------------------------------------------------------ preview

        Transform _headVisual;
        Renderer _headMesh, _bodyMesh;
        Renderer[] _hands;

        void SetupPreview()
        {
            if (previewCharacter == null) return;
            _headVisual = Find(previewCharacter, "HeadVisual");
            _headMesh = Find(previewCharacter, "HeadMesh")?.GetComponent<Renderer>();
            _bodyMesh = Find(previewCharacter, "BodyMesh")?.GetComponent<Renderer>();
            _hands = new[] { Find(previewCharacter, "Hand_L")?.GetComponent<Renderer>(), Find(previewCharacter, "Hand_R")?.GetComponent<Renderer>() };

            _cosmetics = previewCharacter.GetComponent<PreviewCosmetics>();
            if (_cosmetics == null)
            {
                // Configure before Awake reads the default materials.
                previewCharacter.gameObject.SetActive(false);
                _cosmetics = previewCharacter.gameObject.AddComponent<PreviewCosmetics>();
                _cosmetics.Configure(_headVisual, _headMesh, _bodyMesh, _hands);
                previewCharacter.gameObject.SetActive(true);
            }
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
            var studio = new ThumbnailStudio();
            var holder = new GameObject("__ThumbHolder");
            holder.SetActive(false);

            foreach (CosmeticItem item in State.Items)
            {
                switch (item.Slot)
                {
                    case WardrobeSlot.Head:
                        if (item.HeadPrefab != null)
                            item.Thumbnail = studio.Render(PreviewCosmetics.Recolour(Strip(Instantiate(item.HeadPrefab, holder.transform)), item.Material), 192, 205f, 14f);
                        else
                            item.Thumbnail = studio.Render(Strip(Instantiate(_headVisual.gameObject, holder.transform)), 192, 200f, 8f, 1.05f);
                        break;
                    case WardrobeSlot.Body:
                        item.Thumbnail = studio.Render(Mannequin(holder.transform, item.Material), 192, 205f, 8f, 1.08f);
                        break;
                    default:
                        item.Thumbnail = studio.Render(HandModel(holder.transform, item.Material), 192, 150f, 18f, 1.0f);
                        break;
                }
            }

            studio.Dispose();
            Destroy(holder);
        }

        // Instantiated under an inactive holder, so no script on a prop ever wakes up.
        static GameObject Strip(GameObject go)
        {
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (ParticleSystem p in go.GetComponentsInChildren<ParticleSystem>(true)) DestroyImmediate(p.gameObject);
            go.transform.SetParent(null, false);
            go.SetActive(true);
            return go;
        }

        GameObject Mannequin(Transform holder, Material body)
        {
            GameObject m = Instantiate(previewCharacter.gameObject, holder);
            foreach (PreviewCosmetics pc in m.GetComponentsInChildren<PreviewCosmetics>(true)) DestroyImmediate(pc);
            Transform prop = null;
            foreach (Transform t in m.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("HeadProp_")) prop = t;
            if (prop != null) DestroyImmediate(prop.gameObject);
            m.transform.localScale = Vector3.one;
            Transform bodyMesh = Find(m.transform, "BodyMesh");
            if (body != null && bodyMesh != null) bodyMesh.GetComponent<Renderer>().sharedMaterial = body;
            return Strip(m);
        }

        GameObject HandModel(Transform holder, Material material)
        {
            GameObject h = Instantiate(_hands[1].gameObject, holder);
            if (material != null) h.GetComponent<Renderer>().sharedMaterial = material;
            h.transform.localScale = Vector3.one;
            return Strip(h);
        }

        void RefreshPreview()
        {
            if (_cosmetics == null) return;
            // The preview tries on the selection; other slots show what is equipped.
            CosmeticItem Pick(WardrobeSlot slot)
            {
                bool tryOn = State.Screen == ConceptScreen.Customize && State.Tab == CustomizeTab.Wardrobe;
                return tryOn ? State.SelectedItem(slot) : State.EquippedItem(slot);
            }
            _cosmetics.Show(Pick(WardrobeSlot.Head), Pick(WardrobeSlot.Body), Pick(WardrobeSlot.Hands));
        }

        // ------------------------------------------------------------------ state

        void OnStateChanged()
        {
            _wantCustomizeEnvironment = State.Screen == ConceptScreen.Customize;
            if (_wantCustomizeEnvironment != _customizeEnvironment && _fadePhase == 0) _fadePhase = 1;
            AudioListener.volume = State.MasterVolume;
            RefreshPreview();
            if (_active >= 0) _views[_active].Refresh();
        }

        public void ShowConcept(int index)
        {
            if (index == _active || index < 0 || index >= _views.Count) return;
            if (_active >= 0) _views[_active].Canvas.gameObject.SetActive(false);
            _active = index;
            _views[_active].Canvas.gameObject.SetActive(true);
            ApplyFraming(_views[_active]);
            _views[_active].Refresh();
            _views[_active].OnShown();
            EventSystem.current?.SetSelectedGameObject(null);

            for (int i = 0; i < _switchLabels.Count; i++)
            {
                bool on = i == _active;
                _switchLabels[i].color = on ? new Color(0.1f, 0.09f, 0.08f) : new Color(1f, 1f, 1f, 0.7f);
                _switchBacks[i].color = on ? new Color(0.94f, 0.9f, 0.82f, 0.95f) : new Color(1f, 1f, 1f, 0.06f);
            }
        }

        /// <summary>Aims the Customize camera and turns the character the way this concept's layout wants.</summary>
        void ApplyFraming(ConceptView view)
        {
            if (customizeCamera == null || turntable == null) return;
            Transform cam = customizeCamera.transform;
            Vector3 pivot = turntable.transform.position;
            cam.LookAt(pivot + Vector3.up * view.PreviewLookHeight);
            cam.Rotate(0f, view.PreviewYaw, 0f, Space.World);
            customizeCamera.fieldOfView = view.PreviewFov;

            Vector3 toCam = cam.position - pivot;
            toCam.y = 0f;
            float facing = Quaternion.LookRotation(toCam).eulerAngles.y + view.PreviewTurn;
            if (turntable.transform.parent != null) facing -= turntable.transform.parent.eulerAngles.y;
            turntable.SetRestYaw(facing, true);
        }

        public void Toast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toastTimer = 2.4f;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && !IsTyping())
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ShowConcept(0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) ShowConcept(1);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ShowConcept(2);
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) ShowConcept(3);
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) ShowConcept(4);
                if (kb.hKey.wasPressedThisFrame && _switchBar != null) _switchBar.SetActive(!_switchBar.activeSelf);
                if (kb.f12Key.wasPressedThisFrame) CaptureBoth();
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();

            UpdateFade();

            if (_toastGroup != null)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                _toastGroup.alpha = Mathf.Clamp01(_toastTimer / 0.3f);
            }
        }

        static bool IsTyping()
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            var input = sel.GetComponent<TMP_InputField>();
            return input != null && input.isFocused;
        }

        public void Back()
        {
            if (State.Panel != MenuPanel.None) { State.Panel = MenuPanel.None; State.JoinMessage = string.Empty; State.Notify(); return; }
            if (State.Screen != ConceptScreen.MainMenu) { State.Screen = ConceptScreen.MainMenu; State.Notify(); }
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
            if (_fade != null)
            {
                _fade.color = new Color(0, 0, 0, _fadeAlpha);
                _fade.raycastTarget = _fadeAlpha > 0.5f;
            }
        }

        void ApplyEnvironment(bool customize, bool force)
        {
            if (!force && customize == _customizeEnvironment) return;
            _customizeEnvironment = customize;
            if (menuSet != null) menuSet.SetActive(!customize);
            if (tavernSet != null) tavernSet.SetActive(customize);
            if (menuCamera != null) menuCamera.enabled = !customize;
            if (customizeCamera != null) customizeCamera.enabled = customize;
            RenderSettings.ambientSkyColor = customize ? TavernSky : MenuSky;
            RenderSettings.ambientEquatorColor = customize ? TavernEq : MenuEq;
            RenderSettings.ambientGroundColor = customize ? TavernGround : MenuGround;
        }

        /// <summary>Environment switch without the fade, for captures.</summary>
        public void SnapEnvironment()
        {
            _fadePhase = 0;
            _fadeAlpha = 0f;
            ApplyEnvironment(State.Screen == ConceptScreen.Customize, false);
            UpdateFade();
        }

        // ------------------------------------------------------------------ overlay

        void BuildOverlay()
        {
            _overlay = MakeCanvas("ConceptSwitcherOverlay", 100);
            RectTransform root = (RectTransform)_overlay.transform;
            TMP_FontAsset ui = TMP_Settings.defaultFontAsset;

            _fade = K.Img(root, "EnvironmentFade", ConceptSprites.White(), new Color(0, 0, 0, 0));
            _fade.rectTransform.Fill();

            // Prototype switcher: deliberately plain, so it never reads as part of a concept.
            string[] names = { "1  CANDLELIGHT", "2  CANDLELIGHT SHOWROOM", "3  CANDLELIGHT OLD", "4  CANDLELIGHT SHOWROOM OLD", "5  MAIN" };
            float[] widths = { 152, 266, 196, 310, 96 };
            float barW = 124 + 12;
            foreach (float w in widths) barW += w + 6;
            RectTransform bar = K.Node(root, "SwitcherBar").At(1, 1, 1, 1, -20, -18, barW, 40);
            _switchBar = bar.gameObject;
            K.Img(bar, "Back", ConceptSprites.RoundRect(10), new Color(0, 0, 0, 0.55f)).rectTransform.Fill();
            TextMeshProUGUI tag = K.Text(bar, "Tag", "UI CONCEPT", ui, 15, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.MidlineLeft);
            tag.rectTransform.TL(14, 0, 110, 40);
            tag.characterSpacing = 6;
            float x = 118;
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float w = widths[i];
                Button b = K.Button(bar, "Concept" + i, () => ShowConcept(index), out _);
                b.GetComponent<RectTransform>().TL(x, 6, w, 28);
                Image back = K.Img(b.transform, "Back", ConceptSprites.RoundRect(8), Color.white);
                back.rectTransform.Fill();
                TextMeshProUGUI label = K.Text(b.transform, "Label", names[i], ui, 15, Color.white, TextAlignmentOptions.Center);
                label.rectTransform.Fill();
                label.fontStyle = FontStyles.Bold;
                _switchBacks.Add(back);
                _switchLabels.Add(label);
                x += w + 6;
            }
            TextMeshProUGUI hint = K.Text(bar, "Hint", "ESC BACK  ·  H HIDE  ·  F12 CAPTURE", ui, 12, new Color(1, 1, 1, 0.4f), TextAlignmentOptions.Right);
            hint.rectTransform.At(1, 0, 1, 1, -6, -6, 400, 18);

            RectTransform toast = K.Node(root, "Toast").At(0.5f, 0, 0.5f, 0, 0, 40, 900, 44);
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            K.Img(toast, "Back", ConceptSprites.RoundRect(12), new Color(0, 0, 0, 0.7f)).rectTransform.Fill();
            _toast = K.Text(toast, "Text", string.Empty, ui, 18, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.Center);
            _toast.rectTransform.Fill();
        }

        // ------------------------------------------------------------------ capture

        void CaptureBoth()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Screenshots", "UIConcept");
            string name = _views[_active].Code + "_" + State.Screen + (State.Panel != MenuPanel.None ? "_" + State.Panel : "") + (State.Screen == ConceptScreen.Customize ? "_" + State.Tab : "");
            Capture(Path.Combine(dir, name + "_1080p.png"), 1920, 1080);
            Capture(Path.Combine(dir, name + "_1440p.png"), 2560, 1440);
            Toast("SAVED " + name + " TO /Screenshots/UIConcept");
        }

        /// <summary>
        /// Renders the active environment camera plus the active concept UI at an exact
        /// resolution, independent of the Game view size. The UI is rendered on its own
        /// camera twice, over black and over white, which gives its exact coverage; it is
        /// then composited over the scene the way an overlay canvas would be.
        /// </summary>
        public string Capture(string path, int width, int height)
        {
            SnapEnvironment();
            Camera sceneCam = _customizeEnvironment ? customizeCamera : menuCamera;
            Canvas canvas = _views[_active].Canvas;

            Color[] scene = RenderToPixels(sceneCam, width, height);

            var uiGo = new GameObject("__CaptureUICamera");
            var uiCam = uiGo.AddComponent<Camera>();
            uiCam.cullingMask = 1 << 5;
            uiCam.clearFlags = CameraClearFlags.SolidColor;
            uiCam.allowHDR = false;
            uiCam.allowMSAA = false;
            uiGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 1f;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.enabled = false; scaler.enabled = true;
            Canvas.ForceUpdateCanvases();

            uiCam.backgroundColor = Color.black;
            Color[] overBlack = RenderToPixels(uiCam, width, height);
            uiCam.backgroundColor = Color.white;
            Color[] overWhite = RenderToPixels(uiCam, width, height);

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = false; scaler.enabled = true;
            DestroyImmediate(uiGo);
            Canvas.ForceUpdateCanvases();

            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var result = new Color[scene.Length];
            for (int i = 0; i < scene.Length; i++)
            {
                // Over black the UI gives its own colour U; over white it gives U + (1 - coverage).
                Color s = Lin(scene[i], linear), b = Lin(overBlack[i], linear), w = Lin(overWhite[i], linear);
                result[i] = Gam(new Color(
                    b.r + Mathf.Clamp01(w.r - b.r) * s.r,
                    b.g + Mathf.Clamp01(w.g - b.g) * s.g,
                    b.b + Mathf.Clamp01(w.b - b.b) * s.b, 1f), linear);
            }

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.SetPixels(result);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            return path;
        }

        static Color Lin(Color c, bool linear) { return linear ? c.linear : c; }
        static Color Gam(Color c, bool linear) { return linear ? c.gamma : c; }

        static Color[] RenderToPixels(Camera cam, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previousTarget;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Color[] px = tex.GetPixels();
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            return px;
        }
    }
}
