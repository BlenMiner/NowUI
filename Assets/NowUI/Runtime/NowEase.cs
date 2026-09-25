using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Named easing curves evaluated by <see cref="NowEase.Evaluate(NowEasing, float)"/>.
    /// Curves with optional parameters (Back, Elastic, and Spring) use the
    /// defaults of the matching <see cref="NowEase"/> function.
    /// </summary>
    public enum NowEasing : byte
    {
        /// <summary>Constant-rate interpolation.</summary>
        Linear,

        /// <summary>Sinusoidal acceleration from rest.</summary>
        InSine,

        /// <summary>Sinusoidal deceleration into the final value.</summary>
        OutSine,

        /// <summary>Sinusoidal acceleration followed by deceleration.</summary>
        InOutSine,

        /// <summary>Quadratic acceleration from rest.</summary>
        InQuad,

        /// <summary>Quadratic deceleration into the final value.</summary>
        OutQuad,

        /// <summary>Quadratic acceleration followed by deceleration.</summary>
        InOutQuad,

        /// <summary>Cubic acceleration from rest.</summary>
        InCubic,

        /// <summary>Cubic deceleration into the final value.</summary>
        OutCubic,

        /// <summary>Cubic acceleration followed by deceleration.</summary>
        InOutCubic,

        /// <summary>Quartic acceleration from rest.</summary>
        InQuart,

        /// <summary>Quartic deceleration into the final value.</summary>
        OutQuart,

        /// <summary>Quartic acceleration followed by deceleration.</summary>
        InOutQuart,

        /// <summary>Quintic acceleration from rest.</summary>
        InQuint,

        /// <summary>Quintic deceleration into the final value.</summary>
        OutQuint,

        /// <summary>Quintic acceleration followed by deceleration.</summary>
        InOutQuint,

        /// <summary>Exponential acceleration from rest.</summary>
        InExpo,

        /// <summary>Exponential deceleration into the final value.</summary>
        OutExpo,

        /// <summary>Exponential acceleration followed by deceleration.</summary>
        InOutExpo,

        /// <summary>Circular acceleration from rest.</summary>
        InCirc,

        /// <summary>Circular deceleration into the final value.</summary>
        OutCirc,

        /// <summary>Circular acceleration followed by deceleration.</summary>
        InOutCirc,

        /// <summary>Pulls back below zero before accelerating toward one.</summary>
        InBack,

        /// <summary>Overshoots above one before settling.</summary>
        OutBack,

        /// <summary>Pulls back at the start and overshoots at the end.</summary>
        InOutBack,

        /// <summary>Elastic oscillation that grows into the start of the motion.</summary>
        InElastic,

        /// <summary>Elastic oscillation that decays around the final value.</summary>
        OutElastic,

        /// <summary>Elastic oscillation at both ends of the motion.</summary>
        InOutElastic,

        /// <summary>Bounces that grow toward the start of the motion.</summary>
        InBounce,

        /// <summary>Bounces that decay against the final value.</summary>
        OutBounce,

        /// <summary>Bounces at both ends of the motion.</summary>
        InOutBounce,

        /// <summary>Damped spring overshoot that lands exactly on one.</summary>
        Spring,

        /// <summary>Hermite smoothstep, <c>t * t * (3 - 2t)</c>: a gentle S-curve, as used by Mathf.SmoothStep.</summary>
        Smoothstep
    }

    /// <summary>
    /// Stateless, allocation-free easing functions. Every function takes
    /// normalized time, clamps it to [0, 1] (NaN is treated as 0), and returns
    /// eased progress with f(0) = 0 and f(1) = 1 exactly. Back, Elastic, and
    /// Spring curves may leave [0, 1] between the endpoints.
    /// </summary>
    /// <remarks>
    /// NowEase does not own a clock. Convert caller-owned time into normalized
    /// progress with <see cref="Progress"/> or evaluate a time window directly
    /// with <see cref="Evaluate(NowEasing, float, float, float)"/>.
    /// </remarks>
    public static class NowEase
    {
        /// <summary>Default overshoot for the Back curves (about 10% overshoot for <see cref="OutBack"/>).</summary>
        public const float DefaultOvershoot = 1.70158f;

        /// <summary>Default amplitude for the Elastic curves.</summary>
        public const float DefaultElasticAmplitude = 1f;

        /// <summary>Default period, in normalized time, for <see cref="InElastic"/> and <see cref="OutElastic"/>.</summary>
        public const float DefaultElasticPeriod = 0.3f;

        /// <summary>Default period, in normalized time, for <see cref="InOutElastic"/>.</summary>
        public const float DefaultInOutElasticPeriod = 0.45f;

        /// <summary>Default damping ratio for <see cref="Spring"/>.</summary>
        public const float DefaultSpringDamping = 0.5f;

        /// <summary>Default undamped oscillation count across the interval for <see cref="Spring"/>.</summary>
        public const float DefaultSpringFrequency = 2f;

        const float HalfPi = Mathf.PI * 0.5f;
        const float TwoPi = Mathf.PI * 2f;

        /// <summary>
        /// Returns normalized progress of <paramref name="time"/> through the
        /// window [<paramref name="start"/>, <paramref name="end"/>], clamped
        /// to [0, 1]. An empty or inverted window returns 1 when
        /// <paramref name="time"/> has reached <paramref name="end"/> and 0
        /// otherwise. NaN inputs return 0.
        /// </summary>
        /// <param name="time">Caller-owned clock value.</param>
        /// <param name="start">Time at which progress is 0.</param>
        /// <param name="end">Time at which progress reaches 1.</param>
        public static float Progress(float time, float start, float end)
        {
            if (end <= start)
                return time >= end ? 1f : 0f;

            return Clamp01((time - start) / (end - start));
        }

        /// <summary>Evaluates a named easing curve at normalized time <paramref name="t"/>.</summary>
        /// <param name="easing">The curve to evaluate. Unknown values evaluate as <see cref="NowEasing.Linear"/>.</param>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float Evaluate(NowEasing easing, float t)
        {
            switch (easing)
            {
                case NowEasing.InSine: return InSine(t);
                case NowEasing.OutSine: return OutSine(t);
                case NowEasing.InOutSine: return InOutSine(t);
                case NowEasing.InQuad: return InQuad(t);
                case NowEasing.OutQuad: return OutQuad(t);
                case NowEasing.InOutQuad: return InOutQuad(t);
                case NowEasing.InCubic: return InCubic(t);
                case NowEasing.OutCubic: return OutCubic(t);
                case NowEasing.InOutCubic: return InOutCubic(t);
                case NowEasing.InQuart: return InQuart(t);
                case NowEasing.OutQuart: return OutQuart(t);
                case NowEasing.InOutQuart: return InOutQuart(t);
                case NowEasing.InQuint: return InQuint(t);
                case NowEasing.OutQuint: return OutQuint(t);
                case NowEasing.InOutQuint: return InOutQuint(t);
                case NowEasing.InExpo: return InExpo(t);
                case NowEasing.OutExpo: return OutExpo(t);
                case NowEasing.InOutExpo: return InOutExpo(t);
                case NowEasing.InCirc: return InCirc(t);
                case NowEasing.OutCirc: return OutCirc(t);
                case NowEasing.InOutCirc: return InOutCirc(t);
                case NowEasing.InBack: return InBack(t);
                case NowEasing.OutBack: return OutBack(t);
                case NowEasing.InOutBack: return InOutBack(t);
                case NowEasing.InElastic: return InElastic(t);
                case NowEasing.OutElastic: return OutElastic(t);
                case NowEasing.InOutElastic: return InOutElastic(t);
                case NowEasing.InBounce: return InBounce(t);
                case NowEasing.OutBounce: return OutBounce(t);
                case NowEasing.InOutBounce: return InOutBounce(t);
                case NowEasing.Spring: return Spring(t);
                case NowEasing.Smoothstep: return Smoothstep(t);
                default: return Linear(t);
            }
        }

        /// <summary>
        /// Evaluates a named easing curve over a time window. Equivalent to
        /// <c>Evaluate(easing, Progress(time, start, end))</c>.
        /// </summary>
        /// <param name="easing">The curve to evaluate.</param>
        /// <param name="time">Caller-owned clock value.</param>
        /// <param name="start">Time at which the eased value is 0.</param>
        /// <param name="end">Time at which the eased value reaches 1.</param>
        public static float Evaluate(NowEasing easing, float time, float start, float end)
        {
            return Evaluate(easing, Progress(time, start, end));
        }

        /// <summary>
        /// A fade-in, hold, fade-out envelope over caller-owned time: 0 before
        /// <paramref name="inStart"/>, easing up to 1 at <paramref name="inEnd"/>,
        /// 1 until <paramref name="outStart"/>, and easing back to 0 at
        /// <paramref name="outEnd"/>. Use it for anything that appears, stays and
        /// leaves: a toast, a highlight, a caption.
        /// </summary>
        /// <param name="time">Caller-owned clock value.</param>
        /// <param name="inStart">Time the value starts rising from 0.</param>
        /// <param name="inEnd">Time the value reaches 1.</param>
        /// <param name="outStart">Time the value starts falling; clamped to no earlier than <paramref name="inEnd"/>.</param>
        /// <param name="outEnd">Time the value is back at 0.</param>
        /// <param name="easing">Curve for both edges; the falling edge mirrors it.</param>
        public static float Window(
            float time,
            float inStart,
            float inEnd,
            float outStart,
            float outEnd,
            NowEasing easing = NowEasing.InOutSine)
        {
            if (float.IsNaN(time))
                return 0f;

            outStart = Mathf.Max(outStart, inEnd);
            outEnd = Mathf.Max(outEnd, outStart);

            if (time >= outStart)
                return 1f - Evaluate(easing, Progress(time, outStart, outEnd));

            return Evaluate(easing, Progress(time, inStart, inEnd));
        }

        /// <summary>Hermite smoothstep: <c>t * t * (3 - 2t)</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float Smoothstep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Constant-rate progress: returns the clamped <paramref name="t"/>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float Linear(float t)
        {
            return Clamp01(t);
        }

        /// <summary>Sinusoidal ease-in: <c>1 - cos(t * pi / 2)</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InSine(float t)
        {
            if (!InRange(ref t))
                return t;

            return 1f - Mathf.Cos(t * HalfPi);
        }

        /// <summary>Sinusoidal ease-out: <c>sin(t * pi / 2)</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutSine(float t)
        {
            if (!InRange(ref t))
                return t;

            return Mathf.Sin(t * HalfPi);
        }

        /// <summary>Sinusoidal ease-in-out: <c>(1 - cos(t * pi)) / 2</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutSine(float t)
        {
            if (!InRange(ref t))
                return t;

            return (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;
        }

        /// <summary>Quadratic ease-in: <c>t^2</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InQuad(float t)
        {
            if (!InRange(ref t))
                return t;

            return t * t;
        }

        /// <summary>Quadratic ease-out: <c>1 - (1 - t)^2</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutQuad(float t)
        {
            if (!InRange(ref t))
                return t;

            float inverse = 1f - t;
            return 1f - inverse * inverse;
        }

        /// <summary>Quadratic ease-in-out.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutQuad(float t)
        {
            if (!InRange(ref t))
                return t;

            if (t < 0.5f)
                return 2f * t * t;

            float u = -2f * t + 2f;
            return 1f - u * u * 0.5f;
        }

        /// <summary>Cubic ease-in: <c>t^3</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InCubic(float t)
        {
            if (!InRange(ref t))
                return t;

            return t * t * t;
        }

        /// <summary>Cubic ease-out: <c>1 - (1 - t)^3</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutCubic(float t)
        {
            if (!InRange(ref t))
                return t;

            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        /// <summary>Cubic ease-in-out.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutCubic(float t)
        {
            if (!InRange(ref t))
                return t;

            // Kept in this exact form: NowTextAnimationEasing.EaseInOut
            // evaluates through it and must stay bit-identical.
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Quartic ease-in: <c>t^4</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InQuart(float t)
        {
            if (!InRange(ref t))
                return t;

            float squared = t * t;
            return squared * squared;
        }

        /// <summary>Quartic ease-out: <c>1 - (1 - t)^4</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutQuart(float t)
        {
            if (!InRange(ref t))
                return t;

            float inverse = 1f - t;
            float squared = inverse * inverse;
            return 1f - squared * squared;
        }

        /// <summary>Quartic ease-in-out.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutQuart(float t)
        {
            if (!InRange(ref t))
                return t;

            if (t < 0.5f)
            {
                float squared = t * t;
                return 8f * squared * squared;
            }

            float u = -2f * t + 2f;
            float uSquared = u * u;
            return 1f - uSquared * uSquared * 0.5f;
        }

        /// <summary>Quintic ease-in: <c>t^5</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InQuint(float t)
        {
            if (!InRange(ref t))
                return t;

            float squared = t * t;
            return squared * squared * t;
        }

        /// <summary>Quintic ease-out: <c>1 - (1 - t)^5</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutQuint(float t)
        {
            if (!InRange(ref t))
                return t;

            float inverse = 1f - t;
            float squared = inverse * inverse;
            return 1f - squared * squared * inverse;
        }

        /// <summary>Quintic ease-in-out.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutQuint(float t)
        {
            if (!InRange(ref t))
                return t;

            if (t < 0.5f)
            {
                float squared = t * t;
                return 16f * squared * squared * t;
            }

            float u = -2f * t + 2f;
            float uSquared = u * u;
            return 1f - uSquared * uSquared * u * 0.5f;
        }

        /// <summary>Exponential ease-in: <c>2^(10t - 10)</c>, with exact endpoints.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InExpo(float t)
        {
            if (!InRange(ref t))
                return t;

            return Mathf.Pow(2f, 10f * t - 10f);
        }

        /// <summary>Exponential ease-out: <c>1 - 2^(-10t)</c>, with exact endpoints.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutExpo(float t)
        {
            if (!InRange(ref t))
                return t;

            return 1f - Mathf.Pow(2f, -10f * t);
        }

        /// <summary>Exponential ease-in-out, with exact endpoints.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutExpo(float t)
        {
            if (!InRange(ref t))
                return t;

            return t < 0.5f
                ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
        }

        /// <summary>Circular ease-in: <c>1 - sqrt(1 - t^2)</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InCirc(float t)
        {
            if (!InRange(ref t))
                return t;

            return 1f - Mathf.Sqrt(1f - t * t);
        }

        /// <summary>Circular ease-out: <c>sqrt(1 - (t - 1)^2)</c>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutCirc(float t)
        {
            if (!InRange(ref t))
                return t;

            float u = t - 1f;
            return Mathf.Sqrt(1f - u * u);
        }

        /// <summary>Circular ease-in-out.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutCirc(float t)
        {
            if (!InRange(ref t))
                return t;

            if (t < 0.5f)
            {
                float u = 2f * t;
                return (1f - Mathf.Sqrt(1f - u * u)) * 0.5f;
            }

            float v = -2f * t + 2f;
            return (Mathf.Sqrt(1f - v * v) + 1f) * 0.5f;
        }

        /// <summary>
        /// Back ease-in: pulls below zero before accelerating toward one.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="overshoot">Pull-back strength; the default gives about 10%. Non-finite values use <see cref="DefaultOvershoot"/>.</param>
        public static float InBack(float t, float overshoot = DefaultOvershoot)
        {
            if (!InRange(ref t))
                return t;

            float s = FiniteOr(overshoot, DefaultOvershoot);
            return (s + 1f) * t * t * t - s * t * t;
        }

        /// <summary>
        /// Back ease-out: overshoots above one before settling.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="overshoot">Overshoot strength; the default gives about 10%. Non-finite values use <see cref="DefaultOvershoot"/>.</param>
        public static float OutBack(float t, float overshoot = DefaultOvershoot)
        {
            if (!InRange(ref t))
                return t;

            float s = FiniteOr(overshoot, DefaultOvershoot);
            float u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }

        /// <summary>
        /// Back ease-in-out: pulls back at the start and overshoots at the end.
        /// As in the conventional Penner curve, the overshoot is scaled by
        /// 1.525 so each half keeps a similar visual strength.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="overshoot">Overshoot strength before scaling. Non-finite values use <see cref="DefaultOvershoot"/>.</param>
        public static float InOutBack(float t, float overshoot = DefaultOvershoot)
        {
            if (!InRange(ref t))
                return t;

            float s = FiniteOr(overshoot, DefaultOvershoot) * 1.525f;

            if (t < 0.5f)
            {
                float u = 2f * t;
                return u * u * ((s + 1f) * u - s) * 0.5f;
            }

            float v = 2f * t - 2f;
            return (v * v * ((s + 1f) * v + s) + 2f) * 0.5f;
        }

        /// <summary>
        /// Elastic ease-in: the mirror of <see cref="OutElastic"/>, oscillating
        /// with growing amplitude before snapping to one.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="amplitude">Oscillation amplitude. Values below 1 (or non-finite) are treated as 1.</param>
        /// <param name="period">Oscillation period in normalized time. Non-positive or non-finite values use <see cref="DefaultElasticPeriod"/>.</param>
        public static float InElastic(float t, float amplitude = DefaultElasticAmplitude, float period = DefaultElasticPeriod)
        {
            if (!InRange(ref t))
                return t;

            return 1f - ElasticOut(1f - t, amplitude, period, DefaultElasticPeriod);
        }

        /// <summary>
        /// Elastic ease-out: overshoots and oscillates around one with
        /// exponentially decaying amplitude.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="amplitude">Oscillation amplitude. Values below 1 (or non-finite) are treated as 1.</param>
        /// <param name="period">Oscillation period in normalized time. Non-positive or non-finite values use <see cref="DefaultElasticPeriod"/>.</param>
        public static float OutElastic(float t, float amplitude = DefaultElasticAmplitude, float period = DefaultElasticPeriod)
        {
            if (!InRange(ref t))
                return t;

            return ElasticOut(t, amplitude, period, DefaultElasticPeriod);
        }

        /// <summary>
        /// Elastic ease-in-out: oscillates at both ends of the motion.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="amplitude">Oscillation amplitude. Values below 1 (or non-finite) are treated as 1.</param>
        /// <param name="period">Oscillation period in normalized time. Non-positive or non-finite values use <see cref="DefaultInOutElasticPeriod"/>.</param>
        public static float InOutElastic(float t, float amplitude = DefaultElasticAmplitude, float period = DefaultInOutElasticPeriod)
        {
            if (!InRange(ref t))
                return t;

            ElasticShape(amplitude, period, DefaultInOutElasticPeriod, out float a, out float p, out float phase);
            float u = 2f * t - 1f;
            float wave = Mathf.Sin((u - phase) * TwoPi / p);

            return u < 0f
                ? -0.5f * a * Mathf.Pow(2f, 10f * u) * wave
                : 0.5f * a * Mathf.Pow(2f, -10f * u) * wave + 1f;
        }

        /// <summary>Bounce ease-in: the mirror of <see cref="OutBounce"/>.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InBounce(float t)
        {
            if (!InRange(ref t))
                return t;

            return 1f - BounceOut(1f - t);
        }

        /// <summary>Bounce ease-out: decaying bounces against the final value.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float OutBounce(float t)
        {
            if (!InRange(ref t))
                return t;

            return BounceOut(t);
        }

        /// <summary>Bounce ease-in-out: bounces at both ends of the motion.</summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        public static float InOutBounce(float t)
        {
            if (!InRange(ref t))
                return t;

            return t < 0.5f
                ? (1f - BounceOut(1f - 2f * t)) * 0.5f
                : (1f + BounceOut(2f * t - 1f)) * 0.5f;
        }

        /// <summary>
        /// Damped spring: starts at rest, overshoots, and oscillates around one
        /// with decaying amplitude. The spring's residual displacement at
        /// t = 1 is blended out smoothly across the interval, so the curve
        /// lands exactly on one without a visible jump.
        /// </summary>
        /// <param name="t">Normalized time, clamped to [0, 1].</param>
        /// <param name="damping">Damping ratio, clamped to [0, 1]: 0 oscillates without decay, 1 is critically damped (no overshoot). NaN is treated as 0.</param>
        /// <param name="frequency">Undamped oscillations across the interval. Non-positive or non-finite values return linear progress.</param>
        public static float Spring(float t, float damping = DefaultSpringDamping, float frequency = DefaultSpringFrequency)
        {
            if (!InRange(ref t))
                return t;

            if (!(frequency > 0f) || float.IsInfinity(frequency))
                return t;

            float zeta = Clamp01(damping);
            float omega = TwoPi * frequency;
            float residual = SpringResidual(t, zeta, omega);
            float residualAtEnd = SpringResidual(1f, zeta, omega);
            float blend = t * t * (3f - 2f * t);
            return 1f - residual + residualAtEnd * blend;
        }

        /// <summary>
        /// CSS <c>cubic-bezier(x1, y1, x2, y2)</c> timing function: the curve
        /// from (0, 0) to (1, 1) with control points (x1, y1) and (x2, y2),
        /// solved for the y value at x = <paramref name="t"/>. As in CSS,
        /// <paramref name="x1"/> and <paramref name="x2"/> are clamped to
        /// [0, 1]; y values may leave that range to overshoot.
        /// For example, CSS <c>ease</c> is <c>CubicBezier(0.25f, 0.1f, 0.25f, 1f, t)</c>.
        /// </summary>
        /// <param name="x1">First control point x, clamped to [0, 1].</param>
        /// <param name="y1">First control point y.</param>
        /// <param name="x2">Second control point x, clamped to [0, 1].</param>
        /// <param name="y2">Second control point y.</param>
        /// <param name="t">Normalized time (the curve's x), clamped to [0, 1].</param>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (!InRange(ref t))
                return t;

            return SolveCubicBezier(Clamp01(x1), y1, Clamp01(x2), y2, t);
        }

        /// <summary>
        /// Shared cubic-bezier solver (Newton iterations with a bisection
        /// fallback, 1e-4 tolerance in x). Control-point x values are used as
        /// given; <see cref="Internal.NowLottieEasing"/> relies on this exact
        /// behavior for keyframe influence.
        /// </summary>
        internal static float SolveCubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0f)
                return 0f;

            if (t >= 1f)
                return 1f;

            if (Mathf.Approximately(x1, y1) && Mathf.Approximately(x2, y2))
                return t;

            float u = SolveBezierX(x1, x2, t);
            return SampleBezier(y1, y2, u);
        }

        static float SampleBezier(float p1, float p2, float t)
        {
            float oneMinusT = 1f - t;
            return 3f * oneMinusT * oneMinusT * t * p1 + 3f * oneMinusT * t * t * p2 + t * t * t;
        }

        static float SampleBezierDerivative(float p1, float p2, float t)
        {
            float oneMinusT = 1f - t;
            return 3f * oneMinusT * oneMinusT * p1 +
                6f * oneMinusT * t * (p2 - p1) +
                3f * t * t * (1f - p2);
        }

        static float SolveBezierX(float p1, float p2, float x)
        {
            float t = x;

            for (int i = 0; i < 6; ++i)
            {
                float currentX = SampleBezier(p1, p2, t) - x;

                if (Mathf.Abs(currentX) < 0.0001f)
                    return t;

                float derivative = SampleBezierDerivative(p1, p2, t);

                if (Mathf.Abs(derivative) < 0.000001f)
                    break;

                t -= currentX / derivative;
            }

            float low = 0f;
            float high = 1f;
            t = x;

            for (int i = 0; i < 24; ++i)
            {
                float currentX = SampleBezier(p1, p2, t);

                if (Mathf.Abs(currentX - x) < 0.0001f)
                    return t;

                if (currentX < x)
                    low = t;
                else
                    high = t;

                t = (low + high) * 0.5f;
            }

            return t;
        }

        static float ElasticOut(float t, float amplitude, float period, float defaultPeriod)
        {
            ElasticShape(amplitude, period, defaultPeriod, out float a, out float p, out float phase);
            return a * Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - phase) * TwoPi / p) + 1f;
        }

        static void ElasticShape(float amplitude, float period, float defaultPeriod, out float a, out float p, out float phase)
        {
            p = period > 0f && !float.IsInfinity(period) ? period : defaultPeriod;
            a = amplitude > 1f && !float.IsInfinity(amplitude) ? amplitude : 1f;

            // Phase offset chosen so the curve starts exactly at rest (0).
            phase = a > 1f
                ? p / TwoPi * Mathf.Asin(1f / a)
                : p * 0.25f;
        }

        static float BounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;

            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }

            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        static float SpringResidual(float t, float zeta, float omega)
        {
            float decay = Mathf.Exp(-zeta * omega * t);

            if (zeta >= 1f)
                return decay * (1f + omega * t);

            float dampedOmega = omega * Mathf.Sqrt(1f - zeta * zeta);
            return decay * (Mathf.Cos(dampedOmega * t) + zeta * omega / dampedOmega * Mathf.Sin(dampedOmega * t));
        }

        // Clamps t in place. Returns false when t is an endpoint (or NaN,
        // which becomes 0) so callers return the exact endpoint value.
        static bool InRange(ref float t)
        {
            if (!(t > 0f))
            {
                t = 0f;
                return false;
            }

            if (t >= 1f)
            {
                t = 1f;
                return false;
            }

            return true;
        }

        static float Clamp01(float value)
        {
            if (!(value > 0f))
                return 0f;

            return value >= 1f ? 1f : value;
        }

        static float FiniteOr(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
