using NowUI;
using UnityEngine;

public sealed class NowUIRenderTextureGlassSample : MonoBehaviour
{
    [SerializeField] RenderTexture target;

    readonly NowRenderer _renderer = new NowRenderer();
    readonly NowGlassDiagnosticEntry[] _glassDiagnostics = new NowGlassDiagnosticEntry[8];
    int _copiedDiagnosticCount;
    bool _warmed;

    void OnEnable()
    {
        NowGlassSettings.diagnosticsEnabled = true;
        NowGlassSettings.ReserveDiagnostics(_glassDiagnostics.Length);
    }

    void OnDisable()
    {
        NowGlassSettings.diagnosticsEnabled = false;
    }

    void OnDestroy()
    {
        _renderer.Dispose();
    }

    void Update()
    {
        if (target == null)
            return;

        if (!_warmed)
        {
            _renderer.Warmup(target, DrawUI);
            _warmed = true;
        }

        using (_renderer.Begin(target))
            DrawUI();

        _renderer.Render(target, clear: true, clearColor: Color.clear);

        _copiedDiagnosticCount = NowGlassSettings.CopyLastFrameDiagnosticsTo(_glassDiagnostics);
    }

    void DrawUI()
    {
        Now.Rectangle(new NowRect(0, 0, 320, 180))
            .SetColor(new Color(0.12f, 0.15f, 0.18f, 1f))
            .Draw();

        Now.Rectangle(new NowRect(24, 24, 120, 120))
            .SetColor(new Color(0.95f, 0.2f, 0.16f, 1f))
            .SetRadius(18f)
            .Draw();

        Now.Rectangle(new NowRect(104, 36, 160, 96))
            .SetColor(new Color(0.12f, 0.45f, 1f, 1f))
            .SetRadius(14f)
            .Draw();

        Now.Glass(new NowRect(64, 48, 192, 84))
            .SetRadius(20f)
            .SetBlurRadius(18f)
            .SetTint(new Color(1f, 1f, 1f, 0.2f))
            .SetOutline(1f)
            .SetOutlineColor(new Color(1f, 1f, 1f, 0.35f))
            .Draw();
    }
}
