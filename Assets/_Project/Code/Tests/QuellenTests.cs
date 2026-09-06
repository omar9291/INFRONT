using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Die Quellen-Seite im Hauptmenue nennt Werke, Urheber und Quellen.
    /// Lizenzpflichten werden je Eintrag im Quellenkatalog dokumentiert.
    ///
    /// NICHT pruefbar: ob die Seite gut aussieht. Geprueft wird, dass es sie
    /// gibt und dass die Quellen, bei denen es rechtlich zaehlt, wirklich
    /// darauf stehen. Faellt eine heraus, schlaegt der Test an.
    /// </summary>
    public sealed class QuellenTests
    {
        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Ein vorheriger Test kann ein laufendes Spiel hinterlassen haben.
            // Ohne Aufraeumen baut die Menue-Szene ihr UI nicht auf.
            yield return MatchTestHarness.Teardown();
            yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Das Menue wird verzoegert aufgebaut. Eine feste Bildzahl reicht
        /// unter voller Testlast nicht - deshalb auf den fertigen Baum warten.
        /// Genau daran sind diese Tests im vollen Lauf zuerst gescheitert,
        /// obwohl sie einzeln gruen waren.
        /// </summary>
        static IEnumerator WarteAufMenue()
        {
            for (int i = 0; i < 120; i++)
            {
                var ui = Object.FindAnyObjectByType<MainMenuUi>();
                if (ui != null && ui.IsBuiltForTests) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// In der Menue-Szene liegen ZWEI UIDocument-Komponenten (Menue und
        /// Ladebildschirm). FindAnyObjectByType liefert irgendeins davon -
        /// genau daran sind diese Tests im vollen Lauf gescheitert, waehrend
        /// sie einzeln zufaellig das richtige erwischten. Deshalb gezielt das
        /// Dokument des Menues holen.
        /// </summary>
        static UIDocument MenueDokument()
        {
            var ui = Object.FindAnyObjectByType<MainMenuUi>();
            return ui != null ? ui.GetComponent<UIDocument>() : null;
        }

        static string AllerText(VisualElement wurzel)
        {
            var sb = new System.Text.StringBuilder();
            void Sammle(VisualElement e)
            {
                if (e is Label l && !string.IsNullOrEmpty(l.text)) sb.Append(l.text).Append(' ');
                if (e is Button b && !string.IsNullOrEmpty(b.text)) sb.Append(b.text).Append(' ');
                foreach (var kind in e.Children()) Sammle(kind);
            }
            Sammle(wurzel);
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator Menue_hat_eine_Quellen_Seite()
        {
            yield return WarteAufMenue();

            var doc = MenueDokument();
            Assert.IsNotNull(doc, "Kein Menue-Dokument gefunden.");

            string text = AllerText(doc.rootVisualElement);
            Assert.IsTrue(text.Contains("CREDITS"),
                "Im Hauptmenue gibt es keinen Knopf 'CREDITS'.");
        }

        [UnityTest]
        public IEnumerator Quellen_nennen_Mixamo_und_die_Tonaufnahmen()
        {
            yield return WarteAufMenue();

            var doc = MenueDokument();
            Assert.IsNotNull(doc);

            // Ueber den Namen suchen, nicht ueber die Beschriftung - der Name
            // wird in NavButton fest vergeben und aendert sich nicht mit dem Text.
            var quellen = doc.rootVisualElement.Q<Button>("nav-quellen");
            Assert.IsNotNull(quellen, "Knopf 'nav-quellen' nicht gefunden.");

            using (var e = new NavigationSubmitEvent { target = quellen })
                quellen.SendEvent(e);
            for (int i = 0; i < 5; i++) yield return null;

            string text = AllerText(doc.rootVisualElement);

            // Die ausfuehrlichen Credits nennen die einzelnen Werke und
            // Urheber; der Anbieter steht auch in der anklickbaren Quelladresse.
            Assert.IsTrue(text.Contains("Mixamo"),
                "Mixamo fehlt auf der Quellen-Seite.");

            Assert.IsTrue(text.Contains("https://polyhaven.com/a/"), "Poly-Haven-Quelladressen fehlen.");
            Assert.IsTrue(text.Contains("https://ambientcg.com/a/"), "ambientCG-Quelladressen fehlen.");
            Assert.IsTrue(text.Contains("Firearm"),
                "Die Schussaufnahmen fehlen auf der Quellen-Seite.");
        }
    }
}
