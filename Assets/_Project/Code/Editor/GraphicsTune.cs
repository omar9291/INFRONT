using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Infront.EditorTools
{
    /// <summary>
    /// Stellt das URP-Asset auf sinnvolle Desktop-Werte:
    ///  - Adaptive Performance aus (Handy-Funktion, aendert Aufloesung zur Laufzeit -
    ///    war 2026-08 die Ursache der senkrechten Streifen auf dem M1)
    ///  - HDR AN + HDR-Farbgraduierung (ab Nacht 8: es gibt jetzt ACES-Tonemapping,
    ///    Bloom und Farbanpassung ueber PostFxController - dafuer braucht es HDR).
    ///    Sollte der Streifen-Effekt zurueckkommen: im Menue "Bild: Schlicht"
    ///    schaltet die volle Optik wieder ab.
    ///
    /// Headless: Unity -batchmode -quit -executeMethod Infront.EditorTools.GraphicsTune.Apply
    /// </summary>
    public static class GraphicsTune
    {
        const string PipelinePath = "Assets/_Project/Settings/PC_RenderPipeline.asset";
        const string RendererPath = "Assets/_Project/Settings/PC_UniversalRenderer.asset";

        [MenuItem("Infront/Setup/3 - Grafik auf Desktop-Werte")]
        public static void Apply()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (urp == null)
            {
                Debug.LogError($"[Infront] URP-Asset nicht gefunden: {PipelinePath}");
                return;
            }

            var so = new SerializedObject(urp);
            SetBool(so, "m_UseAdaptivePerformance", false);
            SetBool(so, "m_SupportsHDR", true);
            // 0 = LDR, 1 = HDR-Farbgraduierung (bessere Farben mit Tonemapping)
            var grading = so.FindProperty("m_ColorGradingMode");
            if (grading != null) grading.intValue = 1;

            // Nacht 9 / P2: ernste Beleuchtung mit echten Schatten.
            //  - Zusatzlichter (Punktlichter) duerfen jetzt Schatten werfen -
            //    damit dunkeln Waende, Saeulen und Kisten sich gegenseitig ab
            //    und die Objekte "stehen" auf dem Boden statt zu schweben.
            //  - Weiche Schattenkanten.
            //  - Schattenweite und Kaskaden auf die grosse Karte.
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetFloat(so, "m_ShadowDistance", 70f);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetFloat(so, "m_ShadowDepthBias", 1.2f);
            SetFloat(so, "m_ShadowNormalBias", 1.4f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureSsao();

            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();

            Debug.Log($"GRAPHICS_TUNE_OK hdr={urp.supportsHDR} " +
                      $"addLightShadows={urp.supportsAdditionalLightShadows} " +
                      $"softShadows={urp.supportsSoftShadows} shadowDist={urp.shadowDistance} " +
                      $"adaptivePerf={FindBool(urp, "m_UseAdaptivePerformance")}");
        }

        /// <summary>
        /// Umgebungsverdeckung (SSAO) einschalten.
        ///
        /// Der Renderer hatte gar keine Zusatzstufen - <c>m_RendererFeatures</c>
        /// war eine leere Liste. Ohne SSAO fehlt die Abdunklung in Ecken, unter
        /// Kanten und dort, wo ein Gegenstand den Boden beruehrt. Deshalb sahen
        /// Kisten aus, als schwebten sie ueber dem Beton, statt darauf zu stehen.
        /// In einer gedeckelten Halle faellt das noch mehr auf als vorher.
        ///
        /// Die Stufe wird als Unterobjekt in das Renderer-Asset gelegt. Wichtig:
        /// <c>m_RendererFeatures</c> und <c>m_RendererFeatureMap</c> muessen
        /// GLEICH LANG bleiben und die Map muss die lokale Datei-Id enthalten -
        /// sonst nimmt Unity die Stufe stillschweigend nicht an. Genau daran
        /// scheitern solche Skripte sonst.
        ///
        /// Laeuft mehrfach ohne Schaden: ist schon eine SSAO-Stufe drin, passiert nichts.
        /// </summary>
        public static void EnsureSsao()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                Debug.LogWarning($"[Infront] Renderer-Asset nicht gefunden: {RendererPath}");
                return;
            }

            foreach (var vorhanden in data.rendererFeatures)
            {
                if (vorhanden is ScreenSpaceAmbientOcclusion alt)
                {
                    // Schon da - aber die Werte trotzdem nachziehen, sonst
                    // bliebe eine einmal angelegte Stufe fuer immer auf den
                    // alten Zahlen stehen.
                    Werte(alt);
                    EditorUtility.SetDirty(alt);
                    AssetDatabase.SaveAssets();
                    Debug.Log("SSAO_OK schon vorhanden, Werte aufgefrischt");
                    return;
                }
            }

            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            ssao.hideFlags = HideFlags.HideInHierarchy;   // wie Unity es selbst anlegt

            AssetDatabase.AddObjectToAsset(ssao, data);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath, ImportAssetOptions.ForceUpdate);

            Werte(ssao);

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId))
            {
                Debug.LogError("[Infront] SSAO: keine lokale Datei-Id - Stufe wuerde ignoriert.");
                return;
            }

            var so = new SerializedObject(data);
            var liste = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            if (liste == null || map == null)
            {
                Debug.LogError("[Infront] SSAO: m_RendererFeatures/-Map nicht gefunden.");
                return;
            }

            liste.arraySize += 1;
            liste.GetArrayElementAtIndex(liste.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize = liste.arraySize;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"SSAO_OK angelegt features={liste.arraySize} map={map.arraySize} id={localId}");
        }

        /// <summary>Die Zahlen der SSAO-Stufe setzen. Getrennt, damit ein
        /// zweiter Aufruf eine schon vorhandene Stufe auffrischt.</summary>
        /// <summary>Die Zahlen der SSAO-Stufe setzen. Getrennt, damit ein
        /// zweiter Aufruf eine schon vorhandene Stufe auffrischt, statt sie auf
        /// den alten Werten stehen zu lassen.</summary>
        static void Werte(ScreenSpaceAmbientOcclusion ssao)
        {
            // Kraeftig genug, dass man die Abdunklung sieht, aber ohne den
            // typischen dunklen Saum um jede Kante.
            var soFeature = new SerializedObject(ssao);
            SetIfThere(soFeature, "m_Settings.Intensity", 0.85f);
            SetIfThere(soFeature, "m_Settings.Radius", 0.32f);
            SetIfThere(soFeature, "m_Settings.Falloff", 100f);

            // Gemessen auf dem M1: mit 8 Abtastungen fiel das 1-Prozent-Tief von
            // 57 auf 31 Bilder je Sekunde, bei unveraendert 60 im Schnitt - also
            // sichtbares Stottern. Mit 4 bleibt die Abdunklung erhalten und die
            // Kosten im Rahmen.
            SetIntIfThere(soFeature, "m_Settings.SampleCount", 4);
            // Quelle: Tiefe statt Tiefe+Normalen.
            //
            // "Depth Normals" laesst URP einen zusaetzlichen Durchgang ueber die
            // GANZE Szene rendern, nur um Normalen zu sammeln. Seit das Dach
            // rund hundert Objekte dazugebracht hat, kostet dieser Durchgang
            // gemessen das 1-Prozent-Tief: 57 Bilder/s wurden zu 30, und ein
            // Absenken der Abtastungen von 8 auf 4 hat daran nichts geaendert -
            // die Abtastungen waren also nie die Ursache.
            //
            // Aus der Tiefe zurueckgerechnete Normalen sind etwas grober, aber
            // die Abdunklung in Ecken und am Boden bleibt sichtbar.
            SetIntIfThere(soFeature, "m_Settings.NormalSamples", 0);
            SetIntIfThere(soFeature, "m_Settings.Source", 0);
            SetIntIfThere(soFeature, "m_Settings.BlurQuality", 1);     // mittel
            SetBoolIfThere(soFeature, "m_Settings.Downsample", true);
            SetBoolIfThere(soFeature, "m_Settings.AfterOpaque", false);
            soFeature.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetIfThere(SerializedObject so, string prop, float v)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = v;
        }

        static void SetIntIfThere(SerializedObject so, string prop, int v)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = v;
        }

        static void SetBoolIfThere(SerializedObject so, string prop, bool v)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = v;
        }

        static void SetBool(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"[Infront] Property fehlt: {prop}");
        }

        static void SetFloat(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[Infront] Property fehlt: {prop}");
        }

        static void SetInt(SerializedObject so, string prop, int value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[Infront] Property fehlt: {prop}");
        }

        static bool FindBool(Object obj, string prop)
        {
            var p = new SerializedObject(obj).FindProperty(prop);
            return p != null && p.boolValue;
        }
    
        /// <summary>
        /// Sorgt dafuer, dass die zur Laufzeit gesuchten Shader ueberhaupt im
        /// fertigen Spiel liegen.
        ///
        /// Der Fehler dahinter (gefunden 2026-09-04): Unity nimmt nur Shader in
        /// den Build, die von irgendeinem gespeicherten Material benutzt werden.
        /// "Universal Render Pipeline/Lit" steckt in den Karten-Materialien und
        /// ist deshalb da. "Universal Render Pipeline/Unlit" benutzt KEIN
        /// gespeichertes Material - der wird weggeworfen. Im Editor lief alles,
        /// im Build gab Shader.Find dafuer null zurueck, und zehn Effekte fielen
        /// still auf "Sprites/Default" zurueck: Leuchtspur, Muendungsfeuer,
        /// Bodennebel, Staub, Einschlaege, Explosion, Rauchgranate, Menuestaub.
        /// Nachgewiesen an der Rauchwolke: 78 Partikel liefen, Material war
        /// Sprites/Default, im Bild war nichts zu sehen.
        ///
        /// Still ist an dem Fehler das Schlimmste: es gibt keine Meldung, und
        /// im Editor tritt er nie auf.
        /// </summary>
        public static void EnsureShaders()
        {
            string[] gebraucht =
            {
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Particles/Unlit",
            };

            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs == null || gs.Length == 0)
            {
                Debug.LogWarning("[Infront] EnsureShaders: GraphicsSettings nicht lesbar.");
                return;
            }

            var so = new SerializedObject(gs[0]);
            var liste = so.FindProperty("m_AlwaysIncludedShaders");
            if (liste == null)
            {
                Debug.LogWarning("[Infront] EnsureShaders: m_AlwaysIncludedShaders fehlt.");
                return;
            }

            int dazu = 0;
            foreach (var name in gebraucht)
            {
                var sh = Shader.Find(name);
                if (sh == null)
                {
                    Debug.LogWarning($"[Infront] EnsureShaders: '{name}' gibt es im Projekt nicht.");
                    continue;
                }

                bool schonDa = false;
                for (int i = 0; i < liste.arraySize; i++)
                {
                    if (liste.GetArrayElementAtIndex(i).objectReferenceValue == sh) { schonDa = true; break; }
                }
                if (schonDa) continue;

                liste.InsertArrayElementAtIndex(liste.arraySize);
                liste.GetArrayElementAtIndex(liste.arraySize - 1).objectReferenceValue = sh;
                dazu++;
            }

            if (dazu > 0)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[Infront] EnsureShaders: {dazu} Shader ergaenzt, Liste hat jetzt {liste.arraySize}.");
        }

        /// <summary>
        /// Legt zwei echte Material-Dateien fuer durchsichtige Effekte an.
        ///
        /// Warum eine Datei und nicht Shader.Find zur Laufzeit: Unity wirft
        /// nicht nur ungenutzte Shader aus dem Build, sondern auch ungenutzte
        /// SPIELARTEN eines Shaders. "Universal Render Pipeline/Unlit" in die
        /// Liste der immer mitgelieferten Shader zu setzen holt den Shader, aber
        /// nicht seine durchsichtige Spielart - die benutzt ja kein
        /// gespeichertes Material. Ergebnis: das Schluesselwort
        /// _SURFACE_TYPE_TRANSPARENT liess sich zur Laufzeit zwar setzen, es gab
        /// aber gar keinen dazu passenden uebersetzten Code. Rauch und Nebel
        /// blieben harte helle Vielecke.
        ///
        /// Eine gespeicherte Material-Datei loest beides auf einmal: der Shader
        /// bleibt drin UND die Spielart wird uebersetzt. Zur Laufzeit wird das
        /// Material nur noch kopiert.
        /// </summary>
        public static void EnsureFxMaterials()
        {
            const string ordner = "Assets/_Project/Art/Resources/Materials";
            if (!AssetDatabase.IsValidFolder(ordner))
            {
                Debug.LogWarning($"[Infront] EnsureFxMaterials: {ordner} fehlt.");
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[Infront] EnsureFxMaterials: URP/Unlit nicht gefunden.");
                return;
            }

            Baue($"{ordner}/fx_alpha.mat", shader, additiv: false);
            Baue($"{ordner}/fx_additiv.mat", shader, additiv: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Infront] EnsureFxMaterials: fx_alpha und fx_additiv liegen bereit.");
        }

        static void Baue(string pfad, Shader shader, bool additiv)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(pfad);
            bool neu = m == null;
            if (neu) m = new Material(shader);
            m.shader = shader;

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additiv ? 1f : 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additiv ? (float)UnityEngine.Rendering.BlendMode.One
                                            : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (neu) AssetDatabase.CreateAsset(m, pfad);
            else EditorUtility.SetDirty(m);
        }
}
}
