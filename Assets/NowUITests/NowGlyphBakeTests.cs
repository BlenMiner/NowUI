using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;
using UnityEngine;
#if NOWUI_STANDALONE
using NowUI.Engine;
#else
using UnityEngine.Rendering;
#endif

/// <summary>
/// The dynamic glyph bake path: the tile-culled SDF field must equal a brute-force
/// field, managed sessions bake straight into their page's CPU copy, and pages upload
/// only the rows a bake wrote.
/// </summary>
public class NowGlyphBakeTests
{
    const string Ascii = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    bool _previousForceManaged;
    bool _previousForceNative;
    byte[] _fontBytes;

    [SetUp]
    public void SetUp()
    {
        _previousForceManaged = NowFontCompiler.forceManagedCompiler;
        _previousForceNative = NowFontCompiler.forceNativeCompiler;
        NowFontCompiler.forceManagedCompiler = true;
        NowFontCompiler.forceNativeCompiler = false;
        Assert.IsTrue(Now.defaultFont.TryResolveFont(NowFontStyle.Regular, out var source) && source != null);
        Assert.IsTrue(source.TryGetSourceBytes(out _fontBytes));
    }

    [TearDown]
    public void TearDown()
    {
        NowFontCompiler.forceManagedCompiler = _previousForceManaged;
        NowFontCompiler.forceNativeCompiler = _previousForceNative;
    }

    NowFont CompileFont()
    {
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        font.dynamicMaxGlyphSize = 128;
        return font;
    }

    static void Release(NowFont font)
    {
        font.ClearDynamicCache();
        Object.DestroyImmediate(font);
    }

    static int Packed16(byte[] atlas, int offset) => (atlas[offset] << 8) | atlas[offset + 2];

    /// <summary>Brute-force reference: every segment for every pixel, as the job did before culling.</summary>
    static int ReferenceValue(List<Vector4> segments, int start, int count, float px, float py, float range)
    {
        float minDistSq = float.MaxValue;
        int winding = 0;
        var p = new Vector2(px, py);

        for (int s = start; s < start + count; ++s)
        {
            var a = new Vector2(segments[s].x, segments[s].y);
            var b = new Vector2(segments[s].z, segments[s].w);
            Vector2 e = b - a;
            Vector2 w = p - a;
            float lengthSq = Vector2.Dot(e, e);
            float t = lengthSq > 1e-12f ? Mathf.Clamp01(Vector2.Dot(w, e) / lengthSq) : 0f;
            Vector2 d = w - e * t;
            minDistSq = Mathf.Min(minDistSq, Vector2.Dot(d, d));

            if (a.y <= py ? b.y > py : b.y <= py)
            {
                float ix = a.x + (py - a.y) / (b.y - a.y) * e.x;

                if (ix > px)
                    winding += b.y > a.y ? 1 : -1;
            }
        }

        float sd = Mathf.Sqrt(minDistSq);

        if (winding == 0)
            sd = -sd;

        float normalized = Mathf.Clamp01(sd / range + 0.5f);
        return (int)(normalized * 65535f + 0.5f);
    }

