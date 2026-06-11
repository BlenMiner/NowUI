using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>
    /// Decoded TrueType glyph outline: raw points with on/off-curve flags grouped
    /// into contours, in font units. Buffers are reused across glyphs; call
    /// <see cref="Clear"/> (or let the parser do it) before refilling.
    /// </summary>
    internal sealed class NowGlyphOutline
    {
        public readonly List<Vector2> points = new List<Vector2>(64);

        public readonly List<bool> onCurve = new List<bool>(64);

        /// <summary>Exclusive end index into <see cref="points"/> per contour.</summary>
        public readonly List<int> contourEnds = new List<int>(8);

        public bool isEmpty => points.Count == 0;

        public void Clear()
        {
            points.Clear();
            onCurve.Clear();
            contourEnds.Clear();
        }

        /// <summary>
        /// Control-point bounds; quadratic curves stay inside their control hull, so
        /// this is a valid (slightly conservative) raster bound.
        /// </summary>
        public bool TryGetBounds(out Vector2 min, out Vector2 max)
        {
            if (points.Count == 0)
            {
                min = default;
                max = default;
                return false;
            }

            min = max = points[0];

            for (int i = 1; i < points.Count; ++i)
            {
                Vector2 p = points[i];
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return true;
        }
    }

    /// <summary>
    /// Minimal pure-C# TrueType parser for the managed font compiler fallback:
    /// glyph lookup (cmap formats 4 and 12), horizontal metrics, and quadratic
    /// outlines from the glyf table, including composite glyphs. CFF-flavored
    /// OpenType (OTTO) is rejected — those fonts need the native compiler.
    /// </summary>
    internal sealed class NowTrueType
    {
        const uint TAG_TTCF = 0x74746366; // 'ttcf'
        const uint TAG_OTTO = 0x4F54544F; // 'OTTO'
        const uint SFNT_V1 = 0x00010000;
        const uint TAG_TRUE = 0x74727565; // 'true'

        const int MAX_COMPOSITE_DEPTH = 8;

        byte[] _data;

        int _glyf, _glyfLength;
        int _loca;
        int _hmtx;
        int _cmapSubtable;
        int _cmapFormat;

        bool _longLoca;
        int _numberOfHMetrics;

        public int unitsPerEm { get; private set; }

        public int glyphCount { get; private set; }

        public short ascender { get; private set; }

        public short descender { get; private set; }

        public short lineGap { get; private set; }

        public short underlinePosition { get; private set; }

        public short underlineThickness { get; private set; }

        NowTrueType()
        {
        }

        public static bool TryParse(byte[] data, out NowTrueType font, out string error)
        {
            font = null;

            if (data == null || data.Length < 12)
            {
                error = "Font data is empty or truncated.";
                return false;
            }

            var parsed = new NowTrueType { _data = data };

            if (!parsed.ParseDirectory(out error))
                return false;

            font = parsed;
            error = null;
            return true;
        }

        bool ParseDirectory(out string error)
        {
            int offset = 0;
            uint version = ReadU32(0);

            if (version == TAG_TTCF)
            {
                if (_data.Length < 16)
                {
                    error = "TrueType collection header is truncated.";
                    return false;
                }

                // Use the collection's first font; per-face selection is a native-path feature.
                offset = (int)ReadU32(12);

                if (offset < 0 || offset + 12 > _data.Length)
                {
                    error = "TrueType collection offset is out of range.";
                    return false;
                }

                version = ReadU32(offset);
            }

            if (version == TAG_OTTO)
            {
                error = "The managed font compiler supports TrueType outlines (glyf); this font uses CFF outlines and needs the native compiler.";
                return false;
            }

            if (version != SFNT_V1 && version != TAG_TRUE)
            {
                error = "Unrecognized font format.";
                return false;
            }

            int tableCount = ReadU16(offset + 4);
            int head = 0, maxp = 0, hhea = 0, cmap = 0, post = 0;
            int directory = offset + 12;

            if (directory + tableCount * 16 > _data.Length)
            {
                error = "Font table directory is truncated.";
                return false;
            }

            for (int i = 0; i < tableCount; ++i)
            {
                int record = directory + i * 16;
                uint tag = ReadU32(record);
                int tableOffset = (int)ReadU32(record + 8);
                int tableLength = (int)ReadU32(record + 12);

                if (tableOffset < 0 || tableLength < 0 || tableOffset + tableLength > _data.Length)
                    continue;

                switch (tag)
                {
                    case 0x68656164: head = tableOffset; break;          // head
                    case 0x6D617870: maxp = tableOffset; break;          // maxp
                    case 0x68686561: hhea = tableOffset; break;          // hhea
                    case 0x686D7478: _hmtx = tableOffset; break;         // hmtx
                    case 0x636D6170: cmap = tableOffset; break;          // cmap
                    case 0x6C6F6361: _loca = tableOffset; break;         // loca
                    case 0x676C7966: _glyf = tableOffset; _glyfLength = tableLength; break; // glyf
                    case 0x706F7374: post = tableOffset; break;          // post
                }
            }

            if (head == 0 || maxp == 0 || hhea == 0 || _hmtx == 0 || cmap == 0 || _loca == 0 || _glyf == 0)
            {
                error = "Font is missing a required TrueType table (head/maxp/hhea/hmtx/cmap/loca/glyf).";
                return false;
            }

            unitsPerEm = ReadU16(head + 18);

            if (unitsPerEm <= 0)
            {
                error = "Font reports an invalid unitsPerEm.";
                return false;
            }

            _longLoca = ReadS16(head + 50) != 0;
            glyphCount = ReadU16(maxp + 4);
            ascender = ReadS16(hhea + 4);
            descender = ReadS16(hhea + 6);
            lineGap = ReadS16(hhea + 8);
            _numberOfHMetrics = ReadU16(hhea + 34);

            if (post != 0)
            {
                underlinePosition = ReadS16(post + 8);
                underlineThickness = ReadS16(post + 10);
            }

            if (!SelectCmapSubtable(cmap))
            {
                error = "Font has no usable cmap subtable (formats 4 and 12 are supported).";
                return false;
            }

            error = null;
            return true;
        }

        bool SelectCmapSubtable(int cmap)
        {
            int subtableCount = ReadU16(cmap + 2);
            int best = 0;
            int bestFormat = 0;

            for (int i = 0; i < subtableCount; ++i)
            {
                int record = cmap + 4 + i * 8;
                int platform = ReadU16(record);
                int subtable = cmap + (int)ReadU32(record + 4);

                if (subtable < 0 || subtable + 4 > _data.Length)
                    continue;

                // Only Unicode-meaningful platforms: Unicode (0) and Windows (3).
                if (platform != 0 && platform != 3)
                    continue;

                int format = ReadU16(subtable);

                if (format == 12 && bestFormat != 12)
                {
                    best = subtable;
                    bestFormat = 12;
                }
                else if (format == 4 && bestFormat == 0)
                {
                    best = subtable;
                    bestFormat = 4;
                }
            }

            _cmapSubtable = best;
            _cmapFormat = bestFormat;
            return best != 0;
        }

        public bool TryGetGlyphIndex(int codepoint, out int glyphIndex)
        {
            glyphIndex = 0;

            if (codepoint <= 0)
                return false;

            if (_cmapFormat == 12)
            {
                int groups = (int)ReadU32(_cmapSubtable + 12);
                int lo = 0;
                int hi = groups - 1;

                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    int group = _cmapSubtable + 16 + mid * 12;
                    uint start = ReadU32(group);
                    uint end = ReadU32(group + 4);

                    if (codepoint < start)
                        hi = mid - 1;
                    else if (codepoint > end)
                        lo = mid + 1;
                    else
                    {
                        glyphIndex = (int)(ReadU32(group + 8) + (uint)codepoint - start);
                        return glyphIndex > 0 && glyphIndex < glyphCount;
                    }
                }

                return false;
            }

            if (codepoint > 0xFFFF)
                return false;

            int segCountX2 = ReadU16(_cmapSubtable + 6);
            int endCodes = _cmapSubtable + 14;
            int startCodes = endCodes + segCountX2 + 2;
            int idDeltas = startCodes + segCountX2;
            int idRangeOffsets = idDeltas + segCountX2;

            int low = 0;
            int high = (segCountX2 >> 1) - 1;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int endCode = ReadU16(endCodes + mid * 2);

                if (codepoint > endCode)
                {
                    low = mid + 1;
                    continue;
                }

                int startCode = ReadU16(startCodes + mid * 2);

                if (codepoint < startCode)
                {
                    high = mid - 1;
                    continue;
                }

                int idRangeOffsetPos = idRangeOffsets + mid * 2;
                int idRangeOffset = ReadU16(idRangeOffsetPos);
                int delta = ReadS16(idDeltas + mid * 2);

                if (idRangeOffset == 0)
                {
                    glyphIndex = (codepoint + delta) & 0xFFFF;
                    return glyphIndex > 0 && glyphIndex < glyphCount;
                }

                int glyphPos = idRangeOffsetPos + idRangeOffset + (codepoint - startCode) * 2;

                if (glyphPos + 2 > _data.Length)
                    return false;

                int glyph = ReadU16(glyphPos);

                if (glyph == 0)
                    return false;

                glyphIndex = (glyph + delta) & 0xFFFF;
                return glyphIndex > 0 && glyphIndex < glyphCount;
            }

            return false;
        }

        /// <summary>Advance width in font units.</summary>
        public int GetAdvanceWidth(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= glyphCount || _numberOfHMetrics <= 0)
                return 0;

            if (glyphIndex >= _numberOfHMetrics)
                glyphIndex = _numberOfHMetrics - 1;

            return ReadU16(_hmtx + glyphIndex * 4);
        }

        /// <summary>
        /// Decodes the glyph outline (composites resolved) into <paramref name="outline"/>
        /// in font units. Returns true when the glyph exists; an empty outline (e.g.
        /// whitespace) still returns true.
        /// </summary>
        public bool TryGetOutline(int glyphIndex, NowGlyphOutline outline)
        {
            outline.Clear();
            return AppendGlyph(glyphIndex, outline, 1f, 0f, 0f, 1f, 0f, 0f, 0);
        }

        bool TryGetGlyphRange(int glyphIndex, out int start, out int length)
        {
            start = 0;
            length = 0;

            if (glyphIndex < 0 || glyphIndex >= glyphCount)
                return false;

            int begin, end;

            if (_longLoca)
            {
                begin = (int)ReadU32(_loca + glyphIndex * 4);
                end = (int)ReadU32(_loca + glyphIndex * 4 + 4);
            }
            else
            {
                begin = ReadU16(_loca + glyphIndex * 2) * 2;
                end = ReadU16(_loca + glyphIndex * 2 + 2) * 2;
            }

            if (end <= begin || end > _glyfLength)
                return end == begin; // empty glyph (whitespace) is valid

            start = _glyf + begin;
            length = end - begin;
            return true;
        }

        bool AppendGlyph(
            int glyphIndex,
            NowGlyphOutline outline,
            float xx, float xy, float yx, float yy, float dx, float dy,
            int depth)
        {
            if (depth > MAX_COMPOSITE_DEPTH)
                return false;

            if (!TryGetGlyphRange(glyphIndex, out int glyph, out int glyphLength))
                return false;

            if (glyphLength == 0)
                return true;

            int contourCount = ReadS16(glyph);

            if (contourCount >= 0)
            {
                AppendSimpleGlyph(glyph, contourCount, outline, xx, xy, yx, yy, dx, dy);
                return true;
            }

            // Composite glyph: a list of transformed component glyphs.
            int cursor = glyph + 10;

            while (true)
            {
                int flags = ReadU16(cursor);
                int componentIndex = ReadU16(cursor + 2);
                cursor += 4;

                float argX, argY;

                if ((flags & 0x0001) != 0) // ARG_1_AND_2_ARE_WORDS
                {
                    argX = ReadS16(cursor);
                    argY = ReadS16(cursor + 2);
                    cursor += 4;
                }
                else
                {
                    argX = (sbyte)_data[cursor];
                    argY = (sbyte)_data[cursor + 1];
                    cursor += 2;
                }

                // Point-matching composites (ARGS_ARE_XY_VALUES unset) are vanishingly
                // rare; treat the anchor as a zero offset rather than failing the glyph.
                if ((flags & 0x0002) == 0)
                {
                    argX = 0f;
                    argY = 0f;
                }

                float a = 1f, b = 0f, c = 0f, d = 1f;

                if ((flags & 0x0008) != 0) // WE_HAVE_A_SCALE
                {
                    a = d = ReadF2Dot14(cursor);
                    cursor += 2;
                }
                else if ((flags & 0x0040) != 0) // X_AND_Y_SCALE
                {
                    a = ReadF2Dot14(cursor);
                    d = ReadF2Dot14(cursor + 2);
                    cursor += 4;
                }
                else if ((flags & 0x0080) != 0) // TWO_BY_TWO
                {
                    a = ReadF2Dot14(cursor);
                    b = ReadF2Dot14(cursor + 2);
                    c = ReadF2Dot14(cursor + 4);
                    d = ReadF2Dot14(cursor + 6);
                    cursor += 8;
                }

                // Compose child transform (a,b,c,d,argX,argY) with the parent transform.
                float cxx = a * xx + b * yx;
                float cxy = a * xy + b * yy;
                float cyx = c * xx + d * yx;
                float cyy = c * xy + d * yy;
                float cdx = argX * xx + argY * yx + dx;
                float cdy = argX * xy + argY * yy + dy;

                AppendGlyph(componentIndex, outline, cxx, cxy, cyx, cyy, cdx, cdy, depth + 1);

                if ((flags & 0x0020) == 0) // MORE_COMPONENTS
                    break;
            }

            return true;
        }

        void AppendSimpleGlyph(
            int glyph,
            int contourCount,
            NowGlyphOutline outline,
            float xx, float xy, float yx, float yy, float dx, float dy)
        {
            int endPts = glyph + 10;
            int pointCount = ReadU16(endPts + (contourCount - 1) * 2) + 1;
            int instructionLength = ReadU16(endPts + contourCount * 2);
            int flagsStart = endPts + contourCount * 2 + 2 + instructionLength;

            // Decode flags (with repeats), then the delta-encoded coordinate streams.
            Span<byte> flags = pointCount <= 512 ? stackalloc byte[pointCount] : new byte[pointCount];
            int cursor = flagsStart;

            for (int i = 0; i < pointCount;)
            {
                byte flag = _data[cursor++];
                flags[i++] = flag;

                if ((flag & 0x08) != 0) // REPEAT
                {
                    int repeats = _data[cursor++];

                    for (int r = 0; r < repeats && i < pointCount; ++r)
                        flags[i++] = flag;
                }
            }

            int basePoint = outline.points.Count;
            int x = 0;

            for (int i = 0; i < pointCount; ++i)
            {
                byte flag = flags[i];

                if ((flag & 0x02) != 0) // X_SHORT
                {
                    int delta = _data[cursor++];
                    x += (flag & 0x10) != 0 ? delta : -delta;
                }
                else if ((flag & 0x10) == 0) // not SAME
                {
                    x += ReadS16(cursor);
                    cursor += 2;
                }

                outline.points.Add(new Vector2(x, 0f));
                outline.onCurve.Add((flag & 0x01) != 0);
            }

            int y = 0;

            for (int i = 0; i < pointCount; ++i)
            {
                byte flag = flags[i];

                if ((flag & 0x04) != 0) // Y_SHORT
                {
                    int delta = _data[cursor++];
                    y += (flag & 0x20) != 0 ? delta : -delta;
                }
                else if ((flag & 0x20) == 0) // not SAME
                {
                    y += ReadS16(cursor);
                    cursor += 2;
                }

                Vector2 p = outline.points[basePoint + i];
                p.y = y;

                // Apply the composite transform (identity for top-level glyphs).
                outline.points[basePoint + i] = new Vector2(
                    p.x * xx + p.y * yx + dx,
                    p.x * xy + p.y * yy + dy);
            }

            for (int i = 0; i < contourCount; ++i)
                outline.contourEnds.Add(basePoint + ReadU16(endPts + i * 2) + 1);
        }

        float ReadF2Dot14(int offset)
        {
            return ReadS16(offset) / 16384f;
        }

        ushort ReadU16(int offset)
        {
            return (ushort)((_data[offset] << 8) | _data[offset + 1]);
        }

        short ReadS16(int offset)
        {
            return (short)((_data[offset] << 8) | _data[offset + 1]);
        }

        uint ReadU32(int offset)
        {
            return (uint)((_data[offset] << 24) | (_data[offset + 1] << 16) | (_data[offset + 2] << 8) | _data[offset + 3]);
        }
    }
}
