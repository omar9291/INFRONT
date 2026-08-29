using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Erzeugt beim Host-Start die Trainings-Dummies an festen Stellen.
    /// Nur der Server spawnt sie; NGO verteilt sie an alle Clients.
    /// </summary>
    public sealed class DummySpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject _dummyPrefab;
        [SerializeField] Vector3[] _positions =
        {
            new(6f, 1f, 8f),
            new(-6f, 1f, 10f),
            new(0f, 1f, 14f),
        };

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
            if (_done || _dummyPrefab == null)
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
