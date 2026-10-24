using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AS.EditorTools
{
    /// <summary>
    /// Builds the Windows (64-bit) player into Builds/Windows/. Uses the scenes enabled in Build
    /// Settings (SceneBuilder puts the Arena there). Run from the menu (AS/Build/Windows) or headless:
    ///   -executeMethod AS.EditorTools.BuildScript.BuildWindows [-development]
    /// Headless, a failed build exits with code 1 so scripts can tell.
    /// </summary>
    public static class BuildScript
    {
        public const string ProductName = "Arena Shooter";
        public const string WindowsOutput = "Builds/Windows/ArenaShooter.exe";

        [MenuItem("AS/Build/Windows (64-bit)")]
        public static void BuildWindows()
        {
            bool development = Environment.GetCommandLineArgs().Contains("-development");
            var report = Build(BuildTarget.StandaloneWindows64, WindowsOutput, development);
            bool succeeded = report != null && report.summary.result == BuildResult.Succeeded;
            if (Application.isBatchMode) EditorApplication.Exit(succeeded ? 0 : 1);
            else if (succeeded) EditorUtility.RevealInFinder(WindowsOutput);
        }

        static BuildReport Build(BuildTarget target, string output, bool development)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildScript] No scenes enabled in Build Settings. Run AS/Setup/Build Arena Scene first.");
                return null;
            }

            ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = development ? BuildOptions.Development : BuildOptions.None,
            });

            var summary = report.summary;
            string line = $"[BuildScript] {target} {(development ? "development " : "")}build {summary.result}: " +
                          $"{summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalSeconds:0} s, " +
                          $"{summary.totalErrors} errors, {summary.totalWarnings} warnings → {output}";
            if (summary.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line);
            return report;
        }

        /// <summary>Player settings that matter for a desktop build. Written into ProjectSettings, so they stick.</summary>
        static void ConfigurePlayer()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            // Pausing when the window loses focus beats running unattended behind other apps.
            PlayerSettings.runInBackground = false;
        }
    }
}
