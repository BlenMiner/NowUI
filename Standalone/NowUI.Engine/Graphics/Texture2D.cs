// Mirrors UnityEngine.Texture2D for the NowUI standalone build.
// Design: Docs/Standalone/StandaloneCoreDesign.md §3.5 (`Texture2D.cs` member list, the CPU-store and dirty-rect
// contract), §1.2 (the "keep the CPU copy when sealed" decision and the allocation rule), §4.1
// (INowRenderBackend.UploadTexture2D).
// Inventory: Docs/Standalone/UnityDependencyInventory.md §A, row `UnityEngine.Texture2D`.
//
// The CPU store is a **pinned** byte[] (GC.AllocateArray(pinned: true)) so that GetRawTextureData<T> can hand out a
// NativeArray view with no GCHandle: NowFont takes spans over it and NowFontCompiler copies whole atlases through it,
// both on paths that must not allocate or pin per call.
//
// Behaviours that look odd and are Unity-faithful:
//   * Row 0 is the BOTTOM row. Raw data, SetPixels32 and the sub-rect overload all place row 0 at the bottom, which is
//     what makes NowFontCompiler.FlipRgbaRows and NowGradient's row arithmetic (NowGradient.cs:607) come out right.
//     The framebuffer's own convention is the backend's problem, not this file's.
//   * The store holds the WHOLE mip chain, as Unity's does, so LoadRawTextureData's size check and
//     GetRawTextureData's length match Unity for a mipChain texture (NowFont.cs:2846 makes colour pages mip-chained).
//   * `Apply(_, makeNoLongerReadable: true)` clears isReadable but KEEPS the store unless
//     NowRuntime.releaseCpuCopiesOnSeal is set (design §1.2): WebGL context loss has to re-upload every font atlas
//     page, and NowFont.cs:3465-3471 seals every page it finishes.
//   * GetRawTextureData<T>() marks the whole texture dirty. Writes through the returned view are invisible to this
//     class, so the only conservative answer is "assume everything moved"; the byte[] overload copies and therefore
//     does not.
using System;
using NowUI.Engine;
using Unity.Collections;

namespace UnityEngine
{
    /// <summary>
    /// A CPU-backed 2D texture. Under Unity the pixels live in native memory with an optional CPU mirror; here the
    /// CPU store is the source of truth and the backend receives uploads from it.
    /// </summary>
    public sealed class Texture2D : Texture
    {
        // Lazily created 1x1 constants. Static fields rather than a dictionary: they are read on hot paths
        // (NowMaskShader.cs:371 uses blackTexture as the "no mask" binding) and there are exactly five of them.
        private static Texture2D s_Black;
        private static Texture2D s_White;
        private static Texture2D s_Gray;
        private static Texture2D s_Red;
        private static Texture2D s_Normal;

        private TextureFormat m_Format;
        private bool m_Linear;
        private int m_BytesPerPixel;

        /// <summary>The CPU store, pinned, holding every mip level back to back starting at level 0.</summary>
        internal byte[] pixels;

        /// <summary>
        /// Union of the level-0 regions written since the last <see cref="Apply()"/>. An empty rect means "nothing
        /// tracked", which <see cref="Apply()"/> widens to the full texture - Unity re-uploads everything anyway.
        /// </summary>
        internal RectInt dirtyRect;

        /// <summary>True while the CPU store has changes the backend has not seen.</summary>
        internal bool uploadPending;

        /// <summary>RGBA32 with a full mip chain, which is what Unity's two-argument constructor produces.</summary>
        public Texture2D(int width, int height)
            : this(width, height, TextureFormat.RGBA32, -1, false)
        {
        }

        public Texture2D(int width, int height, TextureFormat format, bool mipChain)
            : this(width, height, format, mipChain ? -1 : 1, false)
        {
        }

        public Texture2D(int width, int height, TextureFormat format, bool mipChain, bool linear)
            : this(width, height, format, mipChain ? -1 : 1, linear)
        {
        }

