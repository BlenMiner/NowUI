using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowLayoutTests
{
    [SetUp]
    public void SetUp()
    {
        NowLayout.Reset();

        // The engine falls back to a bundled default font, so pin a font-less
        // label style: these tests rely on labels measuring zero.
        NowLayout.labelStyle = new NowUIText(default, null).SetFontSize(16);
    }

    [TearDown]
    public void TearDown()
    {
        NowLayout.Reset();
    }

    static void AssertRect(Vector4 expected, Vector4 actual, float tolerance = 0.001f)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance, "x");
        Assert.AreEqual(expected.y, actual.y, tolerance, "y");
        Assert.AreEqual(expected.z, actual.z, tolerance, "width");
        Assert.AreEqual(expected.w, actual.w, tolerance, "height");
    }

    [Test]
    public void VerticalAreaStacksRectsDownward()
    {
        NowLayout.Area(new Vector4(10, 20, 300, 400));

        Vector4 first = NowLayout.Rect(100, 30);
        Vector4 second = NowLayout.Rect(50, 40);

        NowLayout.EndArea();

        AssertRect(new Vector4(10, 20, 100, 30), first);
        AssertRect(new Vector4(10, 50, 50, 40), second);
    }

    [Test]
    public void ContentTrackingReportsRootAreaExtent()
    {
        NowLayout.BeginContentTracking();

        NowLayout.Area(new Vector4(10, 10, 400, 300));
        NowLayout.Rect(100, 30);
        NowLayout.Rect(50, 40);
        NowLayout.EndArea();

        Vector2 size = NowLayout.EndContentTracking();

        Assert.AreEqual(110f, size.x, 0.01f, "extent = area origin + widest content");
        Assert.AreEqual(80f, size.y, 0.01f, "extent = area origin + stacked content height");
    }

    [Test]
    public void SpacingAndPaddingOffsetChildren()
    {
        NowLayout.Area(
            new Vector4(0, 0, 300, 400),
            new NowLayoutOptions().SetSpacing(5).SetPadding(10));

        Vector4 first = NowLayout.Rect(100, 20);
        Vector4 second = NowLayout.Rect(100, 20);

        NowLayout.EndArea();

        AssertRect(new Vector4(10, 10, 100, 20), first);
        AssertRect(new Vector4(10, 35, 100, 20), second);
    }

    [Test]
    public void HorizontalGroupPlacesRectsAlongX()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));
        NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(8));

        Vector4 first = NowLayout.Rect(60, 30);
        Vector4 second = NowLayout.Rect(40, 30);

        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        AssertRect(new Vector4(0, 0, 60, 30), first);
        AssertRect(new Vector4(68, 0, 40, 30), second);
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
    public void AutoSizedGroupCorrectsParentCursorOnFirstFrame()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        NowLayout.Horizontal();
        NowLayout.Rect(100, 30);
        NowLayout.EndHorizontal();

        Vector4 below = NowLayout.Rect(50, 10);

        NowLayout.EndArea();

        Assert.AreEqual(30f, below.y, 0.001f, "sibling after auto group should stack below its content on frame 1");
    }

    [Test]
    public void AutoSizedGroupUsesCachedContentSizeOnSecondPass()
    {
        for (int pass = 0; pass < 2; ++pass)
        {
            NowLayout.Area("area", new Vector4(0, 0, 400, 300));

            var row = NowLayout.Horizontal();
            NowLayout.Rect(100, 30);
            NowLayout.EndHorizontal();

            NowLayout.EndArea();

            if (pass == 1)
                AssertRect(new Vector4(0, 0, 400, 30), row.rect);
        }
    }

    [Test]
    public void CrossStretchedGroupMeasuresContentNotAllocation()
    {
        NowLayout.Area("measure-area", new Vector4(0, 0, 60, 60));
        NowLayout.Horizontal();
        NowLayout.Rect(128, 128);
        NowLayout.Rect(40, 16);
        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.IsTrue(NowLayout.TryGetCachedContentSize("measure-area", out var size));
        Assert.AreEqual(168f, size.x, 0.01f, "area must measure the row's content width, not the stretched allocation");
        Assert.AreEqual(128f, size.y, 0.01f);
    }

    [Test]
    public void AlignItemsAlignsChildrenWithPerChildOverride()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));
        NowLayout.Horizontal(new NowLayoutOptions().SetHeight(100).SetAlignItems(NowLayoutAlign.Center));

        Vector4 inherited = NowLayout.Rect(50, 20);
        Vector4 overridden = NowLayout.Rect(new NowLayoutOptions()
            .SetWidth(50).SetHeight(20)
            .SetAlign(NowLayoutAlign.End));

        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.AreEqual(40f, inherited.y, 0.001f, "child without explicit align inherits the group's align-items");
        Assert.AreEqual(80f, overridden.y, 0.001f, "per-child SetAlign overrides the group's align-items");
    }

    [Test]
    public void ContentRectReservesLastHeightAndConvergesViaEnd()
    {
        NowControls.Reset();
        NowUIControlState.Reset();

        NowContentRect Next() => NowLayout.ContentRect();

        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var first = Next();
        Assert.AreEqual(1f, first.rect.height, 0.001f, "unknown height reserves a minimal rect");
        Assert.AreEqual(400f, first.rect.width, 0.001f, "content rects stretch to the available width");

        NowUIControlState.BeginRepaintTracking();
        first.End(72f);
        Assert.IsTrue(NowUIControlState.EndRepaintTracking(), "height change must request a repaint");
        NowLayout.EndArea();

        NowControls.Reset();

        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var second = Next();
        Assert.AreEqual(72f, second.rect.height, 0.001f, "the site-keyed height survives to the next frame");

        NowUIControlState.BeginRepaintTracking();
        second.End(72f);
        Assert.IsFalse(NowUIControlState.EndRepaintTracking(), "settled height must not keep repainting");
        NowLayout.EndArea();

        NowControls.Reset();
        NowUIControlState.Reset();
    }

    [Test]
    public void CrossStretchedGroupShrinksWhenContentShrinks()
    {
        for (int pass = 0; pass < 2; ++pass)
        {
            NowLayout.Area("shrink-area", new Vector4(0, 0, 300, 300));
            NowLayout.Horizontal();
            NowLayout.Rect(200, 20);
            NowLayout.EndHorizontal();
            NowLayout.EndArea();
        }

        NowLayout.Area("shrink-area", new Vector4(0, 0, 300, 300));
        NowLayout.Horizontal();
        NowLayout.Rect(50, 20);
        NowLayout.EndHorizontal();
        NowLayout.EndArea();

        Assert.IsTrue(NowLayout.TryGetCachedContentSize("shrink-area", out var size));
        Assert.AreEqual(50f, size.x, 0.01f, "measured width must follow content back down");
    }

    [Test]
    public void FlexibleSpacePushesTrailingContentToTheEnd()
    {
        Vector4 trailing = default;

        for (int pass = 0; pass < 2; ++pass)
        {
            NowLayout.Area("area", new Vector4(0, 0, 200, 400));

            NowLayout.Rect(100, 20);
            NowLayout.FlexibleSpace();
            trailing = NowLayout.Rect(100, 30);

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
            NowLayout.Area("area", new Vector4(0, 0, 400, 300));
            NowLayout.Horizontal();

            NowLayout.Rect(100, 20);
            b = NowLayout.Rect(NowLayout.StretchWidth(1).SetHeight(20));
            c = NowLayout.Rect(NowLayout.StretchWidth(3).SetHeight(20));

            NowLayout.EndHorizontal();
            NowLayout.EndArea();
        }

        AssertRect(new Vector4(100, 0, 75, 20), b);
        AssertRect(new Vector4(175, 0, 225, 20), c);
    }

    [Test]
    public void StretchOnCrossAxisFillsAvailableSpaceImmediately()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.Rect(NowLayout.StretchWidth().SetHeight(20));

        NowLayout.EndArea();

        Assert.AreEqual(400f, rect.z, 0.001f);
    }

    [Test]
    public void MaxWidthClampsCrossAxisStretch()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.Rect(NowLayout.StretchWidth().SetHeight(20).SetMaxWidth(150));

        NowLayout.EndArea();

        Assert.AreEqual(150f, rect.z, 0.001f);
    }

    [Test]
    public void AlignCenterCentersAlongCrossAxis()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.Rect(
            NowLayout.Size(100, 20).SetAlign(NowLayoutAlign.Center));

        NowLayout.EndArea();

        Assert.AreEqual(150f, rect.x, 0.001f);
    }

    [Test]
    public void AlignEndPlacesAtCrossAxisEnd()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowLayout.Rect(
            NowLayout.Size(100, 20).SetAlign(NowLayoutAlign.End));

        NowLayout.EndArea();

        Assert.AreEqual(300f, rect.x, 0.001f);
    }

    [Test]
    public void NestedGroupsComposePaddingAndPosition()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300), new NowLayoutOptions().SetPadding(10));
        NowLayout.Vertical(new NowLayoutOptions().SetPadding(5).SetHeight(100));

        Vector4 inner = NowLayout.Rect(50, 20);

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
                NowLayout.Rect(10, 10);
            }

            NowLayout.Rect(10, 10);
        }

        Assert.DoesNotThrow(() =>
        {
            NowLayout.Area(new Vector4(0, 0, 10, 10));
            NowLayout.EndArea();
        });
    }

    [Test]
    public void CallbackAreaRunsMeasureThenRealPass()
    {
        int calls = 0;
        bool firstWasMeasure = false;
        bool lastWasMeasure = true;

        NowLayout.Area(new Vector4(0, 0, 100, 100), () =>
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
    public void CallbackAreaResolvesFlexibleSpaceOnFirstFrame()
    {
        Vector4 trailing = default;

        NowLayout.Area("cb", new Vector4(0, 0, 200, 400), () =>
        {
            NowLayout.Rect(100, 20);
            NowLayout.FlexibleSpace();
            trailing = NowLayout.Rect(100, 30);
        });

        Assert.AreEqual(370f, trailing.y, 0.001f, "two-pass layout should resolve flexible space without a warm-up frame");
    }

    [Test]
    public void CallbackAreaSizesAutoGroupOnFirstFrame()
    {
        Vector4 rowRect = default;

        NowLayout.Area(new Vector4(0, 0, 400, 300), () =>
        {
            using (var row = NowLayout.Horizontal())
            {
                NowLayout.Rect(100, 30);

                if (!NowLayout.isMeasurePass)
                    rowRect = row.rect;
            }
        });

        AssertRect(new Vector4(0, 0, 400, 30), rowRect);
    }

    [Test]
    public void CallbackAreaInteractionsAreInertDuringMeasurePass()
    {
        NowUIInput.Reset();
        var provider = new LayoutMockInputProvider
        {
            snapshot = new NowUIInputSnapshot(new Vector2(20, 20), true, true, false)
        };

        try
        {
            using (NowUIInput.Begin(provider, new Vector2(200, 200)))
            {
                NowUIInteraction measure = default;
                NowUIInteraction real = default;

                NowLayout.Area(new Vector4(0, 0, 200, 200), () =>
                {
                    Vector4 rect = NowLayout.Rect(100, 100);
                    var interaction = NowUIInput.Interact(1, rect);

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
            NowUIInput.Reset();
        }
    }

    [Test]
    public void LabelWithoutFontStillAllocates()
    {
        NowLayout.Area(new Vector4(10, 20, 400, 300));

        var text = NowLayout.Label("hello").Draw();
        Vector4 below = NowLayout.Rect(50, 30);

        NowLayout.EndArea();

        Assert.AreEqual(10f, text.rect.x, 0.001f);
        Assert.AreEqual(20f, below.y, 0.001f, "label without a font measures zero and reserves no space");
    }

    [Test]
    public void LabelUsesConfiguredDefaultStyle()
    {
        NowLayout.labelStyle = new NowUIText(default, null)
            .SetFontSize(22)
            .SetColor(Color.red);

        NowLayout.Area(new Vector4(0, 0, 400, 300));
        var text = NowLayout.Label("hello").Draw();
        NowLayout.EndArea();

        Assert.AreEqual(22f, text.fontSize, 0.001f);
        Assert.AreEqual((Vector4)Color.red, text.color);
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
        Vector4 below = NowLayout.Rect(50, 30);

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
        Vector4 below = NowLayout.Rect(10, 10);

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

        NowLayout.Lottie(null).Draw();
        Vector4 below = NowLayout.Rect(50, 30);

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
    public void LottieReserveThenDrawConsumesNoExtraSpace()
    {
        NowLayout.Area(new Vector4(0, 0, 400, 300));

        var lottie = NowLayout.Lottie(null).SetLayoutSize(60, 40).Reserve();
        lottie.Draw();
        Vector4 below = NowLayout.Rect(10, 10);

        NowLayout.EndArea();

        AssertRect(new Vector4(0, 0, 60, 40), lottie.rect);
        Assert.AreEqual(40f, below.y, 0.001f);
    }

    [Test]
    public void RectOutsideAreaThrows()
    {
        Assert.Throws<InvalidOperationException>(() => NowLayout.Rect(10, 10));
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

    sealed class LayoutMockInputProvider : INowUIInputProvider
    {
        public NowUIInputSnapshot snapshot;

        public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot)
        {
            snapshot = this.snapshot;
            return true;
        }
    }
}
