using Shields.Social;
using UnityEngine;

namespace Shields.Settings
{
    /// <summary>
    /// The local player's nickname and equipped cosmetics, saved on this machine.
    /// In matches only the local player's own character shows the saved face: names and
    /// cosmetics are not part of the match or the network state.
    /// </summary>
    public static class PlayerProfile
    {
        const string Prefix = "shields.profile.";

        public const string DefaultNickname = "PLAYER";
        public const int MaxNicknameLength = 16;

        public static string Nickname
        {
            get
            {
                string name = PlayerPrefs.GetString(Prefix + "nickname", DefaultNickname).Trim();
                return name.Length == 0 ? DefaultNickname : name;
            }
            set
            {
                string clean = (value ?? string.Empty).Trim().ToUpperInvariant();
                if (clean.Length > MaxNicknameLength) clean = clean.Substring(0, MaxNicknameLength);
                if (clean.Length == 0) clean = DefaultNickname;
                PlayerPrefs.SetString(Prefix + "nickname", clean);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The cosmetic id equipped in a slot, or <paramref name="fallback"/> if none was saved.</summary>
        public static string GetEquipped(string slot, string fallback)
        {
            return PlayerPrefs.GetString(Prefix + "equipped." + slot, fallback);
        }

        /// <summary>Whether anything was ever saved for a slot.</summary>
        public static bool HasEquipped(string slot)
        {
            return PlayerPrefs.HasKey(Prefix + "equipped." + slot);
        }

        public static void SetEquipped(string slot, string id)
        {
            PlayerPrefs.SetString(Prefix + "equipped." + slot, id);
            PlayerPrefs.Save();
        }

        /// <summary>The saved face. The wardrobe saves it under the same slot names (Eyes, Eyebrows, Mouth).</summary>
        public static FaceLoadout EquippedFace()
        {
            return new FaceLoadout(
                GetEquipped(FaceSlot.Eyes.ToString(), null),
                GetEquipped(FaceSlot.Eyebrows.ToString(), null),
                GetEquipped(FaceSlot.Mouth.ToString(), null));
        }

        /// <summary>Wardrobe slot names the body is saved under: the outfit, and its own base colour.</summary>
        public const string BodyOutfitSlot = "Body", BodyColorSlot = "BodyColor";

        /// <summary>
        /// The saved body outfit and base colour. Before outfits existed the "Body" slot held
        /// the colour, so a profile with no colour of its own still finds it there.
        /// </summary>
        public static BodyLoadout EquippedBody()
        {
            string outfit = GetEquipped(BodyOutfitSlot, null);
            return new BodyLoadout(outfit, GetEquipped(BodyColorSlot, outfit));
        }
    }
}