        /// <summary>
        /// The general constructor. <paramref name="mipCount"/> is Unity's: a negative value means "the full chain",
        /// and anything larger than the chain is clamped down to it rather than rejected.
        /// </summary>
        public Texture2D(int width, int height, TextureFormat format, int mipCount, bool linear)
        {
            Initialize(width, height, format, mipCount, linear);
        }

        /// <summary>
        /// Unity's uninitialized-texture overload. The shim's store is always zeroed (a pinned managed array), so
        /// <paramref name="createUninitialized"/> only mirrors the signature; nothing is uploaded until Apply either way.
        /// </summary>
        public Texture2D(int width, int height, TextureFormat format, int mipCount, bool linear, bool createUninitialized)
            : this(width, height, format, mipCount, linear)
        {
        }

        /// <summary>
        /// Texel width. The setter is deliberately inert: Unity refuses to resize a <c>Texture2D</c> through it (it
        /// logs "Setting texture width is not allowed" and keeps the old size), and honouring the write here would
        /// leave <c>width</c> disagreeing with the CPU store, so every subsequent row offset would be wrong.
        /// <see cref="Reinitialize(int,int)"/> is the resize.
        /// </summary>
        public override int width
        {
            get { return base.width; }
            set { }
        }

        /// <summary>Texel height. Inert for the same reason <see cref="width"/> is.</summary>
        public override int height
        {
            get { return base.height; }
            set { }
        }

        /// <summary>Pixel format. Read-only in Unity: changing it means <see cref="Reinitialize(int,int,TextureFormat,bool)"/>.</summary>
        public TextureFormat format
        {
            get { return m_Format; }
        }

        /// <summary>
        /// Whether the texels are linear rather than sRGB-encoded. NowUI passes <c>true</c> for every mask, ramp and
        /// SDF atlas it builds (NowValueControls, NowGradient, NowFont), so a backend must honour it.
        /// </summary>
        public bool isLinear
        {
            get { return m_Linear; }
        }

        /// <summary>1x1 opaque black.</summary>
        public static Texture2D blackTexture
        {
            get
            {
                if (s_Black == null)
                    s_Black = CreateConstant("UnityBlack", new Color32(0, 0, 0, 255));

                return s_Black;
            }
        }

        /// <summary>1x1 opaque white.</summary>
        public static Texture2D whiteTexture
        {
            get
            {
                if (s_White == null)
                    s_White = CreateConstant("UnityWhite", new Color32(255, 255, 255, 255));

                return s_White;
            }
        }

        /// <summary>1x1 opaque mid grey.</summary>
        public static Texture2D grayTexture
        {
            get
            {
                if (s_Gray == null)
                    s_Gray = CreateConstant("UnityGrey", new Color32(128, 128, 128, 255));

                return s_Gray;
            }
        }

        /// <summary>1x1 opaque red.</summary>
        public static Texture2D redTexture
        {
            get
            {
                if (s_Red == null)
                    s_Red = CreateConstant("UnityRed", new Color32(255, 0, 0, 255));

                return s_Red;
            }
        }

        /// <summary>
        /// 1x1 flat tangent-space normal. Unity's built-in is stored in whatever form the platform's normal-map
        /// encoding uses; the shim stores the uncompressed (0.5, 0.5, 1, 1) form, which is the one a GLSL backend can
        /// sample without a decode. Nothing in NowUI reads it.
        /// </summary>
        public static Texture2D normalTexture
        {
            get
            {
                if (s_Normal == null)
                    s_Normal = CreateConstant("UnityNormalMap", new Color32(128, 128, 255, 255));

                return s_Normal;
            }
        }

