using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NowUI.Internal
{
    /// <summary>
    /// HarfBuzz shaping bindings (nowui-msdf v4+). A shaper is standalone — it only
    /// shapes text into glyph indices with em-unit positions and cluster mapping —
    /// so it composes with both the native and managed glyph bakers.
    ///
    /// When the plugin is missing or predates the shaping API,
    /// <see cref="supported"/> turns false and callers keep the per-codepoint text
    /// path: shaping is an enhancement (ligatures, ZWJ emoji, complex scripts),
    /// never a requirement.
    /// </summary>
    internal sealed class NowTextShaper : IDisposable
    {
        // WebGL and iOS link the plugin statically into the player, so the
        // binding resolves against the executable itself.
#if (UNITY_WEBGL || UNITY_IOS) && !UNITY_EDITOR
        const string LIBRARY_NAME = "__Internal";
#else
        const string LIBRARY_NAME = "nowui-msdf";
#endif

        const int NATIVE_OK = 0;
        const int NATIVE_BUFFER_TOO_SMALL = 2;
        const int ERROR_CAPACITY = 1024;

        [StructLayout(LayoutKind.Sequential)]
        public struct ShapedGlyph
        {
            public uint glyphIndex;

            /// <summary>UTF-16 code unit index into the shaped text.</summary>
            public uint cluster;

            /// <summary>Pen advance in em units; multiply by font size for pixels.</summary>
            public float xAdvance;

            public float yAdvance;

            /// <summary>Glyph placement offset from the pen position, em units.</summary>
            public float xOffset;

            public float yOffset;
        }

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern int nowui_shaper_create(
            byte[] fontData,
            int fontDataLength,
            out IntPtr shaper,
            [Out] byte[] errorBuffer,
            int errorBufferLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern int nowui_shaper_shape_utf16(
            IntPtr shaper,
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            int textLength,
            [Out] ShapedGlyph[] glyphs,
            int glyphCapacity,
            out int glyphCount,
            [Out] byte[] errorBuffer,
            int errorBufferLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        static extern void nowui_shaper_destroy(IntPtr shaper);

        static bool s_unsupported;

        IntPtr _handle;
        ShapedGlyph[] _glyphScratch = new ShapedGlyph[128];
        readonly byte[] _errorBuffer = new byte[ERROR_CAPACITY];

        /// <summary>
        /// False once the platform is known to lack the shaping API; callers skip
        /// shaper creation attempts entirely from then on.
        /// </summary>
        public static bool supported => !s_unsupported;

        NowTextShaper(IntPtr handle)
        {
            _handle = handle;
        }

        ~NowTextShaper()
        {
            ReleaseHandle();
        }

        public static bool TryCreate(byte[] fontData, out NowTextShaper shaper, out string error)
        {
            shaper = null;

            if (s_unsupported)
            {
                error = "Text shaping is not available on this platform.";
                return false;
            }

            if (fontData == null || fontData.Length == 0)
            {
                error = "Font data is empty.";
                return false;
            }

            var errorBuffer = new byte[ERROR_CAPACITY];

            try
            {
                int result = nowui_shaper_create(fontData, fontData.Length, out IntPtr handle, errorBuffer, errorBuffer.Length);

                if (result != NATIVE_OK || handle == IntPtr.Zero)
                {
                    error = NativeError(errorBuffer, "The native shaper failed to parse the font.");
                    return false;
                }

                shaper = new NowTextShaper(handle);
                error = null;
                return true;
            }
            catch (DllNotFoundException)
            {
                s_unsupported = true;
                error = "The NowUI native plugin was not found; text shaping is unavailable.";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                s_unsupported = true;
                error = "The NowUI native plugin predates the shaping API; rebuild/import the latest plugins.";
                return false;
            }
            catch (BadImageFormatException)
            {
                s_unsupported = true;
                error = "The NowUI native plugin has the wrong architecture for this platform.";
                return false;
            }
        }

        /// <summary>
        /// Shapes <paramref name="text"/> and appends the shaped glyphs to
        /// <paramref name="results"/>. Output is in visual order; clusters map each
        /// glyph back to the UTF-16 index of the character(s) it came from.
        /// </summary>
        public bool TryShape(string text, List<ShapedGlyph> results, out string error)
        {
            if (_handle == IntPtr.Zero)
            {
                error = "The shaper has been disposed.";
                return false;
            }

            if (string.IsNullOrEmpty(text))
            {
                error = null;
                return true;
            }

            using var profile = NowUIProfiler.TextShape.Auto();

            Array.Clear(_errorBuffer, 0, _errorBuffer.Length);

            int result = nowui_shaper_shape_utf16(
                _handle, text, text.Length, _glyphScratch, _glyphScratch.Length, out int glyphCount, _errorBuffer, _errorBuffer.Length);

            if (result == NATIVE_BUFFER_TOO_SMALL)
            {
                _glyphScratch = new ShapedGlyph[UnityEngine.Mathf.NextPowerOfTwo(glyphCount)];
                result = nowui_shaper_shape_utf16(
                    _handle, text, text.Length, _glyphScratch, _glyphScratch.Length, out glyphCount, _errorBuffer, _errorBuffer.Length);
            }

            if (result != NATIVE_OK)
            {
                error = NativeError(_errorBuffer, "The native shaper failed.");
                return false;
            }

            for (int i = 0; i < glyphCount; ++i)
                results.Add(_glyphScratch[i]);

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

            nowui_shaper_destroy(_handle);
            _handle = IntPtr.Zero;
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
