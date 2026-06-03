using UnityEngine;

[AddComponentMenu("NowUI/Examples/NowUI Render Texture Example")]
public sealed class NowUIRenderTextureExample : MonoBehaviour
{
    static readonly int _mainTexId = Shader.PropertyToID("_MainTex");

    static readonly int _baseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] NowFont _font;

    [SerializeField] RenderTexture _target;

    [SerializeField] Renderer _targetRenderer;

    [SerializeField] Vector2Int _fallbackSize = new Vector2Int(512, 288);

    [SerializeField, Range(0.75f, 1.5f)] float _scale = 1f;

    NowUIRenderer _renderer;

    RenderTexture _ownedTarget;

    MaterialPropertyBlock _propertyBlock;

    static Color Rgb(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    static Vector4 Rect(float x, float y, float width, float height)
    {
        return new Vector4(x, y, width, height);
    }

    void OnEnable()
    {
        _renderer = new NowUIRenderer();
    }

    void Update()
    {
        if (_renderer == null)
            _renderer = new NowUIRenderer();

        RenderTexture target = GetTarget();

        if (target == null)
            return;

        ApplyTargetTexture(target);
        using (_renderer.Begin(target))
            DrawNowUI(new Rect(0, 0, target.width, target.height));

        _renderer.Render(target, clear: true, clearColor: Color.clear);
    }

    void OnDisable()
    {
        if (_renderer != null)
        {
            _renderer.Dispose();
            _renderer = null;
        }

        ReleaseOwnedTarget();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        _fallbackSize.x = Mathf.Max(1, _fallbackSize.x);
        _fallbackSize.y = Mathf.Max(1, _fallbackSize.y);
    }
#endif

    RenderTexture GetTarget()
    {
        if (_target != null)
            return _target;

        if (_ownedTarget != null &&
            _ownedTarget.width == _fallbackSize.x &&
            _ownedTarget.height == _fallbackSize.y)
        {
            return _ownedTarget;
        }

        ReleaseOwnedTarget();

        _ownedTarget = new RenderTexture(_fallbackSize.x, _fallbackSize.y, 0, RenderTextureFormat.ARGB32)
        {
            name = "NowUI Render Texture Example",
            hideFlags = HideFlags.HideAndDontSave
        };

        _ownedTarget.Create();
        return _ownedTarget;
    }

    void ReleaseOwnedTarget()
    {
        if (_ownedTarget == null)
            return;

        _ownedTarget.Release();

        if (Application.isPlaying)
            Destroy(_ownedTarget);
        else
            DestroyImmediate(_ownedTarget);

        _ownedTarget = null;
    }

    void ApplyTargetTexture(RenderTexture target)
    {
        if (_targetRenderer == null)
            return;

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        _targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetTexture(_mainTexId, target);
        _propertyBlock.SetTexture(_baseMapId, target);
        _targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    void DrawNowUI(Rect rect)
    {
        float width = rect.width;
        float height = rect.height;
        Vector4 bounds = Rect(0, 0, width, height);

        NowUI.Rectangle(bounds)
            .SetColor(Rgb(15, 23, 42))
            .Draw();

        NowUI.Rectangle(Rect(0, 0, width, 8))
            .SetColor(Rgb(56, 189, 248))
            .Draw();

        DrawPanel(Rect(28, 32, width - 56, height - 64));
        DrawBadge(Rect(width - 174, 52, 118, 30), "RenderTexture", Rgb(14, 165, 233));
    }

    void DrawPanel(Vector4 rect)
    {
        NowUI.Rectangle(rect)
            .SetColor(Rgb(30, 41, 59))
            .SetRadius(18)
            .Draw();

        NowUI.Rectangle(Rect(rect.x + 22, rect.y + 70, rect.z - 44, 1))
            .SetColor(Rgb(71, 85, 105))
            .Draw();

        DrawText("NowUI Renderer", Rect(rect.x + 24, rect.y + 22, rect.z - 48, 34), 26, Color.white);
        DrawText(
            "Immediate UI drawn into a pure texture, then applied to scene geometry.",
            Rect(rect.x + 24, rect.y + 88, rect.z - 48, 28),
            15,
            Rgb(203, 213, 225));

        float cardWidth = Mathf.Max(90f, (rect.z - 64f) / 3f);
        DrawMetric(Rect(rect.x + 24, rect.y + rect.w - 82, cardWidth, 48), "Batches", "1+");
        DrawMetric(Rect(rect.x + 32 + cardWidth, rect.y + rect.w - 82, cardWidth, 48), "Target", "RT");
        DrawMetric(Rect(rect.x + 40 + cardWidth * 2f, rect.y + rect.w - 82, cardWidth, 48), "Pipeline", "Any");
    }

    void DrawBadge(Vector4 rect, string label, Color color)
    {
        NowUI.Rectangle(rect)
            .SetColor(color)
            .SetRadius(rect.w * 0.5f)
            .Draw();

        DrawText(label, Rect(rect.x + 14, rect.y + 5, rect.z - 28, rect.w - 10), 13, Color.white);
    }

    void DrawMetric(Vector4 rect, string label, string value)
    {
        NowUI.Rectangle(rect)
            .SetColor(Rgb(51, 65, 85))
            .SetRadius(10)
            .Draw();

        DrawText(label, Rect(rect.x + 12, rect.y + 7, rect.z - 24, 16), 11, Rgb(148, 163, 184));
        DrawText(value, Rect(rect.x + 12, rect.y + 22, rect.z - 24, 22), 18, Color.white);
    }

    void DrawText(string text, Vector4 rect, float size, Color color)
    {
        if (_font == null || string.IsNullOrEmpty(text))
            return;

        NowUI.Text(rect, _font)
            .SetFontSize(size * _scale)
            .SetColor(color)
            .SetMask(rect)
            .Draw(text);
    }
}
