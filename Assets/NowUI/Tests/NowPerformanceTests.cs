using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using NowUI;
using Object = UnityEngine.Object;

/// <summary>
/// Baseline timings for the immediate-mode hot paths. These exist to catch
/// regressions in draw-call building cost, not to assert absolute numbers:
/// compare against previous runs in the Performance Test Report window.
/// </summary>
public class NowPerformanceTests
{
    const int RectanglesPerFrame = 1000;

    const int LabelsPerFrame = 100;

    const string TextSample = "The quick brown fox jumps over 0123456789";

    static int _overlayState;

    static readonly NowGlassDiagnosticEntry[] _diagnosticScratch = new NowGlassDiagnosticEntry[4];

    sealed class PerfWorldGraphic : NowWorldGraphic
    {
        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            for (int i = 0; i < 24; ++i)
            {
                Now.Rectangle(new NowRect((i * 17) % 260, (i * 11) % 180, 48, 24))
                    .SetColor(Color.white)
                    .SetRadius(3f)
                    .Draw();
            }
        }
    }

    static long AllocatedBytesOrIgnore()
    {
        try
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return 0;
        }
    }

    static void AssertNoAllocAfterWarmup(Action draw, string message)
    {
        draw();
        draw();
        draw();

        long before = AllocatedBytesOrIgnore();
        draw();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0, allocated, message);
    }

    static void DrawDeferredOverlay(int state)
    {
        _overlayState = state;
        Now.Rectangle(new NowRect(8, 8, 24, 24)).SetColor(Color.white).Draw();
    }

    [Test, Performance]
    public void RectangleFrameBuild()
    {
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1920, 1080)))
                    {
                        for (int i = 0; i < RectanglesPerFrame; ++i)
                        {
                            Now.Rectangle(new NowRect((i * 7) % 1800, (i * 13) % 1000, 64, 32))
                                .SetColor(Color.white)
                                .SetRadius(4)
                                .Draw();
                        }
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test, Performance]
    public void TextFrameBuild()
    {
        Assert.NotNull(Now.defaultFont, "Default font resource is required for the text baseline.");

        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1920, 1080)))
                    {
                        for (int i = 0; i < LabelsPerFrame; ++i)
                        {
                            Now.Text(new NowRect(8, (i * 24) % 1000, 600, 24))
                                .SetFontSize(18)
                                .SetColor(Color.white)
                                .Draw(TextSample);
                        }
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test, Performance]
    public void EffectModifierFrameBuild()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(512, 256)))
                    using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 18f))
                               .SetId(12001)
                               .SetSubdivision(4)
                               .Begin())
                    {
                        for (int i = 0; i < 40; ++i)
                        {
                            Now.Rectangle(new NowRect((i * 13) % 460, (i * 7) % 220, 48, 24))
                                .SetColor(Color.white)
                                .Draw();
                        }
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test, Performance]
    public void WorldGraphicRebuild()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var go = new GameObject("Now Perf World Graphic");

        try
        {
            var graphic = go.AddComponent<PerfWorldGraphic>();

            Measure.Method(() => graphic.RebuildNowUI())
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RectangleFrameBuildIsAllocationFreeAfterWarmup()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(640, 360)))
                {
                    for (int i = 0; i < 128; ++i)
                    {
                        Now.Rectangle(new NowRect((i * 7) % 600, (i * 13) % 320, 32, 18))
                            .SetColor(Color.white)
                            .Draw();
                    }
                }
            }

            AssertNoAllocAfterWarmup(DrawFrame, "steady-state rectangle draw-list build must not allocate");
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void TextFrameBuildIsAllocationFreeAfterGlyphWarmup()
    {
        Assert.NotNull(Now.defaultFont, "Default font resource is required for the text allocation baseline.");
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(640, 360)))
                {
                    for (int i = 0; i < 24; ++i)
                    {
                        Now.Text(new NowRect(8, (i * 20) % 320, 500, 20))
                            .SetFontSize(16f)
                            .SetColor(Color.white)
                            .Draw(TextSample);
                    }
                }
            }

            AssertNoAllocAfterWarmup(DrawFrame, "steady-state text draw-list build must not allocate after glyph warmup");
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void EffectMeshModifierIsAllocationFreeAfterWarmup()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(256, 128)))
                using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f))
                           .SetId(12002)
                           .SetSubdivision(4)
                           .Begin())
                {
                    Now.Rectangle(new NowRect(8, 8, 80, 40))
                        .SetColor(Color.white)
                        .Draw();
                }
            }

            AssertNoAllocAfterWarmup(DrawFrame, "steady-state mesh effect modifier must not allocate");
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void EffectTextureModifierStableSizeIsAllocationFreeAfterWarmup()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var drawList = new NowDrawList();
        var sourceRect = new NowRect(8, 8, 80, 40);

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(256, 128)))
                using (NowEffects.Modifier(NowDeformers.Genie(new NowRect(96, 48, 20, 12), 0.5f))
                           .SetId(12003)
                           .SetRenderToTexture()
                           .SetSourceRect(sourceRect)
                           .SetSubdivision(4)
                           .Begin())
                {
                    Now.Rectangle(sourceRect)
                        .SetColor(Color.white)
                        .Draw();
                }
            }

            AssertNoAllocAfterWarmup(DrawFrame, "steady-state texture effect modifier must not allocate at stable size");
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void OverlayIntegerDeferIsAllocationFreeAfterWarmup()
    {
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(128, 64)))
                    NowOverlay.Defer(new NowRect(8, 8, 24, 24), 42, DrawDeferredOverlay);
            }

            AssertNoAllocAfterWarmup(DrawFrame, "steady-state integer overlay defer must not allocate");
            Assert.AreEqual(42, _overlayState);
        }
        finally
        {
            drawList.Dispose();
            NowOverlay.Reset();
        }
    }

    [Test]
    public void GlassDiagnosticsAreAllocationFreeAfterWarmup()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassBlurMaterial"));

        var previousDiagnostics = NowGlassSettings.diagnosticsEnabled;
        int previousCapacity = NowGlassSettings.diagnosticEntryCapacity;
        var drawList = new NowDrawList();
        var commandBuffer = new UnityEngine.Rendering.CommandBuffer();

        try
        {
            NowGlassSettings.ReserveDiagnostics(_diagnosticScratch.Length);
            NowGlassSettings.diagnosticsEnabled = true;

            void DrawFrame()
            {
                commandBuffer.Clear();

                using (drawList.Begin(new Vector2(128, 64)))
                {
                    Now.Rectangle(new NowRect(0, 0, 128, 64))
                        .SetColor(Color.red)
                        .Draw();
                    Now.Glass(new NowRect(16, 12, 64, 32))
                        .SetBlurRadius(12f)
                        .Draw();
                }

                NowRenderer.Draw(commandBuffer, drawList);
                NowGlassSettings.CopyLastFrameDiagnosticsTo(_diagnosticScratch);
            }

            AssertNoAllocAfterWarmup(DrawFrame, "steady-state glass diagnostics must not allocate after storage warmup");
        }
        finally
        {
            commandBuffer.Release();
            drawList.Dispose();
            NowGlassSettings.diagnosticsEnabled = previousDiagnostics;
            NowGlassSettings.ReserveDiagnostics(previousCapacity);
        }
    }
}
