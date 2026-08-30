using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Internal;
using Object = UnityEngine.Object;

public class NowManagedFontCompilerTests
{
    const string FontAssetPath = "Assets/NowUI/Assets/Fonts/NotoSans/NotoSans-Regular.ttf.asset";
    const string ArabicFontAssetPath = "Assets/NowUI/Assets/Fonts/Noto_Sans_Arabic/NotoSansArabic-Regular.ttf.asset";

    const int Size = 64;
    const int PixelRange = 16;
    const int AtlasSide = 512;

    byte[] _fontBytes;
    byte[] _arabicFontBytes;

    [OneTimeSetUp]
    public void LoadFontBytes()
    {
        var font = AssetDatabase.LoadAssetAtPath<NowFont>(FontAssetPath);
        Assert.NotNull(font, $"Test font asset not found at {FontAssetPath}");
        Assert.IsTrue(font.TryGetSourceBytes(out _fontBytes), "Test font asset has no embedded source bytes.");

        var arabicFont = AssetDatabase.LoadAssetAtPath<NowFont>(ArabicFontAssetPath);
        Assert.NotNull(arabicFont, $"Test font asset not found at {ArabicFontAssetPath}");
        Assert.IsTrue(
            arabicFont.TryGetSourceBytes(out _arabicFontBytes),
            "Arabic test font asset has no embedded source bytes.");
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

            Assert.GreaterOrEqual(glyph.atlasBounds.right - glyph.atlasBounds.left, PixelRange,
                "The padded cell must be larger than the distance range it carries.");
        }

        byte[] atlas = null;
        Assert.IsTrue(session.TryCopyAtlas(ref atlas, out error), error);
        Assert.AreEqual(AtlasSide * AtlasSide * 4, atlas.Length);

        bool hasInk = false;
        bool hasPackedLowByte = false;

        for (int i = 0; i < atlas.Length; i += 4)
        {
            hasInk |= atlas[i] > 140;
            hasPackedLowByte |=
                atlas[i] == atlas[i + 1] &&
                atlas[i] == atlas[i + 3] &&
                atlas[i] != atlas[i + 2];

            if (hasInk && hasPackedLowByte)
                break;
        }

