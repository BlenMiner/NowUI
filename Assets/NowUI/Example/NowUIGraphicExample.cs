using System;
using UnityEngine;

[AddComponentMenu("NowUI/Examples/NowUI Graphic Example")]
public class NowUIGraphicExample : NowUIGraphic
{
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset _lottie;
    [SerializeField] float _size = 14f;
    [SerializeField] string _content;
    [SerializeField] int _count = 1;

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

        if (_font == null)
            return;

        NowUI.Text(new Vector4(72, 14, width - 96, 30), _font)
            .SetFontSize(20)
            .SetColor(Color.white)
            .SetMask(bounds)
            .Draw("NowUI Graphic");

        NowUI.Text(new Vector4(72, 42, width - 96, 24), _font)
            .SetFontSize(_size)
            .SetMask(bounds)
            .Draw(_content);


        NowUI.Text(new Vector4(72, 42 + 32, width - 96, 24), _font)
            .SetFontSize(_size)
            .SetMask(bounds)
            .SetBold()
            .Draw(_content);

        NowUI.Text(new Vector4(72, 42 + 32 + 32, width - 96, 24), _font)
            .SetFontSize(_size)
            .SetMask(bounds)
            .SetBold()
            .SetItalic()
            .Draw(_content);

        NowUI.Text(new Vector4(72, 42 + 32 + 32 + 32, width - 96, 24), _font)
            .SetFontSize(_size)
            .SetMask(bounds)
            .SetItalic()
            .Draw(_content);

        float cellSize = height / _count;

        for (int x = 0; x < _count; ++x)
        {
            for (int y = 0; y < _count; ++y)
            {
                var gridSegment = new Vector4(x * cellSize, y * cellSize, cellSize, cellSize);
                NowUI.Lottie(gridSegment, _lottie)
                    .SetNormalizedTime((Time.time + x * 0.1f + y * 0.1f) % 1f)
                    .Draw();
            }
        }
    }
}
