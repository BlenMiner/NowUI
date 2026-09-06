using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Rendering;
using NowUI;
using NowUI.Internal;
using NowUI.Sdf;

/// <summary>
/// SDF overview: one scene per sample, frame building plus command-buffer
/// recording. Mask capture and image baking issue GPU commands internally;
/// these timings measure CPU submission/driver cost, never completed GPU work.
/// Fixture creation, source-texture edits, assertions and diagnostics are outside
/// timing. Cold image cases clear SDF caches before each individual sample.
/// </summary>
public class NowSdfPerformanceTests
{
    const int AllocationSamples = 16;
    static readonly Vector2 SurfaceSize = new Vector2(512f, 512f);
    static readonly NowRect SceneRect = new NowRect(0f, 0f, 256f, 256f);
    static readonly NowId SceneId = new NowId("overview-sdf-scene");

    public enum MaskChange { Stable, Animated, Resize }
    public enum ImageChange { Cached, SourceInvalidated, Cold }

    NowDrawList _drawList;
    CommandBuffer _commands;
    Texture2D[] _images;
    float _previousUiScale;
    int _frame;
    int _lastMaskWidth;

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
        _commands = new CommandBuffer { name = "Now SDF Overview CPU Submission" };
        _frame = 0;
    }

    [TearDown]
    public void TearDown()
    {
        _commands?.Release();
        _commands = null;
        _drawList?.Dispose();
        _drawList = null;
        NowSdf.Reset();
        NowControls.Reset();
        if (_images != null)
        {
            foreach (var texture in _images)
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
        }
        _images = null;
        Now.SetUIScale(_previousUiScale);
    }

    static void RequireGraphicsDevice()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("SDF masks and image bakes require a graphics device.");
    }

    static void Counter(string name, double value, SampleUnit unit = SampleUnit.Undefined)
    {
        Measure.Custom(new SampleGroup(name, unit, false), value);
    }

    static void MeasureCpu(Action submit, Action prepare = null)
    {
        using var allocations = new NowBenchmarkAllocations();
        var measurement = Measure.Method(submit)
            .SampleGroup(new SampleGroup("CPU.BuildAndRecord", SampleUnit.Millisecond))
            .WarmupCount(5)
            .MeasurementCount(64)
            .IterationsPerMeasurement(1);
        if (prepare != null)
            measurement.SetUp(prepare);
        measurement.Run();

        // Allocation tracking probes its backend: byte totals when reliable,
        // otherwise profiler allocation calls. Setup remains outside the sample.
        long allocated = 0;
        for (int i = 0; i < AllocationSamples; ++i)
        {
            prepare?.Invoke();
            allocations.Begin();
            submit();
            allocated += allocations.End();
        }
        allocations.Report(allocated / (double)AllocationSamples);
    }

    void SubmitFrame(Action draw)
    {
        _commands.Clear();
        using (_drawList.Begin(SurfaceSize))
            draw();
        NowRenderer.Draw(_commands, _drawList);
    }

    Material AssertDirect(int shapes, int layers)
    {
        Assert.AreEqual(4, _drawList.mesh.vertexCount, "The SDF scene must emit one quad.");
        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(NowMeshKind.Sdf, _drawList.batches[0].kind);
        var material = _drawList.batches[0].material;
        Assert.AreEqual(shapes, material.GetFloat("_SdfShapeCount"), "Shape truncation/no-op changed the workload.");
        Assert.AreEqual(layers, material.GetFloat("_SdfLayerCount"));
        Assert.AreEqual(1, NowSdf.cacheCount, "A frame must reuse its stable scene identity.");
        return material;
    }

    void RecordDirect(int shapes, int layers)
    {
        AssertDirect(shapes, layers);
        Counter("Scenes", 1);
        Counter("Shapes", shapes);
        Counter("Layers", layers);
        Counter("Vertices", _drawList.mesh.vertexCount);
        Counter("Batches", _drawList.batchCount);
    }

    static void BuildGraph(NowSdfGraph graph, int shapes, float offset = 0f)
    {
        graph.Clear().SetColor(Color.cyan);
        for (int i = 0; i < shapes; ++i)
        {
            graph.SmoothUnion(3f)
                .Circle(new Vector2(24f + (i % 8) * 28f + offset, 24f + (i / 8) * 28f), 15f);
        }
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(1, false)]
    [TestCase(16, false)]
    [TestCase(64, false)]
    [TestCase(1, true)]
    [TestCase(16, true)]
    [TestCase(64, true)]
    public void SdfCpu_GraphReuseOrAnimatedRebuild(int shapes, bool rebuild)
    {
        var graph = NowSdf.Graph();
        BuildGraph(graph, shapes);
        Action draw = () =>
        {
            if (rebuild)
                BuildGraph(graph, shapes, (++_frame & 1) == 0 ? 0f : 2f);
            NowSdf.Scene(SceneRect, SceneId).Graph(graph).Draw();
        };
        Action submit = () => SubmitFrame(draw);
        MeasureCpu(submit);
        submit();
        float before = AssertDirect(shapes, 1).GetVectorArray("_SdfData1")[0].x;
        submit();
        float after = AssertDirect(shapes, 1).GetVectorArray("_SdfData1")[0].x;
        Assert.AreEqual(rebuild, before != after, "Animated graphs must change uploaded geometry every frame.");
        RecordDirect(shapes, 1);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(1, false)]
    [TestCase(16, false)]
    [TestCase(16, true)]
    public void SdfCpu_GraphLayers(int layers, bool reuseGraph)
    {
        var graphs = new NowSdfGraph[reuseGraph ? 1 : layers];
        for (int i = 0; i < graphs.Length; ++i)
        {
            graphs[i] = NowSdf.Graph();
            BuildGraph(graphs[i], 4, i * 2f);
        }
        Action draw = () =>
        {
            var scene = NowSdf.Scene(SceneRect, SceneId);
            for (int i = 0; i < layers; ++i)
                scene = scene.SmoothUnion(2f).Graph(graphs[reuseGraph ? 0 : i]);
            scene.Draw();
        };
        Action submit = () => SubmitFrame(draw);
        MeasureCpu(submit);
        RecordDirect(graphs.Length * 4, layers);
        var ranges = _drawList.batches[0].material.GetVectorArray("_SdfLayerData1");
        Assert.AreEqual(4f, ranges[0].z);
        Assert.AreEqual(reuseGraph ? 4f : (layers - 1) * 4 * 128 + 4f, ranges[layers - 1].z);
        Counter("ReferencedShapes", layers * 4);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(false)]
    [TestCase(true)]
    public void SdfCpu_Morph16To16(bool animated)
    {
        var from = NowSdf.Graph();
        var to = NowSdf.Graph();
        BuildGraph(from, 16);
        BuildGraph(to, 16, 10f);
        Action draw = () =>
        {
            float t = animated && (++_frame & 1) != 0 ? 0.75f : 0.25f;
            NowSdf.Scene(SceneRect, SceneId).Morph(from, to, t).Draw();
        };
        Action submit = () => SubmitFrame(draw);
        MeasureCpu(submit);
        submit();
        float before = AssertDirect(32, 1).GetVectorArray("_SdfLayerData1")[0].y;
        submit();
        var layer = AssertDirect(32, 1).GetVectorArray("_SdfLayerData1")[0];
        Assert.AreEqual(animated, before != layer.y, "Animated morphs must change the uploaded interpolation.");
        Assert.AreEqual(16f, layer.z);
        Assert.AreEqual(16 * 128 + 16f, layer.w);
        RecordDirect(32, 1);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(false)]
    [TestCase(true)]
    public void SdfCpu_FullEffects16(bool animated)
    {
        var graph = NowSdf.Graph();
        BuildGraph(graph, 16);
        Action draw = () =>
        {
            float phase = animated && (++_frame & 1) != 0 ? 1f : 0f;
            NowSdf.Scene(SceneRect, SceneId)
                .SetOutline(2f + phase, Color.white, 1f)
                .SetGlow(8f + phase, Color.cyan)
                .SetShadow(new Vector2(4f + phase, 5f), 6f, Color.black, 1f)
                .SetInnerShadow(new Vector2(-2f, -3f), 4f, Color.black)
                .SetEmboss(new Vector2(-0.6f, -0.8f), 0.3f, 3f)
                .SetContours(8f, 1f, Color.blue, phase, 3)
                .SetWarp(2f, 40f, 0f, phase)
                .Graph(graph).Draw();
        };
        Action submit = () => SubmitFrame(draw);
        MeasureCpu(submit);
        submit();
        float before = AssertDirect(16, 1).GetVector("_SdfOutline").x;
        submit();
        var material = AssertDirect(16, 1);
        Assert.AreEqual(animated, before != material.GetVector("_SdfOutline").x);
        Assert.Greater(material.GetVector("_SdfGlow").x, 0f);
        Assert.Greater(material.GetVector("_SdfShadow").z, 0f);
        Assert.Greater(material.GetVector("_SdfInnerShadow").z, 0f);
        Assert.Greater(material.GetVector("_SdfContour").x, 0f);
        RecordDirect(16, 1);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(MaskChange.Stable, 0.5f)]
    [TestCase(MaskChange.Stable, 1f)]
    [TestCase(MaskChange.Stable, 2f)]
    [TestCase(MaskChange.Animated, 0.5f)]
    [TestCase(MaskChange.Animated, 1f)]
    [TestCase(MaskChange.Animated, 2f)]
    [TestCase(MaskChange.Resize, 1f)]
    public void SdfCpu_Mask16ShapesWithContent(MaskChange change, float scale)
    {
        RequireGraphicsDevice();
        var graph = NowSdf.Graph();
        BuildGraph(graph, 16);
        Action draw = () =>
        {
            float phase = (++_frame & 1) == 0 ? 0f : 1f;
            if (change == MaskChange.Animated)
                BuildGraph(graph, 16, phase * 2f);
            _lastMaskWidth = change == MaskChange.Resize ? 256 + (int)phase : 256;
            var rect = new NowRect(0f, 0f, _lastMaskWidth, 256f);
            using (NowSdf.Scene(rect, SceneId).SetMaskResolutionScale(scale).Graph(graph).BeginMask())
            {
                // Masked child geometry is submitted, so mask state is actually consumed.
                for (int i = 0; i < 16; ++i)
                    Now.Rectangle(new NowRect(8f + (i % 4) * 60f, 8f + (i / 4) * 60f, 52f, 52f))
                        .SetColor(Color.white).Draw();
            }
        };
        Action submit = () => SubmitFrame(draw);
        MeasureCpu(submit);
        submit();
        var first = AssertMask(scale);
        int before = NowSdf.maskRasterizationCount;
        submit();
        var second = AssertMask(scale);
        int rasterizations = NowSdf.maskRasterizationCount - before;
        Assert.AreEqual(change == MaskChange.Stable ? 0 : 1, rasterizations);
        Assert.AreEqual(change != MaskChange.Resize, ReferenceEquals(first, second), "Only resizing should replace the target.");
        Counter("Scenes", 1);
        Counter("Shapes", 16);
        Counter("ChildRectangles", 16);
        Counter("Vertices", _drawList.mesh.vertexCount);
        Counter("Batches", _drawList.batchCount);
        Counter("MaskResolutionScale", scale);
        Counter("MaskPixels", NowSdf.cachedMaskPixels);
        Counter("MaskTextures", NowSdf.maskTextureCount);
        Counter("MaskRasterizations.PerFrame", rasterizations);
    }

    RenderTexture AssertMask(float scale)
    {
        Assert.AreEqual(64, _drawList.mesh.vertexCount);
        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(1, _drawList.batches[0].maskState.textureCount);
        Assert.AreEqual(1, NowSdf.cacheCount);
        Assert.AreEqual(1, NowSdf.maskTextureCount);
        var target = _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        Assert.NotNull(target);
        Assert.IsTrue(target.IsCreated());
        Assert.AreEqual(Mathf.CeilToInt(_lastMaskWidth * scale), target.width);
        Assert.AreEqual(Mathf.CeilToInt(256f * scale), target.height);
        Assert.AreEqual((long)target.width * target.height, NowSdf.cachedMaskPixels);
        return target;
    }

    void CreateImages(int count)
    {
        _images = new Texture2D[count];
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; ++y)
            for (int x = 0; x < 32; ++x)
                pixels[y * 32 + x] = new Color32(255, 180, 80, (byte)(x < 16 && y < 16 ? 255 : 0));
        for (int i = 0; i < count; ++i)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "Now SDF Overview Source " + i,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            _images[i] = texture;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(1, ImageChange.Cached)]
    [TestCase(8, ImageChange.Cached)]
    [TestCase(1, ImageChange.SourceInvalidated)]
    [TestCase(1, ImageChange.Cold)]
    public void SdfCpu_ImageFieldAndAtlas(int images, ImageChange change)
    {
        RequireGraphicsDevice();
        Assert.NotNull(Resources.Load<Material>("NowUI/SdfImageField"));
        CreateImages(images);
        Action prepare = null;
        if (change == ImageChange.Cold)
            prepare = NowSdf.Reset;
        else if (change == ImageChange.SourceInvalidated)
        {
            prepare = () =>
            {
                // Source creation/edit/upload belongs to the caller, outside
                // measurement. The timed call must detect updateCount and rebake.
                _images[0].SetPixel(0, 0, (++_frame & 1) == 0 ? Color.clear : Color.white);
                _images[0].Apply(false, false);
            };
        }
        Action draw = () =>
        {
            var scene = NowSdf.Scene(SceneRect, SceneId).SetOutline(2f, Color.white);
            for (int i = 0; i < images; ++i)
                scene = scene.Image(new NowRect(8f + (i % 4) * 60f, 8f + (i / 4) * 60f, 48f, 48f), _images[i]);
            scene.Draw();
        };
        Action submit = () => SubmitFrame(draw);
        MeasureCpu(submit, prepare);
        prepare?.Invoke();
        int before = NowSdfImageFields.bakeCount;
        submit();
        int bakes = NowSdfImageFields.bakeCount - before;
        Assert.AreEqual(change == ImageChange.Cached ? 0 : images, bakes);
        Assert.AreEqual(images, NowSdfImageFields.fieldCount);
        var material = AssertDirect(images, 1);
        var fieldAtlas = material.GetTexture("_SdfImageField") as RenderTexture;
        var colorAtlas = material.GetTexture("_SdfImageColor") as RenderTexture;
        Assert.NotNull(fieldAtlas);
        Assert.NotNull(colorAtlas);
        Assert.IsTrue(fieldAtlas.IsCreated());
        Assert.IsTrue(colorAtlas.IsCreated());
        RecordDirect(images, 1);
        Counter("ImageFields", NowSdfImageFields.fieldCount);
        Counter("ImageBakes.PerFrame", bakes);
        Counter("ImageSourcePixels", images * 32 * 32);
        Counter("ImageAtlasPixels", (long)fieldAtlas.width * fieldAtlas.height + (long)colorAtlas.width * colorAtlas.height);
    }
}
