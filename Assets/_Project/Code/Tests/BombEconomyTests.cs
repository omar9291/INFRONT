using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Gruppe C.4 - Bomben-Modus, Etappe 3: Halbzeit-Seitenwechsel, Geld-Boni
    /// und die Bomben-Meldungen im Kill-Feed.
    ///  - Nach der halben Rundenzahl tauschen Angriff und Verteidigung.
    ///  - Zur Halbzeit faellt das Geld aller auf den Startbetrag zurueck.
    ///  - Legen bringt dem Leger Geld.
    ///  - Angreifer, die trotz gelegter Bombe verlieren, bekommen Trostgeld.
    ///  - "Bombe gelegt / entschaerft / explodiert" landet im Kill-Feed.
    /// </summary>
    public sealed class BombEconomyTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static int MyTeam(NetworkPlayerController player) => player.GetComponent<TeamMember>().TeamId;

        static TeamMember AnyBotOnTeam(int team)
        {
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                var tm = b.GetComponent<TeamMember>();
                if (tm.TeamId == team) return tm;
            }
            return null;
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
        public IEnumerator Halbzeit_wechselt_die_Seiten()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            match.ServerForceBombMode(Team.Alpha);
            match.ServerSetRoundsPerHalfForTests(2);
            Assert.AreEqual(Team.Alpha, match.AttackingTeam);

            match.ServerForceRoundEndForTests(Team.Alpha);   // Runde 1
            match.ServerForceRoundEndForTests(Team.Alpha);   // Runde 2 -> Halbzeit
            yield return null;

            Assert.AreEqual(Team.Bravo, match.AttackingTeam, "Seiten wurden nicht getauscht.");
            Assert.AreEqual(Team.Alpha, match.DefendingTeam);
        }

        [UnityTest]
        public IEnumerator Halbzeit_setzt_das_Geld_auf_Start()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            match.ServerForceBombMode(Team.Alpha);
            match.ServerSetRoundsPerHalfForTests(1);

            var wallet = player.GetComponent<Wallet>();
            wallet.ServerAdd(9000);
            Assert.Greater(wallet.Money, 800);

            match.ServerForceRoundEndForTests(Team.Bravo);   // 1 Runde -> Halbzeit
            yield return null;

            Assert.AreEqual(800, wallet.Money, "Geld wurde zur Halbzeit nicht auf den Startbetrag gesetzt.");
        }

        [UnityTest]
        public IEnumerator Legen_bringt_dem_Leger_Geld()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);   // Spieler = Angreifer

            var wallet = player.GetComponent<Wallet>();
            int before = wallet.Money;

            Bomb.Instance.ServerPlantForTests(0, player.GetComponent<TeamMember>());
            yield return null;

            Assert.AreEqual(before + 300, wallet.Money, "Der Leger bekam nicht +300.");
        }

        [UnityTest]
        public IEnumerator Angreifer_verlieren_trotz_Bombe_bekommen_Trostgeld()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);   // Spieler = Angreifer

            var wallet = player.GetComponent<Wallet>();
            Bomb.Instance.ServerPlantForTests(0, player.GetComponent<TeamMember>());   // +300
            yield return null;
            int afterPlant = wallet.Money;

            match.ServerForceRoundEndForTests(Team.Opponent(myTeam));   // Verteidiger gewinnen
            yield return null;

            // Niederlagengeld (1400, Serie 0) + Trostgeld (800) fuer die gelegte Bombe.
            Assert.AreEqual(afterPlant + 1400 + 800, wallet.Money,
                "Trostgeld fuer die trotzdem verlorene Runde fehlt.");
        }

        [UnityTest]
        public IEnumerator Meldungen_Legen_und_Entschaerfen_erreichen_den_Kill_Feed()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            var feed = Object.FindAnyObjectByType<KillFeedHud>();
            Assert.IsNotNull(feed, "Kein KillFeedHud in der Szene.");

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);   // Spieler = Angreifer

            Bomb.Instance.ServerPlantForTests(0, player.GetComponent<TeamMember>());
            yield return MatchTestHarness.WaitUntil(
                () => feed.LastNoteForTests != null && feed.LastNoteForTests.Contains("planted"), 3f,
                "Meldung 'planted' kam nicht im Kill-Feed an.");

            var defender = AnyBotOnTeam(Team.Opponent(myTeam));
            Assert.IsNotNull(defender, "Kein Verteidiger-Bot.");
            match.ServerOnBombDefused(defender);
            yield return MatchTestHarness.WaitUntil(
                () => feed.LastNoteForTests != null && feed.LastNoteForTests.Contains("defused"), 3f,
                "Meldung 'defused' kam nicht im Kill-Feed an.");
        }

        [UnityTest]
        public IEnumerator Meldung_Explosion_erreicht_den_Kill_Feed()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            var feed = Object.FindAnyObjectByType<KillFeedHud>();
            Assert.IsNotNull(feed, "Kein KillFeedHud in der Szene.");

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);   // Spieler = Angreifer

            var bomb = Bomb.Instance;
            bomb.ServerPlantForTests(0, player.GetComponent<TeamMember>());
            bomb.ServerSetDetonateInForTests(0.4f);

            yield return MatchTestHarness.WaitUntil(
                () => feed.LastNoteForTests != null && feed.LastNoteForTests.Contains("exploded"), 4f,
                "Meldung 'exploded' kam nicht im Kill-Feed an.");
        }
    }
}
