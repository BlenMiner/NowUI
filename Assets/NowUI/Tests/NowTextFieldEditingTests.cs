using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using NowUI;

/// <summary>
/// Single-line text field behavior tests: shift-click selection extension,
/// Escape revert, undo/redo and the focused-empty placeholder, driven through
/// fake pointer and keyboard sources.
/// </summary>
public class NowTextFieldEditingTests
{
    sealed class ResultRecordingLayoutGraphic : NowLayoutGraphic
    {
        public INowInputProvider inputProvider;
        public string value = "query";
        public int drawCount;
        public NowTextFieldResult measureResult;
        public NowTextFieldResult drawResult;

        protected override INowInputProvider GetInputProvider()
        {
            return inputProvider;
        }

        protected override void DrawNowUI(NowRect rect)
        {
            using (NowLayout.Column(rect).Begin())
            {
                NowTextFieldResult result = NowLayout.TextField("host-field")
                    .SetStretchWidth()
                    .SetHeight(40f)
                    .Draw(ref value);

                ++drawCount;

                if (NowLayout.isMeasurePass)
                    measureResult = result;
                else
                    drawResult = result;
            }
        }
    }

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
    static readonly NowRect NavigationBeforeRect = new NowRect(20, 70, 90, 30);
    static readonly NowRect NavigationFieldRect = new NowRect(130, 70, 240, 30);
    static readonly NowRect NavigationAfterRect = new NowRect(390, 70, 90, 30);

    NowFontAsset _font;
    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowDrawList _drawList;
    int _snapshotFrame;

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
        _snapshotFrame = 0;
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

    static int BeforeId => NowInput.GetId("before-name");

    static int AfterId => NowInput.GetId("after-name");

    void Focus()
    {
        NowFocus.Focus(Id);
    }

    [Test]
    public void UndoRegistryEvictsLeastRecentlyUsedStackAtCapacity()
    {
        var oldest = NowTextUndoRegistry.Get(1);

        for (int id = 2; id <= NowTextUndoRegistry.Capacity + 1; ++id)
            NowTextUndoRegistry.Get(id);

        Assert.AreEqual(NowTextUndoRegistry.Capacity, NowTextUndoRegistry.count);
        Assert.AreNotSame(oldest, NowTextUndoRegistry.Get(1));
        Assert.AreEqual(NowTextUndoRegistry.Capacity, NowTextUndoRegistry.count);
    }

    ref NowTextEditState State()
    {
        return ref NowControlState.Get<NowTextEditState>(Id);
    }

    NowTextFieldResult FrameResult(ref string text, NowTextInputFrame keys = default, string placeholder = null)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        NowTextFieldResult result;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var field = Now.TextField(FieldRect, "name");

            if (placeholder != null)
                field = field.SetPlaceholder(placeholder);

