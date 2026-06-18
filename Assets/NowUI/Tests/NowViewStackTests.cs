using System;
using System.Threading;
using NowUI;
using NUnit.Framework;
using UnityEngine;

public class NowViewStackTests
{
    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class RecordingView : INowView
    {
        public int draws;
        public int passiveDraws;
        public NowViewContext lastContext;
        public Action<NowViewContext> onDraw;

        public void Draw(NowViewContext context)
        {
            ++draws;
            lastContext = context;

            if (NowInput.isPassive)
                ++passiveDraws;

            onDraw?.Invoke(context);
        }
    }

    static readonly Vector2 SurfaceSize = new Vector2(512f, 256f);
    static readonly NowRect Surface = new NowRect(0f, 0f, 512f, 256f);
    static readonly NowRect PopupRect = new NowRect(80f, 40f, 180f, 120f);

    FakeProvider _provider;
    NowDrawList _drawList;

    static NowViewOptions InstantFullScreen()
    {
        return NowViewOptions.FullScreen(NowViewTransitionPreset.None, 0f);
    }

    static NowViewOptions InstantPopup()
    {
        return NowViewOptions.Popup(PopupRect, NowViewTransitionPreset.None, 0f);
    }

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
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
    }

    void DrawFrame(NowViewStack stack, NowInputSnapshot snapshot = default)
    {
        _provider.snapshot = snapshot;

        using (NowInput.Begin(_provider, SurfaceSize))
        using (_drawList.Begin(SurfaceSize))
            stack.Draw(Surface);
    }

    [Test]
    public void PushWithoutStoredHandleCanPopTop()
    {
        var stack = new NowViewStack();
        var bottom = new RecordingView();
        var top = new RecordingView();

        stack.Push(bottom, InstantFullScreen());
        stack.Push(top, InstantFullScreen());

        Assert.AreEqual(2, stack.count);
        Assert.IsTrue(stack.Pop());
        Assert.AreEqual(1, stack.count);

        DrawFrame(stack);

        Assert.AreEqual(1, bottom.draws);
        Assert.AreEqual(0, top.draws);
    }

    [Test]
    public void ReturnedHandleCanCloseSpecificEntry()
    {
        var stack = new NowViewStack();
        var bottom = new RecordingView();
        var top = new RecordingView();
        var bottomHandle = stack.Push(bottom, InstantFullScreen());

        stack.Push(top, InstantFullScreen());

        Assert.IsTrue(bottomHandle.Close());
        Assert.AreEqual(1, stack.count);

        DrawFrame(stack);

        Assert.AreEqual(0, bottom.draws);
        Assert.AreEqual(1, top.draws);
    }

    [Test]
    public void KeyedDuplicatePushThrows()
    {
        var stack = new NowViewStack();

        stack.Push("settings", new RecordingView(), InstantFullScreen());

        Assert.Throws<InvalidOperationException>(() =>
            stack.Push("settings", new RecordingView(), InstantFullScreen()));
    }

    [Test]
    public void PopupOutsideClickClosesTop()
    {
        var stack = new NowViewStack();
        var view = new RecordingView();
        var outsidePress = new NowInputSnapshot(new Vector2(20f, 20f), true, true, false);

        stack.Push(view, InstantPopup());
        DrawFrame(stack, outsidePress);

        Assert.AreEqual(1, view.draws);
        Assert.AreEqual(0, stack.count);
    }

    [Test]
    public void FullScreenCancelClosesTop()
    {
        var stack = new NowViewStack();
        var view = new RecordingView();
        var cancel = default(NowInputSnapshot);
        cancel.cancelPressed = true;

        stack.Push(view, InstantFullScreen());
        DrawFrame(stack, cancel);

        Assert.AreEqual(1, view.draws);
        Assert.AreEqual(0, stack.count);
    }

    [Test]
    public void ModalPopupBlocksUnderlyingPointerNextFrame()
    {
        var stack = new NowViewStack();
        var view = new RecordingView();
        var buttonRect = new NowRect(10f, 10f, 100f, 40f);

        stack.Push(view, InstantPopup());
        DrawFrame(stack);

        NowOverlay.ForceNewFrame();
        _provider.snapshot = new NowInputSnapshot(new Vector2(20f, 20f), false, false, false);

        using (NowInput.Begin(_provider, SurfaceSize))
        using (_drawList.Begin(SurfaceSize))
        {
            var interaction = NowInput.Interact(42, buttonRect);
            stack.Draw(Surface);

            Assert.IsFalse(interaction.hovered);
        }
    }

    [Test]
    public void CoveredViewsDrawPassively()
    {
        var stack = new NowViewStack();
        var bottom = new RecordingView();
        var top = new RecordingView();

        stack.Push(bottom, InstantFullScreen());
        stack.Push(top, InstantFullScreen());

        DrawFrame(stack);

        Assert.AreEqual(1, bottom.draws);
        Assert.AreEqual(1, bottom.passiveDraws);
        Assert.AreEqual(1, top.draws);
        Assert.AreEqual(0, top.passiveDraws);
    }

    [Test]
    public void PushDuringDrawIsDeferredUntilNextFrame()
    {
        var stack = new NowViewStack();
        var pushed = new RecordingView();
        var opener = new RecordingView();

        opener.onDraw = _ =>
        {
            if (stack.count == 1)
                stack.Push(pushed, InstantFullScreen());
        };

        stack.Push(opener, InstantFullScreen());
        DrawFrame(stack);

        Assert.AreEqual(2, stack.count);
        Assert.AreEqual(0, pushed.draws);

        DrawFrame(stack);

        Assert.AreEqual(1, pushed.draws);
    }

    [Test]
    public void HandleReturnedDuringDrawCanCancelPendingPush()
    {
        var stack = new NowViewStack();
        var pushed = new RecordingView();
        var opener = new RecordingView();

        opener.onDraw = _ =>
        {
            var handle = stack.Push(pushed, InstantFullScreen());

            Assert.IsTrue(handle.isValid);
            Assert.IsTrue(handle.Close());
        };

        stack.Push(opener, InstantFullScreen());
        DrawFrame(stack);

        Assert.AreEqual(1, stack.count);

        DrawFrame(stack);

        Assert.AreEqual(0, pushed.draws);
    }

    [Test]
    public void ExitTransitionKeepsViewUntilCompletion()
    {
        var stack = new NowViewStack();
        var view = new RecordingView();

        stack.Push(view, NowViewOptions.FullScreen(NowViewTransitionPreset.Fade, 0.03f));

        Assert.IsTrue(stack.Pop());
        Assert.AreEqual(1, stack.count);

        DrawFrame(stack);

        Assert.AreEqual(1, view.draws);
        Assert.AreEqual(1, stack.count);

        Thread.Sleep(60);
        DrawFrame(stack);

        Assert.AreEqual(0, stack.count);
    }
}
