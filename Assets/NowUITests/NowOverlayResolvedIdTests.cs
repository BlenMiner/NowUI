using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowOverlayResolvedIdTests
{
    sealed class InputProvider : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = default;
            return true;
        }
    }

    readonly InputProvider _input = new InputProvider();

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
    public void CanonicalDeferKeepsCallbackPayloadSeparateFromOverlayIdentity()
    {
        NowResolvedId sourceId = ControlId(101UL, 17);
        NowResolvedId expectedOverlayId = sourceId.InDomain(NowIdDomain.Overlay);
        const int payload = 9001;
        int observedPayload = 0;
        NowResolvedId observedOverlayId = default;
        NowResolvedId observedSourceId = default;

        using (NowInput.Begin(_input, new Vector2(640f, 360f)))
        {
            NowOverlay.DeferScreen(
                new NowRect(10f, 10f, 80f, 40f),
                sourceId,
                payload,
                state =>
                {
                    observedPayload = state;
                    observedOverlayId = NowOverlay.currentFocusLayerId;
                    observedSourceId = NowOverlay.currentFocusLayerSourceId;
                });

            NowOverlay.Flush();
        }

        Assert.That(observedPayload, Is.EqualTo(payload));
        Assert.That(observedOverlayId, Is.EqualTo(expectedOverlayId));
        Assert.That(observedSourceId, Is.EqualTo(sourceId));
        Assert.That(
            observedOverlayId,
            Is.Not.EqualTo(NowResolvedId.FromLegacy(payload).InDomain(NowIdDomain.Overlay)));
    }

    [Test]
    public void NamedOverlayApisRejectAnEmptyResolvedSource()
    {
        using (NowInput.Begin(_input, new Vector2(640f, 360f)))
        {
            Assert.Throws<System.ArgumentException>(() =>
                NowOverlay.DeferScreen(
                    new NowRect(10f, 10f, 80f, 40f),
                    NowResolvedId.None,
                    () => { }));
            Assert.Throws<System.ArgumentException>(() =>
                NowOverlay.BlockAllSurfaces(NowResolvedId.None));
        }
    }

    [Test]
    public void EqualLocalOverlayTreesRemainIsolatedByOwnerRoot()
    {
        NowResolvedId firstRoot = ControlId(202UL, 17);
        NowResolvedId firstChild = ControlId(202UL, 18);
        NowResolvedId secondRoot = ControlId(303UL, 17);
        NowResolvedId secondChild = ControlId(303UL, 18);
        var firstChildRect = new NowRect(100f, 10f, 60f, 40f);
        var secondChildRect = new NowRect(400f, 10f, 60f, 40f);
        Vector2 firstChildPoint = firstChildRect.center;
        Vector2 secondChildPoint = secondChildRect.center;

        using (NowInput.Begin(_input, new Vector2(640f, 360f)))
        {
            NowOverlay.DeferScreen(
                new NowRect(10f, 10f, 60f, 40f),
                firstRoot,
                () => NowOverlay.DeferScreen(firstChildRect, firstChild, () => { }));
            NowOverlay.DeferScreen(
                new NowRect(310f, 10f, 60f, 40f),
                secondRoot,
                () => NowOverlay.DeferScreen(secondChildRect, secondChild, () => { }));

            NowOverlay.Flush();

            Assert.That(NowOverlay.HasNestedOverlay(firstRoot), Is.True);
            Assert.That(NowOverlay.HasNestedOverlay(secondRoot), Is.True);
            Assert.That(
                NowOverlay.IsPointerInsideOverlayTree(firstRoot, firstChildPoint),
                Is.True);
            Assert.That(
                NowOverlay.IsPointerInsideOverlayTree(firstRoot, secondChildPoint),
                Is.False);
            Assert.That(
                NowOverlay.IsPointerInsideOverlayTree(secondRoot, secondChildPoint),
                Is.True);
            Assert.That(
                NowOverlay.IsPointerInsideOverlayTree(secondRoot, firstChildPoint),
                Is.False);
        }
    }

    [Test]
    public void ActiveOverlaySourceParticipatesInFocusOwnershipWithoutCrossingOwners()
    {
        NowResolvedId firstOwner = ControlId(404UL, 17);
        NowResolvedId firstPopup = ControlId(404UL, 18);
        NowResolvedId secondOwner = ControlId(505UL, 17);
        NowResolvedId secondPopup = ControlId(505UL, 18);

        using (NowInput.Begin(_input, new Vector2(640f, 360f)))
        {
            NowFocus.DeclareOwner(firstPopup, firstOwner);
            NowFocus.DeclareOwner(secondPopup, secondOwner);
            NowOverlay.DeferScreen(
                new NowRect(10f, 10f, 80f, 40f),
                firstPopup,
                () => { });

            Assert.That(NowOverlay.activeFocusLayerSourceId, Is.EqualTo(firstPopup));
            Assert.That(NowFocus.IsFocusedWithin(firstOwner), Is.True);
            Assert.That(NowFocus.IsFocusedWithin(secondOwner), Is.False);
        }
    }

    static NowResolvedId ControlId(ulong ownerNonce, int authoredId)
    {
        return NowResolvedId.CreateOwnerRoot(ownerNonce)
            .Derive(NowIdDomain.Control, authoredId);
    }
}
