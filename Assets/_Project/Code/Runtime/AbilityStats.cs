using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Kennwerte einer Faehigkeit. Als Asset im Projekt, damit Balance ohne
    /// Code-Aenderung geht (Menue "Assets/Create/Infront/Faehigkeit") - genau
    /// wie bei den Waffen (<see cref="WeaponStats"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Infront/Faehigkeit", fileName = "Faehigkeit")]
    public sealed class AbilityStats : ScriptableObject
    {
        public AbilityKind Kind = AbilityKind.Rauchwand;
        public string DisplayName = "Smoke Wall";
        public AbilitySlot Slot = AbilitySlot.Q;

        [Tooltip("Preis im Kaufmenue.")]
        public int Price = 300;

        [Tooltip("Ladungen pro Runde.")]
        [Min(1)] public int Charges = 1;

        [Tooltip("Sekunden zwischen zwei Einsaetzen (0 = keine Sperre).")]
        public float Cooldown = 0f;

        [Tooltip("Wie lange der Effekt in der Welt bleibt.")]
        public float Duration = 15f;

        [Tooltip("Wirkradius in Metern.")]
        public float Radius = 4f;

        [Tooltip("Wie weit vor dem Werfer der Effekt entsteht.")]
        public float ThrowRange = 16f;

        /// <summary>
        /// Steht diese Ausruestung im Kaufmenue? Schritt 6: der Scan-Puls
        /// bleibt vollstaendig im Spiel, wird aber nicht mehr angeboten - er
        /// zeigt Gegner durch Waende und passt nicht zum Realismus.
        /// Auf true stellen und er ist sofort wieder da. Nichts geloescht.
        /// </summary>
        public bool Angeboten = true;

        /// <summary>
        /// Wirkt sofort beim Benutzer statt geworfen zu werden (Verbandspaket).
        /// </summary>
        public bool AmBenutzer = false;
    }
}
