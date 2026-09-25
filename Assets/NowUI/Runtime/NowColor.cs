using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Small color helpers for authored (display/sRGB) colors, the space NowUI
    /// builders and theme tokens use. They are extension methods, so they read
    /// inline: <c>accent.WithAlpha(0.4f)</c>, <c>panel.Lighten(0.1f)</c>.
    /// </summary>
    public static class NowColor
    {
        /// <summary>The same color with its alpha replaced by <paramref name="alpha"/>.</summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>The same color with its alpha multiplied by <paramref name="factor"/>, for fading an existing color.</summary>
        public static Color MultiplyAlpha(this Color color, float factor)
        {
            color.a *= factor;
            return color;
        }

        /// <summary>
        /// Mixes toward white by <paramref name="amount"/> (0 unchanged, 1 white),
        /// keeping alpha. The mix happens on authored values, like the theme's own
        /// hover and pressed tints.
        /// </summary>
        public static Color Lighten(this Color color, float amount)
        {
            return MixRgb(color, Color.white, amount);
        }

        /// <summary>Mixes toward black by <paramref name="amount"/> (0 unchanged, 1 black), keeping alpha.</summary>
        public static Color Darken(this Color color, float amount)
        {
            return MixRgb(color, Color.black, amount);
        }

        /// <summary>
        /// Mixes the color's RGB toward <paramref name="other"/> by <paramref name="amount"/>
        /// (unclamped), keeping this color's alpha. Use <see cref="Color.Lerp"/> to blend
        /// alpha as well.
        /// </summary>
        public static Color MixRgb(this Color color, Color other, float amount)
        {
            var mixed = Color.LerpUnclamped(color, other, amount);
            mixed.a = color.a;
            return mixed;
        }

        /// <summary>Relative luminance of the authored color (Rec. 709 weights), ignoring alpha.</summary>
        public static float Luminance(this Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }
    }
}
