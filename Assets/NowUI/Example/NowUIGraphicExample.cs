using UnityEngine;
using NowUI;

[AddComponentMenu("NowUI/Examples/NowUI Graphic Example")]
public class NowUIGraphicExample : NowUIGraphic
{
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset _lottie;
    [SerializeField] float _size = 14f;
    [SerializeField] string _content;

    protected override void DrawNowUI(NowRect rect)
    {
        float width = rect.width;
        float height = rect.height;
        var bounds = new NowRect(0, 0, width, height);

        Now.Rectangle(bounds)
            .SetColor(new Color(0.08f, 0.1f, 0.14f, 0.92f))
            .SetRadius(12)
            .SetMask(bounds)
            .Draw();

        Now.Rectangle(new Vector4(16, 16, 42, 42))
            .SetColor(new Color(0.1f, 0.45f, 0.95f, 1f))
            .SetRadius(10)
            .SetMask(bounds)
            .Draw();

        if (_font == null)
            return;

        Now.defaultFont = _font;

        using (NowLayout.Area(bounds.Inset(10)))
        {
            NowLayout.Label("NowUI Graphic").Draw();
            NowLayout.Label("Hello World\nNowUI Graphic").Draw();
            var content = NowLayout.Label(_content)
                .SetFontSize(_size)
                .SetOutlineColor(Color.green)
                .Reserve();

            Now.Rectangle(content.rect).SetColor(Color.black).Draw();
            content.Draw();

            using (NowLayout.Horizontal())
            {
                NowLayout.Label("N").Draw();
                NowLayout.Label("o").Draw();
                NowLayout.Label("w").Draw();
                NowLayout.FlexibleSpace();
                NowLayout.Label("U").Draw();
                NowLayout.FlexibleSpace();
                NowLayout.Label("I").Draw();
            }

            DrawLottie();
            using (NowLayout.Horizontal())
            {
                DrawLottie();
                DrawLottie();
                DrawLottie();
                DrawLottie();
            }
            DrawLottie();
        }
    }

    private void DrawLottie()
    {
        var reservedRect = NowLayout.Rect(128, 128);

        Now.Lottie(reservedRect, _lottie)
            .SetTime(Time.time)
            .Draw();
    }
}
