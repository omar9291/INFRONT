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

        /// <summary>Anzeige-Modus: Vollbild (randloses Fenster in Bildschirmgroesse)
        /// oder Fenster (1280x720, damit man leicht rauswechseln kann).</summary>
        public enum Anzeige { Vollbild = 0, Fenster = 1 }

        /// <summary>
        /// Farbmodus fuer Menschen, die Farben anders sehen.
        ///
        /// Das eigentliche Problem in diesem Spiel ist nicht Blau gegen Rot -
        /// das unterscheidet fast jeder. Es ist die Lebensanzeige: gruen, gelb,
        /// rot. Rot-Gruen-Schwaeche ist die haeufigste Form, und genau diese
        /// drei Farben laufen dann ineinander.
        /// </summary>
        public enum Farbmodus
        {
            /// <summary>Wie bisher.</summary>
            Standard = 0,
            /// <summary>Rot-Gruen-Schwaeche: Leben laeuft blau - gelb - magenta.</summary>
            RotGruen = 1,
            /// <summary>Blau-Gelb-Schwaeche: Leben laeuft gruen - weiss - rot.</summary>
            BlauGelb = 2,
            /// <summary>Nur Helligkeit, kaum Farbe. Fuer sehr starke Einschraenkung.</summary>
            HoherKontrast = 3,
        }

        const string KeyTeamSize = "infront.teamSize";
        const string KeyDifficulty = "infront.difficulty";
        const string KeySensitivity = "infront.sensitivity";
        const string KeyMode = "infront.mode";
        const string KeySfxVolume = "infront.sfxVolume";
        const string KeyGraphics = "infront.graphics";
        const string KeyDisplay = "infront.display";
        const string KeyColorMode = "infront.colorMode";
        const string KeyUiScale = "infront.uiScale";
        const string KeyReduceMotion = "infront.reduceMotion";
        const string KeyToggleAim = "infront.toggleAim";
        const string KeyToggleCrouch = "infront.toggleCrouch";
        const string KeyToggleSprint = "infront.toggleSprint";
        const string KeyCrosshair = "infront.crosshairScale";

        public static int TeamSize { get; set; } = 3;
        public static Level Difficulty { get; set; } = Level.Normal;
        public static float MouseSensitivity { get; set; } = 0.1f;
        public static Mode GameMode { get; set; } = Mode.Ausscheiden;
        public static Graphics GraphicsQuality { get; set; } = Graphics.Voll;
        public static Anzeige DisplayMode { get; set; } = Anzeige.Vollbild;

        /// <summary>Gesamtlautstärke aller Töne, 0..1. Der <see cref="AudioService"/> multipliziert damit.</summary>
        public static float SfxVolume { get; set; } = 0.85f;

        // ------------------------------------------------------------------
        //  Zugaenglichkeit
        // ------------------------------------------------------------------

        /// <summary>Wie Farben gewaehlt werden. Siehe <see cref="Farbmodus"/>.</summary>
        public static Farbmodus ColorMode { get; set; } = Farbmodus.Standard;

        /// <summary>Groesse der gesamten Oberflaeche, 0,8 bis 1,6. Wirkt auf
        /// Menue UND Anzeige im Spiel, weil beide am selben Panel haengen.</summary>
        public static float UiScale { get; set; } = 1f;

        /// <summary>
        /// Weniger Bewegung: Atem-Schwenk, Waffenwippen und Kamerastoesse
        /// werden stark gedaempft. Fuer alle, denen von der Kamerabewegung
        /// schlecht wird - das ist bei Ego-Spielen haeufig und hat nichts mit
        /// Koennen zu tun.
        /// </summary>
        public static bool ReduceMotion { get; set; }

        /// <summary>Zielen umschalten statt gedrueckt halten.</summary>
        public static bool ToggleAim { get; set; }

        /// <summary>Ducken umschalten statt gedrueckt halten.</summary>
        public static bool ToggleCrouch { get; set; }

        /// <summary>Sprint umschalten statt gedrueckt halten.</summary>
        public static bool ToggleSprint { get; set; }

        /// <summary>Groesse des Fadenkreuzes, 0,6 bis 2,0.</summary>
        public static float CrosshairScale { get; set; } = 1f;

        /// <summary>
        /// Faktor fuer alles, was die Kamera oder die Waffe bewegt. 1 = normal,
        /// klein = ruhig. Eine Stelle, damit nicht jedes Bauteil selbst
        /// entscheidet, was "weniger Bewegung" heisst.
        /// </summary>
        public static float BewegungsFaktor => ReduceMotion ? 0.15f : 1f;

        static GameSettings() => Load();

        public static void Load()
        {
            TeamSize = Mathf.Clamp(PlayerPrefs.GetInt(KeyTeamSize, 3), 1, 10);
            Difficulty = (Level)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 1), 0, 2);
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 0.1f), 0.02f, 0.5f);
            GameMode = (Mode)Mathf.Clamp(PlayerPrefs.GetInt(KeyMode, 0), 0, 1);
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 0.85f));
            GraphicsQuality = (Graphics)Mathf.Clamp(PlayerPrefs.GetInt(KeyGraphics, 0), 0, 1);
            DisplayMode = (Anzeige)Mathf.Clamp(PlayerPrefs.GetInt(KeyDisplay, 0), 0, 1);
            ColorMode = (Farbmodus)Mathf.Clamp(PlayerPrefs.GetInt(KeyColorMode, 0), 0, 3);
            UiScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyUiScale, 1f), 0.8f, 1.6f);
            ReduceMotion = PlayerPrefs.GetInt(KeyReduceMotion, 0) != 0;
            ToggleAim = PlayerPrefs.GetInt(KeyToggleAim, 0) != 0;
            ToggleCrouch = PlayerPrefs.GetInt(KeyToggleCrouch, 0) != 0;
            ToggleSprint = PlayerPrefs.GetInt(KeyToggleSprint, 0) != 0;
            CrosshairScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyCrosshair, 1f), 0.6f, 2f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KeyTeamSize, TeamSize);
            PlayerPrefs.SetInt(KeyDifficulty, (int)Difficulty);
            PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
            PlayerPrefs.SetInt(KeyMode, (int)GameMode);
            PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
            PlayerPrefs.SetInt(KeyGraphics, (int)GraphicsQuality);
            PlayerPrefs.SetInt(KeyDisplay, (int)DisplayMode);
            PlayerPrefs.SetInt(KeyColorMode, (int)ColorMode);
            PlayerPrefs.SetFloat(KeyUiScale, UiScale);
            PlayerPrefs.SetInt(KeyReduceMotion, ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(KeyToggleAim, ToggleAim ? 1 : 0);
            PlayerPrefs.SetInt(KeyToggleCrouch, ToggleCrouch ? 1 : 0);
            PlayerPrefs.SetInt(KeyToggleSprint, ToggleSprint ? 1 : 0);
            PlayerPrefs.SetFloat(KeyCrosshair, CrosshairScale);
            PlayerPrefs.Save();
        }
    }
}
