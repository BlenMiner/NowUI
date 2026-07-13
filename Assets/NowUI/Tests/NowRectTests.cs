using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowRectTests
{
    static void AssertRect(NowRect expected, NowRect actual, float tolerance = 0.001f)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance, "x");
        Assert.AreEqual(expected.y, actual.y, tolerance, "y");
        Assert.AreEqual(expected.width, actual.width, tolerance, "width");
        Assert.AreEqual(expected.height, actual.height, tolerance, "height");
    }

    [Test]
    public void InsetShrinksEveryEdge()
    {
        var rect = new NowRect(10, 20, 100, 50);

        AssertRect(new NowRect(15, 25, 90, 40), rect.Inset(5));
        AssertRect(new NowRect(13, 24, 94, 42), rect.Inset(3, 4));
        AssertRect(new NowRect(11, 22, 96, 44), rect.Inset(1, 2, 3, 4));
    }

    [Test]
    public void OutsetGrowsEveryEdge()
    {
        var rect = new NowRect(10, 20, 100, 50);

        AssertRect(new NowRect(5, 15, 110, 60), rect.Outset(5));
        AssertRect(rect, rect.Outset(7).Inset(7));
    }

    [Test]
    public void UnionContainsBothRects()
    {
        var a = new NowRect(0, 0, 10, 10);
        var b = new NowRect(20, 5, 10, 30);

        AssertRect(new NowRect(0, 0, 30, 35), a.Union(b));
        AssertRect(a.Union(b), b.Union(a));
    }

    [Test]
    public void IntersectReturnsOverlapOrEmpty()
    {
        var a = new NowRect(0, 0, 10, 10);
        var b = new NowRect(5, 5, 10, 10);
        var c = new NowRect(50, 50, 10, 10);

        AssertRect(new NowRect(5, 5, 5, 5), a.Intersect(b));
        Assert.IsTrue(a.Intersect(c).isEmpty);
    }

    [Test]
    public void ContainsUsesHalfOpenBounds()
    {
        var rect = new NowRect(0, 0, 10, 10);

        Assert.IsTrue(rect.Contains(new Vector2(0, 0)));
        Assert.IsTrue(rect.Contains(new Vector2(9.99f, 9.99f)));
        Assert.IsFalse(rect.Contains(new Vector2(10, 10)));
        Assert.IsFalse(rect.Contains(new Vector2(-0.01f, 5)));
    }

    [Test]
    public void ConvertsImplicitlyWithVector4AndRect()
    {
        var rect = new NowRect(1, 2, 3, 4);

        Vector4 v = rect;
        Assert.AreEqual(new Vector4(1, 2, 3, 4), v);

        NowRect back = v;
        Assert.IsTrue(back == rect);

        Rect unity = rect;
        Assert.AreEqual(new Rect(1, 2, 3, 4), unity);

        NowRect fromUnity = unity;
        Assert.IsTrue(fromUnity == rect);
    }

    [Test]
    public void OffsetTranslatesPositionOnly()
    {
        var rect = new NowRect(10, 20, 30, 40);

        AssertRect(new NowRect(15, 17, 30, 40), rect.Offset(5, -3));
        AssertRect(rect.Offset(new Vector2(5, -3)), rect.Offset(5, -3));
    }

    [Test]
    public void CenteredPlacesRequestedSizeAtCenter()
    {
        var rect = new NowRect(10, 20, 100, 60);

        AssertRect(new NowRect(40, 40, 40, 20), rect.Centered(40, 20));
        AssertRect(rect.Centered(40, 20), rect.Centered(new Vector2(40, 20)));
    }

    [Test]
    public void AlignPlacesRequestedSizeOnEitherAxis()
    {
        var rect = new NowRect(10, 20, 100, 60);

        AssertRect(
            new NowRect(10, 70, 20, 10),
            rect.Align(20, 10, NowLayoutAlign.Start, NowLayoutAlign.End));
        AssertRect(
            new NowRect(90, 45, 20, 10),
            rect.Align(new Vector2(20, 10), NowLayoutAlign.End, NowLayoutAlign.Center));
    }

    [Test]
    public void AlignAllowsIntentionalOverflow()
    {
        var rect = new NowRect(10, 20, 100, 60);

        AssertRect(
            new NowRect(0, 10, 120, 80),
            rect.Align(120, 80, NowLayoutAlign.Center, NowLayoutAlign.Center));
    }

    [Test]
    public void EdgeSlicesReturnTakenAndRemainingRectsWithoutMutatingSource()
    {
        var rect = new NowRect(10, 20, 100, 60);

        NowRect top = rect.TakeTop(15, out NowRect below);
        AssertRect(new NowRect(10, 20, 100, 15), top);
        AssertRect(new NowRect(10, 35, 100, 45), below);

        NowRect bottom = rect.TakeBottom(15, out NowRect above);
        AssertRect(new NowRect(10, 65, 100, 15), bottom);
        AssertRect(new NowRect(10, 20, 100, 45), above);

        NowRect left = rect.TakeLeft(25, out NowRect rightRemainder);
        AssertRect(new NowRect(10, 20, 25, 60), left);
        AssertRect(new NowRect(35, 20, 75, 60), rightRemainder);

        NowRect right = rect.TakeRight(25, out NowRect leftRemainder);
        AssertRect(new NowRect(85, 20, 25, 60), right);
        AssertRect(new NowRect(10, 20, 75, 60), leftRemainder);

        AssertRect(new NowRect(10, 20, 100, 60), rect);
    }

    [Test]
    public void EdgeSlicesAreSafeWhenRemainderAliasesTheReceiver()
    {
        var topRemainder = new NowRect(10, 20, 100, 60);
        NowRect top = topRemainder.TakeTop(15, out topRemainder);
        AssertRect(new NowRect(10, 20, 100, 15), top);
        AssertRect(new NowRect(10, 35, 100, 45), topRemainder);

        var bottomRemainder = new NowRect(10, 20, 100, 60);
        NowRect bottom = bottomRemainder.TakeBottom(15, out bottomRemainder);
        AssertRect(new NowRect(10, 65, 100, 15), bottom);
        AssertRect(new NowRect(10, 20, 100, 45), bottomRemainder);

        var leftRemainder = new NowRect(10, 20, 100, 60);
        NowRect left = leftRemainder.TakeLeft(25, out leftRemainder);
        AssertRect(new NowRect(10, 20, 25, 60), left);
        AssertRect(new NowRect(35, 20, 75, 60), leftRemainder);

        var rightRemainder = new NowRect(10, 20, 100, 60);
        NowRect right = rightRemainder.TakeRight(25, out rightRemainder);
        AssertRect(new NowRect(85, 20, 25, 60), right);
        AssertRect(new NowRect(10, 20, 75, 60), rightRemainder);
    }

    [Test]
    public void EdgeSlicesClampToAvailableSize()
    {
        var rect = new NowRect(10, 20, 100, 60);

        AssertRect(rect, rect.TakeTop(1000, out NowRect below));
        AssertRect(new NowRect(10, 80, 100, 0), below);

        AssertRect(rect, rect.TakeBottom(1000, out NowRect above));
        AssertRect(new NowRect(10, 20, 100, 0), above);

        AssertRect(rect, rect.TakeLeft(1000, out NowRect right));
        AssertRect(new NowRect(110, 20, 0, 60), right);

        AssertRect(rect, rect.TakeRight(1000, out NowRect left));
        AssertRect(new NowRect(10, 20, 0, 60), left);
    }

    [Test]
    public void PlacementAndSlicesRejectInvalidArguments()
    {
        var rect = new NowRect(10, 20, 100, 60);

        Assert.Throws<ArgumentOutOfRangeException>(() => rect.Centered(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.Centered(10, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            rect.Align(10, 10, (NowLayoutAlign)99, NowLayoutAlign.Start));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            rect.Align(10, 10, NowLayoutAlign.Start, (NowLayoutAlign)99));

        Assert.Throws<ArgumentOutOfRangeException>(() => rect.TakeTop(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.TakeBottom(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.TakeLeft(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => rect.TakeRight(float.NegativeInfinity));
    }
}
