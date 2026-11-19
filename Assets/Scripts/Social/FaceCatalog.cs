using System.Collections.Generic;

namespace Shields.Social
{
    /// <summary>The three face slots. Facial hair is a Mouth option, so a mouth and a beard never stack.</summary>
    public enum FaceSlot { Eyes, Eyebrows, Mouth }

    /// <summary>One face cosmetic: its id, display name, slot and the mesh it comes from in SHIELDS_FaceCosmetics.fbx.</summary>
    public sealed class FaceCosmetic
    {
        public readonly string Id;
        public readonly string Name;
        public readonly FaceSlot Slot;
        public readonly string SourceMesh;
        public readonly bool IsFacialHair;
        public readonly bool IsDefault;

        public FaceCosmetic(string id, string name, FaceSlot slot, string sourceMesh, bool isFacialHair, bool isDefault)
        {
            Id = id;
            Name = name;
            Slot = slot;
            SourceMesh = sourceMesh;
            IsFacialHair = isFacialHair;
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// Every face cosmetic, in wardrobe order. Cosmetic only: never part of the match.
    /// The defaults are the combination left visible in the authoring .blend.
    /// </summary>
    public static class FaceCatalog
    {
        public static readonly IReadOnlyList<FaceCosmetic> All = new[]
        {
            new FaceCosmetic("eyes_sleepy", "SLEEPY", FaceSlot.Eyes, "Eyes | Sleepy", false, false),
            new FaceCosmetic("eyes_squinty", "SQUINTY", FaceSlot.Eyes, "Eyes | Squinty", false, false),
            new FaceCosmetic("eyes_suspicious", "SUSPICIOUS", FaceSlot.Eyes, "Eyes | Suspicious", false, true),
            new FaceCosmetic("eyes_smug", "SMUG", FaceSlot.Eyes, "Eyes | Smug", false, false),
            new FaceCosmetic("eyes_wide_goofy", "WIDE-GOOFY", FaceSlot.Eyes, "Eyes | Wide-Goofy", false, false),
            new FaceCosmetic("eyes_bluffing", "BLUFFING", FaceSlot.Eyes, "Eyes | Bluffing", false, false),

            new FaceCosmetic("brows_mischievous", "MISCHIEVOUS", FaceSlot.Eyebrows, "Brows | Mischievous", false, true),
            new FaceCosmetic("brows_skeptical", "SKEPTICAL", FaceSlot.Eyebrows, "Brows | Skeptical", false, false),
            new FaceCosmetic("brows_heavy_doubtful", "HEAVY/DOUBTFUL", FaceSlot.Eyebrows, "Brows | Heavy/Doubtful", false, false),
            new FaceCosmetic("brows_playful_raised", "PLAYFUL/RAISED", FaceSlot.Eyebrows, "Brows | Playful/Raised", false, false),
            new FaceCosmetic("brows_furrowed", "FURROWED", FaceSlot.Eyebrows, "Brows | Furrowed", false, false),
            new FaceCosmetic("brows_surprised", "SURPRISED", FaceSlot.Eyebrows, "Brows | Surprised", false, false),

            new FaceCosmetic("mouth_crooked_grin", "CROOKED GRIN", FaceSlot.Mouth, "MouthSlot | Crooked Grin", false, true),
            new FaceCosmetic("mouth_poker_face", "POKER FACE", FaceSlot.Mouth, "MouthSlot | Poker Face", false, false),
            new FaceCosmetic("mouth_goofy_smile", "GOOFY SMILE", FaceSlot.Mouth, "MouthSlot | Goofy Smile", false, false),
            new FaceCosmetic("mouth_sly_smirk", "SLY SMIRK", FaceSlot.Mouth, "MouthSlot | Sly Smirk", false, false),
            new FaceCosmetic("mouth_uncertain_grimace", "UNCERTAIN GRIMACE", FaceSlot.Mouth, "MouthSlot | Uncertain Grimace", false, false),
            new FaceCosmetic("mouth_toothy_bluffing", "TOOTHY BLUFFING", FaceSlot.Mouth, "MouthSlot | Toothy Bluffing", false, false),
            new FaceCosmetic("mouth_curled_mustache", "CURLED MUSTACHE", FaceSlot.Mouth, "MouthSlot | Curled Mustache", true, false),
            new FaceCosmetic("mouth_stubby_beard", "STUBBY BEARD", FaceSlot.Mouth, "MouthSlot | Stubby Beard", true, false),
            new FaceCosmetic("mouth_rogue_goatee", "TAVERN ROGUE GOATEE", FaceSlot.Mouth, "MouthSlot | Tavern Rogue Goatee", true, false),
            new FaceCosmetic("mouth_rounded_beard", "ROUNDED BEARD", FaceSlot.Mouth, "MouthSlot | Rounded Beard", true, false),
        };

        public static FaceCosmetic Find(string id)
        {
            foreach (FaceCosmetic item in All) if (item.Id == id) return item;
            return null;
        }

        public static FaceCosmetic DefaultFor(FaceSlot slot)
        {
            foreach (FaceCosmetic item in All) if (item.Slot == slot && item.IsDefault) return item;
            return null;
        }

        /// <summary>The id if it names a cosmetic of this slot, otherwise the slot's default.</summary>
        public static string Resolve(FaceSlot slot, string id)
        {
            FaceCosmetic item = Find(id);
            return item != null && item.Slot == slot ? item.Id : DefaultFor(slot).Id;
        }
    }

    /// <summary>One valid id per face slot: the face one participant shows.</summary>
    public readonly struct FaceLoadout
    {
        public readonly string Eyes;
        public readonly string Eyebrows;
        public readonly string Mouth;

        /// <summary>Unknown or wrong-slot ids fall back to that slot's default.</summary>
        public FaceLoadout(string eyes, string eyebrows, string mouth)
        {
            Eyes = FaceCatalog.Resolve(FaceSlot.Eyes, eyes);
            Eyebrows = FaceCatalog.Resolve(FaceSlot.Eyebrows, eyebrows);
            Mouth = FaceCatalog.Resolve(FaceSlot.Mouth, mouth);
        }

        public static FaceLoadout Default { get { return new FaceLoadout(null, null, null); } }

        public string IdFor(FaceSlot slot)
        {
            switch (slot)
            {
                case FaceSlot.Eyes: return Eyes;
                case FaceSlot.Eyebrows: return Eyebrows;
                default: return Mouth;
            }
        }
    }
}
