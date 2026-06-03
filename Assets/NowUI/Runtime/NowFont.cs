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

public class NowFont : ScriptableObject
{
    public const string ATLAS_TYPE_MTSDF = "mtsdf";
    public const string ATLAS_TYPE_RGBA = "rgba";
    public const int DEFAULT_DYNAMIC_ATLAS_SIZE = 64;
    public const int DEFAULT_DYNAMIC_PIXEL_RANGE = 16;
    public const int DEFAULT_DYNAMIC_MAX_ATLAS_SIZE = 2048;
    public const int DEFAULT_DYNAMIC_MAX_ATLAS_BYTES = 16 * 1024 * 1024;

    public Texture2D atlas;

    public NowFontAtlasInfo atlasInfo;

    public Material material;

    public bool dynamicFont;

    public TextAsset dynamicFontData;

    public byte[] dynamicFontBytes;

    public int dynamicAtlasSize = DEFAULT_DYNAMIC_ATLAS_SIZE;

    public int dynamicPixelRange = DEFAULT_DYNAMIC_PIXEL_RANGE;

    public int dynamicMaxAtlasSize = DEFAULT_DYNAMIC_MAX_ATLAS_SIZE;

    public int dynamicMaxAtlasBytes = DEFAULT_DYNAMIC_MAX_ATLAS_BYTES;

    public bool isColor => atlasInfo.atlas.type == ATLAS_TYPE_RGBA;

    class DynamicAtlasPage
    {
        public NowFont font;
        public string characters;
        public int materialId = -1;
    }

    [System.NonSerialized]
    NowFontAtlasInfo.Glyph[] _denseGlyphTable;

