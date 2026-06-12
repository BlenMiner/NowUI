using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;

public class NowTextSelectionTests
{
    sealed class FakePointer : INowUIInputProvider
    {
        public NowUIInputSnapshot snapshot;

        public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowUITextInputSource
    {
        public NowUITextInputFrame frame;

        public bool TryGetFrame(out NowUITextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    const string Text = "hello world wide";
    const float Size = 16f;
    static readonly Vector2 Surface = new Vector2(512, 256);

    NowFontAsset _font;
    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowUIDrawList _drawList;
    List<NowTextSelectionLine> _lines;
    string _copied;
    System.Action<string> _previousCopy;

    [OneTimeSetUp]
    public void LoadFont()
    {
        _font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(_font, "Default font resource missing.");
    }

    [SetUp]
    public void SetUp()
    {
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
        NowUITextInput.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowUITextInput.source = _keyboard;
        _drawList = new NowUIDrawList();
        _lines = new List<NowTextSelectionLine>
        {
            new NowTextSelectionLine { rect = new NowRect(0, 0, 300, 20), start = 0, length = Text.Length }
        };

        _copied = null;
        _previousCopy = NowUIClipboard.setText;
        NowUIClipboard.setText = text => _copied = text;
    }

    [TearDown]
    public void TearDown()
    {
        NowUIClipboard.setText = _previousCopy;
        _drawList.Dispose();
        NowUITextInput.Reset();
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
    }

    NowTextSelectionResult Frame(Vector2 pointer, bool down, bool pressed, bool released, NowUITextInputFrame keys = default, bool forceFocusFrame = false)
    {
        _keyboard.frame = keys;
        NowUITextInput.Invalidate();
        _pointer.snapshot = new NowUIInputSnapshot(pointer, down, pressed, released);
        return RunFrame(forceFocusFrame);
    }

    NowTextSelectionResult RightClickFrame(Vector2 pointer)
    {
        _keyboard.frame = default;
        NowUITextInput.Invalidate();
        _pointer.snapshot = new NowUIInputSnapshot(
            pointer,
            NowUIPointerButtons.Secondary,
            NowUIPointerButtons.Secondary,
            NowUIPointerButtons.None);
        return RunFrame(false);
    }

    NowTextSelectionResult RunFrame(bool forceFocusFrame)
    {
        NowTextSelectionResult result;

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            if (forceFocusFrame)
                NowUIFocus.ForceNewFrame();

            result = NowTextSelection.Draw(
                42, Text, _lines, _font, Size, NowFontStyle.Regular, new Vector4(0f, 0f, 1f, 0.3f));
        }

        return result;
    }

    float XAt(string prefix)
    {
        return _font.MeasureText(prefix, Size).x + 1f;
    }

    [Test]
    public void DragSelectsAndCtrlCCopies()
    {
        var fromX = new Vector2(XAt("hello "), 10f);
        var toX = new Vector2(XAt("hello world"), 10f);

        Frame(fromX, down: true, pressed: true, released: false);
        bool selected = Frame(toX, down: true, pressed: false, released: false).hasSelection;
        Assert.IsTrue(selected, "drag must produce a selection");

        Frame(toX, down: false, pressed: false, released: true);
        Frame(toX, down: false, pressed: false, released: false, new NowUITextInputFrame { copyPressed = true });

        Assert.AreEqual("world", _copied);
    }

    [Test]
    public void DoubleClickSelectsWordAndSelectAllSelectsEverything()
    {
        var insideWorld = new Vector2(XAt("hello wo"), 10f);

        Frame(insideWorld, down: true, pressed: true, released: false);
        Frame(insideWorld, down: false, pressed: false, released: true);
        bool selected = Frame(insideWorld, down: true, pressed: true, released: false).hasSelection;
        Assert.IsTrue(selected, "double click must select the word");

        Frame(insideWorld, down: false, pressed: false, released: true);
        Frame(insideWorld, down: false, pressed: false, released: false, new NowUITextInputFrame { copyPressed = true });
        Assert.AreEqual("world", _copied);

        Frame(insideWorld, down: false, pressed: false, released: false, new NowUITextInputFrame { selectAllPressed = true });
        Frame(insideWorld, down: false, pressed: false, released: false, new NowUITextInputFrame { copyPressed = true });
        Assert.AreEqual(Text, _copied);
    }

    [Test]
    public void TripleClickSelectsTheLine()
    {
        var insideWorld = new Vector2(XAt("hello wo"), 10f);

        for (int i = 0; i < 3; ++i)
        {
            Frame(insideWorld, down: true, pressed: true, released: false);
            Frame(insideWorld, down: false, pressed: false, released: true);
        }

        Frame(insideWorld, down: false, pressed: false, released: false, new NowUITextInputFrame { copyPressed = true });
        Assert.AreEqual(Text, _copied, "Triple-click selects the whole line.");
    }

    [Test]
    public void ClickingOutsideClearsTheSelection()
    {
        var fromX = new Vector2(XAt("hello "), 10f);
        var toX = new Vector2(XAt("hello world"), 10f);

        Frame(fromX, down: true, pressed: true, released: false);
        Frame(toX, down: true, pressed: false, released: false);
        Frame(toX, down: false, pressed: false, released: true);

        var outside = new Vector2(450f, 200f);
        Frame(outside, down: true, pressed: true, released: false, forceFocusFrame: true);
        Frame(outside, down: false, pressed: false, released: true);
        bool selected = Frame(outside, down: false, pressed: false, released: false).hasSelection;

        Assert.IsFalse(selected, "clicking empty space must clear the selection");

        Frame(outside, down: false, pressed: false, released: false, new NowUITextInputFrame { copyPressed = true });
        Assert.IsNull(_copied, "no selection means nothing to copy");
    }

    [Test]
    public void RightClickReportsAndPreservesTheSelection()
    {
        var fromX = new Vector2(XAt("hello "), 10f);
        var toX = new Vector2(XAt("hello world"), 10f);

        Frame(fromX, down: true, pressed: true, released: false);
        Frame(toX, down: true, pressed: false, released: false);
        Frame(toX, down: false, pressed: false, released: true);

        var result = RightClickFrame(new Vector2(XAt("hello wo"), 10f));

        Assert.IsTrue(result.rightClicked, "secondary press inside the region must report");
        Assert.IsTrue(result.hasSelection, "right-clicking must not destroy the selection");

        Frame(toX, down: false, pressed: false, released: false, new NowUITextInputFrame { copyPressed = true });
        Assert.AreEqual("world", _copied, "the selection survives the right-click");
    }
}
