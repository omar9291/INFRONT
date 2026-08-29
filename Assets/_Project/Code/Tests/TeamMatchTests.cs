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
        public IEnumerator Abschuss_gibt_dem_Schuetzen_Team_einen_Punkt()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            var enemy = BotsOnTeam(Team.Opponent(myTeam))[0].GetComponent<Health>();
            enemy.ResetFull();
            yield return null;

            int before = match.GetScore(myTeam);
            enemy.ApplyDamage(9999, player.gameObject);
            yield return null;
            yield return null;
            Assert.AreEqual(before + 1, match.GetScore(myTeam));
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
            Vector3 spot = player.transform.position + aim * 5f;

            // 1) Verbuendeter davor -> kein Schaden
            friend.transform.position = spot;
            var friendHp = friend.GetComponent<Health>();
            friendHp.ResetFull();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            int friendBefore = friendHp.Current;

            input.FireHeld = true;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;
            Assert.AreEqual(friendBefore, friendHp.Current, "Freundschaftsbeschuss!");

            // 2) Gegner an dieselbe Stelle -> Schaden
            friend.transform.position += Vector3.up * 60f;
            enemy.transform.position = spot;
            var enemyHp = enemy.GetComponent<Health>();
            enemyHp.ResetFull();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            int enemyBefore = enemyHp.Current;
            Assert.Greater(enemyBefore, 0, "Gegner-Bot tot - Testaufbau kaputt.");

            input.FireHeld = true;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;
            Assert.Less(enemyHp.Current, enemyBefore, "Gegner wurde nicht getroffen.");
        }

        [UnityTest]
        public IEnumerator Runde_endet_bei_Punktelimit_und_startet_neu()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            match.SuspendedForTests = false;
            match.ServerApplyTestConfig(scoreLimit: 2, roundDuration: 999f, restDuration: 1f);

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            var enemies = BotsOnTeam(Team.Opponent(myTeam));
            Assert.GreaterOrEqual(enemies.Count, 2);

            var h0 = enemies[0].GetComponent<Health>();
            var h1 = enemies[1].GetComponent<Health>();
            h0.ResetFull(); h1.ResetFull();
            yield return null;
            h0.ApplyDamage(9999, player.gameObject);
            yield return null;
            h1.ApplyDamage(9999, player.gameObject);
            yield return null; yield return null;

            Assert.AreEqual(MatchManager.Phase.RoundOver, match.CurrentPhase, "Runde endete nicht.");
            Assert.AreEqual(myTeam, match.Winner);

            yield return MatchTestHarness.WaitUntil(() => match.CurrentPhase == MatchManager.Phase.Playing, 6f,
                "Neue Runde startete nicht.");
            Assert.AreEqual(0, match.GetScore(Team.Alpha));
            Assert.AreEqual(0, match.GetScore(Team.Bravo));
        }
    }
}
