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
    }
}
