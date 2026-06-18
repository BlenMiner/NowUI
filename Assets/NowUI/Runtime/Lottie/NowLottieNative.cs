using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>
    /// Bindings for the nowui-vg native tessellator. When the native library is
    /// missing (or not yet built for the platform) <see cref="available"/> turns
    /// false and the renderer transparently uses the managed tessellator instead.
    ///
    /// WebGL and iOS link statically (__Internal), so the nowui-vg.bc / libnowui-vg.a
    /// plugin must be present in the project when building for those platforms — a
    /// missing library fails the IL2CPP link rather than falling back at runtime.
    ///
    /// Define the NOWUI_VG_DISABLE_NATIVE scripting symbol to compile the bindings
    /// out entirely and always use the managed tessellator.
    /// </summary>
    public static class NowLottieNative
    {
        /// <summary>
        /// Forces the managed tessellator even when the native library is present.
        /// Intended for profiling comparisons and debugging.
        /// </summary>
        public static bool forceManagedTessellation;

        /// <summary>
        /// Forces the managed vertex copy loops even when the native library supports
        /// them. Intended for profiling comparisons and debugging.
        /// </summary>
        public static bool forceManagedCopy;

#if NOWUI_VG_DISABLE_NATIVE
        public static bool available => false;

        public static bool tessellationAvailable => false;

        public static bool blitAvailable => false;

        public static bool packCanvasAvailable => false;

        public static bool packRenderAvailable => false;

        public static bool textBlitAvailable => false;

        public static void PackCanvas(
            Vector3[] srcVerts,
            Vector2[] srcUvs,
            Vector4[] srcRadius,
            Vector4[] srcRawUv,
            Vector4[] srcColors,
            Vector4[] srcRect,
            Vector4[] srcMask,
            Vector4[] srcExtra,
            Vector4[] srcOutline,
            int vertexCount,
            bool isText,
            Vector2 positionOffset,
            NowCanvasVertex[] destination,
            int destinationBase) { }

        public static void PackRender(
            Vector3[] srcVerts,
            Vector2[] srcUvs,
            Vector4[] srcRadius,
            Vector4[] srcRawUv,
            Vector4[] srcColors,
            Vector4[] srcRect,
            Vector4[] srcMask,
            Vector4[] srcExtra,
            Vector4[] srcOutline,
            int vertexCount,
            Vector2 positionOffset,
            NowRenderVertex[] destination,
            int destinationBase) { }

        internal static void BlitTextRun(
            NowFont.PreparedTextGlyph[] glyphs,
            int start,
            int end,
            float x,
            float y,
            float fontSize,
            float baseline,
            Vector4 mask,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float pixelRange,
            Vector3[] dstVerts,
            Vector2[] dstUvs,
            Vector4[] dstRawUv,
            Vector4[] dstRect,
            Vector4[] dstRadius,
            Vector4[] dstColor,
            Vector4[] dstOutline,
            Vector4[] dstExtra,
            Vector4[] dstMask,
            int dstVertexBase,
            int[] dstIndices,
            int dstIndexBase,
            out float penX,
            out int vertexCount,
            out int indexCount,
            out Vector4 bounds)
        {
            penX = x;
            vertexCount = 0;
            indexCount = 0;
            bounds = default;
        }

        public static void BlitMesh(
            NowLottieDrawBuffer buffer,
            float positionScale,
            Vector2 positionOffset,
            Vector4 tint,
            Vector4 mask,
            Vector4 rect,
            Vector3[] dstVerts,
            Vector2[] dstUvs,
            Vector4[] dstRawUv,
            Vector4[] dstRect,
            Vector4[] dstRadius,
            Vector4[] dstColor,
            Vector4[] dstOutline,
            Vector4[] dstExtra,
            Vector4[] dstMask,
            int dstVertexBase,
            int[] dstIndices,
            int dstIndexBase,
            int indexOffset) { }

        public static void Begin(float flattenTolerance, float aaWidth) { }

        public static void Fill(
            NowLottieContourSet contours,
            NowLottieContourSet clip,
            bool clipInvert,
            in NowLottiePaint paint,
            float gradientSpan,
            bool evenOdd,
            in NowLottieTrimInfo trim) { }

        public static void Stroke(
            NowLottieContourSet contours,
            NowLottieContourSet clip,
            bool clipInvert,
            in NowLottiePaint paint,
            float width,
            int cap,
            int join,
            in NowLottieTrimInfo trim) { }

        public static bool Finish(NowLottieDrawBuffer buffer) => false;
#else
#if (UNITY_WEBGL || UNITY_IOS) && !UNITY_EDITOR
        const string LIBRARY_NAME = "__Internal";
#else
        const string LIBRARY_NAME = "nowui-vg";
#endif

        static bool _probed;

        static bool _available;

        static int _version;

        static float[] _paintBuffer = new float[64];

        static readonly float[] _bounds = new float[4];

        static readonly float[] _tintScratch = new float[4];

        static readonly float[] _maskScratch = new float[4];

        static readonly float[] _rectScratch = new float[4];

        public static bool available
        {
            get
            {
                if (forceManagedTessellation)
                    return false;

                if (!_probed)
                {
                    _probed = true;

                    try
                    {
                        _version = nowui_vg_version();
                        _available = _version >= 1;
                    }
                    catch (DllNotFoundException)
                    {
                        _available = false;
                    }
                    catch (EntryPointNotFoundException)
                    {
                        _available = false;
                    }
                }

                return _available;
            }
        }

        /// <summary>Lottie tessellation duplicate-point cleanup was added in version 6.</summary>
        public static bool tessellationAvailable => available && _version >= 6;

        /// <summary>The bulk vertex copy entry points were added in version 2.</summary>
        public static bool blitAvailable => !forceManagedCopy && available && _version >= 2;

        /// <summary>The interleaved canvas vertex pack was added in version 3.</summary>
        public static bool packCanvasAvailable => !forceManagedCopy && available && _version >= 3;

        /// <summary>The interleaved render vertex pack was added in version 4.</summary>
        public static bool packRenderAvailable => !forceManagedCopy && available && _version >= 4;

        /// <summary>The prepared shaped text run blitter was added in version 5.</summary>
        public static bool textBlitAvailable => !forceManagedCopy && available && _version >= 5;

        public static unsafe void PackCanvas(
            Vector3[] srcVerts,
            Vector2[] srcUvs,
            Vector4[] srcRadius,
            Vector4[] srcRawUv,
            Vector4[] srcColors,
            Vector4[] srcRect,
            Vector4[] srcMask,
            Vector4[] srcExtra,
            Vector4[] srcOutline,
            int vertexCount,
            bool isText,
            Vector2 positionOffset,
            NowCanvasVertex[] destination,
            int destinationBase)
        {
            fixed (Vector3* verts = srcVerts)
            fixed (Vector2* uvs = srcUvs)
            fixed (Vector4* radius = srcRadius)
            fixed (Vector4* rawUv = srcRawUv)
            fixed (Vector4* colors = srcColors)
            fixed (Vector4* rect = srcRect)
            fixed (Vector4* mask = srcMask)
            fixed (Vector4* extra = srcExtra)
            fixed (Vector4* outline = srcOutline)
            fixed (NowCanvasVertex* output = destination)
            {
                nowui_vg_pack_canvas(
                    (float*)verts,
                    (float*)uvs,
                    (float*)radius,
                    (float*)rawUv,
                    (float*)colors,
                    (float*)rect,
                    (float*)mask,
                    (float*)extra,
                    (float*)outline,
                    vertexCount,
                    isText ? 1 : 0,
                    positionOffset.x,
                    positionOffset.y,
                    (float*)output,
                    destinationBase);
            }
        }

        public static unsafe void PackRender(
            Vector3[] srcVerts,
            Vector2[] srcUvs,
            Vector4[] srcRadius,
            Vector4[] srcRawUv,
            Vector4[] srcColors,
            Vector4[] srcRect,
            Vector4[] srcMask,
            Vector4[] srcExtra,
            Vector4[] srcOutline,
            int vertexCount,
            Vector2 positionOffset,
            NowRenderVertex[] destination,
            int destinationBase)
        {
            fixed (Vector3* verts = srcVerts)
            fixed (Vector2* uvs = srcUvs)
            fixed (Vector4* radius = srcRadius)
            fixed (Vector4* rawUv = srcRawUv)
            fixed (Vector4* colors = srcColors)
            fixed (Vector4* rect = srcRect)
            fixed (Vector4* mask = srcMask)
            fixed (Vector4* extra = srcExtra)
            fixed (Vector4* outline = srcOutline)
            fixed (NowRenderVertex* output = destination)
            {
                nowui_vg_pack_render(
                    (float*)verts,
                    (float*)uvs,
                    (float*)radius,
                    (float*)rawUv,
                    (float*)colors,
                    (float*)rect,
                    (float*)mask,
                    (float*)extra,
                    (float*)outline,
                    vertexCount,
                    positionOffset.x,
                    positionOffset.y,
                    (float*)output,
                    destinationBase);
            }
        }

        internal static unsafe void BlitTextRun(
            NowFont.PreparedTextGlyph[] glyphs,
            int start,
            int end,
            float x,
            float y,
            float fontSize,
            float baseline,
            Vector4 mask,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float pixelRange,
            Vector3[] dstVerts,
            Vector2[] dstUvs,
            Vector4[] dstRawUv,
            Vector4[] dstRect,
            Vector4[] dstRadius,
            Vector4[] dstColor,
            Vector4[] dstOutline,
            Vector4[] dstExtra,
            Vector4[] dstMask,
            int dstVertexBase,
            int[] dstIndices,
            int dstIndexBase,
            out float penX,
            out int vertexCount,
            out int indexCount,
            out Vector4 bounds)
        {
            float* mask4 = stackalloc float[4] { mask.x, mask.y, mask.z, mask.w };
            float* color4 = stackalloc float[4] { color.x, color.y, color.z, color.w };
            float* outline4 = stackalloc float[4] { outlineColor.x, outlineColor.y, outlineColor.z, outlineColor.w };
            float* outPenX = stackalloc float[1];
            int* outCounts = stackalloc int[2];
            float* outBounds = stackalloc float[4];

            fixed (NowFont.PreparedTextGlyph* srcGlyphs = glyphs)
            fixed (Vector3* verts = dstVerts)
            fixed (Vector2* uvs = dstUvs)
            fixed (Vector4* rawUv = dstRawUv)
            fixed (Vector4* rect = dstRect)
            fixed (Vector4* radius = dstRadius)
            fixed (Vector4* dstColorPtr = dstColor)
            fixed (Vector4* dstOutlinePtr = dstOutline)
            fixed (Vector4* extra = dstExtra)
            fixed (Vector4* masks = dstMask)
            fixed (int* indices = dstIndices)
            {
                nowui_vg_blit_text_run(
                    (float*)srcGlyphs,
                    start,
                    end,
                    x,
                    y,
                    fontSize,
                    baseline,
                    mask4,
                    color4,
                    outline4,
                    outline,
                    pixelRange,
                    (float*)verts,
                    (float*)uvs,
                    (float*)rawUv,
                    (float*)rect,
                    (float*)radius,
                    (float*)dstColorPtr,
                    (float*)dstOutlinePtr,
                    (float*)extra,
                    (float*)masks,
                    dstVertexBase,
                    indices,
                    dstIndexBase,
                    outPenX,
                    outCounts,
                    outBounds);
            }

            penX = outPenX[0];
            vertexCount = outCounts[0];
            indexCount = outCounts[1];
            bounds = new Vector4(outBounds[0], outBounds[1], outBounds[2], outBounds[3]);
        }

        /// <summary>
        /// Bulk copy through raw pointers: passing the (large, capacity-sized) managed
        /// arrays through the marshaler made Mono copy them in and out on every call,
        /// which was orders of magnitude slower than the copy being replaced. The
        /// fixed blocks pin in place with zero copies.
        /// </summary>
        public static unsafe void BlitMesh(
            NowLottieDrawBuffer buffer,
            float positionScale,
            Vector2 positionOffset,
            Vector4 tint,
            Vector4 mask,
            Vector4 rect,
            Vector3[] dstVerts,
            Vector2[] dstUvs,
            Vector4[] dstRawUv,
            Vector4[] dstRect,
            Vector4[] dstRadius,
            Vector4[] dstColor,
            Vector4[] dstOutline,
            Vector4[] dstExtra,
            Vector4[] dstMask,
            int dstVertexBase,
            int[] dstIndices,
            int dstIndexBase,
            int indexOffset)
        {
            float* tint4 = stackalloc float[4] { tint.x, tint.y, tint.z, tint.w };
            float* mask4 = stackalloc float[4] { mask.x, mask.y, mask.z, mask.w };
            float* rect4 = stackalloc float[4] { rect.x, rect.y, rect.z, rect.w };

            fixed (Vector2* srcPositions = buffer.positions.array)
            fixed (Vector4* srcColors = buffer.colors.array)
            fixed (int* srcIndices = buffer.indices.array)
            fixed (Vector3* verts = dstVerts)
            fixed (Vector2* uvs = dstUvs)
            fixed (Vector4* rawUv = dstRawUv)
            fixed (Vector4* rects = dstRect)
            fixed (Vector4* radius = dstRadius)
            fixed (Vector4* color = dstColor)
            fixed (Vector4* outline = dstOutline)
            fixed (Vector4* extra = dstExtra)
            fixed (Vector4* masks = dstMask)
            fixed (int* indices = dstIndices)
            {
                nowui_vg_blit_mesh(
                    (float*)srcPositions,
                    (float*)srcColors,
                    buffer.positions.count,
                    srcIndices,
                    buffer.indices.count,
                    positionScale,
                    positionOffset.x,
                    positionOffset.y,
                    tint4,
                    mask4,
                    rect4,
                    (float*)verts,
                    (float*)uvs,
                    (float*)rawUv,
                    (float*)rects,
                    (float*)radius,
                    (float*)color,
                    (float*)outline,
                    (float*)extra,
                    (float*)masks,
                    dstVertexBase,
                    indices,
                    dstIndexBase,
                    indexOffset);
            }
        }

        public static void Begin(float flattenTolerance, float aaWidth)
        {
            nowui_vg_begin(flattenTolerance, aaWidth);
        }

        public static void Fill(
            NowLottieContourSet contours,
            NowLottieContourSet clip,
            bool clipInvert,
            in NowLottiePaint paint,
            float gradientSpan,
            bool evenOdd,
            in NowLottieTrimInfo trim)
        {
            if (contours == null || contours.isEmpty)
                return;

            int paintFloatCount = PackPaint(paint, gradientSpan);
            bool hasClip = clip != null && !clip.isEmpty;

            nowui_vg_fill(
                contours.data.array,
                contours.data.count,
                contours.contourCount,
                hasClip ? clip.data.array : null,
                hasClip ? clip.data.count : 0,
                hasClip ? clip.contourCount : 0,
                clipInvert ? 1 : 0,
                _paintBuffer,
                paintFloatCount,
                evenOdd ? 1 : 0,
                trim.active ? 1 : 0,
                trim.start,
                trim.end,
                trim.offset,
                trim.individual ? 1 : 0);
        }

        public static void Stroke(
            NowLottieContourSet contours,
            NowLottieContourSet clip,
            bool clipInvert,
            in NowLottiePaint paint,
            float width,
            int cap,
            int join,
            in NowLottieTrimInfo trim)
        {
            if (contours == null || contours.isEmpty)
                return;

            int paintFloatCount = PackPaint(paint, 0f);
            bool hasClip = clip != null && !clip.isEmpty;

            nowui_vg_stroke(
                contours.data.array,
                contours.data.count,
                contours.contourCount,
                hasClip ? clip.data.array : null,
                hasClip ? clip.data.count : 0,
                hasClip ? clip.contourCount : 0,
                clipInvert ? 1 : 0,
                _paintBuffer,
                paintFloatCount,
                width,
                cap,
                join,
                trim.active ? 1 : 0,
                trim.start,
                trim.end,
                trim.offset,
                trim.individual ? 1 : 0);
        }

        /// <summary>Copies the accumulated frame geometry into the draw buffer.</summary>
        public static bool Finish(NowLottieDrawBuffer buffer)
        {
            if (nowui_vg_end(out int vertexCount, out int indexCount, _bounds) != 0)
                return false;

            if (vertexCount <= 0 || indexCount <= 0)
                return false;

            buffer.positions.EnsureCapacity(vertexCount);
            buffer.colors.EnsureCapacity(vertexCount);
            buffer.indices.EnsureCapacity(indexCount);

            int result;

            unsafe
            {
                fixed (Vector2* positions = buffer.positions.array)
                fixed (Vector4* colors = buffer.colors.array)
                fixed (int* indices = buffer.indices.array)
                {
                    result = nowui_vg_copy(
                        (float*)positions,
                        (float*)colors,
                        indices,
                        buffer.positions.array.Length,
                        buffer.indices.array.Length);
                }
            }

            if (result != 0)
                return false;

            buffer.positions.count = vertexCount;
            buffer.colors.count = vertexCount;
            buffer.indices.count = indexCount;
            buffer.boundsMin = new Vector2(_bounds[0], _bounds[1]);
            buffer.boundsMax = new Vector2(_bounds[2], _bounds[3]);
            buffer.hasBounds = true;
            return true;
        }

        static int PackPaint(in NowLottiePaint paint, float gradientSpan)
        {
            int stopFloatCount = 0;

            if (paint.isGradient && paint.gradientStops != null)
                stopFloatCount = Mathf.Min(paint.gradientStopDataLength, paint.gradientStops.Length);

            int total = 13 + stopFloatCount;

            if (_paintBuffer.Length < total)
                _paintBuffer = new float[Mathf.NextPowerOfTwo(total)];

            var buffer = _paintBuffer;
            buffer[0] = paint.isGradient ? paint.gradientType : 0;
            buffer[1] = paint.color.x;
            buffer[2] = paint.color.y;
            buffer[3] = paint.color.z;
            buffer[4] = paint.color.w;
            buffer[5] = paint.alphaMultiplier;
            buffer[6] = paint.gradientStart.x;
            buffer[7] = paint.gradientStart.y;
            buffer[8] = paint.gradientEnd.x;
            buffer[9] = paint.gradientEnd.y;
            buffer[10] = paint.colorStopCount;
            buffer[11] = stopFloatCount;
            buffer[12] = gradientSpan;

            for (int i = 0; i < stopFloatCount; ++i)
                buffer[13 + i] = paint.gradientStops[i];

            return total;
        }

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern void nowui_vg_begin(float flattenTolerance, float aaWidth);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_vg_fill(
            float[] contours,
            int contourFloatCount,
            int contourCount,
            float[] clipContours,
            int clipFloatCount,
            int clipContourCount,
            int clipInvert,
            float[] paint,
            int paintFloatCount,
            int evenOdd,
            int hasTrim,
            float trimStart,
            float trimEnd,
            float trimOffset,
            int trimIndividual);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_vg_stroke(
            float[] contours,
            int contourFloatCount,
            int contourCount,
            float[] clipContours,
            int clipFloatCount,
            int clipContourCount,
            int clipInvert,
            float[] paint,
            int paintFloatCount,
            float width,
            int cap,
            int join,
            int hasTrim,
            float trimStart,
            float trimEnd,
            float trimOffset,
            int trimIndividual);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_vg_end(out int vertexCount, out int indexCount, [Out] float[] bounds);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern unsafe int nowui_vg_copy(
            float* positions,
            float* colors,
            int* indices,
            int vertexCapacity,
            int indexCapacity);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern unsafe void nowui_vg_blit_mesh(
            float* srcPositions,
            float* srcColors,
            int vertexCount,
            int* srcIndices,
            int indexCount,
            float positionScale,
            float offsetX,
            float offsetY,
            float* tint4,
            float* mask4,
            float* rect4,
            float* dstVerts,
            float* dstUvs,
            float* dstRawUv,
            float* dstRect,
            float* dstRadius,
            float* dstColor,
            float* dstOutline,
            float* dstExtra,
            float* dstMask,
            int dstVertexBase,
            int* dstIndices,
            int dstIndexBase,
            int indexOffset);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern unsafe void nowui_vg_blit_text_run(
            float* glyphs,
            int start,
            int end,
            float x,
            float y,
            float fontSize,
            float baseline,
            float* mask4,
            float* color4,
            float* outline4,
            float outline,
            float pixelRange,
            float* dstVerts,
            float* dstUvs,
            float* dstRawUv,
            float* dstRect,
            float* dstRadius,
            float* dstColor,
            float* dstOutline,
            float* dstExtra,
            float* dstMask,
            int dstVertexBase,
            int* dstIndices,
            int dstIndexBase,
            float* outPenX,
            int* outCounts,
            float* outBounds);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern unsafe void nowui_vg_pack_canvas(
            float* srcVerts,
            float* srcUvs,
            float* srcRadius,
            float* srcRawUv,
            float* srcColors,
            float* srcRect,
            float* srcMask,
            float* srcExtra,
            float* srcOutline,
            int vertexCount,
            int isText,
            float offsetX,
            float offsetY,
            float* dst,
            int dstVertexBase);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern unsafe void nowui_vg_pack_render(
            float* srcVerts,
            float* srcUvs,
            float* srcRadius,
            float* srcRawUv,
            float* srcColors,
            float* srcRect,
            float* srcMask,
            float* srcExtra,
            float* srcOutline,
            int vertexCount,
            float offsetX,
            float offsetY,
            float* dst,
            int dstVertexBase);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_vg_version();
#endif
    }
}
