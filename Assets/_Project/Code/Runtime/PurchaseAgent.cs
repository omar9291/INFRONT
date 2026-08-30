using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Nimmt Kaeufe im Kaufmenue entgegen und prueft sie server-autoritativ:
    ///  - nur waehrend der Kaufzeit
    ///  - nur solange man lebt
    ///  - nur wenn das Geld reicht
    ///
    /// Der Spieler-Client schickt eine Anfrage per Rpc; ein Bot ruft die
    /// Server-Methoden direkt auf (siehe <see cref="BotBuyer"/>).
    /// </summary>
    public sealed class PurchaseAgent : NetworkBehaviour
    {
        [SerializeField] WeaponCatalog _catalog;
        [SerializeField] int _armorPrice = 1000;

        Wallet _wallet;
        NetworkWeapon _weapon;
        Health _health;

        public int ArmorPrice => _armorPrice;
        public WeaponCatalog Catalog => _catalog;

        void Awake()
        {
            _wallet = GetComponent<Wallet>();
            _weapon = GetComponent<NetworkWeapon>();
            _health = GetComponent<Health>();
        }

        bool IsBot => GetComponent<BotBrain>() != null;

        // ---- Anfragen vom besitzenden Client ----

        [Rpc(SendTo.Server)]
        public void RequestBuyWeaponRpc(int buyIndex) => ServerBuyWeapon(buyIndex);

        [Rpc(SendTo.Server)]
        public void RequestBuyArmorRpc() => ServerBuyArmor();

        // ---- Server-Pruefung ----

        bool CanBuyNow()
        {
            if (!IsServer || _catalog == null || _wallet == null) return false;
            if (_health != null && !_health.IsAlive) return false;
            var mm = MatchManager.Instance;
            return mm != null && mm.IsBuyTime;
        }

        /// <summary>Nur Server. Gibt zurueck, ob der Kauf geklappt hat.</summary>
        public bool ServerBuyWeapon(int buyIndex)
        {
            if (!CanBuyNow() || !_catalog.HasBuyEntry(buyIndex))
                return false;

            var entry = _catalog.GetBuyEntry(buyIndex);
            int weaponIndex = IsBot ? entry.BotWeaponIndex : entry.PlayerWeaponIndex;

            // Schon genau diese Waffe in der Hand? Dann nicht nochmal zahlen.
            if (_weapon != null && _weapon.HasPrimary && _weapon.PrimaryIndex == weaponIndex)
                return false;

            if (!_wallet.ServerTrySpend(entry.Price))
                return false;

            _weapon?.ServerSetPrimary(weaponIndex);
            return true;
        }

        /// <summary>Nur Server. Gibt zurueck, ob der Kauf geklappt hat.</summary>
        public bool ServerBuyArmor()
        {
            if (!CanBuyNow() || _health == null)
                return false;
            if (_health.Armor >= _health.MaxArmor)
                return false;
            if (!_wallet.ServerTrySpend(_armorPrice))
                return false;

            _health.ServerGiveArmor(_health.MaxArmor);
            return true;
        }
    }
}
