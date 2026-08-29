using System;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Leben einer Figur. Der Wert ist eine NetworkVariable, die NUR der Server
    /// schreibt. Clients lesen ihn nur (fuer HUD, Effekte).
    /// </summary>
    public sealed class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField] int _maxHealth = 100;

        readonly NetworkVariable<int> _current = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        readonly NetworkVariable<bool> _alive = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int Current => _current.Value;
        public int Max => _maxHealth;
        public bool IsAlive => _alive.Value;

        /// <summary>Server + Clients: (vorher, nachher).</summary>
        public event Action<int, int> HealthChanged;

        /// <summary>Nur Server: wurde gerade getroffen. (Schaden, Verursacher).</summary>
        public event Action<int, ulong> DamageTaken;

        /// <summary>Server + Clients: Figur ist gerade gestorben.</summary>
        public event Action Died;

        /// <summary>Server + Clients: Figur wurde wiederbelebt.</summary>
        public event Action Revived;

        public override void OnNetworkSpawn()
        {
            _current.OnValueChanged += OnCurrentChanged;
            _alive.OnValueChanged += OnAliveChanged;

            if (IsServer)
            {
                _current.Value = _maxHealth;
                _alive.Value = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            _current.OnValueChanged -= OnCurrentChanged;
            _alive.OnValueChanged -= OnAliveChanged;
        }

        public void ApplyDamage(int amount, ulong sourceClientId)
        {
            if (!IsServer || !_alive.Value || amount <= 0)
                return;

            int newValue = Mathf.Max(0, _current.Value - amount);
            _current.Value = newValue;
            DamageTaken?.Invoke(amount, sourceClientId);

            if (newValue == 0)
                _alive.Value = false;
        }

        /// <summary>Nur Server: volles Leben, wieder lebendig.</summary>
        public void ResetFull()
        {
            if (!IsServer)
                return;

            _current.Value = _maxHealth;
            _alive.Value = true;
        }

        void OnCurrentChanged(int previous, int current)
        {
            HealthChanged?.Invoke(previous, current);
        }

        void OnAliveChanged(bool previous, bool current)
        {
            if (!previous && current)
                Revived?.Invoke();
            else if (previous && !current)
                Died?.Invoke();
        }
    }
}
