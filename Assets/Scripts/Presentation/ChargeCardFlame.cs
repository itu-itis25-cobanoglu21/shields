using System.Collections.Generic;
using UnityEngine;

namespace Shields.Presentation
{
    /// <summary>
    /// A stored Charge card burning across its top face.
    ///
    /// One fire, drawn by the "Shields/Charge Card Flame Volume" shader inside a box that
    /// stands on the card face: the box's footprint is the card face and its height is the
    /// flame height. The fire is raymarched in card-local space, so it is a real volume,
    /// not a billboard: every seat sees the same flame in the same place on the card, and
    /// nothing is drawn outside the card's footprint.
    ///
    /// Presentation only: no collider, no gameplay data, and it is destroyed with its card.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChargeCardFlame : MonoBehaviour
    {
        [Tooltip("Material using the Shields/Charge Card Flame Volume shader. Colours and motion live there.")]
        [SerializeField] Material flameMaterial;

        [Header("Card Face (card-local units)")]
        [SerializeField] Vector2 faceSize = new Vector2(0.063f, 0.088f);

        [Tooltip("How high the fire rises above the card face.")]
        [SerializeField] float flameHeight = 0.07f;

        [Header("Startup")]
        [Tooltip("Seconds for the fire to grow from the face to full height.")]
        [SerializeField] float fadeInSeconds = 0.35f;

        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int IgniteId = Shader.PropertyToID("_Ignite");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        MeshRenderer _renderer;
        Mesh _mesh;
        MaterialPropertyBlock _block;

        float _seed;
        float _lit;
        float _litAt = -1f;

        void Awake()
        {
            // Cards burning side by side should not flicker in step.
            _seed = Random.value * 100f;
            Build();
            Apply();
        }

        /// <summary>Starts burning. Settled skips the fade-in, for a Charge nobody saw stored.</summary>
        public void Ignite(bool settled)
        {
            _litAt = settled ? Time.time - fadeInSeconds : Time.time;
            _lit = settled ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            if (_litAt < 0f || _lit >= 1f) return;

            _lit = fadeInSeconds > 0f ? Mathf.Clamp01((Time.time - _litAt) / fadeInSeconds) : 1f;
            Apply();
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void Apply()
        {
            if (_renderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            _block.Clear();
            _block.SetVector(SizeId, new Vector4(faceSize.x, flameHeight, faceSize.y, 0f));
            _block.SetFloat(IgniteId, Mathf.SmoothStep(0f, 1f, _lit));
            _block.SetFloat(SeedId, _seed);
            _renderer.SetPropertyBlock(_block);
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            if (flameMaterial == null) return;

            _mesh = BuildBox(faceSize.x * 0.5f, flameHeight, faceSize.y * 0.5f);

            GameObject child = new GameObject("FlameVolume");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);

            child.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = child.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = flameMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// The box the fire lives in: the card face as its floor, the flame height as its
        /// height. Outward-facing, so only the faces toward the camera are drawn.
        /// </summary>
        static Mesh BuildBox(float hx, float height, float hz)
        {
            Vector3[] corners =
            {
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz),
                new Vector3(-hx, height, -hz), new Vector3(hx, height, -hz), new Vector3(hx, height, hz), new Vector3(-hx, height, hz)
            };

            // Clockwise seen from outside, which is Unity's front face.
            int[][] faces =
            {
                new[] { 4, 7, 6, 5 }, // top
                new[] { 0, 1, 2, 3 }, // bottom
                new[] { 0, 4, 5, 1 }, // -z
                new[] { 2, 6, 7, 3 }, // +z
                new[] { 3, 7, 4, 0 }, // -x
                new[] { 1, 5, 6, 2 }  // +x
            };

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            foreach (int[] face in faces)
            {
                int start = vertices.Count;
                foreach (int corner in face) vertices.Add(corners[corner]);

                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            Mesh mesh = new Mesh { name = "ChargeCardFlameVolume" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
