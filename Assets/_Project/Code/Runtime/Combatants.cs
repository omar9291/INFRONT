using System;
using System.Collections.Generic;

namespace Infront
{
    /// <summary>
    /// Verzeichnis aller aktiven Kaempfer. Bots fragen hier ihre Gegner ab,
    /// statt jedes Mal die ganze Szene zu durchsuchen.
    /// </summary>
    public static class Combatants
    {
        static readonly List<TeamMember> All = new();

        public static event Action<TeamMember> Added;
        public static event Action<TeamMember> Removed;

        public static IReadOnlyList<TeamMember> Everyone => All;

        public static void Register(TeamMember member)
        {
            if (All.Contains(member)) return;
            All.Add(member);
            Added?.Invoke(member);
        }

        public static void Unregister(TeamMember member)
        {
            if (All.Remove(member))
                Removed?.Invoke(member);
        }

        /// <summary>Lebende Gegner des angegebenen Teams.</summary>
        public static void CollectEnemies(int myTeam, List<TeamMember> into)
        {
            into.Clear();
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var member = All[i];
                if (member == null) { All.RemoveAt(i); continue; }
                if (member.TeamId == myTeam) continue;
                if (member.Health == null || !member.Health.IsAlive) continue;
                into.Add(member);
            }
        }

        /// <summary>Nur fuer Tests: Liste komplett leeren.</summary>
        public static void ResetForTests()
        {
            All.Clear();
            Added = null;
            Removed = null;
        }

        public static int CountByTeam(int team)
        {
            int n = 0;
            foreach (var m in All)
                if (m != null && m.TeamId == team)
                    n++;
            return n;
        }
    }
}
