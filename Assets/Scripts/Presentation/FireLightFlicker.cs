using UnityEngine;

namespace Shields.Presentation
{
    /// <summary>
    /// Makes a fire-lit Light (torch, candle, hearth, lantern) breathe and crackle by
    /// varying its intensity with smooth noise around the intensity set in the scene.
    ///
    /// Presentation only: it reads nothing from gameplay and drives nothing but its Light.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class FireLightFlicker : MonoBehaviour
    {
        [Tooltip("How far the intensity swings, as a fraction of the intensity set in the scene.")]
        [Range(0f, 0.6f)]
        [SerializeField] float amount = 0.15f;

        [Tooltip("Flicker speed. Higher values look more nervous.")]
        [SerializeField] float speed = 5f;

        Light _light;
        float _baseIntensity;
        float _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            // Each light gets its own noise track so neighbouring fires never pulse in sync.
            _seed = Random.Range(0f, 1000f);
        }

        void OnDisable()
        {
            if (_light != null)
                _light.intensity = _baseIntensity;
        }

        void Update()
        {
            float t = Time.time * speed;
            // A slow breathing layer plus a faster, smaller crackle, both in 0..1.
            float noise = Mathf.PerlinNoise(_seed, t) * 0.7f
                        + Mathf.PerlinNoise(_seed + 37.1f, t * 2.7f) * 0.3f;
            _light.intensity = _baseIntensity * (1f + (noise * 2f - 1f) * amount);
        }
    }
}
