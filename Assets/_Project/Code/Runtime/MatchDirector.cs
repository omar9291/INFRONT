using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Baut das Match auf: teilt Spieler in Teams ein, fuellt beide Teams mit
    /// Bots auf die eingestellte Groesse auf und erzeugt danach den MatchManager.
    /// Laeuft nur auf dem Server.
    ///
    /// Loest den frueheren BotSpawner ab.
    /// </summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        [SerializeField] NetworkObject _botPrefab;
        [SerializeField] NetworkObject _matchManagerPrefab;
        [SerializeField] int _teamSize = 3;
        [SerializeField] float _startDelay = 0.6f;

        [Header("Bot-Schwierigkeit")]
        [SerializeField] BotStats _statsEasy;
        [SerializeField] BotStats _statsNormal;
        [SerializeField] BotStats _statsHard;

        readonly int[] _teams = { Team.Alpha, Team.Bravo };
        readonly Dictionary<int, int> _slotCount = new();
        bool _started;

        void Start()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return;

            manager.OnServerStarted += OnServerStarted;
            manager.OnClientConnectedCallback += OnClientConnected;
            if (manager.IsServer)
                OnServerStarted();
        }

        void OnDestroy()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return;
            manager.OnServerStarted -= OnServerStarted;
            manager.OnClientConnectedCallback -= OnClientConnected;
        }

        void OnServerStarted()
        {
            if (_started) return;
            _started = true;
            Invoke(nameof(BuildMatch), _startDelay);
        }

        void OnClientConnected(ulong clientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            if (!manager.ConnectedClients.TryGetValue(clientId, out var client)) return;

            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<TeamMember>() : null;
            if (player != null && player.TeamId == Team.None)
                player.SetTeam(SmallerTeam());
        }

        void BuildMatch()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            _teamSize = Mathf.Clamp(GameSettings.TeamSize, 1, 10);
            _slotCount.Clear();

            // Spieler ohne Team noch zuteilen
            foreach (var client in manager.ConnectedClientsList)
            {
                var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<TeamMember>() : null;
                if (player == null) continue;
                if (player.TeamId == Team.None)
                    player.SetTeam(SmallerTeam());
                if (player.Slot == 0)
                    player.SetSlot(NextSlot(player.TeamId));
            }

            // Beide Teams mit Bots auffuellen
            if (_botPrefab != null)
            {
                foreach (int team in _teams)
                {
                    int missing = _teamSize - Combatants.CountByTeam(team);
                    for (int i = 0; i < missing; i++)
                        SpawnBot(team);
                }
            }

            // MatchManager erzeugen (Teams stehen jetzt)
            if (_matchManagerPrefab != null && MatchManager.Instance == null)
            {
                manager.SpawnManager.InstantiateAndSpawn(
                    _matchManagerPrefab, ownerClientId: NetworkManager.ServerClientId, destroyWithScene: true);
            }
        }

        void SpawnBot(int team)
        {
            var manager = NetworkManager.Singleton;
            SpawnService.TryGetSpawn(team, out Vector3 pos, out Quaternion rot);

            var instance = manager.SpawnManager.InstantiateAndSpawn(
                _botPrefab, ownerClientId: NetworkManager.ServerClientId,
                destroyWithScene: true, position: pos, rotation: rot);

            var member = instance.GetComponent<TeamMember>();
            if (member != null)
            {
                member.SetTeam(team);
                member.SetSlot(NextSlot(team));
            }

            var brain = instance.GetComponent<BotBrain>();
            if (brain != null)
                brain.SetStats(StatsForDifficulty());
        }

        BotStats StatsForDifficulty()
        {
            switch (GameSettings.Difficulty)
            {
                case GameSettings.Level.Leicht: return _statsEasy != null ? _statsEasy : _statsNormal;
                case GameSettings.Level.Schwer: return _statsHard != null ? _statsHard : _statsNormal;
                default: return _statsNormal;
            }
        }

        int NextSlot(int team)
        {
            _slotCount.TryGetValue(team, out int n);
            n++;
            _slotCount[team] = n;
            return n;
        }

        int SmallerTeam()
        {
            return Combatants.CountByTeam(Team.Alpha) <= Combatants.CountByTeam(Team.Bravo)
                ? Team.Alpha : Team.Bravo;
        }
    }
}
