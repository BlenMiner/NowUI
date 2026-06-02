using UnityEngine;

public struct NowUIText
{
    public Vector4 Rect;

    public Vector4 Mask;

    public Vector4 Color;

    public Vector4 OutlineColor;

    public Vector4 Padding;

    public float Outline;

    public float FontSize;

    public NowFont Font;

    public NowUIText(Vector4 rect, NowFont font)
    {
        Rect = rect;
        Padding = default;
        Outline = default;
        Mask = rect;
        FontSize = 50;
        Color = new Vector4(1, 1, 1, 1);
        OutlineColor = new Vector4(0, 0, 0, 1);
        Font = font;
    }

    public NowUIText SetFont(NowFont font)
    {
        Font = font;
        return this;
    }

    public NowUIText SetFontSize(float fontSize)
    {
        FontSize = fontSize;
        return this;
    }

    public NowUIText SetPadding(float all)
    {
        Padding = new Vector4(all, all, all, all);
        return this;
    }

    public NowUIText SetOutline(float outline)
    {
        Outline = outline;
        return this;
    }

    public NowUIText SetOutlineColor(Vector4 outline)
    {
        OutlineColor = outline;
        return this;
    }

    public NowUIText SetPosition(Vector4 rect)
    {
        Rect = rect;
        return this;
    }

    public NowUIText SetMask(Vector4 mask)
    {
        Mask = mask;
        return this;
    }

    public NowUIText SetColor(Color color)
    {
        Color = color;
        return this;
    }

    public NowUIText SetColor(Vector4 color)
    {
        Color = color;
        return this;
    }

    public NowUIText Draw(string value)
    {
        NowUI.DrawString(this, value);
        return this;
    }

    public Vector2 Measure(string value)
    {
        return Font != null ? Font.MeasureText(value, FontSize) : default;
    }

    public Vector4 MeasureBounds(string value)
    {
        return Font != null ? Font.MeasureTextBounds(value, FontSize) : default;
    }

    public NowUIText Draw(char character)
    {
        if (Font.GetGlyph(character, out var g))
            NowUI.DrawCharacter(this, g);
        return this;
    }

    public NowUIText Draw(NowFontAtlasInfo.Glyph character)
    {
        NowUI.DrawCharacter(this, character);
        return this;
    }
}
