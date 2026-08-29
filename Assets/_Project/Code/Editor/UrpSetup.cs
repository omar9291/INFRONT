using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Infront.EditorTools
{
    /// <summary>
    /// Richtet die Universal Render Pipeline (URP) ein: erzeugt Pipeline- und
    /// Renderer-Asset in Assets/_Project/Settings/ und traegt die Pipeline in
    /// den Graphics-Settings ein.
    ///
    /// Aufruf im Editor ueber Menue "Infront/Setup/1 - URP einrichten" oder
    /// headless:
    ///   Unity -batchmode -quit -executeMethod Infront.EditorTools.UrpSetup.Run
    /// </summary>
    public static class UrpSetup
    {
        const string SettingsDir = "Assets/_Project/Settings";
        const string PipelinePath = SettingsDir + "/PC_RenderPipeline.asset";
        const string RendererPath = SettingsDir + "/PC_UniversalRenderer.asset";
        const string TempRendererPath = "Assets/UniversalRenderer.asset";

        [MenuItem("Infront/Setup/1 - URP einrichten")]
        public static void Run()
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
                AssetDatabase.Refresh();
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);

                // Erzeugt ein vollstaendiges Renderer-Asset (mit allen Ressourcen)
                // unter TempRendererPath und haengt es an die Pipeline.
                pipeline.LoadBuiltinRendererData();
                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssets();

                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(TempRendererPath) != null)
                {
                    string moveError = AssetDatabase.MoveAsset(TempRendererPath, RendererPath);
                    if (!string.IsNullOrEmpty(moveError))
                        Debug.LogWarning($"[Infront] Renderer-Asset konnte nicht verschoben werden: {moveError}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            // Alle Qualitaetsstufen auf dieselbe Pipeline setzen
            int levels = QualitySettings.names.Length;
            int active = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(active, false);

            AssetDatabase.SaveAssets();
            Debug.Log($"URP_SETUP_OK pipeline={AssetDatabase.GetAssetPath(pipeline)} " +
                      $"default={(GraphicsSettings.defaultRenderPipeline != null)}");
        }
    }
}
