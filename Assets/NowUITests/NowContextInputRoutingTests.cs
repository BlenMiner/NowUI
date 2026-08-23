using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// Regression coverage for context-menu pointer ownership and the input
/// context captured by deferred overlays.
/// </summary>
public class NowContextInputRoutingTests
{
    sealed class InputProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    static readonly Vector2 SurfaceSize = new Vector2(640f, 480f);
    static readonly NowRect ContextRegion = new NowRect(20f, 20f, 180f, 80f);

    InputProvider _outerProvider;
    InputProvider _innerProvider;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowLayout.Reset();

        _outerProvider = new InputProvider();
        _innerProvider = new InputProvider();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowContextMenu.Reset();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    [Test]
    public void OpeningContextTriggerClaimsOnlyItsSecondaryPressForThatInputPass()
    {
        Vector2 pointer = ContextRegion.center;
        NowPointerButtons initialButtons =
            NowPointerButtons.Secondary | NowPointerButtons.Middle;
        _outerProvider.snapshot = PointerSnapshot(
            pointer,
            initialButtons,
            initialButtons,
            NowPointerButtons.None,
            inputPass: 100);

        NowResolvedId middleId = NowResolvedId.None;
        NowResolvedId secondarySiblingId = NowResolvedId.None;

        using (NowInput.Begin(_outerProvider, SurfaceSize))
        {
            NowResolvedId menuId = NowControls.GetControlId("claimed-secondary-menu");
            middleId = NowControls.GetControlId("middle-sibling");
            secondarySiblingId = NowControls.GetControlId("secondary-sibling");

            NowContextTrigger trigger = NowContextAction.Resolve(
                ContextRegion,
                actionInvoked: false,
                actionAnchor: default);

            Assert.IsTrue(trigger.triggered);
            Assert.AreEqual(NowContextTriggerSource.SecondaryPointer, trigger.source);

            NowContextMenu.Open(menuId, in trigger);

            Assert.IsFalse(
                NowInput.WasRightClicked(ContextRegion),
                "A later sibling must not observe the secondary press that opened the menu.");
            Assert.IsFalse(
                NowInput.Interact(
                    secondarySiblingId,
                    ContextRegion,
                    NowPointerButton.Secondary).pressed,
                "A later sibling must not capture the secondary press that opened the menu.");
            Assert.IsTrue(
                NowInput.Interact(middleId, ContextRegion, NowPointerButton.Middle).pressed,
                "Claiming a context press must not consume another pointer button in the same pass.");

            NowContextMenu.Close();
        }

        _outerProvider.snapshot = PointerSnapshot(
            pointer,
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.Middle,
            inputPass: 101);

        using (NowInput.Begin(_outerProvider, SurfaceSize))
        {
            Assert.IsTrue(
                NowInput.Interact(middleId, ContextRegion, NowPointerButton.Middle).released,
                "The unrelated middle capture should release normally on the next pass.");
            Assert.IsTrue(
                NowInput.WasRightClicked(ContextRegion),
                "The secondary claim must reset for the provider's next input pass.");
            Assert.IsTrue(
                NowInput.Interact(
                    secondarySiblingId,
                    ContextRegion,
                    NowPointerButton.Secondary).pressed,
                "A fresh secondary press in a later pass must remain capturable.");
        }
    }

    [Test]
    public void SecondaryChildCapturePreventsLaterParentContextActionInSamePass()
    {
        var childRect = new NowRect(70f, 40f, 60f, 40f);
        Vector2 pointer = childRect.center;
        _outerProvider.snapshot = PointerSnapshot(
            pointer,
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.None,
            inputPass: 150);

        using (NowInput.Begin(_outerProvider, SurfaceSize))
        {
            NowResolvedId childId = NowControls.GetControlId("secondary-child");
            NowInteraction child = NowInput.Interact(
                childId,
                childRect,
                NowPointerButton.Secondary);

            Assert.IsTrue(child.pressed);
            Assert.IsFalse(
                NowInput.WasRightClicked(ContextRegion),
                "A parent declared after a captured child must not observe the child's press.");

            NowContextTrigger parent = NowContextAction.Resolve(
                ContextRegion,
                actionInvoked: false,
                actionAnchor: default);
            Assert.IsFalse(
                parent.triggered,
                "A later parent context action must stand down after its child captures Secondary.");
        }
    }

