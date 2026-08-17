using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;
using UnityEngine;

public class NowTextStylingTests
{
    const float FontSize = 40f;

    static readonly Vector2 Surface = new Vector2(512f, 192f);

    static readonly NowRect TextRect = new NowRect(16f, 16f, 480f, 128f);

    static readonly NowRect GradientRect = new NowRect(20f, 20f, 200f, 80f);

    static readonly string[] AtomicShapingSamples =
    {
        "a\u0338\u0307B",
        "q\u0323\u0307Z",
        "x\u0301\u0327Y",
        "fiX",
        "ffiX"
    };

    NowFontAsset _fontAsset;

    NowFont _font;

    NowDrawList _drawList;

    [OneTimeSetUp]
    public void ResolveFont()
    {
        _fontAsset = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(_fontAsset, "Default NotoSans font resource is missing.");
        Assert.IsTrue(_fontAsset.TryResolveFont(NowFontStyle.Regular, out _font));
        Assert.NotNull(_font);
    }

    [SetUp]
    public void SetUp()
    {
        NowGradientRampCache.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        Now.textShaping = true;
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        _drawList = null;
        NowGradientRampCache.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        Now.textShaping = true;
    }

    [Test]
    public void GradientTextDrawsRemainTextAndBatchTogether()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Text(new NowRect(16f, 16f, 220f, 64f), _fontAsset)
                .SetFontSize(32f)
                .SetGradient(Color.red, Color.blue)
                .SetGradientLinear(NowGradientDirection.ToRight)
                .Draw("AB");

