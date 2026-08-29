using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Baeckt das NavMesh der Arena zur Laufzeit, sobald der Host laeuft.
    /// So bleibt die ganze Arena code-erzeugt, ohne Handklick im Editor.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class NavMeshBaker : MonoBehaviour
    {
        NavMeshSurface _surface;
        bool _baked;

        void Awake() => _surface = GetComponent<NavMeshSurface>();

        void Start()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Bake();
                return;
            }

            manager.OnServerStarted += Bake;
            if (manager.IsServer)
                Bake();
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= Bake;
        }

        void Bake()
        {
            if (_baked) return;
            _baked = true;

            _surface.BuildNavMesh();
            Debug.Log($"[Infront] NavMesh gebacken. Hat Daten: {_surface.navMeshData != null}");
        }
    }
}
