using NUnit.Framework;
using NowUI;
using Unity.PerformanceTesting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Decision benchmark for the isolated Graphics.RenderMesh backend, the
/// caller-owned scene Renderer path, and the original clone baseline. These
/// tests report data; they intentionally do not turn machine-specific timings
/// into CI gates.
/// </summary>
[Category("ModelPreviewBackendBenchmark")]
public class NowModelPreviewBackendPerformanceTests
{
    const int StaticMeshCount = 12;
    const int ThumbnailCount = 24;
    const int PreviewResolution = 96;

    GameObject _staticSource;
    Material _material;
    Mesh _cubeMesh;

    [SetUp]
    public void SetUp()
    {
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cubeMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive);

        var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        Assert.NotNull(shader, "A built-in benchmark shader is required.");
        _material = new Material(shader)
        {
            name = "Now Model Preview Backend Benchmark",
            hideFlags = HideFlags.HideAndDontSave,
            color = new Color(0.72f, 0.81f, 0.95f, 1f)
        };
        _staticSource = CreateStaticHierarchy(StaticMeshCount);
        SetLayerRecursively(_staticSource, 29);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_staticSource);
        Object.DestroyImmediate(_material);
        _staticSource = null;
        _material = null;
        _cubeMesh = null;
    }

    [Test, Performance]
    public void StaticInitialization_RendererClone()
    {
        MeasureInitialization(NowModelPreviewBackend.RendererClone);
    }

    [Test, Performance]
    public void StaticInitialization_RenderMesh()
    {
        MeasureInitialization(NowModelPreviewBackend.RenderMesh);
    }

    [Test, Performance]
    public void StaticInitialization_SceneObject()
    {
        NowModelPreview preview = null;

        Measure.Method(() => preview = CreateScenePreview(_staticSource, 29))
            .SetUp(() => preview = null)
            .CleanUp(() => preview?.Dispose())
            .WarmupCount(3)
            .MeasurementCount(15)
            .Run();
    }

    [Test, Performance]
    public void StaticRefresh_RendererClone()
    {
        MeasureStaticRefresh(NowModelPreviewBackend.RendererClone);
    }

    [Test, Performance]
    public void StaticRefresh_RenderMesh()
    {
        MeasureStaticRefresh(NowModelPreviewBackend.RenderMesh);
    }

    [Test, Performance]
    public void StaticRefresh_SceneObject()
    {
        using var preview = CreateScenePreview(_staticSource, 29);
        Assert.IsTrue(preview.RenderNow(), "Benchmark warmup render failed.");

        Measure.Method(() => preview.RenderNow())
            .WarmupCount(3)
            .MeasurementCount(20)
            .Run();
    }

    [Test, Performance]
    public void TwentyFourStaticThumbnails_RendererClone()
    {
        MeasureThumbnailBatch(NowModelPreviewBackend.RendererClone);
    }

    [Test, Performance]
    public void TwentyFourStaticThumbnails_RenderMesh()
    {
        MeasureThumbnailBatch(NowModelPreviewBackend.RenderMesh);
    }

    [Test, Performance]
    public void NinetySixStaticThumbnails_RendererClone()
    {
        MeasureThumbnailBatch(NowModelPreviewBackend.RendererClone, 96, 32, 8);
    }

    [Test, Performance]
    public void NinetySixStaticThumbnails_RenderMesh()
    {
        MeasureThumbnailBatch(NowModelPreviewBackend.RenderMesh, 96, 32, 8);
    }

    [Test, Performance]
    public void AnimatedSkinnedRefresh_RendererClone()
    {
        MeasureSkinnedRefresh(NowModelPreviewBackend.RendererClone);
    }

    [Test, Performance]
    public void AnimatedSkinnedRefresh_RenderMeshSnapshot()
    {
        MeasureSkinnedRefresh(NowModelPreviewBackend.RenderMesh);
    }

    [Test, Performance]
    public void AnimatedSkinnedRefresh_SceneObject()
    {
        var source = CreateSkinnedSource(out var animatedBone, out var ownedMesh);
        SetLayerRecursively(source, 30);

        try
        {
            using var preview = CreateScenePreview(source, 30);
            Assert.IsTrue(preview.RenderNow(), "Benchmark warmup render failed.");
            int frame = 0;

            Measure.Method(() =>
                {
                    animatedBone.localRotation = Quaternion.Euler(0f, 0f, (frame++ % 31) - 15f);
                    preview.RenderNow();
                })
                .WarmupCount(3)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(ownedMesh);
        }
    }

    [Test, Performance]
    public void RetainedFootprint()
    {
        using var clone = CreatePreview(_staticSource, NowModelPreviewBackend.RendererClone);
        using var raw = CreatePreview(_staticSource, NowModelPreviewBackend.RenderMesh);
        using var scene = CreateScenePreview(_staticSource, 29);

        Measure.Custom(
            new SampleGroup("RendererClone.PresentationGameObjects", SampleUnit.Undefined, false),
            clone.presentationCloneGameObjectCount);
        Measure.Custom(
            new SampleGroup("RenderMesh.PresentationGameObjects", SampleUnit.Undefined, false),
            raw.presentationCloneGameObjectCount);
        Measure.Custom(
            new SampleGroup("RenderMesh.StagingGameObjects", SampleUnit.Undefined, false),
            raw.stagingRigGameObjectCount);
        Measure.Custom(
            new SampleGroup("SceneObject.StagingGameObjects", SampleUnit.Undefined, false),
            scene.stagingRigGameObjectCount);
        Measure.Custom(
            new SampleGroup("RenderMesh.MeshSources", SampleUnit.Undefined, false),
            raw.renderMeshSourceCount);
        Measure.Custom(
            new SampleGroup("RenderMesh.SubmeshDraws", SampleUnit.Undefined, false),
            raw.renderMeshDrawCount);
    }

    [Test, Performance]
    public void ManagedAllocationFootprint()
    {
        long probeBefore;
        long probeAfter;

        try
        {
            probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probeAllocation = new byte[256];
            probeAfter = System.GC.GetAllocatedBytesForCurrentThread();
            System.GC.KeepAlive(probeAllocation);
        }
        catch (System.NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking is unavailable on this runtime.");
            return;
        }

        if (probeAfter <= probeBefore)
        {
            Assert.Ignore("This Mono runtime exposes the allocation API but does not report bytes.");
            return;
        }

        using var anchor = new NowModelPreview();

        // JIT and shared preview infrastructure are outside the samples.
        using (CreatePreview(_staticSource, NowModelPreviewBackend.RendererClone)) { }
        using (CreatePreview(_staticSource, NowModelPreviewBackend.RenderMesh)) { }

        var cloneInitialization = new SampleGroup(
            "RendererClone.Initialization.ManagedBytes",
            SampleUnit.Byte,
            false);
        var rawInitialization = new SampleGroup(
            "RenderMesh.Initialization.ManagedBytes",
            SampleUnit.Byte,
            false);
        var cloneRefresh = new SampleGroup(
            "RendererClone.Refresh.ManagedBytes",
            SampleUnit.Byte,
            false);
        var rawRefresh = new SampleGroup(
            "RenderMesh.Refresh.ManagedBytes",
            SampleUnit.Byte,
            false);

        for (int i = 0; i < 12; ++i)
        {
            MeasureInitializationAllocation(NowModelPreviewBackend.RendererClone, cloneInitialization);
            MeasureInitializationAllocation(NowModelPreviewBackend.RenderMesh, rawInitialization);
        }

        using var clone = CreatePreview(_staticSource, NowModelPreviewBackend.RendererClone);
        using var raw = CreatePreview(_staticSource, NowModelPreviewBackend.RenderMesh);
        clone.RenderNow();
        raw.RenderNow();

        for (int i = 0; i < 12; ++i)
        {
            MeasureRefreshAllocation(clone, cloneRefresh);
            MeasureRefreshAllocation(raw, rawRefresh);
        }

    }

    [Test, Performance]
    public void PairedCpuComparison()
    {
        var cloneInitialization = new SampleGroup(
            "Paired.RendererClone.Initialization",
            SampleUnit.Millisecond,
            false);
        var rawInitialization = new SampleGroup(
            "Paired.RenderMesh.Initialization",
            SampleUnit.Millisecond,
            false);
        var sceneInitialization = new SampleGroup(
            "Paired.SceneObject.Initialization",
            SampleUnit.Millisecond,
            false);
        var cloneRefresh = new SampleGroup(
            "Paired.RendererClone.Refresh",
            SampleUnit.Millisecond,
            false);
        var rawRefresh = new SampleGroup(
            "Paired.RenderMesh.Refresh",
            SampleUnit.Millisecond,
            false);
        var sceneRefresh = new SampleGroup(
            "Paired.SceneObject.Refresh",
            SampleUnit.Millisecond,
            false);

        using var anchor = new NowModelPreview();

        // Warm JIT and native preview-scene paths before interleaving samples.
        using (CreatePreview(_staticSource, NowModelPreviewBackend.RendererClone)) { }
        using (CreatePreview(_staticSource, NowModelPreviewBackend.RenderMesh)) { }
        using (CreateScenePreview(_staticSource, 29)) { }

        for (int i = 0; i < 24; ++i)
        {
            if (i % 3 == 0)
            {
                MeasureCreation(NowModelPreviewBackend.RendererClone, cloneInitialization);
                MeasureCreation(NowModelPreviewBackend.RenderMesh, rawInitialization);
                MeasureSceneCreation(sceneInitialization);
            }
            else if (i % 3 == 1)
            {
                MeasureCreation(NowModelPreviewBackend.RenderMesh, rawInitialization);
                MeasureSceneCreation(sceneInitialization);
                MeasureCreation(NowModelPreviewBackend.RendererClone, cloneInitialization);
            }
            else
            {
                MeasureSceneCreation(sceneInitialization);
                MeasureCreation(NowModelPreviewBackend.RendererClone, cloneInitialization);
                MeasureCreation(NowModelPreviewBackend.RenderMesh, rawInitialization);
            }
        }

        using var clone = CreatePreview(_staticSource, NowModelPreviewBackend.RendererClone);
        using var raw = CreatePreview(_staticSource, NowModelPreviewBackend.RenderMesh);
        using var scene = CreateScenePreview(_staticSource, 29);
        clone.RenderNow();
        raw.RenderNow();
        scene.RenderNow();

        for (int i = 0; i < 30; ++i)
        {
            if (i % 3 == 0)
            {
                MeasureRefresh(clone, cloneRefresh);
                MeasureRefresh(raw, rawRefresh);
                MeasureRefresh(scene, sceneRefresh);
            }
            else if (i % 3 == 1)
            {
                MeasureRefresh(raw, rawRefresh);
                MeasureRefresh(scene, sceneRefresh);
                MeasureRefresh(clone, cloneRefresh);
            }
            else
            {
                MeasureRefresh(scene, sceneRefresh);
                MeasureRefresh(clone, cloneRefresh);
                MeasureRefresh(raw, rawRefresh);
            }
        }
    }

    [Test, Performance]
    public void PairedSkinnedSourceModeCpuComparison()
    {
        var rawRefresh = new SampleGroup(
            "PairedSkinned.RenderMesh.Refresh",
            SampleUnit.Millisecond,
            false);
        var sceneRefresh = new SampleGroup(
            "PairedSkinned.SceneObject.Refresh",
            SampleUnit.Millisecond,
            false);
        var source = CreateSkinnedSource(out var animatedBone, out var ownedMesh);
        SetLayerRecursively(source, 30);

        try
        {
            using var raw = CreatePreview(source, NowModelPreviewBackend.RenderMesh);
            using var scene = CreateScenePreview(source, 30);
            raw.RenderNow();
            scene.RenderNow();

            for (int i = 0; i < 30; ++i)
            {
                animatedBone.localRotation = Quaternion.Euler(0f, 0f, i - 15f);

                if ((i & 1) == 0)
                {
                    MeasureRefresh(raw, rawRefresh);
                    MeasureRefresh(scene, sceneRefresh);
                }
                else
                {
                    MeasureRefresh(scene, sceneRefresh);
                    MeasureRefresh(raw, rawRefresh);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(ownedMesh);
        }
    }

    void MeasureInitialization(NowModelPreviewBackend backend)
    {
        NowModelPreview preview = null;

        Measure.Method(() =>
            {
                preview = CreatePreview(_staticSource, backend);
            })
            .SetUp(() => preview = null)
            .CleanUp(() => preview?.Dispose())
            .WarmupCount(3)
            .MeasurementCount(15)
            .Run();
    }

    void MeasureStaticRefresh(NowModelPreviewBackend backend)
    {
        using var preview = CreatePreview(_staticSource, backend);
        Assert.IsTrue(preview.RenderNow(), "Benchmark warmup render failed.");

        Measure.Method(() => preview.RenderNow())
            .WarmupCount(3)
            .MeasurementCount(20)
            .Run();
    }

    void MeasureThumbnailBatch(
        NowModelPreviewBackend backend,
        int thumbnailCount = ThumbnailCount,
        int resolution = 64,
        int measurementCount = 12)
    {
        var previews = new NowModelPreview[thumbnailCount];

        try
        {
            for (int i = 0; i < previews.Length; ++i)
            {
                previews[i] = CreatePreview(_staticSource, backend, resolution);
                Assert.IsTrue(previews[i].RenderNow(), $"Benchmark warmup render {i} failed.");
            }

            Measure.Method(() =>
                {
                    for (int i = 0; i < previews.Length; ++i)
                        previews[i].RenderNow();
                })
                .WarmupCount(2)
                .MeasurementCount(measurementCount)
                .Run();
        }
        finally
        {
            for (int i = previews.Length - 1; i >= 0; --i)
                previews[i]?.Dispose();
        }
    }

    void MeasureSkinnedRefresh(NowModelPreviewBackend backend)
    {
        var source = CreateSkinnedSource(out var sourceBone, out var ownedMesh);

        try
        {
            using var preview = CreatePreview(source, backend);
            Transform animatedBone = backend == NowModelPreviewBackend.RendererClone
                ? preview.presentationInstance.transform.Find("Bone Root/Bone Tip")
                : sourceBone;
            Assert.NotNull(animatedBone);
            Assert.IsTrue(preview.RenderNow(), "Benchmark warmup render failed.");
            int frame = 0;

            Measure.Method(() =>
                {
                    animatedBone.localRotation = Quaternion.Euler(0f, 0f, (frame++ % 31) - 15f);
                    preview.RenderNow();
                })
                .WarmupCount(3)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(ownedMesh);
        }
    }

    NowModelPreview CreatePreview(
        GameObject source,
        NowModelPreviewBackend backend,
        int resolution = PreviewResolution)
    {
        return new NowModelPreview(source, 31, backend)
            .SetFixedResolution(resolution, resolution)
            .SetUpdateMode(NowModelPreviewUpdateMode.Manual);
    }

    static NowModelPreview CreateScenePreview(
        GameObject source,
        int layer,
        int resolution = PreviewResolution)
    {
        return NowModelPreview.FromSceneObject(source, 1 << layer)
            .SetFixedResolution(resolution, resolution)
            .SetUpdateMode(NowModelPreviewUpdateMode.Manual);
    }

    void MeasureInitializationAllocation(
        NowModelPreviewBackend backend,
        SampleGroup sampleGroup)
    {
        long before = System.GC.GetAllocatedBytesForCurrentThread();
        var preview = CreatePreview(_staticSource, backend);
        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        preview.Dispose();
        Measure.Custom(sampleGroup, allocated);
    }

    static void MeasureRefreshAllocation(NowModelPreview preview, SampleGroup sampleGroup)
    {
        long before = System.GC.GetAllocatedBytesForCurrentThread();
        preview.RenderNow();
        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        Measure.Custom(sampleGroup, allocated);
    }

    void MeasureCreation(NowModelPreviewBackend backend, SampleGroup sampleGroup)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var preview = CreatePreview(_staticSource, backend);
        double milliseconds = ElapsedMilliseconds(start);
        preview.Dispose();
        Measure.Custom(sampleGroup, milliseconds);
    }

    void MeasureSceneCreation(SampleGroup sampleGroup)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var preview = CreateScenePreview(_staticSource, 29);
        double milliseconds = ElapsedMilliseconds(start);
        preview.Dispose();
        Measure.Custom(sampleGroup, milliseconds);
    }

    static void MeasureRefresh(NowModelPreview preview, SampleGroup sampleGroup)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        preview.RenderNow();
        Measure.Custom(sampleGroup, ElapsedMilliseconds(start));
    }

    static double ElapsedMilliseconds(long start)
    {
        return (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 /
            System.Diagnostics.Stopwatch.Frequency;
    }

    GameObject CreateStaticHierarchy(int meshCount)
    {
        var root = new GameObject("Static Preview Benchmark Source");

        for (int i = 0; i < meshCount; ++i)
        {
            var child = new GameObject($"Mesh {i:00}");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(
                (i % 4 - 1.5f) * 0.72f,
                (i / 4 - 1f) * 0.72f,
                (i % 3) * 0.08f);
            child.transform.localScale = Vector3.one * 0.52f;
            child.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }

        return root;
    }

    GameObject CreateSkinnedSource(out Transform animatedBone, out Mesh ownedMesh)
    {
        var root = new GameObject("Skinned Preview Benchmark Source");
        var boneRoot = new GameObject("Bone Root").transform;
        boneRoot.SetParent(root.transform, false);
        var boneTip = new GameObject("Bone Tip").transform;
        boneTip.SetParent(boneRoot, false);
        boneTip.localPosition = Vector3.up * 0.5f;
        var rendererObject = new GameObject("Skinned Mesh");
        rendererObject.transform.SetParent(root.transform, false);
        var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();

        ownedMesh = Object.Instantiate(_cubeMesh);
        ownedMesh.name = "Now Preview Benchmark Skinned Cube";
        var vertices = ownedMesh.vertices;
        var weights = new BoneWeight[vertices.Length];

        for (int i = 0; i < vertices.Length; ++i)
        {
            bool upper = vertices[i].y > 0f;
            weights[i] = new BoneWeight
            {
                boneIndex0 = upper ? 1 : 0,
                weight0 = 1f
            };
        }

        ownedMesh.boneWeights = weights;
        ownedMesh.bindposes = new[]
        {
            boneRoot.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix,
            boneTip.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix
        };
        ownedMesh.RecalculateBounds();

        renderer.sharedMesh = ownedMesh;
        renderer.sharedMaterial = _material;
        renderer.bones = new[] { boneRoot, boneTip };
        renderer.rootBone = boneRoot;
        renderer.localBounds = ownedMesh.bounds;
        renderer.updateWhenOffscreen = true;
        animatedBone = boneTip;
        return root;
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;

        for (int i = 0; i < root.transform.childCount; ++i)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }
}