            Now.Text(new NowRect(260f, 16f, 220f, 64f), _fontAsset)
                .SetFontSize(32f)
                .SetGradient(Color.green, Color.yellow)
                .SetGradientRadial(NowGradientShape.Circle)
                .Draw("CD");
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(1, _drawList.batchCount,
            "Gradient choice and mapping must not split a shared text-material batch.");
        Assert.AreEqual(1, _drawList.mesh.subMeshCount);
        Assert.AreEqual(NowMeshKind.Text, _drawList.batches[0].kind);
    }

    [Test]
    public void DirectTextGradientUsesTextVertexStreams()
    {
        using (_drawList.Begin(Surface))
        {
            DrawRadialGradientText();
        }

        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(NowMeshKind.Text, _drawList.batches[0].kind);

        var payload = new List<Vector4>();
        var extras = new List<Vector4>();
        _drawList.mesh.GetUVs(2, payload);
        _drawList.mesh.GetUVs(5, extras);

        Assert.Greater(_drawList.mesh.vertexCount, 0);
        Assert.AreEqual(_drawList.mesh.vertexCount, payload.Count);
        Assert.AreEqual(_drawList.mesh.vertexCount, extras.Count);
        AssertRadialPayload(payload, extras);
    }

    [Test]
    public void CanvasTextGradientUsesNormalAndUv3Streams()
    {
        using var canvasList = new NowDrawList(NowMeshLayout.Canvas, "Now Text Styling Canvas Test");

        using (canvasList.Begin(Surface))
        {
            DrawRadialGradientText();
        }

        Assert.AreEqual(1, canvasList.batchCount);
        Assert.AreEqual(NowMeshKind.Text, canvasList.batches[0].kind);

        var normals = new List<Vector3>();
        var extras = new List<Vector4>();
        canvasList.mesh.GetNormals(normals);
        canvasList.mesh.GetUVs(3, extras);

        Assert.Greater(canvasList.mesh.vertexCount, 0);
        Assert.AreEqual(canvasList.mesh.vertexCount, normals.Count);
        Assert.AreEqual(canvasList.mesh.vertexCount, extras.Count);

        for (int i = 0; i < normals.Count; ++i)
        {
            Assert.AreEqual(70f, normals[i].x, 0.0001f, "radial center x belongs in NORMAL.x");
            Assert.AreEqual(80f, normals[i].y, 0.0001f, "radial center y belongs in NORMAL.y");
            Assert.AreEqual(80f, normals[i].z, 0.0001f, "radial radius x belongs in NORMAL.z");
            Assert.AreEqual(48f, extras[i].z, 0.0001f, "radial radius y belongs in UV3.z");
            Assert.Greater(extras[i].w, 0f, "the encoded ramp belongs in UV3.w");
        }
    }

    [Test]
    public void CanvasTextColorPackingCanMatchLinearGradientRamps()
    {
        var authoredFill = new Color(0.5f, 0.25f, 0.75f, 0.6f);
        var authoredOutline = new Vector4(0.2f, 0.4f, 0.8f, 0.7f);
        Color convertedFill = NowMesh.TextCanvasColorToWorkingSpace(authoredFill, ColorSpace.Linear);
        Vector4 convertedOutline = NowMesh.TextCanvasColorToWorkingSpace(authoredOutline, ColorSpace.Linear);

        Assert.AreEqual(Mathf.GammaToLinearSpace(authoredFill.r), convertedFill.r, 0.000001f);
        Assert.AreEqual(Mathf.GammaToLinearSpace(authoredFill.g), convertedFill.g, 0.000001f);
        Assert.AreEqual(Mathf.GammaToLinearSpace(authoredFill.b), convertedFill.b, 0.000001f);
        Assert.AreEqual(authoredFill.a, convertedFill.a, 0.000001f);
        Assert.AreEqual(Mathf.GammaToLinearSpace(authoredOutline.x), convertedOutline.x, 0.000001f);
        Assert.AreEqual(Mathf.GammaToLinearSpace(authoredOutline.y), convertedOutline.y, 0.000001f);
        Assert.AreEqual(Mathf.GammaToLinearSpace(authoredOutline.z), convertedOutline.z, 0.000001f);
        Assert.AreEqual(authoredOutline.w, convertedOutline.w, 0.000001f);
        Assert.AreEqual(authoredFill,
            NowMesh.TextCanvasColorToWorkingSpace(authoredFill, ColorSpace.Gamma));
        Assert.AreEqual(authoredOutline,
            NowMesh.TextCanvasColorToWorkingSpace(authoredOutline, ColorSpace.Gamma));
    }

    [Test]
    public void TypewriterAtZeroEmitsNothingWithoutChangingMeasure()
    {
        const string Text = "Hello";
        Vector2 expectedMeasure = Now.Text(TextRect, _fontAsset)
            .SetFontSize(FontSize)
            .Measure(Text);
        Vector2 animatedMeasure = Now.Text(TextRect, _fontAsset)
            .SetFontSize(FontSize)
            .SetAnimation(NowTextAnimations.Typewriter(10f))
            .SetTime(0f)
            .Measure(Text);

        AssertVector2Equal(expectedMeasure, animatedMeasure, 0.001f);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f))
                .SetTime(0f)
                .Draw(Text);
        }

        Assert.AreEqual(0, _drawList.mesh.vertexCount);
        Assert.AreEqual(0, _drawList.batchCount);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f))
                .SetTime(0.11f)
                .Draw(Text);
        }

        Assert.IsTrue(_drawList.hasGeometry, "a later caller-owned sample time should reveal text");
        Assert.AreEqual(NowMeshKind.Text, _drawList.batches[0].kind);
    }

    [Test]
    public void SpanNumericCharacterAndAtlasGlyphPathsHonorFirstClassStyling()
    {
        var animation = NowTextAnimations.Typewriter(10f);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetGradient(Color.red, Color.blue)
                .SetAnimation(animation)
                .SetTime(0.11f)
                .Draw("AB".AsSpan());
        }

        Assert.AreEqual(4, _drawList.mesh.vertexCount,
            "the span path should reveal one grapheme at the first boundary");
        Assert.Greater(ReadFirstVector4Stream(5).w, 0f);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(animation)
                .SetTime(0.11f)
                .Draw(42);
        }

        Assert.AreEqual(4, _drawList.mesh.vertexCount,
            "formatted numeric draws should share the span animation path");

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(animation)
                .SetTime(0f)
                .Draw('A');
        }

        Assert.AreEqual(0, _drawList.mesh.vertexCount);

        Assert.IsTrue(_fontAsset.TryResolveGlyph(
            'A', FontSize, NowFontStyle.Regular, out _, out var glyph, out _));

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetGradient(Color.cyan, Color.magenta)
                .SetAnimation(animation)
                .SetTime(0.11f)
                .Draw(glyph);
        }

        Assert.AreEqual(4, _drawList.mesh.vertexCount);
        Assert.Greater(ReadFirstVector4Stream(5).w, 0f,
            "atlas-glyph draws should carry the same gradient payload");
    }

    [Test]
    public void FadeUpChangesGlyphRectAndAlphaWithoutChangingMeasure()
    {
        const string Text = "A";
        Vector2 expectedMeasure = Now.Text(TextRect, _fontAsset)
            .SetFontSize(FontSize)
            .Measure(Text);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .Draw(Text);
        }

        Vector4 settledRect = ReadFirstVector4Stream(1);
        float settledAlpha = ReadFirstVector4Stream(3).w;
        var animation = NowTextAnimations.FadeUp(20f, 1f, 0f)
            .SetEasing(NowTextAnimationEasing.Linear);
        Vector2 animatedMeasure = Now.Text(TextRect, _fontAsset)
            .SetFontSize(FontSize)
            .SetAnimation(animation)
            .SetTime(0.5f)
            .Measure(Text);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(animation)
                .SetTime(0.5f)
                .Draw(Text);
        }

        Vector4 movingRect = ReadFirstVector4Stream(1);
        float movingAlpha = ReadFirstVector4Stream(3).w;

        AssertVector2Equal(expectedMeasure, animatedMeasure, 0.001f);
        Assert.AreEqual(settledRect.x, movingRect.x, 0.001f);
        Assert.AreEqual(settledRect.z, movingRect.z, 0.001f);
        Assert.AreEqual(settledRect.w, movingRect.w, 0.001f);
        Assert.Greater(Mathf.Abs(settledRect.y - movingRect.y), 9f,
            "the half-complete 20-unit entrance should retain a 10-unit offset");
        Assert.AreEqual(settledAlpha * 0.5f, movingAlpha, 0.001f);
    }

    [Test]
    public void ScaleInResizesGlyphAroundItsVisualCenter()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .Draw("A");
        }

        Vector4 settled = ReadFirstVector4Stream(1);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.ScaleIn(0.5f, 1f, 0f))
                .SetTime(0f)
                .Draw("A");
        }

        Vector4 scaled = ReadFirstVector4Stream(1);
        Assert.AreEqual(settled.z * 0.5f, scaled.z, 0.001f);
        Assert.AreEqual(settled.w * 0.5f, scaled.w, 0.001f);
        Assert.AreEqual(settled.x + settled.z * 0.5f, scaled.x + scaled.z * 0.5f, 0.001f);
        Assert.AreEqual(settled.y + settled.w * 0.5f, scaled.y + scaled.w * 0.5f, 0.001f);
    }

    [Test]
    public void WaveChangesGlyphPositionAtCallerOwnedTime()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .Draw("A");
        }

        Vector4 settled = ReadFirstVector4Stream(1);

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Wave(10f, 6f, 1f))
                .SetTime(0.25f)
                .Draw("A");
        }

        Vector4 waved = ReadFirstVector4Stream(1);
        Assert.AreEqual(10f, settled.y - waved.y, 0.001f);
    }

    [Test]
    public void MirroredTransformPreservesSignedAnimationMotion()
    {
        var scale = new Vector2(1f, -1f);
        var origin = new Vector2(0f, 160f);

        using (_drawList.Begin(Surface))
        using (Now.Transform(scale, origin))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetMask(new NowRect(-256f, -256f, 1024f, 1024f))
                .Draw("A");
        }

        Vector4 settled = ReadFirstVector4Stream(1);

        using (_drawList.Begin(Surface))
        using (Now.Transform(scale, origin))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetMask(new NowRect(-256f, -256f, 1024f, 1024f))
                .SetAnimation(NowTextAnimations.FadeUp(20f, 1f, 0f)
                    .SetEasing(NowTextAnimationEasing.Linear))
                .SetTime(0.5f)
                .Draw("A");
        }

        Vector4 moving = ReadFirstVector4Stream(1);
        Assert.AreEqual(10f, moving.y - settled.y, 0.001f,
            "negative-y transforms must mirror the local FadeUp offset");
    }

    [Test]
    public void ShapedLigatureOrCombiningClusterRevealsAtomicallyWhenAvailable()
    {
        if (!_font.TryGetShapedRun("A", out _))
            Assert.Ignore("Shaping unavailable on this machine.");

        if (!TryFindAtomicShapedSample(
                out string sample,
                out int firstUnitVisibleGlyphs,
                out int nextVisibleUnit))
        {
            Assert.Ignore("NotoSans exposed no deterministic multi-glyph combining cluster or ligature for this backend.");
        }

        const float Rate = 10f;
        int firstRevealVertices = DrawTypewriterAt(sample, 1.01f / Rate, Rate);
        int beforeNextUnitVertices = DrawTypewriterAt(sample, (nextVisibleUnit + 0.99f) / Rate, Rate);
        int afterNextUnitVertices = DrawTypewriterAt(sample, (nextVisibleUnit + 1.01f) / Rate, Rate);

        Assert.AreEqual(firstUnitVisibleGlyphs * 4, firstRevealVertices,
            "every visible glyph in the first shaped cluster must appear in the same sample");
        Assert.AreEqual(firstRevealVertices, beforeNextUnitVertices,
            "a shaped cluster must not reveal incrementally between logical units");
        Assert.Greater(afterNextUnitVertices, beforeNextUnitVertices,
            "the next shaped unit should still reveal on its own boundary");
    }

    [Test]
    public void ShapedLigatureUsesExactNormalizedTimelineAndCompletionWhenAvailable()
    {
        const string Text = "fiX";

        if (!_font.TryGetPreparedShapedRun(Text, FontSize, out var run) || run.animationUnitCount != 2)
            Assert.Ignore("The active NotoSans shaping backend did not form the fi ligature.");

        int firstUnitVisibleGlyphs = 0;

        for (int i = 0; i < run.length; ++i)
        {
            if (run.glyphs[i].visible && run.glyphs[i].animationUnit == 0)
                ++firstUnitVisibleGlyphs;
        }

        Assert.Greater(firstUnitVisibleGlyphs, 0);
        Assert.AreEqual(2, Now.GetTextAnimationUnitCount(
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f)),
            Text));

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f))
                .SetNormalizedTime(0.8f)
                .Draw(Text);
        }

        Assert.AreEqual(firstUnitVisibleGlyphs * 4, _drawList.mesh.vertexCount,
            "normalized time must use two shaped clusters, not three source code points");

        NowControlState.BeginRepaintTracking();

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f))
                .SetTime(0.21f)
                .Draw(Text);
        }

        Assert.IsFalse(NowControlState.EndRepaintTracking(),
            "retained repaint requests must stop at the exact shaped-cluster completion time");
    }

    [Test]
    public void StaticUnanimatedTextStillEmitsTextGeometry()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .Draw("Static text");
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.Greater(_drawList.mesh.vertexCount, 0);
        Assert.AreEqual(NowMeshKind.Text, _drawList.batches[0].kind);
    }

    [Test]
    public void UnshapedGraphemeSequencesStopAtLayoutControls()
    {
        Assert.AreEqual(2, NowTextUnitCursor.Count("\ud83d\udc69\u200d\n\ud83d\ude80".AsSpan()),
            "a ZWJ before a line break must not join the first glyph on the next line");
        Assert.AreEqual(2, NowTextUnitCursor.Count("\ud83c\uddeb\t\ud83c\uddf7".AsSpan()),
            "regional indicators separated by a tab must not become one flag unit");
        Assert.AreEqual(2, NowTextUnitCursor.Count("\u0301A".AsSpan()),
            "a leading combining mark forms its own unit instead of absorbing the following base");
        Assert.AreEqual(2, NowTextUnitCursor.Count("A\n\u0301".AsSpan()),
            "a combining mark after a line break starts a new unit");
        Assert.AreEqual(2, NowTextUnitCursor.Count("A\rB".AsSpan()),
            "a carriage return is a zero-unit sequence boundary");
    }

    [Test]
    public void CarriageReturnHasZeroAdvanceAcrossStringAndSpanMeasurement()
    {
        var text = Now.Text(TextRect, _fontAsset).SetFontSize(FontSize);
        Vector2 shapedA = text.Measure("A");
        Vector2 shapedB = text.Measure("B");
        Vector2 shaped = text.Measure("A\rB");

        Assert.AreEqual(shapedA.x + shapedB.x, shaped.x, 0.001f);
        Assert.AreEqual(shapedA.y, shaped.y, 0.001f);

        ReadOnlySpan<char> spanA = "A".AsSpan();
        ReadOnlySpan<char> spanB = "B".AsSpan();
        ReadOnlySpan<char> span = "A\rB".AsSpan();
        Vector2 measuredA = text.Measure(spanA);
        Vector2 measuredB = text.Measure(spanB);
        Vector2 measured = text.Measure(span);

        Assert.AreEqual(measuredA.x + measuredB.x, measured.x, 0.001f);
        Assert.AreEqual(measuredA.y, measured.y, 0.001f);
    }

    [Test]
    public void NormalizedTimeOneCompletesFiniteAnimation()
    {
        const string Text = "Done";

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .Draw(Text);
        }

        int settledVertexCount = _drawList.mesh.vertexCount;

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.FadeIn(0.5f, 0.2f))
                .SetNormalizedTime(1f)
                .Draw(Text);
        }

        var colors = new List<Vector4>();
        _drawList.mesh.GetUVs(3, colors);

        Assert.AreEqual(settledVertexCount, _drawList.mesh.vertexCount);
        Assert.Greater(colors.Count, 0);

        for (int i = 0; i < colors.Count; ++i)
            Assert.AreEqual(1f, colors[i].w, 0.0001f, "every staggered unit should be settled at normalized time 1");
    }

    [Test]
    public void WrappedTextCarriesAnimationSequenceAcrossWordRuns()
    {
        const string Text = "A B";
        var runs = new List<NowTextRun>();
        var style = new NowText(default, _fontAsset)
            .SetFontSize(FontSize)
            .SetAnimation(NowTextAnimations.Typewriter(10f))
            .SetTime(0.11f);
        NowTextWrap.Layout(style, Text, 400f, runs);

        using (_drawList.Begin(Surface))
            NowTextWrap.Draw(style, Text, runs, new Vector2(16f, 16f));

        Assert.AreEqual(4, _drawList.mesh.vertexCount,
            "the second word must not restart its typewriter sequence at unit zero");

        style = style.SetTime(0.21f);

        using (_drawList.Begin(Surface))
            NowTextWrap.Draw(style, Text, runs, new Vector2(16f, 16f));

        Assert.AreEqual(8, _drawList.mesh.vertexCount,
            "the second word should reveal at the next global sequence boundary");
    }

    [Test]
    public void WrappedShapedLigatureDoesNotInsertADeadSequenceSlot()
    {
        const string Text = "fi X";
        var runs = new List<NowTextRun>();
        var style = new NowText(default, _fontAsset)
            .SetFontSize(FontSize)
            .SetAnimation(NowTextAnimations.Typewriter(10f))
            .SetTime(0.21f);
        NowTextWrap.Layout(style, Text, 400f, runs);

        if (runs.Count != 2 || Now.GetTextAnimationUnitCount(style, "fi") != 1)
            Assert.Ignore("The active NotoSans shaping backend did not form the fi ligature.");

        using (_drawList.Begin(Surface))
            NowTextWrap.Draw(style, Text, runs, new Vector2(16f, 16f));

        int animatedVertices = _drawList.mesh.vertexCount;
        style = style.ClearAnimation();

        using (_drawList.Begin(Surface))
            NowTextWrap.Draw(style, Text, runs, new Vector2(16f, 16f));

        Assert.AreEqual(_drawList.mesh.vertexCount, animatedVertices,
            "the second word should reveal immediately after the ligature cluster");
    }

    [Test]
    public void AnimatedGradientTextIsAllocationFreeAfterWarmup()
    {
        const string Text = "Animated gradient fi 012345";
        var animation = NowTextAnimations.FadeUp(8f, 0.4f, 0.015f);

        void DrawFrame()
        {
            using (_drawList.Begin(Surface))
            {
                Now.Text(TextRect, _fontAsset)
                    .SetFontSize(28f)
                    .SetGradient(Color.cyan, Color.magenta)
                    .SetGradientLinear(90f)
                    .SetAnimation(animation)
                    .SetTime(0.2f)
                    .Draw(Text);
            }
        }

        DrawFrame();
        DrawFrame();
        DrawFrame();

        long before;

        try
        {
            before = GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return;
        }

        DrawFrame();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0, allocated, "warmed gradient animation must not allocate per frame");
    }

    [Test]
    public void CallerTimedAnimationsRequestOnlyNeededRetainedRepaints()
    {
        NowControlState.BeginRepaintTracking();

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.FadeIn(1f, 0.1f))
                .SetTime(0.25f)
                .Draw("Live");
        }

        Assert.IsTrue(NowControlState.EndRepaintTracking(),
            "an unfinished caller-timed entrance should request its next retained frame");

        NowControlState.BeginRepaintTracking();

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.FadeIn(1f, 0.1f))
                .SetTime(2f)
                .Draw("Live");
        }

        Assert.IsFalse(NowControlState.EndRepaintTracking(),
            "a settled finite animation must stop requesting retained frames");

        NowControlState.BeginRepaintTracking();

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Wave())
                .SetTime(2f)
                .Draw("Live");
        }

        Assert.IsTrue(NowControlState.EndRepaintTracking(),
            "a live wave should keep requesting retained frames");

        NowControlState.BeginRepaintTracking();

        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.FadeIn(1f, 0.1f))
                .SetNormalizedTime(0.5f)
                .Draw("Live");
        }

        Assert.IsFalse(NowControlState.EndRepaintTracking(),
            "externally scrubbed normalized time must not create its own repaint loop");
    }

    [Test]
    public void RichTextCarriesAnimationSequenceAcrossStyledRuns()
    {
        const string Markup = "<color=#ff0000>A</color>B";

        using (_drawList.Begin(Surface))
        {
            Now.RichText(TextRect, Markup)
                .ParseDefaultTags()
                .SetFont(_fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f))
                .SetTime(0.11f)
                .Draw();
        }

        Assert.AreEqual(4, _drawList.mesh.vertexCount,
            "the second styled run must not restart its typewriter sequence at unit zero");

        using (_drawList.Begin(Surface))
        {
            Now.RichText(TextRect, Markup)
                .ParseDefaultTags()
                .SetFont(_fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(10f))
                .SetTime(0.21f)
                .Draw();
        }

        Assert.AreEqual(8, _drawList.mesh.vertexCount,
            "the next styled run should reveal at the next document-wide sequence boundary");
    }

    [Test]
    public void LayoutRichTextReservesSpanAwareIntrinsicWidth()
    {
        NowRichTextResult result;

        using (_drawList.Begin(Surface))
        using (NowLayout.Area(new Vector4(0f, 0f, 480f, 192f)))
        {
            result = NowLayout.RichText("AA <size=80>AA</size>")
                .ParseDefaultTags()
                .SetFont(_fontAsset)
                .SetFontSize(FontSize)
                .Draw();
        }

        Assert.AreEqual(1, result.layout.lines.Count,
            "span-styled text must reserve its full flowed width instead of wrapping early");
        Assert.GreaterOrEqual(result.rect.width, result.layout.bounds.width - 0.001f,
            "the reserved rect must fit the span-aware flowed width");
    }

    [Test]
    public void LayoutRichTextSetAlignCentersInLayoutCell()
    {
        NowRichTextResult result;

        using (_drawList.Begin(Surface))
        using (NowLayout.Area(new Vector4(0f, 0f, 480f, 192f)))
        {
            result = NowLayout.RichText("AA")
                .SetFont(_fontAsset)
                .SetFontSize(FontSize)
                .SetAlign(NowLayoutAlign.Center)
                .Draw();
        }

        Assert.Greater(result.rect.x, 0f,
            "centered rich text should be inset from the area's left edge");
        Assert.AreEqual(240f, result.rect.center.x, 1f,
            "SetAlign(Center) must center the reserved rect in the layout cell");
    }

    [Test]
    public void RichTextGeneratedMaskIncludesBoundedAnimationMotion()
    {
        using (_drawList.Begin(Surface))
        {
            Now.RichText(TextRect, "A")
                .SetFont(_fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.FadeUp(20f, 1f, 0f))
                .SetTime(0.5f)
                .Draw();
        }

        var masks = new List<Vector4>();
        _drawList.mesh.GetUVs(6, masks);
        Assert.Greater(masks.Count, 0);
        Assert.LessOrEqual(masks[0].x, TextRect.x - 24f + 0.001f);
        Assert.LessOrEqual(masks[0].y, TextRect.y - 24f + 0.001f);
        Assert.GreaterOrEqual(masks[0].x + masks[0].z, TextRect.xMax + 24f - 0.001f);
        Assert.GreaterOrEqual(masks[0].y + masks[0].w, TextRect.yMax + 24f - 0.001f);
    }

    void DrawRadialGradientText()
    {
        Now.Text(GradientRect, _fontAsset)
            .SetFontSize(FontSize)
            .SetGradient(Color.red, Color.blue)
            .SetGradientRadial(new Vector2(0.25f, 0.75f), new Vector2(0.4f, 0.6f))
            .Draw("A");
    }

    static void AssertRadialPayload(List<Vector4> payload, List<Vector4> extras)
    {
        for (int i = 0; i < payload.Count; ++i)
        {
            Assert.AreEqual(70f, payload[i].x, 0.0001f, "radial center x belongs in UV2.x");
            Assert.AreEqual(80f, payload[i].y, 0.0001f, "radial center y belongs in UV2.y");
            Assert.AreEqual(80f, payload[i].z, 0.0001f, "radial radius x belongs in UV2.z");
            Assert.AreEqual(48f, extras[i].z, 0.0001f, "radial radius y belongs in UV5.z");
            Assert.Greater(extras[i].w, 0f, "the encoded ramp belongs in UV5.w");
        }
    }

    Vector4 ReadFirstVector4Stream(int channel)
    {
        var values = new List<Vector4>();
        _drawList.mesh.GetUVs(channel, values);
        Assert.Greater(values.Count, 0, $"UV{channel} contained no text vertices.");
        return values[0];
    }

    bool TryFindAtomicShapedSample(
        out string sample,
        out int firstUnitVisibleGlyphs,
        out int nextVisibleUnit)
    {
        for (int s = 0; s < AtomicShapingSamples.Length; ++s)
        {
            string candidate = AtomicShapingSamples[s];

            if (!_font.TryGetPreparedShapedRun(candidate, FontSize, out var run))
                continue;

            int firstVisible = 0;
            int nextUnit = int.MaxValue;

            for (int i = 0; i < run.length; ++i)
            {
                var glyph = run.glyphs[i];

                if (!glyph.visible)
                    continue;

                if (glyph.animationUnit == 0)
                    ++firstVisible;
                else if (glyph.animationUnit > 0)
                    nextUnit = Mathf.Min(nextUnit, glyph.animationUnit);
            }

            bool multiGlyphCombiningCluster = firstVisible > 1;
            bool ligatureSpansLogicalUnits = firstVisible > 0 &&
                nextUnit == 1 &&
                run.animationUnitCount < NowTextUnitCursor.Count(candidate.AsSpan());

            if ((!multiGlyphCombiningCluster && !ligatureSpansLogicalUnits) || nextUnit == int.MaxValue)
                continue;

            sample = candidate;
            firstUnitVisibleGlyphs = firstVisible;
            nextVisibleUnit = nextUnit;
            return true;
        }

        sample = null;
        firstUnitVisibleGlyphs = 0;
        nextVisibleUnit = 0;
        return false;
    }

    int DrawTypewriterAt(string text, float time, float rate)
    {
        using (_drawList.Begin(Surface))
        {
            Now.Text(TextRect, _fontAsset)
                .SetFontSize(FontSize)
                .SetAnimation(NowTextAnimations.Typewriter(rate))
                .SetTime(time)
                .Draw(text);
        }

        return _drawList.mesh.vertexCount;
    }

    static void AssertVector2Equal(Vector2 expected, Vector2 actual, float tolerance)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance);
        Assert.AreEqual(expected.y, actual.y, tolerance);
    }
}
