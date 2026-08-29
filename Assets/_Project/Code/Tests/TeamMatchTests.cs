using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-4-Tests: Teams, Abschuss-Wertung, kein Freundschaftsbeschuss,
    /// Rundenende und Neustart.
    /// </summary>
    public sealed class TeamMatchTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null;
                Object.Destroy(NetworkManager.Singleton.gameObject);
            }
            yield return null;
        }

        static IEnumerator LoadAndSettle(System.Action<NetworkPlayerController, MatchManager> ready)
        {
            SceneManager.LoadScene("Arena");
            yield return null; yield return null;

            float t = 8f;
            while ((NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) && t > 0f)
            { t -= Time.deltaTime; yield return null; }
            Assert.IsTrue(NetworkManager.Singleton.IsListening);

            NetworkPlayerController player = null;
            t = 8f;
            while (player == null && t > 0f) { player = Object.FindAnyObjectByType<NetworkPlayerController>(); t -= Time.deltaTime; yield return null; }
            Assert.IsNotNull(player);

            t = 8f;
            while (MatchManager.Instance == null && t > 0f) { t -= Time.deltaTime; yield return null; }
            Assert.IsNotNull(MatchManager.Instance, "Kein MatchManager.");

            // Teams + Startaufstellung abwarten
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            ready(player, MatchManager.Instance);
        }

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
            NetworkPlayerController player = null; MatchManager match = null;
            yield return LoadAndSettle((p, m) => { player = p; match = m; });

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            Assert.AreNotEqual(Team.None, myTeam, "Spieler hat kein Team.");
            Assert.Greater(Combatants.CountByTeam(Team.Alpha), 0);
            Assert.Greater(Combatants.CountByTeam(Team.Bravo), 0);
        }

        [UnityTest]
        public IEnumerator Abschuss_gibt_dem_Schuetzen_Team_einen_Punkt()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return LoadAndSettle((p, m) => { player = p; match = m; });

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            int enemyTeam = Team.Opponent(myTeam);

            var enemyBots = BotsOnTeam(enemyTeam);
            Assert.Greater(enemyBots.Count, 0, "Kein Gegner-Bot.");

            int before = match.GetScore(myTeam);
            enemyBots[0].GetComponent<Health>().ApplyDamage(9999, player.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(before + 1, match.GetScore(myTeam),
                "Der Abschuss hat dem Team keinen Punkt gebracht.");
        }

        [UnityTest]
        public IEnumerator Kein_Freundschaftsbeschuss_Kugel_fliegt_durch_Verbuendete()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return LoadAndSettle((p, m) => { player = p; match = m; });

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            var friend = BotsOnTeam(myTeam);
            var enemies = BotsOnTeam(Team.Opponent(myTeam));
            Assert.Greater(friend.Count, 0, "Kein verbuendeter Bot.");
            Assert.Greater(enemies.Count, 0, "Kein Gegner-Bot.");

            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                b.SetActive(false);

            // Verbuendeten 3 m vor den Spieler, Gegner 6 m dahinter - eine Linie
            Vector3 fwd = player.transform.forward;
            friend[0].transform.position = player.transform.position + fwd * 3f;
            enemies[0].transform.position = player.transform.position + fwd * 6f;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            var friendHp = friend[0].GetComponent<Health>();
            var enemyHp = enemies[0].GetComponent<Health>();
            int friendBefore = friendHp.Current;
            int enemyBefore = enemyHp.Current;

            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = player.transform.eulerAngles.y, LookPitch = 0f, FireHeld = true };
            player.SetInputSource(input);
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;

            Assert.AreEqual(friendBefore, friendHp.Current, "Verbuendeter hat Schaden genommen (Freundschaftsbeschuss!).");
            Assert.Less(enemyHp.Current, enemyBefore, "Der Gegner dahinter wurde nicht getroffen.");
        }

        [UnityTest]
        public IEnumerator Runde_endet_bei_Punktelimit_und_startet_neu()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return LoadAndSettle((p, m) => { player = p; match = m; });

            match.ServerApplyTestConfig(scoreLimit: 2, roundDuration: 999f, restDuration: 1f);

            int myTeam = player.GetComponent<TeamMember>().TeamId;
            var enemyBots = BotsOnTeam(Team.Opponent(myTeam));
            Assert.GreaterOrEqual(enemyBots.Count, 2, "Zu wenige Gegner-Bots fuer den Test.");

            enemyBots[0].GetComponent<Health>().ApplyDamage(9999, player.gameObject);
            yield return null;
            enemyBots[1].GetComponent<Health>().ApplyDamage(9999, player.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(MatchManager.Phase.RoundOver, match.CurrentPhase, "Runde endete nicht beim Limit.");
            Assert.AreEqual(myTeam, match.Winner, "Falscher Sieger.");

            // Nach der Pause: neue Runde, Punkte zurueck
            float waited = 0f;
            while (match.CurrentPhase != MatchManager.Phase.Playing && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(MatchManager.Phase.Playing, match.CurrentPhase, "Neue Runde startete nicht.");
            Assert.AreEqual(0, match.GetScore(Team.Alpha));
            Assert.AreEqual(0, match.GetScore(Team.Bravo));
        }
    }
}
