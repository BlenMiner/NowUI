using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using NowUI;

/// <summary>
/// Key-binding field capture tests, driven through fake pointer and key
/// sources like the other control fixtures.
/// </summary>
public class NowKeyBindingFieldTests
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

    sealed class FakeKeys : INowKeyInputSource
    {
        public NowKeyInputFrame frame;

        public bool TryGetFrame(out NowKeyInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    sealed class RecordingRenderer : NowControlRenderer
    {
        public int textInputFrames;

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            ++textInputFrames;
            base.DrawTextInputFrame(context);
        }
    }

    static void SetRenderer(NowThemeAsset theme, NowControlRenderer renderer)
    {
        typeof(NowThemeAsset)
            .GetField("_controlRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(theme, renderer);
    }

    static readonly Vector2 Surface = new Vector2(512, 420);
    static readonly NowRect FieldRect = new NowRect(20, 20, 140, 30);
    static readonly Vector2 InsideField = new Vector2(60, 35);
    static readonly Vector2 OutsideField = new Vector2(400, 400);

    FakePointer _pointer;
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
        _keys = new FakeKeys();
        NowKeyInput.source = _keys;
        _drawList = new NowDrawList();
        _frame = Time.frameCount + 1;
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

    bool DrawFrame(ref Key value, NowInputSnapshot snapshot, Key pressed = Key.None, bool allowClear = true, bool advanceFrame = true)
    {
        if (advanceFrame)
            ++_frame;

        snapshot.frame = _frame;
        _pointer.snapshot = snapshot;
        _keys.frame = new NowKeyInputFrame { pressedKey = pressed };
        NowKeyInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.KeyBindingField(FieldRect, "bind").SetAllowClear(allowClear).Draw(ref value);
    }

    void ClickField(ref Key value)
    {
        Assert.IsFalse(DrawFrame(ref value, new NowInputSnapshot(InsideField, true, true, false)));
        Assert.IsFalse(DrawFrame(ref value, new NowInputSnapshot(InsideField, false, false, true)));
    }

    static NowInputSnapshot IdleSnapshot()
    {
        return new NowInputSnapshot(InsideField, false, false, false);
    }

    static NowInputSnapshot SubmitSnapshot()
    {
        return new NowInputSnapshot(
            true,
            InsideField,
            InsideField,
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            Vector2.zero,
            Vector2.zero,
            true,
            true,
            false,
            false,
            false,
            false,
            0,
            0f);
    }

    [Test]
    public void ClickThenKeyPressRebinds()
    {
        var value = Key.E;
        ClickField(ref value);

        Assert.IsTrue(DrawFrame(ref value, IdleSnapshot(), Key.K));
        Assert.AreEqual(Key.K, value);
    }

    [Test]
    public void KeyOnTheArmFrameIsIgnored()
    {
        var value = Key.E;
        ClickField(ref value);

        Assert.IsFalse(DrawFrame(ref value, IdleSnapshot(), Key.K, advanceFrame: false));
        Assert.AreEqual(Key.E, value);

        Assert.IsTrue(DrawFrame(ref value, IdleSnapshot(), Key.K));
        Assert.AreEqual(Key.K, value);
    }

    [Test]
    public void EscapeCancelsWithoutRebinding()
    {
        var value = Key.E;
        ClickField(ref value);

        Assert.IsFalse(DrawFrame(ref value, IdleSnapshot(), Key.Escape));
        Assert.AreEqual(Key.E, value);

        Assert.IsFalse(DrawFrame(ref value, IdleSnapshot(), Key.K));
        Assert.AreEqual(Key.E, value);
    }

    [Test]
    public void DeleteClearsTheBinding()
    {
        var value = Key.E;
        ClickField(ref value);

        Assert.IsTrue(DrawFrame(ref value, IdleSnapshot(), Key.Delete));
        Assert.AreEqual(Key.None, value);
    }

    [Test]
    public void DeleteBindsWhenClearingIsDisallowed()
    {
        var value = Key.E;

        Assert.IsFalse(DrawFrame(ref value, new NowInputSnapshot(InsideField, true, true, false), allowClear: false));
        Assert.IsFalse(DrawFrame(ref value, new NowInputSnapshot(InsideField, false, false, true), allowClear: false));

        Assert.IsTrue(DrawFrame(ref value, IdleSnapshot(), Key.Delete, allowClear: false));
        Assert.AreEqual(Key.Delete, value);
    }

    [Test]
    public void OutsidePressCancelsCapture()
    {
        var value = Key.E;
        ClickField(ref value);

        Assert.IsFalse(DrawFrame(ref value, new NowInputSnapshot(OutsideField, true, true, false)));

        Assert.IsFalse(DrawFrame(ref value, IdleSnapshot(), Key.K));
        Assert.AreEqual(Key.E, value);
    }

    [Test]
    public void ClickingTheFieldAgainCancelsCapture()
    {
        var value = Key.E;
        ClickField(ref value);
        ClickField(ref value);

        Assert.IsFalse(DrawFrame(ref value, IdleSnapshot(), Key.K));
        Assert.AreEqual(Key.E, value);
    }

    [Test]
    public void SubmitWhileFocusedStartsCapture()
    {
        var value = Key.E;
        NowFocus.Focus(NowControls.GetControlId("bind"));

        Assert.IsFalse(DrawFrame(ref value, SubmitSnapshot()));

        Assert.IsTrue(DrawFrame(ref value, IdleSnapshot(), Key.M));
        Assert.AreEqual(Key.M, value);
    }

    [Test]
    public void SubmitKeyPressedAfterArmingBindsThatKey()
    {
        var value = Key.E;
        NowFocus.Focus(NowControls.GetControlId("bind"));

        Assert.IsFalse(DrawFrame(ref value, SubmitSnapshot()));

        Assert.IsTrue(DrawFrame(ref value, SubmitSnapshot(), Key.Space));
        Assert.AreEqual(Key.Space, value);

        Assert.IsFalse(DrawFrame(ref value, IdleSnapshot(), Key.K));
        Assert.AreEqual(Key.Space, value);
    }

    [Test]
    public void FieldUsesControlRendererFrame()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var value = Key.E;

            using (NowTheme.Scope(theme))
                DrawFrame(ref value, IdleSnapshot());

            Assert.AreEqual(1, renderer.textInputFrames);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void KeyNamesFallBackToReadableEnumNames()
    {
        Assert.AreEqual("None", NowKeyNames.GetName(Key.None));

        if (Keyboard.current != null)
            return;

        Assert.AreEqual("Left Shift", NowKeyNames.GetName(Key.LeftShift));
        Assert.AreEqual("5", NowKeyNames.GetName(Key.Digit5));
        Assert.AreEqual("Num Enter", NowKeyNames.GetName(Key.NumpadEnter));
        Assert.AreEqual("F12", NowKeyNames.GetName(Key.F12));
        Assert.AreEqual("Space", NowKeyNames.GetName(Key.Space));
    }
}
