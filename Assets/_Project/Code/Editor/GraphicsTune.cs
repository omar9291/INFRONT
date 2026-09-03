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

            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();

            Debug.Log($"GRAPHICS_TUNE_OK hdr={urp.supportsHDR} " +
                      $"addLightShadows={urp.supportsAdditionalLightShadows} " +
                      $"softShadows={urp.supportsSoftShadows} shadowDist={urp.shadowDistance} " +
                      $"adaptivePerf={FindBool(urp, "m_UseAdaptivePerformance")}");
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
    }
}
