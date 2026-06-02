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

    public NowFont font;

    public NowUIText(Vector4 rect, NowFont font)
    {
        this.rect = rect;
        padding = default;
        outline = default;
        mask = rect;
        fontSize = 50;
        color = new Vector4(1, 1, 1, 1);
        outlineColor = new Vector4(0, 0, 0, 1);
        this.font = font;
    }

    public NowUIText SetFont(NowFont font)
    {
        this.font = font;
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
        return font != null ? font.MeasureText(value, fontSize) : default;
    }

    public Vector4 MeasureBounds(string value)
    {
        return font != null ? font.MeasureTextBounds(value, fontSize) : default;
    }

    public NowUIText Draw(char character)
    {
        if (font.GetGlyph(character, out var g))
            NowUI.DrawCharacter(this, g);
        return this;
    }

    public NowUIText Draw(NowFontAtlasInfo.Glyph character)
    {
        NowUI.DrawCharacter(this, character);
        return this;
    }
}
