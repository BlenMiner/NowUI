using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowInteractionRepaintTests
{
    static readonly Vector2 Size = new Vector2(100f, 100f);

    static NowInputSnapshot Snapshot(
        Vector2 position,
        bool hasPointer = true,
        NowPointerButtons down = NowPointerButtons.None,
        NowPointerButtons pressed = NowPointerButtons.None,
        NowPointerButtons released = NowPointerButtons.None,
        Vector2 scroll = default,
        Vector2 navigation = default,
        bool submitDown = false,
        bool submitPressed = false,
        bool cancelDown = false,
        bool cancelPressed = false,
        bool focusNextPressed = false)
    {
        return new NowInputSnapshot(
            hasPointer,
            position,
            position,
            Vector2.zero,
            down,
            pressed,
            released,
            scroll,
            navigation,
            false,
            focusNextPressed,
            submitDown,
            submitPressed,
            false,
            cancelDown,
            cancelPressed,
            false,
            1,
            0f);
    }

    sealed class MockInputProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool hasSnapshot = true;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = this.snapshot;
            return hasSnapshot;
        }
    }

    [Test]
    public void FromSnapshotDetectsPointerInsideSurface()
    {
        var inside = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);
        var outside = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(150f, 50f)), Size);
        var noPointer = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f), hasPointer: false), Size);

        Assert.IsTrue(inside.pointerInside);
        Assert.IsFalse(outside.pointerInside);
        Assert.IsFalse(noPointer.pointerInside);
    }

    [Test]
    public void UnchangedInputReportsNoChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);

        Assert.IsFalse(current.HasChangedSince(previous));
    }

    [Test]
    public void PointerMovementBelowEpsilonReportsNoChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50.3f, 50f)), Size);

        Assert.IsFalse(current.HasChangedSince(previous));
    }

    [Test]
    public void PointerMovementInsideSurfaceReportsChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(60f, 50f)), Size);

        Assert.IsTrue(current.HasChangedSince(previous));
    }

    [Test]
    public void PointerMovementOutsideSurfaceReportsNoChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(150f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(180f, 50f)), Size);

        Assert.IsFalse(current.HasChangedSince(previous));
    }

    [Test]
    public void PointerEnteringSurfaceReportsChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(150f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);

        Assert.IsTrue(current.HasChangedSince(previous));
    }

    [Test]
    public void ButtonPressEdgeReportsChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(50f, 50f), down: NowPointerButtons.Primary, pressed: NowPointerButtons.Primary),
            Size);

        Assert.IsTrue(current.HasChangedSince(previous));
    }

    [Test]
    public void ScrollWhilePointerInsideReportsChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(50f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(50f, 50f), scroll: new Vector2(0f, 1f)),
            Size);

        Assert.IsTrue(current.HasChangedSince(previous));
    }

    [Test]
    public void ScrollWhilePointerOutsideReportsNoChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(150f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(150f, 50f), scroll: new Vector2(0f, 1f)),
            Size);

        Assert.IsFalse(current.HasChangedSince(previous));
    }

    [Test]
    public void NavigationChangeReportsChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(150f, 50f)), Size);
        var current = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(150f, 50f), navigation: new Vector2(1f, 0f)),
            Size);

        Assert.IsTrue(current.HasChangedSince(previous));
    }

    [Test]
    public void SubmitAndCancelEdgesReportChange()
    {
        var previous = NowInteractionInputState.FromSnapshot(Snapshot(new Vector2(150f, 50f)), Size);
        var submit = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(150f, 50f), submitDown: true, submitPressed: true),
            Size);
        var cancel = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(150f, 50f), cancelDown: true, cancelPressed: true),
            Size);
        var focus = NowInteractionInputState.FromSnapshot(
            Snapshot(new Vector2(150f, 50f), focusNextPressed: true),
            Size);

        Assert.IsTrue(submit.HasChangedSince(previous));
        Assert.IsTrue(cancel.HasChangedSince(previous));
        Assert.IsTrue(focus.HasChangedSince(previous));
    }

    [Test]
    public void TrackerFirstSamplePrimesWithoutReportingChange()
    {
        var tracker = new NowInteractionRepaintTracker();
        var provider = new MockInputProvider { snapshot = Snapshot(new Vector2(50f, 50f)) };
        var surface = new NowInputSurface(Size);

        Assert.IsFalse(tracker.HasInputChanged(provider, surface));
        Assert.IsFalse(tracker.HasInputChanged(provider, surface));

        provider.snapshot = Snapshot(new Vector2(70f, 50f));

        Assert.IsTrue(tracker.HasInputChanged(provider, surface));
    }

    [Test]
    public void TrackerPollDoesNotAdvanceStoredState()
    {
        var tracker = new NowInteractionRepaintTracker();
        var provider = new MockInputProvider { snapshot = Snapshot(new Vector2(50f, 50f)) };
        var surface = new NowInputSurface(Size);

        tracker.StoreFrameInput(provider.snapshot, Size);
        provider.snapshot = Snapshot(new Vector2(70f, 50f));

        Assert.IsTrue(tracker.HasInputChanged(provider, surface));
        Assert.IsTrue(tracker.HasInputChanged(provider, surface));
    }

    [Test]
    public void TrackerStoreFrameInputEstablishesBaseline()
    {
        var tracker = new NowInteractionRepaintTracker();
        var provider = new MockInputProvider { snapshot = Snapshot(new Vector2(50f, 50f)) };
        var surface = new NowInputSurface(Size);

        tracker.StoreFrameInput(provider.snapshot, Size);

        Assert.IsFalse(tracker.HasInputChanged(provider, surface));
    }

    [Test]
    public void TrackerWantsRepaintDrivesShouldRepaint()
    {
        var tracker = new NowInteractionRepaintTracker();
        var provider = new MockInputProvider { snapshot = Snapshot(new Vector2(50f, 50f)) };
        var surface = new NowInputSurface(Size);

        tracker.StoreFrameInput(provider.snapshot, Size);
        tracker.SetWantsRepaint(true);

        Assert.IsTrue(tracker.wantsRepaint);
        Assert.IsTrue(tracker.ShouldRepaint(provider, surface));

        tracker.SetWantsRepaint(false);

        Assert.IsFalse(tracker.ShouldRepaint(provider, surface));
    }

    [Test]
    public void TrackerResetRequiresRepriming()
    {
        var tracker = new NowInteractionRepaintTracker();
        var provider = new MockInputProvider { snapshot = Snapshot(new Vector2(50f, 50f)) };
        var surface = new NowInputSurface(Size);

        tracker.StoreFrameInput(provider.snapshot, Size);
        tracker.Reset();
        provider.snapshot = Snapshot(new Vector2(70f, 50f));

        Assert.IsFalse(tracker.HasInputChanged(provider, surface));
        provider.snapshot = Snapshot(new Vector2(90f, 50f));
        Assert.IsTrue(tracker.HasInputChanged(provider, surface));
    }

    [Test]
    public void TrackerTreatsMissingSnapshotAsDefault()
    {
        var tracker = new NowInteractionRepaintTracker();
        var provider = new MockInputProvider { snapshot = Snapshot(new Vector2(50f, 50f)) };
        var surface = new NowInputSurface(Size);

        tracker.StoreFrameInput(provider.snapshot, Size);
        provider.hasSnapshot = false;

        Assert.IsTrue(tracker.HasInputChanged(provider, surface));
    }
}
