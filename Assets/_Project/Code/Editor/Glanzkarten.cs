using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Baut aus den mitgelieferten Rauheits-Karten die Karte, die URP wirklich
    /// liest.
    ///
    /// Das Problem: die heruntergeladenen Saetze (ambientCG, Poly Haven)
    /// bringen eine ROUGHNESS-Karte mit. URP/Lit kennt keine Roughness. Es
    /// liest <c>_MetallicGlossMap</c>: RGB = Metall, ALPHA = Glaette. Solange
    /// niemand das umrechnet, liegen die Rauheits-Karten ungenutzt herum und
    /// jede Flaeche bekommt denselben festen Glanzwert - der Grund, warum die
    /// Halle wie eine gut beleuchtete Graukiste aussah.
    ///
    /// Also: smoothness = 1 - roughness ins Alpha, Metall ins RGB, als PNG
    /// neben die Quelldateien. Danach nimmt <see cref="AssetImporterTools"/>
    /// die Karte beim Materialbau automatisch mit.
    ///
    /// Warum das hier in C# steht und nicht in einem Skript daneben: in diesem
    /// Projekt wird alles vom Editor-Code erzeugt. Ein Bildbearbeitungsschritt,
    /// den nur ein Zettel beschreibt, ist beim naechsten Textur-Download
    /// verloren.
    /// </summary>
    public static class Glanzkarten
    {
        const string Endung = "_MetalSmooth.png";

        static readonly string[] RauhHinweise  = { "roughness", "_rough", "_rgh" };
        static readonly string[] MetallHinweise = { "metalness", "metallic", "_metal", "_mtl" };

        /// <summary>Metallwert fuer Saetze, die keine eigene Metall-Karte
        /// mitbringen. Lackierter Stahl ist ueberwiegend nicht metallisch;
        /// 0,3 ist der Wert, der vorher pro Objekt gesetzt wurde.</summary>
        static readonly Dictionary<string, float> MetallOhneKarte = new Dictionary<string, float>
        {
            { "deckung_metall", 0.30f },
        };

        [MenuItem("Infront/Assets/Glanzkarten aus Rauheit bauen")]
        public static void BaueAlle()
        {
            var ordner = new List<string>();
            if (Directory.Exists("Assets/_Project/Art/Textures"))
                ordner.AddRange(Directory.GetDirectories("Assets/_Project/Art/Textures"));
            if (Directory.Exists("Assets/_Project/Art/Models"))
                ordner.AddRange(Directory.GetDirectories("Assets/_Project/Art/Models")
                    .Select(d => Path.Combine(d, "textures"))
                    .Where(Directory.Exists));

            int gebaut = 0, uebersprungen = 0;
            foreach (string o in ordner)
                foreach (var satz in Saetze(o))
                    if (Baue(o, satz.basis, satz.rauh, satz.metall)) gebaut++;
                    else uebersprungen++;

            AssetDatabase.Refresh();
            Debug.Log($"[Glanz] {gebaut} Karte(n) gebaut, {uebersprungen} uebersprungen.");
        }

        /// <summary>Alle Rauheits-Karten eines Ordners, jeweils mit der
        /// Metall-Karte, die zum selben Namensstamm gehoert. Ein Ordner kann
        /// mehrere Saetze enthalten (z. B. Waffe + Zubehoer).</summary>
        static IEnumerable<(string basis, string rauh, string metall)> Saetze(string ordner)
        {
            var dateien = Directory.GetFiles(ordner)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(Endung))
                .ToArray();

            foreach (string rauh in dateien.Where(f => Passt(f, RauhHinweise)))
            {
                string basis = Stamm(rauh, RauhHinweise);
                string metall = dateien.FirstOrDefault(
                    f => Passt(f, MetallHinweise) && Stamm(f, MetallHinweise) == basis);
                yield return (basis, rauh, metall);
            }
        }

        static bool Passt(string pfad, string[] hinweise)
        {
            string n = Path.GetFileName(pfad).ToLowerInvariant();
            return hinweise.Any(h => n.Contains(h));
        }

        /// <summary>Der Dateiname ohne den Karten-Teil - damit rough und metal
        /// desselben Satzes zueinanderfinden.</summary>
        static string Stamm(string pfad, string[] hinweise)
        {
            string n = Path.GetFileNameWithoutExtension(pfad).ToLowerInvariant();
            foreach (string h in hinweise)
            {
                int i = n.IndexOf(h, System.StringComparison.Ordinal);
                if (i >= 0) return n.Substring(0, i).TrimEnd('_', '-');
            }
            return n;
        }

        static bool Baue(string ordner, string basis, string rauhPfad, string metallPfad)
        {
            var rauh = Lies(rauhPfad);
            if (rauh == null)
            {
                Debug.LogWarning($"[Glanz] {Path.GetFileName(rauhPfad)} nicht lesbar - uebersprungen.");
                return false;
            }

            var metall = metallPfad != null ? Lies(metallPfad) : null;

            int w = rauh.width, h = rauh.height;
            var rp = rauh.GetPixels();
            Color[] mp = null;
            if (metall != null && metall.width == w && metall.height == h) mp = metall.GetPixels();

            // Ordnername entscheidet ueber den Ersatz-Metallwert.
            string schluessel = new DirectoryInfo(ordner).Name.ToLowerInvariant();
            if (schluessel == "textures") schluessel = new DirectoryInfo(ordner).Parent.Name.ToLowerInvariant();
            float ersatzMetall = MetallOhneKarte.TryGetValue(schluessel, out float v) ? v : 0f;

            var ziel = new Color[rp.Length];
            for (int i = 0; i < rp.Length; i++)
            {
                float glaette = Mathf.Clamp01(1f - rp[i].r);          // smoothness = 1 - roughness
                float m = mp != null ? mp[i].r : ersatzMetall;
                ziel[i] = new Color(m, m, m, glaette);
            }

            var aus = new Texture2D(w, h, TextureFormat.RGBA32, false, linear: true);
            aus.SetPixels(ziel);
            aus.Apply(false);

            string pfad = Path.Combine(ordner, basis + Endung);
            File.WriteAllBytes(pfad, aus.EncodeToPNG());
            Object.DestroyImmediate(aus);

            AssetDatabase.ImportAsset(pfad, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Glanz] {Path.GetFileName(pfad)}  ({w}x{h}, "
                      + $"Metall {(mp != null ? "aus Karte" : ersatzMetall.ToString("0.00"))})");
            return true;
        }

        /// <summary>Macht eine Textur lesbar und ohne sRGB-Kurve - eine
        /// Rauheits-Karte ist ein Messwert, kein Farbbild. Wer das vergisst,
        /// bekommt eine verbogene Kennlinie und wundert sich ueber matschigen
        /// Glanz.</summary>
        static Texture2D Lies(string pfad)
        {
            var ti = AssetImporter.GetAtPath(pfad) as TextureImporter;
            if (ti == null) return null;

            bool aendern = false;
            if (!ti.isReadable) { ti.isReadable = true; aendern = true; }
            if (ti.sRGBTexture) { ti.sRGBTexture = false; aendern = true; }
            if (ti.textureType != TextureImporterType.Default)
            { ti.textureType = TextureImporterType.Default; aendern = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
            { ti.textureCompression = TextureImporterCompression.Uncompressed; aendern = true; }
            if (aendern) ti.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(pfad);
        }
    }
}
