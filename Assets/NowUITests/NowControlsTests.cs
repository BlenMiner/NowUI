using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// Controls foundation tests: driven entirely through fake input providers, the
/// same seam custom controls and UGUI hosting use.
/// </summary>
public class NowControlsTests
{
    static readonly NowResolvedId TestIdentityRoot =
        NowResolvedId.CreateOwnerRoot(0x4E4F575549544553UL);

    static NowResolvedId TestId(int id) => TestIdentityRoot.Child(id);

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

    bool DrawSelectableRowFrame(Vector2 pointer, bool down, bool pressed, bool released)
    {
        _provider.snapshot = new NowInputSnapshot(pointer, down, pressed, released);
        bool result;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            result = Now.SelectableRow(ButtonRect, "Warning")
                .SetId("row")
                .SetSelected()
                .Draw();

        return result;
    }

    bool DrawInteractionFrame(Vector2 pointer)
    {
        _provider.snapshot = new NowInputSnapshot(pointer, false, false, false);
        NowControlState.BeginRepaintTracking();

        using (NowInput.Begin(_provider, Surface))
            NowControls.Interact(TestId(101), ButtonRect, out _, out _);

        return NowControlState.EndRepaintTracking();
    }

    static NowInteraction DrawCallSiteInteraction(out bool focused, out bool submitted)
    {
        return NowControls.Interact(ButtonRect, out focused, out submitted);
    }

