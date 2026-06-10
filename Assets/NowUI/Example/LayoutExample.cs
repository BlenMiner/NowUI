using System;
using UnityEngine;

/// <summary>
/// Demonstrates NowUILayout: a settings panel built without any manual
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
        NowUI.defaultFont = _font;
        _drawPanelContent = DrawPanelContent;
    }

    void OnPostRender()
    {
        NowUI.StartUI();

        using (NowUIInput.Begin(new Vector2(Screen.width, Screen.height)))
        {
            DrawPanel();
        }

        NowUI.FlushUI();
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

        NowUI.Rectangle(panelRect)
            .SetColor(Rgb(30, 33, 40))
            .SetRadius(16)
            .Draw();

        // The callback form lays out in two passes (measure, then draw), so
        // flexible space and auto-sized groups are exact every frame.
        NowUILayout.Area("settings", panelRect,
            new NowUILayoutOptions().SetPadding(20).SetSpacing(14),
            _drawPanelContent);
    }

    void DrawPanelContent()
    {
        DrawHeader();
        DrawTabs();
        DrawOptions();

        NowUILayout.FlexibleSpace();

        DrawFooter();
    }

    void DrawHeader()
    {
        using (NowUILayout.Horizontal())
        {
            NowUILayout.Label("Settings", 24f, Rgb(235, 238, 245))
                .SetAlign(NowUILayoutAlign.Center)
                .Draw();

            NowUILayout.FlexibleSpace();

            Button("close", "X", NowUILayout.Size(34, 34), Rgb(55, 60, 72));
        }
    }

    void DrawTabs()
    {
        using (NowUILayout.Horizontal(new NowUILayoutOptions().SetSpacing(8)))
        {
            for (int i = 0; i < _tabs.Length; ++i)
            {
                var background = i == _selectedTab ? Rgb(72, 110, 235) : Rgb(45, 49, 59);
                var size = NowUILayout.StretchWidth().SetHeight(36);

                if (Button(_tabs[i], _tabs[i], size, background))
                    _selectedTab = i;
            }
        }
    }

    void DrawOptions()
    {
        using (NowUILayout.Vertical("options", new NowUILayoutOptions().SetSpacing(8)))
        {
            for (int i = 0; i < _options.Length; ++i)
            {
                using var row = NowUILayout.Horizontal(_options[i],
                    new NowUILayoutOptions().SetHeight(44).SetPadding(10));

                NowUI.Rectangle(row.rect)
                    .SetColor(Rgb(40, 44, 53))
                    .SetRadius(10)
                    .Draw();

                NowUILayout.Label(_options[i], 16f, Rgb(210, 215, 226))
                    .SetAlign(NowUILayoutAlign.Center)
                    .Draw();

                NowUILayout.FlexibleSpace();

                if (Toggle(_options[i] + ".toggle", _toggles[i]))
                    _toggles[i] = !_toggles[i];
            }
        }
    }

    void DrawFooter()
    {
        using (NowUILayout.Horizontal(new NowUILayoutOptions().SetSpacing(10)))
        {
            NowUILayout.FlexibleSpace();

            Button("cancel", "Cancel", NowUILayout.Size(110, 38), Rgb(55, 60, 72));
            Button("apply", "Apply", NowUILayout.Size(110, 38), Rgb(72, 110, 235));
        }
    }

    bool Button(string id, string label, NowUILayoutOptions options, Color background)
    {
        Vector4 rect = NowUILayout.Rect(options);
        var interaction = NowUIInput.Interact(id, rect);

        if (interaction.hovered)
            background = Color.Lerp(background, Color.white, interaction.held ? 0.25f : 0.12f);

        NowUI.Rectangle(rect)
            .SetColor(background)
            .SetRadius(9)
            .Draw();

        DrawCenteredText(label, rect, 16, Rgb(235, 238, 245));
        return interaction.clicked;
    }

    bool Toggle(string id, bool value)
    {
        Vector4 rect = NowUILayout.Rect(
            NowUILayout.Size(44, 24).SetAlign(NowUILayoutAlign.Center));

        var interaction = NowUIInput.Interact(id, rect);

        var track = value ? Rgb(72, 110, 235) : Rgb(60, 65, 77);

        if (interaction.hovered)
            track = Color.Lerp(track, Color.white, 0.1f);

        NowUI.Rectangle(rect)
            .SetColor(track)
            .SetRadius(12)
            .Draw();

        float knobX = value ? rect.x + rect.z - 22f : rect.x + 2f;

        NowUI.Rectangle(new Vector4(knobX, rect.y + 2f, 20f, 20f))
            .SetColor(Color.white)
            .SetRadius(10)
            .Draw();

        return interaction.clicked;
    }

    void DrawCenteredText(string text, Vector4 rect, float size, Color color)
    {
        if (NowUI.defaultFont == null || string.IsNullOrEmpty(text))
            return;

        Vector2 measured = NowUI.defaultFont.MeasureText(text, size);
        var textRect = new Vector4(
            rect.x + (rect.z - measured.x) * 0.5f,
            rect.y + (rect.w - measured.y) * 0.5f,
            measured.x,
            measured.y);

        NowUI.Text(textRect)
            .SetFontSize(size)
            .SetColor(color)
            .SetMask(new Vector4(rect.x - 4f, rect.y - 4f, rect.z + 8f, rect.w + 8f))
            .Draw(text);
    }
}
