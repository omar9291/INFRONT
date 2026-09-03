using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die sichtbare Waffe in der Hand (<see cref="ViewModel"/>).
    ///
    /// NICHT pruefbar: wie die Waffe aussieht, ob Wippen und Nachschwingen sich
    /// gut anfuehlen. Geprueft wird nur: das Modell wird gebaut, ein Schuss
    /// stoesst es messbar zurueck und klingt wieder ab, und beim Tod
    /// verschwindet es.
    /// </summary>
    public sealed class ViewModelTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return MatchTestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Waffe_in_der_Hand_wird_gebaut()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var vm = player.GetComponent<ViewModel>();
            Assert.IsNotNull(vm, "Spieler-Prefab hat kein ViewModel-Bauteil.");

            for (int i = 0; i < 10; i++) yield return null;

            Assert.IsTrue(vm.HasModelForTests,
                "Es wurde kein Waffenmodell vor der Kamera gebaut.");

            // Der Platzhalter-Wuerfel der Kamera darf nicht zusaetzlich da sein.
            var fpc = Camera.main.GetComponent<FirstPersonCamera>();
            Assert.IsNotNull(fpc);
        }

        [UnityTest]
        public IEnumerator Sturmgewehr_und_MP_sind_mehrteilige_Modelle()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var vm = player.GetComponent<ViewModel>();
            var weapon = player.GetComponent<NetworkWeapon>();
            for (int i = 0; i < 10; i++) yield return null;

            // Standard aus dem Prefab = Sturmgewehr (Index 0).
            Assert.Greater(vm.PartCountForTests, 12,
                $"Das Sturmgewehr hat nur {vm.PartCountForTests} Teile - zu grob.");

            weapon.ServerSetPrimary(1);   // Maschinenpistole
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(vm.PartCountForTests, 10,
                $"Die MP hat nur {vm.PartCountForTests} Teile - zu grob.");
        }

        [UnityTest]
        public IEnumerator Schuss_stoesst_die_Waffe_zurueck_und_sie_kehrt_zurueck()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var vm = player.GetComponent<ViewModel>();
            for (int i = 0; i < 10; i++) yield return null;

            float restZ = vm.ModelLocalPosForTests.z;

            vm.PokeRecoilForTests();
            yield return null;
            yield return null;

            float kickZ = vm.ModelLocalPosForTests.z;
            Assert.Less(kickZ, restZ - 0.005f,
                $"Die Waffe ist beim Schuss nicht zurueckgegangen: {restZ:0.000} -> {kickZ:0.000}");

            // Nach kurzer Zeit wieder in Ruhelage.
            for (int i = 0; i < 60; i++) yield return null;
            float backZ = vm.ModelLocalPosForTests.z;
            Assert.Less(Mathf.Abs(backZ - restZ), 0.01f,
                $"Die Waffe ist nicht in die Ruhelage zurueckgekehrt: {backZ:0.000} vs {restZ:0.000}");
        }

        [UnityTest]
        public IEnumerator Waffe_verschwindet_beim_Tod()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            var vm = player.GetComponent<ViewModel>();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse(vm.HiddenForTests, "Waffe war schon vor dem Tod versteckt.");

            player.GetComponent<Health>().ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return MatchTestHarness.WaitUntil(
                () => !player.GetComponent<Health>().IsAlive, 3f, "Spieler wurde nicht getoetet.");
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsTrue(vm.HiddenForTests, "Die Waffe ist nach dem Tod noch sichtbar.");
        }
    }
}
