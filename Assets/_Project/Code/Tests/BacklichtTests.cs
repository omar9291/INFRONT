using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Liegt in der Arena gebackenes Licht?
    ///
    /// Warum das eine eigene Pruefung braucht: SceneBuilder.Build legt die
    /// Szene jedes Mal NEU an und wirft dabei jede gebackene Lichtkarte weg.
    /// Wer danach nur die App baut, bekommt eine Karte ohne indirektes Licht -
    /// ohne Fehlermeldung, ohne Absturz. Sie sieht einfach wieder flach aus,
    /// und das faellt erst auf einem Bild auf.
    ///
    /// Der richtige Weg heisst deshalb SceneBuilder.BuildUndBacke.
    /// </summary>
    public sealed class BacklichtTests
    {
        [UnityTest]
        public IEnumerator Die_Arena_hat_gebackenes_Licht()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var karten = LightmapSettings.lightmaps;
            Assert.IsNotNull(karten, "LightmapSettings.lightmaps ist null.");
            Assert.Greater(karten.Length, 0,
                "In der Arena liegt keine Lichtkarte. Vermutlich wurde die Szene neu "
                + "gebaut, ohne danach zu backen - benutze SceneBuilder.BuildUndBacke. "
                + "Ohne Backvorgang fehlt der indirekte Anteil und die Halle sieht "
                + "wieder wie eine Graukiste aus.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Die_Karte_ist_als_statisch_markiert()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var karte = GameObject.Find("Map");
            Assert.IsNotNull(karte, "Kein Objekt 'Map' gefunden.");

            int mitKarte = 0, gesamt = 0;
            foreach (var r in karte.GetComponentsInChildren<MeshRenderer>(true))
            {
                gesamt++;
                // 65535 heisst "keine Lichtkarte zugewiesen".
                if (r.lightmapIndex >= 0 && r.lightmapIndex < 65535) mitKarte++;
            }

            Assert.Greater(gesamt, 100, "Die Karte hat kaum Renderer - stimmt der Aufbau?");
            Assert.Greater(mitKarte, gesamt / 2,
                $"Nur {mitKarte} von {gesamt} Flaechen haben eine Lichtkarte. Ohne das "
                + "Kennzeichen 'statisch' nimmt der Backvorgang ein Objekt gar nicht wahr.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Boden_Schmutzflecken_sind_weiche_Deko_ohne_eigene_Lichtkarte()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var flecken = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.name == "Fleck")
                .ToArray();
            Assert.Greater(flecken.Length, 10,
                "Kaum Boden-Schmutzflecken gefunden - stimmt BuildDecorationWerk noch?");

            foreach (var f in flecken)
            {
                // Frueher bekamen die flachen Quads eine eigene Lichtkarte plus
                // AO-Naht ringsum und standen als hartes Rechteck im Boden.
                Assert.IsTrue(f.lightmapIndex < 0 || f.lightmapIndex >= 65535,
                    "Ein Fleck hat wieder eine eigene Lichtkarte (Index "
                    + f.lightmapIndex + ") - das gibt den harten Rand zurueck.");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, f.shadowCastingMode,
                    "Ein Fleck wirft Schatten - der 2-cm-Spalt malt einen Rahmen auf den Boden.");
                Assert.IsFalse(f.receiveShadows,
                    "Ein Fleck empfaengt Schatten - die Schattenkarte rastert die Kante.");
                Assert.IsNull(f.GetComponent<Collider>(),
                    "Ein Fleck hat einen Collider und wuerde Wege/NavMesh anfassen.");
            }

            yield return MatchTestHarness.Teardown();
        }
    }
}
