using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Das neue UI-Toolkit-Menue (MainMenuUi) und der Ladebildschirm
    /// (LoadingOverlay). Prueft, was sich headless pruefen laesst:
    ///  - Der Menue-Baum wird gebaut, das alte Menue bleibt als Rueckfallebene.
    ///  - Modus / Teamgroesse / Schwierigkeit / Empfindlichkeit landen in
    ///    GameSettings und werden gespeichert.
    ///  - Der Ladebildschirm zeigt Fortschritt und verschwindet wieder.
    ///
    /// NICHT pruefbar: wie das Ganze aussieht (Farben, Abstaende, Hover).
    /// </summary>
    public sealed class MenuUiTests
    {
        int _team;
        GameSettings.Level _diff;
        float _sens;
        GameSettings.Mode _mode;
        float _vol;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // echte Menue-Einstellungen des Nutzers merken und spaeter zuruecksetzen
            _team = GameSettings.TeamSize;
            _diff = GameSettings.Difficulty;
            _sens = GameSettings.MouseSensitivity;
            _mode = GameSettings.GameMode;
            _vol = GameSettings.SfxVolume;

            yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettings.TeamSize = _team;
            GameSettings.Difficulty = _diff;
            GameSettings.MouseSensitivity = _sens;
            GameSettings.GameMode = _mode;
            GameSettings.SfxVolume = _vol;
            GameSettings.Save();

            if (LoadingOverlay.Instance != null)
                LoadingOverlay.Instance.ForceHideForTests();
            yield return null;
        }

        static MainMenuUi Ui() => Object.FindAnyObjectByType<MainMenuUi>();

        static IEnumerator WaitBuilt(MainMenuUi ui)
        {
            for (int i = 0; i < 30 && (ui == null || !ui.IsBuiltForTests); i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Menue_baut_den_Baum_und_schaltet_das_alte_stumm()
        {
            var ui = Ui();
            Assert.IsNotNull(ui, "Kein MainMenuUi in der Menue-Szene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<UIDocument>(), "Kein UIDocument.");

            yield return WaitBuilt(ui);

            Assert.IsTrue(ui.IsBuiltForTests, "Der Menue-Baum wurde nicht gebaut.");
            Assert.Greater(ui.RootForTests.childCount, 0, "Die Menue-Wurzel ist leer.");
            Assert.IsTrue(MainMenu.Suppressed, "Das alte IMGUI-Menue wurde nicht stummgeschaltet.");
        }

        [UnityTest]
        public IEnumerator Credits_lassen_Kopf_und_Fuss_bei_drei_Fenstergroessen_lesbar()
        {
            yield return MenuUiHarness.LadeMenue();
            var ui = Ui();
            var root = ui.RootForTests;
            var oldWidth = root.style.width;
            var oldHeight = root.style.height;
            var oldGrow = root.style.flexGrow;
            try
            {
                root.style.flexGrow = 0f;
                foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1600, 784), new Vector2(1920, 1080) })
                {
                    root.style.width = size.x;
                    root.style.height = size.y;
                    Assert.IsTrue(ui.ClickForTests("nav-quellen"));
                    yield return new WaitForSecondsRealtime(1.2f);
                    var header = root.Q("menu-header").worldBound;
                    var title = root.Q<Label>("menu-title").worldBound;
                    var tagline = root.Q<Label>("menu-tagline").worldBound;
                    var footer = root.Q("menu-footer");
                    Assert.Greater(title.height, 40f, "Der Titel darf nicht zusammengequetscht werden: " + size);
                    Assert.GreaterOrEqual(tagline.yMin, title.yMax, "Unterzeile ueberlappt den Titel: " + size);
                    Assert.LessOrEqual(tagline.yMax, header.yMax, "Unterzeile ragt aus der Kopfzeile: " + size);
                    // Der unsichtbare untere Innenabstand darf sich mit der
                    // Maus-Parallaxe bewegen. Entscheidend ist sichtbarer Text.
                    foreach (var label in footer.Query<Label>().ToList())
                        Assert.LessOrEqual(label.worldBound.yMax, root.worldBound.yMax,
                            "Fusszeilentext ragt aus dem Fenster: " + size);
                    var credits = root.Q<ScrollView>("credits-scroll");
                    Assert.Greater(credits.contentViewport.worldBound.height, 100f, "Credits haben keinen nutzbaren Ausschnitt.");
                }
            }
            finally
            {
                root.style.width = oldWidth;
                root.style.height = oldHeight;
                root.style.flexGrow = oldGrow;
            }
        }

        [UnityTest]
        public IEnumerator Altes_Menue_bleibt_als_Rueckfallebene_erhalten()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenu>(),
                "Das alte IMGUI-Menue darf nicht geloescht werden.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Modus_Schalter_schreibt_und_speichert_GameSettings()
        {
            var ui = Ui();
            yield return WaitBuilt(ui);

            GameSettings.GameMode = GameSettings.Mode.Ausscheiden;

            Assert.IsTrue(ui.ClickForTests("seg-modus-1"), "Knopf 'Bombe' nicht gefunden.");
            Assert.AreEqual(GameSettings.Mode.Bombe, GameSettings.GameMode);
            Assert.AreEqual(1, PlayerPrefs.GetInt("infront.mode", -1), "Modus wurde nicht gespeichert.");

            ui.ClickForTests("seg-modus-0");
            Assert.AreEqual(GameSettings.Mode.Ausscheiden, GameSettings.GameMode);
        }

        [UnityTest]
        public IEnumerator Teamgroesse_und_Schwierigkeit_landen_in_GameSettings()
        {
            var ui = Ui();
            yield return WaitBuilt(ui);

            Assert.IsTrue(ui.ClickForTests("seg-team-2"), "Teamgroessen-Knopf nicht gefunden.");
            Assert.AreEqual(4, GameSettings.TeamSize, "Index 2 muss Teamgroesse 4 bedeuten.");

            Assert.IsTrue(ui.ClickForTests("seg-diff-2"), "Schwierigkeits-Knopf nicht gefunden.");
            Assert.AreEqual(GameSettings.Level.Schwer, GameSettings.Difficulty);
        }

        [UnityTest]
        public IEnumerator Bild_Schalter_liegt_jetzt_auf_der_Einstellungsseite()
        {
            var ui = Ui();
            yield return WaitBuilt(ui);

            var old = GameSettings.GraphicsQuality;

            ui.ClickForTests("nav-einstellungen");
            yield return null;

            Assert.IsTrue(ui.ClickForTests("seg-grafik-1"), "Bild-Schalter 'SCHLICHT' nicht gefunden.");
            Assert.AreEqual(GameSettings.Graphics.Schlicht, GameSettings.GraphicsQuality);

            ui.ClickForTests("seg-grafik-0");
            GameSettings.GraphicsQuality = old;
            GameSettings.Save();
        }

        [UnityTest]
        public IEnumerator Einstellungsseite_setzt_die_Empfindlichkeit()
        {
            var ui = Ui();
            yield return WaitBuilt(ui);

            ui.ClickForTests("nav-einstellungen");
            yield return null;

            ui.SetSensitivityForTests(0.2f);
            Assert.AreEqual(0.2f, GameSettings.MouseSensitivity, 0.001f);
        }

        [UnityTest]
        public IEnumerator Einstellungsseite_setzt_die_Lautstaerke()
        {
            var ui = Ui();
            yield return WaitBuilt(ui);

            ui.ClickForTests("nav-einstellungen");
            yield return null;

            ui.SetVolumeForTests(0.3f);
            Assert.AreEqual(0.3f, GameSettings.SfxVolume, 0.001f);
            Assert.AreEqual(0.3f, PlayerPrefs.GetFloat("infront.sfxVolume", -1f), 0.001f,
                "Die Lautstaerke wurde nicht gespeichert.");
        }

        [UnityTest]
        public IEnumerator Ladebildschirm_zeigt_Fortschritt_und_verschwindet()
        {
            var overlay = LoadingOverlay.Instance;
            Assert.IsNotNull(overlay, "Kein LoadingOverlay - GameFlow-Bootstrap fehlt?");

            for (int i = 0; i < 60 && !overlay.ReadyForTests; i++) yield return null;
            Assert.IsTrue(overlay.ReadyForTests, "Der Ladebildschirm wurde nicht aufgebaut.");

            overlay.Begin("BOMBE");
            yield return null;
            Assert.IsTrue(overlay.IsVisibleForTests, "Der Ladebildschirm wird nicht angezeigt.");

            overlay.SetProgress(0.5f);
            overlay.SnapProgressForTests();
            Assert.AreEqual(0.5f, overlay.ShownProgressForTests, 0.01f);

            overlay.ForceHideForTests();
            yield return null;
            Assert.IsFalse(overlay.IsVisibleForTests, "Der Ladebildschirm bleibt sichtbar.");
        }
    }
}
