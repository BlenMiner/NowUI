using NowUI;
using NUnit.Framework;
using UnityEngine;

public class NowDialogTests
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

    sealed class Owner
    {
        public int closed;
        public int confirmed;
        public int canceled;
    }

    static readonly Vector2 SurfaceSize = new Vector2(512f, 256f);
    static readonly NowRect Surface = new NowRect(0f, 0f, 512f, 256f);

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

    static NowInputSnapshot CancelSnapshot()
    {
        var snapshot = default(NowInputSnapshot);
        snapshot.cancelPressed = true;
        return snapshot;
    }

    [Test]
    public void MessageBoxCancelInvokesClosedAndPops()
    {
        var stack = new NowViewStack();
        int closed = 0;

        stack.Push(
            NowViews.MessageBox("Saved", "Your changes were saved.", () => ++closed),
            NowViewOptions.FullScreen(NowViewTransitionPreset.None, 0f));

        DrawFrame(stack, CancelSnapshot());

        Assert.AreEqual(1, closed);
        Assert.AreEqual(0, stack.count);
    }

    [Test]
    public void ConfirmCancelInvokesCancelOnly()
    {
        var stack = new NowViewStack();
        int confirmed = 0;
        int canceled = 0;

        stack.Push(
            NowViews.Confirm("Delete?", "This cannot be undone.", () => ++confirmed, () => ++canceled),
            NowViewOptions.FullScreen(NowViewTransitionPreset.None, 0f));

        DrawFrame(stack, CancelSnapshot());

        Assert.AreEqual(0, confirmed);
        Assert.AreEqual(1, canceled);
        Assert.AreEqual(0, stack.count);
    }

    [Test]
    public void DialogOwnerCallbacksAvoidCapturingLambdaRequirement()
    {
        var stack = new NowViewStack();
        var owner = new Owner();

        stack.Push(
            NowViews.MessageBox(
                "Notice",
                "Done.",
                owner,
                static target => ++target.closed),
            NowViewOptions.FullScreen(NowViewTransitionPreset.None, 0f));

        DrawFrame(stack, CancelSnapshot());

        Assert.AreEqual(1, owner.closed);
        Assert.AreEqual(0, owner.confirmed);
        Assert.AreEqual(0, owner.canceled);
        Assert.AreEqual(0, stack.count);
    }
}
