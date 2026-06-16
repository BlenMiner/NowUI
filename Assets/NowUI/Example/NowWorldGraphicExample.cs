using UnityEngine;
using NowUI;

[AddComponentMenu("NowUI/Examples/Now World Graphic Example")]
public sealed class NowWorldGraphicExample : NowWorldGraphic
{
    [SerializeField] string _title = "Player";
    [SerializeField] string _detail = "Hover for details";
    [SerializeField] NowLottieAsset _lottie;

    protected override void DrawNowUI(NowRect rect)
    {
        Now.Glass(rect)
            .SetTint(new Color(0, 0, 0, 0.5f))
            .SetRadius(10f)
            .SetBlurRadius(44f)
            .Draw();

        using (NowLayout.Area(rect, padding: 10, spacing: 6))
        {
            NowLayout.Label(_title).Draw();
            NowLayout.Lottie(_lottie)
                .SetWidth(32)
                .SetTime(Time.time)
                .Draw();
            if (NowLayout.Button(_detail).Draw())
            {
                Debug.Log("Clicked", this);
            }
        }
    }
}
