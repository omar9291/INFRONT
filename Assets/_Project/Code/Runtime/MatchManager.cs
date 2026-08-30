using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Rundenmodus mit Ausscheiden (wie Counter-Strike):
    ///  - Wer stirbt, bleibt die ganze Runde tot (kein Respawn).
    ///  - Ist ein Team ausgeloescht, gewinnt das andere die Runde.
    ///  - Laeuft die Zeit ab, gewinnt das Team mit mehr Ueberlebenden
    ///    (Gleichstand = kein Punkt).
    ///  - Wer zuerst 15 Runden gewinnt, gewinnt das Match; danach neues Match.
    ///
    /// Alle Zahlen sind server-geschriebene NetworkVariables. Die Restzeit
    /// wird nicht laufend gesendet - der Server nennt einmal die Endzeit.
    /// </summary>
    public sealed class MatchManager : NetworkBehaviour
    {
        public enum Phase { Playing = 0, RoundOver = 1 }

        [SerializeField] int _roundsToWin = 15;
        [SerializeField] float _roundDuration = 120f;
        [SerializeField] float _restDuration = 5f;
        [SerializeField] float _matchEndRest = 8f;
        [SerializeField] float _freezeDuration = 3f;

        public static MatchManager Instance { get; private set; }

        readonly NetworkVariable<int> _winsAlpha = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _winsBravo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _phase = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _roundEndTime = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _roundWinner = new(Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _matchWinner = new(Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _freezeEndTime = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly HashSet<Health> _hooked = new();
        readonly List<Transform> _spawnBuffer = new();
        Coroutine _restartRoutine;

        public int RoundsToWin => _roundsToWin;
        public int GetScore(int team) => team == Team.Alpha ? _winsAlpha.Value : team == Team.Bravo ? _winsBravo.Value : 0;
        public Phase CurrentPhase => (Phase)_phase.Value;
        public int RoundWinner => _roundWinner.Value;
        public int MatchWinner => _matchWinner.Value;

        public double SecondsRemaining =>
            Mathf.Max(0f, (float)(_roundEndTime.Value - NetworkManager.ServerTime.Time));

        public bool SuspendedForTests { get; set; }
        public bool SkipFreezeForTests { get; set; }

        /// <summary>Startsperre: niemand darf laufen oder schiessen.</summary>
        public bool IsFrozen =>
            !SkipFreezeForTests
            && CurrentPhase == Phase.Playing
            && NetworkManager != null
            && NetworkManager.ServerTime.Time < _freezeEndTime.Value;

        public double FreezeSecondsLeft =>
            Mathf.Max(0f, (float)(_freezeEndTime.Value - NetworkManager.ServerTime.Time));

        /// <summary>Event auf allen Clients: (ToeterId, OpferId). ToeterId 0 = kein gueltiger Toeter.</summary>
        public event System.Action<ulong, ulong> KillReported;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;

            Combatants.Added += HookCombatant;
            Combatants.Removed += UnhookCombatant;
            foreach (var member in Combatants.Everyone)
                HookCombatant(member);

            StartMatch();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (!IsServer) return;

            Combatants.Added -= HookCombatant;
            Combatants.Removed -= UnhookCombatant;
            _hooked.Clear();
        }

        void HookCombatant(TeamMember member)
        {
            if (member == null || member.Health == null) return;
            if (_hooked.Add(member.Health))
                member.Health.DiedWithInstigator += g => OnCombatantDied(member, g);
        }

        void UnhookCombatant(TeamMember member)
        {
            if (member == null || member.Health == null) return;
            _hooked.Remove(member.Health);
            // Delegat-Abmeldung: beim Szenenabbau wird ohnehin alles zerstoert.
        }

        void OnCombatantDied(TeamMember victim, GameObject instigator)
        {
            if (!IsServer) return;

            // Kill-/Tod-Statistik und Kill-Feed
            ulong victimId = victim.NetworkObject != null ? victim.NetworkObject.NetworkObjectId : 0;
            ulong killerId = 0;
            var killer = instigator != null ? instigator.GetComponentInParent<TeamMember>() : null;

            victim.AddDeath();
            if (killer != null && killer != victim && !Team.AreFriendly(killer.TeamId, victim.TeamId))
            {
                killer.AddKill();
                killerId = killer.NetworkObject != null ? killer.NetworkObject.NetworkObjectId : 0;
            }
            BroadcastKillRpc(killerId, victimId);

            if (SuspendedForTests || CurrentPhase != Phase.Playing)
                return;

            int aliveAlpha = AliveCount(Team.Alpha);
            int aliveBravo = AliveCount(Team.Bravo);
            if (aliveAlpha == 0 && aliveBravo == 0) EndRound(Team.None);
            else if (aliveAlpha == 0) EndRound(Team.Bravo);
            else if (aliveBravo == 0) EndRound(Team.Alpha);
        }

        [Rpc(SendTo.Everyone)]
        void BroadcastKillRpc(ulong killerId, ulong victimId) => KillReported?.Invoke(killerId, victimId);

        static int AliveCount(int team)
        {
            int n = 0;
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == team && m.Health != null && m.Health.IsAlive)
                    n++;
            return n;
        }

        void Update()
        {
            if (!IsServer || SuspendedForTests || CurrentPhase != Phase.Playing)
                return;

            if (NetworkManager.ServerTime.Time >= _roundEndTime.Value)
            {
                int a = AliveCount(Team.Alpha), b = AliveCount(Team.Bravo);
                EndRound(a > b ? Team.Alpha : b > a ? Team.Bravo : Team.None);
            }
        }

        void EndRound(int winner)
        {
            _phase.Value = (int)Phase.RoundOver;
            _roundWinner.Value = winner;

            if (winner == Team.Alpha) _winsAlpha.Value += 1;
            else if (winner == Team.Bravo) _winsBravo.Value += 1;

            bool matchOver = false;
            if (_winsAlpha.Value >= _roundsToWin) { _matchWinner.Value = Team.Alpha; matchOver = true; }
            else if (_winsBravo.Value >= _roundsToWin) { _matchWinner.Value = Team.Bravo; matchOver = true; }

            if (_restartRoutine != null) StopCoroutine(_restartRoutine);
            _restartRoutine = StartCoroutine(RestartAfterRest(matchOver ? _matchEndRest : _restDuration, matchOver));
        }

        IEnumerator RestartAfterRest(float rest, bool matchOver)
        {
            yield return new WaitForSeconds(rest);
            if (matchOver) StartMatch();
            else StartRound();
        }

        /// <summary>Nur Server: Pause ueberspringen.</summary>
        public void ServerStartNextRoundNow()
        {
            if (!IsServer || CurrentPhase != Phase.RoundOver) return;
            if (_restartRoutine != null) StopCoroutine(_restartRoutine);
            if (_matchWinner.Value != Team.None) StartMatch();
            else StartRound();
        }

        /// <summary>Nur fuer Tests.</summary>
        public void ServerApplyTestConfig(int roundsToWin, float roundDuration, float restDuration)
        {
            if (!IsServer) return;
            _roundsToWin = roundsToWin;
            _roundDuration = roundDuration;
            _restDuration = restDuration;
            _matchEndRest = restDuration;
            _freezeEndTime.Value = 0;
            _roundEndTime.Value = NetworkManager.ServerTime.Time + roundDuration;
        }

        /// <summary>Nur Server: neues Match - Rundensiege auf 0, dann erste Runde.</summary>
        public void StartMatch()
        {
            if (!IsServer) return;
            _winsAlpha.Value = 0;
            _winsBravo.Value = 0;
            _matchWinner.Value = Team.None;
            foreach (var m in Combatants.Everyone)
                m?.ResetStats();
            StartRound();
        }

        /// <summary>Nur Server: alle wiederbeleben, an eigene Spawns, Magazine voll.</summary>
        public void StartRound()
        {
            if (!IsServer) return;

            _roundWinner.Value = Team.None;
            _phase.Value = (int)Phase.Playing;
            double now = NetworkManager.ServerTime.Time;
            _freezeEndTime.Value = now + _freezeDuration;
            _roundEndTime.Value = now + _freezeDuration + _roundDuration;

            PlaceTeam(Team.Alpha);
            PlaceTeam(Team.Bravo);
        }

        void PlaceTeam(int team)
        {
            SpawnService.CollectTeamSpawns(team, _spawnBuffer);
            int i = 0;

            foreach (var member in Combatants.Everyone)
            {
                if (member == null || member.TeamId != team) continue;

                if (_spawnBuffer.Count > 0)
                {
                    var sp = _spawnBuffer[i % _spawnBuffer.Count];
                    i++;
                    member.GetComponent<IRespawnable>()?.ServerTeleport(sp.position, sp.rotation);
                }

                member.Health.ResetFull();
                member.GetComponent<NetworkWeapon>()?.ServerRefillMagazine();
            }
        }
    }
}
