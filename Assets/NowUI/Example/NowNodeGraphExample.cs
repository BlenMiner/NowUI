using UnityEngine;
using NowUI;
using NowUI.NodeGraph;

[AddComponentMenu("NowUI/Examples/Now Node Graph Example")]
public sealed class NowNodeGraphExample : MonoBehaviour
{
    const int Float = 1;
    const int Float4 = 4;
    const int TextureSample = 100;
    const int TintColor = 110;
    const int GradientRamp = 120;
    const int CurveRemap = 130;
    const int Multiply = 200;
    const int FragmentOutput = 300;
    const int PortRgba = 10;
    const int PortAlpha = 11;
    const int PortColor = 20;
    const int PortA = 30;
    const int PortB = 31;
    const int PortOut = 32;
    const int PortBase = 40;
    const int PortGradientT = 50;
    const int PortGradientColor = 51;
    const int PortCurveT = 60;
    const int PortCurveValue = 61;

    [SerializeField] NowFontAsset _font;

    readonly NowNodeGraph _graph = new NowNodeGraph();
    readonly NowNodeGraphHistory _history = new NowNodeGraphHistory();
    readonly NowNodeGraphContextMenu _menu = new NowNodeGraphContextMenu();
    NowNodeGraphSchema _schema;
    Texture2D _previewTexture;
    Color _tint = new Color(0.36f, 0.72f, 1f, 1f);
    Gradient _gradient;
    AnimationCurve _curve;
    bool _initialized;

    void Awake()
    {
        BuildGraph();
    }

    void OnPostRender()
    {
        if (!_initialized)
            BuildGraph();

        using (Now.StartUI())
        {
            if (_font != null)
            {
                using (Now.Font(_font))
                    DrawGraphUI();
            }
            else
            {
                DrawGraphUI();
            }
        }
    }

    void OnDisable()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowControlState.Reset();
    }

    void DrawGraphUI()
    {
        var rect = new NowRect(24f, 24f, Screen.width - 48f, Screen.height - 48f);
        NowNodes.Canvas(_graph, rect, 8101)
            .SetSchema(_schema)
            .SetHistory(_history)
            .SetContextMenu(_menu)
            .Draw();
    }

    void OnDestroy()
    {
        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
            _previewTexture = null;
        }
    }

    void BuildGraph()
    {
        if (_initialized)
            return;

        _initialized = true;
        EnsurePreviewTexture();
        EnsureValueFields();
        BuildSchema();
        _graph.nodes.Clear();
        _graph.links.Clear();
        _graph.SetSchema(_schema);
        _history.Clear();

        _graph.AddNode(_schema, TextureSample, new Vector2(64f, 80f), id: "texture");
        _graph.AddNode(_schema, TintColor, new Vector2(64f, 270f), id: "tint");
        _graph.AddNode(_schema, GradientRamp, new Vector2(64f, 400f), id: "gradient");
        _graph.AddNode(_schema, Multiply, new Vector2(340f, 156f), id: "multiply");
        _graph.AddNode(_schema, CurveRemap, new Vector2(340f, 330f), id: "curve");
        _graph.AddNode(_schema, FragmentOutput, new Vector2(620f, 176f), id: "output");

        _graph.TryAddLink("texture", PortRgba, "multiply", PortA);
        _graph.TryAddLink("tint", PortColor, "multiply", PortB);
        _graph.TryAddLink("multiply", PortOut, "output", PortBase);
    }

    void BuildSchema()
    {
        if (_schema != null)
            return;

        _schema = new NowNodeGraphSchema();

        _schema.Node(TextureSample, "Texture Sample")
            .SetSize(210f, 152f)
            .Output(PortRgba, "RGBA", Float4)
            .Output(PortAlpha, "Alpha", Float)
            .Render(ctx =>
            {
                var preview = new NowRect(ctx.bodyRect.x, ctx.bodyRect.y + ctx.Scale(4f), ctx.bodyRect.width, ctx.Scale(54f));
                ctx.Texture(_previewTexture, preview, 4f);
                DrawNodeCaption(preview, "Checker Preview");
            });

        _schema.Node(TintColor, "Tint Color")
            .SetSize(180f, 112f)
            .Output(PortColor, "Color", Float4)
            .Render(ctx =>
            {
                var picker = new NowRect(ctx.bodyRect.x, ctx.bodyRect.y + ctx.Scale(8f), ctx.bodyRect.width, ctx.Scale(30f));

                using (NowControls.IdScope(ctx.node.id))
                {
                    if (Now.ColorPicker(picker).SetShowAlpha(false).Draw(ref _tint))
                        ctx.MarkChanged();
                }
            });

        _schema.Node(GradientRamp, "Gradient Ramp")
            .SetSize(320f, 124f)
            .Input(PortGradientT, "T", Float)
            .Output(PortGradientColor, "Color", Float4)
            .Render(ctx =>
            {
                EnsureValueFields();
                var field = new NowRect(ctx.bodyRect.x, ctx.bodyRect.y + ctx.Scale(8f), ctx.bodyRect.width, ctx.Scale(30f));

                using (NowControls.IdScope(ctx.node.id))
                {
                    if (Now.GradientField(field).SetPopupWidth(300f).Draw(ref _gradient))
                        ctx.MarkChanged();
                }
            });

        _schema.Node(Multiply, "Multiply")
            .SetSize(176f, 118f)
            .Input(PortA, "A", Float4)
            .Input(PortB, "B", Float4)
            .Output(PortOut, "Result", Float4);

        _schema.Node(CurveRemap, "Animation Curve")
            .SetSize(320f, 124f)
            .Input(PortCurveT, "T", Float)
            .Output(PortCurveValue, "Value", Float)
            .Render(ctx =>
            {
                EnsureValueFields();
                var field = new NowRect(ctx.bodyRect.x, ctx.bodyRect.y + ctx.Scale(8f), ctx.bodyRect.width, ctx.Scale(34f));

                using (NowControls.IdScope(ctx.node.id))
                {
                    if (Now.AnimationCurveField(field)
                            .SetTimeRange(0f, 1f)
                            .SetValueRange(0f, 1f)
                            .SetPopupSize(320f, 220f)
                            .Draw(ref _curve))
                    {
                        ctx.MarkChanged();
                    }
                }
            });

        _schema.Node(FragmentOutput, "Fragment Output")
            .SetSize(190f, 94f)
            .Input(PortBase, "Base Color", Float4);
    }

    void EnsureValueFields()
    {
        if (_gradient == null)
        {
            _gradient = new Gradient();
            _gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.09f, 0.25f, 0.72f, 1f), 0f),
                    new GradientColorKey(new Color(0.36f, 0.72f, 1f, 1f), 0.55f),
                    new GradientColorKey(new Color(1f, 0.56f, 0.18f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
        }

        if (_curve == null || _curve.length == 0)
            _curve = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);
    }

    void EnsurePreviewTexture()
    {
        if (_previewTexture != null)
            return;

        _previewTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        _previewTexture.SetPixels(new[]
        {
            new Color(0.18f, 0.20f, 0.24f, 1f),
            new Color(0.92f, 0.94f, 0.98f, 1f),
            new Color(0.92f, 0.94f, 0.98f, 1f),
            new Color(0.18f, 0.20f, 0.24f, 1f)
        });
        _previewTexture.Apply();
    }
    static void DrawNodeCaption(NowRect rect, string text)

    {
        Now.Text(rect.Inset(8f, 6f))
            .SetFontSize(11f)
            .SetColor(Color.white)
            .Draw(text);
    }
}
