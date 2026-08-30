using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Eine Trefferflaeche einer Figur (Kopf oder Koerper). Liegt auf der
    /// Hitbox-Ebene, damit sie nur fuers Schiessen zaehlt und die Bewegung
    /// nicht stoert. Verweist auf die Health der Figur.
    /// </summary>
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField] bool _isHead;
        [SerializeField] Health _owner;

        public bool IsHead => _isHead;
        public Health Owner => _owner;

        // Vom SceneBuilder gesetzt
        public void Configure(bool isHead, Health owner)
        {
            _isHead = isHead;
            _owner = owner;
        }
    }
}
