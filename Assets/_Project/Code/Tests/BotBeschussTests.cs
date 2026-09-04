using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Wie ein Bot darauf reagiert, dass auf ihn geschossen wird.
    ///
    /// Der Zustand davor: gar nicht. Man konnte einem Bot in den Ruecken
    /// schiessen, und er lief seine Runde weiter, bis er zufaellig jemanden
    /// SAH. Das war der groesste Rest von "das ist ein Programm, kein Gegner".
    ///
    /// NICHT pruefbar: ob es sich richtig anfuehlt. Pruefbar: dass er den Ort
    /// des Schuetzen uebernimmt, dass ein richtiger Treffer ihn in Deckung
    /// schickt, dass ein Streifschuss das nicht tut, und dass er dabei
    /// Abstand zur Gefahr gewinnt statt hineinzulaufen.
    /// </summary>
    public sealed class BotBeschussTests
    {
        BotBrain _bot;

        IEnumerator HoleBot()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((p, m) => { });

            _bot = Object.FindAnyObjectByType<BotBrain>();
            Assert.IsNotNull(_bot, "Kein Bot in der Arena.");
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(_bot, new Vector3(0f, 0f, 0f), out _),
                "Der Bot liess sich nicht auf das NavMesh setzen.");
            MatchTestHarness.Unfreeze(_bot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ein_Treffer_verraet_woher_geschossen_wurde()
        {
            yield return HoleBot();

            var von = _bot.transform.position - _bot.transform.forward * 12f;
            _bot.ServerBeschussForTests(30, von);
            yield return null;

            Assert.IsTrue(_bot.WurdeBeschossenForTests,
                "Der Bot muesste gemerkt haben, dass auf ihn geschossen wurde.");
            Assert.AreEqual(von, _bot.BeschussAusForTests,
                "Er muesste sich merken, WOHER der Schuss kam - auch wenn er den "
                + "Schuetzen nicht sieht. Wer angeschossen wird, weiss das auch "
                + "in Wirklichkeit ungefaehr.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Ein_richtiger_Treffer_schickt_ihn_in_Deckung()
        {
            yield return HoleBot();

            Assert.IsFalse(_bot.InDeckung, "Vorher duerfte er nicht in Deckung wollen.");

            _bot.ServerBeschussForTests(35, _bot.transform.position + Vector3.forward * 10f);
            yield return null;

            Assert.IsTrue(_bot.InDeckung,
                "Nach einem richtigen Treffer muesste der Bot in Deckung wollen.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Ein_Streifschuss_laesst_ihn_stehen()
        {
            yield return HoleBot();

            _bot.ServerBeschussForTests(3, _bot.transform.position + Vector3.forward * 10f);
            yield return null;

            Assert.IsFalse(_bot.InDeckung,
                "Ein Streifschuss darf ihn nicht fluechten lassen - sonst zuckt er bei "
                + "jedem Kratzer zurueck und das Gefecht zerfaellt.");
            Assert.IsTrue(_bot.WurdeBeschossenForTests,
                "Gemerkt haben muesste er es trotzdem.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Er_geht_von_der_Gefahr_weg_nicht_darauf_zu()
        {
            yield return HoleBot();
            MatchTestHarness.ClearArena();
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(_bot, new Vector3(0f, 0f, 0f), out var start),
                "Der Bot liess sich nicht setzen.");
            MatchTestHarness.Unfreeze(_bot);
            yield return null;

            Vector3 gefahr = start + new Vector3(0f, 0f, 10f);
            float vorher = Vector3.Distance(start, gefahr);

            _bot.ServerBeschussForTests(40, gefahr);

            // Ein paar Sekunden laufen lassen.
            for (int i = 0; i < 180; i++) yield return null;

            float nachher = Vector3.Distance(_bot.transform.position, gefahr);
            Assert.Greater(nachher, vorher + 0.5f,
                $"Der Bot muesste sich von der Gefahr entfernen (vorher {vorher:F1} m, "
                + $"nachher {nachher:F1} m).");

            yield return MatchTestHarness.Teardown();
        }
    }
}
