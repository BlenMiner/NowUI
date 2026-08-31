using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace NowUI.Editor
{
    public static class NowVisualHarnessRunner
    {
        const string BaselineRoot = "Assets/NowUITests/Baselines/Visual";
        static readonly GoldenComparisonTolerance DefaultTolerance = new GoldenComparisonTolerance(8, 0.01f);
        static readonly GoldenComparisonTolerance LandingPageTolerance = new GoldenComparisonTolerance(4, 0.0001f);
        static readonly GoldenComparisonTolerance SdfTolerance = new GoldenComparisonTolerance(8, 0.0025f);

        public static void Capture()
        {
            NowHarnessScenarios.renderScale = 2;
            NowHarnessScenarios.brandCaptures = true;
            string outputRoot = NowHarnessScenarios.ReadArgument(
                "-nowuiArtifactsPath",
                Path.Combine(NowHarnessScenarios.ProjectPath(), "artifacts", "local", "visual"));
            string scenarioFilter = NowHarnessScenarios.ReadArgument("-nowuiScenarioFilter", null);

            var captures = new List<NowHarnessCapture>();
            foreach (var scenario in NowHarnessScenarios.All())
            {
                if (!MatchesScenarioFilter(scenario.name, scenarioFilter))
                    continue;

                string outputPath = Path.Combine(outputRoot, $"{scenario.name}.png");
                captures.Add(NowHarnessScenarios.Capture(scenario, outputPath));
            }

            if (captures.Count == 0)
                throw new InvalidOperationException($"No visual scenarios matched '{scenarioFilter}'.");

            File.WriteAllText(Path.Combine(outputRoot, "manifest.json"), NowHarnessScenarios.BuildManifest(captures));
            Debug.Log($"NowUI visual harness wrote {captures.Count} captures to {outputRoot}.");
        }

        public static void CaptureAnimations()
        {
            string outputRoot = NowHarnessScenarios.ReadArgument(
                "-nowuiArtifactsPath",
                Path.Combine(NowHarnessScenarios.ProjectPath(), "artifacts", "local", "animation"));
            string scenarioFilter = NowHarnessScenarios.ReadArgument("-nowuiScenarioFilter", null);
            Directory.CreateDirectory(outputRoot);

            var captures = new List<NowHarnessAnimationCapture>();
            foreach (NowHarnessAnimationScenario scenario in NowHarnessAnimationScenarios.All())
            {
                if (!MatchesScenarioFilter(scenario.name, scenarioFilter))
                    continue;

                string frameDirectory = Path.Combine(outputRoot, scenario.name);
                string gifPath = Path.Combine(outputRoot, $"{scenario.name}.gif");
                captures.Add(NowHarnessAnimationRenderer.Capture(scenario, frameDirectory, gifPath));
            }

            File.WriteAllText(
                Path.Combine(outputRoot, "manifest.json"),
                NowHarnessAnimationRenderer.BuildManifest(captures));

            if (captures.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(scenarioFilter))
                    throw new InvalidOperationException($"No animation scenarios matched '{scenarioFilter}'.");

                Debug.LogWarning("NowUI animation harness has no registered scenarios.");
                return;
            }

            Debug.Log($"NowUI animation harness wrote {captures.Count} frame sequences to {outputRoot}.");
        }

        static bool MatchesScenarioFilter(string scenarioName, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                scenarioName.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void CompareGoldens()
        {
            NowHarnessScenarios.renderScale = 1;
            NowHarnessScenarios.brandCaptures = false;
            bool update = NowHarnessScenarios.HasArgument("-nowuiUpdateBaselines");
            string outputRoot = NowHarnessScenarios.ReadArgument(
                "-nowuiArtifactsPath",
                Path.Combine(NowHarnessScenarios.ProjectPath(), "artifacts", "local", "golden"));
            string baselineRoot = Path.Combine(NowHarnessScenarios.ProjectPath(), BaselineRoot);
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(baselineRoot);

            var failures = new List<string>();
            var captures = new List<NowHarnessCapture>();

            foreach (var scenario in NowHarnessScenarios.All(includeThemeReviews: false))
            {
                if (!scenario.includeInGoldens)
                    continue;

                string actualPath = Path.Combine(outputRoot, $"{scenario.name}.actual.png");
                var capture = NowHarnessScenarios.Capture(scenario, actualPath);
                captures.Add(capture);

                string baselinePath = Path.Combine(baselineRoot, $"{scenario.name}.png");
                if (update)
                {
                    File.Copy(actualPath, baselinePath, overwrite: true);
                    AssetDatabase.ImportAsset(ToProjectRelativePath(baselinePath));
                    continue;
                }

                if (!File.Exists(baselinePath))
                {
                    failures.Add($"{scenario.name}: missing baseline at {ToProjectRelativePath(baselinePath)}");
                    continue;
                }

                GoldenComparisonTolerance tolerance = ToleranceForScenario(scenario.name);
                if (!ImagesMatch(
                        File.ReadAllBytes(baselinePath),
                        File.ReadAllBytes(actualPath),
                        tolerance,
                        out string difference))
                    failures.Add($"{scenario.name}: {difference}");
            }

            File.WriteAllText(Path.Combine(outputRoot, "manifest.json"), NowHarnessScenarios.BuildManifest(captures));

            if (update)
            {
                AssetDatabase.Refresh();
                Debug.Log($"NowUI golden baselines updated in {BaselineRoot}.");
                return;
            }

            if (failures.Count > 0)
                throw new InvalidOperationException("NowUI golden comparison failed:\n" + string.Join("\n", failures));

            Debug.Log($"NowUI golden comparison passed for {captures.Count} captures.");
        }

        internal static GoldenComparisonTolerance ToleranceForScenario(string scenarioName)
        {
            switch (scenarioName)
            {
                case "landing-page-now":
                case "landing-page-now-layout":
                case "landing-page-now-compact":
                case "landing-page-now-layout-compact":
                    return LandingPageTolerance;
                case "sdf-mask-glow-clip":
                case "sdf-mask-gallery":
                case "sdf-planar-primitives":
                case "sdf-radial-primitives":
                    return SdfTolerance;
                default:
                    return DefaultTolerance;
            }
        }

        static bool ImagesMatch(
            byte[] expectedBytes,
            byte[] actualBytes,
            GoldenComparisonTolerance tolerance,
            out string difference)
        {
            var expected = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var actual = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!expected.LoadImage(expectedBytes))
                {
                    difference = "expected image could not be decoded";
                    return false;
                }

                if (!actual.LoadImage(actualBytes))
                {
                    difference = "actual image could not be decoded";
                    return false;
                }

                if (expected.width != actual.width || expected.height != actual.height)
                {
                    difference = $"size changed from {expected.width}x{expected.height} to {actual.width}x{actual.height}";
                    return false;
                }

                return PixelsMatch(expected.GetPixels32(), actual.GetPixels32(), tolerance, out difference);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
                UnityEngine.Object.DestroyImmediate(actual);
            }
        }

        internal static bool PixelsMatch(
            Color32[] expectedPixels,
            Color32[] actualPixels,
            GoldenComparisonTolerance tolerance,
            out string difference)
        {
            if (expectedPixels == null || actualPixels == null)
                throw new ArgumentNullException(expectedPixels == null ? nameof(expectedPixels) : nameof(actualPixels));

            if (expectedPixels.Length != actualPixels.Length)
            {
                difference = $"pixel count changed from {expectedPixels.Length} to {actualPixels.Length}";
                return false;
            }

            int mismatched = 0;
            long totalError = 0;

            for (int i = 0; i < expectedPixels.Length; ++i)
            {
                int error =
                    Math.Abs(expectedPixels[i].r - actualPixels[i].r) +
                    Math.Abs(expectedPixels[i].g - actualPixels[i].g) +
                    Math.Abs(expectedPixels[i].b - actualPixels[i].b) +
                    Math.Abs(expectedPixels[i].a - actualPixels[i].a);

                totalError += error;

                if (error > tolerance.channelTolerance * 4)
                    ++mismatched;
            }

            float mismatchRatio = expectedPixels.Length > 0 ? mismatched / (float)expectedPixels.Length : 0f;
            if (mismatchRatio > tolerance.allowedMismatchRatio)
            {
                float averageError = expectedPixels.Length > 0 ? totalError / (float)expectedPixels.Length : 0f;
                difference = $"{mismatched} pixels differ ({mismatchRatio:P4}, average error {averageError:0.00}; " +
                    $"allowed {tolerance.allowedMismatchRatio:P4} at channel tolerance {tolerance.channelTolerance})";
                return false;
            }

            difference = null;
            return true;
        }

        static string ToProjectRelativePath(string path)
        {
            string project = NowHarnessScenarios.ProjectPath().Replace('\\', '/').TrimEnd('/');
            string full = Path.GetFullPath(path).Replace('\\', '/');
            return full.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length + 1)
                : full;
        }
    }

    internal readonly struct GoldenComparisonTolerance
    {
        public readonly int channelTolerance;
        public readonly float allowedMismatchRatio;

        public GoldenComparisonTolerance(int channelTolerance, float allowedMismatchRatio)
        {
            if (channelTolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(channelTolerance));

            if (allowedMismatchRatio < 0f || allowedMismatchRatio > 1f)
                throw new ArgumentOutOfRangeException(nameof(allowedMismatchRatio));

            this.channelTolerance = channelTolerance;
            this.allowedMismatchRatio = allowedMismatchRatio;
        }
    }
}
