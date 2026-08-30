using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-4-Tests: Teams, Abschuss-Wertung, kein Freundschaftsbeschuss,
    /// Rundenende und Neustart.
    /// </summary>
    public sealed class TeamMatchTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static List<BotBrain> BotsOnTeam(int team)
        {
            var list = new List<BotBrain>();
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId == team)
                    list.Add(b);
            return list;
        }

        [UnityTest]
        public IEnumerator Spieler_und_Bots_haben_Teams()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            Assert.AreNotEqual(Team.None, player.GetComponent<TeamMember>().TeamId);
            Assert.Greater(Combatants.CountByTeam(Team.Alpha), 0);
            Assert.Greater(Combatants.CountByTeam(Team.Bravo), 0);
        }

        [UnityTest]
        public IEnumerator Jeder_Kaempfer_hat_Team_Nummer_und_Namen()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var alpha = new System.Collections.Generic.HashSet<int>();
            var bravo = new System.Collections.Generic.HashSet<int>();

            foreach (var member in Combatants.Everyone)
            {
                Assert.AreNotEqual(Team.None, member.TeamId, "Kaempfer ohne Team.");
                Assert.Greater(member.Slot, 0, $"{member.name} hat keine Slot-Nummer.");
                Assert.IsTrue(member.DisplayName.StartsWith("Alpha-") || member.DisplayName.StartsWith("Bravo-"),
                    $"Unerwarteter Name: {member.DisplayName}");

                var set = member.TeamId == Team.Alpha ? alpha : bravo;
                Assert.IsTrue(set.Add(member.Slot), $"Doppelte Slot-Nummer {member.Slot} in einem Team.");
            }

            Assert.AreEqual(3, alpha.Count);
            Assert.AreEqual(3, bravo.Count);
        }

        [UnityTest]
        public IEnumerator Abschuss_zaehlt_beim_Schuetzen_und_Tod_beim_Opfer()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var me = player.GetComponent<TeamMember>();
            var enemy = BotsOnTeam(Team.Opponent(me.TeamId))[0];
            var enemyTm = enemy.GetComponent<TeamMember>();

            int myKills = me.Kills;
            int enemyDeaths = enemyTm.Deaths;

            enemy.GetComponent<Health>().ApplyDamage(9999, player.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(myKills + 1, me.Kills, "Abschuss nicht gezaehlt.");
            Assert.AreEqual(enemyDeaths + 1, enemyTm.Deaths, "Tod nicht gezaehlt.");
        }

        [UnityTest]
        public IEnumerator Freeze_Time_blockiert_Bewegung_am_Rundenstart()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            MatchTestHarness.ClearArena();
            match.SuspendedForTests = false;
            match.SkipFreezeForTests = false;
            match.StartRound();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(match.IsFrozen, "Nach Rundenstart sollte Freeze-Time aktiv sein.");

            player.SetMovementEnabled(true);
            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = 0f };
            player.SetInputSource(input);

            Vector3 start = player.transform.position;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            float movedDuringFreeze = Vector3.Distance(start, player.transform.position);
            Assert.Less(movedDuringFreeze, 0.5f, "Spieler hat sich waehrend Freeze bewegt.");

            // Freeze abwarten (3 s), dann muss Bewegung gehen
            yield return MatchTestHarness.WaitUntil(() => !match.IsFrozen, 5f, "Freeze endete nicht.");
            Vector3 s2 = player.transform.position;
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(Vector3.Distance(s2, player.transform.position), 1f, "Bewegung nach Freeze klappt nicht.");
        }

        [UnityTest]
        public IEnumerator Team_ausloeschen_gibt_einen_Rundensieg()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });
            match.SuspendedForTests = false;

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            int enemyTeam = Team.Opponent(myTeam);
            int before = match.GetScore(myTeam);

            // Alle Gegner ausschalten
            foreach (var e in BotsOnTeam(enemyTeam))
                e.GetComponent<Health>().ApplyDamage(9999, player.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(MatchManager.Phase.RoundOver, match.CurrentPhase, "Runde endete nicht.");
            Assert.AreEqual(myTeam, match.RoundWinner, "Falscher Rundensieger.");
            Assert.AreEqual(before + 1, match.GetScore(myTeam), "Kein Rundensieg gutgeschrieben.");
        }

        [UnityTest]
        public IEnumerator Toter_bleibt_die_ganze_Runde_tot()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });
            match.SuspendedForTests = false;

            var health = player.GetComponent<Health>();
            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive);

            // deutlich laenger warten als der alte Respawn (3 s)
            float waited = 0f;
            while (waited < 5f && match.CurrentPhase == MatchManager.Phase.Playing)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            // Solange die Runde laeuft, bleibt der Spieler tot
            if (match.CurrentPhase == MatchManager.Phase.Playing)
                Assert.IsFalse(health.IsAlive, "Spieler ist mitten in der Runde respawnt.");
        }

        [UnityTest]
        public IEnumerator Kein_Freundschaftsbeschuss_aber_Gegner_wird_getroffen()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            var friend = BotsOnTeam(myTeam)[0];
            var enemy = BotsOnTeam(Team.Opponent(myTeam))[0];

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = 0f, LookPitch = 0f };
            player.SetInputSource(input);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();

            Vector3 aim = player.AimDirection; aim.y = 0f; aim.Normalize();
            Vector3 spot = player.transform.position + aim * 3.5f;

            // 1) Verbuendeter davor -> kein Schaden
            friend.transform.position = spot;
            var friendHp = friend.GetComponent<Health>();
            friendHp.ResetFull();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            int friendBefore = friendHp.Current;

            input.FireHeld = true;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;
            Assert.AreEqual(friendBefore, friendHp.Current, "Freundschaftsbeschuss!");
            // Rueckstoss abklingen lassen
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            // 2) Gegner an dieselbe Stelle -> Schaden
            friend.transform.position += Vector3.up * 60f;
            enemy.transform.position = spot;
            var enemyHp = enemy.GetComponent<Health>();
            enemyHp.ResetFull();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            int enemyBefore = enemyHp.Current;
            Assert.Greater(enemyBefore, 0, "Gegner-Bot tot - Testaufbau kaputt.");

            input.FireHeld = true;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;
            Assert.Less(enemyHp.Current, enemyBefore, "Gegner wurde nicht getroffen.");
        }

        [UnityTest]
        public IEnumerator Match_endet_bei_genug_Rundensiegen_und_startet_neu()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            match.SuspendedForTests = false;
            match.ServerApplyTestConfig(roundsToWin: 2, roundDuration: 999f, restDuration: 1f);

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            int enemyTeam = Team.Opponent(myTeam);

            // Zwei Runden gewinnen: jeweils alle Gegner ausschalten
            for (int round = 1; round <= 2; round++)
            {
                yield return MatchTestHarness.WaitUntil(
                    () => match.CurrentPhase == MatchManager.Phase.Playing, 6f, $"Runde {round} startete nicht.");
                for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

                foreach (var e in BotsOnTeam(enemyTeam))
                {
                    var h = e.GetComponent<Health>();
                    if (h.IsAlive) h.ApplyDamage(9999, player.gameObject);
                }
                yield return null;
                yield return null;
                Assert.AreEqual(MatchManager.Phase.RoundOver, match.CurrentPhase, $"Runde {round} endete nicht.");
            }

            Assert.AreEqual(myTeam, match.MatchWinner, "Falscher Match-Sieger.");

            // Neues Match: Rundensiege zurueck auf 0
            yield return MatchTestHarness.WaitUntil(
                () => match.CurrentPhase == MatchManager.Phase.Playing && match.GetScore(Team.Alpha) == 0 && match.GetScore(Team.Bravo) == 0,
                8f, "Neues Match startete nicht mit 0:0.");
        }
    }
}
