using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kennwerte einer Waffe. Als Asset im Projekt, damit Balance ohne
    /// Code-Aenderung geht (Menue "Assets/Create/Infront/Waffe").
    /// </summary>
    [CreateAssetMenu(menuName = "Infront/Waffe", fileName = "Waffe")]
    public sealed class WeaponStats : ScriptableObject
    {
        public enum Slot { Primaer = 0, Pistole = 1 }

        public string DisplayName = "Assault Rifle";
        [Tooltip("Auf welchem Platz die Waffe gefuehrt wird.")]
        public Slot SlotKind = Slot.Primaer;
        [Tooltip("Welcher Schuss-Ton beim Feuern gespielt wird.")]
        public SoundId ShotSound = SoundId.SchussGewehr;
        [Tooltip("Wechselzeit in Sekunden - so lange kann nach dem Umschalten nicht gefeuert werden.")]
        public float SwitchTime = 0.5f;

        [Tooltip("Schaden pro Treffer.")]
        public int Damage = 18;

        [Tooltip("Schuesse pro Sekunde.")]
        public float FireRate = 9f;

        [Tooltip("Patronen pro Magazin.")]
        public int MagazineSize = 30;

        [Tooltip("Nachladedauer in Sekunden.")]
        public float ReloadTime = 2f;

        [Tooltip("Maximale Schussreichweite in Metern.")]
        public float Range = 200f;

        [Tooltip("Sekunden zwischen zwei Schuessen.")]
        public float ShotInterval => FireRate > 0f ? 1f / FireRate : 0.1f;

        [Header("Zielen (rechte Maustaste)")]
        [Tooltip("Streuungs-Faktor beim Zielen ueber Kimme/Korn. 1 = kein Vorteil, 0.3 = deutlich praeziser.")]
        [Range(0f, 1f)] public float AdsSpreadMul = 0.35f;
        [Tooltip("Zoom-Faktor eines echten Zielfernrohrs. 0 oder 1 = kein Fernrohr (nur Kimme/Korn), " +
                 "4 = vierfache Vergroesserung mit schwarzem Fernrohr-Bild.")]
        public float ScopeZoom = 0f;

        [Header("Kopfschuss")]
        [Tooltip("Schadensfaktor bei Kopftreffer. 999 = sofort tot.")]
        public float HeadshotMultiplier = 999f;

        [Header("Rueckstoss (festes Muster, lernbar)")]
        [Tooltip("Vertikaler Kick pro Schuss in Grad (zieht nach oben).")]
        public float RecoilUp = 1.1f;
        [Tooltip("Seitlicher Kick pro Schuss in Grad (alterniert / driftet).")]
        public float RecoilSide = 0.35f;
        [Tooltip("Wie schnell der Rueckstoss zurueckgeht (Grad/Sekunde).")]
        public float RecoilRecovery = 14f;

        // --- Schritt 4: Waffenmasse und Streuung des Rueckstosses ------------
        // Bisher war der Rueckstoss vollstaendig vorhersagbar: fester Wert nach
        // oben, seitlich eine Sinuskurve ueber die Schusszahl. Man konnte ihn
        // also auswendig lernen und exakt ausgleichen. Diese beiden Werte
        // streuen ihn - Anteil vom Grundwert, 0.35 = plus/minus 35 %.
        [Range(0f, 1f)] public float RecoilRandomUp = 0.3f;
        [Range(0f, 1f)] public float RecoilRandomSide = 0.55f;

        // Zeit in Sekunden, bis die Waffe im Anschlag ist. Vorher war das fuer
        // jede Waffe gleich und mit rund 0.11 s praktisch sofort.
        public float AdsTime = 0.34f;

        // Wie stark die Waffe beim Drehen nachschwingt. 1 = wie bisher.
        // Schwere Waffen bekommen mehr, die Pistole weniger.
        public float SwayScale = 1f;

        [Header("Streuung (rechnet der Server)")]
        [Tooltip("Grundstreuung im Stehen (Grad).")]
        public float SpreadStand = 0.15f;
        [Tooltip("Zusatz beim Gehen (Grad).")]
        public float SpreadWalk = 1.4f;
        [Tooltip("Zusatz beim Sprinten (Grad).")]
        public float SpreadSprint = 3.2f;
        [Tooltip("Zusatz in der Luft (Grad).")]
        public float SpreadAir = 5f;
        [Tooltip("Zusatz pro abgegebenem Schuss (Grad, baut sich auf).")]
        public float SpreadPerShot = 0.5f;
        [Tooltip("Wie schnell die Aufbau-Streuung zurueckgeht (Grad/Sekunde).")]
        public float SpreadRecovery = 6f;
        [Tooltip("Maximale Gesamtstreuung (Grad).")]
        public float SpreadMax = 9f;
    }
}
