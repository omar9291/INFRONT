using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Nichts Aufgemaltes und kein Gelaender darf ueber seiner Flaeche schweben.
    ///
    /// Gemeldet wurde es an den Bombenplaetzen A und B, und beim Nachmessen war
    /// es schlimmer als vermutet: der Rahmen und die Buchstaben hingen einen
    /// GANZEN METER in der Luft und schnitten quer durch die Kisten. Grund war
    /// eine feste Hoehe von 1,28, die aus der kleinen Karte stammt - dort liegt
    /// unter dem Platz ein Podest, in der grossen Karte nicht.
    ///
    /// Dieser Test hat in seiner ersten Fassung genau denselben Fehler gemacht:
    /// er verglich gegen eine von Hand eingetragene Tabelle mit Sollhoehen, und
    /// in der Tabelle stand dieselbe falsche Annahme. Er war gruen, waehrend das
    /// Band in der Luft hing. Deshalb sucht er die Flaeche jetzt selbst - per
    /// Strahl nach unten. Eine Pruefung darf nicht dieselbe Quelle benutzen wie
    /// das, was sie prueft.
    /// </summary>
    public sealed class SchwebeTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        /// <summary>Teile, die auf etwas aufliegen muessen. Bei Gelaendern nur
        /// die Pfosten (_p) - die Holme gehoeren nach oben.</summary>
        static readonly string[] Liegt =
        {
            "SiteA_Bar", "SiteB_Bar",       // aufgemalte Buchstaben A und B
            "_Mark",                        // Rahmen um den Bombenplatz
            "MidEdge", "BalcEdge",          // Kantenleisten
            "MidRail_A_p", "MidRail_B_p",   // Gelaenderpfosten Mittelpodest
            "BalcRailDeko_1_p", "BalcRailDeko_-1_p",
        };

        /// <summary>Hoehe der Flaeche unter einem Teil - per Strahl gesucht,
        /// nicht aus einer Tabelle. Das Teil selbst wird uebersprungen.</summary>
        static bool FlaecheDarunter(Renderer r, out float y)
        {
            y = 0f;
            Bounds b = r.bounds;
            Vector3 von = new Vector3(b.center.x, b.max.y + 3f, b.center.z);
            var treffer = Physics.RaycastAll(von, Vector3.down, 20f, ~0,
                                             QueryTriggerInteraction.Ignore);
            float beste = float.MinValue;
            bool da = false;
            foreach (var t in treffer)
            {
                if (t.collider.gameObject == r.gameObject) continue;
                // nur, was wirklich UNTER dem Teil liegt
                if (t.point.y > b.min.y + 0.02f) continue;
                if (t.point.y > beste) { beste = t.point.y; da = true; }
            }
            y = beste;
            return da;
        }

        [UnityTest]
        public IEnumerator Aufgemaltes_und_Gelaender_stehen_auf_ihrer_Flaeche()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var karte = GameObject.Find("Map");
            Assert.IsNotNull(karte, "Keine Karte da.");

            int geprueft = 0;
            float schlimmste = 0f;
            string schlimmsterName = null;

            foreach (var r in karte.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                bool gemeint = false;
                foreach (var teil in Liegt)
                    if (n.Contains(teil)) { gemeint = true; break; }
                if (!gemeint) continue;

                if (!FlaecheDarunter(r, out float flaeche)) continue;

                float luft = r.bounds.min.y - flaeche;
                if (luft > schlimmste) { schlimmste = luft; schlimmsterName = n; }
                geprueft++;
            }

            Assert.Greater(geprueft, 20,
                $"Nur {geprueft} Teile gefunden - die Namen haben sich geaendert, "
                + "der Test prueft dann nichts mehr.");

            Assert.LessOrEqual(schlimmste, 0.05f,
                $"{schlimmsterName} schwebt {schlimmste:F2} m ueber der Flaeche darunter.");
        }
    }
}
