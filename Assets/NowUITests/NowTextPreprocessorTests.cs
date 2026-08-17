using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NowUI;
using NowUI.Internal;

public class NowTextPreprocessorTests
{
    NowFontAsset _font;
    NowDrawList _drawList;
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
        _drawList = new NowDrawList();
        Now.defaultFont = _font;
    }

    [TearDown]
    public void TearDown()
    {
        Now.ClearTextPreprocessor();
        _drawList.Dispose();
        Now.defaultFont = null;
    }

    NowText Style(float size = 16f)
    {
        return new NowText(default, _font).SetFontSize(size);
    }

    [Test]
    public void WithoutPreprocessorReturnsSameInstance()
    {
        const string Text = "unchanged";
        Assert.AreSame(Text, Now.PreprocessText(Text));
        Assert.IsNull(Now.PreprocessText(null));
        Assert.AreEqual(string.Empty, Now.PreprocessText(string.Empty));
    }

    [Test]
    public void PreprocessorAppliesAndMemoizesStableInstances()
    {
        int calls = 0;
        Now.SetTextPreprocessor(value => { ++calls; return value + "!"; });

        string first = Now.PreprocessText("hello");
        string second = Now.PreprocessText("hello");

        Assert.AreEqual("hello!", first);
        Assert.AreSame(first, second, "repeat lookups must return the memoized instance");
        Assert.AreEqual(1, calls, "the hook must run once per unique string");
    }

    [Test]
    public void InvalidateReRunsThePreprocessor()
    {
        string suffix = "-en";
        Now.SetTextPreprocessor(value => value + suffix);

        Assert.AreEqual("word-en", Now.PreprocessText("word"));

        suffix = "-de";
        Assert.AreEqual("word-en", Now.PreprocessText("word"), "memo holds until invalidated");

        Now.InvalidateTextPreprocessor();
        Assert.AreEqual("word-de", Now.PreprocessText("word"));
    }

    [Test]
    public void NullResultFallsBackToSource()
    {
        Now.SetTextPreprocessor(value => null);
        Assert.AreEqual("keep", Now.PreprocessText("keep"));
    }

    [Test]
    public void ThrowingPreprocessorLogsAndFallsBackToSource()
    {
        Now.SetTextPreprocessor(value => throw new System.InvalidOperationException("boom"));
        LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
        Assert.AreEqual("keep", Now.PreprocessText("keep"));
    }

    [Test]
    public void MeasureSizesThePreprocessedText()
    {
        Vector2 shortSize = Style().Measure("ab");
        Now.SetTextPreprocessor(value => value == "ab" ? "abababab" : value);

        Vector2 measured = Style().Measure("ab");
        Vector2 expected = Style().Measure("abababab");

        Assert.Greater(measured.x, shortSize.x, "measure must size the transformed text");
        Assert.AreEqual(expected.x, measured.x, 0.01f);
    }

    [Test]
    public void SetRawBypassesThePreprocessor()
    {
        Vector2 verbatim = Style().Measure("ab");
        Now.SetTextPreprocessor(value => value + value + value);

        Assert.AreEqual(verbatim.x, Style().SetRaw().Measure("ab").x, 0.01f);
        Assert.Greater(Style().Measure("ab").x, verbatim.x);
    }

    [Test]
    public void WrapLaysOutAndDrawsThePreprocessedText()
    {
        Now.SetTextPreprocessor(value => value == "one two" ? "uno dos tres" : value);
        var runs = new List<NowTextRun>();

        NowTextWrap.Layout(Style(), "one two", 10000f, runs);
        Assert.AreEqual(3, runs.Count, "layout must segment the transformed text");

        using (_drawList.Begin(Surface))
            NowTextWrap.Draw(Style(), "one two", runs, new Vector2(5f, 5f));

        Assert.IsTrue(_drawList.hasGeometry, "wrapped preprocessed text drew no geometry");
        Assert.AreEqual("uno", runs[0].text);
        Assert.AreEqual("dos", runs[1].text);
        Assert.AreEqual("tres", runs[2].text);
    }

    [Test]
    public void WrapWithRawStyleKeepsTheSourceText()
    {
        Now.SetTextPreprocessor(value => value == "one two" ? "uno dos tres" : value);
        var runs = new List<NowTextRun>();

        NowTextWrap.Layout(Style().SetRaw(), "one two", 10000f, runs);
        Assert.AreEqual(2, runs.Count);
    }

    [Test]
    public void DrawEmitsGeometryThroughThePreprocessor()
    {
        Now.SetTextPreprocessor(value => value == "src" ? "translated" : value);
        var style = Style();
        style.rect = new NowRect(5f, 5f, 400f, 60f);
        style.mask = style.rect;

        using (_drawList.Begin(Surface))
            style.Draw("src");

        Assert.IsTrue(_drawList.hasGeometry, "preprocessed draw produced no geometry");
    }
}
