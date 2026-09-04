using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Der Funk zwischen den Bots.
    ///
    /// Der Zustand davor: die Ansagen ("Feind gesichtet!") waren reine
    /// Anzeige - Text im Meldungsfenster, sonst nichts. Fuenf Bots liefen als
    /// fuenf Einzelgaenger durch die Halle und kamen einer nach dem anderen an.
    ///
    /// Die Grenzen sind der eigentliche Inhalt: ohne sie waere es
    /// Gedankenuebertragung, und dann kommt das ganze Team auf einmal, egal wo
    /// es stand. Genau das wird hier festgehalten.
    /// </summary>
    public sealed class BotFunkTests
    {
        [SetUp]
        public void SetUp() => BotFunk.Reset();

        [TearDown]
        public void TearDown() => BotFunk.Reset();

        static readonly Vector3 Melder = new Vector3(0f, 0f, 0f);
        static readonly Vector3 Feind = new Vector3(20f, 0f, 5f);

        [UnityTest]
        public IEnumerator Ein_Funkspruch_kommt_bei_den_eigenen_Leuten_an()
        {
            BotFunk.ServerFeindGesichtet(Team.Alpha, Melder, Feind);

            // Direkt danach ist er noch nicht da - jemand muss den Satz erst sagen.
            Assert.IsFalse(BotFunk.TryEmpfangen(Team.Alpha, new Vector3(10f, 0f, 0f), out _),
                "Sofort darf noch nichts angekommen sein.");

            yield return new WaitForSeconds(BotFunk.Verzoegerung + 0.15f);

            Assert.IsTrue(BotFunk.TryEmpfangen(Team.Alpha, new Vector3(10f, 0f, 0f), out var ort),
                "Nach der kurzen Verzoegerung muesste die Meldung ankommen.");
            Assert.AreEqual(Feind, ort, "Der gemeldete Ort muesste stimmen.");
        }

        [UnityTest]
        public IEnumerator Der_Gegner_hoert_nicht_mit()
        {
            BotFunk.ServerFeindGesichtet(Team.Alpha, Melder, Feind);
            yield return new WaitForSeconds(BotFunk.Verzoegerung + 0.15f);

            Assert.IsFalse(BotFunk.TryEmpfangen(Team.Bravo, new Vector3(10f, 0f, 0f), out _),
                "Das andere Team darf den Funk nicht empfangen.");
        }

        [UnityTest]
        public IEnumerator Zu_weit_weg_hoert_man_nichts()
        {
            BotFunk.ServerFeindGesichtet(Team.Alpha, Melder, Feind);
            yield return new WaitForSeconds(BotFunk.Verzoegerung + 0.15f);

            var weit = Melder + Vector3.right * (BotFunk.Reichweite + 15f);
            Assert.IsFalse(BotFunk.TryEmpfangen(Team.Alpha, weit, out _),
                "Aus dem anderen Ende der Halle darf nichts ankommen - sonst weiss "
                + "das ganze Team alles, egal wo es steht.");

            var nah = Melder + Vector3.right * (BotFunk.Reichweite - 10f);
            Assert.IsTrue(BotFunk.TryEmpfangen(Team.Alpha, nah, out _),
                "In Reichweite muesste es ankommen.");
        }

        [UnityTest]
        public IEnumerator Eine_alte_Meldung_ist_wertlos()
        {
            BotFunk.ServerFeindGesichtet(Team.Alpha, Melder, Feind);
            yield return new WaitForSeconds(BotFunk.Haltbarkeit + 0.4f);

            Assert.IsFalse(BotFunk.TryEmpfangen(Team.Alpha, new Vector3(10f, 0f, 0f), out _),
                "Nach ein paar Sekunden steht der Gegner nicht mehr dort - die Meldung "
                + "muesste verfallen.");
            Assert.AreEqual(0, BotFunk.AnzahlForTests,
                "Verfallene Meldungen muessten auch weggeraeumt werden.");
        }

        [UnityTest]
        public IEnumerator Der_Melder_funkt_sich_nicht_selbst_an()
        {
            BotFunk.ServerFeindGesichtet(Team.Alpha, Melder, Feind);
            yield return new WaitForSeconds(BotFunk.Verzoegerung + 0.15f);

            Assert.IsFalse(BotFunk.TryEmpfangen(Team.Alpha, Melder, out _),
                "Wer selbst gemeldet hat, braucht seine eigene Meldung nicht.");
        }

        // --- Auffaechern ------------------------------------------------------

        [UnityTest]
        public IEnumerator Bots_eines_Teams_starten_nicht_alle_am_selben_Punkt()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var bots = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None);
            Assert.Greater(bots.Length, 1, "Fuer diesen Test braucht es mehrere Bots.");

            // Pro Team die Vorrueck-Punkte einsammeln.
            var proTeam = new System.Collections.Generic.Dictionary<int,
                              System.Collections.Generic.List<Vector3>>();
            foreach (var b in bots)
            {
                var tm = b.GetComponent<TeamMember>();
                if (tm == null) continue;
                if (!proTeam.TryGetValue(tm.TeamId, out var liste))
                    proTeam[tm.TeamId] = liste = new System.Collections.Generic.List<Vector3>();
                liste.Add(b.BaseAnchorForTests);
            }

            foreach (var paar in proTeam)
            {
                var liste = paar.Value;
                if (liste.Count < 2) continue;

                float groessterAbstand = 0f;
                for (int a = 0; a < liste.Count; a++)
                    for (int b2 = a + 1; b2 < liste.Count; b2++)
                        groessterAbstand = Mathf.Max(groessterAbstand,
                                                     Vector3.Distance(liste[a], liste[b2]));

                Assert.Greater(groessterAbstand, 6f,
                    $"Die Bots von Team {paar.Key} ruecken alle zum selben Punkt vor "
                    + $"(groesster Abstand nur {groessterAbstand:F1} m). Dann laufen sie "
                    + "denselben Weg und treffen einer nach dem anderen ein.");
            }

            yield return MatchTestHarness.Teardown();
        }

        [Test]
        public void Derselbe_Satz_geht_im_Team_nur_einmal_heraus()
        {
            Assert.IsTrue(BotFunk.DarfRufen(Team.Alpha, "Enemy spotted!"),
                "Der erste Ruf muss durchkommen.");
            Assert.IsFalse(BotFunk.DarfRufen(Team.Alpha, "Enemy spotted!"),
                "Fuenf Bots duerfen nicht fuenfmal dasselbe in die Liste schreiben.");
        }

        [Test]
        public void Ein_anderer_Satz_und_das_andere_Team_duerfen_trotzdem()
        {
            Assert.IsTrue(BotFunk.DarfRufen(Team.Alpha, "Enemy spotted!"));
            Assert.IsTrue(BotFunk.DarfRufen(Team.Alpha, "Need help!"),
                "Eine andere Ansage ist keine Wiederholung.");
            Assert.IsTrue(BotFunk.DarfRufen(Team.Bravo, "Enemy spotted!"),
                "Die Sperre gilt je Team, nicht fuer die ganze Karte.");
        }

    }
}
