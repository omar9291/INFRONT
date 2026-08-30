using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-3-Tests: NavMesh, Bot-Wahrnehmung, Bot-Beschuss, Bot-Respawn.
    /// Fuer Verhaltenstests wird gezielt EIN Gegner-Bot aufgetaut.
    /// </summary>
    public sealed class BotTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static BotBrain EnemyBot(NetworkPlayerController player)
        {
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                int bt = b.GetComponent<TeamMember>().TeamId;
                if (bt != myTeam && bt != Team.None)
                    return b;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator NavMesh_wird_zur_Laufzeit_gebacken()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            var tri = NavMesh.CalculateTriangulation();
            Assert.Greater(tri.vertices.Length, 0);
        }

        [UnityTest]
        public IEnumerator Bots_finden_einen_Weg_von_Spawn_zu_Spawn()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            // NavMesh ist beim LoadReady schon gebacken. Alle Spawn-Punkte holen.
            var alpha = new System.Collections.Generic.List<Vector3>();
            var bravo = new System.Collections.Generic.List<Vector3>();
            foreach (var sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
                (sp.TeamId == Team.Alpha ? alpha : bravo).Add(sp.transform.position);

            Assert.Greater(alpha.Count, 0);
            Assert.Greater(bravo.Count, 0);

            // Von jedem Alpha-Spawn zu mindestens einem Bravo-Spawn muss ein
            // vollstaendiger Weg existieren.
            foreach (var a in alpha)
            {
                bool reached = false;
                foreach (var b in bravo)
                {
                    var path = new UnityEngine.AI.NavMeshPath();
                    if (UnityEngine.AI.NavMesh.CalculatePath(a, b, UnityEngine.AI.NavMesh.AllAreas, path)
                        && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    {
                        reached = true;
                        break;
                    }
                }
                Assert.IsTrue(reached, $"Von Alpha-Spawn {a} fuehrt kein vollstaendiger Weg zu Bravo.");
            }
        }

        [UnityTest]
        public IEnumerator Bot_spawnt_und_steht_auf_dem_NavMesh()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bot = EnemyBot(player);
            Assert.IsNotNull(bot);
            var agent = bot.GetComponent<NavMeshAgent>();
            yield return MatchTestHarness.WaitUntil(() => agent.isOnNavMesh, 5f, "Bot nicht auf NavMesh.");
            Assert.IsTrue(bot.GetComponent<Health>().IsAlive);
        }

        [UnityTest]
        public IEnumerator Bot_entdeckt_und_verfolgt_den_Spieler()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var brain = EnemyBot(player);
            Assert.IsNotNull(brain);
            var agent = brain.GetComponent<NavMeshAgent>();
            yield return MatchTestHarness.WaitUntil(() => agent.isOnNavMesh, 5f, "Bot nicht auf NavMesh.");

            // Kisten weg, alle Bots weg - dann der eine Bot 14 m vor den Spieler
            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(brain, new Vector3(0f, 1f, 14f), out _),
                "Bot nicht platzierbar.");
            brain.transform.LookAt(player.transform.position);
            BotBrain.GloballyFrozen = false;
            brain.SetActive(true);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            float startDist = Vector3.Distance(brain.transform.position, player.transform.position);
            yield return MatchTestHarness.WaitUntil(() => brain.HasTarget, 5f, "Bot hat den Spieler nicht entdeckt.");
            for (int i = 0; i < 4; i++) yield return new WaitForSeconds(0.5f);

            float endDist = Vector3.Distance(brain.transform.position, player.transform.position);
            Assert.Less(endDist, startDist - 1f, $"Bot nicht naeher gekommen ({startDist:F1} -> {endDist:F1}).");
        }

        [UnityTest]
        public IEnumerator Bot_schiesst_auf_den_Spieler()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var brain = EnemyBot(player);
            Assert.IsNotNull(brain);
            var botWeapon = brain.GetComponent<NetworkWeapon>();
            var agent = brain.GetComponent<NavMeshAgent>();
            yield return MatchTestHarness.WaitUntil(() => agent.isOnNavMesh, 5f, "Bot nicht auf NavMesh.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(brain, new Vector3(0f, 1f, 4f), out _),
                "Bot nicht platzierbar.");
            brain.transform.LookAt(player.transform.position);

            var playerHp = player.GetComponent<Health>();
            playerHp.ResetFull();
            brain.GetComponent<Health>().ResetFull();
            BotBrain.GloballyFrozen = false;
            brain.SetActive(true);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            int ammoBefore = botWeapon.Ammo;
            int hpBefore = playerHp.Current;
            for (int i = 0; i < 8; i++) yield return new WaitForSeconds(0.5f);

            Assert.Less(botWeapon.Ammo, ammoBefore, "Der Bot hat nicht geschossen.");
            Assert.IsTrue(playerHp.Current < hpBefore || !playerHp.IsAlive, "Der Spieler hat keinen Bot-Schaden genommen.");
        }

        [UnityTest]
        public IEnumerator Bot_lebt_beim_Rundenstart_wieder()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            var brain = EnemyBot(player);
            Assert.IsNotNull(brain);
            var health = brain.GetComponent<Health>();

            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive);

            match.StartRound();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(health.IsAlive, "Bot lebt nach Rundenstart nicht.");
            Assert.AreEqual(health.Max, health.Current);
        }
    }
}
