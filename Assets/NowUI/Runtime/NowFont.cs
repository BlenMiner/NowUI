using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public struct NowFontAtlasInfo
{
    [Serializable]
    public struct Atlas
    {
        public string type;
        public int distanceRange;
        public int size;
        public int width;
        public int height;
        public string yOrigin;
    }

    [Serializable]
    public struct Metrics
    {
        public float emSize;
        public float lineHeight;
        public float ascender;
        public float descender;
        public float underlineY;
        public float underlineThickness;
    }

    [Serializable]
    public struct Bounds
    {
        public float left;
        public float bottom;
        public float right;
        public float top;
    }

    [Serializable]
    public struct Glyph
    {
        public int unicode;
        public float advance;
        public Bounds planeBounds;
        public Bounds atlasBounds;
    }

    public Atlas atlas;

    public Metrics metrics;

    public Glyph[] glyphs;
}

[Flags]
public enum NowFontStyle
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
    BoldItalic = Bold | Italic
}

public abstract class NowFontAsset : ScriptableObject
{
    [SerializeField]
    NowFontAsset[] _fallbacks;

    [NonSerialized]
    HashSet<NowFontAsset> _visitCache;

    public IReadOnlyList<NowFontAsset> fallbacks => _fallbacks;

    protected abstract bool TryGetOwnFont(NowFontStyle style, out NowFont font);

    HashSet<NowFontAsset> GetVisitCache()
    {
        _visitCache ??= new HashSet<NowFontAsset>();
        _visitCache.Clear();
        return _visitCache;
    }

    public bool TryResolveFont(NowFontStyle style, out NowFont font)
    {
        var visited = GetVisitCache();

        try
        {
            return TryResolveFont(style, visited, out font);
        }
        finally
        {
            visited.Clear();
        }
    }

    internal bool TryResolveFont(NowFontStyle style, HashSet<NowFontAsset> visited, out NowFont font)
    {
        font = null;

        if (this == null || !visited.Add(this))
            return false;

        if (TryGetOwnFont(style, out font) && font != null)
            return true;

        if (_fallbacks == null)
            return false;

        for (int i = 0; i < _fallbacks.Length; ++i)
        {
            var fallback = _fallbacks[i];

            if (fallback != null && fallback.TryResolveFont(style, visited, out font))
                return true;
        }

        return false;
    }

    public bool TryResolveGlyph(
        int unicode,
        float fontSize,
        NowFontStyle style,
        out NowFont font,
        out NowFontAtlasInfo.Glyph glyph,
        out Material material)
    {
        var visited = GetVisitCache();

        try
        {
            return TryResolveGlyph(unicode, fontSize, style, visited, out font, out glyph, out material);
        }
        finally
        {
            visited.Clear();
        }
    }

    internal bool TryResolveGlyph(
        int unicode,
        float fontSize,
        NowFontStyle style,
        HashSet<NowFontAsset> visited,
        out NowFont font,
        out NowFontAtlasInfo.Glyph glyph,
        out Material material)
    {
        font = null;
        glyph = default;
        material = null;

        if (this == null || !visited.Add(this))
            return false;

        if (TryGetOwnFont(style, out var ownFont) &&
            ownFont != null &&
            ownFont.GetGlyph(unicode, fontSize, out glyph, out material))
        {
            font = ownFont;
            return true;
        }

        if (_fallbacks == null)
            return false;

        for (int i = 0; i < _fallbacks.Length; ++i)
        {
            var fallback = _fallbacks[i];

            if (fallback != null &&
                fallback.TryResolveGlyph(unicode, fontSize, style, visited, out font, out glyph, out material))
            {
                return true;
            }
        }

        return false;
    }

    public virtual void EnsureGlyphs(string value, float fontSize, NowFontStyle style = NowFontStyle.Regular)
    {
        var visited = GetVisitCache();

        try
        {
            EnsureGlyphs(value, fontSize, style, visited);
        }
        finally
        {
            visited.Clear();
        }
    }

    internal void EnsureGlyphs(
        string value,
        float fontSize,
        NowFontStyle style,
        HashSet<NowFontAsset> visited)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return;

        if (this == null || !visited.Add(this))
            return;

        if (TryGetOwnFont(style, out var font) && font != null)
            font.EnsureGlyphs(value, fontSize);

        if (_fallbacks == null)
            return;

        for (int i = 0; i < _fallbacks.Length; ++i)
        {
            var fallback = _fallbacks[i];

            if (fallback != null)
                fallback.EnsureGlyphs(value, fontSize, style, visited);
        }
    }

    public float GetLineHeight(NowFontStyle style = NowFontStyle.Regular)
    {
        var visited = GetVisitCache();

        try
        {
            return GetLineHeight(style, visited);
        }
        finally
        {
            visited.Clear();
        }
    }

    internal float GetLineHeight(NowFontStyle style, HashSet<NowFontAsset> visited)
    {
        if (this == null || !visited.Add(this))
            return 1;

        if (TryGetOwnFont(style, out var font) && font != null)
            return font.GetLineHeight();

        if (_fallbacks != null)
        {
            for (int i = 0; i < _fallbacks.Length; ++i)
            {
                var fallback = _fallbacks[i];

                if (fallback != null)
                    return fallback.GetLineHeight(style, visited);
            }
        }

        return 1;
    }

    /// <summary>
    /// Distance from the top of a line box to its baseline, in em units. Used to
    /// position the first baseline so ascent and descent both fit inside the
    /// measured line height.
    /// </summary>
    public float GetAscender(NowFontStyle style = NowFontStyle.Regular)
    {
        var visited = GetVisitCache();

        try
        {
            return GetAscender(style, visited);
        }
        finally
        {
            visited.Clear();
        }
    }

    internal float GetAscender(NowFontStyle style, HashSet<NowFontAsset> visited)
    {
        if (this == null || !visited.Add(this))
            return 1;

        if (TryGetOwnFont(style, out var font) && font != null)
            return font.GetAscender();

        if (_fallbacks != null)
        {
            for (int i = 0; i < _fallbacks.Length; ++i)
            {
                var fallback = _fallbacks[i];

                if (fallback != null)
                    return fallback.GetAscender(style, visited);
            }
        }

        return 1;
    }

    public virtual Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
    {
        return MeasureText(value, fontSize, NowFontStyle.Regular, tabSpaces);
    }

    public virtual Vector2 MeasureText(string value, float fontSize, NowFontStyle style, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return default;

        float lineWidth = 0;
        float maxWidth = 0;
        int lineCount = 1;
        var visited = GetVisitCache();

        EnsureGlyphs(value, fontSize, style, visited);
        visited.Clear();

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            if (codepoint == '\n')
            {
                if (lineWidth > maxWidth)
                    maxWidth = lineWidth;

                lineWidth = 0;
                ++lineCount;
                continue;
            }

            if (codepoint == '\t')
            {
                visited.Clear();

                if (TryResolveGlyph(' ', fontSize, style, visited, out _, out var space, out _))
                    lineWidth += space.advance * fontSize * tabSpaces;

                continue;
            }

            visited.Clear();

            if (TryResolveGlyph(codepoint, fontSize, style, visited, out _, out var glyph, out _))
                lineWidth += glyph.advance * fontSize;
        }

        if (lineWidth > maxWidth)
            maxWidth = lineWidth;

        visited.Clear();
        float lineHeight = GetLineHeight(style, visited);
        visited.Clear();
        return new Vector2(maxWidth, lineHeight * fontSize * lineCount);
    }

    public virtual Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
    {
        return MeasureTextBounds(value, fontSize, NowFontStyle.Regular, tabSpaces);
    }

    public virtual Vector4 MeasureTextBounds(string value, float fontSize, NowFontStyle style, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return default;

        float cursorX = 0;
        float lineY = 0;
        float lineHeight = 0;
        float baseline = 0;
        float minX = 0;
        float minY = 0;
        float maxX = 0;
        float maxY = 0;
        bool hasBounds = false;
        var visited = GetVisitCache();

        EnsureGlyphs(value, fontSize, style, visited);
        visited.Clear();

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            if (codepoint == '\n')
            {
                if (lineHeight <= 0)
                {
                    visited.Clear();
                    lineHeight = GetLineHeight(style, visited) * fontSize;
                    visited.Clear();
                    baseline = GetAscender(style, visited) * fontSize;
                }

                cursorX = 0;
                lineY += lineHeight;
                continue;
            }

            if (codepoint == '\t')
            {
                visited.Clear();

                if (TryResolveGlyph(' ', fontSize, style, visited, out _, out var space, out _))
                    cursorX += space.advance * fontSize * tabSpaces;

                continue;
            }

            visited.Clear();

            if (!TryResolveGlyph(codepoint, fontSize, style, visited, out _, out var glyph, out _))
                continue;

            if (lineHeight <= 0)
            {
                visited.Clear();
                lineHeight = GetLineHeight(style, visited) * fontSize;
                visited.Clear();
                baseline = GetAscender(style, visited) * fontSize;
            }

            if (glyph.atlasBounds.left != glyph.atlasBounds.right)
            {
                float glyphLeft = cursorX + glyph.planeBounds.left * fontSize;
                float glyphRight = cursorX + glyph.planeBounds.right * fontSize;
                float glyphTop = lineY + baseline - glyph.planeBounds.top * fontSize;
                float glyphBottom = lineY + baseline - glyph.planeBounds.bottom * fontSize;

                if (!hasBounds)
                {
                    minX = glyphLeft;
                    minY = glyphTop;
                    maxX = glyphRight;
                    maxY = glyphBottom;
                    hasBounds = true;
                }
                else
                {
                    if (glyphLeft < minX)
                        minX = glyphLeft;

                    if (glyphTop < minY)
                        minY = glyphTop;

                    if (glyphRight > maxX)
                        maxX = glyphRight;

                    if (glyphBottom > maxY)
                        maxY = glyphBottom;
                }
            }

            cursorX += glyph.advance * fontSize;
        }

        visited.Clear();
        return hasBounds ? new Vector4(minX, minY, maxX - minX, maxY - minY) : default;
    }
}

