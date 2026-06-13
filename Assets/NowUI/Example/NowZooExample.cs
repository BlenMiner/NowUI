using System;
using System.Collections.Generic;
using UnityEngine;
using NowUI;

/// <summary>
/// The kitchen sink: every NowUI feature on one UGUI graphic — labels, buttons
/// (plain, styled, content scopes), checkboxes, radios, sliders, text fields,
/// dropdowns, scroll views, Lottie, masks, theme swatches, alignment, and
/// keyboard/gamepad focus. Drop it on a Canvas, assign a font (and optionally
/// a Lottie asset), and poke around.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Zoo")]
public class NowZooExample : NowGraphic
{
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset _lottie;

    bool _shadows = true;
    bool _vsync;
    bool _animate = true;
    int _difficulty = 1;
    float _volume = 0.7f;
    float _gamma = 2.2f;
    string _playerName = "";
    string _notes = "";
    int _resolution = 1;
    int _clicks;
    float _hoverSpin;
    string _clicksLabel = "Clicked 0 times";
    string _volumeLabel;
    string _gammaLabel;
    string _greetingLabel;
    readonly char[] _fpsBuffer = new char[16];
    readonly List<string> _log = new List<string>();

    static readonly string[] Resolutions = { "1280 x 720", "1920 x 1080", "2560 x 1440", "3840 x 2160" };
    static readonly string[] Difficulties = { "Story", "Normal", "Hard" };

    protected override void DrawNowUI(NowRect rect)
    {
        if (_font == null)
            return;

        Now.defaultFont = _font;
        var theme = NowControls.themeAsset;
        var bounds = new NowRect(0, 0, rect.width, rect.height);

        // The style must carry a font — labels don't resolve one at draw time.
        NowLayout.labelStyle = new NowText(default, _font)
            .SetFontSize(14)
            .SetColor(theme.GetColor(NowColorToken.Text, Color.black));

        theme.Rectangle(bounds, NowRectangleStyle.Surface).SetRadius(14).Draw();

        if (_animate)
            NowControlState.RequestRepaint();

        using (NowLayout.Area(bounds.Inset(16), spacing: 12))
        {
            Header(theme);

            using (NowLayout.Horizontal(spacing: 16))
            {
                using (NowLayout.Vertical(spacing: 10, stretchWidth: true))
                {
                    Buttons(theme);
                    Toggles(theme);
                    Sliders(theme);
                }

                using (NowLayout.Vertical(spacing: 10, stretchWidth: true))
                {
                    Fields(theme);
                    ScrollLog(theme);
                    Swatches(theme);
                }
            }

            Marquee(theme);
        }
    }

    void Header(NowThemeAsset themeAsset)
    {
        using (NowLayout.Horizontal(spacing: 10, alignItems: NowLayoutAlign.Center))
        {
            if (_lottie != null)
                NowLayout.Lottie(_lottie).SetTime(Time.time).SetHeight(36).Draw();

            NowLayout.Label("Now Zoo").SetFontSize(26).SetBold().Draw();
            NowLayout.Label("Tab / arrows navigate, Enter activates").SetFontSize(11)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();

            NowLayout.FlexibleSpace();
            DrawFpsCounter(themeAsset);
            NowLayout.Checkbox("Animate").Draw(ref _animate);
        }

        Separator(themeAsset);
    }

    /// <summary>
    /// Truly zero-GC dynamic text: format into a reusable char buffer and draw
    /// the span — no string is ever created.
    /// </summary>
    void DrawFpsCounter(NowThemeAsset themeAsset)
    {
        var rect = NowLayout.Rect(64, 16, align: NowLayoutAlign.Center);
        int fps = Mathf.RoundToInt(1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f));

        fps.TryFormat(_fpsBuffer, out int written);
        " fps".AsSpan().CopyTo(_fpsBuffer.AsSpan(written));

