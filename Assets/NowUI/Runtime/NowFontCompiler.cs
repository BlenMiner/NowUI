using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public static class NowFontCompiler
{
    const int ATLAS_SIZE = 64;
    const int PIXEL_RANGE = 16;
    const int ERROR_CAPACITY = 4096;
    const int NATIVE_OK = 0;
    const int NATIVE_BUFFER_TOO_SMALL = 2;

    public static bool usedCodepointFallback {get; private set;}

#if UNITY_WEBGL && !UNITY_EDITOR
    const string LIBRARY_NAME = "__Internal";
#else
    const string LIBRARY_NAME = "nowui-msdf";
#endif

    [StructLayout(LayoutKind.Sequential)]
    struct NativeMetrics
    {
        public float emSize;
        public float lineHeight;
        public float ascender;
        public float descender;
        public float underlineY;
        public float underlineThickness;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeGlyph
    {
        public uint unicode;
        public float advance;
        public float planeLeft;
        public float planeBottom;
        public float planeRight;
        public float planeTop;
        public float atlasLeft;
        public float atlasBottom;
        public float atlasRight;
        public float atlasTop;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeAtlasInfo
    {
        public int width;
        public int height;
        public int glyphCount;
        public int atlasByteCount;
        public float size;
        public float distanceRange;
        public NativeMetrics metrics;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeColorGlyph
    {
        public uint unicode;
        public uint glyphIndex;
        public float advance;
        public float planeLeft;
        public float planeBottom;
        public float planeRight;
        public float planeTop;
        public float atlasLeft;
        public float atlasBottom;
        public float atlasRight;
        public float atlasTop;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeColorAtlasInfo
    {
        public int width;
        public int height;
        public int glyphCount;
        public int atlasByteCount;
        public float size;
        public float lineHeight;
        public float ascender;
        public float descender;
    }

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    static extern int nowui_compile_font_from_memory(
        byte[] fontData,
        int fontDataLength,
        int size,
        int pixelRange,
        [Out] byte[] atlasRgba,
        int atlasRgbaLength,
        [Out] NativeGlyph[] glyphs,
        int glyphCapacity,
        ref NativeAtlasInfo info,
        [Out] byte[] errorBuffer,
        int errorBufferLength);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    static extern int nowui_compile_font_from_memory_with_codepoints(
        byte[] fontData,
        int fontDataLength,
        int size,
        int pixelRange,
        int[] codepoints,
        int codepointCount,
        [Out] byte[] atlasRgba,
        int atlasRgbaLength,
        [Out] NativeGlyph[] glyphs,
        int glyphCapacity,
        ref NativeAtlasInfo info,
        [Out] byte[] errorBuffer,
        int errorBufferLength);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    static extern int nowui_compile_color_font_from_memory(
        byte[] fontData,
        int fontDataLength,
        int size,
        [Out] byte[] atlasRgba,
        int atlasRgbaLength,
        [Out] NativeColorGlyph[] glyphs,
        int glyphCapacity,
        ref NativeColorAtlasInfo info,
        [Out] byte[] errorBuffer,
        int errorBufferLength);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    static extern int nowui_compile_color_font_from_memory_with_codepoints(
        byte[] fontData,
        int fontDataLength,
        int size,
        int[] codepoints,
        int codepointCount,
        [Out] byte[] atlasRgba,
        int atlasRgbaLength,
        [Out] NativeColorGlyph[] glyphs,
        int glyphCapacity,
        ref NativeColorAtlasInfo info,
        [Out] byte[] errorBuffer,
        int errorBufferLength);

    public static bool TryCompile(byte[] fontData, out NowFont font, out string error)
    {
        return TryCompile(fontData, ATLAS_SIZE, PIXEL_RANGE, (Material)null, out font, out error);
    }

    public static bool TryCompile(byte[] fontData, int size, int pixelRange, out NowFont font, out string error)
    {
        return TryCompile(fontData, size, pixelRange, (Material)null, out font, out error);
    }

    public static bool TryCompile(byte[] fontData, string extraCharacters, out NowFont font, out string error)
    {
        return TryCompile(fontData, ATLAS_SIZE, PIXEL_RANGE, extraCharacters, null, out font, out error);
    }

    public static bool TryCompile(
        byte[] fontData,
        int size,
        int pixelRange,
        string extraCharacters,
        out NowFont font,
        out string error)
    {
        return TryCompile(fontData, size, pixelRange, extraCharacters, null, out font, out error);
    }

    public static bool TryCompile(
        byte[] fontData,
        int size,
        int pixelRange,
        Material materialTemplate,
        out NowFont font,
        out string error)
    {
        return TryCompile(fontData, size, pixelRange, null, materialTemplate, out font, out error);
    }

    public static bool TryCompile(
        byte[] fontData,
        int size,
        int pixelRange,
        string extraCharacters,
        Material materialTemplate,
        out NowFont font,
        out string error)
    {
        font = null;

        if (fontData == null || fontData.Length == 0)
        {
            error = "Font data is empty.";
            return false;
        }

        if (size <= 0)
        {
            error = "Font atlas size must be greater than zero.";
            return false;
        }

        if (pixelRange <= 0)
        {
            error = "Font atlas pixel range must be greater than zero.";
            return false;
        }

        byte[] errorBuffer = new byte[ERROR_CAPACITY];
        NativeAtlasInfo info = default;
        int[] codepoints = CollectCodepoints(extraCharacters);
        usedCodepointFallback = false;

        if (ContainsColorGlyphTables(fontData))
            return TryCompileColorFont(fontData, size, codepoints, materialTemplate, out font, out error);

        try
        {
            int queryResult = CompileFont(
                fontData,
                fontData.Length,
                size,
                pixelRange,
                codepoints,
                null,
                0,
                null,
                0,
                ref info,
                errorBuffer,
                errorBuffer.Length);

            if (queryResult != NATIVE_BUFFER_TOO_SMALL)
            {
                error = NativeError(errorBuffer, "The native font compiler did not return atlas buffer sizing information.");
                if (usedCodepointFallback && codepoints != null && codepoints.Length > 0)
                    error = "The loaded NowUI native plugin does not support extra Unicode codepoint compilation. Rebuild/import the latest NowUI native plugins. ASCII fallback also failed: " + error;

                return false;
            }

            if (info.width <= 0 || info.height <= 0 || info.glyphCount <= 0 || info.atlasByteCount <= 0)
            {
                error = "The native font compiler returned invalid atlas sizing information.";
                if (usedCodepointFallback && codepoints != null && codepoints.Length > 0)
                    error = "The loaded NowUI native plugin does not support extra Unicode codepoint compilation. Rebuild/import the latest NowUI native plugins. ASCII fallback also failed: " + error;

                return false;
            }

            byte[] atlasRgba = new byte[info.atlasByteCount];
            var nativeGlyphs = new NativeGlyph[info.glyphCount];
            Array.Clear(errorBuffer, 0, errorBuffer.Length);

            int compileResult = CompileFont(
                fontData,
                fontData.Length,
                size,
                pixelRange,
                codepoints,
                atlasRgba,
                atlasRgba.Length,
                nativeGlyphs,
                nativeGlyphs.Length,
                ref info,
                errorBuffer,
                errorBuffer.Length);

            if (compileResult != NATIVE_OK)
            {
                error = NativeError(errorBuffer, "The native font compiler failed without an error message.");
                if (usedCodepointFallback && codepoints != null && codepoints.Length > 0)
                    error = "The loaded NowUI native plugin does not support extra Unicode codepoint compilation. Rebuild/import the latest NowUI native plugins. ASCII fallback also failed: " + error;

                return false;
            }

            font = CreateFont(atlasRgba, nativeGlyphs, info, materialTemplate, out error);
            return font != null;
        }
        catch (DllNotFoundException ex)
        {
            error = "NowUI native font compiler plugin was not found for this platform.\n" + ex.Message;
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = "NowUI native font compiler plugin is missing a required entry point. Rebuild/import the latest NowUI native plugins.\n" + ex.Message;
            return false;
        }
        catch (BadImageFormatException ex)
        {
            error = "NowUI native font compiler plugin has the wrong architecture for this platform.\n" + ex.Message;
            return false;
        }
    }

    static bool TryCompileColorFont(
        byte[] fontData,
        int size,
        int[] codepoints,
        Material materialTemplate,
        out NowFont font,
        out string error)
    {
        font = null;

        if (codepoints == null || codepoints.Length == 0)
        {
            error = "Color fonts require requested characters. Pass the emoji/text you want to include so NowUI does not import the entire color glyph set into a huge RGBA atlas.";
            return false;
        }

        byte[] errorBuffer = new byte[ERROR_CAPACITY];
        NativeColorAtlasInfo info = default;

        try
        {
            int queryResult = nowui_compile_color_font_from_memory_with_codepoints(
                fontData,
                fontData.Length,
                size,
                codepoints,
                codepoints.Length,
                null,
                0,
                null,
                0,
                ref info,
                errorBuffer,
                errorBuffer.Length);

            if (queryResult != NATIVE_BUFFER_TOO_SMALL)
            {
                error = NativeError(errorBuffer, "The native color font compiler did not return atlas buffer sizing information.");
                return false;
            }

            if (info.width <= 0 || info.height <= 0 || info.glyphCount <= 0 || info.atlasByteCount <= 0)
            {
                error = "The native color font compiler returned invalid atlas sizing information.";
                return false;
            }

            byte[] atlasRgba = new byte[info.atlasByteCount];
            var nativeGlyphs = new NativeColorGlyph[info.glyphCount];
            Array.Clear(errorBuffer, 0, errorBuffer.Length);

            int compileResult = nowui_compile_color_font_from_memory_with_codepoints(
                fontData,
                fontData.Length,
                size,
                codepoints,
                codepoints.Length,
                atlasRgba,
                atlasRgba.Length,
                nativeGlyphs,
                nativeGlyphs.Length,
                ref info,
                errorBuffer,
                errorBuffer.Length);

            if (compileResult != NATIVE_OK)
            {
                error = NativeError(errorBuffer, "The native color font compiler failed without an error message.");
                return false;
            }

            font = CreateColorFont(atlasRgba, nativeGlyphs, info, materialTemplate, out error);
            return font != null;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = "NowUI native font compiler plugin is missing filtered color font support. Rebuild/import the latest NowUI native plugins.\n" + ex.Message;
            return false;
        }
    }

    static int CompileFont(
        byte[] fontData,
        int fontDataLength,
        int size,
        int pixelRange,
        int[] codepoints,
        byte[] atlasRgba,
        int atlasRgbaLength,
        NativeGlyph[] glyphs,
        int glyphCapacity,
        ref NativeAtlasInfo info,
        byte[] errorBuffer,
        int errorBufferLength)
    {
        if (codepoints == null || codepoints.Length == 0)
        {
            return nowui_compile_font_from_memory(
                fontData,
                fontDataLength,
                size,
                pixelRange,
                atlasRgba,
                atlasRgbaLength,
                glyphs,
                glyphCapacity,
                ref info,
                errorBuffer,
                errorBufferLength);
        }

        try
        {
            return nowui_compile_font_from_memory_with_codepoints(
                fontData,
                fontDataLength,
                size,
                pixelRange,
                codepoints,
                codepoints.Length,
                atlasRgba,
                atlasRgbaLength,
                glyphs,
                glyphCapacity,
                ref info,
                errorBuffer,
                errorBufferLength);
        }
        catch (EntryPointNotFoundException)
        {
            usedCodepointFallback = true;
            return nowui_compile_font_from_memory(
                fontData,
                fontDataLength,
                size,
                pixelRange,
                atlasRgba,
                atlasRgbaLength,
                glyphs,
                glyphCapacity,
                ref info,
                errorBuffer,
                errorBufferLength);
        }
    }

    static int[] CollectCodepoints(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var codepoints = new HashSet<int>();

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint;

            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                codepoint = char.ConvertToUtf32(value[i], value[i + 1]);
                ++i;
            }
            else
            {
                codepoint = value[i];
            }

            if (codepoint > 0)
                codepoints.Add(codepoint);
        }

        if (codepoints.Count == 0)
            return null;

        int[] result = new int[codepoints.Count];
        codepoints.CopyTo(result);
        return result;
    }

    static bool ContainsColorGlyphTables(byte[] fontData)
    {
        return HasOpenTypeTable(fontData, "sbix") ||
            HasOpenTypeTable(fontData, "CBDT") ||
            HasOpenTypeTable(fontData, "CBLC") ||
            HasOpenTypeTable(fontData, "COLR") ||
            HasOpenTypeTable(fontData, "CPAL") ||
            HasOpenTypeTable(fontData, "SVG ");
    }

    static bool HasOpenTypeTable(byte[] fontData, string tableTag)
    {
        if (fontData == null || fontData.Length < 12 || string.IsNullOrEmpty(tableTag) || tableTag.Length != 4)
            return false;

        int tableCount = (fontData[4] << 8) | fontData[5];
        int tableDirectoryEnd = 12 + tableCount * 16;

        if (tableDirectoryEnd > fontData.Length)
            return false;

        byte tag0 = (byte)tableTag[0];
        byte tag1 = (byte)tableTag[1];
        byte tag2 = (byte)tableTag[2];
        byte tag3 = (byte)tableTag[3];

        for (int i = 0; i < tableCount; ++i)
        {
            int offset = 12 + i * 16;

            if (fontData[offset] == tag0 &&
                fontData[offset + 1] == tag1 &&
                fontData[offset + 2] == tag2 &&
                fontData[offset + 3] == tag3)
            {
                return true;
            }
        }

        return false;
    }

    static NowFont CreateFont(
        byte[] atlasRgba,
        NativeGlyph[] nativeGlyphs,
        NativeAtlasInfo info,
        Material materialTemplate,
        out string error)
    {
        if (materialTemplate == null)
            materialTemplate = Resources.Load<Material>("NowUI/TxtMaterial");

        if (materialTemplate == null)
        {
            error = "Failed to load NowUI text material template.";
            return null;
        }

        var texture = new Texture2D(info.width, info.height, TextureFormat.RGBA32, false, true)
        {
            name = "Font Atlas Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.LoadRawTextureData(atlasRgba);
        texture.Apply(false, true);

        var material = UnityEngine.Object.Instantiate(materialTemplate);
        material.name = "NowUI Font Material";
        material.mainTexture = texture;

        var font = ScriptableObject.CreateInstance<NowFont>();
        font.name = "NowUI Runtime Font";
        font.atlas = texture;
        font.material = material;
        font.atlasInfo = ToAtlasInfo(nativeGlyphs, info);

        error = null;
        return font;
    }

    static NowFont CreateColorFont(
        byte[] atlasRgba,
        NativeColorGlyph[] nativeGlyphs,
        NativeColorAtlasInfo info,
        Material materialTemplate,
        out string error)
    {
        if (materialTemplate == null)
            materialTemplate = Resources.Load<Material>("NowUI/TxtMaterialRGBA");

        if (materialTemplate == null)
        {
            error = "Failed to load NowUI RGBA text material template.";
            return null;
        }

        var texture = new Texture2D(info.width, info.height, TextureFormat.RGBA32, false, false)
        {
            name = "Color Font Atlas Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.LoadRawTextureData(atlasRgba);
        texture.Apply(false, true);

        var material = UnityEngine.Object.Instantiate(materialTemplate);
        material.name = "NowUI Color Font Material";
        material.mainTexture = texture;

        var font = ScriptableObject.CreateInstance<NowFont>();
        font.name = "NowUI Runtime Color Font";
        font.atlas = texture;
        font.material = material;
        font.atlasInfo = ToColorAtlasInfo(nativeGlyphs, info);

        error = null;
        return font;
    }

    static NowFontAtlasInfo ToAtlasInfo(NativeGlyph[] nativeGlyphs, NativeAtlasInfo info)
    {
        var atlasInfo = new NowFontAtlasInfo
        {
            atlas = new NowFontAtlasInfo.Atlas
            {
                type = NowFont.ATLAS_TYPE_MTSDF,
                distanceRange = Mathf.RoundToInt(info.distanceRange),
                size = Mathf.RoundToInt(info.size),
                width = info.width,
                height = info.height,
                yOrigin = "bottom"
            },
            metrics = new NowFontAtlasInfo.Metrics
            {
                emSize = info.metrics.emSize,
                lineHeight = info.metrics.lineHeight,
                ascender = info.metrics.ascender,
                descender = info.metrics.descender,
                underlineY = info.metrics.underlineY,
                underlineThickness = info.metrics.underlineThickness
            },
            glyphs = new NowFontAtlasInfo.Glyph[nativeGlyphs.Length]
        };

        for (int i = 0; i < nativeGlyphs.Length; ++i)
        {
            var nativeGlyph = nativeGlyphs[i];
            atlasInfo.glyphs[i] = new NowFontAtlasInfo.Glyph
            {
                unicode = unchecked((int)nativeGlyph.unicode),
                advance = nativeGlyph.advance,
                planeBounds = new NowFontAtlasInfo.Bounds
                {
                    left = nativeGlyph.planeLeft,
                    bottom = nativeGlyph.planeBottom,
                    right = nativeGlyph.planeRight,
                    top = nativeGlyph.planeTop
                },
                atlasBounds = new NowFontAtlasInfo.Bounds
                {
                    left = nativeGlyph.atlasLeft,
                    bottom = nativeGlyph.atlasBottom,
                    right = nativeGlyph.atlasRight,
                    top = nativeGlyph.atlasTop
                }
            };
        }

        return atlasInfo;
    }

    static NowFontAtlasInfo ToColorAtlasInfo(NativeColorGlyph[] nativeGlyphs, NativeColorAtlasInfo info)
    {
        var atlasInfo = new NowFontAtlasInfo
        {
            atlas = new NowFontAtlasInfo.Atlas
            {
                type = NowFont.ATLAS_TYPE_RGBA,
                distanceRange = 0,
                size = Mathf.RoundToInt(info.size),
                width = info.width,
                height = info.height,
                yOrigin = "top"
            },
            metrics = new NowFontAtlasInfo.Metrics
            {
                emSize = 1,
                lineHeight = info.lineHeight,
                ascender = info.ascender,
                descender = info.descender,
                underlineY = 0,
                underlineThickness = 0
            },
            glyphs = new NowFontAtlasInfo.Glyph[nativeGlyphs.Length]
        };

        for (int i = 0; i < nativeGlyphs.Length; ++i)
        {
            var nativeGlyph = nativeGlyphs[i];
            atlasInfo.glyphs[i] = new NowFontAtlasInfo.Glyph
            {
                unicode = unchecked((int)nativeGlyph.unicode),
                advance = nativeGlyph.advance,
                planeBounds = new NowFontAtlasInfo.Bounds
                {
                    left = nativeGlyph.planeLeft,
                    bottom = nativeGlyph.planeBottom,
                    right = nativeGlyph.planeRight,
                    top = nativeGlyph.planeTop
                },
                atlasBounds = new NowFontAtlasInfo.Bounds
                {
                    left = nativeGlyph.atlasLeft,
                    bottom = nativeGlyph.atlasBottom,
                    right = nativeGlyph.atlasRight,
                    top = nativeGlyph.atlasTop
                }
            };
        }

        return atlasInfo;
    }

    static string NativeError(byte[] errorBuffer, string fallback)
    {
        int length = Array.IndexOf(errorBuffer, (byte)0);
        if (length < 0)
            length = errorBuffer.Length;

        if (length == 0)
            return fallback;

        string error = Encoding.UTF8.GetString(errorBuffer, 0, length);
        return string.IsNullOrWhiteSpace(error) ? fallback : error;
    }
}
