using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

    [Test]
    public void NestedStartUIThrowsWithoutReplacingTheOuterFrame()
    {
        var outer = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Now.StartUI(new NowRect(0f, 0f, 20f, 20f)))
                {
                }
            });

            Assert.AreEqual(100f, Now.screenMask.width, 0.001f);
            Assert.AreEqual(80f, Now.screenMask.height, 0.001f);
        }
        finally
        {
            outer.Dispose();
        }
    }

    [Test]
    public void DisposingCopiedScreenScopeCannotFinishANewerFrame()
    {
        var first = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));
        var staleCopy = first;
        first.Dispose();

        var current = Now.StartUI(new NowRect(0f, 0f, 60f, 40f));

        try
        {
            staleCopy.Dispose();

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Now.StartUI(new NowRect(0f, 0f, 20f, 20f)))
                {
                }
            });
        }
        finally
        {
            current.Dispose();
        }
    }

    [Test]
    public void LeakedScreenFrameDiscardsItsDeferredOverlays()
    {
        NowOverlay.Reset();
        bool invoked = false;
        var drawList = new NowDrawList();
        var abandoned = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));
        var abandonedDraw = drawList.Begin(new Vector2(100f, 80f));
        NowOverlay.DeferScreen(new NowRect(0f, 0f, 10f, 10f), () => invoked = true);

        try
        {
            var startedAt = typeof(Now).GetField("_screenFrameStartedAt", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(startedAt);
            startedAt.SetValue(null, Time.frameCount - 1);

            LogAssert.Expect(
                LogType.Error,
                "NowUI: a NowUIScreenScope from a previous frame was never disposed; the abandoned frame was discarded. Wrap Now.StartUI in a using statement.");

            using (Now.StartUI(new NowRect(0f, 0f, 60f, 40f)))
            {
            }

            Assert.IsFalse(invoked, "Deferred callbacks from an abandoned frame must not run in its replacement frame.");
            Assert.IsFalse(NowOverlay.hasOpenOverlay);

            abandonedDraw.Dispose();

            using (drawList.Begin(new Vector2(100f, 80f)))
                Now.Rectangle(new NowRect(4f, 6f, 20f, 16f)).SetColor(Color.white).Draw();

            Assert.IsTrue(drawList.hasGeometry, "Abandoned nested captures must not poison the next draw-list frame.");
        }
        finally
        {
            abandonedDraw.Cancel();
            abandoned.Dispose();
            NowOverlay.Reset();
            drawList.Dispose();
        }
    }

    [Test]
    public void StartUIRejectsAnActiveDrawListWithoutCorruptingItsCapture()
    {
        var drawList = new NowDrawList();
        var draw = drawList.Begin(new Vector2(100f, 80f));

        try
        {
            Assert.Throws<InvalidOperationException>(() => Now.StartUI(new NowRect(0f, 0f, 100f, 80f)));
            Now.Rectangle(new NowRect(4f, 6f, 20f, 16f)).SetColor(Color.white).Draw();
            draw.Dispose();
            Assert.IsTrue(drawList.hasGeometry);
        }
        finally
        {
            draw.Cancel();
            drawList.Dispose();
        }
    }

    [Test]
    public void StartUIRejectsAnActiveRetainedFrameWithoutChangingItsScale()
    {
        var frame = NowFrame.Begin(2f);

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Now.StartUI(new NowRect(0f, 0f, 40f, 30f)))
                {
                }
            });

            Assert.AreEqual(2f, Now.uiScale,
                "a rejected nested StartUI must not reset its retained host's ambient scale");
        }
        finally
        {
            frame.Dispose();
        }
    }

    [Test]
    public void StartUIRejectsAnActiveInputScopeWithoutReplacingItsSurface()
    {
        var expected = new NowInputSurface(new Vector2(91f, 47f));
        var input = NowInput.Begin(null, expected);

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Now.StartUI(new NowRect(0f, 0f, 40f, 30f)))
                {
                }
            });

            Assert.AreEqual(expected.size, NowInput.surface.size);
        }
        finally
        {
            input.Dispose();
        }
    }

    [Test]
    public void ScreenScopeDisposalWaitsForItsNestedInputScope()
    {
        var frame = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));
        var input = NowInput.Begin(null, new NowInputSurface(new Vector2(50f, 40f)));

        try
        {
            Assert.Throws<InvalidOperationException>(() => frame.Dispose());
            input.Dispose();
            Assert.DoesNotThrow(() => frame.Dispose(),
                "the screen scope should remain retryable after the nested input scope is closed");
        }
        finally
        {
            input.Dispose();
            frame.Dispose();
        }
    }

    [Test]
    public void DirectInputUpdateCannotReplaceAnActiveScreenFrame()
    {
        using (Now.StartUI(new NowRect(0f, 0f, 100f, 80f)))
        {
            Vector2 screenSize = NowInput.surface.size;

            Assert.Throws<InvalidOperationException>(() =>
                NowInput.Update(null, new NowInputSurface(new Vector2(12f, 9f))));

            Assert.AreEqual(screenSize, NowInput.surface.size);
        }
    }

    [Test]
    public void StartUIRejectsASuppressedNowGUIScopeWithoutUnsuppressingIt()
    {
        var gui = NowGUI.AutoForEvent(
            4701,
            new Rect(0f, 0f, 80f, 30f),
            Color.clear,
            1f,
            repaint: false);

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Now.StartUI(new NowRect(0f, 0f, 40f, 30f)))
                {
                }
            });
        }
        finally
        {
            gui.Dispose();
            NowGUI.DisposeAll();
            NowInput.Reset();
            NowControls.Reset();
        }
    }

    [Test]
    public void StartUIRejectsRunMeasuredCallbacksWithoutBreakingTheOuterCycle()
    {
        int rejected = 0;

        NowLayout.RunMeasured(
            "start-ui-reentry",
            new NowRect(0f, 0f, 100f, 60f),
            () =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (Now.StartUI(new NowRect(0f, 0f, 40f, 30f)))
                    {
                    }
                });
                ++rejected;
            });

        Assert.AreEqual(2, rejected,
            "both the measure and real callbacks should preserve their outer layout cycle");
        Assert.IsFalse(NowLayout.isMeasurePass);

        Assert.DoesNotThrow(() =>
        {
            using (Now.StartUI(new NowRect(0f, 0f, 40f, 30f)))
            {
            }
        });
    }

    [Test]
    public void ImmediateModifierLeavesDeferredOverlayForTheScreenFrame()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        NowOverlay.Reset();
        bool overlayRan = false;

        try
        {
            using (Now.StartUI(new NowRect(0f, 0f, 100f, 80f)))
            {
                NowOverlay.DeferScreen(new NowRect(0f, 0f, 12f, 12f), () => overlayRan = true);

                using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f))
                    .SetRenderToTexture()
                    .Begin())
                    Now.Rectangle(new NowRect(8f, 10f, 40f, 20f)).SetColor(Color.white).Draw();

                Assert.IsFalse(overlayRan,
                    "an inner passive effect must not flush or capture the screen frame's overlay");
            }

            Assert.IsTrue(overlayRan);
        }
        finally
        {
            NowOverlay.Reset();
        }
    }

    [Test]
    public void ThrowingScreenOverlayRollsBackItsPointerRegistration()
    {
        var block = new NowRect(4f, 6f, 18f, 14f);
        NowOverlay.Reset();
        var frame = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));

        try
        {
            NowOverlay.DeferScreen(block, () =>
                throw new InvalidOperationException("screen overlay failed"));

            Assert.Throws<InvalidOperationException>(() => frame.Dispose());
            Assert.IsFalse(NowOverlay.hasOpenOverlay,
                "a failed screen-frame overlay must not leave an invisible open overlay");

            NowOverlay.ForceNewFrame();
            Assert.IsFalse(NowOverlay.IsPointerBlocked(block.center),
                "a failed screen-frame overlay must not block the pointer on the next frame");
        }
        finally
        {
            frame.Dispose();
            NowOverlay.Reset();
        }
    }

    [Test]
    public void NestedInputScopePreservesTheScreenOverlayTransaction()
    {
        var block = new NowRect(11f, 13f, 22f, 17f);
        NowOverlay.Reset();
        var frame = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));

        try
        {
            using (NowInput.Begin(null, new NowInputSurface(new Vector2(50f, 40f))))
            {
            }

            NowOverlay.DeferScreen(block, () =>
                throw new InvalidOperationException("screen overlay failed after nested input"));

            Assert.Throws<InvalidOperationException>(() => frame.Dispose());
            Assert.IsFalse(NowOverlay.hasOpenOverlay,
                "a nested custom input surface must not replace the owning screen transaction");

            NowOverlay.ForceNewFrame();
            Assert.IsFalse(NowOverlay.IsPointerBlocked(block.center));
        }
        finally
        {
            frame.Dispose();
            NowOverlay.Reset();
        }
    }

    [Test]
    public void ThrowingOverlayQueuedBeforeNestedInputRollsBackTheScreenBlock()
    {
        var block = new NowRect(15f, 17f, 24f, 19f);
        NowOverlay.Reset();
        var frame = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));
        NowInputScope input = default;

        try
        {
            NowOverlay.DeferScreen(block, () =>
                throw new InvalidOperationException("screen overlay failed inside nested input"));

            input = NowInput.Begin(null, new NowInputSurface(new Vector2(50f, 40f)));
            Assert.Throws<InvalidOperationException>(() => input.Dispose());

            Assert.IsFalse(NowOverlay.hasOpenOverlay,
                "abandoning the global deferred queue must also remove blocks queued before the nested input checkpoint");

            Assert.DoesNotThrow(() => frame.Dispose());
            NowOverlay.ForceNewFrame();
            Assert.IsFalse(NowOverlay.IsPointerBlocked(block.center),
                "the abandoned screen overlay must not become an invisible blocker on the next frame");
        }
        finally
        {
            input.Dispose();
            frame.Dispose();
            NowOverlay.Reset();
            NowInput.Reset();
        }
    }

    [Test]
    public void ThrowingInputFrameOverlayRollsBackItsPointerRegistration()
    {
        var block = new NowRect(7f, 9f, 20f, 16f);
        NowOverlay.Reset();
        var input = NowInput.Begin(null, new NowInputSurface(new Vector2(100f, 80f)));

        try
        {
            NowOverlay.DeferScreen(block, () =>
                throw new InvalidOperationException("input overlay failed"));

            Assert.Throws<InvalidOperationException>(() => input.Dispose());
            Assert.IsFalse(NowOverlay.hasOpenOverlay,
                "a failed input-frame overlay must not leave an invisible open overlay");

            NowOverlay.ForceNewFrame();
            Assert.IsFalse(NowOverlay.IsPointerBlocked(block.center),
                "a failed input-frame overlay must not block the pointer on the next frame");
        }
        finally
        {
            input.Dispose();
            NowOverlay.Reset();
            NowInput.Reset();
        }
    }

    [Test]
    public void ScreenFrameStaysOwnedWhileDeferredCallbacksFlush()
    {
        NowOverlay.Reset();
        Exception reentryError = null;
        var frame = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));

        try
        {
            NowOverlay.DeferPassive(1, _ =>
            {
                try
                {
                    using (Now.StartUI(new NowRect(0f, 0f, 20f, 20f)))
                    {
                    }
                }
                catch (Exception ex)
                {
                    reentryError = ex;
                }
            });

            frame.Dispose();
            frame = default;

            Assert.IsInstanceOf<InvalidOperationException>(reentryError);
            Assert.DoesNotThrow(() =>
            {
                using (Now.StartUI(new NowRect(0f, 0f, 40f, 30f)))
                {
                }
            });
        }
        finally
        {
            frame.Dispose();
            NowOverlay.Reset();
        }
    }

    [Test]
    public void ScreenFrameDisposeCanRetryAfterNestedCaptureCloses()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var frame = Now.StartUI(new NowRect(0f, 0f, 100f, 80f));
        var drawList = new NowDrawList();
        var nested = drawList.Begin(new Vector2(100f, 80f));

        try
        {
            Assert.Throws<InvalidOperationException>(() => frame.Dispose());

            Now.Rectangle(new NowRect(4f, 6f, 20f, 16f)).SetColor(Color.white).Draw();
            nested.Dispose();
            Assert.IsTrue(drawList.hasGeometry);

            Assert.DoesNotThrow(() => frame.Dispose());
            frame = default;
        }
        finally
        {
            nested.Cancel();
            frame.Dispose();
            drawList.Dispose();
        }
    }
}
