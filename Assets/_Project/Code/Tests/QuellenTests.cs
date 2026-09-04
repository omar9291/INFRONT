using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    /// <summary>
    /// Die Quellen-Seite im Hauptmenue. Rechtlich noetig ist eine Nennung bei
    /// CC0 nicht - bei Mixamo schon, und ausserdem gehoert es sich.
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
            Assert.IsTrue(text.Contains("QUELLEN"),
                "Im Hauptmenue gibt es keinen Knopf 'QUELLEN'.");
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

            // Mixamo MUSS genannt werden - das ist die einzige Quelle im
            // Projekt, die nicht CC0 ist.
            Assert.IsTrue(text.Contains("Mixamo"),
                "Mixamo fehlt auf der Quellen-Seite. Das ist die einzige Quelle, " +
                "die nicht CC0 ist - sie gehoert dort zwingend hin.");

            Assert.IsTrue(text.Contains("Poly Haven"), "Poly Haven fehlt.");
            Assert.IsTrue(text.Contains("ambientCG"), "ambientCG fehlt.");
            Assert.IsTrue(text.Contains("Firearm"),
                "Die Schussaufnahmen fehlen auf der Quellen-Seite.");
        }
    }
}
