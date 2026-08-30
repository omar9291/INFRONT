using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Infront.Tests
{
    /// <summary>
    /// Gemeinsamer Auf- und Abbau fuer alle Match-Tests. Sorgt fuer einen
    /// DETERMINISTISCHEN Startzustand: Host laeuft, NavMesh gebacken, Teams
    /// zugeteilt, alle Kaempfer da und auf vollem Leben - aber KEIN laufendes
    /// Gefecht (Bots eingefroren, Runde ausgesetzt).
    ///
    /// Damit fallen die frueheren wackeligen Tests weg, die gegen das aktive
    /// 3v3 anliefen.
    /// </summary>
    public static class MatchTestHarness
    {
        public static void BeginFreeze()
        {
            // So frueh wie moeglich, damit Bots gar nicht erst loslaufen.
            BotBrain.GloballyFrozen = true;
        }

        public static IEnumerator Teardown()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null;
                UnityEngine.Object.Destroy(NetworkManager.Singleton.gameObject);
            }
            yield return null;

            BotBrain.GloballyFrozen = false;
            Combatants.Reset();
            SpawnService.Reset();
            yield return null;
        }

        /// <summary>Laedt die Arena und liefert einen stabilen Startzustand.</summary>
        public static IEnumerator LoadReady(Action<NetworkPlayerController, MatchManager> ready,
                                            int expectedCombatants = 6)
        {
            BeginFreeze();

            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;

            yield return WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening, 8f,
                "Host wurde nicht gestartet.");

            yield return WaitUntil(() => MatchManager.Instance != null, 8f, "Kein MatchManager.");
            var match = MatchManager.Instance;
            match.SuspendedForTests = true;
            match.SkipFreezeForTests = true;

            NetworkPlayerController player = null;
            yield return WaitUntil(() =>
            {
                player = UnityEngine.Object.FindAnyObjectByType<NetworkPlayerController>();
                return player != null && player.GetComponent<TeamMember>().TeamId != Team.None;
            }, 8f, "Spieler ohne Team.");

            yield return WaitUntil(() => Combatants.Everyone.Count >= expectedCombatants, 8f,
                $"Nicht alle {expectedCombatants} Kaempfer da.");

            yield return WaitUntil(() =>
            {
                var tri = NavMesh.CalculateTriangulation();
                return tri.vertices != null && tri.vertices.Length > 0;
            }, 6f, "NavMesh nicht gebacken.");

            // Alle frisch machen und einen Moment setzen lassen
            foreach (var member in Combatants.Everyone)
                if (member != null && member.Health != null)
                    member.Health.ResetFull();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();

            ready(player, match);
        }

        public static IEnumerator WaitUntil(Func<bool> condition, float timeout, string message)
        {
            float t = 0f;
            while (!condition() && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), message);
        }

        /// <summary>
        /// Macht die Arena fuer Sichtlinien-Tests frei: blendet alle Kisten aus
        /// und raeumt alle Bots weit weg. Danach ist der Boden eine leere Flaeche.
        /// </summary>
        public static void ClearArena()
        {
            var boxes = GameObject.Find("Boxes");
            if (boxes != null) boxes.SetActive(false);

            foreach (var bot in UnityEngine.Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                bot.SetActive(false);
                var ag = bot.GetComponent<NavMeshAgent>();
                if (ag != null) ag.enabled = false;
                bot.transform.position = new Vector3(0f, 300f, 0f);
            }
        }

        /// <summary>
        /// Stellt den Spieler auf eine feste freie Stelle (0,y,0) und friert ihn
        /// per SetMovementEnabled(false) an Ort und Stelle ein (Schwerkraft aus,
        /// CharacterController bleibt aktiv). Blickrichtung waehlbar.
        /// </summary>
        public static void PlacePlayer(NetworkPlayerController player, Vector3 position, float yaw)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            player.SetMovementEnabled(false);
        }

        /// <summary>Holt einen Bot auf eine NavMesh-Stelle zurueck und taut nur ihn auf.</summary>
        public static bool ReviveBotAt(BotBrain bot, Vector3 near, out Vector3 placed)
        {
            var agent = bot.GetComponent<NavMeshAgent>();
            agent.enabled = true;
            if (NavMesh.SamplePosition(near, out NavMeshHit nh, 12f, NavMesh.AllAreas))
            {
                bot.transform.position = nh.position;
                agent.Warp(nh.position);
                placed = nh.position;
                agent.ResetPath();
                return true;
            }
            placed = near;
            return false;
        }

        /// <summary>Einen einzelnen Bot fuer einen Test aktiv schalten (Rest bleibt eingefroren).</summary>
        public static void Unfreeze(BotBrain bot)
        {
            bot.SetActive(true);
            // GloballyFrozen bleibt true; wir heben es gezielt per Flag auf:
            BotBrain.GloballyFrozen = false;
            foreach (var other in UnityEngine.Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (other != bot)
                    other.SetActive(false);
        }
    }
}
