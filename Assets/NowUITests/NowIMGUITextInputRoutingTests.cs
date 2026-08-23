using System;
using System.Reflection;
using NowUI;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// End-to-end coverage for the Editor IMGUI keyboard boundary. Unity routes
/// keyboard events as Ignore for a passive host control even though the native
/// event is still KeyDown/KeyUp, so these tests deliberately preserve that
/// distinction while drawing a real focused text field.
/// </summary>
public class NowIMGUITextInputRoutingTests
{
    static readonly FieldInfo InputSnapshotField = typeof(NowInput).GetField(
        "_snapshot",
        BindingFlags.NonPublic | BindingFlags.Static);

    static readonly MethodInfo GetEntryMethod = typeof(NowGUI).GetMethod(
        "GetEntry",
        BindingFlags.NonPublic | BindingFlags.Static);

    static readonly Vector2 SurfaceSize = new Vector2(320f, 120f);
    static readonly NowInputSurface Surface = new NowInputSurface(SurfaceSize);
    static readonly NowRect FieldRect = new NowRect(20f, 20f, 240f, 32f);

    NowIMGUIInputProvider _provider;
    NowDrawList _drawList;
    Event _previousEvent;
    bool _previousGUIChanged;
    int _previousHotControl;
    Action _previousRepaintRequested;
    Action<NowIMGUIInputProvider> _previousHostRepaintRequested;
    Action<NowIMGUIInputProvider, float> _previousHostRepaintAfterRequested;
    NowResolvedId _fieldId;
    NowResolvedId _areaId;

    NowResolvedId FieldId => _fieldId;

    [SetUp]
    public void SetUp()
    {
        _previousEvent = Event.current;
        _previousGUIChanged = GUI.changed;
        _previousHotControl = GUIUtility.hotControl;
        _previousRepaintRequested = NowIMGUIInputProvider.repaintRequested;
        _previousHostRepaintRequested = NowIMGUIInputProvider.hostRepaintRequested;
        _previousHostRepaintAfterRequested = NowIMGUIInputProvider.hostRepaintAfterRequested;

        Event.current = null;
        GUI.changed = false;
        GUIUtility.hotControl = 0;
        NowIMGUIInputProvider.repaintRequested = () => { };
        NowIMGUIInputProvider.hostRepaintRequested = _ => { };
        NowIMGUIInputProvider.hostRepaintAfterRequested = (_, __) => { };

        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowTextInput.Reset();
        NowTextUndoRegistry.Reset();
        NowLayout.Reset();

        _provider = new NowIMGUIInputProvider(18101, new object());
        _drawList = new NowDrawList();
        _fieldId = ResolveId("native-field");
        _areaId = ResolveId("native-area");
    }

    [TearDown]
    public void TearDown()
    {
        _provider.ResetState(releaseNativeCapture: false);
        _drawList.Dispose();

        NowTextUndoRegistry.Reset();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowFocus.Reset();
        NowInput.Reset();

        NowIMGUIInputProvider.repaintRequested = _previousRepaintRequested;
        NowIMGUIInputProvider.hostRepaintRequested = _previousHostRepaintRequested;
        NowIMGUIInputProvider.hostRepaintAfterRequested = _previousHostRepaintAfterRequested;
        GUIUtility.hotControl = _previousHotControl;
        GUI.changed = _previousGUIChanged;
        Event.current = _previousEvent;
    }

    [Test]
    public void PassiveHostRoutedIgnoreNativeCharacterIsInsertedExactlyOnce()
    {
        string text = string.Empty;
        FocusAndPrime(ref text, 0);
        var keyDown = KeyEvent(EventType.KeyDown, KeyCode.A, 'a');

        NowTextFieldResult result = Draw(ref text, keyDown, EventType.Ignore);

        Assert.IsTrue(result.changed);
        Assert.AreEqual("a", text);
        Assert.AreEqual(EventType.Used, keyDown.type,
            "The focused field must claim the native KeyDown from sibling IMGUI controls.");

        Draw(ref text, NativeEvent(EventType.Repaint), EventType.Repaint);
        Assert.AreEqual("a", text,
            "A character packet must be spent after its input pass and never replay on Repaint.");
    }

