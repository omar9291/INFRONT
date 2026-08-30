using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-2-Tests: Schaden, Waffe, Tod und Respawn - server-autoritativ.
    /// </summary>
    public sealed class WeaponAndDamageTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static TargetDummy FindDummy()
        {
            foreach (var d in Object.FindObjectsByType<TargetDummy>(FindObjectsSortMode.None))
                return d;
            return null;
        }

        [UnityTest]
        public IEnumerator Server_Schaden_senkt_Leben_des_Dummys()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { }, expectedCombatants: 6);

            // Dummy spawnt separat (DummySpawner) - kurz warten
            TargetDummy dummy = null;
            yield return MatchTestHarness.WaitUntil(() => (dummy = FindDummy()) != null, 6f, "Kein Dummy.");
            var health = dummy.GetComponent<Health>();
            health.ResetFull();
            yield return null;

            int before = health.Current;
            health.ApplyDamage(10, NetworkManager.ServerClientId);
            yield return null;
            Assert.AreEqual(before - 10, health.Current);
        }

        [UnityTest]
        public IEnumerator Dummy_stirbt_bei_null_Leben_und_respawnt()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            TargetDummy dummy = null;
            yield return MatchTestHarness.WaitUntil(() => (dummy = FindDummy()) != null, 6f, "Kein Dummy.");
            var health = dummy.GetComponent<Health>();

            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive);

            yield return MatchTestHarness.WaitUntil(() => health.IsAlive, 8f, "Dummy nicht respawnt.");
            Assert.AreEqual(health.Max, health.Current);
        }

        [UnityTest]
        public IEnumerator Waffe_startet_mit_vollem_Magazin()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var weapon = player.GetComponent<NetworkWeapon>();

            Assert.AreEqual(weapon.MagazineSize, weapon.Ammo);
            Assert.Greater(weapon.MagazineSize, 0);
            Assert.IsFalse(weapon.IsReloading);
        }

        [UnityTest]
        public IEnumerator Spieler_lebt_beim_Rundenstart_wieder()
        {
            NetworkPlayerController player = null; MatchManager match = null;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });
            var health = player.GetComponent<Health>();

            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive);

            match.StartRound();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(health.IsAlive, "Spieler lebt nach Rundenstart nicht.");
            Assert.AreEqual(health.Max, health.Current);
        }

        [UnityTest]
        public IEnumerator Schuss_auf_Dummy_macht_Schaden()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            TargetDummy dummy = null;
            yield return MatchTestHarness.WaitUntil(() => (dummy = FindDummy()) != null, 6f, "Kein Dummy.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = 0f, LookPitch = 0f };
            player.SetInputSource(input);
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();

            Vector3 aim = player.AimDirection; aim.y = 0f; aim.Normalize();
            dummy.transform.position = player.transform.position + aim * 6f;
            var dummyHealth = dummy.GetComponent<Health>();
            dummyHealth.ResetFull();
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();

            int before = dummyHealth.Current;
            input.FireHeld = true;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;

            Assert.Less(dummyHealth.Current, before, $"Dummy nicht getroffen (vorher {before}, jetzt {dummyHealth.Current}).");
        }

        [UnityTest]
        public IEnumerator Toter_Spieler_schiesst_nicht()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var weapon = player.GetComponent<NetworkWeapon>();
            var health = player.GetComponent<Health>();

            var input = new FakePlayerInput { LookYaw = 0f };
            player.SetInputSource(input);

            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive);

            int ammoBefore = weapon.Ammo;
            input.FireHeld = true;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;

            Assert.AreEqual(ammoBefore, weapon.Ammo, "Ein toter Spieler hat geschossen.");
        }

        [UnityTest]
        public IEnumerator Feuerrate_begrenzt_die_Schussanzahl()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            var weapon = player.GetComponent<NetworkWeapon>();

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = 0f };
            player.SetInputSource(input);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            int ammoBefore = weapon.Ammo;
            input.FireHeld = true;
            int frames = 60;
            for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;

            int consumed = ammoBefore - weapon.Ammo;
            Assert.Greater(consumed, 0, "Es wurde gar nicht geschossen.");
            Assert.Less(consumed, frames / 2, $"Feuerrate greift nicht: {consumed} Schuss in {frames} Ticks.");
        }
    }
}
