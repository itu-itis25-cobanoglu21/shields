using Shields.Social;
using UnityEngine;

namespace Shields.Players
{
    /// <summary>
    /// The exported torso outfit models, shared by the Customize preview and the gameplay
    /// PlayerCharacter. Looked up by <see cref="BodyCatalog"/> source asset name, so the
    /// catalog stays the one list of outfits. Presentation only.
    /// </summary>
    [CreateAssetMenu(menuName = "Shields/Body Outfit Library", fileName = "BodyOutfitLibrary")]
    public sealed class BodyOutfitLibrary : ScriptableObject
    {
        [Tooltip("The outfit FBX models, named as the catalog's source assets.")]
        [SerializeField] GameObject[] outfits;

        /// <summary>The model for an outfit id, or null for Bare or an unknown id.</summary>
        public GameObject PrefabFor(string outfitId)
        {
            BodyOutfit outfit = BodyCatalog.FindOutfit(outfitId);
            if (outfit == null || string.IsNullOrEmpty(outfit.SourceAsset) || outfits == null) return null;
            foreach (GameObject go in outfits)
                if (go != null && go.name == outfit.SourceAsset) return go;
            return null;
        }
    }
}
