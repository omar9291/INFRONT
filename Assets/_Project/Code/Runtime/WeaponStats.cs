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

        public string DisplayName = "Sturmgewehr";
        [Tooltip("Auf welchem Platz die Waffe gefuehrt wird.")]
        public Slot SlotKind = Slot.Primaer;
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
