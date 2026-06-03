using UnityEngine;

[AddComponentMenu("NowUI/Examples/NowUI Pipeline Overlay Example")]
public sealed class NowUIPipelineOverlayExample : NowUIPipelineGraphic
{
    [SerializeField] NowFont _font;

    [SerializeField, Range(0.75f, 1.5f)] float _scale = 1f;

    static Color Rgb(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    static Vector4 Rect(float x, float y, float width, float height)
    {
        return new Vector4(x, y, width, height);
    }

    protected override void DrawNowUI(Camera camera, Rect rect)
    {
        float width = rect.width;

        Vector4 panel = Rect(24, 24, Mathf.Min(360, width - 48), 116);

        NowUI.Rectangle(panel)
            .SetColor(new Color(0.02f, 0.03f, 0.05f, 0.82f))
            .SetRadius(14)
            .Draw();

        NowUI.Rectangle(Rect(panel.x, panel.y, 6, panel.w))
            .SetColor(Rgb(14, 165, 233))
            .SetRadius(new Vector4(14, 0, 0, 14))
            .Draw();

        DrawText("NowUI SRP Overlay", Rect(panel.x + 22, panel.y + 18, panel.z - 44, 30), 22, Color.white);
        DrawText(
            camera != null ? camera.name : "Pipeline camera",
            Rect(panel.x + 22, panel.y + 52, panel.z - 44, 22),
            14,
            Rgb(203, 213, 225));

        NowUI.Rectangle(Rect(panel.x + 22, panel.y + 82, 92, 24))
            .SetColor(Rgb(14, 165, 233))
            .SetRadius(12)
            .Draw();

        DrawText("URP/HDRP", Rect(panel.x + 35, panel.y + 84, 70, 18), 12, Color.white);
    }

    void DrawText(string text, Vector4 rect, float size, Color color)
    {
        if (_font == null || string.IsNullOrEmpty(text))
            return;

        NowUI.Text(rect, _font)
            .SetFontSize(size * _scale)
            .SetColor(color)
            .SetMask(rect)
            .Draw(text);
    }
}
