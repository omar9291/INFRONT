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
    /// Gruppe C.4 - Bomben-Modus, Etappe 1: legen dauert die volle Zeit,
    /// nur auf einem Platz, entschaerfen gewinnt fuer die Verteidiger,
    /// Explosion gewinnt fuer die Angreifer, alle Angreifer tot nach dem
    /// Legen laesst die Runde weiterlaufen (davor nicht), Zeitablauf ohne
    /// Legen geht an die Verteidiger, ein gefallener Traeger laesst die
    /// Bombe liegen und ein anderer hebt sie auf. Der Ausscheide-Modus
    /// bleibt unveraendert.
    /// </summary>
    public sealed class BombModeTests
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

        static int MyTeam(NetworkPlayerController player) => player.GetComponent<TeamMember>().TeamId;

        /// <summary>Bomben-Runde vorbereiten: Modus setzen, Kaufzeit aus, Runde neu.</summary>
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
        public IEnumerator Bombe_legen_dauert_die_volle_Zeit()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            yield return StartBombRound(match, MyTeam(player));   // Spieler = Angreifer

            var bomb = Bomb.Instance;
            bomb.ServerGiveToForTests(player.GetComponent<TeamMember>());

            MatchTestHarness.PlacePlayer(player, new Vector3(-19f, 2f, 3f), 0f);
            var input = new FakePlayerInput { UseHeld = true };
            player.SetInputSource(input);
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();

            yield return new WaitForSeconds(1f);
            Assert.AreNotEqual(Bomb.State.Planted, bomb.CurrentState, "Bombe wurde zu frueh gelegt.");

            yield return MatchTestHarness.WaitUntil(
                () => bomb.CurrentState == Bomb.State.Planted, 4f, "Bombe wurde nicht gelegt.");
            Assert.AreEqual(0, bomb.PlantedSiteId, "Falscher Platz.");
        }

        [UnityTest]
        public IEnumerator Ausserhalb_des_Platzes_kein_Legen()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            yield return StartBombRound(match, MyTeam(player));

            var bomb = Bomb.Instance;
            bomb.ServerGiveToForTests(player.GetComponent<TeamMember>());

            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 8f), 0f);   // Mitte, kein Platz
            var input = new FakePlayerInput { UseHeld = true };
            player.SetInputSource(input);

            yield return new WaitForSeconds(4f);
            Assert.AreNotEqual(Bomb.State.Planted, bomb.CurrentState, "Bombe ausserhalb eines Platzes gelegt.");
        }

        [UnityTest]
        public IEnumerator Entschaerfen_gewinnt_die_Runde_fuer_die_Verteidiger()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, Team.Opponent(myTeam));   // Spieler = Verteidiger

            var bomb = Bomb.Instance;
            bomb.ServerPlantForTests(0);
            Assert.AreEqual(Bomb.State.Planted, bomb.CurrentState);

            player.GetComponent<BombAction>().ServerGiveKit();   // 5 s statt 10 s
            MatchTestHarness.PlacePlayer(player, bomb.transform.position, 0f);
            var input = new FakePlayerInput { UseHeld = true };
            player.SetInputSource(input);

            yield return MatchTestHarness.WaitUntil(
                () => match.CurrentPhase == MatchManager.Phase.RoundOver, 9f, "Runde endete nicht.");
            Assert.AreEqual(myTeam, match.RoundWinner, "Verteidiger haben nicht gewonnen.");
        }

        [UnityTest]
        public IEnumerator Explosion_gewinnt_die_Runde_fuer_die_Angreifer()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);   // Spieler = Angreifer

            var bomb = Bomb.Instance;
            bomb.ServerPlantForTests(0);
            bomb.ServerSetDetonateInForTests(0.5f);

            yield return MatchTestHarness.WaitUntil(
                () => match.CurrentPhase == MatchManager.Phase.RoundOver, 4f, "Runde endete nicht.");
            Assert.AreEqual(myTeam, match.RoundWinner, "Angreifer haben nicht gewonnen.");
        }

        [UnityTest]
        public IEnumerator Alle_Angreifer_tot_nach_dem_Legen_Runde_laeuft_weiter()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);

            var bomb = Bomb.Instance;
            bomb.ServerPlantForTests(0);
            bomb.ServerSetDetonateInForTests(30f);   // Zuender laeuft, damit nichts detoniert

            player.GetComponent<Health>().ApplyDamage(9999, (GameObject)null);
            foreach (var b in BotsOnTeam(myTeam))
                b.GetComponent<Health>().ApplyDamage(9999, (GameObject)null);
            yield return null;
            yield return null;

            Assert.AreEqual(MatchManager.Phase.Playing, match.CurrentPhase,
                "Runde endete, obwohl die Bombe schon lag.");
        }

        [UnityTest]
        public IEnumerator Alle_Angreifer_tot_vor_dem_Legen_Verteidiger_gewinnen()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);

            player.GetComponent<Health>().ApplyDamage(9999, (GameObject)null);
            foreach (var b in BotsOnTeam(myTeam))
                b.GetComponent<Health>().ApplyDamage(9999, (GameObject)null);
            yield return null;
            yield return null;

            Assert.AreEqual(MatchManager.Phase.RoundOver, match.CurrentPhase, "Runde endete nicht.");
            Assert.AreEqual(Team.Opponent(myTeam), match.RoundWinner, "Verteidiger haben nicht gewonnen.");
        }

        [UnityTest]
        public IEnumerator Zeit_ablaeuft_ohne_Legen_Verteidiger_gewinnen()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            match.ServerForceBombMode(myTeam);
            match.SkipFreezeForTests = true;
            match.SuspendedForTests = false;
            match.ServerApplyTestConfig(15, 1.2f, 5f);   // Rundenzeit 1.2 s, Kaufzeit 0
            match.StartRound();

            yield return MatchTestHarness.WaitUntil(
                () => match.CurrentPhase == MatchManager.Phase.RoundOver, 6f, "Runde lief nicht ab.");
            Assert.AreEqual(Team.Opponent(myTeam), match.RoundWinner, "Verteidiger haben nicht gewonnen.");
        }

        [UnityTest]
        public IEnumerator Traeger_stirbt_Bombe_faellt_und_wird_aufgehoben()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            int myTeam = MyTeam(player);
            yield return StartBombRound(match, myTeam);

            var bomb = Bomb.Instance;
            var attackers = BotsOnTeam(myTeam);
            Assert.GreaterOrEqual(attackers.Count, 2, "Zu wenige Angreifer-Bots fuer den Test.");
            var carrier = attackers[0];
            var other = attackers[1];

            carrier.GetComponent<NavMeshAgent>().enabled = false;
            other.GetComponent<NavMeshAgent>().enabled = false;

            var spot = new Vector3(3f, 1f, 3f);
            carrier.transform.position = spot;
            bomb.ServerGiveToForTests(carrier.GetComponent<TeamMember>());
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(Bomb.State.Carried, bomb.CurrentState);

            carrier.GetComponent<Health>().ApplyDamage(9999, (GameObject)null);
            yield return null;
            yield return null;
            Assert.AreEqual(Bomb.State.Dropped, bomb.CurrentState, "Bombe ist nicht gefallen.");

            other.transform.position = spot;
            yield return MatchTestHarness.WaitUntil(
                () => bomb.CurrentState == Bomb.State.Carried, 3f, "Bombe wurde nicht aufgehoben.");
            Assert.AreEqual(other.GetComponent<NetworkObject>().NetworkObjectId, bomb.CarrierId,
                "Falscher neuer Traeger.");
        }

        [UnityTest]
        public IEnumerator Bombe_ist_im_Ausscheide_Modus_inaktiv()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });

            Assert.IsFalse(match.IsBombMode, "Standardmodus sollte Ausscheiden sein.");
            yield return MatchTestHarness.WaitUntil(() => Bomb.Instance != null, 3f, "Keine Bombe erzeugt.");

            match.SuspendedForTests = false;
            match.StartRound();
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(Bomb.State.Inactive, Bomb.Instance.CurrentState,
                "Bombe ist im Ausscheide-Modus aktiv.");
        }
    }
}