        /// <summary>
        /// An aliasing view over the CPU store. Writes through it are visible to <see cref="Apply()"/>, which is the
        /// whole point: NowFont clears and blits glyph rectangles through this span and then applies once.
        /// </summary>
        /// <exception cref="UnityException">The texture is no longer readable.</exception>
        public NativeArray<T> GetRawTextureData<T>() where T : struct
        {
            ThrowIfNotReadable("GetRawTextureData");

            int elementSize = NativeArray<T>.ElementSize;

            if (pixels.Length % elementSize != 0)
                throw new UnityException(
                    "GetRawTextureData: the " + pixels.Length + "-byte store is not a whole number of " +
                    typeof(T).Name + " elements.");

            // Conservative: a write through the view cannot be observed here, so the next Apply must upload it all.
            MarkFullyDirty();

            return NativeArray<T>.CreateView(pixels, 0, pixels.Length / elementSize);
        }

        /// <summary>A copy of the CPU store. Read-only by construction, so it does not dirty the texture.</summary>
        /// <exception cref="UnityException">The texture is no longer readable.</exception>
        public byte[] GetRawTextureData()
        {
            ThrowIfNotReadable("GetRawTextureData");

            var copy = new byte[pixels.Length];
            Buffer.BlockCopy(pixels, 0, copy, 0, pixels.Length);
            return copy;
        }

        /// <summary>
        /// Overwrites the whole store, mip levels included. Unity throws when the source is smaller than the store
        /// ("will result in overread") and silently ignores the tail when it is larger, and so does this.
        /// </summary>
        public void LoadRawTextureData(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            ThrowIfNotReadable("LoadRawTextureData");

            if (data.Length < pixels.Length)
                throw new UnityException(
                    "LoadRawTextureData: not enough data provided (will result in overread). Expected " +
                    pixels.Length + " bytes, got " + data.Length + ".");

            Buffer.BlockCopy(data, 0, pixels, 0, pixels.Length);
            MarkFullyDirty();
        }

        /// <summary>Overwrites the whole store from a native array of any blittable element type.</summary>
        public void LoadRawTextureData<T>(NativeArray<T> data) where T : struct
        {
            if (!data.IsCreated)
                throw new ArgumentException("The source array has not been created.", nameof(data));

            ThrowIfNotReadable("LoadRawTextureData");

            ReadOnlySpan<byte> source = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsReadOnlySpan());

            if (source.Length < pixels.Length)
                throw new UnityException(
                    "LoadRawTextureData: not enough data provided (will result in overread). Expected " +
                    pixels.Length + " bytes, got " + source.Length + ".");

            source.Slice(0, pixels.Length).CopyTo(pixels);
            MarkFullyDirty();
        }

        /// <summary>Overwrites the whole store from unmanaged memory.</summary>
        public unsafe void LoadRawTextureData(IntPtr data, int size)
        {
            if (data == IntPtr.Zero)
                throw new ArgumentNullException(nameof(data));

            ThrowIfNotReadable("LoadRawTextureData");

            if (size < pixels.Length)
                throw new UnityException(
                    "LoadRawTextureData: not enough data provided (will result in overread). Expected " +
                    pixels.Length + " bytes, got " + size + ".");

            new ReadOnlySpan<byte>((void*)data, pixels.Length).CopyTo(pixels);
            MarkFullyDirty();
        }

        /// <summary>
        /// Replaces mip level 0. <paramref name="colors"/> is bottom-up and row-major: element 0 is the bottom-left
        /// texel.
        /// </summary>
        public void SetPixels32(Color32[] colors)
        {
            if (colors == null)
                throw new ArgumentNullException(nameof(colors));

            SetPixels32(0, 0, width, height, colors);
        }

        /// <summary>
        /// Replaces a rectangle of mip level 0. The block is placed bottom-up: <c>colors[0]</c> lands at
        /// <c>(x, y)</c>, which for NowGradient's one-row upload (NowGradient.cs:607) means row <c>y</c> counted from
        /// the bottom of the texture.
        /// </summary>
        public void SetPixels32(int x, int y, int blockWidth, int blockHeight, Color32[] colors)
        {
            if (colors == null)
                throw new ArgumentNullException(nameof(colors));

            ThrowIfNotReadable("SetPixels32");
            ValidateBlock(x, y, blockWidth, blockHeight);

            long needed = (long)blockWidth * blockHeight;

            if (colors.Length < needed)
                throw new ArgumentException(
                    "SetPixels32 called with " + colors.Length + " pixel values but " + needed + " are required.",
                    nameof(colors));

            int stride = width * m_BytesPerPixel;

            for (int row = 0; row < blockHeight; ++row)
            {
                int destination = (y + row) * stride + x * m_BytesPerPixel;
                int source = row * blockWidth;

                for (int column = 0; column < blockWidth; ++column)
                    WriteTexel(destination + column * m_BytesPerPixel, colors[source + column]);
            }

            MarkDirty(x, y, blockWidth, blockHeight);
        }

