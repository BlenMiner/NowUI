using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>
    /// Pure-C# replacement for the native font baking session, used automatically on
    /// platforms without the nowui-msdf plugin (consoles, platforms without CI
    /// builds) and when <see cref="NowFontCompiler.forceManagedCompiler"/> is set.
    ///
    /// Matches the native session's contract: a fixed-size atlas that never resizes,
    /// glyphs added on demand with em-unit plane bounds and pixel atlas bounds
    /// (bottom origin), all-or-nothing adds that report AtlasFull so the caller can
    /// seal the page and start a new session. The SDF rasterization itself runs in a
    /// Burst-compiled job (<see cref="NowSdfBakeJob"/>) parallelized across glyphs,
    /// which keeps cost in the same ballpark as the native compiler.
    ///
    /// Limitations versus native: CFF (OTTO) outlines and color fonts (emoji) are
    /// not supported, and corners round off slightly at extreme magnification
    /// because the field is a true SDF rather than a multi-channel MSDF.
    /// </summary>
    internal sealed class NowManagedFontSession : IDisposable
    {
        struct PendingGlyph
        {
            public int unicode;
            public float advance;
            public bool whitespace;
            public int segmentStart;
            public int segmentCount;
            public int cellX0;
            public int cellY0;
            public int width;
            public int height;
            public int atlasX;
            public int atlasY;
        }

        const int ATLAS_BORDER = 1;

        readonly NowTrueType _font;
        readonly byte[] _atlas;
        readonly float _scale;
        readonly Dictionary<int, NowFontAtlasInfo.Glyph> _baked = new Dictionary<int, NowFontAtlasInfo.Glyph>(64);
        readonly Dictionary<int, NowFontAtlasInfo.Glyph> _bakedByIndex = new Dictionary<int, NowFontAtlasInfo.Glyph>(64);

        readonly NowGlyphOutline _outlineScratch = new NowGlyphOutline();
        readonly List<Vector4> _segmentScratch = new List<Vector4>(512);
        readonly List<PendingGlyph> _pendingScratch = new List<PendingGlyph>(64);
        byte[] _bakeScratch;

        int _shelfX = ATLAS_BORDER;
        int _shelfY = ATLAS_BORDER;
        int _shelfHeight;

        public int AtlasSide { get; }

        public float Size { get; }

        public float DistanceRange { get; }

        public NowFontAtlasInfo.Metrics Metrics { get; }

        NowManagedFontSession(NowTrueType font, int size, int pixelRange, int atlasSide)
        {
            _font = font;
            _atlas = new byte[atlasSide * atlasSide * 4];
            _scale = (float)size / font.unitsPerEm;
            AtlasSide = atlasSide;
            Size = size;
            DistanceRange = pixelRange;

            float invUpem = 1f / font.unitsPerEm;
            Metrics = new NowFontAtlasInfo.Metrics
            {
                emSize = 1f,
                ascender = font.ascender * invUpem,
                descender = font.descender * invUpem,
                lineHeight = (font.ascender - font.descender + font.lineGap) * invUpem,
                underlineY = font.underlinePosition * invUpem,
                underlineThickness = font.underlineThickness * invUpem
            };
        }

        public static bool TryCreate(
            byte[] fontData,
            int size,
            int pixelRange,
            int atlasSide,
            out NowManagedFontSession session,
            out string error)
        {
            session = null;

            if (size <= 0 || pixelRange <= 0 || atlasSide <= 0)
            {
                error = "Managed font session parameters must be positive.";
                return false;
            }

            if (!NowTrueType.TryParse(fontData, out var font, out error))
                return false;

            session = new NowManagedFontSession(font, size, pixelRange, atlasSide);
            return true;
        }

        public NowFontCompiler.DynamicSession.AddResult TryAddGlyphs(
            int[] codepoints,
            int codepointCount,
            List<NowFontAtlasInfo.Glyph> results,
            out string error)
        {
            return AddGlyphsCore(codepoints, codepointCount, keysAreGlyphIndices: false, results, out error);
        }

        /// <summary>
        /// Bakes glyphs addressed directly by glyph index — the currency of shaped
        /// text, where ligatures and emoji sequences have no single codepoint. The
        /// returned records carry the glyph index in their unicode field; callers
        /// keep their own index-keyed bookkeeping.
        /// </summary>
        public NowFontCompiler.DynamicSession.AddResult TryAddGlyphsByIndex(
            int[] glyphIndices,
            int glyphIndexCount,
            List<NowFontAtlasInfo.Glyph> results,
            out string error)
        {
            return AddGlyphsCore(glyphIndices, glyphIndexCount, keysAreGlyphIndices: true, results, out error);
        }

        NowFontCompiler.DynamicSession.AddResult AddGlyphsCore(
            int[] keys,
            int keyCount,
            bool keysAreGlyphIndices,
            List<NowFontAtlasInfo.Glyph> results,
            out string error)
        {
            error = null;

            if (keys == null || keyCount <= 0)
                return NowFontCompiler.DynamicSession.AddResult.Ok;

            var codepoints = keys;
            int codepointCount = keyCount;
            var baked = keysAreGlyphIndices ? _bakedByIndex : _baked;

            var pending = _pendingScratch;
            var segments = _segmentScratch;
            pending.Clear();
            segments.Clear();
            int resultsBefore = results.Count;

            // Plan pass: resolve, flatten, and pack every glyph before touching any
            // session state, so a full atlas leaves the session unchanged (matching
            // the native session's all-or-nothing add semantics).
            int shelfX = _shelfX;
            int shelfY = _shelfY;
            int shelfHeight = _shelfHeight;
            float invSize = 1f / Size;

            for (int i = 0; i < codepointCount; ++i)
            {
                int unicode = codepoints[i];

                if (baked.TryGetValue(unicode, out var existing))
                {
                    results.Add(existing);
                    continue;
                }

                int glyphIndex;

                if (keysAreGlyphIndices)
                {
                    glyphIndex = unicode;

                    if (glyphIndex < 0 || glyphIndex >= _font.glyphCount)
                        continue;
                }
                else if (!_font.TryGetGlyphIndex(unicode, out glyphIndex))
                {
                    continue; // not in the font: the caller records a miss
                }

                float advance = _font.GetAdvanceWidth(glyphIndex) * _scale * invSize;

                if (!_font.TryGetOutline(glyphIndex, _outlineScratch) ||
                    !_outlineScratch.TryGetBounds(out Vector2 min, out Vector2 max))
                {
                    pending.Add(new PendingGlyph { unicode = unicode, advance = advance, whitespace = true });
                    continue;
                }

                // Cell bounds in glyph-space pixels, padded so the distance ramp
                // reaches zero inside the cell.
                float pad = DistanceRange * 0.5f + 0.5f;
                int cellX0 = Mathf.FloorToInt(min.x * _scale - pad);
                int cellY0 = Mathf.FloorToInt(min.y * _scale - pad);
                int cellX1 = Mathf.CeilToInt(max.x * _scale + pad);
                int cellY1 = Mathf.CeilToInt(max.y * _scale + pad);
                int width = cellX1 - cellX0;
                int height = cellY1 - cellY0;

                if (!TryPack(ref shelfX, ref shelfY, ref shelfHeight, width, height, out int atlasX, out int atlasY))
                {
                    // A failed add returns nothing and changes nothing, matching the
                    // native session; drop any cached glyphs echoed earlier in this call.
                    results.RemoveRange(resultsBefore, results.Count - resultsBefore);
                    return NowFontCompiler.DynamicSession.AddResult.AtlasFull;
                }

                int segmentStart = segments.Count;
                NowManagedFontBaker.Flatten(_outlineScratch, _scale, new Vector2(cellX0, cellY0), segments);

                pending.Add(new PendingGlyph
                {
                    unicode = unicode,
                    advance = advance,
                    segmentStart = segmentStart,
                    segmentCount = segments.Count - segmentStart,
                    cellX0 = cellX0,
                    cellY0 = cellY0,
                    width = width,
                    height = height,
                    atlasX = atlasX,
                    atlasY = atlasY
                });
            }

            if (pending.Count > 0)
                BakePending(pending, segments);

            // Commit: packing state and glyph records only change once nothing can fail.
            _shelfX = shelfX;
            _shelfY = shelfY;
            _shelfHeight = shelfHeight;

            for (int i = 0; i < pending.Count; ++i)
            {
                PendingGlyph glyph = pending[i];

                var record = new NowFontAtlasInfo.Glyph
                {
                    unicode = glyph.unicode,
                    advance = glyph.advance
                };

                if (!glyph.whitespace)
                {
                    record.planeBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = glyph.cellX0 * invSize,
                        bottom = glyph.cellY0 * invSize,
                        right = (glyph.cellX0 + glyph.width) * invSize,
                        top = (glyph.cellY0 + glyph.height) * invSize
                    };
                    record.atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        left = glyph.atlasX,
                        bottom = glyph.atlasY,
                        right = glyph.atlasX + glyph.width,
                        top = glyph.atlasY + glyph.height
                    };
                }

                baked[glyph.unicode] = record;
                results.Add(record);
            }

            return NowFontCompiler.DynamicSession.AddResult.Ok;
        }

        bool TryPack(ref int shelfX, ref int shelfY, ref int shelfHeight, int width, int height, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (width <= 0 || height <= 0 || width + ATLAS_BORDER * 2 > AtlasSide)
                return false;

            if (shelfX + width + ATLAS_BORDER > AtlasSide)
            {
                shelfY += shelfHeight + ATLAS_BORDER;
                shelfX = ATLAS_BORDER;
                shelfHeight = 0;
            }

            if (shelfY + height + ATLAS_BORDER > AtlasSide)
                return false;

            x = shelfX;
            y = shelfY;
            shelfX += width + ATLAS_BORDER;
            shelfHeight = Mathf.Max(shelfHeight, height);
            return true;
        }

        void BakePending(List<PendingGlyph> pending, List<Vector4> segments)
        {
            int cellCount = 0;
            int outputBytes = 0;

            for (int i = 0; i < pending.Count; ++i)
            {
                if (pending[i].whitespace)
                    continue;

                ++cellCount;
                outputBytes += pending[i].width * pending[i].height * 4;
            }

            if (cellCount == 0)
                return;

            var nativeSegments = new NativeArray<float4>(Mathf.Max(1, segments.Count), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var cells = new NativeArray<NowSdfGlyphCell>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var output = new NativeArray<byte>(outputBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            try
            {
                for (int i = 0; i < segments.Count; ++i)
                {
                    Vector4 segment = segments[i];
                    nativeSegments[i] = new float4(segment.x, segment.y, segment.z, segment.w);
                }

                int cell = 0;
                int outputOffset = 0;

                for (int i = 0; i < pending.Count; ++i)
                {
                    PendingGlyph glyph = pending[i];

                    if (glyph.whitespace)
                        continue;

                    cells[cell++] = new NowSdfGlyphCell
                    {
                        segmentStart = glyph.segmentStart,
                        segmentCount = glyph.segmentCount,
                        width = glyph.width,
                        height = glyph.height,
                        outputOffset = outputOffset
                    };

                    outputOffset += glyph.width * glyph.height * 4;
                }

                new NowSdfBakeJob
                {
                    segments = nativeSegments,
                    cells = cells,
                    distanceRange = DistanceRange,
                    output = output
                }.Schedule(cellCount, 1).Complete();

                if (_bakeScratch == null || _bakeScratch.Length < outputBytes)
                    _bakeScratch = new byte[Mathf.NextPowerOfTwo(outputBytes)];

                NativeArray<byte>.Copy(output, 0, _bakeScratch, 0, outputBytes);

                // Blit each cell's rows into the page atlas (bottom-origin rows on
                // both sides, so this is a straight row copy).
                int sourceOffset = 0;

                for (int i = 0; i < pending.Count; ++i)
                {
                    PendingGlyph glyph = pending[i];

                    if (glyph.whitespace)
                        continue;

                    int rowBytes = glyph.width * 4;

                    for (int row = 0; row < glyph.height; ++row)
                    {
                        int destination = ((glyph.atlasY + row) * AtlasSide + glyph.atlasX) * 4;
                        Buffer.BlockCopy(_bakeScratch, sourceOffset + row * rowBytes, _atlas, destination, rowBytes);
                    }

                    sourceOffset += glyph.height * rowBytes;
                }
            }
            finally
            {
                nativeSegments.Dispose();
                cells.Dispose();
                output.Dispose();
            }
        }

        public bool TryCopyAtlas(ref byte[] buffer, out string error)
        {
            if (buffer == null || buffer.Length != _atlas.Length)
                buffer = new byte[_atlas.Length];

            Buffer.BlockCopy(_atlas, 0, buffer, 0, _atlas.Length);
            error = null;
            return true;
        }

        public void Dispose()
        {
            // Everything is managed memory; nothing to release eagerly.
        }
    }
}
