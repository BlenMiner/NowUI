using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Built-in per-unit text animation presets. A unit is resolved by the text
    /// renderer (normally one shaped glyph cluster), so ligatures and combining
    /// glyphs can move and reveal together.
    /// </summary>
    public enum NowTextAnimationKind : byte
    {
        /// <summary>No animation; text is emitted normally.</summary>
        None,

        /// <summary>Reveals complete text units at a fixed rate.</summary>
        Typewriter,

        /// <summary>Fades units from transparent to their authored opacity.</summary>
        FadeIn,

        /// <summary>Moves units upward into place while fading them in.</summary>
        FadeUp,

        /// <summary>Scales units up to their authored size.</summary>
        ScaleIn,

        /// <summary>Offsets units vertically along a continuous sine wave.</summary>
        Wave
    }

    /// <summary>Easing applied to finite per-unit text animations.</summary>
    public enum NowTextAnimationEasing : byte
    {
        /// <summary>Constant-rate interpolation.</summary>
        Linear,

        /// <summary>Cubic acceleration from rest.</summary>
        EaseIn,

        /// <summary>Cubic deceleration into the final value.</summary>
        EaseOut,

        /// <summary>Cubic acceleration followed by cubic deceleration.</summary>
        EaseInOut
    }

    /// <summary>
    /// Immutable, allocation-free configuration for a built-in per-unit text
    /// animation. The value contains no playback state and never reads a Unity
    /// clock: callers pass the time they want sampled to <c>NowText.SetTime</c>.
    /// Create values with <see cref="NowTextAnimations"/> and optionally override
    /// their common timing with the fluent setters.
    /// </summary>
    public readonly struct NowTextAnimation
    {
        /// <summary>The animation behavior represented by this value.</summary>
        public readonly NowTextAnimationKind kind;

        /// <summary>Duration in seconds of each unit's finite transition.</summary>
        public readonly float duration;

        /// <summary>Delay in seconds between the start of consecutive units.</summary>
        public readonly float stagger;

        /// <summary>Delay in seconds before the animation starts.</summary>
        public readonly float delay;

        /// <summary>
        /// Preset-specific magnitude: distance for FadeUp, starting scale for
        /// ScaleIn, and amplitude for Wave.
        /// </summary>
        public readonly float amount;

        /// <summary>Wave length measured in text units; unused by finite presets.</summary>
        public readonly float wavelength;

        /// <summary>Wave travel speed in cycles per second; unused by finite presets.</summary>
        public readonly float speed;

        /// <summary>Typewriter reveal rate in units per second.</summary>
        public readonly float rate;

        /// <summary>Easing used by finite fade, motion, and scale transitions.</summary>
        public readonly NowTextAnimationEasing easing;

        internal NowTextAnimation(
            NowTextAnimationKind kind,
            float duration,
            float stagger,
            float delay,
            float amount,
            float wavelength,
            float speed,
            float rate,
            NowTextAnimationEasing easing)
        {
            this.kind = kind;
            this.duration = NonNegative(duration);
            this.stagger = NonNegative(stagger);
            this.delay = NonNegative(delay);
            this.amount = FiniteOr(amount, 0f);
            this.wavelength = Positive(wavelength, 1f);
            this.speed = FiniteOr(speed, 0f);
            this.rate = Positive(rate, 24f);
            this.easing = easing;
        }

        /// <summary>Returns a copy with a new per-unit transition duration.</summary>
        public NowTextAnimation SetDuration(float seconds)
        {
            return Copy(duration: NonNegative(seconds));
        }

        /// <summary>Returns a copy with a new delay between consecutive units.</summary>
        public NowTextAnimation SetStagger(float seconds)
        {
            return Copy(stagger: NonNegative(seconds));
        }

        /// <summary>Returns a copy with a new delay before playback begins.</summary>
        public NowTextAnimation SetDelay(float seconds)
        {
            return Copy(delay: NonNegative(seconds));
        }

        /// <summary>Returns a copy using the requested finite-transition easing.</summary>
        public NowTextAnimation SetEasing(NowTextAnimationEasing value)
        {
            return Copy(easing: value);
        }

        internal bool isAnimated => kind != NowTextAnimationKind.None;

        /// <summary>True only when sampling a changing wave requires ongoing frames.</summary>
        internal bool isContinuous =>
            kind == NowTextAnimationKind.Wave &&
            !Mathf.Approximately(amount, 0f) &&
            !Mathf.Approximately(speed, 0f);

        /// <summary>
        /// Conservative UI-unit expansion needed by motion presets. ScaleIn is
        /// constrained to start at or below full size and therefore cannot expand
        /// the final glyph bounds.
        /// </summary>
        internal float boundedOutset => kind switch
        {
            NowTextAnimationKind.FadeUp => Mathf.Abs(amount),
            NowTextAnimationKind.Wave => Mathf.Abs(amount),
            _ => 0f
        };

        /// <summary>Samples one text unit without allocating or consulting a clock.</summary>
        internal NowTextAnimationState Sample(int unitIndex, float time)
        {
            unitIndex = Mathf.Max(0, unitIndex);
            time = float.IsNaN(time) ? 0f : time;

            switch (kind)
            {
                case NowTextAnimationKind.Typewriter:
                {
                    float elapsed = time - delay;
                    bool visible = elapsed >= 0f && elapsed * rate >= unitIndex + 1f;
                    return new NowTextAnimationState(visible, visible ? 1f : 0f, Vector2.zero, 1f);
                }
                case NowTextAnimationKind.FadeIn:
                {
                    float progress = EasedProgress(unitIndex, time);
                    return new NowTextAnimationState(progress > 0f, progress, Vector2.zero, 1f);
                }
                case NowTextAnimationKind.FadeUp:
                {
                    float progress = EasedProgress(unitIndex, time);
                    var offset = new Vector2(0f, amount * (1f - progress));
                    return new NowTextAnimationState(progress > 0f, progress, offset, 1f);
                }
                case NowTextAnimationKind.ScaleIn:
                {
                    float progress = EasedProgress(unitIndex, time);
                    float scale = Mathf.LerpUnclamped(amount, 1f, progress);
                    return new NowTextAnimationState(progress > 0f || scale > 0f, 1f, Vector2.zero, scale);
                }
                case NowTextAnimationKind.Wave:
                {
                    if (time < delay || float.IsInfinity(time))
                        return NowTextAnimationState.identity;

                    float phase = ((time - delay) * speed + unitIndex / wavelength) * Mathf.PI * 2f;
                    return new NowTextAnimationState(true, 1f, new Vector2(0f, Mathf.Sin(phase) * amount), 1f);
                }
                default:
                    return NowTextAnimationState.identity;
            }
        }

        /// <summary>Absolute sample time at which all finite units have settled.</summary>
        internal float CompletionTime(int unitCount)
        {
            if (unitCount <= 0 || kind == NowTextAnimationKind.None)
                return 0f;

            switch (kind)
            {
                case NowTextAnimationKind.Typewriter:
                    return delay + unitCount / rate;
                case NowTextAnimationKind.FadeIn:
                case NowTextAnimationKind.FadeUp:
                case NowTextAnimationKind.ScaleIn:
                    return delay + (unitCount - 1) * stagger + duration;
                case NowTextAnimationKind.Wave:
                    return isContinuous ? float.PositiveInfinity : delay;
                default:
                    return 0f;
            }
        }

        /// <summary>Whether the sampled animation has no future visual changes.</summary>
        internal bool IsComplete(float time, int unitCount)
        {
            if (unitCount <= 0 || kind == NowTextAnimationKind.None)
                return true;

            if (kind == NowTextAnimationKind.Wave)
            {
                if (Mathf.Approximately(amount, 0f))
                    return true;

                time = float.IsNaN(time) ? 0f : time;
                return time >= delay && !isContinuous;
            }

            if (kind == NowTextAnimationKind.ScaleIn && Mathf.Approximately(amount, 1f))
                return true;

            time = float.IsNaN(time) ? 0f : time;
            return time >= CompletionTime(unitCount);
        }

        /// <summary>
        /// Whether a retained host should request another frame for this sample.
        /// The caller still owns and advances the time value.
        /// </summary>
        internal bool RequiresRepaint(float time, int unitCount)
        {
            return unitCount > 0 && !IsComplete(time, unitCount);
        }

        float EasedProgress(int unitIndex, float time)
        {
            float localTime = time - delay - unitIndex * stagger;
            float progress = duration > 0f
                ? Mathf.Clamp01(localTime / duration)
                : localTime >= 0f ? 1f : 0f;
            return Ease(progress, easing);
        }

        NowTextAnimation Copy(
            float? duration = null,
            float? stagger = null,
            float? delay = null,
            NowTextAnimationEasing? easing = null)
        {
            return new NowTextAnimation(
                kind,
                duration ?? this.duration,
                stagger ?? this.stagger,
                delay ?? this.delay,
                amount,
                wavelength,
                speed,
                rate,
                easing ?? this.easing);
        }

        static float Ease(float value, NowTextAnimationEasing easing)
        {
            value = Mathf.Clamp01(value);

            switch (easing)
            {
                case NowTextAnimationEasing.EaseIn:
                    return value * value * value;
                case NowTextAnimationEasing.EaseOut:
                {
                    float inverse = 1f - value;
                    return 1f - inverse * inverse * inverse;
                }
                case NowTextAnimationEasing.EaseInOut:
                    return value < 0.5f
                        ? 4f * value * value * value
                        : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
                default:
                    return value;
            }
        }

        static float NonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }

        static float Positive(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;
        }

        static float FiniteOr(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    /// <summary>Allocation-free factories for built-in text animation values.</summary>
    public static class NowTextAnimations
    {
        /// <summary>
        /// Reveals complete text units at <paramref name="charactersPerSecond"/>.
        /// The name uses the familiar typewriter terminology; shaped clusters are
        /// kept atomic by the renderer.
        /// </summary>
        public static NowTextAnimation Typewriter(float charactersPerSecond = 24f)
        {
            return new NowTextAnimation(
                NowTextAnimationKind.Typewriter,
                0f,
                0f,
                0f,
                0f,
                1f,
                0f,
                charactersPerSecond,
                NowTextAnimationEasing.Linear);
        }

        /// <summary>Fades each text unit in, optionally staggered after the previous unit.</summary>
        public static NowTextAnimation FadeIn(float duration = 0.3f, float stagger = 0.025f)
        {
            return new NowTextAnimation(
                NowTextAnimationKind.FadeIn,
                duration,
                stagger,
                0f,
                0f,
                1f,
                0f,
                24f,
                NowTextAnimationEasing.EaseOut);
        }

        /// <summary>
        /// Starts each unit <paramref name="distance"/> UI units below its final
        /// position, then moves it upward while fading it in.
        /// </summary>
        public static NowTextAnimation FadeUp(
            float distance = 12f,
            float duration = 0.3f,
            float stagger = 0.025f)
        {
            return new NowTextAnimation(
                NowTextAnimationKind.FadeUp,
                duration,
                stagger,
                0f,
                distance,
                1f,
                0f,
                24f,
                NowTextAnimationEasing.EaseOut);
        }

        /// <summary>
        /// Scales each unit from <paramref name="startScale"/> to its authored
        /// size. Starting scale is clamped to 0..1 so the animation cannot expand
        /// beyond the final glyph bounds.
        /// </summary>
        public static NowTextAnimation ScaleIn(
            float startScale = 0.8f,
            float duration = 0.3f,
            float stagger = 0.025f)
        {
            startScale = float.IsNaN(startScale) || float.IsInfinity(startScale)
                ? 0.8f
                : Mathf.Clamp01(startScale);

            return new NowTextAnimation(
                NowTextAnimationKind.ScaleIn,
                duration,
                stagger,
                0f,
                startScale,
                1f,
                0f,
                24f,
                NowTextAnimationEasing.EaseOut);
        }

        /// <summary>
        /// Applies a continuous vertical sine wave. Amplitude is in UI units,
        /// wavelength is measured in text units, and speed is cycles per second.
        /// </summary>
        public static NowTextAnimation Wave(
            float amplitude = 4f,
            float wavelength = 6f,
            float speed = 1f)
        {
            return new NowTextAnimation(
                NowTextAnimationKind.Wave,
                0f,
                0f,
                0f,
                amplitude,
                wavelength,
                speed,
                24f,
                NowTextAnimationEasing.Linear);
        }
    }

    /// <summary>Resolved visual state for one text animation unit.</summary>
    internal readonly struct NowTextAnimationState
    {
        public readonly bool visible;

        public readonly float alpha;

        public readonly Vector2 offset;

        public readonly float scale;

        public static NowTextAnimationState identity =>
            new NowTextAnimationState(true, 1f, Vector2.zero, 1f);

        public NowTextAnimationState(bool visible, float alpha, Vector2 offset, float scale)
        {
            this.visible = visible;
            this.alpha = Mathf.Clamp01(alpha);
            this.offset = offset;
            this.scale = Mathf.Max(0f, scale);
        }
    }

    /// <summary>
    /// Allocation-free approximation of Unicode grapheme boundaries for the
    /// unshaped fallback. Shaped text uses HarfBuzz cluster indices directly;
    /// this cursor keeps combining marks, variation selectors, emoji modifiers,
    /// ZWJ sequences, and regional-indicator pairs atomic as well.
    /// </summary>
    internal struct NowTextUnitCursor
    {
        int _index;
        bool _joinNext;
        int _regionalIndicators;
        bool _hasUnit;

        public int index => Mathf.Max(0, _index);

        public NowTextUnitCursor(int offset)
        {
            _index = Mathf.Max(0, offset) - 1;
            _joinNext = false;
            _regionalIndicators = 0;
            _hasUnit = false;
        }

        public int MoveNext(int codepoint)
        {
            bool regional = codepoint >= 0x1F1E6 && codepoint <= 0x1F1FF;
            bool continuation = _hasUnit && (
                _joinNext ||
                IsContinuation(codepoint) ||
                regional && (_regionalIndicators & 1) != 0);

            if (!continuation)
                ++_index;

            _hasUnit = true;

            _joinNext = codepoint == 0x200D;

            if (regional)
                ++_regionalIndicators;
            else if (codepoint != 0x200D && !IsContinuation(codepoint))
                _regionalIndicators = 0;

            return index;
        }

        /// <summary>Prevents a grapheme sequence from continuing across layout controls.</summary>
        public void BreakSequence()
        {
            _joinNext = false;
            _regionalIndicators = 0;
            _hasUnit = false;
        }

        internal static int Count(System.ReadOnlySpan<char> value)
        {
            var cursor = new NowTextUnitCursor(0);
            int count = 0;

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = ReadCodepoint(value, ref i);

                if (codepoint == '\n' || codepoint == '\r' || codepoint == '\t')
                {
                    cursor.BreakSequence();
                    continue;
                }

                count = cursor.MoveNext(codepoint) + 1;
            }

            return count;
        }

        static int ReadCodepoint(System.ReadOnlySpan<char> value, ref int index)
        {
            char high = value[index];

            if (char.IsHighSurrogate(high) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                return char.ConvertToUtf32(high, value[++index]);

            return high;
        }

        static bool IsContinuation(int codepoint)
        {
            if (codepoint == 0x200D ||
                codepoint >= 0xFE00 && codepoint <= 0xFE0F ||
                codepoint >= 0xE0100 && codepoint <= 0xE01EF ||
                codepoint >= 0x1F3FB && codepoint <= 0x1F3FF)
            {
                return true;
            }

            if (codepoint >= 0x0300 && codepoint <= 0x036F ||
                codepoint >= 0x1AB0 && codepoint <= 0x1AFF ||
                codepoint >= 0x1DC0 && codepoint <= 0x1DFF ||
                codepoint >= 0x20D0 && codepoint <= 0x20FF ||
                codepoint >= 0xFE20 && codepoint <= 0xFE2F ||
                codepoint >= 0x1D165 && codepoint <= 0x1D169 ||
                codepoint >= 0x1D16D && codepoint <= 0x1D172)
            {
                return true;
            }

            if (codepoint > char.MaxValue)
                return false;

            var category = char.GetUnicodeCategory((char)codepoint);
            return category == System.Globalization.UnicodeCategory.NonSpacingMark ||
                category == System.Globalization.UnicodeCategory.SpacingCombiningMark ||
                category == System.Globalization.UnicodeCategory.EnclosingMark;
        }
    }
}
