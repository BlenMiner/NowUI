// CPU-only frame-build benchmark, run headless on the NullRenderBackend so
// timings exclude GPU work and Editor noise. The scenarios mirror the heaviest
// Unity EditMode performance cases plus the features those do not cover
// (shadows, gradients, transform/rotation/opacity scopes, aligned, spaced,
// outlined, animated and gradient text). It compiles the same runtime sources
// as Unity, so a sampling profile here points at the shared hot paths.
//
//   dotnet run -c Release -- [--samples N] [--only a,b] [--rounds R] [--core C, -1 = unpinned]
//   dotnet run -c Release -- --profile <scenario> [--seconds S]   (for dotnet-trace)
//   dotnet run -c Release -- --list
using System.Diagnostics;
using NowUI;
using NowUI.Engine;
using NowUI.Hosting;
using NowUI.NodeGraph;
using UnityEngine;

internal static class Program
{
    const int Width = 1280;
    const int Height = 800;

    static readonly string[] RowLabels = Enumerable.Range(0, 1000).Select(i => $"Entry {i:0000} - asset bundle {i * 13 % 97:00} imported").ToArray();
    static readonly string[] UniqueLabels = Enumerable.Range(0, 200).Select(i => $"Row {i:000} value {i * 37 % 100:00} status ready").ToArray();
    static readonly string[] Tokens = { "public", " ", "static", " ", "void", " ", "Draw", "(", "NowRect", " ", "rect", ")", " ", "{", " ", "return", ";", " ", "}" };
    static readonly string[] Lines = Enumerable.Range(0, 300).Select(i => $"    var value{i} = Compute(rect, {i}, \"label\") + offset * {i % 7};").ToArray();
    static readonly string[] ButtonIds = Enumerable.Range(0, 500).Select(i => $"perf-stress-btn-{i:0000}").ToArray();
    static readonly string[] CheckboxIds = Enumerable.Range(0, 250).Select(i => $"perf-stress-chk-{i:0000}").ToArray();
    static readonly string[] SliderIds = Enumerable.Range(0, 250).Select(i => $"perf-stress-sld-{i:0000}").ToArray();
    static readonly bool[] CheckboxValues = Enumerable.Range(0, 250).Select(i => (i & 1) == 0).ToArray();
    static readonly float[] SliderValues = Enumerable.Range(0, 250).Select(i => i % 10 * 0.1f).ToArray();

    const string TextSample = "The quick brown fox jumps over 0123456789";
    const string MultilineLabel = "Multiline label line one\nsecond line with digits 0123\nthird line mixing AV fi ligatures";

    static readonly NowTextAnimation Wave = NowTextAnimations.Wave();
    static float _time;

    static readonly (string name, Action draw)[] Scenarios =
    {
        ("rectangles", Rectangles),
        ("text-labels", TextLabels),
        ("text-runs", TextRuns),
        ("text-lines", TextLines),
        ("measure-labels", MeasureLabels),
        ("layout-labels", LayoutLabels),
        ("layout-multiline", LayoutMultiline),
        ("scroll-rows", ScrollRows),
        ("controls", Controls),
        ("layout-engine", LayoutEngine),
        ("node-graph", NodeGraph),
        ("shadows", Shadows),
        ("gradients", Gradients),
        ("rotated", Rotated),
        ("scaled", Scaled),
        ("opacity", Opacity),
        ("text-aligned", TextAligned),
        ("text-spaced", TextSpaced),
        ("text-outlined", TextOutlined),
        ("text-animated", TextAnimated),
        ("text-gradient", TextGradient),
    };

