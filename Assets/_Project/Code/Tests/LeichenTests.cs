using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Eine Leiche muss auf dem Boden liegen.
    ///
    /// Gemeldet wurde: "sie liegen nicht auf dem Boden, sondern in der Luft".
    /// Ein Bild allein sagt nicht, wie hoch - deshalb wird hier gemessen:
    /// wo liegt die Unterkante der sichtbaren Figur, nachdem die Sterbe-
    /// Animation durch ist, und wo steht ihr Transform?
    /// </summary>
    public sealed class LeichenTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        static BotBrain Gegner(NetworkPlayerController spieler)
        {
            int meins = spieler.GetComponent<TeamMember>().TeamId;
            foreach (var b in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None))
            {
                int seins = b.GetComponent<TeamMember>().TeamId;
                if (seins != meins && seins != Team.None) return b;
            }
            return null;
        }

        /// <summary>
        /// Hoehenbereich der Figur - gemessen an den KNOCHEN, nicht an
        /// Renderer.bounds.
        ///
        /// Renderer.bounds taugt hier nicht: bei einem SkinnedMeshRenderer ist
        /// das der beim Import gebackene Kasten der Ruhepose, mitgedreht mit dem
        /// Transform. Eine flach liegende Figur meldet darueber weiterhin rund
        /// 1,9 m Hoehe. Genau das ist beim ersten Versuch passiert und haette
        /// fast zu der Diagnose "die Leiche steht noch" gefuehrt.
        /// </summary>
        static bool Hoehenbereich(GameObject go, out float unten, out float oben)
        {
            unten = float.MaxValue; oben = float.MinValue;
            bool etwas = false;

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                foreach (var knochen in smr.bones)
                {
                    if (knochen == null) continue;
                    float y = knochen.position.y;
                    unten = Mathf.Min(unten, y); oben = Mathf.Max(oben, y);
                    etwas = true;
                }
            }
            if (etwas) return true;

            // Ersatzfigur aus Wuerfeln (kein Skinning): dort stimmen die Kaesten.
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude <= 0.0001f) continue;
                unten = Mathf.Min(unten, b.min.y); oben = Mathf.Max(oben, b.max.y);
                etwas = true;
            }
            return etwas;
        }

        [UnityTest]
        public IEnumerator Eine_Leiche_liegt_auf_dem_Boden()
        {
            NetworkPlayerController spieler = null;
            yield return MatchTestHarness.LoadReady((p, m) => spieler = p);

            var bot = Gegner(spieler);
            Assert.IsNotNull(bot, "Kein Gegner-Bot da.");

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(spieler, new Vector3(0f, 1f, 0f), 0f);
            Assert.IsTrue(MatchTestHarness.ReviveBotAt(bot, new Vector3(0f, 1f, 6f), out Vector3 platz),
                          "Bot nicht platzierbar.");
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            float bodenY = platz.y;   // die Stelle, auf der er steht
            // Im Batchmode wird nichts gezeichnet. Ein Animator, der nicht auf
            // AlwaysAnimate steht, haelt dann an, weil sein Renderer nie
            // "sichtbar" wird - die Figur bliebe in der Ruhepose stehen und der
            // Test wuerde eine stehende Leiche melden, die es im Spiel nicht
            // gibt. Deshalb hier ausdruecklich durchlaufen lassen.
            foreach (var anim in bot.GetComponentsInChildren<Animator>())
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var health = bot.GetComponent<Health>();
            health.ApplyDamage(999, spieler.gameObject);

            // Die Sterbe-Animation braucht Zeit. Grosszuegig warten.
            yield return new WaitForSeconds(3f);

            Assert.IsFalse(health.IsAlive, "Der Bot lebt noch.");
            Assert.IsTrue(Hoehenbereich(bot.gameObject, out float unten, out float oben),
                          "Von der Leiche ist nichts mehr zu sehen.");

            float luft = unten - bodenY;
            float hoehe = oben - unten;
            var a = bot.GetComponentInChildren<Animator>();
            string zustand = "(kein Animator)";
            if (a != null)
            {
                var si = a.GetCurrentAnimatorStateInfo(0);
                zustand = $"an={a.enabled} cull={a.cullingMode} dead={a.GetBool("Dead")} "
                          + $"tot={si.IsName("Tot")} fortschritt={si.normalizedTime:F2}";
            }
            Debug.Log($"[Infront] LEICHE unten={unten:F2} oben={oben:F2} boden={bodenY:F2} "
                      + $"luft={luft:F2} hoehe={hoehe:F2} transformY={bot.transform.position.y:F2} | {zustand}");

            // Sie muss liegen, nicht stehen. Ueber Knochen gemessen steht eine
            // Figur rund 1,6 m hoch (Fuss bis Kopf), eine liegende deutlich
            // flacher.
            Assert.Less(hoehe, 1.0f, $"Die Leiche steht noch (Hoehe {hoehe:F2} m).");

            // Und sie muss den Boden beruehren. Der tiefste Knochen liegt bei
            // einer liegenden Figur ein Stueck ueber dem Boden, weil der
            // Koerper um ihn herum Dicke hat - 30 cm Spielraum.
            Assert.Less(luft, 0.30f,
                        $"Die Leiche schwebt {luft:F2} m ueber dem Boden.");
        }
    }
}
