using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shields.Settings
{
    /// <summary>
    /// Player settings, saved on this machine (PlayerPrefs) and applied immediately.
    ///
    /// Master volume drives the AudioListener; music and effects volume are stored for
    /// the audio that does not exist yet. Mouse sensitivity scales the seated head look.
    /// Resolution and window mode go straight to <see cref="Screen"/>, which the player
    /// itself remembers between launches; V-Sync is re-applied at every launch.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "shields.settings.";

        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;

        static bool _loaded;
        static float _master = 0.8f, _music = 0.55f, _effects = 0.75f, _sensitivity = 1f;
        static bool _vsync = true;
        static readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        static int _resolutionIndex = -1;

        /// <summary>Raised after any setting changes.</summary>
        public static event Action Changed;

        // Settings survive Play Mode restarts when domain reload is off, so reload them.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            _loaded = false;
            _resolutions.Clear();
            _resolutionIndex = -1;
            Load();
            ApplyAudio();
            QualitySettings.vSyncCount = _vsync ? 1 : 0;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _master = PlayerPrefs.GetFloat(Prefix + "master", 0.8f);
            _music = PlayerPrefs.GetFloat(Prefix + "music", 0.55f);
            _effects = PlayerPrefs.GetFloat(Prefix + "effects", 0.75f);
            _sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "sensitivity", 1f), MinSensitivity, MaxSensitivity);
            _vsync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
        }

        // ------------------------------------------------------------------ audio

        public static float MasterVolume { get { Load(); return _master; } set { _master = Mathf.Clamp01(value); Save("master", _master); ApplyAudio(); } }
        public static float MusicVolume { get { Load(); return _music; } set { _music = Mathf.Clamp01(value); Save("music", _music); } }
        public static float EffectsVolume { get { Load(); return _effects; } set { _effects = Mathf.Clamp01(value); Save("effects", _effects); } }

        static void ApplyAudio() { AudioListener.volume = _master; }

        // ------------------------------------------------------------------ controls

        /// <summary>Multiplier on the seated look speed, 0.1 .. 3.</summary>
        public static float MouseSensitivity
        {
            get { Load(); return _sensitivity; }
            set { _sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); Save("sensitivity", _sensitivity); }
        }

        // ------------------------------------------------------------------ display

        public static bool VSync
        {
            get { Load(); return _vsync; }
            set
            {
                _vsync = value;
                QualitySettings.vSyncCount = value ? 1 : 0;
                PlayerPrefs.SetInt(Prefix + "vsync", value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool Fullscreen
        {
            get { return Screen.fullScreenMode != FullScreenMode.Windowed; }
            set
            {
                Vector2Int r = CurrentResolution;
                Screen.SetResolution(r.x, r.y, value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                Changed?.Invoke();
            }
        }

        /// <summary>The distinct screen sizes this display offers, smallest first.</summary>
        public static IReadOnlyList<Vector2Int> Resolutions
        {
            get
            {
                if (_resolutions.Count > 0) return _resolutions;
                foreach (Resolution r in Screen.resolutions)
                {
                    var size = new Vector2Int(r.width, r.height);
                    if (!_resolutions.Contains(size)) _resolutions.Add(size);
                }
                var current = new Vector2Int(Screen.width, Screen.height);
                if (!_resolutions.Contains(current)) _resolutions.Add(current);
                _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                return _resolutions;
            }
        }

        public static int ResolutionIndex
        {
            get
            {
                if (_resolutionIndex < 0)
                {
                    var current = new Vector2Int(Screen.width, Screen.height);
                    _resolutionIndex = Mathf.Max(0, IndexOf(current));
                }
                return Mathf.Clamp(_resolutionIndex, 0, Resolutions.Count - 1);
            }
            set
            {
                int n = Resolutions.Count;
                _resolutionIndex = ((value % n) + n) % n;
                Vector2Int r = Resolutions[_resolutionIndex];
                Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
                Changed?.Invoke();
            }
        }

        public static Vector2Int CurrentResolution { get { return Resolutions[ResolutionIndex]; } }

        static int IndexOf(Vector2Int size)
        {
            IReadOnlyList<Vector2Int> list = Resolutions;
            for (int i = 0; i < list.Count; i++) if (list[i] == size) return i;
            return -1;
        }

        // Unity writes PlayerPrefs to disk on quit, so slider drags do not hit the disk.
        static void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            Changed?.Invoke();
        }
    }
}
