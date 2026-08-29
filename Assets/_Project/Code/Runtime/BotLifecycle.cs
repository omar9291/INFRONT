using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Infront
{
    /// <summary>
    /// Tod und Respawn eines Bots. Wie beim Spieler: ausblenden und stillstellen,
    /// nichts loeschen. Nach einer Wartezeit teleportiert der Server zum Spawn.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(BotBrain))]
    public sealed class BotLifecycle : NetworkBehaviour
    {
        [SerializeField] float _respawnDelay = 4f;
        [SerializeField] GameObject[] _hideOnDeath;

        Health _health;
        BotBrain _brain;
        NavMeshAgent _agent;

        void Awake()
        {
            _health = GetComponent<Health>();
            _brain = GetComponent<BotBrain>();
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            _health.Died += OnDied;
            _health.Revived += OnRevived;
        }

        public override void OnNetworkDespawn()
        {
            _health.Died -= OnDied;
            _health.Revived -= OnRevived;
        }

        void OnDied()
        {
            SetVisible(false);
            if (IsServer)
            {
                _brain.SetActive(false);
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                    _agent.ResetPath();
                StartCoroutine(RespawnAfterDelay());
            }
        }

        void OnRevived()
        {
            SetVisible(true);
            if (IsServer)
                _brain.SetActive(true);
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelay);

            SpawnService.TryGetSpawn(out Vector3 position, out Quaternion rotation);
            if (_agent != null && _agent.enabled)
                _agent.Warp(position);
            else
                transform.position = position;

            _health.ResetFull();
        }

        void SetVisible(bool visible)
        {
            if (_hideOnDeath == null) return;
            foreach (var go in _hideOnDeath)
                if (go != null)
                    go.SetActive(visible);
        }
    }
}
