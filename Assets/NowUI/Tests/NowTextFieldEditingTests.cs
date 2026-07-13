using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// Single-line text field behavior tests: shift-click selection extension,
/// Escape revert, undo/redo and the focused-empty placeholder, driven through
/// fake pointer and keyboard sources.
/// </summary>
public class NowTextFieldEditingTests
{
    sealed class AppearanceRecordingRenderer : NowControlRenderer
    {
        public int legacyMeasureCalls;
        public int legacyInnerRectCalls;
        public int styledMeasureCalls;
        public int styledInnerRectCalls;
        public int frameCalls;
        public int elevationCalls;
        public NowControlFrameRenderContext lastFrame;
        public NowRect lastInnerRect;
        public Vector4 lastElevationRadius;
        public NowElevationToken lastElevation;

        public override Vector2 MeasureTextField(NowThemeAsset themeAsset, float lineHeight)
        {
            ++legacyMeasureCalls;
            return base.MeasureTextField(themeAsset, lineHeight);
        }

        public override NowRect TextFieldInnerRect(NowThemeAsset themeAsset, NowRect rect, float lineHeight)
        {
            ++legacyInnerRectCalls;
            return base.TextFieldInnerRect(themeAsset, rect, lineHeight);
        }

        public override Vector2 MeasureTextField(NowThemeAsset themeAsset, float lineHeight, in NowTextFieldAppearance appearance)
        {
            ++styledMeasureCalls;
            return base.MeasureTextField(themeAsset, lineHeight, in appearance);
        }

        public override NowRect TextFieldInnerRect(NowThemeAsset themeAsset, NowRect rect, float lineHeight, in NowTextFieldAppearance appearance)
        {
            ++styledInnerRectCalls;
            lastInnerRect = base.TextFieldInnerRect(themeAsset, rect, lineHeight, in appearance);
            return lastInnerRect;
        }

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            ++frameCalls;
            lastFrame = context;
            base.DrawTextInputFrame(context);
        }

        public override void DrawElevationShadow(NowThemeAsset themeAsset, NowRect rect, Vector4 radius, NowElevationToken level)
        {
            ++elevationCalls;
            lastElevationRadius = radius;
            lastElevation = level;
        }
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

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect FieldRect = new NowRect(20, 20, 240, 30);

    NowFontAsset _font;
    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowDrawList _drawList;

    [OneTimeSetUp]
    public void LoadFont()
    {
        _font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(_font, "Default font resource missing.");
    }

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowTextInput.Reset();
        NowTextUndoRegistry.Reset();
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
        NowTextUndoRegistry.Reset();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    static void SetRenderer(NowThemeAsset theme, NowControlRenderer renderer)
    {
        typeof(NowThemeAsset)
            .GetField("_controlRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(theme, renderer);
    }

    static int Id => NowInput.GetId("name");

    void Focus()
    {
        NowFocus.Focus(Id);
    }

    ref NowTextEditState State()
    {
        return ref NowControlState.Get<NowTextEditState>(Id);
    }

    bool Frame(ref string text, NowTextInputFrame keys = default, string placeholder = null)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var field = Now.TextField(FieldRect, "name");

            if (placeholder != null)
                field = field.SetPlaceholder(placeholder);

            changed = field.Draw(ref text);
        }

