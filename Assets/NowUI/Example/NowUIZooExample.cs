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
[AddComponentMenu("NowUI/Examples/NowUI Zoo")]
public class NowUIZooExample : NowUIGraphic
{
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset _lottie;

    // All control state lives with the caller — NowUI never owns your data.
    bool _shadows = true;
    bool _vsync;
    bool _animate = true;
    int _difficulty = 1;
    float _volume = 0.7f;
    float _gamma = 2.2f;
    string _playerName = "";
    int _resolution = 1;
    int _clicks;
    float _hoverSpin;
    readonly List<string> _log = new List<string>();

    static readonly string[] Resolutions = { "1280 x 720", "1920 x 1080", "2560 x 1440", "3840 x 2160" };
    static readonly string[] Difficulties = { "Story", "Normal", "Hard" };

    protected override void DrawNowUI(NowRect rect)
    {
        if (_font == null)
            return;

        Now.defaultFont = _font;
        var theme = NowControls.theme;
        var bounds = new NowRect(0, 0, rect.width, rect.height);

        // Bare labels default to white (immediate-mode default); follow the
        // theme's text color instead so they read on the light surface. The
        // style must carry a font — labels don't resolve one at draw time.
        NowLayout.labelStyle = new NowUIText(default, _font)
            .SetFontSize(14)
            .SetColor(theme.GetColor(NowColorToken.Text, Color.black));

        theme.Rectangle(bounds, NowRectangleStyle.Surface).SetRadius(14).Draw();

        // Animations only advance when something repaints; while the zoo is
        // animating, keep asking for the next frame.
        if (_animate)
            NowUIControlState.RequestRepaint();

        using (NowLayout.Area(bounds.Inset(16), new NowLayoutOptions().SetSpacing(12)))
        {
            Header(theme);

            using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(16)))
            {
                using (NowLayout.Vertical(new NowLayoutOptions().SetSpacing(10).SetStretchWidth()))
                {
                    Buttons(theme);
                    Toggles(theme);
                    Sliders(theme);
                }

                using (NowLayout.Vertical(new NowLayoutOptions().SetSpacing(10).SetStretchWidth()))
                {
                    Fields(theme);
                    ScrollLog(theme);
                    Swatches(theme);
                }
            }

