using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Prueft, dass das Spiel OHNE echte Asset-Dateien genau wie vorher laeuft
    /// (Rueckfall auf die Code-Geometrie) - und dass <see cref="AssetLibrary"/>
    /// den Rueckfall auch als solchen zaehlt.
    ///
    /// Liegen spaeter echte Modelle unter Resources/Models/, greifen die
    /// zusaetzlichen Prueffungen weiter unten (Mesh hat Eckpunkte, Groesse
    /// plausibel). Fehlen sie, werden diese Pruefungen als "bestanden, nichts
    /// zu pruefen" behandelt - so bleibt der Test in beiden Welten gruen.
    /// </summary>
    public sealed class AssetFallbackTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [Test]
        public void Echte_Schuss_Sounds_liegen_bereit_wenn_vorhanden()
        {
            string[] keys = { "schuss_gewehr", "schuss_mp", "schuss_sniper", "schuss_pistole" };
            int found = 0;
            foreach (var k in keys)
            {
                var clip = Resources.Load<AudioClip>(k);
                if (clip == null) continue;
                found++;
                Assert.Greater(clip.length, 0.03f, $"'{k}' ist praktisch leer ({clip.length:0.###}s).");
                Assert.Less(clip.length, 3f,
                    $"'{k}' ist {clip.length:0.0}s - fuer einen Schuss (bis 14x/s) viel zu lang.");
            }
            if (found == 0)
                Assert.Pass("Noch keine echten Schuss-Sounds - Platzhalter aus ProceduralSfx aktiv.");
        }

        [Test]
        public void Fehlendes_Modell_gibt_null_und_zaehlt_als_Rueckfall()
        {
            AssetLibrary.ResetCounts();
            var go = AssetLibrary.Model("gibt_es_garantiert_nicht_xyz");
            Assert.IsNull(go);
            Assert.AreEqual(0, AssetLibrary.RealCount);
            Assert.AreEqual(1, AssetLibrary.FallbackCount);

            Assert.IsFalse(AssetLibrary.HasModel("gibt_es_garantiert_nicht_xyz"));
        }

        [UnityTest]
        public IEnumerator Figur_baut_sich_auch_ohne_Modelldatei()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            int figures = 0;
            foreach (var cv in Object.FindObjectsByType<CharacterVisual>(FindObjectsSortMode.None))
            {
                Assert.IsTrue(cv.HasFigureForTests || cv.HiddenForOwnerForTests,
                    "Eine Figur wurde weder gebaut noch (als eigener Spieler) versteckt.");
                figures++;
            }
            Assert.Greater(figures, 0, "Keine einzige CharacterVisual in der Arena.");
        }

        [UnityTest]
        public IEnumerator Waffe_in_der_Hand_baut_sich_auch_ohne_Modelldatei()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);
            for (int i = 0; i < 10; i++) yield return null;

            var vm = player.GetComponent<ViewModel>();
            Assert.IsNotNull(vm);
            Assert.IsTrue(vm.HasModelForTests, "Das View-Model (Waffe in der Hand) wurde nicht gebaut.");
        }

        [UnityTest]
        public IEnumerator Echte_Deko_Modelle_sind_wenn_vorhanden_brauchbar()
        {
            // Laeuft nur, wenn wirklich Modelle da sind - sonst nichts zu pruefen.
            string[] keys = { "fass", "muni_kiste", "holz_kiste", "kanister", "rohre", "haengelampe", "sandsack" };
            int found = 0;

            foreach (var key in keys)
            {
                var prefab = Resources.Load<GameObject>("Models/" + key);
                if (prefab == null) continue;
                found++;

                var filters = prefab.GetComponentsInChildren<MeshFilter>();
                Assert.Greater(filters.Length, 0, $"Modell '{key}' hat kein Mesh.");

                int verts = 0;
                foreach (var f in filters)
                    if (f.sharedMesh != null) verts += f.sharedMesh.vertexCount;
                Assert.Greater(verts, 0, $"Modell '{key}' hat 0 Eckpunkte - Import kaputt.");

                // Echte Weltgroesse: Instanz bauen und die Renderer-Bounds messen
                // (beruecksichtigt den Import-Maszstab, anders als sharedMesh.bounds).
                var inst = Object.Instantiate(prefab);
                var rends = inst.GetComponentsInChildren<Renderer>();
                Assert.Greater(rends.Length, 0, $"Modell '{key}' hat keinen Renderer.");
                var wb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) wb.Encapsulate(rends[i].bounds);
                float longest = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
                var mat = rends[0].sharedMaterial;
                Texture baseMap = mat != null && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                bool hasBaseMapSlot = mat != null && mat.HasProperty("_BaseMap");
                Object.DestroyImmediate(inst);

                Assert.Greater(longest, 0.05f, $"Modell '{key}' ist unsichtbar klein ({longest:0.###} m).");
                Assert.Less(longest, 30f, $"Modell '{key}' ist {longest:0} m gross - Maszstab-Fehler.");

                Assert.IsNotNull(mat, $"Modell '{key}' hat kein Material.");
                if (hasBaseMapSlot)
                    Assert.IsNotNull(baseMap,
                        $"Modell '{key}': Material ohne BaseMap - Textur nicht zugewiesen.");
            }

            if (found == 0)
                Assert.Pass("Noch keine Deko-Modelle importiert - nichts zu pruefen.");
            yield break;
        }
    }
}
