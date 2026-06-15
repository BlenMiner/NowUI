using UnityEngine;
using NowUI;

[AddComponentMenu("NowUI/Examples/Now World Graphic Example")]
public sealed class NowWorldGraphicExample : NowWorldGraphic
{
    [SerializeField] string _title = "Player";
    [SerializeField] string _detail = "Hover for details";

    protected override void DrawNowUI(NowRect rect)
    {
        Now.Rectangle(rect)
            .SetColor(Color.black, 0.5f)
            .SetRadius(10f)
            .Draw();

        using (NowLayout.Area(rect, padding: 10, spacing: 6))
        {
            NowLayout.Label(_title).Draw();
            if (NowLayout.Button(_detail).Draw())
            {
                Debug.Log("Clicked", this);
            }
        }
    }
}
