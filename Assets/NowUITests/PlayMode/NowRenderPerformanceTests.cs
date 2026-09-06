using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using NowUI;
using NowUI.Sdf;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Portable PlayMode rendering workloads. Frame.Completion includes CPU build,
/// submission, GPU completion and a synchronous one-pixel readback. GPU.FinalDraw
/// optionally times the target clear and final renderer command buffer. It does
/// not include SDF mask capture, which submits during CPU.Build. No GPU timing
/// is estimated by subtracting CPU time. No PNG encoding or file I/O is timed.
/// </summary>
[Version("2")]
public class NowRenderPerformanceTests
{
    const int WarmupFrames = 8;
    const double WarmupSeconds = 1.0;
    const int SampleFrames = 64;
    const int GpuRecorderDelay = 3;
    enum Workload { Clear, Rectangles, Text, Sdf, Morph, Effects, Mask, Glass }

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Clear512() => MeasureFrame(Workload.Clear, 512);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Rectangles512() => MeasureFrame(Workload.Rectangles, 512);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Text512() => MeasureFrame(Workload.Text, 512);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf256Shapes16() => MeasureFrame(Workload.Sdf, 256, 16);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512Shapes1() => MeasureFrame(Workload.Sdf, 512, 1);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512Shapes16() => MeasureFrame(Workload.Sdf, 512, 16);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf1024Shapes16() => MeasureFrame(Workload.Sdf, 1024, 16);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512Shapes64() => MeasureFrame(Workload.Sdf, 512, 64);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512Overdraw8() => MeasureFrame(Workload.Sdf, 512, 16, 8);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512AnimatedMorph() => MeasureFrame(Workload.Morph, 512, 16);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512AnimatedEffects() => MeasureFrame(Workload.Effects, 512, 16);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512AnimatedMaskFull() => MeasureFrame(Workload.Mask, 512, 16);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Sdf512AnimatedMaskHalf() => MeasureFrame(Workload.Mask, 512, 16, maskScale: 0.5f);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Glass512Pane1() => MeasureFrame(Workload.Glass, 512, overlaps: 1);

    [UnityTest, Performance, Category("NowUI.Overview")]
    public IEnumerator Render_Glass512Panes8() => MeasureFrame(Workload.Glass, 512, overlaps: 8);

    static void BuildGraph(NowSdfGraph graph, int side, int shapes, float phase)
    {
        graph.Clear().SetColor(new Color(0.15f, 0.65f, 0.9f, 0.8f));
        for (int i = 0; i < shapes; ++i)
        {
            float angle = i * 2.399963f + phase;
            float offset = i == 0 ? 0f : side * 0.3f;
            graph.SmoothUnion(side * 0.02f).Circle(
                new Vector2(side * 0.5f + Mathf.Cos(angle) * offset,
                    side * 0.5f + Mathf.Sin(angle) * offset), side * 0.18f);
        }
    }

    IEnumerator MeasureFrame(Workload workload, int side, int shapes = 0, int overlaps = 1, float maskScale = 1f)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Rendering benchmarks require a graphics device.");

        int previousVsync = QualitySettings.vSyncCount;
        int previousRate = Application.targetFrameRate;
        var previousFont = Now.defaultFont;
        float previousScale = Now.uiScale;
        bool previousGlassDiagnostics = NowGlassSettings.diagnosticsEnabled;
        int previousDiagnosticCapacity = NowGlassSettings.diagnosticEntryCapacity;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Now.SetUIScale(1f);
        NowGlassSettings.diagnosticsEnabled = false;
        NowSdf.Reset();
        var target = new RenderTexture(side, side, 0, RenderTextureFormat.ARGB32);
        var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        using var renderer = new NowRenderer();
        renderer.glassBlurQuality = NowGlassBlurQuality.Balanced;
        using var commands = new CommandBuffer { name = "NowUI benchmark render" };
        Recorder gpu = null;

