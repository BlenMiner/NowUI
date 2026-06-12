using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowUIScreenTests
{
    [TearDown]
    public void TearDown()
    {
        NowUIScreen.referenceDpi = 160f;
        Now.StartUI();
    }

    [Test]
    public void StartUIDefaultsToScaleOne()
    {
        Now.StartUI();

        Assert.AreEqual(1f, Now.uiScale);
        Assert.AreEqual(Screen.width, Now.screenMask.width, 0.001f);
        Assert.AreEqual(Screen.height, Now.screenMask.height, 0.001f);
    }

    [Test]
    public void StartUIWithScaleShrinksLogicalScreen()
    {
        Now.StartUI(2f);

        Assert.AreEqual(2f, Now.uiScale);
        Assert.AreEqual(Screen.width / 2f, Now.screenMask.width, 0.001f);
        Assert.AreEqual(Screen.height / 2f, Now.screenMask.height, 0.001f);
    }

    [Test]
    public void StartUIWithScaleMapsInputToLogicalUnits()
    {
        Now.StartUI(2f);

        var surface = NowUIInput.surface;

        Assert.AreEqual(Screen.width / 2f, surface.size.x, 0.001f);
        Assert.AreEqual(Screen.height / 2f, surface.size.y, 0.001f);
        Assert.AreEqual(Screen.width, surface.screenRect.width, 0.001f);
        Assert.AreEqual(Screen.height, surface.screenRect.height, 0.001f);
    }

    [Test]
    public void StartUIWithMaskResetsScale()
    {
        Now.StartUI(3f);
        Now.StartUI(new NowRect(0f, 0f, 100f, 100f));

        Assert.AreEqual(1f, Now.uiScale);
    }

    [Test]
    public void StartUIRejectsInvalidScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Now.StartUI(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Now.StartUI(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Now.StartUI(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Now.StartUI(float.PositiveInfinity));
    }

    [Test]
    public void RecommendedUIScaleNeverShrinksUI()
    {
        Assert.GreaterOrEqual(NowUIScreen.recommendedUIScale, 1f);

        NowUIScreen.referenceDpi = 100000f;
        Assert.AreEqual(1f, NowUIScreen.recommendedUIScale);
    }

    [Test]
    public void ReferenceDpiRejectsNonPositiveValues()
    {
        NowUIScreen.referenceDpi = -5f;
        Assert.GreaterOrEqual(NowUIScreen.referenceDpi, 1f);
    }

    [Test]
    public void SafeAreaIsExpressedInUIUnits()
    {
        Now.StartUI(2f);

        Rect pixels = Screen.safeArea;
        NowRect safe = NowUIScreen.safeArea;

        Assert.AreEqual(pixels.x / 2f, safe.x, 0.001f);
        Assert.AreEqual((Screen.height - pixels.yMax) / 2f, safe.y, 0.001f);
        Assert.AreEqual(pixels.width / 2f, safe.width, 0.001f);
        Assert.AreEqual(pixels.height / 2f, safe.height, 0.001f);
    }

    [Test]
    public void SafeAreaFitsInsideLogicalScreen()
    {
        Now.StartUI(2f);

        NowRect safe = NowUIScreen.safeArea;
        NowRect mask = Now.screenMask;

        Assert.GreaterOrEqual(safe.x, mask.x - 0.001f);
        Assert.GreaterOrEqual(safe.y, mask.y - 0.001f);
        Assert.LessOrEqual(safe.xMax, mask.xMax + 0.001f);
        Assert.LessOrEqual(safe.yMax, mask.yMax + 0.001f);
    }
}
