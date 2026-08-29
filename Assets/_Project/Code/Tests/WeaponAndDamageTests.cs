using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-2-Tests: Schaden, Waffe, Tod und Respawn - alles server-autoritativ.
    /// Laeuft headless im Host-Modus.
    /// </summary>
    public sealed class WeaponAndDamageTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null;
                Object.Destroy(NetworkManager.Singleton.gameObject);
            }
            yield return null;
        }

        static IEnumerator LoadArenaAndHost()
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;

            Assert.IsNotNull(NetworkManager.Singleton, "Kein NetworkManager.");
            float timeout = 8f;
            while (!NetworkManager.Singleton.IsListening && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(NetworkManager.Singleton.IsListening, "Host nicht gestartet.");
        }

        static IEnumerator WaitFor<T>(System.Action<T> onFound, float seconds = 8f) where T : Object
        {
            T found = null;
            while (found == null && seconds > 0f)
            {
                found = Object.FindAnyObjectByType<T>();
                seconds -= Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(found, $"{typeof(T).Name} nicht gefunden.");
            onFound(found);
        }

        [UnityTest]
        public IEnumerator Server_Schaden_senkt_Leben_des_Dummys()
        {
            yield return LoadArenaAndHost();

            TargetDummy dummy = null;
            yield return WaitFor<TargetDummy>(d => dummy = d);

            var health = dummy.GetComponent<Health>();
            int before = health.Current;
            Assert.Greater(before, 0);

            health.ApplyDamage(10, NetworkManager.ServerClientId);
            yield return null;

            Assert.AreEqual(before - 10, health.Current, "Schaden nicht korrekt abgezogen.");
        }

        [UnityTest]
        public IEnumerator Dummy_stirbt_bei_null_Leben_und_respawnt()
        {
            yield return LoadArenaAndHost();

            TargetDummy dummy = null;
            yield return WaitFor<TargetDummy>(d => dummy = d);
            var health = dummy.GetComponent<Health>();

            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;

            Assert.IsFalse(health.IsAlive, "Dummy sollte tot sein.");

            // TargetDummy._reviveDelay ist 4 s -> genug warten
            float waited = 0f;
            while (!health.IsAlive && waited < 8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(health.IsAlive, "Dummy ist nicht respawnt.");
            Assert.AreEqual(health.Max, health.Current, "Dummy sollte volles Leben haben.");
        }

        [UnityTest]
        public IEnumerator Waffe_startet_mit_vollem_Magazin()
        {
            yield return LoadArenaAndHost();

            NetworkWeapon weapon = null;
            yield return WaitFor<NetworkWeapon>(w => weapon = w);

            Assert.AreEqual(weapon.MagazineSize, weapon.Ammo, "Magazin sollte voll sein.");
            Assert.Greater(weapon.MagazineSize, 0);
            Assert.IsFalse(weapon.IsReloading);
        }

        [UnityTest]
        public IEnumerator Schuss_auf_Dummy_macht_Schaden()
        {
            yield return LoadArenaAndHost();

            NetworkPlayerController player = null;
            yield return WaitFor<NetworkPlayerController>(p => player = p);
            var weapon = player.GetComponent<NetworkWeapon>();

            TargetDummy dummy = null;
            yield return WaitFor<TargetDummy>(d => dummy = d);
            var dummyHealth = dummy.GetComponent<Health>();

            // Dummy genau vor die Blickrichtung des Spielers stellen
            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = 0f, LookPitch = 0f };
            player.SetInputSource(input);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Vector3 aimForward = player.transform.forward;
            dummy.transform.position = player.transform.position + aimForward * 6f;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            int before = dummyHealth.Current;
            input.FireHeld = true;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;

            Assert.Less(dummyHealth.Current, before,
                $"Dummy hat keinen Schaden genommen (vorher {before}, jetzt {dummyHealth.Current}).");
        }

        [UnityTest]
        public IEnumerator Feuerrate_begrenzt_die_Schussanzahl()
        {
            yield return LoadArenaAndHost();

            NetworkPlayerController player = null;
            yield return WaitFor<NetworkPlayerController>(p => player = p);
            var weapon = player.GetComponent<NetworkWeapon>();

            var input = new FakePlayerInput { Move = Vector2.zero, LookYaw = 0f, LookPitch = -20f };
            player.SetInputSource(input);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            int ammoBefore = weapon.Ammo;
            input.FireHeld = true;
            int frames = 60;
            for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
            input.FireHeld = false;

            int consumed = ammoBefore - weapon.Ammo;
            Assert.Greater(consumed, 0, "Es wurde gar nicht geschossen.");
            Assert.Less(consumed, frames / 2,
                $"Feuerrate greift nicht: {consumed} Schuss in {frames} Physik-Ticks.");
        }

        [UnityTest]
        public IEnumerator Spieler_stirbt_und_respawnt_mit_vollem_Leben()
        {
            yield return LoadArenaAndHost();

            NetworkPlayerController player = null;
            yield return WaitFor<NetworkPlayerController>(p => player = p);
            var health = player.GetComponent<Health>();

            Vector3 posBeforeDeath = player.transform.position;
            health.ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return null;
            Assert.IsFalse(health.IsAlive, "Spieler sollte tot sein.");

            // PlayerLifecycle._respawnDelay ist 3 s
            float waited = 0f;
            while (!health.IsAlive && waited < 8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(health.IsAlive, "Spieler ist nicht respawnt.");
            Assert.AreEqual(health.Max, health.Current, "Spieler sollte volles Leben haben.");
        }
    }
}
