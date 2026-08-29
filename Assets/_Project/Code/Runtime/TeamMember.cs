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

        public int TeamId => _team.Value;
        public Health Health { get; private set; }

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

        void OnTeamChanged(int previous, int current) => TeamChanged?.Invoke(previous, current);
    }
}
