using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NowUI.Editor
{
    internal readonly struct NowFontGlyphInfo
    {
        public readonly int codepoint;
        public readonly int glyphIndex;
        public readonly string name;

        public NowFontGlyphInfo(int codepoint, int glyphIndex, string name)
        {
            this.codepoint = codepoint;
            this.glyphIndex = glyphIndex;
            this.name = string.IsNullOrWhiteSpace(name) ? null : name;
        }

        public string displayName => !string.IsNullOrEmpty(name) ? name : glyphIndex >= 0 ? $"glyph{glyphIndex}" : "unnamed";

        public string codepointLabel => $"U+{codepoint:X4}";

        public bool TryGetCharacter(out string character)
        {
            if (!IsValidCodepoint(codepoint))
            {
                character = null;
                return false;
            }

            character = char.ConvertFromUtf32(codepoint);
            return true;
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            var tokens = query.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; ++i)
            {
                if (!MatchesToken(tokens[i]))
                    return false;
            }

            return true;
        }

        bool MatchesToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return true;

            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (displayName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                codepointLabel.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (TryGetCharacter(out var character) && character == token)
                return true;

            if (token.StartsWith("gid:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token.Substring(4), out var queriedGlyphIndex))
            {
                return glyphIndex == queriedGlyphIndex;
            }

            string hex = token.StartsWith("u+", StringComparison.OrdinalIgnoreCase)
                ? token.Substring(2)
                : token;

            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var queriedCodepoint) &&
                queriedCodepoint == codepoint;
        }

        static bool IsValidCodepoint(int value)
        {
            return value > 0 && value <= 0x10ffff && (value < 0xd800 || value > 0xdfff);
        }
    }

    internal sealed class NowFontGlyphCatalog
    {
        public readonly NowFontGlyphInfo[] glyphs;

        NowFontGlyphCatalog(NowFontGlyphInfo[] glyphs)
        {
            this.glyphs = glyphs ?? Array.Empty<NowFontGlyphInfo>();
        }

        public static NowFontGlyphCatalog Load(NowFont font)
        {
            if (font == null)
                return new NowFontGlyphCatalog(Array.Empty<NowFontGlyphInfo>());

            if (!font.TryGetSourceBytes(out var sourceBytes))
                return LoadFromAtlas(font, "Embedded source bytes are required for glyph names; showing compiled atlas codepoints only.");

            var parser = new OpenTypeParser(sourceBytes);

            if (!parser.TryReadCmap(out var mappings, out var error))
            {
                var fallbackMessage = string.IsNullOrEmpty(error)
                    ? "Could not read the font cmap; showing compiled atlas codepoints only."
                    : $"{error} Showing compiled atlas codepoints only.";

                return LoadFromAtlas(font, fallbackMessage);
            }

            var names = parser.ReadGlyphNames();
            var glyphs = new List<NowFontGlyphInfo>(mappings.Count);
            bool hasNames = false;

            for (int i = 0; i < mappings.Count; ++i)
            {
                var mapping = mappings[i];
                string name = null;

                if (names != null && mapping.glyphIndex >= 0 && mapping.glyphIndex < names.Length)
                {
                    name = names[mapping.glyphIndex];
                    hasNames |= !string.IsNullOrEmpty(name);
                }

                glyphs.Add(new NowFontGlyphInfo(mapping.codepoint, mapping.glyphIndex, name));
            }

            glyphs.Sort((a, b) => a.codepoint.CompareTo(b.codepoint));
            return new NowFontGlyphCatalog(glyphs.ToArray());
        }

        static NowFontGlyphCatalog LoadFromAtlas(NowFont font, string message)
        {
            if (font == null || font.atlasInfo.glyphs == null || font.atlasInfo.glyphs.Length == 0)
                return new NowFontGlyphCatalog(Array.Empty<NowFontGlyphInfo>());

            var source = font.atlasInfo.glyphs;
            var glyphs = new List<NowFontGlyphInfo>(source.Length);
            var seenCodepoints = new HashSet<int>();

            for (int i = 0; i < source.Length; ++i)
            {
                int codepoint = source[i].unicode;

                if (codepoint <= 0 || !seenCodepoints.Add(codepoint))
                    continue;

                glyphs.Add(new NowFontGlyphInfo(codepoint, -1, null));
            }

            glyphs.Sort((a, b) => a.codepoint.CompareTo(b.codepoint));
            return new NowFontGlyphCatalog(glyphs.ToArray());
        }

        readonly struct CmapMapping
        {
            public readonly int codepoint;
            public readonly int glyphIndex;

            public CmapMapping(int codepoint, int glyphIndex)
            {
                this.codepoint = codepoint;
                this.glyphIndex = glyphIndex;
            }
        }

        readonly struct OpenTypeTable
        {
            public readonly int offset;
            public readonly int length;

            public OpenTypeTable(int offset, int length)
            {
                this.offset = offset;
                this.length = length;
            }
        }

        readonly struct CmapCandidate
        {
            public readonly int format;
            public readonly int platformId;
            public readonly int encodingId;
            public readonly List<CmapMapping> mappings;

            public CmapCandidate(int format, int platformId, int encodingId, List<CmapMapping> mappings)
            {
                this.format = format;
                this.platformId = platformId;
                this.encodingId = encodingId;
                this.mappings = mappings;
            }

            public int score => FormatScore(format) + PlatformScore(platformId, encodingId);

            static int FormatScore(int format)
            {
                switch (format)
                {
                    case 12:
                        return 400;
                    case 4:
                        return 300;
                    case 6:
                        return 200;
                    case 0:
                        return 100;
                    case 13:
                        return 75;
                    default:
                        return 0;
                }
            }

            static int PlatformScore(int platformId, int encodingId)
            {
                if (platformId == 3 && encodingId == 10)
                    return 40;

                if (platformId == 0)
                    return 35;

                if (platformId == 3 && encodingId == 1)
                    return 30;

                if (platformId == 1)
                    return 5;

                return 0;
            }
        }

        sealed class OpenTypeParser
        {
            const uint TTC_TAG = 0x74746366;
            const int TABLE_RECORD_SIZE = 16;
            const int STANDARD_GLYPH_NAME_COUNT = 258;
            const int MAX_CMAP_MAPPINGS = 100000;

            static readonly string[] StandardGlyphNames =
            {
                ".notdef", ".null", "nonmarkingreturn", "space", "exclam", "quotedbl", "numbersign", "dollar",
                "percent", "ampersand", "quotesingle", "parenleft", "parenright", "asterisk", "plus", "comma",
                "hyphen", "period", "slash", "zero", "one", "two", "three", "four", "five", "six", "seven",
                "eight", "nine", "colon", "semicolon", "less", "equal", "greater", "question", "at", "A", "B",
                "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T",
                "U", "V", "W", "X", "Y", "Z", "bracketleft", "backslash", "bracketright", "asciicircum",
                "underscore", "grave", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n",
                "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "braceleft", "bar", "braceright",
                "asciitilde", "Adieresis", "Aring", "Ccedilla", "Eacute", "Ntilde", "Odieresis", "Udieresis",
                "aacute", "agrave", "acircumflex", "adieresis", "atilde", "aring", "ccedilla", "eacute",
                "egrave", "ecircumflex", "edieresis", "iacute", "igrave", "icircumflex", "idieresis", "ntilde",
                "oacute", "ograve", "ocircumflex", "odieresis", "otilde", "uacute", "ugrave", "ucircumflex",
                "udieresis", "dagger", "degree", "cent", "sterling", "section", "bullet", "paragraph",
                "germandbls", "registered", "copyright", "trademark", "acute", "dieresis", "notequal", "AE",
                "Oslash", "infinity", "plusminus", "lessequal", "greaterequal", "yen", "mu", "partialdiff",
                "summation", "product", "pi", "integral", "ordfeminine", "ordmasculine", "Omega", "ae",
                "oslash", "questiondown", "exclamdown", "logicalnot", "radical", "florin", "approxequal",
                "Delta", "guillemotleft", "guillemotright", "ellipsis", "nonbreakingspace", "Agrave", "Atilde",
                "Otilde", "OE", "oe", "endash", "emdash", "quotedblleft", "quotedblright", "quoteleft",
                "quoteright", "divide", "lozenge", "ydieresis", "Ydieresis", "fraction", "currency",
                "guilsinglleft", "guilsinglright", "fi", "fl", "daggerdbl", "periodcentered",
                "quotesinglbase", "quotedblbase", "perthousand", "Acircumflex", "Ecircumflex", "Aacute",
                "Edieresis", "Egrave", "Iacute", "Icircumflex", "Idieresis", "Igrave", "Oacute",
                "Ocircumflex", "apple", "Ograve", "Uacute", "Ucircumflex", "Ugrave", "dotlessi", "circumflex",
                "tilde", "macron", "breve", "dotaccent", "ring", "cedilla", "hungarumlaut", "ogonek", "caron",
                "Lslash", "lslash", "Scaron", "scaron", "Zcaron", "zcaron", "brokenbar", "Eth", "eth",
                "Yacute", "yacute", "Thorn", "thorn", "minus", "multiply", "onesuperior", "twosuperior",
                "threesuperior", "onehalf", "onequarter", "threequarters", "franc", "Gbreve", "gbreve",
                "Idotaccent", "Scedilla", "scedilla", "Cacute", "cacute", "Ccaron", "ccaron", "dcroat"
            };

            readonly byte[] _data;
            readonly Dictionary<string, OpenTypeTable> _tables = new Dictionary<string, OpenTypeTable>();
            bool _didReadDirectory;
            string _directoryError;

            public OpenTypeParser(byte[] data)
            {
                _data = data;
            }

            public bool TryReadCmap(out List<CmapMapping> mappings, out string error)
            {
                mappings = null;

                if (!TryGetTable("cmap", out var cmapTable, out error))
                    return false;

                int cmapOffset = cmapTable.offset;
                int cmapEnd = cmapOffset + cmapTable.length;

                if (!CanRead(cmapOffset, 4))
                {
                    error = "The font cmap table is truncated.";
                    return false;
                }

                int encodingCount = ReadUInt16(cmapOffset + 2);
                int recordsEnd = cmapOffset + 4 + encodingCount * 8;

                if (encodingCount <= 0 || recordsEnd > cmapEnd)
                {
                    error = "The font cmap table has invalid encoding records.";
                    return false;
                }

                CmapCandidate best = default;
                bool hasBest = false;

                for (int i = 0; i < encodingCount; ++i)
                {
                    int recordOffset = cmapOffset + 4 + i * 8;
                    int platformId = ReadUInt16(recordOffset);
                    int encodingId = ReadUInt16(recordOffset + 2);
                    uint subtableRelativeOffset = ReadUInt32(recordOffset + 4);

                    if (subtableRelativeOffset > int.MaxValue)
                        continue;

                    int subtableOffset = cmapOffset + (int)subtableRelativeOffset;

                    if (subtableOffset < cmapOffset || subtableOffset >= cmapEnd)
                        continue;

                    if (!TryReadCmapSubtable(subtableOffset, cmapEnd - subtableOffset, out var candidateMappings, out var format))
                        continue;

                    if (candidateMappings.Count == 0)
                        continue;

                    var candidate = new CmapCandidate(format, platformId, encodingId, candidateMappings);

                    if (!hasBest ||
                        candidate.score > best.score ||
                        (candidate.score == best.score && candidate.mappings.Count > best.mappings.Count))
                    {
                        best = candidate;
                        hasBest = true;
                    }
                }

                if (!hasBest)
                {
                    error = "No supported cmap subtable was found.";
                    return false;
                }

                mappings = best.mappings;
                error = null;
                return true;
            }

            public string[] ReadGlyphNames()
            {
                if (!TryGetTable("post", out var postTable, out _))
                    return null;

                int offset = postTable.offset;
                int end = offset + postTable.length;

                if (!CanRead(offset, 34) || ReadUInt32(offset) != 0x00020000)
                    return null;

                int glyphCount = ReadUInt16(offset + 32);
                int indicesOffset = offset + 34;
                int indicesEnd = indicesOffset + glyphCount * 2;

                if (glyphCount <= 0 || glyphCount > 100000 || indicesEnd > end)
                    return null;

                var names = new string[glyphCount];
                var indices = new ushort[glyphCount];
                int highestCustomIndex = -1;

                for (int i = 0; i < glyphCount; ++i)
                {
                    ushort nameIndex = ReadUInt16(indicesOffset + i * 2);
                    indices[i] = nameIndex;

                    if (nameIndex < StandardGlyphNames.Length)
                    {
                        names[i] = StandardGlyphNames[nameIndex];
                        continue;
                    }

                    highestCustomIndex = Math.Max(highestCustomIndex, nameIndex - STANDARD_GLYPH_NAME_COUNT);
                }

                if (highestCustomIndex < 0)
                    return names;

                var customNames = new string[highestCustomIndex + 1];
                int cursor = indicesEnd;

                for (int i = 0; i < customNames.Length && cursor < end; ++i)
                {
                    int length = _data[cursor++];

                    if (length <= 0)
                    {
                        customNames[i] = string.Empty;
                        continue;
                    }

                    if (cursor + length > end)
                        break;

                    customNames[i] = Encoding.ASCII.GetString(_data, cursor, length);
                    cursor += length;
                }

                for (int i = 0; i < glyphCount; ++i)
                {
                    int customIndex = indices[i] - STANDARD_GLYPH_NAME_COUNT;

                    if (customIndex >= 0 && customIndex < customNames.Length)
                        names[i] = customNames[customIndex];
                }

                return names;
            }

            bool TryReadCmapSubtable(int offset, int maxLength, out List<CmapMapping> mappings, out int format)
            {
                mappings = null;
                format = 0;

                if (maxLength < 2 || !CanRead(offset, 2))
                    return false;

                format = ReadUInt16(offset);

                switch (format)
                {
                    case 0:
                        return TryReadFormat0(offset, maxLength, out mappings);
                    case 4:
                        return TryReadFormat4(offset, maxLength, out mappings);
                    case 6:
                        return TryReadFormat6(offset, maxLength, out mappings);
                    case 12:
                        return TryReadFormat12(offset, maxLength, false, out mappings);
                    case 13:
                        return TryReadFormat12(offset, maxLength, true, out mappings);
                    default:
                        return false;
                }
            }

            bool TryReadFormat0(int offset, int maxLength, out List<CmapMapping> mappings)
            {
                mappings = null;

                if (!TryGetUInt16Length(offset, maxLength, out var length) || length < 262)
                    return false;

                mappings = new List<CmapMapping>(256);

                for (int codepoint = 0; codepoint < 256; ++codepoint)
                    AddMapping(mappings, codepoint, _data[offset + 6 + codepoint]);

                return true;
            }

            bool TryReadFormat4(int offset, int maxLength, out List<CmapMapping> mappings)
            {
                mappings = null;

                if (!TryGetUInt16Length(offset, maxLength, out var length) || length < 16)
                    return false;

                int segCount = ReadUInt16(offset + 6) / 2;

                if (segCount <= 0)
                    return false;

                int endCodeOffset = offset + 14;
                int startCodeOffset = endCodeOffset + segCount * 2 + 2;
                int idDeltaOffset = startCodeOffset + segCount * 2;
                int idRangeOffsetOffset = idDeltaOffset + segCount * 2;
                int tableEnd = offset + length;

                if (idRangeOffsetOffset + segCount * 2 > tableEnd)
                    return false;

                var byCodepoint = new Dictionary<int, int>();

                for (int segment = 0; segment < segCount; ++segment)
                {
                    int endCode = ReadUInt16(endCodeOffset + segment * 2);
                    int startCode = ReadUInt16(startCodeOffset + segment * 2);
                    int idDelta = unchecked((short)ReadUInt16(idDeltaOffset + segment * 2));
                    int idRangeOffsetAddress = idRangeOffsetOffset + segment * 2;
                    int idRangeOffset = ReadUInt16(idRangeOffsetAddress);

                    if (startCode > endCode)
                        continue;

                    for (int codepoint = startCode; codepoint <= endCode; ++codepoint)
                    {
                        if (codepoint == 0xffff)
                            continue;

                        int glyphIndex;

                        if (idRangeOffset == 0)
                        {
                            glyphIndex = (codepoint + idDelta) & 0xffff;
                        }
                        else
                        {
                            int glyphIndexAddress = idRangeOffsetAddress + idRangeOffset + (codepoint - startCode) * 2;

                            if (glyphIndexAddress < offset || glyphIndexAddress + 2 > tableEnd)
                                continue;

                            glyphIndex = ReadUInt16(glyphIndexAddress);

                            if (glyphIndex != 0)
                                glyphIndex = (glyphIndex + idDelta) & 0xffff;
                        }

                        if (glyphIndex > 0)
                            byCodepoint[codepoint] = glyphIndex;

                        if (byCodepoint.Count >= MAX_CMAP_MAPPINGS)
                            break;
                    }

                    if (byCodepoint.Count >= MAX_CMAP_MAPPINGS)
                        break;
                }

                mappings = ToSortedMappings(byCodepoint);
                return true;
            }

            bool TryReadFormat6(int offset, int maxLength, out List<CmapMapping> mappings)
            {
                mappings = null;

                if (!TryGetUInt16Length(offset, maxLength, out var length) || length < 10)
                    return false;

                int firstCode = ReadUInt16(offset + 6);
                int entryCount = ReadUInt16(offset + 8);
                int glyphsOffset = offset + 10;

                if (glyphsOffset + entryCount * 2 > offset + length)
                    return false;

                mappings = new List<CmapMapping>(entryCount);

                for (int i = 0; i < entryCount; ++i)
                    AddMapping(mappings, firstCode + i, ReadUInt16(glyphsOffset + i * 2));

                return true;
            }

            bool TryReadFormat12(int offset, int maxLength, bool constantGlyphIndex, out List<CmapMapping> mappings)
            {
                mappings = null;

                if (!CanRead(offset, 16))
                    return false;

                uint length = ReadUInt32(offset + 4);

                if (length < 16 || length > maxLength || offset + length > _data.Length)
                    return false;

                uint groupCount = ReadUInt32(offset + 12);

                if (groupCount > 100000)
                    return false;

                int groupsOffset = offset + 16;
                int tableEnd = offset + (int)length;

                if (groupsOffset + groupCount * 12 > tableEnd)
                    return false;

                var byCodepoint = new Dictionary<int, int>();

                for (int i = 0; i < groupCount; ++i)
                {
                    int groupOffset = groupsOffset + i * 12;
                    uint start = ReadUInt32(groupOffset);
                    uint end = ReadUInt32(groupOffset + 4);
                    uint startGlyph = ReadUInt32(groupOffset + 8);

                    if (start > end || end > 0x10ffff)
                        continue;

                    for (uint codepoint = start; codepoint <= end; ++codepoint)
                    {
                        uint glyphIndex = constantGlyphIndex
                            ? startGlyph
                            : startGlyph + codepoint - start;

                        if (glyphIndex > int.MaxValue)
                            continue;

                        int codepointValue = unchecked((int)codepoint);

                        if (IsValidCodepoint(codepointValue) && glyphIndex > 0)
                            byCodepoint[codepointValue] = unchecked((int)glyphIndex);

                        if (byCodepoint.Count >= MAX_CMAP_MAPPINGS)
                            break;

                        if (codepoint == uint.MaxValue)
                            break;
                    }

                    if (byCodepoint.Count >= MAX_CMAP_MAPPINGS)
                        break;
                }

                mappings = ToSortedMappings(byCodepoint);
                return true;
            }

            bool TryGetTable(string tag, out OpenTypeTable table, out string error)
            {
                table = default;

                if (!TryReadTableDirectory(out error))
                    return false;

                if (_tables.TryGetValue(tag, out table))
                    return true;

                error = $"The font does not contain a {tag} table.";
                return false;
            }

            bool TryReadTableDirectory(out string error)
            {
                if (_didReadDirectory)
                {
                    error = _directoryError;
                    return string.IsNullOrEmpty(error);
                }

                _didReadDirectory = true;

                if (_data == null || _data.Length < 12)
                {
                    error = _directoryError = "Font data is empty or truncated.";
                    return false;
                }

                int sfntOffset = 0;

                if (ReadUInt32(0) == TTC_TAG)
                {
                    if (!CanRead(0, 16) || ReadUInt32(8) == 0)
                    {
                        error = _directoryError = "The font collection header is invalid.";
                        return false;
                    }

                    uint firstFontOffset = ReadUInt32(12);

                    if (firstFontOffset > int.MaxValue)
                    {
                        error = _directoryError = "The first font in the collection is out of range.";
                        return false;
                    }

                    sfntOffset = (int)firstFontOffset;
                }

                if (!CanRead(sfntOffset, 12))
                {
                    error = _directoryError = "The font table directory is truncated.";
                    return false;
                }

                int tableCount = ReadUInt16(sfntOffset + 4);
                int recordsOffset = sfntOffset + 12;
                int recordsEnd = recordsOffset + tableCount * TABLE_RECORD_SIZE;

                if (tableCount <= 0 || recordsEnd > _data.Length)
                {
                    error = _directoryError = "The font table directory is invalid.";
                    return false;
                }

                for (int i = 0; i < tableCount; ++i)
                {
                    int recordOffset = recordsOffset + i * TABLE_RECORD_SIZE;
                    string tag = Encoding.ASCII.GetString(_data, recordOffset, 4);
                    uint rawOffset = ReadUInt32(recordOffset + 8);
                    uint rawLength = ReadUInt32(recordOffset + 12);

                    if (rawOffset > int.MaxValue || rawLength > int.MaxValue)
                        continue;

                    int tableOffset = (int)rawOffset;
                    int tableLength = (int)rawLength;

                    if (tableOffset < 0 ||
                        tableLength < 0 ||
                        tableOffset > _data.Length ||
                        tableLength > _data.Length - tableOffset)
                    {
                        continue;
                    }

                    _tables[tag] = new OpenTypeTable(tableOffset, tableLength);
                }

                error = _directoryError = null;
                return true;
            }

            bool TryGetUInt16Length(int offset, int maxLength, out int length)
            {
                length = 0;

                if (!CanRead(offset, 4))
                    return false;

                length = ReadUInt16(offset + 2);
                return length <= maxLength && offset + length <= _data.Length;
            }

            bool CanRead(int offset, int length)
            {
                return _data != null &&
                    offset >= 0 &&
                    length >= 0 &&
                    offset <= _data.Length &&
                    length <= _data.Length - offset;
            }

            ushort ReadUInt16(int offset)
            {
                return (ushort)((_data[offset] << 8) | _data[offset + 1]);
            }

            uint ReadUInt32(int offset)
            {
                return ((uint)_data[offset] << 24) |
                    ((uint)_data[offset + 1] << 16) |
                    ((uint)_data[offset + 2] << 8) |
                    _data[offset + 3];
            }

            static void AddMapping(List<CmapMapping> mappings, int codepoint, int glyphIndex)
            {
                if (IsValidCodepoint(codepoint) && glyphIndex > 0)
                    mappings.Add(new CmapMapping(codepoint, glyphIndex));
            }

            static List<CmapMapping> ToSortedMappings(Dictionary<int, int> byCodepoint)
            {
                var mappings = new List<CmapMapping>(byCodepoint.Count);

                foreach (var mapping in byCodepoint)
                    mappings.Add(new CmapMapping(mapping.Key, mapping.Value));

                mappings.Sort((a, b) => a.codepoint.CompareTo(b.codepoint));
                return mappings;
            }

            static bool IsValidCodepoint(int value)
            {
                return value > 0 && value <= 0x10ffff && (value < 0xd800 || value > 0xdfff);
            }
        }
    }
}
