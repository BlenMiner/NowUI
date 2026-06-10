using UnityEngine;

[AddComponentMenu("NowUI/Examples/NowUI Graphic Example")]
public class NowUIGraphicExample : NowUIGraphic
{
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset _lottie;
    [SerializeField] float _size = 14f;
    [SerializeField] string _content;

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

        NowUI.defaultFont = _font;

        using (NowUILayout.Area(bounds))
        {
            NowUILayout.Label("NowUI Graphic").Draw();
            NowUILayout.Label("Hello World\nNowUI Graphic").Draw();
            var content = NowUILayout.Label(_content)
                .SetFontSize(_size)
                .SetOutlineColor(Color.green)
                .Reserve();

            NowUI.Rectangle(content.rect).SetColor(Color.black).Draw();
            content.Draw();

            using (NowUILayout.Horizontal())
            {
                NowUILayout.Label("N").Draw();
                NowUILayout.Label("o").Draw();
                NowUILayout.Label("w").Draw();
                NowUILayout.FlexibleSpace();
                NowUILayout.Label("U").Draw();
                NowUILayout.FlexibleSpace();
                NowUILayout.Label("I").Draw();
            }

            DrawLottie();
            using (NowUILayout.Horizontal())
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
        var reservedRect = NowUILayout.Rect(128, 128);

        NowUI.Lottie(reservedRect, _lottie)
            .SetTime(Time.time)
            .Draw();
    }
}
