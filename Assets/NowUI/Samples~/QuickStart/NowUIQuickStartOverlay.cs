using NowUI;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowUIQuickStartOverlay : MonoBehaviour
{
    void OnEnable()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            Debug.LogWarning("NowUIQuickStartOverlay draws from OnPostRender, which only runs on the Built-in Render Pipeline. On URP/HDRP use the NowUniversalRendererFeature / HDRP custom pass with a NowPipelineLayoutGraphic, or a NowLayoutGraphic under a Canvas — see the package's Documentation~/RenderPipelines.md guide.", this);

        if (!TryGetComponent<Camera>(out var cam) || !cam.enabled)
            Debug.LogWarning("NowUIQuickStartOverlay must live on an enabled Camera for OnPostRender to fire.", this);
    }

    void OnPostRender()
    {
        using (Now.StartUI(NowScreen.recommendedUIScale))
            NowLayout.RunMeasured(
                NowScreen.safeArea,
                this,
                static self => self.DrawOverlay(),
                spacing: 8f,
                padding: 18f);
    }

    void DrawOverlay()
    {
        NowLayout.Label("NowUI", 28f).Draw();

        var buttonRect = NowLayout.ReserveRect(width: 180f, height: 44f);
        bool clicked = Now.Button(buttonRect, "Sample Button").Draw();

        NowLayout.Label(clicked ? "Clicked" : "Ready", 16f).Draw();
    }
}
