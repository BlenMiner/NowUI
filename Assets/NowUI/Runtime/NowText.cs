using System;
using System.Globalization;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Small stack-backed text builder for dynamic UI labels. Use this when a
    /// label combines literals and changing numbers and passing through
    /// string.Format/ToString would allocate every frame.
    /// </summary>
    public ref struct NowTextBuffer
    {
        Span<char> _buffer;
        int _length;
        bool _truncated;

        public NowTextBuffer(Span<char> buffer)
        {
            _buffer = buffer;
            _length = 0;
            _truncated = false;
        }

        public readonly int length => _length;

        public readonly int capacity => _buffer.Length;

        public readonly bool truncated => _truncated;

        public readonly ReadOnlySpan<char> span => _buffer.Slice(0, _length);

        public void Clear()
        {
            _length = 0;
            _truncated = false;
        }

        public bool TryAppend(char value)
        {
            if (_length >= _buffer.Length)
                return false;

            _buffer[_length++] = value;
            return true;
        }

        public bool TryAppend(ReadOnlySpan<char> value)
        {
            if (value.Length > _buffer.Length - _length)
                return false;

            value.CopyTo(_buffer.Slice(_length));
            _length += value.Length;
            return true;
        }

        public bool TryAppend(int value, ReadOnlySpan<char> format = default)
        {
            if (!value.TryFormat(_buffer.Slice(_length), out int written, format, CultureInfo.InvariantCulture))
                return false;

            _length += written;
            return true;
        }

        public bool TryAppend(long value, ReadOnlySpan<char> format = default)
        {
            if (!value.TryFormat(_buffer.Slice(_length), out int written, format, CultureInfo.InvariantCulture))
                return false;

            _length += written;
            return true;
        }

        public bool TryAppend(float value, ReadOnlySpan<char> format = default)
        {
            if (!value.TryFormat(_buffer.Slice(_length), out int written, format, CultureInfo.InvariantCulture))
                return false;

            _length += written;
            return true;
        }

        public bool TryAppend(double value, ReadOnlySpan<char> format = default)
        {
            if (!value.TryFormat(_buffer.Slice(_length), out int written, format, CultureInfo.InvariantCulture))
                return false;

            _length += written;
            return true;
        }

        public void Append(char value)
        {
            if (!TryAppend(value))
                _truncated = true;
        }

        public void Append(ReadOnlySpan<char> value)
        {
            if (TryAppend(value))
                return;

            int count = Mathf.Max(0, _buffer.Length - _length);

            if (count > 0)
            {
                value.Slice(0, count).CopyTo(_buffer.Slice(_length));
                _length += count;
            }

            _truncated = true;
        }

        public void Append(int value, ReadOnlySpan<char> format = default)
        {
            if (!TryAppend(value, format))
                _truncated = true;
        }

        public void Append(long value, ReadOnlySpan<char> format = default)
        {
            if (!TryAppend(value, format))
                _truncated = true;
        }

        public void Append(float value, ReadOnlySpan<char> format = default)
        {
            if (!TryAppend(value, format))
                _truncated = true;
        }

        public void Append(double value, ReadOnlySpan<char> format = default)
        {
            if (!TryAppend(value, format))
                _truncated = true;
        }
    }

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

        [NowConsumer]
        public NowText Draw(int value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[16];
            return DrawFormatted(value, format, buffer);
        }

        [NowConsumer]
        public NowText Draw(long value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[32];
            return DrawFormatted(value, format, buffer);
        }

        [NowConsumer]
        public NowText Draw(float value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[32];
            return DrawFormatted(value, format, buffer);
        }

        [NowConsumer]
        public NowText Draw(double value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[64];
            return DrawFormatted(value, format, buffer);
        }

        public Vector2 Measure(string value)
        {
            return font != null ? font.MeasureText(value, fontSize, fontStyle) : default;
        }

        public Vector2 Measure(System.ReadOnlySpan<char> value)
        {
            return font != null ? font.MeasureText(value, fontSize, fontStyle) : default;
        }

        public Vector2 Measure(int value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[16];
            return MeasureFormatted(value, format, buffer);
        }

        public Vector2 Measure(long value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[32];
            return MeasureFormatted(value, format, buffer);
        }

        public Vector2 Measure(float value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[32];
            return MeasureFormatted(value, format, buffer);
        }

        public Vector2 Measure(double value, System.ReadOnlySpan<char> format = default)
        {
            Span<char> buffer = stackalloc char[64];
            return MeasureFormatted(value, format, buffer);
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

        NowText DrawFormatted(int value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            if (value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
                Draw(buffer.Slice(0, written));

            return this;
        }

        NowText DrawFormatted(long value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            if (value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
                Draw(buffer.Slice(0, written));

            return this;
        }

        NowText DrawFormatted(float value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            if (value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
                Draw(buffer.Slice(0, written));

            return this;
        }

        NowText DrawFormatted(double value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            if (value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
                Draw(buffer.Slice(0, written));

            return this;
        }

        Vector2 MeasureFormatted(int value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            return value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture)
                ? Measure(buffer.Slice(0, written))
                : default;
        }

        Vector2 MeasureFormatted(long value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            return value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture)
                ? Measure(buffer.Slice(0, written))
                : default;
        }

        Vector2 MeasureFormatted(float value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            return value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture)
                ? Measure(buffer.Slice(0, written))
                : default;
        }

        Vector2 MeasureFormatted(double value, System.ReadOnlySpan<char> format, Span<char> buffer)
        {
            return value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture)
                ? Measure(buffer.Slice(0, written))
                : default;
        }
    }
}
