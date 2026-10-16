using UnityEngine;

namespace Shields.Menu
{
    /// <summary>
    /// Barely-there "breathing" for the main menu camera, so the diorama feels alive while
    /// the composition stays locked.
    ///
    /// Moves only along the camera's own up and forward axes plus a hair of pitch, as smooth
    /// sines over one cycle. There is no sideways term, so it never pans. Every frame is
    /// computed fresh from the pose captured at start, so nothing can accumulate or drift.
    ///
    /// Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuCameraBreathing : MonoBehaviour
    {
        [Tooltip("Up/down travel along the camera's own Y axis, in metres.")]
        [SerializeField] float heightAmplitude = 0.007f;

        [Tooltip("Forward/back travel along the camera's own Z axis, in metres.")]
        [SerializeField] float depthAmplitude = 0.014f;

        [Tooltip("Pitch swing in degrees.")]
        [SerializeField] float pitchAmplitude = 0.07f;

        [Tooltip("Seconds for one full breath.")]
        [SerializeField] float cycleSeconds = 10f;

        Vector3 _restPosition;
        Quaternion _restRotation;

        void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        void OnDisable()
        {
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
        }

        void LateUpdate()
        {
            // Unscaled, so the menu keeps breathing even if something pauses time.
            float phase = Time.unscaledTime / Mathf.Max(0.1f, cycleSeconds) * Mathf.PI * 2f;

            // The three motions run slightly out of step, so it never reads as a simple bob.
            float rise = Mathf.Sin(phase);
            float push = Mathf.Sin(phase + 1.1f);
            float tilt = Mathf.Sin(phase + 0.6f);

            Vector3 offset = _restRotation * new Vector3(0f, rise * heightAmplitude, push * depthAmplitude);
            transform.localPosition = _restPosition + offset;
            transform.localRotation = _restRotation * Quaternion.Euler(tilt * pitchAmplitude, 0f, 0f);
        }
    }
}
