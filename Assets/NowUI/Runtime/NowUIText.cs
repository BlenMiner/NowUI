using UnityEngine;

public struct NowUIText
{
    public Vector4 rect;

    public Vector4 mask;

    public Vector4 color;

    public Vector4 outlineColor;

    public Vector4 padding;

    public float outline;

    public float fontSize;

    public NowFontAsset font;

    public NowFontStyle fontStyle;

    public NowUIText(Vector4 rect, NowFontAsset font)
    {
        this.rect = rect;
        padding = default;
        outline = default;
        mask = rect;
        fontSize = 50;
        fontStyle = NowFontStyle.Regular;
        color = new Vector4(1, 1, 1, 1);
        outlineColor = new Vector4(0, 0, 0, 1);
        this.font = font;
    }

    public NowUIText SetFont(NowFontAsset font)
    {
        this.font = font;
        return this;
    }

    public NowUIText SetFontStyle(NowFontStyle fontStyle)
    {
        this.fontStyle = fontStyle;
        return this;
    }

    public NowUIText SetBold(bool value = true)
    {
        fontStyle = value ? fontStyle | NowFontStyle.Bold : fontStyle & ~NowFontStyle.Bold;
        return this;
    }

    public NowUIText SetItalic(bool value = true)
    {
        fontStyle = value ? fontStyle | NowFontStyle.Italic : fontStyle & ~NowFontStyle.Italic;
        return this;
    }

    public NowUIText SetFontSize(float fontSize)
    {
        this.fontSize = fontSize;
        return this;
    }

    public NowUIText SetPadding(float all)
    {
        padding = new Vector4(all, all, all, all);
        return this;
    }

    /// <summary>
    /// Outline thickness relative to the font size (em units), so the stroke
    /// keeps the same visual weight at any size: 0.05 ≈ a 5%-of-em outline.
    /// Negative values inset the outline. For an absolute pixel width, pass
    /// <c>pixels / fontSize</c>.
    /// </summary>
    public NowUIText SetOutline(float outline)
    {
        this.outline = outline;
        return this;
    }

    public NowUIText SetOutlineColor(Vector4 outline)
    {
        outlineColor = outline;
        return this;
    }

    public NowUIText SetPosition(Vector4 rect)
    {
        this.rect = rect;
        return this;
    }

    public NowUIText SetMask(Vector4 mask)
    {
        this.mask = mask;
        return this;
    }

    public NowUIText SetColor(Color color)
    {
        this.color = color;
        return this;
    }

    public NowUIText SetColor(Vector4 color)
    {
        this.color = color;
        return this;
    }

    public NowUIText Draw(string value)
    {
        NowUI.DrawString(this, value);
        return this;
    }

    public Vector2 Measure(string value)
    {
        return font != null ? font.MeasureText(value, fontSize, fontStyle) : default;
    }

    public readonly Vector4 MeasureBounds(string value)
    {
        return font != null ? font.MeasureTextBounds(value, fontSize, fontStyle) : default;
    }

    public NowUIText Draw(char character)
    {
        if (font != null &&
            font.TryResolveGlyph(character, fontSize, fontStyle, out var resolvedFont, out var glyph, out _))
        {
            NowUI.DrawCharacter(this, glyph, resolvedFont);
        }

        return this;
    }

    public NowUIText Draw(NowFontAtlasInfo.Glyph character)
    {
        NowUI.DrawCharacter(this, character);
        return this;
    }
}
