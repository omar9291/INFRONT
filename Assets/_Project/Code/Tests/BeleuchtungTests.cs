using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// "Die Welt lebt" - P2: ernste Beleuchtung mit echten Schatten.
    ///
    /// NICHT prüfbar: wie es aussieht. Geprüft wird:
    ///  - das URP-Asset erlaubt Zusatzlicht- und weiche Schatten, mit
    ///    ausreichender Schattenweite für die grosse Karte,
    ///  - in der Arena werfen genug Lichter echte Schatten (Sonne + Ankerlichter),
    ///  - das Umgebungslicht ist heruntergefahren (sonst sieht man die Schatten nicht).
    /// </summary>
    public sealed class BeleuchtungTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [Test]
        public void URP_Asset_erlaubt_echte_Schatten()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(urp, "Kein URP-Asset aktiv.");

            Assert.IsTrue(urp.supportsAdditionalLightShadows,
                "Zusatzlichter dürfen keine Schatten werfen - Objekte schweben dann.");
            Assert.IsTrue(urp.supportsSoftShadows, "Weiche Schatten sind aus.");
            Assert.GreaterOrEqual(urp.shadowDistance, 60f,
                "Die Schattenweite ist zu kurz für die 100-m-Karte.");
        }

        [UnityTest]
        public IEnumerator Arena_hat_genug_schattenwerfende_Lichter()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            int casters = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.shadows != LightShadows.None) casters++;

            Assert.GreaterOrEqual(casters, 5,
                $"Nur {casters} Lichter werfen Schatten - zu wenig Kontrast/Tiefe.");
        }

        [UnityTest]
        public IEnumerator Arena_hat_Lichtschaechte_und_treibenden_Staub()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            for (int i = 0; i < 5; i++) yield return null;

            int shafts = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Spot && l.name.StartsWith("Shaft_")) shafts++;
            Assert.GreaterOrEqual(shafts, 4, "Zu wenige Lichtschächte in der Arena.");

            var dust = Object.FindObjectsByType<AtmosphereDust>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(dust.Length, 3, "Zu wenige Staub-Volumen.");
            foreach (var d in dust)
                Assert.IsTrue(d.RunningForTests, "Ein Staub-Volumen läuft nicht.");
        }

        [UnityTest]
        public IEnumerator Karte_hat_mehr_Detail_bekommen()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var deko = GameObject.Find("Map")?.transform.Find("Deko");
            Assert.IsNotNull(deko, "Kein Deko-Objekt.");

            int frames = 0, rubble = 0;
            foreach (Transform t in deko.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Fensterrahmen")) frames++;
                if (t.name.StartsWith("Truemmer")) rubble++;
            }
            Assert.Greater(frames, 8, "Zu wenige Fensterrahmen in den Wänden.");
            Assert.Greater(rubble, 12, "Zu wenig Trümmer auf der Karte.");
            Assert.Greater(deko.childCount, 120, "Die Karte ist noch zu leer (Deko-Kinder).");
        }

        [UnityTest]
        public IEnumerator Umgebungslicht_ist_heruntergefahren()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            for (int i = 0; i < 3; i++) yield return null;

            if (RenderSettings.ambientMode == AmbientMode.Skybox)
            {
                // Heruntergefahren (Default ist 1.0), aber NICHT so weit, dass
                // Schattenseiten schwarz absaufen (Screenshot-Test 2026-09-04).
                Assert.LessOrEqual(RenderSettings.ambientIntensity, 0.75f,
                    "Das Umgebungslicht (Skybox) ist zu hell - die Schatten verschwinden.");
                Assert.GreaterOrEqual(RenderSettings.ambientIntensity, 0.45f,
                    "Das Umgebungslicht (Skybox) ist zu dunkel - man sieht nichts im Schatten.");
            }
            else
            {
                Assert.Less(RenderSettings.ambientSkyColor.maxColorComponent, 0.25f,
                    "Das Umgebungslicht (Trilight) ist zu hell.");
                Assert.Greater(RenderSettings.ambientSkyColor.maxColorComponent, 0.08f,
                    "Das Umgebungslicht (Trilight) ist zu dunkel.");
            }
        }
    }
}
