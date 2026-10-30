using UnityEngine;

namespace Shields.Settings
{
    /// <summary>
    /// The local player's nickname and equipped cosmetics, saved on this machine.
    /// Not used in matches yet: names and cosmetics are not part of the match or the
    /// network state.
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

        public static void SetEquipped(string slot, string id)
        {
            PlayerPrefs.SetString(Prefix + "equipped." + slot, id);
            PlayerPrefs.Save();
        }
    }
}
