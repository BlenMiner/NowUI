using NowUI;
using NUnit.Framework;
using UnityEngine;

public class NowUIToolkitTests
{
    static readonly NowInputSurface Surface = new NowInputSurface(new Vector2(200f, 100f));

    [Test]
    public void UIToolkitInputProviderReportsPointerClickEdges()
    {
        var provider = new NowUIToolkitInputProvider();

        provider.SetPointerDown(new Vector2(32f, 24f), 0, 1);
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var pressed));

        Assert.IsTrue(pressed.hasPointer);
        Assert.AreEqual(new Vector2(32f, 24f), pressed.pointerPosition);
        Assert.IsTrue(pressed.primaryDown);
        Assert.IsTrue(pressed.primaryPressed);
        Assert.IsFalse(pressed.primaryReleased);

        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var held));
        Assert.IsTrue(held.primaryDown);
        Assert.IsFalse(held.primaryPressed);
        Assert.IsFalse(held.primaryReleased);

        provider.SetPointerUp(new Vector2(36f, 28f), 0, 0);
        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var released));

        Assert.IsFalse(released.primaryDown);
        Assert.IsFalse(released.primaryPressed);
        Assert.IsTrue(released.primaryReleased);
        Assert.AreEqual(new Vector2(4f, 4f), released.pointerDelta);
    }

    [Test]
    public void UIToolkitInputProviderKeepsKeyboardNavigationUntilRelease()
    {
        var provider = new NowUIToolkitInputProvider();

        Assert.IsTrue(provider.KeyDown(KeyCode.RightArrow));
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var first));
        Assert.AreEqual(Vector2.right, first.navigation);

        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var held));
        Assert.AreEqual(Vector2.right, held.navigation);

        Assert.IsTrue(provider.KeyUp(KeyCode.RightArrow));
        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var released));
        Assert.AreEqual(Vector2.zero, released.navigation);
    }

    [Test]
    public void UIToolkitInputProviderReportsTabFocusPulses()
    {
        var provider = new NowUIToolkitInputProvider();

        Assert.IsTrue(provider.KeyDown(KeyCode.Tab));
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var next));
        Assert.IsTrue(next.focusNextPressed);
        Assert.IsFalse(next.focusPreviousPressed);

        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var idle));
        Assert.IsFalse(idle.focusNextPressed);
        Assert.IsFalse(idle.focusPreviousPressed);

        Assert.IsTrue(provider.KeyDown(KeyCode.Tab, shift: true));
        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var previous));
        Assert.IsTrue(previous.focusPreviousPressed);
        Assert.IsFalse(previous.focusNextPressed);
    }

    [Test]
    public void UIToolkitInputProviderTreatsSubmitNavigationAsPulse()
    {
        var provider = new NowUIToolkitInputProvider();

        provider.PressSubmit();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var submit));
        Assert.IsTrue(submit.submitDown);
        Assert.IsTrue(submit.submitPressed);
        Assert.IsTrue(submit.submitReleased);

        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var idle));
        Assert.IsFalse(idle.submitDown);
        Assert.IsFalse(idle.submitPressed);
        Assert.IsFalse(idle.submitReleased);
    }

    [Test]
    public void UIToolkitInputProviderKeepsTransientsForRepeatReadsWithinOneFrame()
    {
        var provider = new NowUIToolkitInputProvider();

        provider.SetPointerDown(new Vector2(10f, 10f), 0, 1);
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var first));
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var second));

        Assert.IsTrue(first.primaryPressed);
        Assert.IsTrue(second.primaryPressed);
        Assert.IsTrue(second.primaryDown);

        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var next));
        Assert.IsFalse(next.primaryPressed);
        Assert.IsTrue(next.primaryDown);
    }

    [Test]
    public void UIToolkitInputProviderNormalizesWheelDeltaToNotches()
    {
        var provider = new NowUIToolkitInputProvider();

        provider.AddScrollDelta(new Vector2(3f, 3f));
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var snapshot));

        Assert.AreEqual(1f, snapshot.scrollDelta.x, 0.0001f);
        Assert.AreEqual(-1f, snapshot.scrollDelta.y, 0.0001f);

        provider.Invalidate();
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var cleared));
        Assert.AreEqual(Vector2.zero, cleared.scrollDelta);
    }

    [Test]
    public void VisualElementHostTypesExposeExplicitAndLayoutContracts()
    {
        var element = new NowVisualElement();
        var layoutElement = new NowLayoutVisualElement();

        try
        {
            Assert.IsFalse(element.rebuildEveryFrame);
            Assert.IsTrue(element.autoRebuildOnInteraction);
            Assert.IsInstanceOf<NowVisualElement>(layoutElement);
            Assert.AreNotEqual(element.GetType(), layoutElement.GetType());
            Assert.IsNull(typeof(NowVisualElement).GetProperty("layoutMeasurePass"),
                "Measurement is selected by the dedicated host type, not a mutable public toggle.");
            Assert.IsTrue(element.usePanelScale);

            element.uiScale = -5f;
            Assert.AreEqual(1f, element.uiScale, 0.0001f);

            element.clearColor = Color.black;
            Assert.AreEqual(Color.black, element.clearColor);
        }
        finally
        {
            layoutElement.Dispose();
            element.Dispose();
        }
    }

}
