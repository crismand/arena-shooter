using UnityEngine;

namespace AS.Core
{
    /// <summary>
    /// Player settings kept in PlayerPrefs: music and effects volume (0 to 1) and fullscreen.
    /// Setting a value stores it; <see cref="Save"/> writes the stored values to disk.
    /// Also whether the field guide has been seen this run, which is never saved.
    /// </summary>
    public static class GameSettings
    {
        public const string DefaultKeyPrefix = "AS.Settings.";
        /// <summary>Lowest mixer level, used for a volume of 0.</summary>
        public const float SilentDecibels = -80f;

        /// <summary>PlayerPrefs key prefix. Tests point this at throwaway keys so they never touch real settings.</summary>
        public static string KeyPrefix { get; set; } = DefaultKeyPrefix;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyPrefix + "MusicVolume", 1f);
            set => PlayerPrefs.SetFloat(KeyPrefix + "MusicVolume", Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KeyPrefix + "SfxVolume", 1f);
            set => PlayerPrefs.SetFloat(KeyPrefix + "SfxVolume", Mathf.Clamp01(value));
        }

        /// <summary>Defaults to whatever the window is now, so a first launch keeps the player's choice.</summary>
        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KeyPrefix + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            set => PlayerPrefs.SetInt(KeyPrefix + "Fullscreen", value ? 1 : 0);
        }

        /// <summary>
        /// Set once the guide has been shown, so Play skips it for the rest of this run. Not saved, so
        /// every launch shows it again before the first game.
        /// </summary>
        public static bool GuideSeen { get; set; }

        // Domain reload is off for Play mode, so statics would otherwise carry over between editor sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForNewRun() => GuideSeen = false;

        public static void Save() => PlayerPrefs.Save();

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyPrefix + "MusicVolume");
            PlayerPrefs.DeleteKey(KeyPrefix + "SfxVolume");
            PlayerPrefs.DeleteKey(KeyPrefix + "Fullscreen");
        }

        /// <summary>A 0 to 1 slider value as a mixer level: full is 0 dB, half is about -6 dB, 0 is silent.</summary>
        public static float ToDecibels(float volume) =>
            volume <= 0.0001f ? SilentDecibels : Mathf.Max(SilentDecibels, Mathf.Log10(volume) * 20f);
    }
}
