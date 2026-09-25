using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;
using UnityEngine;

public class NowOpacityTests
{
    static readonly Vector2 Surface = new Vector2(256f, 128f);

    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowOverlay.Reset();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowOverlay.Reset();
    }

    static List<Vector4> Colors(NowDrawList drawList)
    {
        var colors = new List<Vector4>();
        drawList.mesh.GetUVs(3, colors);
        return colors;
    }

    [Test]
    public void OpacityMultipliesFillAndOutlineAlpha()
    {
        using (_drawList.Begin(Surface))
        using (Now.Opacity(0.5f))
        {
            Now.Rectangle(new NowRect(10f, 10f, 40f, 20f))
                .SetColor(new Color(1f, 0.5f, 0.25f, 0.8f))
                .SetOutline(2f, new Color(0f, 1f, 0f, 1f))
                .Draw();
        }

        var colors = Colors(_drawList);
        Assert.AreEqual(4, colors.Count);
        Assert.AreEqual(1f, colors[0].x, 0.0001f, "Opacity must not change color channels.");
        Assert.AreEqual(0.5f, colors[0].y, 0.0001f);
        Assert.AreEqual(0.4f, colors[0].w, 0.0001f);
    }

    [Test]
    public void NestedScopesMultiplyAndRestoreInOrder()
    {
        using (_drawList.Begin(Surface))
        {
            using (Now.Opacity(0.5f))
            {
                Assert.AreEqual(0.5f, Now.currentTint.a, 0.0001f);

                using (Now.Opacity(0.5f))
                using (Now.Tint(new Color(0.5f, 1f, 1f, 1f)))
                {
                    Assert.AreEqual(0.25f, Now.currentTint.a, 0.0001f);
                    Assert.AreEqual(0.5f, Now.currentTint.r, 0.0001f);
                    Now.Rectangle(new NowRect(0f, 0f, 10f, 10f)).SetColor(Color.white).Draw();
                }

                Assert.AreEqual(0.5f, Now.currentTint.a, 0.0001f);
                Assert.AreEqual(1f, Now.currentTint.r, 0.0001f);
            }

            Assert.AreEqual(Color.white, Now.currentTint);
        }

        var colors = Colors(_drawList);
        Assert.AreEqual(0.5f, colors[0].x, 0.0001f);
        Assert.AreEqual(0.25f, colors[0].w, 0.0001f);
    }

    [Test]
    public void OpacityIsClampedAndNanIsIgnored()
    {
        using (_drawList.Begin(Surface))
        {
            using (Now.Opacity(2f))
                Assert.AreEqual(1f, Now.currentTint.a, 0.0001f);

            using (Now.Opacity(-1f))
                Assert.AreEqual(0f, Now.currentTint.a, 0.0001f);

            using (Now.Opacity(float.NaN))
                Assert.AreEqual(1f, Now.currentTint.a, 0.0001f);

            using (Now.Tint(new Color(-1f, float.NaN, 2f, 1f)))
            {
                Assert.AreEqual(0f, Now.currentTint.r, 0.0001f);
                Assert.AreEqual(1f, Now.currentTint.g, 0.0001f);
                Assert.AreEqual(2f, Now.currentTint.b, 0.0001f);
            }
        }
    }

    [Test]
    public void DisposingOuterScopeFirstThrowsWithoutChangingTint()
    {
        using (_drawList.Begin(Surface))
        {
            var outer = Now.Opacity(0.5f);
            var inner = Now.Opacity(0.5f);

            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            Assert.AreEqual(0.25f, Now.currentTint.a, 0.0001f);

            inner.Dispose();
            outer.Dispose();
            Assert.AreEqual(1f, Now.currentTint.a, 0.0001f);

            // A copied or already disposed handle is harmless.
            outer.Dispose();
            default(NowTintScope).Dispose();
            Assert.AreEqual(1f, Now.currentTint.a, 0.0001f);
        }
    }

    [Test]
    public void LeakedScopeDoesNotOutliveItsCapture()
    {
        using (_drawList.Begin(Surface))
            Now.Opacity(0.25f);

        Assert.AreEqual(Color.white, Now.currentTint, "Ending a capture restores the tint it started with.");

        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(0f, 0f, 10f, 10f)).SetColor(Color.white).Draw();

        Assert.AreEqual(1f, Colors(_drawList)[0].w, 0.0001f);
    }

    [Test]
    public void IndependentCaptureStartsUntintedAndRestoresTheOuterTint()
    {
        var inner = new NowDrawList();

        try
        {
            using (_drawList.Begin(Surface))
            using (Now.Opacity(0.5f))
            {
                using (inner.Begin(Surface))
                {
                    Assert.AreEqual(1f, Now.currentTint.a, 0.0001f);
                    Now.Rectangle(new NowRect(0f, 0f, 10f, 10f)).SetColor(Color.white).Draw();
                }

                Assert.AreEqual(0.5f, Now.currentTint.a, 0.0001f);
                Now.Rectangle(new NowRect(0f, 0f, 10f, 10f)).SetColor(Color.white).Draw();
            }

            Assert.AreEqual(1f, Colors(inner)[0].w, 0.0001f);
            Assert.AreEqual(0.5f, Colors(_drawList)[0].w, 0.0001f);
        }
        finally
        {
            inner.Dispose();
        }
    }

    [Test]
    public void TextLinesAndShapesHonorOpacity()
    {
        using (_drawList.Begin(Surface))
        using (Now.Opacity(0.5f))
        {
            Now.Text(new NowRect(0f, 0f, 200f, 40f)).SetFontSize(20f).SetColor(Color.white).Draw("A");
            Now.Line(new Vector2(0f, 60f), new Vector2(100f, 60f)).SetWidth(4f).SetColor(Color.white).Draw();
            Now.Circle(new Vector2(150f, 60f), 12f).SetColor(Color.white).Draw();
        }

        Assert.Greater(Colors(_drawList).Count, 0);
        foreach (var color in Colors(_drawList))
            Assert.LessOrEqual(color.w, 0.5001f, "Every emitted vertex must carry the faded alpha.");
    }

    [Test]
    public void GlassFadesThroughItsOpacityTermNotItsTintAlpha()
    {
        using (_drawList.Begin(Surface))
        using (Now.Tint(new Color(0.5f, 1f, 1f, 0.25f)))
        {
            Now.Glass(new NowRect(4f, 6f, 32f, 20f))
                .SetTint(new Color(1f, 1f, 1f, 0.2f))
                .SetOutline(1f)
                .SetOutlineColor(new Color(1f, 1f, 1f, 0.6f))
                .Draw();
        }

        Assert.AreEqual(NowMeshKind.Glass, _drawList.batches[0].kind);
        var colors = Colors(_drawList);
        var extras = new List<Vector4>();
        _drawList.mesh.GetUVs(5, extras);

        Assert.AreEqual(0.5f, colors[0].x, 0.0001f, "Tint RGB still multiplies the glass tint.");
        Assert.AreEqual(0.2f, colors[0].w, 0.0001f, "Tint alpha keeps its own meaning.");
        Assert.AreEqual(0.25f, extras[0].x, 0.0001f, "The whole pane, backdrop included, fades in the shader.");
    }

    [Test]
    public void DeferredOverlayReplaysTheTintItWasQueuedWith()
    {
        var pointer = new FakePointer();

        using (NowInput.Begin(pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            using (Now.Opacity(0.5f))
            {
                NowOverlay.Defer(new NowRect(0f, 0f, 50f, 50f), () =>
                {
                    Assert.AreEqual(0.5f, Now.currentTint.a, 0.0001f);
                    Now.Rectangle(new NowRect(10f, 10f, 20f, 20f)).SetColor(Color.white).Draw();
                });
            }

            NowOverlay.DeferScreen(new NowRect(0f, 0f, 50f, 50f), () =>
                Assert.AreEqual(1f, Now.currentTint.a, 0.0001f));
        }

        Assert.AreEqual(0.5f, Colors(_drawList)[0].w, 0.0001f);
        Assert.AreEqual(Color.white, Now.currentTint);
    }

    [Test]
    public void TextureModifierDoesNotFadeOrTransformItsSurfaceTwice()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        using (_drawList.Begin(Surface))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        using (Now.Opacity(0.5f))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f)).SetRenderToTexture().Begin())
        {
            Now.Rectangle(new NowRect(4f, 6f, 30f, 20f)).SetColor(Color.white).Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        var colors = Colors(_drawList);
        Assert.AreEqual(1f, colors[0].w, 0.0001f, "The captured pixels already carry the fade.");
        var effected = _drawList.batches[0].bounds;

        using (_drawList.Begin(Surface))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
            Now.Rectangle(new NowRect(4f, 6f, 30f, 20f)).SetColor(Color.white).Draw();

        // The flattened surface covers the once-transformed rectangle
        // (18, 17, 60, 40), not a second application of the transform.
        var direct = _drawList.batches[0].bounds;
        Assert.AreEqual(direct.x, effected.x, 2f);
        Assert.AreEqual(direct.y, effected.y, 2f);
        Assert.AreEqual(direct.width, effected.width, 4f);
        Assert.AreEqual(direct.height, effected.height, 4f);
    }

    [Test]
    public void OpacityScopesAreAllocationFreeAfterWarmup()
    {
        void Frame()
        {
            using (_drawList.Begin(Surface))
            using (Now.Opacity(0.5f))
            using (Now.Tint(Color.red))
                Now.Rectangle(new NowRect(0f, 0f, 10f, 10f)).SetColor(Color.white).Draw();
        }

        Frame();
        Frame();
        Frame();

        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        Frame();
        long allocated = allocations.End();
        allocations.AssertZero(allocated, "opacity and tint scopes must not allocate");
    }
}
