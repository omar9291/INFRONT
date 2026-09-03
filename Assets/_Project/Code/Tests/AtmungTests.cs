using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Realismus-Etappe Schritt 3: Atmung.
    ///
    /// NICHT pruefbar: ob die Staerke angenehm ist, ob es nach Atmen klingt
    /// oder ob einem davon schlecht wird. Das muss gespielt werden.
    ///
    /// Geprueft wird, was messbar ist:
    ///  - Der Atem laeuft in einem Rhythmus (die Phase bewegt sich).
    ///  - Sprinten baut Anstrengung auf, Ruhe baut sie wieder ab.
    ///  - Anstrengung macht den Atem schneller und staerker.
    ///  - Wenig Leben macht den Atem schwerer, auch ohne Anstrengung.
    ///  - Anhalten beruhigt, ist aber begrenzt und schlaegt danach zurueck.
    ///  - Die Waffe folgt dem Blick verzoegert, nicht sofort.
    /// </summary>
    public sealed class AtmungTests
    {
        GameObject _go;
        Breathing _atem;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AtemTest");
            _atem = _go.AddComponent<Breathing>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Atem_laeuft_in_einem_Rhythmus()
        {
            yield return Frames(3);
            float a = _atem.PhaseForTests;
            yield return Frames(30);
            float b = _atem.PhaseForTests;
            Assert.AreNotEqual(a, b, "Die Atem-Phase steht still.");
        }

        [UnityTest]
        public IEnumerator Sprinten_baut_Anstrengung_auf_und_Ruhe_wieder_ab()
        {
            _atem.Sprinting = true;
            yield return Frames(120);
            float nachSprint = _atem.Exertion01ForTests;
            Assert.Greater(nachSprint, 0.05f,
                $"Sprinten baut keine Anstrengung auf (={nachSprint:F3}).");

            _atem.Sprinting = false;
            yield return Frames(120);
            Assert.Less(_atem.Exertion01ForTests, nachSprint,
                "Die Anstrengung baut sich in Ruhe nicht wieder ab.");
        }

        [UnityTest]
        public IEnumerator Anstrengung_macht_den_Atem_schneller_und_staerker()
        {
            yield return Frames(2);
            _atem.SetExertionForTests(0f);
            yield return Frames(2);
            float rateRuhe = _atem.RateForTests;
            float ampRuhe = _atem.AmplitudeForTests;

            _atem.SetExertionForTests(1f);
            yield return Frames(2);

            Assert.Greater(_atem.RateForTests, rateRuhe,
                "Der Atem wird bei Anstrengung nicht schneller.");
            Assert.Greater(_atem.AmplitudeForTests, ampRuhe,
                "Der Atem wird bei Anstrengung nicht staerker.");
        }

        [UnityTest]
        public IEnumerator Wenig_Leben_macht_den_Atem_schwerer()
        {
            _atem.Health01 = 1f;
            yield return Frames(2);
            float gesund = _atem.AmplitudeForTests;

            _atem.Health01 = 0.15f;
            yield return Frames(2);
            float verwundet = _atem.AmplitudeForTests;

            Assert.Greater(verwundet, gesund,
                $"Wenig Leben macht den Atem nicht schwerer (gesund={gesund:F3} verwundet={verwundet:F3}).");
        }

        [UnityTest]
        public IEnumerator Luft_anhalten_beruhigt_ist_aber_begrenzt()
        {
            _atem.Aiming = true;
            yield return Frames(2);
            float normal = _atem.AmplitudeForTests;

            _atem.WantHold = true;
            yield return Frames(3);
            Assert.IsTrue(_atem.IsHoldingForTests, "Die Luft wird gar nicht angehalten.");
            Assert.Less(_atem.AmplitudeForTests, normal,
                "Anhalten beruhigt den Atem nicht.");

            // Restluft kuenstlich fast leeren, damit der Test nicht Sekunden
            // lang warten muss.
            _atem.SetHoldLeftForTests(0.05f);
            yield return Frames(20);
            Assert.IsFalse(_atem.IsHoldingForTests,
                "Die Luft laesst sich unbegrenzt anhalten - das waere ein Vorteil, kein Realismus.");
        }

        [UnityTest]
        public IEnumerator Nach_dem_Anhalten_geht_der_Atem_staerker()
        {
            _atem.Aiming = true;
            _atem.SetExertionForTests(0.1f);
            _atem.WantHold = true;
            yield return Frames(3);
            float vorher = _atem.Exertion01ForTests;

            _atem.SetHoldLeftForTests(0.02f);
            yield return Frames(20);

            Assert.Greater(_atem.Exertion01ForTests, vorher,
                "Nach dem Anhalten schlaegt der Atem nicht zurueck.");
        }

        [UnityTest]
        public IEnumerator Waffe_folgt_dem_Blick_verzoegert()
        {
            _atem.SetExertionForTests(1f);
            yield return Frames(2);

            // Direkt nach einem Sprung im Versatz darf die Waffe noch nicht
            // dort sein, wo der Blick schon ist.
            bool jeUnterschiedlich = false;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if ((_atem.Offset - _atem.WeaponOffset).magnitude > 0.01f)
                {
                    jeUnterschiedlich = true;
                    break;
                }
            }
            Assert.IsTrue(jeUnterschiedlich,
                "Die Waffe klebt starr am Blick statt verzoegert zu folgen.");
        }

        [UnityTest]
        public IEnumerator Angehalten_wird_nur_beim_Zielen()
        {
            _atem.Aiming = false;
            _atem.WantHold = true;
            yield return Frames(5);
            Assert.IsFalse(_atem.IsHoldingForTests,
                "Ohne zu zielen darf die Luft nicht angehalten werden.");
        }
    }
}
