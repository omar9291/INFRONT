using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die Waffe an der Wand.
    ///
    /// Der Zustand davor: stand man an einer Wand, steckte die Waffe im Beton.
    /// In der Ego-Sicht ist das der auffaelligste Bruch ueberhaupt - die Welt
    /// ist fest, aber das Ding in den eigenen Haenden ist es nicht.
    ///
    /// NICHT pruefbar: ob die Bewegung gut aussieht. Pruefbar: dass die Waffe
    /// vor einer Wand angezogen wird, im Freien nicht, und dass ein Mitspieler
    /// sie NICHT anhebt - sonst zuckt sie im Gefecht dauernd.
    /// </summary>
    public sealed class WaffeAnDerWandTests
    {
        GameObject _wand;

        [TearDown]
        public void TearDown()
        {
            if (_wand != null) Object.DestroyImmediate(_wand);
        }

        /// <summary>
        /// Eine Wand vor die Spielerkamera stellen.
        ///
        /// WICHTIG: die Kamera erst zur Ruhe kommen lassen. Beim ersten Anlauf
        /// stand die Wand bei (-24, 1.9, -29), waehrend die Kamera schon bei
        /// (0, 1.68, 0) war - der Spieler war zwar gesetzt, die Kamera aber
        /// noch nicht nachgezogen. Ein einzelner Frame reicht dafuer nicht.
        /// </summary>
        GameObject StelleWand(Transform kamera, float entfernung, int schicht)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Testwand";
            w.layer = schicht;
            w.transform.position = kamera.position + kamera.forward * entfernung;
            w.transform.localScale = new Vector3(6f, 6f, 0.4f);
            w.transform.rotation = Quaternion.LookRotation(kamera.forward);
            Physics.SyncTransforms();   // sonst sieht der SphereCast sie erst spaeter
            return w;
        }

        /// <summary>Warten, bis die Kamera wirklich beim Spieler steht.</summary>
        static IEnumerator KameraBeruhigen()
        {
            Vector3 vorher = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                var jetzt = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                if (i > 5 && (jetzt - vorher).sqrMagnitude < 0.0001f) yield break;
                vorher = jetzt;
            }
        }

        [UnityTest]
        public IEnumerator Vor_einer_Wand_wird_die_Waffe_angezogen()
        {
            MatchTestHarness.BeginFreeze();
            NetworkPlayerController spieler = null;
            yield return MatchTestHarness.LoadReady((p, m) => spieler = p);
            MatchTestHarness.ClearArena();

            var vm = Object.FindAnyObjectByType<ViewModel>();
            Assert.IsNotNull(vm, "Kein ViewModel gefunden.");

            var cam = Camera.main;
            Assert.IsNotNull(cam, "Keine Kamera.");

            // Erst frei: nichts vor der Nase.
            MatchTestHarness.PlacePlayer(spieler, new Vector3(0f, 1f, 0f), 0f);
            yield return KameraBeruhigen();
            for (int i = 0; i < 20; i++) yield return null;
            float frei = vm.WandPoseForTests;

            // Jetzt eine Wand direkt davor.
            _wand = StelleWand(cam.transform, 0.45f, 0);
            for (int i = 0; i < 60; i++) yield return null;
            float anDerWand = vm.WandPoseForTests;

            Assert.Less(frei, 0.15f,
                $"Ohne etwas davor duerfte die Waffe nicht angezogen sein (war {frei:F2}).");
            Assert.Greater(anDerWand, 0.4f,
                $"Direkt vor einer Wand muesste die Waffe deutlich angezogen sein "
                + $"(war {anDerWand:F2}). Sonst steckt sie im Beton.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Nach_dem_Wegtreten_kommt_die_Waffe_zurueck()
        {
            MatchTestHarness.BeginFreeze();
            NetworkPlayerController spieler = null;
            yield return MatchTestHarness.LoadReady((p, m) => spieler = p);
            MatchTestHarness.ClearArena();

            var vm = Object.FindAnyObjectByType<ViewModel>();
            var cam = Camera.main;
            MatchTestHarness.PlacePlayer(spieler, new Vector3(0f, 1f, 0f), 0f);
            yield return KameraBeruhigen();

            _wand = StelleWand(cam.transform, 0.45f, 0);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.Greater(vm.WandPoseForTests, 0.4f,
                "Die Wand wurde gar nicht bemerkt. "
                + $"LateUpdate lief: {vm.WandLateUpdateLaeuftForTests}, "
                + $"Treffer: {vm.WandLetzterTrefferForTests}, "
                + $"Entfernung: {vm.WandLetzteEntfernungForTests:F2}, "
                + $"getroffen: {vm.WandLetzterNameForTests}, "
                + $"Kamera: {cam.transform.position}, Wand: {_wand.transform.position}");

            Object.DestroyImmediate(_wand);
            _wand = null;
            for (int i = 0; i < 60; i++) yield return null;

            Assert.Less(vm.WandPoseForTests, 0.15f,
                "Ohne Wand muesste die Waffe wieder in die normale Haltung kommen.");

            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Ein_Mitspieler_hebt_die_Waffe_nicht_an()
        {
            MatchTestHarness.BeginFreeze();
            NetworkPlayerController spieler = null;
            yield return MatchTestHarness.LoadReady((p, m) => spieler = p);
            MatchTestHarness.ClearArena();

            var vm = Object.FindAnyObjectByType<ViewModel>();
            var cam = Camera.main;
            MatchTestHarness.PlacePlayer(spieler, new Vector3(0f, 1f, 0f), 0f);
            yield return KameraBeruhigen();

            // Schicht 6 ist die Schicht der Spielerkoerper und Trefferflaechen.
            _wand = StelleWand(cam.transform, 0.45f, 6);
            for (int i = 0; i < 60; i++) yield return null;

            Assert.Less(vm.WandPoseForTests, 0.15f,
                "Ein Koerper vor der Kamera darf die Waffe NICHT anheben - sonst zuckt "
                + "sie im Gefecht dauernd, wenn jemand vorbeilaeuft.");

            yield return MatchTestHarness.Teardown();
        }
    }
}