    static int Main(string[] args)
    {
        if (args.Contains("--list"))
        {
            foreach (var (name, _) in Scenarios)
                Console.WriteLine(name);
            return 0;
        }

        int samples = int.Parse(Option(args, "--samples") ?? "300");
        int rounds = int.Parse(Option(args, "--rounds") ?? "1");
        string profile = Option(args, "--profile");
        double seconds = double.Parse(Option(args, "--seconds") ?? "8", System.Globalization.CultureInfo.InvariantCulture);
        var only = Option(args, "--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        // Pin to one core at high priority: on multi-CCD CPUs, migrations between
        // core complexes otherwise add run-to-run swings larger than most changes.
        int core = int.Parse(Option(args, "--core") ?? "4");
        if (core >= 0 && OperatingSystem.IsWindows())
        {
            using var process = Process.GetCurrentProcess();
            process.ProcessorAffinity = (IntPtr)(1L << core);
            process.PriorityClass = ProcessPriorityClass.High;
        }

        using var resources = new NowFileResources();
        var host = new Host(resources);
        NowRuntime.Initialize(host, new NullRenderBackend());
        NowRuntime.isPlaying = true;
        NowInput.defaultProvider = host;

        try
        {
            if (profile != null)
            {
                var draw = Scenarios.First(s => s.name == profile).draw;
                var warmup = Stopwatch.StartNew();
                for (int i = 0; i < 300 || warmup.Elapsed.TotalSeconds < 1.5; ++i)
                    Frame(draw);
                Console.WriteLine("NOWUI_PROFILE_START");
                var sw = Stopwatch.StartNew();
                int frames = 0;
                while (sw.Elapsed.TotalSeconds < seconds)
                {
                    Frame(draw);
                    ++frames;
                }
                Console.WriteLine($"{profile}: {frames} frames, {sw.Elapsed.TotalMilliseconds / frames:0.0000} ms/frame");
                return 0;
            }

            for (int round = 0; round < rounds; ++round)
            {
                foreach (var (name, draw) in Scenarios)
                {
                    if (only != null && !only.Contains(name))
                        continue;

                    // Warm up by time as well as count: tiered JIT promotes hot
                    // methods in the background, so a short count-based warmup
                    // measures unoptimized code for the first scenarios.
                    var warmup = Stopwatch.StartNew();
                    for (int i = 0; i < 200 || warmup.Elapsed.TotalSeconds < 1.0; ++i)
                        Frame(draw);

                    var times = new double[samples];
                    for (int i = 0; i < samples; ++i)
                        times[i] = Frame(draw);

                    Array.Sort(times);
                    Console.WriteLine($"{name,-16} median {times[samples / 2]:0.0000} ms  p10 {times[samples / 10]:0.0000}  p90 {times[samples * 9 / 10]:0.0000}");
                }
            }
        }
        finally
        {
            NowRuntime.Shutdown();
        }

        return 0;
    }

    static string Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    static double Frame(Action draw)
    {
        long start = Stopwatch.GetTimestamp();
        _time += 1f / 60f;
        NowRuntime.BeginFrame();
        var scope = Now.StartUI(1f);
        draw();
        scope.Dispose();
        NowRuntime.EndFrame();
        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }

    // ---------------------------------------------------------------- mirrors of Unity cases

    // RectangleFrameBuild
    static void Rectangles()
    {
        for (int i = 0; i < 1000; ++i)
            Now.Rectangle(new NowRect(i * 7 % 1200, i * 13 % 760, 64, 32)).SetColor(Color.white).SetRadius(4).Draw();
    }

    // TextFrameBuild
    static void TextLabels()
    {
        for (int i = 0; i < 100; ++i)
            Now.Text(new NowRect(8, i * 24 % 760, 600, 24)).SetFontSize(18).SetColor(Color.white).Draw(TextSample);
    }

    // Short tokens drawn one by one, like a code editor's syntax runs.
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

    // Long lines, most of them outside the mask.
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

    // LabelPipelineUniqueStrings
    static void LayoutLabels()
    {
        using (NowLayout.Area("perf-labels-unique", new NowRect(16f, 16f, 600f, 4200f)))
        using (NowLayout.VerticalScope(spacing: 2f))
        {
            for (int i = 0; i < UniqueLabels.Length; ++i)
                NowLayout.Label(UniqueLabels[i]).Draw();
        }
    }

    // LabelPipelineMultilineStrings
    static void LayoutMultiline()
    {
        using (NowLayout.Area("perf-labels-multiline", new NowRect(16f, 16f, 600f, 12000f)))
        using (NowLayout.VerticalScope(spacing: 2f))
        {
            for (int i = 0; i < 200; ++i)
                NowLayout.Label(MultilineLabel).Draw();
        }
    }

    // ScrollViewThousandRowsFrameBuild
    static void ScrollRows()
    {
        using (NowLayout.Area("perf-scroll-rows-area", new NowRect(16f, 16f, 480f, 640f)))
        using (NowLayout.ScrollView("perf-scroll-rows").Begin())
        using (NowLayout.VerticalScope(spacing: 2f, padding: 2f))
        {
            for (int i = 0; i < RowLabels.Length; ++i)
                NowLayout.Label(RowLabels[i]).Draw();
        }
    }

    // ManyControlsStressFrameBuild
    static void Controls()
    {
        using (NowLayout.Area("perf-stress-area", new NowRect(16f, 16f, 460f, 640f)))
        using (NowLayout.ScrollView("perf-stress-scroll").Begin())
        using (NowLayout.VerticalScope(spacing: 2f, padding: 2f))
        {
            for (int i = 0; i < ButtonIds.Length; ++i)
                NowLayout.Button("Command").SetId(ButtonIds[i]).SetStretchWidth().Draw();

            for (int i = 0; i < CheckboxIds.Length; ++i)
                NowLayout.Checkbox("Enabled").SetId(CheckboxIds[i]).Draw(ref CheckboxValues[i]);

            for (int i = 0; i < SliderIds.Length; ++i)
                NowLayout.Slider(0f, 1f).SetWidth(180f).SetId(SliderIds[i]).Draw(ref SliderValues[i]);
        }
    }

    // LayoutEngineStressRebuild
    static void LayoutEngine()
    {
        using (NowLayout.Area("perf-layout-stress", new NowRect(0f, 0f, 1600f, 1000f)))
        {
            for (int g = 0; g < 25; ++g)
            {
                using (NowLayout.VerticalScope(spacing: 2f, stretchWidth: true))
                {
                    using (NowLayout.HorizontalScope(spacing: 4f, stretchWidth: true))
                    {
                        for (int c = 0; c < 18; ++c)
                            NowLayout.ReserveRect(width: 20f + (c % 4) * 12f, height: 14f);

                        NowLayout.FlexibleSpace();
                        NowLayout.ReserveRect(height: 14f, stretchWidth: true);
                    }

                    using (NowLayout.HorizontalScope(spacing: 4f, alignItems: NowLayoutAlign.Center))
                    {
                        NowLayout.Space(8f);

                        for (int c = 0; c < 18; ++c)
                            NowLayout.ReserveRect(width: 16f + (c % 3) * 10f, height: 12f + (c % 2) * 6f);

                        NowLayout.FlexibleSpace(2f);
                    }
                }
            }
        }
    }

    // NodeGraphCanvas100: 100 nodes with mixed-type ports and ~1.5 links per node.
    static NowNodeGraph _graph;

    static void NodeGraph()
    {
        _graph ??= BuildGraph(100);
        NowNodes.Canvas(_graph, new NowRect(0f, 0f, Width, Height), "perf-nodes-100").Draw();
    }

    static NowNodeGraph BuildGraph(int nodeCount)
    {
        const int FloatType = 1;
        const int VectorType = 3;

        var graph = new NowNodeGraph();
        int columns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));

