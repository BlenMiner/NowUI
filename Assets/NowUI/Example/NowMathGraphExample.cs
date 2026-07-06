using System;
using UnityEngine;
using NowUI;
using NowUI.NodeGraph;

[AddComponentMenu("NowUI/Examples/Now Math Graph Example")]
public sealed class NowMathGraphExample : MonoBehaviour
{
    const int Scalar = 1;
    const int Integer = 2;

    const int VariableX = 10;
    const int Constant = 20;
    const int Add = 30;
    const int Multiply = 40;
    const int Sine = 50;
    const int Lerp = 60;
    const int Plot = 70;
    const int Round = 80;
    const int RerouteKind = 90;

    const int PortOut = 1;
    const int PortA = 2;
    const int PortB = 3;
    const int PortT = 4;
    const int PortIn = 5;

    const float DomainMin = -3f;
    const float DomainMax = 3f;
    const float RangeMin = -2f;
    const float RangeMax = 2f;
    const int PlotSamples = 64;
    const int PlotPointEvery = 8;
    const int SparklineSamples = 32;
    const float PreviewHeight = 64f;

    static readonly Color ScalarColor = new Color(0.20f, 0.55f, 0.95f, 1f);
    static readonly Color IntegerColor = new Color(0.93f, 0.50f, 0.16f, 1f);

    [SerializeField] NowFontAsset _font;

