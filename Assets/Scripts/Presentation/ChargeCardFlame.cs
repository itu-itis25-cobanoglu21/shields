using System.Collections.Generic;
using UnityEngine;

namespace Shields.Presentation
{
    /// <summary>
    /// A stored Charge card burning across its top face.
    ///
    /// Everything is built in card-local space and nothing turns toward the camera, so
    /// every seat sees the fire in the same place on the card. Four layers:
    /// a soft glow lying on the face, a bright glowing rim along the card border, low
    /// flame walls standing on the four edges plus a few short flames on the face (the
    /// Vefects fire shader, its per-particle data written into the mesh UVs instead), and
    /// small embers rising from random points of the face.
    ///
    /// Presentation only: no collider, no gameplay data, and it is destroyed with its card.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChargeCardFlame : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("Vefects fire shader material for the edge and surface flames.")]
        [SerializeField] Material flameMaterial;

        [Tooltip("Additive soft glow laid on the card face. Also used for the embers.")]
        [SerializeField] Material glowMaterial;

        [Tooltip("Additive untextured material for the glowing rim. Its colour is the rim colour.")]
        [SerializeField] Material rimMaterial;

        [Header("Card Face (card-local units)")]
        [SerializeField] Vector2 faceSize = new Vector2(0.063f, 0.088f);

        [Header("Edge Flames")]
        [Tooltip("Distance between flames along an edge.")]
        [SerializeField] float edgeSpacing = 0.014f;

        [Tooltip("How far each flame overlaps its neighbours, as a multiple of the spacing.")]
        [SerializeField] float edgeOverlap = 1.9f;

        [SerializeField] float edgeFlameHeight = 0.026f;

        [Tooltip("How far the edge flames stand in from the card border.")]
        [SerializeField] float edgeInset = 0.002f;

        [Header("Surface Flames")]
        [SerializeField] int surfaceColumns = 2;
        [SerializeField] int surfaceRows = 3;
        [SerializeField] Vector2 surfaceFlameSize = new Vector2(0.03f, 0.022f);

        [Tooltip("Free margin along the card edges before the first surface flame.")]
        [SerializeField] float surfaceInset = 0.016f;

        [Header("Shared Flame Shape")]
        [Range(0f, 0.5f)]
        [SerializeField] float sizeJitter = 0.3f;

        [Tooltip("How far flames start below the face, so their base melts into the card.")]
        [SerializeField] float sink = 0.003f;

        [Tooltip("Same layout on every machine and every card.")]
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

        // Per-vertex data the fire shader normally receives from particle custom data.
        const float Distortion = 0.6f;
        const float Emissive = 1f;
        const float ErosionSmoothness = 0.85f;
        const float MaxRandomOffset = 69f;

        static readonly int ErosionId = Shader.PropertyToID("_Erosion");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MeshRenderer _flames;
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
                _flames.SetPropertyBlock(_block);
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

            if (flameMaterial != null)
            {
                _litErosion = flameMaterial.HasProperty(ErosionId) ? flameMaterial.GetFloat(ErosionId) : 0f;
                _flames = AddRenderer("Flames", BuildFlames(), flameMaterial);
            }

            if (glowMaterial != null) _embers = BuildEmbers();
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

