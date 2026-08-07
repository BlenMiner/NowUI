using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NowUI.EditorCI
{
    /// <summary>
    /// CI entry point: builds a player for the active build target so platform
    /// problems surface in automation instead of on someone's machine — IL2CPP
    /// compiling the whole managed surface, the platform linker consuming every
    /// native plugin (a WebGL build is the only thing that catches wasm-ld
    /// symbol collisions), and player-side stripping.
    ///
    /// Invoke with:
    ///   Unity -batchmode -nographics -projectPath . -buildTarget WebGL
    ///         -executeMethod NowUI.EditorCI.NowBuildVerification.Build
    /// The method always calls EditorApplication.Exit, so do not pass -quit.
    /// Output goes to NOWUI_BUILD_OUTPUT (or artifacts/build-verification/).
    /// </summary>
    public static class NowBuildVerification
    {
        public static void Build()
        {
            try
            {
                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                ConfigureTarget(target);

                string output = Environment.GetEnvironmentVariable("NOWUI_BUILD_OUTPUT");
                if (string.IsNullOrEmpty(output))
                    output = Path.Combine("artifacts", "build-verification", target.ToString());

                var options = new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    target = target,
                    locationPathName = PlayerLocation(output, target),
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log(
                    $"Build verification: {summary.result} for {summary.platform} " +
                    $"in {summary.totalTime.TotalSeconds:F0}s — " +
                    $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                    $"{summary.totalSize / (1024 * 1024)} MiB");

                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Build verification threw: {exception}");
                EditorApplication.Exit(1);
            }
        }

        static void ConfigureTarget(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                    PlayerSettings.SetScriptingBackend(
                        NamedBuildTarget.Standalone,
                        ScriptingImplementation.IL2CPP);
                    Debug.Log($"Build verification: configured {target} with IL2CPP.");
                    return;

                case BuildTarget.StandaloneOSX:
                    // Do not inherit a runner/project preference that verifies
                    // only Intel or only Apple Silicon. Unity uses 2 for the
                    // universal standalone macOS architecture.
                    PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2);
                    PlayerSettings.SetScriptingBackend(
                        NamedBuildTarget.Standalone,
                        ScriptingImplementation.IL2CPP);
                    Debug.Log("Build verification: configured macOS Universal with IL2CPP.");
                    return;

                case BuildTarget.Android:
                    // The distributed Android plugins are arm64-only. An ARMv7
                    // smoke build would succeed without exercising them.
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    PlayerSettings.SetScriptingBackend(
                        NamedBuildTarget.Android,
                        ScriptingImplementation.IL2CPP);
                    Debug.Log("Build verification: configured Android ARM64 with IL2CPP.");
                    return;
            }

            // WebGL and iOS use IL2CPP by platform design.
        }

        /// <summary>
        /// The docs scene exercises fonts, markup and the code editor, handing
        /// the linker and stripper the widest NowUI surface; any scene works as
        /// a fallback so the build never trivially passes with zero content.
        /// </summary>
        static string[] Scenes()
        {
            const string preferred = "Assets/Scenes/DocsScene.unity";
            if (File.Exists(preferred))
                return new[] { preferred };

            string fallback = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault();

            if (fallback == null)
                throw new InvalidOperationException("No scene found to build.");

            return new[] { fallback };
        }

        static string PlayerLocation(string output, BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(output, "NowUI.exe");
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(output, "NowUI.x86_64");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(output, "NowUI.app");
                case BuildTarget.Android:
                    return Path.Combine(output, "NowUI.apk");
                case BuildTarget.WebGL:
                case BuildTarget.iOS:
                    // Folder-shaped outputs (WebGL player, Xcode project).
                    return output;
                default:
                    return Path.Combine(output, "NowUI");
            }
        }
    }
}
