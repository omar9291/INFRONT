using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Kaufmenue fuer den lokalen Spieler - Zustand und Tastatur. Oeffnet sich
    /// am Rundenanfang automatisch, B blendet es ein/aus, Ziffern kaufen.
    ///
    /// Gezeichnet wird das Menue im <see cref="HudController"/> (UI Toolkit).
    /// Diese Klasse haelt nur den Zustand und bietet dem HUD die noetigen
    /// Bausteine (Katalog, Preise, "hab ich schon"). Laeuft nur beim Besitzer.
    /// </summary>
    public sealed class BuyMenuHud : NetworkBehaviour
    {
        public static BuyMenuHud Local { get; private set; }

        PurchaseAgent _agent;
        NetworkWeapon _weapon;
        Health _health;
        Wallet _wallet;
        TeamMember _team;
        BombAction _bombAction;
        AbilityHolder _abilities;

        bool _open;
        bool _wasBuyTime;

        /// <summary>true, solange das Kaufmenue sichtbar ist (Maus frei, Sicht steht still).</summary>
        public static bool IsOpen { get; private set; }

        // ---- fuer das HUD ----
        public PurchaseAgent Agent => _agent;
        public WeaponCatalog Catalog => _agent != null ? _agent.Catalog : null;
        public AbilityCatalog AbilityCatalog => _agent != null ? _agent.AbilityCatalog : null;
        public int Money => _wallet != null ? _wallet.Money : 0;
        public bool ShouldShowMenu => BuyTimeNow() && _open;
        public bool ShouldShowHint => BuyTimeNow() && !_open;
        public bool KitOffered
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm != null && mm.IsBombMode && _team != null && _team.TeamId == mm.DefendingTeam;
            }
        }
        public bool OwnsWeapon(int buyIndex)
        {
            if (_weapon == null || Catalog == null || !Catalog.HasBuyEntry(buyIndex)) return false;
            var e = Catalog.GetBuyEntry(buyIndex);
            return _weapon.HasPrimary && _weapon.PrimaryIndex == e.PlayerWeaponIndex;
        }
        public bool OwnsArmor => _health != null && _health.Armor >= _health.MaxArmor;
        public bool OwnsKit => _bombAction != null && _bombAction.HasKit;
        public bool OwnsAbility(AbilityKind k) => _abilities != null && _abilities.HasKind(k);

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            Local = this;

            _agent = GetComponent<PurchaseAgent>();
            _weapon = GetComponent<NetworkWeapon>();
            _health = GetComponent<Health>();
            _wallet = GetComponent<Wallet>();
            _team = GetComponent<TeamMember>();
            _bombAction = GetComponent<BombAction>();
            _abilities = GetComponent<AbilityHolder>();
        }

        public override void OnNetworkDespawn()
        {
            IsOpen = false;
            if (Local == this) Local = null;
        }
        void OnDisable() => IsOpen = false;

        bool BuyTimeNow()
        {
            var mm = MatchManager.Instance;
            return mm != null && !mm.SuspendedForTests && mm.IsBuyTime
                   && _health != null && _health.IsAlive;
        }

        void Update()
        {
            bool buyTime = BuyTimeNow();
            if (buyTime && !_wasBuyTime) _open = true;
            if (!buyTime && _wasBuyTime) _open = false;
            _wasBuyTime = buyTime;

            IsOpen = buyTime && _open;
            if (!buyTime) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.bKey.wasPressedThisFrame) _open = !_open;
            if (!_open) return;

            if (kb.digit1Key.wasPressedThisFrame) BuyWeapon(0);
            if (kb.digit2Key.wasPressedThisFrame) BuyWeapon(1);
            if (kb.digit3Key.wasPressedThisFrame) BuyWeapon(2);
            if (kb.digit4Key.wasPressedThisFrame) BuyArmor();
            if (kb.digit5Key.wasPressedThisFrame) BuyKit();
            if (kb.digit6Key.wasPressedThisFrame) BuyAbility(0);
            if (kb.digit7Key.wasPressedThisFrame) BuyAbility(1);
            if (kb.digit8Key.wasPressedThisFrame) BuyAbility(2);
            if (kb.digit9Key.wasPressedThisFrame) BuyAbility(3);
            if (kb.digit0Key.wasPressedThisFrame) BuyAbility(4);
        }

        // ---- Kauf-Aktionen (vom HUD-Klick oder von der Tastatur) ----
        public void BuyWeapon(int buyIndex) { if (_agent != null) _agent.RequestBuyWeaponRpc(buyIndex); }
        public void BuyArmor() { if (_agent != null) _agent.RequestBuyArmorRpc(); }
        public void BuyKit() { if (_agent != null && KitOffered) _agent.RequestBuyKitRpc(); }
        public void BuyAbility(int index) { if (_agent != null) _agent.RequestBuyAbilityRpc(index); }

        public void Ready()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.RequestEndBuyTimeRpc();
            _open = false;
        }
    }
}
