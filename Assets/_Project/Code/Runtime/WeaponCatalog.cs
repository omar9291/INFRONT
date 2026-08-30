using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Liste aller Waffen des Spiels. Ueber das Netz geht nur der Index in
    /// diese Liste, kein Waffenname - dieselbe Sparsamkeit wie bei den
    /// Team-Nummern.
    /// </summary>
    [CreateAssetMenu(menuName = "Infront/Waffen-Katalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        public WeaponStats[] Weapons = new WeaponStats[0];

        public WeaponStats Get(int index) =>
            index >= 0 && index < Weapons.Length ? Weapons[index] : null;

        public int IndexOf(WeaponStats stats)
        {
            for (int i = 0; i < Weapons.Length; i++)
                if (Weapons[i] == stats) return i;
            return -1;
        }

        /// <summary>Erster Eintrag mit der gesuchten Slot-Art (Fallback: -1).</summary>
        public int FirstOfSlot(WeaponStats.Slot slot)
        {
            for (int i = 0; i < Weapons.Length; i++)
                if (Weapons[i] != null && Weapons[i].SlotKind == slot) return i;
            return -1;
        }
    }
}
