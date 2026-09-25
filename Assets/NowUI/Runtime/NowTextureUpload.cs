using UnityEngine;
using Object = UnityEngine.Object;
#if !NOWUI_STANDALONE
using Unity.Collections;
#endif

namespace NowUI.Internal
{
    /// <summary>
    /// Partial uploads for readable RGBA32 textures that NowUI writes through
    /// <c>GetRawTextureData</c>, such as dynamic font pages. <see cref="Texture2D.Apply()"/>
    /// re-uploads a whole texture; font pages only ever change a band of rows at a
    /// time, so uploading that band keeps adding glyphs to a large page cheap.
    /// </summary>
    internal static class NowTextureUpload
    {
#if !NOWUI_STANDALONE
        /// <summary>Staging textures at most this large are kept for reuse.</summary>
        const int MAX_CACHED_STAGING_BYTES = 1024 * 1024;

        const int MIN_STAGING_ROWS = 32;

        static Texture2D s_staging;
#endif

        /// <summary>Zeroes the texture's CPU copy without uploading it.</summary>
        public static void Clear(Texture2D texture)
        {
            texture.GetRawTextureData<byte>().AsSpan().Clear();
        }

        /// <summary>
        /// Uploads rows <paramref name="y"/> to <paramref name="y"/> + <paramref name="rowCount"/>
        /// of mip 0 from the texture's CPU copy. The caller promises no other texels
        /// changed since the last upload. Falls back to a full <c>Apply</c> when the
        /// platform cannot copy texture regions or the band is most of the texture.
        /// </summary>
        public static void UploadRows(Texture2D texture, int y, int rowCount)
        {
            if (texture == null)
                return;

            y = Mathf.Clamp(y, 0, texture.height);
            rowCount = Mathf.Clamp(rowCount, 0, texture.height - y);

            if (rowCount <= 0)
                return;

#if NOWUI_STANDALONE
            texture.ApplyRows(y, rowCount);
#else
            if (rowCount * 2 > texture.height ||
                texture.format != TextureFormat.RGBA32 ||
                texture.mipmapCount != 1 ||
                (SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.Basic) == 0)
            {
                texture.Apply(false, false);
                return;
            }

            int width = texture.width;
            int rowBytes = width * 4;
            Texture2D staging = RentStaging(width, rowCount, out bool temporary);

            // Full-width rows are contiguous in both textures (row 0 is the bottom
            // row on both sides), so the band is a single copy.
            NativeArray<byte> source = texture.GetRawTextureData<byte>();
            NativeArray<byte> target = staging.GetRawTextureData<byte>();
            NativeArray<byte>.Copy(source, y * rowBytes, target, 0, rowCount * rowBytes);
            staging.Apply(false, false);
            Graphics.CopyTexture(staging, 0, 0, 0, 0, width, rowCount, texture, 0, 0, 0, y);

            if (temporary)
                Release(staging);
#endif
        }

#if !NOWUI_STANDALONE
        static Texture2D RentStaging(int width, int rowCount, out bool temporary)
        {
            int rows = Mathf.Max(MIN_STAGING_ROWS, Mathf.NextPowerOfTwo(rowCount));
            temporary = (long)width * rows * 4 > MAX_CACHED_STAGING_BYTES;

            if (!temporary &&
                s_staging != null &&
                s_staging.width == width &&
                s_staging.height >= rowCount)
            {
                return s_staging;
            }

            var staging = new Texture2D(width, rows, TextureFormat.RGBA32, 1, true, true)
            {
                name = "Now Texture Upload Staging",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!temporary)
            {
                Release(s_staging);
                s_staging = staging;
            }

            return staging;
        }

        static void Release(Texture2D texture)
        {
            if (texture == null)
                return;

            // Destruction is queued after the copy that reads it.
            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }
#endif
    }
}