    [Test]
    public void DeferredMenuRestoresNestedInputContextAndIgnoresItsOpeningPress()
    {
        var outerSurface = new NowInputSurface(SurfaceSize);
        var innerSurface = new NowInputSurface(
            new Vector2(240f, 160f),
            new Rect(70f, 45f, 480f, 320f));
        var outerPointer = new Vector2(560f, 420f);
        var innerPointer = new Vector2(60f, 50f);

        _outerProvider.snapshot = PointerSnapshot(
            outerPointer,
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.None,
            inputPass: 200);
        _innerProvider.snapshot = PointerSnapshot(
            innerPointer,
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.None,
            inputPass: 201);

        INowInputProvider callbackProvider = null;
        NowInputSurface callbackSurface = default;
        NowInputSnapshot callbackSnapshot = default;
        int callbackCount = 0;

        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(_outerProvider, outerSurface))
        using (_drawList.Begin(outerSurface.size))
        {
            using (NowInput.Begin(_innerProvider, innerSurface))
            {
                NowResolvedId menuId = NowControls.GetControlId("nested-input-context-menu");
                NowContextTrigger trigger = NowContextAction.Resolve(
                    new NowRect(0f, 0f, innerSurface.size.x, innerSurface.size.y),
                    actionInvoked: false,
                    actionAnchor: default);

                Assert.IsTrue(trigger.triggered);
                NowContextMenu.Open(menuId, in trigger);
                Assert.IsTrue(NowContextMenu.Begin(menuId));
                NowContextMenu.Item("Action", id: "action");
                NowContextMenu.End();

                NowOverlay.DeferScreen(
                    new NowRect(innerSurface.size.x - 2f, innerSurface.size.y - 2f, 1f, 1f),
                    () =>
                    {
                        ++callbackCount;
                        callbackProvider = NowInput.currentProvider;
                        callbackSurface = NowInput.surface;
                        callbackSnapshot = NowInput.current;
                    });
            }

            Assert.AreSame(_outerProvider, NowInput.currentProvider);
            Assert.AreEqual(200, NowInput.current.inputPass);

            NowOverlay.Flush();

            Assert.AreSame(
                _outerProvider,
                NowInput.currentProvider,
                "Each deferred callback must restore the surrounding input context when it returns.");
            Assert.AreEqual(200, NowInput.current.inputPass);
        }

        Assert.AreEqual(1, callbackCount);
        Assert.AreSame(
            _innerProvider,
            callbackProvider,
            "A deferred callback must run against the provider active when it was queued.");
        Assert.AreEqual(innerSurface.size, callbackSurface.size);
        Assert.AreEqual(innerSurface.screenRect, callbackSurface.screenRect);
        Assert.AreEqual(innerPointer, callbackSnapshot.pointerPosition);
        Assert.AreEqual(201, callbackSnapshot.inputPass);
        Assert.IsTrue(
            NowContextMenu.isOpen,
            "Restoring the nested opening pass prevents its press from immediately dismissing the menu.");

        NowContextMenu.Close();
    }

    static NowInputSnapshot PointerSnapshot(
        Vector2 position,
        NowPointerButtons down,
        NowPointerButtons pressed,
        NowPointerButtons released,
        int inputPass)
    {
        var snapshot = new NowInputSnapshot(
            hasPointer: true,
            pointerPosition: position,
            previousPointerPosition: position,
            pointerDelta: Vector2.zero,
            pointerButtonsDown: down,
            pointerButtonsPressed: pressed,
            pointerButtonsReleased: released,
            scrollDelta: Vector2.zero,
            navigation: Vector2.zero,
            focusPreviousPressed: false,
            focusNextPressed: false,
            submitDown: false,
            submitPressed: false,
            submitReleased: false,
            cancelDown: false,
            cancelPressed: false,
            cancelReleased: false,
            frame: inputPass,
            time: inputPass * 0.01f);
        snapshot.inputPass = inputPass;
        return snapshot;
    }
}
