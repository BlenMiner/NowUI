// CPU-only frame-build benchmark for text-heavy workloads, run headless on the
// NullRenderBackend so timings exclude GPU work and Editor noise. Uses only APIs
// that exist at older commits, so the same file builds across a bisect.
using System.Diagnostics;
using NowUI;
using NowUI.Engine;
using NowUI.Hosting;
using UnityEngine;

internal static class Program
{
    const int Width = 1280;
    const int Height = 800;

    static readonly string[] RowLabels = Enumerable.Range(0, 1000).Select(i => $"Row {i:0000}: scrolling label with enough text to measure").ToArray();
    static readonly string[] Tokens = { "public", " ", "static", " ", "void", " ", "Draw", "(", "NowRect", " ", "rect", ")", " ", "{", " ", "return", ";", " ", "}" };
    static readonly string[] Lines = Enumerable.Range(0, 300).Select(i => $"    var value{i} = Compute(rect, {i}, \"label\") + offset * {i % 7};").ToArray();

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "sizes")
        {
            // SIZES
            foreach (var t in new[] { typeof(NowText), typeof(NowTextAnimation), typeof(NowRectangle), typeof(NowLayoutOptions) })
            {
                var size = (int)typeof(System.Runtime.CompilerServices.Unsafe).GetMethod("SizeOf", Type.EmptyTypes).MakeGenericMethod(t).Invoke(null, null);
                Console.WriteLine($"{t.Name} {size}");
            }
            return 0;
        }

        int samples = args.Length > 0 ? int.Parse(args[0]) : 400;
        string only = args.Length > 1 ? args[1] : null;
        using var resources = new NowFileResources();
        var host = new Host(resources);
        NowRuntime.Initialize(host, new NullRenderBackend());
        NowRuntime.isPlaying = true;

        var scenarios = new (string name, Action draw)[]
        {
            ("scroll-rows", ScrollRows),
            ("text-runs", TextRuns),
            ("text-lines", TextLines),
            ("measure-labels", MeasureLabels),
            ("rectangles", Rectangles),
        };

        try
        {
            foreach (var (name, draw) in scenarios)
            {
                if (only != null && only != name)
                    continue;

                for (int i = 0; i < 200; ++i)
                    Frame(draw);

                var times = new double[samples];
                for (int i = 0; i < samples; ++i)
                    times[i] = Frame(draw);

                Array.Sort(times);
                Console.WriteLine($"{name,-16} median {times[samples / 2]:0.0000} ms  p10 {times[samples / 10]:0.0000}  p90 {times[samples * 9 / 10]:0.0000}");
            }
        }
        finally
        {
            NowRuntime.Shutdown();
        }

        return 0;
    }

    static double Frame(Action draw)
    {
        long start = Stopwatch.GetTimestamp();
        NowRuntime.BeginFrame();
        var scope = Now.StartUI(1f);
        draw();
        scope.Dispose();
        NowRuntime.EndFrame();
        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }

    static void ScrollRows()
    {
        using (NowLayout.Area("bench-scroll-area", new NowRect(16f, 16f, 480f, 640f)))
        using (NowLayout.ScrollView("bench-scroll").Begin())
        using (NowLayout.VerticalScope(spacing: 2f, padding: 2f))
        {
            for (int i = 0; i < RowLabels.Length; ++i)
                NowLayout.Label(RowLabels[i]).Draw();
        }
    }

    static void TextRuns()
    {
        var style = Now.Text(default).SetFontSize(13f).SetColor(Color.white);
        for (int line = 0; line < 60; ++line)
        {
            float x = 8f;
            for (int t = 0; t < Tokens.Length; ++t)
            {
                var text = style;
                text.rect = new NowRect(x, 8f + line * 13f, 200f, 16f);
                text.Draw(Tokens[t]);
                x += Tokens[t].Length * 7f;
            }
        }
    }

    static void TextLines()
    {
        using (Now.Mask(new NowRect(0f, 0f, 900f, 700f)))
        {
            for (int i = 0; i < Lines.Length; ++i)
                Now.Text(new NowRect(8f, 8f + i * 16f, 900f, 16f)).SetFontSize(13f).SetColor(Color.white).Draw(Lines[i]);
        }
    }

    static void MeasureLabels()
    {
        var style = Now.Text(default).SetFontSize(14f);
        float total = 0f;
        for (int i = 0; i < RowLabels.Length; ++i)
            total += style.Measure(RowLabels[i]).x;
        if (total < 0f) Console.WriteLine(total);
    }

    static void Rectangles()
    {
        for (int i = 0; i < 2000; ++i)
            Now.Rectangle(new NowRect(i % 50 * 24f, i / 50 * 18f, 20f, 14f)).SetColor(Color.gray).SetRadius(4f).Draw();
    }

    sealed class Host : INowHostServices, INowClock, INowInputProvider
    {
        readonly DefaultHostServices defaults = new();
        public Host(INowResourceProvider resources) { this.resources = resources; }
        public double realtimeSeconds { get; set; }
        public INowClock clock => this;
        public NowScreenInfo screen => new(Width, Height, 96);
        public INowLogger logger => defaults.logger;
        public INowClipboard clipboard => null;
        public INowTouchKeyboard touchKeyboard => null;
        public INowResourceProvider resources { get; }
        public INowImageDecoder imageDecoder { get; } = new NowPngImageDecoder();
        public INowFetchProvider fetch => null;
        public RuntimePlatform platform => defaults.platform;
        public string persistentDataPath => defaults.persistentDataPath;
        public string dataPath => AppContext.BaseDirectory;
        public string[] layerNames => defaults.layerNames;
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        { snapshot = default; snapshot.frame = Time.frameCount; snapshot.inputPass = Time.frameCount; snapshot.time = Time.realtimeSinceStartup; return true; }
    }
}
