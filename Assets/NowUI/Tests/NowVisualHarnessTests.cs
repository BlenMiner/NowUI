using NUnit.Framework;
using NowUI.Editor;
using UnityEngine;

public class NowVisualHarnessTests
{
    [TestCase("landing-page-now")]
    [TestCase("landing-page-now-layout")]
    [TestCase("landing-page-now-compact")]
    [TestCase("landing-page-now-layout-compact")]
    public void LandingPagesUseStrictGoldenTolerance(string scenarioName)
    {
        GoldenComparisonTolerance tolerance = NowVisualHarnessRunner.ToleranceForScenario(scenarioName);

        Assert.AreEqual(4, tolerance.channelTolerance);
        Assert.AreEqual(0.0001f, tolerance.allowedMismatchRatio);
    }

    [Test]
    public void UnspecifiedScenarioUsesGeneralGoldenTolerance()
    {
        GoldenComparisonTolerance tolerance = NowVisualHarnessRunner.ToleranceForScenario("controls");

        Assert.AreEqual(8, tolerance.channelTolerance);
        Assert.AreEqual(0.01f, tolerance.allowedMismatchRatio);
    }

    [Test]
    public void StrictToleranceRejectsSmallLocalizedRegression()
    {
        var expected = SolidPixels(10000, new Color32(255, 255, 255, 255));
        var actual = SolidPixels(10000, new Color32(255, 255, 255, 255));
        for (int i = 0; i < 10; ++i)
            actual[i] = new Color32(0, 0, 0, 0);

        GoldenComparisonTolerance general = NowVisualHarnessRunner.ToleranceForScenario("controls");
        GoldenComparisonTolerance strict = NowVisualHarnessRunner.ToleranceForScenario("landing-page-now");

        Assert.IsTrue(NowVisualHarnessRunner.PixelsMatch(expected, actual, general, out string generalDifference), generalDifference);
        Assert.IsFalse(NowVisualHarnessRunner.PixelsMatch(expected, actual, strict, out string strictDifference));
        StringAssert.Contains("10 pixels differ", strictDifference);
        StringAssert.Contains("channel tolerance 4", strictDifference);
    }

    [Test]
    public void StrictToleranceIgnoresSmallPerChannelNoise()
    {
        var expected = SolidPixels(100, new Color32(100, 100, 100, 255));
        var actual = SolidPixels(100, new Color32(104, 104, 104, 255));
        GoldenComparisonTolerance strict = NowVisualHarnessRunner.ToleranceForScenario("landing-page-now-layout");

        Assert.IsTrue(NowVisualHarnessRunner.PixelsMatch(expected, actual, strict, out string difference), difference);
    }

    static Color32[] SolidPixels(int count, Color32 color)
    {
        var pixels = new Color32[count];
        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = color;

        return pixels;
    }
}
