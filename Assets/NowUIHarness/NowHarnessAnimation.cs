using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NowUI.Markdown;
using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// Deterministic timing supplied to every README animation frame. The final
    /// loop endpoint is deliberately excluded, so frame zero is not duplicated
    /// at the end of an encoded loop.
    /// </summary>
    internal readonly struct NowHarnessAnimationFrame
    {
        public readonly int index;
        public readonly int count;
        public readonly float framesPerSecond;
        public readonly float timeSeconds;
        public readonly float deltaTimeSeconds;
        public readonly float durationSeconds;
        public readonly float normalizedTime;

        public NowHarnessAnimationFrame(int index, int count, float framesPerSecond)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (framesPerSecond <= 0f || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond));

            this.index = index;
            this.count = count;
            this.framesPerSecond = framesPerSecond;
            timeSeconds = index / framesPerSecond;
            deltaTimeSeconds = 1f / framesPerSecond;
            durationSeconds = count / framesPerSecond;
            normalizedTime = index / (float)count;
        }
    }

    internal delegate void NowHarnessAnimationDraw(NowRect rect, NowHarnessAnimationFrame frame);

    internal sealed class NowHarnessAnimationScenario
    {
        public readonly string name;
        public readonly int width;
        public readonly int height;
        public readonly int frameCount;
        public readonly float framesPerSecond;
        public readonly NowHarnessAnimationDraw draw;

        public int warmupFrames = 1;
        public Color clearColor = new Color(0.018f, 0.026f, 0.050f, 1f);

        public NowHarnessAnimationScenario(
            string name,
            int width,
            int height,
            int frameCount,
            float framesPerSecond,
            NowHarnessAnimationDraw draw)
        {
            this.name = name;
            this.width = width;
            this.height = height;
            this.frameCount = frameCount;
            this.framesPerSecond = framesPerSecond;
            this.draw = draw;
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Animation scenario names cannot be empty.");

            if (name == "." || name == ".." || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException($"Animation scenario name '{name}' is not a safe file name.");

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Animation scenario '{name}' must have positive dimensions.");

            if (frameCount <= 0)
                throw new InvalidOperationException($"Animation scenario '{name}' must contain at least one frame.");

            if (framesPerSecond <= 0f || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
                throw new InvalidOperationException($"Animation scenario '{name}' must have a finite positive frame rate.");

            if (warmupFrames < 0)
                throw new InvalidOperationException($"Animation scenario '{name}' cannot have a negative warmup frame count.");

            if (draw == null)
                throw new InvalidOperationException($"Animation scenario '{name}' does not have a draw callback.");
        }
    }

    internal sealed class NowHarnessAnimationCapture
    {
        public string name;
        public int width;
        public int height;
        public int frameCount;
        public float framesPerSecond;
        public float durationSeconds;
        public string frameDirectory;
        public string framePattern;
        /// <summary>Absolute output path without an extension. The harness script appends one per encoded container.</summary>
        public string outputStem;
        public int maximumBatchCount;
        public int maximumVertexCount;
        public long elapsedMilliseconds;
    }

    /// <summary>
    /// Scenario providers live in a separate partial declaration. This keeps
    /// README showcases independent from the static visual/golden catalogue.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        public static IReadOnlyList<NowHarnessAnimationScenario> All()
        {
            var scenarios = new List<NowHarnessAnimationScenario>();
            Populate(scenarios);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < scenarios.Count; ++i)
            {
                NowHarnessAnimationScenario scenario = scenarios[i];
                if (scenario == null)
                    throw new InvalidOperationException($"Animation scenario at index {i} is null.");

                scenario.Validate();
                if (!names.Add(scenario.name))
                    throw new InvalidOperationException($"Duplicate animation scenario name '{scenario.name}'.");
            }

            return scenarios;
        }

        static partial void Populate(List<NowHarnessAnimationScenario> scenarios);
    }

    internal static class NowHarnessAnimationRenderer
    {
        const string FramePattern = "frame-%04d.png";

        sealed class ExplicitTimeInputProvider : INowInputProvider
        {
            NowHarnessAnimationFrame _frame;

            public void SetFrame(NowHarnessAnimationFrame frame)
            {
                _frame = frame;
            }

            public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
            {
                snapshot = new NowInputSnapshot(
                    false,
                    default,
                    default,
                    default,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    default,
                    default,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    _frame.index,
                    _frame.timeSeconds);
                return true;
            }
        }

        public static NowHarnessAnimationCapture Capture(
            NowHarnessAnimationScenario scenario,
            string frameDirectory,
            string outputStem)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            scenario.Validate();
            if (string.IsNullOrWhiteSpace(frameDirectory))
                throw new ArgumentException("A frame output directory is required.", nameof(frameDirectory));

            Directory.CreateDirectory(frameDirectory);
            RemovePreviousFrames(frameDirectory);
            ResetFrameState();

            var stopwatch = Stopwatch.StartNew();
            using var renderer = new NowRenderer();
            var target = new RenderTexture(scenario.width, scenario.height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"NowUI Animation Target ({scenario.name})",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();

            int maximumBatchCount = 0;
            int maximumVertexCount = 0;

            try
            {
                Now.SetUIScale(1f);
                var surface = new NowInputSurface(new Vector2(scenario.width, scenario.height));
                var rect = new NowRect(0f, 0f, scenario.width, scenario.height);
                var inputProvider = new ExplicitTimeInputProvider();
                var firstFrame = new NowHarnessAnimationFrame(0, scenario.frameCount, scenario.framesPerSecond);
                inputProvider.SetFrame(firstFrame);

                for (int i = 0; i < scenario.warmupFrames; ++i)
                    renderer.Warmup(surface, inputProvider, () => scenario.draw(rect, firstFrame));

                for (int frameIndex = 0; frameIndex < scenario.frameCount; ++frameIndex)
                {
                    var frame = new NowHarnessAnimationFrame(
                        frameIndex,
                        scenario.frameCount,
                        scenario.framesPerSecond);
                    inputProvider.SetFrame(frame);

                    using (NowInput.Begin(inputProvider, surface))
                    using (renderer.Begin(new Vector2(scenario.width, scenario.height)))
                        scenario.draw(rect, frame);

                    renderer.Render(target, clear: true, clearColor: scenario.clearColor);
                    WritePng(target, Path.Combine(frameDirectory, $"frame-{frameIndex:D4}.png"));

                    maximumBatchCount = Mathf.Max(maximumBatchCount, renderer.batchCount);
                    maximumVertexCount = Mathf.Max(
                        maximumVertexCount,
                        renderer.mesh != null ? renderer.mesh.vertexCount : 0);
                }

                stopwatch.Stop();
                return new NowHarnessAnimationCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    frameCount = scenario.frameCount,
                    framesPerSecond = scenario.framesPerSecond,
                    durationSeconds = scenario.frameCount / scenario.framesPerSecond,
                    frameDirectory = Path.GetFullPath(frameDirectory),
                    framePattern = FramePattern,
                    outputStem = Path.GetFullPath(outputStem),
                    maximumBatchCount = maximumBatchCount,
                    maximumVertexCount = maximumVertexCount,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                Now.SetUIScale(1f);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                ResetFrameState();
            }
        }

        public static string BuildManifest(IEnumerable<NowHarnessAnimationCapture> captures)
        {
            if (captures == null)
                throw new ArgumentNullException(nameof(captures));

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"captures\": [");

            bool first = true;
            foreach (NowHarnessAnimationCapture capture in captures)
            {
                if (!first)
                    json.AppendLine(",");

                first = false;
                json.Append("    { ");
                json.AppendFormat("\"name\": \"{0}\", ", Escape(capture.name));
                json.AppendFormat("\"width\": {0}, \"height\": {1}, ", capture.width, capture.height);
                json.AppendFormat("\"frameCount\": {0}, ", capture.frameCount);
                json.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "\"framesPerSecond\": {0:0.###}, \"durationSeconds\": {1:0.###}, ",
                    capture.framesPerSecond,
                    capture.durationSeconds);
                json.AppendFormat("\"maximumBatchCount\": {0}, \"maximumVertexCount\": {1}, ", capture.maximumBatchCount, capture.maximumVertexCount);
                json.AppendFormat("\"elapsedMilliseconds\": {0}, ", capture.elapsedMilliseconds);
                json.AppendFormat("\"frameDirectory\": \"{0}\", ", Escape(capture.frameDirectory.Replace('\\', '/')));
                json.AppendFormat("\"framePattern\": \"{0}\", ", Escape(capture.framePattern));
                json.AppendFormat("\"outputStem\": \"{0}\"", Escape(capture.outputStem.Replace('\\', '/')));
                json.Append(" }");
            }

            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        static void RemovePreviousFrames(string frameDirectory)
        {
            string[] previousFrames = Directory.GetFiles(frameDirectory, "frame-*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < previousFrames.Length; ++i)
                File.Delete(previousFrames[i]);
        }

        static void ResetFrameState()
        {
            NowSdf.Reset();
            NowTheme.Reset();
            NowInput.Reset();
            NowFocus.Reset();
            NowControlState.Reset();
            NowControls.Reset();
            NowLayout.Reset();
            NowOverlay.Reset();
            NowContextMenu.Reset();
            NowMarkdown.Reset();
        }

        static void WritePng(RenderTexture target, string path)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;

            try
            {
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
