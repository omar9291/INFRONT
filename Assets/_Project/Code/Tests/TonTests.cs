using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Realismus-Etappe Schritt 7: Ton.
    ///
    /// NICHT pruefbar: ob es gut klingt. Die Schussgeraeusche selbst bleiben
    /// prozedural erzeugt und damit der schwaechste Teil des Spiels - dagegen
    /// hilft nur eine echte Aufnahme.
    ///
    /// Geprueft wird, was messbar ist:
    ///  - Ferne Toene werden dumpfer (Grenzfrequenz faellt mit der Entfernung).
    ///  - Eine nahe Explosion daempft alles andere, eine ferne nicht.
    ///  - Die Daempfung klingt wieder ab.
    ///  - Schritte richten sich nach dem neuen Tempo aus Schritt 2.
    ///  - Der Untergrund faerbt den Schritt.
    /// </summary>
    public sealed class TonTests
    {
        [Test]
        public void Ferne_Toene_werden_dumpfer()
        {
            float nah = AudioService.CutoffFuerEntfernung(5f);
            float mittel = AudioService.CutoffFuerEntfernung(35f);
            float weit = AudioService.CutoffFuerEntfernung(85f);

            Assert.Greater(nah, mittel, "Auf 35 m muesste es dumpfer sein als auf 5 m.");
            Assert.Greater(mittel, weit, "Auf 85 m muesste es dumpfer sein als auf 35 m.");
            Assert.Less(weit, 2000f,
                "Ganz weit weg sollte nur noch ein Grollen uebrig bleiben.");
        }

        [UnityTest]
        public IEnumerator Nahe_Explosion_daempft_alles_andere()
        {
            var go = new GameObject("OhrTest");
            go.AddComponent<AudioListener>().enabled = false;
            var ohr = go.AddComponent<EarRinging>();
            yield return null;

            Assert.AreEqual(0f, ohr.Level01, 0.001f, "Ohne Explosion darf nichts gedaempft sein.");

            ohr.Explosion(go.transform.position + Vector3.right * 1f);
            yield return null;
            Assert.Greater(ohr.Level01, 0.3f,
                "Eine Explosion direkt daneben muesste die Ohren deutlich zusetzen.");

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator Ferne_Explosion_laesst_die_Ohren_in_Ruhe()
        {
            var go = new GameObject("OhrTest2");
            go.AddComponent<AudioListener>().enabled = false;
            var ohr = go.AddComponent<EarRinging>();
            yield return null;

            ohr.Explosion(go.transform.position + Vector3.right * (ohr.MaxEntfernungForTests + 5f));
            yield return null;

            Assert.AreEqual(0f, ohr.Level01, 0.001f,
                "Eine Explosion ausser Reichweite darf die Ohren nicht betreffen.");

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator Die_Daempfung_klingt_wieder_ab()
        {
            var go = new GameObject("OhrTest3");
            go.AddComponent<AudioListener>().enabled = false;
            var ohr = go.AddComponent<EarRinging>();
            yield return null;

            ohr.SetLevelForTests(1f);
            yield return null;
            float start = ohr.Level01;

            float gewartet = 0f;
            while (gewartet < 2f) { yield return null; gewartet += Time.deltaTime; }

            Assert.Less(ohr.Level01, start, "Die Daempfung klingt nicht ab.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Schritte_passen_zu_den_neuen_Tempi()
        {
            // Sprint ist seit Schritt 2 nur noch 7.2 m/s. Waere die Schwelle
            // fuer laute Schritte hoeher, waere Sprinten lautlos - genau das
            // Gegenteil der Absicht.
            Assert.AreEqual(SoundId.SchrittLaut, FootstepSounds.TierFor(7.2f),
                "Sprinten (7.2 m/s) muesste laute Schritte machen.");
            Assert.AreEqual(SoundId.SchrittNormal, FootstepSounds.TierFor(4.6f),
                "Gehen (4.6 m/s) muesste normale Schritte machen.");
            Assert.AreEqual(SoundId.SchrittLeise, FootstepSounds.TierFor(1.9f),
                "Ducken (1.9 m/s) muesste leise Schritte machen.");
        }

        [Test]
        public void Der_Untergrund_faerbt_den_Schritt()
        {
            float metall = FootstepSounds.LautstaerkeFaktor(FootstepSounds.Untergrund.Metall);
            float beton = FootstepSounds.LautstaerkeFaktor(FootstepSounds.Untergrund.Beton);
            float schutt = FootstepSounds.LautstaerkeFaktor(FootstepSounds.Untergrund.Schutt);

            Assert.Greater(metall, beton, "Metall muesste lauter sein als Beton.");
            Assert.Less(schutt, beton, "Schutt muesste leiser sein als Beton.");

            Assert.Greater(FootstepSounds.StreuungFuer(FootstepSounds.Untergrund.Schutt),
                           FootstepSounds.StreuungFuer(FootstepSounds.Untergrund.Metall),
                "Schutt sollte unregelmaessiger klingen als Metall.");
        }
    }
}