    [System.NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> _sparseGlyphTable;

    [System.NonSerialized]
    byte[] _dynamicFontData;

    [System.NonSerialized]
    string _dynamicCharacters;

    [System.NonSerialized]
    Material _dynamicMaterialTemplate;

    [System.NonSerialized]
    HashSet<int> _dynamicMisses;

    [System.NonSerialized]
    List<DynamicAtlasPage> _dynamicPages;

    [System.NonSerialized]
    Dictionary<int, DynamicAtlasPage> _dynamicGlyphPages;

    [System.NonSerialized]
    int _glyphTableOffset;

    [System.NonSerialized]
    public int materialId = -1;

    const int MAX_DENSE_GLYPH_RANGE = 4096;

    public bool isDynamic
    {
        get
        {
            var fontData = DynamicFontBytes;
            return dynamicFont && fontData != null && fontData.Length > 0;
        }
    }

    byte[] DynamicFontBytes
    {
        get
        {
            if (_dynamicFontData != null && _dynamicFontData.Length > 0)
                return _dynamicFontData;

            if (dynamicFontBytes != null && dynamicFontBytes.Length > 0)
                return dynamicFontBytes;

            return dynamicFontData != null ? dynamicFontData.bytes : null;
        }
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

    static string CodepointToString(int codepoint)
    {
        return codepoint <= char.MaxValue
            ? ((char)codepoint).ToString()
            : char.ConvertFromUtf32(codepoint);
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

    string BuildCharactersFromGlyphs()
    {
        if (atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
            return string.Empty;

        var builder = new StringBuilder(atlasInfo.glyphs.Length);

        for (int i = 0; i < atlasInfo.glyphs.Length; ++i)
        {
            int unicode = atlasInfo.glyphs[i].unicode;

            if (unicode > 0)
                builder.Append(CodepointToString(unicode));
        }

        return builder.ToString();
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

        if (atlas == null || atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
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

        return _sparseGlyphTable.TryGetValue(unicode, out glyph);
    }

    bool TryGetDynamicCachedGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
    {
        glyph = default;

        if (_dynamicGlyphPages == null || !_dynamicGlyphPages.TryGetValue(unicode, out var page) || page.font == null)
            return false;

        return page.font.GetGlyph(unicode, out glyph);
    }

    void MapDynamicPageGlyphs(DynamicAtlasPage page)
    {
        if (page == null || page.font == null || page.font.atlasInfo.glyphs == null)
            return;

        if (_dynamicGlyphPages == null)
            _dynamicGlyphPages = new Dictionary<int, DynamicAtlasPage>();

        var glyphs = page.font.atlasInfo.glyphs;

        for (int i = 0; i < glyphs.Length; ++i)
            _dynamicGlyphPages[glyphs[i].unicode] = page;
    }

    bool TryCompileDynamicPage(string characters, out NowFont font)
    {
        font = null;
        var fontData = DynamicFontBytes;

        if (fontData == null || fontData.Length == 0 || string.IsNullOrEmpty(characters))
            return false;

        if (!NowFontCompiler.TryCompile(
            fontData,
            dynamicAtlasSize > 0 ? dynamicAtlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE,
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

    bool TryCompileMissingGlyph(int unicode)
    {
        if (!isDynamic || unicode <= 0)
            return false;

        if (_dynamicMisses == null)
            _dynamicMisses = new HashSet<int>();

        if (_dynamicMisses.Contains(unicode))
            return false;

        if (_dynamicGlyphPages != null && _dynamicGlyphPages.ContainsKey(unicode))
            return true;

        if (ContainsCodepoint(BuildCharactersFromGlyphs(), unicode))
        {
            _dynamicMisses.Add(unicode);
            return false;
        }

        if (_dynamicPages == null)
            _dynamicPages = new List<DynamicAtlasPage>();

        string character = CodepointToString(unicode);

        for (int i = _dynamicPages.Count - 1; i >= 0; --i)
        {
            var page = _dynamicPages[i];

            if (ContainsCodepoint(page.characters, unicode))
                return true;

            string nextCharacters = page.characters + character;

            if (!TryCompileDynamicPage(nextCharacters, out var updatedFont))
                continue;

            page.font = updatedFont;
            page.characters = nextCharacters;
            MapDynamicPageGlyphs(page);
            return true;
        }

        string newPageCharacters = string.IsNullOrEmpty(_dynamicCharacters)
            ? character
            : _dynamicCharacters;

        if (!ContainsCodepoint(newPageCharacters, unicode))
            newPageCharacters += character;

        if (!TryCompileDynamicPage(newPageCharacters, out var newFont))
        {
            _dynamicMisses.Add(unicode);
            return false;
        }

        var newPage = new DynamicAtlasPage
        {
            font = newFont,
            characters = newPageCharacters
        };

        _dynamicPages.Add(newPage);
        MapDynamicPageGlyphs(newPage);
        return true;
    }

    public void ConfigureDynamicCompilation(
        byte[] fontData,
        string initialCharacters = null,
        int atlasSize = 64,
        int pixelRange = 16,
        int maxAtlasSize = DEFAULT_DYNAMIC_MAX_ATLAS_SIZE,
        int maxAtlasBytes = DEFAULT_DYNAMIC_MAX_ATLAS_BYTES,
        Material materialTemplate = null)
    {
        dynamicFont = fontData != null && fontData.Length > 0;
        _dynamicFontData = fontData;
        _dynamicCharacters = initialCharacters;
        dynamicAtlasSize = atlasSize;
        dynamicPixelRange = pixelRange;
        dynamicMaxAtlasSize = maxAtlasSize;
        dynamicMaxAtlasBytes = maxAtlasBytes;
        _dynamicMaterialTemplate = materialTemplate;
        _dynamicMisses = null;
        _dynamicPages = null;
        _dynamicGlyphPages = null;
    }

    public bool GetGlyph(char c, out NowFontAtlasInfo.Glyph glyph)
    {
        return GetGlyph((int)c, out glyph);
    }

    public bool GetGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
    {
        if (TryGetCachedGlyph(unicode, out glyph))
            return true;

        if (TryGetDynamicCachedGlyph(unicode, out glyph))
            return true;

        return TryCompileMissingGlyph(unicode) && TryGetDynamicCachedGlyph(unicode, out glyph);
    }

    public bool GetGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph, out Material glyphMaterial)
    {
        if (TryGetCachedGlyph(unicode, out glyph))
        {
            glyphMaterial = material;
            return true;
        }

        if ((TryGetDynamicCachedGlyph(unicode, out glyph) ||
            (TryCompileMissingGlyph(unicode) && TryGetDynamicCachedGlyph(unicode, out glyph))) &&
            _dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(unicode, out var page))
        {
            glyphMaterial = page.font.material;
            return true;
        }

        glyphMaterial = null;
        return false;
    }

    public int GetMaterialId(int unicode)
    {
        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(unicode, out var page) &&
            page != null)
        {
            return page.materialId;
        }

        return materialId;
    }

    public void SetMaterialId(int unicode, int value)
    {
        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(unicode, out var page) &&
            page != null)
        {
            page.materialId = value;
            return;
        }

        materialId = value;
    }

    public Material GetMaterial(int unicode)
    {
        if (_dynamicGlyphPages != null &&
            _dynamicGlyphPages.TryGetValue(unicode, out var page) &&
            page != null &&
            page.font != null)
        {
            return page.font.material;
        }

        return material;
    }

    public Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0 || atlas == null || atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
            return default;

        float lineWidth = 0;
        float maxWidth = 0;
        int lineCount = 1;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = ReadCodepoint(value, ref i);

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
                if (GetGlyph(' ', out var space))
                    lineWidth += space.advance * fontSize * tabSpaces;

                continue;
            }

            if (GetGlyph(codepoint, out var glyph))
                lineWidth += glyph.advance * fontSize;
        }

        if (lineWidth > maxWidth)
            maxWidth = lineWidth;

        return new Vector2(maxWidth, atlasInfo.metrics.lineHeight * fontSize * lineCount);
    }

    public Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0 || atlas == null || atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
            return default;

        float cursorX = 0;
        float lineY = 0;
        float lineHeight = atlasInfo.metrics.lineHeight * fontSize;
        float minX = 0;
        float minY = 0;
        float maxX = 0;
        float maxY = 0;
        bool hasBounds = false;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = ReadCodepoint(value, ref i);

            if (codepoint == '\n')
            {
                cursorX = 0;
                lineY += lineHeight;
                continue;
            }

            if (codepoint == '\t')
            {
                if (GetGlyph(' ', out var space))
                    cursorX += space.advance * fontSize * tabSpaces;

                continue;
            }

            if (!GetGlyph(codepoint, out var glyph))
                continue;

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
