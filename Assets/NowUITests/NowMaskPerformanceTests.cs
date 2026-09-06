using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Rendering;
using NowUI;
using NowUI.Sdf;

/// <summary>
/// CPU-side baselines for ambient-mask frame building and renderer submission.
/// These tests deliberately keep geometry identical between variants so their
/// deltas isolate mask-stack capture, property-block recording and ordered batch
/// churn. They do not claim to measure fragment-shader or texture-sampling cost.
/// </summary>
public class NowMaskPerformanceTests
{
    enum MaskKind
    {
        None,
        HardRectangle,
        Analytic,
        Texture
    }

    static readonly Vector2 SurfaceSize = new Vector2(1920f, 1080f);

    static readonly NowRect SurfaceRect = new NowRect(0f, 0f, 1920f, 1080f);

    const int StaticDrawCount = 1000;

    const int ChurnDrawCount = 256;

    const int MaximumMaskDepth = 32;

    const int WarmupCount = 5;

    const int MeasurementCount = 20;

    const int AllocationSampleFrames = 16;

    const int SdfMaskCount = 16;

    static readonly string[] StableSdfMaskIds = CreateSdfMaskIds("perf-sdf-mask-stable-");

    static readonly string[] ResizedSdfMaskIds = CreateSdfMaskIds("perf-sdf-mask-resize-");

    readonly NowMaskScope[] _maskScopes = new NowMaskScope[MaximumMaskDepth];

    readonly NowMaskShape[] _analyticMasks = new NowMaskShape[8];

    readonly NowMaskTexture[] _textureMasks = new NowMaskTexture[2];

    NowDrawList _drawList;

    CommandBuffer _commandBuffer;

    Texture2D _coverageTexture0;

    Texture2D _coverageTexture1;

    float _previousUiScale;

    bool _resizedSdfWide;

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/SdfMaterial"));

        _previousUiScale = Now.uiScale;
        Now.SetUIScale(1f);
        NowControls.Reset();
        NowSdf.Reset();

        _drawList = new NowDrawList();
        _commandBuffer = new CommandBuffer
        {
            name = "Now Mask Performance Submission"
        };

        _coverageTexture0 = CreateWhiteCoverageTexture("Now Mask Perf Coverage A");
        _coverageTexture1 = CreateWhiteCoverageTexture("Now Mask Perf Coverage B");
        _textureMasks[0] = NowMaskTexture.Alpha(_coverageTexture0, SurfaceRect);
        _textureMasks[1] = NowMaskTexture.Red(_coverageTexture1, SurfaceRect);

        for (int i = 0; i < _analyticMasks.Length; ++i)
            _analyticMasks[i] = NowMaskShape.RoundedRect(SurfaceRect, 12f + i * 8f);

