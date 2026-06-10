using System.Diagnostics;
using System.IO;
using NowUIInternal;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Diagnostics for the Lottie vector pipeline: renders sample frames of every
/// animation under Assets/NowUI/Example/Lottie to Temp/*.png and logs tessellation
/// timings. Usable from the menu or via -executeMethod in batch mode.
/// </summary>
static class NowLottieDebugRender
{
    [MenuItem("NowUI/Lottie/Render Debug Samples")]
    static void RenderSamplesMenu()
    {
        RenderSamplesInternal();
    }

    // Batch entry point: -executeMethod NowLottieDebugRender.RenderSamples
    static void RenderSamples()
    {
        bool success = RenderSamplesInternal();

        if (Application.isBatchMode)
            EditorApplication.Exit(success ? 0 : 1);
    }

    static bool RenderSamplesInternal()
    {
        Debug.Log($"DebugRender: native tessellator available = {NowLottieNative.available}");

        var renderer = new NowLottiePreviewRenderer();
        float[] fractions = { 0f, 0.25f, 0.5f, 0.75f };
        bool success = true;

        foreach (var guid in AssetDatabase.FindAssets("t:NowLottieAsset", new[] { "Assets/NowUI/Example/Lottie" }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<NowLottieAsset>(assetPath);
            var composition = asset != null ? asset.composition : null;

            if (composition == null)
            {
                Debug.LogError($"DebugRender: failed to load {assetPath}");
                success = false;
                continue;
            }

            foreach (var fraction in fractions)
            {
                float frame = composition.inPoint + composition.durationFrames * fraction;
                var texture = renderer.RenderToTexture(asset, frame, 256, 256, new Color(0.22f, 0.22f, 0.22f, 1f));
                File.WriteAllBytes($"Temp/lottie_{asset.name}_{fraction:0.00}.png", texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            Benchmark(asset.name, composition);
        }

        renderer.Dispose();
        return success;
    }

    // Also a batch entry point: -executeMethod NowLottieDebugRender.ValidateCanvasPages
    // Verifies that large Lottie grids are split across canvas pages that respect
    // CanvasRenderer's 16-bit index requirement (<= 65535 verts per page mesh).
    [MenuItem("NowUI/Lottie/Validate Canvas Pages")]
    static void ValidateCanvasPages()
    {
        var lottie = AssetDatabase.LoadAssetAtPath<NowLottieAsset>("Assets/NowUI/Assets/AnimatedEmoji/Heart-eyes-cat.lottie");

        if (lottie == null)
        {
            Debug.LogError("ValidateCanvasPages: asset missing");
            EditorApplication.Exit(1);
            return;
        }

        var canvasObject = new GameObject("Canvas", typeof(Canvas));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var graphicObject = new GameObject("Graphic", typeof(GridGraphic));
        graphicObject.transform.SetParent(canvasObject.transform, false);
        graphicObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1060f, 560f);

        var graphic = graphicObject.GetComponent<GridGraphic>();
        graphic.lottie = lottie;

        bool success = true;

        foreach (int count in new[] { 3, 4, 6 })
        {
            graphic.count = count;
            graphic.MarkDirty();
            Canvas.ForceUpdateCanvases();
            success &= ValidatePages(graphic, count);
        }

        Debug.Log(success ? "ValidateCanvasPages: DONE all pages within CanvasRenderer limits" : "ValidateCanvasPages: FAILED");

        Object.DestroyImmediate(canvasObject);

        if (Application.isBatchMode)
            EditorApplication.Exit(success ? 0 : 1);
    }

    sealed class GridGraphic : NowUIGraphic
    {
        public NowLottieAsset lottie;

        public int count = 4;

        public float time = 0.37f;

        public float playbackFrameRate;

        protected override void DrawNowUI(Rect rect)
        {
            var bounds = new Vector4(0, 0, rect.width, rect.height);

            NowUI.Rectangle(bounds)
                .SetColor(new Color(0.08f, 0.1f, 0.14f, 0.92f))
                .SetRadius(12)
                .SetMask(bounds)
                .Draw();

            float cellSize = rect.height / count;

            for (int x = 0; x < count; ++x)
            {
                for (int y = 0; y < count; ++y)
                {
                    NowUI.Lottie(new Vector4(x * cellSize, y * cellSize, cellSize, cellSize), lottie)
                        .SetNormalizedTime((time + x * 0.1f + y * 0.1f) % 1f)
                        .SetPlaybackFrameRate(playbackFrameRate)
                        .Draw();
                }
            }
        }
    }

    // Also a batch entry point: -executeMethod NowLottieDebugRender.BenchGrid
    // Measures main-thread cost of a full canvas rebuild for an 8x8 emoji grid.
    [MenuItem("NowUI/Lottie/Bench 8x8 Grid")]
    static void BenchGrid()
    {
        var lottie = AssetDatabase.LoadAssetAtPath<NowLottieAsset>("Assets/NowUI/Assets/AnimatedEmoji/Heart-eyes-cat.lottie");

        if (lottie == null)
        {
            Debug.LogError("BenchGrid: asset missing");
            EditorApplication.Exit(1);
            return;
        }

        var canvasObject = new GameObject("Canvas", typeof(Canvas));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var graphicObject = new GameObject("Graphic", typeof(GridGraphic));
        graphicObject.transform.SetParent(canvasObject.transform, false);
        graphicObject.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 560f);

        var graphic = graphicObject.GetComponent<GridGraphic>();
        graphic.lottie = lottie;
        graphic.count = 8;

        Debug.Log($"BenchGrid: native={NowLottieNative.available} blit={NowLottieNative.blitAvailable}");

        MeasureGrid(graphic, "blit on, native fps", 0f);
        MeasureGrid(graphic, "blit on, 15 fps cap", 15f);

        NowLottieNative.forceManagedCopy = true;
        MeasureGrid(graphic, "blit off, native fps", 0f);
        NowLottieNative.forceManagedCopy = false;

        Object.DestroyImmediate(canvasObject);

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    static void MeasureGrid(GridGraphic graphic, string label, float playbackFrameRate)
    {
        graphic.playbackFrameRate = playbackFrameRate;

        // Warmup.
        for (int frame = 0; frame < 10; ++frame)
        {
            graphic.time = frame / 60f;
            graphic.MarkDirty();
            Canvas.ForceUpdateCanvases();
        }

        const int FRAMES = 60;
        var stopwatch = Stopwatch.StartNew();

        for (int frame = 0; frame < FRAMES; ++frame)
        {
            graphic.time = (10 + frame) / 60f;
            graphic.MarkDirty();
            Canvas.ForceUpdateCanvases();
        }

        stopwatch.Stop();
        Debug.Log($"BenchGrid: [{label}] avg {stopwatch.Elapsed.TotalMilliseconds / FRAMES:0.00} ms/rebuild");
    }

    static bool ValidatePages(NowUIGraphic graphic, int count)
    {
        int pageCount = graphic.canvasPageCount;
        bool success = true;
        long totalVertices = 0;
        int usedPages = 0;

        for (int page = 0; page < pageCount; ++page)
        {
            var mesh = graphic.GetCanvasPageMesh(page);

            if (mesh == null || mesh.vertexCount == 0)
                continue;

            ++usedPages;
            totalVertices += mesh.vertexCount;

            if (mesh.vertexCount > 65535)
            {
                Debug.LogError($"ValidateCanvasPages: count={count} page={page} has {mesh.vertexCount} verts (over CanvasRenderer limit)");
                success = false;
            }

            if (mesh.indexFormat != UnityEngine.Rendering.IndexFormat.UInt16)
            {
                Debug.LogError($"ValidateCanvasPages: count={count} page={page} uses {mesh.indexFormat} indices (CanvasRenderer needs UInt16)");
                success = false;
            }
        }

        Debug.Log($"ValidateCanvasPages: count={count} pages={usedPages} totalVerts={totalVertices} ok={success}");
        return success;
    }

    static void Benchmark(string name, NowLottieComposition composition)
    {
        BenchmarkSize(name, composition, 256f);
        BenchmarkSize(name, composition, 1080f);

        // A/B against the managed fallback.
        if (NowLottieNative.available)
        {
            NowLottieNative.forceManagedTessellation = true;
            BenchmarkSize(name + " (managed)", composition, 256f);
            BenchmarkSize(name + " (managed)", composition, 1080f);
            NowLottieNative.forceManagedTessellation = false;
        }
    }

    static void BenchmarkSize(string name, NowLottieComposition composition, float size)
    {
        var buffer = new NowLottieDrawBuffer();
        var rect = new Vector4(0f, 0f, size, size);

        // Warmup (also primes pools).
        for (int i = 0; i < 20; ++i)
            NowLottieRenderer.Render(composition, composition.inPoint + i % 50, rect, true, Vector4.one, buffer);

        const int ITERATIONS = 300;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < ITERATIONS; ++i)
        {
            float frame = composition.inPoint + composition.durationFrames * (i % 100) / 100f;
            NowLottieRenderer.Render(composition, frame, rect, true, Vector4.one, buffer);
        }

        stopwatch.Stop();

        Debug.Log($"DebugRender: BENCH {name} @{size:0}px avg {stopwatch.Elapsed.TotalMilliseconds / ITERATIONS:0.000} ms/frame, verts={buffer.positions.count} indices={buffer.indices.count}");
    }
}
