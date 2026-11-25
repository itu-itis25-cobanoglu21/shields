using UnityEngine;

namespace Shields.Players
{
    /// <summary>
    /// The attachment point for the torso outfit, a child of the character's Body at
    /// identity. The outfits were authored in Blender against that shared body origin, so
    /// the model goes in exactly as exported, with no per-outfit offsets. Holds at most one
    /// outfit: showing another removes the previous one first. Visual only: the outfit
    /// carries no colliders and the weapon's sight test skips it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BodyCosmeticAnchor : MonoBehaviour
    {
        public const string AnchorName = "BodyCosmeticAnchor";

        string _outfitId;
        GameObject _outfit;

        public string OutfitId { get { return _outfitId; } }
        public GameObject Outfit { get { return _outfit; } }

        /// <summary>The anchor under <paramref name="body"/>, created at identity if the rig has none.</summary>
        public static BodyCosmeticAnchor On(Transform body)
        {
            if (body == null) return null;
            Transform t = body.Find(AnchorName);
            if (t == null)
            {
                t = new GameObject(AnchorName).transform;
                t.SetParent(body, false);
                t.gameObject.layer = body.gameObject.layer;
            }
            BodyCosmeticAnchor anchor = t.GetComponent<BodyCosmeticAnchor>();
            return anchor != null ? anchor : t.gameObject.AddComponent<BodyCosmeticAnchor>();
        }

        /// <summary>
        /// Wears <paramref name="model"/> (null: Bare). Repeating the same outfit does nothing,
        /// so identity refreshes that run on every network snapshot stay free.
        /// </summary>
        public void Show(string outfitId, GameObject model)
        {
            if (model == null) outfitId = null;
            if (outfitId == _outfitId && (outfitId == null ? transform.childCount == 0 : _outfit != null)) return;

            Clear();
            _outfitId = outfitId;
            if (model == null) return;

            // Kept exactly as imported: position 0, scale 1, and the FBX's own Z-up correction.
            _outfit = Instantiate(model, transform, false);
            _outfit.name = "Outfit_" + outfitId;
            foreach (Collider c in _outfit.GetComponentsInChildren<Collider>(true)) Kill(c);
            foreach (Transform t in _outfit.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
        }

        // Every child goes, not just the tracked one, so no stale mesh can survive a rebind.
        void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);   // gone from the anchor now, not at frame end
                Kill(child);
            }
            _outfit = null;
            _outfitId = null;
        }

        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
