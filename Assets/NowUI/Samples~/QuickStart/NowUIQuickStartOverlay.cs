using NowUI;
using UnityEngine;

public sealed class NowUIQuickStartOverlay : MonoBehaviour
{
    void OnPostRender()
    {
        using (Now.StartUI(NowScreen.recommendedUIScale))
        {
            using (NowLayout.Area(NowScreen.safeArea, padding: 18f, spacing: 10f))
            using (NowLayout.Vertical(spacing: 8f))
            {
                NowLayout.Label("NowUI", 28f).Draw();

                var buttonRect = NowLayout.Rect(width: 180f, height: 44f);
                bool clicked = Now.Button(buttonRect, "Sample Button")
                    .SetId(1001)
                    .Draw();

                NowLayout.Label(clicked ? "Clicked" : "Ready", 16f).Draw();
            }
        }
    }
}
