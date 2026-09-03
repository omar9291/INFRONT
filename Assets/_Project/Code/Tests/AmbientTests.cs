using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// "Die Welt lebt" - P4: Umgebungston (Krieg drumherum).
    ///
    /// NICHT prüfbar: wie es klingt. Geprüft wird:
    ///  - jeder neue Umgebungston hat einen Platzhalter,
    ///  - das Windbett läuft in Schleife,
    ///  - ein fernes Ereignis fordert wirklich einen Ton an,
    ///  - die Kaufzeit senkt die Windlautstärke.
    /// </summary>
    public sealed class AmbientTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [Test]
        public void Neue_Umgebungstoene_haben_Platzhalter()
        {
            foreach (var id in new[]
                     {
                         SoundId.Wind, SoundId.FernesFeuergefecht, SoundId.Artillerie,
                         SoundId.Hubschrauber, SoundId.MetallKnarzen,
                     })
            {
                var clip = ProceduralSfx.Build(id);
                Assert.IsNotNull(clip, $"Kein Platzhalter für {id}.");
                Assert.Greater(clip.samples, 0, $"Platzhalter für {id} ist leer.");
            }
        }

        [UnityTest]
        public IEnumerator Windbett_laeuft_in_Schleife()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var amb = UnityEngine.Object.FindAnyObjectByType<AmbientWar>();
            Assert.IsNotNull(amb, "Kein AmbientWar in der Arena.");

            for (int i = 0; i < 30; i++) yield return null;

            Assert.IsTrue(amb.WindRunningForTests, "Das Windbett läuft nicht (oder nicht in Schleife).");
            Assert.Greater(amb.WindVolumeForTests, 0f, "Das Windbett ist stumm.");
        }

        [UnityTest]
        public IEnumerator Fernes_Ereignis_fordert_einen_Ton_an()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var amb = UnityEngine.Object.FindAnyObjectByType<AmbientWar>();
            var audio = AudioService.EnsureForTests();
            audio.ResetTestState();

            amb.FireEventForTests();
            for (int i = 0; i < 10; i++) yield return null;

            Assert.Greater(audio.PlayCountForTests, 0, "Ein fernes Ereignis hat keinen Ton angefordert.");
            Assert.IsNotNull(amb.LastEventForTests);
            var expected = new[]
            {
                SoundId.FernesFeuergefecht, SoundId.Artillerie,
                SoundId.Hubschrauber, SoundId.MetallKnarzen,
            };
            Assert.Contains(amb.LastEventForTests.Value, expected, "Unerwarteter Umgebungston.");
        }

        [UnityTest]
        public IEnumerator Kaufzeit_senkt_die_Windlautstaerke()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });

            var amb = UnityEngine.Object.FindAnyObjectByType<AmbientWar>();
            amb.BuyTimeOverrideForTests = false;
            for (int i = 0; i < 120; i++) yield return null;
            float loud = amb.WindVolumeForTests;

            amb.BuyTimeOverrideForTests = true;
            for (int i = 0; i < 120; i++) yield return null;
            float quiet = amb.WindVolumeForTests;

            Assert.Less(quiet, loud * 0.95f,
                $"Die Kaufzeit macht den Wind nicht leiser ({loud:0.000} -> {quiet:0.000}).");
        }
    }
}