        /// <summary>Replaces mip level 0 from floating-point colours.</summary>
        public void SetPixels(Color[] colors)
        {
            if (colors == null)
                throw new ArgumentNullException(nameof(colors));

            ThrowIfNotReadable("SetPixels");

            long needed = (long)width * height;

            if (colors.Length < needed)
                throw new ArgumentException(
                    "SetPixels called with " + colors.Length + " pixel values but " + needed + " are required.",
                    nameof(colors));

            for (int i = 0; i < needed; ++i)
                WriteTexel(i * m_BytesPerPixel, colors[i]);

            MarkFullyDirty();
        }

        /// <summary>Mip level 0 as bottom-up, row-major <see cref="Color32"/>.</summary>
        public Color32[] GetPixels32()
        {
            ThrowIfNotReadable("GetPixels32");

            int count = width * height;
            var result = new Color32[count];

            for (int i = 0; i < count; ++i)
                result[i] = ReadTexel32(i * m_BytesPerPixel);

            return result;
        }

        /// <summary>Mip level 0 as bottom-up, row-major <see cref="Color"/>.</summary>
        public Color[] GetPixels()
        {
            ThrowIfNotReadable("GetPixels");

            int count = width * height;
            var result = new Color[count];

            for (int i = 0; i < count; ++i)
                result[i] = ReadTexel(i * m_BytesPerPixel);

            return result;
        }

        /// <summary>
        /// One texel of mip level 0. Out-of-range coordinates are clamped to the edge rather than throwing, which is
        /// what Unity does for the default <see cref="TextureWrapMode.Clamp"/> path; the shim clamps for every wrap
        /// mode because no NowUI call site reads a texel outside the texture.
        /// </summary>
        public Color GetPixel(int x, int y)
        {
            ThrowIfNotReadable("GetPixel");
            return ReadTexel(ClampedOffset(x, y));
        }

        /// <summary>Writes one texel of mip level 0.</summary>
        public void SetPixel(int x, int y, Color color)
        {
            ThrowIfNotReadable("SetPixel");

            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            WriteTexel((y * width + x) * m_BytesPerPixel, color);
            MarkDirty(x, y, 1, 1);
        }

        /// <summary>Uploads the CPU store, regenerating mips.</summary>
        public void Apply()
        {
            Apply(true, false);
        }

        /// <summary>Uploads the CPU store.</summary>
        public void Apply(bool updateMipmaps)
        {
            Apply(updateMipmaps, false);
        }

        /// <summary>
        /// Uploads the CPU store and optionally seals the texture. Sealing clears <see cref="Texture.isReadable"/>
        /// exactly as Unity does, but KEEPS the store unless <see cref="NowRuntime.releaseCpuCopiesOnSeal"/> is set:
        /// a browser that loses its WebGL context has to re-upload every sealed font atlas page (design §1.2).
        /// </summary>
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
        {
            if (pixels != null)
            {
                RectInt region = dirtyRect;

                // Nothing tracked since the last Apply means "upload everything", which is Unity's behaviour for an
                // Apply with no preceding write.
                if (region.width <= 0 || region.height <= 0)
                    region = new RectInt(0, 0, width, height);

                IncrementUpdateCount();

                unchecked
                {
                    ++version;
                }

                INowRenderBackend backend = NowRuntime.backend;

                if (backend != null)
                {
                    // A span over the existing array: no copy, no allocation, which is what the steady-state gate
                    // (design §1.2) measures on the font-atlas path.
                    backend.UploadTexture2D(this, new ReadOnlySpan<byte>(pixels), region, updateMipmaps);
                    uploadPending = false;
                }
                else
                {
                    uploadPending = true;
                }

                dirtyRect = default;
            }

            if (!makeNoLongerReadable)
                return;

            SetReadable(false);

            if (!NowRuntime.releaseCpuCopiesOnSeal)
                return;

            pixels = null;
            SetMemoryFootprint(0);
        }

