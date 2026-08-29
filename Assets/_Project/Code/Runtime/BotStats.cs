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
    }
}
