using System.Collections.Generic;
using UnityEngine;

namespace Shields.Presentation
{
    /// <summary>
    /// A stored Charge card burning across its top face.
    ///
    /// Everything is anchored in card-local space, so every seat sees the fire in the same
    /// place on the card. Four layers: a soft glow lying on the face, a bright glowing rim
    /// along the card border, one big flame standing on the card centre, and small embers
    /// rising from random points of the face.
    ///
    /// The flame is a single Vefects fire particle drawn as a vertical billboard: its
    /// root stays on the card centre and it only turns about the card's up axis, so each
    /// seat sees the same whole flame, never edge-on and never shifted.
    ///
    /// Presentation only: no collider, no gameplay data, and it is destroyed with its card.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChargeCardFlame : MonoBehaviour
    {
        [Header("Flame")]
        [Tooltip("The one flame on the card centre: a Vefects fire particle prefab whose pivot is the flame's base. Its own scale sets the flame size.")]
        [SerializeField] GameObject flamePrefab;

        [Header("Materials")]
        [Tooltip("Additive soft glow laid on the card face. Also used for the embers.")]
        [SerializeField] Material glowMaterial;

        [Tooltip("Additive untextured material for the glowing rim. Its colour is the rim colour.")]
        [SerializeField] Material rimMaterial;

        [Header("Card Face (card-local units)")]
        [SerializeField] Vector2 faceSize = new Vector2(0.063f, 0.088f);

        [Tooltip("Base for the glow's breathing, so the same card breathes alike on every machine.")]
        [SerializeField] int layoutSeed = 7;

        [Header("Glow")]
        [SerializeField] float glowOversize = 1.25f;
        [SerializeField] float glowLift = 0.0006f;

        [Tooltip("Rim band width inside the card border and outside it.")]
        [SerializeField] Vector2 rimWidth = new Vector2(0.012f, 0.006f);

        [Range(0f, 0.5f)]
        [SerializeField] float flicker = 0.18f;
        [SerializeField] float flickerSpeed = 3.5f;

        [Header("Embers")]
        [SerializeField] float emberRate = 14f;
        [SerializeField] Vector2 emberLifetime = new Vector2(0.7f, 1.4f);
        [SerializeField] Vector2 emberSize = new Vector2(0.003f, 0.007f);
        [SerializeField] Vector2 emberRiseSpeed = new Vector2(0.025f, 0.06f);
        [SerializeField] Color emberColor = new Color(0.85f, 0.6f, 1f, 1f);

        [Header("Startup")]
        [SerializeField] float fadeInSeconds = 0.35f;

        [Tooltip("Erosion while unlit. Anything above the material's own erosion plus one is fully gone.")]
        [SerializeField] float unlitErosion = 1.6f;

        static readonly int ErosionId = Shader.PropertyToID("_Erosion");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] _flames;
        MeshRenderer _glow;
        MeshRenderer _rim;
        ParticleSystem _embers;
        readonly List<Mesh> _meshes = new List<Mesh>();
        MaterialPropertyBlock _block;

        float _litErosion;
        Color _glowColor;
        Color _rimColor;
        float _flickerSeed;
        float _lit;
        float _litAt = -1f;

        void Awake()
        {
            // Cards burning side by side should not breathe in step.
            _flickerSeed = layoutSeed * 13.37f + Random.value * 100f;
            Build();
            Apply();
        }

        /// <summary>Starts burning. Settled skips the fade-in, for a Charge nobody saw stored.</summary>
        public void Ignite(bool settled)
        {
            _litAt = settled ? Time.time - fadeInSeconds : Time.time;
            _lit = settled ? 1f : 0f;

            if (_embers != null)
            {
                if (settled) _embers.Simulate(emberLifetime.y, true, true);
                _embers.Play(true);
            }

            Apply();
        }

