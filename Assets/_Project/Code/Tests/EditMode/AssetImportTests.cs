using System.IO;
using NUnit.Framework;
using Infront.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Infront.Tests
{
    /// <summary>
    /// Prueft das Import-Werkzeug (<see cref="AssetImporterTools"/>) rechnerisch,
    /// ohne dass ein Mensch etwas ansehen muss - das ist die Antwort auf den
    /// alten "headless nicht pruefbar"-Einwand.
    ///
    /// Baut sich seine Test-Texturen selbst (kleine PNGs), laesst das Werkzeug
    /// ein Material daraus bauen und prueft: richtiger Shader, BaseMap gesetzt,
    /// Normalmap wirklich als Normalmap markiert, Datenkarte nicht als sRGB.
    /// Raeumt am Ende alles wieder weg.
    /// </summary>
    public sealed class AssetImportTests
    {
        const string TestFolder = AssetImporterTools.TexturesDir + "/_test_synthetic";
        const string TestMatKey = "_test_synthetic";
        const string TestMatPath = AssetImporterTools.MaterialsDir + "/_test_synthetic.mat";

        [SetUp]
        public void MakeSyntheticTextures()
        {
            Directory.CreateDirectory(TestFolder);
            WritePng(TestFolder + "/Rock_Color.png", new Color(0.5f, 0.4f, 0.3f));
            WritePng(TestFolder + "/Rock_NormalGL.png", new Color(0.5f, 0.5f, 1f));
            WritePng(TestFolder + "/Rock_Roughness.png", new Color(0.6f, 0.6f, 0.6f));
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void CleanUp()
        {
            if (Directory.Exists(TestFolder)) AssetDatabase.DeleteAsset(TestFolder);
            if (File.Exists(TestMatPath)) AssetDatabase.DeleteAsset(TestMatPath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Material_wird_aus_Texturordner_gebaut()
        {
            bool ok = AssetImporterTools.BuildSurfaceMaterial(TestFolder, TestMatKey);
            Assert.IsTrue(ok, "BuildSurfaceMaterial hat die Farb-Karte nicht gefunden.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(TestMatPath);
            Assert.IsNotNull(mat, "Kein Material unter " + TestMatPath);
            Assert.AreEqual("Universal Render Pipeline/Lit", mat.shader.name,
                "Material laeuft nicht auf dem URP/Lit-Shader.");

            Assert.IsNotNull(mat.GetTexture("_BaseMap"), "Keine BaseMap im Material.");
            Assert.IsNotNull(mat.GetTexture("_BumpMap"), "Keine Normalmap im Material.");
            Assert.IsTrue(mat.IsKeywordEnabled("_NORMALMAP"), "_NORMALMAP-Keyword nicht an.");
        }

        [Test]
        public void Normalmap_ist_als_Normalmap_markiert()
        {
            AssetImporterTools.BuildSurfaceMaterial(TestFolder, TestMatKey);

            var nrm = AssetImporter.GetAtPath(TestFolder + "/Rock_NormalGL.png") as TextureImporter;
            Assert.IsNotNull(nrm);
            Assert.AreEqual(TextureImporterType.NormalMap, nrm.textureType,
                "Die Normalmap wurde nicht als Normalmap importiert - sie wuerde falsch aussehen.");

            var col = AssetImporter.GetAtPath(TestFolder + "/Rock_Color.png") as TextureImporter;
            Assert.IsNotNull(col);
            Assert.IsTrue(col.sRGBTexture, "Farb-Karte muss sRGB sein.");

            var rgh = AssetImporter.GetAtPath(TestFolder + "/Rock_Roughness.png") as TextureImporter;
            Assert.IsNotNull(rgh);
            Assert.IsFalse(rgh.sRGBTexture, "Rauheits-Karte darf NICHT als sRGB behandelt werden.");
        }

        [Test]
        public void Ohne_Farbkarte_wird_kein_Material_gebaut()
        {
            // leerer Ordner
            string empty = AssetImporterTools.TexturesDir + "/_test_leer";
            Directory.CreateDirectory(empty);
            AssetDatabase.Refresh();

            bool ok = AssetImporterTools.BuildSurfaceMaterial(empty, "_test_leer");
            Assert.IsFalse(ok, "Aus einem leeren Ordner darf kein Material entstehen.");

            AssetDatabase.DeleteAsset(empty);
            AssetDatabase.Refresh();
        }

        static void WritePng(string path, Color fill)
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
