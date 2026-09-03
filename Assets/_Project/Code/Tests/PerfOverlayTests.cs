using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die Leistungsanzeige (<see cref="PerfOverlay"/>, F3).
    ///
    /// NICHT prüfbar: wie sie aussieht. Geprüft wird:
    ///  - standardmäßig unsichtbar, Umschalten wirkt,
    ///  - sie sammelt Frame-Zeiten und rechnet daraus sinnvolle Werte
    ///    (geglättete FPS, 1%-Tiefpunkt).
    /// </summary>
    public sealed class PerfOverlayTests
    {
        PerfOverlay _po;

        [SetUp]
        public void Setup()
        {
            // Eigene, isolierte Instanz - NICHT die vom GameFlow (die traegt
            // auch AudioService/LoadingOverlay, die wollen wir nicht anfassen).
            _po = new GameObject("PerfOverlay (Test)").AddComponent<PerfOverlay>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_po != null) Object.Destroy(_po.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Standardmaessig_aus_und_umschaltbar()
        {
            yield return null;

            Assert.IsFalse(_po.VisibleForTests, "Die Anzeige darf beim Start nicht sichtbar sein.");

            _po.Toggle();
            Assert.IsTrue(_po.VisibleForTests, "F3 (Toggle) hat die Anzeige nicht eingeblendet.");

            _po.Toggle();
            Assert.IsFalse(_po.VisibleForTests, "Zweites Umschalten hat sie nicht wieder ausgeblendet.");
        }

        [UnityTest]
        public IEnumerator Sammelt_Frame_Zeiten_und_rechnet_sinnvolle_Werte()
        {
            // Genug Frames für mindestens ein Neuberechnungs-Fenster (0,5 s).
            for (int i = 0; i < 90; i++) yield return null;

            Assert.Greater(_po.SamplesForTests, 5, "Es wurden kaum Frame-Zeiten gesammelt.");
            Assert.Greater(_po.SmoothFpsForTests, 0f, "Keine geglättete Bildrate.");
            Assert.Less(_po.SmoothFpsForTests, 100000f, "Die Bildrate ist unrealistisch hoch.");
            Assert.Greater(_po.OnePercentLowForTests, 0f, "Kein 1%-Tiefpunkt berechnet.");
            Assert.LessOrEqual(_po.OnePercentLowForTests, _po.MaxFpsForTests + 0.01f,
                "Der 1%-Tiefpunkt kann nicht über der Höchst-Bildrate liegen.");
        }
    }
}