        return changed;
    }

    bool FloatFrame(ref float value, NowTextInputFrame keys = default)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "name").Draw(ref value);

        return changed;
    }

    void PointerFrame(ref string text, Vector2 point, bool down, bool pressed, bool released, NowTextInputFrame keys = default)
    {
        _pointer.snapshot = new NowInputSnapshot(point, down, pressed, released);
        Frame(ref text, keys);
    }

    static Vector2 TextFieldPoint(string textBefore)
    {
        var theme = NowTheme.themeAsset;
        var textStyle = theme.Text(default, NowTextStyle.Body);
        float lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize : textStyle.fontSize * 1.2f;
        var inner = theme.controlRenderer.TextFieldInnerRect(theme, FieldRect, lineHeight);
        float x = inner.x + (textStyle.font != null ? textStyle.font.MeasureText(textBefore, textStyle.fontSize, textStyle.fontStyle).x : 0f) + 1f;
        return new Vector2(x, inner.y + inner.height * 0.5f);
    }

    [Test]
    public void ShiftClickExtendsSelectionFromTheExistingAnchor()
    {
        string text = "hello world wide";
        var afterHello = TextFieldPoint("hello");
        var afterWorld = TextFieldPoint("hello world");

        PointerFrame(ref text, afterHello, down: true, pressed: true, released: false);
        PointerFrame(ref text, afterHello, down: false, pressed: false, released: true);
        Assert.AreEqual(5, State().caret, "The plain click places the caret after 'hello'.");
        Assert.IsFalse(State().hasSelection);

        PointerFrame(ref text, afterWorld, down: true, pressed: true, released: false,
            new NowTextInputFrame { shift = true });

        Assert.AreEqual(5, State().selectionMin, "Shift-click keeps the existing anchor.");
        Assert.AreEqual(11, State().selectionMax, "Shift-click moves only the caret to the hit index.");
        Assert.AreEqual(" world", NowTextEdit.GetSelection(text, State()));
    }

    [Test]
    public void ShiftClickDragKeepsExtendingFromTheAnchor()
    {
        string text = "hello world wide";
        var afterHello = TextFieldPoint("hello");
        var afterWorld = TextFieldPoint("hello world");
        var afterWide = TextFieldPoint("hello world wide");

        PointerFrame(ref text, afterHello, down: true, pressed: true, released: false);
        PointerFrame(ref text, afterHello, down: false, pressed: false, released: true);

        PointerFrame(ref text, afterWorld, down: true, pressed: true, released: false,
            new NowTextInputFrame { shift = true });
        PointerFrame(ref text, afterWide, down: true, pressed: false, released: false,
            new NowTextInputFrame { shift = true });
        PointerFrame(ref text, afterWide, down: false, pressed: false, released: true);

        Assert.AreEqual(" world wide", NowTextEdit.GetSelection(text, State()),
            "Dragging after a shift-click keeps the original anchor.");
    }

    [Test]
    public void EscapeRevertsToTheFocusGainText()
    {
        string text = "hello";
        Focus();

        Frame(ref text);
        Assert.IsTrue(Frame(ref text, new NowTextInputFrame { characters = "!!" }));
        Assert.AreEqual("hello!!", text);

        bool changed = Frame(ref text, new NowTextInputFrame { escapePressed = true });

        Assert.IsFalse(changed, "The revert frame must not report a change.");
        Assert.AreEqual("hello", text, "Escape restores the text captured on focus gain.");
        Assert.AreEqual(0, NowFocus.focusedId, "Escape still blurs the field.");
    }

    [Test]
    public void EnterKeepsCommittingTheEditedText()
    {
        string text = "hello";
        Focus();

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { characters = "!" });
        bool changed = Frame(ref text, new NowTextInputFrame { enterPressed = true });

        Assert.IsFalse(changed, "Enter without new characters reports no change.");
        Assert.AreEqual("hello!", text, "Enter commits instead of reverting.");
        Assert.AreEqual(0, NowFocus.focusedId, "Enter blurs the field.");
    }

    [Test]
    public void EscapeRevertsNumericValueToTheFocusGainValue()
    {
        float value = 5f;
        Focus();

        FloatFrame(ref value);
        FloatFrame(ref value, new NowTextInputFrame { characters = "1" });
        Assert.AreEqual(51f, value, "Typing while focused updates the parsed value.");

        bool changed = FloatFrame(ref value, new NowTextInputFrame { escapePressed = true });

        Assert.IsFalse(changed, "The revert frame must not report a change.");
        Assert.AreEqual(5f, value, "Escape restores the value captured on focus gain.");
        Assert.AreEqual(0, NowFocus.focusedId);
    }

    [Test]
    public void UndoAndRedoRoundTripInTheTextField()
    {
        string text = string.Empty;
        Focus();

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { characters = "ab" });
        Frame(ref text, new NowTextInputFrame { characters = "c" });
        Assert.AreEqual("abc", text);

        Frame(ref text, new NowTextInputFrame { undoPressed = true, command = true });
        Assert.AreEqual(string.Empty, text, "Undo removes the coalesced typing burst.");

        Frame(ref text, new NowTextInputFrame { redoPressed = true, command = true });
        Assert.AreEqual("abc", text, "Redo reapplies the edit.");
    }

    [Test]
    public void UndoRestoresTextRemovedByCut()
    {
        var previousSet = NowClipboard.setText;
        var previousGet = NowClipboard.getText;
        string clipboard = string.Empty;
        NowClipboard.setText = value => clipboard = value;
        NowClipboard.getText = () => clipboard;

        try
        {
            string text = "keep me";
            Focus();

            Frame(ref text);
            Frame(ref text, new NowTextInputFrame { selectAllPressed = true, command = true });
            Frame(ref text, new NowTextInputFrame { cutPressed = true, command = true });
            Assert.AreEqual(string.Empty, text);

            Frame(ref text, new NowTextInputFrame { undoPressed = true, command = true });
            Assert.AreEqual("keep me", text, "Undo restores the cut text.");
        }
        finally
        {
            NowClipboard.setText = previousSet;
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void PlaceholderStaysVisibleWhileFocusedAndEmpty()
    {
        string text = string.Empty;
        Focus();

        Frame(ref text);
        Assert.AreEqual(Id, NowFocus.focusedId, "Fixture must keep the field focused.");

        Frame(ref text, placeholder: "Type here");
        int withPlaceholder = _drawList.mesh.vertexCount;

        Frame(ref text);
        int withoutPlaceholder = _drawList.mesh.vertexCount;

        Assert.AreEqual(Id, NowFocus.focusedId);
        Assert.Greater(withPlaceholder, withoutPlaceholder,
            "A focused empty field must still draw its placeholder.");
    }

    [Test]
    public void PerInstanceAppearanceFlowsThroughMeasurementAndFrameRendering()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<AppearanceRecordingRenderer>();
        SetRenderer(theme, renderer);

        var background = new Color(0.96f, 0.97f, 0.99f, 1f);
        var border = new Color(0.72f, 0.75f, 0.80f, 1f);
        var focus = new Color(0.12f, 0.38f, 0.92f, 1f);
        var textColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        var placeholderColor = new Color(0.36f, 0.38f, 0.42f, 1f);
        var rect = new NowRect(20f, 20f, 240f, 50f);
        string text = string.Empty;

        try
        {
            NowFocus.Focus(NowInput.GetId("styled-field"));

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                Now.TextField(rect, "styled-field")
                    .SetRadius(NowRadiusToken.Pill)
                    .SetBackgroundColor(background)
                    .SetBorderColor(border)
                    .SetFocusColor(focus)
                    .SetTextColor(textColor)
                    .SetPlaceholderColor(placeholderColor)
                    .SetPadding(18f, 9f, 22f, 11f)
                    .SetOutlineWidth(1.25f)
                    .SetFocusOutlineWidth(2.5f)
                    .SetElevation(NowElevationToken.Raised)
                    .Draw(ref text);
            }

            Assert.AreEqual(1, renderer.styledMeasureCalls);
            Assert.AreEqual(1, renderer.styledInnerRectCalls);
            Assert.AreEqual(1, renderer.frameCalls);
            Assert.IsTrue(renderer.lastFrame.focused);

            var appearance = renderer.lastFrame.appearance;
            Assert.IsTrue(appearance.hasOverrides);
            Assert.AreEqual(new Vector4(18f, 9f, 22f, 11f), appearance.padding);
            Assert.AreEqual(1.25f, appearance.outlineWidth, 0.0001f);
            Assert.AreEqual(2.5f, appearance.focusOutlineWidth, 0.0001f);
            Assert.AreEqual(background, appearance.ResolveBackgroundColor(theme, Color.clear));
            Assert.AreEqual(border, appearance.ResolveBorderColor(theme, Color.clear));
            Assert.AreEqual(focus, appearance.ResolveFocusColor(theme, Color.clear));
            Assert.AreEqual(textColor, appearance.ResolveTextColor(theme, Color.clear));
            Assert.AreEqual(placeholderColor, appearance.ResolvePlaceholderColor(theme, Color.clear));

            Assert.AreEqual(38f, renderer.lastInnerRect.x, 0.0001f);
            Assert.AreEqual(200f, renderer.lastInnerRect.width, 0.0001f);
            Assert.AreEqual(1, renderer.elevationCalls);
            Assert.AreEqual(NowElevationToken.Raised, renderer.lastElevation);
            Assert.AreEqual(new Vector4(25f, 25f, 25f, 25f), renderer.lastElevationRadius);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void DefaultAppearanceDelegatesToLegacyRendererMetrics()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<AppearanceRecordingRenderer>();
        SetRenderer(theme, renderer);
        NowTextFieldAppearance appearance = default;

        try
        {
            Vector2 legacyMeasure = renderer.MeasureTextField(theme, 20f);
            Vector2 appearanceMeasure = renderer.MeasureTextField(theme, 20f, in appearance);
            NowRect rect = new NowRect(20f, 20f, 240f, 50f);
            NowRect legacyInner = renderer.TextFieldInnerRect(theme, rect, 20f);
            NowRect appearanceInner = renderer.TextFieldInnerRect(theme, rect, 20f, in appearance);

            Assert.IsFalse(appearance.hasOverrides);
            Assert.AreEqual(legacyMeasure, appearanceMeasure);
            Assert.AreEqual(legacyInner, appearanceInner);
            Assert.AreEqual(2, renderer.legacyMeasureCalls,
                "The appearance overload must dispatch through a legacy renderer's measurement override.");
            Assert.AreEqual(2, renderer.legacyInnerRectCalls,
                "The appearance overload must dispatch through a legacy renderer's inner-rect override.");

            string text = string.Empty;

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
                Now.TextField(rect, "default-field").Draw(ref text);

            Assert.IsFalse(renderer.lastFrame.appearance.hasOverrides,
                "An unstyled field must leave renderer theming fully intact.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void MaterialRendererUsesLiteralFrameOverridesInBothFocusStates()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<NowMaterialControlRenderer>();
        var drawList = new NowDrawList();
        var rect = new NowRect(20f, 20f, 240f, 48f);
        var background = new Color(0.91f, 0.92f, 0.93f, 1f);
        var border = new Color(0.31f, 0.32f, 0.33f, 1f);
        var focus = new Color(0.11f, 0.42f, 0.88f, 1f);
        var appearance = new NowTextFieldAppearance()
            .SetRadius(NowRadiusToken.Pill)
            .SetBackgroundColor(background)
            .SetBorderColor(border)
            .SetFocusColor(focus)
            .SetOutlineWidth(1.25f)
            .SetFocusOutlineWidth(2.75f);

        try
        {
            void AssertFrame(bool focused, Color expectedOutline, float expectedWidth)
            {
                drawList.Clear();

                using (drawList.Begin(Surface))
                    renderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused, in appearance));

                var radii = new System.Collections.Generic.List<Vector4>();
                var colors = new System.Collections.Generic.List<Vector4>();
                var outlineColors = new System.Collections.Generic.List<Vector4>();
                var extras = new System.Collections.Generic.List<Vector4>();
                drawList.mesh.GetUVs(2, radii);
                drawList.mesh.GetUVs(3, colors);
                drawList.mesh.GetUVs(4, outlineColors);
                drawList.mesh.GetUVs(5, extras);

                Assert.AreEqual(new Vector4(24f, 24f, 24f, 24f), radii[0]);
                Assert.AreEqual((Vector4)background, colors[0]);
                Assert.AreEqual((Vector4)expectedOutline, outlineColors[0]);
                Assert.AreEqual(expectedWidth, extras[0].y, 0.0001f);
            }

            AssertFrame(false, border, 1.25f);
            AssertFrame(true, focus, 2.75f);
        }
        finally
        {
            drawList.Dispose();
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void LargeAsymmetricLiteralRadiusIsNotTreatedAsPillToken()
    {
        var corners = new NowCornerRadius(
            topLeft: 1000f,
            topRight: 7f,
            bottomRight: 11f,
            bottomLeft: 13f);
        var appearance = default(NowTextFieldAppearance).SetRadius(corners);
        var rect = new NowRect(0f, 0f, 120f, 40f);

        Vector4 resolved = appearance.ResolveRadius(null, rect, fallback: Vector4.zero);

        Assert.AreEqual(corners.packed, resolved,
            "Literal per-corner radii must remain asymmetric even when one value exceeds the Pill sentinel.");
    }

    [Test]
    public void TextFieldExposesCompleteLayoutConstraints()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<AppearanceRecordingRenderer>();
        SetRenderer(theme, renderer);
        string text = string.Empty;

        try
        {
            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            using (NowLayout.Area(new NowRect(0f, 0f, 400f, 200f)))
            {
                NowLayout.TextField("layout-field")
                    .SetStretchWidth()
                    .SetMinWidth(160f)
                    .SetMaxWidth(240f)
                    .SetHeight(48f)
                    .SetMinHeight(40f)
                    .SetMaxHeight(60f)
                    .SetAlign(NowLayoutAlign.Center)
                    .Draw(ref text);
            }

            Assert.AreEqual(80f, renderer.lastFrame.rect.x, 0.0001f);
            Assert.AreEqual(0f, renderer.lastFrame.rect.y, 0.0001f);
            Assert.AreEqual(240f, renderer.lastFrame.rect.width, 0.0001f);
            Assert.AreEqual(48f, renderer.lastFrame.rect.height, 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void AppearanceRejectsInvalidGeometryAndTokens()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetRadius(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetPadding(new Vector4(1f, float.NaN, 1f, 1f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetOutlineWidth(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetRadius((NowRadiusToken)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetBackgroundColor((NowColorToken)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetPlaceholderColor((NowColorToken)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowTextFieldAppearance).SetElevation((NowElevationToken)999));
    }
}
