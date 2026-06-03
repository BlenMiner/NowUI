using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public struct NowFontAtlasInfo
{
    [System.Serializable]
    public struct Atlas
    {
        public string type;
        public int distanceRange;
        public int size;
        public int width;
        public int height;
        public string yOrigin;
    }

    [System.Serializable]
    public struct Metrics
    {
        public float emSize;
        public float lineHeight;
        public float ascender;
        public float descender;
        public float underlineY;
        public float underlineThickness;
    }

    [System.Serializable]
    public struct Bounds
    {
        public float left;
        public float bottom;
        public float right;
        public float top;
    }

    [System.Serializable]
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

[System.Flags]
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

    public IReadOnlyList<NowFontAsset> fallbacks => _fallbacks;

    protected abstract bool TryGetOwnFont(NowFontStyle style, out NowFont font);

    public bool TryResolveFont(NowFontStyle style, out NowFont font)
    {
        var visited = new HashSet<NowFontAsset>();
        return TryResolveFont(style, visited, out font);
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
        var visited = new HashSet<NowFontAsset>();
        return TryResolveGlyph(unicode, fontSize, style, visited, out font, out glyph, out material);
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
        EnsureGlyphsAndGetMissing(value, fontSize, style, new HashSet<NowFontAsset>());
    }

    internal string EnsureGlyphsAndGetMissing(
        string value,
        float fontSize,
        NowFontStyle style,
        HashSet<NowFontAsset> visited)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return null;

        if (this == null || !visited.Add(this))
            return value;

        string missing = value;

        if (TryGetOwnFont(style, out var font) && font != null)
        {
            font.EnsureGlyphs(value, fontSize);
            missing = GetCharactersMissingFromFont(value, font, fontSize);
        }

        if (string.IsNullOrEmpty(missing) || _fallbacks == null)
            return missing;

        for (int i = 0; i < _fallbacks.Length; ++i)
        {
            var fallback = _fallbacks[i];

            if (fallback == null)
                continue;

            missing = fallback.EnsureGlyphsAndGetMissing(missing, fontSize, style, visited);

            if (string.IsNullOrEmpty(missing))
                break;
        }

        return missing;
    }

    static string GetCharactersMissingFromFont(string value, NowFont font, float fontSize)
    {
        if (string.IsNullOrEmpty(value) || font == null)
            return value;

        HashSet<int> uniqueCodepoints = null;
        StringBuilder builder = null;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            if (codepoint == '\n')
                continue;

            if (codepoint == '\t')
                codepoint = ' ';

            if (font.GetGlyph(codepoint, fontSize, out _))
                continue;

            uniqueCodepoints ??= new HashSet<int>();

            if (!uniqueCodepoints.Add(codepoint))
                continue;

            builder ??= new StringBuilder();
            builder.Append(NowFont.CodepointToString(codepoint));
        }

        return builder?.ToString();
    }

    public float GetLineHeight(NowFontStyle style = NowFontStyle.Regular)
    {
        var visited = new HashSet<NowFontAsset>();
        return GetLineHeight(style, visited);
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

    public virtual Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
    {
        return MeasureText(value, fontSize, NowFontStyle.Regular, tabSpaces);
    }

    public virtual Vector2 MeasureText(string value, float fontSize, NowFontStyle style, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return default;

        EnsureGlyphs(value, fontSize, style);

        float lineWidth = 0;
        float maxWidth = 0;
        int lineCount = 1;
        var visited = new HashSet<NowFontAsset>();

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

        return new Vector2(maxWidth, GetLineHeight(style) * fontSize * lineCount);
    }

    public virtual Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
    {
        return MeasureTextBounds(value, fontSize, NowFontStyle.Regular, tabSpaces);
    }

    public virtual Vector4 MeasureTextBounds(string value, float fontSize, NowFontStyle style, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return default;

        EnsureGlyphs(value, fontSize, style);

        float cursorX = 0;
        float lineY = 0;
        float lineHeight = 0;
        float minX = 0;
        float minY = 0;
        float maxX = 0;
        float maxY = 0;
        bool hasBounds = false;
        var visited = new HashSet<NowFontAsset>();

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            if (codepoint == '\n')
            {
                if (lineHeight <= 0)
                    lineHeight = GetLineHeight(style) * fontSize;

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
                lineHeight = GetLineHeight(style) * fontSize;

            if (glyph.atlasBounds.left != glyph.atlasBounds.right)
            {
                float glyphLeft = cursorX + glyph.planeBounds.left * fontSize;
                float glyphRight = cursorX + glyph.planeBounds.right * fontSize;
                float glyphTop = lineY + lineHeight - glyph.planeBounds.top * fontSize;
                float glyphBottom = lineY + lineHeight - glyph.planeBounds.bottom * fontSize;

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
    const int DYNAMIC_GLYPH_PADDING = 1;

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
        public string characters;
        public HashSet<int> codepoints;
        public int atlasSize;
        public int cursorX;
        public int cursorY;
        public int rowHeight;
        public int materialId = -1;
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

    readonly struct DynamicGlyphKey : System.IEquatable<DynamicGlyphKey>
    {
        readonly int _unicode;
        readonly int _atlasSize;

        public int unicode => _unicode;

        public int atlasSize => _atlasSize;

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

    [System.NonSerialized]
    NowFontAtlasInfo.Glyph[] _denseGlyphTable;

    [System.NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> _sparseGlyphTable;

    [System.NonSerialized]
    Material _dynamicMaterialTemplate;

    [System.NonSerialized]
    HashSet<DynamicGlyphKey> _dynamicMisses;

    [System.NonSerialized]
    List<DynamicAtlasPage> _dynamicPages;

    [System.NonSerialized]
    Dictionary<DynamicGlyphKey, DynamicAtlasPage> _dynamicGlyphPages;

    [System.NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> _dynamicColorLayoutGlyphs;

    [System.NonSerialized]
    NowFontAtlasInfo.Metrics _dynamicColorLayoutMetrics;

    [System.NonSerialized]
    bool _hasDynamicColorLayoutMetrics;

    [System.NonSerialized]
    int[] _dynamicColorBitmapSizes;

    [System.NonSerialized]
    bool _didReadDynamicColorBitmapSizes;

    [System.NonSerialized]
    int _glyphTableOffset;

    [System.NonSerialized]
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

    public void ClearDynamicCache()
    {
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

    static bool TryGetOpenTypeTable(byte[] fontData, string tag, out int tableOffset, out int tableLength)
    {
        tableOffset = 0;
        tableLength = 0;

        if (fontData == null || fontData.Length < 12 || string.IsNullOrEmpty(tag) || tag.Length != 4)
            return false;

        int tableCount = ReadUInt16BigEndian(fontData, 4);
        int directoryEnd = 12 + tableCount * 16;

        if (directoryEnd > fontData.Length)
            return false;

        byte tag0 = (byte)tag[0];
        byte tag1 = (byte)tag[1];
        byte tag2 = (byte)tag[2];
        byte tag3 = (byte)tag[3];

        for (int i = 0; i < tableCount; ++i)
        {
            int offset = 12 + i * 16;

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

    static bool ContainsCodepoint(string value, int codepoint)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < value.Length; ++i)
        {
            if (ReadCodepoint(value, ref i) == codepoint)
                return true;
        }

        return false;
    }

    bool HasStaticGlyphMetadata(int unicode)
    {
        if (!atlas || !material || atlasInfo.glyphs == null)
            return false;

        var glyphs = atlasInfo.glyphs;

        for (int i = 0; i < glyphs.Length; ++i)
        {
            if (glyphs[i].unicode == unicode)
                return true;
        }

        return false;
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

            return;
        }

        if (string.IsNullOrEmpty(page.characters))
            return;

        for (int i = 0; i < page.characters.Length; ++i)
        {
            int unicode = ReadCodepoint(page.characters, ref i);
            var key = new DynamicGlyphKey(unicode, page.atlasSize);

            if (_dynamicGlyphPages.TryGetValue(key, out var mappedPage) && ReferenceEquals(mappedPage, page))
                _dynamicGlyphPages.Remove(key);
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

        if (!atlas || !material || atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
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
        int baseSize = dynamicAtlasSize > 0 ? dynamicAtlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE;

        if (TryGetLargestColorBitmapSize(out var colorBitmapSize))
            return colorBitmapSize;

        return baseSize;
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

    bool TryGetLargestColorBitmapSize(out int bitmapSize)
    {
        bitmapSize = 0;
        var sizes = GetColorBitmapSizes();

        if (sizes == null || sizes.Length == 0)
            return false;

        bitmapSize = sizes[sizes.Length - 1];
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
            int pageIndex = _dynamicPages != null ? _dynamicPages.IndexOf(page) : -1;

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

        if (!NowFontCompiler.TryCompilePage(
            fontData,
            atlasSize > 0 ? atlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE,
            dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE,
            characters,
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

    int GetDynamicPageSize(int requiredSize)
    {
        int pageSize = dynamicPageSize > 0 ? dynamicPageSize : DEFAULT_DYNAMIC_PAGE_SIZE;
        int maxAtlasSize = dynamicMaxAtlasSize > 0 ? dynamicMaxAtlasSize : DEFAULT_DYNAMIC_MAX_ATLAS_SIZE;
        int maxAtlasBytes = dynamicMaxAtlasBytes > 0 ? dynamicMaxAtlasBytes : DEFAULT_DYNAMIC_MAX_ATLAS_BYTES;

        if (maxAtlasBytes > 0)
        {
            int maxSizeByBytes = Mathf.FloorToInt(Mathf.Sqrt(maxAtlasBytes / 4f));
            if (maxSizeByBytes > 0)
                maxAtlasSize = Mathf.Min(maxAtlasSize, maxSizeByBytes);
        }

        pageSize = Mathf.Max(pageSize, requiredSize);
        return Mathf.Min(pageSize, maxAtlasSize);
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
        return rect.width >= 0 && rect.height >= 0;
    }

    DynamicAtlasPage CreateDynamicPage(NowFont glyphFont, int requiredSize)
    {
        if (!glyphFont || !glyphFont.atlas || !glyphFont.material)
            return null;

        int pageSize = GetDynamicPageSize(requiredSize);
        if (pageSize < requiredSize)
            return null;

        bool isColorPage = glyphFont.isColor;
        bool useMipMaps = isColorPage;
        var pageTexture = new Texture2D(pageSize, pageSize, TextureFormat.RGBA32, useMipMaps, !isColorPage)
        {
            name = isColorPage ? "NowUI Dynamic Color Font Page" : "NowUI Dynamic Font Page",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        pageTexture.SetPixels32(new Color32[pageSize * pageSize]);
        pageTexture.Apply(useMipMaps, false);

        var pageMaterial = new Material(glyphFont.material)
        {
            name = glyphFont.material.name + " Page",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = pageTexture
        };

        var pageFont = ScriptableObject.CreateInstance<NowFont>();
        pageFont.name = isColorPage ? "NowUI Runtime Color Font Page" : "NowUI Runtime Font Page";
        pageFont.hideFlags = HideFlags.HideAndDontSave;
        pageFont.atlas = pageTexture;
        pageFont.material = pageMaterial;
        pageFont.atlasInfo = glyphFont.atlasInfo;
        pageFont.atlasInfo.atlas.width = pageSize;
        pageFont.atlasInfo.atlas.height = pageSize;

        if (isColorPage && _hasDynamicColorLayoutMetrics)
            pageFont.atlasInfo.metrics = _dynamicColorLayoutMetrics;

        pageFont.atlasInfo.glyphs = new NowFontAtlasInfo.Glyph[0];

        return new DynamicAtlasPage
        {
            font = pageFont,
            characters = string.Empty,
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
            System.Array.Copy(glyphs!, nextGlyphs, length);

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
            System.Array.Copy(glyphs!, nextGlyphs, length);

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

            builder ??= new StringBuilder();
            builder.Append(CodepointToString(unicode));
        }

        return builder?.ToString();
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
        DynamicGlyphAppendBatch batch = null)
    {
        if (!IsSameDynamicPageType(page, glyphFont, atlasSize) ||
            glyphFont.atlasInfo.glyphs == null ||
            glyphFont.atlasInfo.glyphs.Length == 0)
        {
            return false;
        }

        if (!TryGetRawGlyph(glyphFont, unicode, out var glyph))
            return false;

        if (!TryGetGlyphSourceRect(glyphFont, glyph, out var sourceRect))
            return false;

        if (!TryAllocateGlyphRect(page, sourceRect, out var targetRect))
            return false;

        if (sourceRect.width > 0 && sourceRect.height > 0)
        {
            var pixels = glyphFont.atlas.GetPixels(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height);
            page.font.atlas.SetPixels(targetRect.x, targetRect.y, targetRect.width, targetRect.height, pixels);

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

        page.characters += CodepointToString(unicode);
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

        if (HasStaticGlyphMetadata(unicode))
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

            uniqueCodepoints ??= new HashSet<int>();

            if (!uniqueCodepoints.Add(codepoint))
                continue;

            builder ??= new StringBuilder();
            builder.Append(CodepointToString(codepoint));
        }

        return builder?.ToString();
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

            if (!IsSameDynamicPageType(page, glyphFont, atlasSize))
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

            if (TryAppendDynamicGlyph(page, glyphFont, unicode, atlasSize, batch))
                return true;
        }

        var newPage = CreateDynamicPage(glyphFont, requiredPageSize);

        if (newPage != null && TryAppendDynamicGlyph(newPage, glyphFont, unicode, atlasSize, batch))
        {
            _dynamicPages.Add(newPage);
            return true;
        }

        return false;
    }

    bool TryCompileMissingGlyphsIndividually(string characters, int atlasSize)
    {
        bool compiledAny = false;

        for (int i = 0; i < characters.Length; ++i)
        {
            int unicode = ReadCodepoint(characters, ref i);

            if (TryCompileMissingGlyph(unicode, atlasSize))
                compiledAny = true;
        }

        return compiledAny;
    }

    bool TryCompileMissingGlyphs(string characters, int atlasSize)
    {
        if (DynamicFontBytes == null || string.IsNullOrEmpty(characters))
            return false;

        if (!TryCompileDynamicPage(characters, atlasSize, out var glyphFont))
            return TryCompileMissingGlyphsIndividually(characters, atlasSize);

        try
        {
            EnsureColorLayoutGlyphs(characters, atlasSize, glyphFont);

            bool compiledAny = false;
            var batch = new DynamicGlyphAppendBatch();
            var rawGlyphs = BuildRawGlyphMap(glyphFont);

            for (int i = 0; i < characters.Length; ++i)
            {
                int unicode = ReadCodepoint(characters, ref i);
                var key = new DynamicGlyphKey(unicode, atlasSize);

                if (TryCacheCompiledGlyph(glyphFont, unicode, atlasSize, batch, rawGlyphs))
                {
                    compiledAny = true;
                    continue;
                }

                AddDynamicMiss(key);
            }

            batch.Commit();
            return compiledAny;
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

        if (!TryCompileDynamicPage(character, atlasSize, out var glyphFont))
        {
            AddDynamicMiss(key);
            return false;
        }

        try
        {
            EnsureColorLayoutGlyphs(character, atlasSize, glyphFont);

            if (TryCacheCompiledGlyph(glyphFont, unicode, atlasSize))
                return true;

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

        return Mathf.Max(1f, fontSize / atlasSize * pixelRange);
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
