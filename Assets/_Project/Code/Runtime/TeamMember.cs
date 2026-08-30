using System;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Haengt an jedem Kaempfer (Spieler, Bot). Haelt das Team als
    /// server-geschriebene NetworkVariable und traegt den Kaempfer in die
    /// <see cref="Combatants"/>-Liste ein.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class TeamMember : NetworkBehaviour
    {
        readonly NetworkVariable<int> _team = new(
            Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Team-interne Nummer (1,2,3...). Der Name entsteht daraus lokal -
        // viel sparsamer als Text ueber das Netz zu schicken.
        readonly NetworkVariable<int> _slot = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> _kills = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _deaths = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int TeamId => _team.Value;
        public int Slot => _slot.Value;
        public int Kills => _kills.Value;
        public int Deaths => _deaths.Value;
        public Health Health { get; private set; }

        public string DisplayName
        {
            get
            {
                string tag = _team.Value == Team.Alpha ? "Alpha"
                           : _team.Value == Team.Bravo ? "Bravo" : "?";
                return _slot.Value > 0 ? $"{tag}-{_slot.Value}" : tag;
            }
        }

        public event Action<int, int> TeamChanged;

        void Awake() => Health = GetComponent<Health>();

        public override void OnNetworkSpawn()
        {
            _team.OnValueChanged += OnTeamChanged;
            Combatants.Register(this);
        }

        public override void OnNetworkDespawn()
        {
            _team.OnValueChanged -= OnTeamChanged;
            Combatants.Unregister(this);
        }

        /// <summary>Nur Server.</summary>
        public void SetTeam(int team)
        {
            if (IsServer)
                _team.Value = team;
        }

        /// <summary>Nur Server.</summary>
        public void SetSlot(int slot)
        {
            if (IsServer)
                _slot.Value = slot;
        }

        /// <summary>Nur Server.</summary>
        public void AddKill() { if (IsServer) _kills.Value += 1; }
        public void AddDeath() { if (IsServer) _deaths.Value += 1; }
        public void ResetStats() { if (IsServer) { _kills.Value = 0; _deaths.Value = 0; } }

        void OnTeamChanged(int previous, int current) => TeamChanged?.Invoke(previous, current);
    }
}
