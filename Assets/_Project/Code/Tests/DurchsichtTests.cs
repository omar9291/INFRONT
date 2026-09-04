using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Durchsichtige Effekte: Rauch, Nebel, Staub, Leuchtspur, Einschlaege.
    ///
    /// Der Zustand davor waren ZWEI Fehler uebereinander, die sich gegenseitig
    /// versteckt haben:
    ///
    /// 1. "Universal Render Pipeline/Unlit" lag gar nicht im fertigen Spiel.
    ///    Unity nimmt nur Shader mit, die ein gespeichertes Material benutzt -
    ///    und kein gespeichertes Material benutzte diesen. Shader.Find gab im
    ///    Build null zurueck, zehn Effekte fielen still auf "Sprites/Default".
    ///    Im Editor lief alles, im Spiel nicht. Ohne Fehlermeldung.
    ///
    /// 2. Bei den URP-Shadern reicht es nicht, _Surface und die Mischfaktoren
    ///    zu setzen. Ohne das Schluesselwort _SURFACE_TYPE_TRANSPARENT setzt
    ///    der Shader die Deckkraft fest auf 1. Und selbst mit Schluesselwort
    ///    fehlt die passende uebersetzte Spielart, wenn kein gespeichertes
    ///    Material sie benutzt.
    ///
    /// Fehler 2 war unsichtbar, solange Fehler 1 alles auf Sprites/Default
    /// umgeleitet hat - der ist naemlich von Haus aus durchsichtig. Erst nach
    /// dem Beheben von 1 lag statt einer Rauchwolke ein Haufen harter heller
    /// Vielecke im Bild.
    ///
    /// NICHT pruefbar: wie es aussieht. Pruefbar: dass die Vorlagen da sind,
    /// dass das Schluesselwort sitzt und dass nichts mehr auf Sprites/Default
    /// zurueckfaellt.
    /// </summary>
    public sealed class DurchsichtTests
    {
        [Test]
        public void Die_beiden_Effekt_Vorlagen_liegen_in_Resources()
        {
            Assert.IsTrue(UrpMaterial.VorlagenDaForTests,
                "Resources/Materials/fx_alpha und fx_additiv muessten da sein. Sie sind "
                + "der Grund, warum die durchsichtige Spielart des Shaders ueberhaupt "
                + "uebersetzt wird. Anlegen: GraphicsTune.EnsureFxMaterials (laeuft im "
                + "SceneBuilder.Build mit).");
        }

        [Test]
        public void Ein_Effekt_Material_ist_wirklich_auf_durchsichtig_gestellt()
        {
            var m = UrpMaterial.NeuFx(additiv: false, "PruefAlpha");
            Assert.IsTrue(UrpMaterial.IstDurchsichtigForTests(m),
                "Ohne _SURFACE_TYPE_TRANSPARENT liefert der Shader ueberall Deckkraft 1 "
                + "- das Ergebnis sind harte Vielecke statt einer Wolke.");
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Kein_Effekt_faellt_mehr_auf_Sprites_Default_zurueck()
        {
            foreach (bool additiv in new[] { false, true })
            {
                var m = UrpMaterial.NeuFx(additiv, "Pruef");
                Assert.AreNotEqual("Sprites/Default", m.shader.name,
                    $"additiv={additiv}: Sprites/Default ist die Rueckfallebene fuer den "
                    + "Fall, dass der URP-Shader fehlt. Taucht er hier auf, liegt der "
                    + "Shader wieder nicht im Build (GraphicsTune.EnsureShaders).");
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void Additiv_und_Alpha_mischen_unterschiedlich()
        {
            var alpha = UrpMaterial.NeuFx(additiv: false, "PruefA");
            var add = UrpMaterial.NeuFx(additiv: true, "PruefB");

            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
                alpha.GetFloat("_DstBlend"), 0.01f,
                "Rauch und Nebel decken ab - das ist Alpha-Mischung.");
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.One,
                add.GetFloat("_DstBlend"), 0.01f,
                "Leuchtspur und Muendungsfeuer sind Licht - das ist additiv.");

            Object.DestroyImmediate(alpha);
            Object.DestroyImmediate(add);
        }

        [UnityTest]
        public IEnumerator Die_Rauchwolke_ist_eine_Wolke_und_keine_Kugel()
        {
            var go = new GameObject("PruefRauch");
            go.transform.position = new Vector3(0f, 2f, 0f);
            var rauch = go.AddComponent<SmokeVolume>();
            rauch.Init(4f, 30f);

            yield return null;   // Start() laufen lassen
            yield return null;

            var ps = go.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(ps,
                "Der Rauch war frueher eine einzige undurchsichtige Kugel aus URP/Lit - "
                + "im Bild eine weisse Billardkugel mitten in der Halle. Jetzt muessten "
                + "es Partikel sein.");

            var mf = go.GetComponentsInChildren<MeshFilter>()
                       .FirstOrDefault(f => f.sharedMesh != null
                                            && f.sharedMesh.name.Contains("Sphere"));
            Assert.IsNull(mf, "Es haengt immer noch eine Kugel am Rauch.");

            var r = ps.GetComponent<ParticleSystemRenderer>();
            Assert.IsTrue(UrpMaterial.IstDurchsichtigForTests(r.sharedMaterial),
                "Eine Rauchwolke, durch die man nicht hindurchsieht, ist keine.");

            Object.DestroyImmediate(go);
        }
    }
}
