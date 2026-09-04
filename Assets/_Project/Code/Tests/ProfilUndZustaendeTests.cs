using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Das oertliche Spielerprofil, der Erstlauf und die vier Oberflaechen-
    /// Zustaende (leer, laedt, Fehler, Netz).
    ///
    /// Diese ersetzen zusammen das, wofuer sonst ein Konto-System noetig waere -
    /// ohne Server, ohne Rechnung und ohne dass jemand fuer fremde Daten
    /// geradestehen muss.
    ///
    /// NICHT pruefbar: ob der Erstlauf wirklich hilft. Pruefbar: speichern und
    /// laden, Erstlauf genau einmal, Loeschen raeumt wirklich auf, und jeder
    /// Zustand hat Titel, Text und (ausser beim Laden) einen Ausweg.
    /// </summary>
    public sealed class ProfilUndZustaendeTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerProfile.ForgetForTests();
            PlayerProfile.DeleteEverything();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.DeleteEverything();
            PlayerProfile.ForgetForTests();
        }

        // --- Profil ---------------------------------------------------------

        [Test]
        public void Frisches_Profil_ist_ein_Erstlauf()
        {
            PlayerProfile.ForgetForTests();
            Assert.IsTrue(PlayerProfile.IsFirstRun,
                "Ein frisches Profil muesste als Erstlauf gelten.");
        }

        [Test]
        public void Erstlauf_wird_gemerkt_und_ueberlebt_einen_Neustart()
        {
            PlayerProfile.MarkOnboardingDone();
            Assert.IsFalse(PlayerProfile.IsFirstRun, "Der Erstlauf wurde nicht vermerkt.");

            // Neustart nachstellen: alles aus dem Speicher werfen und neu laden.
            PlayerProfile.ForgetForTests();
            Assert.IsFalse(PlayerProfile.IsFirstRun,
                "Nach einem Neustart waere der Erstlauf wieder aufgetaucht - " +
                "die Datei wurde also nicht geschrieben oder nicht gelesen.");
        }

        [Test]
        public void Name_wird_gespeichert()
        {
            PlayerProfile.DisplayName = "Driftlab";
            PlayerProfile.ForgetForTests();
            Assert.AreEqual("Driftlab", PlayerProfile.DisplayName);
        }

        [Test]
        public void Ohne_Namen_gibt_es_einen_Platzhalter()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerProfile.DisplayName),
                "Ohne gesetzten Namen darf nicht nichts dastehen.");
        }

        [Test]
        public void Loeschen_raeumt_Profil_und_Laufbahn_auf()
        {
            PlayerProfile.DisplayName = "Weg";
            PlayerProfile.MarkOnboardingDone();
            CareerStats.RecordMatch(true);
            Assert.Greater(CareerStats.Matches, 0, "Aufbau des Tests stimmt nicht.");

            PlayerProfile.DeleteEverything();

            Assert.AreEqual(0, CareerStats.Matches, "Die Laufbahn wurde nicht geloescht.");
            Assert.IsTrue(PlayerProfile.IsFirstRun,
                "Nach dem Loeschen muesste das Spiel wie frisch installiert sein.");
        }

        [Test]
        public void Eine_kaputte_Profildatei_reisst_das_Spiel_nicht_mit()
        {
            System.IO.File.WriteAllText(PlayerProfile.FilePath, "{ das ist kein json ][");
            PlayerProfile.ForgetForTests();

            // Darf nicht werfen, und muss ein brauchbares Profil liefern.
            Assert.IsNotNull(PlayerProfile.Data);
            Assert.IsTrue(PlayerProfile.IsFirstRun,
                "Nach einer kaputten Datei sollte einfach frisch angefangen werden.");
            LogAssert.ignoreFailingMessages = true;
        }

        // --- Erstlauf -------------------------------------------------------

        [Test]
        public void Der_Erstlauf_hat_drei_kurze_Karten()
        {
            Assert.AreEqual(3, FirstRunFlow.Karten.Length,
                "Drei Karten. Mehr liest niemand.");
            foreach (var k in FirstRunFlow.Karten)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(k.Titel), "Karte ohne Titel.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(k.Text), "Karte ohne Text.");
                Assert.Less(k.Text.Length, 320,
                    $"Die Karte '{k.Titel}' ist zu lang - das liest niemand beim ersten Start.");
            }
        }

        [UnityTest]
        public IEnumerator Erstlauf_laeuft_nur_beim_ersten_Mal()
        {
            var wurzel = new VisualElement();

            bool ersterAufruf = FirstRunFlow.ZeigeWennNoetig(wurzel, null);
            Assert.IsTrue(ersterAufruf, "Beim ersten Start muesste der Ablauf kommen.");

            var flow = Object.FindAnyObjectByType<FirstRunFlow>();
            Assert.IsNotNull(flow, "Kein FirstRunFlow angelegt.");
            flow.UeberspringenForTests();
            yield return null;

            bool zweiterAufruf = FirstRunFlow.ZeigeWennNoetig(wurzel, null);
            Assert.IsFalse(zweiterAufruf,
                "Der Erstlauf kam ein zweites Mal - er soll genau einmal laufen.");
        }

        [UnityTest]
        public IEnumerator Ueberspringen_ruft_trotzdem_das_Fertig_zurueck()
        {
            var wurzel = new VisualElement();
            bool fertig = false;
            FirstRunFlow.ZeigeWennNoetig(wurzel, () => fertig = true);

            var flow = Object.FindAnyObjectByType<FirstRunFlow>();
            flow.UeberspringenForTests();
            yield return null;

            Assert.IsTrue(fertig,
                "Wer ueberspringt, muss trotzdem im Spiel landen - " +
                "sonst haengt der Erstlauf den Spieler auf.");
        }

        // --- Zustaende ------------------------------------------------------

        [Test]
        public void Jeder_Zustand_sagt_was_los_ist()
        {
            var faelle = new[]
            {
                UiStates.KeineLaufbahn(() => { }),
                UiStates.Laedt("Karte wird gebaut"),
                UiStates.Fehler("Der Host konnte nicht starten.", () => { }),
                UiStates.Netz(() => { }),
            };

            foreach (var f in faelle)
            {
                int labels = f.Query<Label>().ToList().Count;
                Assert.GreaterOrEqual(labels, 2,
                    $"{f.name}: ein Zustand braucht Titel UND Erklaerung, nicht nur ein Wort.");
            }
        }

        [Test]
        public void Ausser_beim_Laden_hat_jeder_Zustand_einen_Ausweg()
        {
            Assert.IsNotNull(UiStates.KeineLaufbahn(() => { }).Q<Button>("state-action"),
                "Der Leer-Zustand braucht einen Knopf, der weiterhilft.");
            Assert.IsNotNull(UiStates.Fehler("x", () => { }).Q<Button>("state-action"),
                "Ein Fehler ohne Ausweg ist eine Sackgasse.");
            Assert.IsNotNull(UiStates.Netz(() => { }).Q<Button>("state-action"),
                "Verbindung weg ohne Weg zurueck ist eine Sackgasse.");

            Assert.IsNull(UiStates.Laedt("test").Q<Button>("state-action"),
                "Beim Laden gibt es nichts zu tun ausser warten - kein Knopf.");
        }

        [Test]
        public void Fehlertexte_geben_dem_Spieler_nicht_die_Schuld()
        {
            var f = UiStates.Fehler("Der Host konnte nicht starten.", () => { });
            string text = "";
            f.Query<Label>().ForEach(l => text += " " + l.text.ToLowerInvariant());

            foreach (var schuld in new[] { "you did", "your fault", "your mistake", "did it wrong" })
                Assert.IsFalse(text.Contains(schuld),
                    $"Der Fehlertext schiebt dem Spieler die Schuld zu ('{schuld}').");
        }
    }
}
