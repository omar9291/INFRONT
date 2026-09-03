using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Realismus-Etappe Schritt 2: "Gewicht und Traegheit".
    ///
    /// NICHT pruefbar: ob sich die Bewegung schwer genug anfuehlt. Das ist
    /// Geschmack und muss gespielt werden.
    ///
    /// Geprueft wird, was messbar ist:
    ///  - Die Geschwindigkeit steigt allmaehlich, nicht sprunghaft.
    ///  - Nach dem Loslassen rutscht man noch ein Stueck weiter.
    ///  - Sprinten hat eine Anlaufzeit und endet nicht abrupt.
    ///  - Eine harte Landung kostet kurz Kontrolle.
    /// </summary>
    public sealed class GewichtTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static IEnumerator Fixed(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Bereit(System.Action<NetworkPlayerController> weiter)
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);
            weiter(player);
        }

        [UnityTest]
        public IEnumerator Geschwindigkeit_steigt_allmaehlich_statt_sofort()
        {
            NetworkPlayerController player = null;
            yield return Bereit(p => player = p);

            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = 0f };
            player.SetInputSource(input);

            // Nach einem einzigen Schritt darf noch fast nichts passiert sein.
            yield return Fixed(2);
            float frueh = player.HorizontalVelocityForTests.magnitude;

            // Nach knapp einer Sekunde muss die Zielgeschwindigkeit stehen.
            yield return Fixed(50);
            float spaet = player.HorizontalVelocityForTests.magnitude;

            Assert.Greater(spaet, 3f,
                $"Der Spieler kommt gar nicht auf Tempo (spaet={spaet:F2}).");
            Assert.Less(frueh, spaet * 0.5f,
                $"Die Geschwindigkeit steht sofort an - keine Traegheit. " +
                $"frueh={frueh:F2} spaet={spaet:F2}");
        }

        [UnityTest]
        public IEnumerator Nach_dem_Loslassen_rutscht_man_noch_weiter()
        {
            NetworkPlayerController player = null;
            yield return Bereit(p => player = p);

            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = 0f };
            player.SetInputSource(input);
            yield return Fixed(60);

            input.Move = Vector2.zero;
            yield return Fixed(2);
            float direktNachher = player.HorizontalVelocityForTests.magnitude;

            Assert.Greater(direktNachher, 0.5f,
                "Der Spieler klebt sofort am Boden fest - keine Nachlaufzeit.");

            yield return Fixed(60);
            Assert.Less(player.HorizontalVelocityForTests.magnitude, 0.5f,
                "Der Spieler rutscht endlos weiter.");
        }

        [UnityTest]
        public IEnumerator Sprint_hat_Anlauf_und_laeuft_aus()
        {
            NetworkPlayerController player = null;
            yield return Bereit(p => player = p);

            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = 0f, Sprint = true };
            player.SetInputSource(input);

            yield return Fixed(5);
            float anfang = player.SprintRampForTests;
            Assert.Less(anfang, 0.5f,
                $"Der Sprint ist sofort auf voller Stufe (Rampe={anfang:F2}) - kein Anlauf.");

            yield return Fixed(90);
            Assert.Greater(player.SprintRampForTests, 0.9f,
                "Der Sprint erreicht nie die volle Stufe.");

            input.Sprint = false;
            yield return Fixed(5);
            float kurzDanach = player.SprintRampForTests;
            Assert.Greater(kurzDanach, 0.1f,
                "Der Sprint bricht schlagartig ab statt auszulaufen.");

            yield return Fixed(60);
            Assert.Less(player.SprintRampForTests, 0.1f,
                "Die Sprint-Rampe faellt nie wieder auf null.");
        }

        [UnityTest]
        public IEnumerator Harte_Landung_kostet_kurz_Kontrolle()
        {
            NetworkPlayerController player = null;
            yield return Bereit(p => player = p);

            // Aus der Hoehe fallen lassen, damit die Fallgeschwindigkeit reicht.
            // Achtung: PlacePlayer schaltet die Bewegung am Ende wieder ab -
            // ohne das erneute Einschalten laeuft die Landungs-Erkennung nicht.
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 6f, 0f), 0f);
            player.SetMovementEnabled(true);
            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = 0f };
            player.SetInputSource(input);

            bool gesehen = false;
            for (int i = 0; i < 200 && !gesehen; i++)
            {
                yield return new WaitForFixedUpdate();
                if (player.LandStunLeftForTests > 0f) gesehen = true;
            }

            Assert.IsTrue(gesehen,
                "Nach dem Aufkommen aus 6 m gab es keinen Kontrollverlust.");

            // Nicht auf eine feste Bildzahl warten: der Server zaehlt die
            // Restzeit beim Verarbeiten der Eingabe herunter, und unter voller
            // Testlast kommen weniger Eingaben an als bei einem Einzellauf.
            // Deshalb auf das Ereignis warten, mit grosszuegiger Obergrenze.
            bool vorbei = false;
            for (int i = 0; i < 400 && !vorbei; i++)
            {
                yield return new WaitForFixedUpdate();
                if (player.LandStunLeftForTests <= 0.001f) vorbei = true;
            }
            Assert.IsTrue(vorbei,
                $"Der Kontrollverlust nach der Landung hoert nie auf " +
                $"(Rest={player.LandStunLeftForTests:F3} s).");
        }
    }
}
