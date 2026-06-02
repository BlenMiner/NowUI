using UnityEngine;

public class TextTests : MonoBehaviour
{
    [SerializeField] NowFont _font;

    [SerializeField] Color _color;

    [SerializeField] Color _outlineColor;

    [SerializeField, Range(-100, 100)] float _outline = 0f;

    [SerializeField, Range(1, 200)] float _fontSize = 100;

    [SerializeField, Range(-200, 200)] float _padding;

    [SerializeField, TextArea] string _text = "Hello World";

    private void DrawTextBox(Vector4 rect, string txt)
    {
        float radius = Mathf.PerlinNoise(rect.x * 1000, Time.time) * 35;

        var textContainer = NowUI.Rectangle(rect)
            .SetColor(Color.black);

        NowUI.Rectangle(textContainer)
            .SetColor(Color.HSVToRGB(Mathf.PerlinNoise(rect.x * 100, Time.time), 1, 1))
            .SetPadding(2)
            .Draw();

        var textContainerBorder = NowUI.Rectangle(textContainer)
            .SetColor(Color.HSVToRGB(Mathf.PerlinNoise(Time.time, rect.y * 100), 1, 1))
            .SetBlur(40)
            .SetPadding(20)
            .SetRadius(20)
        ;

        textContainerBorder.Draw();
        textContainer.Draw();

        NowUI.Text(rect, _font)
            .SetColor(_color)
            .SetFontSize(_fontSize + radius)
            .SetPadding(_padding)
            .SetOutline(_outline)
            .SetOutlineColor(_outlineColor)
            .Draw(rect.ToString() + "\n" + txt);
    }

    private void OnPostRender()
    {
        NowUI.StartUI();

        Vector4 pos = new Vector4(0, 0, 200, 200);

        for (int x = 0; x < 10; ++x)
            for (int y = 0; y < 5; ++y)
            {
                pos.x = 10 + (pos.z + 10) * x;
                pos.y = 10 + (pos.w + 10) * y;
                DrawTextBox(pos, _text);
            }

        NowUI.FlushUI();
    }
}
