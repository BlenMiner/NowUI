using System;
using UnityEngine;
using NowUI;

/// <summary>
/// Demonstrates NowLayout: a settings panel built without any manual
/// coordinate math. Attach to a camera, assign a font, and enter play mode.
/// </summary>
public class LayoutExample : MonoBehaviour
{
    [SerializeField] NowFontAsset _font;

    static readonly string[] _tabs = { "General", "Audio", "Video" };

    static readonly string[] _options = {
        "Enable notifications",
        "Auto-save projects",
        "Hardware acceleration",
        "Send anonymous statistics"
    };

    readonly bool[] _toggles = { true, true, false, false };

    int _selectedTab;

    Action _drawPanelContent;

    static Color Rgb(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    void Awake()
    {
        Now.defaultFont = _font;
        _drawPanelContent = DrawPanelContent;
    }

    void OnPostRender()
    {
        using (Now.StartUI())
        {
            DrawPanel();
        }
    }

    void DrawPanel()
    {
        const float width = 480f;
        const float height = 420f;

        var panelRect = new Vector4(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        Now.Rectangle(panelRect)
            .SetColor(Rgb(30, 33, 40))
            .SetRadius(16)
            .Draw();

        NowLayout.Area("settings", panelRect, _drawPanelContent, padding: 20, spacing: 14);
    }

    void DrawPanelContent()
    {
        DrawHeader();
        DrawTabs();
        DrawOptions();

        NowLayout.FlexibleSpace();

        DrawFooter();
    }

    void DrawHeader()
    {
        using (NowLayout.Horizontal())
        {
            NowLayout.Label("Settings", 24f, Rgb(235, 238, 245))
                .SetAlign(NowLayoutAlign.Center)
                .Draw();

            NowLayout.FlexibleSpace();

            Button("close", "X", NowLayout.Size(34, 34), Rgb(55, 60, 72));
        }
    }

    void DrawTabs()
    {
        using (NowLayout.Horizontal(spacing: 8))
        {
            for (int i = 0; i < _tabs.Length; ++i)
            {
                var background = i == _selectedTab ? Rgb(72, 110, 235) : Rgb(45, 49, 59);
                var size = NowLayout.StretchWidth().SetHeight(36);

                if (Button(_tabs[i], _tabs[i], size, background))
                    _selectedTab = i;
            }
        }
    }

    void DrawOptions()
    {
        using (NowLayout.Vertical("options", spacing: 8))
        {
            for (int i = 0; i < _options.Length; ++i)
            {
                using var row = NowLayout.Horizontal(_options[i], height: 44, padding: 10);

                Now.Rectangle(row.rect)
                    .SetColor(Rgb(40, 44, 53))
                    .SetRadius(10)
                    .Draw();

                NowLayout.Label(_options[i], 16f, Rgb(210, 215, 226))
                    .SetAlign(NowLayoutAlign.Center)
                    .Draw();

                NowLayout.FlexibleSpace();

                if (Toggle(_options[i] + ".toggle", _toggles[i]))
                    _toggles[i] = !_toggles[i];
            }
        }
    }

    void DrawFooter()
    {
        using (NowLayout.Horizontal(spacing: 10))
        {
            NowLayout.FlexibleSpace();

            Button("cancel", "Cancel", NowLayout.Size(110, 38), Rgb(55, 60, 72));
            Button("apply", "Apply", NowLayout.Size(110, 38), Rgb(72, 110, 235));
        }
    }

    bool Button(string id, string label, NowLayoutOptions options, Color background)
    {
        Vector4 rect = NowLayout.Rect(options);
        var interaction = NowInput.Interact(id, rect);

        if (interaction.hovered)
            background = Color.Lerp(background, Color.white, interaction.held ? 0.25f : 0.12f);

        Now.Rectangle(rect)
            .SetColor(background)
            .SetRadius(9)
            .Draw();

        DrawCenteredText(label, rect, 16, Rgb(235, 238, 245));
        return interaction.clicked;
    }

    bool Toggle(string id, bool value)
    {
        Vector4 rect = NowLayout.Rect(
            NowLayout.Size(44, 24).SetAlign(NowLayoutAlign.Center));

        var interaction = NowInput.Interact(id, rect);

        var track = value ? Rgb(72, 110, 235) : Rgb(60, 65, 77);

        if (interaction.hovered)
            track = Color.Lerp(track, Color.white, 0.1f);

        Now.Rectangle(rect)
            .SetColor(track)
            .SetRadius(12)
            .Draw();

        float knobX = value ? rect.x + rect.z - 22f : rect.x + 2f;

        Now.Rectangle(new Vector4(knobX, rect.y + 2f, 20f, 20f))
            .SetColor(Color.white)
            .SetRadius(10)
            .Draw();

        return interaction.clicked;
    }

    void DrawCenteredText(string text, NowRect rect, float size, Color color)
    {
        if (Now.font == null || string.IsNullOrEmpty(text))
            return;

        Vector2 measured = Now.font.MeasureText(text, size);
        var textRect = new NowRect(rect.center - measured * 0.5f, measured);

        Now.Text(textRect)
            .SetFontSize(size)
            .SetColor(color)
            .SetMask(rect.Outset(4f))
            .Draw(text);
    }
}
