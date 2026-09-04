using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Etappe D: Bots hoeren (Schuesse / Schritte) und sagen etwas an.
    ///
    /// NICHT pruefbar: ob sich der Kampf gegen die Bots "schlau" anfuehlt.
    /// </summary>
    public sealed class BotSenseTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static BotBrain EnemyBot(NetworkPlayerController player)
        {
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId != myTeam
                    && b.GetComponent<TeamMember>().TeamId != Team.None)
                    return b;
            return null;
        }

        [UnityTest]
        public IEnumerator Bot_hoert_einen_Schuss_hinter_sich_und_geht_nachschauen()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bot = EnemyBot(player);
            Assert.IsNotNull(bot);
            var agent = bot.GetComponent<NavMeshAgent>();
            yield return MatchTestHarness.WaitUntil(() => agent.isOnNavMesh, 5f, "Bot nicht auf NavMesh.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, -40f), 0f);  // weit weg, ausser Sicht
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 0f), out _));
            bot.transform.rotation = Quaternion.Euler(0f, 0f, 0f);   // schaut nach +Z
            MatchTestHarness.Unfreeze(bot);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual("Patrol", bot.CurrentState, "Testaufbau: Bot ist nicht in Patrol.");

            // Ein Schuss GEGEN den Bot, hinter ihm (-Z).
            SoundEvents.ServerReport(new Vector3(0f, 1f, -10f), SoundEvents.ShotLoud,
                player.GetComponent<TeamMember>().TeamId);

            yield return MatchTestHarness.WaitUntil(
                () => bot.CurrentState == "Search", 3f,
                "Der Bot hat den Schuss hinter sich nicht gehoert.");
        }

        [UnityTest]
        public IEnumerator Bot_sagt_etwas_an_wenn_er_einen_Feind_sieht()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bot = EnemyBot(player);
            Assert.IsNotNull(bot);
            var agent = bot.GetComponent<NavMeshAgent>();
            yield return MatchTestHarness.WaitUntil(() => agent.isOnNavMesh, 5f, "Bot nicht auf NavMesh.");

            var feed = Object.FindAnyObjectByType<KillFeedHud>();
            Assert.IsNotNull(feed, "Kein KillFeedHud.");

            string got = null;
            void OnCallout(string t, int team) => got = t;
            MatchManager.Instance.CalloutReported += OnCallout;

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 10f), out _));
            bot.transform.LookAt(player.transform.position);
            MatchTestHarness.Unfreeze(bot);

            yield return MatchTestHarness.WaitUntil(() => got != null, 5f,
                "Der Bot hat beim Entdecken nichts angesagt.");
            MatchManager.Instance.CalloutReported -= OnCallout;

            Assert.IsTrue(got.Contains("Enemy") || got.Contains("help") || got.Contains("hear"),
                $"Unerwartete Ansage: {got}");
        }
    }
}
