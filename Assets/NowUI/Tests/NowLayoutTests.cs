using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;

// Most tests intentionally exercise the internal explicit End* primitives so
// mismatches and cache finalization can be asserted directly. Public callers
// only get the [NowScope] using-based API.
#pragma warning disable NOWUI002

public class NowLayoutTests
{
    [SetUp]
    public void SetUp()
    {
        NowControls.Reset();
        NowLayout.Reset();

        // The engine falls back to a bundled default font, so pin a font-less
        // label style: these tests rely on labels measuring zero.
        NowLayout.labelStyle = new NowText(default, null).SetFontSize(16);
    }

    [TearDown]
    public void TearDown()
    {
        NowLayout.Reset();
        NowControls.Reset();
    }

    static void AssertRect(Vector4 expected, Vector4 actual, float tolerance = 0.001f)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance, "x");
        Assert.AreEqual(expected.y, actual.y, tolerance, "y");
        Assert.AreEqual(expected.z, actual.z, tolerance, "width");
        Assert.AreEqual(expected.w, actual.w, tolerance, "height");
    }

    static void DrawConditionalSiblingGroup()
    {
        using (NowLayout.Row().Begin())
            NowLayout.ReserveRect(10f, 80f);
    }

    static NowRect DrawStableGroup()
    {
        NowRect rect;

        using (var group = NowLayout.Row().Begin())
        {
            rect = group.rect;
            NowLayout.ReserveRect(10f, 30f);
        }

        return rect;
    }

    static void DrawWeightedGrowRow(float secondWeight, out NowRect first, out NowRect second)
    {
        using (NowLayout.Row(new NowRect(0f, 0f, 400f, 20f))
            .SetId("descriptor-only-flex-cache")
            .Begin())
        {
            using (var child = NowLayout.Column().Grow().Height(20f).Begin())
                first = child.rect;

            using (var child = NowLayout.Column().Grow(secondWeight).Height(20f).Begin())
                second = child.rect;
        }
    }

    struct FrameProbeContent : INowFrameContent
    {
        public int measureCalls;
        public int drawCalls;
        public bool firstWasMeasure;
        public bool lastWasMeasure;

        public void Draw(NowRect rect)
        {
            if (measureCalls == 0 && drawCalls == 0)
                firstWasMeasure = NowLayout.isMeasurePass;

            if (NowLayout.isMeasurePass)
                ++measureCalls;
            else
                ++drawCalls;

            lastWasMeasure = NowLayout.isMeasurePass;

            NowLayout.Area(rect);
            NowLayout.ReserveRect(40f, 20f);
            NowLayout.EndArea();
        }
    }

    struct ReentrantFrameProbeContent : INowFrameContent
    {
        public void Draw(NowRect rect)
        {
            var nested = new FrameProbeContent();
            NowFrame.DrawContent(
                ref nested,
                rect,
                measurePass: true,
                trackContent: false,
                flushOverlays: false);
        }
    }

    struct ReentrantMeasureProbeContent : INowFrameContent
    {
        public void Draw(NowRect rect)
        {
            var nested = new FrameProbeContent();
            NowFrame.MeasureContent(ref nested, rect);
        }
    }

    [Test]
    public void VerticalAreaStacksRectsDownward()
    {
        NowLayout.Area(new Vector4(10, 20, 300, 400));

        Vector4 first = NowLayout.ReserveRect(100, 30);
        Vector4 second = NowLayout.ReserveRect(50, 40);

        NowLayout.EndArea();

        AssertRect(new Vector4(10, 20, 100, 30), first);
        AssertRect(new Vector4(10, 50, 50, 40), second);
    }

    [Test]
    public void ContentTrackingReportsRootAreaExtent()
    {
        NowLayout.BeginContentTracking();

        NowLayout.Area(new Vector4(10, 10, 400, 300));
        NowLayout.ReserveRect(100, 30);
        NowLayout.ReserveRect(50, 40);
        NowLayout.EndArea();

        Vector2 size = NowLayout.EndContentTracking();

        Assert.AreEqual(110f, size.x, 0.01f, "extent = area origin + widest content");
        Assert.AreEqual(80f, size.y, 0.01f, "extent = area origin + stacked content height");
    }

    [Test]
    public void FrameDrawContentRunsMeasureThenTrackedDrawPass()
    {
        var content = new FrameProbeContent();

        Vector2 measured = NowFrame.DrawContent(
            ref content,
            new NowRect(10f, 10f, 100f, 100f),
            measurePass: true,
            trackContent: true,
            flushOverlays: false);

        Assert.AreEqual(1, content.measureCalls);
        Assert.AreEqual(1, content.drawCalls);
        Assert.IsTrue(content.firstWasMeasure);
        Assert.IsFalse(content.lastWasMeasure);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.AreEqual(50f, measured.x, 0.001f);
        Assert.AreEqual(30f, measured.y, 0.001f);
    }

    [Test]
    public void ExactFrameHostsRejectRecursiveMeasureCyclesWithoutLeakingState()
    {
        var content = new ReentrantFrameProbeContent();

        var error = Assert.Throws<InvalidOperationException>(() =>
            NowFrame.DrawContent(
                ref content,
                new NowRect(0f, 0f, 100f, 100f),
                measurePass: true,
                trackContent: false,
                flushOverlays: false));

        StringAssert.Contains("cannot rebuild recursively", error.Message);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.IsFalse(NowInput.isPassive);
        Assert.DoesNotThrow(() =>
        {
            var next = new FrameProbeContent();
            NowFrame.DrawContent(
                ref next,
                new NowRect(0f, 0f, 100f, 100f),
                measurePass: true,
                trackContent: false,
                flushOverlays: false);
        });
    }

    [Test]
    public void MeasureOnlyHostsRejectRecursiveExactRebuildsWithoutLeakingState()
    {
        var content = new ReentrantFrameProbeContent();

        var error = Assert.Throws<InvalidOperationException>(() =>
            NowFrame.MeasureContent(
                ref content,
                new NowRect(0f, 0f, 100f, 100f)));

        StringAssert.Contains("cannot rebuild recursively", error.Message);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.IsFalse(NowInput.isPassive);
        Assert.DoesNotThrow(() =>
        {
            var next = new FrameProbeContent();
            NowFrame.MeasureContent(
                ref next,
                new NowRect(0f, 0f, 100f, 100f));
        });
    }

    [Test]
    public void ExactHostsRejectRecursiveMeasureOnlyRebuildsWithoutLeakingState()
    {
        var content = new ReentrantMeasureProbeContent();

        var error = Assert.Throws<InvalidOperationException>(() =>
            NowFrame.DrawContent(
                ref content,
                new NowRect(0f, 0f, 100f, 100f),
                measurePass: true,
                trackContent: false,
                flushOverlays: false));

        StringAssert.Contains("cannot rebuild recursively", error.Message);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.IsFalse(NowInput.isPassive);
    }

    [Test]
    public void OnePassTrackedHostsRejectRecursiveMeasureOnlyRebuildsWithoutLosingTheirExtent()
    {
        var content = new ReentrantMeasureProbeContent();

        var error = Assert.Throws<InvalidOperationException>(() =>
            NowFrame.DrawContent(
                ref content,
                new NowRect(0f, 0f, 100f, 100f),
                measurePass: false,
                trackContent: true,
                flushOverlays: false));

        StringAssert.Contains("cannot rebuild recursively", error.Message);
        Assert.IsFalse(NowLayout.isTrackingContent);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.IsFalse(NowInput.isPassive);

        var next = new FrameProbeContent();
        Vector2 measured = NowFrame.DrawContent(
            ref next,
            new NowRect(0f, 0f, 100f, 100f),
            measurePass: false,
            trackContent: true,
            flushOverlays: false);

        Assert.AreEqual(new Vector2(40f, 20f), measured);
    }

    [Test]
    public void FrameScopeRestoresScaleAndReportsTrackedRepaint()
    {
        float previousScale = Now.uiScale;
        var scope = NowFrame.Begin(2f, trackRepaint: true);

        try
        {
            Assert.AreEqual(2f, Now.uiScale, 0.001f);

            NowControlState.RequestRepaint();

            Assert.IsTrue(scope.EndRepaintTracking());
        }
        finally
        {
            scope.Dispose();
            NowControlState.Reset();
            Now.SetUIScale(previousScale);
        }

        Assert.AreEqual(previousScale, Now.uiScale, 0.001f);
    }

    [Test]
    public void SpacingAndPaddingOffsetChildren()
    {
        NowLayout.Area(new Vector4(0, 0, 300, 400), spacing: 5, padding: 10);

        Vector4 first = NowLayout.ReserveRect(100, 20);
        Vector4 second = NowLayout.ReserveRect(100, 20);

        NowLayout.EndArea();

        AssertRect(new Vector4(10, 10, 100, 20), first);
        AssertRect(new Vector4(10, 35, 100, 20), second);
    }

    [Test]
    public void VectorPaddingOverloadsOffsetChildren()
    {
        NowLayout.Area(new Vector4(0, 0, 300, 400), new Vector4(4, 6, 8, 10), spacing: 5);

        Vector4 areaFirst = NowLayout.ReserveRect(100, 20);
        Vector4 areaSecond = NowLayout.ReserveRect(100, 20);

        NowLayout.EndArea();

        AssertRect(new Vector4(4, 6, 100, 20), areaFirst);
        AssertRect(new Vector4(4, 31, 100, 20), areaSecond);

        NowLayout.Area(new Vector4(0, 0, 300, 400));
        NowLayout.Horizontal(new Vector4(3, 4, 5, 6), spacing: 7);

        Vector4 rowFirst = NowLayout.ReserveRect(10, 8);
        Vector4 rowSecond = NowLayout.ReserveRect(20, 8);

        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        AssertRect(new Vector4(3, 4, 10, 8), rowFirst);
        AssertRect(new Vector4(20, 4, 20, 8), rowSecond);
    }

    [Test]
    public void HorizontalGroupPlacesRectsAlongX()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));
        NowLayout.Horizontal(spacing: 8);

        Vector4 first = NowLayout.ReserveRect(60, 30);
        Vector4 second = NowLayout.ReserveRect(40, 30);

        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        AssertRect(new Vector4(0, 0, 60, 30), first);
        AssertRect(new Vector4(68, 0, 40, 30), second);
    }

    [Test]
    public void FluentColumnAndRowApplyConstraintsAndAlignment()
    {
        NowRect first = default;
        NowRect second = default;
        NowRect below = default;

        using (NowLayout.Column(new NowRect(0f, 0f, 300f, 200f))
            .Padding(10f)
            .Gap(5f)
            .Begin())
        {
            using (NowLayout.Row()
                .FillWidth(160f)
                .Height(30f)
                .AlignSelf(NowLayoutAlign.Center)
                .AlignChildren(NowLayoutAlign.Center)
                .Gap(8f)
                .Begin())
            {
                first = NowLayout.ReserveRect(20f, 10f);
                second = NowLayout.ReserveRect(30f, 10f);
            }

            below = NowLayout.ReserveRect(40f, 10f);
        }

        AssertRect(new Vector4(70f, 20f, 20f, 10f), first);
        AssertRect(new Vector4(98f, 20f, 30f, 10f), second);
        AssertRect(new Vector4(10f, 45f, 40f, 10f), below);
    }

    [Test]
    public void RootRowLaysOutChildrenHorizontally()
    {
        NowRect first;
        NowRect second;

        using (NowLayout.Row(new NowRect(10f, 20f, 200f, 80f))
            .Padding(5f, 6f)
            .Gap(7f)
            .Begin())
        {
            first = NowLayout.ReserveRect(20f, 10f);
            second = NowLayout.ReserveRect(30f, 10f);
        }

        AssertRect(new Vector4(15f, 26f, 20f, 10f), first);
        AssertRect(new Vector4(42f, 26f, 30f, 10f), second);
    }

    [Test]
    public void GroupFillsParentCrossAxisByDefault()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var row = NowLayout.Horizontal();
        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.AreEqual(400f, row.width, 0.001f);
    }

    [Test]
    public void AlignChildrenFitsAndPositionsAnAutoSizedChildGroup()
    {
        NowRect rowRect = default;
        var options = default(NowLayoutOptions).SetAlignItems(NowLayoutAlign.Center);

        NowLayout.RunMeasured("aligned-auto-group", new NowRect(0f, 0f, 300f, 100f), options, () =>
        {
            using (var row = NowLayout.Row().Height(20f).Begin())
            {
                rowRect = row.rect;
                NowLayout.ReserveRect(40f, 20f);
            }
        });

        AssertRect(new NowRect(130f, 0f, 40f, 20f), rowRect);
    }

    [Test]
    public void AutoSizedGroupCorrectsParentCursorOnFirstFrame()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        NowLayout.Horizontal();
        NowLayout.ReserveRect(100, 30);
        NowLayout.EndHorizontal();

        Vector4 below = NowLayout.ReserveRect(50, 10);

        NowLayout.EndArea();

        Assert.AreEqual(30f, below.y, 0.001f, "sibling after auto group should stack below its content on frame 1");
    }

    [Test]
    public void AutoSizedGroupUsesCachedContentSizeOnSecondPass()
    {
        for (int pass = 0; pass < 2; ++pass)
        {
            NowControls.ResetControlIdOccurrences();
            NowLayout.Area("area", new Vector4(0, 0, 400, 300));

            var row = NowLayout.Horizontal();
            NowLayout.ReserveRect(100, 30);
            NowLayout.EndHorizontal();

            NowLayout.EndArea();

            if (pass == 1)
                AssertRect(new Vector4(0, 0, 400, 30), row.rect);
        }
    }

    [Test]
    public void RetainedFrameScopesRejectRecursiveRebuildsWithoutLeakingOuterState()
    {
        float previousScale = Now.uiScale;
        var outer = NowFrame.Begin(2f, trackRepaint: true);

        try
        {
            NowControlState.RequestRepaint();

            var error = Assert.Throws<InvalidOperationException>(() => NowFrame.Begin(3f, trackRepaint: true));

            StringAssert.Contains("cannot rebuild recursively", error.Message);
            Assert.AreEqual(2f, Now.uiScale, 0.001f);
            Assert.IsTrue(outer.EndRepaintTracking(), "the failed nested frame must not clear the outer repaint request");
        }
        finally
        {
            outer.Dispose();
            Now.SetUIScale(previousScale);
        }

        Assert.DoesNotThrow(() =>
        {
            using var next = NowFrame.Begin(1f);
        });
    }

    [Test]
    public void GroupCallSiteKeepsItsMeasurementWhenConditionalSiblingDisappears()
    {
        using (NowLayout.Area("call-site-area", new NowRect(0f, 0f, 300f, 300f)))
        {
            DrawConditionalSiblingGroup();
            DrawStableGroup();
        }

        NowControls.ResetControlIdOccurrences();

        NowRect stable;

        using (NowLayout.Area("call-site-area", new NowRect(0f, 0f, 300f, 300f)))
            stable = DrawStableGroup();

        Assert.AreEqual(30f, stable.height, 0.001f,
            "the stable group's own call-site cache must survive a preceding conditional group disappearing");
    }

    [Test]
    public void CrossStretchedGroupMeasuresContentNotAllocation()
    {
        NowLayout.Area("measure-area", new Vector4(0, 0, 60, 60));
        NowLayout.Horizontal();
        NowLayout.ReserveRect(128, 128);
        NowLayout.ReserveRect(40, 16);
        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize("measure-area", out var size));
        Assert.AreEqual(168f, size.x, 0.01f, "area must measure the row's content width, not the stretched allocation");
        Assert.AreEqual(128f, size.y, 0.01f);
    }

    [Test]
    public void CachedAreaLookupDoesNotClaimParentScopedNestedGroupIds()
    {
        using (NowLayout.Area("root-area-cache", new NowRect(0f, 0f, 100f, 100f)))
        {
            using (NowLayout.Vertical("nested-group-cache"))
                NowLayout.ReserveRect(40f, 20f);
        }

        Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize("root-area-cache", out var areaSize));
        Assert.AreEqual(new Vector2(40f, 20f), areaSize);
        Assert.IsFalse(NowLayout.TryGetCachedAreaContentSize("nested-group-cache", out _),
            "nested group caches are parent-scoped and are not global area ids");
    }

    [Test]
    public void IntegerAreaIdsRemainIsolatedAcrossRetainedHostScopes()
    {
        const int areaId = 42;
        int firstHost = NowControls.AllocateHostScopeId();
        int secondHost = NowControls.AllocateHostScopeId();

        using (NowControls.RestoreIdScope(firstHost))
        {
            using (NowLayout.Area(areaId, new NowRect(0f, 0f, 200f, 100f)))
                NowLayout.ReserveRect(40f, 20f);

            Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize(areaId, out var firstSize));
            Assert.AreEqual(new Vector2(40f, 20f), firstSize);
        }

        using (NowControls.RestoreIdScope(secondHost))
        {
            Assert.IsFalse(NowLayout.TryGetCachedAreaContentSize(areaId, out _),
                "a second retained host must not observe the first host's area cache");

            using (NowLayout.Area(areaId, new NowRect(0f, 0f, 200f, 100f)))
                NowLayout.ReserveRect(90f, 35f);

            Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize(areaId, out var secondSize));
            Assert.AreEqual(new Vector2(90f, 35f), secondSize);
        }

        using (NowControls.RestoreIdScope(firstHost))
        {
            Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize(areaId, out var firstSize));
            Assert.AreEqual(new Vector2(40f, 20f), firstSize,
                "drawing another host must not overwrite this host's cached measurement");
        }

        int resolvedFirstArea = NowControls.ResolveHostControlId(firstHost, areaId);
        Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize(
            NowId.Resolved(resolvedFirstArea), out var resolvedSize));
        Assert.AreEqual(new Vector2(40f, 20f), resolvedSize,
            "NowId.Resolved must remain the explicit escape hatch for an already-composed key");
    }

    [Test]
    public void AlignItemsAlignsChildrenWithPerChildOverride()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));
        NowLayout.Horizontal(height: 100, alignItems: NowLayoutAlign.Center);

        Vector4 inherited = NowLayout.ReserveRect(50, 20);
        Vector4 overridden = NowLayout.ReserveRect(50, 20, align: NowLayoutAlign.End);

        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.AreEqual(40f, inherited.y, 0.001f, "child without explicit align inherits the group's align-items");
        Assert.AreEqual(80f, overridden.y, 0.001f, "per-child SetAlign overrides the group's align-items");
    }

    [Test]
    public void ContentRectReservesLastHeightAndConvergesViaEnd()
    {
        NowControls.Reset();
        NowControlState.Reset();

        NowContentRect Next() => NowLayout.ContentRect();

        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var first = Next();
        Assert.AreEqual(1f, first.rect.height, 0.001f, "unknown height reserves a minimal rect");
        Assert.AreEqual(400f, first.rect.width, 0.001f, "content rects stretch to the available width");

        NowControlState.BeginRepaintTracking();
        first.End(72f);
        Assert.IsTrue(NowControlState.EndRepaintTracking(), "height change must request a repaint");
        NowLayout.EndArea();

        NowControls.Reset();

        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var second = Next();
        Assert.AreEqual(72f, second.rect.height, 0.001f, "the site-keyed height survives to the next frame");

        NowControlState.BeginRepaintTracking();
        second.End(72f);
        Assert.IsFalse(NowControlState.EndRepaintTracking(), "settled height must not keep repainting");
        NowLayout.EndArea();

        NowControls.Reset();
        NowControlState.Reset();
    }

    [Test]
    public void CrossStretchedGroupShrinksWhenContentShrinks()
    {
        for (int pass = 0; pass < 2; ++pass)
        {
            NowControls.ResetControlIdOccurrences();
            NowLayout.Area("shrink-area", new Vector4(0, 0, 300, 300));
            NowLayout.Horizontal();
            NowLayout.ReserveRect(200, 20);
            NowLayout.EndHorizontal();
            NowLayout.EndArea();
        }

        NowControls.ResetControlIdOccurrences();
        NowLayout.Area("shrink-area", new Vector4(0, 0, 300, 300));
        NowLayout.Horizontal();
        NowLayout.ReserveRect(50, 20);
        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize("shrink-area", out var size));
        Assert.AreEqual(50f, size.x, 0.01f, "measured width must follow content back down");
    }

    [Test]
    public void FlexibleSpacePushesTrailingContentToTheEnd()
    {
        Vector4 trailing = default;

        for (int pass = 0; pass < 2; ++pass)
        {
            NowLayout.Area("area", new Vector4(0, 0, 200, 400));

            NowLayout.ReserveRect(100, 20);
            NowLayout.FlexibleSpace();
            trailing = NowLayout.ReserveRect(100, 30);

            NowLayout.EndArea();
        }

        Assert.AreEqual(370f, trailing.y, 0.001f, "trailing rect should sit at the bottom after one warm-up pass");
    }

    [Test]
    public void StretchSharesRemainingMainAxisSpaceByWeight()
    {
        Vector4 b = default;
        Vector4 c = default;

        for (int pass = 0; pass < 2; ++pass)
        {
            NowControls.ResetControlIdOccurrences();
            NowLayout.Area("area", new Vector4(0, 0, 400, 300));
            NowLayout.Horizontal();

            NowLayout.ReserveRect(100, 20);
            b = NowLayout.ReserveRect(NowLayout.StretchWidth(1).SetHeight(20));
            c = NowLayout.ReserveRect(NowLayout.StretchWidth(3).SetHeight(20));

            NowLayout.EndHorizontal();
            NowLayout.EndArea();
        }

        AssertRect(new Vector4(100, 0, 75, 20), b);
        AssertRect(new Vector4(175, 0, 225, 20), c);
    }

    [Test]
    public void OnePassFlexCacheRequestsRepaintWhenOnlyWeightsChange()
    {
        DrawWeightedGrowRow(1f, out _, out _);
        DrawWeightedGrowRow(1f, out NowRect first, out NowRect second);
        AssertRect(new NowRect(0f, 0f, 200f, 20f), first);
        AssertRect(new NowRect(200f, 0f, 200f, 20f), second);

        NowControlState.BeginRepaintTracking();
        DrawWeightedGrowRow(3f, out first, out second);
        AssertRect(new NowRect(0f, 0f, 200f, 20f), first);
        Assert.IsTrue(NowControlState.EndRepaintTracking(),
            "a descriptor-only cache change must schedule the converging rebuild");

        NowControlState.BeginRepaintTracking();
        DrawWeightedGrowRow(3f, out first, out second);
        AssertRect(new NowRect(0f, 0f, 100f, 20f), first);
        AssertRect(new NowRect(100f, 0f, 300f, 20f), second);
        Assert.IsFalse(NowControlState.EndRepaintTracking(),
            "the converged descriptor cache must not repaint forever");
    }

    [Test]
    public void FluentGrowSharesRemainingSpaceAlongTheParentDirection()
    {
        NowRect one = default;
        NowRect three = default;

        NowLayout.RunMeasured("grow-area", new NowRect(0f, 0f, 400f, 100f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                NowLayout.ReserveRect(100f, 20f);

                using (var growing = NowLayout.Column().Grow().Height(20f).Begin())
                    one = growing.rect;

                using (var growing = NowLayout.Column().Grow(3f).Height(20f).Begin())
                    three = growing.rect;
            }
        });

        AssertRect(new Vector4(100f, 0f, 75f, 20f), one);
        AssertRect(new Vector4(175f, 0f, 225f, 20f), three);
    }

    [Test]
    public void GrowMinRedistributesTheRemainingSpace()
    {
        NowRect constrained = default;
        NowRect sibling = default;

        NowLayout.RunMeasured("grow-min", new NowRect(0f, 0f, 400f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                using (var child = NowLayout.Column().Grow().MinWidth(300f).Height(20f).Begin())
                    constrained = child.rect;

                using (var child = NowLayout.Column().Grow().Height(20f).Begin())
                    sibling = child.rect;
            }
        });

        AssertRect(new NowRect(0f, 0f, 300f, 20f), constrained);
        AssertRect(new NowRect(300f, 0f, 100f, 20f), sibling);
    }

    [Test]
    public void GrowMaxRedistributesTheRemainingSpace()
    {
        NowRect constrained = default;
        NowRect sibling = default;

        NowLayout.RunMeasured("grow-max", new NowRect(0f, 0f, 400f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                using (var child = NowLayout.Column().Grow().MaxWidth(50f).Height(20f).Begin())
                    constrained = child.rect;

                using (var child = NowLayout.Column().Grow().Height(20f).Begin())
                    sibling = child.rect;
            }
        });

        AssertRect(new NowRect(0f, 0f, 50f, 20f), constrained);
        AssertRect(new NowRect(50f, 0f, 350f, 20f), sibling);
    }

    [Test]
    public void GrowConstraintsPreserveTheRemainingWeights()
    {
        NowRect constrained = default;
        NowRect one = default;
        NowRect two = default;

        NowLayout.RunMeasured("grow-weighted-clamp", new NowRect(0f, 0f, 500f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                using (var child = NowLayout.Column().Grow().MaxWidth(50f).Height(20f).Begin())
                    constrained = child.rect;

                using (var child = NowLayout.Column().Grow().Height(20f).Begin())
                    one = child.rect;

                using (var child = NowLayout.Column().Grow(2f).Height(20f).Begin())
                    two = child.rect;
            }
        });

        AssertRect(new NowRect(0f, 0f, 50f, 20f), constrained);
        AssertRect(new NowRect(50f, 0f, 150f, 20f), one);
        AssertRect(new NowRect(200f, 0f, 300f, 20f), two);
    }

    [Test]
    public void GrowMixedMinAndMaxConstraintsFreezeOnlyTheRequiredBound()
    {
        NowRect max = default;
        NowRect min = default;
        NowRect free = default;

        NowLayout.RunMeasured("grow-mixed-bounds", new NowRect(0f, 0f, 100f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                using (var child = NowLayout.Column().Grow().MaxWidth(10f).Height(20f).Begin())
                    max = child.rect;

                using (var child = NowLayout.Column().Grow().MinWidth(40f).Height(20f).Begin())
                    min = child.rect;

                using (var child = NowLayout.Column().Grow().Height(20f).Begin())
                    free = child.rect;
            }
        });

        AssertRect(new NowRect(0f, 0f, 10f, 20f), max);
        AssertRect(new NowRect(10f, 0f, 45f, 20f), min);
        AssertRect(new NowRect(55f, 0f, 45f, 20f), free);
    }

    [Test]
    public void GrowMixedBoundsUseAFeasibleAllocationInsteadOfOverflowing()
    {
        NowRect max = default;
        NowRect min = default;

        NowLayout.RunMeasured("grow-feasible-bounds", new NowRect(0f, 0f, 100f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                using (var child = NowLayout.Column().Grow().MaxWidth(40f).Height(20f).Begin())
                    max = child.rect;

                using (var child = NowLayout.Column().Grow().MinWidth(70f).Height(20f).Begin())
                    min = child.rect;
            }
        });

        AssertRect(new NowRect(0f, 0f, 30f, 20f), max);
        AssertRect(new NowRect(30f, 0f, 70f, 20f), min);
    }

    [Test]
    public void JustifyPositionsSpaceLeftByMaxCappedGrowItems()
    {
        NowRect first = default;
        NowRect second = default;

        NowLayout.RunMeasured("grow-capped-justify", new NowRect(0f, 0f, 400f, 20f), () =>
        {
            using (NowLayout.Row()
                .FillWidth()
                .Height(20f)
                .Justify(NowLayoutJustify.Center)
                .Begin())
            {
                using (var child = NowLayout.Column().Grow().MaxWidth(50f).Height(20f).Begin())
                    first = child.rect;

                using (var child = NowLayout.Column().Grow().MaxWidth(50f).Height(20f).Begin())
                    second = child.rect;
            }
        });

        AssertRect(new NowRect(150f, 0f, 50f, 20f), first);
        AssertRect(new NowRect(200f, 0f, 50f, 20f), second);
    }

    [Test]
    public void SpacerKeepsItsPlaceAmongConstrainedGrowItems()
    {
        NowRect first = default;
        NowRect last = default;

        NowLayout.RunMeasured("grow-spacer-index", new NowRect(0f, 0f, 400f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                using (var child = NowLayout.Column().Grow().MaxWidth(50f).Height(20f).Begin())
                    first = child.rect;

                NowLayout.Spacer();

                using (var child = NowLayout.Column().Grow().Height(20f).Begin())
                    last = child.rect;
            }
        });

        AssertRect(new NowRect(0f, 0f, 50f, 20f), first);
        AssertRect(new NowRect(225f, 0f, 175f, 20f), last);
    }

    [Test]
    public void MainAxisStretchUsesTheSameConstraintRedistributionAsGrow()
    {
        NowRect constrained = default;
        NowRect sibling = default;

        NowLayout.RunMeasured("stretch-min", new NowRect(0f, 0f, 400f, 20f), () =>
        {
            using (NowLayout.Row().FillWidth().Height(20f).Begin())
            {
                constrained = NowLayout.ReserveRect(
                    NowLayout.StretchWidth().SetMinWidth(300f).SetHeight(20f));
                sibling = NowLayout.ReserveRect(
                    NowLayout.StretchWidth().SetHeight(20f));
            }
        });

        AssertRect(new NowRect(0f, 0f, 300f, 20f), constrained);
        AssertRect(new NowRect(300f, 0f, 100f, 20f), sibling);
    }

    [TestCase(NowLayoutJustify.Center, 70f, 110f)]
    [TestCase(NowLayoutJustify.End, 140f, 180f)]
    [TestCase(NowLayoutJustify.SpaceBetween, 0f, 180f)]
    public void FluentJustifyPlacesChildrenAlongTheMainAxis(
        NowLayoutJustify justify,
        float expectedFirstX,
        float expectedSecondX)
    {
        NowRect first = default;
        NowRect second = default;

        NowLayout.RunMeasured("justify-area", new NowRect(0f, 0f, 200f, 100f), () =>
        {
            using (NowLayout.Row()
                .FillWidth()
                .Height(20f)
                .Justify(justify)
                .Begin())
            {
                first = NowLayout.ReserveRect(40f, 20f);
                second = NowLayout.ReserveRect(20f, 20f);
            }
        });

        Assert.AreEqual(expectedFirstX, first.x, 0.001f);
        Assert.AreEqual(expectedSecondX, second.x, 0.001f);
    }

    [TestCase(NowLayoutJustify.Center, "empty-center")]
    [TestCase(NowLayoutJustify.End, "empty-end")]
    public void EmptyJustifiedAreaDoesNotReportPhantomContent(
        NowLayoutJustify justify,
        string id)
    {
        var options = default(NowLayoutOptions).SetJustify(justify);

        NowLayout.RunMeasured(id, new NowRect(0f, 0f, 200f, 100f), options, static () => { });

        Assert.IsTrue(NowLayout.TryGetCachedAreaContentSize(id, out var contentSize));
        Assert.AreEqual(Vector2.zero, contentSize);
    }

    [Test]
    public void StretchOnCrossAxisFillsAvailableSpaceImmediately()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.ReserveRect(NowLayout.StretchWidth().SetHeight(20));

        NowLayout.EndArea();

        Assert.AreEqual(400f, rect.z, 0.001f);
    }

    [Test]
    public void MaxWidthClampsCrossAxisStretch()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.ReserveRect(NowLayout.StretchWidth().SetHeight(20).SetMaxWidth(150));

        NowLayout.EndArea();

        Assert.AreEqual(150f, rect.z, 0.001f);
    }

    [Test]
    public void AlignCenterCentersAlongCrossAxis()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.ReserveRect(
            NowLayout.Size(100, 20).SetAlign(NowLayoutAlign.Center));

        NowLayout.EndArea();

        Assert.AreEqual(150f, rect.x, 0.001f);
    }

    [Test]
    public void AlignEndPlacesAtCrossAxisEnd()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.ReserveRect(
            NowLayout.Size(100, 20).SetAlign(NowLayoutAlign.End));

        NowLayout.EndArea();

        Assert.AreEqual(300f, rect.x, 0.001f);
    }

    [Test]
    public void NestedGroupsComposePaddingAndPosition()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300), padding: 10);
        NowLayout.Vertical(padding: 5, height: 100);

        Vector4 inner = NowLayout.ReserveRect(50, 20);

        NowLayout.EndVertical();
        NowLayout.EndArea();

        AssertRect(new Vector4(15, 15, 50, 20), inner);
    }

    [Test]
    public void ScopesEndGroupsOnDispose()
    {
        using (NowLayout.Area(new Vector4(0, 0, 400, 300)))
        {
            using (var row = NowLayout.Horizontal())
            {
                Assert.AreEqual(400f, row.width, 0.001f);
                NowLayout.ReserveRect(10, 10);
            }

            NowLayout.ReserveRect(10, 10);
        }

        Assert.DoesNotThrow(() =>
        {
            NowLayout.Area(new Vector4(0, 0, 10, 10));
            NowLayout.EndArea();
        });
    }

    [Test]
    public void DisposingCopiedLayoutScopeCannotCloseANewerArea()
    {
        var first = NowLayout.Area(new NowRect(0, 0, 100, 100));
        var staleCopy = first;
        first.Dispose();

        var current = NowLayout.Area(new NowRect(0, 0, 100, 100));

        try
        {
            staleCopy.Dispose();
            Assert.DoesNotThrow(() => NowLayout.ReserveRect(10, 10));
        }
        finally
        {
            current.Dispose();
        }
    }

    [Test]
    public void OutOfOrderLayoutDisposeThrowsWithoutCorruptingStack()
    {
        var area = NowLayout.Area(new NowRect(0, 0, 100, 100));
        var row = NowLayout.Horizontal();

        try
        {
            Assert.Throws<InvalidOperationException>(() => area.Dispose());
            Assert.DoesNotThrow(() => NowLayout.ReserveRect(10, 10));
        }
        finally
        {
            row.Dispose();
            area.Dispose();
        }
    }

    [Test]
    public void LayoutOptionsRejectFixedAndStretchOnTheSameAxis()
    {
        Assert.Throws<InvalidOperationException>(() =>
            default(NowLayoutOptions).SetWidth(40f).SetStretchWidth(2f));
        Assert.Throws<InvalidOperationException>(() =>
            default(NowLayoutOptions).SetStretchWidth(2f).SetWidth(40f));
        Assert.Throws<InvalidOperationException>(() =>
            default(NowLayoutOptions).SetHeight(40f).SetStretchHeight(2f));
        Assert.Throws<InvalidOperationException>(() =>
            default(NowLayoutOptions).SetStretchHeight(2f).SetHeight(40f));

        Assert.Throws<InvalidOperationException>(() => NowLayout.Row().Width(40f).FillWidth());
        Assert.Throws<InvalidOperationException>(() => NowLayout.Column().FillHeight().Height(40f));
    }

    [Test]
    public void ExplicitRootBoundsRejectSilentlyIgnoredPlacementOptions()
    {
        var rect = new NowRect(0f, 0f, 200f, 100f);
        var width = default(NowLayoutOptions).SetWidth(50f);

        Assert.Throws<InvalidOperationException>(() => NowLayout.Row(rect).Width(50f));
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var area = NowLayout.Area(rect, width);
        });
        Assert.Throws<InvalidOperationException>(() =>
            NowLayout.RunMeasured(rect, width, () => { }));

        Assert.DoesNotThrow(() =>
        {
            using var root = NowLayout.Column(rect)
                .Padding(8f)
                .Gap(4f)
                .AlignChildren(NowLayoutAlign.Center)
                .Justify(NowLayoutJustify.Center)
                .Begin();
        });
    }

    [Test]
    public void GrowRejectsStretchOnTheParentMainAxis()
    {
        using (NowLayout.Row(new NowRect(0f, 0f, 200f, 100f)).Begin())
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var invalid = NowLayout.Column()
                    .Grow(2f)
                    .FillWidth()
                    .Height(20f)
                    .Begin();
            });
        }

        using (NowLayout.Column(new NowRect(0f, 0f, 200f, 100f)).Begin())
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var invalid = NowLayout.Row()
                    .Grow(2f)
                    .FillHeight()
                    .Width(20f)
                    .Begin();
            });
        }
    }

    [Test]
    public void LayoutOptionsRejectInvalidValuesAndContradictoryConstraints()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => default(NowLayoutOptions).SetWidth(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(NowLayoutOptions).SetHeight(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(NowLayoutOptions).SetStretchWidth(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(NowLayoutOptions).SetGrow(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(NowLayoutOptions).SetSpacing(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            default(NowLayoutOptions).SetJustify((NowLayoutJustify)99));
        Assert.Throws<ArgumentException>(() => default(NowLayoutOptions).SetMinWidth(20f).SetMaxWidth(10f));
        Assert.Throws<ArgumentException>(() => default(NowLayoutOptions).SetMaxHeight(10f).SetMinHeight(20f));
    }

    [Test]
    public void RunMeasuredRunsMeasureThenRealPass()
    {
        int calls = 0;
        bool firstWasMeasure = false;
        bool lastWasMeasure = true;

        NowLayout.RunMeasured(new Vector4(0, 0, 100, 100), () =>
        {
            calls++;

            if (calls == 1)
                firstWasMeasure = NowLayout.isMeasurePass;

            lastWasMeasure = NowLayout.isMeasurePass;
        });

        Assert.AreEqual(2, calls);
        Assert.IsTrue(firstWasMeasure, "first invocation should be the measure pass");
        Assert.IsFalse(lastWasMeasure, "second invocation should be the real pass");
        Assert.IsFalse(NowLayout.isMeasurePass);
    }

    [Test]
    public void RepeatedRunMeasuredCallsAtOneCallSiteKeepMeasureAndDrawIdsAligned()
    {
        var resolvedX = new List<float>();

        for (int i = 0; i < 3; ++i)
        {
            NowLayout.RunMeasured(new NowRect(0f, i * 30f, 100f, 20f), () =>
            {
                using (NowLayout.Row().FillWidth().Height(20f).Begin())
                {
                    NowLayout.Spacer();
                    NowRect child = NowLayout.ReserveRect(20f, 20f);

                    if (!NowLayout.isMeasurePass)
                        resolvedX.Add(child.x);
                }
            });
        }

        CollectionAssert.AreEqual(new[] { 80f, 80f, 80f }, resolvedX);
    }

    [Test]
    public void RunMeasuredRewindsIdsWhenTheOuterHostIsAlreadyPassive()
    {
        float resolvedX = -1f;
        NowInput.BeginPassive();

        try
        {
            NowLayout.RunMeasured(new NowRect(0f, 0f, 100f, 20f), () =>
            {
                using (NowLayout.Row().FillWidth().Height(20f).Begin())
                {
                    NowLayout.Spacer();
                    NowRect child = NowLayout.ReserveRect(20f, 20f);

                    if (!NowLayout.isMeasurePass)
                        resolvedX = child.x;
                }
            });
        }
        finally
        {
            NowInput.EndPassive();
        }

        Assert.AreEqual(80f, resolvedX, 0.0001f);
    }

    [Test]
    public void NestedRunMeasuredReusesTheOuterTwoPassCycle()
    {
        int outerCalls = 0;
        int nestedCalls = 0;
        bool outerFirstWasMeasure = false;
        bool outerLastWasMeasure = true;
        bool nestedFirstWasMeasure = false;
        bool nestedLastWasMeasure = true;

        NowLayout.RunMeasured("outer", new NowRect(0f, 0f, 200f, 200f), () =>
        {
            ++outerCalls;

            if (outerCalls == 1)
                outerFirstWasMeasure = NowLayout.isMeasurePass;

            outerLastWasMeasure = NowLayout.isMeasurePass;

            NowLayout.RunMeasured("nested", new NowRect(10f, 10f, 100f, 100f), () =>
            {
                ++nestedCalls;

                if (nestedCalls == 1)
                    nestedFirstWasMeasure = NowLayout.isMeasurePass;

                nestedLastWasMeasure = NowLayout.isMeasurePass;
                NowLayout.ReserveRect(20f, 20f);
            });
        });

        Assert.AreEqual(2, outerCalls);
        Assert.AreEqual(2, nestedCalls, "the nested region must run once per outer pass, not start two more passes");
        Assert.IsTrue(outerFirstWasMeasure);
        Assert.IsTrue(nestedFirstWasMeasure);
        Assert.IsFalse(outerLastWasMeasure);
        Assert.IsFalse(nestedLastWasMeasure);
    }

    [Test]
    public void RunMeasuredRestoresMeasureStateAfterMeasureAndDrawExceptions()
    {
        var measureFailure = new InvalidOperationException("measure failed");
        var thrownMeasureFailure = Assert.Throws<InvalidOperationException>(() =>
            NowLayout.RunMeasured("measure-failure", new NowRect(0f, 0f, 100f, 100f), () =>
            {
                if (NowLayout.isMeasurePass)
                    throw measureFailure;
            }));

        Assert.AreSame(measureFailure, thrownMeasureFailure);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.IsFalse(NowInput.isPassive);

        var drawFailure = new InvalidOperationException("draw failed");
        var thrownDrawFailure = Assert.Throws<InvalidOperationException>(() =>
            NowLayout.RunMeasured("draw-failure", new NowRect(0f, 0f, 100f, 100f), () =>
            {
                if (!NowLayout.isMeasurePass)
                    throw drawFailure;
            }));

        Assert.AreSame(drawFailure, thrownDrawFailure);
        Assert.IsFalse(NowLayout.isMeasurePass);
        Assert.IsFalse(NowInput.isPassive);

        int recoveredCalls = 0;
        Assert.DoesNotThrow(() =>
            NowLayout.RunMeasured("after-failure", new NowRect(0f, 0f, 100f, 100f), () => ++recoveredCalls));
        Assert.AreEqual(2, recoveredCalls, "a leaked nested-cycle depth would collapse this to one pass");
    }

    [Test]
    public void RunMeasuredResolvesFlexibleSpaceOnFirstFrame()
    {
        Vector4 trailing = default;

        NowLayout.RunMeasured("cb", new Vector4(0, 0, 200, 400), () =>
        {
            NowLayout.ReserveRect(100, 20);
            NowLayout.FlexibleSpace();
            trailing = NowLayout.ReserveRect(100, 30);
        });

        Assert.AreEqual(370f, trailing.y, 0.001f, "two-pass layout should resolve flexible space without a warm-up frame");
    }

    [Test]
    public void RunMeasuredSizesAutoGroupOnFirstFrame()
    {
        Vector4 rowRect = default;

        NowLayout.RunMeasured(new Vector4(0, 0, 400, 300), () =>
        {
            using (var row = NowLayout.Horizontal())
            {
                NowLayout.ReserveRect(100, 30);

                if (!NowLayout.isMeasurePass)
                    rowRect = row.rect;
            }
        });

        AssertRect(new Vector4(0, 0, 400, 30), rowRect);
    }

    [Test]
    public void RunMeasuredInteractionsAreInertDuringMeasurePass()
    {
        NowInput.Reset();
        var provider = new LayoutMockInputProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(20, 20), true, true, false)
        };

        try
        {
            using (NowInput.Begin(provider, new Vector2(200, 200)))
            {
                NowInteraction measure = default;
                NowInteraction real = default;

                NowLayout.RunMeasured(new Vector4(0, 0, 200, 200), () =>
                {
                    Vector4 rect = NowLayout.ReserveRect(100, 100);
                    var interaction = NowInput.Interact(1, rect);

                    if (NowLayout.isMeasurePass)
                        measure = interaction;
                    else
                        real = interaction;
                });

                Assert.IsTrue(measure.hovered, "hover is a pure read and should survive the measure pass");
                Assert.IsFalse(measure.pressed, "presses must not register during the measure pass");
                Assert.IsTrue(real.pressed, "the real pass should interact normally");
            }
        }
        finally
        {
            NowInput.Reset();
        }
    }

    [Test]
    public void LabelWithoutFontStillAllocates()
    {
        NowLayout.Area(new Vector4(10, 20, 400, 300));

        var text = NowLayout.Label("hello").Draw();
        Vector4 below = NowLayout.ReserveRect(50, 30);

        NowLayout.EndArea();

        Assert.AreEqual(10f, text.rect.x, 0.001f);
        Assert.AreEqual(20f, below.y, 0.001f, "label without a font measures zero and reserves no space");
    }

    [Test]
    public void LabelUsesConfiguredDefaultStyle()
    {
        NowLayout.labelStyle = new NowText(default, null)
            .SetFontSize(22)
            .SetColor(Color.red);

        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var text = NowLayout.Label("hello").Draw();
        NowLayout.EndArea();

        Assert.AreEqual(22f, text.fontSize, 0.001f);
        Assert.AreEqual((Vector4)Color.red, text.color);
    }

    [Test]
    public void DefaultLabelStyleUsesActiveThemeTextColor()
    {
        NowLayout.Reset();
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            using (NowTheme.Scope(theme))
            {
                var text = NowLayout.labelStyle;
                Color themeText = theme.GetColor(NowColorToken.Text, Color.black);

                Assert.AreEqual(16f, text.fontSize, 0.001f);
                Assert.AreEqual((Vector4)themeText, text.color);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(theme);
            NowLayout.Reset();
        }
    }

    [Test]
    public void LabelFontSizeOverloadOverridesDefaultStyle()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var text = NowLayout.Label("hello", 30f).Draw();
        NowLayout.EndArea();

        Assert.AreEqual(30f, text.fontSize, 0.001f);
    }

    [Test]
    public void LabelFluentChainStylesBeforePlacement()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        var text = NowLayout.Label("hello")
            .SetFontSize(40)
            .SetColor(Color.blue)
            .SetBold()
            .Draw();

        NowLayout.EndArea();

        Assert.AreEqual(40f, text.fontSize, 0.001f);
        Assert.AreEqual((Vector4)Color.blue, text.color);
        Assert.IsTrue((text.fontStyle & NowFontStyle.Bold) != 0);
    }

    [Test]
    public void LabelWithoutDrawAllocatesNothing()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        _ = NowLayout.Label("hello").SetFontSize(99);
        Vector4 below = NowLayout.ReserveRect(50, 30);

        NowLayout.EndArea();

        Assert.AreEqual(0f, below.y, 0.001f, "a label builder without Draw() must not reserve space; the '_ =' discard is the NOWUI001 analyzer opt-out");
    }

    [Test]
    public void LabelDrawOutsideAreaThrows()
    {
        Assert.Throws<InvalidOperationException>(() => NowLayout.Label("hello").Draw());
    }

    [Test]
    public void LabelReserveAllocatesAndDrawConsumesNoSpace()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        var label = NowLayout.Label("hello")
            .SetWidth(50)
            .SetHeight(20)
            .Reserve();

        var text = label.Draw();
        Vector4 below = NowLayout.ReserveRect(10, 10);

        NowLayout.EndArea();

        AssertRect(new Vector4(0, 0, 50, 20), label.rect);
        AssertRect(label.rect, text.rect);
        Assert.AreEqual(20f, below.y, 0.001f, "Draw() on a reserved label must not allocate additional space");
    }

    [Test]
    public void LabelRectBeforeReserveThrows()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        var label = NowLayout.Label("hello");

        Assert.Throws<InvalidOperationException>(() => _ = label.rect);

        NowLayout.EndArea();
    }

    [Test]
    public void LabelLayoutSettersAffectAllocation()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        var text = NowLayout.Label("hello")
            .SetStretchWidth()
            .SetHeight(20)
            .Draw();

        NowLayout.EndArea();

        Assert.AreEqual(400f, text.rect.width, 0.001f, "SetStretchWidth on the label should fill the area width");
        Assert.AreEqual(20f, text.rect.height, 0.001f, "SetHeight on the label should fix the allocated height");
    }

    [Test]
    public void LottieWithoutAssetAllocatesNothing()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        NowLayout.Lottie((NowLottieAsset)null).Draw();
        Vector4 below = NowLayout.ReserveRect(50, 30);

        NowLayout.EndArea();

        Assert.AreEqual(0f, below.y, 0.001f);
    }

    [Test]
    public void LottieUsesNativeSizeAndDerivesAspect()
    {
        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource("{\"v\":\"5.5.7\",\"fr\":30,\"ip\":0,\"op\":60,\"w\":200,\"h\":100,\"layers\":[]}");

            NowLayout.Area(new Vector4(0, 0, 400, 300));

            var native = NowLayout.Lottie(asset).Reserve();
            var halfWidth = NowLayout.Lottie(asset).SetWidth(100).Reserve();
            var fixedHeight = NowLayout.Lottie(asset).SetHeight(25).Reserve();

            NowLayout.EndArea();

            AssertRect(new Vector4(0, 0, 200, 100), native.rect);
            Assert.AreEqual(50f, halfWidth.height, 0.001f, "height should follow the animation's aspect ratio");
            Assert.AreEqual(50f, fixedHeight.width, 0.001f, "width should follow the animation's aspect ratio");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void LottieUrlUsesCachedAssetForLayout()
    {
        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource("{\"v\":\"5.5.7\",\"fr\":30,\"ip\":0,\"op\":60,\"w\":200,\"h\":100,\"layers\":[]}");
            NowLottieCache.SetAsset("https://example.com/spinner.json", asset);

            NowLayout.Area(new Vector4(0, 0, 400, 300));

            var native = NowLayout.Lottie("https://example.com/spinner.json").Reserve();
            var fixedHeight = NowLayout.Lottie("https://example.com/spinner.json").SetHeight(25).Reserve();

            NowLayout.EndArea();

            AssertRect(new Vector4(0, 0, 200, 100), native.rect);
            Assert.AreEqual(50f, fixedHeight.width, 0.001f, "width should follow the downloaded animation's aspect ratio");
        }
        finally
        {
            NowLottieCache.Reset();
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void LottieReserveThenDrawConsumesNoExtraSpace()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        var lottie = NowLayout.Lottie((NowLottieAsset)null).SetLayoutSize(60, 40).Reserve();
        lottie.Draw();
        Vector4 below = NowLayout.ReserveRect(10, 10);

        NowLayout.EndArea();

        AssertRect(new Vector4(0, 0, 60, 40), lottie.rect);
        Assert.AreEqual(40f, below.y, 0.001f);
    }

    [Test]
    public void ReserveRectOutsideAreaThrows()
    {
        Assert.Throws<InvalidOperationException>(() => NowLayout.ReserveRect(10, 10));
    }

    [Test]
    public void MismatchedEndThrows()
    {
        NowLayout.Area(new Vector4(0, 0, 100, 100));
        NowLayout.Horizontal();

        Assert.Throws<InvalidOperationException>(NowLayout.EndVertical);
    }

    [Test]
    public void EndAreaWithOpenGroupThrows()
    {
        NowLayout.Area(new Vector4(0, 0, 100, 100));
        NowLayout.Vertical();

        Assert.Throws<InvalidOperationException>(NowLayout.EndArea);
    }

    [Test]
    public void EndAreaWithoutBeginThrows()
    {
        Assert.Throws<InvalidOperationException>(NowLayout.EndArea);
    }

    sealed class LayoutMockInputProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = this.snapshot;
            return true;
        }
    }
}
