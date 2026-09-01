using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Liste aller Faehigkeiten des Spiels. Ueber das Netz geht nur der Index
    /// bzw. die <see cref="AbilityKind"/> - kein Name. Aufbau wie
    /// <see cref="WeaponCatalog"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Infront/Faehigkeiten-Katalog", fileName = "AbilityCatalog")]
    public sealed class AbilityCatalog : ScriptableObject
    {
        public AbilityStats[] Abilities = new AbilityStats[0];

        public AbilityStats Get(int index) =>
            index >= 0 && index < Abilities.Length ? Abilities[index] : null;

        public AbilityStats Find(AbilityKind kind)
        {
            foreach (var a in Abilities)
                if (a != null && a.Kind == kind) return a;
            return null;
        }

        public int IndexOf(AbilityKind kind)
        {
            for (int i = 0; i < Abilities.Length; i++)
                if (Abilities[i] != null && Abilities[i].Kind == kind) return i;
            return -1;
        }
    }
}
