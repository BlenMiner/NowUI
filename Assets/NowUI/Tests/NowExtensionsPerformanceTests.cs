using System;
using System.Text;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Internal;
using NowUI.CodeEditor;
using NowUI.Docking;
using NowUI.Markdown;
using NowUI.Markup;
using NowUI.NodeGraph;
using NowUI.Sdf;

/// <summary>
/// Baseline timings for the extension hot paths that the core perf suite does
/// not cover: markdown relayout and code-fence drawing, cold markdown/markup
/// parsing, markup steady-state drawing, node graph canvas rebuilds, code
/// editor repaint, Lottie frame tessellation, SDF scene constant upload, and
/// docking layout. Timings catch regressions relative to previous runs;
/// steady-state allocation counts are recorded as a "GC.Alloc" sample group
/// (bytes over a fixed number of frames) for before/after visibility without
/// failing the run on current behavior.
/// </summary>
public class NowExtensionsPerformanceTests
{
    const int ParseVariantCount = 25;

    const int AllocationSampleFrames = 8;

    const int SdfSceneCount = 10;

    const int SdfShapesPerScene = 10;

    static readonly string[] DockWindowTitles =
    {
        "Hierarchy",
        "Scene",
        "Game",
        "Inspector",
        "Console",
        "Project"
    };

    static readonly string[] _sdfSceneIds = CreateSdfSceneIds();

    static readonly string[] _dockRows = CreateDockRows();

