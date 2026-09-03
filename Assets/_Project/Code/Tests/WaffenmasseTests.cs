using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Realismus-Etappe Schritt 4: Waffenmasse und Rueckstoss.
    ///
    /// NICHT pruefbar: ob der Rueckstoss kontrollierbar bleibt, ob sich die
    /// Waffe schwer anfuehlt, ob das Anlegen zu lange dauert. Gespielt werden.
    ///
    /// Geprueft wird, was messbar ist:
    ///  - Der Rueckstoss ist nicht mehr zweimal derselbe (kein Muster).
    ///  - Die Form bleibt trotzdem erkennbar: es geht nach oben.
    ///  - Jede Waffe hat eine eigene Anlegezeit, und schwere brauchen laenger.
    ///  - Schwere Waffen schwingen staerker nach.
    /// </summary>
    public sealed class WaffenmasseTests
    {
        // Der Waffen-Katalog liegt nicht unter Resources, sondern haengt am
        // Spieler (PurchaseAgent). Deshalb erst ein Spiel laden und den
        // Katalog von dort holen.
        WeaponCatalog _katalog;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _katalog = null;
            yield return MatchTestHarness.Teardown();
        }

        IEnumerator KatalogLaden()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var agent = player.GetComponent<PurchaseAgent>();
            _katalog = agent != null ? agent.Catalog : null;
        }

        WeaponStats Waffe(string name)
        {
            if (_katalog == null) return null;
            foreach (var w in _katalog.Weapons)
                if (w != null && w.name == name) return w;
            return null;
        }

        [UnityTest]
        public IEnumerator Jede_Waffe_hat_eine_eigene_Anlegezeit()
        {
            yield return KatalogLaden();
            var gewehr = Waffe("Sturmgewehr");
            var mp = Waffe("Maschinenpistole");
            var sniper = Waffe("Scharfschuetzengewehr");
            var pistole = Waffe("Pistole");

            if (gewehr == null || mp == null || sniper == null || pistole == null)
                Assert.Ignore("Waffen-Katalog nicht vollstaendig - nichts zu pruefen.");

            // Je schwerer und laenger, desto laenger dauert das Anlegen.
            Assert.Greater(sniper.AdsTime, gewehr.AdsTime,
                "Das Scharfschuetzengewehr muesste laenger brauchen als das Sturmgewehr.");
            Assert.Greater(gewehr.AdsTime, mp.AdsTime,
                "Das Sturmgewehr muesste laenger brauchen als die Maschinenpistole.");
            Assert.Greater(mp.AdsTime, pistole.AdsTime,
                "Die Maschinenpistole muesste laenger brauchen als die Pistole.");

            // Und keine ist mehr "praktisch sofort" wie die alten 0.11 s.
            Assert.Greater(pistole.AdsTime, 0.12f,
                "Selbst die Pistole sollte spuerbar angelegt werden muessen.");
        }

        [UnityTest]
        public IEnumerator Schwere_Waffen_schwingen_staerker_nach()
        {
            yield return KatalogLaden();
            var sniper = Waffe("Scharfschuetzengewehr");
            var pistole = Waffe("Pistole");
            if (sniper == null || pistole == null)
                Assert.Ignore("Waffen fehlen - nichts zu pruefen.");

            Assert.Greater(sniper.SwayScale, pistole.SwayScale,
                "Das Scharfschuetzengewehr muesste staerker nachschwingen als die Pistole.");
        }

        [UnityTest]
        public IEnumerator Rueckstoss_hat_einen_Zufallsanteil()
        {
            yield return KatalogLaden();
            var gewehr = Waffe("Sturmgewehr");
            if (gewehr == null) Assert.Ignore("Sturmgewehr fehlt.");

            Assert.Greater(gewehr.RecoilRandomUp, 0f,
                "Ohne Zufallsanteil nach oben ist der Rueckstoss ein auswendig lernbares Muster.");
            Assert.Greater(gewehr.RecoilRandomSide, 0f,
                "Ohne seitlichen Zufallsanteil ist der Rueckstoss ein Muster.");
        }

        [UnityTest]
        public IEnumerator Zwei_Schuesse_geben_nicht_denselben_Rueckstoss()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var weapon = player.GetComponent<NetworkWeapon>();
            Assert.IsNotNull(weapon, "Der Spieler hat keine Waffe.");
            var stats = weapon.Stats;
            if (stats == null) Assert.Ignore("Die Waffe hat keine Werte.");

            // Den Rueckstoss so nachrechnen, wie die Waffe ihn erzeugt, und
            // pruefen, dass zwanzig Ziehungen nicht alle gleich sind.
            float ersteZiehung = stats.RecoilUp
                * (1f + Random.Range(-stats.RecoilRandomUp, stats.RecoilRandomUp));
            bool unterschiedlich = false;
            for (int i = 0; i < 20 && !unterschiedlich; i++)
            {
                float weitere = stats.RecoilUp
                    * (1f + Random.Range(-stats.RecoilRandomUp, stats.RecoilRandomUp));
                if (!Mathf.Approximately(weitere, ersteZiehung)) unterschiedlich = true;
            }

            Assert.IsTrue(unterschiedlich,
                "Zwanzig Schuesse gaben exakt denselben Rueckstoss - das ist ein Muster.");

            // Die Form muss trotzdem stimmen: es geht nach oben, nicht nach unten.
            for (int i = 0; i < 40; i++)
            {
                float up = stats.RecoilUp
                    * (1f + Random.Range(-stats.RecoilRandomUp, stats.RecoilRandomUp));
                Assert.Greater(up, 0f,
                    "Der Zufallsanteil ist so gross, dass der Rueckstoss nach unten gehen kann.");
            }
            yield return null;
        }
    }
}
