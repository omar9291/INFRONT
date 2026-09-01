using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Rauch blockiert die Sicht - und zwar auch die der Bots (das Entscheidende
    /// am Valorant-Weg).
    /// </summary>
    public sealed class AbilitySightTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [Test]
        public void Rauch_Verzeichnis_blockiert_die_Sichtlinie()
        {
            SmokeRegistry.Reset();
            Vector3 a = new Vector3(0f, 1.6f, 0f);
            Vector3 b = new Vector3(0f, 1.6f, 20f);

            Assert.IsFalse(SmokeRegistry.Blocks(a, b), "Ohne Rauch darf nichts blockieren.");

            var cloud = new GameObject("Wolke");
            cloud.transform.position = new Vector3(0f, 1.6f, 10f);   // genau dazwischen
            SmokeRegistry.Register(cloud.transform, 3f);
            Assert.IsTrue(SmokeRegistry.Blocks(a, b), "Rauch auf der Linie blockiert nicht.");

            // Eine Linie, die weit daneben laeuft, bleibt frei.
            Assert.IsFalse(SmokeRegistry.Blocks(new Vector3(20f, 1.6f, 0f), new Vector3(20f, 1.6f, 20f)),
                "Rauch blockiert eine Linie, die gar nicht durchgeht.");

            SmokeRegistry.Unregister(cloud.transform);
            Assert.IsFalse(SmokeRegistry.Blocks(a, b), "Nach dem Abbau darf nichts mehr blockieren.");
            Object.DestroyImmediate(cloud);
        }

        [UnityTest]
        public IEnumerator Rauch_nimmt_dem_Bot_die_Sicht_auf_den_Spieler()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            BotBrain bot = null;
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId != myTeam) { bot = b; break; }
            Assert.IsNotNull(bot);

            var agent = bot.GetComponent<UnityEngine.AI.NavMeshAgent>();
            yield return MatchTestHarness.WaitUntil(() => agent.isOnNavMesh, 5f, "Bot nicht auf NavMesh.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 12f), out _));
            bot.transform.LookAt(player.transform.position);

            // Rauchwand genau zwischen Bot und Spieler - VOR dem Auftauen.
            var cloud = new GameObject("TestRauch");
            cloud.transform.position = new Vector3(0f, 1.4f, 6f);
            SmokeRegistry.Register(cloud.transform, 3.5f);

            MatchTestHarness.Unfreeze(bot);
            for (int i = 0; i < 90; i++) yield return null;   // ~1,5 s Zeit zum Entdecken

            Assert.IsFalse(bot.HasTarget,
                "Der Bot hat den Spieler DURCH den Rauch entdeckt.");

            // Gegenprobe: Rauch weg -> Bot findet den Spieler. Der Bot kann
            // inzwischen patrouilliert / sich weggedreht haben, deshalb zuruecksetzen
            // und beim Warten laufend wieder auf den Spieler ausrichten.
            SmokeRegistry.Unregister(cloud.transform);
            Object.DestroyImmediate(cloud);
            MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 12f), out _);

            float t = 0f;
            while (!bot.HasTarget && t < 6f)
            {
                bot.transform.LookAt(player.transform.position);
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(bot.HasTarget, "Ohne Rauch muss der Bot den Spieler sehen.");
        }
    }
}