    static NowInteraction DrawBuilderFallbackInteraction(
        NowId id,
        NowCallSiteId fallbackIdentity,
        out bool focused,
        out bool submitted)
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
    void RegisterFocusPolicyRow(NowFocusNavigationLock navigationLock, bool consumesCancel = false)
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), new NowRect(10, 10, 80, 30));
            NowFocus.Register(TestId(2), new NowRect(110, 10, 80, 30), default, navigationLock, consumesCancel);
            NowFocus.Register(TestId(3), new NowRect(210, 10, 80, 30));
            NowFocus.Focus(TestId(2));
        }
    }

    [Test]
    public void TransitionDoesNotAdvanceDuringPassiveMeasurePass()
    {
        NowResolvedId id = TestId(909);

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
                NowLayout.RunMeasured(new NowRect(0f, 0f, 100f, 100f), () =>
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
    public void TransitionAdvancesOnFirstActiveFrameAfterIdle()
    {
        NowResolvedId id = TestId(910);

        Assert.AreEqual(0f, NowControlState.Transition(id, false, 100f));
        System.Threading.Thread.Sleep(20);

        Assert.Greater(NowControlState.Transition(id, true, 100f), 0f);
    }

    [Test]
    public void PressAnimationStartsOnTriggerAndRequestsRepaint()
    {
        var origin = new Vector2(12f, 18f);

        Assert.AreEqual(0, NowControlState.pressAnimationStateCount);
        NowControlState.BeginRepaintTracking();
        var animation = NowControlState.PressAnimation(TestId(707), true, origin, 1f);
        bool repaint = NowControlState.EndRepaintTracking();

        Assert.IsTrue(animation.active);
        Assert.AreEqual(origin, animation.origin);
        Assert.AreEqual(0f, animation.progress);
        Assert.AreEqual(1, NowControlState.pressAnimationStateCount);
        Assert.IsTrue(repaint);
    }

    [Test]
    public void PressAnimationAdvancesAndRequestsRepaintWhileActive()
    {
        NowResolvedId id = TestId(708);
        var origin = new Vector2(3f, 7f);

        NowControlState.PressAnimation(id, true, origin, 10f);
        System.Threading.Thread.Sleep(20);

        NowControlState.BeginRepaintTracking();
        var animation = NowControlState.PressAnimation(id, false, default, 10f);
        bool repaint = NowControlState.EndRepaintTracking();

        Assert.IsTrue(animation.active);
        Assert.AreEqual(origin, animation.origin);
        Assert.Greater(animation.progress, 0f);
        Assert.Less(animation.progress, 1f);
        Assert.IsTrue(repaint);
    }

    [Test]
    public void IdlePressAnimationDoesNotCreateStateOrRequestRepaint()
    {
        Assert.AreEqual(0, NowControlState.pressAnimationStateCount);
        NowControlState.BeginRepaintTracking();

        for (int i = 0; i < 100; ++i)
        {
            var animation = NowControlState.PressAnimation(TestId(8000 + i), false, default, 1f);
            Assert.IsFalse(animation.active);
        }

        bool repaint = NowControlState.EndRepaintTracking();

        Assert.AreEqual(0, NowControlState.pressAnimationStateCount);
        Assert.IsFalse(repaint);
    }

    [Test]
    public void ScheduledCaretBlinkRequestsOnlyItsNextPhaseBoundary()
    {
        float anchor = Time.realtimeSinceStartup;

        NowControlState.BeginRepaintTracking();
        NowControlState.ScheduledBlink(1f, anchor);
        bool immediate = NowControlState.EndRepaintTracking(out float nextRepaintAt);

        Assert.IsFalse(immediate);
        Assert.IsFalse(float.IsInfinity(nextRepaintAt));
        Assert.That(
            nextRepaintAt - Time.realtimeSinceStartup,
            Is.InRange(0f, 0.51f));
    }

    [Test]
    public void RepaintTrackerWaitsForScheduledDeadline()
    {
        var tracker = new NowInteractionRepaintTracker();
        float now = Time.realtimeSinceStartup;

        tracker.SetRepaintRequest(false, now + 10f);
        Assert.IsFalse(tracker.wantsRepaint);

        tracker.SetRepaintRequest(false, now - 0.01f);
        Assert.IsTrue(tracker.wantsRepaint);
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
            animation = NowControlState.PressAnimation(TestId(808), true, new Vector2(8f, 9f), 1f);
            NowInput.EndPassive();
        }

        bool repaint = NowControlState.EndRepaintTracking();

        Assert.IsFalse(animation.active);
        Assert.IsFalse(repaint);
    }

    [Test]
    public void ControlStateWarmupCreatesSlotWithoutOverwritingExistingValue()
    {
        NowResolvedId id = TestId(7171);

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
    public void SelectableRowClicksOnPressAndReleaseInside()
    {
        Vector2 inside = new Vector2(60, 36);

        Assert.IsFalse(DrawSelectableRowFrame(inside, down: true, pressed: true, released: false));
        Assert.IsTrue(DrawSelectableRowFrame(inside, down: false, pressed: false, released: true));
        Assert.IsTrue(_drawList.hasGeometry, "Selectable row drew no visuals.");
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
    public void InteractDoesNotMaterializeRepaintStateUntilActive()
    {
        Vector2 inside = new Vector2(60, 36);
        Vector2 outside = new Vector2(400, 200);

        DrawInteractionFrame(outside);
        Assert.AreEqual(0, InteractionRepaintStateCount());

        DrawInteractionFrame(inside);
        Assert.AreEqual(1, InteractionRepaintStateCount());

        DrawInteractionFrame(outside);
        Assert.AreEqual(1, InteractionRepaintStateCount(), "The cleared entry should remain cached for allocation-free reuse.");
    }

    static int InteractionRepaintStateCount()
    {
        var stateType = typeof(NowControls).GetNestedType("InteractionRepaintState", BindingFlags.NonPublic);
        var storeDefinition = typeof(NowControlState).GetNestedType("Store`1", BindingFlags.NonPublic);

        Assert.NotNull(stateType);
        Assert.NotNull(storeDefinition);

        var storeType = storeDefinition.MakeGenericType(stateType);
        var entriesField = storeType.GetField("entries", BindingFlags.Static | BindingFlags.Public);

        Assert.NotNull(entriesField);
        return ((IDictionary)entriesField.GetValue(null)).Count;
    }

    [Test]
    public void ButtonDoesNotClickWhenReleasedOutside()
    {
        Assert.IsFalse(DrawButtonFrame(new Vector2(60, 36), true, true, false));
        Assert.IsFalse(DrawButtonFrame(new Vector2(400, 200), false, false, true));
    }

    [Test]
    public void ExactMeasureReplayContinuesFromTheActiveOccurrenceOffset()
    {
        NowCallSiteId site = NowControls.SiteId("test", 253);

        using (NowInput.Begin(_provider, Surface))
        {
            NowResolvedId active1 = NowControls.GetControlId(default, site);
            NowResolvedId active2 = NowControls.GetControlId(default, site);

            int snapshot = NowLayout.BeginMeasurePass();
            NowResolvedId measured1;
            NowResolvedId measured2;

            try
            {
                measured1 = NowControls.GetControlId(default, site);
                measured2 = NowControls.GetControlId(default, site);
            }
            finally
            {
                NowLayout.EndMeasurePass(snapshot);
            }

            NowResolvedId drawn1 = NowControls.GetControlId(default, site);
            NowResolvedId drawn2 = NowControls.GetControlId(default, site);

            Assert.AreNotEqual(active1, active2, "Repeated controls at one call site must salt apart.");
            Assert.AreEqual(drawn1, measured1,
                "A measured region after active content must start at the active occurrence offset.");
            Assert.AreEqual(drawn2, measured2,
                "The real replay must preserve measured occurrence order from that offset.");
        }
    }

    [Test]
    public void PassiveOnlyRegionReservesItsControlIdOccurrences()
    {
        NowCallSiteId site = NowControls.SiteId("test", 254);

        using (NowInput.Begin(_provider, Surface))
        {
            NowResolvedId before = NowControls.GetControlId(default, site);

            NowInput.BeginPassive();
            NowResolvedId passive = NowControls.GetControlId(default, site);
            NowInput.EndPassive();

            NowResolvedId after = NowControls.GetControlId(default, site);

            Assert.AreEqual(3, new HashSet<NowResolvedId> { before, passive, after }.Count,
                "A passive-only subtree must reserve its logical slots in the surrounding draw.");
        }
    }

    [Test]
    public void NestedPassiveRegionKeepsMeasureAndDrawOccurrencesAligned()
    {
        NowCallSiteId site = NowControls.SiteId("test", 255);
        var measured = new List<NowResolvedId>();
        var drawn = new List<NowResolvedId>();

        using (NowInput.Begin(_provider, Surface))
        {
            NowLayout.RunMeasured(new NowRect(0f, 0f, 100f, 20f), () =>
            {
                List<NowResolvedId> ids = NowLayout.isMeasurePass ? measured : drawn;
                ids.Add(NowControls.GetControlId(default, site));

                NowInput.BeginPassive();
                try
                {
                    ids.Add(NowControls.GetControlId(default, site));
                }
                finally
                {
                    NowInput.EndPassive();
                }

                ids.Add(NowControls.GetControlId(default, site));
            });
        }

        CollectionAssert.AreEqual(measured, drawn);
        Assert.AreEqual(3, new HashSet<NowResolvedId>(drawn).Count,
            "The nested passive subtree must consume one stable occurrence in both passes.");
    }

    [Test]
    public void NestedInputSurfaceDoesNotResetOuterControlOccurrences()
    {
        NowCallSiteId site = NowControls.SiteId("test", 256);

        using (NowInput.Begin(_provider, Surface))
        {
            NowResolvedId before = NowControls.GetControlId(default, site);
            NowResolvedId nested;

            using (NowInput.Begin(_provider, Surface))
                nested = NowControls.GetControlId(default, site);

            NowResolvedId after = NowControls.GetControlId(default, site);

            Assert.AreEqual(3, new HashSet<NowResolvedId> { before, nested, after }.Count,
                "A nested input surface must not restart the outer surface's occurrence sequence.");
        }
    }

    [Test]
    public void ButtonPressTakesFocus()
    {
        NowResolvedId expectedId;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            expectedId = NowControls.GetControlId("Save");

        DrawButtonFrame(new Vector2(60, 36), true, true, false);
        Assert.AreEqual(expectedId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void FocusedButtonActivatesOnSubmit()
    {
        NowResolvedId id;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            id = NowControls.GetControlId("Save");

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
        NowResolvedId id = interaction.id;

        Assert.IsTrue(focused);
        Assert.IsFalse(submitted);
        Assert.AreEqual(id, NowFocus.focusedResolvedId);

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
        using (_drawList.Begin(Surface))
        {
            NowCallSiteId site = NowControls.SiteId("builder-fallback", 7001);
            var fallback = DrawBuilderFallbackInteraction(default, site, out _, out _);
            var explicitId = DrawBuilderFallbackInteraction(7002, site, out _, out _);

            Assert.IsTrue(fallback.id.hasValue);
            Assert.AreEqual(
                NowControls.GetControlId(new NowId(7002)),
                explicitId.id);
            Assert.AreNotEqual(fallback.id, explicitId.id);
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
    public void RepeatedCallSiteGetsDistinctStableIds()
    {
        NowResolvedId first, second, third;
        NowCallSiteId site = NowControls.SiteId("test", 423);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            first = NowControls.GetControlId(default, site);
            second = NowControls.GetControlId(default, site);
            third = NowControls.GetControlId(default, site);
        }

        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(second, third);
        Assert.AreNotEqual(first, third);

        NowResolvedId firstNextFrame;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            firstNextFrame = NowControls.GetControlId(default, site);

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
    public void ResolvedChildIdsAreStableAndNeverEmpty()
    {
        NowResolvedId parent = TestId(7);

        Assert.AreEqual(parent.Child(3), parent.Child(3));
        Assert.AreNotEqual(parent.Child(3), parent.Child(4));
        Assert.IsTrue(parent.Child(0).hasValue);
    }

    [Test]
    public void NowIdSupportsStringIntAndDefaultIdentity()
    {
        NowId none = (string)null;
        NowId stringId = "row-7";
        NowId intId = 77;

        Assert.IsFalse(none.hasValue);

        Assert.IsTrue(stringId.isString);
        Assert.AreEqual("row-7", stringId.stringValue);

        Assert.IsTrue(intId.isInt);
        Assert.AreEqual(77, intId.intValue);

        using (NowInput.Begin(_provider, Surface))
        {
            NowCallSiteId site = NowControls.SiteId("authored-id-resolution", 1);
            NowResolvedId stringResolved = NowInput.GetId(stringId, site);
            NowResolvedId intResolved = NowInput.GetId(intId, site);

            Assert.IsTrue(stringResolved.hasValue);
            Assert.IsTrue(intResolved.hasValue);
            Assert.AreNotEqual(stringResolved, intResolved);

            using (NowControls.IdScope("panel"))
            {
                Assert.AreNotEqual(stringResolved, NowInput.GetId(stringId, site));
                Assert.AreNotEqual(intResolved, NowInput.GetId(intId, site));
            }
        }
    }

    [Test]
    public void NowIdAcceptsIntegerZeroButRejectsEmptyStringsAndDefaultInteraction()
    {
        NowId zero = 0;
        Assert.IsTrue(zero.hasValue);
        Assert.IsTrue(zero.isInt);
        Assert.AreEqual(0, zero.intValue);
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
        NowResolvedId byLabel, byId;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
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
        NowResolvedId byLabel, byId;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            byLabel = NowControls.GetControlId("Delete");
            byId = NowControls.GetControlId(new NowId(9007));
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
        NowResolvedId outer = NowControls.GetControlId("Delete");
        NowResolvedId scoped;

        using (NowControls.IdScope("row-1"))
            scoped = NowControls.GetControlId("Delete");

        NowResolvedId scopedOther;

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
                using (var button = NowLayout.Button("Item").SetId("item").Begin())
                {
                    small = button.rect;
                    NowLayout.ReserveRect(width: 40f, height: 20f);
                }

                using (NowControls.IdScope(1002))
                using (var button = NowLayout.Button("Item").SetId("item").Begin())
                {
                    large = button.rect;
                    NowLayout.ReserveRect(width: 160f, height: 20f);
                }
            }
        }

        Assert.GreaterOrEqual(small.width, 40f);
        Assert.GreaterOrEqual(large.width, 160f);
        Assert.Less(small.width, large.width, "Scoped controls with the same child id must not share a layout cache.");
    }

    [Test]
    public void CopiedTreeScopeCannotReturnAFrameAfterItWasRentedAgain()
    {
        var firstState = new NowTreeViewState { selectedKey = NowTreeNodeKey.From(11) };
        var secondState = new NowTreeViewState { selectedKey = NowTreeNodeKey.From(22) };
        var thirdState = new NowTreeViewState { selectedKey = NowTreeNodeKey.From(33) };
        var first = NowLayout.TreeView(firstState).SetId("first-tree").Begin();
        var stale = first;
        NowTreeViewScope second = default;
        NowTreeViewScope third = default;

        try
        {
            first.Dispose();
            second = NowLayout.TreeView(secondState).SetId("second-tree").Begin();

            // This copied handle refers to the frame now owned by `second`.
            // It must not put that live frame back into the pool.
            stale.Dispose();
            third = NowLayout.TreeView(thirdState).SetId("third-tree").Begin();

            Assert.AreEqual(NowTreeNodeKey.From(22), second.selectedKey);
            Assert.AreEqual(NowTreeNodeKey.From(33), third.selectedKey);
        }
        finally
        {
            third.Dispose();
            second.Dispose();
            stale.Dispose();
            first.Dispose();
        }
    }

    [Test]
    public void NestedTreeScopesRequireReverseOrderButOuterCanRetry()
    {
        var outerState = new NowTreeViewState { selectedKey = NowTreeNodeKey.From(41) };
        var innerState = new NowTreeViewState { selectedKey = NowTreeNodeKey.From(42) };
        var outer = NowLayout.TreeView(outerState).SetId("outer-tree").Begin();
        var inner = NowLayout.TreeView(innerState).SetId("inner-tree").Begin();

        try
        {
            Assert.Throws<System.InvalidOperationException>(() => outer.Dispose());
            Assert.AreEqual(NowTreeNodeKey.From(41), outer.selectedKey, "A rejected out-of-order dispose must leave the outer lease usable.");
        }
        finally
        {
            inner.Dispose();
            outer.Dispose();
        }
    }

    [Test]
    public void SpatialNavigationMovesFocusRight()
    {
        var left = new NowRect(10, 10, 80, 30);
        var right = new NowRect(200, 10, 80, 30);

        using (NowInput.Begin(_provider, Surface))
        {
            _provider.snapshot = default;
            NowFocus.Register(TestId(1), left);
            NowFocus.Register(TestId(2), right);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, new Vector2(1f, 0f),
            false, false, false, false, false, false, 2, 2f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [Test]
    public void EmptyPrimaryPressClearsFocusWhenInputPassesShareUnityFrame()
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
            NowControls.Interact(TestId(1), ButtonRect, out _, out _);

        NowFocus.Focus(TestId(1));
        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId);

        _provider.snapshot = new NowInputSnapshot(
            new Vector2(400f, 200f),
            primaryDown: true,
            primaryPressed: true,
            primaryReleased: false);
        NowControlState.BeginRepaintTracking();

        using (NowInput.Begin(_provider, Surface))
            NowControls.Interact(TestId(1), ButtonRect, out _, out _);

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId,
            "An unclaimed press must blur focus even when IMGUI already registered controls during another event in the same Unity frame.");
        Assert.IsTrue(NowControlState.EndRepaintTracking(),
            "Background defocus must request a repaint so the stale focus and caret visuals disappear.");
    }

    [Test]
    public void FocusableRegionClaimsPrimaryPressWhenNestedInteractionOwnsCapture()
    {
        NowFocus.Focus(TestId(1));
        _provider.snapshot = new NowInputSnapshot(
            ButtonRect.center,
            primaryDown: true,
            primaryPressed: true,
            primaryReleased: false);

        using (NowInput.Begin(_provider, Surface))
        {
            var nested = NowInput.Interact(TestId(2), ButtonRect);
            Assert.IsTrue(nested.pressed);
            NowFocus.Register(TestId(1), ButtonRect);
        }

        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId,
            "A raw nested interaction inside the focused parent must not make the parent region look like empty space.");
    }

    [Test]
    public void RetainFocusProtectsBackgroundPrimaryPress()
    {
        NowFocus.Focus(TestId(1));
        _provider.snapshot = new NowInputSnapshot(
            new Vector2(400f, 200f),
            primaryDown: true,
            primaryPressed: true,
            primaryReleased: false);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.RetainFocus();
            NowControls.Interact(TestId(1), ButtonRect, out _, out _);
        }

        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId,
            "Focus-retaining overlays must preserve their owner's selection when a press dismisses them.");
    }

    [Test]
    public void DirectionalNavigationLockBlocksSpatialMovement()
    {
        RegisterFocusPolicyRow(NowFocusNavigationLock.Directional);
        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [TestCase(false, 3)]
    [TestCase(true, 1)]
    public void DirectionalNavigationLockAllowsTabTraversal(bool previous, int expected)
    {
        RegisterFocusPolicyRow(NowFocusNavigationLock.Directional);
        _provider.snapshot = NavigationSnapshot(Vector2.zero, previous: previous, next: !previous);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(expected), NowFocus.focusedResolvedId);
    }

    [Test]
    public void FullNavigationLockBlocksTabTraversal()
    {
        RegisterFocusPolicyRow(NowFocusNavigationLock.All);
        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [Test]
    public void NavigationLockDoesNotFollowFocusToAnotherControl()
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), new NowRect(10, 10, 80, 30));
            NowFocus.Register(TestId(2), new NowRect(110, 10, 80, 30));
            NowFocus.Register(TestId(3), new NowRect(210, 10, 80, 30));
            NowFocus.Focus(TestId(2));
            NowFocus.LockNavigation();
            NowFocus.Focus(TestId(3));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, previous: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [Test]
    public void CancelOwnerReceivesCancelBeforeGlobalFocusClear()
    {
        RegisterFocusPolicyRow(NowFocusNavigationLock.Directional, consumesCancel: true);
        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, true, false, false, false,
            cancelDown: true, cancelPressed: true, cancelReleased: false, frame: 2, time: 2f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [Test]
    public void CancelOwnerThatDisappearsDoesNotLeaveGhostFocus()
    {
        RegisterFocusPolicyRow(NowFocusNavigationLock.Directional, consumesCancel: true);
        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false,
            cancelDown: true, cancelPressed: true, cancelReleased: false, frame: 2, time: 2f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void ExplicitFocusSurvivesSimultaneousNavigationBeforeRegistration()
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), new NowRect(10, 10, 80, 30));
            NowFocus.Register(TestId(3), new NowRect(210, 10, 80, 30));
        }

        NowFocus.Focus(TestId(2));
        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [Test]
    public void DirectionalNavigationWithoutFocusStartsAtOppositeEdge()
    {
        var left = new NowRect(10, 10, 80, 30);
        var right = new NowRect(200, 10, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(2), right);
            NowFocus.Register(TestId(1), left);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId, "Right navigation should start at the left edge, not draw order.");
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
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
            NowFocus.Register(TestId(3), third);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
            NowFocus.Register(TestId(3), third);
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, previous: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId);
    }

    [Test]
    public void ImmediateTabNavigationDoesNotWaitForUnityFrameCount()
    {
        var first = new NowRect(10, 10, 80, 30);
        var second = new NowRect(10, 50, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
            NowFocus.Focus(TestId(1));
        }

        // Repeated editor IMGUI passes may all share one Time.frameCount.
        // Re-registering must update the pass registry rather than grow it.
        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
        }

        Assert.AreEqual(2, NowFocus.immediateRegistrationCount,
            "Repeated IMGUI passes in one Unity frame must not grow the focus registry.");

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.ProcessImmediateTabNavigationPass();
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
        }

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId,
            "An IMGUI Tab pulse must traverse the latest registry even when Time.frameCount has not advanced.");

        _provider.snapshot = NavigationSnapshot(Vector2.zero, previous: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ProcessImmediateTabNavigationPass();

        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId);
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
            NowFocus.Register(TestId(1), baseRect);
            NowFocus.Focus(TestId(1));
            NowOverlay.DeferScreen(popupRect, TestId(100), () =>
            {
                NowFocus.Register(TestId(2), popupFirst);
                NowFocus.Register(TestId(3), popupSecond);
            });
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.ForceNewFrame();
        }

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
        Assert.IsFalse(NowFocus.IsFocused(TestId(1)), "Base focus must not be visible while an overlay layer is active.");
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
            NowFocus.Register(TestId(1), baseRect);
            NowFocus.Focus(TestId(1));
            NowOverlay.DeferScreen(popupRect, TestId(100), () => NowFocus.Register(TestId(2), popupItem));
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
        bool baseFocused;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.ForceNewFrame();
            submitted = NowFocus.SubmitPressed(TestId(1));
            baseFocused = NowFocus.IsFocused(TestId(1));
        }

        Assert.IsFalse(submitted);
        Assert.IsFalse(baseFocused);
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
            NowOverlay.DeferScreen(parentRect, TestId(100), () =>
            {
                NowFocus.Register(TestId(2), parentItem);
                NowFocus.Focus(TestId(2));
                NowOverlay.DeferScreen(childRect, TestId(200), () => NowFocus.Register(TestId(3), childItem));
            });
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.ForceNewFrame();
        }

        Assert.AreEqual(TestId(3), NowFocus.focusedResolvedId);
        Assert.IsFalse(NowFocus.IsFocused(TestId(2)), "Parent overlay focus must yield to the nested overlay layer.");
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
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
            NowFocus.Register(TestId(3), third);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0.2f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(1), NowFocus.focusedResolvedId, "Held navigation should wait for the repeat delay.");

        NowFocus.Reset();
        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0f);

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), first);
            NowFocus.Register(TestId(2), second);
            NowFocus.Register(TestId(3), third);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right, time: 0.5f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId, "Held navigation should repeat after the delay.");
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
            NowFocus.Register(TestId(1), first, NowFocusNavigation.Right(TestId(3)));
            NowFocus.Register(TestId(2), nearest);
            NowFocus.Register(TestId(3), explicitTarget);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(3), NowFocus.focusedResolvedId);
    }

    [Test]
    public void ExplicitDirectionalNavigationFallsBackWhenTargetIsMissing()
    {
        var first = new NowRect(10, 10, 80, 30);
        var nearest = new NowRect(120, 10, 80, 30);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), first, NowFocusNavigation.Right(TestId(99)));
            NowFocus.Register(TestId(2), nearest);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
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
            NowFocus.Register(TestId(3), MemoryTopRight);
            NowFocus.Register(TestId(4), MemoryMiddle);
            NowFocus.Register(TestId(5), MemoryBottomLeft);
            NowFocus.Register(TestId(6), MemoryBottomCenter);
            NowFocus.Register(TestId(7), MemoryBottomRight);
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
        NowFocus.Focus(TestId(3));

        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(TestId(4), NowFocus.focusedResolvedId, "First move down should reach the offset middle button.");

        RegisterMemoryLayout();
        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(TestId(7), NowFocus.focusedResolvedId, "Second move down should return to the starting column, not the middle button's column.");
    }

    [Test]
    public void ExplicitFocusClearsDirectionalNavigationMemory()
    {
        RegisterMemoryLayout();
        NowFocus.Focus(TestId(3));

        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(TestId(4), NowFocus.focusedResolvedId);

        RegisterMemoryLayout();
        NowFocus.Focus(TestId(5));

        NavigateMemoryLayout(Vector2.up);
        Assert.AreEqual(TestId(4), NowFocus.focusedResolvedId, "After an explicit focus the stale cross-axis anchor must not pull navigation sideways.");
    }

    [Test]
    public void HorizontalMoveUpdatesDirectionalNavigationMemory()
    {
        RegisterMemoryLayout();
        NowFocus.Focus(TestId(5));

        NavigateMemoryLayout(Vector2.right);
        Assert.AreEqual(TestId(6), NowFocus.focusedResolvedId);

        RegisterMemoryLayout();
        NavigateMemoryLayout(Vector2.right);
        Assert.AreEqual(TestId(7), NowFocus.focusedResolvedId);

        RegisterMemoryLayout();
        NavigateMemoryLayout(Vector2.up);
        Assert.AreEqual(TestId(3), NowFocus.focusedResolvedId, "Horizontal movement must re-anchor the column used by later vertical moves.");
    }

    void RegisterShiftedColumns(float shift)
    {
        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        {
            NowFocus.Register(TestId(1), new NowRect(410 + shift, 10, 80, 30));
            NowFocus.Register(TestId(2), new NowRect(210 + shift, 10, 80, 30));
            NowFocus.Register(TestId(3), new NowRect(410 + shift, 60, 80, 30));
            NowFocus.Register(TestId(4), new NowRect(210 + shift, 60, 80, 30));
            NowFocus.Register(TestId(5), new NowRect(410 + shift, 110, 80, 30));
            NowFocus.Register(TestId(6), new NowRect(210 + shift, 110, 80, 30));
        }
    }

    [Test]
    public void NavigationMemoryShiftsWithScrolledContent()
    {
        RegisterShiftedColumns(0f);
        NowFocus.Focus(TestId(2));

        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(TestId(4), NowFocus.focusedResolvedId);

        RegisterShiftedColumns(-200f);
        NavigateMemoryLayout(Vector2.down);
        Assert.AreEqual(TestId(6), NowFocus.focusedResolvedId, "The cross-axis anchor must move with scrolled content, not point at the old screen position.");
    }

    [Test]
    public void EdgeFocusSeedingPrefersVisibleControls()
    {
        var viewport = new NowRect(0, 0, 120, 40);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        using (Now.Mask(viewport))
        using (NowFocus.BeginScrollRegion(TestId(500)))
        {
            NowFocus.Register(TestId(1), new NowRect(0, -100, 100, 30));
            NowFocus.Register(TestId(2), new NowRect(0, 0, 100, 30));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.down);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId, "Seeding should land on a visible control, not one clipped out of the scroll viewport.");
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
            NowFocus.Register(TestId(1), first, NowFocusNavigation.Next(TestId(3)));
            NowFocus.Register(TestId(2), second);
            NowFocus.Register(TestId(3), explicitTarget);
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(Vector2.zero, next: true);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(3), NowFocus.focusedResolvedId);
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
            Now.Button(first, "First").SetId(TestId(1)).SetNavigation(NowFocusNavigation.Right(TestId(3))).Draw();
            Now.Button(nearest, "Nearest").SetId(TestId(2)).Draw();
            Now.Button(explicitTarget, "Target").SetId(TestId(3)).Draw();
        }

        NowFocus.Focus(TestId(1));
        _provider.snapshot = NavigationSnapshot(Vector2.right);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(3), NowFocus.focusedResolvedId);
    }

    [Test]
    public void ScrollRegionKeepsCulledControlsNavigable()
    {
        var viewport = new NowRect(0, 0, 120, 40);

        _provider.snapshot = default;

        using (NowInput.Begin(_provider, Surface))
        using (Now.Mask(viewport))
        using (NowFocus.BeginScrollRegion(TestId(500)))
        {
            NowFocus.Register(TestId(1), new NowRect(0, 0, 100, 30));
            NowFocus.Register(TestId(2), new NowRect(0, 50, 100, 30));
            NowFocus.Focus(TestId(1));
        }

        _provider.snapshot = NavigationSnapshot(new Vector2(0f, -1f));

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(TestId(2), NowFocus.focusedResolvedId);
    }

    [Test]
    public void CopiedScrollScopeCannotPopTheOuterFocusRegion()
    {
        _provider.snapshot = default;
        NowScrollScope outer = default;
        NowScrollScope inner = default;
        NowScrollScope staleInner = default;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            try
            {
                outer = Now.ScrollView(
                    new NowRect(0f, 0f, 220f, 140f),
                    "copied-scroll-outer").Begin();
                NowResolvedId outerRegion = NowFocus.currentScrollRegionResolvedId;

                inner = Now.ScrollView(
                    new NowRect(10f, 10f, 180f, 80f),
                    "copied-scroll-inner").Begin();
                NowResolvedId innerRegion = NowFocus.currentScrollRegionResolvedId;
                staleInner = inner;

                Assert.IsTrue(outerRegion.hasValue);
                Assert.AreNotEqual(outerRegion, innerRegion);

                inner.Dispose();
                Assert.AreEqual(outerRegion, NowFocus.currentScrollRegionResolvedId);

                staleInner.Dispose();
                Assert.AreEqual(outerRegion, NowFocus.currentScrollRegionResolvedId,
                    "disposing a stale copy of the inner scroll scope must not pop the live outer focus region");
            }
            finally
            {
                staleInner.Dispose();
                inner.Dispose();
                outer.Dispose();
            }

            Assert.AreEqual(NowResolvedId.None, NowFocus.currentScrollRegionResolvedId);
        }
    }

    [Test]
    public void CancelClearsFocus()
    {
        NowFocus.Focus(TestId(42));

        _provider.snapshot = new NowInputSnapshot(
            true, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false,
            cancelDown: true, cancelPressed: true, cancelReleased: false, frame: 1, time: 1f);

        using (NowInput.Begin(_provider, Surface))
            NowFocus.ForceNewFrame();

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void ControlStateSlotsPersistAndReset()
    {
        NowResolvedId id = TestId(7);
        ref int slot = ref NowControlState.Get<int>(id);
        slot = 123;

        Assert.AreEqual(123, NowControlState.Get<int>(id));

        NowControlState.Reset();
        Assert.AreEqual(0, NowControlState.Get<int>(id));
    }

    [Test]
    public void ControlStateNamedSlotsUseDerivedIds()
    {
        NowResolvedId id = TestId(7);
        NowControlState.Warmup(id, "slot", 12);
        Assert.AreEqual(12, NowControlState.Get<int>(id, "slot"));

        NowControlState.Get<int>(id, "slot") = 34;

        Assert.AreEqual(34, NowControlState.Get<int>(id, "slot"));
        Assert.AreEqual(0, NowControlState.Get<int>(id, "other"));
    }

    [Test]
    public void RepeatPulsesOnInitialPress()
    {
        NowResolvedId id = TestId(1);
        Assert.IsTrue(NowControlState.Repeat(id, held: true));
        Assert.IsFalse(NowControlState.Repeat(id, held: true), "No pulse before the repeat delay.");
        Assert.IsFalse(NowControlState.Repeat(id, held: false));
        Assert.IsTrue(NowControlState.Repeat(id, held: true), "Releasing resets the initial pulse.");
    }

    [Test]
    public void RepeatNamedKeysUseSeparateSlots()
    {
        NowResolvedId id = TestId(7);
        Assert.IsTrue(NowControlState.Repeat(id, "left", held: true));
        Assert.IsTrue(NowControlState.Repeat(id, "right", held: true));
        Assert.IsFalse(NowControlState.Repeat(id, "left", held: true));
        Assert.IsFalse(NowControlState.Repeat(id, "right", held: true));
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
                NowLayout.ReserveRect(128, 128);
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

#if NOWUI_UGUI
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

            NowFocus.Focus(TestId(7));
            eventSystem.SetSelectedGameObject(selectable);

            if (eventSystem.currentSelectedGameObject == null)
                Assert.Ignore("EventSystem selection inactive in this environment.");

            using (NowInput.Begin(_provider, Surface))
                NowFocus.ForceNewFrame();

            Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId, "UGUI selection must clear NowUI focus.");

            eventSystem.SetSelectedGameObject(selectable);
            NowFocus.Focus(TestId(9));
            Assert.IsNull(eventSystem.currentSelectedGameObject, "NowUI focus must deselect the EventSystem.");
            Assert.AreEqual(TestId(9), NowFocus.focusedResolvedId);
        }
        finally
        {
            Object.DestroyImmediate(selectable);
            Object.DestroyImmediate(eventSystemObject);
        }
    }
#endif

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
                NowLayout.ReserveRect(180f, 30f);

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