    [Test]
    public void NativeSpaceIsTextAndNeverGenericSubmit()
    {
        string text = "ab";
        FocusAndPrime(ref text, 1);
        var keyDown = KeyEvent(EventType.KeyDown, KeyCode.Space, ' ');

        NowTextFieldResult result = Draw(ref text, keyDown, EventType.Ignore);

        Assert.IsTrue(result.changed);
        Assert.IsFalse(result.submitted);
        Assert.AreEqual("a b", text);
        Assert.AreEqual(FieldId, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, keyDown.type);
    }

    [TestCase(KeyCode.Backspace, "acd", 1)]
    [TestCase(KeyCode.Delete, "abd", 2)]
    public void PassiveHostRoutedIgnoreNativeDeletionEditsFocusedFieldOnce(
        KeyCode keyCode,
        string expectedText,
        int expectedCaret)
    {
        string text = "abcd";
        FocusAndPrime(ref text, 2);
        var keyDown = KeyEvent(EventType.KeyDown, keyCode);

        NowTextFieldResult result = Draw(ref text, keyDown, EventType.Ignore);

        Assert.IsTrue(result.changed);
        Assert.AreEqual(expectedText, text);
        Assert.AreEqual(expectedCaret, State().caret);
        Assert.AreEqual(FieldId, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, keyDown.type);

        Draw(ref text, NativeEvent(EventType.Repaint), EventType.Repaint);
        Assert.AreEqual(expectedText, text,
            "The held-key state may survive until KeyUp, but an immediate Repaint must not repeat the edit.");
    }

    [TestCase(KeyCode.LeftArrow, 1)]
    [TestCase(KeyCode.RightArrow, 3)]
    [TestCase(KeyCode.Home, 0)]
    [TestCase(KeyCode.End, 4)]
    public void PassiveHostRoutedIgnoreNativeCaretKeyMovesInsideFocusedField(
        KeyCode keyCode,
        int expectedCaret)
    {
        string text = "abcd";
        FocusAndPrime(ref text, 2);
        var keyDown = KeyEvent(EventType.KeyDown, keyCode);

        NowTextFieldResult result = Draw(ref text, keyDown, EventType.Ignore);

        Assert.IsFalse(result.changed);
        Assert.AreEqual("abcd", text);
        Assert.AreEqual(expectedCaret, State().caret);
        Assert.AreEqual(expectedCaret, State().anchor);
        Assert.AreEqual(FieldId, NowFocus.focusedResolvedId,
            "Caret navigation belongs to the editor and must not move control focus.");
        Assert.AreEqual(EventType.Used, keyDown.type);
    }

    [TestCase(KeyCode.Return)]
    [TestCase(KeyCode.KeypadEnter)]
    public void PassiveHostRoutedIgnoreNativeEnterSubmitsBlursAndConsumes(KeyCode keyCode)
    {
        string text = "ready";
        FocusAndPrime(ref text, text.Length);
        var keyDown = KeyEvent(EventType.KeyDown, keyCode, '\n');

        NowTextFieldResult result = Draw(ref text, keyDown, EventType.Ignore);

        Assert.IsTrue(result.submitted);
        Assert.IsFalse(result.changed);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId,
            "Submitting a single-line field must leave editing mode so its caret disappears.");
        Assert.AreEqual(EventType.Used, keyDown.type);