[PreferBinarySerialization]
public class NowFont : NowFontAsset
{
    public const string ATLAS_TYPE_MTSDF = "mtsdf";
    public const string ATLAS_TYPE_RGBA = "rgba";
    public const int DEFAULT_DYNAMIC_ATLAS_SIZE = 64;
    public const int DEFAULT_DYNAMIC_PIXEL_RANGE = 16;
    public const int DEFAULT_DYNAMIC_PAGE_SIZE = 1024;
    public const int DEFAULT_DYNAMIC_MAX_ATLAS_SIZE = 2048;
    public const int DEFAULT_DYNAMIC_MAX_ATLAS_BYTES = 16 * 1024 * 1024;
    const uint OPENTYPE_TTC_TAG = 0x74746366;
    const int DYNAMIC_GLYPH_PADDING = 1;
    const int MAX_CMAP_ENCODING_RECORDS = 1024;
    const int MAX_DYNAMIC_SOURCE_CMAP_CODEPOINTS = 200000;

    [HideInInspector]
    public Texture2D atlas;

    [HideInInspector]
    public NowFontAtlasInfo atlasInfo;

    [HideInInspector]
    public Material material;

    [SerializeField, HideInInspector]
    byte[] _fontBytes;

    public int dynamicAtlasSize = DEFAULT_DYNAMIC_ATLAS_SIZE;

    public int dynamicPixelRange = DEFAULT_DYNAMIC_PIXEL_RANGE;

    public int dynamicPageSize = DEFAULT_DYNAMIC_PAGE_SIZE;

    public int dynamicMaxAtlasSize = DEFAULT_DYNAMIC_MAX_ATLAS_SIZE;

    public int dynamicMaxAtlasBytes = DEFAULT_DYNAMIC_MAX_ATLAS_BYTES;

    public bool isColor => atlasInfo.atlas.type == ATLAS_TYPE_RGBA;

    class DynamicAtlasPage
    {
        public NowFont font;
        public HashSet<int> codepoints;
        public int atlasSize;
        public int cursorX;
        public int cursorY;
        public int rowHeight;
        public int materialId = -1;
        // Pages owned by a native baking session are repacked and re-uploaded wholesale from
        // native atlas storage; the legacy cursor-based packer must never write into them.
        public bool sessionOwned;
    }

    sealed class DynamicGlyphAppendBatch
    {
        readonly Dictionary<DynamicAtlasPage, List<NowFontAtlasInfo.Glyph>> _pendingGlyphs = new Dictionary<DynamicAtlasPage, List<NowFontAtlasInfo.Glyph>>();
        readonly HashSet<DynamicAtlasPage> _dirtyPages = new HashSet<DynamicAtlasPage>();

        public void AddGlyph(DynamicAtlasPage page, NowFontAtlasInfo.Glyph glyph)
        {
            if (!_pendingGlyphs.TryGetValue(page, out var glyphs))
            {
                glyphs = new List<NowFontAtlasInfo.Glyph>();
                _pendingGlyphs[page] = glyphs;
            }

            glyphs.Add(glyph);
        }

        public void MarkTextureDirty(DynamicAtlasPage page)
        {
            _dirtyPages.Add(page);
        }

        public void Commit()
        {
            foreach (var entry in _pendingGlyphs)
            {
                var page = entry.Key;

                if (!IsDynamicPageValid(page))
                    continue;

                var fontAtlasInfo = page.font.atlasInfo;
                AppendGlyphs(ref fontAtlasInfo.glyphs, entry.Value);
                page.font.atlasInfo = fontAtlasInfo;
                page.font.ClearGlyphCache();
            }

            foreach (var page in _dirtyPages)
            {
                if (IsDynamicPageValid(page))
                    page.font.atlas.Apply(page.font.isColor, false);
            }
        }
    }

