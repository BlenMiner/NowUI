using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowInputTests
{
    readonly Rect _rect = new Rect(10, 10, 40, 30);

    MockInputProvider _provider;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        _provider = new MockInputProvider();
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
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
            Assert.AreEqual(new Vector2(12, 12), NowInput.current.pointerPosition);

            using (NowInput.Begin(second, new Vector2(100, 100)))
                Assert.AreEqual(new Vector2(80, 80), NowInput.current.pointerPosition);

            Assert.AreEqual(new Vector2(12, 12), NowInput.current.pointerPosition);
        }

        Assert.IsFalse(NowInput.hasContext);
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
}
