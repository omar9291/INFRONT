using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Steht ein Spawn frei, oder klebt er vor einer Wand?
    ///
    /// Der Zustand davor: die Rückwand des Spawn-Raums besteht aus fünf
    /// Blöcken mit Lücken dazwischen. Die sechs Spawn-Punkte je Team waren
    /// aber nach anderen x-Werten gesetzt worden, und vier von sechs landeten
    /// hinter einem Block. Zwei Drittel aller Rundenanfänge begannen also mit
    /// einer Betonwand 1,4 m vor dem Gesicht - sichtbar auf jedem Bild aus
    /// dem Oberflächen-Rundgang, aber von keinem Test bemerkt.
    ///
    /// Warum es lange niemandem auffiel: ein Strahl geradeaus reicht nicht.
    /// Die Kamera ist 85° breit; eine Wand LÄNGS der Blickrichtung wird von
    /// keinem Strahl nach vorne getroffen. Deshalb prüft dieser Test einen
    /// Fächer bis 45° - so weit, wie der Spieler tatsächlich sieht.
    /// </summary>
    public sealed class SpawnFreiTests
    {
        const float Augenhoehe = 1.6f;
        const float Mindestsicht = 6f;

        [UnityTest]
        public IEnumerator Vor_jedem_Spawn_ist_Platz()
        {
            MatchTestHarness.BeginFreeze();
            yield return MatchTestHarness.LoadReady((player, match) => { });

            var punkte = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            Assert.Greater(punkte.Length, 0, "Es gibt gar keine Spawn-Punkte.");

            var klagen = new System.Text.StringBuilder();
            foreach (var sp in punkte)
            {
                Vector3 auge = sp.transform.position + Vector3.up * Augenhoehe;
                foreach (float winkel in new[] { -45f, -30f, -15f, 0f, 15f, 30f, 45f })
                {
                    Vector3 richtung = Quaternion.AngleAxis(winkel, Vector3.up) * sp.transform.forward;
                    if (!Physics.Raycast(auge, richtung, out var treffer, Mindestsicht,
                                         ~0, QueryTriggerInteraction.Ignore))
                        continue;

                    // Deckung direkt vor dem Spawn ist gewollt und niedrig.
                    // Was hier stoert, sind die hohen Riegel der Rueckwand.
                    if (treffer.collider.name.StartsWith("SpawnCov")) continue;

                    klagen.Append($"\n  {sp.name} bei {sp.transform.position}: "
                                  + $"{winkel:0}° -> {treffer.collider.name} "
                                  + $"in {treffer.distance:0.0} m");
                }
            }

            Assert.AreEqual(0, klagen.Length,
                $"Vor diesen Spawns steht etwas naeher als {Mindestsicht} m. Wer dort "
                + "erscheint, sieht als Erstes eine Wand:" + klagen);

            yield return MatchTestHarness.Teardown();
        }
    }
}
