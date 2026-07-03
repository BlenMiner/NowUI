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
    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;
        public bool hasInput = true;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return hasInput;
        }
    }

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect ButtonRect = new NowRect(20, 20, 120, 40);

    FakeProvider _provider;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        _provider = new FakeProvider();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
    }

    bool DrawButtonFrame(Vector2 pointer, bool down, bool pressed, bool released)
    {
        _provider.snapshot = new NowInputSnapshot(pointer, down, pressed, released);
        bool result;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            result = Now.Button(ButtonRect, "Save").SetId("Save").Draw();

        return result;
    }

    bool DrawInteractionFrame(Vector2 pointer)
    {
        _provider.snapshot = new NowInputSnapshot(pointer, false, false, false);
        NowControlState.BeginRepaintTracking();

        using (NowInput.Begin(_provider, Surface))
            NowControls.Interact(101, ButtonRect, out _, out _);

        return NowControlState.EndRepaintTracking();
    }

    static NowInteraction DrawCallSiteInteraction(out bool focused, out bool submitted)
    {
        return NowControls.Interact(ButtonRect, out focused, out submitted);
    }

    static NowInteraction DrawBuilderFallbackInteraction(NowId id, int fallbackIdentity, out bool focused, out bool submitted)
    {
        return NowControls.Interact(id, fallbackIdentity, ButtonRect, out focused, out submitted);
    }

    [Test]
    public void CornerRadiusUsesNamedCornerOrder()
    {
        var radius = new NowCornerRadius(topLeft: 4f, topRight: 2f, bottomRight: 1f, bottomLeft: 3f);

        Assert.AreEqual(4f, radius.topLeft);
        Assert.AreEqual(2f, radius.topRight);
        Assert.AreEqual(1f, radius.bottomRight);
        Assert.AreEqual(3f, radius.bottomLeft);
        Assert.AreEqual(new Vector4(2f, 1f, 4f, 3f), radius.packed);
        Assert.AreEqual(new Vector4(5f, 0f, 5f, 0f), NowCornerRadius.Top(5f).packed);
    }

    static NowInputSnapshot NavigationSnapshot(Vector2 navigation, bool previous = false, bool next = false, float time = 1f)
    {
        return new NowInputSnapshot(
            false, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            default, navigation,
            previous, next,
            false, false, false, false, false, false,
            1, time);
    }

    [Test]
    public void TransitionDoesNotAdvanceDuringPassiveMeasurePass()
    {
        const int id = 909;

        try
        {
            NowControlState.Transition(id, true, 100f);
            System.Threading.Thread.Sleep(20);
            float active = NowControlState.Transition(id, true, 100f);

            Assert.Greater(active, 0f);

            System.Threading.Thread.Sleep(20);
            float passive = -1f;

            using (NowInput.Begin(_provider, Surface))
            {
                NowLayout.Area(new NowRect(0f, 0f, 100f, 100f), () =>
                {
                    if (NowInput.isPassive)
                        passive = NowControlState.Transition(id, false, 100f);
                });
            }

            Assert.AreEqual(active, passive, 0.0001f);

            System.Threading.Thread.Sleep(20);
            float after = NowControlState.Transition(id, false, 100f);

            Assert.Less(after, passive);
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    [Test]
    public void PressAnimationStartsOnTriggerAndRequestsRepaint()
    {
        var origin = new Vector2(12f, 18f);

        NowControlState.BeginRepaintTracking();
        var animation = NowControlState.PressAnimation(707, true, origin, 1f);
        bool repaint = NowControlState.EndRepaintTracking();

        Assert.IsTrue(animation.active);
        Assert.AreEqual(origin, animation.origin);
        Assert.LessOrEqual(animation.progress, 0.05f);
        Assert.IsTrue(repaint);
    }

    [Test]
    public void PressAnimationDoesNotStartDuringPassiveMeasurePass()
    {
        var animation = new NowPressAnimation();

        _provider.snapshot = new NowInputSnapshot(new Vector2(40f, 40f), true, true, false);
        NowControlState.BeginRepaintTracking();

        using (NowInput.Begin(_provider, Surface))
        {
            NowInput.BeginPassive();
            animation = NowControlState.PressAnimation(808, true, new Vector2(8f, 9f), 1f);
            NowInput.EndPassive();
        }

        bool repaint = NowControlState.EndRepaintTracking();

        Assert.IsFalse(animation.active);
        Assert.IsFalse(repaint);
    }

    [Test]
    public void ControlStateWarmupCreatesSlotWithoutOverwritingExistingValue()
    {
        const int id = 7171;

        NowControlState.Warmup(id, 42);
        Assert.AreEqual(42, NowControlState.Get<int>(id));

        NowControlState.Get<int>(id) = 99;
        NowControlState.Warmup(id, 42);

        Assert.AreEqual(99, NowControlState.Get<int>(id));
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
    public void InteractRequestsRepaintOnlyWhenInteractionStateChanges()
    {
        Vector2 inside = new Vector2(60, 36);
        Vector2 outside = new Vector2(400, 200);

        Assert.IsTrue(DrawInteractionFrame(inside), "Entering hover should repaint.");
        Assert.IsFalse(DrawInteractionFrame(inside), "Stable hover should stay retained.");
        Assert.IsTrue(DrawInteractionFrame(outside), "Leaving hover should repaint.");
        Assert.IsFalse(DrawInteractionFrame(outside), "Stable non-hover should stay retained.");
    }

    [Test]
    public void ButtonDoesNotClickWhenReleasedOutside()
    {
        Assert.IsFalse(DrawButtonFrame(new Vector2(60, 36), true, true, false));
        Assert.IsFalse(DrawButtonFrame(new Vector2(400, 200), false, false, true));
    }

    [Test]
    public void PassiveMeasurePassMirrorsActiveOccurrenceSalting()
    {
        using (NowInput.Begin(_provider, Surface))
        {
            int active1 = NowControls.GetControlId("row");
            int active2 = NowControls.GetControlId("row");

            NowInput.BeginPassive();
            int passive1 = NowControls.GetControlId("row");
            int passive2 = NowControls.GetControlId("row");
            NowInput.EndPassive();

            NowInput.BeginPassive();
            int secondPass1 = NowControls.GetControlId("row");
            NowInput.EndPassive();

            Assert.AreNotEqual(active1, active2, "Repeated draws of one id must salt apart.");
            Assert.AreEqual(active1, passive1, "Measure passes must resolve the same ids as the real pass.");
            Assert.AreEqual(active2, passive2, "Measure passes must resolve the same ids as the real pass.");
            Assert.AreEqual(active1, secondPass1, "Every measure pass restarts its occurrence count.");
        }
    }

    [Test]
    public void ButtonPressTakesFocus()
    {
        int expectedId;

        using (NowInput.Begin(_provider, Surface))
            expectedId = NowControls.GetControlId("Save");

        DrawButtonFrame(new Vector2(60, 36), true, true, false);
        Assert.AreEqual(expectedId, NowFocus.focusedId);
    }

    [Test]
    public void FocusedButtonActivatesOnSubmit()
    {
        int id = NowControls.GetControlId("Save");
        NowFocus.Focus(id);

        _provider.snapshot = new NowInputSnapshot(
            true, new Vector2(400, 200), new Vector2(400, 200), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 1f);

        bool activated;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            activated = Now.Button(ButtonRect, "Save").SetId("Save").Draw();

        Assert.IsTrue(activated, "Submit on a focused button must activate it.");
    }

    [Test]
    public void IdlessControlInteractUsesCallSiteIdentityAcrossFrames()
    {
        var inside = new Vector2(60, 36);
        NowInteraction interaction = default;
        bool focused = false;
        bool submitted = false;

        void Frame(bool down, bool pressed, bool released)
        {
            _provider.snapshot = new NowInputSnapshot(inside, down, pressed, released);

            using (NowInput.Begin(_provider, Surface))
                interaction = DrawCallSiteInteraction(out focused, out submitted);
        }

        Frame(down: true, pressed: true, released: false);
        int id = interaction.id;

        Assert.IsTrue(focused);
        Assert.IsFalse(submitted);
        Assert.AreEqual(id, NowFocus.focusedId);

        Frame(down: false, pressed: false, released: true);

        Assert.AreEqual(id, interaction.id);
        Assert.IsTrue(interaction.clicked);
        Assert.IsTrue(focused);
    }

    [Test]
    public void ControlInteractCanResolveOptionalBuilderIdentity()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(60, 36), false, false, false);

        using (NowInput.Begin(_provider, Surface))
        {
            var fallback = DrawBuilderFallbackInteraction(default, 7001, out _, out _);
            var explicitId = DrawBuilderFallbackInteraction(7002, 7001, out _, out _);

            Assert.AreEqual(7001, fallback.id);
            Assert.AreEqual(7002, explicitId.id);
        }
    }

    [Test]
    public void CheckboxTogglesRefValueOnClick()
    {
        bool value = false;
        var rect = new NowRect(10, 10, 160, 28);
        Vector2 inside = new Vector2(20, 24);

        _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            Now.Checkbox(rect, "Shadows").SetId("shadows").Draw(ref value);

        Assert.IsFalse(value);

        _provider.snapshot = new NowInputSnapshot(inside, false, false, true);
        bool changed;

        using (NowInput.Begin(_provider, Surface))
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

        _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            Now.Radio(rect, "High", false).SetId("high").Draw();

        _provider.snapshot = new NowInputSnapshot(inside, false, false, true);
        bool clicked;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            clicked = Now.Radio(rect, "High", false).SetId("high").Draw();

        Assert.IsTrue(clicked);
    }

    [Test]
    public void SliderDragsValueFromPointer()
    {
        float value = 0f;
        var rect = new NowRect(0, 0, 200, 20);

        _provider.snapshot = new NowInputSnapshot(new Vector2(150, 10), true, true, false);
        bool changed;

        using (NowInput.Begin(_provider, Surface))
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

        using (NowInput.Begin(_provider, Surface))
        {
            first = NowControls.GetControlId("Delete");
            second = NowControls.GetControlId("Delete");
            third = NowControls.GetControlId("Delete");
        }

        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(second, third);
        Assert.AreNotEqual(first, third);

        int firstNextFrame;

        using (NowInput.Begin(_provider, Surface))
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
            _provider.snapshot = new NowInputSnapshot(insideSecond, down, pressed, released);

            using (NowInput.Begin(_provider, Surface))
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
    public void CombineIdIsStableAndNeverZero()
    {
        Assert.AreEqual(NowInput.CombineId(7, 3), NowInput.CombineId(7, 3));
        Assert.AreNotEqual(NowInput.CombineId(7, 3), NowInput.CombineId(7, 4));
        Assert.AreNotEqual(0, NowInput.CombineId(0, 0));
    }

    [Test]
    public void NowIdSupportsStringIntAndDefaultIdentity()
    {
        NowId none = (string)null;
        NowId stringId = "row-7";
        NowId intId = 77;

        Assert.IsFalse(none.hasValue);
        Assert.AreEqual(123, NowInput.GetId(none, 123));

        Assert.IsTrue(stringId.isString);
        Assert.AreEqual("row-7", stringId.stringValue);
        Assert.AreEqual(NowInput.GetId("row-7"), NowInput.GetId(stringId, 1));

        Assert.IsTrue(intId.isInt);
        Assert.AreEqual(77, intId.intValue);
        Assert.AreEqual(77, NowInput.GetId(intId, 1));
    }

    [Test]
    public void NowIdRejectsReservedOrEmptyExplicitIds()
    {
        Assert.Throws<System.ArgumentException>(() => { NowId id = 0; _ = id; });
        Assert.Throws<System.ArgumentException>(() => { NowId id = string.Empty; _ = id; });
        Assert.Throws<System.ArgumentException>(() => NowInput.Interact(default(NowId), ButtonRect));
    }

    [Test]
    public void NowIdInteractClicksAcrossFrames()
    {
        var id = new NowId(7001);
        Vector2 inside = new Vector2(60, 36);
        bool clicked = false;

        void Frame(bool down, bool pressed, bool released)
        {
            _provider.snapshot = new NowInputSnapshot(inside, down, pressed, released);

            using (NowInput.Begin(_provider, Surface))
                clicked = NowInput.Interact(id, ButtonRect).clicked;
        }

        Frame(down: true, pressed: true, released: false);
        Assert.IsFalse(clicked);
        Frame(down: false, pressed: false, released: true);
        Assert.IsTrue(clicked);
    }

    [Test]
    public void IdlessInteractClicksAcrossFramesFromOneSite()
    {
        var rect = new NowRect(10, 10, 100, 30);
        Vector2 inside = new Vector2(40, 24);
        bool clicked = false;

        void Frame(bool down, bool pressed, bool released)
        {
            _provider.snapshot = new NowInputSnapshot(inside, down, pressed, released);

            using (NowInput.Begin(_provider, Surface))
                clicked = NowInput.Interact(rect).clicked;
        }

        Frame(down: true, pressed: true, released: false);
        Assert.IsFalse(clicked);
        Frame(down: false, pressed: false, released: true);
        Assert.IsTrue(clicked, "site-identity interact must track press and release across frames");
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
            _provider.snapshot = new NowInputSnapshot(insideSecond, down, pressed, released);

            using (NowInput.Begin(_provider, Surface))
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

        using (NowInput.Begin(_provider, Surface))
        {
            byLabel = NowControls.GetControlId("Delete");
            byId = NowControls.GetControlId("row-7-delete");
        }

        Assert.AreNotEqual(byLabel, byId);

        NowFocus.Focus(byId);

        _provider.snapshot = new NowInputSnapshot(
            true, new Vector2(400, 200), new Vector2(400, 200), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 1f);

        bool activated;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            activated = Now.Button(ButtonRect, "Delete").SetId("row-7-delete").Draw();

        Assert.IsTrue(activated);
    }

    [Test]
    public void IntegerSetIdDecouplesIdentityFromLabel()
    {
        int byLabel, byId;

        using (NowInput.Begin(_provider, Surface))
        {
            byLabel = NowControls.GetControlId("Delete");
            byId = NowControls.GetControlId(9007);
        }

        Assert.AreNotEqual(byLabel, byId);

        NowFocus.Focus(byId);

        _provider.snapshot = new NowInputSnapshot(
            true, new Vector2(400, 200), new Vector2(400, 200), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 1f);

        bool activated;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            activated = Now.Button(ButtonRect, "Delete").SetId(9007).Draw();

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
    public void ScopedContentControlsKeepSeparateLayoutCaches()
    {
        NowLayout.Reset();
        NowRect small = default;
        NowRect large = default;

        for (int frame = 0; frame < 5; ++frame)
        {
            _provider.snapshot = default;

            using (NowInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
            using (NowLayout.Area(new Vector4(0, 0, 420, 220)))
            {
                using (NowControls.IdScope(1001))
                using (var button = NowLayout.Button("Item").SetId(7).Begin())
                {
                    small = button.rect;
                    NowLayout.Rect(width: 40f, height: 20f);
                }

                using (NowControls.IdScope(1002))
                using (var button = NowLayout.Button("Item").SetId(7).Begin())
                {
                    large = button.rect;
                    NowLayout.Rect(width: 160f, height: 20f);
                }
            }
        }

        Assert.GreaterOrEqual(small.width, 40f);
        Assert.GreaterOrEqual(large.width, 160f);
        Assert.Less(small.width, large.width, "Scoped controls with the same child id must not share a layout cache.");
    }

    [Test]
    public void SpatialNavigationMovesFocusRight()
    {
        var left = new NowRect(10, 10, 80, 30);
        var right = new NowRect(200, 10, 80, 30);

        using (NowInput.Begin(_provider, Surface))
        {
            _provider.snapshot = default;
            NowFocus.Register(1, left);
            NowFocus.Register(2, right);
            NowFocus.Focus(1);
        }

        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, new Vector2(1f, 0f),
            false, false, false, false, false, false, 2, 2f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(2, NowFocus.focusedId);
    }

    [Test]
    public void DirectionalNavigationWithoutFocusStartsAtOppositeEdge()
    {
        var left = new NowRect(10, 10, 80, 30);
        var right = new NowRect(200, 10, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(2, right);
            NowFocus.Register(1, left);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(1, NowFocus.focusedId, "Right navigation should start at the left edge, not draw order.");
    }

    [Test]
    public void TabNavigationCyclesByRegistrationOrder()
    {
        var first = new NowRect(10, 10, 80, 30);
        var second = new NowRect(10, 50, 80, 30);
        var third = new NowRect(10, 90, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first);
            NowFocus.Register(2, second);
            NowFocus.Register(3, third);
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(2, NowFocus.focusedId);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first);
            NowFocus.Register(2, second);
            NowFocus.Register(3, third);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, previous: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(1, NowFocus.focusedId);
    }

    [Test]
    public void OverlayNavigationIgnoresBaseLayerControls()
    {
        var baseRect = new NowRect(10, 10, 80, 30);
        var popupRect = new NowRect(120, 10, 120, 80);
        var popupFirst = new NowRect(130, 20, 100, 24);
        var popupSecond = new NowRect(130, 50, 100, 24);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, baseRect);
            NowFocus.Focus(1);
            NowOverlay.DeferScreen(popupRect, 100, _ =>
            {
                NowFocus.Register(2, popupFirst);
                NowFocus.Register(3, popupSecond);
            });
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
        {
            NowOverlay.ForceNewFrame();
            NowFocus.ForceNewFrame();
        }

        Assert.AreEqual(2, NowFocus.focusedId);
        Assert.IsFalse(NowFocus.IsFocused(1), "Base focus must not be visible while an overlay layer is active.");
    }

    [Test]
    public void OverlaySubmitIgnoresFocusedBaseLayerControl()
    {
        var baseRect = new NowRect(10, 10, 80, 30);
        var popupRect = new NowRect(120, 10, 120, 80);
        var popupItem = new NowRect(130, 20, 100, 24);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, baseRect);
            NowFocus.Focus(1);
            NowOverlay.DeferScreen(popupRect, 100, _ => NowFocus.Register(2, popupItem));
        }

        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            focusPreviousPressed: false, focusNextPressed: false,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 2, time: 2f);

        bool submitted;

        using (NowInput.Begin(_provider, Surface))
        {
            NowOverlay.ForceNewFrame();
            NowFocus.ForceNewFrame();
            submitted = NowFocus.SubmitPressed(1);
        }

        Assert.IsFalse(submitted);
        Assert.IsFalse(NowFocus.IsFocused(1));
    }

    [Test]
    public void NestedOverlayNavigationUsesTopmostLayer()
    {
        var parentRect = new NowRect(100, 20, 120, 80);
        var parentItem = new NowRect(110, 30, 100, 24);
        var childRect = new NowRect(230, 20, 100, 60);
        var childItem = new NowRect(240, 30, 80, 24);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowOverlay.DeferScreen(parentRect, 100, _ =>
            {
                NowFocus.Register(2, parentItem);
                NowFocus.Focus(2);
                NowOverlay.DeferScreen(childRect, 200, __ => NowFocus.Register(3, childItem));
            });
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
        {
            NowOverlay.ForceNewFrame();
            NowFocus.ForceNewFrame();
        }

        Assert.AreEqual(3, NowFocus.focusedId);
        Assert.IsFalse(NowFocus.IsFocused(2), "Parent overlay focus must yield to the nested overlay layer.");
    }

    [Test]
    public void HeldDirectionalNavigationRepeatsAfterDelay()
    {
        var first = new NowRect(10, 10, 80, 30);
        var second = new NowRect(120, 10, 80, 30);
        var third = new NowRect(230, 10, 80, 30);

        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0f);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first);
            NowFocus.Register(2, second);
            NowFocus.Register(3, third);
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0.2f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(1, NowFocus.focusedId, "Held navigation should wait for the repeat delay.");

        NowFocus.Reset();
        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0f);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first);
            NowFocus.Register(2, second);
            NowFocus.Register(3, third);
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0.5f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(2, NowFocus.focusedId, "Held navigation should repeat after the delay.");
    }

    [Test]
    public void ExplicitDirectionalNavigationOverridesSpatialChoice()
    {
        var first = new NowRect(10, 10, 80, 30);
        var nearest = new NowRect(120, 10, 80, 30);
        var explicitTarget = new NowRect(260, 10, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first, NowFocusNavigation.Right(3));
            NowFocus.Register(2, nearest);
            NowFocus.Register(3, explicitTarget);
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(3, NowFocus.focusedId);
    }

    [Test]
    public void ExplicitDirectionalNavigationFallsBackWhenTargetIsMissing()
    {
        var first = new NowRect(10, 10, 80, 30);
        var nearest = new NowRect(120, 10, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first, NowFocusNavigation.Right(99));
            NowFocus.Register(2, nearest);
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(2, NowFocus.focusedId);
    }

    static readonly NowRect MemoryTopRight = new NowRect(210, 10, 80, 30);
    static readonly NowRect MemoryMiddle = new NowRect(110, 60, 80, 30);
    static readonly NowRect MemoryBottomLeft = new NowRect(10, 400, 80, 30);
    static readonly NowRect MemoryBottomCenter = new NowRect(110, 400, 80, 30);
    static readonly NowRect MemoryBottomRight = new NowRect(210, 400, 80, 30);

    void RegisterMemoryLayout()
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(3, MemoryTopRight);
            NowFocus.Register(4, MemoryMiddle);
            NowFocus.Register(5, MemoryBottomLeft);
            NowFocus.Register(6, MemoryBottomCenter);
            NowFocus.Register(7, MemoryBottomRight);
        }
    }

    void NavigateMemoryLayout(Vector2 navigation)
    {
        _provider.snapshot = NavigationSnapshot(navigation);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();
    }

    [Test]
    public void DirectionalNavigationRemembersCrossAxisOrigin()
    {
        RegisterMemoryLayout();
        NowFocus.Focus(3);

        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(4, NowFocus.focusedId, "First move down should reach the offset middle button.");

        RegisterMemoryLayout();
        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(7, NowFocus.focusedId, "Second move down should return to the starting column, not the middle button's column.");
    }

    [Test]
    public void ExplicitFocusClearsDirectionalNavigationMemory()
    {
        RegisterMemoryLayout();
        NowFocus.Focus(3);

        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(4, NowFocus.focusedId);

        RegisterMemoryLayout();
        NowFocus.Focus(5);

        NavigateMemoryLayout(Vector2.up);
        Assert.AreEqual(4, NowFocus.focusedId, "After an explicit focus the stale cross-axis anchor must not pull navigation sideways.");
    }

    [Test]
    public void HorizontalMoveUpdatesDirectionalNavigationMemory()
    {
        RegisterMemoryLayout();
        NowFocus.Focus(5);

        NavigateMemoryLayout(Vector2.right);
        Assert.AreEqual(6, NowFocus.focusedId);

        RegisterMemoryLayout();
        NavigateMemoryLayout(Vector2.right);
        Assert.AreEqual(7, NowFocus.focusedId);

        RegisterMemoryLayout();
        NavigateMemoryLayout(Vector2.up);
        Assert.AreEqual(3, NowFocus.focusedId, "Horizontal movement must re-anchor the column used by later vertical moves.");
    }

    void RegisterShiftedColumns(float shift)
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, new NowRect(410 + shift, 10, 80, 30));
            NowFocus.Register(2, new NowRect(210 + shift, 10, 80, 30));
            NowFocus.Register(3, new NowRect(410 + shift, 60, 80, 30));
            NowFocus.Register(4, new NowRect(210 + shift, 60, 80, 30));
            NowFocus.Register(5, new NowRect(410 + shift, 110, 80, 30));
            NowFocus.Register(6, new NowRect(210 + shift, 110, 80, 30));
        }
    }

    [Test]
    public void NavigationMemoryShiftsWithScrolledContent()
    {
        RegisterShiftedColumns(0f);
        NowFocus.Focus(2);

        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(4, NowFocus.focusedId);

        RegisterShiftedColumns(-200f);
        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(6, NowFocus.focusedId, "The cross-axis anchor must move with scrolled content, not point at the old screen position.");
    }

    [Test]
    public void EdgeFocusSeedingPrefersVisibleControls()
    {
        var viewport = new NowRect(0, 0, 120, 40);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        using (Now.Mask(viewport))
        using (NowFocus.BeginScrollRegion(500))
        {
            NowFocus.Register(1, new NowRect(0, -100, 100, 30));
            NowFocus.Register(2, new NowRect(0, 0, 100, 30));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.down);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(2, NowFocus.focusedId, "Seeding should land on a visible control, not one clipped out of the scroll viewport.");
    }

    [Test]
    public void ExplicitTabNavigationOverridesRegistrationOrder()
    {
        var first = new NowRect(10, 10, 80, 30);
        var second = new NowRect(10, 50, 80, 30);
        var explicitTarget = new NowRect(10, 90, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(1, first, NowFocusNavigation.Next(3));
            NowFocus.Register(2, second);
            NowFocus.Register(3, explicitTarget);
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(3, NowFocus.focusedId);
    }

    [Test]
    public void ButtonBuilderAppliesExplicitNavigation()
    {
        var first = new NowRect(10, 10, 80, 30);
        var nearest = new NowRect(120, 10, 80, 30);
        var explicitTarget = new NowRect(260, 10, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            Now.Button(first, "First").SetId(1).SetNavigation(NowFocusNavigation.Right(3)).Draw();
            Now.Button(nearest, "Nearest").SetId(2).Draw();
            Now.Button(explicitTarget, "Target").SetId(3).Draw();
        }

        NowFocus.Focus(1);
        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(3, NowFocus.focusedId);
    }

    [Test]
    public void ScrollRegionKeepsCulledControlsNavigable()
    {
        var viewport = new NowRect(0, 0, 120, 40);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        using (Now.Mask(viewport))
        using (NowFocus.BeginScrollRegion(500))
        {
            NowFocus.Register(1, new NowRect(0, 0, 100, 30));
            NowFocus.Register(2, new NowRect(0, 50, 100, 30));
            NowFocus.Focus(1);
        }

        _provider.snapshot = NavigationSnapshot(new Vector2(0f, -1f));

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(2, NowFocus.focusedId);
    }

    [Test]
    public void CancelClearsFocus()
    {
        NowFocus.Focus(42);

        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false,
            cancelDown: true, cancelPressed: true, cancelReleased: false, frame: 1, time: 1f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(0, NowFocus.focusedId);
    }

    [Test]
    public void ControlStateSlotsPersistAndReset()
    {
        ref int slot = ref NowControlState.Get<int>(7);
        slot = 123;

        Assert.AreEqual(123, NowControlState.Get<int>(7));

        NowControlState.Reset();
        Assert.AreEqual(0, NowControlState.Get<int>(7));
    }

    [Test]
    public void ControlStateNamedSlotsUseDerivedIds()
    {
        NowControlState.Warmup(7, "slot", 12);
        Assert.AreEqual(12, NowControlState.Get<int>(7, "slot"));

        NowControlState.Get<int>(7, "slot") = 34;

        Assert.AreEqual(34, NowControlState.Get<int>(NowInput.GetId(7, "slot")));
        Assert.AreEqual(0, NowControlState.Get<int>(7, "other"));
    }

    [Test]
    public void RepeatPulsesOnInitialPress()
    {
        Assert.IsTrue(NowControlState.Repeat(1, held: true));
        Assert.IsFalse(NowControlState.Repeat(1, held: true), "No pulse before the repeat delay.");
        Assert.IsFalse(NowControlState.Repeat(1, held: false));
        Assert.IsTrue(NowControlState.Repeat(1, held: true), "Releasing resets the initial pulse.");
    }

    [Test]
    public void RepeatNamedKeysUseSeparateSlots()
    {
        Assert.IsTrue(NowControlState.Repeat(7, "left", held: true));
        Assert.IsTrue(NowControlState.Repeat(7, "right", held: true));
        Assert.IsFalse(NowControlState.Repeat(7, "left", held: true));
        Assert.IsFalse(NowControlState.Repeat(7, "right", held: true));
    }

    [Test]
    public void ButtonContentScopeReportsClickInside()
    {
        Vector2 inside = new Vector2(60, 36);

        _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var button = Now.Button(ButtonRect).SetId("content-button").Begin())
        {
            Assert.IsFalse(button.clicked);
            NowLayout.Label("Hi").Draw();
        }

        _provider.snapshot = new NowInputSnapshot(inside, false, false, true);
        bool sawClick = false;

        using (NowInput.Begin(_provider, Surface))
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
            using (NowInput.Begin(_provider, Surface))
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

        _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var box = Now.Checkbox(rect).SetId("scope-box").Begin(ref value))
        {
            Assert.IsFalse(box.clicked);
            NowLayout.Label("On").Draw();
        }

        Assert.IsFalse(value);

        _provider.snapshot = new NowInputSnapshot(inside, false, false, true);
        bool sawChange = false;
        bool sawValue = false;

        using (NowInput.Begin(_provider, Surface))
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

        _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var radio = Now.Radio(rect, false).SetId("scope-radio").Begin())
            NowLayout.Label("High").Draw();

        _provider.snapshot = new NowInputSnapshot(inside, false, false, true);
        bool sawClick = false;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        using (var radio = Now.Radio(rect, false).SetId("scope-radio").Begin())
        {
            sawClick = radio.clicked;
            NowLayout.Label("High").Draw();
        }

        Assert.IsTrue(sawClick);
    }

    [Test]
    public void EventSystemSelectionSuspendsNowFocus()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var selectable = new GameObject("Selected");

        try
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;

            if (eventSystem == null)
                Assert.Ignore("EventSystem.current unavailable in this environment.");

            NowFocus.Focus(7);
            eventSystem.SetSelectedGameObject(selectable);

            if (eventSystem.currentSelectedGameObject == null)
                Assert.Ignore("EventSystem selection inactive in this environment.");

            using (NowInput.Begin(_provider, Surface))
                NowFocus.ForceNewFrame();

            Assert.AreEqual(0, NowFocus.focusedId, "UGUI selection must clear NowUI focus.");

            eventSystem.SetSelectedGameObject(selectable);
            NowFocus.Focus(9);
            Assert.IsNull(eventSystem.currentSelectedGameObject, "NowUI focus must deselect the EventSystem.");
            Assert.AreEqual(9, NowFocus.focusedId);
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
        Assert.NotNull(NowTheme.themeAsset);
        Assert.AreEqual(NowTheme.themeAsset, NowTheme.themeAsset, "Default theme must be cached.");
        Assert.AreEqual(NowTheme.themeAsset, NowControls.themeAsset, "NowControls should delegate to NowTheme.");
    }

    [Test]
    public void ThemeScopesRestorePreviousTheme()
    {
        var first = ScriptableObject.CreateInstance<NowThemeAsset>();
        var second = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            using (NowTheme.Scope(first))
            {
                Assert.AreSame(first, NowTheme.themeAsset);

                using (NowControls.Theme(second))
                    Assert.AreSame(second, NowTheme.themeAsset);

                Assert.AreSame(first, NowTheme.themeAsset);
            }

            Assert.AreSame(NowTheme.themeAsset, NowControls.themeAsset);
            Assert.AreNotSame(first, NowTheme.themeAsset);
            Assert.AreNotSame(second, NowTheme.themeAsset);
        }
        finally
        {
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(first);
        }
    }

    void DrawScrollFrame(System.Action<NowScrollScope> body = null)
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            var scroll = Now.ScrollView(new NowRect(0, 0, 200, 100), "scroll-api").Begin();

            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(180f, 30f);

            body?.Invoke(scroll);
            scroll.Dispose();
        }
    }

    [Test]
    public void ScrollScopeOffsetSetterClampsToContentRange()
    {
        DrawScrollFrame();

        Vector2 max = default;
        Vector2 clampedHigh = default;
        Vector2 clampedLow = default;

        DrawScrollFrame(scroll =>
        {
            max = scroll.maxScrollOffset;
            scroll.scrollOffset = new Vector2(0f, 10000f);
            clampedHigh = scroll.scrollOffset;
            scroll.scrollOffset = new Vector2(-50f, -50f);
            clampedLow = scroll.scrollOffset;
        });

        Assert.Greater(max.y, 0f, "Ten 30px rows in a 100px viewport must produce vertical overflow.");
        Assert.AreEqual(max, clampedHigh, "Setting past the end must clamp to the max offset.");
        Assert.AreEqual(Vector2.zero, clampedLow, "Setting before the start must clamp to zero.");
    }

    [Test]
    public void ScrollScopeScrollToEndPersistsAcrossFrames()
    {
        DrawScrollFrame();
        DrawScrollFrame(scroll => scroll.ScrollToEnd());

        Vector2 max = default;
        Vector2 observed = default;

        DrawScrollFrame(scroll =>
        {
            max = scroll.maxScrollOffset;
            observed = scroll.scrollOffset;
        });

        Assert.Greater(max.y, 0f);
        Assert.AreEqual(max, observed, "ScrollToEnd must persist into the next frame.");
    }
}
