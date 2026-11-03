using Shields.Settings;
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

        bool _inputSuppressed;
        int _suppressionEndedFrame = -1;
        bool _pitchLocked;

        bool _aimLocked;
        Vector3 _aimPoint;

        public Transform HeadPivot { get { return headPivot; } }

        /// <summary>True while the head is held on a world point (the player's own shot).</summary>
        public bool AimLocked { get { return _aimLocked; } }

        /// <summary>
        /// Turns the head to face a world point and holds it there, ignoring look input,
        /// until <see cref="ReleaseAim"/>. Call again whenever the point moves. The seated
        /// limits still apply. The head turns with the usual smoothing, and the turn is
        /// part of the head pose, so other machines see it too. Presentation only.
        /// </summary>
        public void LockAim(Vector3 worldPoint)
        {
            _aimLocked = true;
            _aimPoint = worldPoint;
        }

        /// <summary>Gives the head back to look input, from where it points now: nothing snaps back.</summary>
        public void ReleaseAim()
        {
            _aimLocked = false;
        }

        /// <summary>True while vertical look is frozen (drinking). Yaw keeps working.</summary>
        public bool PitchLocked { get { return _pitchLocked; } }

        /// <summary>
        /// Freezes vertical look at the pitch the head has right now, without touching yaw,
        /// the cursor or this component's enabled state. Unlocking simply lets mouse pitch
        /// through again from that same pitch, so there is nothing to snap back to.
        /// </summary>
        public void SetPitchLocked(bool locked)
        {
            if (locked && !_pitchLocked) _targetPitch = _pitch;

            _pitchLocked = locked;
        }

        /// <summary>True while a menu owns the mouse and this look ignores input.</summary>
        public bool InputSuppressed { get { return _inputSuppressed; } }

        /// <summary>
        /// Lets a small menu borrow the mouse without disabling this component, which
        /// would reset the look and stop head pose replication. While suppressed the head
        /// keeps its pose, reads no mouse delta, and leaves the cursor alone. Input stays
        /// ignored for the rest of the frame suppression ends in, so the key or click that
        /// closed the menu is not also read as a look or cursor toggle.
        /// </summary>
        public void SetInputSuppressed(bool suppressed)
        {
            if (_inputSuppressed && !suppressed) _suppressionEndedFrame = Time.frameCount;

            _inputSuppressed = suppressed;
        }

        /// <summary>Current look offset from the neutral seated direction, in degrees.</summary>
        public Vector2 LookAngles { get { return new Vector2(_yaw, _pitch - restPitch); } }

        /// <summary>
        /// The yaw and pitch actually written to the pivot, in degrees. Unlike
        /// <see cref="LookAngles"/> this is not relative to <see cref="restPitch"/>, so it
        /// can be sent to another machine and applied verbatim: the pivot is a child of an
        /// already seat oriented transform, making these angles seat independent.
        /// </summary>
        public Vector2 PivotAngles { get { return new Vector2(_yaw, _pitch); } }

        /// <summary>
        /// Points a pivot at a yaw and pitch received from its owning machine. Used for
        /// remote players only, whose own <see cref="SeatedHeadLook"/> is disabled, so this
        /// never competes with local input.
        /// </summary>
        public static void ApplyPivotAngles(Transform pivot, float yaw, float pitch)
        {
            if (pivot == null) return;

            pivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

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
            _aimLocked = false;
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

            if (_pitchLocked) return;

            // Positive pitch looks down, so moving the pointer up must lower it.
            float pitchDelta = pixelDelta.y * degreesPerPixel * (invertY ? -1f : 1f);
            _targetPitch = Mathf.Clamp(_targetPitch - pitchDelta,
                restPitch - maxLookUp, restPitch + maxLookDown);
        }

        void Update()
        {
            bool inputBlocked = _inputSuppressed || Time.frameCount == _suppressionEndedFrame;

            if (Application.isPlaying && !inputBlocked)
            {
                HandleCursorToggle();

                if (!_aimLocked && (allowLookWhileUnlocked || Cursor.lockState == CursorLockMode.Locked))
                    ApplyLookDelta(ReadLookDelta() * GameSettings.MouseSensitivity);
            }

            if (_aimLocked) AimTargetsAt(_aimPoint);

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

        /// <summary>
        /// Sets the look targets so the eye (the camera, when it rides on this head) looks
        /// straight at a world point. The eye sits off the pivot, so the angles are refined
        /// a few times from the eye's position at the previous guess.
        /// </summary>
        void AimTargetsAt(Vector3 worldPoint)
        {
            if (headPivot == null) return;

            Transform reference = headPivot.parent;
            Vector3 target = reference != null ? reference.InverseTransformPoint(worldPoint) : worldPoint;
            Vector3 pivot = headPivot.localPosition;
            Vector3 eye = EyeOffset();

            float yaw = _targetYaw, pitch = _targetPitch;
            for (int i = 0; i < 3; i++)
            {
                Vector3 toTarget = target - (pivot + Quaternion.Euler(pitch, yaw, 0f) * eye);
                if (toTarget.sqrMagnitude < 0.000001f) return;

                toTarget.Normalize();
                yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                pitch = -Mathf.Asin(Mathf.Clamp(toTarget.y, -1f, 1f)) * Mathf.Rad2Deg;
            }

            _targetYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            _targetPitch = Mathf.Clamp(pitch, restPitch - maxLookUp, restPitch + maxLookDown);
        }

        /// <summary>The camera's position in the pivot's own space, or zero when no camera rides on this head.</summary>
        Vector3 EyeOffset()
        {
            Camera view = Camera.main;
            if (view == null || !view.transform.IsChildOf(headPivot)) return Vector3.zero;
            return headPivot.InverseTransformPoint(view.transform.position);
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