        /// <summary>
        /// Uploads only rows <paramref name="y"/> to <paramref name="y"/> + <paramref name="rowCount"/> of level 0.
        /// NowUI-specific (NowUI.Internal.NowTextureUpload): the caller wrote those rows through
        /// <see cref="GetRawTextureData{T}"/>, which can only mark the whole texture dirty, and promises nothing else
        /// changed since the last upload - so a font page that gains a few glyphs does not re-upload the whole page.
        /// </summary>
        internal void ApplyRows(int y, int rowCount)
        {
            if (y < 0)
                y = 0;

            if (rowCount > height - y)
                rowCount = height - y;

            if (rowCount <= 0)
                return;

            dirtyRect = new RectInt(0, y, width, rowCount);
            Apply(false, false);
        }

        /// <summary>Resizes, keeping the format and mip setting. Contents are undefined afterwards, as in Unity.</summary>
        public void Reinitialize(int width, int height)
        {
            Reinitialize(width, height, m_Format, mipmapCount > 1);
        }

        /// <summary>Resizes and re-formats. Contents are undefined afterwards, as in Unity.</summary>
        public void Reinitialize(int width, int height, TextureFormat format, bool hasMipMap)
        {
            ThrowIfNotReadable("Reinitialize");
            Initialize(width, height, format, hasMipMap ? -1 : 1, m_Linear);
        }

        /// <summary>Clones the CPU store, so <c>Object.Instantiate</c> yields an independent texture.</summary>
        internal override Object CloneForInstantiate()
        {
            var clone = new Texture2D(width, height, m_Format, mipmapCount, m_Linear);

            if (pixels != null)
                Buffer.BlockCopy(pixels, 0, clone.pixels, 0, pixels.Length);

            clone.filterMode = filterMode;
            clone.wrapModeU = wrapModeU;
            clone.wrapModeV = wrapModeV;
            clone.wrapModeW = wrapModeW;
            clone.anisoLevel = anisoLevel;
            clone.MarkFullyDirty();
            return clone;
        }

        /// <summary>Releases the GPU texture and drops the CPU store.</summary>
        internal override void OnDestroyResources()
        {
            INowRenderBackend backend = NowRuntime.backend;

            if (backend != null)
                backend.ReleaseTexture(this);

            pixels = null;
            base.OnDestroyResources();
        }

        /// <summary>
        /// Bytes one texel of <paramref name="format"/> occupies, or 0 when the shim cannot address the format
        /// texel-by-texel (every block-compressed format).
        /// </summary>
        internal static int BytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                case TextureFormat.R8_SIGNED:
                    return 1;

                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA4444:
                case TextureFormat.RGB565:
                case TextureFormat.R16:
                case TextureFormat.R16_SIGNED:
                case TextureFormat.RHalf:
                case TextureFormat.RG16:
                case TextureFormat.RG16_SIGNED:
                    return 2;

                case TextureFormat.RGB24:
                case TextureFormat.RGB24_SIGNED:
                    return 3;

                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGBA32_SIGNED:
                case TextureFormat.RGHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RG32:
                case TextureFormat.RG32_SIGNED:
                case TextureFormat.RGB9e5Float:
                    return 4;

                case TextureFormat.RGB48:
                case TextureFormat.RGB48_SIGNED:
                    return 6;

                case TextureFormat.RGBAHalf:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBA64:
                case TextureFormat.RGBA64_SIGNED:
                    return 8;

