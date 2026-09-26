using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NowUI;
using UnityEngine;

/// <summary>
/// Draw-time shortcuts must never change what is emitted: controls, labels and
/// rectangles clipped away by an ambient mask skip their work early, and text
/// under a uniform transform takes the bulk glyph writer instead of the
/// per-glyph path. Each test compares the shortcut against the full path.
/// </summary>
public class NowEarlyCullingTests
{
    static readonly Vector2 Surface = new Vector2(800f, 600f);
    static readonly NowRect Viewport = new NowRect(160f, 140f, 360f, 240f);

    sealed class RestingPointer : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = new NowInputSnapshot(new Vector2(-500f, -500f), false, false, false);
            return true;
        }
    }

    sealed class SubclassedRenderer : NowControlRenderer
    {
        public int buttons;

        public override void DrawButton(in NowButtonRenderContext context)
        {
            ++buttons;
            base.DrawButton(context);
        }
    }

    struct Geometry
    {
        public List<Vector3> positions;
        public List<Vector4>[] channels;
    }

    static readonly string[] Ids = BuildIds(1024);

    NowDrawList _drawList;
    RestingPointer _pointer;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        _drawList = new NowDrawList();
        _pointer = new RestingPointer();
    }

    [TearDown]
    public void TearDown()
    {
        Now.earlyAmbientCulling = true;
        _drawList.Dispose();
        NowLayout.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowFocus.Reset();
        NowInput.Reset();
    }

    static string[] BuildIds(int count)
    {
        var ids = new string[count];

        for (int i = 0; i < count; ++i)
            ids[i] = "cull-" + i;

        return ids;
    }

    static void SetRenderer(NowThemeAsset theme, NowControlRenderer renderer)
    {
        typeof(NowThemeAsset)
            .GetField("_controlRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(theme, renderer);
    }

    Geometry Capture(Action draw)
    {
        _drawList.Clear();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            draw();

        var geometry = new Geometry
        {
            positions = new List<Vector3>(),
            channels = new List<Vector4>[8]
        };

        _drawList.mesh.GetVertices(geometry.positions);

        for (int channel = 0; channel < geometry.channels.Length; ++channel)
        {
            geometry.channels[channel] = new List<Vector4>();

            if (_drawList.mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + channel))
                _drawList.mesh.GetUVs(channel, geometry.channels[channel]);
        }

        return geometry;
    }

    /// <summary>
    /// Draws a few frames so retained state (scroll extents, transitions) settles,
    /// proves two full draws agree, then compares a culled draw against them.
    /// </summary>
    void AssertCullingPreservesOutput(Action draw, string context)
    {
        Now.earlyAmbientCulling = false;

        for (int i = 0; i < 3; ++i)
            Capture(draw);

        var full = Capture(draw);
        AssertSameGeometry(full, Capture(draw), 0f, context + " (steady state)");
        Assert.Greater(full.positions.Count, 0, context + ": the scene draws visible geometry.");

        Now.earlyAmbientCulling = true;
        var culled = Capture(draw);
        AssertSameGeometry(full, culled, 0f, context);
    }

    static void AssertSameGeometry(in Geometry expected, in Geometry actual, float tolerance, string context)
    {
        Assert.AreEqual(expected.positions.Count, actual.positions.Count, context + ": vertex count");

        for (int i = 0; i < expected.positions.Count; ++i)
        {
            Assert.AreEqual(expected.positions[i].x, actual.positions[i].x, tolerance, $"{context}: vertex {i} x");
            Assert.AreEqual(expected.positions[i].y, actual.positions[i].y, tolerance, $"{context}: vertex {i} y");
        }

        for (int channel = 0; channel < expected.channels.Length; ++channel)
        {
            var a = expected.channels[channel];
            var b = actual.channels[channel];
            Assert.AreEqual(a.Count, b.Count, $"{context}: channel {channel} count");

            for (int i = 0; i < a.Count; ++i)
            {
                for (int component = 0; component < 4; ++component)
                    Assert.AreEqual(a[i][component], b[i][component], tolerance, $"{context}: channel {channel} vertex {i}[{component}]");
            }
        }
    }

    /// <summary>
    /// Places every control kind at offsets that sweep across each edge of the
    /// viewport, from well outside to just inside, so both culled and partly
    /// visible instances (including elevation shadows reaching in) are drawn.
    /// </summary>
    static void DrawControlSweep(NowThemeAsset theme)
    {
        int id = 0;
        bool check = true;
        bool toggle = false;
        float value = 0.4f;

        using (NowTheme.Scope(theme))
        using (Now.Mask(Viewport))
        {
            for (int kind = 0; kind < 8; ++kind)
            for (int edge = 0; edge < 4; ++edge)
            {
                for (float offset = -90f; offset <= 12f; offset += 6f)
                {
                    const float Width = 150f;
                    const float Height = 32f;
                    NowRect rect;

                    switch (edge)
                    {
                        case 0:
                            rect = new NowRect(Viewport.x + 40f, Viewport.y - Height - offset - 12f, Width, Height);
                            break;
                        case 1:
                            rect = new NowRect(Viewport.x + 40f, Viewport.yMax + offset + 12f, Width, Height);
                            break;
                        case 2:
                            rect = new NowRect(Viewport.x - Width - offset - 12f, Viewport.y + 60f, Width, Height);
                            break;
                        default:
                            rect = new NowRect(Viewport.xMax + offset + 12f, Viewport.y + 60f, Width, Height);
                            break;
                    }

                    switch (kind)
                    {
                        case 0:
                            Now.Button(rect, "Apply").SetId(Ids[id]).SetStyle(NowRectangleStyle.Elevated).Draw();
                            break;
                        case 1:
                            Now.Button(rect, "Apply").SetId(Ids[id]).Draw();
                            break;
                        case 2:
                            Now.Checkbox(rect, "Enabled").SetId(Ids[id]).Draw(ref check);
                            break;
                        case 3:
                            Now.Radio(rect, "Choice", true).SetId(Ids[id]).Draw();
                            break;
                        case 4:
                            Now.Switch(rect, "Switch").SetId(Ids[id]).Draw(ref toggle);
                            break;
                        case 5:
                            Now.Slider(rect, 0f, 1f).SetLabel("Gain").SetValueFormat("0.00").SetId(Ids[id]).Draw(ref value);
                            break;
                        case 6:
                            Now.SelectableRow(rect, "Row").SetSelected().SetId(Ids[id]).Draw();
                            break;
                        default:
                            Now.Rectangle(rect).SetColor(Color.white).SetRadius(6f).SetBlur(9f).SetOutline(2f).Draw();
                            Now.Text(rect).SetFontSize(14f).SetColor(Color.white).Draw("Plain label");
                            break;
                    }

                    ++id;
                }
            }
        }
    }

    static NowControlRenderer CreateRenderer(Type type)
    {
        return (NowControlRenderer)ScriptableObject.CreateInstance(type);
    }

    [Test]
    public void ControlsClippedAwayEmitExactlyWhatAFullDrawDoes(
        [Values(typeof(NowControlRenderer), typeof(NowMaterialControlRenderer), typeof(NowUnityEditorControlRenderer))] Type rendererType,
        [Values(1f, 0.4f, 2.5f)] float scale)
    {
        Assert.NotNull(Now.defaultFont, "The default font is required to draw labels.");

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = CreateRenderer(rendererType);
        SetRenderer(theme, renderer);

        try
        {
            Assert.IsTrue(renderer.isBuiltIn);

            void Draw()
            {
                using (Now.Transform(scale, new Vector2(12f, -7f)))
                    DrawControlSweep(theme);
            }

            AssertCullingPreservesOutput(Draw, rendererType.Name);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void LayoutRowsScrolledOutOfViewEmitExactlyWhatAFullDrawDoes()
    {
        Assert.NotNull(Now.defaultFont, "The default font is required to draw labels.");

        void Draw()
        {
            using (NowLayout.Area("cull-scroll-area", Viewport))
            using (NowLayout.ScrollView("cull-scroll").Begin())
            using (NowLayout.VerticalScope(spacing: 2f, padding: 2f))
            {
                for (int i = 0; i < 60; ++i)
                {
                    NowLayout.Label(Ids[i]).Draw();
                    NowLayout.Button("Command").SetId(Ids[100 + i]).SetStretchWidth().Draw();
                }
            }
        }

        AssertCullingPreservesOutput(Draw, "scroll rows");
    }

    [Test]
    public void SubclassedRenderersAreCalledEvenWhenTheControlIsClippedAway()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<SubclassedRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            Assert.IsFalse(renderer.isBuiltIn);

            Capture(() =>
            {
                using (NowTheme.Scope(theme))
                using (Now.Mask(Viewport))
                    Now.Button(new NowRect(0f, 560f, 100f, 30f), "Far away").SetId("cull-custom").Draw();
            });

            Assert.AreEqual(1, renderer.buttons, "A custom renderer may draw beyond the control rect.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    /// <summary>
    /// A uniform positive transform draws text through the bulk glyph writer. A
    /// scale that differs by one part in a million still takes the per-glyph
    /// path, so it is the reference: positions agree to a hundredth of a unit and
    /// every glyph, color, mask and range channel matches. Tabs and line breaks
    /// check that the pen keeps advancing in local units between segments.
    /// </summary>
    [Test]
    public void UniformlyScaledTextMatchesThePerGlyphPath(
        [Values(true, false)] bool shaping,
        [Values(0.6f, 1.75f)] float scale)
    {
        Assert.NotNull(Now.defaultFont, "The default font is required to draw text.");

        const string Value = "Scaled AV fi text\twith a tab\nand a second line 0123";
        var origin = new Vector2(31f, -17f);
        bool previousShaping = Now.textShaping;
        Now.textShaping = shaping;

        try
        {
            void Draw(Vector2 transformScale)
            {
                using (Now.Transform(transformScale, origin))
                {
                    Now.Text(new NowRect(12f, 20f, 420f, 60f)).SetFontSize(15f).SetColor(Color.white).Draw(Value);
                    Now.Text(new NowRect(12f, 90f, 420f, 30f)).SetFontSize(22f).SetOutline(0.1f).Draw("Outlined AV");
                }
            }

            var bulk = Capture(() => Draw(new Vector2(scale, scale)));
            var reference = Capture(() => Draw(new Vector2(scale, scale * (1f + 1e-6f))));

            Assert.Greater(bulk.positions.Count, 0, "Scaled text draws.");
            AssertSameGeometry(reference, bulk, 0.01f, shaping ? "shaped" : "codepoints");
        }
        finally
        {
            Now.textShaping = previousShaping;
        }
    }
}
