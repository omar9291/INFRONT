using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Etappe E: erkannte Momente (Doppelkill, Ace ...) und die Laufbahn-
    /// Statistik.
    /// </summary>
    public sealed class HighlightTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [Test]
        public void Laufbahn_ueberlebt_einen_Neustart()
        {
            CareerStats.ResetForTests();
            Assert.AreEqual(0, CareerStats.Matches);

            CareerStats.RecordMatch(true);
            CareerStats.RecordMatch(true);
            CareerStats.RecordAce();
            CareerStats.RecordMatch(false);

            Assert.AreEqual(3, CareerStats.Matches);
            Assert.AreEqual(2, CareerStats.Wins);
            Assert.AreEqual(1, CareerStats.Aces);
            Assert.AreEqual(0, CareerStats.Streak, "Nach einer Niederlage ist die Serie 0.");
            Assert.AreEqual(2, CareerStats.BestStreak);

            // "Neustart" = die Werte liegen dauerhaft in PlayerPrefs.
            Assert.AreEqual(3, PlayerPrefs.GetInt("infront.career.matches", -1));

            CareerStats.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Doppelkill_wird_erkannt()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var tracker = Object.FindAnyObjectByType<HighlightTracker>();
            Assert.IsNotNull(tracker, "Kein HighlightTracker.");
            for (int i = 0; i < 3; i++) yield return null;   // hooken lassen

            var hits = new List<HighlightKind>();
            void OnHi(int k, ulong id) => hits.Add((HighlightKind)k);
            MatchManager.Instance.HighlightReported += OnHi;

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            var enemies = new List<TeamMember>();
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId != myTeam && m.Health != null && m.Health.IsAlive)
                    enemies.Add(m);
            Assert.GreaterOrEqual(enemies.Count, 2, "Zu wenige Gegner fuer den Test.");

            enemies[0].Health.ApplyDamage(9999, player.gameObject);
            yield return null;
            enemies[1].Health.ApplyDamage(9999, player.gameObject);
            for (int i = 0; i < 5; i++) yield return null;

            MatchManager.Instance.HighlightReported -= OnHi;
            Assert.Contains(HighlightKind.Doppelkill, hits,
                "Zwei schnelle Abschuesse haben keinen Doppelkill gemeldet.");
        }
    }
}
