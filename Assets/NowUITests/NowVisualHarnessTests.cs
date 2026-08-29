using System.Collections.Generic;
using NUnit.Framework;
using NowUI.Editor;
using UnityEditor;
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

    [TestCase("sdf-mask-glow-clip")]
    [TestCase("sdf-mask-gallery")]
    [TestCase("sdf-planar-primitives")]
    [TestCase("sdf-radial-primitives")]
    public void SdfScenariosUseFocusedGoldenTolerance(string scenarioName)
    {
        GoldenComparisonTolerance tolerance = NowVisualHarnessRunner.ToleranceForScenario(scenarioName);

        Assert.AreEqual(8, tolerance.channelTolerance);
        Assert.AreEqual(0.0025f, tolerance.allowedMismatchRatio);
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

    [Test]
    public void ThemeReviewScenariosCoverEveryShippedTheme()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:NowThemeAsset",
            new[] { "Assets/NowUI/Assets/Themes" });
        var expectedPaths = new HashSet<string>();

        for (int i = 0; i < guids.Length; ++i)
            expectedPaths.Add(AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/'));

        IReadOnlyList<NowHarnessScenario> scenarios = NowHarnessScenarios.ThemeReviewScenarios();
        var actualPaths = new HashSet<string>();
        var scenarioNames = new HashSet<string>();

        for (int i = 0; i < scenarios.Count; ++i)
        {
            NowHarnessScenario scenario = scenarios[i];
            StringAssert.StartsWith("theme-review-", scenario.name);
            Assert.IsTrue(scenarioNames.Add(scenario.name), $"Duplicate review scenario '{scenario.name}'.");
            Assert.IsFalse(scenario.includeInGoldens, scenario.name);
            Assert.IsFalse(scenario.includeInPerf, scenario.name);
            Assert.IsTrue(scenario.suppressBadge, scenario.name);
            Assert.IsNotNull(scenario.draw, scenario.name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.themePath), scenario.name);
            Assert.IsTrue(actualPaths.Add(scenario.themePath), $"Duplicate theme path '{scenario.themePath}'.");
        }

        CollectionAssert.AreEquivalent(expectedPaths, actualPaths);
    }

    [Test]
    public void CoreScenarioEnumerationExcludesThemeReviews()
    {
        IReadOnlyList<NowHarnessScenario> scenarios = NowHarnessScenarios.All(includeThemeReviews: false);

        for (int i = 0; i < scenarios.Count; ++i)
            Assert.IsFalse(scenarios[i].name.StartsWith("theme-review-"), scenarios[i].name);
    }

    [Test]
    public void EditorComparisonScenarioUsesARealWindowCaptureOnly()
    {
        IReadOnlyList<NowHarnessScenario> scenarios =
            NowHarnessScenarios.All(includeThemeReviews: false);
        NowHarnessScenario comparison = null;

        for (int i = 0; i < scenarios.Count; ++i)
        {
            if (scenarios[i].name == "editorgui-unity-editor-dark")
            {
                comparison = scenarios[i];
                break;
            }
        }

        Assert.IsNotNull(comparison);
        Assert.IsNotNull(comparison.capture);
        Assert.IsNull(comparison.draw);
        Assert.IsFalse(comparison.includeInGoldens);
        Assert.IsFalse(comparison.includeInPerf);
        Assert.IsTrue(comparison.suppressBadge);
        Assert.AreEqual(1100, comparison.width);
        Assert.AreEqual(660, comparison.height);
    }

    [Test]
    public void UnityEditorFilePickerScenarioOpensTheRealDeterministicDialog()
    {
        IReadOnlyList<NowHarnessScenario> scenarios =
            NowHarnessScenarios.All(includeThemeReviews: false);
        NowHarnessScenario picker = null;

        for (int i = 0; i < scenarios.Count; ++i)
        {
            if (scenarios[i].name == "file-picker-unity-editor-dark-open")
            {
                picker = scenarios[i];
                break;
            }
        }

        Assert.IsNotNull(picker);
        Assert.AreEqual("Assets/NowUI/Assets/Themes/UnityEditorDark.asset", picker.themePath);
        Assert.AreEqual(1024, picker.width);
        Assert.AreEqual(640, picker.height);
        Assert.AreEqual(4, picker.warmupFrames);
        Assert.IsNotNull(picker.prepare);
        Assert.IsNotNull(picker.createInputProvider);
        Assert.IsNotNull(picker.afterWarmup);
        Assert.IsNotNull(picker.draw);
        Assert.IsNull(picker.capture);
        Assert.IsFalse(picker.includeInGoldens);
        Assert.IsFalse(picker.includeInPerf);
        Assert.IsTrue(picker.suppressBadge);
    }

    static Color32[] SolidPixels(int count, Color32 color)
    {
        var pixels = new Color32[count];
        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = color;

        return pixels;
    }
}
