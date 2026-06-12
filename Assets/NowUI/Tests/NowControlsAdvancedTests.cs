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

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect FieldRect = new NowRect(20, 20, 200, 30);

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowUIDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
        NowUIOverlay.Reset();
        NowUITextInput.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowUITextInput.source = _keyboard;
        _drawList = new NowUIDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowUITextInput.Reset();
        NowUIOverlay.Reset();
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
    }

    bool DrawTextFieldFrame(ref string text, NowUITextInputFrame keys)
    {
        _keyboard.frame = keys;
        NowUITextInput.Invalidate();
        bool changed;

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "name").Draw(ref text);

        return changed;
    }

    void FocusField()
    {
        NowUIFocus.Focus(NowControls.GetControlId("name"));
    }

    [Test]
    public void TextFieldTypesCharactersWhileFocused()
    {
        string text = string.Empty;
        FocusField();

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowUITextInputFrame { characters = "Hi" }));
        Assert.AreEqual("Hi", text);

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowUITextInputFrame { characters = "!" }));
        Assert.AreEqual("Hi!", text);
    }

    [Test]
    public void TextFieldIgnoresTypingWhenUnfocused()
    {
        string text = "keep";

        Assert.IsFalse(DrawTextFieldFrame(ref text, new NowUITextInputFrame { characters = "x" }));
        Assert.AreEqual("keep", text);
    }

    [Test]
    public void TextFieldBackspaceDeletes()
    {
        string text = "abc";
        FocusField();

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowUITextInputFrame { backspaceHeld = true }));
        Assert.AreEqual("ab", text);
    }

    [Test]
    public void TextFieldSelectAllThenTypeReplaces()
    {
        string text = "old text";
        FocusField();

        DrawTextFieldFrame(ref text, new NowUITextInputFrame { selectAllPressed = true, command = true });
        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowUITextInputFrame { characters = "n" }));
        Assert.AreEqual("n", text);
    }

    [Test]
    public void TextFieldCopyPasteRoundTrip()
    {
        // Batch-mode editors can lack a clipboard entirely; probe before testing.
        GUIUtility.systemCopyBuffer = "probe";

        if (GUIUtility.systemCopyBuffer != "probe")
            Assert.Ignore("System clipboard is not functional in this environment.");

        string text = "copyme";
        FocusField();

        DrawTextFieldFrame(ref text, new NowUITextInputFrame { selectAllPressed = true, command = true });
        DrawTextFieldFrame(ref text, new NowUITextInputFrame { copyPressed = true, command = true });
        Assert.AreEqual("copyme", GUIUtility.systemCopyBuffer);

        DrawTextFieldFrame(ref text, new NowUITextInputFrame { pastePressed = true, command = true });
        Assert.AreEqual("copyme", text, "Paste over a full selection keeps the same text.");

        DrawTextFieldFrame(ref text, new NowUITextInputFrame { pastePressed = true, command = true });
        Assert.AreEqual("copymecopyme", text, "Second paste appends at the caret.");
    }

    [Test]
    public void TextFieldClickPlacesCaretAndEscapeBlurs()
    {
        string text = "hello";
        FocusField();

        // Click far right: caret lands at the end.
        _pointer.snapshot = new NowUIInputSnapshot(new Vector2(FieldRect.xMax - 4, 35), true, true, false);

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.TextField(FieldRect, "name").Draw(ref text);

        _pointer.snapshot = default;
        DrawTextFieldFrame(ref text, new NowUITextInputFrame { characters = "!" });
        Assert.AreEqual("hello!", text);

        DrawTextFieldFrame(ref text, new NowUITextInputFrame { escapePressed = true });
        Assert.AreEqual(0, NowUIFocus.focusedId, "Escape must blur the field.");
    }

    [Test]
    public void RaycastPressGateLatchesAtPressTime()
    {
        bool latch = true;

        // Idle over occluding UGUI: invisible.
        Assert.IsFalse(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: false));

        // Press begins while blocked: invisible, and stays invisible for the whole
        // press even if the gate would now allow (no click-through).
        Assert.IsFalse(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: false));
        Assert.IsFalse(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: true));
        Assert.IsFalse(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: true));

        // After release the gate re-evaluates.
        Assert.IsTrue(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: true));

        // Press begins while allowed, drags under occluding UGUI: keeps tracking,
        // and the release still arrives.
        Assert.IsTrue(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: true));
        Assert.IsTrue(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: false));
        Assert.IsTrue(NowUIRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: false));
    }

    [Test]
    public void OverlayDeferredDrawRunsAtFlush()
    {
        bool ran = false;

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowUIOverlay.Defer(new NowRect(0, 0, 100, 100), () =>
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

        using (NowUIInput.Begin(_pointer, Surface))
        {
            NowUIOverlay.Block(blocked);
            Assert.IsFalse(NowUIOverlay.IsPointerBlocked(new Vector2(50, 50)), "Blocking applies one frame late.");
        }

        NowUIOverlay.ForceNewFrame();

        Assert.IsTrue(NowUIOverlay.IsPointerBlocked(new Vector2(50, 50)));
        Assert.IsFalse(NowUIOverlay.IsPointerBlocked(new Vector2(200, 200)));

        // A control under the blocked area must not hover.
        _pointer.snapshot = new NowUIInputSnapshot(new Vector2(50, 50), false, false, false);

        using (NowUIInput.Begin(_pointer, Surface))
        {
            var interaction = NowUIInput.Interact(99, new NowRect(0, 0, 100, 100));
            Assert.IsFalse(interaction.hovered);
        }
    }

    [Test]
    public void ScrollViewClampsAndStoresScroll()
    {
        // Frame 1: populate the layout cache with tall content.
        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "list").Begin())
        {
            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(new NowLayoutOptions().SetSize(180, 30));
        }

        // Frame 2: wheel down scrolls within bounds.
        _pointer.snapshot = new NowUIInputSnapshot(
            true, new Vector2(100, 50), new Vector2(100, 50), Vector2.zero,
            NowUIPointerButtons.None, NowUIPointerButtons.None, NowUIPointerButtons.None,
            new Vector2(0f, -2f), Vector2.zero,
            false, false, false, false, false, false, 2, 2f);

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "list").Begin())
        {
            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(new NowLayoutOptions().SetSize(180, 30));
        }

        int scrollId;

        using (NowUIInput.Begin(_pointer, Surface))
            scrollId = NowControls.GetControlId("list");

        ref Vector2 scroll = ref NowUIControlState.Get<Vector2>(scrollId);
        Assert.Greater(scroll.y, 0f, "Wheel must scroll the content.");
        Assert.LessOrEqual(scroll.y, 200f, "Scroll must clamp to content - viewport.");
    }

    [Test]
    public void DropdownOpensAndAppliesPendingSelection()
    {
        var options = new List<string> { "Low", "Medium", "High" };
        int selected = 0;
        var rect = new NowRect(20, 20, 160, 30);

        // Click the field: opens.
        _pointer.snapshot = new NowUIInputSnapshot(new Vector2(60, 35), true, true, false);

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Dropdown(rect, "quality", options).Draw(ref selected);

        _pointer.snapshot = new NowUIInputSnapshot(new Vector2(60, 35), false, false, true);

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Dropdown(rect, "quality", options).Draw(ref selected);

        int dropdownId;

        using (NowUIInput.Begin(_pointer, Surface))
            dropdownId = NowControls.GetControlId("quality");

        Assert.IsTrue(NowUIControlState.Get<bool>(dropdownId), "Click must open the dropdown.");

        // Simulate the popup writing a pending selection (as the deferred item click does).
        NowUIControlState.Get<int>(NowUIInput.GetId(dropdownId, "pending")) = 3;

        _pointer.snapshot = default;
        bool changed;

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.Dropdown(rect, "quality", options).Draw(ref selected);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, selected, "Pending selection 3 maps to index 2.");
    }
}
