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
