using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Die stilisierte Figur aus Code (<see cref="CharacterVisual"/>) statt der
    /// nackten Kapsel.
    ///
    /// NICHT pruefbar: wie die Figur aussieht oder ob das Laufen gut wirkt.
    /// Geprueft wird: die Figur wird gebaut, die Kapsel wird ausgeblendet, die
    /// eigene Figur ist unsichtbar, und beim Tod kippt sie um.
    /// </summary>
    public sealed class CharacterVisualTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() { yield return MatchTestHarness.Teardown(); }

        static CharacterVisual FirstBotVisual()
        {
            foreach (var m in Combatants.Everyone)
            {
                if (m == null) continue;
                if (m.GetComponent<BotBrain>() == null) continue;
                var cv = m.GetComponent<CharacterVisual>();
                if (cv != null) return cv;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator Figur_wird_gebaut_und_Kapsel_ausgeblendet()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            for (int i = 0; i < 10; i++) yield return null;

            var cv = FirstBotVisual();
            Assert.IsNotNull(cv, "Kein Bot mit CharacterVisual.");
            Assert.IsTrue(cv.HasFigureForTests, "Es wurde keine Figur gebaut.");
            Assert.IsTrue(cv.CapsuleHiddenForTests, "Die alte Kapsel ist noch sichtbar.");
        }

        [UnityTest]
        public IEnumerator Eigene_Figur_ist_unsichtbar()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            for (int i = 0; i < 10; i++) yield return null;

            var cv = player.GetComponent<CharacterVisual>();
            Assert.IsNotNull(cv, "Spieler ohne CharacterVisual.");
            Assert.IsTrue(cv.HiddenForOwnerForTests,
                "Die eigene Figur ist sichtbar - man wuerde in sich selbst schauen.");
        }

        [UnityTest]
        public IEnumerator Figur_kippt_beim_Tod()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            for (int i = 0; i < 10; i++) yield return null;

            var cv = FirstBotVisual();
            Assert.IsNotNull(cv);
            Assert.IsFalse(cv.LeaningForTests, "Testaufbau: Figur war schon umgekippt.");

            cv.GetComponent<Health>().ApplyDamage(9999, NetworkManager.ServerClientId);
            yield return MatchTestHarness.WaitUntil(
                () => !cv.GetComponent<Health>().IsAlive, 3f, "Bot wurde nicht getoetet.");
            for (int i = 0; i < 40; i++) yield return null;

            Assert.IsTrue(cv.LeaningForTests, "Die Figur ist beim Tod nicht umgekippt.");
        }
    }
}
