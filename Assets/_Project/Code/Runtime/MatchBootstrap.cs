using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Startet die Runde. In Phase 1 nur: Host starten, sobald die Szene laeuft.
    /// (Host = eigener Rechner ist Server und Spieler zugleich.)
    ///
    /// Spaeter ersetzt ein richtiges Menue diese Automatik.
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [SerializeField] bool _autoStartHost = true;

        void Start()
        {
            if (!_autoStartHost)
                return;

            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[Infront] Kein NetworkManager in der Szene.");
                return;
            }

            if (!manager.IsListening)
                manager.StartHost();
        }
    }
}
