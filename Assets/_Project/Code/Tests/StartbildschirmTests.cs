using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Der Startbildschirm (<see cref="BootFlow"/>).
    ///
    /// Der Fehler, den diese Tests festhalten: der Ladebildschirm hing nur am
    /// Szenenwechsel. Die Menue-Szene ist die Startszene, beim Programmstart
    /// wechselt also nichts - und deshalb war beim Start nie einer zu sehen.
    ///
    /// NICHT pruefbar: ob er schoen aussieht. Pruefbar: dass er beim Start
    /// ueberhaupt laeuft, jede Phase erreicht, danach sauber fertig meldet und
    /// im Testlauf uebersprungen wird.
    /// </summary>
    public sealed class StartbildschirmTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp() => BootFlow.ResetForTests();

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            BootFlow.ResetForTests();
        }

        BootFlow Baue(float mindest = 0.15f)
        {
            _go = new GameObject("BootFlowTest");
            _go.AddComponent<AudioService>();
            _go.AddComponent<LoadingOverlay>();
            var boot = _go.AddComponent<BootFlow>();
            boot.SetDauerForTests(mindest, 3f);
            return boot;
        }

        // --- Ablauf ---------------------------------------------------------

        [UnityTest]
        public IEnumerator Startbildschirm_laeuft_und_meldet_sich_fertig()
        {
            BootFlow.ForceRunForTests = true;
            var boot = Baue();
            yield return null;

            float grenze = Time.unscaledTime + 10f;
            while (!BootFlow.Done && Time.unscaledTime < grenze) yield return null;

            Assert.IsTrue(BootFlow.Done,
                "Der Startbildschirm muesste nach spaetestens 10 Sekunden fertig sein. "
                + "Haengengeblieben bei: " + boot.PhaseForTests);
            Assert.IsFalse(BootFlow.Running,
                "Nach dem Start duerfte er nicht mehr als laufend gelten.");
            Assert.AreEqual("READY", boot.PhaseForTests,
                "Die letzte Phase muesste BEREIT sein.");
        }

        [UnityTest]
        public IEnumerator Jede_Phase_wird_wirklich_erreicht()
        {
            BootFlow.ForceRunForTests = true;
            var boot = Baue(0.4f);

            var gesehen = new List<string>();
            float grenze = Time.unscaledTime + 10f;
            while (!BootFlow.Done && Time.unscaledTime < grenze)
            {
                string p = boot.PhaseForTests;
                if (!string.IsNullOrEmpty(p) && (gesehen.Count == 0 || gesehen[gesehen.Count - 1] != p))
                    gesehen.Add(p);
                yield return null;
            }

            foreach (var erwartet in new[] { "READING PROFILE", "SETTINGS", "CAREER",
                                             "PREPARING AUDIO", "BUILDING MENU", "READY" })
                Assert.Contains(erwartet, gesehen,
                    "Phase '" + erwartet + "' wurde nie angezeigt. Gesehen: "
                    + string.Join(" -> ", gesehen));
        }

        [UnityTest]
        public IEnumerator Ladebildschirm_ist_waehrend_des_Starts_sichtbar()
        {
            BootFlow.ForceRunForTests = true;
            Baue(0.6f);
            yield return null;
            yield return null;
            yield return null;

            var ov = LoadingOverlay.Instance;
            Assert.IsNotNull(ov, "Der Ladebildschirm muesste da sein.");
            Assert.IsTrue(ov.IsVisibleForTests,
                "Waehrend des Starts muesste der Ladebildschirm sichtbar sein - "
                + "sonst sieht der Spieler beim Programmstart wieder nichts.");
        }

        // --- Uebersprungen --------------------------------------------------

        [UnityTest]
        public IEnumerator Im_Testlauf_wird_der_Start_uebersprungen()
        {
            BootFlow.ForceRunForTests = false;
            Baue();
            yield return null;

            Assert.IsTrue(BootFlow.Done,
                "Im Batchmode muesste der Startbildschirm sofort uebersprungen werden - "
                + "sonst wartet jeder Testlauf drei Sekunden pro Start.");
        }

        // --- WhenDone -------------------------------------------------------

        [Test]
        public void WhenDone_laeuft_sofort_wenn_der_Start_schon_durch_ist()
        {
            BootFlow.MarkDoneForTests();
            bool gelaufen = false;
            BootFlow.WhenDone(() => gelaufen = true);
            Assert.IsTrue(gelaufen, "Nach dem Start muesste WhenDone sofort ausloesen.");
        }

        [Test]
        public void WhenDone_wartet_und_loest_danach_aus()
        {
            bool gelaufen = false;
            BootFlow.WhenDone(() => gelaufen = true);
            Assert.IsFalse(gelaufen, "Vor dem Ende des Starts duerfte noch nichts laufen.");

            BootFlow.MarkDoneForTests();
            Assert.IsTrue(gelaufen, "Am Ende des Starts muesste der Wartende laufen.");
        }

        [Test]
        public void Ein_Fehler_im_Wartenden_stoppt_die_anderen_nicht()
        {
            bool zweiterLief = false;
            BootFlow.WhenDone(() => throw new System.InvalidOperationException("Absicht"));
            BootFlow.WhenDone(() => zweiterLief = true);

            LogAssert.ignoreFailingMessages = true;
            BootFlow.MarkDoneForTests();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(zweiterLief,
                "Ein kaputter Wartender darf den Rest des Starts nicht mitreissen.");
        }

        // --- Verdrahtung ----------------------------------------------------

        [Test]
        public void GameFlow_bringt_den_Startbildschirm_mit()
        {
            var flow = Object.FindAnyObjectByType<GameFlow>();
            Assert.IsNotNull(flow, "GameFlow muesste beim Start angelegt worden sein.");
            Assert.IsNotNull(flow.GetComponent<BootFlow>(),
                "Am GameFlow-Objekt muesste ein BootFlow haengen - sonst laeuft der "
                + "Startbildschirm im fertigen Spiel nie.");
        }
    }
}
