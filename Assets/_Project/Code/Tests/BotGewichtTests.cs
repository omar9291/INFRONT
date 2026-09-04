using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Das Gewicht der Bots (<see cref="BotLocomotion"/>).
    ///
    /// Der Fehler, den diese Tests festhalten: seit dem Gewichts-Umbau hatte
    /// der Spieler Anlauf, Bremsweg und Nachteile beim Bluten - der Bot lief
    /// weiter mit einer festen Geschwindigkeit. Er glitt, und ein gleitender
    /// Gegner macht jede Grafik-Arbeit wieder kaputt.
    ///
    /// NICHT pruefbar: ob es sich richtig anfuehlt. Pruefbar: dass das Tempo
    /// Zeit braucht, dass der Kampf langsamer ist als das Gehen, und dass ein
    /// rennender Bot nicht schiessen darf.
    /// </summary>
    public sealed class BotGewichtTests
    {
        GameObject _go;
        BotLocomotion _loco;
        NavMeshAgent _agent;
        BotStats _stats;

        [SetUp]
        public void SetUp()
        {
            _stats = ScriptableObject.CreateInstance<BotStats>();
            _stats.MoveSpeed = 4.5f;

            _go = new GameObject("BotLoco");
            _agent = _go.AddComponent<NavMeshAgent>();
            _agent.enabled = true;
            _loco = _go.AddComponent<BotLocomotion>();
            _loco.SetStats(_stats);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_stats != null) Object.DestroyImmediate(_stats);
        }

        void Treibe(float sekunden, float schritt = 0.02f)
        {
            for (float t = 0f; t < sekunden; t += schritt) _loco.Schritt(schritt);
        }

        // --- Anlauf ---------------------------------------------------------

        [Test]
        public void Rennen_braucht_Anlauf()
        {
            _loco.SetzeAbsicht(BotLocomotion.Absicht.Rennen);
            _loco.Schritt(0.05f);
            float frueh = _agent.speed;

            Treibe(2f);
            float voll = _agent.speed;

            Assert.Less(frueh, voll * 0.8f,
                "Direkt nach dem Losrennen duerfte der Bot noch nicht fast auf vollem "
                + "Tempo sein - genau dieses sofortige Vollgas war das Gleiten.");
            Assert.Greater(voll, _stats.MoveSpeed * 1.4f,
                "Nach zwei Sekunden Anlauf muesste er deutlich ueber Gehtempo liegen.");
        }

        [Test]
        public void Nach_dem_Rennen_faellt_das_Tempo_wieder()
        {
            _loco.SetzeAbsicht(BotLocomotion.Absicht.Rennen);
            Treibe(2f);
            float voll = _agent.speed;

            _loco.SetzeAbsicht(BotLocomotion.Absicht.Gehen);
            Treibe(1.5f);

            Assert.Less(_agent.speed, voll * 0.75f,
                "Beim Wechsel auf Gehen muesste das Tempo wieder fallen.");
            Assert.AreEqual(_stats.MoveSpeed, _agent.speed, 0.2f,
                "Im Gehen muesste wieder das Grundtempo aus den BotStats stehen.");
        }

        [Test]
        public void Im_Kampf_ist_der_Bot_langsamer_als_beim_Gehen()
        {
            _loco.SetzeAbsicht(BotLocomotion.Absicht.Gehen);
            Treibe(0.5f);
            float gehen = _agent.speed;

            _loco.SetzeAbsicht(BotLocomotion.Absicht.Kampf);
            Treibe(0.5f);

            Assert.Less(_agent.speed, gehen,
                "Mit der Waffe im Anschlag muesste der Bot langsamer sein als beim Gehen.");
        }

        [Test]
        public void Stehen_heisst_wirklich_stehen()
        {
            _loco.SetzeAbsicht(BotLocomotion.Absicht.Stehen);
            Treibe(0.3f);

            Assert.AreEqual(0f, _agent.speed, 0.001f, "Stehen muesste Tempo 0 bedeuten.");
            Assert.IsTrue(_loco.Angehalten, "Der Bot muesste als angehalten gelten.");
        }

        [Test]
        public void Schwierigkeit_wirkt_weiter_aufs_Tempo()
        {
            _stats.MoveSpeed = 6f;
            _loco.SetStats(_stats);
            _loco.SetzeAbsicht(BotLocomotion.Absicht.Gehen);
            Treibe(0.3f);

            Assert.AreEqual(6f, _agent.speed, 0.2f,
                "Das Grundtempo muss weiter aus den BotStats kommen, sonst waeren die "
                + "Schwierigkeitsstufen wirkungslos.");
        }

        // --- Schiessen ------------------------------------------------------

        [Test]
        public void Wer_rennt_darf_nicht_schiessen()
        {
            _loco.SetTempoForTests(_loco.RennSchwelleForTests + 1f);
            Assert.IsTrue(_loco.Rennt, "Bei diesem Tempo muesste er als rennend gelten.");
            Assert.IsFalse(_loco.DarfSchiessen,
                "Ein rennender Bot darf nicht feuern - im vollen Lauf zu treffen war "
                + "der auffaelligste unrealistische Rest.");
        }

        [Test]
        public void Wer_geht_darf_schiessen()
        {
            _loco.SetTempoForTests(1f);
            Assert.IsFalse(_loco.Rennt);
            Assert.IsTrue(_loco.DarfSchiessen, "Im Gehen muesste der Bot feuern duerfen.");
        }

        [Test]
        public void Bewegung_kostet_Genauigkeit()
        {
            _loco.SetTempoForTests(3f);
            float inBewegung = _loco.StreuungsMalus;

            _loco.SetzeAbsicht(BotLocomotion.Absicht.Stehen);
            Treibe(1.5f);
            float imStand = _loco.StreuungsMalus;

            Assert.Greater(inBewegung, imStand + 1f,
                "In Bewegung muesste der Bot spuerbar schlechter zielen als im Stand.");
            Assert.AreEqual(0f, imStand, 0.4f,
                "Wer lange genug ruhig steht, sollte keinen Bewegungsmalus mehr haben.");
        }

        [Test]
        public void Ruhe_stellt_sich_erst_nach_einer_Weile_ein()
        {
            _loco.SetTempoForTests(4f);
            Assert.Less(_loco.Ruhe01, 0.2f, "In Bewegung duerfte kaum Ruhe da sein.");

            _loco.SetzeAbsicht(BotLocomotion.Absicht.Stehen);
            _loco.Schritt(0.05f);
            Assert.Less(_loco.Ruhe01, 0.5f,
                "Einen Sekundenbruchteil nach dem Stehenbleiben darf die Waffe noch "
                + "nicht ruhig liegen.");

            Treibe(1.5f);
            Assert.Greater(_loco.Ruhe01, 0.9f, "Nach einer Weile muesste er ruhig stehen.");
        }

        // --- Verdrahtung ----------------------------------------------------

        [UnityTest]
        public IEnumerator Bots_in_der_Arena_haben_das_Gewicht_dabei()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var bots = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None);
            Assert.Greater(bots.Length, 0, "In der Arena muesste mindestens ein Bot stehen.");
            foreach (var bot in bots)
                Assert.IsNotNull(bot.GetComponent<BotLocomotion>(),
                    "Am Bot '" + bot.name + "' fehlt BotLocomotion - dann gleitet er "
                    + "im fertigen Spiel weiter.");

            yield return MatchTestHarness.Teardown();
        }

    }
}
