using System;
using NUnit.Framework;
using NowUI;
using UnityEngine;

public sealed class NowContextMenuLifetimeHost : MonoBehaviour
{
}

public class NowContextMenuOwnerLifetimeTests
{
    static readonly Vector2 Surface = new Vector2(480f, 320f);
    static readonly Vector2 Anchor = new Vector2(24f, 24f);

    NowInputReplay _ownerInput;
    NowInputReplay _otherInput;
    NowDrawList _drawList;
    GameObject _ownerObject;
    GameObject _otherObject;
    NowContextMenuLifetimeHost _owner;
    NowContextMenuLifetimeHost _otherOwner;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();

        _ownerInput = new NowInputReplay();
        _otherInput = new NowInputReplay();
        _ownerInput.Idle(hasPointer: true);
        _otherInput.Idle(hasPointer: true);
        _drawList = new NowDrawList();
        _ownerObject = new GameObject("Context menu lifetime owner");
        _otherObject = new GameObject("Other context menu lifetime owner");
        _owner = _ownerObject.AddComponent<NowContextMenuLifetimeHost>();
        _otherOwner = _otherObject.AddComponent<NowContextMenuLifetimeHost>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_owner)
            NowOverlay.ReleaseRegistrationOwner(_owner);

        if (_otherOwner)
            NowOverlay.ReleaseRegistrationOwner(_otherOwner);

        _drawList.Dispose();

        if (_ownerObject)
            UnityEngine.Object.DestroyImmediate(_ownerObject);

        if (_otherObject)
            UnityEngine.Object.DestroyImmediate(_otherObject);

        NowContextMenu.Reset();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
    }

    [Test]
    public void IdleRetainedOwnerKeepsItsOpenMenu()
    {
        NowResolvedId menuId = default;

        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("idle-owner-menu");
            OpenAndDeclare(menuId);
        });

        for (int i = 0; i < 4; ++i)
            NowOverlay.ForceNewFrame();

        Assert.That(NowContextMenu.IsOpen(menuId), Is.True,
            "No owner pass ran, so retained-host idleness must not invalidate its last menu declaration.");
    }

    [Test]
    public void SubsequentOwnerPassWithoutDeclarationClosesMenu()
    {
        NowResolvedId menuId = default;

        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("vanished-menu");
            OpenAndDeclare(menuId);
        });

        Frame(_otherOwner, _otherInput, () => { });
        Assert.That(NowContextMenu.IsOpen(menuId), Is.True,
            "A different owner's pass must not invalidate this owner's menu.");

        Frame(_owner, _ownerInput, () => { });
        Assert.That(NowContextMenu.IsOpen(menuId), Is.False,
            "Once the owning host completes a later pass without declaring the menu, stale global menu state must close.");
    }

    [Test]
    public void FailedOwnerPassDoesNotInvalidateLastSuccessfulDeclaration()
    {
        NowResolvedId menuId = default;

        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("failed-pass-menu");
            OpenAndDeclare(menuId);
        });

        FailedFrame(_owner, _ownerInput);

        Assert.That(NowContextMenu.IsOpen(menuId), Is.True,
            "A rolled-back pass is not evidence that the owner intentionally stopped declaring its menu.");

        Frame(_owner, _ownerInput, () => Declare(menuId));
        Assert.That(NowContextMenu.IsOpen(menuId), Is.True);
    }

    [Test]
    public void ReleasingOwnerClosesOpenMenuImmediately()
    {
        NowResolvedId menuId = default;

        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("released-owner-menu");
            OpenAndDeclare(menuId);
        });

        NowOverlay.ReleaseRegistrationOwner(_owner);

        Assert.That(NowContextMenu.IsOpen(menuId), Is.False);
        Assert.That(NowContextMenu.pendingDeliveryCount, Is.Zero);
    }

    [Test]
    public void NestedSiblingProviderCannotDeclareOpenMenu()
    {
        NowResolvedId menuId = default;

        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("provider-isolated-menu");
            OpenAndDeclare(menuId);
        });

        Frame(_owner, _ownerInput, () =>
        {
            using (NowInput.Begin(_otherInput, Surface))
            {
                Assert.That(NowContextMenu.Begin(menuId), Is.False,
                    "The outer owner supplies liveness, but the nested provider must not claim another surface's menu.");
            }

            Declare(menuId);
        });

        Assert.That(NowContextMenu.IsOpen(menuId), Is.True);
    }

    [Test]
    public void PendingClickIsScopedToOwnerAndDroppedAfterItsNextPass()
    {
        NowResolvedId menuId = default;
        Vector2 itemPoint = ContextItemPoint();

        _ownerInput.Move(itemPoint);
        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("pending-owner-menu");
            OpenAndDeclare(menuId);
        });

        _ownerInput.Press(itemPoint);
        Frame(_owner, _ownerInput, () => Declare(menuId));

        _ownerInput.Release(itemPoint);
        Frame(_owner, _ownerInput, () => Declare(menuId));

        Assert.That(NowContextMenu.isOpen, Is.False);
        Assert.That(NowContextMenu.pendingDeliveryCount, Is.EqualTo(1));

        Frame(_otherOwner, _otherInput, () =>
        {
            NowResolvedId otherMenu = NowControls.GetControlId("unrelated-menu");
            Assert.That(NowContextMenu.Begin(otherMenu), Is.False);
        });

        Assert.That(NowContextMenu.pendingDeliveryCount, Is.EqualTo(1),
            "An unrelated owner's pass must neither receive nor discard another owner's pending click.");

        _ownerInput.Idle(hasPointer: true);
        Frame(_owner, _ownerInput, () => { });

        Assert.That(NowContextMenu.pendingDeliveryCount, Is.Zero,
            "If the clicked menu vanishes, its owner gets one pass and then the pending delivery is discarded.");
    }

    [Test]
    public void ReleasingOwnerDropsPendingClick()
    {
        NowResolvedId menuId = default;
        Vector2 itemPoint = ContextItemPoint();

        _ownerInput.Move(itemPoint);
        Frame(_owner, _ownerInput, () =>
        {
            menuId = NowControls.GetControlId("released-pending-menu");
            OpenAndDeclare(menuId);
        });

        _ownerInput.Press(itemPoint);
        Frame(_owner, _ownerInput, () => Declare(menuId));
        _ownerInput.Release(itemPoint);
        Frame(_owner, _ownerInput, () => Declare(menuId));

        Assert.That(NowContextMenu.pendingDeliveryCount, Is.EqualTo(1));

        NowOverlay.ReleaseRegistrationOwner(_owner);

        Assert.That(NowContextMenu.pendingDeliveryCount, Is.Zero);
    }

    void Frame(Component owner, NowInputReplay provider, Action draw)
    {
        NowOverlay.ForceNewFrame();

        using (NowOverlay.Host(owner))
        using (NowInput.Begin(provider, Surface))
        using (_drawList.Begin(Surface))
        {
            draw();
            NowOverlay.Flush();
        }
    }

    void FailedFrame(Component owner, NowInputReplay provider)
    {
        NowOverlay.ForceNewFrame();

        using (NowOverlay.Host(owner))
        {
            var input = NowInput.Begin(provider, Surface);

            using (_drawList.Begin(Surface))
                NowOverlay.Flush();

            NowOverlay.EndFrameTransaction(completed: false);
            input.Dispose();
        }
    }

    static void OpenAndDeclare(NowResolvedId menuId)
    {
        NowContextMenu.Open(menuId, Anchor);
        Declare(menuId);
    }

    static void Declare(NowResolvedId menuId)
    {
        if (!NowContextMenu.Begin(menuId))
            return;

        NowContextMenu.Item("Action", id: "action");
        NowContextMenu.End();
    }

    static Vector2 ContextItemPoint()
    {
        var styles = NowTheme.themeAsset.controlStyles;
        return new Vector2(
            Anchor.x + styles.popupPadding + 12f,
            Anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);
    }
}
