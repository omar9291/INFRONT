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

        /// <summary>
        /// Schritt 1 der Realismus-Etappe: die echte Mixamo-Figur wird benutzt,
        /// nicht mehr der Wuerfel-Rueckfall. Zusaetzlich wird geprueft, dass der
        /// Animator wirklich haengt und die vier Zustaende kennt.
        ///
        /// NICHT pruefbar: ob die Bewegungen glaubwuerdig aussehen.
        /// </summary>
        [UnityTest]
        public IEnumerator Echte_Mixamo_Figur_wird_benutzt()
        {
            yield return MatchTestHarness.LoadReady((p, m) => { });
            for (int i = 0; i < 10; i++) yield return null;

            var cv = FirstBotVisual();
            Assert.IsNotNull(cv, "Kein Bot mit CharacterVisual.");
            Assert.IsTrue(cv.UsingRealModelForTests,
                "Der Wuerfel-Rueckfall laeuft noch - figur.prefab fehlt oder ist kaputt.");

            var anim = cv.GetComponentInChildren<Animator>();
            Assert.IsNotNull(anim, "Die Figur hat keinen Animator.");
            Assert.IsNotNull(anim.runtimeAnimatorController, "Kein Animator-Controller gesetzt.");

            bool hatSpeed = false, hatDead = false;
            foreach (var par in anim.parameters)
            {
                if (par.name == "Speed") hatSpeed = true;
                if (par.name == "Dead") hatDead = true;
            }
            Assert.IsTrue(hatSpeed, "Der Animator kennt den Wert 'Speed' nicht.");
            Assert.IsTrue(hatDead, "Der Animator kennt den Wert 'Dead' nicht.");
        }

        /// <summary>
        /// Die Sterbe-Animation darf nicht in einer Schleife laufen - sonst
        /// steht die Leiche endlos wieder auf und faellt erneut um.
        /// </summary>
        [Test]
        public void Sterbe_Animation_laeuft_nicht_in_Schleife()
        {
            var figur = Resources.Load<GameObject>("Models/figur");
            if (figur == null) Assert.Ignore("Keine echte Figur vorhanden - nichts zu pruefen.");

            var anim = figur.GetComponentInChildren<Animator>();
            Assert.IsNotNull(anim, "Die Figur hat keinen Animator.");
            var ctrl = anim.runtimeAnimatorController;
            Assert.IsNotNull(ctrl, "Kein Animator-Controller gesetzt.");

            // Mixamo nennt jeden Clip "mixamo.com" - ueber den Namen ist die
            // Sterbe-Animation also nicht zu finden. Stattdessen wird die
            // Eigenschaft selbst geprueft: idle, walk und run laufen in einer
            // Schleife, das Sterben genau nicht. Also darf von vier Clips
            // genau einer nicht schleifen.
            var clips = ctrl.animationClips;
            if (clips.Length < 4)
                Assert.Ignore($"Nur {clips.Length} Animationen vorhanden - nichts zu pruefen.");

            int ohneSchleife = 0;
            foreach (var c in clips)
                if (!c.isLooping) ohneSchleife++;

            Assert.AreEqual(1, ohneSchleife,
                $"Von {clips.Length} Animationen laufen {clips.Length - ohneSchleife} in einer " +
                "Schleife. Erwartet: idle, walk und run schleifen, das Sterben nicht. " +
                "Sonst steht die Leiche endlos wieder auf.");
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
