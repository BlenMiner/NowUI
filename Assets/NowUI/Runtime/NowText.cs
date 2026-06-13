using UnityEngine;

namespace NowUI
{
    [NowBuilder]
    public struct NowText
    {
        public NowRect rect;

        public NowRect mask;

        public Vector4 color;

        public Vector4 outlineColor;

        public Vector4 padding;

        public float outline;

        public float fontSize;

        public NowFontAsset font;

        public NowFontStyle fontStyle;

        public NowText(NowRect rect, NowFontAsset font)
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

        public NowText SetFont(NowFontAsset font)
        {
            this.font = font;
            return this;
        }

        public NowText SetFontStyle(NowFontStyle fontStyle)
        {
            this.fontStyle = fontStyle;
            return this;
        }

        public NowText SetBold(bool value = true)
        {
            fontStyle = value ? fontStyle | NowFontStyle.Bold : fontStyle & ~NowFontStyle.Bold;
            return this;
        }

        public NowText SetItalic(bool value = true)
        {
            fontStyle = value ? fontStyle | NowFontStyle.Italic : fontStyle & ~NowFontStyle.Italic;
            return this;
        }

        public NowText SetFontSize(float fontSize)
        {
            this.fontSize = fontSize;
            return this;
        }

        public NowText SetPadding(float all)
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
        public NowText SetOutline(float outline)
        {
            this.outline = outline;
            return this;
        }

        public NowText SetOutlineColor(Vector4 outline)
        {
            outlineColor = outline;
            return this;
        }

        public NowText SetPosition(NowRect rect)
        {
            this.rect = rect;
            return this;
        }

        public NowText SetMask(NowRect mask)
        {
            this.mask = mask;
            return this;
        }

        public NowText SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowText SetColor(Vector4 color)
        {
            this.color = color;
            return this;
        }

        [NowConsumer]
        public NowText Draw(string value)
        {
            Now.DrawString(this, value);
            return this;
        }

        /// <summary>
        /// Allocation-free draw for dynamic text: format into a reusable char
        /// buffer and pass the span. Always the per-codepoint path — shaping is
        /// keyed by string and does not apply to spans.
        /// </summary>
        [NowConsumer]
        public NowText Draw(System.ReadOnlySpan<char> value)
        {
            Now.DrawString(this, value);
            return this;
        }

        public Vector2 Measure(string value)
        {
            return font != null ? font.MeasureText(value, fontSize, fontStyle) : default;
        }

        public Vector2 Measure(System.ReadOnlySpan<char> value)
        {
            return font != null ? font.MeasureText(value, fontSize, fontStyle) : default;
        }

        public readonly Vector4 MeasureBounds(string value)
        {
            return font != null ? font.MeasureTextBounds(value, fontSize, fontStyle) : default;
        }

        [NowConsumer]
        public NowText Draw(char character)
        {
            if (font != null &&
                font.TryResolveGlyph(character, fontSize, fontStyle, out var resolvedFont, out var glyph, out _))
            {
                Now.DrawCharacter(this, glyph, resolvedFont);
            }

            return this;
        }

        [NowConsumer]
        public NowText Draw(NowFontAtlasInfo.Glyph character)
        {
            Now.DrawCharacter(this, character);
            return this;
        }
    }
}
