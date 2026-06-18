using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowScreenTests
{
    [TearDown]
    public void TearDown()
    {
        NowScreen.referenceDpi = 160f;
        using (Now.StartUI())
        {
        }
    }

    [Test]
    public void StartUIDefaultsToScaleOne()
    {
        using (Now.StartUI())
        {
            Assert.AreEqual(1f, Now.uiScale);
            Assert.AreEqual(Screen.width, Now.screenMask.width, 0.001f);
            Assert.AreEqual(Screen.height, Now.screenMask.height, 0.001f);
        }
    }

    [Test]
    public void StartUIWithScaleShrinksLogicalScreen()
    {
        using (Now.StartUI(2f))
        {
            Assert.AreEqual(2f, Now.uiScale);
            Assert.AreEqual(Screen.width / 2f, Now.screenMask.width, 0.001f);
            Assert.AreEqual(Screen.height / 2f, Now.screenMask.height, 0.001f);
        }
    }

    [Test]
    public void StartUIWithScaleMapsInputToLogicalUnits()
    {
        using (Now.StartUI(2f))
        {
            var surface = NowInput.surface;

            Assert.AreEqual(Screen.width / 2f, surface.size.x, 0.001f);
            Assert.AreEqual(Screen.height / 2f, surface.size.y, 0.001f);
            Assert.AreEqual(Screen.width, surface.screenRect.width, 0.001f);
            Assert.AreEqual(Screen.height, surface.screenRect.height, 0.001f);
        }
    }

    [Test]
    public void StartUIWithMaskResetsScale()
    {
        using (Now.StartUI(3f))
        {
        }

        using (Now.StartUI(new NowRect(0f, 0f, 100f, 100f)))
            Assert.AreEqual(1f, Now.uiScale);
    }

    [Test]
    public void StartUIRejectsInvalidScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => { using (Now.StartUI(0f)) { } });
        Assert.Throws<ArgumentOutOfRangeException>(() => { using (Now.StartUI(-1f)) { } });
        Assert.Throws<ArgumentOutOfRangeException>(() => { using (Now.StartUI(float.NaN)) { } });
        Assert.Throws<ArgumentOutOfRangeException>(() => { using (Now.StartUI(float.PositiveInfinity)) { } });
    }

    [Test]
    public void RecommendedUIScaleNeverShrinksUI()
    {
        Assert.GreaterOrEqual(NowScreen.recommendedUIScale, 1f);

        NowScreen.referenceDpi = 100000f;
        Assert.AreEqual(1f, NowScreen.recommendedUIScale);
    }

    [Test]
    public void ReferenceDpiRejectsNonPositiveValues()
    {
        NowScreen.referenceDpi = -5f;
        Assert.GreaterOrEqual(NowScreen.referenceDpi, 1f);
    }

    [Test]
    public void SafeAreaIsExpressedInUIUnits()
    {
        using (Now.StartUI(2f))
        {
            Rect pixels = Screen.safeArea;
            NowRect safe = NowScreen.safeArea;

            Assert.AreEqual(pixels.x / 2f, safe.x, 0.001f);
            Assert.AreEqual((Screen.height - pixels.yMax) / 2f, safe.y, 0.001f);
            Assert.AreEqual(pixels.width / 2f, safe.width, 0.001f);
            Assert.AreEqual(pixels.height / 2f, safe.height, 0.001f);
        }
    }

    [Test]
    public void SafeAreaFitsInsideLogicalScreen()
    {
        using (Now.StartUI(2f))
        {
            NowRect safe = NowScreen.safeArea;
            NowRect mask = Now.screenMask;

            Assert.GreaterOrEqual(safe.x, mask.x - 0.001f);
            Assert.GreaterOrEqual(safe.y, mask.y - 0.001f);
            Assert.LessOrEqual(safe.xMax, mask.xMax + 0.001f);
            Assert.LessOrEqual(safe.yMax, mask.yMax + 0.001f);
        }
    }
}
