using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NowUI;

/// <summary>
/// Coverage-gap benchmarks for the runtime hot paths the docs-focused suite in
/// <see cref="NowPerformanceTests"/> does not isolate: the pure layout engine,
/// the label pipeline, per-frame text wrapping, large editable text, control
/// pipeline stress, id hashing, scroll views, theme scope resolution, an open
/// dropdown popup, batch-heavy mesh uploads and glass replay scaling. Timings
/// exist for before/after comparison, never as absolute assertions, and
/// allocation numbers are recorded via a GC.Alloc sample group instead of
/// asserted, so they give visibility without failing CI.
/// </summary>
public class NowRuntimePerformanceTests
{
    static readonly Vector2 FrameSize = new Vector2(1280, 720);

    const int SteadyStateAllocIterations = 16;

    const int LabelRows = 200;

    const string RepeatedLabel = "Inspector row label 0123456789";

    const string MultilineLabel =
        "Multiline label line one\nsecond line with digits 0123\nthird line mixing AV fi ligatures";

    const int DropdownFieldId = 0x5D5D0100;

    const int TextAreaFieldId = 0x54455841;

    const int ThemeButtonIdBase = 0x54480000;

    static readonly string[] UniqueLabels = BuildUniqueLabels(LabelRows);

    static readonly string[] ScrollRowLabels = BuildScrollRowLabels(1000);

    static readonly string[] StressButtonIds = BuildIds("perf-stress-btn-", 500);

    static readonly string[] StressCheckboxIds = BuildIds("perf-stress-chk-", 250);

    static readonly string[] StressSliderIds = BuildIds("perf-stress-sld-", 250);

    static readonly string[] InteractIds = BuildIds("perf-hash-", 1000);

    static readonly string[] DropdownOptions = BuildDropdownOptions(1000);

    static readonly bool[] StressCheckboxValues = BuildCheckboxValues(250);

    static readonly float[] StressSliderValues = BuildSliderValues(250);

    static readonly string TooltipParagraph = BuildTooltipParagraph();

    static readonly string LargeTextAreaText = BuildLargeTextAreaText();

    sealed class PerfPointer : INowInputProvider
    {
        public NowInputSnapshot snapshot = new NowInputSnapshot(new Vector2(-100f, -100f), false, false, false);

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    PerfPointer _pointer;

    string _textAreaScratch;

    int _dropdownSelected;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();

        _pointer = new PerfPointer();
        _textAreaScratch = LargeTextAreaText;
        _dropdownSelected = 0;
    }

    [TearDown]
    public void TearDown()
    {
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    static string[] BuildIds(string prefix, int count)
    {
        var ids = new string[count];

        for (int i = 0; i < count; ++i)
            ids[i] = prefix + i.ToString("0000");

        return ids;
    }

    static string[] BuildUniqueLabels(int count)
    {
        var labels = new string[count];

        for (int i = 0; i < count; ++i)
            labels[i] = $"Row {i:000} value {i * 37 % 100:00} status ready";

        return labels;
    }

    static string[] BuildScrollRowLabels(int count)
    {
        var labels = new string[count];

        for (int i = 0; i < count; ++i)
            labels[i] = $"Entry {i:0000} — asset bundle {i * 13 % 97:00} imported";

        return labels;
    }

    static string[] BuildDropdownOptions(int count)
    {
        var options = new string[count];

        for (int i = 0; i < count; ++i)
            options[i] = $"Option {i:0000}";

        return options;
    }

    static bool[] BuildCheckboxValues(int count)
    {
        var values = new bool[count];

        for (int i = 0; i < count; ++i)
            values[i] = (i & 1) == 0;

        return values;
    }

    static float[] BuildSliderValues(int count)
    {
        var values = new float[count];

        for (int i = 0; i < count; ++i)
            values[i] = (i % 10) * 0.1f;

        return values;
    }

    static string BuildTooltipParagraph()
    {
        const string Sentence =
            "Tooltips relayout their entire paragraph every frame because wrap positions depend on the measured width of each word at the current font size and style. ";
        var builder = new StringBuilder(Sentence.Length * 8 + 16);

        for (int i = 0; i < 8; ++i)
            builder.Append(Sentence);

        return builder.ToString();
    }

    static string BuildLargeTextAreaText()
    {
        const string Sentence =
            "The text area relays out every visual line on each rebuild, so long documents stress wrapping cost. ";
        var builder = new StringBuilder(Sentence.Length * 50 + 16);

        for (int i = 0; i < 50; ++i)
        {
            builder.Append(Sentence);

            if (i % 5 == 4)
                builder.Append('\n');
        }

        return builder.ToString();
    }

    static NowThemeAsset LoadPerfTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");
        Assert.NotNull(theme, "MaterialDark theme asset is required for runtime perf benchmarks.");
        return theme;
    }