        /// <summary>
        /// Low flame walls standing on the four card edges, each in the plane of its edge,
        /// plus a few short crossed flames over the face. All fixed in card space.
        /// </summary>
        Mesh BuildFlames()
        {
            System.Random random = new System.Random(layoutSeed);
            SheetBuilder sheets = new SheetBuilder();

            float hx = faceSize.x * 0.5f - edgeInset;
            float hz = faceSize.y * 0.5f - edgeInset;

            // Edges, corner to corner, so the whole border burns.
            AddEdge(sheets, random, new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz));
            AddEdge(sheets, random, new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz));
            AddEdge(sheets, random, new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz));
            AddEdge(sheets, random, new Vector3(-hx, 0f, hz), new Vector3(-hx, 0f, -hz));

            // Short flames over the face, each a crossed pair so it has body from any side.
            float sx = Mathf.Max(0f, faceSize.x * 0.5f - surfaceInset);
            float sz = Mathf.Max(0f, faceSize.y * 0.5f - surfaceInset);

            for (int row = 0; row < surfaceRows; row++)
            {
                for (int column = 0; column < surfaceColumns; column++)
                {
                    float x = surfaceColumns > 1 ? Mathf.Lerp(-sx, sx, column / (float)(surfaceColumns - 1)) : 0f;
                    float z = surfaceRows > 1 ? Mathf.Lerp(-sz, sz, row / (float)(surfaceRows - 1)) : 0f;

                    Vector2 size = surfaceFlameSize * Jitter(random);
                    float yaw = (float)random.NextDouble() * 90f;
                    float offset = (float)random.NextDouble() * MaxRandomOffset;

                    for (int pair = 0; pair < 2; pair++)
                        sheets.Add(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw + pair * 90f, 0f) * Vector3.right,
                            size, sink, offset);
                }
            }

            return sheets.ToMesh("ChargeCardFlames");
        }

        void AddEdge(SheetBuilder sheets, System.Random random, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(0.001f, edgeSpacing)));
            along /= length;

            for (int i = 0; i < count; i++)
            {
                Vector3 center = Vector3.Lerp(from, to, (i + 0.5f) / count);
                float jitter = Jitter(random);
                Vector2 size = new Vector2(length / count * edgeOverlap, edgeFlameHeight * jitter);

                sheets.Add(center, along, size, sink, (float)random.NextDouble() * MaxRandomOffset);
            }
        }

        float Jitter(System.Random random)
        {
            return 1f + ((float)random.NextDouble() * 2f - 1f) * sizeJitter;
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

        // ------------------------------------------------------------------ sheets

        /// <summary>Two-sided vertical flame quads carrying the fire shader's per-particle data.</summary>
        sealed class SheetBuilder
        {
            readonly List<Vector3> _positions = new List<Vector3>();
            readonly List<Vector3> _normals = new List<Vector3>();
            readonly List<Color> _colors = new List<Color>();
            readonly List<Vector4> _uv0 = new List<Vector4>();
            readonly List<Vector4> _uv1 = new List<Vector4>();
            readonly List<int> _triangles = new List<int>();

            static readonly Vector2[] Corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

            public void Add(Vector3 center, Vector3 across, Vector2 size, float sink, float randomOffset)
            {
                Vector3 half = across.normalized * (size.x * 0.5f);
                Vector3 bottom = center + Vector3.down * sink;
                Vector3 top = bottom + Vector3.up * size.y;

                // Facing the same way as the front triangles below.
                Vector3 normal = Vector3.Cross(Vector3.up, across).normalized;

                Vector3[] corners = { bottom - half, bottom + half, top + half, top - half };

                // Front and back as separate faces with their own normals, so both sides
                // fade the same way when the sheet is seen edge-on (the shader's fresnel).
                for (int side = 0; side < 2; side++)
                {
                    int first = _positions.Count;

                    for (int i = 0; i < 4; i++)
                    {
                        _positions.Add(corners[i]);
                        _normals.Add(side == 0 ? normal : -normal);
                        _colors.Add(Color.white);

                        // TEXCOORD0: uv, erosion, distortion. TEXCOORD1: emissive, random
                        // offset, erosion smoothness, random rotation.
                        _uv0.Add(new Vector4(Corners[i].x, Corners[i].y, 0f, Distortion));
                        _uv1.Add(new Vector4(Emissive, randomOffset, ErosionSmoothness, 0f));
                    }

                    if (side == 0) _triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                    else _triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(_positions);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetUVs(0, _uv0);
                mesh.SetUVs(1, _uv1);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
