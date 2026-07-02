namespace NowUI
{
    /// <summary>Built-in rectangle styles (theme rectangle presets).</summary>
    public enum NowRectangleStyle
    {
        Surface = 0,
        Muted = 1,
        Outline = 2,
        Accent = 3,
        Elevated = 4,
        AccentSoft = 5,
        Danger = 6,
        Ghost = 7
    }

    /// <summary>Built-in text styles (theme text presets).</summary>
    public enum NowTextStyle
    {
        Title = 0,
        Body = 1,
        Muted = 2,
        Button = 3,
        Display = 4,
        Heading = 5,
        Subheading = 6,
        BodyStrong = 7,
        Label = 8,
        Caption = 9
    }

    /// <summary>
    /// Built-in palette colors. Values are serialized in theme assets — new
    /// members are appended with explicit values, never inserted.
    /// </summary>
    public enum NowColorToken
    {
        Background = 0,
        Surface = 1,
        SurfaceMuted = 2,
        Text = 3,
        TextMuted = 4,
        Border = 5,
        Accent = 6,
        AccentText = 7,
        SurfaceElevated = 8,
        SurfaceHover = 9,
        SurfacePressed = 10,
        AccentHover = 11,
        AccentPressed = 12,
        AccentMuted = 13,
        BorderStrong = 14,
        FocusRing = 15,
        Success = 16,
        SuccessText = 17,
        SuccessMuted = 18,
        Warning = 19,
        WarningText = 20,
        WarningMuted = 21,
        Danger = 22,
        DangerText = 23,
        DangerMuted = 24,
        Shadow = 25,
        Scrim = 26
    }

    /// <summary>Built-in spacing steps.</summary>
    public enum NowSpacingToken
    {
        None = 0,
        Xs = 1,
        Sm = 2,
        Md = 3,
        Lg = 4,
        Panel = 5,
        Xl = 6,
        Xxl = 7
    }

    /// <summary>Built-in corner radii.</summary>
    public enum NowRadiusToken
    {
        None = 0,
        Sm = 1,
        Md = 2,
        Lg = 3,
        Pill = 4,
        Xl = 5
    }

    /// <summary>
    /// Built-in elevation levels: each maps to a themed drop-shadow preset
    /// (key + ambient layer) drawn behind the surface.
    /// </summary>
    public enum NowElevationToken
    {
        None = 0,
        Raised = 1,
        Overlay = 2,
        Modal = 3
    }
}
