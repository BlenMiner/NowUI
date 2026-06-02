using System.Collections.Generic;
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
    public Texture2D Atlas;

    public NowFontAtlasInfo AtlasInfo;

    public Material Material;

    [System.NonSerialized]
    NowFontAtlasInfo.Glyph[] m_denseGlyphTable;

    [System.NonSerialized]
    Dictionary<int, NowFontAtlasInfo.Glyph> m_sparseGlyphTable;

    [System.NonSerialized]
    int m_glyphTableOffset;

    [System.NonSerialized]
    public int MaterialID = -1;

    const int MaxDenseGlyphRange = 4096;

    static void NormalizeGlyphAtlasBounds(ref NowFontAtlasInfo.Glyph glyph, Texture2D atlas)
    {
        glyph.atlasBounds.left /= atlas.width;
        glyph.atlasBounds.right /= atlas.width;
        glyph.atlasBounds.top /= atlas.height;
        glyph.atlasBounds.bottom /= atlas.height;
    }

    void BuildGlyphCache()
    {
        var glyphs = AtlasInfo.glyphs;
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
        bool useDenseTable = first > 0 && range <= MaxDenseGlyphRange && range <= glyphs.Length * 4;

        if (useDenseTable)
        {
            m_glyphTableOffset = first;
            m_denseGlyphTable = new NowFontAtlasInfo.Glyph[range];

            for (int i = 0; i < glyphs.Length; ++i)
            {
                var glyphValue = glyphs[i];
                NormalizeGlyphAtlasBounds(ref glyphValue, Atlas);
                m_denseGlyphTable[glyphValue.unicode - m_glyphTableOffset] = glyphValue;
            }

            return;
        }

        m_sparseGlyphTable = new Dictionary<int, NowFontAtlasInfo.Glyph>(glyphs.Length);

        for (int i = 0; i < glyphs.Length; ++i)
        {
            var glyphValue = glyphs[i];
            NormalizeGlyphAtlasBounds(ref glyphValue, Atlas);
            m_sparseGlyphTable[glyphValue.unicode] = glyphValue;
        }
    }

    public bool GetGlyph(char c, out NowFontAtlasInfo.Glyph glyph)
    {
        glyph = default;

        if (Atlas == null || AtlasInfo.glyphs == null || AtlasInfo.glyphs.Length == 0)
            return false;

        if (m_denseGlyphTable == null && m_sparseGlyphTable == null)
            BuildGlyphCache();

        int unicode = c;

        if (m_denseGlyphTable != null)
        {
            int idx = unicode - m_glyphTableOffset;

            if (idx < 0 || idx >= m_denseGlyphTable.Length)
                return false;

            glyph = m_denseGlyphTable[idx];
            return glyph.unicode == unicode;
        }

        return m_sparseGlyphTable.TryGetValue(unicode, out glyph);
    }

    public Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0 || Atlas == null || AtlasInfo.glyphs == null || AtlasInfo.glyphs.Length == 0)
            return default;

        float lineWidth = 0;
        float maxWidth = 0;
        int lineCount = 1;

        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];

            if (c == '\n')
            {
                if (lineWidth > maxWidth)
                    maxWidth = lineWidth;

                lineWidth = 0;
                ++lineCount;
                continue;
            }

            if (c == '\t')
            {
                if (GetGlyph(' ', out var space))
                    lineWidth += space.advance * fontSize * tabSpaces;

                continue;
            }

            if (GetGlyph(c, out var glyph))
                lineWidth += glyph.advance * fontSize;
        }

        if (lineWidth > maxWidth)
            maxWidth = lineWidth;

        return new Vector2(maxWidth, AtlasInfo.metrics.lineHeight * fontSize * lineCount);
    }

    public Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
    {
        if (string.IsNullOrEmpty(value) || fontSize <= 0 || Atlas == null || AtlasInfo.glyphs == null || AtlasInfo.glyphs.Length == 0)
            return default;

        float cursorX = 0;
        float lineY = 0;
        float lineHeight = AtlasInfo.metrics.lineHeight * fontSize;
        float minX = 0;
        float minY = 0;
        float maxX = 0;
        float maxY = 0;
        bool hasBounds = false;

        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];

            if (c == '\n')
            {
                cursorX = 0;
                lineY += lineHeight;
                continue;
            }

            if (c == '\t')
            {
                if (GetGlyph(' ', out var space))
                    cursorX += space.advance * fontSize * tabSpaces;

                continue;
            }

            if (!GetGlyph(c, out var glyph))
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
