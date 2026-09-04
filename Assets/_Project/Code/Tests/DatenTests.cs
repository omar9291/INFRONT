using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die eigenen Zahlen, die Fehlerberichte und die Seite "Deine Daten".
    ///
    /// Der Anspruch, an dem sich das messen laesst: es verlaesst nichts diesen
    /// Rechner, und der Spieler kommt an alles heran, was ueber ihn
    /// gespeichert ist.
    ///
    /// NICHT pruefbar: dass wirklich nie jemand etwas verschickt - das steht
    /// im Code und nicht in einem Test. Pruefbar: dass die Zahlen stimmen, das
    /// Speichern ueberleben, dass ein Fehler eine Datei erzeugt und dass
    /// "alles loeschen" wirklich alles wegraeumt.
    /// </summary>
    public sealed class DatenTests
    {
        [SetUp]
        public void SetUp()
        {
            Spielstatistik.ForgetForTests();
            Spielstatistik.AllesLoeschen();
            Absturzbericht.ForgetForTests();
            Absturzbericht.AllesLoeschen();
        }

        [TearDown]
        public void TearDown()
        {
            Spielstatistik.AllesLoeschen();
            Spielstatistik.ForgetForTests();
            Absturzbericht.AllesLoeschen();
            Absturzbericht.ForgetForTests();
        }

        // --- Zahlen ---------------------------------------------------------

        [Test]
        public void Frische_Statistik_ist_leer()
        {
            Assert.AreEqual(0, Spielstatistik.Daten.Schuesse);
            Assert.AreEqual(0f, Spielstatistik.Trefferquote, 0.001f,
                "Ohne Schuesse darf die Trefferquote nicht durch null teilen.");
            Assert.AreEqual(0f, Spielstatistik.Kopfquote, 0.001f);
            Assert.AreEqual(0f, Spielstatistik.Verhaeltnis, 0.001f);
        }

        [Test]
        public void Trefferquote_rechnet_richtig()
        {
            for (int i = 0; i < 10; i++) Spielstatistik.Schuss();
            Spielstatistik.Treffer(false);
            Spielstatistik.Treffer(false);
            Spielstatistik.Treffer(true);

            Assert.AreEqual(0.3f, Spielstatistik.Trefferquote, 0.001f);
            Assert.AreEqual(1f / 3f, Spielstatistik.Kopfquote, 0.001f);
        }

        [Test]
        public void Ohne_Tode_ist_das_Verhaeltnis_die_Zahl_der_Abschuesse()
        {
            Spielstatistik.Abschuss();
            Spielstatistik.Abschuss();
            Assert.AreEqual(2f, Spielstatistik.Verhaeltnis, 0.001f,
                "Ohne Tode darf nicht durch null geteilt werden.");

            Spielstatistik.Tod();
            Assert.AreEqual(2f, Spielstatistik.Verhaeltnis, 0.001f);
            Spielstatistik.Tod();
            Assert.AreEqual(1f, Spielstatistik.Verhaeltnis, 0.001f);
        }

        [Test]
        public void Zahlen_ueberleben_das_Speichern()
        {
            Spielstatistik.Schuss();
            Spielstatistik.Schuss();
            Spielstatistik.Treffer(true);
            Spielstatistik.SpielVorbei(true);

            Spielstatistik.ForgetForTests();
            Spielstatistik.Laden();

            Assert.AreEqual(2, Spielstatistik.Daten.Schuesse);
            Assert.AreEqual(1, Spielstatistik.Daten.Kopftreffer);
            Assert.AreEqual(1, Spielstatistik.Daten.Spiele);
            Assert.AreEqual(1, Spielstatistik.Daten.Siege);
        }

        [Test]
        public void Eine_kaputte_Datei_haelt_das_Spiel_nicht_auf()
        {
            File.WriteAllText(Spielstatistik.FilePath, "{ das ist kein JSON ][");
            Spielstatistik.ForgetForTests();

            LogAssert.ignoreFailingMessages = true;
            var d = Spielstatistik.Daten;
            LogAssert.ignoreFailingMessages = false;

            Assert.IsNotNull(d, "Bei kaputter Datei muesste frisch angefangen werden.");
            Assert.AreEqual(0, d.Schuesse);
        }

        // --- Fehlerberichte ---------------------------------------------------

        [Test]
        public void Ein_Fehler_schreibt_eine_Datei()
        {
            Assert.AreEqual(0, Absturzbericht.Anzahl);

            Absturzbericht.SchreibeTestbericht("Etwas ist schiefgegangen");

            Assert.AreEqual(1, Absturzbericht.Anzahl,
                "Ein Fehler muesste genau einen Bericht schreiben.");

            var datei = Directory.GetFiles(Absturzbericht.Ordner, "*.txt")[0];
            var inhalt = File.ReadAllText(datei);
            Assert.IsTrue(inhalt.Contains("Etwas ist schiefgegangen"),
                "Die Meldung muesste im Bericht stehen.");
            Assert.IsTrue(inhalt.Contains("Nothing is sent anywhere"),
                "Im Bericht muesste stehen, dass er nirgendwo hingeht - sonst weiss "
                + "der Spieler nicht, was damit passiert.");
        }

        [Test]
        public void Der_Bericht_enthaelt_nichts_ueber_die_Person()
        {
            Absturzbericht.SchreibeTestbericht("Test");
            var inhalt = File.ReadAllText(Directory.GetFiles(Absturzbericht.Ordner, "*.txt")[0]);

            Assert.IsFalse(inhalt.Contains("@"),
                "Im Bericht darf keine E-Mail-Adresse stehen.");
            Assert.IsFalse(inhalt.Contains("/Users/"),
                "Im Bericht darf kein Pfad ins Benutzerverzeichnis stehen - da steht "
                + "der Name des Menschen drin.");
        }

        [Test]
        public void Eine_Schleife_ertraenkt_den_Ordner_nicht()
        {
            for (int i = 0; i < 40; i++) Absturzbericht.SchreibeTestbericht("Fehler " + i);
            Assert.LessOrEqual(Absturzbericht.Anzahl, 20,
                "Es duerfen nicht beliebig viele Berichte liegenbleiben.");
        }

        // --- Loeschen ----------------------------------------------------------

        [Test]
        public void Alles_loeschen_raeumt_wirklich_alles_weg()
        {
            Spielstatistik.Schuss();
            Spielstatistik.Speichern();
            Absturzbericht.SchreibeTestbericht("Test");
            PlayerProfile.MarkOnboardingDone();

            Assert.IsTrue(File.Exists(Spielstatistik.FilePath));
            Assert.AreEqual(1, Absturzbericht.Anzahl);

            PlayerProfile.DeleteEverything();

            Assert.AreEqual(0, Absturzbericht.Anzahl,
                "Die Fehlerberichte muessten mit weg sein.");
            Assert.AreEqual(0, Spielstatistik.Daten.Schuesse,
                "Die Zahlen muessten zurueckgesetzt sein.");
            Assert.AreEqual(0, CareerStats.Matches,
                "Die Laufbahn muesste zurueckgesetzt sein.");
        }

        // --- Menue --------------------------------------------------------------

        [UnityTest]
        public IEnumerator Die_Seite_Deine_Daten_steht_im_Menue()
        {
            yield return MenuUiHarness.OeffneSeite("YOUR DATA");

            Assert.IsNotNull(MenuUiHarness.Finde("btn-ordner"),
                "Es muesste einen Knopf geben, der den Ordner oeffnet - sonst ist die "
                + "Behauptung 'du kommst an alles heran' nur eine Behauptung.");
            Assert.IsNotNull(MenuUiHarness.Finde("btn-alles-weg"),
                "Es muesste einen Knopf zum Loeschen geben.");
            Assert.IsNotNull(MenuUiHarness.Finde("btn-berichte-weg"));
        }
    }
}
