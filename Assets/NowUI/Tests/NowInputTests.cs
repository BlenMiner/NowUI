using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowInputTests
{
    static readonly FieldInfo InputSnapshotField = typeof(NowInput).GetField(
        "_snapshot",
        BindingFlags.NonPublic | BindingFlags.Static);

    readonly Rect _rect = new Rect(10, 10, 40, 30);

    MockInputProvider _provider;

    Action _previousRepaintRequested;

    Action<NowIMGUIInputProvider> _previousHostRepaintRequested;

    Action<NowIMGUIInputProvider, float> _previousHostRepaintAfterRequested;

    [SetUp]
    public void SetUp()
    {
        _previousRepaintRequested = NowIMGUIInputProvider.repaintRequested;
        _previousHostRepaintRequested = NowIMGUIInputProvider.hostRepaintRequested;
        _previousHostRepaintAfterRequested = NowIMGUIInputProvider.hostRepaintAfterRequested;
        NowIMGUIInputProvider.repaintRequested = () => { };
        NowIMGUIInputProvider.hostRepaintRequested = null;
        NowIMGUIInputProvider.hostRepaintAfterRequested = null;
        NowInput.Reset();
        _provider = new MockInputProvider();
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
        NowIMGUIInputProvider.repaintRequested = _previousRepaintRequested;
        NowIMGUIInputProvider.hostRepaintRequested = _previousHostRepaintRequested;
        NowIMGUIInputProvider.hostRepaintAfterRequested = _previousHostRepaintAfterRequested;
    }

    [Test]
    public void InteractionReportsHoverFromProviderSnapshot()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var interaction = NowInput.Interact(1, _rect);

            Assert.IsTrue(interaction.hovered);
            Assert.IsFalse(interaction.pressed);
            Assert.IsFalse(interaction.clicked);
        }
    }

    [Test]
    public void InteractionIgnoresPointerOutsideAmbientMask()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(45, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Mask(new NowRect(10, 10, 20, 30)))
        {
            var interaction = NowInput.Interact(1, _rect);

            Assert.IsFalse(interaction.hovered);
            Assert.IsFalse(interaction.pressed);
            Assert.IsFalse(interaction.held);
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void HoverUsesNestedAmbientMaskIntersection()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(32, 20), false, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Mask(new NowRect(10, 10, 40, 30)))
        using (Now.Mask(new NowRect(10, 10, 20, 30)))
        {
            Assert.IsFalse(NowInput.IsHovered(_rect));
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(28, 20), false, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Mask(new NowRect(10, 10, 40, 30)))
        using (Now.Mask(new NowRect(10, 10, 20, 30)))
        {
            Assert.IsTrue(NowInput.IsHovered(_rect));
        }
    }

    [Test]
    public void ScrollDeltaIgnoresPointerOutsideAmbientMask()
    {
        _provider.snapshot = new NowInputSnapshot(
            true,
            new Vector2(45, 20),
            new Vector2(45, 20),
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            new Vector2(0f, -1f),
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            1,
            0.5f);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Mask(new NowRect(10, 10, 20, 30)))
        {
            Assert.AreEqual(Vector2.zero, NowInput.ConsumeScrollDelta(_rect));
        }
    }

    [Test]
    public void IMGUIScrollConsumptionUsesTheEventAndMarksGUIChanged()
    {
        bool previousChanged = GUI.changed;
        Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        int repaintCount = 0;
        var scrollEvent = new Event
        {
            type = EventType.ScrollWheel,
            mousePosition = new Vector2(24f, 22f),
            delta = new Vector2(0f, 3f)
        };

        try
        {
            GUI.changed = false;
            NowIMGUIInputProvider.repaintRequested = () => ++repaintCount;
            NowIMGUIInputProvider.ConsumeScrollEvent(scrollEvent);

            Assert.AreEqual(
                EventType.Used,
                scrollEvent.type,
                "A NowUI scroll view must own the wheel event it consumed so an enclosing IMGUI control cannot also handle it.");
            Assert.IsTrue(
                GUI.changed,
                "Consuming wheel input must mark IMGUI changed so the editor schedules a repaint for the updated scroll offset.");
            Assert.AreEqual(1, repaintCount, "The editor bridge must explicitly repaint the window that consumed the wheel event.");
        }
        finally
        {
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
            GUI.changed = previousChanged;
        }
    }

    [Test]
    public void IMGUIBlockOnlyOverlayConsumesTheNativeScrollEvent()
    {
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        var scrollEvent = new Event
        {
            type = EventType.ScrollWheel,
            mousePosition = new Vector2(24f, 22f),
            delta = new Vector2(0f, 3f)
        };
        Event previousEvent = Event.current;
        bool previousChanged = GUI.changed;

        try
        {
            Event.current = null;
            GUI.changed = false;

            using (NowInput.Begin(provider, surface))
            {
                InstallIMGUISnapshot(
                    provider,
                    surface,
                    scrollEvent,
                    EventType.ScrollWheel,
                    ownsCapture: false);
                NowOverlay.BlockScreen(new NowRect(10f, 10f, 40f, 30f));
                NowOverlay.Flush();
            }

            Assert.AreEqual(
                EventType.Used,
                scrollEvent.type,
                "A manually drawn overlay block must contain wheel input before native IMGUI can scroll its parent.");
            Assert.IsTrue(
                GUI.changed,
                "Consuming the block-owned native wheel event must schedule the editor repaint.");
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
            Event.current = previousEvent;
            GUI.changed = previousChanged;
        }
    }

    [Test]
    public void IMGUIFocusClearMarksGUIChangedAndRequestsRepaint()
    {
        bool previousChanged = GUI.changed;
        Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        int repaintCount = 0;

        try
        {
            GUI.changed = false;
            NowIMGUIInputProvider.repaintRequested = () => ++repaintCount;
            NowIMGUIInputProvider.instance.NotifyFocusCleared();

            Assert.IsTrue(
                GUI.changed,
                "Background defocus must mark IMGUI changed so stale focus visuals are not retained.");
            Assert.AreEqual(
                1,
                repaintCount,
                "The editor bridge must repaint after background defocus.");
        }
        finally
        {
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
            GUI.changed = previousChanged;
        }
    }

    [Test]
    public void IMGUIClaimedTextEventIsConsumedAndRequestsRepaint()
    {
        bool previousChanged = GUI.changed;
        Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        int repaintCount = 0;
        var keyEvent = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Return
        };

        try
        {
            GUI.changed = false;
            NowIMGUIInputProvider.repaintRequested = () => ++repaintCount;
            NowIMGUIInputProvider.ConsumeClaimedTextEvent(keyEvent);

            Assert.AreEqual(EventType.Used, keyEvent.type,
                "A focused text consumer must own its native IMGUI key event.");
            Assert.IsTrue(GUI.changed);
            Assert.AreEqual(1, repaintCount);
        }
        finally
        {
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
            GUI.changed = previousChanged;
        }
    }

    [Test]
    public void InteractionClicksWhenPressedAndReleasedInsideRect()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var press = NowInput.Interact(1, _rect);

            Assert.IsTrue(press.pressed);
            Assert.IsTrue(press.held);
            Assert.IsTrue(press.active);
            Assert.IsFalse(press.clicked);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var release = NowInput.Interact(1, _rect);

            Assert.IsTrue(release.released);
            Assert.IsTrue(release.clicked);
            Assert.IsFalse(release.dragEnded);
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void InteractionDoesNotClickAfterDrag()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        _provider.snapshot = new NowInputSnapshot(new Vector2(28, 20), new Vector2(10, 0), true, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var drag = NowInput.Interact(1, _rect);

            Assert.IsTrue(drag.dragStarted);
            Assert.IsTrue(drag.dragging);
            Assert.AreEqual(new Vector2(10, 0), drag.dragDelta);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(30, 20), new Vector2(2, 0), false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var release = NowInput.Interact(1, _rect);

            Assert.IsTrue(release.dragEnded);
            Assert.IsFalse(release.clicked);
        }
    }

    [Test]
    public void ActiveCaptureClearsWhenMissingControlReleases()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        Assert.AreEqual(1, NowInput.activeId);

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var other = NowInput.Interact(2, new Rect(60, 10, 30, 30));
            Assert.IsFalse(other.pressed);
        }

        Assert.AreEqual(1, NowInput.activeId);

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void ActiveCaptureClearsWhenRemovedBeforeRelease()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void PassiveInteractionDoesNotKeepStaleActiveCaptureAlive()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            NowInput.BeginPassive();
            try
            {
                NowInput.Interact(1, _rect);
            }
            finally
            {
                NowInput.EndPassive();
            }
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void ActiveCaptureSurvivesWhenControlIsDrawnWhileHeld()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        _provider.snapshot = new NowInputSnapshot(new Vector2(20, 20), new Vector2(2, 0), true, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var held = NowInput.Interact(1, _rect);
            Assert.IsTrue(held.active);
            Assert.IsTrue(held.held);
        }

        Assert.AreEqual(1, NowInput.activeId);
    }

    [Test]
    public void ActiveCaptureSurvivesDifferentProviderEarlierInNextFrame()
    {
        var otherProvider = new MockInputProvider();
        _provider.snapshot = SnapshotAt(20, NowPointerButtons.Primary, NowPointerButtons.Primary, NowPointerButtons.None);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        otherProvider.snapshot = SnapshotAt(21, NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.Primary);

        using (NowInput.Begin(otherProvider, new Vector2(100, 100)))
            Assert.AreEqual(1, NowInput.activeId);

        _provider.snapshot = SnapshotAt(21, NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.Primary);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var release = NowInput.Interact(1, _rect);
            Assert.IsTrue(release.clicked);
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void ActiveCaptureClearsAfterOwningProviderMissesFullFrame()
    {
        var nextSceneProvider = new MockInputProvider();
        _provider.snapshot = SnapshotAt(30, NowPointerButtons.Primary, NowPointerButtons.Primary, NowPointerButtons.None);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        nextSceneProvider.snapshot = SnapshotAt(31, NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None);

        using (NowInput.Begin(nextSceneProvider, new Vector2(100, 100)))
            Assert.AreEqual(1, NowInput.activeId);

        nextSceneProvider.snapshot = SnapshotAt(32, NowPointerButtons.Primary, NowPointerButtons.Primary, NowPointerButtons.None);

        using (NowInput.Begin(nextSceneProvider, new Vector2(100, 100)))
        {
            var press = NowInput.Interact(2, _rect);
            Assert.IsTrue(press.pressed);
        }

        Assert.AreEqual(2, NowInput.activeId);
    }

    [Test]
    public void EndFrameClearsStaleActiveCaptureForDirectUpdateFlow()
    {
        var surface = new NowInputSurface(new Vector2(100, 100));
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);
        NowInput.Update(_provider, surface);
        NowInput.Interact(1, _rect);

        Assert.AreEqual(1, NowInput.activeId);

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, true);
        NowInput.Update(_provider, surface);
        NowInput.EndFrame();

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void StartUIScopeClearsStaleActiveCaptureWhenDisposed()
    {
        NowInput.defaultProvider = _provider;
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (Now.StartUI(new NowRect(0, 0, 100, 100)))
            NowInput.Interact(1, _rect);

        Assert.AreEqual(1, NowInput.activeId);

        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, true);

        using (Now.StartUI(new NowRect(0, 0, 100, 100)))
        {
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void TopLevelInputScopeFlushesOverlayBeforeRestoringPreviousContext()
    {
        var outer = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(4f, 4f), false, false, false)
        };
        var inner = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(42f, 24f), false, false, false)
        };
        Vector2 overlayPointer = default;
        bool ran = false;

        NowInput.defaultProvider = outer;

        using (Now.StartUI(new NowRect(0, 0, 100, 100)))
        using (NowInput.Begin(inner, new Vector2(100, 100)))
        {
            NowOverlay.DeferScreen(new NowRect(0, 0, 10, 10), () =>
            {
                ran = true;
                overlayPointer = NowInput.current.pointerPosition;
            });
        }

        Assert.IsTrue(ran);
        Assert.AreEqual(new Vector2(42f, 24f), overlayPointer);
    }

    [Test]
    public void TransformedInteractionReportsLocalPointerCoordinates()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(46, 45), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        {
            var press = NowInput.Interact(1, _rect);

            Assert.IsTrue(press.pressed);
            Assert.AreEqual(new Vector2(18f, 20f), press.pointerPosition);
            Assert.AreEqual(_rect, press.rect);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(56, 45), new Vector2(10, 0), true, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        {
            var drag = NowInput.Interact(1, _rect);

            Assert.IsTrue(drag.dragStarted);
            Assert.IsTrue(drag.dragging);
            Assert.AreEqual(new Vector2(23f, 20f), drag.pointerPosition);
            Assert.AreEqual(new Vector2(5f, 0f), drag.pointerDelta);
            Assert.AreEqual(new Vector2(5f, 0f), drag.dragDelta);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(56, 45), Vector2.zero, false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
            NowInput.Interact(1, _rect);
    }

    [Test]
    public void InteractionCanUseSecondaryPointerButton()
    {
        _provider.snapshot = new NowInputSnapshot(
            new Vector2(18, 20),
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.None);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var primary = NowInput.Interact(1, _rect);
            var secondary = NowInput.Interact(2, _rect, NowPointerButton.Secondary);

            Assert.IsFalse(primary.pressed);
            Assert.IsTrue(secondary.pressed);
            Assert.IsTrue(secondary.held);
            Assert.AreEqual(NowPointerButton.Secondary, secondary.button);
        }

        _provider.snapshot = new NowInputSnapshot(
            new Vector2(18, 20),
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.Secondary);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var secondary = NowInput.Interact(2, _rect, NowPointerButton.Secondary);

            Assert.IsTrue(secondary.released);
            Assert.IsTrue(secondary.clicked);
        }
    }

    [Test]
    public void IdlessRectInteractionClicksAcrossFramesFromOneSite()
    {
        Vector2 inside = new Vector2(18, 20);
        bool clicked = false;

        void Frame(bool down, bool pressed, bool released)
        {
            _provider.snapshot = new NowInputSnapshot(inside, down, pressed, released);

            using (NowInput.Begin(_provider, new Vector2(100, 100)))
                clicked = NowInput.Interact(_rect).clicked;
        }

        Frame(down: true, pressed: true, released: false);
        Assert.IsFalse(clicked);

        Frame(down: false, pressed: false, released: true);
        Assert.IsTrue(clicked);
    }

    [Test]
    public void IdlessInteractionCanUseSecondaryPointerButton()
    {
        var rect = new NowRect(10, 10, 40, 30);
        var inside = new Vector2(18, 20);
        NowInteraction interaction = default;

        void Frame(NowPointerButtons down, NowPointerButtons pressed, NowPointerButtons released)
        {
            _provider.snapshot = new NowInputSnapshot(inside, down, pressed, released);

            using (NowInput.Begin(_provider, new Vector2(100, 100)))
                interaction = NowInput.Interact(rect, NowPointerButton.Secondary);
        }

        Frame(NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None);
        Assert.IsTrue(interaction.pressed);
        Assert.IsTrue(interaction.held);
        Assert.AreEqual(NowPointerButton.Secondary, interaction.button);

        Frame(NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.Secondary);
        Assert.IsTrue(interaction.released);
        Assert.IsTrue(interaction.clicked);
    }

    [Test]
    public void InteractionDerivesSubIdsAndStateSlots()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), false, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var interaction = NowInput.Interact(17, _rect);

            Assert.AreEqual(NowInput.GetId(interaction.id, "hover"), interaction.GetId("hover"));
            Assert.AreEqual(NowInput.CombineId(interaction.id, 3), interaction.GetId(3));

            ref int stringSlot = ref interaction.State<int>("hover");
            ref int numericSlot = ref interaction.State<int>(3);

            stringSlot = 23;
            numericSlot = 42;

            Assert.AreEqual(23, NowControlState.Get<int>(interaction.GetId("hover")));
            Assert.AreEqual(42, NowControlState.Get<int>(interaction.GetId(3)));

            Assert.GreaterOrEqual(NowControlState.Transition(interaction, "fade", true), 0f);
            Assert.IsTrue(NowControlState.Repeat(interaction, "nav", held: true));
            Assert.IsFalse(NowControlState.Repeat(interaction, "nav", held: true));
            Assert.IsTrue(NowControlState.PressAnimation(
                interaction, "press", true, new Vector2(12f, 14f), 1f).active);
        }
    }

    [Test]
    public void SnapshotCanCarryNavigationWithoutPointer()
    {
        _provider.snapshot = new NowInputSnapshot(
            false,
            default,
            default,
            default,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            default,
            Vector2.right,
            true,
            true,
            false,
            false,
            false,
            false,
            1,
            0.5f);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var interaction = NowInput.Interact(1, _rect);

            Assert.IsFalse(interaction.hovered);
            Assert.IsFalse(interaction.pressed);
            Assert.AreEqual(Vector2.right, NowInput.current.navigation);
            Assert.IsTrue(NowInput.current.submitDown);
            Assert.IsTrue(NowInput.current.submitPressed);
        }
    }

    [Test]
    public void RectTransformProviderReturnsNavigationOnlySnapshotAsAvailable()
    {
        var gameObject = new GameObject("NowUI RectTransform input test", typeof(RectTransform));

        try
        {
            var provider = new NowRectTransformInputProvider(gameObject.GetComponent<RectTransform>());
            var raw = new NowMouseInput
            {
                hasPointer = false,
                pointerButtonsDown = NowPointerButtons.Primary,
                pointerButtonsPressed = NowPointerButtons.Primary,
                pointerButtonsReleased = NowPointerButtons.Primary,
                scrollDelta = new Vector2(2f, 3f),
                navigation = new Vector2(-1f, 1f),
                focusPreviousPressed = true,
                focusNextPressed = true,
                submitDown = true,
                submitPressed = true,
                submitReleased = true,
                cancelDown = true,
                cancelPressed = true,
                cancelReleased = true
            };

            bool available = provider.TryGetSnapshot(
                new NowInputSurface(new Vector2(100f, 100f)), raw, out var snapshot);

            Assert.IsTrue(available);
            Assert.IsFalse(snapshot.hasPointer);
            Assert.AreEqual(raw.navigation, snapshot.navigation);
            Assert.IsTrue(snapshot.focusPreviousPressed);
            Assert.IsTrue(snapshot.focusNextPressed);
            Assert.IsTrue(snapshot.submitDown);
            Assert.IsTrue(snapshot.submitPressed);
            Assert.IsTrue(snapshot.submitReleased);
            Assert.IsTrue(snapshot.cancelDown);
            Assert.IsTrue(snapshot.cancelPressed);
            Assert.IsTrue(snapshot.cancelReleased);
            Assert.AreEqual(NowPointerButtons.None, snapshot.pointerButtonsDown);
            Assert.AreEqual(NowPointerButtons.None, snapshot.pointerButtonsPressed);
            Assert.AreEqual(NowPointerButtons.None, snapshot.pointerButtonsReleased);
            Assert.AreEqual(Vector2.zero, snapshot.scrollDelta);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void InputScopeRestoresPreviousContext()
    {
        var first = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(12, 12), false, false, false)
        };
        var second = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(80, 80), false, false, false)
        };

        using (NowInput.Begin(first, new Vector2(100, 100)))
        {
            Assert.AreSame(first, NowInput.currentProvider);
            Assert.AreEqual(new Vector2(12, 12), NowInput.current.pointerPosition);

            using (NowInput.Begin(second, new Vector2(100, 100)))
            {
                Assert.AreSame(second, NowInput.currentProvider);
                Assert.AreEqual(new Vector2(80, 80), NowInput.current.pointerPosition);
            }

            Assert.AreSame(first, NowInput.currentProvider);
            Assert.AreEqual(new Vector2(12, 12), NowInput.current.pointerPosition);
        }

        Assert.IsFalse(NowInput.hasContext);
        Assert.IsNull(NowInput.currentProvider);
    }

    [Test]
    public void ThrowingProviderRollsBackNestedInputScope()
    {
        var outer = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(12, 12), false, false, false)
        };

        NowOverlay.Reset();

        try
        {
            using (NowInput.Begin(outer, new Vector2(100, 100)))
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    _ = NowInput.Begin(new ThrowingInputProvider(), new Vector2(50, 50));
                });

                Assert.IsTrue(NowInput.hasContext);
                Assert.AreSame(outer, NowInput.currentProvider);
                Assert.AreEqual(new Vector2(12, 12), NowInput.current.pointerPosition);
            }

            Assert.IsFalse(NowInput.hasContext);
            Assert.IsNull(NowInput.currentProvider);

            bool flushed = false;

            using (NowInput.Begin(outer, new Vector2(100, 100)))
                NowOverlay.DeferScreen(new NowRect(0, 0, 1, 1), () => flushed = true);

            Assert.IsTrue(flushed, "The failed nested Begin must not leave the next scope nested permanently.");
        }
        finally
        {
            NowOverlay.Reset();
        }
    }

    [Test]
    public void ThrowingProviderRollsBackMeasurementInputScope()
    {
        var outer = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(12, 12), false, false, false)
        };

        using (NowInput.Begin(outer, new Vector2(100, 100)))
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = NowInput.BeginMeasurement(new ThrowingInputProvider(), new NowInputSurface(new Vector2(50, 50)));
            });

            Assert.IsTrue(NowInput.hasContext);
            Assert.AreSame(outer, NowInput.currentProvider);
            Assert.AreEqual(new Vector2(12, 12), NowInput.current.pointerPosition);
        }
    }

    [Test]
    public void ThrowingProvidersDoNotAccumulateOverlayRegistrationOwners()
    {
        NowOverlay.Reset();

        for (int i = 0; i < 100; ++i)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = NowInput.Begin(
                    new ThrowingInputProvider(),
                    new Vector2(50f, 50f));
            });
        }

        Assert.AreEqual(
            0,
            NowOverlay.registrationOwnerCount,
            "Provider sampling fails before an overlay transaction exists.");
    }

    [Test]
    public void ThrowingOverlayStillClosesTopLevelInputScope()
    {
        NowOverlay.Reset();

        try
        {
            var scope = NowInput.Begin(_provider, new Vector2(100, 100));
            NowOverlay.DeferScreen(
                new NowRect(0, 0, 1, 1),
                () => throw new InvalidOperationException("overlay failed"));

            Assert.Throws<InvalidOperationException>(() =>
            {
                scope.Dispose();
            });
            Assert.IsFalse(NowInput.hasContext);
            Assert.IsNull(NowInput.currentProvider);

            bool flushed = false;

            using (NowInput.Begin(_provider, new Vector2(100, 100)))
                NowOverlay.DeferScreen(new NowRect(0, 0, 1, 1), () => flushed = true);

            Assert.IsTrue(flushed, "An exception during Flush must not leave the next scope nested permanently.");
        }
        finally
        {
            NowOverlay.Reset();
        }
    }

    [Test]
    public void DisposingCopiedInputScopeCannotCloseANewerScope()
    {
        var first = NowInput.Begin(_provider, new Vector2(100, 100));
        var staleCopy = first;
        first.Dispose();

        var current = NowInput.Begin(_provider, new Vector2(50, 50));

        try
        {
            staleCopy.Dispose();
            Assert.IsTrue(NowInput.hasContext);
            Assert.AreEqual(new Vector2(50, 50), NowInput.surface.size);
        }
        finally
        {
            current.Dispose();
        }

        Assert.IsFalse(NowInput.hasContext);
    }

    [Test]
    public void OutOfOrderInputDisposeThrowsWithoutCorruptingContext()
    {
        var outerProvider = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(12, 12), false, false, false)
        };
        var innerProvider = new MockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(30, 30), false, false, false)
        };
        var outer = NowInput.Begin(outerProvider, new Vector2(100, 100));
        var inner = NowInput.Begin(innerProvider, new Vector2(50, 50));

        try
        {
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            Assert.AreSame(innerProvider, NowInput.currentProvider);
            Assert.AreEqual(new Vector2(30, 30), NowInput.current.pointerPosition);
        }
        finally
        {
            inner.Dispose();
            outer.Dispose();
        }

        Assert.IsFalse(NowInput.hasContext);
    }

    [Test]
    public void ScrollConsumptionCarriesIntoAndOutOfNestedScopes()
    {
        _provider.snapshot = ScrollSnapshot(new Vector2(18, 20), new Vector2(0f, -1f));

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            Assert.AreEqual(new Vector2(0f, -1f), NowInput.ConsumeScrollDelta(_rect));

            using (NowInput.Begin(_provider, new Vector2(100, 100)))
                Assert.AreEqual(Vector2.zero, NowInput.ConsumeScrollDelta(_rect));

            Assert.AreEqual(Vector2.zero, NowInput.ConsumeScrollDelta(_rect));
        }
    }

    [Test]
    public void ScrollConsumedInNestedScopeStaysConsumedInOuterScope()
    {
        _provider.snapshot = ScrollSnapshot(new Vector2(18, 20), new Vector2(0f, -1f));

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            using (NowInput.Begin(_provider, new Vector2(100, 100)))
                Assert.AreEqual(new Vector2(0f, -1f), NowInput.ConsumeScrollDelta(_rect));

            Assert.AreEqual(Vector2.zero, NowInput.ConsumeScrollDelta(_rect));
        }

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            Assert.AreEqual(new Vector2(0f, -1f), NowInput.ConsumeScrollDelta(_rect));
    }

    [Test]
    public void DragThresholdExplicitOverrideWinsAndResetRestoresScaledDefault()
    {
        NowInput.dragThreshold = 12f;
        Assert.AreEqual(12f, NowInput.dragThreshold);

        NowInput.Reset();

        float dpi = Screen.dpi;
        float expected = dpi > 0f ? 4f * Mathf.Max(1f, dpi / 160f) : 4f;
        Assert.AreEqual(expected, NowInput.dragThreshold, 0.0001f);
        Assert.GreaterOrEqual(NowInput.dragThreshold, 4f);
    }

    [Test]
    public void ExplicitDragThresholdSuppressesSmallDragsAndKeepsClicks()
    {
        NowInput.dragThreshold = 50f;
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        _provider.snapshot = new NowInputSnapshot(new Vector2(28, 20), new Vector2(10, 0), true, false, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var held = NowInput.Interact(1, _rect);

            Assert.IsFalse(held.dragging);
            Assert.IsFalse(held.dragStarted);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(28, 20), Vector2.zero, false, false, true);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var release = NowInput.Interact(1, _rect);

            Assert.IsTrue(release.clicked);
            Assert.IsFalse(release.dragEnded);
        }
    }

    [Test]
    public void NavigationKeysDefaultToAllAndResetRestoresThem()
    {
        Assert.AreEqual(NowNavigationKeys.All, NowInput.navigationKeys);

        NowInput.navigationKeys = NowNavigationKeys.Arrows | NowNavigationKeys.TabFocus;
        Assert.AreEqual(NowNavigationKeys.Arrows | NowNavigationKeys.TabFocus, NowInput.navigationKeys);

        NowInput.Reset();
        Assert.AreEqual(NowNavigationKeys.All, NowInput.navigationKeys);
    }

    [Test]
    public void ActiveCaptureCancelsWhenButtonStateDropsWithoutRelease()
    {
        _provider.snapshot = new NowInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            NowInput.Interact(1, _rect);

        _provider.snapshot = new NowInputSnapshot(
            new Vector2(28, 20),
            new Vector2(10, 0),
            true,
            false,
            false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
            Assert.IsTrue(NowInput.Interact(1, _rect).dragging);

        _provider.snapshot = new NowInputSnapshot(
            new Vector2(28, 20),
            Vector2.zero,
            false,
            false,
            false);

        using (NowInput.Begin(_provider, new Vector2(100, 100)))
        {
            var cancelled = NowInput.Interact(1, _rect);

            Assert.IsTrue(cancelled.cancelled);
            Assert.IsTrue(cancelled.dragCancelled);
            Assert.IsFalse(cancelled.released);
            Assert.IsFalse(cancelled.dragEnded);
            Assert.IsFalse(cancelled.clicked);
        }

        Assert.AreEqual(0, NowInput.activeId);
    }

    [Test]
    public void CollidingControlIdFromAnotherProviderCannotDriveOrCancelActiveCapture()
    {
        var other = new MockInputProvider();
        var pressed = new NowInputSnapshot(new Vector2(18f, 20f), true, true, false)
        {
            frame = 50,
            inputPass = 1
        };
        _provider.snapshot = pressed;

        using (NowInput.Begin(_provider, new Vector2(100f, 100f)))
            Assert.IsTrue(NowInput.Interact(1, _rect).pressed);

        other.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), false, false, false)
        {
            frame = 50,
            inputPass = 2
        };

        using (NowInput.Begin(other, new Vector2(100f, 100f)))
        {
            var collision = NowInput.Interact(1, _rect);

            Assert.IsFalse(collision.active);
            Assert.IsFalse(collision.cancelled);
            Assert.IsFalse(collision.clicked);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), true, false, false)
        {
            frame = 50,
            inputPass = 3
        };

        using (NowInput.Begin(_provider, new Vector2(100f, 100f)))
        {
            var owner = NowInput.Interact(1, _rect);

            Assert.IsTrue(owner.active);
            Assert.IsTrue(owner.held);
        }
    }

    [Test]
    public void FreshSameFramePressTransfersFromAnOmittedProvider()
    {
        var other = new MockInputProvider();
        _provider.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), true, true, false)
        {
            frame = 60,
            inputPass = 1
        };

        using (NowInput.Begin(_provider, new Vector2(100f, 100f)))
            Assert.IsTrue(NowInput.Interact(1, _rect).pressed);

        other.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), true, true, false)
        {
            frame = 60,
            inputPass = 2
        };

        using (NowInput.Begin(other, new Vector2(100f, 100f)))
        {
            var transferred = NowInput.Interact(2, _rect);

            Assert.IsTrue(
                transferred.pressed,
                "A new native press must not wait for Time.frameCount to clear a capture whose panel disappeared.");
            Assert.IsTrue(transferred.active);
            Assert.AreEqual(2, NowInput.activeId);
        }
    }

    [Test]
    public void CancelClaimsAreInputPassAndProviderScoped()
    {
        var other = new MockInputProvider();
        _provider.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), false, false, false)
        {
            frame = 80,
            inputPass = 10
        };

        using (NowInput.Begin(_provider, new Vector2(100f, 100f)))
        {
            NowInput.ConsumeCancel();
            Assert.IsTrue(NowInput.cancelConsumed);
        }

        other.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), false, false, false)
        {
            frame = 80,
            inputPass = 11
        };

        using (NowInput.Begin(other, new Vector2(100f, 100f)))
        {
            Assert.IsFalse(
                NowInput.cancelConsumed,
                "A claim in one panel must not suppress Escape handling in another panel.");
            Assert.IsFalse(NowInput.cancelConsumedForFrameSwap);
        }

        _provider.snapshot = new NowInputSnapshot(new Vector2(18f, 20f), false, false, false)
        {
            frame = 81,
            inputPass = 99
        };

        using (NowInput.Begin(_provider, new Vector2(100f, 100f)))
        {
            Assert.IsFalse(NowInput.cancelConsumed);
            Assert.IsTrue(
                NowInput.cancelConsumedForFrameSwap,
                "Frame-swap consumers keep the owning provider's previous-frame claim even after many IMGUI passes.");
        }
    }

    [Test]
    public void IMGUIButtonStateSurvivesSameFrameLayoutAndRepaintUntilMouseUp()
    {
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));

        try
        {
            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = new Vector2(20f, 20f)
                },
                EventType.MouseDown,
                false,
                out var pressed));
            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                new Event
                {
                    type = EventType.Layout,
                    mousePosition = new Vector2(20f, 20f)
                },
                EventType.Layout,
                false,
                out var layout));
            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                new Event
                {
                    type = EventType.Repaint,
                    mousePosition = new Vector2(20f, 20f)
                },
                EventType.Repaint,
                false,
                out var repaint));
            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                new Event
                {
                    type = EventType.MouseDrag,
                    button = 0,
                    mousePosition = new Vector2(32f, 20f),
                    delta = new Vector2(12f, 0f)
                },
                EventType.MouseDrag,
                false,
                out var dragged));
            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                new Event
                {
                    type = EventType.MouseUp,
                    button = 0,
                    mousePosition = new Vector2(32f, 20f)
                },
                EventType.MouseUp,
                false,
                out var released));

            Assert.IsTrue(pressed.primaryDown);
            Assert.IsTrue(layout.primaryDown);
            Assert.IsTrue(repaint.primaryDown);
            Assert.IsTrue(dragged.primaryDown);
            Assert.IsFalse(released.primaryDown);

            int pressedEdges =
                (pressed.primaryPressed ? 1 : 0) +
                (layout.primaryPressed ? 1 : 0) +
                (repaint.primaryPressed ? 1 : 0) +
                (dragged.primaryPressed ? 1 : 0) +
                (released.primaryPressed ? 1 : 0);
            int releasedEdges =
                (pressed.primaryReleased ? 1 : 0) +
                (layout.primaryReleased ? 1 : 0) +
                (repaint.primaryReleased ? 1 : 0) +
                (dragged.primaryReleased ? 1 : 0) +
                (released.primaryReleased ? 1 : 0);

            Assert.AreEqual(1, pressedEdges, "The native MouseDown edge must not replay on Layout or Repaint.");
            Assert.AreEqual(1, releasedEdges, "Only the native MouseUp pass may report the release edge.");
            Assert.AreEqual(pressed.frame, layout.frame);
            Assert.AreEqual(pressed.frame, repaint.frame);
            Assert.AreEqual(pressed.frame, dragged.frame);
            Assert.AreEqual(pressed.frame, released.frame);
            Assert.Greater(layout.inputPass, pressed.inputPass);
            Assert.Greater(repaint.inputPass, layout.inputPass);
            Assert.Greater(dragged.inputPass, repaint.inputPass);
            Assert.Greater(released.inputPass, dragged.inputPass);
        }
        finally
        {
            provider.ResetState();
        }
    }

    [Test]
    public void IMGUIFocusLossClearsLatchedKeyboardState()
    {
        var provider = new NowIMGUIInputProvider(9101, new object());
        var surface = new NowInputSurface(new Vector2(100f, 100f));

        try
        {
            Assert.IsFalse(provider.NotifyHostFocusChanged(true, releaseNativeCapture: false));

            var keyDown = new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.Return,
                mousePosition = new Vector2(20f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                keyDown,
                EventType.KeyDown,
                ownsCapture: false,
                out var pressed));
            Assert.IsTrue(pressed.submitDown);

            var leftDown = new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.LeftArrow,
                mousePosition = new Vector2(20f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                leftDown,
                EventType.KeyDown,
                ownsCapture: false,
                out var navigated));
            Assert.IsTrue(navigated.submitDown);
            Assert.AreEqual(Vector2.left, navigated.navigation);

            Assert.IsTrue(provider.NotifyHostFocusChanged(false, releaseNativeCapture: false));
            Assert.IsFalse(provider.NotifyHostFocusChanged(true, releaseNativeCapture: false));

            var layout = new Event
            {
                type = EventType.Layout,
                mousePosition = new Vector2(20f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                layout,
                EventType.Layout,
                ownsCapture: false,
                out var afterFocusReturn));
            Assert.IsFalse(
                afterFocusReturn.submitDown,
                "A missed native KeyUp while the window is unfocused must not leave submit latched.");
            Assert.AreEqual(Vector2.zero, afterFocusReturn.navigation);
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
        }
    }

    [Test]
    public void IMGUIFreshPressAfterFocusLossIsNotEatenByPendingCancellation()
    {
        const int HostControlId = 9111;
        const int FirstControlId = 9112;
        const int SecondControlId = 9113;
        var provider = new NowIMGUIInputProvider(HostControlId, new object());
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        Event previousEvent = Event.current;
        int previousHotControl = GUIUtility.hotControl;
        bool previousChanged = GUI.changed;

        try
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
            GUI.changed = false;
            Assert.IsFalse(provider.NotifyHostFocusChanged(true, releaseNativeCapture: false));

            using (NowInput.Begin(provider, surface))
            {
                InstallIMGUISnapshot(
                    provider,
                    surface,
                    new Event
                    {
                        type = EventType.MouseDown,
                        button = 0,
                        mousePosition = new Vector2(20f, 20f)
                    },
                    EventType.MouseDown,
                    ownsCapture: false);
                Assert.IsTrue(NowInput.Interact(FirstControlId, _rect).pressed);
            }

            Assert.AreEqual(FirstControlId, NowInput.activeId);
            Assert.AreEqual(HostControlId, GUIUtility.hotControl);
            Assert.IsTrue(provider.NotifyHostFocusChanged(false, releaseNativeCapture: false));
            Assert.IsFalse(provider.NotifyHostFocusChanged(true, releaseNativeCapture: false));

            using (NowInput.Begin(provider, surface))
            {
                var freshSnapshot = InstallIMGUISnapshot(
                    provider,
                    surface,
                    new Event
                    {
                        type = EventType.MouseDown,
                        button = 0,
                        mousePosition = new Vector2(20f, 20f)
                    },
                    EventType.MouseDown,
                    ownsCapture: true);
                Assert.IsTrue(freshSnapshot.pointerCaptureCancelled);
                Assert.IsTrue(
                    freshSnapshot.hasPointer,
                    "A real MouseDown must remain actionable while it also reports stale capture cancellation.");

                var fresh = NowInput.Interact(SecondControlId, _rect);
                Assert.IsTrue(fresh.pressed, "The first click after focus returns must not be discarded.");
                Assert.IsTrue(fresh.active);
                Assert.IsFalse(fresh.cancelled);
            }

            Assert.AreEqual(SecondControlId, NowInput.activeId);
            Assert.AreEqual(HostControlId, GUIUtility.hotControl);
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
            GUIUtility.hotControl = previousHotControl;
            GUI.changed = previousChanged;
            Event.current = previousEvent;
        }
    }

    [Test]
    public void IMGUIInteractRejectsCaptureWhenNativeHotControlChangesAfterSampling()
    {
        const int HostControlId = 9121;
        const int ForeignControlId = 9122;
        const int NowControlId = 9123;
        var provider = new NowIMGUIInputProvider(HostControlId, new object());
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        var down = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(20f, 20f)
        };
        Event previousEvent = Event.current;
        int previousHotControl = GUIUtility.hotControl;
        bool previousChanged = GUI.changed;

        try
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
            GUI.changed = false;

            using (NowInput.Begin(provider, surface))
            {
                InstallIMGUISnapshot(
                    provider,
                    surface,
                    down,
                    EventType.MouseDown,
                    ownsCapture: false);
                Assert.IsTrue(
                    NowInput.current.primaryPressed,
                    "The provider must sample the native press before the competing control acquires capture.");

                GUIUtility.hotControl = ForeignControlId;
                var interaction = NowInput.Interact(NowControlId, _rect);

                Assert.IsFalse(
                    interaction.pressed,
                    "A NowUI control must not activate after another native control wins hotControl.");
                Assert.IsFalse(interaction.active);
                Assert.AreEqual(0, NowInput.activeId);
            }

            Assert.AreEqual(
                ForeignControlId,
                GUIUtility.hotControl,
                "Rejecting the press must not steal capture from the native control that won the race.");
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
            GUIUtility.hotControl = previousHotControl;
            GUI.changed = previousChanged;
            Event.current = previousEvent;
        }
    }

    [Test]
    public void IMGUIInteractCaptureIsCancelledByIgnoreAndCannotResumeOnReentry()
    {
        const int HostControlId = 9141;
        const int NowControlId = 9142;
        var provider = new NowIMGUIInputProvider(HostControlId, new object());
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        Event previousEvent = Event.current;
        int previousHotControl = GUIUtility.hotControl;
        bool previousChanged = GUI.changed;

        NowInteraction Draw(Event inputEvent, EventType routedType, bool ownsCapture)
        {
            Event.current = null;

            using (NowInput.Begin(provider, surface))
            {
                InstallIMGUISnapshot(
                    provider,
                    surface,
                    inputEvent,
                    routedType,
                    ownsCapture);
                return NowInput.Interact(NowControlId, _rect);
            }
        }

        try
        {
            GUIUtility.hotControl = 0;
            GUI.changed = false;
            var down = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(18f, 20f)
            };

            var pressed = Draw(down, EventType.MouseDown, ownsCapture: false);

            Assert.IsTrue(pressed.pressed);
            Assert.IsTrue(pressed.active);
            Assert.AreEqual(NowControlId, NowInput.activeId);
            Assert.AreEqual(HostControlId, GUIUtility.hotControl);
            Assert.AreEqual(
                EventType.Used,
                down.type,
                "Acquiring the NowUI interaction must consume the native MouseDown.");

            var drag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(40f, 20f),
                delta = new Vector2(22f, 0f)
            };
            var dragged = Draw(drag, EventType.MouseDrag, ownsCapture: true);

            Assert.IsTrue(dragged.dragging);
            Assert.IsTrue(dragged.active);
            Assert.AreEqual(NowControlId, NowInput.activeId);
            Assert.AreEqual(EventType.Used, drag.type);

            var ignored = new Event
            {
                type = EventType.Ignore,
                button = 0,
                mousePosition = new Vector2(40f, 20f)
            };
            var cancelled = Draw(ignored, EventType.Ignore, ownsCapture: true);

            Assert.IsTrue(cancelled.cancelled);
            Assert.IsTrue(cancelled.dragCancelled);
            Assert.IsFalse(cancelled.clicked);
            Assert.AreEqual(0, NowInput.activeId);
            Assert.AreEqual(
                0,
                GUIUtility.hotControl,
                "Capture loss must release the panel's native hotControl immediately.");

            var reentryDrag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(24f, 20f),
                delta = new Vector2(-16f, 0f)
            };
            var afterReentry = Draw(reentryDrag, EventType.MouseDrag, ownsCapture: false);

            Assert.IsFalse(afterReentry.active);
            Assert.IsFalse(afterReentry.dragging);
            Assert.IsFalse(afterReentry.clicked);
            Assert.AreEqual(0, NowInput.activeId);
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
            GUIUtility.hotControl = previousHotControl;
            GUI.changed = previousChanged;
            Event.current = previousEvent;
        }
    }

    [Test]
    public void IMGUIReleaseOutsidePanelEndsCapturedDrag()
    {
        const int HostControlId = 9151;
        const int NowControlId = 9152;
        var provider = new NowIMGUIInputProvider(HostControlId, new object());
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        Event previousEvent = Event.current;
        int previousHotControl = GUIUtility.hotControl;
        bool previousChanged = GUI.changed;

        NowInteraction Draw(Event inputEvent, EventType routedType, bool ownsCapture)
        {
            Event.current = null;

            using (NowInput.Begin(provider, surface))
            {
                InstallIMGUISnapshot(
                    provider,
                    surface,
                    inputEvent,
                    routedType,
                    ownsCapture);
                return NowInput.Interact(NowControlId, _rect);
            }
        }

        try
        {
            GUIUtility.hotControl = 0;
            GUI.changed = false;
            var down = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(18f, 20f)
            };

            NowInteraction pressed = Draw(
                down,
                EventType.MouseDown,
                ownsCapture: false);

            Assert.IsTrue(pressed.pressed);
            Assert.AreEqual(HostControlId, GUIUtility.hotControl);
            Assert.AreEqual(EventType.Used, down.type);

            var drag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(140f, 20f),
                delta = new Vector2(122f, 0f)
            };

            NowInteraction dragged = Draw(
                drag,
                EventType.MouseDrag,
                ownsCapture: true);

            Assert.IsTrue(dragged.dragging);
            Assert.IsFalse(dragged.hovered);
            Assert.AreEqual(EventType.Used, drag.type);

            var up = new Event
            {
                type = EventType.MouseUp,
                button = 0,
                mousePosition = new Vector2(140f, 20f)
            };

            NowInteraction released = Draw(
                up,
                EventType.MouseUp,
                ownsCapture: true);

            Assert.IsTrue(released.released);
            Assert.IsTrue(released.dragEnded);
            Assert.IsFalse(released.clicked);
            Assert.IsFalse(released.cancelled);
            Assert.AreEqual(EventType.Used, up.type);
            Assert.AreEqual(0, NowInput.activeId);
            Assert.AreEqual(
                0,
                GUIUtility.hotControl,
                "Releasing outside the EditorWindow must end native and NowUI capture together.");
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
            GUIUtility.hotControl = previousHotControl;
            GUI.changed = previousChanged;
            Event.current = previousEvent;
        }
    }

    [TestCase(EventType.Ignore)]
    [TestCase(EventType.MouseLeaveWindow)]
    public void IMGUICaptureLossCancelsDraggedScrollbarAndReentryCannotResumeIt(EventType lossType)
    {
        const int scrollbarId = 9137;
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        var track = new NowRect(80f, 10f, 12f, 80f);
        Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        bool previousChanged = GUI.changed;
        float value = 0f;

        bool DrawScrollbar()
        {
            var metrics = NowScrollbar.Calculate(
                NowScrollbarAxis.Vertical,
                track,
                20f,
                100f,
                value,
                10f);

            using (NowInput.Begin(_provider, surface))
                return NowScrollbar.Interact(scrollbarId, NowScrollbarAxis.Vertical, metrics, ref value);
        }

        try
        {
            NowIMGUIInputProvider.repaintRequested = () => { };
            GUI.changed = false;

            var down = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(86f, 18f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, down, EventType.MouseDown, false, out _provider.snapshot));
            Assert.IsTrue(DrawScrollbar());
            provider.NotifyPointerCaptured(NowPointerButton.Primary);
            Assert.AreEqual(scrollbarId, NowInput.activeId);

            var drag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(86f, 130f),
                delta = new Vector2(0f, 112f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, drag, EventType.MouseDrag, true, out _provider.snapshot));
            Assert.IsTrue(DrawScrollbar());
            Assert.AreEqual(scrollbarId, NowInput.activeId);
            Assert.Greater(value, 0f, "The fixture must establish an active scrollbar drag before capture is lost.");

            var lost = new Event
            {
                type = lossType,
                button = 0,
                mousePosition = new Vector2(86f, 130f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, lost, lossType, true, out _provider.snapshot));
            Assert.IsTrue(_provider.snapshot.pointerCaptureCancelled);
            Assert.IsFalse(DrawScrollbar());
            Assert.AreEqual(0, NowInput.activeId, "Capture loss must cancel the active scrollbar immediately.");
            float valueAfterCancellation = value;

            var reentryDrag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(86f, 22f),
                delta = new Vector2(0f, -108f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                reentryDrag,
                EventType.MouseDrag,
                false,
                out _provider.snapshot));
            Assert.IsFalse(DrawScrollbar());
            Assert.AreEqual(0, NowInput.activeId);
            Assert.AreEqual(
                valueAfterCancellation,
                value,
                0.001f,
                "A stray drag after pointer re-entry must not resume the cancelled scrollbar.");
        }
        finally
        {
            provider.ResetState();
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
            GUI.changed = previousChanged;
        }
    }

    [Test]
    public void IMGUIIgnoreWithoutTrackedCaptureDoesNotInventCancellation()
    {
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        int repaintCount = 0;

        try
        {
            NowIMGUIInputProvider.repaintRequested = () => ++repaintCount;
            var ignored = new Event
            {
                type = EventType.Ignore,
                button = 0,
                mousePosition = new Vector2(20f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(
                surface,
                ignored,
                EventType.Ignore,
                false,
                out var snapshot));

            Assert.IsFalse(snapshot.pointerCaptureCancelled);
            Assert.AreEqual(NowPointerButtons.None, snapshot.pointerButtonsDown);
            Assert.AreEqual(NowPointerButtons.None, snapshot.pointerButtonsPressed);
            Assert.AreEqual(NowPointerButtons.None, snapshot.pointerButtonsReleased);
            Assert.AreEqual(0, repaintCount, "An ignored event from another native control is not capture loss for this panel.");
        }
        finally
        {
            provider.ResetState();
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
        }
    }

    [TestCase(EventType.Ignore)]
    [TestCase(EventType.MouseLeaveWindow)]
    public void IMGUICaptureLossCancelsLatchedButtonsAndAdvancesInputPass(EventType lossType)
    {
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        int repaintCount = 0;

        try
        {
            NowIMGUIInputProvider.repaintRequested = () => ++repaintCount;
            var down = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(20f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, down, EventType.MouseDown, false, out var pressed));
            Assert.IsTrue(pressed.primaryDown);
            Assert.IsTrue(pressed.primaryPressed);

            var drag = new Event
            {
                type = EventType.MouseDrag,
                button = 0,
                mousePosition = new Vector2(140f, 20f),
                delta = new Vector2(120f, 0f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, drag, EventType.MouseDrag, true, out var held));
            Assert.IsTrue(held.primaryDown);

            var lost = new Event
            {
                type = lossType,
                button = 0,
                mousePosition = new Vector2(140f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, lost, lossType, true, out var cancelled));
            Assert.IsTrue(cancelled.pointerCaptureCancelled);
            Assert.IsFalse(cancelled.hasPointer);
            Assert.IsFalse(cancelled.primaryDown);
            Assert.Greater(cancelled.inputPass, held.inputPass);

            var move = new Event
            {
                type = EventType.MouseMove,
                mousePosition = new Vector2(20f, 20f)
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, move, EventType.MouseMove, false, out var after));
            Assert.IsFalse(after.primaryDown);
            Assert.IsFalse(after.pointerCaptureCancelled);
            Assert.Greater(after.inputPass, cancelled.inputPass);
            Assert.GreaterOrEqual(repaintCount, 1);
        }
        finally
        {
            provider.ResetState();
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
        }
    }

    [Test]
    public void IMGUIProviderDoesNotEnterCrossSurfacePointerArbitration()
    {
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));
        var move = new Event
        {
            type = EventType.MouseMove,
            mousePosition = new Vector2(20f, 20f)
        };

        for (int i = 0; i < 100; ++i)
        {
            Assert.IsTrue(provider.TryGetSnapshot(surface, move, EventType.MouseMove, false, out _));
            Assert.AreEqual(
                0,
                NowPointerArbiter.currentContentCount,
                "Native IMGUI contexts own unrelated local coordinate spaces and must not compete in the runtime surface arbiter.");
        }
    }

    [Test]
    public void IMGUIPanelReceivesMouseDownDespiteAnUnrelatedArbiterWinner()
    {
        var previousWinner = new object();
        var provider = new NowIMGUIInputProvider();
        var surface = new NowInputSurface(new Vector2(100f, 100f));

        NowPointerArbiter.Claim(
            previousWinner,
            NowPointerArbiter.TierCanvas,
            0f,
            hit: true,
            buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();
        Assert.IsTrue(NowPointerArbiter.IsOwner(previousWinner));

        var down = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(20f, 20f)
        };

        Assert.IsTrue(provider.TryGetSnapshot(
            surface,
            down,
            EventType.MouseDown,
            false,
            out var snapshot));
        Assert.IsTrue(snapshot.hasPointer);
        Assert.IsTrue(snapshot.primaryPressed);
    }

    static NowInputSnapshot InstallIMGUISnapshot(
        NowIMGUIInputProvider provider,
        NowInputSurface surface,
        Event inputEvent,
        EventType routedType,
        bool ownsCapture)
    {
        Assert.NotNull(InputSnapshotField, "NowInput snapshot test seam was not found.");
        Assert.IsTrue(provider.TryGetSnapshot(
            surface,
            inputEvent,
            routedType,
            ownsCapture,
            out var snapshot));
        InputSnapshotField.SetValue(null, snapshot);
        return snapshot;
    }

    static NowInputSnapshot ScrollSnapshot(Vector2 position, Vector2 scrollDelta)
    {
        return new NowInputSnapshot(
            true,
            position,
            position,
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            scrollDelta,
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            1,
            0.5f);
    }

    static NowInputSnapshot SnapshotAt(
        int frame,
        NowPointerButtons down,
        NowPointerButtons pressed,
        NowPointerButtons released)
    {
        var position = new Vector2(18f, 20f);

        return new NowInputSnapshot(
            true,
            position,
            position,
            Vector2.zero,
            down,
            pressed,
            released,
            Vector2.zero,
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            frame,
            frame / 60f);
    }

    sealed class MockInputProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = this.snapshot;
            return true;
        }
    }

    sealed class ThrowingInputProvider : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = default;
            throw new InvalidOperationException("input failed");
        }
    }
}
