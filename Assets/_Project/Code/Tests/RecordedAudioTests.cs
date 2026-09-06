using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Infront.Tests
{
    public sealed class RecordedAudioTests
    {
        [Test]
        public void Schritte_nutzen_je_Belag_eigene_Aufnahmen_und_bleiben_temposensitiv()
        {
            Assert.AreEqual(SoundId.SchrittMetall, FootstepSounds.SoundFor(5f, FootstepSounds.Untergrund.Metall));
            Assert.AreEqual(SoundId.SchrittSchutt, FootstepSounds.SoundFor(5f, FootstepSounds.Untergrund.Schutt));
            Assert.AreEqual(SoundId.SchrittNormal, FootstepSounds.SoundFor(5f, FootstepSounds.Untergrund.Beton));
            Assert.Less(FootstepSounds.StepGain(2f), FootstepSounds.StepGain(5f));
            Assert.Less(FootstepSounds.StepGain(5f), FootstepSounds.StepGain(7.2f));
            int last = -1;
            for (int i = 0; i < 40; i++)
            {
                int next = AudioCatalog.NextVariant(5, last);
                Assert.That(next, Is.InRange(0, 4));
                Assert.AreNotEqual(last, next, "Unmittelbare Wiederholung klingt mechanisch.");
                last = next;
            }
        }

        [Test]
        public void Spannung_endet_mit_Kaufzeit_Pause_Rundenende_und_Menue()
        {
            Assert.AreEqual(0, MusicDirector.TensionFor(false, true, false, false, 31));
            Assert.Greater(MusicDirector.TensionFor(false, true, false, false, 5),
                MusicDirector.TensionFor(false, true, false, false, 30));
            Assert.AreEqual(0, MusicDirector.TensionFor(true, true, false, false, 5));
            Assert.AreEqual(0, MusicDirector.TensionFor(false, false, false, false, 5));
            Assert.AreEqual(0, MusicDirector.TensionFor(false, true, true, false, 5));
            Assert.AreEqual(0, MusicDirector.TensionFor(false, true, false, true, 5));
        }

        [UnityTest]
        public IEnumerator Musikregler_speichert_und_schaltet_die_echte_Menuequelle_stumm()
        {
            float original = GameSettings.MusicVolume;
            try
            {
                yield return MenuUiHarness.LadeMenue();
                var ui = MenuUiHarness.Ui();
                var root = ui.RootForTests;
                var oldWidth = root.style.width;
                var oldHeight = root.style.height;
                Assert.IsTrue(ui.ClickForTests("nav-einstellungen"));
                var slider = ui.RootForTests.Q<Slider>("slider-music");
                Assert.IsNotNull(slider);
                try
                {
                    root.style.width = 1280f;
                    root.style.height = 720f;
                    yield return new WaitForSecondsRealtime(1f);
                    var scroll = root.Q<ScrollView>("settings-scroll");
                    Assert.IsNotNull(scroll, "Alle Regler muessen auch im kleinen Fenster erreichbar sein.");
                    scroll.ScrollTo(slider);
                    for (int i = 0; i < 5; i++) yield return null;
                    var viewport = scroll.contentViewport.worldBound;
                    Assert.Greater(slider.worldBound.height, 10f, "Musikregler wurde zusammengedrueckt.");
                    Assert.GreaterOrEqual(slider.worldBound.yMin, viewport.yMin - 1f);
                    Assert.LessOrEqual(slider.worldBound.yMax, viewport.yMax + 1f);
                }
                finally
                {
                    root.style.width = oldWidth;
                    root.style.height = oldHeight;
                }
                slider.value = .6f;
                yield return new WaitForSecondsRealtime(1f);
                Assert.AreEqual(.6f, GameSettings.MusicVolume, .001f);
                Assert.AreEqual(.6f, PlayerPrefs.GetFloat("infront.musicVolume"), .001f);
                Assert.IsNotNull(MusicDirector.Instance);
                Assert.Greater(MusicDirector.Instance.MenuVolumeForTests, 0f);
                Assert.IsTrue(MusicDirector.Instance.MenuPlayingForTests, "Die Musikquelle muss tatsächlich spielen.");
                slider.value = 0f;
                yield return null;
                yield return null;
                Assert.AreEqual(0, MusicDirector.Instance.MenuVolumeForTests);
            }
            finally
            {
                GameSettings.MusicVolume = original;
                GameSettings.Save();
            }
        }
    }
}