        Now.Text(rect)
            .SetFontSize(12)
            .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw(_fpsBuffer.AsSpan(0, written + 4));
    }

    void Buttons(NowThemeAsset themeAsset)
    {
        SectionTitle(themeAsset, "Buttons");

        using (NowLayout.Horizontal(spacing: 6))
        {
            if (NowLayout.Button("Accent").Draw())
                Log("Accent clicked");

            if (NowLayout.Button("Outline").SetStyle(NowRectangleStyle.Outline).SetTextStyle(NowTextStyle.Body).Draw())
                Log("Outline clicked");

            if (NowLayout.Button("Muted").SetStyle(NowRectangleStyle.Muted).SetTextStyle(NowTextStyle.Muted).Draw())
                Log("Muted clicked");
        }

        using (var button = NowLayout.Button().SetAlignItems(NowLayoutAlign.Center).Begin())
        {
            _hoverSpin = button.interaction.hovered ? _hoverSpin + Time.deltaTime : 0f;

            if (_lottie != null)
                NowLayout.Lottie(_lottie).SetTime(_hoverSpin).SetHeight(28).Draw();

            NowLayout.Label(_clicksLabel)
                .SetColor(themeAsset.GetColor(NowColorToken.AccentText, Color.white)).Draw();
            NowLayout.Label("(hover spins the icon)").SetFontSize(10)
                .SetColor(themeAsset.GetColor(NowColorToken.AccentText, Color.white)).Draw();

            if (button.clicked)
            {
                _clicks++;
                _clicksLabel = $"Clicked {_clicks} times";
                Log($"Content button #{_clicks}");
            }
        }
    }

    void Toggles(NowThemeAsset themeAsset)
    {
        SectionTitle(themeAsset, "Toggles");

        NowLayout.Checkbox("Shadows").Draw(ref _shadows);

        using (NowLayout.Checkbox().SetAlignItems(NowLayoutAlign.Center).Begin(ref _vsync))
        {
            NowLayout.Label("VSync").Draw();
            NowLayout.Label(_vsync ? "(locked to refresh)" : "(uncapped)").SetFontSize(11)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
        }

        using (NowLayout.Horizontal(spacing: 10))
        {
            for (int i = 0; i < Difficulties.Length; ++i)
                if (NowLayout.Radio(Difficulties[i], _difficulty == i).Draw())
                {
                    _difficulty = i;
                    Log($"Difficulty: {Difficulties[i]}");
                }
        }
    }

    void Sliders(NowThemeAsset themeAsset)
    {
        SectionTitle(themeAsset, "Sliders");

        // Per-frame string interpolation is the classic UI GC trap: format only
        // when the value changes, draw the cached string otherwise.
        using (NowLayout.Horizontal(spacing: 8, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Volume").Draw();

            if (NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _volume) || _volumeLabel == null)
                _volumeLabel = $"{Mathf.RoundToInt(_volume * 100)}%";

            NowLayout.Label(_volumeLabel).SetFontSize(12).Draw();
        }

        using (NowLayout.Horizontal(spacing: 8, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Gamma").Draw();

            if (NowLayout.Slider(1f, 3f).SetStretchWidth().Draw(ref _gamma) || _gammaLabel == null)
                _gammaLabel = $"{_gamma:0.00}";

            NowLayout.Label(_gammaLabel).SetFontSize(12).Draw();
        }
    }

    void Fields(NowThemeAsset themeAsset)
    {
        SectionTitle(themeAsset, "Fields");

        if (NowLayout.TextField().SetPlaceholder("Player name...").SetStretchWidth().Draw(ref _playerName))
            _greetingLabel = string.IsNullOrEmpty(_playerName) ? null : $"Hello, {_playerName}!";

        using (NowLayout.Horizontal(spacing: 8))
        {
            NowLayout.Dropdown(Resolutions).SetStretchWidth().Draw(ref _resolution);
        }

        NowLayout.TextArea().SetPlaceholder("Notes... (Enter for a new line)").SetStretchWidth().SetLines(3, 6).Draw(ref _notes);

        if (_greetingLabel != null)
            NowLayout.Label(_greetingLabel).SetFontSize(12)
                .SetColor(themeAsset.GetColor(NowColorToken.Accent, Color.cyan)).Draw();
    }

    void ScrollLog(NowThemeAsset themeAsset)
    {
        SectionTitle(themeAsset, "Scroll view (event log)");

        using (NowLayout.ScrollView().SetHeight(120).Begin())
        {
            if (_log.Count == 0)
                NowLayout.Label("Interact with anything...").SetFontSize(12)
                    .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();

            for (int i = _log.Count - 1; i >= 0; --i)
                NowLayout.Label(_log[i]).SetFontSize(12).Draw();
        }
    }

    static readonly NowRectangleStyle[] SwatchStyles =
    {
        NowRectangleStyle.Surface, NowRectangleStyle.Muted, NowRectangleStyle.Outline, NowRectangleStyle.Accent
    };

    void Swatches(NowThemeAsset themeAsset)
    {
        SectionTitle(themeAsset, "Theme presets");

        using (NowLayout.Horizontal(spacing: 6))
        {
            for (int i = 0; i < SwatchStyles.Length; ++i)
            {
                var swatch = NowLayout.Rect(54, 24);
                themeAsset.Rectangle(swatch, SwatchStyles[i]).Draw();
            }
        }
    }

    void Marquee(NowThemeAsset themeAsset)
    {
        var strip = NowLayout.Rect(height: 22, stretchWidth: true);
        themeAsset.Rectangle(strip, NowRectangleStyle.Muted).SetRadius(6).Draw();

        const string Text = "  masks * lottie * shaped text * focus * overlays * burst fonts  ";
        float scroll = _animate ? Mathf.Repeat(Time.time * 60f, strip.width + 460f) : 80f;

        using (Now.Mask(strip))
        {
            NowLayout.Label(Text).SetFontSize(13)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw(new NowRect(strip.x + strip.width - scroll, strip.y + 3f, 460f, 18f));
        }
    }

    void SectionTitle(NowThemeAsset themeAsset, string title)
    {
        NowLayout.Label(title).SetFontSize(15).SetBold().Draw();
    }

    void Separator(NowThemeAsset themeAsset)
    {
        var line = NowLayout.Rect(height: 1, stretchWidth: true);
        themeAsset.Rectangle(line, NowRectangleStyle.Muted).Draw();
    }

    void Log(string entry)
    {
        _log.Add($"[{Time.frameCount}] {entry}");

        if (_log.Count > 50)
            _log.RemoveAt(0);
    }
}
