using UnityEngine;
using UnityEngine.EventSystems;

namespace Shields.Menu
{
    /// <summary>
    /// Spins the Customize preview character around its vertical axis while the mouse
    /// drags across the preview area, with a short smoothing tail. Letting go stops it;
    /// there is no free spin. Adds a very small idle breath. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterTurntable : MonoBehaviour
    {
        [Tooltip("Degrees of yaw per pixel of horizontal drag.")]
        [SerializeField] float degreesPerPixel = 0.42f;

        [Tooltip("Seconds the yaw takes to catch up with the drag.")]
        [SerializeField] float smoothing = 0.07f;

        [SerializeField] Transform breathingTarget;
        [SerializeField] float breathAmount = 0.012f;
        [SerializeField] float breathSeconds = 3.6f;

        float _restYaw;
        float _targetYaw;
        float _yaw;
        float _velocity;
        bool _hasRest;
        Vector3 _breathRest = Vector3.one;

        void Awake()
        {
            // The controller may already have set the facing before this wakes up.
            if (!_hasRest)
            {
                _restYaw = transform.localEulerAngles.y;
                _targetYaw = _yaw = _restYaw;
            }
            if (breathingTarget != null) _breathRest = breathingTarget.localScale;
        }

        public void SetBreathingTarget(Transform target)
        {
            breathingTarget = target;
            if (target != null) _breathRest = target.localScale;
        }

        /// <summary>Changes the resting facing (local yaw); the character turns to it smoothly unless snapped.</summary>
        public void SetRestYaw(float yaw, bool snap)
        {
            _hasRest = true;
            _restYaw = yaw;
            _targetYaw = yaw;
            if (snap) { _yaw = yaw; _velocity = 0f; }
        }

        public void Drag(float pixels) { _targetYaw -= pixels * degreesPerPixel; }
        public void ResetView() { _targetYaw = _restYaw + Mathf.Round((_yaw - _restYaw) / 360f) * 360f; }

        void LateUpdate()
        {
            _yaw = Mathf.SmoothDamp(_yaw, _targetYaw, ref _velocity, smoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            Vector3 e = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(e.x, _yaw, e.z);

            if (breathingTarget == null) return;
            float s = Mathf.Sin(Time.unscaledTime / Mathf.Max(0.1f, breathSeconds) * Mathf.PI * 2f);
            breathingTarget.localScale = new Vector3(_breathRest.x * (1f - s * breathAmount * 0.4f), _breathRest.y * (1f + s * breathAmount), _breathRest.z * (1f - s * breathAmount * 0.4f));
        }
    }

    /// <summary>
    /// Invisible UI rect over the preview area that forwards drags to the turntable.
    /// Sits behind the other UI, so buttons on top keep working. Double-click resets the view.
    /// </summary>
    public sealed class CharacterDragZone : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public CharacterTurntable Turntable;
        public System.Action<bool> DragChanged;

        public void OnBeginDrag(PointerEventData e)
        {
            if (Turntable == null || e.button != PointerEventData.InputButton.Left) return;
            DragChanged?.Invoke(true);
        }

        public void OnDrag(PointerEventData e)
        {
            if (Turntable == null || e.button != PointerEventData.InputButton.Left) return;
            // Canvas-scaled delta, so the speed is the same at every resolution.
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Turntable.Drag(e.delta.x / Mathf.Max(0.01f, scale));
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (Turntable == null) return;
            DragChanged?.Invoke(false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (Turntable != null && e.clickCount == 2) Turntable.ResetView();
        }
    }
}