        _resizedSdfWide = false;
    }

    [TearDown]
    public void TearDown()
    {
        _commandBuffer?.Release();
        _commandBuffer = null;

        _drawList?.Dispose();
        _drawList = null;

        NowSdf.Reset();
        NowControls.Reset();

        DestroyImmediate(_coverageTexture0);
        DestroyImmediate(_coverageTexture1);
        _coverageTexture0 = null;
        _coverageTexture1 = null;

        Now.SetUIScale(_previousUiScale);
    }

    static Texture2D CreateWhiteCoverageTexture(string name)
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[16];

        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static string[] CreateSdfMaskIds(string prefix)
    {
        var ids = new string[SdfMaskCount];

        for (int i = 0; i < ids.Length; ++i)
            ids[i] = prefix + i.ToString("00");

        return ids;
    }

    static void DestroyImmediate(UnityEngine.Object value)
    {
        if (value != null)
            UnityEngine.Object.DestroyImmediate(value);
    }

    static void RecordSteadyStateAllocations(Action drawFrame)
    {
        drawFrame();
        drawFrame();
        drawFrame();

        using var allocations = new NowBenchmarkAllocations();
        allocations.Begin();

        for (int i = 0; i < AllocationSampleFrames; ++i)
            drawFrame();

        long allocated = allocations.End();
        allocations.Report(allocated / (double)AllocationSampleFrames);
    }

    void PushMasks(MaskKind kind, int depth, ref int pushed)
    {
        for (int i = 0; i < depth; ++i)
        {
            _maskScopes[pushed++] = kind switch
            {
                MaskKind.HardRectangle => Now.Mask(SurfaceRect),
                MaskKind.Analytic => Now.Mask(_analyticMasks[i]),
                MaskKind.Texture => Now.Mask(_textureMasks[i]),
                _ => default
            };
        }
    }

    void PopMasks(ref int pushed)
    {
        while (pushed > 0)
        {
            --pushed;
            _maskScopes[pushed].Dispose();
            _maskScopes[pushed] = default;
        }
    }

    static void DrawStaticRectangles()
    {
        for (int i = 0; i < StaticDrawCount; ++i)
        {
            Now.Rectangle(new NowRect((i * 7) % 1800, (i * 13) % 1000, 64f, 32f))
                .SetColor(Color.white)
                .SetRadius(4f)
                .Draw();
        }
    }

    static void DrawChurnRectangle(int index)
    {
        float x = 8f + (index % 32) * 58f;
        float y = 8f + (index / 32) * 40f;
        Now.Rectangle(new NowRect(x, y, 52f, 28f))
            .SetColor(Color.white)
            .SetRadius(4f)
            .Draw();
    }

    void DrawStaticMaskFrame(MaskKind kind, int depth)
    {
        _commandBuffer.Clear();

        using (_drawList.Begin(SurfaceSize))
        {
            int pushed = 0;

            try
            {
                PushMasks(kind, depth, ref pushed);
                DrawStaticRectangles();
            }
            finally
            {
                PopMasks(ref pushed);
            }
        }

        NowRenderer.Draw(_commandBuffer, _drawList);
    }

    void MeasureStaticMaskFrame(MaskKind kind, int depth)
    {
        Action drawFrame = () => DrawStaticMaskFrame(kind, depth);

        Measure.Method(drawFrame)
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .Run();

        DrawStaticMaskFrame(kind, depth);
        Assert.AreEqual(1, _drawList.batchCount, "Stable mask state unexpectedly split the rectangle batch.");
        Assert.AreEqual(StaticDrawCount * 4, _drawList.mesh.vertexCount, "Mask variant changed emitted geometry.");
        Measure.Custom(new SampleGroup("Batches", SampleUnit.Undefined, false), _drawList.batchCount);
        RecordSteadyStateAllocations(drawFrame);
    }

    [Test, Performance]
    public void MaskCpuSubmission_NoMask()
    {
        MeasureStaticMaskFrame(MaskKind.None, 0);
    }

    [Test, Performance]
    public void MaskCpuSubmission_HardRect1()
    {
        MeasureStaticMaskFrame(MaskKind.HardRectangle, 1);
    }

    [Test, Performance]
    public void MaskCpuSubmission_HardRect32()
    {
        MeasureStaticMaskFrame(MaskKind.HardRectangle, 32);
    }

    [Test, Performance]
    public void MaskCpuSubmission_Analytic1()
    {
        MeasureStaticMaskFrame(MaskKind.Analytic, 1);
    }

    [Test, Performance]
    public void MaskCpuSubmission_Analytic4()
    {
        MeasureStaticMaskFrame(MaskKind.Analytic, 4);
    }

    [Test, Performance]
    public void MaskCpuSubmission_Analytic8()
    {
        MeasureStaticMaskFrame(MaskKind.Analytic, 8);
    }

    [Test, Performance]
    public void MaskCpuSubmission_Texture1()
    {
        MeasureStaticMaskFrame(MaskKind.Texture, 1);
    }

    [Test, Performance]
    public void MaskCpuSubmission_Texture2()
    {
        MeasureStaticMaskFrame(MaskKind.Texture, 2);
    }

    void DrawAnalyticChurnFrame(bool alternate)
    {
        _commandBuffer.Clear();

        using (_drawList.Begin(SurfaceSize))
        {
            for (int i = 0; i < ChurnDrawCount; ++i)
            {
                int shapeIndex = alternate ? i & 1 : 0;

                using (Now.Mask(_analyticMasks[shapeIndex]))
                    DrawChurnRectangle(i);
            }
        }

        NowRenderer.Draw(_commandBuffer, _drawList);
    }

    void DrawAlternatingHardMaskFrame()
    {
        _commandBuffer.Clear();
        NowRect hardMaskA = SurfaceRect.Outset(1f);
        NowRect hardMaskB = SurfaceRect.Outset(2f);

        using (_drawList.Begin(SurfaceSize))
        {
            for (int i = 0; i < ChurnDrawCount; ++i)
            {
                using (Now.Mask((i & 1) == 0 ? hardMaskA : hardMaskB))
                    DrawChurnRectangle(i);
            }
        }

        NowRenderer.Draw(_commandBuffer, _drawList);
    }

    void MeasureBatchChurn(Action drawFrame, int expectedBatches)
    {
        Measure.Method(drawFrame)
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .Run();

        drawFrame();
        Assert.AreEqual(expectedBatches, _drawList.batchCount);
        Assert.AreEqual(ChurnDrawCount * 4, _drawList.mesh.vertexCount, "Mask churn changed emitted geometry.");
        Measure.Custom(new SampleGroup("Batches", SampleUnit.Undefined, false), _drawList.batchCount);
        RecordSteadyStateAllocations(drawFrame);
    }

    [Test, Performance]
    public void MaskBatching_RepeatedAnalyticState()
    {
        MeasureBatchChurn(() => DrawAnalyticChurnFrame(false), 1);
    }

    [Test, Performance]
    public void MaskBatching_AlternatingAnalyticState()
    {
        MeasureBatchChurn(() => DrawAnalyticChurnFrame(true), ChurnDrawCount);
    }

    [Test, Performance]
    public void MaskBatching_AlternatingHardRectState()
    {
        MeasureBatchChurn(DrawAlternatingHardMaskFrame, 1);
    }

    void SubmitSdfMasks(NowRect rect, string[] ids)
    {
        using (_drawList.Begin(new Vector2(512f, 512f)))
        {
            for (int i = 0; i < ids.Length; ++i)
            {
                using (NowSdf.Scene(rect, ids[i])
                    .RoundedBox(new NowRect(16f, 16f, 224f, 224f), 32f)
                    .BeginMask())
                {
                }
            }
        }
    }

    void SubmitStableSdfMasks()
    {
        SubmitSdfMasks(new NowRect(0f, 0f, 256f, 256f), StableSdfMaskIds);
    }

    void SubmitResizedSdfMasks()
    {
        _resizedSdfWide = !_resizedSdfWide;
        float width = _resizedSdfWide ? 257f : 256f;
        SubmitSdfMasks(new NowRect(0f, 0f, width, 256f), ResizedSdfMaskIds);
    }

    void MeasureSdfSubmission(Action submit)
    {
        Measure.Method(submit)
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .Run();

        RecordSteadyStateAllocations(submit);
    }

    [Test, Performance]
    public void SdfBeginMaskCpuSubmission_Stable16x256()
    {
        MeasureSdfSubmission(SubmitStableSdfMasks);
    }

    [Test, Performance]
    public void SdfBeginMaskCpuSubmission_Resize16x256To257()
    {
        MeasureSdfSubmission(SubmitResizedSdfMasks);
    }
}