    readonly struct DynamicGlyphKey : IEquatable<DynamicGlyphKey>
    {
        readonly int _unicode;
        readonly int _atlasSize;

        public DynamicGlyphKey(int unicode, int atlasSize)
        {
            _unicode = unicode;
            _atlasSize = atlasSize;
        }

        public bool Equals(DynamicGlyphKey other)
        {
            return _unicode == other._unicode && _atlasSize == other._atlasSize;
        }

        public override bool Equals(object obj)
        {
            return obj is DynamicGlyphKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_unicode * 397) ^ _atlasSize;
            }
        }
    }

    [NonSerialized]
    NowFontAtlasInfo.Glyph[] _denseGlyphTable;

    [NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> _sparseGlyphTable;

    [NonSerialized]
    Material _dynamicMaterialTemplate;

    [NonSerialized]
    HashSet<DynamicGlyphKey> _dynamicMisses;

    [NonSerialized]
    List<DynamicAtlasPage> _dynamicPages;

    [NonSerialized]
    Dictionary<DynamicGlyphKey, DynamicAtlasPage> _dynamicGlyphPages;

    [NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> _dynamicColorLayoutGlyphs;

    [NonSerialized]
    NowFontAtlasInfo.Metrics _dynamicColorLayoutMetrics;

    [NonSerialized]
    bool _hasDynamicColorLayoutMetrics;

    [NonSerialized]
    int[] _dynamicColorBitmapSizes;

    [NonSerialized]
    bool _didReadDynamicColorBitmapSizes;

    [NonSerialized]
    HashSet<int> _dynamicSourceCodepoints;

    [NonSerialized]
    bool _didReadDynamicSourceCodepoints;

    [NonSerialized]
    HashSet<int> _dynamicCodepointScratch;

    [NonSerialized]
    StringBuilder _dynamicStringBuilder;

    [NonSerialized]
    int[] _dynamicCompileCodepoints;

    [NonSerialized]
    NowFontCompiler.DynamicSession _dynamicSession;

    [NonSerialized]
    DynamicAtlasPage _dynamicSessionPage;

    [NonSerialized]
    bool _dynamicSessionFailed;

    [NonSerialized]
    bool? _dynamicSourceIsColor;

    [NonSerialized]
    byte[] _dynamicSessionAtlasScratch;

    [NonSerialized]
    List<NowFontAtlasInfo.Glyph> _dynamicSessionGlyphScratch;

    [NonSerialized]
    int[] _dynamicSessionChunkScratch;

    [NonSerialized]
    HashSet<int> _dynamicSessionReturnedScratch;

    static bool s_dynamicSessionUnsupported;

    [NonSerialized]
    int _glyphTableOffset;

    [NonSerialized]
    public int materialId = -1;

    const int MAX_DENSE_GLYPH_RANGE = 4096;

    byte[] DynamicFontBytes => _fontBytes != null && _fontBytes.Length > 0 ? _fontBytes : null;

    public bool HasEmbeddedSource => DynamicFontBytes != null;

    public int GetSourceByteCount()
    {
        return DynamicFontBytes?.Length ?? 0;
    }

    public bool TryGetSourceBytes(out byte[] fontBytes)
    {
        var source = DynamicFontBytes;

        if (source == null)
        {
            fontBytes = null;
            return false;
        }

        fontBytes = (byte[])source.Clone();
        return true;
    }

    public int GetCachedDynamicPageCount()
    {
        return _dynamicPages?.Count ?? 0;
    }

    public int GetCachedDynamicGlyphCount()
    {
        return _dynamicGlyphPages?.Count ?? 0;
    }

    public void GetCachedDynamicAtlasTextures(List<Texture2D> atlases)
    {
        if (atlases == null)
            return;

        atlases.Clear();

        if (_dynamicPages == null)
            return;

        for (int i = 0; i < _dynamicPages.Count; ++i)
        {
            var page = _dynamicPages[i];

            if (IsDynamicPageValid(page))
                atlases.Add(page.font.atlas);
        }
    }

    protected override bool TryGetOwnFont(NowFontStyle style, out NowFont font)
    {
        font = this;
        return font != null;
    }

    void OnDisable()
    {
        // Releases the native session (parsed font, atlas storage) when the asset unloads or
        // the domain reloads; already-baked pages keep working and the session is recreated
        // lazily on the next missing glyph.
        ResetDynamicSession();
    }

    public void ClearDynamicCache()
    {
        ResetDynamicSession();
        _dynamicSessionFailed = false;

        if (_dynamicPages != null)
        {
            for (int i = 0; i < _dynamicPages.Count; ++i)
                DestroyDynamicPage(_dynamicPages[i]);
        }

        _dynamicMisses = null;
        _dynamicPages = null;
        _dynamicGlyphPages = null;
        _dynamicColorLayoutGlyphs = null;
        _dynamicColorLayoutMetrics = default;
        _hasDynamicColorLayoutMetrics = false;
        materialId = -1;
    }

    public static int ReadCodepoint(string value, ref int index)
    {
        if (string.IsNullOrEmpty(value) || index < 0 || index >= value.Length)
            return -1;

        char character = value[index];

        if (char.IsHighSurrogate(character) &&
            index + 1 < value.Length &&
            char.IsLowSurrogate(value[index + 1]))
        {
            ++index;
            return char.ConvertToUtf32(character, value[index]);
        }

        return character;
    }

    static void NormalizeGlyphAtlasBounds(ref NowFontAtlasInfo.Glyph glyph, Texture2D atlas)
    {
        glyph.atlasBounds.left /= atlas.width;
        glyph.atlasBounds.right /= atlas.width;
        glyph.atlasBounds.top /= atlas.height;
        glyph.atlasBounds.bottom /= atlas.height;
    }

    internal static string CodepointToString(int codepoint)
    {
        return codepoint <= char.MaxValue
            ? ((char)codepoint).ToString()
            : char.ConvertFromUtf32(codepoint);
    }

    static void AppendCodepoint(StringBuilder builder, int codepoint)
    {
        if (builder == null || !IsValidUnicodeScalar(codepoint))
            return;

        if (codepoint <= char.MaxValue)
        {
            builder.Append((char)codepoint);
            return;
        }

        int scalar = codepoint - 0x10000;
        builder.Append((char)((scalar >> 10) + 0xd800));
        builder.Append((char)((scalar & 0x3ff) + 0xdc00));
    }

    static ushort ReadUInt16BigEndian(byte[] data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    static uint ReadUInt32BigEndian(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24) |
            ((uint)data[offset + 1] << 16) |
            ((uint)data[offset + 2] << 8) |
            data[offset + 3];
    }

    static bool CanRead(byte[] data, int offset, int length)
    {
        return data != null &&
            offset >= 0 &&
            length >= 0 &&
            offset <= data.Length &&
            length <= data.Length - offset;
    }

    static bool TryGetOpenTypeTable(byte[] fontData, string tag, out int tableOffset, out int tableLength)
    {
        tableOffset = 0;
        tableLength = 0;

        if (fontData == null || fontData.Length < 12 || string.IsNullOrEmpty(tag) || tag.Length != 4)
            return false;

        int sfntOffset = 0;

        if (ReadUInt32BigEndian(fontData, 0) == OPENTYPE_TTC_TAG)
        {
            if (!CanRead(fontData, 0, 16) || ReadUInt32BigEndian(fontData, 8) == 0)
                return false;

            uint firstFontOffset = ReadUInt32BigEndian(fontData, 12);

            if (firstFontOffset > int.MaxValue)
                return false;

            sfntOffset = (int)firstFontOffset;
        }

        if (!CanRead(fontData, sfntOffset, 12))
            return false;

        int tableCount = ReadUInt16BigEndian(fontData, sfntOffset + 4);
        int recordsOffset = sfntOffset + 12;
        int directoryEnd = recordsOffset + tableCount * 16;

        if (tableCount <= 0 || directoryEnd < recordsOffset || directoryEnd > fontData.Length)
            return false;

        byte tag0 = (byte)tag[0];
        byte tag1 = (byte)tag[1];
        byte tag2 = (byte)tag[2];
        byte tag3 = (byte)tag[3];

        for (int i = 0; i < tableCount; ++i)
        {
            int offset = recordsOffset + i * 16;

            if (fontData[offset] != tag0 ||
                fontData[offset + 1] != tag1 ||
                fontData[offset + 2] != tag2 ||
                fontData[offset + 3] != tag3)
            {
                continue;
            }

            uint rawOffset = ReadUInt32BigEndian(fontData, offset + 8);
            uint rawLength = ReadUInt32BigEndian(fontData, offset + 12);

            if (rawOffset > int.MaxValue || rawLength > int.MaxValue)
                return false;

            tableOffset = (int)rawOffset;
            tableLength = (int)rawLength;

            return tableOffset >= 0 &&
                tableLength >= 0 &&
                tableOffset <= fontData.Length &&
                tableLength <= fontData.Length - tableOffset;
        }

        return false;
    }

    static bool TryReadDynamicSourceCodepoints(byte[] fontData, out HashSet<int> codepoints)
    {
        codepoints = null;

        if (!TryGetOpenTypeTable(fontData, "cmap", out var cmapOffset, out var cmapLength))
            return false;

        if (!CanRead(fontData, cmapOffset, 4))
            return false;

        int cmapEnd = cmapOffset + cmapLength;
        int encodingCount = ReadUInt16BigEndian(fontData, cmapOffset + 2);
        int recordsEnd = cmapOffset + 4 + encodingCount * 8;

        if (encodingCount <= 0 ||
            encodingCount > MAX_CMAP_ENCODING_RECORDS ||
            recordsEnd < cmapOffset ||
            recordsEnd > cmapEnd)
        {
            return false;
        }

        int bestScore = 0;
        int bestSubtableOffset = 0;
        int bestSubtableLength = 0;

        for (int i = 0; i < encodingCount; ++i)
        {
            int recordOffset = cmapOffset + 4 + i * 8;
            int platformId = ReadUInt16BigEndian(fontData, recordOffset);
            int encodingId = ReadUInt16BigEndian(fontData, recordOffset + 2);
            uint subtableRelativeOffset = ReadUInt32BigEndian(fontData, recordOffset + 4);

            if (subtableRelativeOffset > int.MaxValue)
                continue;

            int subtableOffset = cmapOffset + (int)subtableRelativeOffset;

            if (subtableOffset < cmapOffset || subtableOffset >= cmapEnd || !CanRead(fontData, subtableOffset, 2))
                continue;

            int format = ReadUInt16BigEndian(fontData, subtableOffset);
            int score = GetCmapCoverageScore(format, platformId, encodingId);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestSubtableOffset = subtableOffset;
            bestSubtableLength = cmapEnd - subtableOffset;
        }

        return bestScore > 0 &&
            TryReadCmapCoverageSubtable(fontData, bestSubtableOffset, bestSubtableLength, out codepoints);
    }

    static int GetCmapCoverageScore(int format, int platformId, int encodingId)
    {
        int score;

        switch (format)
        {
            case 12:
                score = 500;
                break;
            case 13:
                score = 490;
                break;
            case 4:
                score = 400;
                break;
            case 6:
                score = 200;
                break;
            case 0:
                score = 100;
                break;
            default:
                return 0;
        }

        if (platformId == 3 && encodingId == 10)
            score += 90;
        else if (platformId == 0)
            score += 80;
        else if (platformId == 3 && encodingId == 1)
            score += 60;
        else if (platformId == 3)
            score += 20;

        return score;
    }

    static bool TryReadCmapCoverageSubtable(
        byte[] fontData,
        int offset,
        int maxLength,
        out HashSet<int> codepoints)
    {
        codepoints = null;

        if (maxLength < 2 || !CanRead(fontData, offset, 2))
            return false;

        int format = ReadUInt16BigEndian(fontData, offset);

        switch (format)
        {
            case 0:
                return TryReadCmapFormat0Coverage(fontData, offset, maxLength, out codepoints);
            case 4:
                return TryReadCmapFormat4Coverage(fontData, offset, maxLength, out codepoints);
            case 6:
                return TryReadCmapFormat6Coverage(fontData, offset, maxLength, out codepoints);
            case 12:
                return TryReadCmapFormat12Coverage(fontData, offset, maxLength, false, out codepoints);
            case 13:
                return TryReadCmapFormat12Coverage(fontData, offset, maxLength, true, out codepoints);
            default:
                return false;
        }
    }

    static bool TryReadCmapFormat0Coverage(byte[] fontData, int offset, int maxLength, out HashSet<int> codepoints)
    {
        codepoints = null;

        if (!TryGetUInt16CmapLength(fontData, offset, maxLength, out var length) || length < 262)
            return false;

        var coverage = new HashSet<int>();

        for (int codepoint = 0; codepoint < 256; ++codepoint)
        {
            int glyphIndex = fontData[offset + 6 + codepoint];

            if (glyphIndex > 0 && !TryAddCmapCodepoint(coverage, codepoint))
                return false;
        }

        codepoints = coverage;
        return true;
    }

    static bool TryReadCmapFormat4Coverage(byte[] fontData, int offset, int maxLength, out HashSet<int> codepoints)
    {
        codepoints = null;

        if (!TryGetUInt16CmapLength(fontData, offset, maxLength, out var length) || length < 16)
            return false;

        int segCount = ReadUInt16BigEndian(fontData, offset + 6) / 2;

        if (segCount <= 0)
            return false;

        int endCodeOffset = offset + 14;
        int startCodeOffset = endCodeOffset + segCount * 2 + 2;
        int idDeltaOffset = startCodeOffset + segCount * 2;
        int idRangeOffsetOffset = idDeltaOffset + segCount * 2;
        int tableEnd = offset + length;

        if (idRangeOffsetOffset + segCount * 2 > tableEnd)
            return false;

        var coverage = new HashSet<int>();

        for (int segment = 0; segment < segCount; ++segment)
        {
            int endCode = ReadUInt16BigEndian(fontData, endCodeOffset + segment * 2);
            int startCode = ReadUInt16BigEndian(fontData, startCodeOffset + segment * 2);
            int idDelta = unchecked((short)ReadUInt16BigEndian(fontData, idDeltaOffset + segment * 2));
            int idRangeOffsetAddress = idRangeOffsetOffset + segment * 2;
            int idRangeOffset = ReadUInt16BigEndian(fontData, idRangeOffsetAddress);

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

                    if (!CanRead(fontData, glyphIndexAddress, 2) || glyphIndexAddress + 2 > tableEnd)
                        continue;

                    glyphIndex = ReadUInt16BigEndian(fontData, glyphIndexAddress);

                    if (glyphIndex != 0)
                        glyphIndex = (glyphIndex + idDelta) & 0xffff;
                }

                if (glyphIndex > 0 && !TryAddCmapCodepoint(coverage, codepoint))
                    return false;
            }
        }

        codepoints = coverage;
        return true;
    }

    static bool TryReadCmapFormat6Coverage(byte[] fontData, int offset, int maxLength, out HashSet<int> codepoints)
    {
        codepoints = null;

        if (!TryGetUInt16CmapLength(fontData, offset, maxLength, out var length) || length < 10)
            return false;

        int firstCode = ReadUInt16BigEndian(fontData, offset + 6);
        int entryCount = ReadUInt16BigEndian(fontData, offset + 8);
        int glyphsOffset = offset + 10;
        int glyphsLength = entryCount * 2;

        if (glyphsOffset + glyphsLength > offset + length)
            return false;

        var coverage = new HashSet<int>();

        for (int i = 0; i < entryCount; ++i)
        {
            int glyphIndex = ReadUInt16BigEndian(fontData, glyphsOffset + i * 2);

            if (glyphIndex > 0 && !TryAddCmapCodepoint(coverage, firstCode + i))
                return false;
        }

        codepoints = coverage;
        return true;
    }

    static bool TryReadCmapFormat12Coverage(
        byte[] fontData,
        int offset,
        int maxLength,
        bool constantGlyphIndex,
        out HashSet<int> codepoints)
    {
        codepoints = null;

        if (!CanRead(fontData, offset, 16))
            return false;

        uint rawLength = ReadUInt32BigEndian(fontData, offset + 4);

        if (rawLength < 16 || rawLength > int.MaxValue || rawLength > maxLength)
            return false;

        int length = (int)rawLength;

        if (!CanRead(fontData, offset, length))
            return false;

        uint groupCount = ReadUInt32BigEndian(fontData, offset + 12);

        if (groupCount > 100000)
            return false;

        int groupsOffset = offset + 16;
        int tableEnd = offset + length;

        if (groupsOffset + groupCount * 12 > tableEnd)
            return false;

        var coverage = new HashSet<int>();

        for (int i = 0; i < groupCount; ++i)
        {
            int groupOffset = groupsOffset + i * 12;
            uint start = ReadUInt32BigEndian(fontData, groupOffset);
            uint end = ReadUInt32BigEndian(fontData, groupOffset + 4);
            uint startGlyph = ReadUInt32BigEndian(fontData, groupOffset + 8);

            if (start > end || end > 0x10ffff)
                continue;

            for (uint codepoint = start; codepoint <= end; ++codepoint)
            {
                ulong glyphIndex = constantGlyphIndex
                    ? startGlyph
                    : (ulong)startGlyph + codepoint - start;

                if (glyphIndex > 0 &&
                    glyphIndex <= int.MaxValue &&
                    !TryAddCmapCodepoint(coverage, unchecked((int)codepoint)))
                {
                    return false;
                }
            }
        }

        codepoints = coverage;
        return true;
    }

    static bool TryGetUInt16CmapLength(byte[] fontData, int offset, int maxLength, out int length)
    {
        length = 0;

        if (!CanRead(fontData, offset, 4))
            return false;

        length = ReadUInt16BigEndian(fontData, offset + 2);
        return length <= maxLength && CanRead(fontData, offset, length);
    }

    static bool TryAddCmapCodepoint(HashSet<int> codepoints, int codepoint)
    {
        if (!IsValidUnicodeScalar(codepoint))
            return true;

        if (codepoints.Contains(codepoint))
            return true;

        if (codepoints.Count >= MAX_DYNAMIC_SOURCE_CMAP_CODEPOINTS)
            return false;

        codepoints.Add(codepoint);
        return true;
    }

    static bool IsValidUnicodeScalar(int value)
    {
        return value > 0 && value <= 0x10ffff && (value < 0xd800 || value > 0xdfff);
    }

    static int[] ReadColorBitmapSizes(byte[] fontData)
    {
        if (!TryGetOpenTypeTable(fontData, "CBLC", out var tableOffset, out var tableLength))
            return null;

        if (tableLength < 8)
            return null;

        uint sizeCount = ReadUInt32BigEndian(fontData, tableOffset + 4);

        if (sizeCount == 0 || sizeCount > 4096)
            return null;

        int recordsOffset = tableOffset + 8;
        int recordsLength = checked((int)sizeCount * 48);

        if (recordsLength > tableLength - 8)
            return null;

        var sizes = new List<int>((int)sizeCount);

        for (int i = 0; i < sizeCount; ++i)
        {
            int offset = recordsOffset + i * 48;
            int ppemX = fontData[offset + 44];
            int ppemY = fontData[offset + 45];
            int ppem = Mathf.Max(ppemX, ppemY);

            if (ppem <= 0 || sizes.Contains(ppem))
                continue;

            sizes.Add(ppem);
        }

        if (sizes.Count == 0)
            return null;

        sizes.Sort();
        return sizes.ToArray();
    }

    static bool IsAtlasWithinLimit(NowFont font, int maxAtlasSize, int maxAtlasBytes)
    {
        if (font == null || font.atlas == null)
            return false;

        if (maxAtlasSize > 0 && (font.atlas.width > maxAtlasSize || font.atlas.height > maxAtlasSize))
            return false;

        int byteCount = font.atlas.width * font.atlas.height * 4;
        return maxAtlasBytes <= 0 || byteCount <= maxAtlasBytes;
    }

    static void DestroyDynamicPage(DynamicAtlasPage page)
    {
        if (page == null || page.font == null)
            return;

        DestroyDynamicFont(page.font);
        page.font = null;
    }

    static void DestroyDynamicFont(NowFont font)
    {
        if (font == null)
            return;

        DestroyDynamicObject(font.material);
        DestroyDynamicObject(font.atlas);
        DestroyDynamicObject(font);
    }

    static void DestroyDynamicObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    static bool IsDynamicPageValid(DynamicAtlasPage page)
    {
        return page != null &&
            page.font &&
            page.font.atlas &&
            page.font.material;
    }

    void RemoveDynamicGlyphMappings(DynamicAtlasPage page)
    {
        if (page == null || _dynamicGlyphPages == null)
            return;

        if (page.codepoints != null)
        {
            foreach (int unicode in page.codepoints)
            {
                var key = new DynamicGlyphKey(unicode, page.atlasSize);

                if (_dynamicGlyphPages.TryGetValue(key, out var mappedPage) && ReferenceEquals(mappedPage, page))
                    _dynamicGlyphPages.Remove(key);
            }
        }
    }

    void RemoveDynamicPageAt(int index)
    {
        if (_dynamicPages == null || index < 0 || index >= _dynamicPages.Count)
            return;

        var page = _dynamicPages[index];
        RemoveDynamicGlyphMappings(page);
        _dynamicPages.RemoveAt(index);
        DestroyDynamicPage(page);
    }

    public float GetLineHeight()
    {
        if (atlasInfo.metrics.lineHeight > 0)
            return atlasInfo.metrics.lineHeight;

        if (_hasDynamicColorLayoutMetrics && _dynamicColorLayoutMetrics.lineHeight > 0)
            return _dynamicColorLayoutMetrics.lineHeight;

        if (_dynamicPages == null)
            return 1;

        for (int i = 0; i < _dynamicPages.Count; ++i)
        {
            var font = _dynamicPages[i].font;

            if (font != null && font.atlasInfo.metrics.lineHeight > 0)
                return font.atlasInfo.metrics.lineHeight;
        }

        return 1;
    }

    public float GetAscender()
    {
        if (atlasInfo.metrics.ascender > 0)
            return atlasInfo.metrics.ascender;

        if (_hasDynamicColorLayoutMetrics && _dynamicColorLayoutMetrics.ascender > 0)
            return _dynamicColorLayoutMetrics.ascender;

        if (_dynamicPages != null)
        {
            for (int i = 0; i < _dynamicPages.Count; ++i)
            {
                var font = _dynamicPages[i].font;

                if (font != null && font.atlasInfo.metrics.ascender > 0)
                    return font.atlasInfo.metrics.ascender;
            }
        }

        // Without an ascender metric, fall back to the line height, which
        // reproduces the legacy baseline-at-line-bottom placement.
        return GetLineHeight();
    }

    void ClearGlyphCache()
    {
        _denseGlyphTable = null;
        _sparseGlyphTable = null;
        _glyphTableOffset = 0;
    }

    void BuildGlyphCache()
    {
        var glyphs = atlasInfo.glyphs;
        int first = glyphs[0].unicode;
        int last = glyphs[0].unicode;

        for (int i = 1; i < glyphs.Length; ++i)
        {
            int unicode = glyphs[i].unicode;

            if (unicode < first)
                first = unicode;
            else if (unicode > last)
                last = unicode;
        }

        int range = last - first + 1;
        bool useDenseTable = first > 0 && range <= MAX_DENSE_GLYPH_RANGE && range <= glyphs.Length * 4;

        if (useDenseTable)
        {
            _glyphTableOffset = first;
            _denseGlyphTable = new NowFontAtlasInfo.Glyph[range];

            for (int i = 0; i < glyphs.Length; ++i)
            {
                var glyphValue = glyphs[i];
                NormalizeGlyphAtlasBounds(ref glyphValue, atlas);
                _denseGlyphTable[glyphValue.unicode - _glyphTableOffset] = glyphValue;
            }

            return;
        }

        _sparseGlyphTable = new Dictionary<int, NowFontAtlasInfo.Glyph>(glyphs.Length);

        for (int i = 0; i < glyphs.Length; ++i)
        {
            var glyphValue = glyphs[i];
            NormalizeGlyphAtlasBounds(ref glyphValue, atlas);
            _sparseGlyphTable[glyphValue.unicode] = glyphValue;
        }
    }

    bool TryGetCachedGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
    {
        glyph = default;

        if (!atlas || atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
            return false;

        if (_denseGlyphTable == null && _sparseGlyphTable == null)
            BuildGlyphCache();

        if (_denseGlyphTable != null)
        {
            int idx = unicode - _glyphTableOffset;

            if (idx < 0 || idx >= _denseGlyphTable.Length)
                return false;

            glyph = _denseGlyphTable[idx];
            return glyph.unicode == unicode;
        }

        return _sparseGlyphTable != null && _sparseGlyphTable.TryGetValue(unicode, out glyph);
    }

    public int GetDynamicGlyphSize(float fontSize)
    {
        return GetBaseDynamicGlyphSize();
    }

    int GetBaseDynamicGlyphSize()
    {
        int baseSize = dynamicAtlasSize > 0 ? dynamicAtlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE;
        return TryGetLargestColorBitmapSize(out var colorBitmapSize) ? colorBitmapSize : baseSize;
    }

    int[] GetColorBitmapSizes()
    {
        if (!_didReadDynamicColorBitmapSizes)
        {
            _dynamicColorBitmapSizes = ReadColorBitmapSizes(DynamicFontBytes);
            _didReadDynamicColorBitmapSizes = true;
        }

        return _dynamicColorBitmapSizes;
    }

    HashSet<int> GetDynamicCodepointScratch()
    {
        _dynamicCodepointScratch ??= new HashSet<int>();
        _dynamicCodepointScratch.Clear();
        return _dynamicCodepointScratch;
    }

    StringBuilder GetDynamicStringBuilder()
    {
        _dynamicStringBuilder ??= new StringBuilder();
        _dynamicStringBuilder.Length = 0;
        return _dynamicStringBuilder;
    }

    int[] GetDynamicCompileCodepoints(string value, out int count)
    {
        count = 0;

        if (string.IsNullOrEmpty(value))
            return null;

        var uniqueCodepoints = GetDynamicCodepointScratch();

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = ReadCodepoint(value, ref i);

            if (!IsValidUnicodeScalar(codepoint) || !uniqueCodepoints.Add(codepoint))
                continue;

            if (_dynamicCompileCodepoints == null || count >= _dynamicCompileCodepoints.Length)
            {
                int currentCapacity = _dynamicCompileCodepoints?.Length ?? 0;
                int nextCapacity = Mathf.Max(count + 1, currentCapacity > 0 ? currentCapacity * 2 : 8);
                Array.Resize(ref _dynamicCompileCodepoints, nextCapacity);
            }

            _dynamicCompileCodepoints[count++] = codepoint;
        }

        uniqueCodepoints.Clear();
        return count > 0 ? _dynamicCompileCodepoints : null;
    }

    bool DynamicSourceContainsCodepoint(int unicode)
    {
        if (!IsValidUnicodeScalar(unicode))
            return false;

        if (!_didReadDynamicSourceCodepoints)
        {
            if (!TryReadDynamicSourceCodepoints(DynamicFontBytes, out _dynamicSourceCodepoints))
                _dynamicSourceCodepoints = null;

            _didReadDynamicSourceCodepoints = true;
        }

        return _dynamicSourceCodepoints == null || _dynamicSourceCodepoints.Contains(unicode);
    }

    bool TryGetLargestColorBitmapSize(out int bitmapSize)
    {
        bitmapSize = 0;
        var sizes = GetColorBitmapSizes();

        if (sizes == null || sizes.Length == 0)
            return false;

        bitmapSize = sizes[^1];
        return true;
    }

    bool TryGetDynamicCachedGlyph(int unicode, int atlasSize, out NowFontAtlasInfo.Glyph glyph)
    {
        glyph = default;
        var key = new DynamicGlyphKey(unicode, atlasSize);

        if (_dynamicGlyphPages == null || !_dynamicGlyphPages.TryGetValue(key, out var page))
            return false;

        if (!IsDynamicPageValid(page))
        {
            int pageIndex = _dynamicPages?.IndexOf(page) ?? -1;

            if (pageIndex >= 0)
                RemoveDynamicPageAt(pageIndex);
            else
                _dynamicGlyphPages.Remove(key);

            return false;
        }

        if (page.font.GetGlyph(unicode, out glyph))
            return true;

        _dynamicGlyphPages.Remove(key);
        return false;
    }

    bool TryCompileDynamicPage(string characters, int atlasSize, out NowFont font)
    {
        font = null;
        var fontData = DynamicFontBytes;

        if (fontData == null || fontData.Length == 0 || string.IsNullOrEmpty(characters))
            return false;

        var codepoints = GetDynamicCompileCodepoints(characters, out var codepointCount);

        if (codepointCount <= 0)
            return false;

        if (!NowFontCompiler.TryCompilePage(
            fontData,
            atlasSize > 0 ? atlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE,
            dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE,
            codepoints,
            codepointCount,
            _dynamicMaterialTemplate,
            out font,
            out _))
        {
            return false;
        }

        return IsAtlasWithinLimit(
            font,
            dynamicMaxAtlasSize > 0 ? dynamicMaxAtlasSize : DEFAULT_DYNAMIC_MAX_ATLAS_SIZE,
            dynamicMaxAtlasBytes > 0 ? dynamicMaxAtlasBytes : DEFAULT_DYNAMIC_MAX_ATLAS_BYTES);
    }

    int GetDynamicMaxAtlasSide()
    {
        int maxAtlasSize = dynamicMaxAtlasSize > 0 ? dynamicMaxAtlasSize : DEFAULT_DYNAMIC_MAX_ATLAS_SIZE;
        int maxAtlasBytes = dynamicMaxAtlasBytes > 0 ? dynamicMaxAtlasBytes : DEFAULT_DYNAMIC_MAX_ATLAS_BYTES;

        if (maxAtlasBytes > 0)
        {
            int maxSizeByBytes = Mathf.FloorToInt(Mathf.Sqrt(maxAtlasBytes / 4f));
            if (maxSizeByBytes > 0)
                maxAtlasSize = Mathf.Min(maxAtlasSize, maxSizeByBytes);
        }

        return maxAtlasSize;
    }

    int GetDynamicPageSize(int requiredSize)
    {
        int pageSize = dynamicPageSize > 0 ? dynamicPageSize : DEFAULT_DYNAMIC_PAGE_SIZE;
        pageSize = Mathf.Max(pageSize, requiredSize);
        return Mathf.Min(pageSize, GetDynamicMaxAtlasSide());
    }

    static bool TryGetGlyphSourceRect(NowFont font, NowFontAtlasInfo.Glyph glyph, out RectInt rect)
    {
        rect = default;

        if (!font || !font.atlas)
            return false;

        int left = Mathf.FloorToInt(Mathf.Min(glyph.atlasBounds.left, glyph.atlasBounds.right));
        int right = Mathf.CeilToInt(Mathf.Max(glyph.atlasBounds.left, glyph.atlasBounds.right));
        int bottom = Mathf.FloorToInt(Mathf.Min(glyph.atlasBounds.bottom, glyph.atlasBounds.top));
        int top = Mathf.CeilToInt(Mathf.Max(glyph.atlasBounds.bottom, glyph.atlasBounds.top));

        left = Mathf.Clamp(left, 0, font.atlas.width);
        right = Mathf.Clamp(right, 0, font.atlas.width);
        bottom = Mathf.Clamp(bottom, 0, font.atlas.height);
        top = Mathf.Clamp(top, 0, font.atlas.height);

        rect = new RectInt(left, bottom, right - left, top - bottom);
        return rect is { width: >= 0, height: >= 0 };
    }

    DynamicAtlasPage CreateDynamicPage(NowFont glyphFont, int requiredSize, int pageIndex)
    {
        if (!glyphFont || !glyphFont.atlas || !glyphFont.material)
            return null;

        int pageSize = GetDynamicPageSize(requiredSize);
        if (pageSize < requiredSize)
            return null;

        bool isColorPage = glyphFont.isColor;
        var pageTexture = new Texture2D(pageSize, pageSize, TextureFormat.RGBA32, isColorPage, !isColorPage)
        {
            name = isColorPage ? $"NowUI Color Page {pageIndex}" : $"NowUI Font Page {pageIndex}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        pageTexture.GetRawTextureData<Color32>().AsSpan().Clear();
        pageTexture.Apply(isColorPage, false);

        var pageMaterial = new Material(glyphFont.material)
        {
            name = glyphFont.material.name + " Page",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = pageTexture
        };

        var pageFont = CreateInstance<NowFont>();
        pageFont.name = isColorPage ? "NowUI Runtime Color Font Page" : "NowUI Runtime Font Page";
        pageFont.hideFlags = HideFlags.HideAndDontSave;
        pageFont.atlas = pageTexture;
        pageFont.material = pageMaterial;
        pageFont.atlasInfo = glyphFont.atlasInfo;
        pageFont.atlasInfo.atlas.width = pageSize;
        pageFont.atlasInfo.atlas.height = pageSize;

        if (isColorPage && _hasDynamicColorLayoutMetrics)
            pageFont.atlasInfo.metrics = _dynamicColorLayoutMetrics;

        pageFont.atlasInfo.glyphs = Array.Empty<NowFontAtlasInfo.Glyph>();

        return new DynamicAtlasPage
        {
            font = pageFont,
            codepoints = new HashSet<int>(),
            atlasSize = glyphFont.atlasInfo.atlas.size > 0 ? glyphFont.atlasInfo.atlas.size : requiredSize
        };
    }

    static bool IsSameDynamicPageType(DynamicAtlasPage page, NowFont glyphFont, int atlasSize)
    {
        return page != null &&
            page.font != null &&
            glyphFont != null &&
            page.atlasSize == atlasSize &&
            page.font.atlasInfo.atlas.type == glyphFont.atlasInfo.atlas.type;
    }

    bool TryAllocateGlyphRect(DynamicAtlasPage page, RectInt sourceRect, out RectInt targetRect)
    {
        targetRect = default;

        if (page == null || page.font == null || page.font.atlas == null)
            return false;

        if (sourceRect.width <= 0 || sourceRect.height <= 0)
            return true;

        int paddedWidth = sourceRect.width + DYNAMIC_GLYPH_PADDING;
        int paddedHeight = sourceRect.height + DYNAMIC_GLYPH_PADDING;
        int pageWidth = page.font.atlas.width;
        int pageHeight = page.font.atlas.height;

        if (sourceRect.width > pageWidth || sourceRect.height > pageHeight)
            return false;

        if (page.cursorX + sourceRect.width > pageWidth)
        {
            page.cursorX = 0;
            page.cursorY += page.rowHeight;
            page.rowHeight = 0;
        }

        if (page.cursorY + sourceRect.height > pageHeight)
            return false;

        targetRect = new RectInt(page.cursorX, page.cursorY, sourceRect.width, sourceRect.height);
        page.cursorX += paddedWidth;
        page.rowHeight = Mathf.Max(page.rowHeight, paddedHeight);
        return true;
    }

    static void AppendGlyph(ref NowFontAtlasInfo.Glyph[] glyphs, NowFontAtlasInfo.Glyph glyph)
    {
        int length = glyphs?.Length ?? 0;
        var nextGlyphs = new NowFontAtlasInfo.Glyph[length + 1];

        if (length > 0)
            Array.Copy(glyphs!, nextGlyphs, length);

        nextGlyphs[length] = glyph;
        glyphs = nextGlyphs;
    }

    static void AppendGlyphs(ref NowFontAtlasInfo.Glyph[] glyphs, List<NowFontAtlasInfo.Glyph> additions)
    {
        if (additions == null || additions.Count == 0)
            return;

        int length = glyphs?.Length ?? 0;
        var nextGlyphs = new NowFontAtlasInfo.Glyph[length + additions.Count];

        if (length > 0)
            Array.Copy(glyphs!, nextGlyphs, length);

        additions.CopyTo(nextGlyphs, length);
        glyphs = nextGlyphs;
    }

    static bool TryGetRawGlyph(NowFont font, int unicode, out NowFontAtlasInfo.Glyph glyph)
    {
        glyph = default;

        if (!font || font.atlasInfo.glyphs == null)
            return false;

        var glyphs = font.atlasInfo.glyphs;

        for (int i = 0; i < glyphs.Length; ++i)
        {
            if (glyphs[i].unicode != unicode)
                continue;

            glyph = glyphs[i];
            return true;
        }

        return false;
    }

    static Dictionary<int, NowFontAtlasInfo.Glyph> BuildRawGlyphMap(NowFont font)
    {
        if (!font || font.atlasInfo.glyphs == null || font.atlasInfo.glyphs.Length == 0)
            return null;

        var glyphs = font.atlasInfo.glyphs;
        var map = new Dictionary<int, NowFontAtlasInfo.Glyph>(glyphs.Length);

        for (int i = 0; i < glyphs.Length; ++i)
            map[glyphs[i].unicode] = glyphs[i];

        return map;
    }

    void StoreColorLayoutGlyphs(NowFont font, string characters)
    {
        if (!font || !font.isColor || string.IsNullOrEmpty(characters))
            return;

        if (!_hasDynamicColorLayoutMetrics)
        {
            _dynamicColorLayoutMetrics = font.atlasInfo.metrics;
            _hasDynamicColorLayoutMetrics = true;
        }

        _dynamicColorLayoutGlyphs ??= new Dictionary<int, NowFontAtlasInfo.Glyph>();

        for (int i = 0; i < characters.Length; ++i)
        {
            int unicode = ReadCodepoint(characters, ref i);

            if (_dynamicColorLayoutGlyphs.ContainsKey(unicode))
                continue;

            if (TryGetRawGlyph(font, unicode, out var glyph))
                _dynamicColorLayoutGlyphs[unicode] = glyph;
        }
    }

    string GetMissingColorLayoutCharacters(string characters)
    {
        if (string.IsNullOrEmpty(characters))
            return null;

        StringBuilder builder = null;

        for (int i = 0; i < characters.Length; ++i)
        {
            int unicode = ReadCodepoint(characters, ref i);

            if (_dynamicColorLayoutGlyphs != null && _dynamicColorLayoutGlyphs.ContainsKey(unicode))
                continue;

            builder ??= GetDynamicStringBuilder();
            AppendCodepoint(builder, unicode);
        }

        if (builder == null)
            return null;

        string result = builder.ToString();
        builder.Length = 0;
        return result;
    }

    void EnsureColorLayoutGlyphs(string characters, int atlasSize, NowFont glyphFont)
    {
        if (!glyphFont || !glyphFont.isColor || string.IsNullOrEmpty(characters))
            return;

        int baseAtlasSize = GetBaseDynamicGlyphSize();

        if (atlasSize == baseAtlasSize)
        {
            StoreColorLayoutGlyphs(glyphFont, characters);
            return;
        }

        string missingCharacters = GetMissingColorLayoutCharacters(characters);

        if (string.IsNullOrEmpty(missingCharacters))
            return;

        if (!TryCompileDynamicPage(missingCharacters, baseAtlasSize, out var layoutFont))
        {
            StoreColorLayoutGlyphs(glyphFont, missingCharacters);
            return;
        }

        try
        {
            StoreColorLayoutGlyphs(layoutFont, missingCharacters);
        }
        finally
        {
            DestroyDynamicFont(layoutFont);
        }
    }

    static void ApplyColorLayoutGlyph(ref NowFontAtlasInfo.Glyph glyph, NowFontAtlasInfo.Glyph layoutGlyph)
    {
        glyph.advance = layoutGlyph.advance;
        glyph.planeBounds = layoutGlyph.planeBounds;
    }

    bool TryAppendDynamicGlyph(
        DynamicAtlasPage page,
        NowFont glyphFont,
        int unicode,
        int atlasSize,
        NowFontAtlasInfo.Glyph glyph,
        RectInt sourceRect,
        DynamicGlyphAppendBatch batch = null)
    {
        if (!IsSameDynamicPageType(page, glyphFont, atlasSize))
            return false;

        if (!TryAllocateGlyphRect(page, sourceRect, out var targetRect))
            return false;

        if (sourceRect.width > 0 && sourceRect.height > 0)
        {
            var sourceData = glyphFont.atlas.GetRawTextureData<Color32>().AsSpan();
            var targetData = page.font.atlas.GetRawTextureData<Color32>().AsSpan();
            int sourceWidth = glyphFont.atlas.width;
            int targetWidth = page.font.atlas.width;

            for (int y = 0; y < sourceRect.height; ++y)
            {
                int sourceIndex = (sourceRect.y + y) * sourceWidth + sourceRect.x;
                int targetIndex = (targetRect.y + y) * targetWidth + targetRect.x;
                sourceData.Slice(sourceIndex, sourceRect.width).CopyTo(targetData.Slice(targetIndex, sourceRect.width));
            }

            if (batch != null)
                batch.MarkTextureDirty(page);
            else
                page.font.atlas.Apply(page.font.isColor, false);

            glyph.atlasBounds.left = targetRect.x + (glyph.atlasBounds.left - sourceRect.x);
            glyph.atlasBounds.right = targetRect.x + (glyph.atlasBounds.right - sourceRect.x);
            glyph.atlasBounds.bottom = targetRect.y + (glyph.atlasBounds.bottom - sourceRect.y);
            glyph.atlasBounds.top = targetRect.y + (glyph.atlasBounds.top - sourceRect.y);
        }
        else
        {
            glyph.atlasBounds = default;
        }

        if (glyphFont.isColor &&
            _dynamicColorLayoutGlyphs != null &&
            _dynamicColorLayoutGlyphs.TryGetValue(unicode, out var layoutGlyph))
        {
            ApplyColorLayoutGlyph(ref glyph, layoutGlyph);
        }

        if (batch != null)
        {
            batch.AddGlyph(page, glyph);
        }
        else
        {
            var fontAtlasInfo = page.font.atlasInfo;
            AppendGlyph(ref fontAtlasInfo.glyphs, glyph);
            page.font.atlasInfo = fontAtlasInfo;
            page.font.ClearGlyphCache();
        }

        page.codepoints ??= new HashSet<int>();
        page.codepoints.Add(unicode);
        _dynamicGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();
        _dynamicGlyphPages[new DynamicGlyphKey(unicode, atlasSize)] = page;
        return true;
    }

    void AddDynamicMiss(DynamicGlyphKey key)
    {
        _dynamicMisses ??= new HashSet<DynamicGlyphKey>();
        _dynamicMisses.Add(key);
    }

    bool ShouldCompileDynamicGlyph(int unicode, int atlasSize)
    {
        if (DynamicFontBytes == null || unicode <= 0)
            return false;

        var key = new DynamicGlyphKey(unicode, atlasSize);

        if (_dynamicMisses != null && _dynamicMisses.Contains(key))
            return false;

        if (TryGetCachedGlyph(unicode, out _) ||
            TryGetDynamicCachedGlyph(unicode, atlasSize, out _))
        {
            return false;
        }

        if (!DynamicSourceContainsCodepoint(unicode))
        {
            AddDynamicMiss(key);
            return false;
        }

        return true;
    }

    string GetMissingDynamicCharacters(string value, int atlasSize)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        HashSet<int> uniqueCodepoints = null;
        StringBuilder builder = null;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = ReadCodepoint(value, ref i);

            if (codepoint == '\n')
                continue;

            if (codepoint == '\t')
                codepoint = ' ';

            if (!ShouldCompileDynamicGlyph(codepoint, atlasSize))
                continue;

            uniqueCodepoints ??= GetDynamicCodepointScratch();

            if (!uniqueCodepoints.Add(codepoint))
                continue;

            builder ??= GetDynamicStringBuilder();
            AppendCodepoint(builder, codepoint);
        }

        uniqueCodepoints?.Clear();

        if (builder == null)
            return null;

        string result = builder.ToString();
        builder.Length = 0;
        return result;
    }

    bool TryCacheCompiledGlyph(
        NowFont glyphFont,
        int unicode,
        int atlasSize,
        DynamicGlyphAppendBatch batch = null,
        Dictionary<int, NowFontAtlasInfo.Glyph> rawGlyphs = null)
    {
        if (!glyphFont ||
            glyphFont.atlasInfo.glyphs == null ||
            glyphFont.atlasInfo.glyphs.Length == 0)
        {
            return false;
        }

        NowFontAtlasInfo.Glyph compiledGlyph;

        if (rawGlyphs != null)
        {
            if (!rawGlyphs.TryGetValue(unicode, out compiledGlyph))
                return false;
        }
        else if (!TryGetRawGlyph(glyphFont, unicode, out compiledGlyph))
        {
            return false;
        }

        if (!TryGetGlyphSourceRect(glyphFont, compiledGlyph, out var sourceRect))
            return false;

        int requiredPageSize = Mathf.Max(sourceRect.width, sourceRect.height);
        var key = new DynamicGlyphKey(unicode, atlasSize);
        _dynamicPages ??= new List<DynamicAtlasPage>();

        for (int i = _dynamicPages.Count - 1; i >= 0; --i)
        {
            var page = _dynamicPages[i];

            if (!IsDynamicPageValid(page))
            {
                RemoveDynamicPageAt(i);
                continue;
            }

            if (page.sessionOwned || !IsSameDynamicPageType(page, glyphFont, atlasSize))
                continue;

            if (page.codepoints != null && page.codepoints.Contains(unicode))
            {
                if (page.font.GetGlyph(unicode, out _))
                {
                    _dynamicGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();
                    _dynamicGlyphPages[key] = page;
                    return true;
                }

                continue;
            }

            if (TryAppendDynamicGlyph(page, glyphFont, unicode, atlasSize, compiledGlyph, sourceRect, batch))
                return true;
        }

        var newPage = CreateDynamicPage(glyphFont, requiredPageSize, _dynamicPages.Count);

        if (newPage != null && TryAppendDynamicGlyph(newPage, glyphFont, unicode, atlasSize, compiledGlyph, sourceRect, batch))
        {
            _dynamicPages.Add(newPage);
            return true;
        }

        return false;
    }

    void TryCompileMissingGlyphsIndividually(string characters, int atlasSize)
    {
        for (int i = 0; i < characters.Length; ++i)
        {
            int unicode = ReadCodepoint(characters, ref i);
            TryCompileMissingGlyph(unicode, atlasSize);
        }
    }

    void ResetDynamicSession()
    {
        _dynamicSession?.Dispose();
        _dynamicSession = null;
        _dynamicSessionPage = null;
    }

    bool TryEnsureDynamicSession(byte[] fontData)
    {
        if (_dynamicSession != null)
            return true;

        try
        {
            if (!NowFontCompiler.DynamicSession.TryCreate(
                fontData,
                dynamicAtlasSize > 0 ? dynamicAtlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE,
                dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE,
                GetDynamicPageSize(0),
                out _dynamicSession,
                out _))
            {
                _dynamicSessionFailed = true;
                return false;
            }
        }
        catch (DllNotFoundException)
        {
            s_dynamicSessionUnsupported = true;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            s_dynamicSessionUnsupported = true;
            return false;
        }
        catch (BadImageFormatException)
        {
            s_dynamicSessionUnsupported = true;
            return false;
        }

        return true;
    }

    DynamicAtlasPage CreateDynamicSessionPage(int side, int atlasSize)
    {
        var session = _dynamicSession;
        var materialTemplate = _dynamicMaterialTemplate;

        if (materialTemplate == null)
            materialTemplate = Resources.Load<Material>("NowUI/TxtMaterial");

        if (materialTemplate == null)
            return null;

        var texture = new Texture2D(side, side, TextureFormat.RGBA32, false, true)
        {
            name = $"NowUI Font Page {_dynamicPages?.Count ?? 0}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var material = new Material(materialTemplate)
        {
            name = materialTemplate.name + " Page",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = texture
        };

        var pageFont = CreateInstance<NowFont>();
        pageFont.name = "NowUI Runtime Font Page";
        pageFont.hideFlags = HideFlags.HideAndDontSave;
        pageFont.atlas = texture;
        pageFont.material = material;
        pageFont.atlasInfo = new NowFontAtlasInfo
        {
            atlas = new NowFontAtlasInfo.Atlas
            {
                type = ATLAS_TYPE_MTSDF,
                distanceRange = Mathf.RoundToInt(session.DistanceRange),
                size = Mathf.RoundToInt(session.Size),
                width = side,
                height = side,
                yOrigin = "bottom"
            },
            metrics = session.Metrics,
            glyphs = Array.Empty<NowFontAtlasInfo.Glyph>()
        };

        return new DynamicAtlasPage
        {
            font = pageFont,
            codepoints = new HashSet<int>(),
            atlasSize = atlasSize,
            sessionOwned = true
        };
    }

    bool TryCommitSessionGlyphs(List<NowFontAtlasInfo.Glyph> glyphs, int atlasSize)
    {
        var session = _dynamicSession;
        int side = session.AtlasSide;

        if (side <= 0)
            return false;

        var page = _dynamicSessionPage;

        if (page == null)
        {
            page = CreateDynamicSessionPage(side, atlasSize);

            if (page == null)
                return false;

            _dynamicSessionPage = page;
            _dynamicPages ??= new List<DynamicAtlasPage>();
            _dynamicPages.Add(page);
        }

        var texture = page.font.atlas;

        // Fixed-size sessions never resize their atlas — page textures and glyph UVs must
        // stay valid for meshes built in earlier frames. A mismatch means the session and
        // page are out of sync; refuse to touch the page rather than corrupt it.
        if (texture.width != side)
            return false;

        if (!session.TryCopyAtlas(ref _dynamicSessionAtlasScratch, out _))
            return false;

        texture.LoadRawTextureData(_dynamicSessionAtlasScratch);
        texture.Apply(false, false);

        var fontAtlasInfo = page.font.atlasInfo;
        AppendGlyphs(ref fontAtlasInfo.glyphs, glyphs);
        page.font.atlasInfo = fontAtlasInfo;
        page.font.ClearGlyphCache();

        page.codepoints ??= new HashSet<int>();
        _dynamicGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();

        for (int i = 0; i < glyphs.Count; ++i)
        {
            int unicode = glyphs[i].unicode;
            page.codepoints.Add(unicode);
            _dynamicGlyphPages[new DynamicGlyphKey(unicode, atlasSize)] = page;
        }

        return true;
    }

    void MarkSessionMisses(int[] codepoints, int codepointCount, HashSet<int> returned, int atlasSize)
    {
        if (returned.Count >= codepointCount)
            return;

        for (int i = 0; i < codepointCount; ++i)
        {
            if (!returned.Contains(codepoints[i]))
                AddDynamicMiss(new DynamicGlyphKey(codepoints[i], atlasSize));
        }
    }

    bool CommitAndFailDynamicSession(List<NowFontAtlasInfo.Glyph> results, int atlasSize)
    {
        if (results.Count > 0)
            TryCommitSessionGlyphs(results, atlasSize);

        ResetDynamicSession();
        _dynamicSessionFailed = true;
        return false;
    }

    /// <summary>
    /// Bakes the missing characters through the persistent native session. Session atlases
    /// have a fixed size, so baked glyph UVs and page textures stay valid forever; when a
    /// page fills up it is sealed and a fresh session/page takes over, mirroring the legacy
    /// multi-page behavior. Returns true when the request was handled (glyphs baked and/or
    /// misses recorded); false means the caller should use the legacy per-page compiler
    /// (color fonts, old native plugins, failures).
    /// </summary>
    bool TryAddGlyphsToSession(string characters, int atlasSize)
    {
        const int SESSION_ADD_CHUNK = 64;

        if (s_dynamicSessionUnsupported || _dynamicSessionFailed)
            return false;

        var fontData = DynamicFontBytes;

        if (fontData == null || string.IsNullOrEmpty(characters))
            return false;

        _dynamicSourceIsColor ??= NowFontCompiler.IsColorFont(fontData);

        if (_dynamicSourceIsColor.Value)
            return false;

        if (_dynamicSessionPage != null && !IsDynamicPageValid(_dynamicSessionPage))
            ResetDynamicSession();

        var codepoints = GetDynamicCompileCodepoints(characters, out int codepointCount);

        if (codepointCount <= 0)
            return TryEnsureDynamicSession(fontData);

        var results = _dynamicSessionGlyphScratch ??= new List<NowFontAtlasInfo.Glyph>();
        var returned = _dynamicSessionReturnedScratch ??= new HashSet<int>();
        var chunkCodepoints = _dynamicSessionChunkScratch ??= new int[SESSION_ADD_CHUNK];
        results.Clear();
        returned.Clear();

        int offset = 0;
        int chunkLimit = SESSION_ADD_CHUNK;

        while (offset < codepointCount)
        {
            if (!TryEnsureDynamicSession(fontData))
            {
                // Keep what was already baked usable, then let the legacy path take over.
                if (results.Count > 0)
                    TryCommitSessionGlyphs(results, atlasSize);

                return false;
            }

            int chunk = Mathf.Min(chunkLimit, codepointCount - offset);
            Array.Copy(codepoints, offset, chunkCodepoints, 0, chunk);

            int resultsBefore = results.Count;
            var status = _dynamicSession.TryAddGlyphs(chunkCodepoints, chunk, results, out _);

            if (status == NowFontCompiler.DynamicSession.AddResult.Ok)
            {
                for (int i = resultsBefore; i < results.Count; ++i)
                    returned.Add(results[i].unicode);

                offset += chunk;
                chunkLimit = SESSION_ADD_CHUNK;
                continue;
            }

            if (status == NowFontCompiler.DynamicSession.AddResult.AtlasFull)
            {
                // Commit pending glyphs into the current page before deciding how to retry;
                // the failed add did not modify the native session.
                if (results.Count > 0)
                {
                    bool committed = TryCommitSessionGlyphs(results, atlasSize);
                    results.Clear();

                    if (!committed)
                    {
                        ResetDynamicSession();
                        _dynamicSessionFailed = true;
                        return false;
                    }
                }

                if (_dynamicSessionPage == null)
                {
                    // Even an empty page cannot fit this chunk: shrink it, and record a
                    // glyph that can never fit on its own as missing.
                    if (chunk <= 1)
                    {
                        AddDynamicMiss(new DynamicGlyphKey(codepoints[offset], atlasSize));
                        ++offset;
                        chunkLimit = SESSION_ADD_CHUNK;
                        continue;
                    }

                    chunkLimit = Mathf.Max(1, chunk / 2);
                    continue;
                }

                // Seal the full page (it stays alive with its glyph mappings) and retry the
                // chunk in a fresh session backed by a new page.
                ResetDynamicSession();
                continue;
            }

            return CommitAndFailDynamicSession(results, atlasSize);
        }

        if (results.Count > 0 && !TryCommitSessionGlyphs(results, atlasSize))
        {
            ResetDynamicSession();
            _dynamicSessionFailed = true;
            return false;
        }

        MarkSessionMisses(codepoints, codepointCount, returned, atlasSize);
        results.Clear();
        returned.Clear();
        return true;
    }

    void TryCompileMissingGlyphs(string characters, int atlasSize)
    {
        if (DynamicFontBytes == null || string.IsNullOrEmpty(characters)) return;

        if (TryAddGlyphsToSession(characters, atlasSize))
            return;

        if (!TryCompileDynamicPage(characters, atlasSize, out var glyphFont))
        {
            TryCompileMissingGlyphsIndividually(characters, atlasSize);
            return;
        }

        try
        {
            EnsureColorLayoutGlyphs(characters, atlasSize, glyphFont);

            var batch = new DynamicGlyphAppendBatch();
            var rawGlyphs = BuildRawGlyphMap(glyphFont);

            for (int i = 0; i < characters.Length; ++i)
            {
                int unicode = ReadCodepoint(characters, ref i);
                var key = new DynamicGlyphKey(unicode, atlasSize);

                if (TryCacheCompiledGlyph(glyphFont, unicode, atlasSize, batch, rawGlyphs))
                {
                    continue;
                }

                AddDynamicMiss(key);
            }

            batch.Commit();
        }
        finally
        {
            DestroyDynamicFont(glyphFont);
        }
    }

    bool TryCompileMissingGlyph(int unicode, int atlasSize)
    {
        if (!ShouldCompileDynamicGlyph(unicode, atlasSize))
            return false;

        var key = new DynamicGlyphKey(unicode, atlasSize);
        string character = CodepointToString(unicode);

        if (TryAddGlyphsToSession(character, atlasSize))
            return TryGetDynamicCachedGlyph(unicode, atlasSize, out _);

        if (!TryCompileDynamicPage(character, atlasSize, out var glyphFont))
        {
            AddDynamicMiss(key);
            return false;
        }

        try
        {
            EnsureColorLayoutGlyphs(character, atlasSize, glyphFont);

            var batch = new DynamicGlyphAppendBatch();

            if (TryCacheCompiledGlyph(glyphFont, unicode, atlasSize, batch))
            {
                batch.Commit();
                return true;
            }

            AddDynamicMiss(key);
            return false;
        }
        finally
        {
            DestroyDynamicFont(glyphFont);
        }
    }

    internal void InitializeDynamicSource(
        byte[] fontData,
        int atlasSize = 64,
        int pixelRange = 16,
        int maxAtlasSize = DEFAULT_DYNAMIC_MAX_ATLAS_SIZE,
        int maxAtlasBytes = DEFAULT_DYNAMIC_MAX_ATLAS_BYTES,
        Material materialTemplate = null)
    {
        atlas = null;
        atlasInfo = default;
        material = null;
        _fontBytes = fontData;
        dynamicAtlasSize = atlasSize;
        dynamicPixelRange = pixelRange;
        dynamicPageSize = DEFAULT_DYNAMIC_PAGE_SIZE;
        dynamicMaxAtlasSize = maxAtlasSize;
        dynamicMaxAtlasBytes = maxAtlasBytes;
        _dynamicMaterialTemplate = materialTemplate;
        _dynamicColorBitmapSizes = null;
        _didReadDynamicColorBitmapSizes = false;
        _dynamicSourceCodepoints = null;
        _didReadDynamicSourceCodepoints = false;
        _dynamicSourceIsColor = null;
        ClearDynamicCache();
        ClearGlyphCache();
    }

    public void EnsureGlyphs(string value, float fontSize)
    {
        if (DynamicFontBytes == null || string.IsNullOrEmpty(value) || fontSize <= 0)
            return;

        int atlasSize = GetDynamicGlyphSize(fontSize);
        string missingCharacters = GetMissingDynamicCharacters(value, atlasSize);

        if (!string.IsNullOrEmpty(missingCharacters))
            TryCompileMissingGlyphs(missingCharacters, atlasSize);
    }

    public bool GetGlyph(char c, out NowFontAtlasInfo.Glyph glyph)
    {
        return GetGlyph((int)c, out glyph);
    }

    public bool GetGlyph(char c, float fontSize, out NowFontAtlasInfo.Glyph glyph)
    {
        return GetGlyph((int)c, fontSize, out glyph);
    }

    public bool GetGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
    {
        return GetGlyph(unicode, dynamicAtlasSize, out glyph);
    }

    public bool GetGlyph(int unicode, float fontSize, out NowFontAtlasInfo.Glyph glyph)
    {
        if (TryGetCachedGlyph(unicode, out glyph))
            return true;

        int atlasSize = GetDynamicGlyphSize(fontSize);

        if (TryGetDynamicCachedGlyph(unicode, atlasSize, out glyph))
            return true;

        return TryCompileMissingGlyph(unicode, atlasSize) &&
            TryGetDynamicCachedGlyph(unicode, atlasSize, out glyph);
    }

    public bool GetGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph, out Material glyphMaterial)
    {
        return GetGlyph(unicode, dynamicAtlasSize, out glyph, out glyphMaterial);
    }

    public bool GetGlyph(int unicode, float fontSize, out NowFontAtlasInfo.Glyph glyph, out Material glyphMaterial)
    {
        if (TryGetCachedGlyph(unicode, out glyph))
        {
            glyphMaterial = material;
            return true;
        }

        int atlasSize = GetDynamicGlyphSize(fontSize);
        var key = new DynamicGlyphKey(unicode, atlasSize);

        if ((TryGetDynamicCachedGlyph(unicode, atlasSize, out glyph) ||
            (TryCompileMissingGlyph(unicode, atlasSize) && TryGetDynamicCachedGlyph(unicode, atlasSize, out glyph))) &&
            _dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(key, out var page))
        {
            glyphMaterial = page.font.material;
            return true;
        }

        glyphMaterial = null;
        return false;
    }

    public int GetMaterialId(int unicode)
    {
        return GetMaterialId(unicode, dynamicAtlasSize);
    }

    public int GetMaterialId(int unicode, float fontSize)
    {
        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(new DynamicGlyphKey(unicode, GetDynamicGlyphSize(fontSize)), out var page) &&
            page != null)
        {
            return page.materialId;
        }

        return materialId;
    }

    public void SetMaterialId(int unicode, int value)
    {
        SetMaterialId(unicode, dynamicAtlasSize, value);
    }

    public void SetMaterialId(int unicode, float fontSize, int value)
    {
        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(new DynamicGlyphKey(unicode, GetDynamicGlyphSize(fontSize)), out var page) &&
            page != null)
        {
            page.materialId = value;
            return;
        }

        materialId = value;
    }

    public Material GetMaterial(int unicode)
    {
        return GetMaterial(unicode, dynamicAtlasSize);
    }

    public Material GetMaterial(int unicode, float fontSize)
    {
        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(new DynamicGlyphKey(unicode, GetDynamicGlyphSize(fontSize)), out var page) &&
            page != null &&
            page.font != null)
        {
            return page.font.material;
        }

        return material;
    }

    public float GetScreenPixelRange(int unicode, float fontSize)
    {
        var fontAtlas = atlasInfo.atlas;

        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(new DynamicGlyphKey(unicode, GetDynamicGlyphSize(fontSize)), out var page) &&
            IsDynamicPageValid(page))
        {
            fontAtlas = page.font.atlasInfo.atlas;
        }

        int atlasSize = fontAtlas.size > 0 ? fontAtlas.size : DEFAULT_DYNAMIC_ATLAS_SIZE;
        int pixelRange = fontAtlas.distanceRange > 0
            ? fontAtlas.distanceRange
            : dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE;

        // Distance-field range in local units; the text shaders convert this to
        // screen pixels per-fragment (and floor it there) so canvas/transform
        // scale does not soften or alias the glyph edges.
        return fontSize / atlasSize * pixelRange;
    }

    public override Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
    {
        return base.MeasureText(value, fontSize, NowFontStyle.Regular, tabSpaces);
    }

    public override Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
    {
        return base.MeasureTextBounds(value, fontSize, NowFontStyle.Regular, tabSpaces);
    }
}
