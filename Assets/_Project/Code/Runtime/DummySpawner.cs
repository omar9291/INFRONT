using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Erzeugt auf ausdruecklichen Test-Auftrag Trainings-Dummies.
    /// Ein normales Match enthaelt keine Trainingsziele.
    /// Nur der Server spawnt sie; NGO verteilt sie an alle Clients.
    /// </summary>
    public sealed class DummySpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject _dummyPrefab;
        [SerializeField] Vector3[] _positions =
        {
            new(6f, 0f, 8f),
            new(-6f, 0f, 10f),
            new(0f, 0f, 14f),
        };

        /// <summary>Vor Szenenstart setzen; der Test-Harness setzt es danach zurueck.</summary>
        public static bool SpawnForTests { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTestRequest() => SpawnForTests = false;

        bool _done;

        void Start()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
                return;

            manager.OnServerStarted += SpawnAll;
            if (manager.IsServer)
                SpawnAll();
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= SpawnAll;
        }

        void SpawnAll()
        {
            if (!SpawnForTests || _done || _dummyPrefab == null)
                return;
            _done = true;

            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
                return;

            foreach (var position in _positions)
            {
                manager.SpawnManager.InstantiateAndSpawn(
                    _dummyPrefab,
                    ownerClientId: NetworkManager.ServerClientId,
                    destroyWithScene: true,
                    position: position,
                    rotation: Quaternion.identity);
            }
        }
    }
}
