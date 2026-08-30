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
        [SerializeField] int _maxArmor = 100;

        readonly NetworkVariable<int> _current = new(
            100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _alive = new(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Schutzweste. Schluckt die Haelfte des Koerperschadens und verbraucht
        // sich dabei. Kopfschuesse ignorieren die Weste (siehe ApplyDamage).
        readonly NetworkVariable<int> _armor = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int Current => _current.Value;
        public int Max => _maxHealth;
        public int Armor => _armor.Value;
        public int MaxArmor => _maxArmor;
        public bool IsAlive => _alive.Value;

        /// <summary>Server + Clients: (vorher, nachher).</summary>
        public event Action<int, int> HealthChanged;
        /// <summary>Nur Server: wurde gerade getroffen. (Schaden, Verursacher-ClientId).</summary>
        public event Action<int, ulong> DamageTaken;
        /// <summary>Server + Clients: Figur ist gerade gestorben.</summary>
        public event Action Died;
        /// <summary>Nur Server: gestorben, mit dem Verursacher (kann null sein).</summary>
        public event Action<GameObject> DiedWithInstigator;
        /// <summary>Server + Clients: Figur wurde wiederbelebt.</summary>
        public event Action Revived;

        /// <summary>NUR beim getroffenen Client: Weltposition des Angreifers.</summary>
        public event Action<Vector3> LocalDamageFrom;

        /// <summary>NUR beim getoeteten Client: NetworkObjectId des Toeters (0 = unbekannt).</summary>
        public event Action<ulong> LocalKilledBy;

        GameObject _lastInstigator;

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

        public void ApplyDamage(int amount, ulong sourceClientId) => ApplyDamage(amount, (GameObject)null, false);

        public void ApplyDamage(int amount, GameObject instigator) => ApplyDamage(amount, instigator, false);

        /// <param name="ignoreArmor">true bei Kopfschuss - die Weste hilft dann nicht.</param>
        public void ApplyDamage(int amount, GameObject instigator, bool ignoreArmor)
        {
            if (!IsServer || !_alive.Value || amount <= 0)
                return;

            _lastInstigator = instigator;

            // Schutzweste: schluckt die Haelfte des ankommenden Schadens,
            // solange sie Punkte hat, und verbraucht diese dabei.
            if (!ignoreArmor && _armor.Value > 0)
            {
                int half = amount / 2;
                int fromArmor = Mathf.Min(_armor.Value, half);
                _armor.Value -= fromArmor;
                amount -= fromArmor;
            }

            int newValue = Mathf.Max(0, _current.Value - amount);
            _current.Value = newValue;

            if (instigator != null)
                DamageDirectionRpc(instigator.transform.position);

            ulong sourceId = NetworkManager != null ? NetworkManager.ServerClientId : 0;
            DamageTaken?.Invoke(amount, sourceId);

            if (newValue == 0)
            {
                _alive.Value = false;
                ulong killerId = 0;
                if (instigator != null)
                {
                    var no = instigator.GetComponentInParent<NetworkObject>();
                    if (no != null) killerId = no.NetworkObjectId;
                }
                KilledByRpc(killerId);
            }
        }

        /// <summary>
        /// Nur Server: volles Leben, wieder lebendig. Die Weste wird hier NICHT
        /// angefasst - die verwaltet der MatchManager (Kauf bzw. Verlust bei Tod).
        /// </summary>
        public void ResetFull()
        {
            if (!IsServer)
                return;

            _lastInstigator = null;
            _current.Value = _maxHealth;
            _alive.Value = true;
        }

        /// <summary>Nur Server: Weste geben (Kaufmenue).</summary>
        public void ServerGiveArmor(int amount)
        {
            if (IsServer)
                _armor.Value = Mathf.Clamp(amount, 0, _maxArmor);
        }

        /// <summary>Nur Server: Weste weg (Tod, Matchstart).</summary>
        public void ServerClearArmor()
        {
            if (IsServer)
                _armor.Value = 0;
        }

        [Rpc(SendTo.Owner)]
        void DamageDirectionRpc(Vector3 attackerPosition)
        {
            LocalDamageFrom?.Invoke(attackerPosition);
        }

        [Rpc(SendTo.Owner)]
        void KilledByRpc(ulong killerObjectId) => LocalKilledBy?.Invoke(killerObjectId);

        void OnCurrentChanged(int previous, int current) => HealthChanged?.Invoke(previous, current);

        void OnAliveChanged(bool previous, bool current)
        {
            if (!previous && current)
            {
                Revived?.Invoke();
            }
            else if (previous && !current)
            {
                Died?.Invoke();
                if (IsServer)
                    DiedWithInstigator?.Invoke(_lastInstigator);
            }
        }
    }
}
