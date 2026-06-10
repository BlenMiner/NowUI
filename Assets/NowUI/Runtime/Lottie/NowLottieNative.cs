using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NowUIInternal
{
    /// <summary>
    /// Bindings for the nowui-vg native tessellator. When the native library is
    /// missing (or not yet built for the platform) <see cref="available"/> turns
    /// false and the renderer transparently uses the managed tessellator instead.
    ///
    /// WebGL links statically (__Internal), so the nowui-vg.bc plugin must be present
    /// in the project when building for WebGL — a missing library fails the IL2CPP
    /// link rather than falling back at runtime.
    ///
    /// Define the NOWUI_VG_DISABLE_NATIVE scripting symbol to compile the bindings
    /// out entirely and always use the managed tessellator.
    /// </summary>
    public static class NowLottieNative
    {
#if NOWUI_VG_DISABLE_NATIVE
        public static bool available => false;

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
#if UNITY_WEBGL && !UNITY_EDITOR
        const string LIBRARY_NAME = "__Internal";
#else
        const string LIBRARY_NAME = "nowui-vg";
#endif

        static bool _probed;

        static bool _available;

        static float[] _paintBuffer = new float[64];

        static readonly float[] _bounds = new float[4];

        public static bool available
        {
            get
            {
                if (!_probed)
                {
                    _probed = true;

                    try
                    {
                        _available = nowui_vg_version() >= 1;
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

            int result = nowui_vg_copy(
                buffer.positions.array,
                buffer.colors.array,
                buffer.indices.array,
                buffer.positions.array.Length,
                buffer.indices.array.Length);

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
        static extern int nowui_vg_copy(
            [Out] Vector2[] positions,
            [Out] Vector4[] colors,
            [Out] int[] indices,
            int vertexCapacity,
            int indexCapacity);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_vg_version();
#endif
    }
}
