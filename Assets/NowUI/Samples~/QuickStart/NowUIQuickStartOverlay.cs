using NowUI;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowUIQuickStartOverlay : MonoBehaviour
{
    void OnEnable()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            Debug.LogWarning("NowUIQuickStartOverlay draws from OnPostRender, which only runs on the Built-in Render Pipeline. On URP/HDRP use the NowUniversalRendererFeature / HDRP custom pass with a NowPipelineGraphic, or a NowGraphic under a Canvas — see Docs/RenderPipelines.md.", this);

        if (!TryGetComponent<Camera>(out var cam) || !cam.enabled)
            Debug.LogWarning("NowUIQuickStartOverlay must live on an enabled Camera for OnPostRender to fire.", this);
    }

    void OnPostRender()
    {
        using (Now.StartUI(NowScreen.recommendedUIScale))
        {
            using (NowLayout.Area(NowScreen.safeArea, padding: 18f, spacing: 10f))
            using (NowLayout.Vertical(spacing: 8f))
            {
                NowLayout.Label("NowUI", 28f).Draw();

                var buttonRect = NowLayout.Rect(width: 180f, height: 44f);
                bool clicked = Now.Button(buttonRect, "Sample Button").Draw();

                NowLayout.Label(clicked ? "Clicked" : "Ready", 16f).Draw();
            }
        }
    }
}
