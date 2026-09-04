using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Das zusammenhaengende Spiel-HUD (<see cref="HudController"/>, UI Toolkit).
    ///
    /// NICHT pruefbar: wie es aussieht, ob etwas verrutscht, ob Animationen
    /// ruckeln. Geprueft wird: der Baum wird gebaut, Leben/Munition stehen
    /// richtig drin, die Lebende-Rauten zaehlen mit, die Statuszeile zeigt die
    /// Kaufzeit, und das HUD faengt keine Mausklicks ab (PickingMode).
    /// </summary>
    public sealed class HudControllerTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static HudController Hud() => Object.FindAnyObjectByType<HudController>();

        [UnityTest]
        public IEnumerator HUD_wird_gebaut()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            var hud = Hud();
            Assert.IsNotNull(hud, "Kein HudController in der Arena.");

            for (int i = 0; i < 20 && !hud.IsBuiltForTests; i++) yield return null;
            Assert.IsTrue(hud.IsBuiltForTests, "Der HUD-Baum wurde nicht gebaut.");
            Assert.IsNotNull(hud.RootForTests);
        }

        [UnityTest]
        public IEnumerator Leben_und_Munition_stehen_im_HUD()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var hud = Hud();
            for (int i = 0; i < 20 && !hud.IsBuiltForTests; i++) yield return null;

            player.GetComponent<Health>().ApplyDamage(25, NetworkManager.ServerClientId);
            for (int i = 0; i < 10; i++) yield return null;

            int hp = player.GetComponent<Health>().Current;
            Assert.AreEqual(hp.ToString(), hud.HealthTextForTests,
                "Die HUD-Lebenszahl passt nicht zum echten Leben.");

            int ammo = player.GetComponent<NetworkWeapon>().Ammo;
            Assert.AreEqual(ammo.ToString(), hud.AmmoTextForTests,
                "Die HUD-Munition passt nicht zur echten Munition.");
        }

        [UnityTest]
        public IEnumerator Lebende_Rauten_zaehlen_mit()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var hud = Hud();
            for (int i = 0; i < 20 && !hud.IsBuiltForTests; i++) yield return null;
            for (int i = 0; i < 5; i++) yield return null;

            int alphaBefore = hud.AliveDotsForTests(Team.Alpha);
            int bravoBefore = hud.AliveDotsForTests(Team.Bravo);
            Assert.Greater(alphaBefore, 0, "Keine ALPHA-Rauten.");
            Assert.Greater(bravoBefore, 0, "Keine BRAVO-Rauten.");

            // einen Gegner ausschalten -> eine Raute weniger auf seiner Seite
            int myTeam = player.GetComponent<TeamMember>().TeamId;
            int foeTeam = Team.Opponent(myTeam);
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == foeTeam && m.Health != null && m.Health.IsAlive)
                {
                    m.Health.ApplyDamage(9999, NetworkManager.ServerClientId);
                    break;
                }
            for (int i = 0; i < 10; i++) yield return null;

            int foeAfter = hud.AliveDotsForTests(foeTeam);
            Assert.AreEqual((foeTeam == Team.Alpha ? alphaBefore : bravoBefore) - 1, foeAfter,
                "Nach einem Abschuss ist keine Raute erloschen.");
        }

        [UnityTest]
        public IEnumerator Statuszeile_zeigt_die_Kaufzeit()
        {
            MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => match = m);
            var hud = Hud();
            for (int i = 0; i < 20 && !hud.IsBuiltForTests; i++) yield return null;

            match.SuspendedForTests = false;
            match.ForceBuyTimeForTests = true;
            for (int i = 0; i < 10; i++) yield return null;

            string s = hud.StatusLineForTests;
            Assert.IsTrue(s != null && s.Contains("BUY TIME"),
                $"Die Statuszeile zeigt die Kaufzeit nicht an (war: '{s}').");

            match.ForceBuyTimeForTests = false;
        }

        [UnityTest]
        public IEnumerator HUD_faengt_keine_Mausklicks_ab()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            var hud = Hud();
            for (int i = 0; i < 20 && !hud.IsBuiltForTests; i++) yield return null;

            // Ein Punkt mitten im Bild (dort ist das Fadenkreuz / geschossen wird).
            var root = hud.RootForTests;
            var panel = root.panel;
            Assert.IsNotNull(panel);
            Vector2 mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var hit = panel.Pick(mid);
            Assert.IsTrue(hit == null || hit == root,
                $"Das HUD faengt in der Bildmitte einen Klick ab ({hit?.name}) - Schiessen waere blockiert.");
        }
    }
}
