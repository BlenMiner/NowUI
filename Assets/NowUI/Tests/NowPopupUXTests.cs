using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using NowUI;

/// <summary>
/// Popup and overlay UX tests: modal outside-press consumption, the combo box
/// filter staying typable on the popup's focus layer, dropdown keyboard
/// driving, one-press-one-layer dismissal for nested overlays, and key-capture
/// cancel consumption — driven frame by frame with
/// <c>NowOverlay.ForceNewFrame</c> so the one-frame-late pointer blocks engage.
/// </summary>
public class NowPopupUXTests
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

    sealed class FakeKeys : INowKeyInputSource
    {
        public NowKeyInputFrame frame;

        public bool TryGetFrame(out NowKeyInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(600, 600);
    static readonly NowRect FieldRect = new NowRect(20, 20, 160, 30);
    static readonly NowRect ButtonRect = new NowRect(20, 320, 140, 32);
    static readonly NowRect ViewPopupRect = new NowRect(80f, 40f, 220f, 160f);
    static readonly List<string> Options = new List<string> { "Low", "Medium", "High" };

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    FakeKeys _keys;
    NowDrawList _drawList;
    int _frame;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowTextInput.Reset();
        NowKeyInput.Reset();
        NowLayout.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        _keys = new FakeKeys();
        NowTextInput.source = _keyboard;
        NowKeyInput.source = _keys;
        _drawList = new NowDrawList();
        _frame = 10;
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowKeyInput.Reset();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    NowInputSnapshot Snapshot(
        Vector2 position,
        bool down = false,
        bool pressed = false,
        bool released = false,
        Vector2 navigation = default,
        bool submitPressed = false,
        bool cancelPressed = false)
    {
        ++_frame;

        return new NowInputSnapshot(
            true, position, position, Vector2.zero,
            NowInputSnapshot.ToButtonMask(down, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(pressed, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(released, NowPointerButton.Primary),
            Vector2.zero, navigation,
            false, false,
            submitPressed, submitPressed, false,
            cancelPressed, cancelPressed, false,
            _frame, _frame * 0.25f);
    }

    int ResolveControlId(string id)
    {
        using (NowInput.Begin(_pointer, Surface))
            return NowControls.GetControlId(id);
    }

    bool DrawComboFrame(ref int selected, NowInputSnapshot snapshot, string typed = null)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keyboard.frame = new NowTextInputFrame { characters = typed };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.ComboBox(FieldRect, Options).SetId("combo").Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    bool DrawComboStringFrame(ref string selected, NowInputSnapshot snapshot, string typed = null)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keyboard.frame = new NowTextInputFrame { characters = typed };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.ComboBox(FieldRect, Options)
                .SetId("combo-string")
                .SetAllowCustomValue()
                .Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    [Test]
    public void ComboBoxFilterStaysTypableWhileThePopupBlockIsActive()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;

        DrawComboFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawComboFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawComboFrame(ref selected, Snapshot(fieldCenter));
        DrawComboFrame(ref selected, Snapshot(fieldCenter));

        DrawComboFrame(ref selected, Snapshot(fieldCenter), typed: "med");
        DrawComboFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(
            DrawComboFrame(ref selected, Snapshot(fieldCenter)),
            "Typing on the 3rd+ open frame must reach the filter and submit its first match.");
        Assert.AreEqual(1, selected, "The filter 'med' must select Medium.");
    }

    [Test]
    public void ComboBoxStringModeCanCommitCustomText()
    {
        string selected = string.Empty;
        var fieldCenter = FieldRect.center;

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter), typed: "Custom.Method");
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(
            DrawComboStringFrame(ref selected, Snapshot(fieldCenter)),
            "String combo boxes with custom values must apply the committed filter on the next Draw.");
        Assert.AreEqual("Custom.Method", selected);
    }

    [Test]
    public void ComboBoxStringModePrefersOptionMatchesBeforeCustomText()
    {
        string selected = string.Empty;
        var fieldCenter = FieldRect.center;

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter), typed: "med");
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(DrawComboStringFrame(ref selected, Snapshot(fieldCenter)));
        Assert.AreEqual("Medium", selected);
    }

    bool DrawDropdownAndButtonFrame(ref int selected, NowInputSnapshot snapshot)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        bool buttonClicked;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Now.Dropdown(FieldRect, "dd", Options).Draw(ref selected);
            buttonClicked = Now.Button(ButtonRect, "Outside").SetId("btn").Draw();
            NowOverlay.Flush();
        }

        return buttonClicked;
    }

    [Test]
    public void DropdownOutsidePressDismissesWithoutActivatingTheControlBeneath()
    {
        int selected = 0;
        var buttonCenter = ButtonRect.center;

        Assert.IsFalse(DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, down: true, pressed: true)));
        Assert.IsTrue(
            DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, released: true)),
            "With no popup open, the button must click.");

        int dropdownId = ResolveControlId("dd");
        NowControlState.Get<bool>(dropdownId) = true;

        DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter));
        DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter));

        bool pressClicked = DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, down: true, pressed: true));
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "The outside press must dismiss the popup.");

        bool releaseClicked = DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, released: true));

        Assert.IsFalse(pressClicked || releaseClicked, "The dismissing press must not activate the button beneath.");
    }

    [Test]
    public void DropdownFieldPressWhileOpenClosesWithoutReopening()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;
        int dropdownId = ResolveControlId("dd");

        NowControlState.Get<bool>(dropdownId) = true;
        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter));
        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter));

        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "Pressing the field while open must close the popup.");

        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter));

        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "The release must not toggle the popup back open.");
    }

    bool DrawDropdownFrame(ref int selected, NowInputSnapshot snapshot)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keyboard.frame = default;
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.Dropdown(FieldRect, "dd", Options).Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    [Test]
    public void DropdownArrowsMoveHighlightFromSelectionAndSubmitCommits()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;
        var down = new Vector2(0f, -1f);

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, released: true));

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, navigation: down));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsFalse(NowControlState.Get<bool>(ResolveControlId("dd")), "Submit must close the popup.");
        Assert.IsTrue(
            DrawDropdownFrame(ref selected, Snapshot(fieldCenter)),
            "The committed highlight must apply on the next Draw.");
        Assert.AreEqual(1, selected, "One down pulse from the selected item must highlight the next option.");
    }

    [Test]
    public void DropdownTypeToSelectJumpsToTheMatchingOption()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, released: true));

        NowOverlay.ForceNewFrame();
        _pointer.snapshot = Snapshot(fieldCenter);
        _keyboard.frame = new NowTextInputFrame { characters = "h" };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Now.Dropdown(FieldRect, "dd", Options).Draw(ref selected);
            NowOverlay.Flush();
        }

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(DrawDropdownFrame(ref selected, Snapshot(fieldCenter)));
        Assert.AreEqual(2, selected, "Typing 'h' must highlight High and submit must commit it.");
    }

    sealed class NestedDropdownView : INowView
    {
        public int selected;

        public void Draw(NowViewContext context)
        {
            Now.Dropdown(
                    new NowRect(context.rect.x + 10f, context.rect.y + 10f, 140f, 28f),
                    "nested-dd",
                    Options)
                .Draw(ref selected);
        }
    }

    void DrawStackFrame(NowViewStack stack, NowInputSnapshot snapshot)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            stack.Draw(new NowRect(0f, 0f, Surface.x, Surface.y));
            NowOverlay.Flush();
        }
    }

    [Test]
    public void CancelClosesTheNestedDropdownBeforeThePopupView()
    {
        var stack = new NowViewStack();
        var idle = new Vector2(500f, 500f);
        int dropdownId = ResolveControlId("nested-dd");

        stack.Push(new NestedDropdownView(), NowViewOptions.Popup(ViewPopupRect, NowViewTransitionPreset.None, 0f));
        NowControlState.Get<bool>(dropdownId) = true;

        DrawStackFrame(stack, Snapshot(idle));

        DrawStackFrame(stack, Snapshot(idle, cancelPressed: true));
        Assert.AreEqual(1, stack.count, "Cancel must close only the nested dropdown, not the popup view.");
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "Cancel must close the nested dropdown.");

        DrawStackFrame(stack, Snapshot(idle));
        DrawStackFrame(stack, Snapshot(idle, cancelPressed: true));
        Assert.AreEqual(0, stack.count, "With no nested overlay left, cancel must close the popup view.");
    }

    [Test]
    public void OutsidePressClosesTheNestedDropdownBeforeThePopupView()
    {
        var stack = new NowViewStack();
        var outside = new Vector2(500f, 500f);
        int dropdownId = ResolveControlId("nested-dd");

        stack.Push(new NestedDropdownView(), NowViewOptions.Popup(ViewPopupRect, NowViewTransitionPreset.None, 0f));
        NowControlState.Get<bool>(dropdownId) = true;

        DrawStackFrame(stack, Snapshot(outside));

        DrawStackFrame(stack, Snapshot(outside, down: true, pressed: true));
        Assert.AreEqual(1, stack.count, "An outside press must dismiss only the nested dropdown.");
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId));

        DrawStackFrame(stack, Snapshot(outside, released: true));
        DrawStackFrame(stack, Snapshot(outside, down: true, pressed: true));
        Assert.AreEqual(0, stack.count, "The next outside press must close the popup view.");
    }

    sealed class KeyBindingView : INowView
    {
        public Key value = Key.E;

        public void Draw(NowViewContext context)
        {
            Now.KeyBindingField(new NowRect(context.rect.x + 10f, context.rect.y + 10f, 140f, 30f), "bind")
                .Draw(ref value);
        }
    }

    void DrawBindingFrame(NowViewStack stack, NowInputSnapshot snapshot, Key pressed = Key.None)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keys.frame = new NowKeyInputFrame { pressedKey = pressed };
        NowKeyInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            stack.Draw(new NowRect(0f, 0f, Surface.x, Surface.y));
            NowOverlay.Flush();
        }
    }

    [Test]
    public void EscapeCancellingKeyCaptureDoesNotCloseTheEnclosingPopup()
    {
        var stack = new NowViewStack();
        var view = new KeyBindingView();
        var insideField = new Vector2(ViewPopupRect.x + 20f, ViewPopupRect.y + 20f);

        stack.Push(view, NowViewOptions.Popup(ViewPopupRect, NowViewTransitionPreset.None, 0f));

        DrawBindingFrame(stack, Snapshot(insideField, down: true, pressed: true));
        DrawBindingFrame(stack, Snapshot(insideField, released: true));

        DrawBindingFrame(stack, Snapshot(insideField, cancelPressed: true), Key.Escape);

        Assert.AreEqual(1, stack.count, "Escape that cancels a key capture must not also close the popup.");
        Assert.AreEqual(Key.E, view.value, "Escape must cancel the capture without rebinding.");

        DrawBindingFrame(stack, Snapshot(insideField));
        DrawBindingFrame(stack, Snapshot(insideField, cancelPressed: true));

        Assert.AreEqual(0, stack.count, "With no capture in progress, cancel must close the popup.");
    }
}
