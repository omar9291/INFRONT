using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die Leuchtspur eines Schusses.
    ///
    /// Der Zustand davor: bei jedem Schuss wurde die GANZE Strecke vom Lauf bis
    /// zum Treffer als eine undurchsichtige gelbe Linie gezeichnet, 7 cm breit
    /// und bis zu 45 m lang. Auf den Rundgang-Bildern lagen dadurch gelbe
    /// Bretter quer durch die Halle, mitten durch Kisten hindurch - eine
    /// undurchsichtige Linie ist ja an jeder Stelle gleich hell. Genau das hatte
    /// der Spieler als "da bugt was in der Mitte" gemeldet.
    ///
    /// Sichtbar ist an einem Schuss nicht die Bahn, sondern ein kurzes helles
    /// Stueck, das die Bahn entlangfliegt.
    ///
    /// NICHT pruefbar: wie es aussieht. Pruefbar: dass nie die ganze Strecke auf
    /// einmal gezeichnet wird und dass die Spur additiv gemischt ist.
    /// </summary>
    public sealed class LeuchtspurTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [UnityTest]
        public IEnumerator Die_Spur_zeichnet_nie_die_ganze_Strecke()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, -30f), 0f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            var spur = player.GetComponent<TracerEffect>();
            Assert.IsNotNull(spur, "Am Spieler haengt keine TracerEffect-Komponente.");

            var weapon = player.GetComponent<NetworkWeapon>();
            Assert.IsTrue(weapon.ServerTryFire(), "Testschuss ging nicht raus.");

            // Ueber mehrere Bilder mitschauen: das Stueck wandert los, waechst
            // auf seine Laenge und darf sie nie ueberschreiten.
            float laengste = 0f;
            for (int f = 0; f < 30; f++)
            {
                for (int i = 0; i < 8; i++)
                    laengste = Mathf.Max(laengste, spur.SpurLaengeForTests(i));
                yield return null;
            }

            Assert.Greater(laengste, 0.05f,
                "Es war ueberhaupt keine Spur zu sehen - dann pruefen wir nichts.");
            Assert.LessOrEqual(laengste, spur.SegmentForTests + 0.5f,
                $"Die Spur war {laengste:F1} m lang, erlaubt sind hoechstens "
                + $"{spur.SegmentForTests:F1} m. Damit ist wieder die ganze Flugbahn "
                + "auf einmal gezeichnet - das sah aus wie ein gelbes Brett quer "
                + "durch die Halle.");
        }

        [UnityTest]
        public IEnumerator Die_Spur_verschwindet_wieder()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, -30f), 0f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            var spur = player.GetComponent<TracerEffect>();
            var weapon = player.GetComponent<NetworkWeapon>();
            Assert.IsTrue(weapon.ServerTryFire(), "Testschuss ging nicht raus.");

            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, spur.AktiveSpurenForTests,
                "Nach anderthalb Sekunden duerfte keine Leuchtspur mehr stehen. "
                + "Bleiben sie liegen, sammelt sich ein Gitter aus Strichen an.");
        }
    }
}
