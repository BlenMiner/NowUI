using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowTextLayoutTests
{
    static readonly Vector2 Surface = new Vector2(640f, 480f);

    sealed class RecordingAnimator : INowTextGlyphAnimator
    {
        public int lastCount;
        public float rotation;
        public Vector2 offset;
        public float alpha = 1f;

        public NowTextGlyphState Evaluate(int unitIndex, int unitCount, float time)
        {
            lastCount = unitCount;
            return new NowTextGlyphState(offset, 1f, alpha, rotation);
        }
    }

    NowDrawList _drawList;
    NowDrawList _reference;

    [SetUp]
    public void SetUp()
    {
        _drawList = new NowDrawList();
        _reference = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        _reference.Dispose();
    }

    static NowText Text(NowRect rect, float size = 20f)
    {
        return Now.Text(rect).SetFontSize(size).SetColor(Color.white);
    }

    static List<Vector2> UiVertices(NowDrawList drawList)
    {
        var positions = new List<Vector3>();
        drawList.mesh.GetVertices(positions);
        var result = new List<Vector2>();
        foreach (var vertex in positions)
            result.Add(new Vector2(vertex.x, -vertex.y));
        return result;
    }

    static NowRect VertexBounds(NowDrawList drawList)
    {
        var vertices = UiVertices(drawList);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

        foreach (var v in vertices)
        {
            minX = Mathf.Min(minX, v.x);
            minY = Mathf.Min(minY, v.y);
            maxX = Mathf.Max(maxX, v.x);
            maxY = Mathf.Max(maxY, v.y);
        }

        return new NowRect(minX, minY, maxX - minX, maxY - minY);
    }

    [Test]
    public void HorizontalAlignmentPlacesTheLineInsideTheRect()
    {
        var rect = new NowRect(40f, 20f, 300f, 60f);
        var rects = new NowRect[16];

        using (_drawList.Begin(Surface))
        {
            float width = Text(rect).Measure("Align").x;

            Text(rect).GetUnitRects("Align", rects);
            Assert.AreEqual(rect.x, rects[0].x, 0.01f);

            Text(rect).SetAlign(NowTextAlign.Center).GetUnitRects("Align", rects);
            Assert.AreEqual(rect.x + (rect.width - width) * 0.5f, rects[0].x, 0.01f);

            Text(rect).SetAlign(NowTextAlign.Right).GetUnitRects("Align", rects);
            Assert.AreEqual(rect.xMax - width, rects[0].x, 0.01f);
        }
    }

    [Test]
    public void DrawnCenteredTextMatchesItsUnitRects()
    {
        var rect = new NowRect(40f, 20f, 300f, 60f);
        var rects = new NowRect[8];

        using (_drawList.Begin(Surface))
        {
            var text = Text(rect, 32f).SetAlign(NowTextAlign.Center, NowTextVerticalAlign.Middle);
            int count = text.GetUnitRects("MOVE", rects);
            text.Draw("MOVE");
            Assert.AreEqual(4, count);
        }

        var bounds = VertexBounds(_drawList);
        float unitsCenter = (rects[0].x + rects[3].xMax) * 0.5f;
        Assert.AreEqual(unitsCenter, bounds.center.x, 3f, "Glyph ink should be centered where the unit boxes are.");
        Assert.AreEqual(rect.center.x, unitsCenter, 0.5f);
    }

    [Test]
    public void VerticalAlignmentPlacesTheBlock()
    {
        var rect = new NowRect(0f, 100f, 300f, 200f);
        var rects = new NowRect[4];

        using (_drawList.Begin(Surface))
        {
            float lineHeight = Text(rect).Measure("A").y;

            Text(rect).SetVerticalAlign(NowTextVerticalAlign.Middle).GetUnitRects("A", rects);
            Assert.AreEqual(rect.y + (rect.height - lineHeight) * 0.5f, rects[0].y, 0.01f);

            Text(rect).SetVerticalAlign(NowTextVerticalAlign.Bottom).GetUnitRects("A", rects);
            Assert.AreEqual(rect.yMax - lineHeight, rects[0].y, 0.01f);

            Text(rect).SetVerticalAlign(NowTextVerticalAlign.Bottom).GetUnitRects("A\nB", rects);
            Assert.AreEqual(rect.yMax - lineHeight * 2f, rects[0].y, 0.01f);
            Assert.AreEqual(rect.yMax - lineHeight, rects[1].y, 0.01f);
        }
    }

    [Test]
    public void CapMiddleCentersCapitalsOptically()
    {
        var rect = new NowRect(0f, 100f, 300f, 60f);
        var rects = new NowRect[4];
        const float size = 30f;

        using (_drawList.Begin(Surface))
        {
            var text = Text(rect, size).SetVerticalAlign(NowTextVerticalAlign.CapMiddle);
            text.GetUnitRects("H", rects);
            var metrics = text.font.GetMetrics(text.fontStyle).Scale(size);
            float baseline = rects[0].y + metrics.ascender;
            float capTop = baseline - metrics.capHeight;
            Assert.AreEqual(rect.center.y, (capTop + baseline) * 0.5f, 0.01f);
        }
    }

    [Test]
    public void MultilineCenterAlignsEachLineSeparately()
    {
        var rect = new NowRect(0f, 0f, 400f, 200f);
        var rects = new NowRect[16];

        using (_drawList.Begin(Surface))
        {
            var text = Text(rect).SetAlign(NowTextAlign.Center);
            int count = text.GetUnitRects("Wide line\nab", rects);
            Assert.AreEqual(11, count);

            float wide = Text(rect).Measure("Wide line").x;
            float narrow = Text(rect).Measure("ab").x;
            Assert.AreEqual((rect.width - wide) * 0.5f, rects[0].x, 0.01f);
            Assert.AreEqual((rect.width - narrow) * 0.5f, rects[9].x, 0.01f);
            Assert.Greater(rects[9].y, rects[0].y);
        }
    }

    [Test]
    public void LetterSpacingWidensMeasureAndUnitsButNotTheLastUnit()
    {
        var rect = new NowRect(0f, 0f, 400f, 60f);
        var plain = new NowRect[8];
        var spaced = new NowRect[8];
        const float size = 20f;
        const float em = 0.5f;

        using (_drawList.Begin(Surface))
        {
            var basic = Text(rect, size);
            var tracked = Text(rect, size).SetLetterSpacing(em);

            Assert.AreEqual(basic.Measure("ABCD").x + em * size * 3f, tracked.Measure("ABCD").x, 0.01f);

            basic.GetUnitRects("ABCD", plain);
            tracked.GetUnitRects("ABCD", spaced);

            for (int i = 0; i < 4; i++)
                Assert.AreEqual(plain[i].x + em * size * i, spaced[i].x, 0.01f);

            Assert.AreEqual(size * em, Text(rect, size).SetLetterSpacingPixels(size * em).letterSpacing * size, 0.0001f);
        }
    }

    [Test]
    public void LetterSpacingMovesDrawnGlyphs()
    {
        var rect = new NowRect(10f, 10f, 400f, 60f);

        using (_reference.Begin(Surface))
            Text(rect, 30f).Draw("AB");

        using (_drawList.Begin(Surface))
            Text(rect, 30f).SetLetterSpacingPixels(12f).Draw("AB");

        var reference = UiVertices(_reference);
        var actual = UiVertices(_drawList);
        Assert.AreEqual(8, actual.Count);

        for (int i = 0; i < 4; i++)
            Assert.AreEqual(reference[i].x, actual[i].x, 0.01f, "The first unit stays in place.");

        for (int i = 4; i < 8; i++)
            Assert.AreEqual(reference[i].x + 12f, actual[i].x, 0.01f, "The second unit moves by one spacing.");
    }

    [Test]
    public void UnitRectsReportTheTotalEvenWhenTheBufferIsShort()
    {
        var rects = new NowRect[2];

        using (_drawList.Begin(Surface))
        {
            int count = Text(new NowRect(0f, 0f, 400f, 40f)).GetUnitRects("Hello", rects);
            Assert.AreEqual(5, count);
            Assert.Greater(rects[1].x, rects[0].x);
        }
    }

    [Test]
    public void CustomAnimatorMovesFadesAndRotatesGlyphs()
    {
        var rect = new NowRect(40f, 40f, 400f, 80f);
        var animator = new RecordingAnimator { offset = new Vector2(10f, 5f), alpha = 0.5f };

        using (_reference.Begin(Surface))
            Text(rect, 40f).Draw("HI");

        using (_drawList.Begin(Surface))
            Text(rect, 40f).SetAnimation(NowTextAnimations.Custom(animator, 20f)).SetTime(0f).Draw("HI");

        Assert.AreEqual(2, animator.lastCount);
        var reference = UiVertices(_reference);
        var moved = UiVertices(_drawList);

        for (int i = 0; i < reference.Count; i++)
            Assert.AreEqual(0f, (reference[i] + new Vector2(10f, 5f) - moved[i]).magnitude, 0.01f);

        var colors = new List<Vector4>();
        _drawList.mesh.GetUVs(3, colors);
        Assert.AreEqual(0.5f, colors[0].w, 0.001f);

        animator.rotation = 90f;
        animator.offset = Vector2.zero;
        animator.alpha = 1f;

        using (_drawList.Begin(Surface))
            Text(rect, 40f).SetAnimation(NowTextAnimations.Custom(animator)).SetTime(0f).Draw("I");

        using (_reference.Begin(Surface))
            Text(rect, 40f).Draw("I");

        var upright = VertexBounds(_reference);
        var turned = VertexBounds(_drawList);
        Assert.AreEqual(upright.width, turned.height, 0.05f, "A quarter turn swaps the glyph quad's extents.");
        Assert.AreEqual(upright.height, turned.width, 0.05f);
        Assert.AreEqual(upright.center.x, turned.center.x, 0.05f, "Glyphs turn around their own center.");
    }

    [Test]
    public void FiniteCustomAnimationStopsRepaintingButKeepsItsFinalState()
    {
        var animator = new RecordingAnimator { offset = new Vector2(0f, 7f) };
        var animation = NowTextAnimations.Custom(animator, 10f, 0.5f);

        Assert.IsFalse(animation.IsComplete(0.25f, 3));
        Assert.IsTrue(animation.IsComplete(0.5f, 3));
        Assert.IsTrue(float.IsPositiveInfinity(NowTextAnimations.Custom(animator).CompletionTime(3)));

        var rect = new NowRect(40f, 40f, 400f, 80f);

        using (_reference.Begin(Surface))
            Text(rect, 40f).Draw("A");

        using (_drawList.Begin(Surface))
            Text(rect, 40f).SetAnimation(animation).SetTime(5f).Draw("A");

        var reference = UiVertices(_reference);
        var held = UiVertices(_drawList);
        Assert.AreEqual(reference[0].y + 7f, held[0].y, 0.01f, "A settled custom animation keeps its final offset.");
    }

    [Test]
    public void SetClipFalseKeepsGlyphsOutsideTheRect()
    {
        var tiny = new NowRect(20f, 20f, 10f, 10f);

        using (_reference.Begin(Surface))
            Text(tiny, 40f).Draw("Overflow");

        using (_drawList.Begin(Surface))
            Text(tiny, 40f).SetClip(false).Draw("Overflow");

        var unclipped = new NowDrawList();

        try
        {
            using (unclipped.Begin(Surface))
                Text(new NowRect(20f, 20f, 600f, 80f), 40f).Draw("Overflow");

            Assert.Greater(_drawList.mesh.vertexCount, _reference.mesh.vertexCount);
            Assert.AreEqual(unclipped.mesh.vertexCount, _drawList.mesh.vertexCount, "Every glyph is emitted, as with a rect large enough.");
        }
        finally
        {
            unclipped.Dispose();
        }
    }

    [Test]
    public void FontMetricsReportPlausibleCapAndXHeights()
    {
        var metrics = Now.defaultFont.GetMetrics();
        Assert.Greater(metrics.capHeight, 0.55f);
        Assert.Less(metrics.capHeight, metrics.ascender);
        Assert.Greater(metrics.xHeight, 0.35f);
        Assert.Less(metrics.xHeight, metrics.capHeight);
        Assert.Greater(metrics.descender, 0f);
        Assert.AreEqual(metrics.capHeight * 10f, metrics.Scale(10f).capHeight, 0.0001f);
    }

    [Test]
    public void WrappedTextIgnoresBlockAlignmentAndSpacing()
    {
        var rect = new NowRect(0f, 0f, 120f, 200f);

        const string words = "wrap these words please";
        var runs = new List<NowTextRun>();
        var plainStyle = Text(rect);
        var styled = Text(rect).SetAlign(NowTextAlign.Right).SetLetterSpacing(1f);

        using (_reference.Begin(Surface))
        {
            NowTextWrap.Layout(plainStyle, words, rect.width, runs);
            NowTextWrap.Draw(plainStyle, words, runs, rect.position);
        }

        using (_drawList.Begin(Surface))
        {
            NowTextWrap.Layout(styled, words, rect.width, runs);
            NowTextWrap.Draw(styled, words, runs, rect.position);
        }

        var reference = UiVertices(_reference);
        var actual = UiVertices(_drawList);
        Assert.AreEqual(reference.Count, actual.Count);

        for (int i = 0; i < reference.Count; i++)
            Assert.AreEqual(reference[i], actual[i]);
    }

    [Test]
    public void AlignedSpacedTextIsAllocationFreeAfterWarmup()
    {
        var rect = new NowRect(0f, 0f, 400f, 120f);

        void Frame()
        {
            using (_drawList.Begin(Surface))
            {
                Text(rect, 24f)
                    .SetAlign(NowTextAlign.Center, NowTextVerticalAlign.CapMiddle)
                    .SetLetterSpacing(0.2f)
                    .Draw("Tracked\nand centered");
            }
        }

        Frame();
        Frame();
        Frame();

        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        Frame();
        long allocated = allocations.End();
        allocations.AssertZero(allocated, "aligned, letter-spaced multi-line text must not allocate after warmup");
    }
}
