using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die grossen Aussenwaende sind keine leeren Flaechen mehr.
    ///
    /// Gemessen am 2026-09-05: auf den Gangwaenden lag die Helligkeits-
    /// Streuung bei 8,8 bis 9,8 - vom Boden bis zur Decke eine einzige
    /// gleichmaessige Flaeche. Eine Werkhalle besteht aus Fertigteilen, und
    /// man sieht jede Fuge.
    ///
    /// NICHT pruefbar: ob es gut aussieht. Pruefbar: dass die Teile da sind,
    /// dass sie an allen vier Waenden sitzen und dass sie kein Hindernis
    /// bilden - Deko darf Balance und NavMesh nicht anfassen.
    /// </summary>
    public sealed class WandDetailTests
    {
        [UnityTest]
        public IEnumerator Alle_vier_Aussenwaende_haben_Sockel_und_Fugen()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var alle = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

            foreach (string seite in new[] { "W", "O", "S", "N" })
            {
                Assert.IsTrue(alle.Any(t => t.name == "Sockel_" + seite),
                    "Der Wandsockel fehlt an der Seite " + seite + ". Ohne ihn stoesst "
                    + "die Wandflaeche ohne Uebergang auf den Boden.");

                int fugen = alle.Count(t => t.name.StartsWith("Fuge_" + seite + "_"));
                Assert.GreaterOrEqual(fugen, 10,
                    "An der Wand " + seite + " gibt es nur " + fugen + " Plattenfugen. "
                    + "Erwartet werden zwei waagerechte und rund elf senkrechte.");
            }

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Das_Wanddetail_ist_reine_Deko_ohne_Collider()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var mitCollider = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Sockel_") || t.name.StartsWith("Fuge_"))
                .Where(t => t.GetComponent<Collider>() != null)
                .Select(t => t.name)
                .ToArray();

            Assert.IsEmpty(mitCollider,
                "Diese Deko-Teile haben einen Collider und wuerden damit Balance und "
                + "NavMesh veraendern: " + string.Join(", ", mitCollider)
                + ". Genau so sind bei den Bombenplatz-Markierungen 5-cm-Stufen "
                + "entstanden, die dem NavMesh Loecher gerissen haben.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Hallenwaende_haben_Rippen_und_Kabeltrassen_ohne_neue_Hindernisse()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var alle = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Hallen"))
                .ToArray();
            Assert.GreaterOrEqual(alle.Count(t => t.name.StartsWith("HallenRippe_")), 40,
                "Jede der acht sichtbaren Hallenwandseiten braucht fünf senkrechte Rippen.");
            Assert.AreEqual(8, alle.Count(t => t.name.StartsWith("HallenKabelOben_")),
                "Jede Hallenwandseite braucht eine obere Kabeltrasse.");
            Assert.AreEqual(8, alle.Count(t => t.name.StartsWith("HallenSockel_")),
                "Die Trennwände brauchen wie die Außenwände einen sichtbaren Sockel.");

            var mitCollider = alle.Where(t => t.GetComponent<Collider>() != null)
                                 .Select(t => t.name).ToArray();
            Assert.IsEmpty(mitCollider,
                "Hallenwand-Detail darf Wege und NavMesh nicht verändern: " + string.Join(", ", mitCollider));
            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Die_Eckmasten_stehen_am_Boden_und_haben_einen_Kopf()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var alle = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            foreach (string id in new[] { "WN", "ON", "WS", "OS" })
            {
                var rohr = alle.FirstOrDefault(r => r.name == "Mast_" + id);
                Assert.IsNotNull(rohr,
                    "In der Ecke " + id + " fehlt der Mast. Vorher waren es zwei nackte "
                    + "schwarze Stangen in nur zwei Ecken, die oben im Nichts endeten.");

                Assert.Less(rohr.bounds.min.y, 0.3f,
                    "Der Mast " + id + " berührt den Boden nicht (Unterkante "
                    + rohr.bounds.min.y.ToString("0.00") + " m) - genau das ließ die "
                    + "alten Stangen schweben.");
                Assert.Greater(rohr.bounds.max.y, 11.5f,
                    "Der Mast " + id + " endet bei " + rohr.bounds.max.y.ToString("0.00")
                    + " m, deutlich unter dem Binder - er wirkt abgeschnitten.");

                Assert.IsTrue(alle.Any(r => r.name == "Mast_" + id + "_Kopf"),
                    "Dem Mast " + id + " fehlt der Strahlerkopf. Ohne ihn ist es wieder "
                    + "nur eine Stange, die im Nichts endet.");
                Assert.IsTrue(alle.Any(r => r.name == "Mast_" + id + "_Fuss"),
                    "Dem Mast " + id + " fehlt die Fussplatte am Boden.");
            }

            var mitCollider = alle.Where(r => r.name.StartsWith("Mast_"))
                                  .Where(r => r.GetComponent<Collider>() != null)
                                  .Select(r => r.name).ToArray();
            Assert.IsEmpty(mitCollider,
                "Die Eckmasten haben Collider und würden Wege/NavMesh verändern: "
                + string.Join(", ", mitCollider));

            yield return MatchTestHarness.Teardown();
        }
    }
}
