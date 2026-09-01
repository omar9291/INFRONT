using System;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die Faehigkeiten eines Kaempfers - die "Werkzeuge" des Valorant-Wegs.
    ///
    /// Genau wie das Waffen-System aufgebaut:
    ///  - Faehigkeiten sind Assets (<see cref="AbilityStats"/>), kein Code
    ///  - server-autoritativ: der Client fragt (RequestUseRpc), der Server
    ///    entscheidet und erzeugt den Effekt
    ///  - Ladungen pro Runde, Abklingzeit
    ///  - gekauft im bestehenden Kaufmenue mit dem bestehenden Geld-System
    ///
    /// Tasten: Q = Platz 1, F = Platz 2, G = Granate. Ein Bot ruft
    /// <see cref="ServerTryUse"/> direkt auf.
    ///
    /// Haengt am Spieler- und am Bot-Prefab.
    /// </summary>
    public sealed class AbilityHolder : NetworkBehaviour
    {
        [SerializeField] AbilityCatalog _catalog;

        // Server-Wahrheit
        readonly AbilityKind[] _kinds = { AbilityKind.Keine, AbilityKind.Keine, AbilityKind.Keine };
        readonly double[] _cooldownEnd = new double[3];
        double _blindUntilServer;

        // Fuer HUD/Clients repliziert: 3x4 Bit Art + 3 Ladungszahlen
        readonly NetworkVariable<int> _packedKinds = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _charge0 = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _charge1 = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _charge2 = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        IAimSource _aim;
        Health _health;
        TeamMember _team;
        NetworkPlayerController _playerController;

        double ServerNow => NetworkManager.ServerTime.Time;

        /// <summary>Nur beim Besitzer: gerade wurde man geblendet (Sekunden).</summary>
        public event Action<float> OwnerBlinded;

        public AbilityCatalog Catalog => _catalog;
        public bool IsBlindForTests => ServerNow < _blindUntilServer;

        void Awake()
        {
            _aim = GetComponent<IAimSource>();
            _health = GetComponent<Health>();
            _team = GetComponent<TeamMember>();
            _playerController = GetComponent<NetworkPlayerController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                ServerClearLoadout();   // jede Runde ohne Faehigkeit starten (Kaufmenue)
        }

        void Update()
        {
            // Nur der besitzende Client liest die Tasten (wie bei der Waffe).
            if (_playerController == null || !IsOwner || _playerController.Input == null)
                return;
            if (PauseMenu.IsPaused || BuyMenuHud.IsOpen) return;

            int slot = _playerController.Input.UseAbilitySlot;
            if (slot >= 0 && slot <= 2)
                RequestUseRpc(slot);
        }

        // ---- Ladungen lesen (HUD) -------------------------------------

        public AbilityKind KindInSlot(int slot)
        {
            int p = _packedKinds.Value;
            return slot switch
            {
                0 => (AbilityKind)(p & 0xF),
                1 => (AbilityKind)((p >> 4) & 0xF),
                2 => (AbilityKind)((p >> 8) & 0xF),
                _ => AbilityKind.Keine,
            };
        }

        public int ChargesInSlot(int slot) => slot switch
        {
            0 => _charge0.Value,
            1 => _charge1.Value,
            2 => _charge2.Value,
            _ => 0,
        };

        public float CooldownLeft(int slot)
        {
            if (slot < 0 || slot > 2) return 0f;
            return Mathf.Max(0f, (float)(_cooldownEnd[slot] - ServerNow));
        }

        // ---- Server: Bestueckung ------------------------------------

        /// <summary>Nur Server: alle Faehigkeiten entfernen (Rundenstart / nach Tod).</summary>
        public void ServerClearLoadout()
        {
            if (!IsServer) return;
            for (int i = 0; i < 3; i++) { _kinds[i] = AbilityKind.Keine; _cooldownEnd[i] = 0; }
            _packedKinds.Value = 0;
            _charge0.Value = _charge1.Value = _charge2.Value = 0;
        }

        /// <summary>Nur Server: eine gekaufte Faehigkeit auf ihren Platz legen
        /// und die Ladungen auffuellen. Gibt zurueck, ob es geklappt hat.</summary>
        public bool ServerGrant(AbilityKind kind)
        {
            if (!IsServer || _catalog == null || kind == AbilityKind.Keine) return false;
            var stats = _catalog.Find(kind);
            if (stats == null) return false;

            int slot = (int)stats.Slot;
            _kinds[slot] = kind;
            SetCharge(slot, stats.Charges);
            _cooldownEnd[slot] = 0;
            RepackKinds();
            return true;
        }

        /// <summary>Nur Server: die Ladungen aller gefuehrten Faehigkeiten wieder
        /// auffuellen (Rundenstart, wenn man ueberlebt hat).</summary>
        public void ServerRefreshCharges()
        {
            if (!IsServer || _catalog == null) return;
            for (int slot = 0; slot < 3; slot++)
            {
                if (_kinds[slot] == AbilityKind.Keine) continue;
                var stats = _catalog.Find(_kinds[slot]);
                if (stats != null) SetCharge(slot, stats.Charges);
                _cooldownEnd[slot] = 0;
            }
        }

        /// <summary>Hat der Kaempfer diese Faehigkeit schon? (Server-Wahrheit.)</summary>
        public bool ServerHas(AbilityKind kind)
        {
            for (int i = 0; i < 3; i++) if (_kinds[i] == kind) return true;
            return false;
        }

        /// <summary>Wie <see cref="ServerHas"/>, aber aus den replizierten Daten -
        /// auch auf einem reinen Client richtig (Kaufmenue-Anzeige).</summary>
        public bool HasKind(AbilityKind kind)
        {
            for (int i = 0; i < 3; i++) if (KindInSlot(i) == kind) return true;
            return false;
        }

        // ---- Einsatz ------------------------------------------------

        [Rpc(SendTo.Server)]
        public void RequestUseRpc(int slot) => ServerTryUse(slot);

        /// <summary>Nur Server (auch vom Bot). Gibt zurueck, ob die Faehigkeit
        /// ausgeloest wurde.</summary>
        public bool ServerTryUse(int slot)
        {
            if (!IsServer || slot < 0 || slot > 2) return false;
            if (_catalog == null) return false;
            if (_health != null && !_health.IsAlive) return false;

            var mm = MatchManager.Instance;
            if (mm == null || mm.CurrentPhase != MatchManager.Phase.Playing) return false;
            if (mm.IsFrozen || mm.IsSoloPaused) return false;

            if (_kinds[slot] == AbilityKind.Keine) return false;
            if (ChargeOf(slot) <= 0) return false;
            if (ServerNow < _cooldownEnd[slot]) return false;

            var stats = _catalog.Find(_kinds[slot]);
            if (stats == null) return false;

            Vector3 origin = _aim != null ? _aim.AimOrigin : transform.position + Vector3.up * 1.6f;
            Vector3 dir = _aim != null ? _aim.AimDirection : transform.forward;
            int teamId = _team != null ? _team.TeamId : Team.None;

            var spawned = AbilitySpawner.ServerSpawn(stats, gameObject, origin, dir, teamId);
            if (spawned == null) return false;

            SetCharge(slot, ChargeOf(slot) - 1);
            _cooldownEnd[slot] = ServerNow + Mathf.Max(0f, stats.Cooldown);
            return true;
        }

        // ---- Blenden ----------------------------------------------

        /// <summary>Nur Server: den Besitzer dieses Objekts blenden.</summary>
        public void ServerBlindOwner(float seconds)
        {
            if (!IsServer || seconds <= 0f) return;
            _blindUntilServer = Mathf.Max((float)_blindUntilServer, (float)ServerNow + seconds);
            BlindRpc(seconds);
        }

        [Rpc(SendTo.Owner)]
        void BlindRpc(float seconds) => OwnerBlinded?.Invoke(seconds);

        // ---- intern ---------------------------------------------

        int ChargeOf(int slot) => slot switch
        {
            0 => _charge0.Value, 1 => _charge1.Value, 2 => _charge2.Value, _ => 0
        };

        void SetCharge(int slot, int value)
        {
            value = Mathf.Max(0, value);
            switch (slot)
            {
                case 0: _charge0.Value = value; break;
                case 1: _charge1.Value = value; break;
                case 2: _charge2.Value = value; break;
            }
        }

        void RepackKinds()
        {
            _packedKinds.Value =
                ((int)_kinds[0] & 0xF)
                | (((int)_kinds[1] & 0xF) << 4)
                | (((int)_kinds[2] & 0xF) << 8);
        }
    }
}
