using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// One keyframe: the <see cref="value"/> reached at <see cref="time"/>, arriving
    /// from the previous key along <see cref="ease"/>. Build them with
    /// <see cref="NowKey.At{T}(float, T, NowEasing)"/> and evaluate an array with
    /// <see cref="NowKeyframes"/>.
    /// </summary>
    public readonly struct NowKey<T>
    {
        /// <summary>Time, on the caller's clock, at which the value is reached.</summary>
        public readonly float time;

        /// <summary>The value at <see cref="time"/>.</summary>
        public readonly T value;

        /// <summary>Curve used to travel from the previous key to this one.</summary>
        public readonly NowEasing ease;

        public NowKey(float time, T value, NowEasing ease = NowEasing.Linear)
        {
            this.time = time;
            this.value = value;
            this.ease = ease;
        }
    }

    /// <summary>Type-inferring factory for <see cref="NowKey{T}"/>.</summary>
    public static class NowKey
    {
        /// <summary>
        /// A key reaching <paramref name="value"/> at <paramref name="time"/> along
        /// <paramref name="ease"/>. Repeat the previous value to hold it:
        /// <c>NowKey.At(1.2f, open), NowKey.At(1.8f, open), NowKey.At(2.2f, closed, NowEasing.InOutCubic)</c>.
        /// </summary>
        public static NowKey<T> At<T>(float time, T value, NowEasing ease = NowEasing.Linear)
        {
            return new NowKey<T>(time, value, ease);
        }
    }

    /// <summary>
    /// Stateless, allocation-free keyframe evaluation over caller-owned time. Keys
    /// must be sorted by time. Before the first key the first value holds; after
    /// the last key the last value holds. Between two keys the value eases along
    /// the later key's curve (overshooting curves such as OutBack extrapolate).
    /// <code>
    /// static readonly NowKey&lt;Vector2&gt;[] Cursor =
    /// {
    ///     NowKey.At(0.0f, new Vector2(40, 300)),
    ///     NowKey.At(0.6f, new Vector2(420, 180), NowEasing.InOutCubic),
    ///     NowKey.At(1.4f, new Vector2(420, 180)),                  // hold
    ///     NowKey.At(2.0f, new Vector2(640, 260), NowEasing.OutBack),
    /// };
    /// Vector2 cursor = NowKeyframes.Evaluate(time, Cursor);
    /// </code>
    /// </summary>
    public static class NowKeyframes
    {
        public static float Evaluate(float time, ReadOnlySpan<NowKey<float>> keys)
        {
            if (!Locate(time, keys, out int index, out float t))
                return keys.IsEmpty ? 0f : keys[index].value;

            return Mathf.LerpUnclamped(keys[index].value, keys[index + 1].value, t);
        }

        public static Vector2 Evaluate(float time, ReadOnlySpan<NowKey<Vector2>> keys)
        {
            if (!Locate(time, keys, out int index, out float t))
                return keys.IsEmpty ? default : keys[index].value;

            return Vector2.LerpUnclamped(keys[index].value, keys[index + 1].value, t);
        }

        public static Vector3 Evaluate(float time, ReadOnlySpan<NowKey<Vector3>> keys)
        {
            if (!Locate(time, keys, out int index, out float t))
                return keys.IsEmpty ? default : keys[index].value;

            return Vector3.LerpUnclamped(keys[index].value, keys[index + 1].value, t);
        }

        /// <summary>Colors interpolate on their authored (display/sRGB) values, alpha included.</summary>
        public static Color Evaluate(float time, ReadOnlySpan<NowKey<Color>> keys)
        {
            if (!Locate(time, keys, out int index, out float t))
                return keys.IsEmpty ? default : keys[index].value;

            return Color.LerpUnclamped(keys[index].value, keys[index + 1].value, t);
        }

        /// <summary>Duration of a key track: the time of its last key, or 0 when empty.</summary>
        public static float Duration<T>(ReadOnlySpan<NowKey<T>> keys)
        {
            return keys.IsEmpty ? 0f : keys[keys.Length - 1].time;
        }

        /// <summary>
        /// Finds the segment containing <paramref name="time"/>. Returns false when the
        /// time is outside the keys (or there is at most one), with
        /// <paramref name="index"/> naming the key whose value holds.
        /// </summary>
        static bool Locate<T>(float time, ReadOnlySpan<NowKey<T>> keys, out int index, out float eased)
        {
            index = 0;
            eased = 0f;

            if (keys.Length <= 1 || float.IsNaN(time) || time <= keys[0].time)
                return false;

            int last = keys.Length - 1;

            if (time >= keys[last].time)
            {
                index = last;
                return false;
            }

            for (int i = 1; i <= last; ++i)
            {
                if (time < keys[i].time)
                {
                    index = i - 1;
                    eased = NowEase.Evaluate(keys[i].ease, time, keys[i - 1].time, keys[i].time);
                    return true;
                }
            }

            index = last;
            return false;
        }
    }
}
