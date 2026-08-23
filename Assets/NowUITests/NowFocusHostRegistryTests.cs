using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowFocusHostRegistryTests
{
    const int FirstHost = 101;
    const int SecondHost = 202;

    static NowResolvedId OwnerRoot(int hostId) =>
        NowResolvedId.CreateOwnerRoot(unchecked(0x4E6F775549486F73UL + (uint)hostId));

    static NowResolvedId HostId(int hostId) =>
        OwnerRoot(hostId).InDomain(NowIdDomain.FocusHost);

    static NowResolvedId ControlId(int hostId, int controlId) =>
        OwnerRoot(hostId).Derive(NowIdDomain.Control, controlId);

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
    }

    [Test]
    public void IdleHostKeepsItsFocusablePolicyWhileAnotherHostRebuilds()
    {
        RegisterHost(
            FirstHost,
            (11, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.Directional));

        RegisterHost(
            SecondHost,
            (21, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None));

        Assert.AreEqual(
            NowFocusMoveResult.Seeded,
            NowFocus.EnterUGUINavigation(HostId(FirstHost), Vector2.zero));
        Assert.AreEqual(ControlId(FirstHost, 11), NowFocus.focusedResolvedId);

        RegisterHost(
            SecondHost,
            (21, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None),
            (22, new NowRect(90f, 10f, 60f, 30f), NowFocusNavigationLock.None));

        Assert.AreEqual(
            NowFocusMoveResult.Consumed,
            NowFocus.RouteUGUINavigation(HostId(FirstHost), Vector2.right));
        Assert.AreEqual(ControlId(FirstHost, 11), NowFocus.focusedResolvedId);
        Assert.IsTrue(NowFocus.IsFocusedInHost(HostId(FirstHost)));
        Assert.IsFalse(NowFocus.IsFocusedInHost(HostId(SecondHost)));
    }

    [Test]
    public void SynchronousRoutingReportsMoveThenBoundary()
    {
        RegisterHost(
            FirstHost,
            (11, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None),
            (12, new NowRect(90f, 10f, 60f, 30f), NowFocusNavigationLock.None));

        Assert.AreEqual(
            NowFocusMoveResult.Seeded,
            NowFocus.EnterUGUINavigation(HostId(FirstHost), Vector2.zero));
        Assert.AreEqual(ControlId(FirstHost, 11), NowFocus.focusedResolvedId);

        Assert.AreEqual(
            NowFocusMoveResult.Moved,
            NowFocus.RouteUGUINavigation(HostId(FirstHost), Vector2.right));
        Assert.AreEqual(ControlId(FirstHost, 12), NowFocus.focusedResolvedId);

        Assert.AreEqual(
            NowFocusMoveResult.Boundary,
            NowFocus.RouteUGUINavigation(HostId(FirstHost), Vector2.right));
        Assert.AreEqual(ControlId(FirstHost, 12), NowFocus.focusedResolvedId);
    }

    [Test]
    public void DirectionalEntrySeedsTheEdgeOppositeTheInboundMove()
    {
        RegisterHost(
            FirstHost,
            (11, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None),
            (12, new NowRect(90f, 10f, 60f, 30f), NowFocusNavigationLock.None));

        Assert.AreEqual(
            NowFocusMoveResult.Seeded,
            NowFocus.EnterUGUINavigation(HostId(FirstHost), Vector2.left));
        Assert.AreEqual(ControlId(FirstHost, 12), NowFocus.focusedResolvedId);

        NowFocus.Clear();

        Assert.AreEqual(
            NowFocusMoveResult.Seeded,
            NowFocus.EnterUGUINavigation(HostId(FirstHost), Vector2.right));
        Assert.AreEqual(ControlId(FirstHost, 11), NowFocus.focusedResolvedId);
    }

    [Test]
    public void FocusRequestedBeforeFirstDrawAdoptsTheRegisteringHost()
    {
        NowResolvedId resolvedControlId = NowResolvedId.CreateOwnerRoot(0x464F435553544553UL).Child(7001);
        NowFocus.Focus(resolvedControlId);

        Assert.AreEqual(resolvedControlId, NowFocus.focusedResolvedId);
        Assert.IsFalse(NowFocus.IsFocusedInHost(HostId(FirstHost)));

        using (NowFocus.BeginHostRegistration(HostId(FirstHost), null))
        {
            NowFocus.Register(
                resolvedControlId,
                new NowRect(10f, 10f, 60f, 30f),
                default,
                NowFocusNavigationLock.None);
        }

        Assert.AreEqual(resolvedControlId, NowFocus.focusedResolvedId);
        Assert.IsTrue(NowFocus.IsFocusedInHost(HostId(FirstHost)));
    }

    [Test]
    public void UnregisteringAnotherHostDoesNotClearTheFocusOwner()
    {
        RegisterHost(
            FirstHost,
            (11, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None));
        RegisterHost(
            SecondHost,
            (21, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None));

        NowFocus.EnterUGUINavigation(HostId(FirstHost), Vector2.zero);
        NowFocus.UnregisterHost(HostId(SecondHost));

        Assert.AreEqual(ControlId(FirstHost, 11), NowFocus.focusedResolvedId);
        Assert.IsTrue(NowFocus.IsFocusedInHost(HostId(FirstHost)));

        NowFocus.UnregisterHost(HostId(FirstHost));

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void RemovedControlIsNotResolvedFromTheRecycledBuildBuffer()
    {
        RegisterHost(
            FirstHost,
            (11, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None));
        RegisterHost(FirstHost);

        NowFocus.Clear();
        NowResolvedId removedId = ControlId(FirstHost, 11);
        NowFocus.Focus(removedId);

        Assert.AreEqual(removedId, NowFocus.focusedResolvedId);
        Assert.IsFalse(NowFocus.IsFocusedInHost(HostId(FirstHost)));
    }

    static void RegisterHost(
        int hostId,
        params (int id, NowRect rect, NowFocusNavigationLock navigationLock)[] controls)
    {
        using (NowFocus.BeginHostRegistration(HostId(hostId), null))
        {
            for (int i = 0; i < controls.Length; ++i)
            {
                var control = controls[i];
                NowFocus.Register(
                    ControlId(hostId, control.id),
                    control.rect,
                    default,
                    control.navigationLock);
            }
        }
    }
}
