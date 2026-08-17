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

        public float outline;

        public float fontSize;

        public NowFontAsset font;

        public NowFontStyle fontStyle;

        internal bool gradientEnabled;

        internal NowGradientKind gradientKind;

        internal NowGradientShape gradientShape;

        internal NowGradientSpread gradientSpread;

        internal Vector4 gradientParameters;

        internal Vector4 gradientColorFrom;

        internal Vector4 gradientColorTo;

        internal UnityEngine.Gradient gradientRamp;

        internal int gradientRampRevision;

        internal float gradientRepetitions;

        internal NowRect gradientBounds;

        internal bool hasGradientBounds;

        internal bool hasExplicitMask;

        internal Vector4 resolvedGradientPayload;

        internal float resolvedGradientRamp;

        internal NowTextAnimation animation;

        internal float animationTime;

        internal bool hasAnimationTime;

        internal bool animationTimeNormalized;

        internal int animationUnitOffset;

        internal int animationUnitCount;

        internal bool raw;

        public NowText(NowRect rect, NowFontAsset font)
        {
            this.rect = rect;
            outline = default;
            mask = rect;
            fontSize = 50;
            fontStyle = NowFontStyle.Regular;
            color = new Vector4(1, 1, 1, 1);
            outlineColor = new Vector4(0, 0, 0, 1);
            this.font = font;
            gradientEnabled = false;
            gradientKind = NowGradientKind.Linear;
            gradientShape = NowGradientShape.Ellipse;
            gradientSpread = NowGradientSpread.Clamp;
            gradientParameters = new Vector4(0f, 1f, 0f, 0f);
            gradientColorFrom = Color.black;
            gradientColorTo = Color.white;
            gradientRamp = null;
            gradientRampRevision = 0;
            gradientRepetitions = 1f;
            gradientBounds = default;
            hasGradientBounds = false;
            hasExplicitMask = false;
            resolvedGradientPayload = default;
            resolvedGradientRamp = 0f;
            animation = default;
            animationTime = 0f;
            hasAnimationTime = false;
            animationTimeNormalized = false;
            animationUnitOffset = 0;
            animationUnitCount = 0;
            raw = false;
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

        /// <summary>
        /// Moves the text rect. The default mask (which the constructor sets to
        /// the rect) follows the move; a mask pinned with
        /// <see cref="SetMask(NowRect)"/> stays where it was put.
        /// </summary>
        public NowText SetPosition(NowRect rect)
        {
            if (!hasExplicitMask && mask == this.rect)
                mask = rect;

            this.rect = rect;
            return this;
        }

        /// <summary>
        /// Pins the clip mask independently of the rect: later
        /// <see cref="SetPosition(NowRect)"/> calls no longer move it.
        /// </summary>
        public NowText SetMask(NowRect mask)
        {
            this.mask = mask;
            hasExplicitMask = true;
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

        /// <summary>
        /// Fills the glyphs with a cached two-color gradient. <see cref="color"/>
        /// remains a multiplicative tint and the outline remains independently solid.
        /// </summary>
        public NowText SetGradient(Color from, Color to)
        {
            gradientEnabled = true;
            gradientColorFrom = from;
            gradientColorTo = to;
            gradientRamp = null;
            gradientRampRevision = 0;
            return this;
        }

        /// <summary>Vector overload of <see cref="SetGradient(Color, Color)"/>.</summary>
        public NowText SetGradient(Vector4 from, Vector4 to)
        {
            gradientEnabled = true;
            gradientColorFrom = from;
            gradientColorTo = to;
            gradientRamp = null;
            gradientRampRevision = 0;
            return this;
        }

        /// <summary>
        /// Fills the glyphs with all color and alpha keys from a Unity gradient.
        /// Increment <paramref name="revision"/> after mutating the same instance,
        /// or call <see cref="Now.InvalidateGradient(UnityEngine.Gradient)"/>.
        /// </summary>
        public NowText SetGradient(UnityEngine.Gradient gradient, int revision = 0)
        {
            gradientEnabled = true;
            gradientRamp = gradient;
            gradientRampRevision = revision;
            return this;
        }

        /// <summary>Alias that makes Unity-gradient assignment explicit at call sites.</summary>
        public NowText SetGradientRamp(UnityEngine.Gradient gradient, int revision = 0)
        {
            return SetGradient(gradient, revision);
        }

        /// <summary>Maps the ramp across the text bounds in a named CSS-style direction.</summary>
        public NowText SetGradientLinear(NowGradientDirection direction = NowGradientDirection.ToBottom)
        {
            gradientEnabled = true;
            gradientKind = NowGradientKind.Linear;

            const float diagonal = 0.70710678118f;

            switch (direction)
            {
                case NowGradientDirection.ToTop: gradientParameters = new Vector4(0f, -1f, 0f, 0f); break;
                case NowGradientDirection.ToTopRight: gradientParameters = new Vector4(diagonal, -diagonal, 0f, 0f); break;
                case NowGradientDirection.ToRight: gradientParameters = new Vector4(1f, 0f, 0f, 0f); break;
                case NowGradientDirection.ToBottomRight: gradientParameters = new Vector4(diagonal, diagonal, 0f, 0f); break;
                case NowGradientDirection.ToBottomLeft: gradientParameters = new Vector4(-diagonal, diagonal, 0f, 0f); break;
                case NowGradientDirection.ToLeft: gradientParameters = new Vector4(-1f, 0f, 0f, 0f); break;
                case NowGradientDirection.ToTopLeft: gradientParameters = new Vector4(-diagonal, -diagonal, 0f, 0f); break;
                default: gradientParameters = new Vector4(0f, 1f, 0f, 0f); break;
            }

            return this;
        }

        /// <summary>Maps the ramp at a CSS-style angle: 0 is up and 90 is right.</summary>
        public NowText SetGradientLinear(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return SetGradientLinear(new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)));
        }

        /// <summary>Maps the ramp along a UI-space direction (positive y points down).</summary>
        public NowText SetGradientLinear(Vector2 direction)
        {
            gradientEnabled = true;
            gradientKind = NowGradientKind.Linear;
            gradientParameters = new Vector4(direction.x, direction.y, 0f, 0f);
            return this;
        }

        /// <summary>Uses a centered ellipse or circle across the text bounds.</summary>
        public NowText SetGradientRadial(NowGradientShape shape = NowGradientShape.Ellipse)
        {
            gradientEnabled = true;
            gradientKind = NowGradientKind.Radial;
            gradientShape = shape;
            gradientParameters = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
            return this;
        }

        /// <summary>Uses an ellipse in normalized text-bound coordinates.</summary>
        public NowText SetGradientRadial(Vector2 center, Vector2 radius)
        {
            gradientEnabled = true;
            gradientKind = NowGradientKind.Radial;
            gradientShape = NowGradientShape.Ellipse;
            gradientParameters = new Vector4(center.x, center.y, radius.x, radius.y);
            return this;
        }

        /// <summary>Uses a circle whose radius is relative to the smaller text-bound dimension.</summary>
        public NowText SetGradientRadial(Vector2 center, float radius)
        {
            gradientEnabled = true;
            gradientKind = NowGradientKind.Radial;
            gradientShape = NowGradientShape.Circle;
            gradientParameters = new Vector4(center.x, center.y, radius, radius);
            return this;
        }

        /// <summary>Uses a clockwise conic sweep centered on the text bounds.</summary>
        public NowText SetGradientConic()
        {
            return SetGradientConic(new Vector2(0.5f, 0.5f), 0f);
        }

        /// <summary>Uses a clockwise conic sweep around a normalized center.</summary>
        public NowText SetGradientConic(Vector2 center, float startAngle = 0f)
        {
            gradientEnabled = true;
            gradientKind = NowGradientKind.Conic;
            gradientParameters = new Vector4(center.x, center.y, startAngle / 360f, 0f);
            return this;
        }

        public NowText SetGradientSpread(NowGradientSpread spread)
        {
            gradientEnabled = true;
            gradientSpread = spread;
            return this;
        }

        public NowText SetGradientRepetitions(float repetitions)
        {
            gradientEnabled = true;
            gradientRepetitions = repetitions;
            return this;
        }

        /// <summary>
        /// Pins gradient mapping to a stable rectangle. By default each draw maps
        /// over this text builder's rect; pinning is useful across styled runs.
        /// </summary>
        public NowText SetGradientBounds(NowRect bounds)
        {
            gradientBounds = bounds;
            hasGradientBounds = true;
            return this;
        }

        public NowText ClearGradient()
        {
            gradientEnabled = false;
            return this;
        }

        /// <summary>
        /// Applies an allocation-free, per-cluster animation. Playback has no
        /// hidden clock; provide the sample time with <see cref="SetTime"/>.
        /// </summary>
        public NowText SetAnimation(NowTextAnimation animation)
        {
            this.animation = animation;
            return this;
        }

        /// <summary>Samples the animation at an absolute caller-owned time in seconds.</summary>
        public NowText SetTime(float seconds)
        {
            animationTime = seconds;
            hasAnimationTime = true;
            animationTimeNormalized = false;
            return this;
        }

        /// <summary>
        /// Scrubs a finite animation from 0 to 1 without requesting continuous
        /// repaint. Continuous Wave animation should use <see cref="SetTime"/>.
        /// </summary>
        public NowText SetNormalizedTime(float progress)
        {
            animationTime = Mathf.Clamp01(progress);
            hasAnimationTime = true;
            animationTimeNormalized = true;
            return this;
        }

        public NowText ClearAnimation()
        {
            animation = default;
            animationTime = 0f;
            hasAnimationTime = false;
            animationTimeNormalized = false;
            animationUnitOffset = 0;
            animationUnitCount = 0;
            return this;
        }

        /// <summary>Internal sequence continuity used by rich text and wrapped runs.</summary>
        internal NowText SetAnimationSequence(int unitOffset, int unitCount)
        {
            animationUnitOffset = Mathf.Max(0, unitOffset);
            animationUnitCount = Mathf.Max(0, unitCount);
            return this;
        }

        /// <summary>
        /// Draws and measures this string verbatim, skipping the registered
        /// text preprocessor (<see cref="Now.SetTextPreprocessor"/>) — for
        /// content that must never be transformed: identifiers, user input,
        /// code. Editable controls set this on their content internally.
        /// </summary>
        public NowText SetRaw(bool value = true)
        {
            raw = value;
            return this;
        }

        [NowConsumer]
        public NowText Draw(string value)
        {
            Now.DrawString(this, raw ? value : Now.PreprocessText(value));
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
            if (!raw)
                value = Now.PreprocessText(value);

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
            if (!raw)
                value = Now.PreprocessText(value);

            return font != null ? font.MeasureTextBounds(value, fontSize, fontStyle) : default;
        }

        [NowConsumer]
        public NowText Draw(char character)
        {
            Now.DrawCharacter(this, character);
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