        Assert.IsTrue(hasInk, "Baked atlas contains no inside-glyph pixels.");
        Assert.IsTrue(hasPackedLowByte,
            "Managed SDF pages must retain the low distance byte in blue instead of collapsing large ranges to 8-bit precision.");
    }

    [Test]
    public void LegacyCustomMaterialKeepsReplicatedManagedSdfChannels()
    {
        var legacyTemplate = Resources.Load<Material>("NowUI/UIMaterial");
        Assert.NotNull(legacyTemplate);
        Assert.IsFalse(legacyTemplate.HasProperty("_NowUITextSdfEncoding"));

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(
            _fontBytes,
            Size,
            PixelRange,
            legacyTemplate,
            out NowFont font,
            out string error), error);

        try
        {
            Assert.IsTrue(font.GetGlyph('A', 80f, out _, out var material));
            var atlas = ((Texture2D)material.mainTexture).GetRawTextureData<Color32>();

            for (int i = 0; i < atlas.Length; ++i)
            {
                Color32 pixel = atlas[i];

                if (pixel.r != pixel.g || pixel.r != pixel.b || pixel.r != pixel.a)
                {
                    Assert.Fail($"Legacy pixel {i} no longer replicates its SDF value across RGBA: {pixel}.");
                }
            }
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void LegacyCustomMaterialKeepsSinglePassOutlineContract()
    {
        var legacyTemplate = Resources.Load<Material>("NowUI/UIMaterial");
        Assert.NotNull(legacyTemplate);
        Assert.IsFalse(legacyTemplate.HasProperty("_NowUITextOutlineOnlyPass"));

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(
            _fontBytes,
            Size,
            PixelRange,
            legacyTemplate,
            out NowFont font,
            out string error), error);

        var drawList = new NowDrawList();
        bool previousShaping = Now.textShaping;

        try
        {
            Now.textShaping = false;

            using (drawList.Begin(new Vector2(256f, 160f)))
            {
                Now.Text(new NowRect(0f, 0f, 256f, 160f), font)
                    .SetFontSize(80f)
                    .SetOutlinePixels(20f)
                    .Draw("A");
            }

            var extras = new List<Vector4>();
            drawList.mesh.GetUVs(5, extras);
            Assert.AreEqual(4, extras.Count,
                "A legacy custom shader must keep the original combined fill/outline quad instead of being drawn twice.");
            Assert.Greater(extras[0].y, 0f,
                "Negative range is a private outline-only signal unsupported by legacy custom shaders.");

            using (drawList.Begin(new Vector2(256f, 160f)))
            {
                Now.Text(new NowRect(0f, 0f, 256f, 160f), font)
                    .SetFontSize(80f)
                    .SetOutlinePixels(20f)
                    .Draw("A".AsSpan());
            }

            extras.Clear();
            drawList.mesh.GetUVs(5, extras);
            Assert.AreEqual(4, extras.Count,
                "The span overload must retain one combined legacy-shader quad.");
            Assert.Greater(extras[0].y, 0f);

            using (drawList.Begin(new Vector2(256f, 160f)))
            {
                Now.Text(new NowRect(0f, 0f, 256f, 160f), font)
                    .SetFontSize(80f)
                    .SetOutlinePixels(20f)
                    .Draw('A');
            }

            extras.Clear();
            drawList.mesh.GetUVs(5, extras);
            Assert.AreEqual(4, extras.Count,
                "The character overload must also retain one combined legacy-shader quad.");
            Assert.Greater(extras[0].y, 0f);
        }
        finally
        {
            Now.textShaping = previousShaping;
            drawList.Dispose();
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void ManagedSessionCopiesDirectlyIntoNativeStorageWithoutAllocating()
    {
        Assert.IsTrue(NowManagedFontSession.TryCreate(
            _fontBytes,
            Size,
            PixelRange,
            AtlasSide,
            out var session,
            out string error), error);

        var results = new List<NowFontAtlasInfo.Glyph>();
        int[] codepoints = { 'A' };

        try
        {
            Assert.AreEqual(
                NowFontCompiler.DynamicSession.AddResult.Ok,
                session.TryAddGlyphs(codepoints, codepoints.Length, results, out error),
                error);

            using var destination = new NativeArray<byte>(
                AtlasSide * AtlasSide * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            Assert.IsTrue(session.TryCopyAtlas(destination, out error), error);

            bool hasInk = false;

            for (int i = 0; i < destination.Length && !hasInk; i += 4)
                hasInk = destination[i] > 140;

            Assert.IsTrue(hasInk);

            // Warm the call and the runtime allocation counter before measuring.
            Assert.IsTrue(session.TryCopyAtlas(destination, out error), error);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool copied = true;

            for (int i = 0; i < 32; ++i)
                copied &= session.TryCopyAtlas(destination, out error);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(copied, error);
            Assert.AreEqual(0, allocated, "Direct atlas copies must not allocate managed staging buffers.");
        }
        finally
        {
            session.Dispose();
        }
    }

    [Test]
    public void ManagedSessionRejectsWrongSizedNativeStorage()
    {
        Assert.IsTrue(NowManagedFontSession.TryCreate(
            _fontBytes,
            Size,
            PixelRange,
            AtlasSide,
            out var session,
            out string error), error);

        try
        {
            int exactBytes = AtlasSide * AtlasSide * 4;
            using var tooSmall = new NativeArray<byte>(exactBytes - 1, Allocator.Temp);
            using var tooLarge = new NativeArray<byte>(exactBytes + 1, Allocator.Temp);

            Assert.IsFalse(session.TryCopyAtlas(tooSmall, out string smallError));
            Assert.IsFalse(string.IsNullOrEmpty(smallError));
            Assert.IsFalse(session.TryCopyAtlas(tooLarge, out string largeError));
            Assert.IsFalse(string.IsNullOrEmpty(largeError));
        }
        finally
        {
            session.Dispose();
        }
    }

    [Test]
    public void RejectedLegacyCompilerPageIsDestroyedAndClearsOutValue()
    {
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            font.dynamicMaxAtlasSize = 1;
            font.dynamicMaxAtlasBytes = 4;
            MethodInfo compilePage = typeof(NowFont).GetMethod(
                "TryCompileDynamicPage",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(compilePage);
            object[] arguments = { "A", Size, PixelRange, null };

            Assert.IsFalse((bool)compilePage.Invoke(font, arguments));
            Assert.IsNull(arguments[3],
                "A compiled page rejected by the atlas limits must be destroyed and removed from the out value.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void FailedFirstSessionAtlasCopyDoesNotPublishEmptyPage()
    {
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        Assert.IsTrue(NowFontCompiler.DynamicSession.TryCreate(
            _fontBytes,
            Size,
            PixelRange,
            AtlasSide,
            out var session,
            out error), error);

        FieldInfo managedField = typeof(NowFontCompiler.DynamicSession).GetField(
            "_managed",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object managedSession = managedField?.GetValue(session);

        try
        {
            Assert.NotNull(managedField);
            Assert.NotNull(managedSession);

            MethodInfo getState = typeof(NowFont).GetMethod(
                "GetDynamicSessionState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo commit = typeof(NowFont).GetMethod(
                "TryCommitSessionGlyphs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(getState);
            Assert.NotNull(commit);

            object state = getState.Invoke(font, new object[] { Size, PixelRange });
            Type stateType = state.GetType();
            FieldInfo sessionField = stateType.GetField("session", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo pageField = stateType.GetField("page", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo reservationField = stateType.GetField(
                "reservedPageBytes",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(sessionField);
            Assert.NotNull(pageField);
            Assert.NotNull(reservationField);

            sessionField.SetValue(state, session);
            reservationField.SetValue(state, 2L * AtlasSide * AtlasSide * 4);

            // Make the native-copy facade fail after page creation without changing
            // production code: a managed session has no native handle once detached.
            managedField.SetValue(session, null);
            object[] arguments =
            {
                state,
                new List<NowFontAtlasInfo.Glyph>(),
                Size,
                PixelRange,
                false
            };

            Assert.IsFalse((bool)commit.Invoke(font, arguments));
            Assert.IsFalse((bool)arguments[4]);
            Assert.AreEqual(0, font.GetCachedDynamicPageCount(),
                "A page is publishable only after its first atlas copy succeeds.");
            Assert.IsNull(pageField.GetValue(state));
        }
        finally
        {
            managedField?.SetValue(session, managedSession);
            font.ClearDynamicCache();
            session.Dispose();
            Object.DestroyImmediate(font);
        }
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
        int[] codepoints = { 0xE321 };

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
    public void DynamicOutlineRangeSupportsOneHundredPixelsAtEightyPixelText()
    {
        const float FontSize = 80f;
        const float OutlinePixels = 100f;
        const float OutlineEm = OutlinePixels / FontSize;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            Assert.IsTrue(font.GetGlyph('A', FontSize, 0f, out var baseGlyph, out var baseMaterial));
            Assert.IsTrue(font.GetGlyph('A', FontSize, OutlineEm, out var outlinedGlyph, out var outlinedMaterial));

            float encodedFullRange = font.GetScreenPixelRange('A', FontSize, OutlineEm);
            float encodedOutwardRange = encodedFullRange * 0.5f;

            Assert.Greater(encodedOutwardRange, OutlinePixels,
                "The encoded field needs AA/filtering headroom beyond the requested outward stroke.");
            Assert.Less(outlinedGlyph.planeBounds.left, baseGlyph.planeBounds.left,
                "A wider field must expand the glyph quad to the left.");
            Assert.Less(outlinedGlyph.planeBounds.bottom, baseGlyph.planeBounds.bottom,
                "A wider field must expand the glyph quad below the contour.");
            Assert.Greater(outlinedGlyph.planeBounds.right, baseGlyph.planeBounds.right,
                "A wider field must expand the glyph quad to the right.");
            Assert.Greater(outlinedGlyph.planeBounds.top, baseGlyph.planeBounds.top,
                "A wider field must expand the glyph quad above the contour.");
            Assert.AreNotSame(baseMaterial, outlinedMaterial,
                "Different distance-range tiers must not bind the same atlas material.");
            Assert.AreNotSame(baseMaterial.mainTexture, outlinedMaterial.mainTexture,
                "Different distance-range tiers must not sample the same atlas texture.");
            Assert.AreEqual(1f, outlinedMaterial.GetFloat("_NowUITextSdfEncoding"), 0.001f,
                "Managed dynamic pages must opt into packed 16-bit distance decoding.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void NativeDynamicOutlineRangeSupportsOneHundredPixelsAtEightyPixelText()
    {
        const float FontSize = 80f;
        const float OutlinePixels = 100f;
        const float OutlineEm = OutlinePixels / FontSize;

        NowFontCompiler.forceNativeCompiler = true;

        if (!NowFontCompiler.DynamicSession.TryCreate(
                _fontBytes,
                Size,
                PixelRange,
                AtlasSide,
                out var probe,
                out _) ||
            probe.isManaged)
        {
            probe?.Dispose();
            Assert.Ignore("Native font compiler not available on this platform.");
        }

        probe.Dispose();
        var materialTemplate = new Material(Resources.Load<Material>("NowUI/TxtMaterial"));
        materialTemplate.SetFloat("_NowUITextSdfEncoding", 1f);
        Assert.IsTrue(NowFontCompiler.TryCompile(
            _fontBytes,
            Size,
            PixelRange,
            materialTemplate,
            out NowFont font,
            out string error), error);

        try
        {
            Assert.IsTrue(font.GetGlyph('A', FontSize, 0f, out var baseGlyph, out _));
            Assert.IsTrue(font.GetGlyph('A', FontSize, OutlineEm, out var outlinedGlyph, out var outlinedMaterial));
            Assert.Greater(font.GetScreenPixelRange('A', FontSize, OutlineEm) * 0.5f, OutlinePixels);
            Assert.Less(outlinedGlyph.planeBounds.left, baseGlyph.planeBounds.left);
            Assert.Less(outlinedGlyph.planeBounds.bottom, baseGlyph.planeBounds.bottom);
            Assert.Greater(outlinedGlyph.planeBounds.right, baseGlyph.planeBounds.right);
            Assert.Greater(outlinedGlyph.planeBounds.top, baseGlyph.planeBounds.top);
            Assert.AreEqual(0f, outlinedMaterial.GetFloat("_NowUITextSdfEncoding"), 0.001f,
                "Native MTSDF pages must clear a managed decode flag inherited from a reused template.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
            Object.DestroyImmediate(materialTemplate);
        }
    }

    [Test]
    public void NativeOneShotPageClearsPackedDecodeInheritedFromTemplate()
    {
        NowFontCompiler.forceNativeCompiler = true;

        if (!NowFontCompiler.DynamicSession.TryCreate(
                _fontBytes,
                Size,
                PixelRange,
                AtlasSide,
                out var probe,
                out _) ||
            probe.isManaged)
        {
            probe?.Dispose();
            Assert.Ignore("Native font compiler not available on this platform.");
        }

        probe.Dispose();
        var materialTemplate = new Material(Resources.Load<Material>("NowUI/TxtMaterial"));
        materialTemplate.SetFloat("_NowUITextSdfEncoding", 1f);
        NowFont page = null;

        try
        {
            int[] codepoints = { 'A' };
            Assert.IsTrue(NowFontCompiler.TryCompilePage(
                _fontBytes,
                Size,
                PixelRange,
                codepoints,
                codepoints.Length,
                materialTemplate,
                out page,
                out string error), error);
            Assert.AreEqual(0f, page.material.GetFloat("_NowUITextSdfEncoding"), 0.001f,
                "One-shot native MTSDF output must not inherit packed managed decoding.");
        }
        finally
        {
            if (page != null)
            {
                Object.DestroyImmediate(page.material);
                Object.DestroyImmediate(page.atlas);
                Object.DestroyImmediate(page);
            }

            Object.DestroyImmediate(materialTemplate);
        }
    }

    [Test]
    public void DynamicOutlineCapacityUsesHiddenPowerOfTwoTiers()
    {
        const float FontSize = 80f;
        float[] outlinePixels = { 0f, 8f, 9f, 19f, 37f, 73f, 100f, -100f };
        int[] expectedRanges = { 16, 16, 32, 64, 128, 256, 256, 256 };

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            for (int i = 0; i < outlinePixels.Length; ++i)
            {
                float outlineEm = outlinePixels[i] / FontSize;
                int range = font.GetDynamicPixelRange(outlineEm, FontSize);

                Assert.AreEqual(expectedRanges[i], range,
                    $"Unexpected hidden SDF capacity for {outlinePixels[i]}px.");
                Assert.AreEqual(0, range & (range - 1),
                    "Uncapped dynamic ranges should be power-of-two capacity tiers.");
            }

            Assert.AreEqual(0, font.GetCachedDynamicPageCount(),
                "Selecting hidden capacity must not allocate until a glyph is actually requested.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void DynamicSdfBudgetIsInternalAndDefaultsToSixtyFourMiB()
    {
        Assert.AreEqual(64L * 1024 * 1024, NowFont.DEFAULT_DYNAMIC_CACHE_BUDGET_BYTES);

        FieldInfo overrideField = typeof(NowFont).GetField(
            "dynamicCacheBudgetBytesOverride",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(overrideField);
        Assert.IsFalse(overrideField.IsPublic);
        Assert.IsTrue(overrideField.IsNotSerialized);
        Assert.IsNull(overrideField.GetCustomAttribute<SerializeField>(),
            "The internal cache ceiling must not become an authored font setting.");
    }

    [Test]
    public void MaximumDynamicRangeFitsBesidePreparedBasePageUnderDefaultBudget()
    {
        const float FontSize = 80f;
        const float HugeOutlineEm = 2000f / FontSize;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            int maximumRange = font.GetDynamicPixelRange(HugeOutlineEm, FontSize);
            Assert.AreEqual(1916, maximumRange,
                "The geometric maximum should be trimmed only enough to retain the prepared face page.");
            Assert.IsTrue(font.GetGlyph(' ', FontSize, 0f, out _, out var baseMaterial));
            Assert.IsTrue(font.GetGlyph(' ', FontSize, HugeOutlineEm, out _, out var effectMaterial),
                "The advertised maximum range must remain allocatable after face prewarming.");
            Assert.AreNotSame(baseMaterial, effectMaterial);
            Assert.IsFalse(font.IsDynamicGlyphCapacityBlocked(' ', Size, maximumRange));
            Assert.LessOrEqual(
                font.GetEstimatedDynamicCacheResidentBytes(),
                NowFont.DEFAULT_DYNAMIC_CACHE_BUDGET_BYTES);
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void InspectorStyleOutlineScrubUsesFiveSparseWritableVariants()
    {
        const float FontSize = 80f;
        var selectedRanges = new HashSet<int>();
        var textures = new List<Texture2D>();

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            for (int pixels = 0; pixels <= 100; ++pixels)
            {
                float outlineEm = pixels / FontSize;
                selectedRanges.Add(font.GetDynamicPixelRange(outlineEm, FontSize));
                Assert.IsTrue(font.GetGlyph('A', FontSize, outlineEm, out _, out _));
            }

            CollectionAssert.AreEquivalent(new[] { 16, 32, 64, 128, 256 }, selectedRanges);
            Assert.AreEqual(5, font.GetCachedDynamicPageCount(),
                "Inspector scrubbing should populate logarithmic capacity tiers, not one page per value.");
            Assert.AreEqual(5, font.GetCachedDynamicGlyphCount());

            font.GetCachedDynamicAtlasTextures(textures);
            Assert.AreEqual(5, textures.Count);

            int readableCount = 0;
            long textureBytes = 0;
            int fullPageCount = 0;
            int sparsePageCount = 0;

            for (int i = 0; i < textures.Count; ++i)
            {
                var texture = textures[i];
                Assert.AreEqual(texture.width, texture.height);
                textureBytes += (long)texture.width * texture.height * 4;

                if (texture.width == 1024)
                    ++fullPageCount;
                else if (texture.width == 512)
                    ++sparsePageCount;

                if (texture.isReadable)
                    ++readableCount;
            }

            Assert.AreEqual(2, fullPageCount);
            Assert.AreEqual(3, sparsePageCount);
            Assert.AreEqual(11L * 1024 * 1024, textureBytes,
                "Sparse effect tiers should not eagerly reserve a full default page.");
            Assert.AreEqual(5, readableCount,
                "Canonical tier sessions remain writable so alternating styles can append without page fragmentation.");
            Assert.AreEqual(44L * 1024 * 1024, font.GetEstimatedDynamicCacheResidentBytes(),
                "Five writable tiers count GPU, readable CPU, session atlas, and conservative work storage.");
            Assert.LessOrEqual(
                font.GetEstimatedDynamicCacheResidentBytes(),
                NowFont.DEFAULT_DYNAMIC_CACHE_BUDGET_BYTES);

            for (int pixels = 100; pixels >= 0; --pixels)
            {
                float outlineEm = pixels / FontSize;
                Assert.IsTrue(font.GetGlyph('A', FontSize, outlineEm, out _, out _));
            }

            Assert.AreEqual(5, font.GetCachedDynamicPageCount());
            Assert.AreEqual(5, font.GetCachedDynamicGlyphCount());

            // Revisit every tier with a character that was not present during the scrub.
            // Keeping canonical sessions writable must append it without stranding a new page.
            foreach (float pixels in new[] { 0f, 9f, 19f, 37f, 73f })
                Assert.IsTrue(font.GetGlyph('B', FontSize, pixels / FontSize, out _, out _));

            Assert.AreEqual(5, font.GetCachedDynamicPageCount());
            Assert.AreEqual(10, font.GetCachedDynamicGlyphCount());
            Assert.AreEqual(44L * 1024 * 1024, font.GetEstimatedDynamicCacheResidentBytes());
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void DynamicBudgetStopsGrowthWithoutCreatingPermanentMisses()
    {
        const float FontSize = 80f;
        const float OutlineEm = 37f / FontSize;
        const int ExpectedRange = 128;
        const long OneWritablePageBudget = 4L * 512 * 512 * 4;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        font.dynamicCacheBudgetBytesOverride = OneWritablePageBudget;

        try
        {
            int succeeded = 0;
            int refused = 0;

            for (int unicode = 'A'; unicode <= 'Z'; ++unicode)
            {
                if (font.GetGlyph(unicode, FontSize, OutlineEm, out _, out _))
                {
                    ++succeeded;
                    continue;
                }

                refused = unicode;
                break;
            }

            Assert.Greater(succeeded, 0);
            Assert.AreNotEqual(0, refused, "The tiny test budget should eventually refuse a second page.");
            Assert.AreEqual(1, font.GetCachedDynamicPageCount(),
                "Published pages remain valid; a denied allocation must not publish a partial page.");
            Assert.LessOrEqual(font.GetEstimatedDynamicCacheResidentBytes(), OneWritablePageBudget);
            Assert.IsTrue(font.IsDynamicGlyphCapacityBlocked(refused, Size, ExpectedRange));
            Assert.IsFalse(font.IsDynamicGlyphMissing(refused, Size, ExpectedRange),
                "Capacity pressure is transient cache state, not a missing font glyph.");

            int pagesBeforeRetry = font.GetCachedDynamicPageCount();
            long bytesBeforeRetry = font.GetEstimatedDynamicCacheResidentBytes();
            Assert.IsFalse(font.GetGlyph(refused, FontSize, OutlineEm, out _, out _));
            Assert.AreEqual(pagesBeforeRetry, font.GetCachedDynamicPageCount());
            Assert.AreEqual(bytesBeforeRetry, font.GetEstimatedDynamicCacheResidentBytes());

            font.ClearDynamicCache();
            Assert.AreEqual(0, font.GetCachedDynamicPageCount());
            Assert.AreEqual(0, font.GetCachedDynamicGlyphCount());
            Assert.AreEqual(0, font.GetEstimatedDynamicCacheResidentBytes());

            font.dynamicCacheBudgetBytesOverride = 0;
            Assert.IsTrue(font.GetGlyph(refused, FontSize, OutlineEm, out _, out _),
                "Clearing the cache must permit a formerly capacity-blocked glyph to bake again.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void DynamicBudgetFallsBackToBestCachedLowerRange()
    {
        const float FontSize = 80f;
        const float OutlineEm = 37f / FontSize;
        const int RequestedRange = 128;
        const long SealedBasePageBudget = 4L * 1024 * 1024;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            Assert.IsTrue(font.GetGlyph('A', FontSize, 0f, out var baseGlyph, out var baseMaterial));
            var baseTexture = (Texture2D)baseMaterial.mainTexture;
            Assert.IsTrue(baseTexture.isReadable);

            font.dynamicCacheBudgetBytesOverride = SealedBasePageBudget;

            Assert.IsTrue(font.GetGlyph('A', FontSize, OutlineEm, out var fallbackGlyph, out var fallbackMaterial));
            Assert.AreSame(baseMaterial, fallbackMaterial);
            Assert.AreSame(baseTexture, fallbackMaterial.mainTexture);
            AssertGlyphBoundsEqual(baseGlyph, fallbackGlyph, "capacity fallback");
            Assert.IsFalse(baseTexture.isReadable,
                "Budget pressure should seal the least-recent writable session without destroying its page.");
            Assert.AreEqual(1, font.GetCachedDynamicPageCount());
            Assert.AreEqual(1, font.GetCachedDynamicGlyphCount());
            Assert.AreEqual(SealedBasePageBudget, font.GetEstimatedDynamicCacheResidentBytes());
            Assert.IsTrue(font.IsDynamicGlyphCapacityBlocked('A', Size, RequestedRange));
            Assert.AreEqual(20f, font.GetScreenPixelRange('A', FontSize, OutlineEm), 0.001f,
                "Shader clamping must use the actual lower-range fallback page.");
            Assert.AreSame(baseMaterial, font.GetMaterial('A', FontSize, OutlineEm));
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void CapacityFallbackRefreshesWhenABetterLowerRangeIsBaked()
    {
        const float FontSize = 80f;
        const float MediumOutlineEm = 37f / FontSize;
        const float HighOutlineEm = 150f / FontSize;
        const int HighRange = 512;
        const long Budget = 16L * 1024 * 1024;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        font.dynamicCacheBudgetBytesOverride = Budget;

        try
        {
            Assert.IsTrue(font.GetGlyph('A', FontSize, 0f, out _, out var baseMaterial));
            Assert.IsTrue(font.GetGlyph('A', FontSize, HighOutlineEm, out _, out var firstFallback));
            Assert.AreSame(baseMaterial, firstFallback);
            Assert.IsTrue(font.IsDynamicGlyphCapacityBlocked('A', Size, HighRange));
            Assert.IsTrue(font.TryGetPreparedCodepointRun(
                "A",
                FontSize,
                HighOutlineEm,
                NowFontStyle.Regular,
                4,
                out var firstPrepared));
            Assert.AreSame(baseMaterial, firstPrepared.glyphs[0].material);

            Assert.IsTrue(font.GetGlyph('A', FontSize, MediumOutlineEm, out _, out var mediumMaterial));
            Assert.AreNotSame(baseMaterial, mediumMaterial);

            Assert.IsTrue(font.GetGlyph('A', FontSize, HighOutlineEm, out _, out var refreshedFallback));
            Assert.AreSame(mediumMaterial, refreshedFallback,
                "A blocked tier must rescan after a better lower-range mapping is published.");
            Assert.IsTrue(font.TryGetPreparedCodepointRun(
                "A",
                FontSize,
                HighOutlineEm,
                NowFontStyle.Regular,
                4,
                out var refreshedPrepared));
            Assert.AreNotSame(firstPrepared, refreshedPrepared);
            Assert.AreSame(mediumMaterial, refreshedPrepared.glyphs[0].material,
                "Prepared runs must not retain the superseded fallback material.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OutlinedDrawReservesBaseFaceBeforeSparseEffectTierUnderBudget(bool spanWithTab)
    {
        const float FontSize = 80f;
        const float OutlinePixels = 37f;
        const float OutlineEm = OutlinePixels / FontSize;
        const long BaseWorkingSetBudget = 4L * 1024 * 1024 * 4;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        var drawList = new NowDrawList();
        bool previousShaping = Now.textShaping;

        try
        {
            Now.textShaping = false;
            font.dynamicCacheBudgetBytesOverride = BaseWorkingSetBudget;

            using (drawList.Begin(new Vector2(256f, 160f)))
            {
                NowText text = Now.Text(new NowRect(0f, 0f, 256f, 160f), font)
                    .SetFontSize(FontSize)
                    .SetOutlinePixels(OutlinePixels);

                if (spanWithTab)
                    text.Draw("\tA".AsSpan());
                else
                    text.Draw("A");
            }

            Assert.IsTrue(font.GetGlyph('A', FontSize, 0f, out _, out var baseMaterial),
                "The face page must survive an outlined draw at the cache cap.");
            Assert.IsTrue(font.GetGlyph('A', FontSize, OutlineEm, out _, out var outlineMaterial));
            Assert.NotNull(baseMaterial);
            Assert.NotNull(outlineMaterial);
            Assert.AreNotSame(baseMaterial, outlineMaterial,
                "The fixture must exercise distinct base and sparse effect pages.");
            Assert.LessOrEqual(font.GetEstimatedDynamicCacheResidentBytes(), BaseWorkingSetBudget);

            if (spanWithTab)
            {
                Assert.IsTrue(font.GetGlyph(' ', FontSize, 0f, out _, out var baseSpaceMaterial),
                    "An outlined span must reserve the base space used for tab advance.");
                Assert.AreSame(baseMaterial, baseSpaceMaterial);
            }

            var extras = new List<Vector4>();
            drawList.mesh.GetUVs(5, extras);
            Assert.AreEqual(8, extras.Count, "The outlined glyph must retain both its ring and face quads.");
            Assert.Less(extras[0].y, 0f, "The first quad must remain the outline-only layer.");
            Assert.AreEqual(20f, extras[4].y, 0.001f,
                "The restored face must use the base page's screen range.");

            if (spanWithTab)
            {
                var vertices = new List<Vector3>();
                drawList.mesh.GetVertices(vertices);
                float outlineCenter =
                    (vertices[0].x + vertices[1].x + vertices[2].x + vertices[3].x) * 0.25f;
                float fillCenter =
                    (vertices[4].x + vertices[5].x + vertices[6].x + vertices[7].x) * 0.25f;
                Assert.AreEqual(outlineCenter, fillCenter, 2f,
                    "Base and effect tab advances must keep the restored face aligned with its outline.");
            }
        }
        finally
        {
            Now.textShaping = previousShaping;
            drawList.Dispose();
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void OutlinedPrimaryGlyphDoesNotWarmUnneededFallbackCaches()
    {
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont primary, out string primaryError), primaryError);
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont fallback, out string fallbackError), fallbackError);
        var drawList = new NowDrawList();
        bool previousShaping = Now.textShaping;

        try
        {
            typeof(NowFontAsset)
                .GetField("_fallbacks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(primary, new NowFontAsset[] { fallback });

            Now.textShaping = false;

            using (drawList.Begin(new Vector2(256f, 160f)))
            {
                Now.Text(new NowRect(0f, 0f, 256f, 160f), primary)
                    .SetFontSize(80f)
                    .SetOutlinePixels(37f)
                    .Draw("A");
            }

            Assert.Greater(primary.GetCachedDynamicPageCount(), 0);
            Assert.AreEqual(0, fallback.GetCachedDynamicPageCount(),
                "Prewarming a primary-owned glyph must not allocate pages in every fallback font.");
            Assert.AreEqual(0, fallback.GetEstimatedDynamicCacheResidentBytes());
        }
        finally
        {
            Now.textShaping = previousShaping;
            drawList.Dispose();
            primary.ClearDynamicCache();
            fallback.ClearDynamicCache();
            Object.DestroyImmediate(primary);
            Object.DestroyImmediate(fallback);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OutlinedMixedOwnerDrawDoesNotWarmUnusedFallbackCaches(bool span)
    {
        const string Text = "A\u0627";

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont primary, out string primaryError), primaryError);
        Assert.IsTrue(NowFontCompiler.TryCompile(_arabicFontBytes, out NowFont owner, out string ownerError), ownerError);
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont unused, out string unusedError), unusedError);
        var drawList = new NowDrawList();
        bool previousShaping = Now.textShaping;

        try
        {
            typeof(NowFontAsset)
                .GetField("_fallbacks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(primary, new NowFontAsset[] { owner, unused });

            Now.textShaping = false;

            using (drawList.Begin(new Vector2(320f, 160f)))
            {
                NowText text = Now.Text(new NowRect(0f, 0f, 320f, 160f), primary)
                    .SetFontSize(80f)
                    .SetOutlinePixels(37f);

                if (span)
                    text.Draw(Text.AsSpan());
                else
                    text.Draw(Text);
            }

            Assert.GreaterOrEqual(primary.GetCachedDynamicPageCount(), 2,
                "The primary-owned glyph must have base and effect variants.");
            Assert.GreaterOrEqual(owner.GetCachedDynamicPageCount(), 2,
                "The fallback-owned glyph must have base and effect variants.");
            Assert.AreEqual(0, unused.GetCachedDynamicPageCount(),
                "A mixed-owner draw must not send the whole string to later fallback fonts.");
            Assert.AreEqual(0, unused.GetEstimatedDynamicCacheResidentBytes());
        }
        finally
        {
            Now.textShaping = previousShaping;
            drawList.Dispose();
            primary.ClearDynamicCache();
            owner.ClearDynamicCache();
            unused.ClearDynamicCache();
            Object.DestroyImmediate(primary);
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(unused);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OutlinedWhitespaceOnlyDrawUsesNoEffectTier(bool span)
    {
        const float FontSize = 80f;
        const float OutlineEm = 37f / FontSize;
        const string Text = " \t  ";

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        var drawList = new NowDrawList();
        bool previousShaping = Now.textShaping;

        try
        {
            Now.textShaping = false;
            Assert.Greater(
                font.GetDynamicPixelRange(OutlineEm, FontSize),
                font.GetDynamicPixelRange(0f, FontSize),
                "The fixture must request a distinct effect tier.");
            Assert.IsTrue(font.GetGlyph(' ', FontSize, 0f, out _, out _));
            int basePageCount = font.GetCachedDynamicPageCount();
            int baseGlyphCount = font.GetCachedDynamicGlyphCount();
            long baseResidentBytes = font.GetEstimatedDynamicCacheResidentBytes();

            using (drawList.Begin(new Vector2(320f, 160f)))
            {
                NowText text = Now.Text(new NowRect(0f, 0f, 320f, 160f), font)
                    .SetFontSize(FontSize)
                    .SetOutlinePixels(37f);

                if (span)
                    text.Draw(Text.AsSpan());
                else
                    text.Draw(Text);
            }

            Assert.AreEqual(basePageCount, font.GetCachedDynamicPageCount(),
                "Whitespace advances should use only the base atlas page.");
            Assert.AreEqual(baseGlyphCount, font.GetCachedDynamicGlyphCount(),
                "Repeated spaces and tabs should share one base-range space record.");
            Assert.AreEqual(baseResidentBytes, font.GetEstimatedDynamicCacheResidentBytes(),
                "An outline-only whitespace pass must not retain an effect-tier session or page.");
        }
        finally
        {
            Now.textShaping = previousShaping;
            drawList.Dispose();
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void ClearDynamicCacheReleasesWorldHostFontMaterialClone()
    {
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        var drawList = new NowDrawList();
        var hostObject = new GameObject("Dynamic Font World Host");

        try
        {
            var host = hostObject.AddComponent<NowWorldGraphic>();

            using (drawList.Begin(new Vector2(160f, 80f)))
            {
                Now.Text(new NowRect(0f, 0f, 160f, 80f))
                    .SetFont(font)
                    .SetFontSize(64f)
                    .Draw("A");
            }

            Assert.Greater(drawList.batches.Count, 0);
            Material worldMaterial = host.GetMaterial(drawList.batches[0]);
            Assert.NotNull(worldMaterial);
            Assert.AreEqual(1f, worldMaterial.GetFloat("_NowUITextSdfEncoding"), 0.001f);
            Assert.AreEqual(1, host.cachedMaterialCount);

            font.ClearDynamicCache();

            Assert.AreEqual(0, host.cachedMaterialCount);
            Assert.IsTrue(worldMaterial == null,
                "Destroying a dynamic font page must release host-local material clones first.");
        }
        finally
        {
            font.ClearDynamicCache();
            drawList.Dispose();
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void DestroyingDynamicFontReleasesOwnedRuntimePageObjects()
    {
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        Material pageMaterial = null;
        Texture2D pageTexture = null;

        try
        {
            Assert.IsTrue(font.GetGlyph('A', 80f, out _, out pageMaterial));
            pageTexture = (Texture2D)pageMaterial.mainTexture;
            Assert.NotNull(pageTexture);

            Object.DestroyImmediate(font);

            Assert.IsTrue(pageMaterial == null,
                "Destroying the owning font must destroy its runtime page material.");
            Assert.IsTrue(pageTexture == null,
                "Destroying the owning font must destroy its runtime page texture.");
        }
        finally
        {
            if (font != null)
            {
                font.ClearDynamicCache();
                Object.DestroyImmediate(font);
            }

            if (pageMaterial != null)
                Object.DestroyImmediate(pageMaterial);

            if (pageTexture != null)
                Object.DestroyImmediate(pageTexture);
        }
    }

#if NOWUI_UGUI
    [Test]
    public void ClearDynamicCacheReleasesUguiFontMaterialClone()
    {
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        var drawList = new NowDrawList();
        var hostObject = new GameObject(
            "Dynamic Font UGUI Host",
            typeof(RectTransform),
            typeof(CanvasRenderer));

        try
        {
            var host = hostObject.AddComponent<NowGraphic>();

            using (drawList.Begin(new Vector2(160f, 80f)))
            {
                Now.Text(new NowRect(0f, 0f, 160f, 80f))
                    .SetFont(font)
                    .SetFontSize(64f)
                    .Draw("A");
            }

            Assert.Greater(drawList.batches.Count, 0);
            Material canvasMaterial = host.GetCanvasMaterial(drawList.batches[0]);
            Assert.NotNull(canvasMaterial);
            Assert.AreEqual(1f, canvasMaterial.GetFloat("_NowUITextSdfEncoding"), 0.001f,
                "The UGUI shader bridge must preserve packed managed-SDF decoding.");
            Assert.AreEqual(1, host.cachedCanvasMaterialCount);

            font.ClearDynamicCache();

            Assert.AreEqual(0, host.cachedCanvasMaterialCount);
            Assert.IsTrue(canvasMaterial == null,
                "Destroying a dynamic font page must release UGUI material clones first.");
        }
        finally
        {
            font.ClearDynamicCache();
            drawList.Dispose();
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(font);
        }
    }
#endif

    [Test]
    public void ShapedOutlineGlyphsSpillAcrossSparseTierPages()
    {
        const float FontSize = 80f;
        const float OutlineEm = 37f / FontSize;
        const string Text = "ABCDEFGHIJKLMN";
        var textures = new List<Texture2D>();
        var usedTextures = new HashSet<Texture>();

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            if (!font.TryGetShapedRun(Text, out var run))
                Assert.Ignore("Native text shaping is unavailable on this platform.");

            Assert.IsTrue(font.EnsureShapedGlyphs(run, FontSize, OutlineEm),
                "An outlined shaped run should spill across sparse pages instead of failing atomically.");
            Assert.AreEqual(Text.Length, run.Length);
            Assert.AreEqual(Text.Length, font.GetCachedDynamicGlyphCount());
            Assert.That(font.GetCachedDynamicPageCount(), Is.InRange(2, 7),
                "The sparse tier should spill without degenerating into one page per glyph.");

            font.GetCachedDynamicAtlasTextures(textures);
            int readableCount = 0;

            for (int i = 0; i < textures.Count; ++i)
            {
                Assert.AreEqual(512, textures[i].width);
                Assert.AreEqual(512, textures[i].height);

                if (textures[i].isReadable)
                    ++readableCount;
            }

            Assert.AreEqual(1, readableCount,
                "Filled pages should be sealed while the current sparse page remains writable.");

            for (int i = 0; i < run.Length; ++i)
            {
                Assert.IsTrue(font.TryGetShapedGlyph(
                    (int)run[i].glyphIndex,
                    FontSize,
                    OutlineEm,
                    out _,
                    out var material));
                Assert.NotNull(material);
                usedTextures.Add(material.mainTexture);
            }

            Assert.AreEqual(textures.Count, usedTextures.Count,
                "Every spilled page should own at least one resolved shaped glyph mapping.");

            int pagesBeforeAppend = font.GetCachedDynamicPageCount();
            Assert.IsTrue(font.TryGetShapedRun("Z", out var appended));
            Assert.IsTrue(font.EnsureShapedGlyphs(appended, FontSize, OutlineEm));
            Assert.AreEqual(1, appended.Length);
            Assert.IsTrue(font.TryGetShapedGlyph(
                (int)appended[0].glyphIndex,
                FontSize,
                OutlineEm,
                out _,
                out _));
            Assert.AreEqual(Text.Length + 1, font.GetCachedDynamicGlyphCount());
            Assert.That(font.GetCachedDynamicPageCount(),
                Is.InRange(pagesBeforeAppend, pagesBeforeAppend + 1));

            for (int i = 0; i < run.Length; ++i)
            {
                Assert.IsTrue(font.TryGetShapedGlyph(
                    (int)run[i].glyphIndex,
                    FontSize,
                    OutlineEm,
                    out _,
                    out _),
                    "Appending to the current tier must not invalidate glyphs on sealed pages.");
            }

            font.GetCachedDynamicAtlasTextures(textures);
            readableCount = 0;

            for (int i = 0; i < textures.Count; ++i)
            {
                if (textures[i].isReadable)
                    ++readableCount;
            }

            Assert.AreEqual(1, readableCount);
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void PreparedShapedOutlineKeepsInvisibleGlyphsOnBaseTier()
    {
        const float FontSize = 80f;
        const float OutlineEm = 100f / FontSize;
        const string Text = "A A";

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            if (!font.TryGetShapedRun(Text, out var shaped))
                Assert.Ignore("Native text shaping is unavailable on this platform.");

            Assert.IsTrue(font.TryGetPreparedShapedRun(Text, FontSize, OutlineEm, out var prepared));
            var baseGlyphs = new HashSet<int>();
            var visibleEffectGlyphs = new HashSet<int>();
            int invisibleCount = 0;

            for (int i = 0; i < shaped.Length; ++i)
                baseGlyphs.Add((int)shaped[i].glyphIndex);

            for (int i = 0; i < prepared.length; ++i)
            {
                var glyph = prepared.glyphs[i];

                if (glyph.visible)
                {
                    visibleEffectGlyphs.Add((int)glyph.glyphIndex);
                    continue;
                }

                ++invisibleCount;
                Assert.IsTrue(font.TryGetShapedGlyph(
                    (int)glyph.glyphIndex,
                    FontSize,
                    0f,
                    out _,
                    out var baseMaterial));
                Assert.AreSame(baseMaterial, glyph.material);
                Assert.IsFalse(font.TryGetShapedGlyph(
                    (int)glyph.glyphIndex,
                    FontSize,
                    OutlineEm,
                    out _,
                    out _),
                    "Invisible shaping controls must not create effect-tier mappings.");
            }

            Assert.Greater(invisibleCount, 0, "The fixture must contain a shaped space glyph.");
            Assert.AreEqual(
                baseGlyphs.Count + visibleEffectGlyphs.Count,
                font.GetCachedDynamicGlyphCount());
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void ShapedOutlineBudgetPressureKeepsCommittedMappingsRetryable()
    {
        const float FontSize = 80f;
        const float OutlineEm = 37f / FontSize;
        const int ExpectedRange = 128;
        const string Text = "ABCDEFGHIJKLMN";
        const long OneWritablePageBudget = 4L * 512 * 512 * 4;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);
        font.dynamicCacheBudgetBytesOverride = OneWritablePageBudget;

        try
        {
            if (!font.TryGetShapedRun(Text, out var run))
                Assert.Ignore("Native text shaping is unavailable on this platform.");

            Assert.IsFalse(font.EnsureShapedGlyphs(run, FontSize, OutlineEm));
            Assert.AreEqual(1, font.GetCachedDynamicPageCount());
            Assert.LessOrEqual(font.GetEstimatedDynamicCacheResidentBytes(), OneWritablePageBudget);

            int resolved = 0;
            int capacityBlocked = 0;

            for (int i = 0; i < run.Length; ++i)
            {
                int encoded = NowFont.EncodeGlyphIndexKey((int)run[i].glyphIndex);

                if (font.TryGetShapedGlyph(
                    (int)run[i].glyphIndex,
                    FontSize,
                    OutlineEm,
                    out _,
                    out _))
                {
                    ++resolved;
                }

                if (!font.IsDynamicGlyphCapacityBlocked(encoded, Size, ExpectedRange))
                    continue;

                ++capacityBlocked;
                Assert.IsFalse(font.IsDynamicGlyphMissing(encoded, Size, ExpectedRange));
            }

            Assert.Greater(resolved, 0, "Glyphs committed before pressure must remain resolvable.");
            Assert.Greater(capacityBlocked, 0);

            int pagesBeforeRetry = font.GetCachedDynamicPageCount();
            Assert.IsFalse(font.EnsureShapedGlyphs(run, FontSize, OutlineEm));
            Assert.AreEqual(pagesBeforeRetry, font.GetCachedDynamicPageCount());

            font.ClearDynamicCache();
            font.dynamicCacheBudgetBytesOverride = 0;
            Assert.IsTrue(font.EnsureShapedGlyphs(run, FontSize, OutlineEm),
                "Clearing capacity state must make the shaped run fully bakeable again.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void DynamicOutlineRangeTiersReuseCachedPagesAndMaterials()
    {
        const float FontSize = 80f;
        const float SmallOutlineEm = 0f;
        const float LargeOutlineEm = 100f / FontSize;

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(_fontBytes, out NowFont font, out string error), error);

        try
        {
            Assert.IsTrue(font.GetGlyph('A', FontSize, SmallOutlineEm, out var firstSmallGlyph, out var firstSmallMaterial));
            Assert.AreEqual(1, font.GetCachedDynamicPageCount());
            Assert.AreEqual(1, font.GetCachedDynamicGlyphCount());

            Assert.IsTrue(font.GetGlyph('A', FontSize, LargeOutlineEm, out var firstLargeGlyph, out var firstLargeMaterial));
            Assert.AreEqual(2, font.GetCachedDynamicPageCount(),
                "A different range tier should allocate exactly one additional atlas page.");
            Assert.AreEqual(2, font.GetCachedDynamicGlyphCount(),
                "The same codepoint should retain one cached mapping per range tier.");
            Assert.AreNotSame(firstSmallMaterial, firstLargeMaterial);
            Assert.AreNotSame(firstSmallMaterial.mainTexture, firstLargeMaterial.mainTexture);

            int pageCountAfterWarmup = font.GetCachedDynamicPageCount();
            int glyphCountAfterWarmup = font.GetCachedDynamicGlyphCount();

            Assert.IsTrue(font.GetGlyph('A', FontSize, SmallOutlineEm, out var secondSmallGlyph, out var secondSmallMaterial));
            Assert.IsTrue(font.GetGlyph('A', FontSize, LargeOutlineEm, out var secondLargeGlyph, out var secondLargeMaterial));

            Assert.AreEqual(pageCountAfterWarmup, font.GetCachedDynamicPageCount(),
                "Returning to a warm range tier must not allocate another atlas page.");
            Assert.AreEqual(glyphCountAfterWarmup, font.GetCachedDynamicGlyphCount(),
                "Returning to a warm range tier must not create another glyph mapping.");
            Assert.AreSame(firstSmallMaterial, secondSmallMaterial);
            Assert.AreSame(firstLargeMaterial, secondLargeMaterial);
            Assert.AreSame(firstSmallMaterial, font.GetMaterial('A', FontSize, SmallOutlineEm));
            Assert.AreSame(firstLargeMaterial, font.GetMaterial('A', FontSize, LargeOutlineEm));
            AssertGlyphBoundsEqual(firstSmallGlyph, secondSmallGlyph, "small range tier");
            AssertGlyphBoundsEqual(firstLargeGlyph, secondLargeGlyph, "large range tier");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
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

    static void AssertGlyphBoundsEqual(
        NowFontAtlasInfo.Glyph expected,
        NowFontAtlasInfo.Glyph actual,
        string rangeTier)
    {
        Assert.AreEqual(expected.planeBounds.left, actual.planeBounds.left, 0.0001f, rangeTier);
        Assert.AreEqual(expected.planeBounds.bottom, actual.planeBounds.bottom, 0.0001f, rangeTier);
        Assert.AreEqual(expected.planeBounds.right, actual.planeBounds.right, 0.0001f, rangeTier);
        Assert.AreEqual(expected.planeBounds.top, actual.planeBounds.top, 0.0001f, rangeTier);
        Assert.AreEqual(expected.atlasBounds.left, actual.atlasBounds.left, 0.0001f, rangeTier);
        Assert.AreEqual(expected.atlasBounds.bottom, actual.atlasBounds.bottom, 0.0001f, rangeTier);
        Assert.AreEqual(expected.atlasBounds.right, actual.atlasBounds.right, 0.0001f, rangeTier);
        Assert.AreEqual(expected.atlasBounds.top, actual.atlasBounds.top, 0.0001f, rangeTier);
    }
}
