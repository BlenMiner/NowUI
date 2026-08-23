using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowInteractionRegionTests
{
    static readonly Vector2 Surface = new Vector2(200f, 120f);
    static readonly NowRect Row = new NowRect(10f, 10f, 120f, 40f);
    static readonly NowRect Child = new NowRect(90f, 10f, 40f, 40f);

    sealed class InputProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    InputProvider _provider;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowOverlay.Reset();
        NowControls.Reset();
        _provider = new InputProvider();
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
        NowOverlay.Reset();
        NowControls.Reset();
    }

    [Test]
    public void RegionStoresFourExclusionsInlineAndRejectsOnlyTheirInteriors()
    {
        FieldInfo[] fields = typeof(NowInteractionRegion).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo field in fields)
        {
            Assert.IsTrue(
                field.FieldType.IsValueType,
                $"{field.Name} must remain inline; reference-backed exclusions allocate per region.");
        }

        var region = new NowInteractionRegion(new NowRect(0f, 0f, 100f, 40f))
            .Exclude(new NowRect(0f, 0f, 10f, 10f))
            .Exclude(new NowRect(20f, 0f, 10f, 10f))
            .Exclude(new NowRect(40f, 0f, 10f, 10f))
            .Exclude(new NowRect(60f, 0f, 10f, 10f));

        Assert.AreEqual(NowInteractionRegion.MaxExclusions, region.exclusionCount);
        Assert.IsFalse(region.Contains(new Vector2(5f, 5f)));
        Assert.IsFalse(region.Contains(new Vector2(25f, 5f)));
        Assert.IsFalse(region.Contains(new Vector2(45f, 5f)));
        Assert.IsFalse(region.Contains(new Vector2(65f, 5f)));
        Assert.IsTrue(region.Contains(new Vector2(80f, 20f)));
        Assert.IsFalse(region.Contains(new Vector2(120f, 20f)));

        Assert.Throws<InvalidOperationException>(() =>
            region.Exclude(new NowRect(80f, 0f, 10f, 10f)));
    }

    [Test]
    public void ExcludedChildReceivesPressWhenParentIsDeclaredFirst()
    {
        var childPoint = Child.center;
        _provider.snapshot = new NowInputSnapshot(childPoint, true, true, false);
        var region = new NowInteractionRegion(Row).Exclude(Child);

        using (NowInput.Begin(_provider, Surface))
        {
            NowResolvedId parentId = NowControls.GetControlId("row");
            NowResolvedId childId = NowControls.GetControlId("row-action");
            var parent = NowInput.Interact(parentId, in region);
            var child = NowInput.Interact(childId, Child);

            Assert.IsFalse(parent.hovered);
            Assert.IsFalse(parent.pressed);
            Assert.IsTrue(child.hovered);
            Assert.IsTrue(child.pressed);
            Assert.AreEqual(childId, NowInput.activeId);
        }
    }

    [Test]
    public void ParentPressReleasedOverExclusionDoesNotClick()
    {
        var parentPoint = new Vector2(30f, 30f);
        var childPoint = Child.center;
        var region = new NowInteractionRegion(Row).Exclude(Child);
        NowResolvedId parentId;

        _provider.snapshot = new NowInputSnapshot(parentPoint, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        {
            parentId = NowControls.GetControlId("row");
            Assert.IsTrue(NowInput.Interact(parentId, in region).pressed);
        }

        _provider.snapshot = new NowInputSnapshot(childPoint, false, false, true);

        using (NowInput.Begin(_provider, Surface))
        {
            Assert.AreEqual(parentId, NowControls.GetControlId("row"));
            var release = NowInput.Interact(parentId, in region);

            Assert.IsTrue(release.released);
            Assert.IsFalse(release.hovered);
            Assert.IsFalse(release.clicked);
        }

        Assert.AreEqual(NowResolvedId.None, NowInput.activeId);
    }

    [Test]
    public void CompositeContextActionDoesNotStealSecondaryPressesFromChildren()
    {
        var region = new NowInteractionRegion(Row).Exclude(Child);
        NowPointerButtons secondary = NowInputSnapshot.ToButtonMask(
            true, NowPointerButton.Secondary);
        _provider.snapshot = new NowInputSnapshot(
            Child.center,
            secondary,
            secondary,
            NowPointerButtons.None);

        using (NowInput.Begin(_provider, Surface))
        {
            Assert.IsFalse(NowInput.WasRightClicked(in region));
            Assert.IsFalse(
                NowContextAction.Resolve(in region, false, Child).triggered);

            NowContextTrigger action = NowContextAction.Resolve(
                in region, true, Child);
            Assert.IsTrue(action.triggered);
            Assert.AreEqual(NowContextTriggerSource.Action, action.source);
        }

        var parentPoint = new Vector2(30f, 30f);
        _provider.snapshot = new NowInputSnapshot(
            parentPoint,
            secondary,
            secondary,
            NowPointerButtons.None);

        using (NowInput.Begin(_provider, Surface))
        {
            NowContextTrigger pointer = NowContextAction.Resolve(
                in region, false, Child);
            Assert.IsTrue(pointer.triggered);
            Assert.AreEqual(NowContextTriggerSource.SecondaryPointer, pointer.source);
            Assert.AreEqual(parentPoint, pointer.screenPointerPosition);
        }
    }

    [Test]
    public void TransformedRegionTestsBoundsAndExclusionsInLocalSpace()
    {
        var region = new NowInteractionRegion(Row).Exclude(Child);
        var transformedChildPoint = new Vector2(
            Child.center.x * 2f + 10f,
            Child.center.y * 2f + 5f);
        _provider.snapshot = new NowInputSnapshot(transformedChildPoint, false, false, false);

        using (NowInput.Begin(_provider, Surface * 2f))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        {
            NowResolvedId id = NowControls.GetControlId("row");
            Assert.IsFalse(NowInput.Interact(id, in region).hovered);
        }

        var localParentPoint = new Vector2(30f, 30f);
        var transformedParentPoint = new Vector2(
            localParentPoint.x * 2f + 10f,
            localParentPoint.y * 2f + 5f);
        _provider.snapshot = new NowInputSnapshot(transformedParentPoint, false, false, false);

        using (NowInput.Begin(_provider, Surface * 2f))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        {
            NowResolvedId id = NowControls.GetControlId("row");
            var interaction = NowInput.Interact(id, in region);

            Assert.IsTrue(interaction.hovered);
            Assert.AreEqual(localParentPoint, interaction.pointerPosition);
            Assert.AreEqual((Rect)Row, interaction.rect);
        }
    }
}
