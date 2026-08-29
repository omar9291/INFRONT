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
        public string DisplayName = "Sturmgewehr";

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
    }
}
