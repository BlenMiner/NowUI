using UnityEngine;

[AddComponentMenu("NowUI/Examples/NowUI Graphic Example")]
public class NowUIGraphicExample : NowUIGraphic
{
    [SerializeField] NowFont m_font;

    protected override void DrawNowUI(Rect rect)
    {
        float width = rect.width;
        float height = rect.height;
        var bounds = new Vector4(0, 0, width, height);

        NowUI.Rectangle(bounds)
            .SetColor(new Color(0.08f, 0.1f, 0.14f, 0.92f))
            .SetRadius(12)
            .SetMask(bounds)
            .Draw();

        NowUI.Rectangle(new Vector4(16, 16, 42, 42))
            .SetColor(new Color(0.1f, 0.45f, 0.95f, 1f))
            .SetRadius(10)
            .SetMask(bounds)
            .Draw();

        if (m_font == null)
            return;

        NowUI.Text(new Vector4(72, 14, width - 96, 30), m_font)
            .SetFontSize(20)
            .SetColor(Color.white)
            .SetMask(bounds)
            .Draw("NowUI Graphic");

        NowUI.Text(new Vector4(72, 42, width - 96, 24), m_font)
            .SetFontSize(14)
            .SetColor(new Color(0.75f, 0.8f, 0.88f, 1f))
            .SetMask(bounds)
            .Draw("Rendered by a UGUI RectTransform component");
    }
}
