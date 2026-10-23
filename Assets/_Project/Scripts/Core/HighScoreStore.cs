using UnityEngine;

namespace AS.Core
{
    /// <summary>The best score ever reached, kept in PlayerPrefs so it survives quitting the game.</summary>
    public static class HighScoreStore
    {
        public const string DefaultKey = "AS.HighScore";

        /// <summary>PlayerPrefs key. Tests point this at a throwaway key so they never touch the real best.</summary>
        public static string Key { get; set; } = DefaultKey;

        public static int Load() => PlayerPrefs.GetInt(Key, 0);

        /// <summary>Saves <paramref name="score"/> if it beats the stored best. True when it did.</summary>
        public static bool TrySubmit(int score)
        {
            if (score <= Load()) return false;
            PlayerPrefs.SetInt(Key, score);
            PlayerPrefs.Save();
            return true;
        }

        public static void Clear() => PlayerPrefs.DeleteKey(Key);
    }
}
