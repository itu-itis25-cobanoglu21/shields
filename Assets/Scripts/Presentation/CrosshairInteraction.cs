using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Shields.Presentation
{
    /// <summary>
    /// First person world targeting. Casts one ray from the exact centre of the gameplay
    /// camera, draws a small dot there as the crosshair, and reports the primary button.
    /// The mouse keeps driving the seated head look the whole time; aiming is done by
    /// turning the head, never by a free cursor.
    ///
    /// This only answers "what is the local player looking at". Whether that thing may
    /// be interacted with is decided by whoever asks, and ultimately by the match state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public class CrosshairInteraction : MonoBehaviour
    {
        [Tooltip("How far the interaction ray reaches, in meters. Covers the whole table at 8 players.")]
        [SerializeField] float rayDistance = 4f;

        [Tooltip("Layers the interaction ray can hit.")]
        [SerializeField] LayerMask interactionMask = ~0;

        [Header("Crosshair")]
        [Tooltip("Diameter of the centre dot in pixels at 1080p. Scales with screen height.")]
        [SerializeField] float dotSize = 4f;

        [SerializeField] Color dotColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Thin dark ring so the dot stays visible over white cards.")]
        [SerializeField] Color outlineColor = new Color(0f, 0f, 0f, 0.55f);

        Camera _camera;
        bool _lookActiveThisFrame;

        public Camera ViewCamera
        {
            get
            {
                if (_camera == null) _camera = GetComponent<Camera>();
                return _camera;
            }
        }

        /// <summary>The ray through the crosshair, from the centre of the camera.</summary>
        public Ray CenterRay
        {
            get { return ViewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); }
        }

        /// <summary>
        /// True when the cursor was already locked to first person look at the start of
        /// this frame. A click that merely re-locks the cursor never counts as an interaction.
        /// </summary>
        public bool InteractionAllowed { get { return enabled && _lookActiveThisFrame; } }

        void Update()
        {
            // Runs before the head look (execution order), so this is the state the
            // player saw when they clicked, not the state the click produced.
            _lookActiveThisFrame = Cursor.lockState == CursorLockMode.Locked;
        }

        /// <summary>The first thing the crosshair ray hits, if anything.</summary>
        public bool TryGetHit(out RaycastHit hit)
        {
            hit = default;
            if (!enabled || ViewCamera == null) return false;

            return Physics.Raycast(CenterRay, out hit, rayDistance, interactionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// The component of type T on whatever the crosshair is pointing at, or null. Only
        /// the first hit counts, so something in front blocks what is behind it.
        /// </summary>
        public T Target<T>() where T : Component
        {
            RaycastHit hit;
            if (!TryGetHit(out hit)) return null;

            return hit.collider.GetComponentInParent<T>();
        }

        /// <summary>True on the frame the primary interaction button goes down during look.</summary>
        public bool PrimaryPressedThisFrame()
        {
            if (!InteractionAllowed) return false;

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.leftButton.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || ViewCamera == null) return;

            // A freed cursor means a menu has the mouse, so the crosshair steps aside.
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Rect view = ViewCamera.pixelRect;
            float size = Mathf.Max(2f, dotSize * Screen.height / 1080f);

            // pixelRect is bottom up; IMGUI is top down.
            Vector2 center = new Vector2(view.x + view.width * 0.5f, Screen.height - (view.y + view.height * 0.5f));

            DrawDot(center, size + 2f, outlineColor);
            DrawDot(center, size, dotColor);
        }

        static void DrawDot(Vector2 center, float size, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
