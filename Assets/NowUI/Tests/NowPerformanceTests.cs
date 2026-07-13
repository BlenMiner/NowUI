using System;
using System.Reflection;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NowUI;
using NowUI.Markdown;
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

    const string DocsParagraph =
        "NowUI rebuilds the page from immediate draw calls, which means text, layout, controls, overlays and mesh upload all need to stay predictable under a real docs workload.";

    const string DocsMarkdownSample =
        "# Performance docs page\n\n" +
        "This benchmark mirrors the docs browser: a markdown article with headings, inline `code`, links, lists, and code fences mixed with ordinary controls.\n\n" +
        "## Rendering model\n\n" +
        "The retained host calls into immediate-mode UI every rebuild. The hot path includes a layout measure pass, shaped text drawing, control measurement, and mesh upload.\n\n" +
        "- paragraph layout and wrapping\n" +
        "- shaped text draw calls\n" +
        "- inline code and emphasis spans\n" +
        "- links, lists, and fenced code\n\n" +
        "```csharp\n" +
        "using (Now.StartUI(surface))\n" +
        "{\n" +
        "    Now.Text(rect).SetFontSize(14).Draw(\"Measured text\");\n" +
        "    Now.Button(buttonRect, \"Apply\").Draw();\n" +
        "}\n" +
        "```\n\n" +
        "The goal of this test is not to assert a universal time, but to preserve a representative workload so optimization choices have a stable target.";

    const int DocsTextRows = 72;

    const int DocsOverviewPage = 0;

    const int DocsGlassDemoPage = 26;

    static readonly string[] DocsMenuItems =
    {
        "Overview",
        "Features",
        "Layout",
        "Controls",
        "Lines",
        "Shapes",
        "Glass",
        "Custom materials",
        "Effects",
        "Custom controls",
        "Styles & themes",
        "Markdown",
        "Lottie",
        "Mobile",
        "Render pipelines",
        "IMGUI",
        "Code editor",
        "Docking",
        "Rich text",
        "SDF shapes",
        "Lines demo",
        "Shapes demo",
        "Glass demo",
        "Effects demo",
        "Live demo",
        "Editor demo"
    };

    static readonly Vector2[] CurvePoints =
    {
        new Vector2(0f, 0.78f),
        new Vector2(0.08f, 0.65f),
        new Vector2(0.16f, 0.72f),
        new Vector2(0.24f, 0.34f),
        new Vector2(0.33f, 0.43f),
        new Vector2(0.43f, 0.18f),
        new Vector2(0.54f, 0.52f),
        new Vector2(0.66f, 0.38f),
        new Vector2(0.78f, 0.82f),
        new Vector2(0.9f, 0.6f),
        new Vector2(1f, 0.7f)
    };

    static int _overlayState;

    static readonly NowGlassDiagnosticEntry[] _diagnosticScratch = new NowGlassDiagnosticEntry[4];

    static readonly string[] _textRows = CreateTextRows();

    static readonly AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    sealed class PerfWorldGraphic : NowWorldGraphic
    {
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

    interface IPerfNowGraphic
    {
        Action<NowRect> draw { set; }

        void RebuildForTest();
    }

    sealed class PerfNowGraphic : NowGraphic, IPerfNowGraphic
    {
        public Action<NowRect> draw { private get; set; }

        protected override void DrawNowUI(NowRect rect)
        {
            draw?.Invoke(rect);
        }

        public void RebuildForTest()
        {
            UpdateGeometry();
        }
    }

    sealed class PerfNowLayoutGraphic : NowLayoutGraphic, IPerfNowGraphic
    {
        public Action<NowRect> draw { private get; set; }

        protected override void DrawNowUI(NowRect rect)
        {
            draw?.Invoke(rect);
        }

        public void RebuildForTest()
        {
            UpdateGeometry();
        }
    }

    static string[] CreateTextRows()
    {
        var rows = new string[DocsTextRows];

        for (int i = 0; i < rows.Length; ++i)
            rows[i] = $"{DocsParagraph} Row {i:00}: AV fi 0123456789 with enough text to exercise shaping and glyph upload.";

        return rows;
    }

    static NowThemeAsset LoadPerfTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");
        Assert.NotNull(theme, "MaterialDark theme asset is required for docs perf benchmarks.");
        return theme;
    }

    static NowFontAsset LoadPerfFont()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource is required for docs perf benchmarks.");
        return font;
    }

    static GameObject CreateCanvasRoot()
    {
        var root = new GameObject("Now Perf Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        return root;
    }

    static IPerfNowGraphic CreatePerfGraphic(
        Transform parent,
        Vector2 size,
        bool exactLayoutPass,
        Action<NowRect> draw)
    {
        var go = new GameObject("Now Perf Graphic", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rectTransform = go.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = size;

        var component = exactLayoutPass
            ? (Graphic)go.AddComponent<PerfNowLayoutGraphic>()
            : go.AddComponent<PerfNowGraphic>();
        component.raycastTarget = false;
        var graphic = (IPerfNowGraphic)component;
        graphic.draw = draw;
        return graphic;
    }

    static Component CreateDocsExample(
        Transform parent,
        Vector2 size,
        NowThemeAsset theme,
        NowFontAsset font,
        int selectedPage,
        out Action rebuild)
    {
        var docsType = FindDocsExampleType();
        var go = new GameObject("Now Perf Docs Example", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rectTransform = go.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = size;

        var docs = go.AddComponent(docsType);
        ((Graphic)docs).raycastTarget = false;
        SetDocsField(docs, "_themeAsset", theme);
        SetDocsField(docs, "_font", font);
        SetDocsField(docs, "_selected", selectedPage);

        var updateGeometry = docsType.GetMethod("UpdateGeometry", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(updateGeometry, "NowDocsExample.UpdateGeometry was not found.");
        rebuild = (Action)updateGeometry.CreateDelegate(typeof(Action), docs);
        return docs;
    }

    static Type FindDocsExampleType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("NowDocsExample");

            if (type != null)
                return type;
        }

        Assert.Fail("NowDocsExample type was not found in loaded assemblies.");
        return null;
    }

    static void SetDocsField(Component docs, string fieldName, object value)
    {
        var field = docs.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, $"NowDocsExample field '{fieldName}' was not found.");
        field.SetValue(docs, value);
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

    static void DrawDocsSidebar(NowThemeAsset theme)
    {
        var menuRect = new NowRect(12, 12, 250, 696);
        var menuTitleRect = new NowRect(menuRect.x, menuRect.y, menuRect.width, 24f);
        var menuListRect = new NowRect(menuRect.x, menuTitleRect.yMax + 8f, menuRect.width, menuRect.yMax - menuTitleRect.yMax - 8f);

        theme.Rectangle(menuRect, NowRectangleStyle.Muted).Draw();

        using (NowLayout.Area(menuTitleRect))
        {
            NowLayout.Label("Now Docs").SetFontSize(13)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
        }

        using (NowLayout.Area(menuListRect))
        using (NowLayout.ScrollView("perf-docs-menu").Begin())
        using (NowLayout.Vertical(spacing: 4f, padding: 2f))
        {
            for (int i = 0; i < DocsMenuItems.Length; ++i)
            {
                var style = i == 6 ? NowRectangleStyle.Accent : NowRectangleStyle.Muted;
                NowLayout.Button(DocsMenuItems[i]).SetId($"perf-doc-{i}").SetStyle(style)
                    .SetTextStyle(NowTextStyle.Body).SetStretchWidth().Draw();
            }
        }
    }

    static void DrawDocsTextRows(NowRect rect)
    {
        for (int i = 0; i < _textRows.Length; ++i)
        {
            Now.Text(new NowRect(rect.x, rect.y + i * 22f, rect.width, 20f))
                .SetFontSize(13f)
                .SetColor(Color.white)
                .Draw(_textRows[i]);
        }
    }

    static void DrawDocsShortTextRows(NowRect rect)
    {
        for (int i = 0; i < _textRows.Length; ++i)
        {
            Now.Text(new NowRect(rect.x, rect.y + i * 22f, rect.width, 20f))
                .SetFontSize(13f)
                .SetColor(Color.white)
                .Draw($"Row {i:00}: AV fi 0123456789");
        }
    }

    static Vector2 MeasureDocsTextRows(NowFontAsset font)
    {
        Vector2 total = default;

        for (int i = 0; i < _textRows.Length; ++i)
            total += font.MeasureText(_textRows[i], 13f);

        return total;
    }

    static void DrawDocsMarkdown(NowRect rect)
    {
        using (NowLayout.Area(rect))
        using (NowLayout.Vertical(spacing: 8f))
        {
            NowMarkdown.Document(DocsMarkdownSample).Draw();
        }
    }

    static void DrawDocsControls(NowThemeAsset theme, NowRect rect)
    {
        using (NowLayout.Area(rect))
        using (NowLayout.Vertical(spacing: 8f))
        {
            NowMarkdown.Document("## Control cluster\n\nA docs page often mixes explanatory text with compact controls.").Draw();

            bool flag = true;
            float a = 0.35f;
            float b = 0.72f;
            int choice = 1;
            string[] choices = { "Auto", "Fast", "Balanced", "High" };

            using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
            {
                NowLayout.Checkbox("Animate").Draw(ref flag);
                NowLayout.Label("Blur").SetWidth(34f).Draw();
                NowLayout.Slider(0f, 1f).SetWidth(120f).Draw(ref a);
                NowLayout.Label("Tint").SetWidth(34f).Draw();
                NowLayout.Slider(0f, 1f).SetWidth(120f).Draw(ref b);
                NowLayout.Dropdown(choices).SetWidth(118f).Draw(ref choice);
            }

            var panel = NowLayout.ReserveRect(height: 118f, stretchWidth: true);
            theme.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(8f).Draw();

            Now.Text(panel.Inset(12f, 10f))
                .SetFontSize(13f)
                .SetColor(theme.GetColor(NowColorToken.Text, Color.white))
                .Draw("This panel intentionally uses multiline shaped text, just like the glass diagnostics and docs examples.");
        }
    }

    static void DrawDocsLinesAndCurve(NowRect rect)
    {
        var plot = rect.Inset(8f);

        for (int i = 0; i <= 8; ++i)
        {
            float x = Mathf.Lerp(plot.x, plot.xMax, i / 8f);
            Now.Line(new Vector2(x, plot.y), new Vector2(x, plot.yMax)).SetColor(new Color(1f, 1f, 1f, 0.08f)).Draw();
        }

        for (int i = 0; i <= 5; ++i)
        {
            float y = Mathf.Lerp(plot.y, plot.yMax, i / 5f);
            Now.Line(new Vector2(plot.x, y), new Vector2(plot.xMax, y)).SetColor(new Color(1f, 1f, 1f, 0.08f)).Draw();
        }

        Span<Vector2> points = stackalloc Vector2[CurvePoints.Length];

        for (int i = 0; i < CurvePoints.Length; ++i)
        {
            var normalized = CurvePoints[i];
            points[i] = new Vector2(
                Mathf.Lerp(plot.x, plot.xMax, normalized.x),
                Mathf.Lerp(plot.yMax, plot.y, normalized.y));
        }

        Now.DrawPolyline(points, 2f, NowLineCap.Round, new Color(0.4f, 0.85f, 1f, 1f));

        for (int i = 0; i < points.Length; ++i)
        {
            Now.Circle(points[i], 4f)
                .SetColor(new Color(0.4f, 0.85f, 1f, 1f))
                .Draw();
        }
    }

    static void DrawDocsComposite(NowThemeAsset theme, NowFontAsset font, NowRect rect)
    {
        WithPerfTheme(theme, font, () =>
        {
            theme.Rectangle(new NowRect(0, 0, rect.width, rect.height), NowRectangleStyle.Surface).Draw();
            DrawDocsSidebar(theme);

            var content = new NowRect(274, 12, rect.width - 286, rect.height - 24);
            theme.Rectangle(content, NowRectangleStyle.Surface).Draw();

            using (NowLayout.Area(content))
            using (NowLayout.ScrollView("perf-docs-content").Begin())
            using (NowLayout.Vertical(spacing: 12f))
            {
                NowMarkdown.Document(DocsMarkdownSample).Draw();
                DrawDocsControls(theme, NowLayout.ReserveRect(height: 210f, stretchWidth: true));
                DrawDocsLinesAndCurve(NowLayout.ReserveRect(height: 180f, stretchWidth: true));
                DrawDocsTextRows(NowLayout.ReserveRect(height: DocsTextRows * 22f, stretchWidth: true));
            }
        });
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

    [Test, Performance]
    public void DocsCompositeGraphicRebuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var root = CreateCanvasRoot();

        try
        {
            var graphic = CreatePerfGraphic(
                root.transform,
                new Vector2(1280, 720),
                exactLayoutPass: true,
                rect => DrawDocsComposite(theme, font, rect));

            Measure.Method(() => graphic.RebuildForTest())
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test, Performance]
    public void DocsCompositeGraphicRebuildOnePass()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var root = CreateCanvasRoot();

        try
        {
            var graphic = CreatePerfGraphic(
                root.transform,
                new Vector2(1280, 720),
                exactLayoutPass: false,
                rect => DrawDocsComposite(theme, font, rect));

            Measure.Method(() => graphic.RebuildForTest())
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test, Performance]
    public void ActualDocsOverviewGraphicRebuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var root = CreateCanvasRoot();

        try
        {
            CreateDocsExample(
                root.transform,
                new Vector2(1280, 720),
                theme,
                font,
                DocsOverviewPage,
                out var rebuild);

            Measure.Method(() => rebuild())
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test, Performance]
    public void ActualDocsGlassDemoGraphicRebuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var root = CreateCanvasRoot();

        try
        {
            CreateDocsExample(
                root.transform,
                new Vector2(1280, 720),
                theme,
                font,
                DocsGlassDemoPage,
                out var rebuild);

            Measure.Method(() => rebuild())
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test, Performance]
    public void DocsTextRowsFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1280, 720)))
                    {
                        WithPerfTheme(theme, font, () => DrawDocsTextRows(new NowRect(16, 16, 1180, 680)));
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
    public void DocsTextRowsWithoutShapingFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();
        bool previousShaping = Now.textShaping;

        try
        {
            Now.textShaping = false;

            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1280, 720)))
                    {
                        WithPerfTheme(theme, font, () => DrawDocsTextRows(new NowRect(16, 16, 1180, 680)));
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Now.textShaping = previousShaping;
            drawList.Dispose();
        }
    }

    [Test, Performance]
    public void DocsShortTextRowsFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1280, 720)))
                    {
                        WithPerfTheme(theme, font, () => DrawDocsShortTextRows(new NowRect(16, 16, 1180, 680)));
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
    public void DocsTextRowsMeasureOnly()
    {
        var font = LoadPerfFont();
        Vector2 measured = default;

        Measure.Method(() => measured = MeasureDocsTextRows(font))
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();

        Assert.Greater(measured.x, 0f);
    }

    [Test, Performance]
    public void DocsTextRowsMeasureOnlyWithoutShaping()
    {
        var font = LoadPerfFont();
        bool previousShaping = Now.textShaping;
        Vector2 measured = default;

        try
        {
            Now.textShaping = false;

            Measure.Method(() => measured = MeasureDocsTextRows(font))
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            Assert.Greater(measured.x, 0f);
        }
        finally
        {
            Now.textShaping = previousShaping;
        }
    }

    [Test, Performance]
    public void DocsMarkdownFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(960, 720)))
                    {
                        WithPerfTheme(theme, font, () => DrawDocsMarkdown(new NowRect(16, 16, 900, 680)));
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
    public void DocsControlsFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(960, 320)))
                    {
                        WithPerfTheme(theme, font, () =>
                        {
                            DrawDocsSidebar(theme);
                            DrawDocsControls(theme, new NowRect(284, 16, 640, 260));
                        });
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
    public void DocsLinesAndCurveFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(960, 320)))
                    {
                        WithPerfTheme(theme, font, () => DrawDocsLinesAndCurve(new NowRect(16, 16, 900, 260)));
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
    public void AnimationCurveFieldFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    var curve = _curve;

                    using (drawList.Begin(new Vector2(760, 360)))
                    {
                        WithPerfTheme(theme, font, () =>
                        {
                            Now.AnimationCurveField(new NowRect(16, 16, 700, 280), "perf-curve")
                                .SetPopupSize(640, 360)
                                .Draw(ref curve);
                        });
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
