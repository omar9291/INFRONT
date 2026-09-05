using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>Die erkennbaren "Momente" - das, was man danach jemandem erzaehlt.</summary>
    public enum HighlightKind
    {
        Doppelkill = 0,
        Dreifachkill = 1,
        Ace = 2,            // alle Gegner allein erledigt
        Clutch = 3,         // 1 gegen viele gewonnen
        BesteDerRunde = 4,  // meiste Abschuesse der Runde
    }

    /// <summary>
    /// Erkennt besondere Momente und meldet sie (Banner + Ton + Kill-Feed).
    /// Laeuft nur auf dem Server; die Meldung geht per
    /// <see cref="MatchManager.ServerReportHighlight"/> an alle.
    ///
    /// Haengt am MatchManager-Prefab.
    /// </summary>
    public sealed class HighlightTracker : MonoBehaviour
    {
        const double MultiKillWindow = 5.0;

        MatchManager _mm;
        readonly Dictionary<ulong, List<double>> _killTimes = new();  // killerId -> Zeitpunkte (Runde)
        readonly Dictionary<ulong, int> _roundKills = new();
        readonly HashSet<ulong> _clutchCandidate = new();
        bool _hooked;

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == _mm) { CheckClutchSetup(); return; }

            Unhook();
            _mm = mm;
            if (_mm != null)
            {
                _mm.KillReported += OnKill;
                _mm.RoundStarted += OnRoundStart;
                _mm.RoundEnded += OnRoundEnd;
                _hooked = true;
            }
        }

        void OnDestroy() => Unhook();

        void Unhook()
        {
            if (_mm != null && _hooked)
            {
                _mm.KillReported -= OnKill;
                _mm.RoundStarted -= OnRoundStart;
                _mm.RoundEnded -= OnRoundEnd;
            }
            _hooked = false;
        }

        bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void OnRoundStart()
        {
            _killTimes.Clear();
            _roundKills.Clear();
            _clutchCandidate.Clear();
        }

        /// <summary>Faellt ein Team auf genau 1 lebenden Kaempfer, waehrend das
        /// andere Team in der Ueberzahl ist -> dieser eine ist Clutch-Kandidat.</summary>
        void CheckClutchSetup()
        {
            if (!IsServer) return;

            foreach (var team in new[] { Team.Alpha, Team.Bravo })
            {
                int mine = 0, theirs = 0;
                TeamMember lastAlive = null;
                foreach (var m in Combatants.Everyone)
                {
                    if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                    if (m.TeamId == team) { mine++; lastAlive = m; }
                    else theirs++;
                }
                if (mine == 1 && theirs >= 2 && lastAlive != null && lastAlive.NetworkObject != null)
                    _clutchCandidate.Add(lastAlive.NetworkObject.NetworkObjectId);
            }
        }

        void OnKill(ulong killerId, ulong victimId)
        {
            if (!IsServer || killerId == 0 || killerId == victimId) return;

            double now = Time.timeAsDouble;
            if (!_killTimes.TryGetValue(killerId, out var list))
                _killTimes[killerId] = list = new List<double>();
            list.Add(now);
            _roundKills.TryGetValue(killerId, out int rk);
            _roundKills[killerId] = rk + 1;

            // Multikill: wie viele Abschuesse im 5-Sekunden-Fenster?
            int recent = 0;
            for (int i = list.Count - 1; i >= 0; i--)
                if (now - list[i] <= MultiKillWindow) recent++;
                else break;

            if (recent == 2) _mm.ServerReportHighlight((int)HighlightKind.Doppelkill, killerId);
            else if (recent == 3) _mm.ServerReportHighlight((int)HighlightKind.Dreifachkill, killerId);

            // Ace: der Kaempfer hat ALLE Abschuesse seines Teams diese Runde und
            // die Gegner sind jetzt alle tot.
            var killer = Resolve(killerId);
            if (killer != null && recent >= 3)
            {
                int enemiesAlive = 0, teamRoundKills = 0;
                foreach (var m in Combatants.Everyone)
                {
                    if (m == null) continue;
                    if (m.TeamId != killer.TeamId && m.Health != null && m.Health.IsAlive) enemiesAlive++;
                }
                foreach (var kv in _roundKills)
                {
                    var k = Resolve(kv.Key);
                    if (k != null && k.TeamId == killer.TeamId) teamRoundKills += kv.Value;
                }
                if (enemiesAlive == 0 && _roundKills[killerId] >= teamRoundKills && _roundKills[killerId] >= 3)
                    _mm.ServerReportHighlight((int)HighlightKind.Ace, killerId);
            }
        }

        void OnRoundEnd(int winner)
        {
            if (!IsServer) return;

            // Beste der Runde
            ulong bestId = 0; int bestKills = 0;
            foreach (var kv in _roundKills)
                if (kv.Value > bestKills) { bestKills = kv.Value; bestId = kv.Key; }
            if (bestId != 0 && bestKills >= 2)
                _mm.ServerReportHighlight((int)HighlightKind.BesteDerRunde, bestId);

            // Clutch: ein Kandidat aus dem Siegerteam?
            foreach (var id in _clutchCandidate)
            {
                var m = Resolve(id);
                if (m != null && m.TeamId == winner && m.Health != null && m.Health.IsAlive)
                {
                    _mm.ServerReportHighlight((int)HighlightKind.Clutch, id);
                    break;
                }
            }
        }

        static TeamMember Resolve(ulong objectId)
        {
            foreach (var m in Combatants.Everyone)
                if (m != null && m.NetworkObject != null && m.NetworkObject.NetworkObjectId == objectId)
                    return m;
            return null;
        }

        public static string Title(HighlightKind kind) => kind switch
        {
            HighlightKind.Doppelkill => GameText.Messages.DoubleKill,
            HighlightKind.Dreifachkill => GameText.Messages.TripleKill,
            HighlightKind.Ace => GameText.Messages.Ace,
            HighlightKind.Clutch => GameText.Messages.Clutch,
            HighlightKind.BesteDerRunde => GameText.Messages.RoundMvp,
            _ => "",
        };
    }
}
