using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowUIInputTests
{
    readonly Rect _rect = new Rect(10, 10, 40, 30);

    MockInputProvider _provider;

    [SetUp]
    public void SetUp()
    {
        NowUIInput.Reset();
        _provider = new MockInputProvider();
    }

    [TearDown]
    public void TearDown()
    {
        NowUIInput.Reset();
    }

    [Test]
    public void InteractionReportsHoverFromProviderSnapshot()
    {
        _provider.snapshot = new NowUIInputSnapshot(new Vector2(18, 20), false, false, false);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var interaction = NowUIInput.Interact(1, _rect);

            Assert.IsTrue(interaction.hovered);
            Assert.IsFalse(interaction.pressed);
            Assert.IsFalse(interaction.clicked);
        }
    }

    [Test]
    public void InteractionClicksWhenPressedAndReleasedInsideRect()
    {
        _provider.snapshot = new NowUIInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var press = NowUIInput.Interact(1, _rect);

            Assert.IsTrue(press.pressed);
            Assert.IsTrue(press.held);
            Assert.IsTrue(press.active);
            Assert.IsFalse(press.clicked);
        }

        _provider.snapshot = new NowUIInputSnapshot(new Vector2(18, 20), false, false, true);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var release = NowUIInput.Interact(1, _rect);

            Assert.IsTrue(release.released);
            Assert.IsTrue(release.clicked);
            Assert.IsFalse(release.dragEnded);
        }

        Assert.AreEqual(0, NowUIInput.activeId);
    }

    [Test]
    public void InteractionDoesNotClickAfterDrag()
    {
        _provider.snapshot = new NowUIInputSnapshot(new Vector2(18, 20), true, true, false);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
            NowUIInput.Interact(1, _rect);

        _provider.snapshot = new NowUIInputSnapshot(new Vector2(28, 20), new Vector2(10, 0), true, false, false);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var drag = NowUIInput.Interact(1, _rect);

            Assert.IsTrue(drag.dragStarted);
            Assert.IsTrue(drag.dragging);
            Assert.AreEqual(new Vector2(10, 0), drag.dragDelta);
        }

        _provider.snapshot = new NowUIInputSnapshot(new Vector2(30, 20), new Vector2(2, 0), false, false, true);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var release = NowUIInput.Interact(1, _rect);

            Assert.IsTrue(release.dragEnded);
            Assert.IsFalse(release.clicked);
        }
    }

    [Test]
    public void InteractionCanUseSecondaryPointerButton()
    {
        _provider.snapshot = new NowUIInputSnapshot(
            new Vector2(18, 20),
            NowUIPointerButtons.Secondary,
            NowUIPointerButtons.Secondary,
            NowUIPointerButtons.None);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var primary = NowUIInput.Interact(1, _rect);
            var secondary = NowUIInput.Interact(2, _rect, NowUIPointerButton.Secondary);

            Assert.IsFalse(primary.pressed);
            Assert.IsTrue(secondary.pressed);
            Assert.IsTrue(secondary.held);
            Assert.AreEqual(NowUIPointerButton.Secondary, secondary.button);
        }

        _provider.snapshot = new NowUIInputSnapshot(
            new Vector2(18, 20),
            NowUIPointerButtons.None,
            NowUIPointerButtons.None,
            NowUIPointerButtons.Secondary);

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var secondary = NowUIInput.Interact(2, _rect, NowUIPointerButton.Secondary);

            Assert.IsTrue(secondary.released);
            Assert.IsTrue(secondary.clicked);
        }
    }

    [Test]
    public void SnapshotCanCarryNavigationWithoutPointer()
    {
        _provider.snapshot = new NowUIInputSnapshot(
            false,
            default,
            default,
            default,
            NowUIPointerButtons.None,
            NowUIPointerButtons.None,
            NowUIPointerButtons.None,
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

        using (NowUIInput.Begin(_provider, new Vector2(100, 100)))
        {
            var interaction = NowUIInput.Interact(1, _rect);

            Assert.IsFalse(interaction.hovered);
            Assert.IsFalse(interaction.pressed);
            Assert.AreEqual(Vector2.right, NowUIInput.current.navigation);
            Assert.IsTrue(NowUIInput.current.submitDown);
            Assert.IsTrue(NowUIInput.current.submitPressed);
        }
    }

    [Test]
    public void InputScopeRestoresPreviousContext()
    {
        var first = new MockInputProvider
        {
            snapshot = new NowUIInputSnapshot(new Vector2(12, 12), false, false, false)
        };
        var second = new MockInputProvider
        {
            snapshot = new NowUIInputSnapshot(new Vector2(80, 80), false, false, false)
        };

        using (NowUIInput.Begin(first, new Vector2(100, 100)))
        {
            Assert.AreEqual(new Vector2(12, 12), NowUIInput.current.pointerPosition);

            using (NowUIInput.Begin(second, new Vector2(100, 100)))
                Assert.AreEqual(new Vector2(80, 80), NowUIInput.current.pointerPosition);

            Assert.AreEqual(new Vector2(12, 12), NowUIInput.current.pointerPosition);
        }

        Assert.IsFalse(NowUIInput.hasContext);
    }

    sealed class MockInputProvider : INowUIInputProvider
    {
        public NowUIInputSnapshot snapshot;

        public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot)
        {
            snapshot = this.snapshot;
            return true;
        }
    }
}
