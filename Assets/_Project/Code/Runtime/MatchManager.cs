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
    /// Geld (wie in Counter-Strike):
    ///  - Matchstart: Startgeld, jeder nur mit Pistole.
    ///  - Am Rundenanfang gibt es eine Kaufzeit (die verlaengerte Startsperre).
    ///  - Rundensieg, Niederlage (mit Serienbonus) und Abschuss bringen Geld.
    ///  - Wer stirbt, verliert seine Primaerwaffe und die Weste fuer die
    ///    naechste Runde. Wer ueberlebt, behaelt beides.
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
        [Tooltip("Startsperre am Rundenanfang = Kaufzeit.")]
        [SerializeField] float _freezeDuration = 10f;

        [Header("Geld")]
        [SerializeField] int _moneyStart = 800;
        [SerializeField] int _moneyRoundWin = 3000;
        [SerializeField] int _moneyRoundLoss = 1400;
        [SerializeField] int _moneyLossStreakBonus = 500;
        [SerializeField] int _moneyLossStreakMax = 4;   // 1400 + 4*500 = 3400
        [SerializeField] int _moneyKill = 300;

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
        readonly HashSet<ulong> _diedThisRound = new();
        Coroutine _restartRoutine;

        int _lossStreakAlpha;
        int _lossStreakBravo;
        bool _freshMatch;

        public int RoundsToWin => _roundsToWin;
        public int GetScore(int team) => team == Team.Alpha ? _winsAlpha.Value : team == Team.Bravo ? _winsBravo.Value : 0;
        public Phase CurrentPhase => (Phase)_phase.Value;
        public int RoundWinner => _roundWinner.Value;
        public int MatchWinner => _matchWinner.Value;

        public double SecondsRemaining =>
            Mathf.Max(0f, (float)(_roundEndTime.Value - NetworkManager.ServerTime.Time));

        public bool SuspendedForTests { get; set; }
        public bool SkipFreezeForTests { get; set; }
        public bool ForceBuyTimeForTests { get; set; }

        /// <summary>Startsperre: niemand darf laufen oder schiessen.</summary>
        public bool IsFrozen =>
            !SkipFreezeForTests
            && CurrentPhase == Phase.Playing
            && NetworkManager != null
            && NetworkManager.ServerTime.Time < _freezeEndTime.Value;

        public double FreezeSecondsLeft =>
            Mathf.Max(0f, (float)(_freezeEndTime.Value - NetworkManager.ServerTime.Time));

        /// <summary>Zeitpunkt, an dem die Kaufzeit endet (ServerTime).</summary>
        public double BuyEndTime => _freezeEndTime.Value;

        /// <summary>Darf gerade gekauft werden? (Kaufzeit laeuft oder Test erzwingt es.)</summary>
        public bool IsBuyTime =>
            ForceBuyTimeForTests
            || (CurrentPhase == Phase.Playing
                && NetworkManager != null
                && NetworkManager.ServerTime.Time < _freezeEndTime.Value);

        public double BuySecondsLeft => FreezeSecondsLeft;

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
                killer.GetComponent<Wallet>()?.ServerAdd(_moneyKill);
                killerId = killer.NetworkObject != null ? killer.NetworkObject.NetworkObjectId : 0;
            }
            BroadcastKillRpc(killerId, victimId);

            // Merken: wer diese Runde stirbt, startet die naechste nur mit
            // Pistole und ohne Weste (siehe PlaceTeam).
            if (victimId != 0) _diedThisRound.Add(victimId);

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

            AwardRoundMoney(winner);
            UpdateLossStreaks(winner);

            bool matchOver = false;
            if (_winsAlpha.Value >= _roundsToWin) { _matchWinner.Value = Team.Alpha; matchOver = true; }
            else if (_winsBravo.Value >= _roundsToWin) { _matchWinner.Value = Team.Bravo; matchOver = true; }

            if (_restartRoutine != null) StopCoroutine(_restartRoutine);
            _restartRoutine = StartCoroutine(RestartAfterRest(matchOver ? _matchEndRest : _restDuration, matchOver));
        }

        void AwardRoundMoney(int winner)
        {
            foreach (var m in Combatants.Everyone)
            {
                var wallet = m != null ? m.GetComponent<Wallet>() : null;
                if (wallet == null) continue;

                if (winner == Team.None)
                {
                    wallet.ServerAdd(_moneyRoundLoss);   // unentschieden: Trostgeld fuer alle
                }
                else if (m.TeamId == winner)
                {
                    wallet.ServerAdd(_moneyRoundWin);
                }
                else
                {
                    int streak = m.TeamId == Team.Alpha ? _lossStreakAlpha : _lossStreakBravo;
                    streak = Mathf.Clamp(streak, 0, _moneyLossStreakMax);
                    wallet.ServerAdd(_moneyRoundLoss + streak * _moneyLossStreakBonus);
                }
            }
        }

        void UpdateLossStreaks(int winner)
        {
            if (winner == Team.Alpha) { _lossStreakAlpha = 0; _lossStreakBravo++; }
            else if (winner == Team.Bravo) { _lossStreakBravo = 0; _lossStreakAlpha++; }
            else { _lossStreakAlpha++; _lossStreakBravo++; }   // unentschieden zaehlt fuer beide
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

        /// <summary>Spieler-Client: "Bereit" - die Kaufzeit sofort beenden.</summary>
        [Rpc(SendTo.Server)]
        public void RequestEndBuyTimeRpc()
        {
            if (!IsServer || CurrentPhase != Phase.Playing) return;
            double now = NetworkManager.ServerTime.Time;
            if (now < _freezeEndTime.Value)
            {
                _freezeEndTime.Value = now;
                _roundEndTime.Value = now + _roundDuration;
            }
        }

        /// <summary>Nur fuer Tests.</summary>
        public void ServerApplyTestConfig(int roundsToWin, float roundDuration, float restDuration)
        {
            if (!IsServer) return;
            _roundsToWin = roundsToWin;
            _roundDuration = roundDuration;
            _restDuration = restDuration;
            _matchEndRest = restDuration;
            _freezeDuration = 0f;
            _freezeEndTime.Value = 0;
            _roundEndTime.Value = NetworkManager.ServerTime.Time + roundDuration;
        }

        /// <summary>Nur fuer Tests: Kaufzeit-/Startsperren-Dauer setzen.</summary>
        public void ServerSetFreezeDuration(float seconds)
        {
            if (IsServer) _freezeDuration = Mathf.Max(0f, seconds);
        }

        /// <summary>Nur Server: neues Match - Rundensiege auf 0, Geld auf Start, dann erste Runde.</summary>
        public void StartMatch()
        {
            if (!IsServer) return;
            _winsAlpha.Value = 0;
            _winsBravo.Value = 0;
            _matchWinner.Value = Team.None;
            _lossStreakAlpha = 0;
            _lossStreakBravo = 0;
            _diedThisRound.Clear();
            _freshMatch = true;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null) continue;
                m.ResetStats();
                m.GetComponent<Wallet>()?.ServerSet(_moneyStart);
                m.GetComponent<NetworkWeapon>()?.ServerSetPistolOnly();
                m.Health?.ServerClearArmor();
            }
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

            _diedThisRound.Clear();
            _freshMatch = false;
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

                // Wer letzte Runde gestorben ist (oder frisches Match): nur Pistole,
                // keine Weste. Wer ueberlebt hat: Waffe und Weste bleiben.
                ulong id = member.NetworkObject != null ? member.NetworkObject.NetworkObjectId : 0;
                bool fresh = _freshMatch || id == 0 || _diedThisRound.Contains(id);

                var weapon = member.GetComponent<NetworkWeapon>();
                if (weapon != null)
                {
                    if (fresh) weapon.ServerSetPistolOnly();
                    else weapon.ServerRefillMagazine();
                }
                if (fresh) member.Health.ServerClearArmor();
            }
        }
    }
}
