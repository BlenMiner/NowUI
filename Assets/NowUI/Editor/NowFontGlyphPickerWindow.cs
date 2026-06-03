using UnityEditor;
using UnityEngine;

public sealed class NowFontGlyphPickerWindow : EditorWindow
{
    readonly NowFontGlyphPickerControl _picker = new NowFontGlyphPickerControl();
    NowFont _font;

    public static void Show(NowFont font)
    {
        var window = GetWindow<NowFontGlyphPickerWindow>("Glyph Explorer");
        window.SetFont(font);
        window.Show();
        window.Focus();
    }

    [MenuItem("Window/NowUI/Glyph Explorer")]
    public static void ShowWindow()
    {
        Show(null);
    }

    void OnEnable()
    {
        minSize = new Vector2(420f, 300f);

        if (_font != null)
            _picker.SetFont(_font);
    }

    void OnGUI()
    {
        _picker.Draw(
            _font,
            true,
            0f,
            true,
            SetFont,
            ShowNotification,
            Repaint);
    }

    void SetFont(NowFont font)
    {
        _font = font;
        _picker.SetFont(font);
        Repaint();
    }
}
