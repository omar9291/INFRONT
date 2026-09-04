using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Spiegelung und Glanz - die zwei Sachen, die zusammengehoeren und lange
    /// beide gefehlt haben.
    ///
    /// Vorher: die Materialien trugen einen FESTEN Glanzwert (0,32) vom Asphalt
    /// bis zum Stahlblech, obwohl in jedem heruntergeladenen Satz eine
    /// Rauheits-Karte lag. Und es gab KEINE einzige Reflexionssonde, waehrend
    /// 870 Renderer auf "Sonde benutzen" standen - also spiegelte jede
    /// glaenzende Flaeche in der geschlossenen Halle den Aussenhimmel.
    ///
    /// Beides einzeln bringt wenig: eine Glanzkarte ohne etwas zum Spiegeln
    /// ist nur ein heller Fleck, und eine Sonde ohne Glanzunterschiede sieht
    /// niemand. Deshalb stehen beide Pruefungen in einer Datei.
    /// </summary>
    public sealed class SpiegelungTests
    {
        [UnityTest]
        public IEnumerator Die_Halle_hat_Reflexionssonden()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var sonden = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(sonden.Length, 4,
                "Ohne Sonden faellt jede Spiegelung auf den Himmel zurueck - in einer "
                + "Halle mit Dach ist das sichtbar falsch.");

            foreach (var s in sonden)
            {
                Assert.IsTrue(s.boxProjection,
                    $"{s.name}: ohne Kasten-Projektion kommt die Spiegelung aus dem "
                    + "Unendlichen statt von der Raumkante.");
                Assert.AreEqual(0, s.cullingMask & (1 << 7),
                    $"{s.name}: Schicht 7 (Figuren) gehoert nicht in eine Spiegelung, "
                    + "sonst brennen sich zufaellig herumstehende Bots als Geister ein.");
            }

            yield return MatchTestHarness.Teardown();
        }

        [Test]
        public void Die_Oberflaechen_haben_eine_echte_Glanzkarte()
        {
            string[] schluessel = { "wand_beton", "boden", "platte", "platz", "deckung_metall" };
            foreach (string k in schluessel)
            {
                var m = Resources.Load<Material>("Materials/" + k);
                Assert.IsNotNull(m, $"Material '{k}' fehlt.");

                Assert.IsNotNull(m.GetTexture("_MetallicGlossMap"),
                    $"'{k}' hat keine Glanzkarte - dann steht der Glanz wieder starr "
                    + "auf einem Wert und jede Flaeche antwortet gleich auf Licht.");
                Assert.IsTrue(m.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                    $"'{k}': Karte zugewiesen, aber das Schluesselwort fehlt - dann "
                    + "liest der Shader sie gar nicht.");

                // _Smoothness ist mit Karte nur noch ein Multiplikator auf das
                // Alpha. Steht da der alte 0,32, ist die Karte auf ein Drittel
                // heruntergedreht und alles stumpfer als vorher.
                Assert.AreEqual(1f, m.GetFloat("_Smoothness"), 0.001f,
                    $"'{k}': _Smoothness muss bei gesetzter Karte 1 sein.");
            }
        }
    }
}
