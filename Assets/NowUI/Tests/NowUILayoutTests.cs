using System;
using NUnit.Framework;
using UnityEngine;

public class NowUILayoutTests
{
    [SetUp]
    public void SetUp()
    {
        NowUILayout.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        NowUILayout.Reset();
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
        NowUILayout.Area(new Vector4(10, 20, 300, 400));

        Vector4 first = NowUILayout.Rect(100, 30);
        Vector4 second = NowUILayout.Rect(50, 40);

        NowUILayout.EndArea();

        AssertRect(new Vector4(10, 20, 100, 30), first);
        AssertRect(new Vector4(10, 50, 50, 40), second);
    }

    [Test]
    public void SpacingAndPaddingOffsetChildren()
    {
        NowUILayout.Area(
            new Vector4(0, 0, 300, 400),
            new NowUILayoutOptions().SetSpacing(5).SetPadding(10));

        Vector4 first = NowUILayout.Rect(100, 20);
        Vector4 second = NowUILayout.Rect(100, 20);

        NowUILayout.EndArea();

        AssertRect(new Vector4(10, 10, 100, 20), first);
        AssertRect(new Vector4(10, 35, 100, 20), second);
    }

    [Test]
    public void HorizontalGroupPlacesRectsAlongX()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));
        NowUILayout.Horizontal(new NowUILayoutOptions().SetSpacing(8));

        Vector4 first = NowUILayout.Rect(60, 30);
        Vector4 second = NowUILayout.Rect(40, 30);

        NowUILayout.EndHorizontal();
        NowUILayout.EndArea();

        AssertRect(new Vector4(0, 0, 60, 30), first);
        AssertRect(new Vector4(68, 0, 40, 30), second);
    }

    [Test]
    public void GroupFillsParentCrossAxisByDefault()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));
        var row = NowUILayout.Horizontal();
        NowUILayout.EndHorizontal();
        NowUILayout.EndArea();

        Assert.AreEqual(400f, row.width, 0.001f);
    }

    [Test]
    public void AutoSizedGroupCorrectsParentCursorOnFirstFrame()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        NowUILayout.Horizontal();
        NowUILayout.Rect(100, 30);
        NowUILayout.EndHorizontal();

        Vector4 below = NowUILayout.Rect(50, 10);

        NowUILayout.EndArea();

        Assert.AreEqual(30f, below.y, 0.001f, "sibling after auto group should stack below its content on frame 1");
    }

    [Test]
    public void AutoSizedGroupUsesCachedContentSizeOnSecondPass()
    {
        for (int pass = 0; pass < 2; ++pass)
        {
            NowUILayout.Area("area", new Vector4(0, 0, 400, 300));

            var row = NowUILayout.Horizontal();
            NowUILayout.Rect(100, 30);
            NowUILayout.EndHorizontal();

            NowUILayout.EndArea();

            if (pass == 1)
                AssertRect(new Vector4(0, 0, 400, 30), row.rect);
        }
    }

    [Test]
    public void FlexibleSpacePushesTrailingContentToTheEnd()
    {
        Vector4 trailing = default;

        for (int pass = 0; pass < 2; ++pass)
        {
            NowUILayout.Area("area", new Vector4(0, 0, 200, 400));

            NowUILayout.Rect(100, 20);
            NowUILayout.FlexibleSpace();
            trailing = NowUILayout.Rect(100, 30);

            NowUILayout.EndArea();
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
            NowUILayout.Area("area", new Vector4(0, 0, 400, 300));
            NowUILayout.Horizontal();

            NowUILayout.Rect(100, 20);
            b = NowUILayout.Rect(NowUILayout.StretchWidth(1).SetHeight(20));
            c = NowUILayout.Rect(NowUILayout.StretchWidth(3).SetHeight(20));

            NowUILayout.EndHorizontal();
            NowUILayout.EndArea();
        }

        AssertRect(new Vector4(100, 0, 75, 20), b);
        AssertRect(new Vector4(175, 0, 225, 20), c);
    }

    [Test]
    public void StretchOnCrossAxisFillsAvailableSpaceImmediately()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowUILayout.Rect(NowUILayout.StretchWidth().SetHeight(20));

        NowUILayout.EndArea();

        Assert.AreEqual(400f, rect.z, 0.001f);
    }

    [Test]
    public void MaxWidthClampsCrossAxisStretch()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowUILayout.Rect(NowUILayout.StretchWidth().SetHeight(20).SetMaxWidth(150));

        NowUILayout.EndArea();

        Assert.AreEqual(150f, rect.z, 0.001f);
    }

    [Test]
    public void AlignCenterCentersAlongCrossAxis()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowUILayout.Rect(
            NowUILayout.Size(100, 20).SetAlign(NowUILayoutAlign.Center));

        NowUILayout.EndArea();

        Assert.AreEqual(150f, rect.x, 0.001f);
    }

    [Test]
    public void AlignEndPlacesAtCrossAxisEnd()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        Vector4 rect = NowUILayout.Rect(
            NowUILayout.Size(100, 20).SetAlign(NowUILayoutAlign.End));

        NowUILayout.EndArea();

        Assert.AreEqual(300f, rect.x, 0.001f);
    }

    [Test]
    public void NestedGroupsComposePaddingAndPosition()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300), new NowUILayoutOptions().SetPadding(10));
        NowUILayout.Vertical(new NowUILayoutOptions().SetPadding(5).SetHeight(100));

        Vector4 inner = NowUILayout.Rect(50, 20);

        NowUILayout.EndVertical();
        NowUILayout.EndArea();

        AssertRect(new Vector4(15, 15, 50, 20), inner);
    }

    [Test]
    public void ScopesEndGroupsOnDispose()
    {
        using (NowUILayout.Area(new Vector4(0, 0, 400, 300)))
        {
            using (var row = NowUILayout.Horizontal())
            {
                Assert.AreEqual(400f, row.width, 0.001f);
                NowUILayout.Rect(10, 10);
            }

            NowUILayout.Rect(10, 10);
        }

        Assert.DoesNotThrow(() =>
        {
            NowUILayout.Area(new Vector4(0, 0, 10, 10));
            NowUILayout.EndArea();
        });
    }

    [Test]
    public void CallbackAreaRunsMeasureThenRealPass()
    {
        int calls = 0;
        bool firstWasMeasure = false;
        bool lastWasMeasure = true;

        NowUILayout.Area(new Vector4(0, 0, 100, 100), () =>
        {
            calls++;

            if (calls == 1)
                firstWasMeasure = NowUILayout.isMeasurePass;

            lastWasMeasure = NowUILayout.isMeasurePass;
        });

        Assert.AreEqual(2, calls);
        Assert.IsTrue(firstWasMeasure, "first invocation should be the measure pass");
        Assert.IsFalse(lastWasMeasure, "second invocation should be the real pass");
        Assert.IsFalse(NowUILayout.isMeasurePass);
    }

    [Test]
    public void CallbackAreaResolvesFlexibleSpaceOnFirstFrame()
    {
        Vector4 trailing = default;

        NowUILayout.Area("cb", new Vector4(0, 0, 200, 400), () =>
        {
            NowUILayout.Rect(100, 20);
            NowUILayout.FlexibleSpace();
            trailing = NowUILayout.Rect(100, 30);
        });

        Assert.AreEqual(370f, trailing.y, 0.001f, "two-pass layout should resolve flexible space without a warm-up frame");
    }

    [Test]
    public void CallbackAreaSizesAutoGroupOnFirstFrame()
    {
        Vector4 rowRect = default;

        NowUILayout.Area(new Vector4(0, 0, 400, 300), () =>
        {
            using (var row = NowUILayout.Horizontal())
            {
                NowUILayout.Rect(100, 30);

                if (!NowUILayout.isMeasurePass)
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

                NowUILayout.Area(new Vector4(0, 0, 200, 200), () =>
                {
                    Vector4 rect = NowUILayout.Rect(100, 100);
                    var interaction = NowUIInput.Interact(1, rect);

                    if (NowUILayout.isMeasurePass)
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
        NowUILayout.Area(new Vector4(10, 20, 400, 300));

        var text = NowUILayout.Label("hello").Draw();
        Vector4 below = NowUILayout.Rect(50, 30);

        NowUILayout.EndArea();

        Assert.AreEqual(10f, text.rect.x, 0.001f);
        Assert.AreEqual(20f, below.y, 0.001f, "label without a font measures zero and reserves no space");
    }

    [Test]
    public void LabelUsesConfiguredDefaultStyle()
    {
        NowUILayout.labelStyle = new NowUIText(default, null)
            .SetFontSize(22)
            .SetColor(Color.red);

        NowUILayout.Area(new Vector4(0, 0, 400, 300));
        var text = NowUILayout.Label("hello").Draw();
        NowUILayout.EndArea();

        Assert.AreEqual(22f, text.fontSize, 0.001f);
        Assert.AreEqual((Vector4)Color.red, text.color);
    }

    [Test]
    public void LabelFontSizeOverloadOverridesDefaultStyle()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));
        var text = NowUILayout.Label("hello", 30f).Draw();
        NowUILayout.EndArea();

        Assert.AreEqual(30f, text.fontSize, 0.001f);
    }

    [Test]
    public void LabelFluentChainStylesBeforePlacement()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        var text = NowUILayout.Label("hello")
            .SetFontSize(40)
            .SetColor(Color.blue)
            .SetBold()
            .Draw();

        NowUILayout.EndArea();

        Assert.AreEqual(40f, text.fontSize, 0.001f);
        Assert.AreEqual((Vector4)Color.blue, text.color);
        Assert.IsTrue((text.fontStyle & NowFontStyle.Bold) != 0);
    }

    [Test]
    public void LabelWithoutDrawAllocatesNothing()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        NowUILayout.Label("hello").SetFontSize(99);
        Vector4 below = NowUILayout.Rect(50, 30);

        NowUILayout.EndArea();

        Assert.AreEqual(0f, below.y, 0.001f, "a label builder without Draw() must not reserve space");
    }

    [Test]
    public void LabelDrawOutsideAreaThrows()
    {
        Assert.Throws<InvalidOperationException>(() => NowUILayout.Label("hello").Draw());
    }

    [Test]
    public void LabelReserveAllocatesAndDrawConsumesNoSpace()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        var label = NowUILayout.Label("hello")
            .SetWidth(50)
            .SetHeight(20)
            .Reserve();

        var text = label.Draw();
        Vector4 below = NowUILayout.Rect(10, 10);

        NowUILayout.EndArea();

        AssertRect(new Vector4(0, 0, 50, 20), label.rect);
        AssertRect(label.rect, text.rect);
        Assert.AreEqual(20f, below.y, 0.001f, "Draw() on a reserved label must not allocate additional space");
    }

    [Test]
    public void LabelRectBeforeReserveThrows()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        var label = NowUILayout.Label("hello");

        Assert.Throws<InvalidOperationException>(() => _ = label.rect);

        NowUILayout.EndArea();
    }

    [Test]
    public void LabelLayoutSettersAffectAllocation()
    {
        NowUILayout.Area(new Vector4(0, 0, 400, 300));

        var text = NowUILayout.Label("hello")
            .SetStretchWidth()
            .SetHeight(20)
            .Draw();

        NowUILayout.EndArea();

        Assert.AreEqual(400f, text.rect.z, 0.001f, "SetStretchWidth on the label should fill the area width");
        Assert.AreEqual(20f, text.rect.w, 0.001f, "SetHeight on the label should fix the allocated height");
    }

    [Test]
    public void RectOutsideAreaThrows()
    {
        Assert.Throws<InvalidOperationException>(() => NowUILayout.Rect(10, 10));
    }

    [Test]
    public void MismatchedEndThrows()
    {
        NowUILayout.Area(new Vector4(0, 0, 100, 100));
        NowUILayout.Horizontal();

        Assert.Throws<InvalidOperationException>(NowUILayout.EndVertical);
    }

    [Test]
    public void EndAreaWithOpenGroupThrows()
    {
        NowUILayout.Area(new Vector4(0, 0, 100, 100));
        NowUILayout.Vertical();

        Assert.Throws<InvalidOperationException>(NowUILayout.EndArea);
    }

    [Test]
    public void EndAreaWithoutBeginThrows()
    {
        Assert.Throws<InvalidOperationException>(NowUILayout.EndArea);
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
