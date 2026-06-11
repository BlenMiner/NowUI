using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Internal;

public class NowManagedFontCompilerTests
{
    const string FontAssetPath = "Assets/NowUI/Assets/Fonts/NotoSans/NotoSans-Regular.ttf.asset";

    const int Size = 64;
    const int PixelRange = 16;
    const int AtlasSide = 512;

    byte[] _fontBytes;

    [OneTimeSetUp]
    public void LoadFontBytes()
    {
        var font = AssetDatabase.LoadAssetAtPath<NowFont>(FontAssetPath);
        Assert.NotNull(font, $"Test font asset not found at {FontAssetPath}");
        Assert.IsTrue(font.TryGetSourceBytes(out _fontBytes), "Test font asset has no embedded source bytes.");
    }

    [TearDown]
    public void ResetFlags()
    {
        NowFontCompiler.forceManagedCompiler = false;
        NowFontCompiler.forceNativeCompiler = false;
    }

    [Test]
    public void DefaultSessionPrefersManagedCompiler()
    {
        Assert.IsTrue(NowFontCompiler.DynamicSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var session, out string error), error);
        Assert.IsTrue(session.isManaged, "TrueType fonts should bake through the managed compiler by default.");
        session.Dispose();
    }

    static int[] Codepoints(string text)
    {
        var codepoints = new List<int>();

        for (int i = 0; i < text.Length; ++i)
            codepoints.Add(NowFont.ReadCodepoint(text, ref i));

        return codepoints.ToArray();
    }

    [Test]
    public void ParserReadsCoreTables()
    {
        Assert.IsTrue(NowTrueType.TryParse(_fontBytes, out var font, out string error), error);
        Assert.Greater(font.unitsPerEm, 0);
        Assert.Greater(font.glyphCount, 0);
        Assert.Greater(font.ascender, 0);
        Assert.Less(font.descender, 0);

        Assert.IsTrue(font.TryGetGlyphIndex('A', out int glyphIndex));
        Assert.Greater(font.GetAdvanceWidth(glyphIndex), 0);

        var outline = new NowGlyphOutline();
        Assert.IsTrue(font.TryGetOutline(glyphIndex, outline));
        Assert.IsFalse(outline.isEmpty);
        Assert.Greater(outline.contourEnds.Count, 0);
    }

    [Test]
    public void ManagedSessionBakesVisibleGlyphs()
    {
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var session, out string error), error);

        var results = new List<NowFontAtlasInfo.Glyph>();
        int[] codepoints = Codepoints("Helo");

        var status = session.TryAddGlyphs(codepoints, codepoints.Length, results, out error);

        Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, status, error);
        Assert.AreEqual(codepoints.Length, results.Count);

        foreach (var glyph in results)
        {
            Assert.Greater(glyph.advance, 0f);
            Assert.Greater(glyph.atlasBounds.right, glyph.atlasBounds.left);
            Assert.Greater(glyph.atlasBounds.top, glyph.atlasBounds.bottom);
            Assert.GreaterOrEqual(glyph.atlasBounds.left, 0f);
            Assert.LessOrEqual(glyph.atlasBounds.right, AtlasSide);
            Assert.Greater(glyph.planeBounds.right, glyph.planeBounds.left);
            Assert.Greater(glyph.planeBounds.top, glyph.planeBounds.bottom);

            // The padded cell must be larger than the distance range it carries.
            Assert.GreaterOrEqual(glyph.atlasBounds.right - glyph.atlasBounds.left, PixelRange);
        }

        byte[] atlas = null;
        Assert.IsTrue(session.TryCopyAtlas(ref atlas, out error), error);
        Assert.AreEqual(AtlasSide * AtlasSide * 4, atlas.Length);

        // Inside-glyph pixels encode above the 0.5 edge threshold (127).
        bool hasInk = false;

        for (int i = 0; i < atlas.Length && !hasInk; i += 4)
            hasInk = atlas[i] > 140;

        Assert.IsTrue(hasInk, "Baked atlas contains no inside-glyph pixels.");
    }

    [Test]
    public void WhitespaceBakesAdvanceOnly()
    {
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var session, out string error), error);

        var results = new List<NowFontAtlasInfo.Glyph>();
        int[] codepoints = { ' ' };

        var status = session.TryAddGlyphs(codepoints, 1, results, out error);

        Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, status, error);
        Assert.AreEqual(1, results.Count);
        Assert.Greater(results[0].advance, 0f);
        Assert.AreEqual(results[0].atlasBounds.left, results[0].atlasBounds.right);
    }

    [Test]
    public void DuplicateAddsReturnTheCachedGlyph()
    {
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var session, out string error), error);

        var first = new List<NowFontAtlasInfo.Glyph>();
        var second = new List<NowFontAtlasInfo.Glyph>();
        int[] codepoints = { 'A' };

        session.TryAddGlyphs(codepoints, 1, first, out _);
        session.TryAddGlyphs(codepoints, 1, second, out _);

        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual(first[0].atlasBounds.left, second[0].atlasBounds.left);
        Assert.AreEqual(first[0].atlasBounds.bottom, second[0].atlasBounds.bottom);
    }

    [Test]
    public void FullAtlasReportsAtlasFullWithoutMutating()
    {
        // A 32px page cannot fit a 64px-em glyph cell.
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, 32, out var session, out string error), error);

        var results = new List<NowFontAtlasInfo.Glyph>();
        int[] codepoints = { 'A' };

        var status = session.TryAddGlyphs(codepoints, 1, results, out _);

        Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.AtlasFull, status);
        Assert.AreEqual(0, results.Count);
    }

    [Test]
    public void MissingCodepointsAreSkippedNotFailed()
    {
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var session, out string error), error);

        var results = new List<NowFontAtlasInfo.Glyph>();
        int[] codepoints = { 0xE321 }; // private use area, not mapped in NotoSans

        var status = session.TryAddGlyphs(codepoints, 1, results, out error);

        Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, status, error);
        Assert.AreEqual(0, results.Count);
    }

    [Test]
    public void ManagedMatchesNativeAdvancesAndMetrics()
    {
        NowFontCompiler.forceNativeCompiler = true;
        bool nativeCreated = NowFontCompiler.DynamicSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var native, out _);
        NowFontCompiler.forceNativeCompiler = false;

        if (!nativeCreated || native.isManaged)
        {
            native?.Dispose();
            Assert.Ignore("Native font compiler not available on this platform; comparison skipped.");
        }

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.DynamicSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var managed, out string managedError), managedError);
        Assert.IsTrue(managed.isManaged);

        int[] codepoints = Codepoints("AgMW2x ");
        var nativeGlyphs = new List<NowFontAtlasInfo.Glyph>();
        var managedGlyphs = new List<NowFontAtlasInfo.Glyph>();

        Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, native.TryAddGlyphs(codepoints, codepoints.Length, nativeGlyphs, out _));
        Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, managed.TryAddGlyphs(codepoints, codepoints.Length, managedGlyphs, out _));

        Assert.AreEqual(nativeGlyphs.Count, managedGlyphs.Count, "Native and managed compilers resolved different glyph sets.");

        for (int i = 0; i < nativeGlyphs.Count; ++i)
        {
            Assert.AreEqual(nativeGlyphs[i].unicode, managedGlyphs[i].unicode);
            Assert.AreEqual(nativeGlyphs[i].advance, managedGlyphs[i].advance, 0.01f,
                $"Advance mismatch for '{(char)nativeGlyphs[i].unicode}'");
        }

        Assert.AreEqual(native.Metrics.ascender, managed.Metrics.ascender, 0.02f);
        Assert.AreEqual(native.Metrics.descender, managed.Metrics.descender, 0.02f);
        Assert.AreEqual(native.Metrics.lineHeight, managed.Metrics.lineHeight, 0.05f);

        native.Dispose();
        managed.Dispose();
    }

    [Test]
    public void BakeByGlyphIndexMatchesCodepointBake()
    {
        Assert.IsTrue(NowTrueType.TryParse(_fontBytes, out var parsed, out string parseError), parseError);
        Assert.IsTrue(parsed.TryGetGlyphIndex('A', out int glyphIndex));

        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var byCodepoint, out string error), error);
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, Size, PixelRange, AtlasSide, out var byIndex, out error), error);

        var codepointResults = new List<NowFontAtlasInfo.Glyph>();
        var indexResults = new List<NowFontAtlasInfo.Glyph>();

        Assert.AreEqual(
            NowFontCompiler.DynamicSession.AddResult.Ok,
            byCodepoint.TryAddGlyphs(new[] { (int)'A' }, 1, codepointResults, out _));
        Assert.AreEqual(
            NowFontCompiler.DynamicSession.AddResult.Ok,
            byIndex.TryAddGlyphsByIndex(new[] { glyphIndex }, 1, indexResults, out _));

        Assert.AreEqual(1, codepointResults.Count);
        Assert.AreEqual(1, indexResults.Count);
        Assert.AreEqual(glyphIndex, indexResults[0].unicode, "Index-baked records carry the glyph index as their key.");
        Assert.AreEqual(codepointResults[0].advance, indexResults[0].advance, 0.0001f);
        Assert.AreEqual(codepointResults[0].planeBounds.left, indexResults[0].planeBounds.left, 0.0001f);
        Assert.AreEqual(codepointResults[0].planeBounds.top, indexResults[0].planeBounds.top, 0.0001f);
    }

    [Test]
    public void ShaperShapesTextOrReportsUnavailable()
    {
        if (!NowTextShaper.TryCreate(_fontBytes, out var shaper, out string error))
        {
            Assert.IsFalse(string.IsNullOrEmpty(error), "Shaper creation failed without an error message.");
            Assert.Ignore($"Native shaping API unavailable on this machine: {error}");
        }

        try
        {
            var glyphs = new List<NowTextShaper.ShapedGlyph>();
            Assert.IsTrue(shaper.TryShape("AVA fi", glyphs, out error), error);
            Assert.Greater(glyphs.Count, 0);

            float totalAdvance = 0f;

            foreach (var glyph in glyphs)
            {
                Assert.Greater((int)glyph.glyphIndex, 0, "Shaped output contains .notdef glyphs for ASCII input.");
                totalAdvance += glyph.xAdvance;
            }

            Assert.Greater(totalAdvance, 0f);

            // Kerned pairs (AV) shape to different total advance than raw advances
            // would only with GPOS active; at minimum clusters must be monotonic.
            for (int i = 1; i < glyphs.Count; ++i)
                Assert.GreaterOrEqual(glyphs[i].cluster, glyphs[i - 1].cluster);
        }
        finally
        {
            shaper.Dispose();
        }
    }

    [Test, Performance]
    public void NativeSessionBakesAsciiBaseline()
    {
        MeasureSessionBake(forceManaged: false);
    }

    [Test, Performance]
    public void ManagedSessionBakesAsciiBaseline()
    {
        MeasureSessionBake(forceManaged: true);
    }

    /// <summary>
    /// Bakes the printable ASCII set through a fresh session per iteration so the
    /// two backends can be compared directly in the performance report.
    /// </summary>
    void MeasureSessionBake(bool forceManaged)
    {
        var codepoints = new int[95];

        for (int i = 0; i < codepoints.Length; ++i)
            codepoints[i] = 32 + i;

        var results = new List<NowFontAtlasInfo.Glyph>(codepoints.Length);

        NowFontCompiler.forceManagedCompiler = forceManaged;
        NowFontCompiler.forceNativeCompiler = !forceManaged;

        try
        {
            Measure.Method(() =>
                {
                    Assert.IsTrue(NowFontCompiler.DynamicSession.TryCreate(
                        _fontBytes, Size, PixelRange, 1024, out var session, out string error), error);

                    Assert.AreEqual(forceManaged, session.isManaged);

                    results.Clear();
                    var status = session.TryAddGlyphs(codepoints, codepoints.Length, results, out string addError);

                    Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, status, addError);
                    session.Dispose();
                })
                .WarmupCount(3)
                .MeasurementCount(15)
                .Run();
        }
        finally
        {
            NowFontCompiler.forceManagedCompiler = false;
            NowFontCompiler.forceNativeCompiler = false;
        }
    }

    [Test]
    public void ForceManagedCompilesEndToEnd()
    {
        NowFontCompiler.forceManagedCompiler = true;

        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            font.EnsureGlyphs("Managed!", 32f, NowFontStyle.Regular);

            Assert.IsTrue(
                font.TryResolveGlyph('M', 32f, NowFontStyle.Regular, out _, out var glyph, out Material material),
                "Managed compiler failed to resolve a baked glyph.");
            Assert.Greater(glyph.advance, 0f);
            Assert.NotNull(material);
            Assert.NotNull(material.mainTexture);
        }
        finally
        {
            Object.DestroyImmediate(font);
        }
    }
}
