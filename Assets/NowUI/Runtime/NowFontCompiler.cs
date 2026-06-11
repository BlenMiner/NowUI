using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NowUI
{
    public static class NowFontCompiler
    {
        const int ATLAS_SIZE = 64;
        const int PIXEL_RANGE = 16;
        const int ERROR_CAPACITY = 4096;
        const int NATIVE_OK = 0;
        const int NATIVE_BUFFER_TOO_SMALL = 2;
        const int NATIVE_ATLAS_FULL = 3;

        // WebGL and iOS link the plugin statically into the player, so the
        // binding resolves against the executable itself.
    #if (UNITY_WEBGL || UNITY_IOS) && !UNITY_EDITOR
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

        [StructLayout(LayoutKind.Sequential)]
        struct NativeSessionInfo
        {
            public float size;
            public float distanceRange;
            public NativeMetrics metrics;
        }

        const int SESSION_RESIZED = 1;

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_msdf_session_create(
            byte[] fontData,
            int fontDataLength,
            int size,
            int pixelRange,
            ref NativeSessionInfo info,
            out IntPtr session,
            [Out] byte[] errorBuffer,
            int errorBufferLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_msdf_session_create_fixed(
            byte[] fontData,
            int fontDataLength,
            int size,
            int pixelRange,
            int atlasSide,
            ref NativeSessionInfo info,
            out IntPtr session,
            [Out] byte[] errorBuffer,
            int errorBufferLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_msdf_session_add_glyphs(
            IntPtr session,
            int[] codepoints,
            int codepointCount,
            [Out] NativeGlyph[] glyphs,
            int glyphCapacity,
            out int glyphCount,
            out int atlasSide,
            out int changeFlags,
            [Out] byte[] errorBuffer,
            int errorBufferLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_msdf_session_copy_atlas(
            IntPtr session,
            [Out] byte[] atlasRgba,
            int atlasRgbaLength,
            [Out] byte[] errorBuffer,
            int errorBufferLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern void nowui_msdf_session_destroy(IntPtr session);

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

        internal static bool IsColorFont(byte[] fontData)
        {
            return ContainsColorGlyphTables(fontData);
        }

        /// <summary>
        /// Stateful incremental baking session backed by the native plugin. Keeps the parsed
        /// font and a fixed-size atlas alive across calls, so adding glyphs on demand costs
        /// only the SDF generation of the new glyphs instead of a full font re-parse per
        /// request. The atlas never resizes, so glyph atlas coordinates and the page texture
        /// stay valid for retained meshes; when the atlas is full, AddResult.AtlasFull tells
        /// the caller to seal the page and start a new session.
        /// Throws DllNotFoundException/EntryPointNotFoundException on creation when the native
        /// plugin predates the session API; callers fall back to the per-page compiler.
        /// </summary>
        internal sealed class DynamicSession : IDisposable
        {
            public enum AddResult
            {
                Ok,
                AtlasFull,
                Failed
            }

            IntPtr _handle;
            NativeGlyph[] _glyphScratch;
            readonly byte[] _errorBuffer = new byte[ERROR_CAPACITY];

            public int AtlasSide { get; }
            public float Size { get; }
            public float DistanceRange { get; }
            public NowFontAtlasInfo.Metrics Metrics { get; }

            DynamicSession(IntPtr handle, int atlasSide, in NativeSessionInfo info)
            {
                _handle = handle;
                AtlasSide = atlasSide;
                Size = info.size;
                DistanceRange = info.distanceRange;
                Metrics = ToMetrics(info.metrics);
            }

            ~DynamicSession()
            {
                ReleaseHandle();
            }

            public static bool TryCreate(byte[] fontData, int size, int pixelRange, int atlasSide, out DynamicSession session, out string error)
            {
                session = null;

                if (fontData == null || fontData.Length == 0)
                {
                    error = "Font data is empty.";
                    return false;
                }

                var errorBuffer = new byte[ERROR_CAPACITY];
                NativeSessionInfo info = default;

                int result = nowui_msdf_session_create_fixed(
                    fontData,
                    fontData.Length,
                    size,
                    pixelRange,
                    atlasSide,
                    ref info,
                    out IntPtr handle,
                    errorBuffer,
                    errorBuffer.Length);

                if (result != NATIVE_OK || handle == IntPtr.Zero)
                {
                    error = NativeError(errorBuffer, "The native font compiler failed to create a baking session.");
                    return false;
                }

                session = new DynamicSession(handle, atlasSide, info);
                error = null;
                return true;
            }

            public AddResult TryAddGlyphs(int[] codepoints, int codepointCount, List<NowFontAtlasInfo.Glyph> results, out string error)
            {
                if (_handle == IntPtr.Zero)
                {
                    error = "The baking session has been disposed.";
                    return AddResult.Failed;
                }

                if (codepoints == null || codepointCount <= 0)
                {
                    error = null;
                    return AddResult.Ok;
                }

                if (_glyphScratch == null || _glyphScratch.Length < codepointCount)
                    _glyphScratch = new NativeGlyph[Mathf.NextPowerOfTwo(codepointCount)];

                Array.Clear(_errorBuffer, 0, _errorBuffer.Length);

                int result = nowui_msdf_session_add_glyphs(
                    _handle,
                    codepoints,
                    codepointCount,
                    _glyphScratch,
                    _glyphScratch.Length,
                    out int glyphCount,
                    out _,
                    out _,
                    _errorBuffer,
                    _errorBuffer.Length);

                if (result == NATIVE_ATLAS_FULL)
                {
                    error = null;
                    return AddResult.AtlasFull;
                }

                if (result != NATIVE_OK)
                {
                    error = NativeError(_errorBuffer, "The native font compiler failed to add glyphs to the baking session.");
                    return AddResult.Failed;
                }

                for (int i = 0; i < glyphCount; ++i)
                    results.Add(ToGlyph(_glyphScratch[i]));

                error = null;
                return AddResult.Ok;
            }

            public bool TryCopyAtlas(ref byte[] buffer, out string error)
            {
                if (_handle == IntPtr.Zero)
                {
                    error = "The baking session has been disposed.";
                    return false;
                }

                int required = AtlasSide * AtlasSide * 4;

                if (required <= 0)
                {
                    error = "The baking session atlas is empty.";
                    return false;
                }

                if (buffer == null || buffer.Length != required)
                    buffer = new byte[required];

                Array.Clear(_errorBuffer, 0, _errorBuffer.Length);

                int result = nowui_msdf_session_copy_atlas(_handle, buffer, buffer.Length, _errorBuffer, _errorBuffer.Length);

                if (result != NATIVE_OK)
                {
                    error = NativeError(_errorBuffer, "The native font compiler failed to copy the session atlas.");
                    return false;
                }

                error = null;
                return true;
            }

            public void Dispose()
            {
                ReleaseHandle();
                GC.SuppressFinalize(this);
            }

            void ReleaseHandle()
            {
                if (_handle == IntPtr.Zero)
                    return;

                nowui_msdf_session_destroy(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public static bool TryCompile(byte[] fontData, out NowFont font, out string error)
        {
            return TryCompile(fontData, ATLAS_SIZE, PIXEL_RANGE, (Material)null, out font, out error);
        }

        public static bool TryCompile(byte[] fontData, int size, int pixelRange, out NowFont font, out string error)
        {
            return TryCompile(fontData, size, pixelRange, (Material)null, out font, out error);
        }

        public static bool TryCompile(
            byte[] fontData,
            int size,
            int pixelRange,
            Material materialTemplate,
            out NowFont font,
            out string error)
        {
            return CreateDynamicFont(fontData, size, pixelRange, materialTemplate, out font, out error);
        }

        internal static bool TryCompilePage(
            byte[] fontData,
            int size,
            int pixelRange,
            int[] codepoints,
            int codepointCount,
            Material materialTemplate,
            out NowFont font,
            out string error)
        {
            return TryCompileInternal(fontData, size, pixelRange, codepoints, codepointCount, materialTemplate, out font, out error);
        }

        static bool CreateDynamicFont(
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
                error = "Dynamic atlas size must be greater than zero.";
                return false;
            }

            if (pixelRange <= 0)
            {
                error = "Dynamic atlas pixel range must be greater than zero.";
                return false;
            }

            font = ScriptableObject.CreateInstance<NowFont>();
            font.name = "NowUI Runtime Font";
            font.InitializeDynamicSource(fontData, size, pixelRange, materialTemplate: materialTemplate);
            error = null;
            return true;
        }

        static bool TryCompileInternal(
            byte[] fontData,
            int size,
            int pixelRange,
            int[] requestedCodepoints,
            int requestedCodepointCount,
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
            int[] codepoints = requestedCodepoints;
            int codepointCount = requestedCodepoints != null ? Mathf.Clamp(requestedCodepointCount, 0, requestedCodepoints.Length) : 0;

            if (ContainsColorGlyphTables(fontData))
                return TryCompileColorFont(fontData, size, codepoints, codepointCount, materialTemplate, out font, out error);

            try
            {
                int queryResult = CompileFont(
                    fontData,
                    fontData.Length,
                    size,
                    pixelRange,
                    codepoints,
                    codepointCount,
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

                int compileResult = CompileFont(
                    fontData,
                    fontData.Length,
                    size,
                    pixelRange,
                    codepoints,
                    codepointCount,
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
            int codepointCount,
            Material materialTemplate,
            out NowFont font,
            out string error)
        {
            font = null;

            if (codepoints == null || codepointCount <= 0)
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
                    codepointCount,
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
                    codepointCount,
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
            int codepointCount,
            byte[] atlasRgba,
            int atlasRgbaLength,
            NativeGlyph[] glyphs,
            int glyphCapacity,
            ref NativeAtlasInfo info,
            byte[] errorBuffer,
            int errorBufferLength)
        {
            if (codepoints == null || codepointCount <= 0)
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

            return nowui_compile_font_from_memory_with_codepoints(
                fontData,
                fontDataLength,
                size,
                pixelRange,
                codepoints,
                codepointCount,
                atlasRgba,
                atlasRgbaLength,
                glyphs,
                glyphCapacity,
                ref info,
                errorBuffer,
                errorBufferLength);
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
                name = "NowUI Dynamic Font Atlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.LoadRawTextureData(atlasRgba);
            texture.Apply(false, false);

            var material = UnityEngine.Object.Instantiate(materialTemplate);
            material.name = "NowUI Dynamic Font Material";
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
                name = "NowUI Dynamic Color Font Atlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            FlipRgbaRows(atlasRgba, info.width, info.height);
            texture.LoadRawTextureData(atlasRgba);
            texture.Apply(false, false);

            var material = UnityEngine.Object.Instantiate(materialTemplate);
            material.name = "NowUI Dynamic Color Font Material";
            material.mainTexture = texture;

            var font = ScriptableObject.CreateInstance<NowFont>();
            font.name = "NowUI Runtime Color Font";
            font.atlas = texture;
            font.material = material;
            font.atlasInfo = ToColorAtlasInfo(nativeGlyphs, info);

            error = null;
            return font;
        }

        static void FlipRgbaRows(byte[] rgba, int width, int height)
        {
            int stride = width * 4;
            var row = new byte[stride];

            for (int y = 0; y < height / 2; ++y)
            {
                int top = y * stride;
                int bottom = (height - y - 1) * stride;

                Buffer.BlockCopy(rgba, top, row, 0, stride);
                Buffer.BlockCopy(rgba, bottom, rgba, top, stride);
                Buffer.BlockCopy(row, 0, rgba, bottom, stride);
            }
        }

        static NowFontAtlasInfo.Metrics ToMetrics(NativeMetrics metrics)
        {
            return new NowFontAtlasInfo.Metrics
            {
                emSize = metrics.emSize,
                lineHeight = metrics.lineHeight,
                ascender = metrics.ascender,
                descender = metrics.descender,
                underlineY = metrics.underlineY,
                underlineThickness = metrics.underlineThickness
            };
        }

        static NowFontAtlasInfo.Glyph ToGlyph(NativeGlyph nativeGlyph)
        {
            return new NowFontAtlasInfo.Glyph
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
                metrics = ToMetrics(info.metrics),
                glyphs = new NowFontAtlasInfo.Glyph[nativeGlyphs.Length]
            };

            for (int i = 0; i < nativeGlyphs.Length; ++i)
                atlasInfo.glyphs[i] = ToGlyph(nativeGlyphs[i]);

            return atlasInfo;
        }

        static NowFontAtlasInfo ToColorAtlasInfo(NativeColorGlyph[] nativeGlyphs, NativeColorAtlasInfo info)
        {
            float metricScale = info.lineHeight > 1.5f ? info.lineHeight : 1;

            var atlasInfo = new NowFontAtlasInfo
            {
                atlas = new NowFontAtlasInfo.Atlas
                {
                    type = NowFont.ATLAS_TYPE_RGBA,
                    distanceRange = 0,
                    size = Mathf.RoundToInt(info.size),
                    width = info.width,
                    height = info.height,
                    yOrigin = "bottom"
                },
                metrics = new NowFontAtlasInfo.Metrics
                {
                    emSize = 1,
                    lineHeight = info.lineHeight / metricScale,
                    ascender = info.ascender / metricScale,
                    descender = info.descender / metricScale,
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
                    advance = nativeGlyph.advance / metricScale,
                    planeBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = nativeGlyph.planeLeft / metricScale,
                        bottom = nativeGlyph.planeBottom / metricScale,
                        right = nativeGlyph.planeRight / metricScale,
                        top = nativeGlyph.planeTop / metricScale
                    },
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = nativeGlyph.atlasLeft,
                        bottom = info.height - nativeGlyph.atlasTop,
                        right = nativeGlyph.atlasRight,
                        top = info.height - nativeGlyph.atlasBottom
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
}
