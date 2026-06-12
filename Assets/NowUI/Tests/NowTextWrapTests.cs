using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;

public class NowTextWrapTests
{
    NowFontAsset _font;
    NowUIDrawList _drawList;
    static readonly Vector2 Surface = new Vector2(512, 512);

    [OneTimeSetUp]
    public void LoadFont()
    {
        _font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(_font, "Default font resource missing.");
    }

    [SetUp]
    public void SetUp()
    {
        _drawList = new NowUIDrawList();
        Now.defaultFont = _font;
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        Now.defaultFont = null;
    }

    NowUIText Style(float size = 16f)
    {
        return new NowUIText(default, _font).SetFontSize(size);
    }

    [Test]
    public void WideWidthKeepsOneLineAndNarrowWidthWraps()
    {
        var runs = new List<NowTextRun>();
        const string Text = "several words that should wrap";

        Vector2 wide = NowTextWrap.Layout(Style(), Text, 10000f, runs);

        foreach (var run in runs)
            Assert.AreEqual(0f, run.y, 0.001f);

        Vector2 narrow = NowTextWrap.Layout(Style(), Text, 90f, runs);
        Assert.Greater(narrow.y, wide.y, "narrow layout must use more lines");
        Assert.LessOrEqual(narrow.x, 90f + 0.5f, "no run may start a line beyond the width");
    }

    [Test]
    public void RunsReferenceSourceRangesAndNewlinesForceBreaks()
    {
        var runs = new List<NowTextRun>();
        const string Text = "alpha  beta\ngamma";

        NowTextWrap.Layout(Style(), Text, 10000f, runs);

        Assert.AreEqual(3, runs.Count);
        Assert.AreEqual("alpha", Text.Substring(runs[0].start, runs[0].length));
        Assert.AreEqual("beta", Text.Substring(runs[1].start, runs[1].length));
        Assert.AreEqual("gamma", Text.Substring(runs[2].start, runs[2].length));
        Assert.AreEqual(runs[0].y, runs[1].y, 0.001f, "collapsed whitespace stays on one line");
        Assert.Greater(runs[2].y, runs[1].y, "newline forces a break");
    }

    [Test]
    public void OverlongWordOverflowsInsteadOfSplitting()
    {
        var runs = new List<NowTextRun>();
        NowTextWrap.Layout(Style(), "supercalifragilistic", 10f, runs);

        Assert.AreEqual(1, runs.Count);
        Assert.AreEqual(0f, runs[0].x, 0.001f);
    }

    [Test]
    public void RangeMeasureMatchesSubstringMeasure()
    {
        const string Text = "measure this range";
        Vector2 byRange = _font.MeasureText(Text, 8, 4, 20f);
        Vector2 bySubstring = _font.MeasureText(Text.Substring(8, 4), 20f, NowFontStyle.Regular);

        Assert.Greater(byRange.x, 0f);
        Assert.AreEqual(bySubstring.x, byRange.x, 0.01f);
    }

    [Test]
    public void DrawEmitsGeometryAndMaterializesRunsOnce()
    {
        var runs = new List<NowTextRun>();
        const string Text = "draw me wrapped";
        NowTextWrap.Layout(Style(), Text, 60f, runs);

        using (_drawList.Begin(Surface))
            NowTextWrap.Draw(Style(), Text, runs, new Vector2(5f, 5f));

        Assert.IsTrue(_drawList.hasGeometry, "wrapped text drew no geometry");

        foreach (var run in runs)
            Assert.NotNull(run.text, "Draw must materialize run strings for reuse");
    }

    [Test]
    public void ResolveTextCarriesAmbientFontAndNoMask()
    {
        var resolved = NowControls.theme.ResolveText();

        Assert.AreEqual(_font, resolved.font);
        Assert.IsTrue(resolved.mask.isEmpty, "resolved styles start unmasked");
        Assert.Greater(resolved.fontSize, 0f);
    }
}
