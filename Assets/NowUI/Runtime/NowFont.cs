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

[PreferBinarySerialization]
public class NowFont : ScriptableObject
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
        public int cursorX;
        public int cursorY;
        public int rowHeight;
        public int materialId = -1;
    }

    [System.NonSerialized]
    NowFontAtlasInfo.Glyph[] _denseGlyphTable;

    [System.NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> _sparseGlyphTable;

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

    byte[] DynamicFontBytes => _fontBytes != null && _fontBytes.Length > 0 ? _fontBytes : null;

    public bool HasEmbeddedSource => DynamicFontBytes != null;

    public int GetSourceByteCount()
    {
        return DynamicFontBytes?.Length ?? 0;
    }

    public int GetCachedDynamicPageCount()
    {
        return _dynamicPages?.Count ?? 0;
    }

    public int GetCachedDynamicGlyphCount()
    {
        return _dynamicGlyphPages?.Count ?? 0;
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
        if (page == null || _dynamicGlyphPages == null || string.IsNullOrEmpty(page.characters))
            return;

        for (int i = 0; i < page.characters.Length; ++i)
        {
            int unicode = ReadCodepoint(page.characters, ref i);

            if (_dynamicGlyphPages.TryGetValue(unicode, out var mappedPage) && ReferenceEquals(mappedPage, page))
                _dynamicGlyphPages.Remove(unicode);
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

    bool TryGetDynamicCachedGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
    {
        glyph = default;

        if (_dynamicGlyphPages == null || !_dynamicGlyphPages.TryGetValue(unicode, out var page))
            return false;

        if (!IsDynamicPageValid(page))
        {
            int pageIndex = _dynamicPages != null ? _dynamicPages.IndexOf(page) : -1;

            if (pageIndex >= 0)
                RemoveDynamicPageAt(pageIndex);
            else
                _dynamicGlyphPages.Remove(unicode);

            return false;
        }

        if (page.font.GetGlyph(unicode, out glyph))
            return true;

        _dynamicGlyphPages.Remove(unicode);
        return false;
    }

    bool TryCompileDynamicPage(string characters, out NowFont font)
    {
        font = null;
        var fontData = DynamicFontBytes;

        if (fontData == null || fontData.Length == 0 || string.IsNullOrEmpty(characters))
            return false;

        if (!NowFontCompiler.TryCompilePage(
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
        var pageTexture = new Texture2D(pageSize, pageSize, TextureFormat.RGBA32, false, !isColorPage)
        {
            name = isColorPage ? "NowUI Dynamic Color Font Page" : "NowUI Dynamic Font Page",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        pageTexture.SetPixels32(new Color32[pageSize * pageSize]);
        pageTexture.Apply(false, false);

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
        pageFont.atlasInfo.glyphs = new NowFontAtlasInfo.Glyph[0];

        return new DynamicAtlasPage
        {
            font = pageFont,
            characters = string.Empty
        };
    }

    static bool IsSameDynamicPageType(DynamicAtlasPage page, NowFont glyphFont)
    {
        return page != null &&
            page.font != null &&
            glyphFont != null &&
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
            System.Array.Copy(glyphs, nextGlyphs, length);

        nextGlyphs[length] = glyph;
        glyphs = nextGlyphs;
    }

    bool TryAppendDynamicGlyph(DynamicAtlasPage page, NowFont glyphFont, int unicode)
    {
        if (!IsSameDynamicPageType(page, glyphFont) ||
            glyphFont.atlasInfo.glyphs == null ||
            glyphFont.atlasInfo.glyphs.Length == 0)
        {
            return false;
        }

        var glyph = glyphFont.atlasInfo.glyphs[0];

        if (glyph.unicode != unicode)
            return false;

        if (!TryGetGlyphSourceRect(glyphFont, glyph, out var sourceRect))
            return false;

        if (!TryAllocateGlyphRect(page, sourceRect, out var targetRect))
            return false;

        if (sourceRect.width > 0 && sourceRect.height > 0)
        {
            var pixels = glyphFont.atlas.GetPixels(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height);
            page.font.atlas.SetPixels(targetRect.x, targetRect.y, targetRect.width, targetRect.height, pixels);
            page.font.atlas.Apply(false, false);

            glyph.atlasBounds.left = targetRect.x + (glyph.atlasBounds.left - sourceRect.x);
            glyph.atlasBounds.right = targetRect.x + (glyph.atlasBounds.right - sourceRect.x);
            glyph.atlasBounds.bottom = targetRect.y + (glyph.atlasBounds.bottom - sourceRect.y);
            glyph.atlasBounds.top = targetRect.y + (glyph.atlasBounds.top - sourceRect.y);
        }
        else
        {
            glyph.atlasBounds = default;
        }

        var fontAtlasInfo = page.font.atlasInfo;
        AppendGlyph(ref fontAtlasInfo.glyphs, glyph);
        page.font.atlasInfo = fontAtlasInfo;
        page.font.ClearGlyphCache();

        page.characters += CodepointToString(unicode);
        _dynamicGlyphPages ??= new Dictionary<int, DynamicAtlasPage>();
        _dynamicGlyphPages[unicode] = page;
        return true;
    }

    bool TryCompileMissingGlyph(int unicode)
    {
        if (DynamicFontBytes == null || unicode <= 0)
            return false;

        _dynamicMisses ??= new HashSet<int>();

        if (_dynamicMisses.Contains(unicode))
            return false;

        if (TryGetDynamicCachedGlyph(unicode, out _))
            return true;

        if (atlas && material && ContainsCodepoint(BuildCharactersFromGlyphs(), unicode))
        {
            _dynamicMisses.Add(unicode);
            return false;
        }

        _dynamicPages ??= new List<DynamicAtlasPage>();

        string character = CodepointToString(unicode);
        if (!TryCompileDynamicPage(character, out var glyphFont))
        {
            _dynamicMisses.Add(unicode);
            return false;
        }

        try
        {
            if (glyphFont.atlasInfo.glyphs == null || glyphFont.atlasInfo.glyphs.Length == 0)
            {
                _dynamicMisses.Add(unicode);
                return false;
            }

            if (!TryGetGlyphSourceRect(glyphFont, glyphFont.atlasInfo.glyphs[0], out var sourceRect))
            {
                _dynamicMisses.Add(unicode);
                return false;
            }

            int requiredPageSize = Mathf.Max(sourceRect.width, sourceRect.height);

            for (int i = _dynamicPages.Count - 1; i >= 0; --i)
            {
                var page = _dynamicPages[i];

                if (!IsDynamicPageValid(page))
                {
                    RemoveDynamicPageAt(i);
                    continue;
                }

                if (ContainsCodepoint(page.characters, unicode))
                {
                    if (page.font.GetGlyph(unicode, out _))
                    {
                        _dynamicGlyphPages ??= new Dictionary<int, DynamicAtlasPage>();
                        _dynamicGlyphPages[unicode] = page;
                        return true;
                    }

                    continue;
                }

                if (TryAppendDynamicGlyph(page, glyphFont, unicode))
                    return true;
            }

            var newPage = CreateDynamicPage(glyphFont, requiredPageSize);

            if (newPage != null && TryAppendDynamicGlyph(newPage, glyphFont, unicode))
            {
                _dynamicPages.Add(newPage);
                return true;
            }

            _dynamicMisses.Add(unicode);
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
        ClearDynamicCache();
        ClearGlyphCache();
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
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
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

        return new Vector2(maxWidth, GetLineHeight() * fontSize * lineCount);
    }

    public Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0)
            return default;

        float cursorX = 0;
        float lineY = 0;
        float lineHeight = 0;
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
                if (lineHeight <= 0)
                    lineHeight = GetLineHeight() * fontSize;

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

            if (lineHeight <= 0)
                lineHeight = GetLineHeight() * fontSize;

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
