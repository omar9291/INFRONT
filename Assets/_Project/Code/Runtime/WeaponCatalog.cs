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

        /// <summary>
        /// Was im Kaufmenue angeboten wird. Ein Eintrag verweist auf zwei
        /// Waffen aus <see cref="Weapons"/>: eine fuer den Spieler, eine
        /// (abgeschwaechte) fuer den Bot. Ueber das Netz geht nur der Index
        /// in diese Liste.
        /// </summary>
        [System.Serializable]
        public struct BuyEntry
        {
            public string DisplayName;
            public int Price;
            [Tooltip("Index in Weapons fuer einen menschlichen Spieler.")]
            public int PlayerWeaponIndex;
            [Tooltip("Index in Weapons fuer einen Bot (etwas schwaecher).")]
            public int BotWeaponIndex;
        }

        public BuyEntry[] BuyEntries = new BuyEntry[0];

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

        public bool HasBuyEntry(int index) => index >= 0 && index < BuyEntries.Length;

        public BuyEntry GetBuyEntry(int index) =>
            HasBuyEntry(index) ? BuyEntries[index] : default;
    }
}
