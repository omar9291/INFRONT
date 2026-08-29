using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-3-Tests: NavMesh, Bot-Wahrnehmung, Bot-Beschuss, Bot-Respawn.
    /// Headless im Host-Modus.
    /// </summary>
    public sealed class BotTests
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

        static IEnumerator LoadArenaAndHost()
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;

            float timeout = 8f;
            while ((NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening, "Host nicht gestartet.");
        }

        static IEnumerator WaitFor<T>(System.Action<T> onFound, float seconds = 10f) where T : Object
        {
            T found = null;
            while (found == null && seconds > 0f)
            {
                found = Object.FindAnyObjectByType<T>();
                seconds -= Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(found, $"{typeof(T).Name} nicht gefunden.");
            onFound(found);
        }

        [UnityTest]
        public IEnumerator NavMesh_wird_zur_Laufzeit_gebacken()
        {
            yield return LoadArenaAndHost();

            float waited = 0f;
            NavMeshTriangulation tri = default;
            while (waited < 6f)
            {
                tri = NavMesh.CalculateTriangulation();
                if (tri.vertices != null && tri.vertices.Length > 0)
                    break;
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(tri.vertices.Length, 0, "Es wurde kein NavMesh gebacken.");
        }

        [UnityTest]
        public IEnumerator Bot_spawnt_und_steht_auf_dem_NavMesh()
        {
            yield return LoadArenaAndHost();

            BotBrain brain = null;
            yield return WaitFor<BotBrain>(b => brain = b);

            // NavMesh + Agent brauchen ein paar Frames
            float waited = 0f;
            var agent = brain.GetComponent<NavMeshAgent>();
            while (!agent.isOnNavMesh && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(agent.isOnNavMesh, "Bot steht nicht auf dem NavMesh.");
            Assert.IsTrue(brain.GetComponent<Health>().IsAlive);
        }

        [UnityTest]
        public IEnumerator Bot_entdeckt_und_verfolgt_den_Spieler()
        {
            yield return LoadArenaAndHost();

            NetworkPlayerController player = null;
            yield return WaitFor<NetworkPlayerController>(p => player = p);

            // Teams abwarten, dann einen GEGNER-Bot nehmen und den Rest stilllegen
            float w0 = 0f;
            while (Infront.MatchManager.Instance == null && w0 < 6f) { w0 += Time.deltaTime; yield return null; }
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            int myTeam = player.GetComponent<TeamMember>().TeamId;

            BotBrain brain = null;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                int bt = b.GetComponent<TeamMember>().TeamId;
                if (bt != myTeam && bt != Team.None && brain == null) brain = b;
                else b.SetActive(false);
            }
            Assert.IsNotNull(brain, "Kein Gegner-Bot gefunden.");
            brain.SetActive(true);

            var agent = brain.GetComponent<NavMeshAgent>();
            float w = 0f;
            while (!agent.isOnNavMesh && w < 5f) { w += Time.deltaTime; yield return null; }

            // Bot 15 m vor den Spieler stellen, so dass er ihn sieht
            Vector3 inFront = player.transform.position + player.transform.forward * 15f + Vector3.up;
            if (NavMesh.SamplePosition(inFront, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                agent.Warp(navHit.position);
            brain.transform.LookAt(player.transform.position);

            float startDist = Vector3.Distance(brain.transform.position, player.transform.position);

            float waited = 0f;
            while (!brain.HasTarget && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(brain.HasTarget, "Bot hat den Spieler nicht entdeckt.");

            // etwas Zeit zum Verfolgen
            for (int i = 0; i < 3; i++) yield return new WaitForSeconds(0.5f);

            float endDist = Vector3.Distance(brain.transform.position, player.transform.position);
            Assert.Less(endDist, startDist - 1f,
                $"Bot ist dem Spieler nicht naeher gekommen (start {startDist:F1}, ende {endDist:F1}).");
        }

        [UnityTest]
        public IEnumerator Bot_schiesst_auf_den_Spieler()
        {
            yield return LoadArenaAndHost();

            NetworkPlayerController player = null;
            yield return WaitFor<NetworkPlayerController>(p => player = p);

            // Warten, bis Teams zugeteilt sind, dann einen GEGNER-Bot nehmen
            float w0 = 0f;
            while (Infront.MatchManager.Instance == null && w0 < 6f) { w0 += Time.deltaTime; yield return null; }
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            int myTeam = player.GetComponent<TeamMember>().TeamId;

            BotBrain brain = null;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                int bt = b.GetComponent<TeamMember>().TeamId;
                if (bt != myTeam && bt != Team.None && brain == null)
                    brain = b;
                else
                    b.SetActive(false); // nur der eine Gegner-Bot bleibt aktiv
            }
            Assert.IsNotNull(brain, "Kein Gegner-Bot gefunden.");
            var botWeapon = brain.GetComponent<NetworkWeapon>();

            var agent = brain.GetComponent<NavMeshAgent>();
            float w = 0f;
            while (!agent.isOnNavMesh && w < 5f) { w += Time.deltaTime; yield return null; }

            // Spieler und Bot auf eine freie Stelle mit klarer Sichtlinie stellen.
            // Eine Stelle mit Boxen dazwischen suchen wir aktiv weg.
            Vector3 basePos = new Vector3(0f, 1f, 0f);
            NavMesh.SamplePosition(basePos, out NavMeshHit baseHit, 8f, NavMesh.AllAreas);
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = baseHit.position + Vector3.up * 0.1f;
            player.GetComponent<CharacterController>().enabled = true;

            Vector3 botSpot = baseHit.position + Vector3.forward * 3.5f;
            if (NavMesh.SamplePosition(botSpot, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
                agent.Warp(navHit.position);
            agent.ResetPath();
            brain.transform.LookAt(player.transform.position);

            var playerHp = player.GetComponent<Health>();
            playerHp.ResetFull();
            brain.GetComponent<Health>().ResetFull();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            int ammoBefore = botWeapon.Ammo;
            int playerHpBefore = playerHp.Current;

            for (int i = 0; i < 8; i++) yield return new WaitForSeconds(0.5f);

            Assert.Less(botWeapon.Ammo, ammoBefore, "Der Bot hat nicht geschossen.");
            Assert.IsTrue(playerHp.Current < playerHpBefore || !playerHp.IsAlive,
                "Der Spieler hat keinen Bot-Schaden genommen.");
        }

        [UnityTest]
        public IEnumerator Bot_stirbt_und_respawnt()
        {
            yield return LoadArenaAndHost();

            BotBrain brain = null;
            yield return WaitFor<BotBrain>(b => brain = b);
            var health = brain.GetComponent<Health>();

            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive, "Bot sollte tot sein.");

            float waited = 0f;
            while (!health.IsAlive && waited < 10f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(health.IsAlive, "Bot ist nicht respawnt.");
            Assert.AreEqual(health.Max, health.Current);
        }
    }
}
