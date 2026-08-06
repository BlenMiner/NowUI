using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowMaskShapeTests
{
    static void AssertRect(NowRect expected, NowRect actual, float tolerance = 0.001f)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance, "x");
        Assert.AreEqual(expected.y, actual.y, tolerance, "y");
        Assert.AreEqual(expected.width, actual.width, tolerance, "width");
        Assert.AreEqual(expected.height, actual.height, tolerance, "height");
    }

    [Test]
    public void DefaultShapeIsEmpty()
    {
        Assert.IsTrue(default(NowMaskShape).isEmpty);
    }

    [Test]
    public void InvalidGeometryProducesEmptyShapes()
    {
        Assert.IsTrue(NowMaskShape.Rectangle(new NowRect(0f, 0f, -1f, 10f)).isEmpty);
        Assert.IsTrue(NowMaskShape.Circle(Vector2.zero, 0f).isEmpty);
        Assert.IsTrue(NowMaskShape.Circle(Vector2.zero, float.NaN).isEmpty);
        Assert.IsTrue(NowMaskShape.Capsule(Vector2.zero, Vector2.one, -1f).isEmpty);
    }

    [Test]
    public void FactoriesExposeConservativeBounds()
    {
        var rect = new NowRect(10f, 20f, 80f, 40f);

        AssertRect(rect, NowMaskShape.Rectangle(rect).bounds);
        AssertRect(rect, NowMaskShape.RoundedRect(rect, 12f).bounds);
        AssertRect(
            rect,
            NowMaskShape.RoundedRect(
                rect,
                new NowCornerRadius(
                    topLeft: 4f,
                    topRight: 8f,
                    bottomRight: 12f,
                    bottomLeft: 16f)).bounds);
        AssertRect(rect, NowMaskShape.Ellipse(rect).bounds);
        AssertRect(rect, NowMaskShape.Capsule(rect).bounds);
        AssertRect(new NowRect(22f, 32f, 16f, 16f), NowMaskShape.Circle(new Vector2(30f, 40f), 8f).bounds);
        AssertRect(
            new NowRect(5f, 15f, 40f, 10f),
            NowMaskShape.Capsule(new Vector2(10f, 20f), new Vector2(40f, 20f), 5f).bounds);
    }

    [Test]
    public void SetFeatherReturnsConfiguredCopyWithoutMutatingSource()
    {
        var original = NowMaskShape.Circle(new Vector2(30f, 40f), 8f);
        var feathered = original.SetFeather(6f);

        Assert.AreEqual(0f, original.feather, 0.001f);
        Assert.AreEqual(6f, feathered.feather, 0.001f);
        AssertRect(original.bounds, feathered.bounds);
        Assert.IsFalse(feathered.isEmpty);
    }

    [Test]
    public void InvalidFeatherFallsBackToDefaultRamp()
    {
        var shape = NowMaskShape.Rectangle(new NowRect(0f, 0f, 20f, 20f));

        Assert.AreEqual(0f, shape.SetFeather(-1f).feather);
        Assert.AreEqual(0f, shape.SetFeather(float.NaN).feather);
        Assert.AreEqual(0f, shape.SetFeather(float.PositiveInfinity).feather);
    }

    [Test]
    public void EqualShapeValuesCompareAndHashEqually()
    {
        var a = NowMaskShape.Capsule(new Vector2(10f, 20f), new Vector2(40f, 24f), 6f)
            .SetFeather(2f);
        var b = NowMaskShape.Capsule(new Vector2(10f, 20f), new Vector2(40f, 24f), 6f)
            .SetFeather(2f);
        var different = b.SetFeather(3f);

        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.IsTrue(a != different);
    }

    [Test]
    public void RoundedRectUsesNamedCornerRadii()
    {
        var shape = NowMaskShape.RoundedRect(
            new NowRect(0f, 0f, 40f, 40f),
            new NowCornerRadius(
                topLeft: 16f,
                topRight: 0f,
                bottomRight: 0f,
                bottomLeft: 0f));

        using (Now.Mask(shape))
        {
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(1f, 1f)), "rounded top-left corner");
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(39f, 1f)), "square top-right corner");
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(39f, 39f)), "square bottom-right corner");
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(1f, 39f)), "square bottom-left corner");
        }
    }

    [Test]
    public void CircleEllipseAndCapsuleUseTheirAnalyticInterior()
    {
        using (Now.Mask(NowMaskShape.Circle(new Vector2(20f, 20f), 10f)))
        {
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(20f, 20f)));
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(27f, 27f)));
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(30f, 20f)), "point on circle boundary");
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(30.01f, 20f)), "point beyond circle boundary");
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(29f, 29f)));
        }

        using (Now.Mask(NowMaskShape.Ellipse(new NowRect(0f, 0f, 40f, 20f))))
        {
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(20f, 10f)));
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(2f, 2f)));
        }

        using (Now.Mask(NowMaskShape.Capsule(new Vector2(10f, 20f), new Vector2(40f, 20f), 5f)))
        {
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(25f, 24f)));
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(7f, 20f)));
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(25f, 27f)));
        }
    }

    [Test]
    public void NestedAnalyticMasksIntersectAndRestoreOuterShape()
    {
        var left = NowMaskShape.Circle(new Vector2(30f, 30f), 20f);
        var right = NowMaskShape.Circle(new Vector2(50f, 30f), 20f);

        using (Now.Mask(left))
        {
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(20f, 30f)));

            using (Now.Mask(right))
            {
                Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(20f, 30f)));
                Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(40f, 30f)));
            }

            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(20f, 30f)));
        }
    }

    [Test]
    public void AnalyticMaskNestingLimitDoesNotCountLegacyRectangles()
    {
        var shape = NowMaskShape.Circle(new Vector2(20f, 20f), 10f);
        var scopes = new NowMaskScope[8];

        using (Now.Mask(new NowRect(0f, 0f, 40f, 40f)))
        {
            try
            {
                for (int i = 0; i < scopes.Length; ++i)
                    scopes[i] = Now.Mask(shape);

                Assert.Throws<InvalidOperationException>(() =>
                {
                    var overflow = Now.Mask(shape);
                    overflow.Dispose();
                });
            }
            finally
            {
                for (int i = scopes.Length - 1; i >= 0; --i)
                    scopes[i].Dispose();
            }

            Assert.IsTrue(
                Now.IsInsideAmbientMask(new Vector2(20f, 20f)),
                "Disposing the analytic scopes did not restore the legacy rectangle.");
        }

        Assert.IsTrue(
            Now.IsInsideAmbientMask(new Vector2(100f, 100f)),
            "Disposing the legacy scope did not restore the unmasked state.");
    }

    [Test]
    public void AnalyticHitTestingUsesTransformCapturedWhenMaskIsPushed()
    {
        var shape = NowMaskShape.Circle(new Vector2(10f, 10f), 4f);
        var scale = new Vector2(-2f, 3f);
        var origin = new Vector2(50f, 7f);

        using (Now.Transform(scale, origin))
        using (Now.Mask(shape))
        {
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(30f, 37f)), "transformed center");
            Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(22f, 37f)), "mirrored boundary");
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(21.8f, 37f)), "outside mirrored boundary");
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(30f, 49.3f)), "outside nonuniform vertical scale");
        }
    }
}
