using System;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Das Geld eines Kaempfers. Wie in Counter-Strike: Rundensieg, Niederlage
    /// und Abschuss bringen Geld, im Kaufmenue gibt man es aus.
    ///
    /// Nur der Server schreibt; Clients lesen den Wert fuers HUD.
    /// </summary>
    public sealed class Wallet : NetworkBehaviour
    {
        [SerializeField] int _maxMoney = 16000;

        readonly NetworkVariable<int> _money = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int Money => _money.Value;

        /// <summary>Server + Clients: der Kontostand hat sich geaendert.</summary>
        public event Action<int> MoneyChanged;

        public override void OnNetworkSpawn()
        {
            _money.OnValueChanged += (_, now) => MoneyChanged?.Invoke(now);
        }

        /// <summary>Nur Server: Kontostand fest setzen (Matchstart).</summary>
        public void ServerSet(int amount)
        {
            if (IsServer)
                _money.Value = Mathf.Clamp(amount, 0, _maxMoney);
        }

        /// <summary>Nur Server: Geld dazu (Rundenende, Abschuss).</summary>
        public void ServerAdd(int amount)
        {
            if (IsServer)
                _money.Value = Mathf.Clamp(_money.Value + amount, 0, _maxMoney);
        }

        /// <summary>Nur Server: bezahlen, wenn genug da ist. Gibt zurueck, ob es geklappt hat.</summary>
        public bool ServerTrySpend(int price)
        {
            if (!IsServer || price < 0 || _money.Value < price)
                return false;
            _money.Value -= price;
            return true;
        }
    }
}
