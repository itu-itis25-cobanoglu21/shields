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
    /// component never decides how much beer is left. Geometry numbers are in the mug's
    /// own units (the prefab root is scaled by 0.4; the visual model sits under it with its
    /// base at y 0, its axis at x = z = 0 and its handle on -X), measured from the wooden
    /// barrel mug: inner floor at 0.017, rim at 0.259, rim outer radius 0.098, inner wall
    /// radius 0.090 near the floor, bulging to 0.099 mid-height, 0.085 at the rim lip.
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

        [Tooltip("Optional inner-wall profile, bottom to top: x = height, y = corner radius (mug mesh units). With two or more points it replaces the straight bottom/top shape, so the beer follows a bulging (barrel) wall.")]
        [SerializeField] Vector2[] liquidProfile = new Vector2[0];

        [Tooltip("Sides of the beer volume. Match the mug's inner wall so no wall pokes through the surface.")]
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

        /// <summary>
        /// The waiter took this (empty) mug back: it is no one's beer at the table any more,
        /// so drinking and table clean-up leave it alone. It leaves on the tray.
        /// </summary>
        public void MarkCollected()
        {
            IsDelivered = false;
            HomeSlot = null;
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

            // Rings bottom to top: the profile when set, else the straight bottom/top shape.
            Vector2[] rings = liquidProfile != null && liquidProfile.Length >= 2
                ? liquidProfile
                : new[] { new Vector2(liquidBottom, liquidRadiusBottom), new Vector2(liquidTop, liquidRadiusTop) };

            float start = liquidCornerAngle * Mathf.Deg2Rad;

            // Side wall: one flat face per inner-wall face and ring band, normals outward.
            for (int band = 0; band + 1 < rings.Length; band++)
            {
                Vector2 lower = rings[band], upper = rings[band + 1];
                float slope = (lower.y - upper.y) / Mathf.Max(0.0001f, upper.x - lower.x);
                Vector3 lowCenter = new Vector3(axisXZ.x, lower.x, axisXZ.y);
                Vector3 highCenter = new Vector3(axisXZ.x, upper.x, axisXZ.y);

                for (int i = 0; i < segments; i++)
                {
                    float a0 = start + i * Mathf.PI * 2f / segments;
                    float a1 = start + (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 normal = ((r0 + r1).normalized + Vector3.up * slope).normalized;

                    int b0 = vertices.Count;
                    vertices.Add(lowCenter + r0 * lower.y);
                    vertices.Add(highCenter + r0 * upper.y);
                    vertices.Add(lowCenter + r1 * lower.y);
                    vertices.Add(highCenter + r1 * upper.y);
                    for (int k = 0; k < 4; k++) normals.Add(normal);

                    int t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                    triangles.Add(b0); triangles.Add(t0); triangles.Add(t1);
                    triangles.Add(b0); triangles.Add(t1); triangles.Add(b1);
                }
            }

            // Bottom cap, facing down. No top cap: the clipped inside is the surface.
            Vector3 bottomCenter = new Vector3(axisXZ.x, rings[0].x, axisXZ.y);
            float bottomRadius = rings[0].y;
            int center = vertices.Count;
            vertices.Add(bottomCenter);
            normals.Add(Vector3.down);
            for (int i = 0; i <= segments; i++)
            {
                float a = start + i * Mathf.PI * 2f / segments;
                vertices.Add(bottomCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * bottomRadius);
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
