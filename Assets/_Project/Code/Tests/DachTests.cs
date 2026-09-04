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
