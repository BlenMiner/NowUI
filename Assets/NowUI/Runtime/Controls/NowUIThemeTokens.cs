namespace NowUI
{
    /// <summary>
    /// Typed names for the built-in theme tokens and presets, so control code and
    /// call sites get autocomplete and typo-safety instead of string literals:
    /// <code>
    /// NowLayout.Button("Save").SetPreset(NowUIThemeTokens.Rect.Outline).Draw();
    /// theme.GetColor(NowUIThemeTokens.Color.Accent, fallback);
    /// </code>
    /// These are plain string constants — not an enum — because themes are
    /// data-driven and user-extensible: custom preset names defined in a
    /// <see cref="NowUITheme"/> asset work everywhere a token does.
    /// </summary>
    public static class NowUIThemeTokens
    {
        /// <summary>Rectangle preset ids (<see cref="NowUITheme.Rectangle"/>).</summary>
        public static class Rect
        {
            public const string Surface = "surface";
            public const string Muted = "muted";
            public const string Outline = "outline";
            public const string Accent = "accent";
        }

        /// <summary>Text preset ids (<see cref="NowUITheme.Text(NowRect, string)"/>).</summary>
        public static class Text
        {
            public const string Title = "title";
            public const string Body = "body";
            public const string Muted = "muted";
            public const string Button = "button";
        }

        /// <summary>Palette color token ids (<see cref="NowUITheme.GetColor"/>).</summary>
        public static class Color
        {
            public const string Background = "background";
            public const string Surface = "surface";
            public const string SurfaceMuted = "surface-muted";
            public const string Text = "text";
            public const string TextMuted = "text-muted";
            public const string Border = "border";
            public const string Accent = "accent";
            public const string AccentText = "accent-text";
        }

        /// <summary>Spacing token ids (<see cref="NowUITheme.GetSpacing"/>).</summary>
        public static class Spacing
        {
            public const string None = "none";
            public const string Xs = "xs";
            public const string Sm = "sm";
            public const string Md = "md";
            public const string Lg = "lg";
            public const string Panel = "panel";
        }

        /// <summary>Radius token ids (<see cref="NowUITheme.GetRadius"/>).</summary>
        public static class Radius
        {
            public const string None = "none";
            public const string Sm = "sm";
            public const string Md = "md";
            public const string Lg = "lg";
            public const string Pill = "pill";
        }
    }
}
