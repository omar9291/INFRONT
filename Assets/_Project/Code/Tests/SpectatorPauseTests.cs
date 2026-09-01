using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Zwei Playtest-Fehler aus Sitzung 7:
    ///  1. Nach dem Tod fror die Kamera ein, wenn kein Verbuendeter mehr lebte
    ///     (statt jemandem zuzuschauen).
    ///  2. Esc hielt die Rundenzeit nicht an.
    ///
    /// NICHT prüfbar: wie sich die Pause anfuehlt, ob die Zuschau-Kamera
    /// ruckelfrei folgt.
    /// </summary>
    public sealed class SpectatorPauseTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Sicher aus einer evtl. haengengebliebenen Pause zurueck.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Zuschauen_wechselt_zu_Gegnern_wenn_das_ganze_Team_tot_ist()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var cam = Camera.main;
            Assert.IsNotNull(cam, "Keine Arena-Kamera.");
            var fpc = cam.GetComponent<FirstPersonCamera>();
            Assert.IsNotNull(fpc, "Arena-Kamera ohne FirstPersonCamera.");

            int myTeam = player.GetComponent<TeamMember>().TeamId;

            // Das ganze eigene Team ausschalten (Spieler + alle Verbuendeten).
            // Die Gegner bleiben am Leben (eingefrorene Bots).
            foreach (var member in Combatants.Everyone)
                if (member != null && member.TeamId == myTeam
                    && member.Health != null && member.Health.IsAlive)
                    member.Health.ApplyDamage(9999, NetworkManager.ServerClientId);

            yield return MatchTestHarness.WaitUntil(
                () => !player.GetComponent<Health>().IsAlive, 3f, "Spieler wurde nicht getoetet.");

            // Ein paar Frames Update() laufen lassen.
            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(MatchManager.Phase.Playing, MatchManager.Instance.CurrentPhase,
                "Testaufbau: die Runde ist beendet, Zuschauen greift dann nicht.");
            Assert.IsTrue(fpc.IsSpectatingForTests,
                "Die Kamera schaut niemandem zu (eingefroren) - genau der Playtest-Fehler.");

            string who = player.SpectatingName;
            Assert.IsNotNull(who, "Kein Zuschau-Ziel angezeigt.");
            Assert.IsTrue(who.StartsWith("Gegner"),
                $"Sollte einem Gegner zuschauen, Anzeige war aber: {who}");
        }

        [UnityTest]
        public IEnumerator Solo_Pause_haelt_die_Rundenuhr_an()
        {
            MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => match = m);

            // Uhr echt laufen lassen, aber die Runde nicht beenden
            // (Gefecht bleibt eingefroren, niemand stirbt).
            match.SuspendedForTests = false;

            double before = match.SecondsRemaining;
            Assert.Greater(before, 5.0, "Testaufbau: zu wenig Rundenzeit uebrig.");

            match.ServerBeginSoloPause();
            Assert.IsTrue(match.IsSoloPaused, "Pause wurde nicht gesetzt.");

            for (int i = 0; i < 60; i++) yield return null;   // ~1 s vergeht real

            match.ServerEndSoloPause();
            Assert.IsFalse(match.IsSoloPaused, "Pause wurde nicht beendet.");

            double after = match.SecondsRemaining;
            Assert.Less(System.Math.Abs(after - before), 0.5,
                $"Die Pause hat Rundenzeit gefressen: {before:0.00} -> {after:0.00}");
        }
    }
}