        Draw(ref text, KeyEvent(EventType.KeyUp, keyCode), EventType.Ignore);
        Draw(ref text, NativeEvent(EventType.Layout), EventType.Layout);
        Draw(ref text, NativeEvent(EventType.Repaint), EventType.Repaint);

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId,
            "Following IMGUI passes must not silently reacquire the submitted field.");

        var character = KeyEvent(EventType.KeyDown, KeyCode.X, 'x');
        NowTextFieldResult blurred = Draw(ref text, character, EventType.Ignore);

        Assert.IsFalse(blurred.changed);
        Assert.AreEqual("ready", text);
        Assert.AreEqual(EventType.KeyDown, character.type,
            "A blurred field must leave later native keys available to the Editor host.");
    }

    [TestCase(KeyCode.Return)]
    [TestCase(KeyCode.KeypadEnter)]
    public void NativeEnterBlursNumericFieldAndKeepsItBlurred(KeyCode keyCode)
    {
        float value = 1200f;
        NowFocus.Focus(FieldId);
        DrawFloat(ref value, NativeEvent(EventType.Layout), EventType.Layout);
        var keyDown = KeyEvent(EventType.KeyDown, keyCode, '\n');

        NowTextFieldResult submitted = DrawFloat(ref value, keyDown, EventType.Ignore);

        Assert.IsTrue(submitted.submitted);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, keyDown.type);

        DrawFloat(ref value, KeyEvent(EventType.KeyUp, keyCode), EventType.Ignore);
        DrawFloat(ref value, NativeEvent(EventType.Repaint), EventType.Repaint);

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId,
            "Numeric overloads must preserve Enter blur across the following Editor pass.");
        Assert.AreEqual(1200f, value);
    }

    [Test]
    public void NativeEnterBlurPersistsAcrossRetainedHostPasses()
    {
        const int HostControlId = 18102;
        var context = new object();
        string text = "ready";
        NowResolvedId fieldId = NowResolvedId.None;

        Assert.NotNull(GetEntryMethod, "NowGUI cache lookup test seam was not found.");
        var entry = (NowGUI.CacheEntry)GetEntryMethod.Invoke(
            null,
            new object[] { context, HostControlId });

        NowTextFieldResult HostPass(Event inputEvent, EventType routedType)
        {
            Event.current = null;

            using (NowGUI.AutoForEvent(
                context,
                HostControlId,
                new Rect(0f, 0f, SurfaceSize.x, SurfaceSize.y),
                Color.clear,
                1f,
                repaint: false,
                hostFocused: true,
                trackInputRepaint: true))
            {
                Assert.IsTrue(entry.inputProvider.TryGetSnapshot(
                    Surface,
                    inputEvent,
                    routedType,
                    ownsCapture: false,
                    out var snapshot));
                InputSnapshotField.SetValue(null, snapshot);
                fieldId = NowControls.GetControlId("retained-native-field");
                return Now.TextField(FieldRect, fieldId).Draw(ref text);
            }
        }

        try
        {
            HostPass(NativeEvent(EventType.Layout), EventType.Layout);
            NowFocus.Focus(fieldId);

            var enter = KeyEvent(EventType.KeyDown, KeyCode.Return, '\n');
            NowTextFieldResult submitted = HostPass(enter, EventType.Ignore);

            Assert.IsTrue(submitted.submitted);
            Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
            Assert.AreEqual(EventType.Used, enter.type);

            HostPass(KeyEvent(EventType.KeyUp, KeyCode.Return), EventType.Ignore);
            HostPass(NativeEvent(EventType.Layout), EventType.Layout);
            HostPass(NativeEvent(EventType.Repaint), EventType.Repaint);

            Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId,
                "The retained host must not restore a field after Enter blurred it.");

            var character = KeyEvent(EventType.KeyDown, KeyCode.X, 'x');
            NowTextFieldResult blurred = HostPass(character, EventType.Ignore);

            Assert.IsFalse(blurred.changed);
            Assert.AreEqual("ready", text);
            Assert.AreEqual(EventType.KeyDown, character.type);
        }
        finally
        {
            NowGUI.DisposeAll();
        }
    }

    [Test]
    public void NativeEnterKeyUpRearmsASecondSubmit()
    {
        string text = "ready";
        FocusAndPrime(ref text, text.Length);

        NowTextFieldResult first = Draw(
            ref text,
            KeyEvent(EventType.KeyDown, KeyCode.Return, '\n'),
            EventType.Ignore);
        NowTextFieldResult released = Draw(
            ref text,
            KeyEvent(EventType.KeyUp, KeyCode.Return),
            EventType.Ignore);

        Assert.IsTrue(first.submitted);
        Assert.IsFalse(released.submitted);

        FocusAndPrime(ref text, text.Length);
        var secondKeyDown = KeyEvent(EventType.KeyDown, KeyCode.Return, '\n');
        NowTextFieldResult second = Draw(ref text, secondKeyDown, EventType.Ignore);

        Assert.IsTrue(second.submitted,
            "The native KeyUp must clear both provider and text-editor Enter suppression.");
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, secondKeyDown.type);
    }

    [Test]
    public void NativeEscapeRevertsBlursAndConsumes()
    {
        string text = "start";
        FocusAndPrime(ref text, text.Length);
        Draw(
            ref text,
            KeyEvent(EventType.KeyDown, KeyCode.X, 'x'),
            EventType.Ignore);
        Assert.AreEqual("startx", text);

        var escape = KeyEvent(EventType.KeyDown, KeyCode.Escape);
        NowTextFieldResult result = Draw(ref text, escape, EventType.Ignore);

        Assert.IsFalse(result.changed);
        Assert.AreEqual("start", text);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, escape.type);
    }

    [Test]
    public void UnhandledFunctionKeyRemainsAvailableToTheEditorHost()
    {
        string text = "ready";
        FocusAndPrime(ref text, text.Length);
        var keyDown = KeyEvent(EventType.KeyDown, KeyCode.F5);

        NowTextFieldResult result = Draw(ref text, keyDown, EventType.Ignore);

        Assert.IsFalse(result.changed);
        Assert.IsFalse(result.submitted);
        Assert.AreEqual(FieldId, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.KeyDown, keyDown.type,
            "A focused text field must not consume a native key it does not handle.");
    }

    [Test]
    public void LegacyCaptureAndClaimSamplesNativePacketBeforeSpendingIt()
    {
        var keyDown = KeyEvent(EventType.KeyDown, KeyCode.A, 'a');
        Event.current = null;

        using (NowInput.Begin(_provider, Surface))
        {
            Assert.IsTrue(_provider.TryGetSnapshot(
                Surface,
                keyDown,
                EventType.Ignore,
                ownsCapture: false,
                out var snapshot));
            InputSnapshotField.SetValue(null, snapshot);
            NowTextInput.RequestTextCapture();
            Assert.AreEqual("a", NowTextInput.current.characters);
#if NOWUI_INPUT_SYSTEM
            Assert.AreEqual(
                UnityEngine.InputSystem.Key.None,
                NowKeyInput.current.pressedKey,
                "Claiming the text packet must also spend the raw-key view of the same native event.");
#endif
            NowTextInput.Invalidate();

            NowTextInputFrame reloaded = NowTextInput.current;
            Assert.IsNull(reloaded.characters,
                "A claimed provider packet must stay spent if a nested scope or invalidation reloads it.");
        }

        Assert.AreEqual(EventType.Used, keyDown.type);
    }

    [Test]
    public void NativeTextAreaEnterInsertsNewlineAndKeepsFocus()
    {
        string text = "ab";
        NowResolvedId id = _areaId;
        NowFocus.Focus(id);
        DrawTextArea(ref text, NativeEvent(EventType.Layout), EventType.Layout);
        ref NowTextEditState state = ref NowControlState.Get<NowTextEditState>(id);
        state.caret = 1;
        state.anchor = 1;
        var enter = KeyEvent(EventType.KeyDown, KeyCode.Return, '\n');

        bool changed = DrawTextArea(ref text, enter, EventType.Ignore);

        Assert.IsTrue(changed);
        Assert.AreEqual("a\nb", text);
        Assert.AreEqual(id, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, enter.type);
    }

    [TestCase(EventType.KeyDown)]
    [TestCase(EventType.KeyUp)]
    public void RoutedIgnoreKeyboardEventDoesNotCancelPointerCapture(EventType nativeType)
    {
        var provider = new NowIMGUIInputProvider();
        var mouseDown = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(40f, 36f)
        };

        try
        {
            Assert.IsTrue(provider.TryGetSnapshot(
                Surface,
                mouseDown,
                EventType.MouseDown,
                ownsCapture: false,
                out var pressed));
            Assert.IsTrue(pressed.primaryPressed);
            Assert.IsTrue(provider.NotifyPointerCaptured(NowPointerButton.Primary));

            var keyboardEvent = KeyEvent(nativeType, KeyCode.LeftArrow);
            Assert.AreEqual(nativeType, keyboardEvent.rawType,
                "The fixture requires a native keyboard event filtered to Ignore only by the passive host control.");
            Assert.IsTrue(provider.TryGetSnapshot(
                Surface,
                keyboardEvent,
                EventType.Ignore,
                ownsCapture: true,
                out var keyboard));

            Assert.IsFalse(keyboard.pointerCaptureCancelled,
                "A routed Ignore is not pointer-capture loss when the native event is keyboard input.");
            Assert.IsTrue(keyboard.primaryDown);

            var drag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(55f, 36f),
                delta = new Vector2(15f, 0f)
            };
            Assert.IsTrue(provider.TryGetSnapshot(
                Surface,
                drag,
                EventType.MouseDrag,
                ownsCapture: true,
                out var afterKeyboard));
            Assert.IsFalse(afterKeyboard.pointerCaptureCancelled);
            Assert.IsTrue(afterKeyboard.primaryDown,
                "The captured drag must remain live after an unrelated keyboard dispatch.");
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
        }
    }

    [Test]
    public void PointerPressEndsAStaleTextRepeatWithoutWaitingForKeyUp()
    {
        var backspace = KeyEvent(EventType.KeyDown, KeyCode.Backspace);
        Assert.IsTrue(_provider.TryGetSnapshot(
            Surface,
            backspace,
            EventType.Ignore,
            ownsCapture: false,
            out var keySnapshot));
        Assert.IsTrue(_provider.TryGetTextInputFrame(
            keySnapshot.inputPass,
            out var held));
        Assert.IsTrue(held.backspaceHeld);

        var mouseDown = PointerEvent(EventType.MouseDown);
        Assert.IsTrue(_provider.TryGetSnapshot(
            Surface,
            mouseDown,
            EventType.MouseDown,
            ownsCapture: false,
            out var pointerSnapshot));
        Assert.IsTrue(_provider.TryGetTextInputFrame(
            pointerSnapshot.inputPass,
            out var afterPointer));
        Assert.IsFalse(afterPointer.backspaceHeld,
            "A focus-changing pointer press must not carry an orphaned edit repeat into the next field.");
    }

    NowResolvedId ResolveId(string id)
    {
        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(SurfaceSize))
            return NowControls.GetControlId(id);
    }

    void FocusAndPrime(ref string text, int caret)
    {
        NowFocus.Focus(FieldId);
        Draw(ref text, NativeEvent(EventType.Layout), EventType.Layout);
        State().caret = caret;
        State().anchor = caret;
    }

    NowTextFieldResult Draw(ref string text, Event inputEvent, EventType routedType)
    {
        Event.current = null;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(SurfaceSize))
        {
            Assert.NotNull(InputSnapshotField, "NowInput snapshot test seam was not found.");
            Assert.IsTrue(_provider.TryGetSnapshot(
                Surface,
                inputEvent,
                routedType,
                ownsCapture: false,
                out var snapshot));
            InputSnapshotField.SetValue(null, snapshot);
            return Now.TextField(FieldRect, "native-field").Draw(ref text);
        }
    }

    bool DrawTextArea(ref string text, Event inputEvent, EventType routedType)
    {
        Event.current = null;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(SurfaceSize))
        {
            Assert.NotNull(InputSnapshotField, "NowInput snapshot test seam was not found.");
            Assert.IsTrue(_provider.TryGetSnapshot(
                Surface,
                inputEvent,
                routedType,
                ownsCapture: false,
                out var snapshot));
            InputSnapshotField.SetValue(null, snapshot);
            return Now.TextArea(FieldRect, "native-area")
                .SetLines(2, 2)
                .Draw(ref text);
        }
    }

    NowTextFieldResult DrawFloat(ref float value, Event inputEvent, EventType routedType)
    {
        Event.current = null;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(SurfaceSize))
        {
            Assert.NotNull(InputSnapshotField, "NowInput snapshot test seam was not found.");
            Assert.IsTrue(_provider.TryGetSnapshot(
                Surface,
                inputEvent,
                routedType,
                ownsCapture: false,
                out var snapshot));
            InputSnapshotField.SetValue(null, snapshot);
            return Now.TextField(FieldRect, "native-field").Draw(ref value);
        }
    }

    ref NowTextEditState State()
    {
        return ref NowControlState.Get<NowTextEditState>(FieldId);
    }

    static Event NativeEvent(EventType type)
    {
        return new Event
        {
            type = type,
            mousePosition = new Vector2(40f, 36f)
        };
    }

    static Event KeyEvent(EventType type, KeyCode keyCode, char character = '\0')
    {
        return new Event
        {
            type = type,
            keyCode = keyCode,
            character = character,
            mousePosition = new Vector2(40f, 36f)
        };
    }

    static Event PointerEvent(EventType type)
    {
        return new Event
        {
            type = type,
            button = 0,
            mousePosition = new Vector2(40f, 36f)
        };
    }
}
