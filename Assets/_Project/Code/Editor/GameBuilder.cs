using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Baut eine startbare macOS-App zum Selber-Spielen.
    /// Headless: Unity -batchmode -quit -executeMethod Infront.EditorTools.GameBuilder.BuildMac
    /// </summary>
    public static class GameBuilder
    {
        const string OutputDir = "Builds";
        const string AppPath = OutputDir + "/INFRONT.app";

        [MenuItem("Infront/Build/macOS-App bauen")]
        public static void BuildMac()
        {
            Directory.CreateDirectory(OutputDir);

            // Mono statt IL2CPP: schneller gebaut, reicht zum Testen
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Fenster statt Vollbild, damit man rauswechseln kann
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "INFRONT";

            // Kein Retina: sonst rendert ein 1280x720-Fenster in 2560x1440+
            // und die Bildrate bricht auf dem Basis-M1 ein.
            PlayerSettings.macRetinaSupport = false;

            // VSync fix an (gegen zerrissenes Bild)
            QualitySettings.vSyncCount = 1;

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AppPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"BUILD_RESULT {summary.result} size={summary.totalSize} out={AppPath}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.LogError("BUILD_FAILED");
        }
    }
}
