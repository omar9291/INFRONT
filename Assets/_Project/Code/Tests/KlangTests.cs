using NUnit.Framework;
using UnityEngine;

namespace Infront.Tests
{
    /// <summary>
    /// Die gebauten Toene - Schritt, Einschlag, Nachladen.
    ///
    /// Hoeren kann ein Test nicht. Pruefen kann er die BEHAUPTUNGEN, die die
    /// Erzeuger aufstellen: ein Schritt hat zwei Stoesse (Ferse, dann Sohle),
    /// ein Einschlag faengt mit einem sehr kurzen harten Knall an, und ein
    /// Nachladevorgang besteht aus drei getrennten Geraeuschen. Genau das
    /// unterscheidet sie von dem einen Rauschstoss, der vorher da war.
    ///
    /// Ausserdem: nichts darf uebersteuern. Ein Ton, der an die Grenze
    /// stoesst, knackt - und das hoert man sofort.
    /// </summary>
    public sealed class KlangTests
    {
        static float[] Hole(SoundId id)
        {
            var clip = ProceduralSfx.Build(id);
            Assert.IsNotNull(clip, "Kein Clip fuer " + id);
            var daten = new float[clip.samples * clip.channels];
            clip.GetData(daten, 0);
            return daten;
        }

        /// <summary>Lautstaerke in kurzen Fenstern - die Umrisslinie des Tons.</summary>
        static float[] Huellkurve(float[] daten, int fenster)
        {
            int n = daten.Length / fenster;
            var h = new float[Mathf.Max(1, n)];
            for (int f = 0; f < h.Length; f++)
            {
                float max = 0f;
                for (int i = f * fenster; i < (f + 1) * fenster && i < daten.Length; i++)
                    max = Mathf.Max(max, Mathf.Abs(daten[i]));
                h[f] = max;
            }
            return h;
        }

        // --- Nichts uebersteuert -------------------------------------------

        [Test]
        public void Kein_Ton_uebersteuert()
        {
            foreach (SoundId id in System.Enum.GetValues(typeof(SoundId)))
            {
                var daten = Hole(id);
                float max = 0f;
                for (int i = 0; i < daten.Length; i++) max = Mathf.Max(max, Mathf.Abs(daten[i]));
                Assert.LessOrEqual(max, 1.001f,
                    $"Der Ton {id} stoesst an die Grenze ({max:F3}) - das knackt hoerbar.");
            }
        }

        [Test]
        public void Kein_Ton_ist_stumm()
        {
            foreach (SoundId id in System.Enum.GetValues(typeof(SoundId)))
            {
                var daten = Hole(id);
                float max = 0f;
                for (int i = 0; i < daten.Length; i++) max = Mathf.Max(max, Mathf.Abs(daten[i]));
                Assert.Greater(max, 0.01f, $"Der Ton {id} ist praktisch stumm.");
            }
        }

        // --- Schritt ---------------------------------------------------------

        [Test]
        public void Ein_Schritt_hat_zwei_Stoesse_statt_einem()
        {
            var daten = Hole(SoundId.SchrittNormal);
            var h = Huellkurve(daten, 441);   // 10-ms-Fenster

            Assert.Greater(h.Length, 8,
                "Der Schritt ist zu kurz fuer Ferse und Abrollen.");

            // Erster Stoss: die Ferse, ganz am Anfang.
            Assert.Greater(h[0], 0.02f, "Am Anfang muesste die Ferse aufsetzen.");

            // Zweiter Stoss: das Abrollen, rund 40 ms spaeter. Gesucht wird ein
            // Wiederanstieg NACH einem Tal - genau das gibt es bei einem
            // einzelnen Rauschstoss nicht, der faellt nur ab.
            bool wiederAnstieg = false;
            for (int i = 2; i < h.Length - 1; i++)
                if (h[i] > h[i - 1] * 1.25f && h[i] > 0.01f) { wiederAnstieg = true; break; }

            Assert.IsTrue(wiederAnstieg,
                "Nach der Ferse muesste die Sohle noch einmal abrollen. Ohne diesen "
                + "zweiten Stoss klingt ein Schritt wie ein Klopfen auf Pappe. "
                + "Huellkurve: " + string.Join(" ", System.Array.ConvertAll(h, x => x.ToString("F2"))));
        }

        [Test]
        public void Lauter_Schritt_ist_lauter_als_leiser()
        {
            float Laut(SoundId id)
            {
                var d = Hole(id);
                float m = 0f;
                for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
                return m;
            }

            Assert.Greater(Laut(SoundId.SchrittLaut), Laut(SoundId.SchrittNormal),
                "Rennen muesste lauter sein als Gehen - daran hoert man Gegner.");
            Assert.Greater(Laut(SoundId.SchrittNormal), Laut(SoundId.SchrittLeise),
                "Gehen muesste lauter sein als Schleichen.");
        }

        // --- Einschlag ---------------------------------------------------------

        [Test]
        public void Ein_Einschlag_faengt_mit_einem_harten_Knall_an()
        {
            var daten = Hole(SoundId.EinschlagWand);

            // Die ersten zwei Millisekunden gegen den Rest.
            int kurz = (int)(44100 * 0.002f);
            float vorne = 0f, spaeter = 0f;
            for (int i = 0; i < kurz && i < daten.Length; i++)
                vorne = Mathf.Max(vorne, Mathf.Abs(daten[i]));
            for (int i = kurz; i < daten.Length; i++)
                spaeter = Mathf.Max(spaeter, Mathf.Abs(daten[i]));

            Assert.Greater(vorne, spaeter * 0.9f,
                "Der Einschlag muesste vorne am lautesten sein. Ohne den kurzen harten "
                + "Knall klingt jeder Treffer weich, und man hoert nicht, ob man "
                + $"getroffen hat (vorne {vorne:F3}, spaeter {spaeter:F3}).");
        }

        // --- Nachladen ----------------------------------------------------------

        [Test]
        public void Nachladen_besteht_aus_drei_Geraeuschen()
        {
            var daten = Hole(SoundId.Nachladen);
            var h = Huellkurve(daten, 2205);   // 50-ms-Fenster

            float schwelle = 0f;
            for (int i = 0; i < h.Length; i++) schwelle = Mathf.Max(schwelle, h[i]);
            schwelle *= 0.22f;

            int stoesse = 0;
            bool drin = false;
            for (int i = 0; i < h.Length; i++)
            {
                if (!drin && h[i] > schwelle) { stoesse++; drin = true; }
                else if (drin && h[i] < schwelle * 0.6f) drin = false;
            }

            Assert.AreEqual(3, stoesse,
                "Ein Magazinwechsel hat drei Geraeusche: Halter auf, Magazin sitzt, "
                + "Verschluss vor. Daran hoert man, wie lange es noch dauert. "
                + "Gezaehlt: " + stoesse);
        }
    }
}
