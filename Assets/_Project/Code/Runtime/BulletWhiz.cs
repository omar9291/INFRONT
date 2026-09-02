using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// "Die Kugel zischt am Kopf vorbei." Sitzt nur am Spieler-Prefab. Der
    /// Server prueft bei jedem gegnerischen Schuss, ob die Kugel dicht am
    /// Spieler vorbeifliegt (ohne zu treffen) - wenn ja, hoert der Spieler ein
    /// kurzes Zischen und die Kamera zuckt leicht. Das ist der Kern des
    /// "es fuehlt sich nach Kampf an"-Gefuehls.
    ///
    /// Server-autoritativ wie alles andere: nur der Server entscheidet, der
    /// betroffene Client bekommt den Ton per RPC.
    ///
    /// NICHT pruefbar: wie es klingt. Geprueft wird die Vorbei-Flug-Geometrie
    /// (<see cref="PassesNear"/>) und dass der Ton beim Besitzer ankommt.
    /// </summary>
    public sealed class BulletWhiz : NetworkBehaviour
    {
        FirstPersonCamera _fpc;

        public int WhizCountForTests { get; private set; }

        public override void OnNetworkSpawn()
        {
            // Nicht abschalten - der Server muss ServerReport aufrufen koennen.
            // Der Ton/Kamera-Ruck laeuft ohnehin nur ueber die SendTo.Owner-RPC.
            if (IsOwner && Camera.main != null)
                _fpc = Camera.main.GetComponent<FirstPersonCamera>();
        }

        /// <summary>Nur Server: eine Kugel ist dicht vorbeigeflogen.
        /// side &lt; 0 = links, &gt; 0 = rechts.</summary>
        public void ServerReport(float side)
        {
            if (!IsServer) return;
            WhizRpc(side);
        }

        [Rpc(SendTo.Owner)]
        void WhizRpc(float side)
        {
            WhizCountForTests++;
            AudioService.Instance?.Play2D(SoundId.Zischen, 0.7f);
            _fpc?.Shake(0.06f, 0.12f);   // kurzes Zusammenzucken
        }

        /// <summary>Nur Tests: den Vorbei-Flug-Ton direkt ausloesen.</summary>
        public void ServerReportForTests(float side) => WhizRpc(side);

        /// <summary>
        /// Fliegt die Strecke [origin .. origin + dir*len] dicht an
        /// <paramref name="point"/> vorbei - naeher als <paramref name="radius"/>,
        /// aber ohne ihn zu treffen - und ist die Vorbei-Stelle weiter als
        /// <paramref name="minRange"/> vom Schuetzen weg?
        /// <paramref name="side"/> ist die Seite (Vorzeichen aus dem Kreuzprodukt).
        /// </summary>
        public static bool PassesNear(Vector3 origin, Vector3 dir, float len,
            Vector3 point, float radius, float minRange, out float side)
        {
            side = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir = dir.normalized;

            float along = Vector3.Dot(point - origin, dir);
            if (along < minRange || along > len) return false;

            Vector3 closest = origin + dir * along;
            Vector3 off = point - closest;
            float d = off.magnitude;
            if (d < 0.35f || d > radius) return false;   // < 0.35 = das war ein Treffer

            side = Mathf.Sign(Vector3.Dot(Vector3.Cross(dir, off), Vector3.up));
            return true;
        }
    }
}
