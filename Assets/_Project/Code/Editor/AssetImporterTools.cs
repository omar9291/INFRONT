using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Baut aus heruntergeladenen CC0-Paketen die Dinge, die das Spiel per
    /// <see cref="AssetLibrary"/> laedt:
    ///
    ///  - <see cref="BuildSurfaceMaterial"/>: aus einem Ordner voller
    ///    Textur-Dateien (ambientCG/Poly Haven) ein URP/Lit-Material unter
    ///    Assets/_Project/Art/Resources/Materials/&lt;key&gt;.mat.
    ///  - <see cref="SetupModelImport"/>: einen Modell-Import (FBX/glTF) auf
    ///    einen Ziel-Maszstab bringen und pruefen, dass die Groesse stimmt.
    ///
    /// Die erste Stufe bleibt bewusst schlicht: BaseMap + NormalMap + skalares
    /// Smoothness/Metallic. Das sieht rund 90 % so gut aus wie die volle
    /// ARM-Verkabelung und hat einen Bruchteil der Fehlerquellen.
    ///
    /// NICHT pruefbar: ob eine Textur gut aussieht oder ob die Kachelgroesse
    /// passt. Pruefbar (siehe AssetImportTests): laedt die Datei fehlerfrei,
    /// hat das Material eine BaseMap, ist die Normalmap als Normalmap markiert,
    /// ist ein importiertes Modell maszstaeblich plausibel.
    /// </summary>
    public static class AssetImporterTools
    {
        public const string ArtRoot = "Assets/_Project/Art";
        public const string ResourcesRoot = ArtRoot + "/Resources";
        public const string MaterialsDir = ResourcesRoot + "/Materials";
        public const string ModelsDir = ResourcesRoot + "/Models";       // fertige Prefabs (Resources.Load)
        public const string RawModelsDir = ArtRoot + "/Models";          // heruntergeladene FBX + textures/
        public const string TexturesDir = ArtRoot + "/Textures";
        public const string SkyboxDir = ArtRoot + "/Sky";
        public const string FiguresDir = ArtRoot + "/Figures";
        public const string AnimatorPath = ResourcesRoot + "/figur_controller.controller";

        // Namensbausteine, an denen die einzelnen Karten erkannt werden.
        static readonly string[] ColorHints = { "color", "diffuse", "_diff", "basecolor", "albedo", "_col" };
        static readonly string[] NormalHints = { "normalgl", "nor_gl", "normal", "_nrm", "_norm", "_nor" };
        static readonly string[] RoughHints = { "roughness", "_rough", "_rgh" };
        static readonly string[] MetalHints = { "metalness", "metallic", "_metal", "_mtl" };
        static readonly string[] AoHints = { "ambientocclusion", "_ao", "occlusion" };

        static readonly string[] TextureExt = { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".exr" };

        [MenuItem("Infront/Assets/Alle Textur-Ordner zu Materialien bauen")]
        public static void BuildAllSurfaceMaterials()
        {
            if (!Directory.Exists(TexturesDir))
            {
                Debug.Log($"[Assets] Kein Ordner {TexturesDir} - nichts zu bauen.");
                return;
            }

            int built = 0;
            foreach (var dir in Directory.GetDirectories(TexturesDir))
            {
                string key = Path.GetFileName(dir).ToLowerInvariant();
                if (BuildSurfaceMaterial(dir, key)) built++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Assets] {built} Material(ien) gebaut.");
        }

        /// <summary>
        /// Sucht in <paramref name="textureFolder"/> die Farb-, Normal- und
        /// Rauheits-Karte, stellt deren Importer richtig und schreibt ein
        /// URP/Lit-Material nach Materials/&lt;matKey&gt;.mat.
        /// Gibt true zurueck, wenn wenigstens eine Farb-Karte gefunden wurde.
        /// </summary>
        public static bool BuildSurfaceMaterial(string textureFolder, string matKey)
        {
            if (!Directory.Exists(textureFolder))
            {
                Debug.LogWarning($"[Assets] Textur-Ordner fehlt: {textureFolder}");
                return false;
            }

            var files = Directory.GetFiles(textureFolder)
                .Where(f => TextureExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();

            return BuildSurfaceMaterialFromMaps(files, matKey);
        }

        /// <summary>
        /// Wie <see cref="BuildSurfaceMaterial"/>, aber mit einer fertigen
        /// Dateiliste statt eines Ordners - fuer Waffen mit mehreren
        /// Material-Gruppen im selben textures-Ordner.
        /// </summary>
        public static bool BuildSurfaceMaterialFromMaps(string[] files, string matKey)
        {
            string color = Pick(files, ColorHints);
            string normal = Pick(files, NormalHints);
            string rough = Pick(files, RoughHints);
            string metal = Pick(files, MetalHints);
            string ao = Pick(files, AoHints);

            if (color == null)
            {
                Debug.LogWarning($"[Assets] Keine Farb-Karte fuer '{matKey}' - uebersprungen.");
                return false;
            }

            SetTextureImporter(color, isNormal: false, isColor: true);
            if (normal != null) SetTextureImporter(normal, isNormal: true, isColor: false);
            if (rough != null) SetTextureImporter(rough, isNormal: false, isColor: false);
            if (metal != null) SetTextureImporter(metal, isNormal: false, isColor: false);
            if (ao != null) SetTextureImporter(ao, isNormal: false, isColor: false);

            Directory.CreateDirectory(MaterialsDir);
            string matPath = MaterialsDir + "/" + matKey + ".mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader) { name = matKey };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            var colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(color);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", colorTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            if (normal != null)
            {
                var nrmTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", nrmTex);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1f);
            }

            // Erste Stufe: skalares Smoothness/Metallic statt Karten-Verkabelung.
            // rough ist das Gegenteil von smoothness - Beton eher rau.
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.32f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal != null ? 0.4f : 0.0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.32f);

            EditorUtility.SetDirty(mat);
            Debug.Log($"[Assets] Material '{matKey}': BaseMap={colorTex != null} Normal={normal != null} " +
                      $"({Path.GetFileName(color)})");
            return true;
        }

        /// <summary>
        /// Bringt einen importierten Modell-Import auf einen Ziel-Maszstab
        /// (laengste Kante der Bounds = <paramref name="targetLongestMeters"/>)
        /// und schaltet fuer Deko/Waffen unnoetiges (Rig, Animation) ab.
        /// Gibt die tatsaechliche laengste Kante nach dem Skalieren zurueck,
        /// oder -1, wenn das Modell nicht ladbar war.
        /// </summary>
        public static float SetupModelImport(string modelPath, float targetLongestMeters, bool keepRig = false)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Assets] Kein ModelImporter fuer {modelPath}");
                return -1f;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            if (!keepRig)
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (go == null) return -1f;

            var bounds = ModelBounds(go);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest <= 0.0001f) return longest;

            // Nur nachjustieren, wenn die Groesse deutlich daneben liegt. WICHTIG:
            // useFileScale bleibt an (sonst faellt die cm->m-Umrechnung des FBX
            // weg und das Modell wird 100x zu grosz). globalScale multipliziert.
            if (targetLongestMeters > 0f)
            {
                float factor = targetLongestMeters / longest;
                if (factor < 0.9f || factor > 1.1f)
                {
                    importer.useFileScale = true;
                    importer.globalScale = factor;
                    importer.SaveAndReimport();

                    go = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    bounds = ModelBounds(go);
                    longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                }
            }

            Debug.Log($"[Assets] Modell {Path.GetFileName(modelPath)}: laengste Kante {longest:0.###} m");
            return longest;
        }

        /// <summary>Speichert ein Prefab aus einem Modell nach Resources/Models/&lt;key&gt;.prefab.</summary>
        public static GameObject SaveModelAsResourcePrefab(string modelPath, string key)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (src == null)
            {
                Debug.LogWarning($"[Assets] Modell nicht ladbar: {modelPath}");
                return null;
            }

            Directory.CreateDirectory(ModelsDir);
            string prefabPath = ModelsDir + "/" + key + ".prefab";

            var instance = Object.Instantiate(src);
            instance.name = key;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool ok);
            Object.DestroyImmediate(instance);

            if (!ok)
            {
                Debug.LogWarning($"[Assets] Prefab konnte nicht gespeichert werden: {prefabPath}");
                return null;
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Assets] Prefab bereit: {prefabPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // ------------------------------------------------------------------
        //  P3: Himmel aus einer HDRI-Datei
        // ------------------------------------------------------------------

        [MenuItem("Infront/Assets/Himmel aus HDRI bauen")]
        public static bool BuildHdriSkybox()
        {
            if (!Directory.Exists(SkyboxDir)) return false;
            var hdr = Directory.GetFiles(SkyboxDir)
                .FirstOrDefault(f => f.ToLowerInvariant().EndsWith(".hdr")
                                  || f.ToLowerInvariant().EndsWith(".exr"));
            if (hdr == null)
            {
                Debug.Log($"[Assets] Keine HDRI in {SkyboxDir} - Himmel bleibt prozedural.");
                return false;
            }
            hdr = hdr.Replace('\\', '/');

            // HDRI als latlong-2D-Textur importieren (fuer Skybox/Panoramic).
            var ti = AssetImporter.GetAtPath(hdr) as TextureImporter;
            if (ti != null)
            {
                bool ch = false;
                if (ti.textureShape != TextureImporterShape.Texture2D) { ti.textureShape = TextureImporterShape.Texture2D; ch = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; ch = true; }
                if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; ch = true; }
                if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; ch = true; }
                if (ch) ti.SaveAndReimport();
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture>(hdr);
            if (tex == null) { Debug.LogWarning("[Assets] HDRI nicht ladbar."); return false; }

            Directory.CreateDirectory(MaterialsDir);
            string matPath = MaterialsDir + "/himmel.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) { Debug.LogWarning("[Assets] Skybox/Panoramic-Shader fehlt."); return false; }
            if (mat == null)
            {
                mat = new Material(shader) { name = "himmel" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader) mat.shader = shader;

            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1f);        // Latitude-Longitude
            if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0f);   // 360
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 0.85f);  // etwas gedimmt zum dunklen Look
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Assets] Himmel-Material aus {Path.GetFileName(hdr)} gebaut.");
            return true;
        }

        // ------------------------------------------------------------------
        //  P4: Deko-Modelle (FBX + eigene Textur-Ordner) zu Resources-Prefabs
        // ------------------------------------------------------------------

        [MenuItem("Infront/Assets/Alle Deko-Modelle bauen")]
        public static void BuildAllDecoModels()
        {
            if (!Directory.Exists(RawModelsDir))
            {
                Debug.Log($"[Assets] Kein Ordner {RawModelsDir} - keine Deko-Modelle.");
                return;
            }

            int built = 0;
            foreach (var dir in Directory.GetDirectories(RawModelsDir))
            {
                string key = Path.GetFileName(dir).ToLowerInvariant();
                if (key.StartsWith("waffe_")) continue;   // Waffen laufen ueber BuildAllWeaponModels
                if (BuildDecoModel(dir, key)) built++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Assets] {built} Deko-Modell(e) gebaut.");
        }

        // Zielgroesse (laengste Kante, Meter) je Waffen-Key.
        static readonly System.Collections.Generic.Dictionary<string, float> WeaponTargetLength = new()
        {
            { "waffe_pistole", 0.22f },
            { "waffe_sniper", 1.15f },
        };

        [MenuItem("Infront/Assets/Alle Waffen-Modelle bauen")]
        public static void BuildAllWeaponModels()
        {
            if (!Directory.Exists(RawModelsDir)) return;

            int built = 0;
            foreach (var dir in Directory.GetDirectories(RawModelsDir))
            {
                string key = Path.GetFileName(dir).ToLowerInvariant();
                if (!key.StartsWith("waffe_")) continue;
                if (BuildWeaponModel(dir, key)) built++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Assets] {built} Waffen-Modell(e) gebaut.");
        }

        /// <summary>
        /// Wie <see cref="BuildDecoModel"/>, aber:
        ///  - mehrere Material-Gruppen (z.B. Koerper + Zubehoer), zugeordnet
        ///    ueber den Namen des FBX-Material-Slots
        ///  - fester Ziel-Maszstab (Waffe im Sichtfeld darf nicht riesig sein)
        ///  - Pivot in die Mitte der Bounds, damit ViewModel sie sauber halten kann
        /// </summary>
        public static bool BuildWeaponModel(string modelFolder, string key)
        {
            var fbx = Directory.GetFiles(modelFolder)
                .FirstOrDefault(f => f.ToLowerInvariant().EndsWith(".fbx"));
            if (fbx == null) return false;
            fbx = fbx.Replace('\\', '/');

            float target = WeaponTargetLength.TryGetValue(key, out var t) ? t : 0.3f;
            SetupModelImport(fbx, target);

            // Textur-Gruppen im textures-Ordner finden (Praefix vor _diff/_color/...).
            string texFolder = modelFolder + "/textures";
            var groups = FindTextureGroups(texFolder);
            var mats = new System.Collections.Generic.Dictionary<string, Material>();
            Directory.CreateDirectory(MaterialsDir);
            foreach (var g in groups)
            {
                string matKey = "wpn_" + key + "_" + g.Key;
                if (BuildSurfaceMaterialFromMaps(g.Value, matKey))
                    mats[g.Key] = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + matKey + ".mat");
            }
            if (mats.Count == 0)
            {
                Debug.LogWarning($"[Assets] Waffe '{key}': keine Textur-Gruppe gefunden.");
                return false;
            }
            Material fallbackMat = null;
            foreach (var m in mats.Values) { fallbackMat = m; break; }

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (src == null) return false;

            Directory.CreateDirectory(ModelsDir);
            var inst = Object.Instantiate(src);
            inst.name = key;
            foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

            CleanWeaponVariants(inst);

            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials;
                var outMats = new Material[slots.Length == 0 ? 1 : slots.Length];
                for (int i = 0; i < outMats.Length; i++)
                {
                    string slotName = i < slots.Length && slots[i] != null ? slots[i].name.ToLowerInvariant() : "";
                    Material chosen = fallbackMat;
                    foreach (var kv in mats)
                        if (slotName.Contains(kv.Key) || kv.Key.Contains(slotName))
                        {
                            chosen = kv.Value;
                            break;
                        }
                    outMats[i] = chosen;
                }
                r.sharedMaterials = outMats;
            }

            string prefabPath = ModelsDir + "/" + key + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath, out bool ok);
            Object.DestroyImmediate(inst);
            if (!ok) return false;
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

            var check = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var b = ModelBounds(check);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            Debug.Log($"[Assets] Waffe '{key}': laengste Kante {longest:0.###} m (Ziel {target}), " +
                      $"{mats.Count} Material-Gruppe(n)");
            return true;
        }

        /// <summary>
        /// Poly-Haven-Waffen-FBX bringen oft ein ganzes "Kit" mit: zwei
        /// Griff-/Schlitten-Varianten (_a und _b), lose Patronen, ein leeres UND
        /// ein volles Magazin. Alles gleichzeitig anzuzeigen sieht doppelt und
        /// wirr aus. Hier wird auf EINE saubere Waffe reduziert.
        /// </summary>
        static void CleanWeaponVariants(GameObject root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var tr in all) names.Add(tr.name.ToLowerInvariant());

            var toKill = new System.Collections.Generic.List<GameObject>();
            foreach (var tr in all)
            {
                if (tr == root.transform) continue;
                string n = tr.name.ToLowerInvariant();

                bool drop =
                    n.Contains("bullet") ||
                    n.Contains("magazine_empty") ||
                    n.Contains("_empty") ||
                    n.Contains("casing") ||
                    // "_b"-Variante entfernen, wenn es die "_a"-Variante gibt
                    (n.EndsWith("_b") && names.Contains(n.Substring(0, n.Length - 2) + "_a"));

                if (drop) toKill.Add(tr.gameObject);
            }

            foreach (var go in toKill)
                if (go != null) Object.DestroyImmediate(go);

            if (toKill.Count > 0)
                Debug.Log($"[Assets] Waffe '{root.name}': {toKill.Count} Varianten-/Deko-Teil(e) entfernt.");
        }

        /// <summary>Gruppiert Textur-Dateien nach dem Namensteil vor _diff/_color/_nor/...</summary>
        static System.Collections.Generic.Dictionary<string, string[]> FindTextureGroups(string folder)
        {
            var result = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            if (!Directory.Exists(folder)) return new();

            string[] mapWords = { "diff", "color", "albedo", "basecolor", "nor_gl", "nor_dx", "normal",
                                  "rough", "roughness", "metal", "metalness", "metallic", "ao", "arm", "alpha", "col" };

            foreach (var f in Directory.GetFiles(folder))
            {
                if (f.EndsWith(".meta")) continue;
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (!TextureExt.Contains(ext)) continue;

                string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                // Praefix = alles vor dem ersten Karten-Wort
                string prefix = name;
                foreach (var w in mapWords)
                {
                    int idx = name.IndexOf("_" + w, System.StringComparison.Ordinal);
                    if (idx > 0) { prefix = name.Substring(0, idx); break; }
                }
                // trailing "_1k" o.ae. weg
                prefix = System.Text.RegularExpressions.Regex.Replace(prefix, @"_\d+k$", "");

                if (!result.TryGetValue(prefix, out var list))
                    result[prefix] = list = new();
                list.Add(f.Replace('\\', '/'));
            }

            var outp = new System.Collections.Generic.Dictionary<string, string[]>();
            foreach (var kv in result)
            {
                // Gruppen-Schluessel: letztes Namensglied (z.B. "accesories"), sonst "body"
                string k = kv.Key;
                int u = k.LastIndexOf('_');
                string shortKey = u >= 0 && u < k.Length - 1 ? k.Substring(u + 1) : "body";
                if (outp.ContainsKey(shortKey)) shortKey = k;   // Kollision vermeiden
                outp[shortKey] = kv.Value.ToArray();
            }
            return outp;
        }

        /// <summary>
        /// Aus einem Ordner mit einer FBX-Datei und einem Unterordner "textures"
        /// ein fertiges Prefab unter Models/&lt;key&gt;.prefab bauen: Material aus
        /// den Texturen, an alle Renderer gehaengt, Collider entfernt.
        /// </summary>
        public static bool BuildDecoModel(string modelFolder, string key)
        {
            var fbx = Directory.GetFiles(modelFolder)
                .FirstOrDefault(f => f.ToLowerInvariant().EndsWith(".fbx")
                                  || f.ToLowerInvariant().EndsWith(".obj"));
            if (fbx == null) return false;
            fbx = fbx.Replace('\\', '/');

            // FBX-Import: keine Rig/Animation/Lichter, Standard-Skalierung.
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi != null)
            {
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importVisibility = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;   // wir setzen eigenes Material
                mi.SaveAndReimport();
            }

            // Material aus dem textures-Unterordner (gleiche Logik wie Flaechen).
            string texFolder = modelFolder + "/textures";
            string matKey = "deco_" + key;
            if (Directory.Exists(texFolder))
                BuildSurfaceMaterial(texFolder, matKey);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + matKey + ".mat");

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (src == null) { Debug.LogWarning($"[Assets] FBX nicht ladbar: {fbx}"); return false; }

            Directory.CreateDirectory(ModelsDir);

            var inst = Object.Instantiate(src);
            inst.name = key;

            foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            if (mat != null)
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }

            string prefabPath = ModelsDir + "/" + key + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath, out bool ok);
            Object.DestroyImmediate(inst);
            if (!ok) { Debug.LogWarning($"[Assets] Prefab fehlgeschlagen: {prefabPath}"); return false; }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

            var check = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var b = ModelBounds(check);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            Debug.Log($"[Assets] Deko '{key}': laengste Kante {longest:0.##} m, Material={mat != null}");
            return true;
        }

        // ------------------------------------------------------------------

        static string Pick(string[] files, string[] hints)
        {
            // Meta-Dateien raus, dann nach dem ersten Treffer eines Bausteins.
            foreach (var hint in hints)
            {
                var hit = files.FirstOrDefault(f =>
                    !f.EndsWith(".meta") &&
                    Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(hint));
                if (hit != null) return hit.Replace('\\', '/');
            }
            return null;
        }

        static void SetTextureImporter(string path, bool isNormal, bool isColor)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            bool changed = false;
            if (isNormal && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (!isNormal && ti.textureType != TextureImporterType.Default)
            {
                ti.textureType = TextureImporterType.Default;
                changed = true;
            }
            if (!isNormal && ti.sRGBTexture != isColor)
            {
                // Farb-Karte: sRGB an. Daten-Karte (rough/metal/ao): sRGB aus.
                ti.sRGBTexture = isColor;
                changed = true;
            }
            if (ti.maxTextureSize > 2048)
            {
                ti.maxTextureSize = 2048;
                changed = true;
            }

            if (changed) ti.SaveAndReimport();
        }

        static Bounds ModelBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        // ------------------------------------------------------------------
        //  P7: Figur aus Mixamo-FBX (braucht den Nutzer - siehe ASSETS.md)
        // ------------------------------------------------------------------
        //
        // Erwartet in Assets/_Project/Art/Figures/:
        //   basis.fbx   - Charakter in T-Pose MIT Haut (Mixamo: "T-Pose")
        //   idle.fbx    - Steh-Animation      ("Idle", Without Skin, In Place)
        //   walk.fbx    - Geh-Animation       ("Walking", ...)
        //   run.fbx     - Lauf-Animation      ("Running", ...)
        //   death.fbx   - Sterbe-Animation    ("Falling Back Death", ...)
        //
        // Ergebnis: Resources/Models/figur.prefab (+ figur_controller.controller).
        // CharacterVisual nimmt das automatisch statt der Wuerfel-Figur.
        //
        // NICHT pruefbar (kein Testlauf, weil die FBX erst der Nutzer besorgt):
        // ob die Animationen sauber blenden, ob die Figur richtig steht.

        [MenuItem("Infront/Assets/Figur aus Mixamo bauen")]
        public static void BuildFigureModel()
        {
            if (!Directory.Exists(FiguresDir))
            {
                Debug.Log($"[Assets] Kein Ordner {FiguresDir} - keine echte Figur, Wuerfel bleiben.");
                return;
            }

            string basis = FindFbx(FiguresDir, "basis", "tpose", "character");
            if (basis == null)
            {
                Debug.LogWarning("[Assets] Keine basis.fbx in Art/Figures - abgebrochen.");
                return;
            }

            // 1) Basis als Humanoid mit eigenem Avatar importieren.
            var basisImp = AssetImporter.GetAtPath(basis) as ModelImporter;
            basisImp.animationType = ModelImporterAnimationType.Human;
            basisImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            basisImp.importAnimation = false;
            basisImp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            basisImp.SaveAndReimport();

            var basisGo = AssetDatabase.LoadAssetAtPath<GameObject>(basis);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(basis).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("[Assets] basis.fbx ergibt kein gueltiges Humanoid-Rig - abgebrochen.");
                return;
            }

            // 2) Animations-FBX auf dasselbe Rig ziehen und den Clip holen.
            // loop=false fuer einmalige Bewegungen. Ohne das lief auch das
            // Sterben in einer Schleife: die Leiche waere endlos wieder
            // aufgestanden und umgefallen.
            AnimationClip Clip(bool loop, params string[] hints)
            {
                string fbx = FindFbx(FiguresDir, hints);
                if (fbx == null) return null;
                var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (imp == null) return null;
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                imp.importAnimation = true;
                imp.SaveAndReimport();

                // Die Schleife MUSS ueber clipAnimations gesetzt werden.
                // AnimationUtility.SetAnimationClipSettings aendert nur die
                // geladene Kopie im Speicher - beim naechsten Import ist die
                // Einstellung wieder weg. Genau daran ist es zuerst
                // gescheitert: danach lief keine einzige Animation mehr in
                // einer Schleife, die Figur stand also still statt zu gehen.
                var defs = imp.defaultClipAnimations;
                if (defs != null && defs.Length > 0)
                {
                    for (int i = 0; i < defs.Length; i++) defs[i].loopTime = loop;
                    imp.clipAnimations = defs;
                    imp.SaveAndReimport();
                }

                var clip = AssetDatabase.LoadAllAssetsAtPath(fbx)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));
                return clip;
            }

            var idle = Clip(true, "idle", "stand");
            var walk = Clip(true, "walk", "geh");
            var run = Clip(true, "run", "lauf", "sprint");
            var death = Clip(false, "death", "tod", "die", "sterb");

            if (idle == null && walk == null && run == null)
            {
                Debug.LogWarning("[Assets] Keine Lauf-Animationen gefunden - abgebrochen.");
                return;
            }

            // 3) Animator-Controller: 1D-Blend auf "Speed" + "Dead"-Bool.
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            var moveState = sm.AddState("Bewegung");
            var blend = new BlendTree { name = "Bewegung", blendType = BlendTreeType.Simple1D, blendParameter = "Speed" };
            AssetDatabase.AddObjectToAsset(blend, ctrl);
            if (idle != null) blend.AddChild(idle, 0f);
            if (walk != null) blend.AddChild(walk, 2.5f);
            if (run != null) blend.AddChild(run, 6f);
            moveState.motion = blend;
            sm.defaultState = moveState;

            if (death != null)
            {
                var deadState = sm.AddState("Tot");
                deadState.motion = death;
                var toDead = sm.AddAnyStateTransition(deadState);
                toDead.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toDead.duration = 0.15f;
                toDead.canTransitionToSelf = false;
                var fromDead = deadState.AddTransition(moveState);
                fromDead.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                fromDead.duration = 0.2f;
            }
            EditorUtility.SetDirty(ctrl);

            // 4) Prefab: Basis + Animator, nach Resources/Models/figur.prefab.
            Directory.CreateDirectory(ModelsDir);
            var inst = Object.Instantiate(basisGo);
            inst.name = "figur";
            foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.avatar = avatar;
            anim.applyRootMotion = false;

            string prefabPath = ModelsDir + "/figur.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath, out bool ok);
            Object.DestroyImmediate(inst);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (ok)
            {
                var b = ModelBounds(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                Debug.Log($"[Assets] Figur gebaut: Hoehe {b.size.y:0.##} m, " +
                          $"Animationen: idle={idle != null} walk={walk != null} run={run != null} death={death != null}");
            }
            else Debug.LogWarning("[Assets] Figur-Prefab fehlgeschlagen.");
        }

        /// <summary>
        /// Listet fuer jedes fertige Deko-Prefab die Masze in Metern auf.
        /// Rein informativ - aendert nichts. Gedacht zum Einbauen neuer Modelle:
        /// ohne die echten Masze wird der Maszstab im SceneBuilder geraten.
        ///
        /// Aufruf headless:
        ///   Unity -batchmode -quit -projectPath ... \
        ///     -executeMethod Infront.EditorTools.AssetImporterTools.ReportDecoBounds
        /// </summary>
        public static void ReportDecoBounds()
        {
            if (!Directory.Exists(ModelsDir))
            {
                Debug.LogWarning($"[Assets] {ModelsDir} gibt es nicht.");
                return;
            }

            var paths = Directory.GetFiles(ModelsDir, "*.prefab")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToArray();

            foreach (var path in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    Debug.LogWarning($"[Bounds] {path} laedt nicht.");
                    continue;
                }

                var b = ModelBounds(go);
                Debug.Log($"BOUNDS {Path.GetFileNameWithoutExtension(path)} " +
                          $"breite={b.size.x:0.##} hoehe={b.size.y:0.##} tiefe={b.size.z:0.##} " +
                          $"mitte=({b.center.x:0.##},{b.center.y:0.##},{b.center.z:0.##})");
            }

            Debug.Log($"BOUNDS_REPORT_OK {paths.Length}");
        }

        static string FindFbx(string dir, params string[] hints)
        {
            var fbxs = Directory.GetFiles(dir)
                .Where(f => f.ToLowerInvariant().EndsWith(".fbx"))
                .Select(f => f.Replace('\\', '/'))
                .ToArray();
            foreach (var h in hints)
            {
                var hit = fbxs.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(h));
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
