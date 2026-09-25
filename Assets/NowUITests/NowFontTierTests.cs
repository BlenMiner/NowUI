using NUnit.Framework;
using NowUI;
using UnityEngine;

/// <summary>
/// Resolution tiers: ordinary text keeps a font's base glyph cell, and only large
/// display text bakes a larger cell, with the field range scaled alongside it.
/// </summary>
public class NowFontTierTests
{
    static NowFont BoldFace()
    {
        Assert.IsTrue(Now.defaultFont.TryResolveFont(NowFontStyle.Bold, out var font) && font != null);
        return font;
    }

    [Test]
    public void OrdinaryTextKeepsTheBaseCell()
    {
        var font = BoldFace();
        int baseSize = font.dynamicAtlasSize;

        foreach (float size in new[] { 8f, 12f, 16f, 24f, 32f, 48f, baseSize * NowFont.DYNAMIC_TIER_MAX_MAGNIFICATION })
            Assert.AreEqual(baseSize, font.GetDynamicGlyphSize(size), $"{size} px text must not bake a larger cell.");
    }

    [Test]
    public void DisplayTextDoublesTheCellUpToTheCap()
    {
        var font = BoldFace();
        int baseSize = font.dynamicAtlasSize;
        float threshold = baseSize * NowFont.DYNAMIC_TIER_MAX_MAGNIFICATION;

        Assert.AreEqual(Mathf.Min(baseSize * 2, NowFont.MAX_DYNAMIC_TIER_GLYPH_SIZE), font.GetDynamicGlyphSize(threshold + 1f));
        Assert.AreEqual(NowFont.MAX_DYNAMIC_TIER_GLYPH_SIZE, font.GetDynamicGlyphSize(1000f));
        Assert.AreEqual(baseSize, font.GetDynamicGlyphSize(float.NaN));
        Assert.AreEqual(baseSize, font.GetDynamicGlyphSize(float.PositiveInfinity));

        int previous = 0;
        for (float size = 4f; size < 600f; size += 7f)
        {
            int cell = font.GetDynamicGlyphSize(size);
            Assert.GreaterOrEqual(cell, previous, "Tiers never shrink as text grows.");
            Assert.LessOrEqual(size, Mathf.Max(cell * NowFont.DYNAMIC_TIER_MAX_MAGNIFICATION, NowFont.MAX_DYNAMIC_TIER_GLYPH_SIZE * 100f));
            previous = cell;
        }
    }

    [Test]
    public void FieldRangeScalesWithTheCell()
    {
        var font = BoldFace();
        float baseRatio = (float)font.dynamicPixelRange / font.dynamicAtlasSize;

        foreach (float size in new[] { 16f, 60f, 150f, 400f })
        {
            int cell = font.GetDynamicGlyphSize(size);
            int range = font.GetDynamicPixelRange(0f, size);
            Assert.AreEqual(baseRatio, (float)range / cell, 0.0001f, $"{size} px keeps the same field reach in em.");
        }

        Assert.GreaterOrEqual(font.GetDynamicPixelRange(0.1f, 150f), font.GetDynamicPixelRange(0f, 150f));
    }

    [Test]
    public void RenderScaleSelectsTheTierAndNeverLowersIt()
    {
        var font = BoldFace();
        const float authored = 40f;
        Assert.Greater(font.GetDynamicGlyphSize(authored * 2f), font.GetDynamicGlyphSize(authored),
            "The fixture must cross a tier when doubled.");

        int authoredRange = font.GetDynamicPixelRange(0f, authored);
        int renderedRange = font.GetDynamicPixelRange(0f, authored * 2f);
        Assert.IsTrue(font.GetGlyph('N', authored, 0f, out _, out var authoredMaterial));
        Assert.IsTrue(font.GetGlyph('N', authored * 2f, 0f, out _, out var renderedMaterial));
        Assert.AreNotSame(authoredMaterial, renderedMaterial, "Each tier bakes into its own page.");

        using (NowFont.PushRenderScale(2f))
        {
            Assert.AreEqual(renderedRange, font.GetDynamicPixelRange(0f, authored));
            Assert.IsTrue(font.GetGlyph('N', authored, 0f, out _, out var material));
            Assert.AreSame(renderedMaterial, material, "Doubled text resolves in the doubled size's tier.");
            Assert.AreEqual(authored * font.dynamicPixelRange / font.dynamicAtlasSize,
                font.GetScreenPixelRange('N', authored), 0.01f,
                "Screen range stays in authored units; the transform scales it at draw time.");
        }

        Assert.AreEqual(authoredRange, font.GetDynamicPixelRange(0f, authored), "The scope restores the authored tier.");

        foreach (float scale in new[] { 0.25f, 1f, float.NaN, float.PositiveInfinity, -3f })
        {
            using (NowFont.PushRenderScale(scale))
            {
                Assert.AreEqual(authoredRange, font.GetDynamicPixelRange(0f, authored),
                    $"Render scale {scale} keeps the authored tier.");
            }
        }
    }

    [Test]
    public void ScreenPixelRangeIsIndependentOfTheTier()
    {
        var font = BoldFace();

        foreach (float size in new[] { 20f, 72f, 150f })
        {
            Assert.IsTrue(font.GetGlyph('N', size, 0f, out _, out _), $"'N' bakes at {size} px.");
            float expected = size * font.dynamicPixelRange / font.dynamicAtlasSize;
            Assert.AreEqual(expected, font.GetScreenPixelRange('N', size), expected * 0.01f,
                $"Glyph edges keep the same anti-aliasing at {size} px.");
        }
    }
}
