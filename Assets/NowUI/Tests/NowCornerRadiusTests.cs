using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowCornerRadiusTests
{
    [Test]
    public void PackedMatchesShaderCornerOrder()
    {
        var radius = new NowCornerRadius(topLeft: 1f, topRight: 2f, bottomRight: 3f, bottomLeft: 4f);

        Assert.AreEqual(new Vector4(2f, 3f, 1f, 4f), radius.packed);
    }

    [Test]
    public void FromPackedRoundTripsEveryCorner()
    {
        var radius = new NowCornerRadius(topLeft: 1f, topRight: 2f, bottomRight: 3f, bottomLeft: 4f);
        var unpacked = NowCornerRadius.FromPacked(radius.packed);

        Assert.AreEqual(radius.topLeft, unpacked.topLeft, "topLeft");
        Assert.AreEqual(radius.topRight, unpacked.topRight, "topRight");
        Assert.AreEqual(radius.bottomRight, unpacked.bottomRight, "bottomRight");
        Assert.AreEqual(radius.bottomLeft, unpacked.bottomLeft, "bottomLeft");
    }

    [Test]
    public void FromPackedRoundTripsSideHelpers()
    {
        AssertRoundTrip(NowCornerRadius.All(5f));
        AssertRoundTrip(NowCornerRadius.Top(5f));
        AssertRoundTrip(NowCornerRadius.Bottom(5f));
        AssertRoundTrip(NowCornerRadius.Left(5f));
        AssertRoundTrip(NowCornerRadius.Right(5f));
    }

    static void AssertRoundTrip(NowCornerRadius radius)
    {
        var unpacked = NowCornerRadius.FromPacked(radius.packed);

        Assert.AreEqual(radius.topLeft, unpacked.topLeft, "topLeft");
        Assert.AreEqual(radius.topRight, unpacked.topRight, "topRight");
        Assert.AreEqual(radius.bottomRight, unpacked.bottomRight, "bottomRight");
        Assert.AreEqual(radius.bottomLeft, unpacked.bottomLeft, "bottomLeft");
    }
}
