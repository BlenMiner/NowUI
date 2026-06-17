using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowValueControlsTests
{
    enum Quality
    {
        Low,
        Medium,
        High
    }

    [Flags]
    enum RenderFlags
    {
        None = 0,
        Shadows = 1 << 0,
        Bloom = 1 << 1,
        Vsync = 1 << 2,
        Everything = Shadows | Bloom | Vsync
    }

    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowTextInputSource
    {
        public NowTextInputFrame frame;

        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    sealed class RecordingRenderer : NowControlRenderer
    {
        public int textInputFrames;

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            ++textInputFrames;
            base.DrawTextInputFrame(context);
        }
    }

    static void SetRenderer(NowThemeAsset theme, NowControlRenderer renderer)
    {
        typeof(NowThemeAsset)
            .GetField("_controlRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(theme, renderer);
    }

    static readonly Vector2 Surface = new Vector2(512, 420);
    static readonly NowRect FieldRect = new NowRect(20, 20, 180, 30);

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowTextInput.Reset();
        NowLayout.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowTextInput.source = _keyboard;
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    bool DrawFloatFieldFrame(ref float value, NowTextInputFrame keys = default)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.FloatField(FieldRect, "float").SetRange(0f, 10f).Draw(ref value);
    }

    bool DrawColorPickerFrame(ref Color value, bool showAlpha = true)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.ColorPicker(FieldRect, "color").SetShowAlpha(showAlpha).Draw(ref value);
            NowOverlay.Flush();
            return changed;
        }
    }

    bool DrawGradientFieldFrame(ref Gradient value)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.GradientField(FieldRect, "gradient").Draw(ref value);
            NowOverlay.Flush();
            return changed;
        }
    }

    Texture2D FindGradientTexture()
    {
        for (int i = 0; i < _drawList.batches.Count; ++i)
        {
            var material = _drawList.batches[i].material;
            var texture = material != null ? material.mainTexture as Texture2D : null;

            if (texture != null && texture.name.StartsWith("Now Gradient", StringComparison.Ordinal))
                return texture;
        }

        return null;
    }

    bool DrawAnimationCurveFrame(ref AnimationCurve value)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.AnimationCurveField(FieldRect, "curve")
                .SetTimeRange(0f, 1f)
                .SetValueRange(0f, 1f)
                .Draw(ref value);
            NowOverlay.Flush();
            return changed;
        }
    }

    void OpenColorPicker()
    {
        using (NowInput.Begin(_pointer, Surface))
        {
            int id = NowControls.GetControlId("color");
            NowControlState.Get<bool>(id) = true;
        }
    }

    void OpenControl(string id)
    {
        using (NowInput.Begin(_pointer, Surface))
        {
            int controlId = NowControls.GetControlId(id);
            NowControlState.Get<bool>(controlId) = true;
        }
    }

    static Vector2 GradientColorMarkerPoint(float time)
    {
        return new Vector2(80f + Mathf.Clamp01(time) * 250f, 123f);
    }

    static Vector2 CurvePoint(float time, float value)
    {
        return new Vector2(32f + Mathf.Clamp01(time) * 296f, 224f - Mathf.Clamp01(value) * 158f);
    }

    [Test]
    public void ValueFieldsUseControlRendererFrames()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var color = Color.red;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            using (NowTheme.Scope(theme))
            {
                DrawColorPickerFrame(ref color);
                DrawGradientFieldFrame(ref gradient);
                DrawAnimationCurveFrame(ref curve);
            }

            Assert.AreEqual(3, renderer.textInputFrames);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void FloatFieldKeepsPartialTextAndClampsParsedValue()
    {
        float value = 0f;
        NowFocus.Focus(NowControls.GetControlId("float"));

        Assert.IsFalse(DrawFloatFieldFrame(ref value, new NowTextInputFrame { backspaceHeld = true }));
        Assert.AreEqual(0f, value, 0.0001f);

        Assert.IsTrue(DrawFloatFieldFrame(ref value, new NowTextInputFrame { characters = "12.5" }));
        Assert.AreEqual(10f, value, 0.0001f);
    }

    [Test]
    public void IntFieldClampsExistingValue()
    {
        int value = 42;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Assert.IsTrue(Now.IntField(FieldRect, "int").SetRange(0, 10).Draw(ref value));
        }

        Assert.AreEqual(10, value);
    }

    [Test]
    public void IntSliderSnapsToWholeNumbers()
    {
        int value = 0;
        var rect = new NowRect(0, 0, 200, 20);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(150, 10), true, true, false);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Assert.IsTrue(Now.Slider(rect, 0f, 10f).Draw(ref value));
        }

        Assert.Greater(value, 5);
        Assert.Less(value, 10);
    }

    [Test]
    public void VectorFieldClampsComponents()
    {
        var value = new Vector3(-1f, 0.5f, 2f);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Assert.IsTrue(Now.Vector3Field(new NowRect(0, 0, 280, 32), "vector").SetRange(0f, 1f).Draw(ref value));
        }

        Assert.AreEqual(new Vector3(0f, 0.5f, 1f), value);
    }

    [Test]
    public void ColorPickerSaturationValuePopupAppliesPendingColor()
    {
        Color value = Color.black;
        OpenColorPicker();

        var popupSaturationValueTopRight = new Vector2(189f, 64f);
        _pointer.snapshot = new NowInputSnapshot(popupSaturationValueTopRight, true, true, false);

        Assert.IsFalse(DrawColorPickerFrame(ref value, showAlpha: false));

        _pointer.snapshot = new NowInputSnapshot(popupSaturationValueTopRight, false, false, true);
        Assert.IsTrue(DrawColorPickerFrame(ref value, showAlpha: false));

        Assert.Greater(value.r, 0.9f);
        Assert.Less(value.g, 0.1f);
        Assert.Less(value.b, 0.1f);
        Assert.AreEqual(1f, value.a, 0.001f);
    }

    [Test]
    public void ColorPickerAlphaPopupAppliesPendingAlpha()
    {
        Color value = Color.red;
        OpenColorPicker();

        var popupAlphaMiddle = new Vector2(110f, 240f);
        _pointer.snapshot = new NowInputSnapshot(popupAlphaMiddle, true, true, false);

        Assert.IsFalse(DrawColorPickerFrame(ref value));

        _pointer.snapshot = new NowInputSnapshot(popupAlphaMiddle, false, false, true);
        Assert.IsTrue(DrawColorPickerFrame(ref value));

        Assert.AreEqual(0.5f, value.a, 0.08f);
        Assert.Greater(value.r, 0.9f);
    }

    [Test]
    public void ColorPickerPopupCopiesHex()
    {
        Color value = Color.red;
        string clipboard = null;
        var previousSet = NowClipboard.setText;
        var previousGet = NowClipboard.getText;
        NowClipboard.setText = text => clipboard = text;
        NowClipboard.getText = () => clipboard ?? string.Empty;

        try
        {
            OpenColorPicker();

            var copyButton = new Vector2(165f, 270f);
            _pointer.snapshot = new NowInputSnapshot(copyButton, true, true, false);
            Assert.IsFalse(DrawColorPickerFrame(ref value));

            _pointer.snapshot = new NowInputSnapshot(copyButton, false, false, true);
            Assert.IsFalse(DrawColorPickerFrame(ref value));

            Assert.AreEqual("#FF0000FF", clipboard);
        }
        finally
        {
            NowClipboard.setText = previousSet;
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void ColorPickerPopupPastesHex()
    {
        Color value = Color.black;
        var previousSet = NowClipboard.setText;
        var previousGet = NowClipboard.getText;
        NowClipboard.setText = _ => { };
        NowClipboard.getText = () => "#33669980";

        try
        {
            OpenColorPicker();

            var pasteButton = new Vector2(197f, 270f);
            _pointer.snapshot = new NowInputSnapshot(pasteButton, true, true, false);
            Assert.IsFalse(DrawColorPickerFrame(ref value));

            _pointer.snapshot = new NowInputSnapshot(pasteButton, false, false, true);
            Assert.IsFalse(DrawColorPickerFrame(ref value));

            _pointer.snapshot = new NowInputSnapshot(pasteButton, false, false, false);
            Assert.IsTrue(DrawColorPickerFrame(ref value));

            Assert.AreEqual(0x33 / 255f, value.r, 0.001f);
            Assert.AreEqual(0x66 / 255f, value.g, 0.001f);
            Assert.AreEqual(0x99 / 255f, value.b, 0.001f);
            Assert.AreEqual(0x80 / 255f, value.a, 0.001f);
        }
        finally
        {
            NowClipboard.setText = previousSet;
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void ColorPickerChannelSliderAppliesPendingColor()
    {
        Color value = Color.black;
        OpenColorPicker();

        var redSliderRight = new Vector2(180f, 296f);
        _pointer.snapshot = new NowInputSnapshot(redSliderRight, true, true, false);
        Assert.IsFalse(DrawColorPickerFrame(ref value));

        _pointer.snapshot = new NowInputSnapshot(redSliderRight, false, false, true);
        Assert.IsTrue(DrawColorPickerFrame(ref value));

        Assert.Greater(value.r, 0.9f);
        Assert.Less(value.g, 0.1f);
        Assert.Less(value.b, 0.1f);
        Assert.AreEqual(1f, value.a, 0.001f);
    }

    [Test]
    public void GradientFieldPreviewUsesCachedTexture()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        DrawGradientFieldFrame(ref gradient);
        var first = FindGradientTexture();

        Assert.NotNull(first);
        Assert.AreEqual(1024, first.width);
        Assert.AreEqual(1, first.height);
        Assert.AreEqual(FilterMode.Bilinear, first.filterMode);

        DrawGradientFieldFrame(ref gradient);
        Assert.AreSame(first, FindGradientTexture());
    }

    [Test]
    public void GradientFieldDragsColorKeyTime()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        _pointer.snapshot = new NowInputSnapshot(new Vector2(80f, 121f), true, true, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(205f, 121f), new Vector2(125f, 0f), true, false, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(205f, 121f), false, false, true);
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.colorKeys;
        Assert.AreEqual(2, keys.Length);
        Assert.Greater(keys[0].time, 0.45f);
        Assert.Less(keys[0].time, 0.55f);
    }

    [Test]
    public void GradientFieldKeepsDraggedColorKeyWhenCrossingNeighbors()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.gray, 0.25f),
                new GradientColorKey(Color.white, 0.75f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var start = GradientColorMarkerPoint(0.75f);
        var first = GradientColorMarkerPoint(0.10f);
        var second = GradientColorMarkerPoint(0.15f);

        _pointer.snapshot = new NowInputSnapshot(start, true, true, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(first, first - start, true, false, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(second, second - first, true, false, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(second, false, false, true);
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.colorKeys;
        Assert.AreEqual(3, keys.Length);
        Assert.That(keys[0].time, Is.InRange(0f, 0.02f));
        Assert.That(keys[1].time, Is.InRange(0.12f, 0.18f));
        Assert.That(keys[2].time, Is.InRange(0.23f, 0.27f), "The crossed neighbor should not inherit the drag.");
        Assert.AreEqual(Color.white, keys[1].color);
        Assert.AreEqual(Color.gray, keys[2].color);
    }

    [Test]
    public void GradientFieldInlineColorEditorChangesSelectedColor()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var redSliderRight = new Vector2(315f, 422f);
        _pointer.snapshot = new NowInputSnapshot(redSliderRight, true, true, false);
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));

        _pointer.snapshot = new NowInputSnapshot(redSliderRight, false, false, true);
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.colorKeys;
        Assert.AreEqual(2, keys.Length);
        Assert.Greater(keys[0].color.r, 0.9f);
        Assert.Less(keys[0].color.g, 0.1f);
        Assert.Less(keys[0].color.b, 0.1f);
    }

    [Test]
    public void GradientFieldPressingAlphaKeyOnlySelectsIt()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var alphaMarker = new Vector2(80f, 71f);
        _pointer.snapshot = new NowInputSnapshot(alphaMarker, true, true, false);
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));

        _pointer.snapshot = new NowInputSnapshot(alphaMarker, false, false, true);
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.alphaKeys;
        Assert.AreEqual(2, keys.Length);
        Assert.AreEqual(0.75f, keys[0].alpha, 0.001f);
    }

    [Test]
    public void GradientFieldSelectedAlphaSliderChangesAlpha()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var alphaMarker = new Vector2(80f, 71f);
        _pointer.snapshot = new NowInputSnapshot(alphaMarker, true, true, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(alphaMarker, false, false, true);
        DrawGradientFieldFrame(ref gradient);

        var alphaSliderRight = new Vector2(245f, 152f);
        _pointer.snapshot = new NowInputSnapshot(alphaSliderRight, true, true, false);
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));

        _pointer.snapshot = new NowInputSnapshot(alphaSliderRight, false, false, true);
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.alphaKeys;
        Assert.AreEqual(2, keys.Length);
        Assert.Greater(keys[0].alpha, 0.8f);
    }

    [Test]
    public void GradientFieldDoubleClickCreatesColorKey()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var stripPoint = new Vector2(205f, 96f);
        _pointer.snapshot = new NowInputSnapshot(stripPoint, true, true, false);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = new NowInputSnapshot(stripPoint, false, false, true);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = default;
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));
        Assert.AreEqual(2, gradient.colorKeys.Length);

        _pointer.snapshot = new NowInputSnapshot(stripPoint, true, true, false);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = new NowInputSnapshot(stripPoint, false, false, true);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = default;
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.colorKeys;
        Assert.AreEqual(3, keys.Length);
        Assert.That(keys[1].time, Is.InRange(0.45f, 0.55f));
    }

    [Test]
    public void GradientFieldDoubleClickCreatesAlphaKey()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var stripPoint = new Vector2(205f, 71f);
        _pointer.snapshot = new NowInputSnapshot(stripPoint, true, true, false);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = new NowInputSnapshot(stripPoint, false, false, true);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = default;
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));
        Assert.AreEqual(2, gradient.alphaKeys.Length);

        _pointer.snapshot = new NowInputSnapshot(stripPoint, true, true, false);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = new NowInputSnapshot(stripPoint, false, false, true);
        DrawGradientFieldFrame(ref gradient);
        _pointer.snapshot = default;
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.alphaKeys;
        Assert.AreEqual(3, keys.Length);
        Assert.That(keys[1].time, Is.InRange(0.45f, 0.55f));
    }

    [Test]
    public void GradientFieldDeleteKeyRemovesSelectedAlphaKey()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        var alphaMarker = new Vector2(80f, 71f);
        _pointer.snapshot = new NowInputSnapshot(alphaMarker, true, true, false);
        DrawGradientFieldFrame(ref gradient);

        _pointer.snapshot = new NowInputSnapshot(alphaMarker, false, false, true);
        DrawGradientFieldFrame(ref gradient);

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        NowTextInput.Invalidate();
        _pointer.snapshot = default;
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));

        _keyboard.frame = default;
        NowTextInput.Invalidate();
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.alphaKeys;
        Assert.AreEqual(1, keys.Length);
        Assert.AreEqual(1f, keys[0].time, 0.001f);
    }

    [Test]
    public void GradientFieldDeleteKeyRemovesSelectedColorKey()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.gray, 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        OpenControl("gradient");

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        NowTextInput.Invalidate();
        _pointer.snapshot = default;
        Assert.IsFalse(DrawGradientFieldFrame(ref gradient));

        _keyboard.frame = default;
        NowTextInput.Invalidate();
        Assert.IsTrue(DrawGradientFieldFrame(ref gradient));

        var keys = gradient.colorKeys;
        Assert.AreEqual(2, keys.Length);
        Assert.AreEqual(0.5f, keys[0].time, 0.001f);
    }

    [Test]
    public void AnimationCurveFieldDragsKeyTimeAndValue()
    {
        var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        OpenControl("curve");

        _pointer.snapshot = new NowInputSnapshot(new Vector2(32f, 224f), true, true, false);
        DrawAnimationCurveFrame(ref curve);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(180f, 145f), new Vector2(148f, -79f), true, false, false);
        DrawAnimationCurveFrame(ref curve);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(180f, 145f), false, false, true);
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(2, keys.Length);
        Assert.Greater(keys[0].time, 0.45f);
        Assert.Less(keys[0].time, 0.55f);
        Assert.Greater(keys[0].value, 0.45f);
        Assert.Less(keys[0].value, 0.55f);
    }

    [Test]
    public void AnimationCurveFieldKeepsDraggedKeyWhenCrossingNeighbors()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0.25f),
            new Keyframe(0.75f, 0.75f));
        OpenControl("curve");

        var start = CurvePoint(0.75f, 0.75f);
        var first = CurvePoint(0.10f, 0.10f);
        var second = CurvePoint(0.15f, 0.15f);

        _pointer.snapshot = new NowInputSnapshot(start, true, true, false);
        DrawAnimationCurveFrame(ref curve);

        _pointer.snapshot = new NowInputSnapshot(first, first - start, true, false, false);
        DrawAnimationCurveFrame(ref curve);

        _pointer.snapshot = new NowInputSnapshot(second, second - first, true, false, false);
        DrawAnimationCurveFrame(ref curve);

        _pointer.snapshot = new NowInputSnapshot(second, false, false, true);
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(3, keys.Length);
        Assert.That(keys[0].time, Is.InRange(0f, 0.02f));
        Assert.That(keys[0].value, Is.InRange(0f, 0.02f));
        Assert.That(keys[1].time, Is.InRange(0.12f, 0.18f));
        Assert.That(keys[1].value, Is.InRange(0.12f, 0.18f));
        Assert.That(keys[2].time, Is.InRange(0.23f, 0.27f), "The crossed neighbor should not inherit the drag.");
        Assert.That(keys[2].value, Is.InRange(0.23f, 0.27f));
    }

    [Test]
    public void AnimationCurveFieldSmoothButtonSetsSelectedTangents()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
        OpenControl("curve");

        var smoothButton = new Vector2(72f, 282f);
        _pointer.snapshot = new NowInputSnapshot(smoothButton, true, true, false);
        Assert.IsFalse(DrawAnimationCurveFrame(ref curve));

        _pointer.snapshot = new NowInputSnapshot(smoothButton, false, false, true);
        Assert.IsFalse(DrawAnimationCurveFrame(ref curve));

        _pointer.snapshot = default;
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(2, keys.Length);
        Assert.AreEqual(1f, keys[0].outTangent, 0.001f);
        Assert.AreEqual(1f, keys[0].inTangent, 0.001f);
        Assert.AreEqual(WeightedMode.None, keys[0].weightedMode);
    }

    [Test]
    public void AnimationCurveFieldDraggingOutTangentUpdatesWeightedTangent()
    {
        var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        OpenControl("curve");

        var outHandle = new Vector2(131f, 171f);
        _pointer.snapshot = new NowInputSnapshot(outHandle, true, true, false);
        DrawAnimationCurveFrame(ref curve);

        var movedHandle = new Vector2(131f, 100f);
        _pointer.snapshot = new NowInputSnapshot(movedHandle, movedHandle - outHandle, true, false, false);
        DrawAnimationCurveFrame(ref curve);

        _pointer.snapshot = new NowInputSnapshot(movedHandle, false, false, true);
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(2, keys.Length);
        Assert.Greater(keys[0].outTangent, 2f);
        Assert.IsTrue((keys[0].weightedMode & WeightedMode.Out) != 0);
    }

    [Test]
    public void AnimationCurveFieldDoubleClickCreatesKey()
    {
        var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        OpenControl("curve");

        var plotPoint = new Vector2(180f, 145f);
        _pointer.snapshot = new NowInputSnapshot(plotPoint, true, true, false);
        DrawAnimationCurveFrame(ref curve);
        _pointer.snapshot = new NowInputSnapshot(plotPoint, false, false, true);
        DrawAnimationCurveFrame(ref curve);
        _pointer.snapshot = default;
        Assert.IsFalse(DrawAnimationCurveFrame(ref curve));
        Assert.AreEqual(2, curve.length);

        _pointer.snapshot = new NowInputSnapshot(plotPoint, true, true, false);
        DrawAnimationCurveFrame(ref curve);
        _pointer.snapshot = new NowInputSnapshot(plotPoint, false, false, true);
        DrawAnimationCurveFrame(ref curve);
        _pointer.snapshot = default;
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(3, keys.Length);
        Assert.That(keys[1].time, Is.InRange(0.45f, 0.55f));
        Assert.That(keys[1].value, Is.InRange(0.45f, 0.55f));
    }

    [Test]
    public void AnimationCurveFieldDeletesSelectedKeyFromInspector()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.5f),
            new Keyframe(1f, 1f));
        OpenControl("curve");

        var deleteButton = new Vector2(291f, 245f);
        _pointer.snapshot = new NowInputSnapshot(deleteButton, true, true, false);
        Assert.IsFalse(DrawAnimationCurveFrame(ref curve));

        _pointer.snapshot = new NowInputSnapshot(deleteButton, false, false, true);
        Assert.IsFalse(DrawAnimationCurveFrame(ref curve));

        _pointer.snapshot = default;
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(2, keys.Length);
        Assert.AreEqual(0.5f, keys[0].time, 0.0001f);
    }

    [Test]
    public void AnimationCurveFieldDeleteKeyRemovesSelectedKey()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.5f),
            new Keyframe(1f, 1f));
        OpenControl("curve");

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        NowTextInput.Invalidate();
        _pointer.snapshot = default;
        Assert.IsFalse(DrawAnimationCurveFrame(ref curve));

        _keyboard.frame = default;
        NowTextInput.Invalidate();
        Assert.IsTrue(DrawAnimationCurveFrame(ref curve));

        var keys = curve.keys;
        Assert.AreEqual(2, keys.Length);
        Assert.AreEqual(0.5f, keys[0].time, 0.0001f);
    }

    [Test]
    public void EnumDropdownAppliesPendingSelection()
    {
        Quality quality = Quality.Low;
        int id;

        using (NowInput.Begin(_pointer, Surface))
            id = NowControls.GetControlId("quality");

        NowControlState.Get<int>(NowInput.GetId(id, "pending")) = 3;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Assert.IsTrue(Now.EnumDropdown<Quality>(new NowRect(0, 0, 180, 30), "quality").Draw(ref quality));
        }

        Assert.AreEqual(Quality.High, quality);
    }

    [Test]
    public void EnumFlagsToggleSingleBitFlags()
    {
        RenderFlags flags = RenderFlags.Everything;
        var pointer = new Vector2(12f, 14f);

        _pointer.snapshot = new NowInputSnapshot(pointer, true, true, false);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Assert.IsFalse(Now.EnumFlags<RenderFlags>(new NowRect(0, 0, 220, 100), "flags").Draw(ref flags));

        _pointer.snapshot = new NowInputSnapshot(pointer, false, false, true);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Assert.IsTrue(Now.EnumFlags<RenderFlags>(new NowRect(0, 0, 220, 100), "flags").Draw(ref flags));

        Assert.AreEqual(RenderFlags.Bloom | RenderFlags.Vsync, flags);
    }
}
