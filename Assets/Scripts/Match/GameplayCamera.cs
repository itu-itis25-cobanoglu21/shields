using UnityEngine;

namespace Shields.Match
{
    /// <summary>
    /// The single gameplay camera in the scene. It is parked at an overview pose until a
    /// match starts, then reparented onto the local player's camera anchor.
    ///
    /// Nothing here knows which player is local. The match bootstrap decides that and
    /// passes the anchor in, so the camera system stays free of any player 0 assumption.
    ///
    /// While attached it can layer two short view effects on top of the anchor: a recoil
    /// kick (the view flips up and eases back) and an impact shake (a quick decaying
    /// jitter). They only rotate the camera under its anchor, never the head, so the head
    /// pose other machines see is untouched. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class GameplayCamera : MonoBehaviour
    {
        [Header("View Effects")]
        [Tooltip("How fast the view snaps up into a recoil kick.")]
        [SerializeField] float kickSharpness = 45f;

        [Tooltip("How fast a recoil kick eases back to rest.")]
        [SerializeField] float kickRecovery = 9f;

        [Tooltip("Jitter speed of an impact shake.")]
        [SerializeField] float shakeFrequency = 22f;

        Transform _parkedParent;
        Vector3 _parkedPosition;
        Quaternion _parkedRotation;
        bool _parkedPoseStored;

        // Recoil: the target eases back to zero, the applied kick chases it.
        Vector2 _kickTarget, _kick;

        float _shakeAmplitude, _shakeStart, _shakeDuration;
        float _shakeSeed;

        public Transform AttachedAnchor { get; private set; }

        /// <summary>A recoil kick: the view flips up by <paramref name="pitchUp"/> degrees (and sideways by <paramref name="yaw"/>), then eases back.</summary>
        public void Kick(float pitchUp, float yaw)
        {
            _kickTarget += new Vector2(pitchUp, yaw);
        }

        /// <summary>A decaying jitter of up to <paramref name="degrees"/>, over <paramref name="duration"/> seconds. A weaker shake never cuts a stronger one short.</summary>
        public void Shake(float degrees, float duration)
        {
            if (degrees <= 0f || duration <= 0f) return;
            if (ShakeAmplitudeNow() > degrees) return;

            _shakeAmplitude = degrees;
            _shakeDuration = duration;
            _shakeStart = Time.time;
            _shakeSeed = Random.Range(0f, 100f);
        }

        float ShakeAmplitudeNow()
        {
            float t = Time.time - _shakeStart;
            if (t < 0f || t >= _shakeDuration) return 0f;
            float fall = 1f - t / _shakeDuration;
            return _shakeAmplitude * fall * fall;
        }

        void LateUpdate()
        {
            if (AttachedAnchor == null) return;

            float dt = Time.deltaTime;
            _kickTarget = Vector2.Lerp(_kickTarget, Vector2.zero, 1f - Mathf.Exp(-kickRecovery * dt));
            _kick = Vector2.Lerp(_kick, _kickTarget, 1f - Mathf.Exp(-kickSharpness * dt));

            Vector3 shake = Vector3.zero;
            float amplitude = ShakeAmplitudeNow();
            if (amplitude > 0f)
            {
                float t = (Time.time - _shakeStart) * shakeFrequency;
                shake = new Vector3(
                    Mathf.PerlinNoise(_shakeSeed, t) * 2f - 1f,
                    Mathf.PerlinNoise(_shakeSeed + 31f, t) * 2f - 1f,
                    (Mathf.PerlinNoise(_shakeSeed + 67f, t) * 2f - 1f) * 0.6f) * amplitude;
            }

            transform.localRotation = Quaternion.Euler(-_kick.x + shake.x, _kick.y + shake.y, shake.z);
        }

        void ClearEffects()
        {
            _kickTarget = Vector2.zero;
            _kick = Vector2.zero;
            _shakeAmplitude = 0f;
            _shakeDuration = 0f;
        }

        void Awake()
        {
            StoreParkedPose();
        }

        void StoreParkedPose()
        {
            if (_parkedPoseStored) return;

            _parkedParent = transform.parent;
            _parkedPosition = transform.position;
            _parkedRotation = transform.rotation;
            _parkedPoseStored = true;
        }

        /// <summary>Reparents the camera onto a player's camera anchor with no offset.</summary>
        public void AttachTo(Transform anchor)
        {
            if (anchor == null) return;

            StoreParkedPose();
            ClearEffects();

            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            AttachedAnchor = anchor;
        }

        /// <summary>Returns the camera to its overview pose so no player owns it.</summary>
        public void Detach()
        {
            if (!_parkedPoseStored) return;

            ClearEffects();
            transform.SetParent(_parkedParent, false);
            transform.SetPositionAndRotation(_parkedPosition, _parkedRotation);
            transform.localScale = Vector3.one;

            AttachedAnchor = null;
        }
    }
}
