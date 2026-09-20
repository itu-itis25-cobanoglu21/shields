using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Shields.Players
{
    /// <summary>
    /// Seated look control. This rotates the player's HeadPivot, not the camera, so the
    /// head visual turns with the view and other players can eventually see where
    /// someone is looking. The camera simply rides along under the pivot.
    ///
    /// There is no movement input. The player stays seated and the rotation is clamped
    /// to what a seated person can comfortably turn.
    /// </summary>
    [DisallowMultipleComponent]
    public class SeatedHeadLook : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The pivot this component rotates. Normally the PlayerCharacter HeadPivot.")]
        [SerializeField] Transform headPivot;

        [Header("Aim")]
        [Tooltip("Resting pitch in degrees, positive looks down. Set from the table geometry at spawn.")]
        [Range(-30f, 45f)] public float restPitch = 18f;

        [Header("Sensitivity")]
        [Tooltip("Degrees of rotation per pixel of pointer movement.")]
        [Range(0.01f, 0.5f)] public float degreesPerPixel = 0.08f;

        public bool invertY = false;

        [Tooltip("0 = raw input, higher values smooth the look.")]
        [Range(0f, 30f)] public float smoothing = 12f;

        [Header("Seated Limits")]
        [Tooltip("How far the head can turn left and right from the neutral direction.")]
        [Range(20f, 170f)] public float maxYaw = 120f;

        [Tooltip("How far the head can look up from the neutral direction.")]
        [Range(5f, 80f)] public float maxLookUp = 45f;

        [Tooltip("How far the head can look down from the neutral direction, to read the table.")]
        [Range(5f, 80f)] public float maxLookDown = 45f;

        [Header("Cursor")]
        public bool lockCursorWhileActive = true;

        [Tooltip("Allow looking around while the cursor is unlocked. Useful when testing in the editor.")]
        public bool allowLookWhileUnlocked = false;

        float _yaw;
        float _pitch;
        float _targetYaw;
        float _targetPitch;

        public Transform HeadPivot { get { return headPivot; } }

        /// <summary>Current look offset from the neutral seated direction, in degrees.</summary>
        public Vector2 LookAngles { get { return new Vector2(_yaw, _pitch - restPitch); } }

        void OnEnable()
        {
            ResetLook();

            if (lockCursorWhileActive && Application.isPlaying)
                SetCursorLocked(true);
        }

        void OnDisable()
        {
            if (Application.isPlaying && Cursor.lockState == CursorLockMode.Locked)
                SetCursorLocked(false);
        }

        /// <summary>Points this component at a pivot. Called by the match bootstrap when needed.</summary>
        public void Bind(Transform pivot)
        {
            headPivot = pivot;
            ResetLook();
        }

        /// <summary>
        /// Pitch, in degrees, that a pivot needs so a point offset from it looks at
        /// aimPoint. Used to rest a seated player's view on the table without any
        /// hard coded angle.
        /// </summary>
        public static float NeutralPitchToward(Transform pivot, Vector3 eyePoint, Vector3 aimPoint)
        {
            if (pivot == null) return 0f;

            Transform reference = pivot.parent != null ? pivot.parent : pivot;
            Vector3 toAim = aimPoint - eyePoint;
            if (toAim.sqrMagnitude < 0.000001f) return 0f;

            Vector3 local = reference.InverseTransformDirection(toAim).normalized;

            // Euler(pitch, 0, 0) sends forward to (0, -sin pitch, cos pitch).
            return -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Snaps the head back to the neutral seated direction.</summary>
        [ContextMenu("Reset Look")]
        public void ResetLook()
        {
            _yaw = 0f;
            _targetYaw = 0f;
            _pitch = restPitch;
            _targetPitch = restPitch;
            ApplyRotation();
        }

        /// <summary>
        /// Feeds a raw pointer delta in pixels into the seated look, applying the limits.
        /// Update calls this with mouse input; any other input source can call it directly.
        /// </summary>
        public void ApplyLookDelta(Vector2 pixelDelta)
        {
            _targetYaw = Mathf.Clamp(_targetYaw + pixelDelta.x * degreesPerPixel, -maxYaw, maxYaw);

            // Positive pitch looks down, so moving the pointer up must lower it.
            float pitchDelta = pixelDelta.y * degreesPerPixel * (invertY ? -1f : 1f);
            _targetPitch = Mathf.Clamp(_targetPitch - pitchDelta,
                restPitch - maxLookUp, restPitch + maxLookDown);
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                HandleCursorToggle();

                if (allowLookWhileUnlocked || Cursor.lockState == CursorLockMode.Locked)
                    ApplyLookDelta(ReadLookDelta());
            }

            if (smoothing > 0f)
            {
                float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
                _yaw = Mathf.Lerp(_yaw, _targetYaw, t);
                _pitch = Mathf.Lerp(_pitch, _targetPitch, t);
            }
            else
            {
                _yaw = _targetYaw;
                _pitch = _targetPitch;
            }

            ApplyRotation();
        }

        void ApplyRotation()
        {
            if (headPivot == null) return;

            headPivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.delta.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#else
            return Vector2.zero;
#endif
        }

        void HandleCursorToggle()
        {
            if (!lockCursorWhileActive) return;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
#endif
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
