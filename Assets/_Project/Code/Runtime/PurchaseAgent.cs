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
        [SerializeField] AbilityCatalog _abilityCatalog;
        [SerializeField] int _armorPrice = 1000;
        [SerializeField] int _kitPrice = 400;

        Wallet _wallet;
        NetworkWeapon _weapon;
        Health _health;
        BombAction _bomb;
        TeamMember _team;
        AbilityHolder _abilities;

        public int ArmorPrice => _armorPrice;
        public int KitPrice => _kitPrice;
        public WeaponCatalog Catalog => _catalog;
        public AbilityCatalog AbilityCatalog => _abilityCatalog;

        void Awake()
        {
            _wallet = GetComponent<Wallet>();
            _weapon = GetComponent<NetworkWeapon>();
            _health = GetComponent<Health>();
            _bomb = GetComponent<BombAction>();
            _team = GetComponent<TeamMember>();
            _abilities = GetComponent<AbilityHolder>();
        }

        bool IsBot => GetComponent<BotBrain>() != null;

        // ---- Anfragen vom besitzenden Client ----

        [Rpc(SendTo.Server)]
        public void RequestBuyWeaponRpc(int buyIndex) => ServerBuyWeapon(buyIndex);

        [Rpc(SendTo.Server)]
        public void RequestBuyArmorRpc() => ServerBuyArmor();

        [Rpc(SendTo.Server)]
        public void RequestBuyKitRpc() => ServerBuyKit();

        [Rpc(SendTo.Server)]
        public void RequestBuyAbilityRpc(int abilityIndex) => ServerBuyAbility(abilityIndex);

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

        /// <summary>Nur Server: eine Faehigkeit kaufen (Index in den
        /// AbilityCatalog). Gibt zurueck, ob es geklappt hat.</summary>
        public bool ServerBuyAbility(int abilityIndex)
        {
            if (!CanBuyNow() || _abilityCatalog == null || _abilities == null)
                return false;

            var stats = _abilityCatalog.Get(abilityIndex);
            if (stats == null) return false;
            if (_abilities.ServerHas(stats.Kind)) return false;   // schon vorhanden
            if (!_wallet.ServerTrySpend(stats.Price)) return false;

            if (!_abilities.ServerGrant(stats.Kind))
            {
                _wallet.ServerAdd(stats.Price);   // Rueckbuchung, falls doch nicht
                return false;
            }
            return true;
        }

        /// <summary>Nur Server. Entschaerfungs-Kit: nur fuer Verteidiger im
        /// Bomben-Modus. Gibt zurueck, ob der Kauf geklappt hat.</summary>
        public bool ServerBuyKit()
        {
            if (!CanBuyNow() || _bomb == null) return false;

            var mm = MatchManager.Instance;
            if (mm == null || !mm.IsBombMode) return false;
            if (_team == null || _team.TeamId != mm.DefendingTeam) return false;
            if (_bomb.HasKit) return false;
            if (!_wallet.ServerTrySpend(_kitPrice)) return false;

            _bomb.ServerGiveKit();
            return true;
        }
    }
}
