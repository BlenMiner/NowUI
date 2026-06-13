using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// TextField, overlay/occlusion, ScrollView and Dropdown tests — all driven
/// through fake pointer and keyboard sources.
/// </summary>
public class NowControlsAdvancedTests
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
    static readonly NowRect FieldRect = new NowRect(20, 20, 200, 30);

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

    bool DrawTextFieldFrame(ref string text, NowTextInputFrame keys)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "name").Draw(ref text);

        return changed;
    }

    void FocusField()
    {
        NowFocus.Focus(NowControls.GetControlId("name"));
    }

    [Test]
    public void TextFieldTypesCharactersWhileFocused()
    {
        string text = string.Empty;
        FocusField();

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "Hi" }));
        Assert.AreEqual("Hi", text);

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "!" }));
        Assert.AreEqual("Hi!", text);
    }

    [Test]
    public void TextFieldIgnoresTypingWhenUnfocused()
    {
        string text = "keep";

        Assert.IsFalse(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "x" }));
        Assert.AreEqual("keep", text);
    }

    [Test]
    public void TextFieldBackspaceDeletes()
    {
        string text = "abc";
        FocusField();

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { backspaceHeld = true }));
        Assert.AreEqual("ab", text);
    }

    [Test]
    public void TextFieldSelectAllThenTypeReplaces()
    {
        string text = "old text";
        FocusField();

        DrawTextFieldFrame(ref text, new NowTextInputFrame { selectAllPressed = true, command = true });
        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "n" }));
        Assert.AreEqual("n", text);
    }

    [Test]
    public void TextFieldCopyPasteRoundTrip()
    {
        var previousSet = NowClipboard.setText;
        var previousGet = NowClipboard.getText;
        string clipboard = string.Empty;
        NowClipboard.setText = value => clipboard = value;
        NowClipboard.getText = () => clipboard;

        try
        {
            string text = "copyme";
            FocusField();

            DrawTextFieldFrame(ref text, new NowTextInputFrame { selectAllPressed = true, command = true });
            DrawTextFieldFrame(ref text, new NowTextInputFrame { copyPressed = true, command = true });
            Assert.AreEqual("copyme", clipboard);

            DrawTextFieldFrame(ref text, new NowTextInputFrame { pastePressed = true, command = true });
            Assert.AreEqual("copyme", text, "Paste over a full selection keeps the same text.");

            DrawTextFieldFrame(ref text, new NowTextInputFrame { pastePressed = true, command = true });
            Assert.AreEqual("copymecopyme", text, "Second paste appends at the caret.");
        }
        finally
        {
            NowClipboard.setText = previousSet;
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void TextFieldClickPlacesCaretAndEscapeBlurs()
    {
        string text = "hello";
        FocusField();

        _pointer.snapshot = new NowInputSnapshot(new Vector2(FieldRect.xMax - 4, 35), true, true, false);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.TextField(FieldRect, "name").Draw(ref text);

        _pointer.snapshot = default;
        DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "!" });
        Assert.AreEqual("hello!", text);

        DrawTextFieldFrame(ref text, new NowTextInputFrame { escapePressed = true });
        Assert.AreEqual(0, NowFocus.focusedId, "Escape must blur the field.");
    }

    [Test]
    public void TextFieldImeCompositionSuppressesKeysAndKeepsText()
    {
        string text = "ab";
        FocusField();

        Assert.IsFalse(DrawTextFieldFrame(ref text, new NowTextInputFrame
        {
            composition = "か",
            backspaceHeld = true,
            enterPressed = true
        }));

        Assert.AreEqual("ab", text, "Composition must not edit the text.");
        Assert.AreNotEqual(0, NowFocus.focusedId, "Enter belongs to the IME while composing.");

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "か" }));
        Assert.AreEqual("abか", text);
    }

    [Test]
    public void RaycastPressGateLatchesAtPressTime()
    {
        bool latch = true;

        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: false));

        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: false));
        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: true),
            "A press that began blocked stays blocked for the whole press even if the gate would now allow (no click-through).");
        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: true));

        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: true));

        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: true));
        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: false));
        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: false));
    }

    [Test]
    public void OverlayDeferredDrawRunsAtFlush()
    {
        bool ran = false;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowOverlay.Defer(new NowRect(0, 0, 100, 100), () =>
            {
                ran = true;
                Now.Rectangle(new NowRect(10, 10, 50, 50)).SetColor(Color.red).Draw();
            });

            Assert.IsFalse(ran, "Deferred draws must not run inline.");
        }

        Assert.IsTrue(ran, "Deferred draws must run when the capture flushes.");
        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void OverlayBlocksPointerUnderneathNextFrame()
    {
        var blocked = new NowRect(0, 0, 100, 100);

        using (NowInput.Begin(_pointer, Surface))
        {
            NowOverlay.Block(blocked);
            Assert.IsFalse(NowOverlay.IsPointerBlocked(new Vector2(50, 50)), "Blocking applies one frame late.");
        }

        NowOverlay.ForceNewFrame();

        Assert.IsTrue(NowOverlay.IsPointerBlocked(new Vector2(50, 50)));
        Assert.IsFalse(NowOverlay.IsPointerBlocked(new Vector2(200, 200)));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(50, 50), false, false, false);

        using (NowInput.Begin(_pointer, Surface))
        {
            var interaction = NowInput.Interact(99, new NowRect(0, 0, 100, 100));
            Assert.IsFalse(interaction.hovered);
        }
    }

    [Test]
    public void ScrollViewClampsAndStoresScroll()
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "list").Begin())
        {
            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(new NowLayoutOptions().SetSize(180, 30));
        }

        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(100, 50), new Vector2(100, 50), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            new Vector2(0f, -2f), Vector2.zero,
            false, false, false, false, false, false, 2, 2f);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "list").Begin())
        {
            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(new NowLayoutOptions().SetSize(180, 30));
        }

        int scrollId;

        using (NowInput.Begin(_pointer, Surface))
            scrollId = NowControls.GetControlId("list");

        ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
        Assert.Greater(scroll.y, 0f, "Wheel must scroll the content.");
        Assert.LessOrEqual(scroll.y, 200f, "Scroll must clamp to content - viewport.");
    }

    [Test]
    public void DropdownOpensAndAppliesPendingSelection()
    {
        var options = new List<string> { "Low", "Medium", "High" };
        int selected = 0;
        var rect = new NowRect(20, 20, 160, 30);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(60, 35), true, true, false);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Dropdown(rect, "quality", options).Draw(ref selected);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(60, 35), false, false, true);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Dropdown(rect, "quality", options).Draw(ref selected);

        int dropdownId;

        using (NowInput.Begin(_pointer, Surface))
            dropdownId = NowControls.GetControlId("quality");

        Assert.IsTrue(NowControlState.Get<bool>(dropdownId), "Click must open the dropdown.");

        NowControlState.Get<int>(NowInput.GetId(dropdownId, "pending")) = 3;

        _pointer.snapshot = default;
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.Dropdown(rect, "quality", options).Draw(ref selected);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, selected, "Pending selection 3 maps to index 2.");
    }
}
