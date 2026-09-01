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

        /// <summary>Spielmodus: Ausscheiden (jeder gegen jeden bis ein Team steht)
        /// oder Bombe (ein Team legt, das andere entschaerft).</summary>
        public enum Mode { Ausscheiden = 0, Bombe = 1 }

        /// <summary>Bild-Aufwertung: Voll (Tonemapping, Bloom, Vignette, Nebel ...)
        /// oder Schlicht (alles aus - Rueckfallebene, falls die volle Optik auf
        /// einem Rechner Streifen oder Ruckeln macht).</summary>
        public enum Graphics { Voll = 0, Schlicht = 1 }

        const string KeyTeamSize = "infront.teamSize";
        const string KeyDifficulty = "infront.difficulty";
        const string KeySensitivity = "infront.sensitivity";
        const string KeyMode = "infront.mode";
        const string KeySfxVolume = "infront.sfxVolume";
        const string KeyGraphics = "infront.graphics";

        public static int TeamSize { get; set; } = 3;
        public static Level Difficulty { get; set; } = Level.Normal;
        public static float MouseSensitivity { get; set; } = 0.1f;
        public static Mode GameMode { get; set; } = Mode.Ausscheiden;
        public static Graphics GraphicsQuality { get; set; } = Graphics.Voll;

        /// <summary>Gesamtlautstärke aller Töne, 0..1. Der <see cref="AudioService"/> multipliziert damit.</summary>
        public static float SfxVolume { get; set; } = 0.85f;

        static GameSettings() => Load();

        public static void Load()
        {
            TeamSize = Mathf.Clamp(PlayerPrefs.GetInt(KeyTeamSize, 3), 1, 10);
            Difficulty = (Level)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 1), 0, 2);
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 0.1f), 0.02f, 0.5f);
            GameMode = (Mode)Mathf.Clamp(PlayerPrefs.GetInt(KeyMode, 0), 0, 1);
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 0.85f));
            GraphicsQuality = (Graphics)Mathf.Clamp(PlayerPrefs.GetInt(KeyGraphics, 0), 0, 1);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KeyTeamSize, TeamSize);
            PlayerPrefs.SetInt(KeyDifficulty, (int)Difficulty);
            PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
            PlayerPrefs.SetInt(KeyMode, (int)GameMode);
            PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
            PlayerPrefs.SetInt(KeyGraphics, (int)GraphicsQuality);
            PlayerPrefs.Save();
        }
    }
}
