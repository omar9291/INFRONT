using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Das Ton-System (AudioService, ProceduralSfx, FootstepSounds).
    ///
    /// Headless prüfbar:
    ///  - Für jede SoundId gibt es einen Platzhalter-Clip.
    ///  - Der Dateiname zum Austauschen folgt der Konvention.
    ///  - Die Gesamtlautstärke greift (0 = still).
    ///  - Schritt-Lautstärke hängt am Tempo.
    ///  - Ein Schuss fordert wirklich einen Ton an.
    ///
    /// NICHT prüfbar: wie es klingt.
    /// </summary>
    public sealed class AudioTests
    {
        float _vol;

        [SetUp]
        public void Setup()
        {
            _vol = GameSettings.SfxVolume;
            AudioService.EnsureForTests();
            AudioService.Instance.ResetTestState();
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.SfxVolume = _vol;
        }

        [Test]
        public void Jeder_Ton_hat_einen_Platzhalter()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                var clip = ProceduralSfx.Build(id);
                Assert.IsNotNull(clip, $"Kein Platzhalter für {id}.");
                Assert.Greater(clip.samples, 0, $"Platzhalter für {id} ist leer.");
            }
        }

        [Test]
        public void Dateiname_folgt_der_Konvention()
        {
            Assert.AreEqual("schuss_gewehr", AudioService.FileName(SoundId.SchussGewehr));
            Assert.AreEqual("schuss_mp", AudioService.FileName(SoundId.SchussMp));
            Assert.AreEqual("einschlag_koerper", AudioService.FileName(SoundId.EinschlagKoerper));
            Assert.AreEqual("bombe_entschaerft", AudioService.FileName(SoundId.BombeEntschaerft));
        }

        [Test]
        public void Gesamtlautstaerke_null_macht_still()
        {
            GameSettings.SfxVolume = 0f;
            AudioService.Instance.PlayAt(SoundId.SchussGewehr, Vector3.zero);
            Assert.AreEqual(0f, AudioService.Instance.LastVolumeForTests, 0.0001f,
                "Bei Lautstärke 0 darf nichts hörbar sein.");

            GameSettings.SfxVolume = 1f;
            AudioService.Instance.PlayAt(SoundId.SchussGewehr, Vector3.zero, 0.8f);
            Assert.Greater(AudioService.Instance.LastVolumeForTests, 0.5f,
                "Bei voller Lautstärke muss der Ton hörbar sein.");
        }

        [Test]
        public void Ton_wird_zwischengespeichert()
        {
            GameSettings.SfxVolume = 1f;
            Assert.IsFalse(AudioService.Instance.IsCachedForTests(SoundId.Nachladen));
            AudioService.Instance.PlayAt(SoundId.Nachladen, Vector3.zero);
            Assert.IsTrue(AudioService.Instance.IsCachedForTests(SoundId.Nachladen),
                "Der Clip wurde nach dem ersten Abspielen nicht behalten.");
        }

        [Test]
        public void Schritt_Lautstaerke_haengt_am_Tempo()
        {
            Assert.AreEqual(SoundId.SchrittLaut, FootstepSounds.TierFor(9f));
            Assert.AreEqual(SoundId.SchrittNormal, FootstepSounds.TierFor(5f));
            Assert.AreEqual(SoundId.SchrittLeise, FootstepSounds.TierFor(2f));

            // Schneller -> kürzerer Abstand zwischen den Schritten.
            Assert.Less(FootstepSounds.StepIntervalFor(10f), FootstepSounds.StepIntervalFor(3f));
        }

        [UnityTest]
        public IEnumerator Ein_Schuss_fordert_einen_Ton_an()
        {
            NetworkWeapon weapon = null;

            yield return MatchTestHarness.LoadReady((player, match) =>
            {
                weapon = player.GetComponent<NetworkWeapon>();
            });

            GameSettings.SfxVolume = 1f;
            var audio = AudioService.EnsureForTests();
            audio.ResetTestState();

            Assert.IsTrue(weapon.ServerTryFire(), "Der Testschuss ging nicht raus.");

            for (int i = 0; i < 20 && audio.PlayCountForTests == 0; i++)
                yield return null;

            Assert.Greater(audio.PlayCountForTests, 0,
                "Ein Schuss hat keinen Ton angefordert.");

            yield return MatchTestHarness.Teardown();
        }
    }
}
