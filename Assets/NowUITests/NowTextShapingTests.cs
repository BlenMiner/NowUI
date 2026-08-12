using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;

/// <summary>
/// Shaped text pipeline tests. These need the nowui-msdf v4 plugin (HarfBuzz);
/// when it is unavailable they verify the graceful fallback instead.
/// </summary>
public class NowTextShapingTests
{
    NowFontAsset _fontAsset;
    NowFont _font;

    [OneTimeSetUp]
    public void Resolve()
    {
        _fontAsset = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(_fontAsset, "Default font resource missing.");
        Assert.IsTrue(_fontAsset.TryResolveFont(NowFontStyle.Regular, out _font));
        Assert.NotNull(_font);
    }

    [TearDown]
    public void ResetFlags()
    {
        Now.textShaping = true;
    }

    bool ShapingAvailable()
    {
        return _font.TryGetShapedRun("A", out _);
    }

    [Test]
    public void ShapedRunResolvesForPlainText()
    {
        if (!ShapingAvailable())
            Assert.Ignore("Shaping unavailable on this machine (plugin predates v4).");

        Assert.IsTrue(_font.TryGetShapedRun("AVA fi", out var run));
        Assert.Greater(run.Length, 0);

        float advance = 0f;

        foreach (var glyph in run)
        {
            Assert.Greater((int)glyph.glyphIndex, 0);
            advance += glyph.xAdvance;
        }

        Assert.Greater(advance, 0f);
    }

    [Test]
    public void SpanMeasureMatchesCodepointStringMeasure()
    {
        Now.textShaping = false;

        const string Sample = "Span 1234.56 fps";
        Vector2 byString = _fontAsset.MeasureText(Sample, 32f);
        Vector2 bySpan = _fontAsset.MeasureText(System.MemoryExtensions.AsSpan(Sample), 32f);

        Assert.Greater(byString.x, 0f);
        Assert.AreEqual(byString.x, bySpan.x, 0.001f);
        Assert.AreEqual(byString.y, bySpan.y, 0.001f);
    }

    [Test]
    public void ShapedRunsAreRejectedForMissingGlyphs()
    {
        if (!ShapingAvailable())
            Assert.Ignore("Shaping unavailable on this machine.");

        Assert.IsFalse(_font.TryGetShapedRun("A\uE321B", out _));
    }

    [Test]
    public void ShapedGlyphsBakeAndResolve()
    {
        if (!ShapingAvailable())
            Assert.Ignore("Shaping unavailable on this machine.");

        Assert.IsTrue(_font.TryGetShapedRun("Halo", out var run));
        Assert.IsTrue(_font.EnsureShapedGlyphs(run, 32f), "Shaped glyphs failed to bake.");

        foreach (var shaped in run)
        {
            Assert.IsTrue(
                _font.TryGetShapedGlyph((int)shaped.glyphIndex, 32f, out var record, out Material material),
                $"No record for glyph index {shaped.glyphIndex}.");
            Assert.NotNull(material);
            Assert.Greater(record.atlasBounds.right, record.atlasBounds.left);
        }
    }

    [Test]
    public void ShapedMeasureMatchesRunAdvances()
    {
        if (!ShapingAvailable())
            Assert.Ignore("Shaping unavailable on this machine.");

        const string Text = "Hello AVA";
        const float Size = 24f;

        Assert.IsTrue(_font.TryGetShapedRun(Text, out var run));

        float expected = 0f;

        foreach (var glyph in run)
            expected += glyph.xAdvance * Size;

        Vector2 measured = _fontAsset.MeasureText(Text, Size);
        Assert.AreEqual(expected, measured.x, 0.01f, "Shaped MeasureText must equal the sum of shaped advances.");
    }

    [Test]
    public void ShapingAffectsTypographyOrIsNeutral()
    {
        if (!ShapingAvailable())
            Assert.Ignore("Shaping unavailable on this machine.");

        const string Text = "fi AVAV";
        const float Size = 32f;

        Now.textShaping = true;
        Vector2 shaped = _fontAsset.MeasureText(Text, Size);

        Now.textShaping = false;
        Vector2 unshaped = _fontAsset.MeasureText(Text, Size);

        Assert.Greater(shaped.x, 0f);
        Assert.Greater(unshaped.x, 0f);

        Assert.LessOrEqual(shaped.x, unshaped.x + 0.01f,
            "Kerning/ligatures only ever tighten this sample; equal means the font carries no features for it, which is fine — never wider.");

        Assert.IsTrue(_font.TryGetShapedRun("fi", out var ligatureRun));

        if (ligatureRun.Length < 2)
            Debug.Log("NotoSans applied the fi ligature; shaped glyph count < character count.");
    }

    [Test]
    public void ShapedDrawProducesGeometry()
    {
        AssertDrawsGeometry("Affinity AV fi", shaping: true);
        AssertDrawsGeometry("Affinity AV fi", shaping: false);
        AssertDrawsGeometry("AB\nCD\tE", shaping: true);
        AssertDrawsGeometry("A\uE321B", shaping: true);
    }

    [Test]
    public void MultilineShapedDrawReservesContinuationSegments()
    {
        if (!ShapingAvailable())
            Assert.Ignore("Shaping unavailable on this machine.");

        Now.textShaping = true;
        string firstLine = new string('A', 600);
        string text = firstLine + "\nB";
        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(30000, 128)))
            {
                Now.Text(new NowRect(4, 4, 29900, 96), _fontAsset)
                    .SetFontSize(32)
                    .SetColor(Color.white)
                    .Draw(text);
            }

            Assert.IsTrue(drawList.hasGeometry);
        }
        finally
        {
            drawList.Dispose();
            Now.textShaping = true;
        }
    }

    void AssertDrawsGeometry(string text, bool shaping)
    {
        Now.textShaping = shaping;

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(512, 128)))
            {
                Now.Text(new NowRect(4, 4, 500, 60), _fontAsset)
                    .SetFontSize(32)
                    .SetColor(Color.white)
                    .Draw(text);
            }

            Assert.IsTrue(drawList.hasGeometry, $"No geometry for '{text}' (shaping={shaping}).");
        }
        finally
        {
            drawList.Dispose();
            Now.textShaping = true;
        }
    }
}
