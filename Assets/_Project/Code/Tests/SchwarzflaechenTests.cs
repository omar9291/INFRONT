using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Keine grosse, flache, fast schwarze Flaeche in der Karte.
    ///
    /// Vorgeschichte (2026-09-05): auf den Rundgangsbildern w0_22 und w0_23 lag
    /// ueber die halbe Bildbreite eine pechschwarze Flaeche, gemessen
    /// RGB (7,8,10). Es war die Balkon-Bruestung "BalcRail_out_L": ein
    /// untexturierter Quader von 12 x 1,1 m in der Farbe (0.09, 0.10, 0.12),
    /// und die Rundgang-Kamera steht fuenf Meter davor.
    ///
    /// Das ist nicht nur haesslich, es ist ein Spielproblem: vor so einer
    /// Flaeche ist ein Gegner in dunkler Ausruestung unsichtbar.
    ///
    /// Wichtig ist hier die GROESSE. Dunkle Kleinteile sind richtig - Sprossen,
    /// Binder, Kabel, Rahmenprofile leben davon. Erst ab mehreren
    /// Quadratmetern wird aus "dunkles Bauteil" eine schwarze Wand.
    /// </summary>
    public sealed class SchwarzflaechenTests
    {
        /// <summary>Ab dieser sichtbaren Flaeche (groesste Seite des Quaders,
        /// in Quadratmetern) zaehlt ein Bauteil als Flaeche und nicht mehr als
        /// Detail.</summary>
        const float GrossAb = 8f;

        /// <summary>Darunter ist eine Flaeche im Spiel praktisch schwarz.
        /// (0.09, 0.10, 0.12) - der alte Wert der Bruestung - ergibt 0,10.</summary>
        const float ZuDunkelUnter = 0.15f;

        [UnityTest]
        public IEnumerator Keine_grosse_fast_schwarze_Flaeche_in_der_Karte()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var karte = GameObject.Find("Map");
            Assert.IsNotNull(karte, "Keine Karte gefunden.");

            var suender = karte.GetComponentsInChildren<Renderer>(false)
                .Where(r => r.sharedMaterial != null)
                .Select(r => new
                {
                    R = r,
                    Flaeche = GroessteSeite(r.bounds.size),
                    Helligkeit = Helligkeit(r.sharedMaterial)
                })
                .Where(e => e.Flaeche >= GrossAb && e.Helligkeit < ZuDunkelUnter)
                .OrderBy(e => e.Helligkeit)
                .ToArray();

            Assert.IsEmpty(suender,
                "Diese Bauteile sind gross UND fast schwarz - im Bild werden daraus "
                + "Loecher, und ein Gegner davor ist unsichtbar:\n"
                + string.Join("\n", suender.Select(e =>
                    $"  {Pfad(e.R.transform)}  {e.Flaeche:0.0} m^2  "
                    + $"Helligkeit {e.Helligkeit:0.00}  (Material {e.R.sharedMaterial.name})")));

            yield return MatchTestHarness.Teardown();
        }

        static float GroessteSeite(Vector3 s) =>
            Mathf.Max(s.x * s.y, Mathf.Max(s.x * s.z, s.y * s.z));

        /// <summary>Wahrgenommene Helligkeit der Grundfarbe. Texturierte
        /// Materialien haben meist eine weisse Grundfarbe und fallen damit
        /// richtigerweise nicht auf - ihre Helligkeit steckt in der Textur.</summary>
        static float Helligkeit(Material m)
        {
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        static string Pfad(Transform t)
        {
            string p = t.name;
            for (var q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
            return p;
        }
    }
}
