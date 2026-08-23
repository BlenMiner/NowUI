#if NOWUI_INPUT_SYSTEM
using System;
using System.Reflection;
using NowUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// End-to-end coverage for raw key binding at the Editor IMGUI boundary.
/// Native key identity must come from the current Event/input pass rather than
/// a later Input System frame sample.
/// </summary>
public class NowIMGUIKeyInputRoutingTests
{
    sealed class FakeKeys : INowKeyInputSource
    {
        public NowKeyInputFrame frame;

        public bool TryGetFrame(out NowKeyInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    static readonly FieldInfo InputSnapshotField = typeof(NowInput).GetField(
        "_snapshot",
        BindingFlags.NonPublic | BindingFlags.Static);

    const int HostControlId = 19201;
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
        NowKeyInput.Reset();
        NowLayout.Reset();

        _provider = new NowIMGUIInputProvider(HostControlId, new object());
        _drawList = new NowDrawList();
        _fieldId = ResolveId();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.ResetState(releaseNativeCapture: false);
        _drawList.Dispose();

        NowKeyInput.Reset();
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
    public void PassiveHostNativeKeyBindsOnceAndConsumesTheEvent()
    {
        Key value = Key.E;
        ArmByClick(ref value);
        var keyDown = KeyEvent(KeyCode.K, 'k');

        bool changed = Draw(ref value, keyDown, EventType.Ignore);

        Assert.IsTrue(changed);
        Assert.AreEqual(Key.K, value);
        Assert.AreEqual(EventType.Used, keyDown.type);

        Assert.IsFalse(Draw(ref value, NativeEvent(EventType.Repaint), EventType.Repaint));
        Assert.AreEqual(Key.K, value, "A claimed raw-key packet must not replay on Repaint.");
    }

    [TestCase(KeyCode.Return, Key.Enter)]
    [TestCase(KeyCode.Alpha5, Key.Digit5)]
    [TestCase(KeyCode.KeypadEnter, Key.NumpadEnter)]
    [TestCase(KeyCode.LeftControl, Key.LeftCtrl)]
    [TestCase(KeyCode.LeftMeta, Key.LeftMeta)]
    [TestCase(KeyCode.RightMeta, Key.RightMeta)]
    [TestCase(KeyCode.F24, Key.F24)]
    public void NativeKeyCodeMapsWithoutPollingAnotherInputFrame(KeyCode keyCode, Key expected)
    {
        Key value = Key.E;
        ArmByClick(ref value);
        var keyDown = KeyEvent(keyCode);

        Assert.IsTrue(Draw(ref value, keyDown, EventType.Ignore));
        Assert.AreEqual(expected, value);
        Assert.AreEqual(EventType.Used, keyDown.type);
    }

    [Test]
    public void SubmitThatArmsCaptureCannotBecomeTheBinding()
    {
        Key value = Key.E;
        NowFocus.Focus(FieldId);
        Draw(ref value, NativeEvent(EventType.Layout), EventType.Layout);
        var enter = KeyEvent(KeyCode.Return, '\n');

        Assert.IsFalse(Draw(ref value, enter, EventType.Ignore));
        Assert.AreEqual(Key.E, value);
        Assert.AreEqual(EventType.Used, enter.type);
        Assert.IsFalse(Draw(ref value, NativeEvent(EventType.Repaint), EventType.Repaint));
        Assert.AreEqual(Key.E, value,
            "The submit key that opened capture must be discarded for its provider pass.");

        Assert.IsTrue(Draw(ref value, KeyEvent(KeyCode.K, 'k'), EventType.Ignore));
        Assert.AreEqual(Key.K, value);
    }

    [Test]
    public void NativeEscapeCancelsCaptureAndConsumesTheEvent()
    {
        Key value = Key.E;
        ArmByClick(ref value);
        var escape = KeyEvent(KeyCode.Escape);

        Assert.IsFalse(Draw(ref value, escape, EventType.Ignore));
        Assert.AreEqual(Key.E, value);
        Assert.AreEqual(EventType.Used, escape.type);

        Assert.IsFalse(Draw(ref value, KeyEvent(KeyCode.K, 'k'), EventType.Ignore));
        Assert.AreEqual(Key.E, value, "Escape must leave the field out of capture mode.");
    }

    [Test]
    public void CustomRawKeySourceOverridesTheNativeProviderPacket()
    {
        Key value = Key.E;
        ArmByClick(ref value);
        NowKeyInput.source = new FakeKeys
        {
            frame = new NowKeyInputFrame { pressedKey = Key.Q }
        };
        NowKeyInput.Invalidate();
        var native = KeyEvent(KeyCode.F24);

        Assert.IsTrue(Draw(ref value, native, EventType.Ignore));
        Assert.AreEqual(Key.Q, value);
        Assert.AreEqual(EventType.Used, native.type);
    }

    void ArmByClick(ref Key value)
    {
        Assert.IsFalse(Draw(ref value, PointerEvent(EventType.MouseDown), EventType.MouseDown));
        Assert.IsFalse(Draw(ref value, PointerEvent(EventType.MouseUp), EventType.MouseUp));
        Assert.AreEqual(FieldId, NowFocus.focusedResolvedId);
    }

    NowResolvedId ResolveId()
    {
        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(SurfaceSize))
            return NowControls.GetControlId("native-bind");
    }

    bool Draw(ref Key value, Event inputEvent, EventType routedType)
    {
        Event.current = null;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(SurfaceSize))
        {
            Assert.NotNull(InputSnapshotField, "NowInput snapshot test seam was not found.");
            bool ownsCapture = GUIUtility.hotControl == HostControlId;
            Assert.IsTrue(_provider.TryGetSnapshot(
                Surface,
                inputEvent,
                routedType,
                ownsCapture,
                out var snapshot));
            InputSnapshotField.SetValue(null, snapshot);
            return Now.KeyBindingField(FieldRect, "native-bind").Draw(ref value);
        }
    }

    static Event NativeEvent(EventType type)
    {
        return new Event
        {
            type = type,
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

    static Event KeyEvent(KeyCode keyCode, char character = '\0')
    {
        return new Event
        {
            type = EventType.KeyDown,
            keyCode = keyCode,
            character = character,
            mousePosition = new Vector2(40f, 36f)
        };
    }
}
#endif
