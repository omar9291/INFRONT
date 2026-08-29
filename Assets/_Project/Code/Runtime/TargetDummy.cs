using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Stehendes Trainings-Ziel. Nimmt Schaden, "stirbt" (blendet aus) und
    /// stellt sich nach einer Wartezeit mit vollem Leben wieder her.
    /// Bewegliche Gegner mit Gegenwehr kommen in Phase 3.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class TargetDummy : NetworkBehaviour
    {
        [SerializeField] float _reviveDelay = 4f;
        [SerializeField] GameObject[] _hideOnDeath;

        Health _health;

        void Awake() => _health = GetComponent<Health>();

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
                StartCoroutine(ReviveAfterDelay());
        }

        void OnRevived() => SetVisible(true);

        IEnumerator ReviveAfterDelay()
        {
            yield return new WaitForSeconds(_reviveDelay);
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