        void Update()
        {
            if (_litAt < 0f) return;

            _lit = fadeInSeconds > 0f ? Mathf.Clamp01((Time.time - _litAt) / fadeInSeconds) : 1f;
            Apply();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) Destroy(_meshes[i]);
        }

        void Apply()
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            if (_flames != null)
            {
                _block.Clear();
                _block.SetFloat(ErosionId, Mathf.Lerp(unlitErosion, _litErosion, _lit));
                for (int i = 0; i < _flames.Length; i++)
                    if (_flames[i] != null) _flames[i].SetPropertyBlock(_block);
            }

            // A slow, uneven breathing of the glow; the flames already move by themselves.
            float t = Time.time * flickerSpeed;
            float breathe = 1f - flicker + flicker * 2f * Mathf.PerlinNoise(_flickerSeed, t);

            SetColor(_glow, _glowColor, _lit * breathe);
            SetColor(_rim, _rimColor, _lit * Mathf.Lerp(breathe, 1f, 0.5f));
        }

        void SetColor(Renderer target, Color color, float alpha)
        {
            if (target == null) return;

            color.a *= alpha;
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            if (glowMaterial != null)
            {
                _glowColor = glowMaterial.HasProperty(BaseColorId) ? glowMaterial.GetColor(BaseColorId) : Color.white;
                _glow = AddRenderer("SurfaceGlow", BuildGlow(), glowMaterial);
            }

            if (rimMaterial != null)
            {
                _rimColor = rimMaterial.HasProperty(BaseColorId) ? rimMaterial.GetColor(BaseColorId) : Color.white;
                _rim = AddRenderer("RimGlow", BuildRim(), rimMaterial);
            }

            if (flamePrefab != null) BuildFlame();

            if (glowMaterial != null) _embers = BuildEmbers();
        }

        /// <summary>
        /// The one flame, standing on the card centre with its base on the face. Its
        /// renderers are kept so the fade-in can erode them through a property block.
        /// </summary>
        void BuildFlame()
        {
            GameObject flame = Instantiate(flamePrefab, transform, false);
            flame.name = "Flame";
            flame.transform.localPosition = Vector3.zero;
            flame.transform.localRotation = Quaternion.identity;

            foreach (Transform part in flame.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = gameObject.layer;

            _flames = flame.GetComponentsInChildren<ParticleSystemRenderer>(true);

            Material material = _flames.Length > 0 ? _flames[0].sharedMaterial : null;
            _litErosion = material != null && material.HasProperty(ErosionId) ? material.GetFloat(ErosionId) : 0f;
        }

        MeshRenderer AddRenderer(string childName, Mesh mesh, Material material)
        {
            _meshes.Add(mesh);

            GameObject child = new GameObject(childName);
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);

            child.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return renderer;
        }

        /// <summary>One soft quad flat on the card face, slightly larger than the card.</summary>
        Mesh BuildGlow()
        {
            float hx = faceSize.x * 0.5f * glowOversize;
            float hz = faceSize.y * 0.5f * glowOversize;

            Mesh mesh = new Mesh { name = "ChargeCardGlow" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-hx, glowLift, -hz), new Vector3(hx, glowLift, -hz),
                new Vector3(hx, glowLift, hz), new Vector3(-hx, glowLift, hz)
            });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A band along the card border, brightest right on the edge and fading to nothing
        /// inward and outward through vertex alpha: the card's edges glow as they burn.
        /// </summary>
        Mesh BuildRim()
        {
            float hx = faceSize.x * 0.5f;
            float hz = faceSize.y * 0.5f;
            float lift = glowLift * 2f;

            // Three rectangles: inner (clear), on the edge (full), outer (clear).
            float[] grow = { -rimWidth.x, 0f, rimWidth.y };
            float[] alpha = { 0f, 1f, 0f };

            List<Vector3> positions = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            for (int ring = 0; ring < 3; ring++)
            {
                float x = hx + grow[ring];
                float z = hz + grow[ring];
                Vector3[] corners = { new Vector3(-x, lift, -z), new Vector3(x, lift, -z), new Vector3(x, lift, z), new Vector3(-x, lift, z) };

                foreach (Vector3 corner in corners)
                {
                    positions.Add(corner);
                    colors.Add(new Color(1f, 1f, 1f, alpha[ring]));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                }
            }

            List<int> triangles = new List<int>();
            for (int ring = 0; ring < 2; ring++)
            {
                int inner = ring * 4;
                int outer = inner + 4;

                for (int side = 0; side < 4; side++)
                {
                    int next = (side + 1) % 4;
                    triangles.AddRange(new[] { inner + side, outer + next, outer + side });
                    triangles.AddRange(new[] { inner + side, inner + next, outer + next });
                }
            }

            Mesh mesh = new Mesh { name = "ChargeCardRim" };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Small embers rising from random points of the card face. They are born and
        /// simulated in card space, so the stream belongs to the card from every seat.
        /// </summary>
        ParticleSystem BuildEmbers()
        {
            GameObject child = new GameObject("Embers");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(emberLifetime.x, emberLifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(emberRiseSpeed.x, emberRiseSpeed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(emberSize.x, emberSize.y);
            main.startColor = emberColor;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = emberRate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            // The box emits along its own +Z, turned here to point up out of the card; so
            // its thin axis is Z, and X and Y span the face.
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(faceSize.x, faceSize.y, 0.0005f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.015f;
            noise.frequency = 2f;

            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = fade;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return system;
        }
    }
}
