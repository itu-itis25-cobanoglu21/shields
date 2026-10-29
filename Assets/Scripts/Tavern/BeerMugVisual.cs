using System.Collections.Generic;
using UnityEngine;
using Shields.Social;

namespace Shields.Tavern
{
    /// <summary>
    /// Presentation of one beer mug: the visible beer inside it, the grip the drinker's hand
    /// takes, and, once handed over, which beer it is and which table slot is its home.
    ///
    /// The beer is a closed interior volume (generated once, shared by every mug) drawn by
    /// "Shields/Beer Liquid". Every pixel above a world horizontal plane is discarded and the
    /// inside of the volume below it is shaded as the liquid surface, so the beer stays
    /// level while the mug tilts and can never show outside the mug: the volume sits inside
    /// the mug's inner wall, and clipping only ever removes from it. The plane passes
    /// through the mug's axis at the height the remaining amount implies, which keeps the
    /// volume about right at any tilt.
    ///
    /// <see cref="Fill"/> is set from authoritative beer state by the drink presenter; this
    /// component never decides how much beer is left. Geometry numbers are in the mug
    /// mesh's own units (the prefab scales it by 0.4), measured from Mug.fbx: inner floor at
    /// 0.049, rim at 0.288, inner wall radius about 0.088 at the bottom and 0.079 at the top,
    /// handle on the mesh's -X side.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeerMugVisual : MonoBehaviour
    {
        static readonly List<BeerMugVisual> s_active = new List<BeerMugVisual>();
        static readonly int FillLevelId = Shader.PropertyToID("_FillLevelWS");
        static Mesh s_liquidMesh;

        [Header("Rig")]
        [Tooltip("Where the drinker's LEFT hand anchor sits while holding this mug (mug local).")]
        [SerializeField] Transform leftHandGripAnchor;

        [Tooltip("The beer volume renderer. Its mesh is generated at runtime.")]
        [SerializeField] MeshRenderer liquid;

        [Header("Interior (mug mesh units)")]
        [Tooltip("Centre of the inner octagon in mesh x, z (from the inner wall corners).")]
        [SerializeField] Vector2 axisXZ = new Vector2(0.0005f, 0.003f);
        [SerializeField] float liquidBottom = 0.052f;
        [SerializeField] float liquidTop = 0.282f;
        [Tooltip("Corner radius of the beer volume at its bottom: the mug's octagonal inner wall (about 0.0887) less a small inset.")]
        [SerializeField] float liquidRadiusBottom = 0.0857f;

        [Tooltip("Corner radius at the top of the volume (inner wall about 0.0787 there).")]
        [SerializeField] float liquidRadiusTop = 0.0757f;

        [Tooltip("The mug's inner wall is an octagon; the beer volume matches it so no wall pokes through the surface.")]
        [SerializeField] int liquidSides = 8;

        [Tooltip("Angle of the first inner-wall corner around the axis, from mesh +X toward +Z.")]
        [SerializeField] float liquidCornerAngle = 22.5f;

        [Tooltip("Liquid level of a full beer: a little under the rim.")]
        [SerializeField] float fullLevel = 0.262f;

        [Header("Rim (mug mesh units)")]
        [SerializeField] float rimHeight = 0.288f;
        [SerializeField] float rimOuterRadius = 0.096f;

        float _fill = 1f;
        MaterialPropertyBlock _block;

        /// <summary>Every live mug. Lets presenters find delivered mugs without scanning the scene.</summary>
        public static IReadOnlyList<BeerMugVisual> Active { get { return s_active; } }

        /// <summary>Remaining beer shown, 0..1. A new mug is full.</summary>
        public float Fill
        {
            get { return _fill; }
            set { _fill = BeerLedger.Clamp01(value); }
        }

        public Transform LeftHandGripAnchor { get { return leftHandGripAnchor; } }

        /// <summary>Set when the waiter has put this mug on a player's table.</summary>
        public bool IsDelivered { get; private set; }

        public BeerId Beer { get; private set; }

        /// <summary>The DrinkSlot this mug was delivered to. It always comes back here.</summary>
        public Transform HomeSlot { get; private set; }

        /// <summary>True while the mug rests in its home slot (not in a hand, not in the air).</summary>
        public bool IsHome { get { return HomeSlot != null && transform.parent == HomeSlot; } }

        /// <summary>Rim centre in mug local space (mesh units).</summary>
        public Vector3 RimCenterLocal { get { return new Vector3(axisXZ.x, rimHeight, axisXZ.y); } }

        public float RimOuterRadius { get { return rimOuterRadius; } }

        void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        void OnDisable()
        {
            s_active.Remove(this);
        }

        void Awake()
        {
            if (liquid != null)
            {
                MeshFilter filter = liquid.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = LiquidMesh();
            }
        }

        /// <summary>The waiter set this mug down: from now on it is this beer, at home in this slot.</summary>
        public void MarkDelivered(BeerId beer, Transform homeSlot)
        {
            Beer = beer;
            HomeSlot = homeSlot;
            IsDelivered = true;
            Fill = 1f;
        }

        /// <summary>Puts the mug back exactly into its home slot, at its table scale.</summary>
        public void SettleHome(Vector3 tableScale)
        {
            if (HomeSlot == null) return;

            transform.SetParent(HomeSlot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = tableScale;
        }

        void LateUpdate()
        {
            if (liquid == null) return;

            bool visible = _fill > 0.001f;
            if (liquid.enabled != visible) liquid.enabled = visible;
            if (!visible) return;

            // The level plane through the mug axis at the height this much beer reaches.
            float level = Mathf.Lerp(liquidBottom, fullLevel, _fill);
            float worldY = transform.TransformPoint(new Vector3(axisXZ.x, level, axisXZ.y)).y;

            if (_block == null) _block = new MaterialPropertyBlock();
            liquid.GetPropertyBlock(_block);
            _block.SetFloat(FillLevelId, worldY);
            liquid.SetPropertyBlock(_block);
        }

        // ------------------------------------------------------------------ liquid volume

        Mesh LiquidMesh()
        {
            if (s_liquidMesh != null) return s_liquidMesh;

            int segments = Mathf.Max(3, liquidSides);
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();

            float slope = (liquidRadiusBottom - liquidRadiusTop) / Mathf.Max(0.0001f, liquidTop - liquidBottom);
            float start = liquidCornerAngle * Mathf.Deg2Rad;
            Vector3 bottomCenter = new Vector3(axisXZ.x, liquidBottom, axisXZ.y);
            Vector3 topCenter = new Vector3(axisXZ.x, liquidTop, axisXZ.y);

            // Side wall: one flat face per inner-wall face, normals outward.
            for (int i = 0; i < segments; i++)
            {
                float a0 = start + i * Mathf.PI * 2f / segments;
                float a1 = start + (i + 1) * Mathf.PI * 2f / segments;
                Vector3 r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 normal = ((r0 + r1).normalized + Vector3.up * slope).normalized;

                int b0 = vertices.Count;
                vertices.Add(bottomCenter + r0 * liquidRadiusBottom);
                vertices.Add(topCenter + r0 * liquidRadiusTop);
                vertices.Add(bottomCenter + r1 * liquidRadiusBottom);
                vertices.Add(topCenter + r1 * liquidRadiusTop);
                for (int k = 0; k < 4; k++) normals.Add(normal);

                int t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                triangles.Add(b0); triangles.Add(t0); triangles.Add(t1);
                triangles.Add(b0); triangles.Add(t1); triangles.Add(b1);
            }

            // Bottom cap, facing down. No top cap: the clipped inside is the surface.
            int center = vertices.Count;
            vertices.Add(bottomCenter);
            normals.Add(Vector3.down);
            for (int i = 0; i <= segments; i++)
            {
                float a = start + i * Mathf.PI * 2f / segments;
                vertices.Add(bottomCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * liquidRadiusBottom);
                normals.Add(Vector3.down);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(center);
                triangles.Add(center + 1 + i);
                triangles.Add(center + 2 + i);
            }

            s_liquidMesh = new Mesh { name = "BeerLiquidVolume" };
            s_liquidMesh.SetVertices(vertices);
            s_liquidMesh.SetNormals(normals);
            s_liquidMesh.SetTriangles(triangles, 0);
            s_liquidMesh.RecalculateBounds();
            return s_liquidMesh;
        }

        /// <summary>Editor wiring used when the prefab is built from code.</summary>
        public void BindRig(Transform gripAnchor, MeshRenderer liquidRenderer)
        {
            leftHandGripAnchor = gripAnchor;
            liquid = liquidRenderer;
        }
    }
}