    readonly NowNodeGraph _graph = new NowNodeGraph();
    readonly NowNodeGraphHistory _history = new NowNodeGraphHistory();
    readonly NowNodeGraphContextMenu _menu = new NowNodeGraphContextMenu();
    NowNodeGraphSchema _schema;
    NowNodeGraphEvaluator<float> _evaluator;
    float _x;
    float _previewX = 0.5f;
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
        NowNodes.Canvas(_graph, rect, 8201)
            .SetSchema(_schema)
            .SetHistory(_history)
            .SetContextMenu(_menu)
            .Draw();
    }

    void BuildGraph()
    {
        if (_initialized)
            return;

        _initialized = true;
        BuildSchema();
        BuildEvaluator();
        _graph.nodes.Clear();
        _graph.links.Clear();
        _graph.SetSchema(_schema);
        _history.Clear();

        _graph.AddNode(_schema, VariableX, new Vector2(40f, 140f), id: "x");
        SetConstant(_graph.AddNode(_schema, Constant, new Vector2(40f, 340f), id: "freq"), 2f);
        _graph.AddNode(_schema, Multiply, new Vector2(280f, 240f), id: "scale-x");
        _graph.AddNode(_schema, Sine, new Vector2(500f, 140f), id: "sine");
        SetConstant(_graph.AddNode(_schema, Constant, new Vector2(500f, 360f), id: "amp"), 0.75f);
        _graph.AddNode(_schema, Multiply, new Vector2(720f, 250f), id: "scale-y");
        _graph.AddNode(_schema, Round, new Vector2(720f, 470f), id: "round");
        _graph.AddNode(_schema, Plot, new Vector2(940f, 120f), id: "plot");

        _graph.TryAddLink("x", PortOut, "scale-x", PortA);
        _graph.TryAddLink("freq", PortOut, "scale-x", PortB);
        _graph.TryAddLink("scale-x", PortOut, "sine", PortIn);
        _graph.TryAddLink("sine", PortOut, "scale-y", PortA);
        _graph.TryAddLink("amp", PortOut, "scale-y", PortB);
        _graph.TryAddLink("scale-y", PortOut, "plot", PortIn);
        _graph.TryAddLink("scale-y", PortOut, "round", PortIn);
    }

    void BuildSchema()
    {
        if (_schema != null)
            return;

        _schema = new NowNodeGraphSchema()
            .AllowSameTypes()
            .Allow(Scalar, Integer)
            .Allow(Integer, Scalar)
            .TypeColor(Scalar, ScalarColor)
            .TypeColor(Integer, IntegerColor);

        _schema.Node(VariableX, "X")
            .SetSize(184f, 86f)
            .SetColor(new Color(0.16f, 0.30f, 0.47f, 1f))
            .Output(PortOut, "x", Scalar)
            .Preview(DrawSparkline, PreviewHeight)
            .Render(ctx =>
            {
                Now.Text(ctx.Row(0, 18f))
                    .SetFontSize(12f)
                    .SetColor(ctx.style.textMuted)
                    .Draw("x in [-3, 3]");
            });

        _schema.Node(Constant, "Constant")
            .SetSize(210f, 110f)
            .Output(PortOut, "Value", Scalar)
            .Preview(DrawSparkline, PreviewHeight)
            .Render(ctx =>
            {
                float value = GetConstant(ctx.node);
                var slider = ctx.Row(0, 22f);

                using (NowControls.IdScope(ctx.node.id))
                {
                    if (Now.Slider(slider, DomainMin, DomainMax).Draw(ref value))
                    {
                        SetConstant(ctx.node, value);
                        ctx.MarkChanged();
                    }
                }

                Now.Text(ctx.Row(1, 18f))
                    .SetFontSize(12f)
                    .SetColor(ctx.style.textMuted)
                    .Draw(GetConstant(ctx.node), "0.00");
            });

        _schema.Node(Add, "Add")
            .SetSize(160f, 100f)
            .Input(PortA, "A", Scalar)
            .Input(PortB, "B", Scalar)
            .Output(PortOut, "Sum", Scalar)
            .Preview(DrawSparkline, PreviewHeight);

        _schema.Node(Multiply, "Multiply")
            .SetSize(160f, 100f)
            .Input(PortA, "A", Scalar)
            .Input(PortB, "B", Scalar)
            .Output(PortOut, "Product", Scalar)
            .Preview(DrawSparkline, PreviewHeight);

        _schema.Node(Sine, "Sine")
            .SetSize(160f, 86f)
            .Input(PortIn, "In", Scalar)
            .Output(PortOut, "sin(in)", Scalar)
            .Preview(DrawSparkline, PreviewHeight);

        _schema.Node(Lerp, "Lerp")
            .SetSize(170f, 124f)
            .Input(PortA, "A", Scalar)
            .Input(PortB, "B", Scalar)
            .Input(PortT, "T", Scalar)
            .Output(PortOut, "Mix", Scalar)
            .Preview(DrawSparkline, PreviewHeight);

        _schema.Node(Round, "Round")
            .SetSize(170f, 86f)
            .SetColor(new Color(0.48f, 0.31f, 0.14f, 1f))
            .Input(PortIn, "In", Scalar)
            .Output(PortOut, "int", Integer)
            .Preview(DrawSparkline, PreviewHeight);

        _schema.Node(Plot, "Plot f(x)")
            .SetSize(330f, 270f)
            .SetColor(new Color(0.34f, 0.23f, 0.5f, 1f))
            .Input(PortIn, "f(x)", Scalar)
            .Render(DrawPlotNode);

        _schema.Reroute(RerouteKind);
    }

    void BuildEvaluator()
    {
        if (_evaluator != null)
            return;

        _evaluator = new NowNodeGraphEvaluator<float>()
            .Kind(VariableX, _ => _x)
            .Kind(Constant, ctx => GetConstant(ctx.node))
            .Kind(Add, ctx => ctx.Input(PortA) + ctx.Input(PortB))
            .Kind(Multiply, ctx => ctx.Input(PortA, 1f) * ctx.Input(PortB, 1f))
            .Kind(Sine, ctx => Mathf.Sin(ctx.Input(PortIn)))
            .Kind(Lerp, ctx => Mathf.Lerp(ctx.Input(PortA), ctx.Input(PortB), Mathf.Clamp01(ctx.Input(PortT))))
            .Kind(Round, ctx => Mathf.Round(ctx.Input(PortIn)))
            .Kind(RerouteKind, ctx => ctx.Input(NowNodeGraphSchema.RerouteInputPortId))
            .Kind(Plot, ctx => ctx.Input(PortIn));
    }

    static Color PreviewFill(in NowNodeGraphStyle style)
    {
        return Color.Lerp(style.node, style.background, 0.35f);
    }

    static Color PreviewBorder(in NowNodeGraphStyle style)
    {
        Color border = style.border;
        border.a *= 0.55f;
        return border;
    }

    void DrawSparkline(NowNodeContentContext ctx)
    {
        var rect = ctx.bodyRect;

        Now.Rectangle(rect)
            .SetColor(PreviewFill(ctx.style))
            .SetRadius(4f)
            .SetOutline(1f)
            .SetOutlineColor(PreviewBorder(ctx.style))
            .Draw();

        if (ctx.node.outputs == null || ctx.node.outputs.Count == 0)
            return;

        Color curveColor = ctx.style.connection;

        if (_schema.TryGetTypeColor(ctx.node.outputs[0].typeId, out var typeColor) && typeColor.a > 0f)
            curveColor = typeColor;

        float saved = _x;
        Vector2 previous = default;

        for (int i = 0; i <= SparklineSamples; ++i)
        {
            float t = i / (float)SparklineSamples;
            _x = Mathf.Lerp(DomainMin, DomainMax, t);
            float value = _evaluator.Evaluate(ctx.graph, ctx.node.id);
            var point = PlotPoint(rect, t, value);

            if (i > 0)
            {
                Now.Line(previous, point)
                    .SetColor(curveColor)
                    .SetWidth(1.5f)
                    .SetCap(NowLineCap.Round)
                    .SetMask(rect)
                    .Draw();
            }

            previous = point;
        }

        _x = saved;
    }

    void DrawPlotNode(NowNodeContentContext ctx)
    {
        var body = ctx.bodyRect;
        var plot = new NowRect(body.x, body.y, body.width, Mathf.Max(24f, body.height - 52f));

        Now.Rectangle(plot)
            .SetColor(PreviewFill(ctx.style))
            .SetRadius(4f)
            .SetOutline(1f)
            .SetOutlineColor(PreviewBorder(ctx.style))
            .Draw();

        DrawPlotAxes(plot, ctx.style);
        DrawPlotCurve(plot, ctx);

        float saved = _x;
        _x = _previewX;
        float previewValue = _evaluator.Evaluate(ctx.graph, ctx.node.id);
        _x = saved;

        DrawPreviewMarker(plot, previewValue, ctx.style);

        var slider = new NowRect(body.x, plot.yMax + 8f, body.width, 18f);

        using (NowControls.IdScope(ctx.node.id))
        {
            if (Now.Slider(slider, DomainMin, DomainMax).Draw(ref _previewX))
                ctx.MarkChanged();
        }

        Span<char> labelChars = stackalloc char[48];
        var label = new NowTextBuffer(labelChars);
        label.Append("f(");
        label.Append(_previewX, "0.00");
        label.Append(") = ");
        label.Append(previewValue, "0.00");

        Now.Text(new NowRect(body.x, slider.yMax + 4f, body.width, 16f))
            .SetFontSize(12f)
            .SetColor(ctx.style.text)
            .Draw(label.span);
    }

    void DrawPlotCurve(NowRect plot, NowNodeContentContext ctx)
    {
        float saved = _x;
        Vector2 previous = default;

        for (int i = 0; i <= PlotSamples; ++i)
        {
            float t = i / (float)PlotSamples;
            _x = Mathf.Lerp(DomainMin, DomainMax, t);
            float value = _evaluator.Evaluate(ctx.graph, ctx.node.id);
            var point = PlotPoint(plot, t, value);

            if (i > 0)
            {
                Now.Line(previous, point)
                    .SetColor(ctx.style.connection)
                    .SetWidth(2f)
                    .SetCap(NowLineCap.Round)
                    .SetMask(plot)
                    .Draw();
            }

            if (i % PlotPointEvery == 0)
            {
                Now.Circle(point, 2.5f)
                    .SetColor(ctx.style.outputPort)
                    .SetMask(plot)
                    .Draw();
            }

            previous = point;
        }

        _x = saved;
    }

    void DrawPreviewMarker(NowRect plot, float previewValue, NowNodeGraphStyle style)
    {
        float t = Mathf.InverseLerp(DomainMin, DomainMax, _previewX);
        var point = PlotPoint(plot, t, previewValue);

        Now.Line(new Vector2(point.x, plot.y), new Vector2(point.x, plot.yMax))
            .SetColor(style.textMuted)
            .SetWidth(1f)
            .SetDash(4f, 4f)
            .SetMask(plot)
            .Draw();

        Now.Circle(point, 4f)
            .SetColor(style.selectedBorder)
            .SetMask(plot)
            .Draw();
    }

    static void DrawPlotAxes(NowRect plot, NowNodeGraphStyle style)
    {
        float zeroX = Mathf.Lerp(plot.x, plot.xMax, Mathf.InverseLerp(DomainMin, DomainMax, 0f));
        float zeroY = Mathf.Lerp(plot.y, plot.yMax, Mathf.InverseLerp(RangeMax, RangeMin, 0f));
        Color axis = style.textMuted;
        axis.a *= 0.4f;

        Now.Line(new Vector2(zeroX, plot.y), new Vector2(zeroX, plot.yMax))
            .SetColor(axis)
            .SetWidth(1f)
            .SetMask(plot)
            .Draw();

        Now.Line(new Vector2(plot.x, zeroY), new Vector2(plot.xMax, zeroY))
            .SetColor(axis)
            .SetWidth(1f)
            .SetMask(plot)
            .Draw();
    }

    static Vector2 PlotPoint(NowRect plot, float t, float value)
    {
        float x = Mathf.Lerp(plot.x, plot.xMax, t);
        float normalized = (RangeMax - value) / (RangeMax - RangeMin);
        float y = plot.y + normalized * plot.height;
        return new Vector2(x, y);
    }

    static float GetConstant(NowNode node)
    {
        return node.userId / 1000f;
    }

    static void SetConstant(NowNode node, float value)
    {
        node.userId = Mathf.RoundToInt(value * 1000f);
    }

}
