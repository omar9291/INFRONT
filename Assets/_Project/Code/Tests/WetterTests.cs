using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// "Die Welt lebt" - P1: Wetter pro Runde, REIN OPTISCH.
    ///
    /// NICHT prüfbar: wie es aussieht. Geprüft wird:
    ///  - jede Wetterlage bleibt unter der sicheren Nebel-Obergrenze
    ///    (die Sichtweite darf sich nicht ändern),
    ///  - aufeinanderfolgende Runden bekommen verschiedene Lagen,
    ///  - "Bild: Schlicht" schaltet Wetter, Nebelbank und Staub ab.
    /// </summary>
    public sealed class WetterTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettings.GraphicsQuality = GameSettings.Graphics.Voll;
            RenderSettings.fog = false;
            yield return MatchTestHarness.Teardown();
        }

        [Test]
        public void Jede_Lage_bleibt_im_sicheren_Nebel_Band()
        {
            foreach (var p in WeatherDirector.Presets)
            {
                Assert.LessOrEqual(p.FogDensity, WeatherDirector.MaxSafeFogDensity,
                    "Eine Wetterlage hat zu dichten Distanz-Nebel - das würde die Sichtweite ändern.");

                // Auf 60 m muss von einem Gegner noch klar Kontrast ankommen.
                float d = p.FogDensity * 60f;
                float transmit = Mathf.Exp(-d * d);   // ExponentialSquared
                Assert.Greater(transmit, 0.4f,
                    $"Bei dieser Lage kommen auf 60 m nur {transmit:P0} durch - zu wenig.");
            }
        }

        [UnityTest]
        public IEnumerator Aufeinanderfolgende_Runden_bekommen_verschiedene_Lagen()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var wd = Object.FindAnyObjectByType<WeatherDirector>();
            Assert.IsNotNull(wd, "Kein WeatherDirector in der Arena.");
            for (int i = 0; i < 3; i++) yield return null;

            var seen = new System.Collections.Generic.HashSet<WeatherKind>();
            var prev = wd.CurrentWeatherForTests;
            seen.Add(prev);

            for (int round = 0; round < 12; round++)
            {
                var next = wd.PickNextForTests();
                Assert.AreNotEqual(prev, next, "Zwei Runden hintereinander dieselbe Wetterlage.");
                seen.Add(next);
                prev = next;
            }

            Assert.GreaterOrEqual(seen.Count, 3, "Über 12 Runden kamen kaum verschiedene Lagen vor.");
        }

        [UnityTest]
        public IEnumerator Schlicht_schaltet_Wetter_Nebelbank_und_Staub_ab()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var wd = Object.FindAnyObjectByType<WeatherDirector>();
            var fog = Object.FindAnyObjectByType<GroundFog>();
            Assert.IsNotNull(wd);
            Assert.IsNotNull(fog, "Keine GroundFog-Nebelbank in der Arena.");

            wd.ForceWeatherForTests(WeatherKind.Bodennebel);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(fog.Intensity01ForTests, 0.2f, "Testaufbau: die Nebelbank kam nicht hoch.");

            GameSettings.GraphicsQuality = GameSettings.Graphics.Schlicht;
            for (int i = 0; i < 100; i++) yield return null;

            Assert.IsFalse(RenderSettings.fog, "'Schlicht' lässt den Nebel an.");
            Assert.Less(fog.Intensity01ForTests, 0.05f, "'Schlicht' fährt die Nebelbank nicht herunter.");

            foreach (var d in Object.FindObjectsByType<AtmosphereDust>(FindObjectsSortMode.None))
                Assert.Less(d.Density01ForTests, 0.05f, "'Schlicht' lässt den Staub laufen.");
        }

        [UnityTest]
        public IEnumerator Nebel_ist_bei_voller_Qualitaet_an_und_im_Band()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            var wd = Object.FindAnyObjectByType<WeatherDirector>();
            Assert.IsNotNull(wd);

            wd.ForceWeatherForTests(WeatherKind.Rauch);   // die dichteste Lage
            for (int i = 0; i < 40; i++) yield return null;

            Assert.IsTrue(RenderSettings.fog, "Bei voller Qualität ist der Nebel aus.");
            Assert.LessOrEqual(RenderSettings.fogDensity, WeatherDirector.MaxSafeFogDensity + 0.0001f,
                "Der Nebel ist über die sichere Obergrenze gelaufen.");
        }
    }
}
