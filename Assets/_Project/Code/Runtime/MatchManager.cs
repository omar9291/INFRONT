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

        /// <summary>Ausscheiden (jeder gegen jeden) oder Bombe (legen / entschaerfen).</summary>
        public enum RoundMode { Ausscheiden = 0, Bombe = 1 }

        /// <summary>Bomben-Ereignis fuer den Kill-Feed.</summary>
        public enum BombEvent { Gelegt = 0, Entschaerft = 1, Explodiert = 2 }

        [SerializeField] int _roundsToWin = 16;
        [Tooltip("Bomben-Modus: nach so vielen Runden Seitenwechsel (Halbzeit).")]
        [SerializeField] int _roundsPerHalf = 15;
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

        [Header("Geld - Bomben-Modus")]
        [SerializeField] int _moneyPlant = 300;
        [SerializeField] int _moneyDefuse = 300;
        [Tooltip("Trostgeld fuer die Angreifer, wenn sie trotz gelegter Bombe verlieren.")]
        [SerializeField] int _moneyPlantedButLost = 800;

        public static MatchManager Instance { get; private set; }

        readonly NetworkVariable<int> _winsAlpha = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _winsBravo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _phase = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _roundEndTime = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _roundWinner = new(Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _matchWinner = new(Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _freezeEndTime = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _mode = new((int)RoundMode.Ausscheiden, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _attackTeam = new(Team.Alpha, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _roundsPlayed = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly HashSet<Health> _hooked = new();
        readonly List<Transform> _spawnBuffer = new();
        readonly HashSet<ulong> _diedThisRound = new();
        Coroutine _restartRoutine;

        int _lossStreakAlpha;
        int _lossStreakBravo;
        bool _freshMatch;
        bool _bombPlantedThisRound;

        public int RoundsToWin => _roundsToWin;
        /// <summary>Bomben-Modus: gespielte Runden und Runden bis zur Halbzeit.</summary>
        public int RoundsPlayed => _roundsPlayed.Value;
        public int RoundsPerHalf => _roundsPerHalf;

        /// <summary>Aktueller Spielmodus.</summary>
        public RoundMode Mode => (RoundMode)_mode.Value;
        public bool IsBombMode => Mode == RoundMode.Bombe;
        /// <summary>Team, das im Bomben-Modus legt.</summary>
        public int AttackingTeam => _attackTeam.Value;
        /// <summary>Team, das im Bomben-Modus entschaerft.</summary>
        public int DefendingTeam => Team.Opponent(_attackTeam.Value);
        /// <summary>Wurde die Bombe in dieser Runde gelegt? (fuer HUD / Geld-Bonus)</summary>
        public bool BombPlantedThisRound => _bombPlantedThisRound;
        public int GetScore(int team) => team == Team.Alpha ? _winsAlpha.Value : team == Team.Bravo ? _winsBravo.Value : 0;
        public Phase CurrentPhase => (Phase)_phase.Value;
        public int RoundWinner => _roundWinner.Value;
        public int MatchWinner => _matchWinner.Value;

        /// <summary>Waehrend der Solo-Pause bereits verstrichene Pausenzeit.
        /// Die ServerTime tickt bei timeScale=0 weiter - damit die HUD-Uhren
        /// waehrend der Pause trotzdem still stehen, wird das hier abgezogen.
        /// (Auch die Bombe liest hier mit.)</summary>
        public double SoloPauseElapsedForHud =>
            IsSoloPaused ? NetworkManager.ServerTime.Time - _pauseStartServerTime : 0.0;

        public double SecondsRemaining =>
            Mathf.Max(0f, (float)(_roundEndTime.Value - NetworkManager.ServerTime.Time + SoloPauseElapsedForHud));

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
            Mathf.Max(0f, (float)(_freezeEndTime.Value - NetworkManager.ServerTime.Time + SoloPauseElapsedForHud));

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

        /// <summary>Event auf allen Clients: (BombEvent-Art, AkteurId). AkteurId 0 = niemand/unbekannt.</summary>
        public event System.Action<int, ulong> BombEventReported;

        /// <summary>Event auf allen Clients: eine Bot-Ansage (Text, Team). Fuer den Kill-Feed.</summary>
        public event System.Action<string, int> CalloutReported;

        /// <summary>Nur Server: eine kurze Bot-Ansage verbreiten ("Feind Mitte!").</summary>
        public void ServerReportCallout(string text, int team)
        {
            if (IsServer && !string.IsNullOrEmpty(text))
                BroadcastCalloutRpc(text, team);
        }

        /// <summary>Event auf allen Clients: eine neue Runde hat begonnen (Kaufzeit läuft).</summary>
        public event System.Action RoundStarted;

        /// <summary>Event auf allen Clients: die Runde ist vorbei. Parameter = Siegerteam (Team.None = unentschieden).</summary>
        public event System.Action<int> RoundEnded;

        /// <summary>Event auf allen Clients: die Kaufzeit ist gerade abgelaufen.</summary>
        public event System.Action BuyTimeEnded;

        /// <summary>Event auf allen Clients: das Match ist entschieden. Parameter = Siegerteam.</summary>
        public event System.Action<int> MatchEnded;

        /// <summary>Event auf allen Clients: ein besonderer Moment (Doppelkill, Ace,
        /// Clutch ...). Parameter: (HighlightKind als int, KaempferObjektId).</summary>
        public event System.Action<int, ulong> HighlightReported;

        /// <summary>Nur Server: einen besonderen Moment verbreiten.</summary>
        public void ServerReportHighlight(int kind, ulong playerObjectId)
        {
            if (IsServer) BroadcastHighlightRpc(kind, playerObjectId);
        }

        bool _buyTimeAnnounced;

        // Echte Pause im Solo-Spiel (Esc). Alle Endzeitpunkte laufen in
        // ServerTime - die tickt auch bei Time.timeScale = 0 weiter. Beim
        // Fortsetzen wird die verstrichene Pausenzeit auf alle Uhren addiert,
        // damit sich nichts "wegdreht", waehrend man im Menue steht.
        double _pauseStartServerTime;

        /// <summary>Laeuft gerade eine echte Solo-Pause? (Runde, Kaufzeit und
        /// Bombenzuender stehen dann still.)</summary>
        public bool IsSoloPaused { get; private set; }

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

            CheckRoundEndAfterDeath();
        }

        void CheckRoundEndAfterDeath()
        {
            if (IsBombMode)
            {
                var bomb = Bomb.Instance;
                if (bomb != null && bomb.IsPlanted)
                {
                    // Bombe tickt. Sind ALLE Verteidiger tot, kann niemand mehr
                    // entschaerfen -> Angreifer gewinnen sofort. Sind alle
                    // Angreifer tot, laeuft die Runde weiter (Bombe geht hoch).
                    if (AliveCount(DefendingTeam) == 0) EndRound(AttackingTeam);
                    return;
                }

                // Noch nicht gelegt: wie Ausscheiden, aber rollenbasiert.
                if (AliveCount(AttackingTeam) == 0) EndRound(DefendingTeam);
                else if (AliveCount(DefendingTeam) == 0) EndRound(AttackingTeam);
                return;
            }

            int aliveAlpha = AliveCount(Team.Alpha);
            int aliveBravo = AliveCount(Team.Bravo);
            if (aliveAlpha == 0 && aliveBravo == 0) EndRound(Team.None);
            else if (aliveAlpha == 0) EndRound(Team.Bravo);
            else if (aliveBravo == 0) EndRound(Team.Alpha);
        }

        [Rpc(SendTo.Everyone)]
        void BroadcastKillRpc(ulong killerId, ulong victimId) => KillReported?.Invoke(killerId, victimId);

        [Rpc(SendTo.Everyone)]
        void BroadcastBombEventRpc(int kind, ulong actorId) => BombEventReported?.Invoke(kind, actorId);

        [Rpc(SendTo.Everyone)]
        void BroadcastCalloutRpc(string text, int team) => CalloutReported?.Invoke(text, team);

        [Rpc(SendTo.Everyone)]
        void BroadcastHighlightRpc(int kind, ulong playerId) => HighlightReported?.Invoke(kind, playerId);

        [Rpc(SendTo.Everyone)]
        void BroadcastMatchEndRpc(int winner) => MatchEnded?.Invoke(winner);

        [Rpc(SendTo.Everyone)]
        void BroadcastRoundStartRpc() => RoundStarted?.Invoke();

        [Rpc(SendTo.Everyone)]
        void BroadcastRoundEndRpc(int winner)
        {
            RoundEnded?.Invoke(winner);
            // Gute Stelle zum Sichern: einmal je Runde statt bei jedem Schuss.
            Spielstatistik.RundeVorbei();
        }

        [Rpc(SendTo.Everyone)]
        void BroadcastBuyTimeEndedRpc() => BuyTimeEnded?.Invoke();

        static int AliveCount(int team)
        {
            int n = 0;
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == team && m.Health != null && m.Health.IsAlive)
                    n++;
            return n;
        }

        /// <summary>Nur Server: echte Pause beginnen (Zeitpunkt merken).</summary>
        public void ServerBeginSoloPause()
        {
            if (!IsServer || IsSoloPaused) return;
            IsSoloPaused = true;
            _pauseStartServerTime = NetworkManager.ServerTime.Time;
        }

        /// <summary>Nur Server: Pause beenden und die verstrichene Zeit auf alle
        /// Rundenuhren (Runde, Kaufzeit, Bombenzuender) aufschlagen.</summary>
        public void ServerEndSoloPause()
        {
            if (!IsServer || !IsSoloPaused) return;
            IsSoloPaused = false;

            double delta = NetworkManager.ServerTime.Time - _pauseStartServerTime;
            if (delta <= 0) return;

            if (_freezeEndTime.Value > 0) _freezeEndTime.Value += delta;
            if (_roundEndTime.Value > 0) _roundEndTime.Value += delta;
            Bomb.Instance?.ServerShiftTimes(delta);
        }

        void Update()
        {
            if (!IsServer || SuspendedForTests || CurrentPhase != Phase.Playing)
                return;

            // Waehrend der Solo-Pause stehen alle Rundenuhren still.
            if (IsSoloPaused)
                return;

            // Kaufzeit gerade abgelaufen -> einmal melden (für Ton / HUD).
            if (!_buyTimeAnnounced && !IsBuyTime)
            {
                _buyTimeAnnounced = true;
                BroadcastBuyTimeEndedRpc();
            }

            if (NetworkManager.ServerTime.Time >= _roundEndTime.Value)
            {
                if (IsBombMode)
                {
                    var bomb = Bomb.Instance;
                    if (bomb != null && bomb.IsPlanted)
                        return;   // Bombe tickt -> die Zuenderzeit entscheidet, nicht die Rundenuhr
                    EndRound(DefendingTeam);   // Zeit abgelaufen ohne Legen -> Verteidiger gewinnen
                    return;
                }

                int a = AliveCount(Team.Alpha), b = AliveCount(Team.Bravo);
                EndRound(a > b ? Team.Alpha : b > a ? Team.Bravo : Team.None);
            }
        }

        // ---- Bomben-Modus: Rueckmeldungen von der Bombe ----

        /// <summary>Nur Server: die Bombe wurde gelegt.</summary>
        public void ServerOnBombPlanted(TeamMember planter)
        {
            if (!IsServer) return;
            _bombPlantedThisRound = true;

            if (planter != null)
                planter.GetComponent<Wallet>()?.ServerAdd(_moneyPlant);

            ulong id = planter != null && planter.NetworkObject != null ? planter.NetworkObject.NetworkObjectId : 0;
            BroadcastBombEventRpc((int)BombEvent.Gelegt, id);
        }

        /// <summary>Nur Server: die Bombe wurde entschaerft -> Verteidiger gewinnen.</summary>
        public void ServerOnBombDefused(TeamMember defuser)
        {
            if (!IsServer || !IsBombMode || CurrentPhase != Phase.Playing) return;

            if (defuser != null)
                defuser.GetComponent<Wallet>()?.ServerAdd(_moneyDefuse);

            ulong id = defuser != null && defuser.NetworkObject != null ? defuser.NetworkObject.NetworkObjectId : 0;
            BroadcastBombEventRpc((int)BombEvent.Entschaerft, id);
            EndRound(DefendingTeam);
        }

        /// <summary>Nur Server: die Bombe ist explodiert -> Angreifer gewinnen.</summary>
        public void ServerOnBombDetonated()
        {
            if (!IsServer || !IsBombMode || CurrentPhase != Phase.Playing) return;
            BroadcastBombEventRpc((int)BombEvent.Explodiert, 0);
            EndRound(AttackingTeam);
        }

        void EndRound(int winner)
        {
            _phase.Value = (int)Phase.RoundOver;
            _roundWinner.Value = winner;
            _roundsPlayed.Value += 1;
            BroadcastRoundEndRpc(winner);

            if (winner == Team.Alpha) _winsAlpha.Value += 1;
            else if (winner == Team.Bravo) _winsBravo.Value += 1;

            AwardRoundMoney(winner);
            UpdateLossStreaks(winner);

            bool matchOver = false;
            if (_winsAlpha.Value >= _roundsToWin) { _matchWinner.Value = Team.Alpha; matchOver = true; }
            else if (_winsBravo.Value >= _roundsToWin) { _matchWinner.Value = Team.Bravo; matchOver = true; }
            if (matchOver) BroadcastMatchEndRpc(_matchWinner.Value);

            // Halbzeit: im Bomben-Modus nach der Haelfte der Runden Seiten
            // tauschen und Geld zuruecksetzen - wie in Counter-Strike.
            if (!matchOver && IsBombMode && _roundsPlayed.Value == _roundsPerHalf)
                ServerHalfTime();

            if (_restartRoutine != null) StopCoroutine(_restartRoutine);
            _restartRoutine = StartCoroutine(RestartAfterRest(matchOver ? _matchEndRest : _restDuration, matchOver));
        }

        /// <summary>Nur Server: Seiten tauschen, Geld auf Start, Serien auf 0.
        /// Die naechste Runde startet fuer alle nur mit Pistole (_freshMatch).</summary>
        void ServerHalfTime()
        {
            _attackTeam.Value = Team.Opponent(_attackTeam.Value);
            _lossStreakAlpha = 0;
            _lossStreakBravo = 0;
            _freshMatch = true;

            foreach (var m in Combatants.Everyone)
                m?.GetComponent<Wallet>()?.ServerSet(_moneyStart);
        }

        void AwardRoundMoney(int winner)
        {
            // Bomben-Modus: Angreifer verlieren, obwohl die Bombe lag -> Trostgeld.
            bool attackersLostWithPlant =
                IsBombMode && _bombPlantedThisRound && winner == DefendingTeam;

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
                    int loss = _moneyRoundLoss + streak * _moneyLossStreakBonus;
                    if (attackersLostWithPlant && m.TeamId == AttackingTeam)
                        loss += _moneyPlantedButLost;
                    wallet.ServerAdd(loss);
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

        /// <summary>Nur fuer Tests: Runden bis zur Halbzeit setzen.</summary>
        public void ServerSetRoundsPerHalfForTests(int rounds)
        {
            if (IsServer) _roundsPerHalf = Mathf.Max(1, rounds);
        }

        /// <summary>Nur fuer Tests: eine Runde sofort mit diesem Sieger beenden.</summary>
        public void ServerForceRoundEndForTests(int winner)
        {
            if (IsServer) EndRound(winner);
        }

        /// <summary>Nur fuer Tests: in den Bomben-Modus schalten und das
        /// angreifende Team festlegen.</summary>
        public void ServerForceBombMode(int attackingTeam)
        {
            if (!IsServer) return;
            _mode.Value = (int)RoundMode.Bombe;
            _attackTeam.Value = attackingTeam == Team.Bravo ? Team.Bravo : Team.Alpha;
            _bombPlantedThisRound = false;
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
            _bombPlantedThisRound = false;
            _roundsPlayed.Value = 0;

            _mode.Value = GameSettings.GameMode == GameSettings.Mode.Bombe
                ? (int)RoundMode.Bombe : (int)RoundMode.Ausscheiden;
            _attackTeam.Value = Team.Alpha;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null) continue;
                m.ResetStats();
                m.GetComponent<Wallet>()?.ServerSet(_moneyStart);
                m.GetComponent<NetworkWeapon>()?.ServerSetPistolOnly();
                m.Health?.ServerClearArmor();
                m.GetComponent<BombAction>()?.ServerClearKit();
            }
            Bomb.Instance?.ServerReset();
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
            _bombPlantedThisRound = false;
            _buyTimeAnnounced = false;

            if (IsBombMode)
                Bomb.Instance?.ServerBeginRound(_attackTeam.Value);

            BroadcastRoundStartRpc();
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

                // Bots: Patrouillen-Punkt Richtung Kartenmitte schieben, sonst
                // bleiben beide Teams je in ihrer Spawn-Blase.
                member.GetComponent<BotBrain>()?.ServerAnchorForward();

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

                var abilities = member.GetComponent<AbilityHolder>();
                if (abilities != null)
                {
                    if (fresh) abilities.ServerClearLoadout();
                    else abilities.ServerRefreshCharges();
                }

                if (fresh)
                {
                    member.Health.ServerClearArmor();
                    member.GetComponent<BombAction>()?.ServerClearKit();
                }
            }
        }
    }
}
