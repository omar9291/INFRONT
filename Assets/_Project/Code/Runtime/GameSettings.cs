using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Vom Menue gewaehlte Optionen. Ueberleben den Szenenwechsel und werden
    /// dauerhaft gespeichert (PlayerPrefs), damit sie auch nach dem Beenden
    /// erhalten bleiben.
    /// </summary>
    public static class GameSettings
    {
        public enum Level { Leicht = 0, Normal = 1, Schwer = 2 }

        const string KeyTeamSize = "infront.teamSize";
        const string KeyDifficulty = "infront.difficulty";
        const string KeySensitivity = "infront.sensitivity";

        public static int TeamSize { get; set; } = 3;
        public static Level Difficulty { get; set; } = Level.Normal;
        public static float MouseSensitivity { get; set; } = 0.1f;

        static GameSettings() => Load();

        public static void Load()
        {
            TeamSize = Mathf.Clamp(PlayerPrefs.GetInt(KeyTeamSize, 3), 1, 10);
            Difficulty = (Level)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 1), 0, 2);
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 0.1f), 0.02f, 0.5f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KeyTeamSize, TeamSize);
            PlayerPrefs.SetInt(KeyDifficulty, (int)Difficulty);
            PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
            PlayerPrefs.Save();
        }
    }
}