        try
        {
            using var allocations = new NowBenchmarkAllocations(".Frame");
            Assert.IsTrue(target.Create());
            Assert.NotNull(Resources.Load<Material>("NowUI/SdfMaterial"));
            Now.defaultFont = Resources.Load<NowFontAsset>("NowUI/NotoSans");
            if (workload == Workload.Text)
                Assert.NotNull(Now.defaultFont);

            var graph = NowSdf.Graph();
            var other = NowSdf.Graph();
            BuildGraph(graph, side, shapes, 0f);
            BuildGraph(other, side, shapes, 0.7f);
            var rect = new NowRect(0f, 0f, side, side);
            // Recorder documents a three-Unity-frame delay. Match each result
            // to the actual Time.frameCount of a measured submission; coroutine
            // loop indices cannot identify delayed or skipped Unity frames.
            var marker = CustomSampler.Create("NowUI.Benchmark." + workload + side + "." + shapes + "." + overlaps + "." + maskScale, true);
            Assert.NotNull(marker);
            gpu = marker.GetRecorder();
            gpu.enabled = true;
            var buildTime = new SampleGroup("CPU.Build", SampleUnit.Millisecond);
            var submitTime = new SampleGroup("CPU.Submit", SampleUnit.Millisecond);
            var completionTime = new SampleGroup("Frame.Completion", SampleUnit.Millisecond);
            var drawGpuTime = new SampleGroup("GPU.FinalDraw", SampleUnit.Millisecond);
            int gpuSamples = 0;
            int measuredFrames = 0;
            int lastPolledFrame = -1;
            var measuredSubmissionFrames = new int[SampleFrames];
            long warmupStart = Stopwatch.GetTimestamp();
            int warmupFrames = 0;

            void CollectGpuSample()
            {
                int currentFrame = Time.frameCount;
                if (currentFrame == lastPolledFrame)
                    return;
                lastPolledFrame = currentFrame;
                int sourceFrame = currentFrame - GpuRecorderDelay;
                if (Array.IndexOf(measuredSubmissionFrames, sourceFrame, 0, measuredFrames) < 0)
                    return;
                if (gpu.isValid && gpu.gpuSampleBlockCount == 1 && gpu.gpuElapsedNanoseconds > 0)
                {
                    Measure.Custom(drawGpuTime, gpu.gpuElapsedNanoseconds / 1000000.0);
                    ++gpuSamples;
                }
            }

            // A handful of microsecond frames warms managed code but does not
            // give GPU clocks/driver state time to settle for this workload.
            for (int frame = 0; measuredFrames < SampleFrames; ++frame)
            {
                bool measuring = frame >= WarmupFrames &&
                    (Stopwatch.GetTimestamp() - warmupStart) / (double)Stopwatch.Frequency >= WarmupSeconds;
                int workloadFrame = measuring ? measuredFrames : frame;
                if (measuring)
                    measuredSubmissionFrames[measuredFrames++] = Time.frameCount;
                else
                    ++warmupFrames;
                allocations.Begin();
                long frameStart = Stopwatch.GetTimestamp();
                float phase = workloadFrame * 0.07f;
                if (workload == Workload.Mask || workload == Workload.Effects)
                    BuildGraph(graph, side, shapes, phase);

                using (renderer.Begin(new Vector2(side, side)))
                {
                    switch (workload)
                    {
                        case Workload.Clear:
                            break;
                        case Workload.Rectangles:
                        case Workload.Glass:
                            for (int i = 0; i < 256; ++i)
                            {
                                float cell = side / 16f;
                                Now.Rectangle(new NowRect((i % 16) * cell, (i / 16) * cell, cell, cell))
                                    .SetRadius(4f).SetColor(new Color((i % 7) / 7f, 0.5f, 0.8f)).Draw();
                            }
                            if (workload == Workload.Glass)
                            {
                                for (int i = 0; i < overlaps; ++i)
                                    // Distinct stable blur radii force ordered captures;
                                    // identical keys would merge all panes into one blur.
                                    Now.Glass(rect.Inset(16f + i * 4f)).SetRadius(12f)
                                        .SetBlurRadius(16f + i).Draw();
                            }
                            break;
                        case Workload.Text:
                            for (int i = 0; i < 100; ++i)
                                Now.Text(new NowRect((i % 4) * (side / 4f), (i / 4) * 18f, side / 4f, 18f))
                                    .SetFontSize(14f).SetColor(Color.white).Draw("AV fi 012345");
                            break;
                        case Workload.Mask:
                            using (NowSdf.Scene(rect, "render-mask").Graph(graph)
                                .SetMaskResolutionScale(maskScale).BeginMask())
                                Now.Rectangle(rect).SetColor(Color.cyan).Draw();
                            break;
                        default:
                            for (int i = 0; i < overlaps; ++i)
                            {
                                var scene = NowSdf.Scene(rect, (NowId)i);
                                scene = workload == Workload.Morph
                                    ? scene.Morph(graph, other, 0.5f + 0.4f * Mathf.Sin(phase))
                                    : scene.Graph(graph);
                                if (workload == Workload.Effects)
                                    scene = scene.SetShadow(new Vector2(6f, 8f), 12f, Color.black)
                                        .SetGlow(16f, Color.cyan).SetOutline(3f, Color.white)
                                        .SetEmboss(new Vector2(-0.6f, -0.8f), 0.3f, 6f);
                                scene.Draw();
                            }
                            break;
                    }
                }
                long buildEnd = Stopwatch.GetTimestamp();
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.BeginSample(marker);
                commands.ClearRenderTarget(false, true, Color.black);
                renderer.Draw(commands, target, side, side);
                commands.EndSample(marker);
                Graphics.ExecuteCommandBuffer(commands);
                long submitEnd = Stopwatch.GetTimestamp();
                var previousTarget = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixel.ReadPixels(new Rect(side / 2, side / 2, 1, 1), 0, 0, false);
                }
                finally
                {
                    RenderTexture.active = previousTarget;
                }
                long frameEnd = Stopwatch.GetTimestamp();
                long allocated = allocations.End();
                if (measuring)
                {
                    Measure.Custom(buildTime, Milliseconds(frameStart, buildEnd));
                    Measure.Custom(submitTime, Milliseconds(buildEnd, submitEnd));
                    Measure.Custom(completionTime, Milliseconds(frameStart, frameEnd));
                    allocations.Report(allocated);
                }

                yield return null;
                CollectGpuSample();
            }

