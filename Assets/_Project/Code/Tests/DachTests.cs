using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Das Hallendach und was daran haengt.
    ///
    /// Der Zustand davor: die Karte war eine oben offene Kiste. Sieben Meter
    /// Wand, darueber der Himmel. Von oben sah man in einen Schacht, im Spiel
    /// ueber jede Wand hinweg ins Leere - das liess alles nach Bauklotz
    /// aussehen und nicht nach Werk.
    ///
    /// NICHT pruefbar: ob es gut aussieht. Pruefbar: dass ein Dach da ist, dass
    /// es nicht ins NavMesh geraet (sonst laufen Bots oben herum), dass der
    /// alte schwebende Rahmen weg ist und dass die Partikel eine Textur haben.
    /// </summary>
    public sealed class DachTests
    {
        [UnityTest]
        public IEnumerator Die_Halle_hat_ein_Dach()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var dach = GameObject.Find("Map/Dach");
            Assert.IsNotNull(dach,
                "Unter 'Map' muesste ein Objekt 'Dach' stehen - sonst ist die Halle "
                + "wieder oben offen.");
            Assert.Greater(dach.transform.childCount, 20,
                "Das Dach muesste aus Bindern, Pfetten, Blechfeldern und Lichtbaendern "
                + "bestehen, nicht aus einer Platte.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Das_Dach_bleibt_aus_dem_NavMesh_heraus()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var dach = GameObject.Find("Map/Dach");
            Assert.IsNotNull(dach, "Kein Dach gefunden.");

            var mod = dach.GetComponent<NavMeshModifier>();
            Assert.IsNotNull(mod,
                "Am Dach muesste ein NavMeshModifier haengen.");
            Assert.IsTrue(mod.ignoreFromBuild,
                "Das Dach muss aus dem NavMesh heraus - sonst waere die Oberseite eine "
                + "begehbare Flaeche und Bots koennten dort oben landen.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Ueber_den_Bombenplaetzen_schwebt_kein_Rahmen_mehr()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var rahmen = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                               .Where(t => t.name.StartsWith("SiteFrame_"))
                               .ToArray();
            Assert.IsEmpty(rahmen,
                "Der frei schwebende Rahmen ueber den Bombenplaetzen ist durch ein "
                + "haengendes Hallenschild ersetzt worden. Wieder aufgetaucht: "
                + string.Join(", ", rahmen.Select(t => t.name)));

            var schilder = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                                 .Where(t => t.name.StartsWith("SiteSign_")).ToArray();
            Assert.AreEqual(2, schilder.Count(t => t.name.EndsWith("_Tafel")),
                "Ueber jedem der zwei Bombenplaetze muesste genau eine Schildtafel haengen.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Die_Lichtbaender_sind_verglast_und_brennen_nicht_aus()
        {
            // Vorgeschichte: jedes Lichtband war EINE 90 m lange Leuchtplatte
            // mit Emission 1,35. Auf den Rundgangsbildern war das ein
            // reinweisses Rechteck ohne Struktur, und der Anteil ausgebrannter
            // Pixel stieg von 1,1 % auf 1,7 %. Diese Pruefung haelt beides
            // fest: das Band ist in Felder zerlegt, und kein Feld leuchtet so
            // hell, dass die Tonwertkurve keine Zeichnung mehr uebrig laesst.
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var felder = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                               .Where(t => t.name.StartsWith("Dach_Lichtband_"))
                               .Select(t => t.GetComponent<Renderer>())
                               .Where(r => r != null && r.sharedMaterial != null)
                               .ToArray();

            Assert.Greater(felder.Length, 20,
                "Die vier Lichtbaender muessten in einzelne Scheibenfelder zerlegt sein "
                + "(12 je Band). Gefunden: " + felder.Length + ". Bei 4 ist die alte "
                + "durchgehende Leuchtplatte zurueck.");

            var leucht = felder
                .Select(r => r.sharedMaterial.GetColor("_EmissionColor"))
                .Select(c => Mathf.Max(c.r, Mathf.Max(c.g, c.b)))
                .ToArray();

            // Diese Pruefung stand hier zuerst als "kein Feld heller als 1,0".
            // Das war der falsche Massstab, und die Messung hat ihn widerlegt:
            // dunkler machen hat den Ausbrand zwar beseitigt (1,7 % auf 0,3 %),
            // aber die Halle mitgerissen (Median 99,7 auf 84,4) - und weder
            // staerkere Tageslichtkegel noch mehr Indirekt-Anteil konnten das
            // aufholen (zusammen +0,5 Median).
            //
            // Papierartig aussehen liess das Band nicht seine Helligkeit,
            // sondern seine Gleichfoermigkeit. Also wird jetzt genau das
            // geprueft: Struktur, nicht Dunkelheit.
            Assert.Greater(leucht.Max() - leucht.Min(), 0.4f,
                "Alle Scheiben leuchten praktisch gleich hell (Spanne "
                + (leucht.Max() - leucht.Min()).ToString("0.00") + "). Echte "
                + "Werkverglasung ist ungleichmaessig verschmutzt; ohne diese Spanne "
                + "liest das Auge die Flaeche als Fehler statt als Glas.");

            int schmutzig = leucht.Count(v => v < leucht.Max() * 0.75f);
            Assert.GreaterOrEqual(schmutzig, felder.Length / 10,
                "Nur " + schmutzig + " von " + felder.Length + " Scheiben sind deutlich "
                + "dunkler als die hellsten. Ohne verschmutzte Felder hat das Band "
                + "keinen Rhythmus und wird wieder zur gleichmaessigen Flaeche.");

            Assert.Less(leucht.Max(), 1.6f,
                "Das hellste Scheibenfeld leuchtet mit " + leucht.Max().ToString("0.00")
                + ". So weit oben verschwindet auch der Unterschied zwischen sauberen "
                + "und verschmutzten Scheiben im Ausbrand.");

            // Die Sprossen muessen auf den Feldgrenzen sitzen, sonst treffen
            // sich Scheibenkante und Sprosse nirgends und es sieht von unten
            // aus wie ein aufgemalter Strich.
            int sprossen = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                                 .Count(t => t.name.StartsWith("Dach_Sprosse_"));
            Assert.GreaterOrEqual(sprossen, 4 * 13,
                "Je Band muessten 13 Sprossen die 12 Felder trennen. Gefunden: " + sprossen);

            int rahmen = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                               .Count(t => t.name.StartsWith("Dach_Bandrahmen_"));
            Assert.AreEqual(8, rahmen,
                "Jedes der vier Baender braucht an beiden Laengskanten ein Rahmenprofil - "
                + "ohne stoesst die leuchtende Scheibe ohne Uebergang ans Blech.");

            yield return MatchTestHarness.Teardown();
        }

        // --- Partikel -------------------------------------------------------

        [Test]
        public void Die_weiche_Partikelwolke_ist_wirklich_rund()
        {
            // Die Textur wird gemerkt und von den Partikelsystemen mitbenutzt.
            // Deshalb nicht auf eine Wunschgroesse bestehen, sondern die
            // tatsaechliche nehmen - sonst misst man an der falschen Stelle.
            var t = SoftParticleTexture.Weich();
            Assert.IsNotNull(t);
            int n = t.width;
            int m = n / 2;

            float mitte = t.GetPixel(m, m).a;
            float rand = t.GetPixel(0, 0).a;
            float kante = t.GetPixel(m, 0).a;

            Assert.Greater(mitte, 0.9f, "In der Mitte muesste die Wolke voll deckend sein.");
            Assert.Less(rand, 0.02f,
                "In der Ecke muesste sie durchsichtig sein - sonst sieht man die Ecke "
                + "des Vierecks, und genau das waren die grossen Dreiecke im Bild.");
            Assert.Less(kante, 0.05f, "Auch an der Seitenmitte muesste sie ausgelaufen sein.");
        }

        [UnityTest]
        public IEnumerator Nebel_und_Staub_laufen_nicht_ohne_Textur()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            foreach (var fog in Object.FindObjectsByType<GroundFog>(FindObjectsSortMode.None))
            {
                var r = fog.GetComponent<ParticleSystemRenderer>();
                Assert.IsNotNull(r.material.mainTexture,
                    "Der Bodennebel laeuft ohne Textur - dann ist jedes Partikel ein "
                    + "blankes Viereck und man sieht Kanten und Diagonale.");
            }

            foreach (var dust in Object.FindObjectsByType<AtmosphereDust>(FindObjectsSortMode.None))
            {
                var r = dust.GetComponent<ParticleSystemRenderer>();
                Assert.IsNotNull(r.material.mainTexture,
                    "Der Staub laeuft ohne Textur.");
            }

            yield return MatchTestHarness.Teardown();
        }
    }
}
