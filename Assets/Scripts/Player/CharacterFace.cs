using Shields.Social;
using UnityEngine;

namespace Shields.Players
{
    /// <summary>
    /// The face cosmetics under a character's head: FaceAnchor (this) holds EyesSlot,
    /// EyebrowsSlot and MouthSlot, and each slot holds one child per cosmetic, named by
    /// its <see cref="FaceCatalog"/> id. Exactly one child per slot is active. The meshes
    /// share the Blender head's placement, so FaceAnchor sits at the HeadVisual origin.
    /// Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterFace : MonoBehaviour
    {
        public const string AnchorName = "FaceAnchor";

        public void Show(FaceLoadout face) { Show(transform, face); }

        /// <summary>Works on a bare FaceAnchor hierarchy too (thumbnail copies have no scripts).</summary>
        public static void Show(Transform anchor, FaceLoadout face)
        {
            if (anchor == null) return;
            ShowIn(SlotRoot(anchor, FaceSlot.Eyes), face.Eyes);
            ShowIn(SlotRoot(anchor, FaceSlot.Eyebrows), face.Eyebrows);
            ShowIn(SlotRoot(anchor, FaceSlot.Mouth), face.Mouth);
        }

        /// <summary>Shows one cosmetic and leaves the other slots empty (wardrobe thumbnails).</summary>
        public static void ShowOnly(Transform anchor, FaceSlot slot, string id)
        {
            if (anchor == null) return;
            ShowIn(SlotRoot(anchor, FaceSlot.Eyes), slot == FaceSlot.Eyes ? id : null);
            ShowIn(SlotRoot(anchor, FaceSlot.Eyebrows), slot == FaceSlot.Eyebrows ? id : null);
            ShowIn(SlotRoot(anchor, FaceSlot.Mouth), slot == FaceSlot.Mouth ? id : null);
        }

        public static string SlotName(FaceSlot slot) { return slot + "Slot"; }

        static Transform SlotRoot(Transform anchor, FaceSlot slot) { return anchor.Find(SlotName(slot)); }

        static void ShowIn(Transform slot, string id)
        {
            if (slot == null) return;
            foreach (Transform part in slot) part.gameObject.SetActive(part.name == id);
        }
    }
}
