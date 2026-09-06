using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NowUI;
using NowUI.Internal;
using NowUI.CodeEditor;

/// <summary>
/// CPU document frame construction, including the draw-list mesh upload, but no
/// rendering, readback, encoding or disk I/O. Inputs are built before measurement.
/// The surface and draw rect are fixed. The editor owns its scrolling viewport;
/// the original rich-text cases measure whole-document emission, while companion
/// cases apply a viewport mask and scroll translation. Pixel checks are untimed.
/// </summary>
public class NowDocumentPerformanceTests
{
    const int AllocationSamples = 8;
    static readonly Vector2 Surface = new Vector2(800f, 600f);
    static readonly NowRect Viewport = new NowRect(16f, 16f, 760f, 560f);

    public enum DocumentWorkload { Steady, EqualContentReplacement, EditAtStart }

    sealed class IdlePointer : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = default;
            return true;
        }
    }

    sealed class IdleKeyboard : INowTextInputSource
    {
        public bool TryGetFrame(out NowTextInputFrame frame)
        {
            frame = default;
            return true;
        }
    }

    // Delegates the measured work to the real C# tokenizer/validator. Counters
    // explain cache reuse without inspecting editor internals or timing asserts.
    sealed class CountingCSharp : NowCSharpLanguage
    {
        public int tokenizedLines;
        public int validations;
        public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
        {
            ++tokenizedLines;
            return base.TokenizeLine(text, start, length, state, tokens);
        }

        public override void Validate(string text, List<NowCodeDiagnostic> diagnostics)
        {
            ++validations;
            base.Validate(text, diagnostics);
        }
    }

    sealed class DocumentFrame : IDisposable
    {
        public readonly NowDrawList drawList;
        readonly IdlePointer _pointer = new IdlePointer();
        readonly NowThemeAsset _theme;
        readonly NowFontAsset _previousFont;
        readonly INowTextInputSource _previousKeyboard;

        public DocumentFrame()
        {
            _theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");
            var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
            Assert.NotNull(_theme);
            Assert.NotNull(font);
            _previousFont = Now.defaultFont;
            _previousKeyboard = NowTextInput.source;
            ResetState();
            Now.defaultFont = font;
            NowTextInput.source = new IdleKeyboard();
            drawList = new NowDrawList();
        }

        public void Draw(Action drawContent)
        {
            NowTextInput.Invalidate();
            using (NowInput.Begin(_pointer, Surface))
            using (drawList.Begin(Surface))
            using (NowTheme.Scope(_theme))
                drawContent();
        }

        public void Dispose()
        {
            drawList.Dispose();
            ResetState();
            Now.defaultFont = _previousFont;
            NowTextInput.source = _previousKeyboard;
        }
    }

    static void ResetState()
    {
        NowCodeEditor.ResetCaches();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowTextInput.Reset();
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(300, DocumentWorkload.Steady)]
    [TestCase(3000, DocumentWorkload.Steady)]
    [TestCase(10000, DocumentWorkload.Steady)]
    [TestCase(300, DocumentWorkload.EqualContentReplacement)]
    [TestCase(3000, DocumentWorkload.EqualContentReplacement)]
    [TestCase(10000, DocumentWorkload.EqualContentReplacement)]
    [TestCase(300, DocumentWorkload.EditAtStart)]
    [TestCase(3000, DocumentWorkload.EditAtStart)]
    [TestCase(10000, DocumentWorkload.EditAtStart)]
    public void CodeEditorCpuFrame(int lineCount, DocumentWorkload workload)
    {
        string original = BuildCSharpDocument(lineCount);
        string alternative = workload == DocumentWorkload.EditAtStart
            ? original.Replace("BenchmarkA", "BenchmarkB")
            : new string(original.ToCharArray());
        Assert.IsFalse(ReferenceEquals(original, alternative));
        Assert.AreEqual(lineCount, original.Split('\n').Length);
        Assert.AreEqual(workload != DocumentWorkload.EditAtStart, original == alternative);

        using (var fixture = new DocumentFrame())
        {
            var language = new CountingCSharp();
            string text = original;
            NowCodeEditorResult result = default;
            Action content = () => result = NowCode.Editor(Viewport, language, "overview-code").Draw(ref text);
            Action frame = () => fixture.Draw(content);
            frame();
            Assert.GreaterOrEqual(language.tokenizedLines, lineCount, "Initial draw must tokenize the full document.");
            Assert.IsTrue(fixture.drawList.hasGeometry);

            int iteration = 0;
            Action operation = () =>
            {
                if (workload != DocumentWorkload.Steady)
                    text = (++iteration & 1) == 0 ? original : alternative;
                language.tokenizedLines = 0;
                language.validations = 0;
                frame();
            };

            MeasureCpu(operation);
            Assert.IsTrue(result.isValid);
            Assert.IsFalse(result.changed, "Caller replacement should not be reported as keyboard input.");
            Assert.AreEqual(workload == DocumentWorkload.Steady || (iteration & 1) == 0 ? original : alternative, text);
            Assert.IsTrue(fixture.drawList.hasGeometry, "A missing font or skipped editor must not win this benchmark.");
            if (workload == DocumentWorkload.EditAtStart)
            {
                Assert.Greater(language.tokenizedLines, 0, "The changed first line must be processed.");
                Assert.Greater(language.validations, 0, "Changed source must be validated.");
            }

            Counter("Document.Lines", lineCount);
            Counter("Document.Characters", text.Length);
            Counter("Code.TokenizedLines", language.tokenizedLines);
            Counter("Code.Validations", language.validations);
            RecordGeometry(fixture.drawList);
        }
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(32, false)]
    [TestCase(256, false)]
    [TestCase(2048, false)]
    [TestCase(32, true)]
    [TestCase(256, true)]
    [TestCase(2048, true)]
    public void RichTextCpuFrame(int spanCount, bool relayout)
    {
        const string word = "token ";
        var builder = new StringBuilder(spanCount * word.Length);
        var spans = new NowRichTextSpan[spanCount];
        for (int i = 0; i < spanCount; ++i)
        {
            builder.Append(word);
            var style = new NowRichTextStyle(14f).SetColor((i & 1) == 0 ? Color.cyan : Color.yellow);
            spans[i] = new NowRichTextSpan(i * word.Length, word.Length, style);
        }
        string text = builder.ToString();

        using (var fixture = new DocumentFrame())
        {
            NowRichTextResult result = default;
            int iteration = 0;
            float width = Viewport.width;
            Action content = () => result = Now.RichText(new NowRect(16f, 16f, width, 560f), text)
                .SetId("overview-rich-text").SetFontSize(14f).SetSpans(spans).SetWrap().Draw();
            Action operation = () =>
            {
                width = relayout && (++iteration & 1) != 0 ? 380f : 760f;
                fixture.Draw(content);
            };

            // Check both widths outside timing. The draw rect sets placement and
            // wrapping, not an explicit clip; layout retains all spans/positions.
            operation();
            Assert.NotNull(result.layout);
            Assert.AreEqual(text.Length, result.layout.textLength);
            int narrowLines = result.layout.lines.Count;
            operation();
            if (relayout)
                Assert.Greater(narrowLines, result.layout.lines.Count);

            MeasureCpu(operation);
            Assert.IsTrue(fixture.drawList.hasGeometry);
            Assert.AreEqual(text.Length, result.layout.textLength);
            Assert.GreaterOrEqual(result.layout.runs.Count, spanCount);
            Counter("RichText.Spans", spanCount);
            Counter("RichText.Runs", result.layout.runs.Count);
            Counter("RichText.Lines", result.layout.lines.Count);
            Counter("Document.Characters", text.Length);
            RecordGeometry(fixture.drawList);
        }
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(32, false)]
    [TestCase(256, false)]
    [TestCase(2048, false)]
    [TestCase(32, true)]
    [TestCase(256, true)]
    [TestCase(2048, true)]
    public void RichTextClippedCpuFrame(int spanCount, bool scrolling)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Clipped rich-text benchmarks require a graphics device for their untimed pixel parity check.");

        // Keep the original document width and text so the full-emission case
        // remains a useful comparison. A narrower viewport permits horizontal
        // scrolling even for the 32-span document, which fits vertically.
        var clip = new NowRect(16f, 16f, 380f, 280f);
        const string word = "token ";
        var builder = new StringBuilder(spanCount * word.Length);
        var spans = new NowRichTextSpan[spanCount];
        for (int i = 0; i < spanCount; ++i)
        {
            builder.Append(word);
            spans[i] = new NowRichTextSpan(i * word.Length, word.Length,
                new NowRichTextStyle(14f).SetColor((i & 1) == 0 ? Color.cyan : Color.yellow));
        }
        string text = builder.ToString();

        using (var fixture = new DocumentFrame())
        {
            NowRichTextResult result = default;
            Vector2 scroll = default;
            bool clipped = false;
            Action translated = () =>
            {
                using (Now.Transform(1f, -scroll))
                    result = Now.RichText(Viewport, text).SetId("overview-rich-text-clipped")
                        .SetFontSize(14f).SetSpans(spans).SetWrap().Draw();
            };
            Action content = () =>
            {
                if (clipped)
                {
                    // Push the fixed viewport before translating the document.
                    using (Now.Mask(clip))
                        translated();
                }
                else
                    translated();
            };
            fixture.Draw(content);
            var retainedLayout = result.layout;
            Assert.NotNull(retainedLayout);
            int fullVertices = fixture.drawList.mesh.vertexCount;
            Assert.AreEqual(spanCount * 5 * 4, fullVertices, "The reference must emit every non-space glyph.");
            float maxScrollY = Mathf.Max(0f, retainedLayout.bounds.height - clip.height);
            var offsets = new[]
            {
                new Vector2(2.25f, 0f),
                new Vector2(39.25f, Mathf.Min(18.75f, maxScrollY)),
                new Vector2(171.25f, Mathf.Min(141.25f, maxScrollY))
            };
            int iteration = 0;
            clipped = true;
            Action operation = () =>
            {
                scroll = offsets[scrolling ? iteration++ % offsets.Length : 0];
                fixture.Draw(content);
            };

            MeasureCpu(operation);
            Assert.AreSame(retainedLayout, result.layout);
            Assert.AreEqual(text.Length, result.layout.textLength);
            Assert.AreEqual(spanCount, result.layout.runs.Count);
            int eligibleRuns = AssertClippedGeometry(fixture.drawList, result.layout, clip, scroll, fullVertices);
            Counter("RichText.Spans", spanCount);
            Counter("RichText.Runs", result.layout.runs.Count);
            Counter("RichText.Lines", result.layout.lines.Count);
            Counter("RichText.MaskOverlappingRuns", eligibleRuns);
            Counter("RichText.UnclippedVertices", fullVertices);
            Counter("RichText.ViewportPixels", (int)(clip.width * clip.height));
            Counter("RichText.ScrollPositions", scrolling ? offsets.Length : 1);
            Counter("Document.Characters", text.Length);
            RecordGeometry(fixture.drawList);

            // Compare to full geometry rendered without Now.Mask, then crop the
            // reference pixels independently on the CPU. These draws/readbacks,
            // arrays and assertions never enter CPU or allocation measurements.
            var target = new RenderTexture((int)Surface.x, (int)Surface.y, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            using var commands = new CommandBuffer { name = "NowUI clipped rich-text parity" };
            try
            {
                Assert.IsTrue(target.Create());
                int boundaryCrossings = 0;
                int validationPositions = scrolling ? offsets.Length : 1;
                for (int i = 0; i < validationPositions; ++i)
                {
                    scroll = offsets[i];
                    clipped = false;
                    fixture.Draw(content);
                    Assert.AreEqual(fullVertices, fixture.drawList.mesh.vertexCount);
                    Color32[] reference = RenderPixels(fixture.drawList, target, readback, commands);
                    clipped = true;
                    fixture.Draw(content);
                    Assert.AreSame(retainedLayout, result.layout, "Scrolling must preserve the full retained layout.");
                    Assert.AreEqual(text.Length, result.layout.textLength);
                    Assert.AreEqual(spanCount, result.layout.runs.Count);
                    AssertClippedGeometry(fixture.drawList, result.layout, clip, scroll, fullVertices);
                    Color32[] actual = RenderPixels(fixture.drawList, target, readback, commands);
                    boundaryCrossings += AssertCroppedPixels(reference, actual, target.width, target.height, clip);
                }
                Assert.Greater(boundaryCrossings, 0, "Parity must exercise partially clipped glyphs, not only empty clip edges.");
                Counter("Validation.PartialGlyphPixelPairs", boundaryCrossings);
            }
            finally
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }
    }

    static int AssertClippedGeometry(NowDrawList drawList, NowRichTextLayout layout,
        NowRect clip, Vector2 scroll, int fullVertices)
    {
        int eligibleRuns = 0;
        for (int i = 0; i < layout.runs.Count; ++i)
        {
            var rect = layout.runs[i].rect;
            rect.x -= scroll.x;
            rect.y -= scroll.y;
            // The base text renderer conservatively allows eight pixels around
            // ordinary glyph runs; partly visible runs must remain eligible.
            if (rect.Outset(8f).Overlaps(clip))
                ++eligibleRuns;
        }
        Assert.Greater(eligibleRuns, 0);
        Assert.Greater(drawList.mesh.vertexCount, 0);
        Assert.Less(drawList.mesh.vertexCount, fullVertices, "A real viewport must reject some full-document glyph geometry.");
        Assert.LessOrEqual(drawList.mesh.vertexCount, eligibleRuns * 5 * 4,
            "Runs outside the padded clip bounds must not emit glyphs.");
        return eligibleRuns;
    }

    static Color32[] RenderPixels(NowDrawList drawList, RenderTexture target, Texture2D readback, CommandBuffer commands)
    {
        commands.Clear();
        commands.SetRenderTarget(target);
        commands.ClearRenderTarget(false, true, Color.black);
        NowRenderer.Draw(commands, drawList, target, target.width, target.height);
        Graphics.ExecuteCommandBuffer(commands);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
            return readback.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    static int AssertCroppedPixels(Color32[] reference, Color32[] actual, int width, int height, NowRect clip)
    {
        int mismatches = 0;
        int visiblePixels = 0;
        int boundaryCrossings = 0;
        var background = new Color32(0, 0, 0, 255);
        for (int y = 0; y < height; ++y)
        {
            float uiY = height - y - 0.5f;
            bool insideY = uiY >= clip.y && uiY < clip.yMax;
            for (int x = 0; x < width; ++x)
            {
                int index = y * width + x;
                bool inside = insideY && x + 0.5f >= clip.x && x + 0.5f < clip.xMax;
                Color32 expected = inside ? reference[index] : background;
                Color32 observed = actual[index];
                if (Math.Abs(expected.r - observed.r) > 2 || Math.Abs(expected.g - observed.g) > 2 ||
                    Math.Abs(expected.b - observed.b) > 2 || Math.Abs(expected.a - observed.a) > 2)
                    ++mismatches;
                if (inside && IsColored(expected))
                    ++visiblePixels;
            }
            // Adjacent colored reference pixels on both sides of the left edge
            // prove that the crop cuts through glyph coverage on this scanline.
            int edge = y * width + (int)clip.x;
            if (insideY && IsColored(reference[edge - 1]) && IsColored(reference[edge]))
                ++boundaryCrossings;
        }
        Assert.Greater(visiblePixels, 20, "The reference crop must contain readable glyph pixels.");
        Assert.AreEqual(0, mismatches, "Clipped geometry must match full text cropped to the viewport, including partial glyphs.");
        return boundaryCrossings;
    }

    static bool IsColored(Color32 pixel) => pixel.r > 16 || pixel.g > 16 || pixel.b > 16;

    static string BuildCSharpDocument(int lineCount)
    {
        var builder = new StringBuilder(lineCount * 40);
        builder.Append("public class BenchmarkA {\n");
        for (int i = 1; i < lineCount - 1; ++i)
            builder.Append("    public int value").Append(i).Append(" = ").Append(i).Append(";\n");
        builder.Append('}');
        return builder.ToString();
    }

    static void MeasureCpu(Action operation)
    {
        Measure.Method(operation)
            .SampleGroup(new SampleGroup("CPU.FrameBuild", SampleUnit.Millisecond, false))
            .WarmupCount(5).MeasurementCount(64).IterationsPerMeasurement(1).Run();
        using var allocations = new NowBenchmarkAllocations();
        allocations.Begin();
        for (int i = 0; i < AllocationSamples; ++i)
            operation();
        long allocated = allocations.End();
        allocations.Report(allocated / (double)AllocationSamples);
    }

    static void RecordGeometry(NowDrawList drawList)
    {
        Counter("Vertices", drawList.mesh.vertexCount);
        Counter("Batches", drawList.batchCount);
    }

    static void Counter(string name, int value)
    {
        Measure.Custom(new SampleGroup(name, SampleUnit.Undefined, false), value);
    }
}
