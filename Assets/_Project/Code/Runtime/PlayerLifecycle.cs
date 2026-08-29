using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Tod und Respawn eines Spielers.
    ///
    ///  - Auf ALLEN Instanzen: bei Health.Died werden Koerper und Kollision
    ///    ausgeblendet, bei Health.Revived wieder eingeblendet.
    ///  - Nur auf dem Server: nach einer Wartezeit wird der Spieler an einen
    ///    Spawn-Punkt teleportiert und sein Leben zurueckgesetzt.
    ///
    /// Nichts wird geloescht oder neu erzeugt - die Figur wird nur aus- und
    /// wieder eingeschaltet.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class PlayerLifecycle : NetworkBehaviour, IRespawnable
    {
        [SerializeField] float _respawnDelay = 3f;
        [SerializeField] GameObject[] _hideOnDeath;

        Health _health;
        NetworkPlayerController _controller;
        CharacterController _characterController;
        NetworkTransform _netTransform;

        void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<NetworkPlayerController>();
            _characterController = GetComponent<CharacterController>();
            _netTransform = GetComponent<NetworkTransform>();
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
                _controller.SetMovementEnabled(false);
                StartCoroutine(RespawnAfterDelay());
            }
        }

        void OnRevived()
        {
            SetVisible(true);

            if (IsServer)
                _controller.SetMovementEnabled(true);
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelay);

            SpawnService.TryGetSpawn(out Vector3 position, out Quaternion rotation);
            TeleportTo(position, rotation);

            _health.ResetFull();
        }

        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            if (IsServer) TeleportTo(position, rotation);
        }

        void TeleportTo(Vector3 position, Quaternion rotation)
        {
            bool hadController = _characterController.enabled;
            _characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _characterController.enabled = hadController;

            if (_netTransform != null)
                _netTransform.Teleport(position, rotation, transform.localScale);
        }

        void SetVisible(bool visible)
        {
            if (_hideOnDeath != null)
            {
                foreach (var go in _hideOnDeath)
                    if (go != null)
                        go.SetActive(visible);
            }
        }
    }
}
