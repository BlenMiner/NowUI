using NowUI;
using UnityEngine;

[AddComponentMenu("NowUI/Examples/Now Model Preview Example")]
public sealed class NowModelPreviewExample : NowLayoutGraphic
{
    [SerializeField] GameObject _model;

    [SerializeField] bool _rotate = true;

    [SerializeField] float _degreesPerSecond = 24f;

    NowModelPreview _preview;

    protected override void OnEnable()
    {
        base.OnEnable();
        CreatePreview();
    }

    protected override void OnDisable()
    {
        _preview?.Dispose();
        _preview = null;
        base.OnDisable();
    }

    void Update()
    {
        if ((_preview == null && _model != null) ||
            (_preview != null && _preview.source != _model))
            CreatePreview();

        if (_preview != null && _rotate)
        {
            _preview.SetRotation(Quaternion.Euler(
                0f,
                Time.time * _degreesPerSecond,
                0f));
        }
    }

    void CreatePreview()
    {
        _preview?.Dispose();
        _preview = _model != null
            ? new NowModelPreview(_model)
                .SetMaxResolution(768)
                .SetUpdateMode(NowModelPreviewUpdateMode.WhenDirty)
            : null;
        MarkDirty();
    }

    protected override void DrawNowUI(NowRect rect)
    {
        Now.Rectangle(rect)
            .SetColor(new Color(0.055f, 0.07f, 0.1f, 1f))
            .SetRadius(20f)
            .Draw();

        if (_preview == null || _model == null)
        {
            Now.Text(rect.Inset(24f))
                .SetFontSize(16f)
                .SetColor(new Color(0.7f, 0.74f, 0.82f, 1f))
                .Draw("Assign a model or prefab");
            return;
        }

        Now.Model(rect.Inset(12f), _preview)
            .SetRadius(14f)
            .SetOutline(1f, new Color(1f, 1f, 1f, 0.16f))
            .Draw();
    }
}