    static NowFontAsset LoadPerfFont()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource is required for runtime perf benchmarks.");
        return font;
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

    /// <summary>
    /// Records the average bytes allocated per steady-state frame as a
    /// "GC.Alloc" sample group. Purely informational: regressions show up in
    /// the Performance Test Report without ever failing CI.
    /// </summary>
    static void RecordSteadyStateAllocations(Action drawFrame, int iterations)
    {
        drawFrame();
        drawFrame();
        drawFrame();

        long before = AllocatedBytesOrIgnore();

        for (int i = 0; i < iterations; ++i)
            drawFrame();

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Measure.Custom(new SampleGroup("GC.Alloc", SampleUnit.Byte, false), allocated / (double)iterations);
    }

    /// <summary>
    /// Pure layout engine overhead: one explicitly-keyed area with 75 nested
    /// horizontal/vertical groups and roughly a thousand plain rects mixing
    /// fixed sizes, stretch widths, spacing, padding and flexible space. Uses
    /// <see cref="NowLayout.Rect(float, float, bool, bool, NowLayoutAlign)"/>
    /// children so no control or text cost pollutes the number.
    /// </summary>
    [Test, Performance]
    public void LayoutEngineStressRebuild()
    {
        Measure.Method(() =>
            {
                using (NowLayout.Area("perf-layout-stress", new NowRect(0f, 0f, 1600f, 1000f)))
                {
                    for (int g = 0; g < 25; ++g)
                    {
                        using (NowLayout.Vertical(spacing: 2f, stretchWidth: true))
                        {
                            using (NowLayout.Horizontal(spacing: 4f, stretchWidth: true))
                            {
                                for (int c = 0; c < 18; ++c)
                                    NowLayout.Rect(width: 20f + (c % 4) * 12f, height: 14f);

                                NowLayout.FlexibleSpace();
                                NowLayout.Rect(height: 14f, stretchWidth: true);
                            }

                            using (NowLayout.Horizontal(spacing: 4f, alignItems: NowLayoutAlign.Center))
                            {
                                NowLayout.Space(8f);

                                for (int c = 0; c < 18; ++c)
                                    NowLayout.Rect(width: 16f + (c % 3) * 10f, height: 12f + (c % 2) * 6f);

                                NowLayout.FlexibleSpace(2f);
                            }
                        }
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();
    }

    /// <summary>
    /// Label pipeline with the friendliest input: 200 identical strings per
    /// rebuild, so every measurement and glyph lookup hits warm caches.
    /// </summary>
    [Test, Performance]
    public void LabelPipelineRepeatedStrings()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(FrameSize))
                    using (NowTheme.Scope(theme))
                    using (NowLayout.Area("perf-labels-repeated", new NowRect(16f, 16f, 600f, 4200f)))
                    using (NowLayout.Vertical(spacing: 2f))
                    {
                        for (int i = 0; i < LabelRows; ++i)
                            NowLayout.Label(RepeatedLabel).Draw();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Label pipeline with 200 distinct per-row strings, the inspector/list
    /// worst case where string-keyed measurement caches cannot collapse rows.
    /// </summary>
    [Test, Performance]
    public void LabelPipelineUniqueStrings()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(FrameSize))
                    using (NowTheme.Scope(theme))
                    using (NowLayout.Area("perf-labels-unique", new NowRect(16f, 16f, 600f, 4200f)))
                    using (NowLayout.Vertical(spacing: 2f))
                    {
                        for (int i = 0; i < UniqueLabels.Length; ++i)
                            NowLayout.Label(UniqueLabels[i]).Draw();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Label pipeline with embedded newlines: 200 three-line labels per
    /// rebuild, guarding the multi-line measure and draw path.
    /// </summary>
    [Test, Performance]
    public void LabelPipelineMultilineStrings()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(FrameSize))
                    using (NowTheme.Scope(theme))
                    using (NowLayout.Area("perf-labels-multiline", new NowRect(16f, 16f, 600f, 12600f)))
                    using (NowLayout.Vertical(spacing: 2f))
                    {
                        for (int i = 0; i < LabelRows; ++i)
                            NowLayout.Label(MultilineLabel).Draw();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// The tooltip/markdown word-wrap path: a fixed 200-word paragraph laid
    /// out through <see cref="NowTextWrap.Layout"/> and rendered through
    /// <see cref="NowTextWrap.Draw"/> every frame. Layout clears the run list
    /// each pass, so Draw re-materializes run substrings every frame — exactly
    /// the relayout-per-frame cost this guards. GC.Alloc is recorded, not
    /// asserted.
    /// </summary>
    [Test, Performance]
    public void TextWrapTooltipRelayout()
    {
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();
        var runs = new List<NowTextRun>(256);
        var style = new NowText(default, font).SetFontSize(14f);

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(FrameSize))
                {
                    NowTextWrap.Layout(style, TooltipParagraph, 480f, runs);
                    NowTextWrap.Draw(style, TooltipParagraph, runs, new Vector2(16f, 16f));
                }
            }

            Measure.Method(DrawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// A text area holding roughly five thousand characters of fixed text,
    /// drawn unfocused at an explicit rect every rebuild. The control re-wraps
    /// every visual line on each draw, which is the cost this guards.
    /// Simplification: no focus or keyboard input is simulated — the wrap and
    /// visible-line rendering path dominates either way. GC.Alloc is recorded,
    /// not asserted.
    /// </summary>
    [Test, Performance]
    public void LargeTextAreaRedraw()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (NowInput.Begin(_pointer, FrameSize))
                using (drawList.Begin(FrameSize))
                using (NowTheme.Scope(theme))
                {
                    Now.TextArea(new NowRect(16f, 16f, 600f, 320f), TextAreaFieldId)
                        .Draw(ref _textAreaScratch);
                }
            }

            Measure.Method(DrawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Control pipeline stress: 500 buttons, 250 checkboxes and 250 sliders
    /// with explicit string ids inside one scroll view, all rebuilt per frame.
    /// Guards the per-control cost of id resolution, interaction, theming and
    /// layout reservation at realistic UI scale. GC.Alloc is recorded, not
    /// asserted.
    /// </summary>
    [Test, Performance]
    public void ManyControlsStressFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (NowInput.Begin(_pointer, FrameSize))
                using (drawList.Begin(FrameSize))
                using (NowTheme.Scope(theme))
                using (NowLayout.Area("perf-stress-area", new NowRect(16f, 16f, 460f, 640f)))
                using (NowLayout.ScrollView("perf-stress-scroll").Begin())
                using (NowLayout.Vertical(spacing: 2f, padding: 2f))
                {
                    for (int i = 0; i < StressButtonIds.Length; ++i)
                        NowLayout.Button("Command").SetId(StressButtonIds[i]).SetStretchWidth().Draw();

                    for (int i = 0; i < StressCheckboxIds.Length; ++i)
                        NowLayout.Checkbox("Enabled").SetId(StressCheckboxIds[i]).Draw(ref StressCheckboxValues[i]);

                    for (int i = 0; i < StressSliderIds.Length; ++i)
                        NowLayout.Slider(0f, 1f).SetWidth(180f).SetId(StressSliderIds[i]).Draw(ref StressSliderValues[i]);
                }
            }

            Measure.Method(DrawFrame)
                .WarmupCount(5)
                .MeasurementCount(10)
                .Run();

            RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// The cheapest interactive path in isolation: one thousand
    /// <see cref="NowInput.Interact(string, NowRect)"/> calls with prebuilt
    /// string ids per frame, measuring id hashing plus pointer arbitration
    /// with no rendering or layout attached.
    /// </summary>
    [Test, Performance]
    public void InteractIdHashingStress()
    {
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (NowInput.Begin(_pointer, FrameSize))
                    using (drawList.Begin(FrameSize))
                    {
                        for (int i = 0; i < InteractIds.Length; ++i)
                            NowInput.Interact(InteractIds[i], new NowRect((i * 29) % 1200, (i * 17) % 680, 48f, 20f));
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

    /// <summary>
    /// Repaint-state tracking for one thousand inactive controls with integer ids,
    /// isolated from rendering, layout, and string hashing. Guards the steady-state
    /// cost of <see cref="NowControls.Interact(int, NowRect, out bool, out bool)"/>.
    /// </summary>
    [Test, Performance]
    public void InteractionRepaintTrackingStress()
    {
        void DrawFrame()
        {
            using (NowInput.Begin(_pointer, FrameSize))
            {
                for (int i = 0; i < 1000; ++i)
                {
                    var rect = new NowRect((i * 29) % 1200, (i * 17) % 680, 48f, 20f);
                    NowControls.Interact(0x49520000 + i, rect, out _, out _);
                }
            }
        }

        Measure.Method(DrawFrame)
            .WarmupCount(10)
            .MeasurementCount(50)
            .Run();

        RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
    }

    /// <summary>
    /// One thousand settled transition updates with stable integer ids, isolating
    /// control-state lookup and frame-clock sampling from rendering and layout.
    /// </summary>
    [Test, Performance]
    public void TransitionTimingStress()
    {
        void DrawFrame()
        {
            for (int i = 0; i < 1000; ++i)
                NowControlState.Transition(0x54520000 + i, false);
        }

        Measure.Method(DrawFrame)
            .WarmupCount(10)
            .MeasurementCount(50)
            .Run();

        RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
    }

    /// <summary>
    /// Non-virtualized scroll view worst case: one thousand unique label rows
    /// laid out per rebuild. Content far outside the viewport still pays
    /// measurement and layout, which is the regression this makes visible.
    /// </summary>
    [Test, Performance]
    public void ScrollViewThousandRowsFrameBuild()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (NowInput.Begin(_pointer, FrameSize))
                    using (drawList.Begin(FrameSize))
                    using (NowTheme.Scope(theme))
                    using (NowLayout.Area("perf-scroll-rows-area", new NowRect(16f, 16f, 480f, 640f)))
                    using (NowLayout.ScrollView("perf-scroll-rows").Begin())
                    using (NowLayout.Vertical(spacing: 2f, padding: 2f))
                    {
                        for (int i = 0; i < ScrollRowLabels.Length; ++i)
                            NowLayout.Label(ScrollRowLabels[i]).Draw();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(10)
                .Run();
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    static void DrawThemedButtonGrid()
    {
        for (int i = 0; i < 100; ++i)
        {
            var rect = new NowRect(8f + (i % 10) * 126f, 8f + (i / 10) * 34f, 118f, 28f);
            Now.Button(rect, "Theme").SetId(ThemeButtonIdBase + i).Draw();
        }
    }

    /// <summary>
    /// One hundred themed buttons resolved through the ambient default theme
    /// with no explicit scope pushed — the fallback resolution path every
    /// unscoped control pays per draw.
    /// </summary>
    [Test, Performance]
    public void ThemeResolutionWithoutScope()
    {
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (NowInput.Begin(_pointer, FrameSize))
                    using (drawList.Begin(FrameSize))
                    {
                        DrawThemedButtonGrid();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// The same hundred-button grid drawn inside three nested
    /// <see cref="NowTheme.Scope"/> levels of the ambient default asset, so
    /// rendering matches <see cref="ThemeResolutionWithoutScope"/> and the
    /// delta isolates stacked-scope resolution cost.
    /// </summary>
    [Test, Performance]
    public void ThemeResolutionNestedScopes()
    {
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var theme = NowTheme.themeAsset;
        Assert.NotNull(theme, "Ambient default theme asset is required for the nested scope benchmark.");
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (NowInput.Begin(_pointer, FrameSize))
                    using (drawList.Begin(FrameSize))
                    using (NowTheme.Scope(theme))
                    using (NowTheme.Scope(theme))
                    using (NowTheme.Scope(theme))
                    {
                        DrawThemedButtonGrid();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// A dropdown field with one thousand options whose popup is held open
    /// every rebuild, deferring the full option list through the overlay
    /// flush. Simplification: instead of simulating the opening click, the
    /// control's retained open flag is set directly via
    /// <see cref="NowControlState.Get{T}(int)"/> — the same state a real click
    /// toggles — which keeps the benchmark free of fragile input scripting.
    /// </summary>
    [Test, Performance]
    public void OpenDropdownPopupSteadyState()
    {
        var theme = LoadPerfTheme();
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            NowControlState.Get<bool>(DropdownFieldId) = true;

            Measure.Method(() =>
                {
                    using (NowInput.Begin(_pointer, FrameSize))
                    using (drawList.Begin(FrameSize))
                    using (NowTheme.Scope(theme))
                    {
                        Now.Dropdown(new NowRect(40f, 40f, 260f, 30f), DropdownFieldId, DropdownOptions)
                            .Draw(ref _dropdownSelected);
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(10)
                .Run();
        }
        finally
        {
            NowControlState.Get<bool>(DropdownFieldId) = false;
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Batch-splitting stress: 200 items alternating rounded rects, shaped
    /// text, circles, lines and textured rects, forcing frequent draw-kind
    /// changes during mesh build and upload. The resulting
    /// <see cref="NowDrawList.batchCount"/> is reported as a "Batches" sample
    /// group so batching regressions are visible next to the timing.
    /// </summary>
    [Test, Performance]
    public void BatchHeavyMeshUpload()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(FrameSize))
                {
                    for (int i = 0; i < 200; ++i)
                    {
                        float x = 8f + (i % 20) * 63f;
                        float y = 8f + (i / 20) * 70f;

                        switch (i % 5)
                        {
                            case 0:
                                Now.Rectangle(new NowRect(x, y, 56f, 24f)).SetColor(Color.white).SetRadius(6f).Draw();
                                break;
                            case 1:
                                Now.Text(new NowRect(x, y, 56f, 20f)).SetFontSize(13f).SetColor(Color.white).Draw("Batch 42");
                                break;
                            case 2:
                                Now.Circle(new Vector2(x + 12f, y + 12f), 10f).SetColor(Color.white).Draw();
                                break;
                            case 3:
                                Now.Line(new Vector2(x, y), new Vector2(x + 48f, y + 20f)).SetColor(Color.white).Draw();
                                break;
                            default:
                                Now.Rectangle(new NowRect(x, y, 56f, 24f)).SetTexture(texture).Draw();
                                break;
                        }
                    }
                }
            }

            Measure.Method(DrawFrame)
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();

            DrawFrame();
            Measure.Custom(new SampleGroup("Batches", SampleUnit.Undefined, false), drawList.batchCount);
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void MeasureGlassReplay(int paneCount)
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassBlurMaterial"));

        var drawList = new NowDrawList();
        var commandBuffer = new CommandBuffer();

        try
        {
            Measure.Method(() =>
                {
                    commandBuffer.Clear();

                    using (drawList.Begin(FrameSize))
                    {
                        for (int i = 0; i < 100; ++i)
                        {
                            Now.Rectangle(new NowRect((i * 37) % 1200, (i * 23) % 660, 72f, 36f))
                                .SetColor(new Color(0.2f + (i % 5) * 0.15f, 0.4f, 0.8f, 1f))
                                .SetRadius(4f)
                                .Draw();
                        }

                        for (int n = 0; n < paneCount; ++n)
                        {
                            Now.Glass(new NowRect(40f + (n % 4) * 300f, 60f + (n / 4) * 160f, 240f, 120f))
                                .SetBlurRadius(12f)
                                .Draw();
                        }
                    }

                    NowRenderer.Draw(commandBuffer, drawList);
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            commandBuffer.Release();
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Glass replay scaling baseline: 100 mixed rects plus a single glass
    /// pane, built and populated into a command buffer per frame like
    /// <c>GlassDiagnosticsAreAllocationFreeAfterWarmup</c> does.
    /// </summary>
    [Test, Performance]
    public void GlassReplayScalingOnePane()
    {
        MeasureGlassReplay(1);
    }

    /// <summary>Glass replay scaling at four panes; compare against the one-pane baseline.</summary>
    [Test, Performance]
    public void GlassReplayScalingFourPanes()
    {
        MeasureGlassReplay(4);
    }

    /// <summary>Glass replay scaling at sixteen panes; compare against the one-pane baseline.</summary>
    [Test, Performance]
    public void GlassReplayScalingSixteenPanes()
    {
        MeasureGlassReplay(16);
    }

    /// <summary>
    /// Steady-state allocation recording for the headline rectangle build
    /// (1000 rects per frame, mirroring <c>RectangleFrameBuild</c>). Reported
    /// as a GC.Alloc sample group only — before/after visibility with no CI
    /// failure mode.
    /// </summary>
    [Test, Performance]
    public void RectangleFrameBuildAllocRecorded()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(1920, 1080)))
                {
                    for (int i = 0; i < 1000; ++i)
                    {
                        Now.Rectangle(new NowRect((i * 7) % 1800, (i * 13) % 1000, 64f, 32f))
                            .SetColor(Color.white)
                            .SetRadius(4f)
                            .Draw();
                    }
                }
            }

            RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    /// <summary>
    /// Steady-state allocation recording for the headline text build
    /// (100 labels per frame, mirroring <c>TextFrameBuild</c>). Reported as a
    /// GC.Alloc sample group only — before/after visibility with no CI
    /// failure mode.
    /// </summary>
    [Test, Performance]
    public void TextFrameBuildAllocRecorded()
    {
        var font = LoadPerfFont();
        var previousFont = Now.defaultFont;
        Now.defaultFont = font;
        var drawList = new NowDrawList();

        try
        {
            void DrawFrame()
            {
                using (drawList.Begin(new Vector2(1920, 1080)))
                {
                    for (int i = 0; i < 100; ++i)
                    {
                        Now.Text(new NowRect(8f, (i * 24) % 1000, 600f, 24f))
                            .SetFontSize(18f)
                            .SetColor(Color.white)
                            .Draw("The quick brown fox jumps over 0123456789");
                    }
                }
            }

            RecordSteadyStateAllocations(DrawFrame, SteadyStateAllocIterations);
        }
        finally
        {
            Now.defaultFont = previousFont;
            drawList.Dispose();
        }
    }
}
