using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Gruppe C.4 - Bomben-Modus, Etappe 2: die Bots verstehen das Ziel.
    ///  - Der Patrouillen-Punkt rueckt beim Rundenstart Richtung Kartenmitte vor
    ///    (sonst treffen sich die Teams nie).
    ///  - Ein Angreifer-Bot mit der Bombe laeuft zum Platz und legt sie.
    ///  - Ein Verteidiger-Bot laeuft zur gelegten Bombe und entschaerft sie.
    /// </summary>
    public sealed class BombBotTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static int MyTeam(NetworkPlayerController player) => player.GetComponent<TeamMember>().TeamId;

        static List<BotBrain> BotsOnTeam(int team)
        {
            var list = new List<BotBrain>();
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<TeamMember>().TeamId == team)
                    list.Add(b);
            return list;
        }

        /// <summary>Alle Bots ausser einem weit weg parken, damit der Testbot
        /// niemanden sieht und ungestoert seinen Auftrag erledigt.</summary>
        static void ParkAllBotsExcept(BotBrain keep)
        {
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                if (b == keep) continue;
                b.SetActive(false);
                var ag = b.GetComponent<NavMeshAgent>();
                if (ag != null) ag.enabled = false;
                b.transform.position = new Vector3(0f, 300f, 0f);
            }
        }

        static IEnumerator StartBombRound(MatchManager match, int attackingTeam)
        {
            match.ServerForceBombMode(attackingTeam);
            match.SkipFreezeForTests = true;
            match.ServerSetFreezeDuration(0f);
            match.SuspendedForTests = false;
            match.StartRound();
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return MatchTestHarness.WaitUntil(() => Bomb.Instance != null, 3f, "Keine Bombe.");
        }

        [UnityTest]
        public IEnumerator Patrouillen_Punkt_rueckt_zur_Kartenmitte_vor()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);

            var bots = BotsOnTeam(myTeam);
            Assert.GreaterOrEqual(bots.Count, 1, "Keine Bots im Team.");
            foreach (var bot in bots)
            {
                Vector3 spawn = bot.transform.position;
                Vector3 anchor = bot.BaseAnchorForTests;
                Assert.Less(Mathf.Abs(anchor.z), Mathf.Abs(spawn.z) - 5f,
                    $"Anker von {bot.name} (z={anchor.z:0.0}) ist nicht deutlich naeher an der Mitte als der Spawn (z={spawn.z:0.0}).");
            }
        }

        [UnityTest]
        public IEnumerator Angreifer_Bot_legt_die_Bombe_auf_dem_Platz()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);   // Spieler + Bots = Angreifer

            var bomb = Bomb.Instance;
            var bot = BotsOnTeam(myTeam)[0];

            ParkAllBotsExcept(bot);
            bomb.ServerGiveToForTests(bot.GetComponent<TeamMember>());
            MatchTestHarness.ReviveBotAt(bot, BombSite.CenterOf(0), out _);
            MatchTestHarness.Unfreeze(bot);

            yield return MatchTestHarness.WaitUntil(
                () => bomb.CurrentState == Bomb.State.Planted, 15f, "Bot hat die Bombe nicht gelegt.");
            Assert.AreEqual(0, bomb.PlantedSiteId, "Falscher Platz.");
        }

        [UnityTest]
        public IEnumerator Verteidiger_Bot_entschaerft_die_Bombe()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, Team.Opponent(myTeam));   // Bots von myTeam = Verteidiger

            var bomb = Bomb.Instance;
            var bot = BotsOnTeam(myTeam)[0];

            ParkAllBotsExcept(bot);
            bomb.ServerPlantForTests(0);
            Assert.AreEqual(Bomb.State.Planted, bomb.CurrentState);

            bot.GetComponent<BombAction>().ServerGiveKit();   // 5 s statt 10 s
            MatchTestHarness.ReviveBotAt(bot, bomb.transform.position + new Vector3(1.5f, 0f, 0f), out _);
            MatchTestHarness.Unfreeze(bot);

            yield return MatchTestHarness.WaitUntil(
                () => match.CurrentPhase == MatchManager.Phase.RoundOver, 15f, "Runde endete nicht.");
            Assert.AreEqual(myTeam, match.RoundWinner, "Verteidiger haben nicht gewonnen.");
        }
    }
}
