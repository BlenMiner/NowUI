using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowFocusResolvedIdTests
{
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
    public void ResolvedFocusWithTheSameAuthoredPathIsIsolatedByOwner()
    {
        NowResolvedId first = ControlId(101UL, 17);
        NowResolvedId second = ControlId(202UL, 17);

        NowFocus.Focus(first);

        Assert.That(NowFocus.focusedResolvedId, Is.EqualTo(first));
        Assert.That(NowFocus.IsFocused(first), Is.True);
        Assert.That(NowFocus.IsFocused(second), Is.False);

        NowFocus.Focus(second);

        Assert.That(NowFocus.focusedResolvedId, Is.EqualTo(second));
        Assert.That(NowFocus.IsFocused(first), Is.False);
        Assert.That(NowFocus.IsFocused(second), Is.True);
    }

    [Test]
    public void ResolvedOwnerChainsDoNotCrossOwnerRoots()
    {
        NowResolvedId firstParent = ControlId(303UL, 17);
        NowResolvedId firstChild = ControlId(303UL, 18);
        NowResolvedId secondParent = ControlId(404UL, 17);
        NowResolvedId secondChild = ControlId(404UL, 18);

        NowFocus.DeclareOwner(firstChild, firstParent);
        NowFocus.DeclareOwner(secondChild, secondParent);
        NowFocus.Focus(firstChild);

        Assert.That(NowFocus.IsFocusedWithin(firstParent), Is.True);
        Assert.That(NowFocus.IsFocusedWithin(secondParent), Is.False);
    }

    [Test]
    public void ResolvedHostRegistriesKeepEqualLocalControlPathsIsolated()
    {
        NowResolvedId firstOwner = NowResolvedId.CreateOwnerRoot(505UL);
        NowResolvedId secondOwner = NowResolvedId.CreateOwnerRoot(606UL);
        NowResolvedId firstHost = firstOwner.InDomain(NowIdDomain.FocusHost);
        NowResolvedId secondHost = secondOwner.InDomain(NowIdDomain.FocusHost);
        NowResolvedId firstControl = firstOwner.Derive(NowIdDomain.Control, 17);
        NowResolvedId secondControl = secondOwner.Derive(NowIdDomain.Control, 17);

        RegisterHost(firstHost, (firstControl, new NowRect(10f, 10f, 60f, 30f)));
        RegisterHost(secondHost, (secondControl, new NowRect(10f, 10f, 60f, 30f)));

        Assert.That(
            NowFocus.EnterUGUINavigation(firstHost, Vector2.zero),
            Is.EqualTo(NowFocusMoveResult.Seeded));
        Assert.That(NowFocus.focusedResolvedId, Is.EqualTo(firstControl));
        Assert.That(NowFocus.IsFocusedInHost(firstHost), Is.True);
        Assert.That(NowFocus.IsFocusedInHost(secondHost), Is.False);
        Assert.That(NowFocus.IsFocused(secondControl), Is.False);
    }

    [Test]
    public void ResolvedNavigationLinkTargetsOnlyItsOwnerGraph()
    {
        NowResolvedId owner = NowResolvedId.CreateOwnerRoot(707UL);
        NowResolvedId otherOwner = NowResolvedId.CreateOwnerRoot(808UL);
        NowResolvedId host = owner.InDomain(NowIdDomain.FocusHost);
        NowResolvedId first = owner.Derive(NowIdDomain.Control, 17);
        NowResolvedId target = owner.Derive(NowIdDomain.Control, 18);
        NowResolvedId sameLocalPathOtherOwner =
            otherOwner.Derive(NowIdDomain.Control, 18);

        using (NowFocus.BeginHostRegistration(host, null))
        {
            NowFocus.Register(
                first,
                new NowRect(10f, 10f, 60f, 30f),
                NowFocusNavigation.Right(target));
            NowFocus.Register(target, new NowRect(10f, 90f, 60f, 30f));
            NowFocus.Register(
                sameLocalPathOtherOwner,
                new NowRect(90f, 10f, 60f, 30f));
        }

        NowFocus.Focus(first);

        Assert.That(
            NowFocus.RouteUGUINavigation(host, Vector2.right),
            Is.EqualTo(NowFocusMoveResult.Moved));
        Assert.That(NowFocus.focusedResolvedId, Is.EqualTo(target));
        Assert.That(NowFocus.IsFocused(sameLocalPathOtherOwner), Is.False);
    }

    static NowResolvedId ControlId(ulong ownerNonce, int authoredId)
    {
        return NowResolvedId.CreateOwnerRoot(ownerNonce)
            .Derive(NowIdDomain.Control, authoredId);
    }

    static void RegisterHost(
        NowResolvedId hostId,
        params (NowResolvedId id, NowRect rect)[] controls)
    {
        using (NowFocus.BeginHostRegistration(hostId, null))
        {
            for (int i = 0; i < controls.Length; ++i)
                NowFocus.Register(controls[i].id, controls[i].rect);
        }
    }
}
