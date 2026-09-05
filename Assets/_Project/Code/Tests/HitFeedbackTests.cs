using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Trefferrückmeldung (Masterplan Etappe A, Paket 2): der Schuss meldet,
    /// was er getroffen hat (Wand / Körper), tödliche Treffer werden als
    /// solche gemeldet, und der Einschlag-Pool recycelt seine Löcher.
    ///
    /// NICHT prüfbar: wie Mündungsfeuer, Funken, Hülsen und Kamera-Ruckeln
    /// aussehen.
    /// </summary>
    public sealed class HitFeedbackTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static TargetDummy FindDummy()
        {
            foreach (var d in Object.FindObjectsByType<TargetDummy>(FindObjectsSortMode.None))
                return d;
            return null;
        }

        [Test]
        public void ImpactPool_recycelt_die_Einschlagloecher()
        {
            var pool = ImpactPool.EnsureForTests();
            pool.ClearForTests();
            var wall = new ShotFx(Vector3.zero, new Vector3(0f, 0f, 5f), Vector3.back, 1);

            for (int i = 0; i < 200; i++)
                pool.SpawnForTests(wall);

            Assert.LessOrEqual(pool.ActiveHolesForTests, 40,
                "Der Loch-Pool waechst unbegrenzt statt zu recyceln.");
            Assert.Greater(pool.ActiveHolesForTests, 0, "Es wurde kein einziges Loch gesetzt.");

            pool.ClearForTests();
        }

        [UnityTest]
        public IEnumerator Schuss_auf_den_Koerper_meldet_Koerper_Einschlag()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p, withTrainingDummy: true);
            TargetDummy dummy = null;
            yield return MatchTestHarness.WaitUntil(() => (dummy = FindDummy()) != null, 6f, "Kein Dummy.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Vector3 aim = player.AimDirection; aim.y = 0f; aim.Normalize();
            dummy.transform.position = player.transform.position + aim * 6f;
            dummy.GetComponent<Health>().ResetFull();
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();

            ShotFx last = default;
            bool got = false;
            void Grab(ShotFx fx) { last = fx; got = true; }
            NetworkWeapon.AnyShotFx += Grab;

            var weapon = player.GetComponent<NetworkWeapon>();
            Assert.IsTrue(weapon.ServerTryFire(), "Testschuss ging nicht raus.");
            for (int i = 0; i < 20 && !got; i++) yield return null;
            NetworkWeapon.AnyShotFx -= Grab;

            Assert.IsTrue(got, "Kein ShotFx angekommen.");
            Assert.AreEqual(2, last.Impact, "Körpertreffer wurde nicht als solcher gemeldet.");
        }

        [UnityTest]
        public IEnumerator Schuss_auf_ein_Hindernis_meldet_Wand_Einschlag()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            var oldDummy = FindDummy();
            if (oldDummy != null) oldDummy.transform.position = new Vector3(0f, 500f, 0f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            // Plombe: ein grosser Wuerfel mit Collider, ohne Health, auf der
            // Default-Ebene - genau in die Schusslinie gesetzt.
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.localScale = Vector3.one * 8f;
            block.transform.position = player.AimOrigin + player.AimDirection * 6f;
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();

            ShotFx last = default;
            bool got = false;
            void Grab(ShotFx fx) { last = fx; got = true; }
            NetworkWeapon.AnyShotFx += Grab;

            var weapon = player.GetComponent<NetworkWeapon>();
            Assert.IsTrue(weapon.ServerTryFire(), "Testschuss ging nicht raus.");
            for (int i = 0; i < 20 && !got; i++) yield return null;
            NetworkWeapon.AnyShotFx -= Grab;
            Object.Destroy(block);

            Assert.IsTrue(got, "Kein ShotFx angekommen.");
            Assert.AreEqual(1, last.Impact, "Umgebungstreffer wurde nicht als Wand gemeldet.");
        }

        [UnityTest]
        public IEnumerator Toedlicher_Treffer_wird_als_lethal_gemeldet()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p, withTrainingDummy: true);
            TargetDummy dummy = null;
            yield return MatchTestHarness.WaitUntil(() => (dummy = FindDummy()) != null, 6f, "Kein Dummy.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Vector3 aim = player.AimDirection; aim.y = 0f; aim.Normalize();
            dummy.transform.position = player.transform.position + aim * 6f;
            var hp = dummy.GetComponent<Health>();
            hp.ResetFull();
            hp.ApplyDamage(hp.Max - 1, NetworkManager.ServerClientId);   // auf 1 Leben
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();

            bool sawLethal = false;
            void OnHit(bool head, bool lethal) { if (lethal) sawLethal = true; }
            var weapon = player.GetComponent<NetworkWeapon>();
            weapon.LocalHitConfirmed += OnHit;

            Assert.IsTrue(weapon.ServerTryFire(), "Testschuss ging nicht raus.");
            for (int i = 0; i < 25 && !sawLethal; i++) yield return null;
            weapon.LocalHitConfirmed -= OnHit;

            Assert.IsFalse(hp.IsAlive, "Der Dummy hat den Schuss ueberlebt.");
            Assert.IsTrue(sawLethal, "Der toedliche Treffer wurde nicht als lethal gemeldet.");
        }
    }
}
