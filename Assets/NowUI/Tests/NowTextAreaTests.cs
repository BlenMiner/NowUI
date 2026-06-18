using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// Multi-line text area tests: the editing line layout (every character
/// covered) and the control's keyboard/pointer ergonomics, driven through
/// fake pointer and keyboard sources.
/// </summary>
public class NowTextAreaTests
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

    static readonly Vector2 Surface = new Vector2(512, 512);
    static readonly NowRect AreaRect = new NowRect(20, 20, 240, 120);

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
    }

    bool Frame(ref string text, NowTextInputFrame keys = default)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextArea(AreaRect, "notes").Draw(ref text);

        return changed;
    }

    static int Id => NowInput.GetId("notes");

    static int AreaStateId => NowInput.GetId(Id, "area");

    void Focus()
    {
        NowFocus.Focus(Id);
    }

    ref NowTextEditState State()
    {
        return ref NowControlState.Get<NowTextEditState>(Id);
    }

    float AreaScrollY()
    {
        var areaType = typeof(NowTextArea).GetNestedType("AreaState", BindingFlags.NonPublic);
        Assert.NotNull(areaType);

        var storeDefinition = typeof(NowControlState).GetNestedType("Store`1", BindingFlags.NonPublic);
        Assert.NotNull(storeDefinition);

        var storeType = storeDefinition.MakeGenericType(areaType);
        var entriesField = storeType.GetField("entries", BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(entriesField);

        var entries = (IDictionary)entriesField.GetValue(null);
        Assert.IsTrue(entries.Contains(AreaStateId), "TextArea state was not created.");

        object entry = entries[AreaStateId];
        object value = entry.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public).GetValue(entry);
        return (float)areaType.GetField("scrollY", BindingFlags.Instance | BindingFlags.Public).GetValue(value);
    }

    void PointerFrame(ref string text, Vector2 pointer, bool down, bool pressed, bool released)
    {
        _pointer.snapshot = new NowInputSnapshot(pointer, down, pressed, released);
        Frame(ref text);
    }

    Vector2 TextPoint(string textBefore, int line = 0)
    {
        var textStyle = NowTheme.themeAsset.Text(default, NowTextStyle.Body);
        float lineHeight = textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize;
        var inner = AreaRect.Inset(8f, 6f, 8f, 6f);
        return new Vector2(
            inner.x + textStyle.font.MeasureText(textBefore, textStyle.fontSize).x + 1f,
            inner.y + lineHeight * (line + 0.5f));
    }

    static List<NowTextAreaLine> Layout(string text, NowFontAsset font, float width, float fontSize = 16f)
    {
        var lines = new List<NowTextAreaLine>();
        NowTextArea.LayoutLines(text, font, fontSize, NowFontStyle.Regular, width, lines);
        return lines;
    }

    static string LongText()
    {
        var lines = new List<string>();

        for (int i = 0; i < 30; ++i)
            lines.Add($"line {i}");

        return string.Join("\n", lines);
    }

    [Test]
    public void LayoutCoversEveryCharacterExactlyOnce()
    {
        const string text = "the quick brown fox jumps\nover the lazy dog\n\nsupercalifragilistic";
        var lines = Layout(text, _font, 90f);

        int cursor = 0;

        for (int i = 0; i < lines.Count; ++i)
        {
            Assert.AreEqual(cursor, lines[i].start, $"Line {i} must start where the previous coverage ended.");
            cursor = lines[i].start + lines[i].length;

            if (cursor < text.Length && text[cursor] == '\n')
                ++cursor;
        }

        Assert.AreEqual(text.Length, cursor, "Lines plus newlines must cover the full text.");
    }

    [Test]
    public void LayoutSplitsOnNewlinesIncludingTrailingEmptyLine()
    {
        var lines = Layout("a\nbb\n", _font, 500f);

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual(0, lines[0].start);
        Assert.AreEqual(1, lines[0].length);
        Assert.AreEqual(2, lines[1].start);
        Assert.AreEqual(2, lines[1].length);
        Assert.AreEqual(5, lines[2].start);
        Assert.AreEqual(0, lines[2].length, "A trailing newline yields an empty final line for the caret.");
    }

    [Test]
    public void LayoutWrapsAtWordBoundaries()
    {
        float width = _font.MeasureText("hello ", 16f).x + _font.MeasureText("wor", 16f).x;
        var lines = Layout("hello world", _font, width);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual(6, lines[1].start, "The wrapped line must start at the word, not mid-word.");
        Assert.AreEqual(5, lines[1].length);
    }

    [Test]
    public void LayoutHardSplitsWordsWiderThanTheArea()
    {
        float width = _font.MeasureText("aaaa", 16f).x + 1f;
        var lines = Layout("aaaaaaaaaa", _font, width);

        Assert.Greater(lines.Count, 1, "A word wider than the area must hard-split.");

        for (int i = 0; i < lines.Count; ++i)
            Assert.Greater(lines[i].length, 0, "Hard splits must always make progress.");
    }

    [Test]
    public void EmptyTextHasOneEmptyLine()
    {
        var lines = Layout(string.Empty, _font, 100f);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(0, lines[0].length);
    }

    [Test]
    public void LineOfPutsWrapBoundaryOnNextLineButHardBreakEndStaysPut()
    {
        const string text = "hello world\nx";
        float width = _font.MeasureText("hello ", 16f).x + _font.MeasureText("wor", 16f).x;
        var lines = Layout(text, _font, width);

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual(1, NowTextArea.LineOf(text, lines, 6), "A caret at a wrap boundary sits at the start of the lower line.");
        Assert.AreEqual(1, NowTextArea.LineOf(text, lines, 11), "A caret before an explicit newline stays at the end of its line.");
        Assert.AreEqual(2, NowTextArea.LineOf(text, lines, 13));
    }

    [Test]
    public void TypingAndEnterBuildMultipleLines()
    {
        string text = string.Empty;
        Focus();

        Assert.IsTrue(Frame(ref text, new NowTextInputFrame { characters = "ab" }));
        Assert.IsTrue(Frame(ref text, new NowTextInputFrame { enterHeld = true }));
        Assert.IsTrue(Frame(ref text, new NowTextInputFrame { characters = "c" }));
        Assert.AreEqual("ab\nc", text);
    }

    [Test]
    public void FocusedWheelScrollCanHideCaretUntilCaretNavigation()
    {
        string text = LongText();
        var point = TextPoint(string.Empty);

        Frame(ref text);
        PointerFrame(ref text, point, down: true, pressed: true, released: false);
        PointerFrame(ref text, point, down: false, pressed: false, released: true);

        Assert.AreEqual(0f, AreaScrollY(), 0.001f, "Fixture must start with the caret-visible top line.");

        _pointer.snapshot = new NowInputSnapshot(
            true, point, point, Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            new Vector2(0f, -3f), Vector2.zero,
            false, false, false, false, false, false, 2, 2f);
        Frame(ref text);

        float scrolled = AreaScrollY();
        Assert.Greater(scrolled, 0f, "Wheel scrolling a focused text area should not be cancelled by caret reveal.");

        _pointer.snapshot = new NowInputSnapshot(
            true, point, point, Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false, false, false, false, 3, 3f);
        Frame(ref text, new NowTextInputFrame { homePressed = true, command = true });

        Assert.AreEqual(0f, AreaScrollY(), 0.001f, "Caret navigation should reveal the caret after free scrolling.");
    }

    [Test]
    public void UpDownPreserveTheGoalColumn()
    {
        string text = "aaaa\naa\naaaa";
        Focus();

        Frame(ref text);
        Assert.AreEqual(12, State().caret, "Focus without a click puts the caret at the end.");

        Frame(ref text, new NowTextInputFrame { upHeld = true });
        Assert.AreEqual(7, State().caret, "Up clamps to the shorter middle line's end.");

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { upHeld = true });
        Assert.AreEqual(4, State().caret, "The original column survives crossing a shorter line.");

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { downHeld = true });
        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { downHeld = true });
        Assert.AreEqual(12, State().caret, "Down retraces to the original position.");
    }

    [Test]
    public void UpFromFirstLineGoesToDocumentStartAndDownFromLastToEnd()
    {
        string text = "ab\ncd";
        Focus();

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { downHeld = true });
        Assert.AreEqual(5, State().caret);

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { upHeld = true });
        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { upHeld = true });
        Assert.AreEqual(0, State().caret, "Up from the first line jumps to the document start.");
    }

    [Test]
    public void HomeAndEndWorkPerLineAndPerDocument()
    {
        string text = "first\nsecond";
        Focus();

        Frame(ref text);
        Assert.AreEqual(12, State().caret);

        Frame(ref text, new NowTextInputFrame { homePressed = true });
        Assert.AreEqual(6, State().caret, "Home goes to the start of the caret's line.");

        Frame(ref text, new NowTextInputFrame { endPressed = true });
        Assert.AreEqual(12, State().caret, "End goes to the end of the caret's line.");

        Frame(ref text, new NowTextInputFrame { homePressed = true, command = true });
        Assert.AreEqual(0, State().caret, "Ctrl+Home goes to the document start.");

        Frame(ref text, new NowTextInputFrame { endPressed = true, command = true });
        Assert.AreEqual(12, State().caret, "Ctrl+End goes to the document end.");
    }

    [Test]
    public void ShiftDownSelectsAcrossTheNewline()
    {
        string text = "ab\ncd";
        Focus();

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { homePressed = true, command = true });
        Frame(ref text, new NowTextInputFrame { downHeld = true, shift = true });

        Assert.IsTrue(State().hasSelection);
        Assert.AreEqual(0, State().selectionMin);
        Assert.GreaterOrEqual(State().selectionMax, 3, "The selection must reach into the second line.");
    }

    [Test]
    public void CopyAndCutKeepNewlines()
    {
        var previousSet = NowClipboard.setText;
        var previousGet = NowClipboard.getText;
        string clipboard = string.Empty;
        NowClipboard.setText = value => clipboard = value;
        NowClipboard.getText = () => clipboard;

        try
        {
            string text = "ab\ncd";
            Focus();

            Frame(ref text, new NowTextInputFrame { selectAllPressed = true, command = true });
            Frame(ref text, new NowTextInputFrame { copyPressed = true, command = true });
            Assert.AreEqual("ab\ncd", clipboard, "Copy must keep the newline.");

            Frame(ref text, new NowTextInputFrame { cutPressed = true, command = true });
            Assert.AreEqual(string.Empty, text, "Cut removes the selection.");
            Assert.AreEqual("ab\ncd", clipboard);
        }
        finally
        {
            NowClipboard.setText = previousSet;
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void PasteNormalizesWindowsLineEndings()
    {
        var previousGet = NowClipboard.getText;
        NowClipboard.getText = () => "a\r\nb\rc";

        try
        {
            string text = string.Empty;
            Focus();

            Frame(ref text, new NowTextInputFrame { pastePressed = true, command = true });
            Assert.AreEqual("a\nb\nc", text);
        }
        finally
        {
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void BackspaceAtLineStartJoinsLines()
    {
        string text = "ab\ncd";
        Focus();

        Frame(ref text);
        State().caret = 3;
        State().anchor = 3;

        Frame(ref text, new NowTextInputFrame { backspaceHeld = true });
        Assert.AreEqual("abcd", text);
        Assert.AreEqual(2, State().caret);
    }

    [Test]
    public void EnterInsertsNewlineInsteadOfBlurring()
    {
        string text = "ab";
        Focus();

        Frame(ref text);
        Frame(ref text, new NowTextInputFrame { enterHeld = true });

        Assert.AreEqual("ab\n", text);
        Assert.AreEqual(Id, NowFocus.focusedId, "Enter must not blur a text area.");

        Frame(ref text, new NowTextInputFrame { escapePressed = true });
        Assert.AreEqual(0, NowFocus.focusedId, "Escape blurs.");
    }

    [Test]
    public void ClickPlacesCaretOnTheClickedLine()
    {
        string text = "aaaa\naa";

        var textStyle = NowTheme.themeAsset.Text(default, NowTextStyle.Body);
        float lineHeight = textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize;
        var inner = AreaRect.Inset(8f, 6f, 8f, 6f);

        _pointer.snapshot = new NowInputSnapshot(
            new Vector2(inner.x + 200f, inner.y + lineHeight * 1.5f), true, true, false);
        Frame(ref text);

        Assert.AreEqual(7, State().caret, "A click far right on the second line lands at its end.");
    }

    [Test]
    public void DoubleClickDragExtendsByWholeWords()
    {
        string text = "hello world wide";
        var insideWorld = TextPoint("hello wo");
        var insideWide = TextPoint("hello world wi");

        PointerFrame(ref text, insideWorld, down: true, pressed: true, released: false);
        PointerFrame(ref text, insideWorld, down: false, pressed: false, released: true);
        PointerFrame(ref text, insideWorld, down: true, pressed: true, released: false);
        PointerFrame(ref text, insideWide, down: true, pressed: false, released: false);
        PointerFrame(ref text, insideWide, down: false, pressed: false, released: true);

        Assert.AreEqual("world wide", NowTextEdit.GetSelection(text, State()),
            "Dragging after a double-click stays in word selection mode.");
    }

    [Test]
    public void TripleClickSelectsTheHardLine()
    {
        string text = "first line\nsecond";

        var textStyle = NowTheme.themeAsset.Text(default, NowTextStyle.Body);
        float lineHeight = textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize;
        var inner = AreaRect.Inset(8f, 6f, 8f, 6f);
        var click = new Vector2(inner.x + 4f, inner.y + lineHeight * 0.5f);

        for (int i = 0; i < 3; ++i)
        {
            _pointer.snapshot = new NowInputSnapshot(click, true, true, false);
            Frame(ref text);
            _pointer.snapshot = new NowInputSnapshot(click, false, false, true);
            Frame(ref text);
        }

        Assert.AreEqual(0, State().selectionMin);
        Assert.AreEqual(10, State().selectionMax, "Triple-click selects the full hard line without its newline.");
    }

    [Test]
    public void TripleClickDragExtendsByWholeLines()
    {
        string text = "first line\nsecond";
        var firstLine = TextPoint(string.Empty, 0);
        var secondLine = TextPoint(string.Empty, 1);

        for (int i = 0; i < 2; ++i)
        {
            PointerFrame(ref text, firstLine, down: true, pressed: true, released: false);
            PointerFrame(ref text, firstLine, down: false, pressed: false, released: true);
        }

        PointerFrame(ref text, firstLine, down: true, pressed: true, released: false);
        PointerFrame(ref text, secondLine, down: true, pressed: false, released: false);
        PointerFrame(ref text, secondLine, down: false, pressed: false, released: true);

        Assert.AreEqual(text, NowTextEdit.GetSelection(text, State()),
            "Dragging after a triple-click stays in line selection mode.");
    }

    [Test]
    public void SelectLineSelectsNewlineDelimitedRange()
    {
        var state = default(NowTextEditState);
        NowTextEdit.SelectLine(ref state, "ab\ncd\nef", 4);

        Assert.AreEqual(3, state.selectionMin);
        Assert.AreEqual(5, state.selectionMax);
    }

    [Test]
    public void ImeCompositionSuppressesEditingKeysAndCommitInserts()
    {
        string text = "ab";
        Focus();

        Frame(ref text);
        Assert.AreEqual(2, State().caret);

        bool changed = Frame(ref text, new NowTextInputFrame
        {
            composition = "か",
            backspaceHeld = true,
            enterHeld = true,
            escapePressed = true
        });

        Assert.IsFalse(changed, "Composition must not edit the text.");
        Assert.AreEqual("ab", text);
        Assert.AreEqual(2, State().caret);
        Assert.AreEqual(Id, NowFocus.focusedId, "Escape belongs to the IME while composing.");

        Frame(ref text, new NowTextInputFrame { characters = "か" });
        Assert.AreEqual("abか", text, "Committed characters insert normally.");
    }

    [Test]
    public void ImeEnablesOnFocusGainAndDisablesOnLoss()
    {
        var calls = new List<bool>();
        NowTextInput.setImeEnabled = enabled => calls.Add(enabled);

        string text = "ab";
        Focus();
        Frame(ref text);

        NowFocus.Clear();
        Frame(ref text);

        Assert.AreEqual(2, calls.Count);
        Assert.IsTrue(calls[0]);
        Assert.IsFalse(calls[1]);
    }

    [Test]
    public void TypingUnfocusedChangesNothing()
    {
        string text = "keep";

        Assert.IsFalse(Frame(ref text, new NowTextInputFrame { characters = "x", enterHeld = true }));
        Assert.AreEqual("keep", text);
    }
}