    [TestCase(32, 8)]
    [TestCase(128, 32)]
    public void CulledFieldMatchesTheBruteForceField(int cell, int range)
    {
        const int side = 2048;
        Assert.IsTrue(NowTrueType.TryParse(_fontBytes, out var font, out string error), error);
        Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, cell, range, side, true, out var session, out error), error);

        var codepoints = new List<int>();
        foreach (char c in Ascii)
            codepoints.Add(c);

        var results = new List<NowFontAtlasInfo.Glyph>();
        Assert.AreEqual(
            NowFontCompiler.DynamicSession.AddResult.Ok,
            session.TryAddGlyphs(codepoints.ToArray(), codepoints.Count, results, out error),
            error);

        byte[] atlas = null;
        Assert.IsTrue(session.TryCopyAtlas(ref atlas, out error), error);

        float scale = (float)cell / font.unitsPerEm;
        var outline = new NowGlyphOutline();
        var segments = new List<Vector4>();
        long pixels = 0;
        long exact = 0;
        int maxDifference = 0;

        foreach (var glyph in results)
        {
            if (glyph.atlasBounds.right <= glyph.atlasBounds.left)
                continue;

            Assert.IsTrue(font.TryGetGlyphIndex(glyph.unicode, out int glyphIndex));
            Assert.IsTrue(font.TryGetOutline(glyphIndex, outline));
            var origin = new Vector2(
                Mathf.Round(glyph.planeBounds.left * cell),
                Mathf.Round(glyph.planeBounds.bottom * cell));
            segments.Clear();
            NowManagedFontBaker.Flatten(outline, scale, origin, segments);

            int x0 = (int)glyph.atlasBounds.left;
            int y0 = (int)glyph.atlasBounds.bottom;
            int width = (int)glyph.atlasBounds.right - x0;
            int height = (int)glyph.atlasBounds.top - y0;

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    int expected = ReferenceValue(segments, 0, segments.Count, x + 0.5f, y + 0.5f, range);
                    int actual = Packed16(atlas, ((y0 + y) * side + x0 + x) * 4);
                    int difference = Mathf.Abs(expected - actual);
                    maxDifference = Mathf.Max(maxDifference, difference);
                    exact += difference == 0 ? 1 : 0;
                    ++pixels;
                }
            }
        }

        Assert.Greater(pixels, 0);
        // Burst may fuse multiply-adds differently from the managed reference, which can
        // move a value that sits on a rounding boundary by one 16-bit step.
        Assert.LessOrEqual(maxDifference, 1, $"cell {cell}: culling must not change the field.");
        Assert.GreaterOrEqual(exact / (double)pixels, 0.999, $"cell {cell}: {pixels - exact} of {pixels} pixels differ.");
    }

    [Test]
    public void PagesHoldTheSessionCellsAndNothingElse()
    {
        var font = CompileFont();

        try
        {
            const float size = 20f;
            font.EnsureGlyphs(Ascii, size);
            int atlasSize = font.GetDynamicGlyphSize(size);
            int range = font.GetDynamicPixelRange(0f, size);

            Assert.IsTrue(font.GetGlyph('A', size, 0f, out _, out Material material));
            var page = (Texture2D)material.mainTexture;
            Assert.IsTrue(page.isReadable);
            byte[] pageData = page.GetRawTextureData();
            int side = page.width;

            Assert.IsTrue(NowManagedFontSession.TryCreate(_fontBytes, atlasSize, range, side, font.ResolvePackedManagedSdf16(), out var reference, out string error), error);
            var covered = new bool[side * side];
            int maxRow = 0;

            foreach (char c in Ascii)
            {
                Assert.IsTrue(font.GetGlyph(c, size, 0f, out var glyph, out Material glyphMaterial));
                Assert.AreSame(material, glyphMaterial, $"'{c}' shares the page.");

                var results = new List<NowFontAtlasInfo.Glyph>();
                Assert.AreEqual(NowFontCompiler.DynamicSession.AddResult.Ok, reference.TryAddGlyphs(new[] { (int)c }, 1, results, out error), error);
                byte[] referenceAtlas = null;
                Assert.IsTrue(reference.TryCopyAtlas(ref referenceAtlas, out error), error);
                var expected = results[0];

                // Resolved glyphs carry normalized atlas bounds; sessions report pixels.
                int x0 = Mathf.RoundToInt(glyph.atlasBounds.left * side);
                int y0 = Mathf.RoundToInt(glyph.atlasBounds.bottom * side);
                int width = Mathf.RoundToInt(glyph.atlasBounds.right * side) - x0;
                int height = Mathf.RoundToInt(glyph.atlasBounds.top * side) - y0;
                Assert.AreEqual((int)expected.atlasBounds.right - (int)expected.atlasBounds.left, width, $"'{c}' cell width");
                Assert.AreEqual((int)expected.atlasBounds.top - (int)expected.atlasBounds.bottom, height, $"'{c}' cell height");
                maxRow = Mathf.Max(maxRow, y0 + height);

                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        int pageOffset = ((y0 + y) * side + x0 + x) * 4;
                        int referenceOffset = (((int)expected.atlasBounds.bottom + y) * side + (int)expected.atlasBounds.left + x) * 4;
                        covered[(y0 + y) * side + x0 + x] = true;

                        for (int channel = 0; channel < 4; ++channel)
                        {
                            if (pageData[pageOffset + channel] != referenceAtlas[referenceOffset + channel])
                                Assert.Fail($"'{c}' differs from the reference bake at ({x}, {y}).");
                        }
                    }
                }
            }

            // Gaps between cells are what bilinear sampling reads past a cell edge.
            for (int y = 0; y <= Mathf.Min(side - 1, maxRow); ++y)
            {
                for (int x = 0; x < side; ++x)
                {
                    if (covered[y * side + x])
                        continue;

                    int offset = (y * side + x) * 4;
                    if (pageData[offset] != 0 || pageData[offset + 1] != 0 || pageData[offset + 2] != 0 || pageData[offset + 3] != 0)
                        Assert.Fail($"The page must be clear outside glyph cells; ({x}, {y}) is not.");
                }
            }
        }
        finally
        {
            Release(font);
        }
    }

