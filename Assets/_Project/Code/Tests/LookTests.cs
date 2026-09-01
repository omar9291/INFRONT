using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// "Der Look" (Etappe B): Bild-Aufwertung per Post-Processing und die
    /// lesbarere Karte.
    ///
    /// NICHT pruefbar: ob es schoen aussieht. Geprueft wird nur, dass das
    /// Post-Processing-Volume mit den erwarteten Effekten existiert, dass
    /// "Bild: Schlicht" es wirklich abschaltet und dass die Karte die neuen
    /// Markierungen hat.
    /// </summary>
    public sealed class LookTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettings.GraphicsQuality = GameSettings.Graphics.Voll;
            RenderSettings.fog = false;
            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Post_Processing_Volume_existiert_mit_Effekten()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var fx = Object.FindAnyObjectByType<PostFxController>();
            Assert.IsNotNull(fx, "Kein PostFxController in der Arena.");

            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsTrue(fx.HasProfileForTests,
                "Das Post-Processing-Profil hat nicht die erwarteten Effekte.");
            Assert.IsTrue(fx.VolumeActiveForTests,
                "Das Volume ist bei voller Bildqualitaet nicht aktiv.");
            Assert.IsTrue(RenderSettings.fog, "Nebel ist bei voller Bildqualitaet aus.");
        }

        [UnityTest]
        public IEnumerator Schlicht_schaltet_die_Bild_Aufwertung_ab()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            var fx = Object.FindAnyObjectByType<PostFxController>();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(fx.VolumeActiveForTests, "Testaufbau: Volume war nicht an.");

            GameSettings.GraphicsQuality = GameSettings.Graphics.Schlicht;
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsFalse(fx.VolumeActiveForTests,
                "'Schlicht' hat das Volume nicht abgeschaltet.");
            Assert.IsFalse(RenderSettings.fog, "'Schlicht' laesst den Nebel an.");

            GameSettings.GraphicsQuality = GameSettings.Graphics.Voll;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(fx.VolumeActiveForTests, "Zurueck auf 'Voll' brachte das Volume nicht wieder.");
        }

        [UnityTest]
        public IEnumerator Karte_hat_lesbare_Markierungen()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var map = GameObject.Find("Map");
            Assert.IsNotNull(map, "Kein Map-Objekt.");

            int glow = 0, letters = 0, lights = 0;
            foreach (var r in map.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("Glow") || r.name.Contains("Edge")) glow++;
                if (r.name.StartsWith("Site") && r.name.Contains("Bar")) letters++;
            }
            foreach (var l in map.GetComponentsInChildren<Light>()) lights++;

            Assert.Greater(glow, 4, "Zu wenige leuchtende Akzentstreifen auf der Karte.");
            Assert.Greater(letters, 6, "Die Bombenplatz-Buchstaben A/B fehlen.");
            Assert.Greater(lights, 2, "Keine Punktlichter an den Engstellen.");

            var deko = map.transform.Find("Deko");
            Assert.IsNotNull(deko, "Kein Deko-Objekt in der Karte.");
            Assert.Greater(deko.childCount, 20, "Zu wenig Deko (Faesser, Lampen, Rohre ...).");

            var ground = GameObject.Find("Ground");
            Assert.IsNotNull(ground);
            var gm = ground.GetComponent<Renderer>().sharedMaterial;
            Assert.IsNotNull(gm);
            Assert.AreEqual("GroundMat", gm.name, "Der Boden hat noch das weisse Standardmaterial.");
        }
    }
}