            result = field.Draw(ref text);
        }

        return result;
    }

    bool Frame(ref string text, NowTextInputFrame keys = default, string placeholder = null)
    {
        return FrameResult(ref text, keys, placeholder);
    }

    [Test]
    public void TextActivityIsConsumedAfterOneInputPassWithinSameUnityFrame()
    {
        string text = string.Empty;
        Focus();
        _keyboard.frame = new NowTextInputFrame { characters = "a" };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.TextField(FieldRect, "name").Draw(ref text);

        Assert.AreEqual("a", text);

        // IMGUI can immediately draw Layout/Repaint again without advancing
        // Time.frameCount. The already-delivered character must be spent.
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.TextField(FieldRect, "name").Draw(ref text);

        Assert.AreEqual("a", text,
            "A cached KeyDown character must not be replayed by another IMGUI pass in the same Unity frame.");
    }

    [Test]
    public void ClaimedTextActivityKeepsHeldModifiersAndComposition()
    {
        _keyboard.frame = new NowTextInputFrame
        {
            characters = "x",
            composition = "か",
            backspaceHeld = true,
            enterPressed = true,
            shift = true,
            command = true
        };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        {
            NowTextInput.ClaimActivity();
            var first = NowTextInput.current;
            Assert.AreEqual("x", first.characters);
            Assert.IsTrue(first.enterPressed);
        }

        using (NowInput.Begin(_pointer, Surface))
        {
            var nextPass = NowTextInput.current;
            Assert.IsNull(nextPass.characters);
            Assert.IsFalse(nextPass.enterPressed);
            Assert.IsTrue(nextPass.backspaceHeld);
            Assert.IsTrue(nextPass.shift);
            Assert.IsTrue(nextPass.command);
            Assert.AreEqual("か", nextPass.composition);
        }
    }

    [Test]
    public void TextCaptureCanDeferClaimWithoutBreakingLegacyCaptureAndClaim()
    {
        _keyboard.frame = new NowTextInputFrame { characters = "x" };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        {
            NowTextInput.RequestTextCapture(claimActivity: false);
            Assert.AreEqual("x", NowTextInput.current.characters);
        }

        using (NowInput.Begin(_pointer, Surface))
        {
            NowTextInput.RequestTextCapture();
            Assert.AreEqual("x", NowTextInput.current.characters,
                "The compatible overload may claim before sampling without hiding the current pass.");
        }

        using (NowInput.Begin(_pointer, Surface))
        {
            Assert.IsNull(NowTextInput.current.characters,
                "The parameterless overload must retain its legacy capture-and-claim behavior.");
        }
    }

    NowInputSnapshot NavigationSnapshot(
        Vector2 navigation = default,
        bool focusPrevious = false,
        bool focusNext = false,
        bool cancel = false)
    {
        int frame = ++_snapshotFrame;

        return new NowInputSnapshot(
            false,
            default,
            default,
            default,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            default,
            navigation,
            focusPrevious,
            focusNext,
            false,
            false,
            false,
            cancel,
            cancel,
            false,
            frame,
            frame);
    }

    NowTextFieldResult NavigationFrame(
        ref string text,
        NowTextInputFrame keys = default,
        Vector2 navigation = default,
        bool focusPrevious = false,
        bool focusNext = false,
        bool cancel = false,
        bool advanceFocusFrame = false)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        _pointer.snapshot = NavigationSnapshot(navigation, focusPrevious, focusNext, cancel);
        NowTextFieldResult result;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            if (advanceFocusFrame)
                NowFocus.ForceNewFrame();

            NowControls.Interact(BeforeId, NavigationBeforeRect, out _, out _);
            result = Now.TextField(NavigationFieldRect, "name").Draw(ref text);
            NowControls.Interact(AfterId, NavigationAfterRect, out _, out _);
        }

        return result;
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

    [TestCase("a", -1f, 0f)]
    [TestCase("d", 1f, 0f)]
    [TestCase("w", 0f, 1f)]
    [TestCase("s", 0f, -1f)]
    public void FocusedTextFieldTypesWasdWithoutMovingFocus(string character, float x, float y)
    {
        string text = string.Empty;
        Focus();
        NavigationFrame(ref text);

        NowTextFieldResult result = NavigationFrame(
            ref text,
            new NowTextInputFrame { characters = character },
            new Vector2(x, y),
            advanceFocusFrame: true);

        Assert.IsTrue(result.changed);
        Assert.AreEqual(character, text);
        Assert.AreEqual(Id, NowFocus.focusedId);
    }

    [TestCase("left", 0)]
    [TestCase("right", 2)]
    [TestCase("up", 1)]
    [TestCase("down", 1)]
    public void FocusedTextFieldArrowKeysDoNotMoveFocus(string direction, int expectedCaret)
    {
        string text = "ab";
        Focus();
        NavigationFrame(ref text);
        State().caret = 1;
        State().anchor = 1;
        var keys = new NowTextInputFrame();
        Vector2 navigation;

        switch (direction)
        {
            case "left":
                keys.leftHeld = true;
                navigation = Vector2.left;
                break;
            case "right":
                keys.rightHeld = true;
                navigation = Vector2.right;
                break;
            case "up":
                keys.upHeld = true;
                navigation = Vector2.up;
                break;
            default:
                keys.downHeld = true;
                navigation = Vector2.down;
                break;
        }

        NavigationFrame(ref text, keys, navigation, advanceFocusFrame: true);

        Assert.AreEqual(expectedCaret, State().caret);
        Assert.AreEqual(Id, NowFocus.focusedId);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FocusedTextFieldAllowsTabTraversal(bool previous)
    {
        string text = "hello";
        Focus();
        NavigationFrame(ref text);

        NavigationFrame(
            ref text,
            focusPrevious: previous,
            focusNext: !previous,
            advanceFocusFrame: true);

        Assert.AreEqual(previous ? BeforeId : AfterId, NowFocus.focusedId);
    }

    [Test]
    public void ImeCompositionOwnsDeviceCancelWithoutBlurring()
    {
        string text = "hello";
        Focus();
        NavigationFrame(ref text);

        NavigationFrame(
            ref text,
            new NowTextInputFrame { composition = "か", escapePressed = true },
            cancel: true,
            advanceFocusFrame: true);

        Assert.AreEqual("hello", text);
        Assert.AreEqual(Id, NowFocus.focusedId);
    }

    [Test]
    public void EscapeRevertsToTheFocusGainText()
    {
        string text = "hello";
        Focus();

        NavigationFrame(ref text);
        Assert.IsTrue(NavigationFrame(
            ref text,
            new NowTextInputFrame { characters = "!!" },
            advanceFocusFrame: true).changed);
        Assert.AreEqual("hello!!", text);

        NowTextFieldResult result = NavigationFrame(
            ref text,
            new NowTextInputFrame { escapePressed = true },
            focusNext: true,
            cancel: true,
            advanceFocusFrame: true);
        bool changed = result.changed;

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
        NowTextFieldResult result = FrameResult(ref text, new NowTextInputFrame { enterPressed = true });
        bool changed = result;

        Assert.IsFalse(changed, "Enter without new characters reports no change.");
        Assert.IsTrue(result.submitted, "Enter is exposed separately from value changes.");
        Assert.AreEqual("hello!", text, "Enter commits instead of reverting.");
        Assert.AreEqual(0, NowFocus.focusedId, "Enter blurs the field.");

        _keyboard.frame = new NowTextInputFrame { enterPressed = true, enterHeld = true };
        NowTextInput.Invalidate();
        var spentEnter = NowTextInput.current;
        Assert.IsFalse(spentEnter.enterPressed,
            "The Enter that committed a field must not submit the next focused control.");
        Assert.IsFalse(spentEnter.enterHeld);
    }

    [Test]
    public void EnterImmediatelyRendersBlurredAndRequestsRepaint()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<AppearanceRecordingRenderer>();
        SetRenderer(theme, renderer);
        string text = "hello";

        try
        {
            Focus();

            using (NowTheme.Scope(theme))
                Frame(ref text);

            renderer.frameCalls = 0;
            NowControlState.BeginRepaintTracking();

            NowTextFieldResult result;
            bool repaintRequested;

            try
            {
                using (NowTheme.Scope(theme))
                    result = FrameResult(ref text, new NowTextInputFrame { enterPressed = true });
            }
            finally
            {
                repaintRequested = NowControlState.EndRepaintTracking();
            }

            Assert.IsTrue(result.submitted);
            Assert.AreEqual(0, NowFocus.focusedId);
            Assert.AreEqual(1, renderer.frameCalls);
            Assert.IsFalse(renderer.lastFrame.focused,
                "The submit draw must not render one stale focused/caret frame after focus was cleared.");
            Assert.IsTrue(repaintRequested,
                "A retained Editor host must redraw immediately instead of keeping its cached focused frame.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void IMGUIEnterResamplesAndBlursWithinTheSameUnityFrame()
    {
        string text = "hello";
        Focus();
        Frame(ref text);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        bool focusPreviousPressed = false;
        bool focusNextPressed = false;
        bool submitPressed = false;
        bool cancelPressed = false;
        var enterEvent = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Return
        };

        try
        {
            NowIMGUIInputProvider.instance.ApplyKeyDown(
                enterEvent,
                ref focusPreviousPressed,
                ref focusNextPressed,
                ref submitPressed,
                ref cancelPressed);

            NowTextFieldResult result;

            // Deliberately do not call NowTextInput.Invalidate here: the
            // native IMGUI key pass must refresh a stale same-frame sample.
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
                result = Now.TextField(FieldRect, "name").Draw(ref text);

            Assert.IsTrue(result.submitted);
            Assert.AreEqual(0, NowFocus.focusedId);
        }
        finally
        {
            bool submitReleased = false;
            bool cancelReleased = false;
            NowIMGUIInputProvider.instance.ApplyKeyUp(
                new Event { type = EventType.KeyUp, keyCode = KeyCode.Return },
                ref submitReleased,
                ref cancelReleased);
        }
    }

    [Test]
    public void PassiveMeasureDoesNotReportFocusedEnterSubmission()
    {
        string text = "hello";
        NowTextFieldResult result = default;
        Focus();
        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            int snapshot = NowLayout.BeginMeasurePass();

            try
            {
                using (NowLayout.Area(new NowRect(0f, 0f, 320f, 80f)))
                    result = NowLayout.TextField("name").SetHeight(40f).Draw(ref text);
            }
            finally
            {
                NowLayout.EndMeasurePass(snapshot);
            }
        }

        Assert.IsTrue(NowFocus.IsFocused(Id),
            "A passive pass must not blur the focused field.");
        Assert.IsFalse(result.submitted,
            "A passive pass must not expose Enter as an actionable event.");
        Assert.IsFalse(result.changed);
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
    public void LayoutTextFieldResultExposesItsResolvedRectWithoutAnotherReservation()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<AppearanceRecordingRenderer>();
        string text = "query";
        NowTextFieldResult result = default;
        NowRect following = default;

        SetRenderer(theme, renderer);

        try
        {
            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            using (NowLayout.Area(new NowRect(0f, 0f, 400f, 200f)))
            {
                result = NowLayout.TextField("decorated-field")
                    .SetWidth(240f)
                    .SetHeight(48f)
                    .SetPadding(44f, 12f, 16f, 12f)
                    .Draw(ref text);

                following = NowLayout.ReserveRect(20f, 10f);
            }

            Assert.AreEqual(renderer.lastFrame.rect, result.rect,
                "The result should report the exact rect used by the renderer.");
            Assert.AreEqual(new NowRect(0f, 0f, 240f, 48f), result.rect);
            Assert.AreEqual(48f, following.y, 0.0001f,
                "Reading the control rect must not consume a second layout slot.");
            Assert.IsFalse(result.changed);
            Assert.IsFalse(result.submitted);

            bool changed = result;
            Assert.IsFalse(changed,
                "The bool-compatible result must retain Draw's changed-value convention.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ExactLayoutHostSuppressesSubmitDuringMeasurePass()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var graphicObject = new GameObject(
            "Now Text Field Result Host",
            typeof(RectTransform),
            typeof(CanvasRenderer));

        try
        {
            graphicObject.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 80f);
            var graphic = graphicObject.AddComponent<ResultRecordingLayoutGraphic>();
            graphic.inputProvider = _pointer;

            _keyboard.frame = new NowTextInputFrame { enterPressed = true };
            NowTextInput.Invalidate();

            graphic.Rebuild(CanvasUpdate.PreRender);

            Assert.AreEqual(2, graphic.drawCount,
                "The exact layout host should run one passive measure and one real draw.");
            Assert.IsFalse(graphic.measureResult.submitted,
                "The passive measure pass must never consume or report submission.");
            Assert.AreEqual(graphic.measureResult.rect, graphic.drawResult.rect,
                "Exact-host measurement and drawing should resolve the same control rect.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graphicObject);
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
