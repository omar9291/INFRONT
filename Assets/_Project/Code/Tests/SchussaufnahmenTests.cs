using NUnit.Framework;
using UnityEngine;

namespace Infront.Tests
{
    /// <summary>
    /// Die vier Schussgeraeusche sind seit 2026-09-04 echte Aufnahmen
    /// (CC0, "The Free Firearm Sound Library") statt prozedural erzeugter
    /// Platzhalter.
    ///
    /// NICHT pruefbar: ob sie gut klingen. Geprueft wird, dass sie ueberhaupt
    /// da sind, dass jede Waffe ihren eigenen Ton hat und dass die Laengen zur
    /// Feuerrate passen - ein Ton, der laenger ist als der Schussabstand,
    /// stapelt sich sonst uebereinander.
    /// </summary>
    public sealed class SchussaufnahmenTests
    {
        static readonly string[] Dateien =
        {
            "schuss_gewehr", "schuss_mp", "schuss_sniper", "schuss_pistole",
        };

        [Test]
        public void Alle_vier_Schuesse_sind_echte_Dateien()
        {
            foreach (var name in Dateien)
            {
                var clip = Resources.Load<AudioClip>(name);
                Assert.IsNotNull(clip,
                    $"{name}.wav fehlt - das Spiel faellt auf den Synthesizer-Platzhalter zurueck.");
                Assert.Greater(clip.length, 0.05f, $"{name} ist verdaechtig kurz.");
            }
        }

        [Test]
        public void Jede_Waffe_hat_ihren_eigenen_Ton()
        {
            // Die alten Platzhalter waren alle vier identisch (gleiche Groesse,
            // gleiche Laenge). Genau das soll nicht wieder passieren.
            var laengen = new System.Collections.Generic.List<float>();
            foreach (var name in Dateien)
            {
                var clip = Resources.Load<AudioClip>(name);
                if (clip != null) laengen.Add(clip.length);
            }

            Assert.AreEqual(4, laengen.Count, "Es fehlen Schussdateien.");

            bool alleGleich = true;
            for (int i = 1; i < laengen.Count; i++)
                if (Mathf.Abs(laengen[i] - laengen[0]) > 0.01f) alleGleich = false;

            Assert.IsFalse(alleGleich,
                "Alle vier Schuesse sind exakt gleich lang - das sind wieder die Platzhalter.");
        }

        [Test]
        public void Die_Maschinenpistole_stapelt_sich_nicht()
        {
            var mp = Resources.Load<AudioClip>("schuss_mp");
            if (mp == null) Assert.Ignore("schuss_mp fehlt.");

            // Die MP schiesst 14 mal pro Sekunde, also alle 0.071 s. Der Ton
            // darf trotzdem nicht beliebig lang sein, sonst liegen staendig
            // ein Dutzend Kopien uebereinander und es wird zu Matsch.
            Assert.Less(mp.length, 0.35f,
                $"Der MP-Schuss ist mit {mp.length:F2} s zu lang fuer 14 Schuss pro Sekunde.");
        }

        [Test]
        public void Das_Scharfschuetzengewehr_darf_am_laengsten_nachhallen()
        {
            var sniper = Resources.Load<AudioClip>("schuss_sniper");
            var mp = Resources.Load<AudioClip>("schuss_mp");
            if (sniper == null || mp == null) Assert.Ignore("Dateien fehlen.");

            Assert.Greater(sniper.length, mp.length,
                "Ein Scharfschuetzengewehr sollte laenger nachhallen als eine Maschinenpistole.");
        }
    }
}
