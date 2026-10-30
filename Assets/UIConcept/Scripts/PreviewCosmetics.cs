using UnityEngine;

namespace Shields.UIConcept
{
    /// <summary>
    /// Shows cosmetics on the visual-only preview character: a head prop fitted to the
    /// head, and material swaps for body and hands. Presentation only; nothing persists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PreviewCosmetics : MonoBehaviour
    {
        [SerializeField] Transform headVisual;
        [SerializeField] Renderer headRenderer;
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Renderer[] handRenderers;

        [Tooltip("Head-prop width relative to the head mesh width.")]
        [SerializeField] float headFit = 1.14f;

        Material _defaultBody;
        Material _defaultHands;
        GameObject _headProp;
        string _headId;

        public void Configure(Transform head, Renderer headMesh, Renderer body, Renderer[] hands)
        {
            headVisual = head;
            headRenderer = headMesh;
            bodyRenderer = body;
            handRenderers = hands;
        }

        void Awake()
        {
            if (bodyRenderer != null) _defaultBody = bodyRenderer.sharedMaterial;
            if (handRenderers != null && handRenderers.Length > 0) _defaultHands = handRenderers[0].sharedMaterial;
        }

        public void Show(CosmeticItem head, CosmeticItem body, CosmeticItem hands)
        {
            ShowHead(head);

            if (bodyRenderer != null)
                bodyRenderer.sharedMaterial = body != null && body.Material != null ? body.Material : _defaultBody;

            if (handRenderers != null)
                foreach (Renderer r in handRenderers)
                    if (r != null) r.sharedMaterial = hands != null && hands.Material != null ? hands.Material : _defaultHands;
        }

        void ShowHead(CosmeticItem item)
        {
            string id = item != null && item.HeadPrefab != null ? item.Id : null;
            if (id == _headId) return;
            _headId = id;

            if (_headProp != null) { _headProp.SetActive(false); Destroy(_headProp); }
            _headProp = null;
            if (id == null || headVisual == null) return;

            _headProp = Instantiate(item.HeadPrefab, headVisual);
            _headProp.name = "HeadProp_" + item.Id;
            Recolour(_headProp, item.Material);
            FitToHead(_headProp, headRenderer, headFit, item.Id);
        }

        /// <summary>
        /// Fits a prop to the head in the head's own space (so the character's rotation
        /// does not matter): helmets are scaled to enclose the head and sit down to the
        /// chin; hats and the mug perch on the crown.
        /// </summary>
        public static void FitToHead(GameObject prop, Renderer head, float fit, string id)
        {
            Transform space = prop.transform.parent;
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;
            foreach (Collider c in prop.GetComponentsInChildren<Collider>()) Destroy(c);
            if (head == null || space == null) return;

            Bounds hb = LocalBounds(head.gameObject, space);
            Bounds pb = LocalBounds(prop, space);
            if (pb.size.x <= 0.0001f || pb.size.z <= 0.0001f) return;

            bool hat = id != null && id.Contains("tricorn");
            bool mug = id != null && id.Contains("mug");
            // pb was measured at local scale 1, so the factor is the new local scale.
            float scale = Mathf.Max(hb.size.x * fit / pb.size.x, hb.size.z * (fit - 0.08f) / pb.size.z);
            if (hat) scale = Mathf.Max(hb.size.x, hb.size.z) * 1.3f / Mathf.Max(pb.size.x, pb.size.z);
            if (mug) scale = hb.size.x * 0.8f / Mathf.Max(pb.size.x, pb.size.z);
            prop.transform.localScale = Vector3.one * scale;

            pb = LocalBounds(prop, space);
            float bottom = hat ? hb.center.y + hb.extents.y * 0.42f
                         : mug ? hb.center.y + hb.extents.y * 0.72f
                         : hb.min.y + hb.size.y * 0.04f;
            prop.transform.localPosition += new Vector3(hb.center.x - pb.center.x, bottom - pb.min.y, hb.center.z - pb.center.z);
        }

        /// <summary>Bounds of every mesh under <paramref name="go"/>, expressed in <paramref name="space"/>.</summary>
        public static Bounds LocalBounds(GameObject go, Transform space)
        {
            bool any = false;
            var b = new Bounds();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Paints every renderer of a prop with one material (recoloured variants). Null leaves it as is.</summary>
        public static GameObject Recolour(GameObject prop, Material material)
        {
            if (material == null) return prop;
            foreach (Renderer r in prop.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
            return prop;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
