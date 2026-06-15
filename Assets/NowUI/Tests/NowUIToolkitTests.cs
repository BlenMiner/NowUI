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

        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var held));
        Assert.IsTrue(held.primaryDown);
        Assert.IsFalse(held.primaryPressed);
        Assert.IsFalse(held.primaryReleased);

        provider.SetPointerUp(new Vector2(36f, 28f), 0, 0);
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

        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var held));
        Assert.AreEqual(Vector2.right, held.navigation);

        Assert.IsTrue(provider.KeyUp(KeyCode.RightArrow));
        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var released));
        Assert.AreEqual(Vector2.zero, released.navigation);
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

        Assert.IsTrue(provider.TryGetSnapshot(Surface, out var idle));
        Assert.IsFalse(idle.submitDown);
        Assert.IsFalse(idle.submitPressed);
        Assert.IsFalse(idle.submitReleased);
    }

    [Test]
    public void VisualElementExposesSafeDefaults()
    {
        var element = new NowVisualElement();

        try
        {
            Assert.IsFalse(element.rebuildEveryFrame);
            Assert.IsTrue(element.autoRebuildOnInteraction);
            Assert.IsTrue(element.layoutMeasurePass);
            Assert.IsTrue(element.usePanelScale);

            element.uiScale = -5f;
            Assert.AreEqual(1f, element.uiScale, 0.0001f);

            element.clearColor = Color.black;
            Assert.AreEqual(Color.black, element.clearColor);
        }
        finally
        {
            element.Dispose();
        }
    }
}
