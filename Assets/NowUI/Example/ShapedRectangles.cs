using UnityEngine;

public class ShapedRectangles : MonoBehaviour
{
    [SerializeField] Color outline = Color.red;

    [SerializeField, Range(-200, 200)] float _radius;

    [SerializeField, Range(0, 200)] float _outline;

    [SerializeField, Range(-200, 200)] float _blur;

    [SerializeField, Range(-200, 200)] float _padding;

    private void OnPostRender()
    {
        int count = Mathf.Max(1, Mathf.RoundToInt((Mathf.Sin(Time.time * 0.5f) + 1) * 100));
        NowUI.StartUI();

        float sizeX = (float)Screen.width / count;
        float sizeY = (float)Screen.height / count;

        var style = NowUI.Rectangle((Vector4)default)
            .SetOutlineColor(outline)
            .SetBlur(_blur)
            .SetRadius(_radius)
            .SetPadding(_padding)
            .SetOutline(_outline);

        for (int x = 0; x < count; ++x)
        {
            for (int y = 0; y < count; ++y)
            {
                var rect = new Vector4(sizeX * x, sizeY * y, sizeX, sizeY);
                if ((x + y) % 2 == 0)
                {
                    style.SetPosition(rect)
                        .Draw();
                }
            }
        }

        NowUI.FlushUI();
    }
}