    /// <summary>Idle pointer: benchmarks measure repaint cost, never interaction.</summary>
    sealed class FakePointer : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = default;
            return true;
        }
    }

    /// <summary>Idle keyboard so text-aware controls read an empty frame deterministically.</summary>
    sealed class FakeKeyboard : INowTextInputSource
    {
        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = default;
            return true;
        }
    }

    static NowThemeAsset LoadPerfTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");
        Assert.NotNull(theme, "MaterialDark theme asset is required for extension perf benchmarks.");
        return theme;
    }

    static NowFontAsset LoadPerfFont()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource is required for extension perf benchmarks.");
        return font;
    }

    static void WithPerfTheme(NowThemeAsset theme, NowFontAsset font, Action draw)
    {
        using (NowTheme.Scope(theme))
        {
            var previousFont = Now.defaultFont;
            Now.defaultFont = font;

            try
            {
                draw();
            }
            finally
            {
                Now.defaultFont = previousFont;
            }
        }
    }

    static void ResetInteractiveState()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowTextInput.Reset();
    }

    /// <summary>
    /// Records the bytes allocated across a fixed number of already-warm frames
    /// as a custom sample group, so allocation regressions are visible in the
    /// Performance Test Report without asserting on current behavior.
    /// </summary>
    static void RecordSteadyStateAllocations(Action drawFrame)
    {
        long before;

        try
        {
            before = GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            return;
        }

        for (int i = 0; i < AllocationSampleFrames; ++i)
            drawFrame();

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Measure.Custom(new SampleGroup("GC.Alloc", SampleUnit.Byte, false), allocated);
    }

    static string[] CreateSdfSceneIds()
    {
        var ids = new string[SdfSceneCount];

        for (int i = 0; i < ids.Length; ++i)
            ids[i] = "perf-sdf-" + i;

        return ids;
    }

    static string[] CreateDockRows()
    {
        var rows = new string[DockWindowTitles.Length * 4];

        for (int window = 0; window < DockWindowTitles.Length; ++window)
        {
            for (int row = 0; row < 4; ++row)
                rows[window * 4 + row] = $"{DockWindowTitles[window]} row {row}: AV fi 0123456789";
        }

        return rows;
    }

    static string BuildMarkdownArticle()
    {
        return
            "# Extension benchmark article\n\n" +
            "This document mirrors a realistic docs page: paragraphs with a [link](https://example.com/docs), " +
            "`inline code`, **bold** and *italic* spans that wrap differently at each of the alternating widths " +
            "used by the relayout stress benchmark.\n\n" +
            "## Layout model\n\n" +
            "The renderer parses once, lays out per width, and replays draw ops per frame, so alternating the " +
            "available width forces the full measure and wrap pass every rebuild.\n\n" +
            "- paragraph wrap and inline spans\n" +
            "- list markers and nested content\n" +
            "- links with hover rects\n" +
            "- fenced code with syntax colors\n\n" +
            "1. parse the block structure\n" +
            "2. lay out inlines for the width\n" +
            "3. replay ops into the draw list\n\n" +
            "> Block quotes indent their children and re-wrap with the rest of the document when the width " +
            "> changes, which keeps this workload honest about nested layout costs.\n\n" +
            "```csharp\n" +
            "using (Now.StartUI(surface))\n" +
            "{\n" +
            "    Now.Text(rect).SetFontSize(14).Draw(\"Measured text\");\n" +
            "    Now.Button(buttonRect, \"Apply\").Draw();\n" +
            "}\n" +
            "```\n\n" +
            "| Stage | Cost | Notes |\n" +
            "| --- | ---: | --- |\n" +
            "| Parse | once | cached by text |\n" +
            "| Layout | per width | wrap, lists, tables |\n" +
            "| Draw | per frame | op replay |\n\n" +
            "Closing paragraph with enough words to wrap at both of the alternating widths and keep the final " +
            "measure pass representative of an actual article footer.";
    }

    static string BuildCodeFenceMarkdown()
    {
        var builder = new StringBuilder(8 * 1024);
        builder.Append("# Generated source listing\n\n```csharp\n");

        for (int i = 0; i < 24; ++i)
        {
            builder.Append("// Section ").Append(i).Append(": deterministic generated block\n");
            builder.Append("public static int Evaluate").Append(i).Append("(int value)\n");
            builder.Append("{\n");
            builder.Append("    string label = \"case-").Append(i).Append("\";\n");
            builder.Append("    return value * ").Append(i + 3).Append(" + label.Length;\n");
            builder.Append("}\n");
        }

        builder.Append("```\n");
        return builder.ToString();
    }

    static string BuildLargeMarkdown()
    {
        var builder = new StringBuilder(32 * 1024);
        builder.Append("# Large parse document\n\n");

        for (int i = 0; i < 56; ++i)
        {
            builder.Append("## Section ").Append(i).Append("\n\n");
            builder.Append("Paragraph for section ").Append(i)
                .Append(" with a [link](https://example.com/").Append(i)
                .Append(") and `code`, **bold**, *italic* spans that give the inline parser realistic work ")
                .Append("across a long document body, including enough plain words to exercise the scanner.\n\n");
            builder.Append("- first bullet ").Append(i).Append('\n');
            builder.Append("- second bullet with `span`\n");
            builder.Append("- third bullet **strong**\n\n");
            builder.Append("```csharp\nint value").Append(i).Append(" = ").Append(i).Append(";\n```\n\n");
            builder.Append("| Key | Value |\n| --- | --- |\n| item").Append(i).Append(" | ").Append(i * 3).Append(" |\n\n");
        }

        return builder.ToString();
    }

    static string BuildMarkupSettingsSource()
    {
        return
            "<style>.card { background: #1d2430; radius: 8; padding: 10; gap: 6; } .hint { color: #9ab0c4; }</style>" +
            "<column gap=\"8\">" +
            "<h2>Perf settings</h2>" +
            "<row gap=\"8\">" +
            "<column class=\"card\" gap=\"6\">" +
            "<text>Audio</text>" +
            "<row gap=\"6\"><text class=\"hint\">Master</text><slider id=\"master\" state=\"master\" /></row>" +
            "<row gap=\"6\"><text class=\"hint\">Music</text><slider id=\"music\" state=\"music\" /></row>" +
            "<row gap=\"6\"><text class=\"hint\">Voice</text><slider id=\"voice\" state=\"voice\" /></row>" +
            "<checkbox id=\"mute\" state=\"mute\">Mute all</checkbox>" +
            "<switch id=\"spatial\" state=\"spatial\">Spatial audio</switch>" +
            "</column>" +
            "<column class=\"card\" gap=\"6\">" +
            "<text>Video</text>" +
            "<row gap=\"6\"><text class=\"hint\">Scale</text><slider id=\"scale\" state=\"scale\" /></row>" +
            "<checkbox id=\"vsync\" state=\"vsync\">VSync</checkbox>" +
            "<checkbox id=\"hdr\" state=\"hdr\">HDR</checkbox>" +
            "<progress id=\"load\" state=\"load\" max=\"100\" />" +
            "</column>" +
            "</row>" +
            "<tabs id=\"pages\" state=\"page\">" +
            "<tab title=\"General\"><column gap=\"4\"><text>General body copy line.</text><badge>New</badge>" +
            "<chip id=\"tag-a\" state=\"tag-a\">Alpha</chip></column></tab>" +
            "<tab title=\"Advanced\"><column gap=\"4\"><text>Advanced body copy line.</text><badge>Beta</badge></column></tab>" +
            "</tabs>" +
            "<hr/>" +
            "<row gap=\"6\"><button id=\"apply\">Apply</button><button id=\"revert\">Revert</button><badge>v2.1</badge></row>" +
            "<text class=\"hint\">Footer hint with <strong>strong</strong> and <em>emphasis</em>.</text>" +
            "</column>";
    }

    static string BuildLargeMarkupSource()
    {
        var builder = new StringBuilder(24 * 1024);
        builder.Append(
            "<style>.card { background: #1d2430; radius: 8; padding: 10; gap: 6; } .hint { color: #9ab0c4; }</style>" +
            "<column gap=\"8\">");

        for (int i = 0; i < 24; ++i)
        {
            builder.Append("<column class=\"card\" gap=\"6\"><h3>Card ").Append(i).Append("</h3>");
            builder.Append("<row gap=\"6\"><text class=\"hint\">Level</text><slider id=\"level-").Append(i)
                .Append("\" state=\"level-").Append(i).Append("\" /></row>");
            builder.Append("<checkbox id=\"enable-").Append(i).Append("\" state=\"enable-").Append(i)
                .Append("\">Enable feature ").Append(i).Append("</checkbox>");
            builder.Append("<row gap=\"6\"><button id=\"apply-").Append(i).Append("\">Apply</button><badge>Slot ")
                .Append(i).Append("</badge></row>");
            builder.Append("<text>Body copy for card ").Append(i)
                .Append(" with <strong>strong</strong> and <em>em</em> spans.</text></column>");
        }

        builder.Append("</column>");
        return builder.ToString();
    }

    static string[] BuildParseVariants(string body)
    {
        var variants = new string[ParseVariantCount];

        for (int i = 0; i < variants.Length; ++i)
            variants[i] = body + "\n\nUnique trailer " + i.ToString("00") + "\n";

        return variants;
    }

    static string[] BuildMarkupParseVariants(string body)
    {
        var variants = new string[ParseVariantCount];

        for (int i = 0; i < variants.Length; ++i)
            variants[i] = body + "<text>Unique trailer " + i.ToString("00") + "</text>";

        return variants;
    }

    static string BuildCSharpDocument()
    {
        var builder = new StringBuilder(16 * 1024);
        builder.Append("using System;\nusing System.Collections.Generic;\n\nnamespace NowUI.Bench\n{\n");

        for (int i = 0; i < 36; ++i)
        {
            builder.Append("    /// <summary>Deterministic block ").Append(i).Append(" for tokenizer stress.</summary>\n");
            builder.Append("    public static class Block").Append(i).Append('\n');
            builder.Append("    {\n");
            builder.Append("        const string Label = \"block-").Append(i).Append("\";\n");
            builder.Append("        public static int Evaluate(int value)\n");
            builder.Append("        {\n");
            builder.Append("            // scale by the block index and fold in the label length\n");
            builder.Append("            return value * ").Append(i + 7).Append(" + Label.Length;\n");
            builder.Append("        }\n");
            builder.Append("    }\n\n");
        }

        builder.Append("}\n");
        return builder.ToString();
    }

    static NowNodeGraph BuildBenchmarkGraph(int nodeCount)
    {
        const int FloatType = 1;
        const int VectorType = 3;

        var graph = new NowNodeGraph();
        int columns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));

        for (int i = 0; i < nodeCount; ++i)
        {
            var node = graph.AddNode(
                $"n{i}",
                $"Node {i}",
                new Vector2((i % columns) * 210f, (i / columns) * 150f));
            node.size = new Vector2(180f, 118f);
            node.AddInput("a", "A", FloatType);
            node.AddInput("b", "B", VectorType);
            node.AddOutput("x", "X", FloatType);
            node.AddOutput("y", "Y", VectorType);
        }

        for (int i = 1; i < nodeCount; ++i)
            Assert.IsTrue(graph.TryAddLink($"n{i - 1}", "x", $"n{i}", "a"));

        for (int i = 2; i < nodeCount; i += 2)
            Assert.IsTrue(graph.TryAddLink($"n{i - 2}", "y", $"n{i}", "b"));

        Assert.Greater(graph.links.Count, nodeCount);
        return graph;
    }

    static void DrawSdfScene(int index)
    {
        var rect = new NowRect((index % 5) * 252f + 8f, (index / 5) * 352f + 8f, 240f, 340f);
        var scene = NowSdf.Scene(rect, _sdfSceneIds[index])
            .SetColor(new Color(0.25f + 0.1f * (index % 5), 0.55f, 0.85f, 1f));

        for (int i = 0; i < SdfShapesPerScene; ++i)
        {
            float x = 40f + (i % 3) * 76f;
            float y = 40f + (i / 3) * 74f;

            switch (i % 5)
            {
                case 0:
                    scene = scene.Circle(new Vector2(x, y), 24f);
                    break;
                case 1:
                    scene = scene.SmoothUnion(8f).RoundedBox(new NowRect(x - 26f, y - 18f, 52f, 36f), 9f);
                    break;
                case 2:
                    scene = scene.Union().Ellipse(new NowRect(x - 28f, y - 15f, 56f, 30f));
                    break;
                case 3:
                    scene = scene.Subtract().Circle(new Vector2(x, y), 12f);
                    break;
                default:
                    scene = scene.SmoothUnion(6f).Capsule(new Vector2(x - 20f, y), new Vector2(x + 20f, y), 11f);
                    break;
            }
        }

        scene.Draw();
    }

    static Action<NowRect>[] CreateDockContents(NowThemeAsset theme)
    {
        var contents = new Action<NowRect>[DockWindowTitles.Length];

        for (int i = 0; i < contents.Length; ++i)
        {
            int window = i;
            contents[i] = rect =>
            {
                theme.Rectangle(rect.Inset(6f), NowRectangleStyle.Muted).SetRadius(6f).Draw();

                for (int row = 0; row < 4; ++row)
                {
                    Now.Text(new NowRect(rect.x + 14f, rect.y + 14f + row * 20f, rect.width - 28f, 18f))
                        .SetFontSize(13f)
                        .SetColor(Color.white)
                        .Draw(_dockRows[window * 4 + row]);
                }
            };
        }

        return contents;
    }

    void MeasureNodeGraphCanvas(int nodeCount, string canvasId, int measurementCount)
    {
        ResetInteractiveState();
        var pointer = new FakePointer();
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;
        var graph = BuildBenchmarkGraph(nodeCount);
        var drawList = new NowDrawList();
        var surface = new Vector2(1920, 1080);
        var canvasRect = new NowRect(0, 0, 1920, 1080);

        try
        {
            Measure.Method(() =>
                {
                    NowTextInput.Invalidate();
                    NowOverlay.ForceNewFrame();

                    using (NowInput.Begin(pointer, surface))
                    using (drawList.Begin(surface))
                    {
                        NowNodes.Canvas(graph, canvasRect, canvasId).Draw();
                        NowOverlay.Flush();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(measurementCount)
                .Run();
        }
        finally
        {
            drawList.Dispose();
            ResetInteractiveState();
        }
    }

    /// <summary>
    /// Guards the full markdown measure-and-wrap pass: the width alternates
    /// between 700 and 640 every iteration, so the cached layout is invalid
    /// each frame and the document re-lays-out headings, lists, links, a code
    /// fence, and a table before replaying its ops.
    /// </summary>
    [Test, Performance]
    public void MarkdownRelayoutStress()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var document = NowMarkdown.Parse(BuildMarkdownArticle());
        var drawList = new NowDrawList();
        int iteration = 0;

        try
        {
            Action drawContent = () =>
            {
                float width = (iteration & 1) == 0 ? 700f : 640f;
                ++iteration;
                document.Draw(new NowRect(16f, 16f, width, 1960f));
            };

            Action drawFrame = () =>
            {
                using (drawList.Begin(new Vector2(960, 2000)))
                    WithPerfTheme(theme, font, drawContent);
            };

            Measure.Method(drawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            RecordSteadyStateAllocations(drawFrame);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Guards the per-frame code-fence draw path: a document that is mostly a
    /// ~140-line C# fence drawn at a fixed width, exercising the colored token
    /// runs and the contrast math that resolves syntax colors every draw.
    /// </summary>
    [Test, Performance]
    public void MarkdownCodeFenceHeavy()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var document = NowMarkdown.Parse(BuildCodeFenceMarkdown());
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(960, 3600)))
                        WithPerfTheme(theme, font, () => document.Draw(new NowRect(16f, 16f, 760f, 3560f)));
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

    /// <summary>
    /// Guards cold markdown parse throughput on a ~30KB document; each
    /// iteration parses a pre-built unique string so no text-keyed cache can
    /// serve the request.
    /// </summary>
    [Test, Performance]
    public void MarkdownParseCold()
    {
        var variants = BuildParseVariants(BuildLargeMarkdown());
        int index = 0;
        NowMarkdownDocument parsed = null;

        Measure.Method(() => parsed = NowMarkdownDocument.Parse(variants[index++ % variants.Length]))
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();

        Assert.NotNull(parsed);
    }

    /// <summary>
    /// Guards cold markup parse throughput (parser, document build, manifest)
    /// on a repeated-card document; each iteration parses a pre-built unique
    /// string so the shared markup cache is never involved.
    /// </summary>
    [Test, Performance]
    public void MarkupParseCold()
    {
        var variants = BuildMarkupParseVariants(BuildLargeMarkupSource());
        int index = 0;
        NowMarkupDocument parsed = null;

        Measure.Method(() => parsed = NowMarkupDocument.Parse(variants[index++ % variants.Length]))
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();

        Assert.NotNull(parsed);
    }

    /// <summary>
    /// Guards the steady-state markup draw path: a ~50-node settings document
    /// (style block, rows, columns, sliders, checkboxes, switch, tabs, badges)
    /// drawn per rebuild from a stable string and state.
    /// </summary>
    [Test, Performance]
    public void MarkupSteadyStateDraw()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        ResetInteractiveState();
        NowMarkup.Reset();

        var pointer = new FakePointer();
        var document = NowMarkupDocument.Parse(BuildMarkupSettingsSource());
        var state = new NowMarkupState();
        state.SetFloat("master", 0.8f);
        state.SetFloat("music", 0.5f);
        state.SetFloat("voice", 0.65f);
        state.SetFloat("scale", 0.9f);
        state.SetFloat("load", 40f);
        state.SetBool("mute", false);
        state.SetBool("spatial", true);
        state.SetBool("vsync", true);
        state.SetBool("hdr", false);
        state.SetInt("page", 0);

        var drawList = new NowDrawList();
        var surface = new Vector2(960, 1200);
        var contentRect = new NowRect(16f, 16f, 720f, 1160f);

        try
        {
            Action drawContent = () => document.Draw(contentRect, state);

            Action drawFrame = () =>
            {
                using (NowInput.Begin(pointer, surface))
                using (drawList.Begin(surface))
                    WithPerfTheme(theme, font, drawContent);
            };

            Measure.Method(drawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            RecordSteadyStateAllocations(drawFrame);
        }
        finally
        {
            drawList.Dispose();
            NowMarkup.Reset();
            ResetInteractiveState();
        }
    }

    /// <summary>
    /// Guards the node graph canvas rebuild at a moderate size: 100 nodes with
    /// mixed-type ports and ~1.5 links per node, drawn through the full canvas
    /// (background, grid, links, nodes, ports) every frame.
    /// </summary>
    [Test, Performance]
    public void NodeGraphCanvas100()
    {
        MeasureNodeGraphCanvas(100, "perf-nodes-100", 20);
    }

    /// <summary>
    /// Guards node graph scaling: same shape as the 100-node benchmark at 400
    /// nodes, so superlinear growth in the canvas rebuild shows up as a
    /// disproportionate gap between the two timings.
    /// </summary>
    [Test, Performance]
    public void NodeGraphCanvas400()
    {
        MeasureNodeGraphCanvas(400, "perf-nodes-400", 10);
    }

    /// <summary>
    /// Guards the code editor repaint path: a fixed ~330-line C# document in a
    /// scrolling viewport, redrawn per rebuild. The editor is drawn unfocused
    /// (no caret or selection input is simulated) because the per-frame cost
    /// being guarded is gutter plus tokenized visible-line drawing.
    /// </summary>
    [Test, Performance]
    public void CodeEditorRepaint300Lines()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        ResetInteractiveState();
        NowCodeEditor.ResetCaches();

        var pointer = new FakePointer();
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;
        string text = BuildCSharpDocument();
        var drawList = new NowDrawList();
        var surface = new Vector2(800, 600);
        var editorRect = new NowRect(16f, 16f, 760f, 560f);

        try
        {
            Action drawContent = () =>
            {
                NowCode.Editor(editorRect, NowCSharpLanguage.instance, "perf-code").Draw(ref text);
            };

            Action drawFrame = () =>
            {
                NowTextInput.Invalidate();

                using (NowInput.Begin(pointer, surface))
                using (drawList.Begin(surface))
                    WithPerfTheme(theme, font, drawContent);
            };

            Measure.Method(drawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            RecordSteadyStateAllocations(drawFrame);
        }
        finally
        {
            drawList.Dispose();
            NowCodeEditor.ResetCaches();
            ResetInteractiveState();
        }
    }

    /// <summary>
    /// Guards Lottie frame tessellation: the caller-passed clock advances a
    /// fixed 1/30s per iteration, which moves the 60fps/140-frame emoji
    /// composition two frames forward each draw, so every iteration misses the
    /// frame cache and runs the evaluate-plus-tessellate path.
    /// </summary>
    [Test, Performance]
    public void LottieFrameTessellation()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var asset = AssetDatabase.LoadAssetAtPath<NowLottieAsset>("Assets/NowUI/Assets/AnimatedEmoji/1f600.lottie");
        Assert.NotNull(asset, "AnimatedEmoji Lottie asset is required for the tessellation benchmark.");
        Assert.NotNull(asset.composition, "AnimatedEmoji Lottie asset failed to parse.");

        NowLottieRenderer.ClearCache();
        var drawList = new NowDrawList();
        float time = 0f;

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(256, 256)))
                    {
                        Now.Lottie(new NowRect(16f, 16f, 96f, 96f), asset)
                            .SetTime(time)
                            .Draw();
                    }

                    time += 1f / 30f;
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
            NowLottieRenderer.ClearCache();
        }
    }

    /// <summary>
    /// Guards the SDF batching and per-scene constant upload path: 10 scenes
    /// of 10 shapes each (100 shapes total) built per rebuild, each scene with
    /// its own id so it keeps its own cache and material like real usage.
    /// </summary>
    [Test, Performance]
    public void SdfShapesFrameBuild()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/SdfMaterial"));
        NowSdf.Reset();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1280, 720)))
                    {
                        for (int s = 0; s < SdfSceneCount; ++s)
                            DrawSdfScene(s);
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
            NowSdf.Reset();
        }
    }

    /// <summary>
    /// Guards the docking layout rebuild: six windows arranged into a typical
    /// editor split (left, right, bottom with merged tabs) are submitted and
    /// drawn every frame, covering tab bars, splitters, and window content
    /// dispatch.
    /// </summary>
    [Test, Performance]
    public void DockingLayoutFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        ResetInteractiveState();

        var pointer = new FakePointer();
        var dock = new NowDockSpace();
        var contents = CreateDockContents(theme);
        var drawList = new NowDrawList();
        var surface = new Vector2(1280, 720);
        var dockRect = new NowRect(0, 0, 1280, 720);

        try
        {
            Action drawSpace = () =>
            {
                NowDock.Space(dock, dockRect, "perf-dock").Draw();
                NowOverlay.Flush();
            };

            Action drawFrame = () =>
            {
                for (int i = 0; i < DockWindowTitles.Length; ++i)
                    dock.Window(DockWindowTitles[i], contents[i]);

                using (NowInput.Begin(pointer, surface))
                using (drawList.Begin(surface))
                    WithPerfTheme(theme, font, drawSpace);
            };

            drawFrame();

            Assert.IsTrue(dock.Dock("Inspector", "Scene", NowDockSide.Right, 0.25f));
            Assert.IsTrue(dock.Dock("Hierarchy", "Scene", NowDockSide.Left, 0.22f));
            Assert.IsTrue(dock.Dock("Console", "Scene", NowDockSide.Bottom, 0.3f));
            Assert.IsTrue(dock.Dock("Project", "Console", NowDockSide.Center));

            drawFrame();

            Measure.Method(drawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
            ResetInteractiveState();
        }
    }
}
