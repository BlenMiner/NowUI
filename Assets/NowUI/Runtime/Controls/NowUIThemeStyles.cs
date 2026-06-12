using UnityEngine;

namespace NowUI
{
    /// <summary>Built-in rectangle styles (theme rectangle presets).</summary>
    public enum NowRectangleStyle
    {
        Surface,
        Muted,
        Outline,
        Accent
    }

    /// <summary>Built-in text styles (theme text presets).</summary>
    public enum NowTextStyle
    {
        Title,
        Body,
        Muted,
        Button
    }

    /// <summary>Built-in palette colors.</summary>
    public enum NowColorToken
    {
        Background,
        Surface,
        SurfaceMuted,
        Text,
        TextMuted,
        Border,
        Accent,
        AccentText
    }

    /// <summary>Built-in spacing steps.</summary>
    public enum NowSpacingToken
    {
        None,
        Xs,
        Sm,
        Md,
        Lg,
        Panel
    }

    /// <summary>Built-in corner radii.</summary>
    public enum NowRadiusToken
    {
        None,
        Sm,
        Md,
        Lg,
        Pill
    }

    /// <summary>
    /// Enum-first theme access: the typed API controls and call sites use.
    /// <code>
    /// theme.Rectangle(rect, NowRectangleStyle.Accent).Draw();
    /// theme.GetColor(NowColorToken.Text, Color.black);
    /// </code>
    /// The string-id methods on <see cref="NowUITheme"/> remain the low-level
    /// layer: theme assets stay data-driven, and custom controls that define
    /// their own preset names use them directly.
    /// </summary>
    public static class NowUIThemeStyleExtensions
    {
        static readonly string[] RectangleIds = { "surface", "muted", "outline", "accent" };

        static readonly string[] TextIds = { "title", "body", "muted", "button" };

        static readonly string[] ColorIds = { "background", "surface", "surface-muted", "text", "text-muted", "border", "accent", "accent-text" };

        static readonly string[] SpacingIds = { "none", "xs", "sm", "md", "lg", "panel" };

        static readonly string[] RadiusIds = { "none", "sm", "md", "lg", "pill" };

        /// <summary>The underlying theme preset id for a built-in style.</summary>
        public static string Id(this NowRectangleStyle style) => RectangleIds[(int)style];

        public static string Id(this NowTextStyle style) => TextIds[(int)style];

        public static string Id(this NowColorToken token) => ColorIds[(int)token];

        public static string Id(this NowSpacingToken token) => SpacingIds[(int)token];

        public static string Id(this NowRadiusToken token) => RadiusIds[(int)token];

        public static NowUIRectangle Rectangle(this NowUITheme theme, NowRect rect, NowRectangleStyle style)
        {
            return theme.Rectangle(rect, style.Id());
        }

        public static NowUIText Text(this NowUITheme theme, NowRect rect, NowTextStyle style)
        {
            return theme.Text(rect, style.Id());
        }

        public static NowUIText Text(this NowUITheme theme, NowRect rect, NowFontAsset font, NowTextStyle style)
        {
            return theme.Text(rect, font, style.Id());
        }

        public static Color GetColor(this NowUITheme theme, NowColorToken token, Color fallback)
        {
            return theme.GetColor(token.Id(), fallback);
        }

        public static Vector4 GetSpacing(this NowUITheme theme, NowSpacingToken token, Vector4 fallback)
        {
            return theme.GetSpacing(token.Id(), fallback);
        }

        public static Vector4 GetRadius(this NowUITheme theme, NowRadiusToken token, Vector4 fallback)
        {
            return theme.GetRadius(token.Id(), fallback);
        }
    }
}
