using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// Controls foundation tests: driven entirely through fake input providers, the
/// same seam custom controls and UGUI hosting use.
/// </summary>
public class NowControlsTests
{
    sealed class FakeProvider : INowUIInputProvider
    {
        public NowUIInputSnapshot snapshot;
        public bool hasInput = true;

        public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot result)
        {
            result = snapshot;
            return hasInput;
        }
    }

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect ButtonRect = new NowRect(20, 20, 120, 40);

    FakeProvider _provider;
    NowUIDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
        _provider = new FakeProvider();
        _drawList = new NowUIDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
    }

    bool DrawButtonFrame(Vector2 pointer, bool down, bool pressed, bool released)
    {
        _provider.snapshot = new NowUIInputSnapshot(pointer, down, pressed, released);
        bool result;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            result = Now.Button(ButtonRect, "Save").SetId("Save").Draw();

        return result;
    }

    [Test]
    public void ButtonClicksOnPressAndReleaseInside()
    {
        Vector2 inside = new Vector2(60, 36);

        Assert.IsFalse(DrawButtonFrame(inside, down: true, pressed: true, released: false));
        Assert.IsTrue(DrawButtonFrame(inside, down: false, pressed: false, released: true));
        Assert.IsTrue(_drawList.hasGeometry, "Button drew no visuals.");
    }

    [Test]
    public void ButtonDoesNotClickWhenReleasedOutside()
    {
        Assert.IsFalse(DrawButtonFrame(new Vector2(60, 36), true, true, false));
        Assert.IsFalse(DrawButtonFrame(new Vector2(400, 200), false, false, true));
    }

    [Test]
    public void ButtonPressTakesFocus()
    {
        int expectedId;

        using (NowUIInput.Begin(_provider, Surface))
            expectedId = NowControls.GetControlId("Save");

        DrawButtonFrame(new Vector2(60, 36), true, true, false);
        Assert.AreEqual(expectedId, NowUIFocus.focusedId);
    }

    [Test]
    public void FocusedButtonActivatesOnSubmit()
    {
        int id = NowControls.GetControlId("Save");
        NowUIFocus.Focus(id);

        _provider.snapshot = new NowUIInputSnapshot(
            true, new Vector2(400, 200), new Vector2(400, 200), Vector2.zero,
            NowUIPointerButtons.None, NowUIPointerButtons.None, NowUIPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 1f);

        bool activated;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            activated = Now.Button(ButtonRect, "Save").SetId("Save").Draw();

        Assert.IsTrue(activated, "Submit on a focused button must activate it.");
    }

    [Test]
    public void CheckboxTogglesRefValueOnClick()
    {
        bool value = false;
        var rect = new NowRect(10, 10, 160, 28);
        Vector2 inside = new Vector2(20, 24);

        _provider.snapshot = new NowUIInputSnapshot(inside, true, true, false);

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            Now.Checkbox(rect, "Shadows").SetId("shadows").Draw(ref value);

        Assert.IsFalse(value);

        _provider.snapshot = new NowUIInputSnapshot(inside, false, false, true);
        bool changed;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.Checkbox(rect, "Shadows").SetId("shadows").Draw(ref value);

        Assert.IsTrue(changed);
        Assert.IsTrue(value);
    }

    [Test]
    public void RadioReportsClickForSelection()
    {
        var rect = new NowRect(10, 10, 160, 28);
        Vector2 inside = new Vector2(20, 24);

        _provider.snapshot = new NowUIInputSnapshot(inside, true, true, false);

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            Now.Radio(rect, "High", false).SetId("high").Draw();

        _provider.snapshot = new NowUIInputSnapshot(inside, false, false, true);
        bool clicked;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            clicked = Now.Radio(rect, "High", false).SetId("high").Draw();

        Assert.IsTrue(clicked);
    }

    [Test]
    public void SliderDragsValueFromPointer()
    {
        float value = 0f;
        var rect = new NowRect(0, 0, 200, 20);

        _provider.snapshot = new NowUIInputSnapshot(new Vector2(150, 10), true, true, false);
        bool changed;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.Slider(rect, 0f, 1f).Draw(ref value);

        Assert.IsTrue(changed);
        Assert.Greater(value, 0.6f);
        Assert.Less(value, 0.9f);
    }

    [Test]
    public void DuplicateLabelsGetDistinctStableIds()
    {
        int first, second, third;

        using (NowUIInput.Begin(_provider, Surface))
        {
            first = NowControls.GetControlId("Delete");
            second = NowControls.GetControlId("Delete");
            third = NowControls.GetControlId("Delete");
        }

        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(second, third);
        Assert.AreNotEqual(first, third);

        int firstNextFrame;

        using (NowUIInput.Begin(_provider, Surface))
            firstNextFrame = NowControls.GetControlId("Delete");

        Assert.AreEqual(first, firstNextFrame);
    }

    [Test]
    public void SameSiteLoopButtonsDoNotShareActivation()
    {
        var rects = new[] { new NowRect(0, 0, 100, 30), new NowRect(0, 50, 100, 30) };
        Vector2 insideSecond = new Vector2(50, 65);
        bool firstClicked = false, secondClicked = false;

        void Frame(bool down, bool pressed, bool released)
        {
            _provider.snapshot = new NowUIInputSnapshot(insideSecond, down, pressed, released);

            using (NowUIInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
            {
                for (int i = 0; i < rects.Length; ++i)
                {
                    bool clicked = Now.Button(rects[i], "Delete").Draw();

                    if (i == 0) firstClicked = clicked;
                    else secondClicked = clicked;
                }
            }
        }

        Frame(down: true, pressed: true, released: false);
        Frame(down: false, pressed: false, released: true);

        Assert.IsFalse(firstClicked, "Same-site sibling must not activate.");
        Assert.IsTrue(secondClicked, "The clicked loop iteration must activate.");
    }

    [Test]
    public void SameLabelDifferentCallSitesAreDistinctControls()
    {
        var rect1 = new NowRect(0, 0, 100, 30);
        var rect2 = new NowRect(0, 50, 100, 30);
        Vector2 insideSecond = new Vector2(50, 65);
        bool firstClicked = false, secondClicked = false;

        void Frame(bool down, bool pressed, bool released)
        {
            _provider.snapshot = new NowUIInputSnapshot(insideSecond, down, pressed, released);

            using (NowUIInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
            {
                firstClicked = Now.Button(rect1, "Delete").Draw();
                secondClicked = Now.Button(rect2, "Delete").Draw();
            }
        }

        Frame(down: true, pressed: true, released: false);
        Frame(down: false, pressed: false, released: true);

        Assert.IsFalse(firstClicked, "A same-label button at another site must not activate.");
        Assert.IsTrue(secondClicked, "The button under the pointer must activate.");
    }

    [Test]
    public void SetIdDecouplesIdentityFromLabel()
    {
        int byLabel, byId;

        using (NowUIInput.Begin(_provider, Surface))
        {
            byLabel = NowControls.GetControlId("Delete");
            byId = NowControls.GetControlId("row-7-delete");
        }

        Assert.AreNotEqual(byLabel, byId);

        NowUIFocus.Focus(byId);

        _provider.snapshot = new NowUIInputSnapshot(
            true, new Vector2(400, 200), new Vector2(400, 200), Vector2.zero,
            NowUIPointerButtons.None, NowUIPointerButtons.None, NowUIPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 1f);

        bool activated;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            activated = Now.Button(ButtonRect, "Delete").SetId("row-7-delete").Draw();

        Assert.IsTrue(activated);
    }

    [Test]
    public void IdScopesDisambiguateIdenticalLabels()
    {
        int outer = NowControls.GetControlId("Delete");
        int scoped;

        using (NowControls.IdScope("row-1"))
            scoped = NowControls.GetControlId("Delete");

        int scopedOther;

        using (NowControls.IdScope("row-2"))
            scopedOther = NowControls.GetControlId("Delete");

        Assert.AreNotEqual(outer, scoped);
        Assert.AreNotEqual(scoped, scopedOther);
    }

    [Test]
    public void SpatialNavigationMovesFocusRight()
    {
        var left = new NowRect(10, 10, 80, 30);
        var right = new NowRect(200, 10, 80, 30);

        using (NowUIInput.Begin(_provider, Surface))
        {
            _provider.snapshot = default;
            NowUIFocus.Register(1, left);
            NowUIFocus.Register(2, right);
            NowUIFocus.Focus(1);
        }

        _provider.snapshot = new NowUIInputSnapshot(
            true, default, default, default,
            NowUIPointerButtons.None, NowUIPointerButtons.None, NowUIPointerButtons.None,
            Vector2.zero, new Vector2(1f, 0f),
            false, false, false, false, false, false, 2, 2f);

        using (NowUIInput.Begin(_provider, Surface))
            NowUIFocus.ForceNewFrame();

        Assert.AreEqual(2, NowUIFocus.focusedId);
    }

    [Test]
    public void CancelClearsFocus()
    {
        NowUIFocus.Focus(42);

        _provider.snapshot = new NowUIInputSnapshot(
            true, default, default, default,
            NowUIPointerButtons.None, NowUIPointerButtons.None, NowUIPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false,
            cancelDown: true, cancelPressed: true, cancelReleased: false, frame: 1, time: 1f);

        using (NowUIInput.Begin(_provider, Surface))
            NowUIFocus.ForceNewFrame();

        Assert.AreEqual(0, NowUIFocus.focusedId);
    }

    [Test]
    public void ControlStateSlotsPersistAndReset()
    {
        ref int slot = ref NowUIControlState.Get<int>(7);
        slot = 123;

        Assert.AreEqual(123, NowUIControlState.Get<int>(7));

        NowUIControlState.Reset();
        Assert.AreEqual(0, NowUIControlState.Get<int>(7));
    }

    [Test]
    public void RepeatPulsesOnInitialPress()
    {
        Assert.IsTrue(NowUIControlState.Repeat(1, held: true));
        Assert.IsFalse(NowUIControlState.Repeat(1, held: true), "No pulse before the repeat delay.");
        Assert.IsFalse(NowUIControlState.Repeat(1, held: false));
        Assert.IsTrue(NowUIControlState.Repeat(1, held: true), "Releasing resets the initial pulse.");
    }

    [Test]
    public void ButtonContentScopeReportsClickInside()
    {
        Vector2 inside = new Vector2(60, 36);

        _provider.snapshot = new NowUIInputSnapshot(inside, true, true, false);

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var button = Now.Button(ButtonRect).SetId("content-button").Begin())
        {
            Assert.IsFalse(button.clicked);
            NowLayout.Label("Hi").Draw();
        }

        _provider.snapshot = new NowUIInputSnapshot(inside, false, false, true);
        bool sawClick = false;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var button = Now.Button(ButtonRect).SetId("content-button").Begin())
        {
            sawClick = button.clicked;
            NowLayout.Label("Hi").Draw();
        }

        Assert.IsTrue(sawClick, "Click result must be readable inside the content scope.");
        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void BeginButtonGrowsToEncompassContent()
    {
        NowLayout.Reset();
        NowRect rect = default;

        for (int frame = 0; frame < 4; ++frame)
        {
            using (NowUIInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
            using (NowLayout.Area(new Vector4(0, 0, 400, 300)))
            using (var button = NowLayout.Button("grow-button").Begin())
            {
                rect = button.rect;
                NowLayout.Rect(128, 128);
            }
        }

        Assert.GreaterOrEqual(rect.width, 128f, "button width must grow to encompass its content");
        Assert.GreaterOrEqual(rect.height, 128f, "button height must grow to encompass its content");
    }

    [Test]
    public void CheckboxContentScopeTogglesInside()
    {
        bool value = false;
        var rect = new NowRect(10, 10, 180, 28);
        Vector2 inside = new Vector2(20, 24);

        _provider.snapshot = new NowUIInputSnapshot(inside, true, true, false);

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var box = Now.Checkbox(rect).SetId("scope-box").Begin(ref value))
        {
            Assert.IsFalse(box.clicked);
            NowLayout.Label("On").Draw();
        }

        Assert.IsFalse(value);

        _provider.snapshot = new NowUIInputSnapshot(inside, false, false, true);
        bool sawChange = false;
        bool sawValue = false;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var box = Now.Checkbox(rect).SetId("scope-box").Begin(ref value))
        {
            sawChange = box.clicked;
            sawValue = value;
            NowLayout.Label("On").Draw();
        }

        Assert.IsTrue(sawChange, "Toggle must be reported inside the scope.");
        Assert.IsTrue(sawValue, "Updated value must be readable inside the scope.");
        Assert.IsTrue(value);
    }

    [Test]
    public void RadioContentScopeReportsClickInside()
    {
        var rect = new NowRect(10, 10, 180, 28);
        Vector2 inside = new Vector2(20, 24);

        _provider.snapshot = new NowUIInputSnapshot(inside, true, true, false);

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var radio = Now.Radio(rect, false).SetId("scope-radio").Begin())
            NowLayout.Label("High").Draw();

        _provider.snapshot = new NowUIInputSnapshot(inside, false, false, true);
        bool sawClick = false;

        using (NowUIInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var radio = Now.Radio(rect, false).SetId("scope-radio").Begin())
        {
            sawClick = radio.clicked;
            NowLayout.Label("High").Draw();
        }

        Assert.IsTrue(sawClick);
    }

    [Test]
    public void EventSystemSelectionSuspendsNowUIFocus()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var selectable = new GameObject("Selected");

        try
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;

            if (eventSystem == null)
                Assert.Ignore("EventSystem.current unavailable in this environment.");

            NowUIFocus.Focus(7);
            eventSystem.SetSelectedGameObject(selectable);

            if (eventSystem.currentSelectedGameObject == null)
                Assert.Ignore("EventSystem selection inactive in this environment.");

            using (NowUIInput.Begin(_provider, Surface))
                NowUIFocus.ForceNewFrame();

            Assert.AreEqual(0, NowUIFocus.focusedId, "UGUI selection must clear NowUI focus.");

            eventSystem.SetSelectedGameObject(selectable);
            NowUIFocus.Focus(9);
            Assert.IsNull(eventSystem.currentSelectedGameObject, "NowUI focus must deselect the EventSystem.");
            Assert.AreEqual(9, NowUIFocus.focusedId);
        }
        finally
        {
            Object.DestroyImmediate(selectable);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [Test]
    public void DefaultThemeIsAvailable()
    {
        Assert.NotNull(NowControls.theme);
        Assert.AreEqual(NowControls.theme, NowControls.theme, "Default theme must be cached.");
    }
}
