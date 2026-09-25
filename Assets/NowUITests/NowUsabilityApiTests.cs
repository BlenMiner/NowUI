using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using UnityEngine;

/// <summary>
/// APIs added after auditing the README demos: the genie collapse, automatic
/// deformer subdivision, color helpers, drop shadows, easing windows and
/// keyframes, labelled sliders, and wrapping layout rows.
/// </summary>
public class NowUsabilityApiTests
{
    static readonly Vector2 Surface = new Vector2(640f, 480f);
    static readonly NowRect Card = new NowRect(100f, 60f, 120f, 80f);

    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    readonly struct Identity : INowVertexDeformer
    {
        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context) => vertex.position;
    }

    NowDrawList _drawList;
    FakeProvider _provider;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        _drawList = new NowDrawList();
        _provider = new FakeProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowLayout.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowInput.Reset();
    }

    static List<Vector2> UiVertices(NowDrawList drawList)
    {
        var positions = new List<Vector3>();
        drawList.mesh.GetVertices(positions);
        var result = new List<Vector2>(positions.Count);
        foreach (var vertex in positions)
            result.Add(new Vector2(vertex.x, -vertex.y));
        return result;
    }

    static void DrawCard() => Now.Rectangle(Card).SetColor(Color.white).Draw();

    // ---------------------------------------------------------------- genie

    [Test]
    public void GenieCollapsesEveryVertexIntoTheTargetAtFullProgress()
    {
        var target = new NowRect(300f, 400f, 24f, 16f);

        foreach (NowEffectDirection direction in new[] { NowEffectDirection.Bottom, NowEffectDirection.Top, NowEffectDirection.Left, NowEffectDirection.Right })
        {
            using (_drawList.Begin(Surface))
            using (NowEffects.Modifier(NowDeformers.Genie(target, 1f, direction)).SetSourceRect(Card).Begin())
                DrawCard();

            foreach (var vertex in UiVertices(_drawList))
            {
                Assert.IsTrue(target.Outset(0.01f).Contains(vertex), $"{direction}: {vertex} must end inside {target}.");
            }
        }
    }

    [Test]
    public void GenieLeavesGeometryUntouchedAtZeroProgress()
    {
        var target = new NowRect(300f, 400f, 24f, 16f);

        using (_drawList.Begin(Surface))
            DrawCard();

        var reference = UiVertices(_drawList);

        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Genie(target, 0f)).SetSourceRect(Card).Begin())
            DrawCard();

        var actual = UiVertices(_drawList);
        Assert.AreEqual(reference.Count, actual.Count, "Nothing to subdivide before the genie starts.");

        for (int i = 0; i < reference.Count; ++i)
            Assert.AreEqual(0f, (reference[i] - actual[i]).magnitude, 0.001f);
    }

    // ---------------------------------------------------------------- subdivision

    [Test]
    public void BuiltInDeformersSubdivideAutomatically()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 6f, 24f)).Begin())
            DrawCard();

        int waveVertices = _drawList.mesh.vertexCount;
        Assert.Greater(waveVertices, 40, "A wave samples every wavelength without a SetSubdivision call.");

        // The wave offsets y by x only, so the grid runs along x and stays one cell tall.
        var xs = new HashSet<float>();
        foreach (var vertex in UiVertices(_drawList))
            xs.Add(Mathf.Round(vertex.x * 100f));

        Assert.Greater(xs.Count, 20);

        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Perspective(35f, 0f)).Begin())
            DrawCard();

        Assert.Greater(_drawList.mesh.vertexCount, 4, "Perspective subdivides so the quad does not fold.");
    }

    [Test]
    public void ExplicitNoneAndCustomDeformersStayUnsubdivided()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 6f, 24f)).SetSubdivision(NowSubdivision.None).Begin())
            DrawCard();

        Assert.AreEqual(4, _drawList.mesh.vertexCount);

        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(new Identity()).Begin())
            DrawCard();

        Assert.AreEqual(4, _drawList.mesh.vertexCount, "Custom deformers opt in with an explicit subdivision.");
    }

    [Test]
    public void MaxCellSizeCanSubdivideOneAxis()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(new Identity()).SetSubdivision(NowSubdivision.MaxCellSize(10f, float.PositiveInfinity)).Begin())
            DrawCard();

        var ys = new HashSet<float>();
        foreach (var vertex in UiVertices(_drawList))
            ys.Add(Mathf.Round(vertex.y * 100f));

        Assert.AreEqual(2, ys.Count, "Only the x axis is split.");
        Assert.Greater(_drawList.mesh.vertexCount, 20);
    }

    // ---------------------------------------------------------------- color

    [Test]
    public void ColorHelpers()
    {
        var accent = new Color(0.2f, 0.4f, 0.6f, 0.8f);

        Assert.AreEqual(new Color(0.2f, 0.4f, 0.6f, 0.25f), accent.WithAlpha(0.25f));
        Assert.AreEqual(0.4f, accent.MultiplyAlpha(0.5f).a, 1e-6f);
        Assert.AreEqual(new Color(1f, 1f, 1f, 0.8f), accent.Lighten(1f));
        Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 0.8f).ToString(), accent.Darken(0.5f).ToString());
        Assert.AreEqual(0.8f, accent.MixRgb(Color.red, 0.5f).a, 1e-6f);
        Assert.AreEqual(1f, Color.white.Luminance(), 1e-5f);
        Assert.AreEqual(0f, Color.black.Luminance(), 1e-5f);
    }

    // ---------------------------------------------------------------- shadow

    [Test]
    public void ShadowIsDrawnOffsetAndSpreadBehindTheRect()
    {
        using (_drawList.Begin(Surface))
            Now.Shadow(Card).SetRadius(12f).SetOffset(0f, 10f).SetSpread(4f).SetBlur(16f).SetColor(new Color(0f, 0f, 0f, 0.5f)).Draw();

        Assert.IsTrue(_drawList.hasGeometry);
        var vertices = UiVertices(_drawList);
        Vector2 center = default;
        foreach (var vertex in vertices)
            center += vertex;
        center /= vertices.Count;

        Assert.AreEqual(Card.center.x, center.x, 0.5f);
        Assert.AreEqual(Card.center.y + 10f, center.y, 0.5f, "The shadow moves by its offset.");
    }

    [Test]
    public void ElevationShadowUsesTheThemePreset()
    {
        using (_drawList.Begin(Surface))
            Now.Shadow(Card).SetRadius(12f).SetElevation(NowElevationToken.Overlay).Draw();

        Assert.IsTrue(_drawList.hasGeometry);

        using (_drawList.Begin(Surface))
            Now.Shadow(new NowRect(0f, 0f, 0f, 0f)).Draw();

        Assert.IsFalse(_drawList.hasGeometry, "An empty rect casts no shadow.");
    }

    // ---------------------------------------------------------------- easing

    [Test]
    public void WindowRisesHoldsAndFalls()
    {
        Assert.AreEqual(0f, NowEase.Window(-1f, 0f, 1f, 3f, 4f));
        Assert.AreEqual(0.5f, NowEase.Window(0.5f, 0f, 1f, 3f, 4f, NowEasing.Linear), 1e-5f);
        Assert.AreEqual(1f, NowEase.Window(2f, 0f, 1f, 3f, 4f));
        Assert.AreEqual(0.25f, NowEase.Window(3.75f, 0f, 1f, 3f, 4f, NowEasing.Linear), 1e-5f);
        Assert.AreEqual(0f, NowEase.Window(5f, 0f, 1f, 3f, 4f));
        Assert.AreEqual(0f, NowEase.Window(float.NaN, 0f, 1f, 3f, 4f));
        // A fall that starts before the rise ends starts from the end of the rise.
        Assert.AreEqual(1f, NowEase.Window(1f, 0f, 1f, 0.5f, 2f), 1e-5f);
        Assert.AreEqual(0.5f, NowEase.Smoothstep(0.5f), 1e-6f);
        Assert.AreEqual(NowEase.Smoothstep(0.3f), NowEase.Evaluate(NowEasing.Smoothstep, 0.3f));
    }

    static readonly NowKey<Vector2>[] Path =
    {
        NowKey.At(1f, new Vector2(0f, 0f)),
        NowKey.At(2f, new Vector2(10f, 0f)),
        NowKey.At(3f, new Vector2(10f, 0f)),
        NowKey.At(4f, new Vector2(10f, 20f), NowEasing.OutBack),
    };

    [Test]
    public void KeyframesInterpolateHoldAndClamp()
    {
        Assert.AreEqual(Vector2.zero, NowKeyframes.Evaluate(0f, Path), "Before the first key the first value holds.");
        Assert.AreEqual(new Vector2(5f, 0f), NowKeyframes.Evaluate(1.5f, Path));
        Assert.AreEqual(new Vector2(10f, 0f), NowKeyframes.Evaluate(2.5f, Path), "Repeating a value holds it.");
        Assert.Greater(NowKeyframes.Evaluate(3.8f, Path).y, 20f, "OutBack overshoots between keys.");
        Assert.AreEqual(new Vector2(10f, 20f), NowKeyframes.Evaluate(9f, Path));
        Assert.AreEqual(4f, NowKeyframes.Duration<Vector2>(Path));

        Assert.AreEqual(0f, NowKeyframes.Evaluate(1f, System.ReadOnlySpan<NowKey<float>>.Empty));
        Assert.AreEqual(3f, NowKeyframes.Evaluate(7f, new[] { NowKey.At(2f, 3f) }));
        Assert.AreEqual(0.5f, NowKeyframes.Evaluate(0.5f, new[] { NowKey.At(0f, Color.black), NowKey.At(1f, Color.white) }).r, 1e-5f);
        Assert.AreEqual(new Vector3(1f, 2f, 3f), NowKeyframes.Evaluate(1f, new[] { NowKey.At(0f, Vector3.zero), NowKey.At(1f, new Vector3(1f, 2f, 3f)) }));
    }

    // ---------------------------------------------------------------- slider

    [Test]
    public void LabelledSliderKeepsItsLabelAndReadoutOutOfTheTrack()
    {
        const string label = "Resolution";
        const string format = "0.00x";
        var rect = new NowRect(0f, 0f, 400f, 24f);
        var style = NowLayout.labelStyle;
        float labelWidth = style.Measure(label).x;
        float valueWidth = Mathf.Max(style.Measure(0.25, format).x, style.Measure(1.0, format).x);
        Assert.Greater(labelWidth, 0f, "The fixture needs a font to measure the label.");

        float trackStart = labelWidth + NowSlider.LabelGap;
        float trackEnd = rect.width - valueWidth - NowSlider.LabelGap;
        float value = 0.25f;

        // Pressing the label does not move the knob.
        _provider.snapshot = new NowInputSnapshot(new Vector2(labelWidth * 0.5f, 12f), true, true, false);
        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            Assert.IsFalse(Now.Slider(rect, 0.25f, 1f).SetId("labelled").SetLabel(label).SetValueFormat(format).Draw(ref value));

        NowControlState.Reset();
        NowInput.Reset();

        // Pressing the middle of the track lands near the middle of the range.
        _provider.snapshot = new NowInputSnapshot(new Vector2((trackStart + trackEnd) * 0.5f, 12f), true, true, false);
        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            Assert.IsTrue(Now.Slider(rect, 0.25f, 1f).SetId("labelled").SetLabel(label).SetValueFormat(format).Draw(ref value));

        Assert.AreEqual(0.625f, value, 0.05f);
    }

    [Test]
    public void LabelledSliderMeasuresItsLabelAndReadoutInLayoutFlow()
    {
        var style = NowLayout.labelStyle;
        float expected = style.Measure("Angle").x + NowSlider.LabelGap + 160f + NowSlider.LabelGap +
            Mathf.Max(style.Measure(0.0, "0").x, style.Measure(360.0, "0").x);
        float value = 90f;

        NowLayout.BeginContentTracking();

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (NowLayout.Row(new NowRect(0f, 0f, 640f, 40f)).SetId("slider-row").Begin())
            NowLayout.Slider(0f, 360f).SetLabel("Angle").SetValueFormat("0").Draw(ref value);

        Vector2 extent = NowLayout.EndContentTracking();
        Assert.AreEqual(expected, extent.x, 0.5f);
    }

    // ---------------------------------------------------------------- small builders

    static List<Vector4> VertexColors(NowDrawList drawList)
    {
        var colors = new List<Vector4>();
        drawList.mesh.GetUVs(3, colors);
        return colors;
    }

    [Test]
    public void HollowRectangleDrawsOnlyItsOutline()
    {
        using (_drawList.Begin(Surface))
            Now.Rectangle(Card).SetFill(false).SetColor(Color.red).SetOutline(2f, Color.white).Draw();

        Assert.IsTrue(_drawList.hasGeometry);
        foreach (var color in VertexColors(_drawList))
            Assert.AreEqual(0f, color.w, "The fill is transparent, whatever color was set after SetFill(false).");

        using (_drawList.Begin(Surface))
            Now.Rectangle(Card).SetFill(false).SetColor(Color.red).Draw();

        Assert.IsFalse(_drawList.hasGeometry, "A hollow rectangle without an outline draws nothing.");
    }

    enum Quality { Low, Medium, High }

    bool DrawRadioFrame(ref Quality quality, Vector2 pointer, bool press)
    {
        _provider.snapshot = new NowInputSnapshot(pointer, press, press, false);
        bool changed;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            changed = Now.Radio(new NowRect(0f, 0f, 120f, 24f), "Low").SetId("low").Draw(ref quality, Quality.Low);
            changed |= Now.Radio(new NowRect(0f, 30f, 120f, 24f), "High").SetId("high").Draw(ref quality, Quality.High);
        }

        return changed;
    }

    [Test]
    public void RadioGroupBindsToASelectedValue()
    {
        var quality = Quality.Low;

        // A click fires when a press is released over the option.
        DrawRadioFrame(ref quality, new Vector2(20f, 42f), true);
        _provider.snapshot = default;
        bool changed = false;

        _provider.snapshot = new NowInputSnapshot(new Vector2(20f, 42f), false, false, true);
        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            changed |= Now.Radio(new NowRect(0f, 0f, 120f, 24f), "Low").SetId("low").Draw(ref quality, Quality.Low);
            changed |= Now.Radio(new NowRect(0f, 30f, 120f, 24f), "High").SetId("high").Draw(ref quality, Quality.High);
        }

        Assert.IsTrue(changed);
        Assert.AreEqual(Quality.High, quality);
    }

    [Test]
    public void BadgeTakesCustomColors()
    {
        var accent = new Color(0.2f, 0.8f, 1f, 1f);

        using (_drawList.Begin(Surface))
            Now.Badge(new NowRect(10f, 10f, 80f, 22f), "LIVE").SetColors(accent.WithAlpha(0.12f), accent, 1f, accent.WithAlpha(0.4f)).Draw();

        Assert.IsTrue(_drawList.hasGeometry);
        bool foundFill = false;
        foreach (var color in VertexColors(_drawList))
            foundFill |= Mathf.Abs(color.w - 0.12f) < 0.01f;

        Assert.IsTrue(foundFill, "The pill uses the custom fill.");
    }

    [Test]
    public void GridLinesSitInsideTheRectAndFollowTheirOffset()
    {
        using (_drawList.Begin(Surface))
            Now.GridLines(new NowRect(0f, 0f, 100f, 50f), 20f).Draw();

        Assert.AreEqual(6 * 4, _drawList.mesh.vertexCount, "Four vertical and two horizontal lines, none on the edges.");

        using (_drawList.Begin(Surface))
            Now.GridLines(new NowRect(0f, 0f, 100f, 50f), 20f).SetOffset(new Vector2(5f, 0f)).SetAxes(true, false).Draw();

        Assert.AreEqual(5 * 4, _drawList.mesh.vertexCount, "An offset of 5 puts lines at 5, 25, 45, 65 and 85.");

        using (_drawList.Begin(Surface))
            Now.GridLines(new NowRect(0f, 0f, 100f, 50f), 0f).Draw();

        Assert.IsFalse(_drawList.hasGeometry);
    }

    [Test]
    public void GradientsCanBePlacedInUiUnits()
    {
        var rect = new NowRect(100f, 50f, 400f, 200f);

        var circle = Now.Gradient(rect).SetRadialAt(new Vector2(300f, 150f), 100f);
        Assert.AreEqual(new Vector4(0.5f, 0.5f, 0.5f, 0.5f), circle.parameters, "100 px over a 200 px smaller side is 0.5.");

        var ellipse = Now.Gradient(rect).SetRadialAt(new Vector2(100f, 250f), new Vector2(200f, 50f));
        Assert.AreEqual(new Vector4(0f, 1f, 0.5f, 0.25f), ellipse.parameters);

        var conic = Now.Gradient(rect).SetConicAt(new Vector2(200f, 100f), 90f);
        Assert.AreEqual(0.25f, conic.parameters.x, 1e-6f);
        Assert.AreEqual(0.25f, conic.parameters.y, 1e-6f);

        Assert.AreEqual(new NowRect(40f, 20f, 20f, 60f), NowRect.FromCenter(new Vector2(50f, 50f), 20f, 60f));
    }

    // ---------------------------------------------------------------- wrap

    static NowRect[] DrawWrappedRow(float width, NowLayoutAlign align, params Vector2[] sizes)
    {
        var rects = new NowRect[sizes.Length];

        using (NowLayout.Row(new NowRect(0f, 0f, width, 400f)).SetId("wrap-row").Gap(10f).Wrap(6f).AlignChildren(align).Begin())
        {
            for (int i = 0; i < sizes.Length; ++i)
                rects[i] = NowLayout.ReserveRect(sizes[i].x, sizes[i].y);
        }

        return rects;
    }

    [Test]
    public void WrappedRowBreaksLinesWhenChildrenDoNotFit()
    {
        var rects = DrawWrappedRow(200f, NowLayoutAlign.Start, new Vector2(80, 20), new Vector2(80, 20), new Vector2(80, 30), new Vector2(150, 20));

        Assert.AreEqual(new NowRect(0f, 0f, 80f, 20f), rects[0]);
        Assert.AreEqual(new NowRect(90f, 0f, 80f, 20f), rects[1]);
        Assert.AreEqual(new NowRect(0f, 26f, 80f, 30f), rects[2], "The third child starts the second line below the first plus the line gap.");
        Assert.AreEqual(new NowRect(0f, 62f, 150f, 20f), rects[3], "Line two is as tall as its tallest child.");
    }

    [Test]
    public void WrappedRowCentersChildrenOnTheirLine()
    {
        DrawWrappedRow(300f, NowLayoutAlign.Center, new Vector2(80, 20), new Vector2(80, 40));
        var rects = DrawWrappedRow(300f, NowLayoutAlign.Center, new Vector2(80, 20), new Vector2(80, 40));

        Assert.AreEqual(10f, rects[0].y, 0.001f, "Centered against the 40 px line measured on the previous pass.");
        Assert.AreEqual(0f, rects[1].y, 0.001f);
    }

    [Test]
    public void StretchingChildFillsTheRestOfItsLineAndNestedWrapReportsItsHeight()
    {
        NowRect filler = default;
        NowRect after = default;

        for (int pass = 0; pass < 2; ++pass)
        {
            using (NowLayout.Column(new NowRect(0f, 0f, 200f, 400f)).SetId("wrap-column").Begin())
            {
                using (NowLayout.Row().SetId("wrap-inner").Gap(10f).Wrap(6f).Begin())
                {
                    NowLayout.ReserveRect(80f, 20f);
                    filler = NowLayout.ReserveRect(new NowLayoutOptions().SetStretchWidth().SetMinWidth(40f).SetHeight(20f));
                    NowLayout.ReserveRect(120f, 24f);
                }

                after = NowLayout.ReserveRect(50f, 10f);
            }
        }

        Assert.AreEqual(90f, filler.x, 0.001f);
        Assert.AreEqual(110f, filler.width, 0.001f, "A stretching child takes what is left of its line.");
        Assert.AreEqual(20f + 6f + 24f, after.y, 0.001f, "The column places its next child below both wrapped lines.");
    }
}
