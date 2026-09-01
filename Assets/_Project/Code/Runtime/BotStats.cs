using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Verhaltens-Kennwerte eines Bots. Als Asset, damit spaeter
    /// Schwierigkeitsstufen einfach neue Assets sind (kein neuer Code).
    /// Menue "Assets/Create/Infront/Bot".
    /// </summary>
    [CreateAssetMenu(menuName = "Infront/Bot", fileName = "Bot")]
    public sealed class BotStats : ScriptableObject
    {
        [Header("Bewegung")]
        public float MoveSpeed = 4.5f;
        public float PatrolRadius = 12f;
        [Tooltip("Wunschabstand zum Ziel im Kampf.")]
        public float CombatRange = 9f;

        [Header("Wahrnehmung")]
        public float ViewDistance = 28f;
        [Tooltip("Sichtkegel-Halbwinkel in Grad.")]
        public float ViewAngle = 60f;
        [Tooltip("Sekunden, die der Bot das Ziel nach Sichtverlust noch sucht.")]
        public float MemoryTime = 4f;

        [Header("Zielen")]
        [Tooltip("Reaktionszeit, bis der Bot nach dem Entdecken feuert (Sekunden).")]
        public float ReactionTime = 0.35f;
        [Tooltip("Zufaelliger Zielfehler in Grad. Groesser = schlechter.")]
        public float AimSpread = 5f;
        [Tooltip("Wie schnell der Bot sein Ziel einfaengt (Grad/Sekunde). Klein = traeges Nachziehen.")]
        public float AimTrackSpeed = 220f;

        [Header("Charakter (ab Nacht 8)")]
        [Range(0f, 1f)]
        [Tooltip("0 = haelt Winkel und wartet, 1 = drueckt aggressiv vor.")]
        public float Aggression = 0.5f;
        [Range(0f, 1.5f)]
        [Tooltip("Hoervermoegen: 1 = normal, mehr = hoert Schuesse/Schritte weiter.")]
        public float Hearing = 1f;
        [Range(0f, 1f)]
        [Tooltip("Wie oft der Bot etwas ansagt / auf Verbuendete achtet.")]
        public float Teamwork = 0.5f;
    }
}
