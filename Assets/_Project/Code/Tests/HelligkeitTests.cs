using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Wie hell die Karte ueberhaupt sein kann.
    ///
    /// Der Zustand davor, gemessen an 25 Rundgang-Bildern: der Bildmittelwert
    /// lag bei 48 von 255, 27 % jedes Bildes waren praktisch schwarz und 51 %
    /// unter der Schwelle, ab der man noch etwas erkennt. Im Westgang stand
    /// eine schwarze Wand neben einem schwarzen Bodenstueck - dort war nicht zu
    /// spielen.
    ///
    /// Eine Ursache war kein Lichtfehler, sondern die Farbwahl: quer durch die
    /// Karte standen Grundfarben zwischen 0,05 und 0,12 Helligkeit. So dunkel
    /// ist in der Wirklichkeit fast nichts. Frischer Asphalt liegt bei etwa
    /// 0,08, alter Beton bei 0,35 bis 0,55, dunkel lackierter Stahl bei 0,15
    /// bis 0,25. Unter 0,10 kommen nur Russ, Kohle und Gummi. Eine Halle aus
    /// solchen Farben kann nicht echt aussehen, egal wie viel Licht man
    /// hineinstellt - die Flaechen werfen einfach nichts zurueck.
    ///
    /// NICHT pruefbar: ob es schoen aussieht. Pruefbar: dass keine Grundfarbe
    /// mehr unter dem physikalisch sinnvollen Wert liegt.
    /// </summary>
    public sealed class HelligkeitTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        const float Mindest = 0.13f;

        static float Helligkeit(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [UnityTest]
        public IEnumerator Keine_Flaeche_ist_dunkler_als_es_etwas_Echtes_sein_kann()
        {
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var zuDunkel = new List<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                // Nur die einfarbigen Karten-Materialien. Texturierte Flaechen
                // holen ihre Helligkeit aus der Textur, und leuchtende Dinge
                // (GlowMat) duerfen dunkel getoent sein.
                if (!m.name.StartsWith("MapMat")) continue;

                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                float h = Helligkeit(c);
                if (h < Mindest - 0.005f)
                    zuDunkel.Add($"{r.name} = {h:F3}");
            }

            Assert.IsEmpty(zuDunkel,
                $"Diese Flaechen sind dunkler als {Mindest:F2} und damit dunkler als "
                + "alles, was es wirklich gibt. Im Bild werden daraus schwarze Loecher. "
                + "Die Untergrenze sitzt in SceneBuilder.Echt(). Betroffen: "
                + string.Join(", ", zuDunkel));
        }

        // Die Hoehe des Umgebungslichts pruefen die BeleuchtungTests
        // (Umgebungslicht_ist_heruntergefahren). Zwei Tests mit eigenen Grenzen
        // auf denselben Wert widersprechen sich frueher oder spaeter - genau das
        // ist am 2026-09-04 passiert.

        [UnityTest]
        public IEnumerator Namensschilder_stuerzen_ohne_Hauptkamera_nicht_ab()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            // Zwischen Tod und Wiedereinstieg gibt es kurz keine Hauptkamera.
            // FriendlyNameplates hatte sich den Transform gemerkt und trotzdem
            // Camera.main benutzt: 2838 NullReferenceExceptions in einem
            // einzigen Durchlauf, eine pro Bild. Wirft es wieder, laesst das
            // Testgeruest diesen Test durchfallen.
            var cam = Camera.main;
            Assert.IsNotNull(cam, "Ohne Hauptkamera koennen wir das nicht pruefen.");

            cam.enabled = false;
            for (int i = 0; i < 12; i++) yield return null;
            cam.enabled = true;
            for (int i = 0; i < 3; i++) yield return null;
        }
    }
}
