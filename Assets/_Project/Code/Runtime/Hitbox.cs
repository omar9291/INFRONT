using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Welcher Koerperbereich getroffen wurde. Realismus-Etappe Schritt 5:
    /// vorher gab es nur "Kopf ja/nein".
    /// </summary>
    public enum KoerperZone
    {
        Torso = 0,
        Kopf = 1,
        Arm = 2,
        Bein = 3,
    }

    /// <summary>
    /// Eine Trefferflaeche einer Figur. Liegt auf der Hitbox-Ebene, damit sie
    /// nur fuers Schiessen zaehlt und die Bewegung nicht stoert. Verweist auf
    /// die Health der Figur.
    ///
    /// Seit Schritt 5 gibt es vier Zonen statt nur Kopf/Koerper. Die alte
    /// Eigenschaft <see cref="IsHead"/> bleibt bestehen und leitet sich aus der
    /// Zone ab - vorhandener Code funktioniert unveraendert weiter.
    /// </summary>
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField] bool _isHead;
        [SerializeField] Health _owner;
        [SerializeField] KoerperZone _zone = KoerperZone.Torso;

        public bool IsHead => _zone == KoerperZone.Kopf || _isHead;
        public Health Owner => _owner;
        public KoerperZone Zone => _zone;

        // Vom SceneBuilder gesetzt (alter Weg, bleibt gueltig)
        public void Configure(bool isHead, Health owner)
        {
            _isHead = isHead;
            _owner = owner;
            _zone = isHead ? KoerperZone.Kopf : KoerperZone.Torso;
        }

        /// <summary>Neuer Weg seit Schritt 5: die Zone direkt setzen.</summary>
        public void Configure(KoerperZone zone, Health owner)
        {
            _zone = zone;
            _isHead = zone == KoerperZone.Kopf;
            _owner = owner;
        }

        /// <summary>
        /// Schadensfaktor der Zone. Kopf ist gesondert geregelt (ueber
        /// WeaponStats.HeadshotMultiplier), deshalb steht er hier auf 1.
        ///
        /// Arme und Beine schlucken einen Teil des Schadens - ein Streifschuss
        /// am Bein toetet niemanden. Dafuer haben sie Folgen (langsamer,
        /// unruhigere Waffe), die den geringeren Schaden aufwiegen.
        /// </summary>
        public static float SchadenFaktor(KoerperZone zone) => zone switch
        {
            KoerperZone.Kopf => 1f,
            KoerperZone.Torso => 1f,
            KoerperZone.Arm => 0.65f,
            KoerperZone.Bein => 0.7f,
            _ => 1f,
        };

        /// <summary>
        /// Wie wahrscheinlich ein Treffer dieser Zone eine Blutung ausloest.
        /// Torso und Beine bluten am ehesten, der Kopf ist meist ohnehin toedlich.
        /// </summary>
        public static float BlutungsChance(KoerperZone zone) => zone switch
        {
            KoerperZone.Kopf => 0.1f,
            KoerperZone.Torso => 0.45f,
            KoerperZone.Arm => 0.3f,
            KoerperZone.Bein => 0.5f,
            _ => 0.3f,
        };
    }
}
