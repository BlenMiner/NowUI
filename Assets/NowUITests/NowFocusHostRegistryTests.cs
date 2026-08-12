using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowFocusHostRegistryTests
{
    const int FirstHost = 101;
    const int SecondHost = 202;

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
            NowFocus.EnterUGUINavigation(FirstHost, Vector2.zero));
        Assert.AreEqual(11, NowFocus.focusedId);

        RegisterHost(
            SecondHost,
            (21, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None),
            (22, new NowRect(90f, 10f, 60f, 30f), NowFocusNavigationLock.None));

        Assert.AreEqual(
            NowFocusMoveResult.Consumed,
            NowFocus.RouteUGUINavigation(FirstHost, Vector2.right));
        Assert.AreEqual(11, NowFocus.focusedId);
        Assert.IsTrue(NowFocus.IsFocusedInHost(FirstHost));
        Assert.IsFalse(NowFocus.IsFocusedInHost(SecondHost));
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
            NowFocus.EnterUGUINavigation(FirstHost, Vector2.zero));
        Assert.AreEqual(11, NowFocus.focusedId);

        Assert.AreEqual(
            NowFocusMoveResult.Moved,
            NowFocus.RouteUGUINavigation(FirstHost, Vector2.right));
        Assert.AreEqual(12, NowFocus.focusedId);

        Assert.AreEqual(
            NowFocusMoveResult.Boundary,
            NowFocus.RouteUGUINavigation(FirstHost, Vector2.right));
        Assert.AreEqual(12, NowFocus.focusedId);
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
            NowFocus.EnterUGUINavigation(FirstHost, Vector2.left));
        Assert.AreEqual(12, NowFocus.focusedId);

        NowFocus.Clear();

        Assert.AreEqual(
            NowFocusMoveResult.Seeded,
            NowFocus.EnterUGUINavigation(FirstHost, Vector2.right));
        Assert.AreEqual(11, NowFocus.focusedId);
    }

    [Test]
    public void FocusRequestedBeforeFirstDrawAdoptsTheRegisteringHost()
    {
        const int resolvedControlId = 7001;
        NowFocus.Focus(resolvedControlId);

        Assert.AreEqual(resolvedControlId, NowFocus.focusedId);
        Assert.IsFalse(NowFocus.IsFocusedInHost(FirstHost));

        RegisterHost(
            FirstHost,
            (resolvedControlId,
                new NowRect(10f, 10f, 60f, 30f),
                NowFocusNavigationLock.None));

        Assert.AreEqual(resolvedControlId, NowFocus.focusedId);
        Assert.IsTrue(NowFocus.IsFocusedInHost(FirstHost));
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

        NowFocus.EnterUGUINavigation(FirstHost, Vector2.zero);
        NowFocus.UnregisterHost(SecondHost);

        Assert.AreEqual(11, NowFocus.focusedId);
        Assert.IsTrue(NowFocus.IsFocusedInHost(FirstHost));

        NowFocus.UnregisterHost(FirstHost);

        Assert.AreEqual(0, NowFocus.focusedId);
    }

    [Test]
    public void RemovedControlIsNotResolvedFromTheRecycledBuildBuffer()
    {
        RegisterHost(
            FirstHost,
            (11, new NowRect(10f, 10f, 60f, 30f), NowFocusNavigationLock.None));
        RegisterHost(FirstHost);

        NowFocus.Clear();
        NowFocus.Focus(11);

        Assert.AreEqual(11, NowFocus.focusedId);
        Assert.IsFalse(NowFocus.IsFocusedInHost(FirstHost));
    }

    static void RegisterHost(
        int hostId,
        params (int id, NowRect rect, NowFocusNavigationLock navigationLock)[] controls)
    {
        using (NowFocus.BeginHostRegistration(hostId, null))
        {
            for (int i = 0; i < controls.Length; ++i)
            {
                var control = controls[i];
                NowFocus.Register(
                    control.id,
                    control.rect,
                    default,
                    control.navigationLock);
            }
        }
    }
}
