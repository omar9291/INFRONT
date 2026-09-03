using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Baut startbare Spiel-Versionen zum Selber-Spielen und Weitergeben.
    ///
    /// Headless:
    ///   macOS:   Unity -batchmode -quit -executeMethod Infront.EditorTools.GameBuilder.BuildMac
    ///   Windows: Unity -batchmode -quit -executeMethod Infront.EditorTools.GameBuilder.BuildWindows
    ///
    /// Windows braucht das Modul "Windows Build Support (Mono)" im Unity Hub.
    /// Fehlt es, bricht BuildWindows sauber ab und meldet das - es geht nichts kaputt.
    /// </summary>
    public static class GameBuilder
    {
        const string OutputDir = "Builds";
        const string MacAppPath = OutputDir + "/INFRONT.app";
        const string WinDir = OutputDir + "/INFRONT-win";
        const string WinExePath = WinDir + "/INFRONT.exe";

        [MenuItem("Infront/Build/macOS-App bauen")]
        public static void BuildMac()
        {
            Directory.CreateDirectory(OutputDir);
            ConfigureCommonPlayerSettings();

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = MacAppPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            RunBuild(options, MacAppPath);
        }

        [MenuItem("Infront/Build/Windows-App bauen")]
        public static void BuildWindows()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.LogError(
                    "BUILD_RESULT Failed size=0 out=" + WinExePath + "\n" +
                    "Das Modul 'Windows Build Support (Mono)' fehlt. " +
                    "Im Unity Hub bei dieser Editor-Version nachinstallieren, dann erneut versuchen.");
                Debug.LogError("BUILD_FAILED");
                return;
            }

            Directory.CreateDirectory(WinDir);
            ConfigureCommonPlayerSettings();

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = WinExePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            RunBuild(options, WinExePath);
        }

        /// <summary>
        /// Einstellungen, die fuer alle Plattformen gleich sind. Bewusst gebuendelt,
        /// damit Mac und Windows garantiert dieselbe Konfiguration bauen.
        /// </summary>
        static void ConfigureCommonPlayerSettings()
        {
            // Mono statt IL2CPP: schneller gebaut, reicht zum Testen
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Start im Vollbild (randloses Fenster in Bildschirmgroesse). Cmd+Tab
            // geht weiterhin, und im Menue kann man auf Fenster (1280x720)
            // zurueckschalten - GraphicsBootstrap.ApplyDisplayMode setzt das beim
            // Start aus den GameSettings.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "INFRONT";
            // Version hochziehen, damit Freunde die Builds auseinanderhalten
            // koennen (Nacht 9: Etappe 3 "Die Welt lebt" + Etappe 5 Waffen).
            PlayerSettings.bundleVersion = "1.1";

            // Kein Retina: sonst rendert das Vollbild auf einem Retina-Panel in
            // 2x Aufloesung und die Bildrate bricht auf dem Basis-M1 ein.
            PlayerSettings.macRetinaSupport = false;

            // VSync fix an (gegen zerrissenes Bild)
            QualitySettings.vSyncCount = 1;
        }

        static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        static void RunBuild(BuildPlayerOptions options, string outPath)
        {
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"BUILD_RESULT {summary.result} size={summary.totalSize} out={outPath}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.LogError("BUILD_FAILED");
        }
    }
}
