using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shields.UIConcept
{
    /// <summary>
    /// Base for one visual direction. A concept builds its own canvas once, registers
    /// small "bind" callbacks that copy the shared state onto its widgets, and calls the
    /// shared actions below. Every concept drives the same <see cref="UIConceptState"/>.
    /// </summary>
    public abstract class ConceptView
    {
        public abstract string Code { get; }
        public Canvas Canvas { get; private set; }

        public UIConceptDirector D;
        public UIConceptState S;
        public RectTransform Root;

        public RectTransform MenuScreen;
        public RectTransform CustomizeScreen;
        public RectTransform SettingsScreen;

        readonly List<Action> _binds = new List<Action>();
        readonly List<ConceptFx> _fx = new List<ConceptFx>();

        protected TMP_FontAsset Bowlby { get { return D.Bowlby; } }

        public void Init(UIConceptDirector director, Canvas canvas)
        {
            D = director;
            S = director.State;
            Canvas = canvas;
            Root = (RectTransform)canvas.transform;

            MenuScreen = K.Node(Root, "MainMenu"); MenuScreen.Fill();
            CustomizeScreen = K.Node(Root, "Customize"); CustomizeScreen.Fill();
            SettingsScreen = K.Node(Root, "Settings"); SettingsScreen.Fill();

            Build();
        }

        protected abstract void Build();

        public void Refresh()
        {
            K.SetActive(MenuScreen, S.Screen == ConceptScreen.MainMenu);
            K.SetActive(CustomizeScreen, S.Screen == ConceptScreen.Customize);
            K.SetActive(SettingsScreen, S.Screen == ConceptScreen.Settings);
            foreach (Action bind in _binds) bind();
        }

        public void OnShown()
        {
            foreach (ConceptFx fx in _fx) if (fx != null && fx.isActiveAndEnabled) fx.Snap();
        }

        public void Bind(Action bind) { _binds.Add(bind); }

        public ConceptFx Track(ConceptFx fx) { _fx.Add(fx); return fx; }

        /// <summary>A button whose look is entirely defined by <paramref name="apply"/>.</summary>
        public Button Btn(Transform parent, string name, Action onClick, Action<ConceptFx> apply, out ConceptFx fx)
        {
            Button b = K.Button(parent, name, onClick, out fx);
            fx.Apply = apply;
            Track(fx);
            return b;
        }

        /// <summary>
        /// An invisible driver that runs <paramref name="tick"/> every frame with a smoothed
        /// 0..1 value following <paramref name="target"/>. Used for screen-level animation.
        /// </summary>
        public ConceptFx Animator(Transform parent, string name, Func<bool> target, Action<float> tick, float speed = 7f)
        {
            RectTransform rt = K.Node(parent, name);
            var fx = rt.gameObject.AddComponent<ConceptFx>();
            fx.Speed = speed;
            fx.Apply = f => { f.Selected = target(); tick(ConceptFx.Ease(f.Select)); };
            Track(fx);
            return fx;
        }

        // ------------------------------------------------------------------ brand

        /// <summary>
        /// The SHIELDS! word mark in Knewave: a red gradient face with a bone inline, a
        /// heavy dark outer stroke and a hard extruded drop. Concepts tune its colours.
        /// </summary>
        public RectTransform Logo(Transform parent, string name, float size, TextAlignmentOptions align = TextAlignmentOptions.Left,
            string top = "#E2492F", string bottom = "#9A1A20", Material faceMaterial = null, Material strokeMaterial = null)
        {
            RectTransform root = K.Node(parent, name);
            Material back = strokeMaterial != null ? strokeMaterial : D.KnewaveLogoStroke != null ? D.KnewaveLogoStroke : K.TextMat(D.Knewave, "LogoBack", 0.42f, new Color(0.09f, 0.03f, 0.03f, 1f),
                new Color(0.05f, 0.015f, 0.015f, 0.95f), 0.9f, -1.1f, 0f, 0.42f, 0.18f);
            TextMeshProUGUI shadow = K.Text(root, "Stroke", "SHIELDS!", D.Knewave, size, new Color(0.09f, 0.03f, 0.03f), align, back);
            shadow.rectTransform.Fill();
            Material faceMat = faceMaterial != null ? faceMaterial : D.KnewaveLogo != null ? D.KnewaveLogo
                : K.TextMat(D.Knewave, "LogoFace", 0.16f, new Color(0.96f, 0.88f, 0.74f), default, 0, 0, 0, 0, 0.06f);
            TextMeshProUGUI face = K.Text(root, "Face", "SHIELDS!", D.Knewave, size, Color.white, align, faceMat);
            face.rectTransform.Fill();
            face.enableVertexGradient = true;
            face.colorGradient = new VertexGradient(K.Hex(top), K.Hex(top), K.Hex(bottom), K.Hex(bottom));
            shadow.characterSpacing = face.characterSpacing = -2f;
            return root;
        }

        // ------------------------------------------------------------------ preview

        // Where the Customize camera puts the character for this concept: camera yaw
        // offset from looking straight at it (+ puts it left of centre), the character's
        // resting turn, the height the camera aims at, and field of view.
        public virtual float PreviewYaw { get { return 17.5f; } }
        public virtual float PreviewTurn { get { return 18f; } }
        public virtual float PreviewLookHeight { get { return 0.84f; } }
        public virtual float PreviewFov { get { return 44f; } }

        /// <summary>
        /// Invisible drag surface over the character preview, spanning x .. x+width across
        /// the full height. Add it first so it sits behind the rest of the UI.
        /// </summary>
        public PreviewDragZone DragZone(Transform parent, Action<bool> dragChanged, float x = 0, float width = 1000)
        {
            Image img = K.Img(parent, "PreviewDragZone", null, new Color(0, 0, 0, 0), true);
            img.rectTransform.At(0, 0, 0, 0, x, 0, width, 1080);
            img.rectTransform.anchorMax = new Vector2(0, 1);
            img.rectTransform.sizeDelta = new Vector2(width, 0);
            var zone = img.gameObject.AddComponent<PreviewDragZone>();
            zone.Turntable = D.Turntable;
            zone.DragChanged = dragChanged;
            return zone;
        }

        // ------------------------------------------------------------------ actions

        public void GoMenu() { S.Screen = ConceptScreen.MainMenu; S.Panel = MenuPanel.None; S.Notify(); }
        public void GoCustomize() { S.Screen = ConceptScreen.Customize; S.Panel = MenuPanel.None; S.Notify(); }
        public void GoSettings() { S.Screen = ConceptScreen.Settings; S.Panel = MenuPanel.None; S.Notify(); }

        public void Exit() { D.Toast("EXIT  ·  DISABLED IN THE CONCEPT SCENE"); }

        /// <summary>Unfolds a main menu panel (Offline Mode, Create Room) under its entry.</summary>
        public void OpenPanel(MenuPanel panel)
        {
            if (S.Panel == panel) return;
            S.Panel = panel;
            S.JoinMessage = string.Empty;
            S.Notify();
        }

        public void ClosePanel()
        {
            S.Panel = MenuPanel.None;
            S.JoinMessage = string.Empty;
            S.Notify();
        }

        public void OpenJoin(TMP_InputField input)
        {
            if (S.Panel == MenuPanel.Join)
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

        static string TableLine(UIConceptState s)
        {
            return s.TablePlayers + " PLAYERS, " + s.TableDecks + (s.TableDecks == 1 ? " DECK" : " DECKS");
        }

        public void ConfirmOffline()
        {
            D.Toast("OFFLINE  ·  " + TableLine(S) + "  ·  WOULD DEAL AND START (CONCEPT ONLY)");
        }

        public void ConfirmCreate()
        {
            D.Toast("CREATE ROOM  ·  " + TableLine(S) + "  ·  WOULD HOST AND SHOW THE ROOM CODE (CONCEPT ONLY)");
        }

        public void ConfirmJoin(TMP_InputField input)
        {
            string code = input != null ? input.text.Replace(" ", string.Empty).Trim().ToUpperInvariant() : string.Empty;
            if (code.Length == 0)
            {
                S.JoinMessage = "ENTER A ROOM CODE";
                S.Notify();
                return;
            }
            S.JoinMessage = string.Empty;
            S.Notify();
            D.Toast("JOIN ROOM " + code + "  ·  WOULD CONNECT (CONCEPT ONLY)");
        }

        public void SetupJoinInput(TMP_InputField input)
        {
            input.characterLimit = 16;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            input.onValueChanged.AddListener(v =>
            {
                string up = v.ToUpperInvariant();
                if (up != v) input.SetTextWithoutNotify(up);
                if (S.JoinMessage.Length > 0) { S.JoinMessage = string.Empty; S.Notify(); }
            });
            input.onSubmit.AddListener(_ => ConfirmJoin(input));
        }

        public void SetupNickname(TMP_InputField input)
        {
            input.characterLimit = 16;
            input.text = S.Nickname;
            input.onValueChanged.AddListener(v =>
            {
                string up = v.ToUpperInvariant();
                if (up != v) input.SetTextWithoutNotify(up);
            });
            input.onEndEdit.AddListener(v =>
            {
                string clean = v.Trim().ToUpperInvariant();
                if (clean.Length == 0) clean = S.Nickname;
                S.Nickname = clean;
                input.SetTextWithoutNotify(clean);
                S.Notify();
            });
            Bind(() => { if (!input.isFocused && input.text != S.Nickname) input.SetTextWithoutNotify(S.Nickname); });
        }

        public void SetTab(CustomizeTab tab) { S.Tab = tab; S.Notify(); }
        public void SetSlot(WardrobeSlot slot) { S.Slot = slot; S.Notify(); }

        public List<CosmeticItem> SlotItems(WardrobeSlot slot)
        {
            return new List<CosmeticItem>(S.ItemsIn(slot));
        }
    }
}
