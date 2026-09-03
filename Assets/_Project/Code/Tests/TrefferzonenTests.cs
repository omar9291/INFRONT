using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Realismus-Etappe Schritt 5: Trefferzonen und Bluten.
    ///
    /// NICHT pruefbar: ob die Werte fair sind, ob sich Bluten spannend
    /// anfuehlt oder ob man mit kaputtem Bein noch Spass hat.
    ///
    /// Geprueft wird, was messbar ist:
    ///  - Eine Figur hat alle vier Zonen als Trefferflaechen.
    ///  - Arme und Beine schlucken Schaden, Torso und Kopf nicht.
    ///  - Eine Blutung nimmt ueber die Zeit Leben weg.
    ///  - Sie hoert nicht von selbst auf.
    ///  - Ein Verband stoppt sie, heilt aber nicht.
    ///  - Man verblutet nicht bis auf null.
    ///  - Beinschaden bremst, Armschaden streut.
    /// </summary>
    public sealed class TrefferzonenTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static Bleeding ErstesBluten()
        {
            foreach (var m in Combatants.Everyone)
            {
                if (m == null) continue;
                var b = m.GetComponent<Bleeding>();
                if (b != null) return b;
            }
            return null;
        }

        [Test]
        public void Arme_und_Beine_schlucken_Schaden_Torso_nicht()
        {
            Assert.AreEqual(1f, Hitbox.SchadenFaktor(KoerperZone.Torso), 0.001f,
                "Der Torso sollte den vollen Schaden bekommen.");
            Assert.AreEqual(1f, Hitbox.SchadenFaktor(KoerperZone.Kopf), 0.001f,
                "Der Kopf wird ueber HeadshotMultiplier geregelt, hier also Faktor 1.");
            Assert.Less(Hitbox.SchadenFaktor(KoerperZone.Arm), 1f,
                "Ein Armtreffer sollte weniger Schaden machen als ein Torsotreffer.");
            Assert.Less(Hitbox.SchadenFaktor(KoerperZone.Bein), 1f,
                "Ein Beintreffer sollte weniger Schaden machen als ein Torsotreffer.");
        }

        [Test]
        public void Torso_und_Beine_bluten_am_ehesten()
        {
            Assert.Greater(Hitbox.BlutungsChance(KoerperZone.Torso),
                           Hitbox.BlutungsChance(KoerperZone.Kopf),
                "Ein Kopftreffer ist meist ohnehin toedlich - Bluten spielt dort kaum eine Rolle.");
            Assert.Greater(Hitbox.BlutungsChance(KoerperZone.Bein),
                           Hitbox.BlutungsChance(KoerperZone.Arm),
                "Beintreffer sollten eher bluten als Armtreffer.");
        }

        [UnityTest]
        public IEnumerator Figur_hat_alle_vier_Zonen()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var boxen = player.GetComponentsInChildren<Hitbox>(true);
            Assert.Greater(boxen.Length, 2,
                "Es gibt nur die alten zwei Trefferflaechen (Kopf und Koerper).");

            bool kopf = false, torso = false, arm = false, bein = false;
            foreach (var b in boxen)
            {
                if (b.Zone == KoerperZone.Kopf) kopf = true;
                if (b.Zone == KoerperZone.Torso) torso = true;
                if (b.Zone == KoerperZone.Arm) arm = true;
                if (b.Zone == KoerperZone.Bein) bein = true;
            }
            Assert.IsTrue(kopf, "Keine Kopf-Trefferflaeche.");
            Assert.IsTrue(torso, "Keine Torso-Trefferflaeche.");
            Assert.IsTrue(arm, "Keine Arm-Trefferflaeche.");
            Assert.IsTrue(bein, "Keine Bein-Trefferflaeche.");
        }

        [UnityTest]
        public IEnumerator Blutung_nimmt_Leben_und_hoert_nicht_von_selbst_auf()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bluten = player.GetComponent<Bleeding>();
            var health = player.GetComponent<Health>();
            Assert.IsNotNull(bluten, "Der Spieler hat keine Blutungs-Komponente.");
            Assert.IsNotNull(health, "Der Spieler hat keine Health.");

            bluten.SetWundenForTests(3);
            int vorher = health.Current;

            float gewartet = 0f;
            while (gewartet < 3f)
            {
                yield return null;
                gewartet += Time.deltaTime;
            }

            Assert.Less(health.Current, vorher,
                $"Nach 3 s Bluten ist kein Leben verloren gegangen (vorher={vorher}, jetzt={health.Current}).");
            Assert.IsTrue(bluten.Blutet,
                "Die Blutung hat von selbst aufgehoert - sie darf nur durch einen Verband stoppen.");
        }

        [UnityTest]
        public IEnumerator Verband_stoppt_die_Blutung_heilt_aber_nicht()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bluten = player.GetComponent<Bleeding>();
            var health = player.GetComponent<Health>();
            bluten.SetWundenForTests(2);

            float gewartet = 0f;
            while (gewartet < 1.5f) { yield return null; gewartet += Time.deltaTime; }
            int nachBluten = health.Current;

            bluten.ServerVerbinden();
            Assert.IsFalse(bluten.Blutet, "Der Verband stoppt die Blutung nicht.");

            gewartet = 0f;
            while (gewartet < 1.5f) { yield return null; gewartet += Time.deltaTime; }

            Assert.AreEqual(nachBluten, health.Current,
                "Nach dem Verbinden hat sich das Leben veraendert - der Verband soll stoppen, nicht heilen.");
        }

        [UnityTest]
        public IEnumerator Man_verblutet_nicht_bis_auf_null()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bluten = player.GetComponent<Bleeding>();
            var health = player.GetComponent<Health>();
            bluten.SetWundenForTests(3);

            // Lange genug bluten lassen, dass es ohne Untergrenze toedlich waere.
            float gewartet = 0f;
            while (gewartet < 12f) { yield return null; gewartet += Time.deltaTime; }

            Assert.IsTrue(health.IsAlive,
                "Der Spieler ist verblutet - in einem Rundenspiel nimmt das jede Chance.");
            Assert.GreaterOrEqual(health.Current, bluten.UntergrenzeForTests,
                $"Die Blutung ist unter die Untergrenze von {bluten.UntergrenzeForTests} gegangen.");
        }

        [UnityTest]
        public IEnumerator Beinschaden_bremst_und_Armschaden_streut()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var bluten = player.GetComponent<Bleeding>();

            bluten.SetBeinMalusForTests(0f);
            yield return null;
            float tempoGesund = bluten.TempoFaktor;

            bluten.SetBeinMalusForTests(1f);
            yield return null;
            Assert.Less(bluten.TempoFaktor, tempoGesund,
                "Beinschaden bremst nicht.");

            bluten.SetArmMalusForTests(0f);
            yield return null;
            float streuungGesund = bluten.ZusatzStreuung;

            bluten.SetArmMalusForTests(1f);
            yield return null;
            Assert.Greater(bluten.ZusatzStreuung, streuungGesund,
                "Armschaden macht die Waffe nicht unruhiger.");
        }
    }
}
