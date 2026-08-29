using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Erzeugt beim Host-Start die Bots. Nur der Server spawnt sie.
    /// Wartet, bis das NavMesh da ist.
    /// </summary>
    public sealed class BotSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject _botPrefab;
        [SerializeField] int _count = 3;
        [SerializeField] float _delay = 0.5f;

        bool _done;

        void Start()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return;

            manager.OnServerStarted += Schedule;
            if (manager.IsServer)
                Schedule();
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= Schedule;
        }

        void Schedule()
        {
            if (_done) return;
            _done = true;
            Invoke(nameof(SpawnAll), _delay);
        }

        void SpawnAll()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || _botPrefab == null)
                return;

            for (int i = 0; i < _count; i++)
            {
                SpawnService.TryGetSpawn(out Vector3 position, out Quaternion rotation);
                manager.SpawnManager.InstantiateAndSpawn(
                    _botPrefab,
                    ownerClientId: NetworkManager.ServerClientId,
                    destroyWithScene: true,
                    position: position,
                    rotation: rotation);
            }
        }
    }
}