            Marquee(theme);
        }
    }

    void Header(NowUITheme theme)
    {
        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(10).SetAlignItems(NowLayoutAlign.Center)))
        {
            if (_lottie != null)
                NowLayout.Lottie(_lottie).SetTime(Time.time).SetHeight(36).Draw();

            NowLayout.Label("NowUI Zoo").SetFontSize(26).SetBold().Draw();
            NowLayout.Label("Tab / arrows navigate, Enter activates").SetFontSize(11)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();

            NowLayout.FlexibleSpace();
            NowLayout.Checkbox("Animate").Draw(ref _animate);
        }

        Separator(theme);
    }

    void Buttons(NowUITheme theme)
    {
        SectionTitle(theme, "Buttons");

        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(6)))
        {
            if (NowLayout.Button("Accent").Draw())
                Log("Accent clicked");

            if (NowLayout.Button("Outline").SetStyle(NowRectangleStyle.Outline).SetTextStyle(NowTextStyle.Body).Draw())
                Log("Outline clicked");

            if (NowLayout.Button("Muted").SetStyle(NowRectangleStyle.Muted).SetTextStyle(NowTextStyle.Muted).Draw())
                Log("Muted clicked");
        }

        // Content scope: anything inside, clipped to the button, result readable
        // inside. No id needed — identity is the call site.
        using (var button = NowLayout.Button().SetAlignItems(NowLayoutAlign.Center).Begin())
        {
            _hoverSpin = button.interaction.hovered ? _hoverSpin + Time.deltaTime : 0f;

            if (_lottie != null)
                NowLayout.Lottie(_lottie).SetTime(_hoverSpin).SetHeight(28).Draw();

            NowLayout.Label($"Clicked {_clicks} times")
                .SetColor(theme.GetColor(NowColorToken.AccentText, Color.white)).Draw();
            NowLayout.Label("(hover spins the icon)").SetFontSize(10)
                .SetColor(theme.GetColor(NowColorToken.AccentText, Color.white)).Draw();

            if (button.clicked)
            {
                _clicks++;
                Log($"Content button #{_clicks}");
            }
        }
    }

    void Toggles(NowUITheme theme)
    {
        SectionTitle(theme, "Toggles");

        NowLayout.Checkbox("Shadows").Draw(ref _shadows);

        // Checkbox as a scope: custom content beside the box, toggled value
        // readable inside.
        using (NowLayout.Checkbox().SetAlignItems(NowLayoutAlign.Center).Begin(ref _vsync))
        {
            NowLayout.Label("VSync").Draw();
            NowLayout.Label(_vsync ? "(locked to refresh)" : "(uncapped)").SetFontSize(11)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
        }

        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(10)))
        {
            // One call site, salted per iteration — loops need no ceremony.
            for (int i = 0; i < Difficulties.Length; ++i)
                if (NowLayout.Radio(Difficulties[i], _difficulty == i).Draw())
                {
                    _difficulty = i;
                    Log($"Difficulty: {Difficulties[i]}");
                }
        }
    }

    void Sliders(NowUITheme theme)
    {
        SectionTitle(theme, "Sliders");

        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(8).SetAlignItems(NowLayoutAlign.Center)))
        {
            NowLayout.Label("Volume").Draw();
            NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _volume);
            NowLayout.Label($"{Mathf.RoundToInt(_volume * 100)}%").SetFontSize(12).Draw();
        }

        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(8).SetAlignItems(NowLayoutAlign.Center)))
        {
            NowLayout.Label("Gamma").Draw();
            NowLayout.Slider(1f, 3f).SetStretchWidth().Draw(ref _gamma);
            NowLayout.Label($"{_gamma:0.00}").SetFontSize(12).Draw();
        }
    }

    void Fields(NowUITheme theme)
    {
        SectionTitle(theme, "Fields");

        NowLayout.TextField().SetPlaceholder("Player name...").SetStretchWidth().Draw(ref _playerName);

        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(8)))
        {
            NowLayout.Dropdown(Resolutions).SetStretchWidth().Draw(ref _resolution);
        }

        if (!string.IsNullOrEmpty(_playerName))
            NowLayout.Label($"Hello, {_playerName}!").SetFontSize(12)
                .SetColor(theme.GetColor(NowColorToken.Accent, Color.cyan)).Draw();
    }

    void ScrollLog(NowUITheme theme)
    {
        SectionTitle(theme, "Scroll view (event log)");

        using (NowLayout.ScrollView().SetHeight(120).Begin())
        {
            if (_log.Count == 0)
                NowLayout.Label("Interact with anything...").SetFontSize(12)
                    .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();

            for (int i = _log.Count - 1; i >= 0; --i)
                NowLayout.Label(_log[i]).SetFontSize(12).Draw();
        }
    }

    void Swatches(NowUITheme theme)
    {
        SectionTitle(theme, "Theme presets");

        using (NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(6)))
        {
            foreach (NowRectangleStyle style in System.Enum.GetValues(typeof(NowRectangleStyle)))
            {
                var swatch = NowLayout.Rect(54, 24);
                theme.Rectangle(swatch, style).Draw();
            }
        }
    }

    void Marquee(NowUITheme theme)
    {
        // Explicit masking: the strip clips a label sliding through it.
        var strip = NowLayout.Rect(new NowLayoutOptions().SetStretchWidth().SetHeight(22));
        theme.Rectangle(strip, NowRectangleStyle.Muted).SetRadius(6).Draw();

        const string Text = "  masks * lottie * shaped text * focus * overlays * burst fonts  ";
        float scroll = _animate ? Mathf.Repeat(Time.time * 60f, strip.width + 460f) : 80f;

        using (Now.Mask(strip))
        {
            NowLayout.Label(Text).SetFontSize(13)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw(new NowRect(strip.x + strip.width - scroll, strip.y + 3f, 460f, 18f));
        }
    }

    void SectionTitle(NowUITheme theme, string title)
    {
        NowLayout.Label(title).SetFontSize(15).SetBold().Draw();
    }

    void Separator(NowUITheme theme)
    {
        var line = NowLayout.Rect(new NowLayoutOptions().SetStretchWidth().SetHeight(1));
        theme.Rectangle(line, NowRectangleStyle.Muted).Draw();
    }

    void Log(string entry)
    {
        _log.Add($"[{Time.frameCount}] {entry}");

        if (_log.Count > 50)
            _log.RemoveAt(0);
    }
}
