using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// "Wucht" (Etappe 2): Geschoss-Zischen am Kopf vorbei, ferner Schuss-Hall,
    /// staerkerer Kamera-Kick, Explosions-Taubheit.
    ///
    /// NICHT pruefbar: wie es klingt und sich anfuehlt. Geprueft wird:
    ///  - die Vorbei-Flug-Geometrie (welche Kugel zischt, welche nicht),
    ///  - dass das Zischen beim betroffenen Spieler ankommt,
    ///  - dass eine nahe Explosion das Ohr kurz dumpf macht und sich das wieder gibt.
    /// </summary>
    public sealed class WuchtTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [Test]
        public void Vorbei_Flug_Erkennung_stimmt()
        {
            Vector3 origin = new Vector3(0f, 1.6f, 0f);
            Vector3 dir = Vector3.forward;

            // dicht vorbei (1 m seitlich, 20 m weit) -> zischt
            Assert.IsTrue(BulletWhiz.PassesNear(origin, dir, 50f,
                new Vector3(1.0f, 1.7f, 20f), 1.7f, 6f, out _),
                "Eine Kugel 1 m seitlich sollte zischen.");

            // weit daneben (5 m) -> nichts
            Assert.IsFalse(BulletWhiz.PassesNear(origin, dir, 50f,
                new Vector3(5f, 1.7f, 20f), 1.7f, 6f, out _),
                "Eine Kugel 5 m daneben darf nicht zischen.");

            // fast im Kopf (0,2 m) -> das war ein Treffer, kein Vorbei
            Assert.IsFalse(BulletWhiz.PassesNear(origin, dir, 50f,
                new Vector3(0.2f, 1.62f, 20f), 1.7f, 6f, out _),
                "Ein quasi-Treffer darf nicht als Vorbei-Flug zaehlen.");

            // dicht, aber direkt vor der Nase (3 m) -> unter Mindestabstand
            Assert.IsFalse(BulletWhiz.PassesNear(origin, dir, 50f,
                new Vector3(1.0f, 1.7f, 3f), 1.7f, 6f, out _),
                "Direkt vor dem Schuetzen soll noch nichts zischen.");

            // hinter dem Ende der Schussstrecke -> nichts
            Assert.IsFalse(BulletWhiz.PassesNear(origin, dir, 15f,
                new Vector3(1.0f, 1.7f, 40f), 1.7f, 6f, out _),
                "Hinter dem Einschlag darf nichts mehr zischen.");
        }

        [UnityTest]
        public IEnumerator Zischen_kommt_beim_betroffenen_Spieler_an()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var whiz = player.GetComponent<BulletWhiz>();
            Assert.IsNotNull(whiz, "Spieler-Prefab hat kein BulletWhiz-Bauteil.");

            var audio = AudioService.Instance;
            Assert.IsNotNull(audio, "Kein AudioService.");
            audio.ResetTestState();

            whiz.ServerReportForTests(1f);
            for (int i = 0; i < 10; i++) yield return null;

            Assert.GreaterOrEqual(whiz.WhizCountForTests, 1, "Das Zischen kam nicht an.");
            Assert.AreEqual(SoundId.Zischen, audio.LastPlayedForTests, "Falscher Ton beim Vorbei-Flug.");
        }

        [UnityTest]
        public IEnumerator Nahe_Explosion_macht_das_Ohr_kurz_dumpf()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var cam = Camera.main;
            Assert.IsNotNull(cam);

            var go = new GameObject("TestExplosionFx");
            var fx = go.AddComponent<BombExplosionFx>();
            yield return null;

            fx.Play(cam.transform.position + Vector3.forward * 2f);   // ganz nah
            for (int i = 0; i < 5; i++) yield return null;

            var filter = cam.GetComponent<AudioLowPassFilter>();
            Assert.IsNotNull(filter, "Nach der nahen Explosion fehlt der Tiefpass auf dem Ohr.");
            Assert.IsTrue(filter.enabled, "Der Tiefpass ist nicht aktiv.");
            Assert.Less(filter.cutoffFrequency, 6000f,
                $"Das Ohr ist nicht dumpf genug ({filter.cutoffFrequency:F0} Hz).");

            // ... und nach ein paar Sekunden ist das Gehoer wieder da.
            yield return new WaitForSeconds(3.5f);
            Assert.IsTrue(!filter.enabled || filter.cutoffFrequency > 15000f,
                $"Das Gehoer hat sich nicht erholt ({filter.cutoffFrequency:F0} Hz).");

            Object.Destroy(go);
        }
    }
}