#if NOWUI_STANDALONE
    [Test]
    public void AddingGlyphsUploadsOnlyTheRowsTheyUse()
    {
        if (!(NowRuntime.backend is NullRenderBackend backend))
        {
            Assert.Ignore("Needs the recording null backend.");
            return;
        }

        var font = CompileFont();

        try
        {
            font.EnsureGlyphs("ABC", 20f);
            Assert.IsTrue(font.GetGlyph('A', 20f, 0f, out _, out Material material));
            var page = (Texture2D)material.mainTexture;
            RectInt first = backend.lastDirtyRect;
            Assert.AreEqual(0, first.x);
            Assert.AreEqual(page.width, first.width);
            Assert.Less(first.height, page.height / 4, "A first bake uploads its band, not the whole page.");

            int uploads = backend.textureUploads;
            font.EnsureGlyphs("abc", 20f);
            Assert.AreEqual(uploads + 1, backend.textureUploads);
            Assert.Less(backend.lastDirtyRect.height, page.height / 4, "Adding glyphs uploads only their rows.");
        }
        finally
        {
            Release(font);
        }
    }
#else
    [TestCase(20f)]
    [TestCase(115f)]
    public void GpuPageMatchesTheCpuCopyAfterBandUploads(float size)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Assert.Ignore("Needs a graphics device.");
            return;
        }

        var font = CompileFont();

        try
        {
            // Two bakes: the page's first band, then a second band added later.
            font.EnsureGlyphs(Ascii.Substring(0, 40), size);
            font.EnsureGlyphs(Ascii.Substring(40), size);
            Assert.IsTrue(font.GetGlyph('A', size, 0f, out _, out Material material));
            var page = (Texture2D)material.mainTexture;
            byte[] cpu = page.GetRawTextureData();

            var request = AsyncGPUReadback.Request(page, 0, TextureFormat.RGBA32);
            request.WaitForCompletion();
            Assert.IsFalse(request.hasError);
            byte[] gpu = request.GetData<byte>().ToArray();
            int side = page.width;
            int checkedTexels = 0;

            foreach (char c in Ascii)
            {
                Assert.IsTrue(font.GetGlyph(c, size, 0f, out var glyph, out _));

                if (glyph.atlasBounds.right <= glyph.atlasBounds.left)
                    continue;

                // The cell plus the one-texel border bilinear sampling reaches.
                int x0 = Mathf.Max(0, Mathf.RoundToInt(glyph.atlasBounds.left * side) - 1);
                int y0 = Mathf.Max(0, Mathf.RoundToInt(glyph.atlasBounds.bottom * side) - 1);
                int x1 = Mathf.Min(side, Mathf.RoundToInt(glyph.atlasBounds.right * side) + 1);
                int y1 = Mathf.Min(side, Mathf.RoundToInt(glyph.atlasBounds.top * side) + 1);

                for (int y = y0; y < y1; ++y)
                {
                    for (int x = x0; x < x1; ++x)
                    {
                        int offset = (y * side + x) * 4;

                        for (int channel = 0; channel < 4; ++channel)
                        {
                            if (cpu[offset + channel] != gpu[offset + channel])
                                Assert.Fail($"'{c}' at ({x}, {y}) reached the GPU as {gpu[offset + channel]}, not {cpu[offset + channel]}.");
                        }

                        ++checkedTexels;
                    }
                }
            }

            Assert.Greater(checkedTexels, 0);
        }
        finally
        {
            Release(font);
        }
    }
#endif
}
