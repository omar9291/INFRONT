using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Infront.EditorTools
{
    /// <summary>
    /// Stellt das URP-Asset auf sinnvolle Desktop-Werte:
    ///  - Adaptive Performance aus (Handy-Funktion, aendert Aufloesung zur Laufzeit)
    ///  - HDR aus (kein Tonemapping vorhanden -> ausgewaschenes Bild; spart Bandbreite)
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
            SetBool(so, "m_SupportsHDR", false);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();

            Debug.Log($"GRAPHICS_TUNE_OK hdr={urp.supportsHDR} adaptivePerf={FindBool(urp, "m_UseAdaptivePerformance")}");
        }

        static void SetBool(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"[Infront] Property fehlt: {prop}");
        }

        static bool FindBool(Object obj, string prop)
        {
            var p = new SerializedObject(obj).FindProperty(prop);
            return p != null && p.boolValue;
        }
    }
}