        for (int i = 0; i < nodeCount; ++i)
        {
            var node = graph.AddNode($"n{i}", $"Node {i}", new Vector2(i % columns * 210f, i / columns * 150f));
            node.size = new Vector2(180f, 118f);
            node.AddInput("a", "A", FloatType);
            node.AddInput("b", "B", VectorType);
            node.AddOutput("x", "X", FloatType);
            node.AddOutput("y", "Y", VectorType);
        }

        for (int i = 1; i < nodeCount; ++i)
            graph.TryAddLink($"n{i - 1}", "x", $"n{i}", "a");

        for (int i = 2; i < nodeCount; i += 2)
            graph.TryAddLink($"n{i - 2}", "y", $"n{i}", "b");

        return graph;
    }

    // ---------------------------------------------------------------- feature coverage

    static NowRect Cell(int i, float width = 118f, float height = 60f)
    {
        return new NowRect(8f + i % 10 * (width + 8f), 8f + i / 10 * (height + 8f), width, height);
    }

    // Material-style cards: shadow, surface and a title.
    static void Shadows()
    {
        for (int i = 0; i < 100; ++i)
        {
            var rect = Cell(i);
            Now.Shadow(rect).SetRadius(8f).SetOffset(0f, 4f).SetBlur(12f).SetColor(new Color(0f, 0f, 0f, 0.35f)).Draw();
            Now.Rectangle(rect).SetColor(new Color(0.16f, 0.18f, 0.22f, 1f)).SetRadius(8f).Draw();
            Now.Text(rect.Inset(8f)).SetFontSize(13f).SetColor(Color.white).Draw("Card title");
        }
    }

    static void Gradients()
    {
        for (int i = 0; i < 300; ++i)
        {
            var rect = new NowRect(i * 37 % 1180, i * 23 % 740, 96f, 48f);
            Now.Gradient(rect, new Color(0.2f, 0.5f, 1f, 1f), new Color(0.9f, 0.3f, 0.6f, 1f))
                .SetLinear(i % 2 == 0 ? NowGradientDirection.ToRight : NowGradientDirection.ToBottom)
                .SetRadius(6f)
                .Draw();
        }
    }

    // Rotated badges: each scope rotates a rectangle and its label.
    static void Rotated()
    {
        for (int i = 0; i < 100; ++i)
        {
            var rect = Cell(i);
            using (Now.Rotate(i * 3.6f + _time * 30f, rect.center))
            {
                Now.Rectangle(rect).SetColor(new Color(0.3f, 0.4f, 0.8f, 1f)).SetRadius(6f).Draw();
                Now.Text(rect.Inset(8f)).SetFontSize(13f).SetColor(Color.white).Draw("Rotated");
            }
        }
    }

    // A zoomed canvas: every draw goes through the transform path.
    static void Scaled()
    {
        using (Now.Transform(1.25f + 0.05f * Mathf.Sin(_time), new Vector2(640f, 400f)))
        {
            for (int i = 0; i < 500; ++i)
                Now.Rectangle(new NowRect(i * 7 % 1200, i * 13 % 760, 48, 24)).SetColor(Color.gray).SetRadius(4).Draw();

            for (int i = 0; i < 100; ++i)
                Now.Text(new NowRect(8f + i % 5 * 240f, i / 5 * 36f, 220f, 20f)).SetFontSize(14f).SetColor(Color.white).Draw(TextSample);
        }
    }

    // Faded panels with nested opacity.
    static void Opacity()
    {
        for (int p = 0; p < 10; ++p)
        {
            using (Now.Opacity(0.5f + p * 0.05f))
            {
                for (int i = 0; i < 10; ++i)
                {
                    var rect = Cell(p * 10 + i);
                    using (Now.Opacity(0.9f))
                    {
                        Now.Rectangle(rect).SetColor(new Color(0.2f, 0.6f, 0.4f, 1f)).SetRadius(6f).Draw();
                        Now.Text(rect.Inset(8f)).SetFontSize(13f).SetColor(Color.white).Draw("Faded");
                    }
                }
            }
        }
    }

    // Block layout: centered and right-aligned labels.
    static void TextAligned()
    {
        for (int i = 0; i < 100; ++i)
        {
            Now.Text(new NowRect(8f + i % 2 * 620f, i / 2 * 15f, 600f, 15f))
                .SetFontSize(13f)
                .SetColor(Color.white)
                .SetAlign(i % 4 < 2 ? NowTextAlign.Center : NowTextAlign.Right, NowTextVerticalAlign.Middle)
                .Draw(TextSample);
        }
    }

    static void TextSpaced()
    {
        for (int i = 0; i < 100; ++i)
            Now.Text(new NowRect(8f + i % 2 * 620f, i / 2 * 15f, 600f, 15f)).SetFontSize(13f).SetColor(Color.white).SetLetterSpacing(0.08f).Draw(TextSample);
    }

    static void TextOutlined()
    {
        for (int i = 0; i < 100; ++i)
            Now.Text(new NowRect(8f + i % 2 * 620f, i / 2 * 15f, 600f, 15f)).SetFontSize(13f).SetColor(Color.white).SetOutline(0.15f).SetOutlineColor(new Vector4(0f, 0f, 0f, 1f)).Draw(TextSample);
    }

    static void TextAnimated()
    {
        for (int i = 0; i < 100; ++i)
            Now.Text(new NowRect(8f + i % 2 * 620f, 8f + i / 2 * 15f, 600f, 15f)).SetFontSize(13f).SetColor(Color.white).SetAnimation(Wave).SetTime(_time).Draw(TextSample);
    }

    static void TextGradient()
    {
        for (int i = 0; i < 100; ++i)
            Now.Text(new NowRect(8f + i % 2 * 620f, i / 2 * 15f, 600f, 15f)).SetFontSize(13f).SetGradient(Color.cyan, Color.magenta).SetGradientLinear(NowGradientDirection.ToRight).Draw(TextSample);
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
        {
            // The pointer rests outside the surface, as in the Unity stress tests.
            snapshot = new NowInputSnapshot(new Vector2(-100f, -100f), false, false, false);
            snapshot.frame = Time.frameCount;
            snapshot.inputPass = Time.frameCount;
            snapshot.time = Time.realtimeSinceStartup;
            return true;
        }
    }
}