            // Drain the final measured timestamps without issuing extra draws.
            while (Time.frameCount < measuredSubmissionFrames[measuredFrames - 1] + GpuRecorderDelay)
            {
                yield return null;
                CollectGpuSample();
            }

            Measure.Custom(new SampleGroup("GPU.ValidSamples", SampleUnit.Undefined), gpuSamples);
            Measure.Custom(new SampleGroup("Workload.WarmupFrames", SampleUnit.Undefined), warmupFrames);
            Measure.Custom(new SampleGroup("Workload.MinimumWarmupSeconds", SampleUnit.Second), WarmupSeconds);
            Measure.Custom(new SampleGroup("Workload.TargetPixels", SampleUnit.Undefined), side * side);
            Measure.Custom(new SampleGroup("Workload.ShapesPerScene", SampleUnit.Undefined), shapes);
            Measure.Custom(new SampleGroup("Workload.Overlaps", SampleUnit.Undefined), overlaps);
            Measure.Custom(new SampleGroup("Workload.MaskScale", SampleUnit.Undefined), maskScale);
            Measure.Custom(new SampleGroup("Batches", SampleUnit.Undefined), renderer.batchCount);
            Measure.Custom(new SampleGroup("Vertices", SampleUnit.Undefined), renderer.mesh.vertexCount);
            Measure.Custom(new SampleGroup("Memory.TargetBytes", SampleUnit.Byte), Profiler.GetRuntimeMemorySizeLong(target));
            AssertGeometry(renderer, workload, overlaps);
            if (workload == Workload.Sdf || workload == Workload.Morph || workload == Workload.Effects || workload == Workload.Mask)
                Assert.Greater(pixel.GetPixel(0, 0).g, 0.1f, "SDF content must reach the render target.");

