using System.Collections;
using System.Linq;
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
                // Die Grenzen galten fuer die oben offene Karte: da schien die
                // Sonne direkt herein, und viel Umgebungslicht haette die
                // Schatten weggewaschen. Seit die Halle ein Dach hat, kommt
                // kein direktes Sonnenlicht mehr an - das Umgebungslicht
                // TRAEGT jetzt den Innenraum. Bei 0,62 waren ganze Waende
                // schwarz.
                //
                // Am 2026-09-04 noch einmal angehoben, diesmal gemessen statt
                // geschaetzt. Ein Rundgang mit 27 festen Kamerastandorten,
                // Wetter fest auf "klar", liefert die Helligkeitsverteilung:
                //
                //   bei 1,10   Mittelwert 48/255, 26,9 % des Bildes schwarz,
                //              50,9 % unter der Erkennbarkeitsgrenze
                //   bei 1,65   Mittelwert 88/255,  12,5 % schwarz,
                //              24,7 % unter der Grenze, 0,5 % ausgebrannt
                //
                // Ganze Wandseiten, die von der Sonne wegzeigten, hatten vorher
                // gar kein Licht: im Suedgang war die eine Wand hell und die
                // gegenueberliegende schwarz. Im Westgang war nicht zu spielen.
                //
                // Die Obergrenze bleibt und bleibt wichtig: wird sie
                // ueberschritten, ist der Raum flach ausgeleuchtet und nichts
                // wirft mehr sichtbare Schatten. Auf den Bildern bei 1,65 sind
                // Lichtflecken und Schattenseiten weiterhin deutlich zu sehen.
                Assert.LessOrEqual(RenderSettings.ambientIntensity, 2.2f,
                    "Das Umgebungslicht (Skybox) ist zu hell - alles wird flach.");
                Assert.GreaterOrEqual(RenderSettings.ambientIntensity, 1.3f,
                    "Das Umgebungslicht (Skybox) ist zu dunkel - in einer gedeckelten "
                    + "Halle saufen dann die Waende ab. Gemessen: bei 1,10 waren "
                    + "26,9 % jedes Bildes praktisch schwarz.");
            }
            else
            {
                Assert.Less(RenderSettings.ambientSkyColor.maxColorComponent, 0.25f,
                    "Das Umgebungslicht (Trilight) ist zu hell.");
                Assert.Greater(RenderSettings.ambientSkyColor.maxColorComponent, 0.08f,
                    "Das Umgebungslicht (Trilight) ist zu dunkel.");
            }
        }

        [UnityTest]
        public IEnumerator Die_Aussengassen_sind_beleuchtet()
        {
            // Gefunden am 2026-09-05 auf den Rundgangsbildern w0_22 und w0_23:
            // das untere Drittel beider Aussenwaende war pechschwarz, gemessen
            // RGB (7,8,10). Ursache war kein dunkles Material, sondern ein
            // Loch in der Beleuchtung: die Lichtbaender im Dach liegen bei
            // x = +-30 und +-10, die Gassen bei x = +-36 bis +-45. Darueber
            // ist geschlossenes Blech.
            //
            // Das ist auch ein Spielproblem - vor einer schwarzen Wand ist ein
            // Gegner in dunkler Ausruestung unsichtbar. Deshalb eine Pruefung
            // und nicht nur ein Kommentar.
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            foreach (int seite in new[] { -1, 1 })
            {
                float gx = 42.5f * seite;
                var inDerGasse = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Where(l => l.isActiveAndEnabled
                                && Mathf.Abs(l.transform.position.x - gx) < 6f)
                    .ToArray();

                Assert.GreaterOrEqual(inDerGasse.Length, 4,
                    "In der Aussengasse bei x = " + gx + " brennen nur "
                    + inDerGasse.Length + " Lichter. Die Gasse ist 90 m lang; mit "
                    + "weniger bleibt das untere Wanddrittel schwarz.");

                // Ueber die Laenge verteilt, nicht alle an einer Stelle.
                float vorn = inDerGasse.Min(l => l.transform.position.z);
                float hinten = inDerGasse.Max(l => l.transform.position.z);
                Assert.Greater(hinten - vorn, 50f,
                    "Die Lichter der Gasse bei x = " + gx + " stehen alle auf "
                    + (hinten - vorn).ToString("0") + " m zusammen. Dann ist der Rest "
                    + "der 90 m langen Gasse weiter unbeleuchtet.");
            }

            yield return MatchTestHarness.Teardown();
        }

    }
}
