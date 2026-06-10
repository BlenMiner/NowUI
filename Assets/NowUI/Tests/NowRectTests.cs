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
}
