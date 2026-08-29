using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Team-Deathmatch-Regeln: Punktestand, Rundenzeit, Rundenende und Neustart.
    /// Alle Zahlen sind server-geschriebene NetworkVariables. Die Restzeit wird
    /// NICHT laufend gesendet - der Server nennt einmal die Endzeit, jeder
    /// rechnet selbst herunter.
    /// </summary>
    public sealed class MatchManager : NetworkBehaviour
    {
        public enum Phase { Playing = 0, RoundOver = 1 }

        [SerializeField] int _scoreLimit = 25;
        [SerializeField] float _roundDuration = 480f;
        [SerializeField] float _restDuration = 6f;

        public static MatchManager Instance { get; private set; }

        readonly NetworkVariable<int> _scoreAlpha = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _scoreBravo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _phase = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _roundEndTime = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _winner = new(Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly HashSet<Health> _hooked = new();
        Coroutine _restartRoutine;

        public int ScoreLimit => _scoreLimit;
        public int GetScore(int team) => team == Team.Alpha ? _scoreAlpha.Value : team == Team.Bravo ? _scoreBravo.Value : 0;
        public Phase CurrentPhase => (Phase)_phase.Value;
        public int Winner => _winner.Value;

        public double SecondsRemaining =>
            Mathf.Max(0f, (float)(_roundEndTime.Value - NetworkManager.ServerTime.Time));

        public override void OnNetworkSpawn()
        {
            Instance = this;

            if (!IsServer)
                return;

            Combatants.Added += HookCombatant;
            Combatants.Removed += UnhookCombatant;
            foreach (var member in Combatants.Everyone)
                HookCombatant(member);

            StartRound();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;

            if (!IsServer)
                return;

            Combatants.Added -= HookCombatant;
            Combatants.Removed -= UnhookCombatant;
            foreach (var health in _hooked)
                if (health != null)
                    health.DiedWithInstigator -= OnCombatantDied;
            _hooked.Clear();
        }

        void HookCombatant(TeamMember member)
        {
            if (member == null || member.Health == null) return;
            if (_hooked.Add(member.Health))
                member.Health.DiedWithInstigator += OnCombatantDied;
        }

        void UnhookCombatant(TeamMember member)
        {
            if (member == null || member.Health == null) return;
            if (_hooked.Remove(member.Health))
                member.Health.DiedWithInstigator -= OnCombatantDied;
        }

        void OnCombatantDied(GameObject instigator)
        {
            if (!IsServer || CurrentPhase != Phase.Playing || instigator == null)
                return;

            var killerTeam = instigator.GetComponentInParent<TeamMember>();
            if (killerTeam == null || killerTeam.TeamId == Team.None)
                return;

            if (killerTeam.TeamId == Team.Alpha) _scoreAlpha.Value += 1;
            else if (killerTeam.TeamId == Team.Bravo) _scoreBravo.Value += 1;

            if (_scoreAlpha.Value >= _scoreLimit) EndRound(Team.Alpha);
            else if (_scoreBravo.Value >= _scoreLimit) EndRound(Team.Bravo);
        }

        /// <summary>Nur fuer Tests: kein automatisches Rundenende (Zeit/Punkte weiter zaehlbar).</summary>
        public bool SuspendedForTests { get; set; }

        void Update()
        {
            if (!IsServer || SuspendedForTests || CurrentPhase != Phase.Playing)
                return;

            if (NetworkManager.ServerTime.Time >= _roundEndTime.Value)
            {
                int winner = _scoreAlpha.Value > _scoreBravo.Value ? Team.Alpha
                           : _scoreBravo.Value > _scoreAlpha.Value ? Team.Bravo
                           : Team.None;
                EndRound(winner);
            }
        }

        void EndRound(int winner)
        {
            _phase.Value = (int)Phase.RoundOver;
            _winner.Value = winner;

            if (_restartRoutine != null) StopCoroutine(_restartRoutine);
            _restartRoutine = StartCoroutine(RestartAfterRest());
        }

        IEnumerator RestartAfterRest()
        {
            yield return new WaitForSeconds(_restDuration);
            StartRound();
        }

        /// <summary>Nur Server: Pause ueberspringen, sofort neue Runde.</summary>
        public void ServerStartNextRoundNow()
        {
            if (!IsServer || CurrentPhase != Phase.RoundOver) return;
            if (_restartRoutine != null) StopCoroutine(_restartRoutine);
            StartRound();
        }

        /// <summary>Nur fuer Tests: kleineres Punktelimit und kurze Pause zwischen Runden.</summary>
        public void ServerApplyTestConfig(int scoreLimit, float roundDuration, float restDuration)
        {
            if (!IsServer) return;
            _scoreLimit = scoreLimit;
            _roundDuration = roundDuration;
            _restDuration = restDuration;
            _roundEndTime.Value = NetworkManager.ServerTime.Time + roundDuration;
        }

        /// <summary>Nur Server. Setzt Punkte, Zeit und alle Kaempfer zurueck.</summary>
        public void StartRound()
        {
            if (!IsServer)
                return;

            _scoreAlpha.Value = 0;
            _scoreBravo.Value = 0;
            _winner.Value = Team.None;
            _phase.Value = (int)Phase.Playing;
            _roundEndTime.Value = NetworkManager.ServerTime.Time + _roundDuration;

            foreach (var member in Combatants.Everyone)
            {
                if (member == null) continue;

                if (SpawnService.TryGetSpawn(member.TeamId, out Vector3 pos, out Quaternion rot))
                {
                    var respawnable = member.GetComponent<IRespawnable>();
                    respawnable?.ServerTeleport(pos, rot);
                }

                member.Health.ResetFull();

                var weapon = member.GetComponent<NetworkWeapon>();
                weapon?.ServerRefillMagazine();
            }
        }
    }
}