                case TextureFormat.RGBAFloat:
                    return 16;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Byte size of a full <paramref name="mipCount"/>-level chain. Each level halves both axes, rounding down but
        /// never below 1 - the standard chain Unity allocates for an uncompressed texture.
        /// </summary>
        internal static long ChainByteSize(int width, int height, int bytesPerPixel, int mipCount)
        {
            long total = 0;

            for (int level = 0; level < mipCount; ++level)
            {
                int levelWidth = width >> level;
                int levelHeight = height >> level;

                if (levelWidth < 1)
                    levelWidth = 1;

                if (levelHeight < 1)
                    levelHeight = 1;

                total += (long)levelWidth * levelHeight * bytesPerPixel;
            }

            return total;
        }

        /// <summary>Levels in a full chain: <c>1 + floor(log2(max(width, height)))</c>.</summary>
        internal static int FullMipChainLength(int width, int height)
        {
            int largest = width > height ? width : height;
            int levels = 1;

            while (largest > 1)
            {
                largest >>= 1;
                ++levels;
            }

            return levels;
        }

        private static Texture2D CreateConstant(string name, Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, 1, false);
            texture.name = name;
            texture.WriteTexel(0, color);
            texture.MarkFullyDirty();
            texture.Apply(false, false);
            return texture;
        }

        private void Initialize(int width, int height, TextureFormat format, int mipCount, bool linear)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Texture2D dimensions must be positive; got " + width + "x" + height + ".");

            int bytesPerPixel = BytesPerPixel(format);

            if (bytesPerPixel == 0)
                throw new ArgumentException(
                    "TextureFormat." + format + " has no CPU-addressable texel layout in the NowUI standalone build.",
                    nameof(format));

            int fullChain = FullMipChainLength(width, height);
            int levels = mipCount < 0 ? fullChain : mipCount;

            if (levels < 1)
                levels = 1;

            if (levels > fullChain)
                levels = fullChain;

            long bytes = ChainByteSize(width, height, bytesPerPixel, levels);

            if (bytes > int.MaxValue)
                throw new ArgumentException("Texture2D " + width + "x" + height + " needs " + bytes +
                                            " bytes, which does not fit in a managed array.");

            SetSizeDirect(width, height);
            SetMipmapCount(levels);
            SetReadable(true);

            m_Format = format;
            m_Linear = linear;
            m_BytesPerPixel = bytesPerPixel;

            // Pinned: GetRawTextureData<T> hands out a NativeArray view, and the collections layer takes raw pointers
            // into the backing array without a GCHandle (see Collections/NativeArray.cs).
            pixels = GC.AllocateArray<byte>((int)bytes, pinned: true);
            SetMemoryFootprint(bytes);
            MarkFullyDirty();
        }

        private void MarkFullyDirty()
        {
            dirtyRect = new RectInt(0, 0, width, height);
            uploadPending = true;
        }

        private void MarkDirty(int x, int y, int blockWidth, int blockHeight)
        {
            uploadPending = true;

            if (dirtyRect.width <= 0 || dirtyRect.height <= 0)
            {
                dirtyRect = new RectInt(x, y, blockWidth, blockHeight);
                return;
            }

            int minX = dirtyRect.x < x ? dirtyRect.x : x;
            int minY = dirtyRect.y < y ? dirtyRect.y : y;
            int maxX = dirtyRect.x + dirtyRect.width;
            int maxY = dirtyRect.y + dirtyRect.height;

            if (x + blockWidth > maxX)
                maxX = x + blockWidth;

            if (y + blockHeight > maxY)
                maxY = y + blockHeight;

            dirtyRect = new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        private void ValidateBlock(int x, int y, int blockWidth, int blockHeight)
        {
            if (blockWidth < 0 || blockHeight < 0)
                throw new ArgumentException("Block size must not be negative; got " + blockWidth + "x" + blockHeight + ".");

            if (x < 0 || y < 0 || x + blockWidth > width || y + blockHeight > height)
                throw new ArgumentException(
                    "The block (" + x + ", " + y + ", " + blockWidth + ", " + blockHeight +
                    ") does not fit inside the " + width + "x" + height + " texture.");
        }

        private int ClampedOffset(int x, int y)
        {
            if (x < 0)
                x = 0;
            else if (x >= width)
                x = width - 1;

            if (y < 0)
                y = 0;
            else if (y >= height)
                y = height - 1;

            return (y * width + x) * m_BytesPerPixel;
        }

        private void ThrowIfNotReadable(string member)
        {
            if (pixels == null)
                throw new UnityException(
                    member + " is not allowed on '" + SafeName() + "': the CPU copy has been released.");

            if (!isReadable)
                throw new UnityException(
                    member + " is not allowed on '" + SafeName() +
                    "' because it is not readable. Enable Read/Write, or do not seal it with Apply(_, true).");
        }

        private string SafeName()
        {
            // name throws once destroyed (VT §13), and an exception message is a poor place to discover that.
            return isDestroyed ? "<destroyed>" : name;
        }

        // ---- texel codecs ------------------------------------------------------------------------------------
        //
        // Only the uncompressed formats BytesPerPixel accepts are handled. Everything NowUI creates is RGBA32; the
        // rest exist so a host that hands the shim an R8 mask or a float SDF page can still round-trip it.

        private void WriteTexel(int offset, Color32 value)
        {
            switch (m_Format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.RGBA32_SIGNED:
                    pixels[offset] = value.r;
                    pixels[offset + 1] = value.g;
                    pixels[offset + 2] = value.b;
                    pixels[offset + 3] = value.a;
                    return;

                case TextureFormat.ARGB32:
                    pixels[offset] = value.a;
                    pixels[offset + 1] = value.r;
                    pixels[offset + 2] = value.g;
                    pixels[offset + 3] = value.b;
                    return;

                case TextureFormat.BGRA32:
                    pixels[offset] = value.b;
                    pixels[offset + 1] = value.g;
                    pixels[offset + 2] = value.r;
                    pixels[offset + 3] = value.a;
                    return;

                case TextureFormat.RGB24:
                case TextureFormat.RGB24_SIGNED:
                    pixels[offset] = value.r;
                    pixels[offset + 1] = value.g;
                    pixels[offset + 2] = value.b;
                    return;

                case TextureFormat.Alpha8:
                    pixels[offset] = value.a;
                    return;

                case TextureFormat.R8:
                case TextureFormat.R8_SIGNED:
                    pixels[offset] = value.r;
                    return;

                case TextureFormat.RG16:
                case TextureFormat.RG16_SIGNED:
                    pixels[offset] = value.r;
                    pixels[offset + 1] = value.g;
                    return;

                // Every remaining format this shim can size is float- or half-typed, so widen and let the float codec
                // handle it. That codec throws for anything neither path addresses, which is what terminates the
                // mutual delegation between the two overloads.
                default:
                    WriteFloatTexel(offset, value);
                    return;
            }
        }

        private void WriteTexel(int offset, Color value)
        {
            switch (m_Format)
            {
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                    WriteFloatTexel(offset, value);
                    return;

                default:
                    WriteTexel(offset, (Color32)value);
                    return;
            }
        }

        private void WriteFloatTexel(int offset, Color value)
        {
            switch (m_Format)
            {
                case TextureFormat.RFloat:
                    WriteFloat(offset, value.r);
                    return;

                case TextureFormat.RGFloat:
                    WriteFloat(offset, value.r);
                    WriteFloat(offset + 4, value.g);
                    return;

                case TextureFormat.RGBAFloat:
                    WriteFloat(offset, value.r);
                    WriteFloat(offset + 4, value.g);
                    WriteFloat(offset + 8, value.b);
                    WriteFloat(offset + 12, value.a);
                    return;

                case TextureFormat.RHalf:
                    WriteHalf(offset, value.r);
                    return;

                case TextureFormat.RGHalf:
                    WriteHalf(offset, value.r);
                    WriteHalf(offset + 2, value.g);
                    return;

                case TextureFormat.RGBAHalf:
                    WriteHalf(offset, value.r);
                    WriteHalf(offset + 2, value.g);
                    WriteHalf(offset + 4, value.b);
                    WriteHalf(offset + 6, value.a);
                    return;

                default:
                    throw UnsupportedTexelFormat("SetPixel");
            }
        }

        private Color32 ReadTexel32(int offset)
        {
            switch (m_Format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.RGBA32_SIGNED:
                    return new Color32(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);

                case TextureFormat.ARGB32:
                    return new Color32(pixels[offset + 1], pixels[offset + 2], pixels[offset + 3], pixels[offset]);

                case TextureFormat.BGRA32:
                    return new Color32(pixels[offset + 2], pixels[offset + 1], pixels[offset], pixels[offset + 3]);

                case TextureFormat.RGB24:
                case TextureFormat.RGB24_SIGNED:
                    return new Color32(pixels[offset], pixels[offset + 1], pixels[offset + 2], 255);

                case TextureFormat.Alpha8:
                    return new Color32(0, 0, 0, pixels[offset]);

                case TextureFormat.R8:
                case TextureFormat.R8_SIGNED:
                    return new Color32(pixels[offset], 0, 0, 255);

                case TextureFormat.RG16:
                case TextureFormat.RG16_SIGNED:
                    return new Color32(pixels[offset], pixels[offset + 1], 0, 255);

                // As on the write side: anything left is float- or half-typed, and the float codec throws for a
                // format neither path addresses, so the delegation terminates.
                default:
                    return ReadFloatTexel(offset);
            }
        }

        private Color ReadTexel(int offset)
        {
            switch (m_Format)
            {
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                    return ReadFloatTexel(offset);

                default:
                    return ReadTexel32(offset);
            }
        }

        private Color ReadFloatTexel(int offset)
        {
            switch (m_Format)
            {
                case TextureFormat.RFloat:
                    return new Color(ReadFloat(offset), 0F, 0F, 1F);

                case TextureFormat.RGFloat:
                    return new Color(ReadFloat(offset), ReadFloat(offset + 4), 0F, 1F);

                case TextureFormat.RGBAFloat:
                    return new Color(ReadFloat(offset), ReadFloat(offset + 4), ReadFloat(offset + 8), ReadFloat(offset + 12));

                case TextureFormat.RHalf:
                    return new Color(ReadHalf(offset), 0F, 0F, 1F);

                case TextureFormat.RGHalf:
                    return new Color(ReadHalf(offset), ReadHalf(offset + 2), 0F, 1F);

                case TextureFormat.RGBAHalf:
                    return new Color(ReadHalf(offset), ReadHalf(offset + 2), ReadHalf(offset + 4), ReadHalf(offset + 6));

                default:
                    throw UnsupportedTexelFormat("GetPixel");
            }
        }

        /// <summary>
        /// Raised for a format the shim can size (so the raw-data paths work) but cannot address texel by texel -
        /// the packed 4444/565/9e5 forms and the 16-bit-per-channel integer forms. Unity likewise refuses
        /// <c>GetPixels</c> on formats it has no CPU codec for.
        /// </summary>
        private UnityException UnsupportedTexelFormat(string member)
        {
            return new UnityException(
                member + " is not supported for TextureFormat." + m_Format +
                " in the NowUI standalone build; use GetRawTextureData/LoadRawTextureData instead.");
        }

        private void WriteFloat(int offset, float value)
        {
            BitConverter.TryWriteBytes(new Span<byte>(pixels, offset, 4), value);
        }

        private float ReadFloat(int offset)
        {
            return BitConverter.ToSingle(pixels, offset);
        }

        private void WriteHalf(int offset, float value)
        {
            BitConverter.TryWriteBytes(new Span<byte>(pixels, offset, 2), BitConverter.HalfToUInt16Bits((Half)value));
        }

        private float ReadHalf(int offset)
        {
            return (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(pixels, offset));
        }
    }
}