            if (workload == Workload.Glass)
            {
                // Verify real capture/blur rather than a cheap fallback, using
                // an untimed replay so diagnostics do not change measured cost.
                NowGlassSettings.ReserveDiagnostics(overlaps);
                NowGlassSettings.diagnosticsEnabled = true;
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(false, true, Color.black);
                renderer.Draw(commands, target, side, side);
                Graphics.ExecuteCommandBuffer(commands);
                var diagnostics = NowGlassSettings.lastFrameDiagnostics;
                Assert.AreEqual(overlaps, diagnostics.paneCount);
                Assert.AreEqual(0, diagnostics.fallbackCount, "Glass must execute its backdrop blur.");
                Assert.Greater(diagnostics.blurPasses, 0);
                Measure.Custom(new SampleGroup("Workload.GlassBlurPasses", SampleUnit.Undefined), diagnostics.blurPasses);
                Measure.Custom(new SampleGroup("Workload.GlassCopiedPixels", SampleUnit.Undefined), diagnostics.copiedPixels);
            }
            AssertPixels(target, workload);
            if (gpuSamples == 0)
                UnityEngine.Debug.Log("GPU.FinalDraw unavailable on this profiler/backend; Frame.Completion remains CPU+GPU+one-pixel readback latency, not pure GPU time.");
        }
        finally
        {
            if (gpu != null)
                gpu.enabled = false;
            Now.defaultFont = previousFont;
            Now.SetUIScale(previousScale);
            NowGlassSettings.ReserveDiagnostics(previousDiagnosticCapacity);
            NowGlassSettings.diagnosticsEnabled = previousGlassDiagnostics;
            NowSdf.Reset();
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixel);
            QualitySettings.vSyncCount = previousVsync;
            Application.targetFrameRate = previousRate;
        }
    }

    static double Milliseconds(long from, long to) => (to - from) * (1000.0 / Stopwatch.Frequency);

    static void AssertGeometry(NowRenderer renderer, Workload workload, int overlaps)
    {
        int vertices = renderer.mesh.vertexCount;
        switch (workload)
        {
            case Workload.Clear:
                Assert.AreEqual(0, vertices);
                break;
            case Workload.Rectangles:
                Assert.AreEqual(256 * 4, vertices);
                break;
            case Workload.Glass:
                Assert.AreEqual((256 + overlaps) * 4, vertices);
                break;
            case Workload.Text:
                Assert.GreaterOrEqual(vertices, 100 * 8 * 4, "All 100 text rows must emit their glyphs.");
                break;
            default:
                Assert.AreEqual(overlaps * 4, vertices, "Each SDF scene/composite must emit one quad.");
                break;
        }
    }

    static void AssertPixels(RenderTexture target, Workload workload)
    {
        // Full validation is deliberately outside all timing/allocation samples.
        // Text has no guaranteed glyph at the one-pixel synchronization point.
        var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            var pixels = readback.GetPixels32();
            int colored = 0;
            foreach (var value in pixels)
                if (value.r > 16 || value.g > 16 || value.b > 16)
                    ++colored;
            if (workload == Workload.Clear)
                Assert.AreEqual(0, colored);
            else
                Assert.Greater(colored, 1000, "The submitted workload must visibly render.");
            if (workload == Workload.Mask)
            {
                Assert.Less(pixels[0].g, 16, "The SDF mask must reject the target corner.");
                Assert.Less(pixels[pixels.Length - 1].g, 16, "The SDF mask must reject the opposite corner.");
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(readback);
        }
    }
}
