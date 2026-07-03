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
}
