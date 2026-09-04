using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Gemeinsame Handgriffe fuer Menue-Tests: Szene laden, auf den fertigen
    /// Baum warten, eine Seite oeffnen, ein Element suchen.
    ///
    /// Warum eigene Datei: den Baum "von Hand" zu finden ist in mehreren Tests
    /// gleich, und dabei ist hier schon einmal etwas schiefgegangen - es gibt
    /// ZWEI UIDocument in der Menue-Szene (Menue und Ladebildschirm), und
    /// FindAnyObjectByType erwischt mal das eine, mal das andere. Deshalb wird
    /// hier immer ueber MainMenuUi gegangen.
    /// </summary>
    public static class MenuUiHarness
    {
        public static MainMenuUi Ui() => Object.FindAnyObjectByType<MainMenuUi>();

        /// <summary>Menue-Szene laden und warten, bis der Baum steht.</summary>
        public static IEnumerator LadeMenue()
        {
            if (SceneManager.GetActiveScene().name != GameFlow.MenuScene)
            {
                yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
                yield return null;
            }

            var ui = Ui();
            for (int i = 0; i < 120 && (ui == null || !ui.IsBuiltForTests); i++)
            {
                yield return null;
                if (ui == null) ui = Ui();
            }

            Assert.IsNotNull(ui, "Kein MainMenuUi in der Menue-Szene.");
            Assert.IsTrue(ui.IsBuiltForTests, "Der Menue-Baum wurde nicht fertig gebaut.");
        }

        /// <summary>Die Wurzel des Menues - NICHT ueber FindAnyObjectByType&lt;UIDocument&gt;.</summary>
        public static VisualElement Wurzel()
        {
            var ui = Ui();
            return ui != null ? ui.RootForTests : null;
        }

        /// <summary>Ein Element im Menue-Baum nach Namen suchen.</summary>
        public static VisualElement Finde(string name)
        {
            var wurzel = Wurzel();
            return wurzel?.Q(name);
        }

        /// <summary>
        /// Eine Seite ueber die Navigation links oeffnen. Der Text muss genau
        /// dem Knopf entsprechen (z. B. "ZUGAENGLICHKEIT").
        /// </summary>
        public static IEnumerator OeffneSeite(string knopfText)
        {
            yield return LadeMenue();

            var wurzel = Wurzel();
            var knopf = wurzel.Query<Button>().ToList()
                              .FirstOrDefault(b => b.text == knopfText);
            Assert.IsNotNull(knopf,
                $"Kein Navigations-Knopf mit der Aufschrift '{knopfText}'. Vorhanden: "
                + string.Join(", ", wurzel.Query<Button>().ToList().Select(b => b.text)));

            using (var ev = NavigationSubmitEvent.GetPooled())
            {
                ev.target = knopf;
                knopf.SendEvent(ev);
            }

            // Der Seitenwechsel gleitet herein und baut ueber ein paar Frames auf.
            for (int i = 0; i < 20; i++) yield return null;
        }
    }
}
