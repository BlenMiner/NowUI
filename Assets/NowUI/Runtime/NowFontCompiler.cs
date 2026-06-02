using System;
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

    public static bool TryCompile(byte[] fontData, out NowFont font, out string error)
    {
        return TryCompile(fontData, ATLAS_SIZE, PIXEL_RANGE, null, out font, out error);
    }

    public static bool TryCompile(byte[] fontData, int size, int pixelRange, out NowFont font, out string error)
    {
        return TryCompile(fontData, size, pixelRange, null, out font, out error);
    }

    public static bool TryCompile(
        byte[] fontData,
        int size,
        int pixelRange,
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

        try
        {
            int queryResult = nowui_compile_font_from_memory(
                fontData,
                fontData.Length,
                size,
                pixelRange,
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
                return false;
            }

            if (info.width <= 0 || info.height <= 0 || info.glyphCount <= 0 || info.atlasByteCount <= 0)
            {
                error = "The native font compiler returned invalid atlas sizing information.";
                return false;
            }

            byte[] atlasRgba = new byte[info.atlasByteCount];
            var nativeGlyphs = new NativeGlyph[info.glyphCount];
            Array.Clear(errorBuffer, 0, errorBuffer.Length);

            int compileResult = nowui_compile_font_from_memory(
                fontData,
                fontData.Length,
                size,
                pixelRange,
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
            error = "NowUI native font compiler plugin is missing the nowui_compile_font_from_memory entry point.\n" + ex.Message;
            return false;
        }
        catch (BadImageFormatException ex)
        {
            error = "NowUI native font compiler plugin has the wrong architecture for this platform.\n" + ex.Message;
            return false;
        }
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

    static NowFontAtlasInfo ToAtlasInfo(NativeGlyph[] nativeGlyphs, NativeAtlasInfo info)
    {
        var atlasInfo = new NowFontAtlasInfo
        {
            atlas = new NowFontAtlasInfo.Atlas
            {
                type = "mtsdf",
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
