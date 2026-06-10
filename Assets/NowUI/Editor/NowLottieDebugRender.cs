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

    static void Benchmark(string name, NowLottieComposition composition)
    {
        BenchmarkSize(name, composition, 256f);
        BenchmarkSize(name, composition, 1080f);

        // A/B against the managed fallback by forcing the native probe off.
        if (NowLottieNative.available && SetNativeAvailable(false))
        {
            BenchmarkSize(name + " (managed)", composition, 256f);
            BenchmarkSize(name + " (managed)", composition, 1080f);
            SetNativeAvailable(true);
        }
    }

    static bool SetNativeAvailable(bool value)
    {
        var type = typeof(NowLottieNative);
        var probed = type.GetField("_probed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var available = type.GetField("_available", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (probed == null || available == null)
            return false;

        probed.SetValue(null, true);
        available.SetValue(null, value);
        return true;
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
