using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Kurze Hinweise unten links (<see cref="Meldungen"/>).
    ///
    /// Wofuer sie NICHT da sind: Abschuesse und Rundenmeldungen - dafuer gibt
    /// es die Abschussliste und das grosse Band. Wer alles in dieselbe Ecke
    /// wirft, macht das Wichtige unsichtbar.
    ///
    /// Wofuer schon: alles, was sonst lautlos passiert. Ein Fehlerbericht, den
    /// niemand bemerkt, und eine Bestleistung, die niemand mitbekommt, koennte
    /// es auch nicht geben.
    /// </summary>
    public sealed class MeldungenTests
    {
        VisualElement _wurzel;

        [SetUp]
        public void SetUp()
        {
            Meldungen.ForgetForTests();
            _wurzel = new VisualElement();
        }

        [TearDown]
        public void TearDown() => Meldungen.ForgetForTests();

        [Test]
        public void Eine_Meldung_erscheint()
        {
            Meldungen.Anhaengen(_wurzel);
            Meldungen.Zeige("Etwas ist passiert");

            Assert.AreEqual(1, Meldungen.SichtbarForTests);
            Assert.AreEqual("Etwas ist passiert", Meldungen.LetzteForTests);
        }

        [Test]
        public void Ohne_Anzeige_geht_nichts_verloren()
        {
            // Noch kein HUD da - z.B. weil es im Menue passiert ist.
            Meldungen.Zeige("Fehlerbericht geschrieben", Meldungen.Art.Fehler);
            Assert.AreEqual(0, Meldungen.SichtbarForTests);

            // Jetzt kommt die Anzeige.
            Meldungen.Anhaengen(_wurzel);
            Assert.AreEqual(1, Meldungen.SichtbarForTests,
                "Was vor dem Aufbau der Anzeige gemeldet wurde, muesste nachgeholt "
                + "werden. Eine Meldung darf nicht verlorengehen, nur weil sie zur "
                + "falschen Zeit kam.");
        }

        [Test]
        public void Zu_viele_auf_einmal_stapeln_sich_nicht_endlos()
        {
            Meldungen.Anhaengen(_wurzel);
            for (int i = 0; i < 12; i++) Meldungen.Zeige("Meldung " + i);

            Assert.LessOrEqual(Meldungen.SichtbarForTests, 4,
                "Es duerfen nicht beliebig viele Hinweise gleichzeitig stehen - sonst "
                + "verdecken sie das Spiel.");
        }

        [Test]
        public void Leere_Meldungen_werden_nicht_gezeigt()
        {
            Meldungen.Anhaengen(_wurzel);
            Meldungen.Zeige("");
            Meldungen.Zeige("   ");
            Meldungen.Zeige(null);
            Assert.AreEqual(0, Meldungen.SichtbarForTests);
        }

        [Test]
        public void Nach_dem_Abhaengen_stuerzt_nichts_ab()
        {
            Meldungen.Anhaengen(_wurzel);
            Meldungen.Abhaengen();
            Assert.DoesNotThrow(() => Meldungen.Zeige("Nach dem HUD"),
                "Eine Meldung ohne Anzeige darf nicht krachen - das passiert beim "
                + "Szenenwechsel staendig.");
        }

        // --- Wirklich verdrahtet ----------------------------------------------

        [Test]
        public void Ein_Fehlerbericht_meldet_sich()
        {
            Absturzbericht.ForgetForTests();
            Absturzbericht.AllesLoeschen();
            Meldungen.ForgetForTests();

            Absturzbericht.SchreibeTestbericht("Test");

            StringAssert.Contains("Fehlerbericht", Meldungen.LetzteForTests,
                "Wenn ein Fehlerbericht geschrieben wird, muss der Spieler das erfahren - "
                + "sonst weiss niemand, dass es die Datei gibt.");

            Absturzbericht.AllesLoeschen();
            Absturzbericht.ForgetForTests();
        }

        [UnityTest]
        public IEnumerator Eine_Bestleistung_meldet_sich()
        {
            CareerStats.ResetForTests();
            Meldungen.ForgetForTests();

            CareerStats.RecordMatch(true);    // Serie 1 - noch keine Meldung
            Assert.AreEqual("", Meldungen.LetzteForTests,
                "Der erste Sieg ist noch keine Bestleistung.");

            CareerStats.RecordMatch(true);    // Serie 2 - jetzt schon
            StringAssert.Contains("Bestleistung", Meldungen.LetzteForTests,
                "Eine neue Bestleistung muesste gemeldet werden.");

            CareerStats.ResetForTests();
            yield break;
        }
    }
}
