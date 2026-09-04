using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Zugaenglichkeit: Farbmodus, Groesse der Anzeige, weniger Bewegung,
    /// Halten oder Umschalten, Fadenkreuz.
    ///
    /// NICHT pruefbar: ob es jemandem wirklich hilft - das kann nur ein Mensch
    /// sagen, der es braucht. Pruefbar: dass die Einstellungen ankommen, dass
    /// sie das Speichern ueberleben und dass die Bedeutungsfarben sich
    /// tatsaechlich unterscheiden.
    /// </summary>
    public sealed class ZugaenglichkeitTests
    {
        GameSettings.Farbmodus _modeVorher;
        float _scaleVorher, _crossVorher;
        bool _motionVorher, _aimVorher, _crouchVorher, _sprintVorher;

        [SetUp]
        public void SetUp()
        {
            _modeVorher = GameSettings.ColorMode;
            _scaleVorher = GameSettings.UiScale;
            _crossVorher = GameSettings.CrosshairScale;
            _motionVorher = GameSettings.ReduceMotion;
            _aimVorher = GameSettings.ToggleAim;
            _crouchVorher = GameSettings.ToggleCrouch;
            _sprintVorher = GameSettings.ToggleSprint;
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.ColorMode = _modeVorher;
            GameSettings.UiScale = _scaleVorher;
            GameSettings.CrosshairScale = _crossVorher;
            GameSettings.ReduceMotion = _motionVorher;
            GameSettings.ToggleAim = _aimVorher;
            GameSettings.ToggleCrouch = _crouchVorher;
            GameSettings.ToggleSprint = _sprintVorher;
            GameSettings.Save();
            Zugaenglichkeit.UiGroesseAnwenden();
        }

        // --- Farben ---------------------------------------------------------

        [Test]
        public void Standard_liefert_genau_die_alten_Farben()
        {
            GameSettings.ColorMode = GameSettings.Farbmodus.Standard;
            Assert.AreEqual(UiTheme.Good, UiTheme.Gut);
            Assert.AreEqual(UiTheme.Warn, UiTheme.Mittel);
            Assert.AreEqual(UiTheme.Bad, UiTheme.Schlecht);
        }

        [Test]
        public void Bei_Rot_Gruen_Schwaeche_ist_kein_Gruen_mehr_im_Spiel()
        {
            GameSettings.ColorMode = GameSettings.Farbmodus.RotGruen;

            // "Gut" darf nicht mehr ueberwiegend gruen sein - sonst haette der
            // Modus nichts geaendert.
            var gut = UiTheme.Gut;
            Assert.IsFalse(gut.g > gut.r && gut.g > gut.b,
                "Im Rot-Gruen-Modus darf 'Leben in Ordnung' nicht gruen sein.");
        }

        [Test]
        public void Die_drei_Lebensfarben_sind_in_jedem_Modus_unterscheidbar()
        {
            foreach (GameSettings.Farbmodus m in System.Enum.GetValues(typeof(GameSettings.Farbmodus)))
            {
                GameSettings.ColorMode = m;
                var a = UiTheme.Gut;
                var b = UiTheme.Mittel;
                var c = UiTheme.Schlecht;

                // Helligkeit als letzte Rueckfallebene: wer gar keine Farben
                // sieht, muss die drei wenigstens am Grauwert auseinanderhalten.
                float la = 0.2126f * a.r + 0.7152f * a.g + 0.0722f * a.b;
                float lb = 0.2126f * b.r + 0.7152f * b.g + 0.0722f * b.b;
                float lc = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

                Assert.Greater(Mathf.Abs(la - lc), 0.15f,
                    $"Im Modus {m} sind 'in Ordnung' und 'kritisch' zu aehnlich hell.");
                Assert.Greater(Mathf.Abs(la - lb) + Mathf.Abs(lb - lc), 0.2f,
                    $"Im Modus {m} liegen alle drei Lebensfarben zu dicht beieinander.");
            }
        }

        // --- Weniger Bewegung ------------------------------------------------

        [Test]
        public void Weniger_Bewegung_daempft_wirklich()
        {
            GameSettings.ReduceMotion = false;
            Assert.AreEqual(1f, GameSettings.BewegungsFaktor, 0.001f);

            GameSettings.ReduceMotion = true;
            Assert.Less(GameSettings.BewegungsFaktor, 0.3f,
                "'Weniger Bewegung' muesste die Kamerabewegung deutlich daempfen.");
            Assert.Greater(GameSettings.BewegungsFaktor, 0f,
                "Ganz auf null waere es kein Atem mehr - ein Rest soll bleiben.");
        }

        [UnityTest]
        public IEnumerator Weniger_Bewegung_erreicht_die_Atmung()
        {
            var go = new GameObject("AtemTest");
            var atem = go.AddComponent<Breathing>();
            yield return null;

            GameSettings.ReduceMotion = false;
            for (int i = 0; i < 30; i++) yield return null;
            float voll = 0f;
            for (int i = 0; i < 40; i++) { yield return null; voll = Mathf.Max(voll, atem.Offset.magnitude); }

            GameSettings.ReduceMotion = true;
            for (int i = 0; i < 30; i++) yield return null;
            float leise = 0f;
            for (int i = 0; i < 40; i++) { yield return null; leise = Mathf.Max(leise, atem.Offset.magnitude); }

            Assert.Less(leise, voll * 0.5f,
                $"Der Atem-Schwenk muesste deutlich kleiner werden (voll {voll:F3}, leise {leise:F3}).");

            Object.DestroyImmediate(go);
        }

        // --- Groesse der Anzeige ---------------------------------------------

        [Test]
        public void Die_Groesse_der_Anzeige_kommt_am_Panel_an()
        {
            GameSettings.UiScale = 1.4f;
            Zugaenglichkeit.UiGroesseAnwenden();
            Assert.AreEqual(1.4f, Zugaenglichkeit.AktuelleGroesseForTests, 0.01f,
                "Die eingestellte Groesse muesste am gemeinsamen Panel ankommen.");

            GameSettings.UiScale = 1f;
            Zugaenglichkeit.UiGroesseAnwenden();
            Assert.AreEqual(1f, Zugaenglichkeit.AktuelleGroesseForTests, 0.01f);
        }

        [Test]
        public void Die_Groesse_bleibt_in_vernuenftigen_Grenzen()
        {
            GameSettings.UiScale = 99f;
            GameSettings.Save();
            GameSettings.Load();
            Assert.LessOrEqual(GameSettings.UiScale, 1.6f,
                "Eine unsinnig grosse Anzeige muesste beim Laden begrenzt werden.");

            GameSettings.UiScale = 0.01f;
            GameSettings.Save();
            GameSettings.Load();
            Assert.GreaterOrEqual(GameSettings.UiScale, 0.8f);
        }

        // --- Speichern --------------------------------------------------------

        [Test]
        public void Alles_ueberlebt_das_Speichern()
        {
            GameSettings.ColorMode = GameSettings.Farbmodus.BlauGelb;
            GameSettings.ReduceMotion = true;
            GameSettings.ToggleAim = true;
            GameSettings.ToggleCrouch = true;
            GameSettings.ToggleSprint = true;
            GameSettings.CrosshairScale = 1.7f;
            GameSettings.UiScale = 1.25f;
            GameSettings.Save();

            GameSettings.ColorMode = GameSettings.Farbmodus.Standard;
            GameSettings.ReduceMotion = false;
            GameSettings.ToggleAim = false;
            GameSettings.CrosshairScale = 1f;
            GameSettings.Load();

            Assert.AreEqual(GameSettings.Farbmodus.BlauGelb, GameSettings.ColorMode);
            Assert.IsTrue(GameSettings.ReduceMotion);
            Assert.IsTrue(GameSettings.ToggleAim);
            Assert.IsTrue(GameSettings.ToggleCrouch);
            Assert.IsTrue(GameSettings.ToggleSprint);
            Assert.AreEqual(1.7f, GameSettings.CrosshairScale, 0.01f);
            Assert.AreEqual(1.25f, GameSettings.UiScale, 0.01f);
        }

        // --- Menue ------------------------------------------------------------

        [UnityTest]
        public IEnumerator Die_Seite_steht_im_Menue_und_hat_alle_Schalter()
        {
            yield return MenuUiHarness.OeffneSeite("ACCESSIBILITY");

            foreach (var name in new[] { "slider-uiscale", "slider-crosshair", "seg-farbe",
                                         "seg-motion", "seg-toggleaim", "seg-togglecrouch",
                                         "seg-togglesprint" })
                Assert.IsNotNull(MenuUiHarness.Finde(name),
                    "Auf der Zugaenglichkeits-Seite fehlt: " + name);
        }
    }
}
