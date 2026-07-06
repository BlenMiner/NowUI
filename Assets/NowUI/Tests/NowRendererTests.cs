using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using NowUI;
using NowUI.Internal;

public class NowRendererTests
{
    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    [Test]
    public void DrawListWarmupCanUseInputProviderAndClearsGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var provider = new FakeProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(24f, 18f), false, false, false)
        };
        var drawList = new NowDrawList();
        bool sawInputContext = false;
        Vector2 pointer = default;

        try
        {
            drawList.Warmup(new Vector2(128, 64), provider, () =>
            {
                sawInputContext = NowInput.hasContext;
                pointer = NowInput.current.pointerPosition;

                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();
            });

            Assert.IsTrue(sawInputContext);
            Assert.AreEqual(new Vector2(24f, 18f), pointer);
            Assert.IsFalse(drawList.hasGeometry);
            Assert.IsFalse(NowInput.hasContext);
        }
        finally
        {
            drawList.Dispose();
            NowInput.Reset();
        }
    }

    [Test]
    public void DrawListWarmupClearsGeometryWhenDrawThrows()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                drawList.Warmup(new Vector2(128, 64), () =>
                {
                    Now.Rectangle(new NowRect(4, 6, 32, 20))
                        .SetColor(Color.white)
                        .Draw();

                    throw new System.InvalidOperationException("warmup failed");
                }));

            Assert.IsFalse(drawList.hasGeometry);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void DrawListBuildCapturesRectangleGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Rectangle(new Vector4(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.AreEqual(1, drawList.mesh.subMeshCount);
            Assert.AreEqual(4, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void RectangleMeshIncludesVisualPaddingForEdgeEffects()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetBlur(3f)
                    .SetOutline(5f)
                    .SetColor(Color.white)
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(new NowRect(-6, -4, 52, 40), drawList.batches[0].bounds);
            Assert.AreEqual(new Vector3(20f, -16f, 0f), drawList.mesh.bounds.center);
            Assert.AreEqual(new Vector3(52f, 40f, 0f), drawList.mesh.bounds.size);

            var vertices = drawList.mesh.vertices;
            Assert.AreEqual(new Vector3(-6f, -36f, 0f), vertices[0]);
            Assert.AreEqual(new Vector3(-6f, 4f, 0f), vertices[1]);
            Assert.AreEqual(new Vector3(46f, 4f, 0f), vertices[2]);
            Assert.AreEqual(new Vector3(46f, -36f, 0f), vertices[3]);

            var rects = new System.Collections.Generic.List<Vector4>();
            var rawUvs = new System.Collections.Generic.List<Vector4>();
            drawList.mesh.GetUVs(1, rects);
            drawList.mesh.GetUVs(7, rawUvs);

            Assert.AreEqual(new Vector4(4f, -26f, 32f, 20f), rects[0]);
            Assert.AreEqual(-10f / 32f, rawUvs[0].x, 0.0001f);
            Assert.AreEqual(-10f / 20f, rawUvs[0].y, 0.0001f);
            Assert.AreEqual(1f + 10f / 32f, rawUvs[2].x, 0.0001f);
            Assert.AreEqual(1f + 10f / 20f, rawUvs[2].y, 0.0001f);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void NativeRenderMeshPackPreservesRenderChannelsWhenAvailable()
    {
        if (!NowLottieNative.packRenderAvailable)
            Assert.Ignore("nowui-vg version 4 render packing is unavailable on this platform.");

        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetBlur(3f)
                    .SetOutline(5f)
                    .SetColor(Color.white)
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);

            var vertices = drawList.mesh.vertices;
            Assert.AreEqual(new Vector3(-6f, -36f, 0f), vertices[0]);
            Assert.AreEqual(new Vector3(46f, 4f, 0f), vertices[2]);

            var uvs = new System.Collections.Generic.List<Vector2>();
            var rects = new System.Collections.Generic.List<Vector4>();
            var colors = new System.Collections.Generic.List<Vector4>();
            var rawUvs = new System.Collections.Generic.List<Vector4>();
            drawList.mesh.GetUVs(0, uvs);
            drawList.mesh.GetUVs(1, rects);
            drawList.mesh.GetUVs(3, colors);
            drawList.mesh.GetUVs(7, rawUvs);

            Assert.AreEqual(4, uvs.Count);
            Assert.AreEqual(4, rects.Count);
            Assert.AreEqual(4, colors.Count);
            Assert.AreEqual(4, rawUvs.Count);

            Assert.AreEqual(-10f / 32f, uvs[0].x, 0.0001f);
            Assert.AreEqual(-10f / 20f, uvs[0].y, 0.0001f);
            Assert.AreEqual(new Vector4(4f, -26f, 32f, 20f), rects[0]);
            Assert.AreEqual(new Vector4(1f, 1f, 1f, 1f), colors[0]);
            Assert.AreEqual(1f + 10f / 32f, rawUvs[2].x, 0.0001f);
            Assert.AreEqual(1f + 10f / 20f, rawUvs[2].y, 0.0001f);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void DrawListBuildCapturesRippleGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/RippleMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Ripple(new NowRect(4, 6, 64, 32))
                    .SetRadius(16f)
                    .SetOrigin(new Vector2(36f, 22f))
                    .SetCircleRadius(30f)
                    .SetColor(new Color(1f, 1f, 1f, 0.2f))
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.AreEqual(1, drawList.mesh.subMeshCount);
            Assert.AreEqual(4, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void DrawListClearRemovesGeometry()
    {
        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Rectangle(new Vector4(4, 6, 32, 20)).Draw();

            drawList.Clear();

            Assert.IsFalse(drawList.hasGeometry);
            Assert.AreEqual(0, drawList.batchCount);
            Assert.AreEqual(0, drawList.mesh.vertexCount);
            Assert.AreEqual(Vector2.zero, drawList.size);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void RendererBuildCapturesRectangleGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var renderer = new NowRenderer();

        try
        {
            using (renderer.Begin(new Vector2(128, 64)))
                Now.Rectangle(new Vector4(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();

            Assert.IsTrue(renderer.hasGeometry);
            Assert.AreEqual(1, renderer.batchCount);
            Assert.AreEqual(1, renderer.mesh.subMeshCount);
            Assert.AreEqual(4, renderer.mesh.vertexCount);
        }
        finally
        {
            renderer.Dispose();
        }
    }

    [Test]
    public void DrawListWarmupClearsCapturedGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            drawList.Warmup(new Vector2(128, 64), () =>
            {
                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();
            });

            Assert.IsFalse(drawList.hasGeometry);
            Assert.AreEqual(0, drawList.batchCount);
            Assert.AreEqual(0, drawList.mesh.vertexCount);
            Assert.AreEqual(new Vector2(128, 64), drawList.size);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void RendererWarmupRejectsMissingDrawCallback()
    {
        var renderer = new NowRenderer();

        try
        {
            Assert.Throws<ArgumentNullException>(() => renderer.Warmup(new Vector2(128, 64), null));
        }
        finally
        {
            renderer.Dispose();
        }
    }

    [Test]
    public void DrawListBuildCapturesGlassGeometryAndBatchData()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterialUGUI"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Glass(new NowRect(4, 6, 32, 20))
                    .SetBlurRadius(12f)
                    .SetVibrancy(1.25f, 0.9f)
                    .SetTint(new Color(1f, 1f, 1f, 0.2f))
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.AreEqual(1, drawList.mesh.subMeshCount);
            Assert.AreEqual(4, drawList.mesh.vertexCount);
            Assert.AreEqual(NowMeshKind.Glass, drawList.batches[0].kind);
            Assert.AreEqual(12f, drawList.batches[0].data.x, 0.0001f);
            Assert.AreEqual(1.25f, drawList.batches[0].data.y, 0.0001f);
            Assert.AreEqual(0.9f, drawList.batches[0].data.z, 0.0001f);
            Assert.AreEqual((float)NowGlassBlurQuality.Balanced, drawList.batches[0].data.w, 0.0001f);
            Assert.AreEqual(new NowRect(2, 4, 36, 24), drawList.batches[0].bounds);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void GlassPreservesDrawOrderAndSplitsDifferentBlurSettings()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            {
                Now.Rectangle(new NowRect(0, 0, 16, 16)).Draw();
                Now.Glass(new NowRect(16, 0, 16, 16)).SetBlurRadius(6f).Draw();
                Now.Glass(new NowRect(32, 0, 16, 16)).SetBlurRadius(18f).Draw();
                Now.Rectangle(new NowRect(48, 0, 16, 16)).Draw();
            }

            Assert.AreEqual(4, drawList.batchCount);
            Assert.AreEqual(NowMeshKind.Rectangle, drawList.batches[0].kind);
            Assert.AreEqual(NowMeshKind.Glass, drawList.batches[1].kind);
            Assert.AreEqual(NowMeshKind.Glass, drawList.batches[2].kind);
            Assert.AreEqual(NowMeshKind.Rectangle, drawList.batches[3].kind);
            Assert.AreEqual(6f, drawList.batches[1].data.x, 0.0001f);
            Assert.AreEqual(18f, drawList.batches[2].data.x, 0.0001f);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void GlassQualityCanUseHostDefaultAndPerPaneOverride()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64), NowGlassBlurQuality.High))
            {
                Now.Glass(new NowRect(0, 0, 16, 16)).Draw();
                Now.Glass(new NowRect(16, 0, 16, 16)).SetBlurQuality(NowGlassBlurQuality.Fast).Draw();
            }

            Assert.AreEqual(2, drawList.batchCount);
            Assert.AreEqual((float)NowGlassBlurQuality.High, drawList.batches[0].data.w, 0.0001f);
            Assert.AreEqual((float)NowGlassBlurQuality.Fast, drawList.batches[1].data.w, 0.0001f);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void RemovedTintOnlyQualityValueResolvesToBlurredDefault()
    {
        var oldTintOnlyValue = (NowGlassBlurQuality)1;
        Assert.AreEqual(NowGlassBlurQuality.Balanced, NowGlassSettings.Resolve(oldTintOnlyValue));

        var plan = NowGlassRenderer.GetBlurPlan(18f, 256, 128, oldTintOnlyValue);
        Assert.AreEqual(NowGlassBlurQuality.Balanced, plan.quality);
        Assert.Greater(plan.iterations, 0);
    }

    [Test]
    public void GlassBlurPlanUsesHigherQualitySettingsForUiRadii()
    {
        var medium = NowGlassRenderer.GetBlurPlan(18f, 512, 256);
        Assert.AreEqual(1, medium.downsample);
        Assert.AreEqual(512, medium.width);
        Assert.AreEqual(256, medium.height);
        Assert.GreaterOrEqual(medium.iterations, 4);

        var large = NowGlassRenderer.GetBlurPlan(36f, 512, 256);
        Assert.AreEqual(2, large.downsample);
        Assert.AreEqual(256, large.width);
        Assert.AreEqual(128, large.height);
        Assert.GreaterOrEqual(large.iterations, medium.iterations);

        var fast = NowGlassRenderer.GetBlurPlan(18f, 512, 256, NowGlassBlurQuality.Fast);
        var ultra = NowGlassRenderer.GetBlurPlan(18f, 512, 256, NowGlassBlurQuality.Ultra);
        Assert.LessOrEqual(fast.iterations, medium.iterations);
        Assert.Greater(ultra.iterations, medium.iterations);

        var tiny = NowGlassRenderer.GetBlurPlan(3f, 512, 256, NowGlassBlurQuality.Balanced);
        Assert.AreEqual(1, tiny.downsample);
        Assert.GreaterOrEqual(tiny.iterations, 2);
    }

    [Test]
    public void NestedDrawListCaptureRestoresAmbientMask()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var outer = new NowDrawList();
        var inner = new NowDrawList();

        try
        {
            using (outer.Begin(new Vector2(100, 100)))
            using (Now.Mask(new NowRect(0, 0, 20, 20)))
            {
                using (inner.Begin(new Vector2(100, 100)))
                {
                    Now.Rectangle(new NowRect(50, 50, 10, 10))
                        .SetColor(Color.white)
                        .Draw();
                }

                Now.Rectangle(new NowRect(50, 50, 10, 10))
                    .SetColor(Color.white)
                    .Draw();
            }

            Assert.IsTrue(inner.hasGeometry, "Inner capture should render without inheriting the outer mask.");
            Assert.IsFalse(outer.hasGeometry, "Outer mask should be restored after nested capture.");
        }
        finally
        {
            inner.Dispose();
            outer.Dispose();
        }
    }

    [Test]
    public void ModifierScopeCapturesAndAppendsMeshGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f)).Begin())
            {
                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.AreEqual(4, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void ModifierSubdivisionSplitsQuadGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f))
                       .SetSubdivision(4)
                       .Begin())
            {
                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(25, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void ModifierTextureBackendDrawsSubdividedSurface()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            using (NowEffects.Modifier(NowDeformers.Genie(new NowRect(80, 44, 20, 12), 0.5f))
                       .SetRenderToTexture()
                       .SetSubdivision(4)
                       .Begin())
            {
                Now.Rectangle(new NowRect(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(25, drawList.mesh.vertexCount);
            Assert.AreEqual(1, drawList.batchCount);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void SnapshotTextureSizeUsesUiScaleAndPixelSnappedBounds()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();
        float previousScale = Now.uiScale;
        NowSnapshotScope snapshot = default;

        try
        {
            Now.SetUIScale(2f);

            using (drawList.Begin(new Vector2(128, 64)))
            {
                snapshot = NowEffects.Snapshot(new NowRect(4.25f, 6.25f, 10.5f, 8.5f))
                    .SetId("pixel-snapped-snapshot")
                    .Begin();

                using (snapshot)
                {
                    Now.Rectangle(new NowRect(4.25f, 6.25f, 10.5f, 8.5f))
                        .SetColor(Color.white)
                        .Draw();
                }
            }

            Assert.NotNull(snapshot.texture);
            Assert.AreEqual(22, snapshot.texture.width);
            Assert.AreEqual(18, snapshot.texture.height);
            Assert.AreEqual(FilterMode.Bilinear, snapshot.texture.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, snapshot.texture.wrapMode);
        }
        finally
        {
            drawList.Dispose();
            Now.SetUIScale(previousScale);
        }
    }

    [Test]
    public void ModifierSubdivisionKeepsTextGlyphQuadsByDefault()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource missing.");

        var previousFont = Now.defaultFont;
        var drawList = new NowDrawList();

        try
        {
            Now.defaultFont = font;

            using (drawList.Begin(new Vector2(128, 64)))
            using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f))
                       .SetSubdivision(4)
                       .Begin())
            {
                Now.Text(new NowRect(4, 6, 80, 24), font)
                    .SetFontSize(18f)
                    .Draw("A");
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(4, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
            Now.defaultFont = previousFont;
        }
    }

    [Test]
    public void ModifierSubdivisionCanOptIntoTextGlyphSubdivision()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource missing.");

        var previousFont = Now.defaultFont;
        var drawList = new NowDrawList();

        try
        {
            Now.defaultFont = font;

            using (drawList.Begin(new Vector2(128, 64)))
            using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f))
                       .SetSubdivision(4)
                       .SetSubdivideText()
                       .Begin())
            {
                Now.Text(new NowRect(4, 6, 80, 24), font)
                    .SetFontSize(18f)
                    .Draw("A");
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(25, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
            Now.defaultFont = previousFont;
        }
    }

    [Test]
    public void TextBufferBuildsCompositeNumericLabel()
    {
        Span<char> chars = stackalloc char[32];
        var buffer = new NowTextBuffer(chars);

        buffer.Append("f(");
        buffer.Append(1.25f, "0.00");
        buffer.Append(") = ");
        buffer.Append(-0.5f, "0.00");

        Assert.IsFalse(buffer.truncated);
        Assert.AreEqual("f(1.25) = -0.50", buffer.span.ToString());
    }

    [Test]
    public void TextDrawsFormattedNumbers()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font, "Default font resource missing.");

        var previousFont = Now.defaultFont;
        var drawList = new NowDrawList();

        try
        {
            Now.defaultFont = font;

            using (drawList.Begin(new Vector2(128, 64)))
            {
                Now.Text(new NowRect(4f, 6f, 80f, 24f), font)
                    .SetFontSize(18f)
                    .Draw(42);

                Now.Text(new NowRect(4f, 30f, 80f, 24f), font)
                    .SetFontSize(18f)
                    .Draw(1.25f, "0.00");
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.Greater(drawList.mesh.vertexCount, 0);
        }
        finally
        {
            drawList.Dispose();
            Now.defaultFont = previousFont;
        }
    }

    [Test]
    public void ModifierScopeRunsControlsPassively()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var provider = new FakeProvider
        {
            snapshot = new NowInputSnapshot(new Vector2(30, 24), true, true, false)
        };
        var drawList = new NowDrawList();
        bool clicked;

        try
        {
            using (NowInput.Begin(provider, new Vector2(128, 64)))
            using (drawList.Begin(new Vector2(128, 64)))
            using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f)).Begin())
            {
                clicked = Now.Button(new NowRect(4, 6, 64, 28), "Save")
                    .SetId("passive-save")
                    .Draw();
            }

            Assert.IsFalse(clicked);
            Assert.AreEqual(0, NowInput.activeId);
        }
        finally
        {
            drawList.Dispose();
            NowInput.Reset();
            NowFocus.Reset();
            NowControlState.Reset();
            NowControls.Reset();
        }
    }

    [Test]
    public void DrawListBuildCapturesLineGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                Now.Line(new Vector2(8, 12), new Vector2(96, 48))
                    .SetWidth(4f)
                    .SetColor(Color.white)
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.AreEqual(1, drawList.mesh.subMeshCount);
            Assert.AreEqual(8, drawList.mesh.vertexCount);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void DrawListBuildCapturesBezierDashAndArrowGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(160, 100)))
                Now.Bezier(
                        new Vector2(12, 80),
                        new Vector2(40, 4),
                        new Vector2(108, 96),
                        new Vector2(144, 20))
                    .SetWidth(3f)
                    .SetDash(8f, 5f, 2f)
                    .SetArrow(NowLineArrow.End)
                    .SetColor(Color.white)
                    .Draw();

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.Greater(drawList.mesh.vertexCount, 8);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void DrawListBuildCapturesCoreShapeGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList();
        var polygon = new[]
        {
            new Vector2(68, 10),
            new Vector2(118, 34),
            new Vector2(96, 78),
            new Vector2(38, 78),
            new Vector2(18, 34)
        };

        try
        {
            using (drawList.Begin(new Vector2(140, 96)))
            {
                Now.Circle(new Vector2(34, 38), 20f)
                    .SetSegments(24)
                    .SetColor(Color.white)
                    .SetOutline(2f)
                    .SetOutlineColor(Color.black)
                    .Draw();

                Now.Polygon(polygon)
                    .SetColor(Color.white)
                    .Draw();
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(1, drawList.batchCount);
            Assert.Greater(drawList.mesh.vertexCount, polygon.Length);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void RendererBuildClearsGeometryWhenSizeIsInvalid()
    {
        var renderer = new NowRenderer();

        try
        {
            using (renderer.Begin(Vector2.zero))
                Now.Rectangle(new Vector4(4, 6, 32, 20)).Draw();

            Assert.IsFalse(renderer.hasGeometry);
            Assert.AreEqual(0, renderer.batchCount);
            Assert.AreEqual(0, renderer.mesh.vertexCount);
        }
        finally
        {
            renderer.Dispose();
        }
    }

    [Test]
    public void RendererDrawRejectsMissingCommandBuffer()
    {
        var renderer = new NowRenderer();

        try
        {
            Assert.Throws<ArgumentNullException>(() => renderer.Draw(null));
        }
        finally
        {
            renderer.Dispose();
        }
    }

    [Test]
    public void RendererStaticDrawRejectsMissingDrawList()
    {
        var commandBuffer = new UnityEngine.Rendering.CommandBuffer();

        try
        {
            Assert.Throws<ArgumentNullException>(() => NowRenderer.Draw(commandBuffer, null));
        }
        finally
        {
            commandBuffer.Release();
        }
    }

    [Test]
    public void RendererDrawWithoutTargetUsesSelfReplayForGlass()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassBlurMaterial"));

        var previousDiagnostics = NowGlassSettings.diagnosticsEnabled;
        NowGlassSettings.diagnosticsEnabled = true;
        var drawList = new NowDrawList();
        var commandBuffer = new UnityEngine.Rendering.CommandBuffer();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            {
                Now.Rectangle(new NowRect(0, 0, 128, 64))
                    .SetColor(Color.red)
                    .Draw();
                Now.Glass(new NowRect(16, 12, 64, 32))
                    .SetBlurRadius(12f)
                    .Draw();
            }

            NowRenderer.Draw(commandBuffer, drawList);

            var diagnostics = NowGlassSettings.lastFrameDiagnostics;
            Assert.IsTrue(DiagnosticsContainHost("NowRendererSelfReplay"));
            Assert.IsFalse(DiagnosticsContainFallback(NowGlassFallbackReason.MissingTargetContext));
        }
        finally
        {
            commandBuffer.Release();
            drawList.Dispose();
            NowGlassSettings.diagnosticsEnabled = previousDiagnostics;
        }
    }

    [Test]
    public void PipelineGraphicBuildsDrawListForTargetCamera()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var cameraObject = new GameObject("Now Test Camera");
        var graphicObject = new GameObject("Now Test Pipeline Graphic");
        var targetTexture = new RenderTexture(64, 32, 0);
        var drawList = new NowDrawList();
        Camera camera = null;

        try
        {
            camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = targetTexture;

            var graphic = graphicObject.AddComponent<TestPipelineGraphic>();
            graphic.targetCamera = camera;

            Assert.IsTrue(NowPipelineGraphic.BuildDrawList(camera, drawList));
            Assert.AreEqual(2, graphic.drawCount, "The default layout measure pass runs the UI twice per build.");
            Assert.IsTrue(drawList.hasGeometry);
            Assert.AreEqual(4, drawList.mesh.vertexCount, "The measure pass must not contribute geometry.");

            graphic.drawCount = 0;
            graphic.layoutMeasurePass = false;

            Assert.IsTrue(NowPipelineGraphic.BuildDrawList(camera, drawList));
            Assert.AreEqual(1, graphic.drawCount, "Disabling layoutMeasurePass skips the extra pass.");
            Assert.AreEqual(4, drawList.mesh.vertexCount);
        }
        finally
        {
            if (camera != null)
                camera.targetTexture = null;

            drawList.Dispose();
            targetTexture.Release();
            Object.DestroyImmediate(targetTexture);
            Object.DestroyImmediate(graphicObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void GraphicColorMultipliesCapturedCanvasContent()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var graphicObject = new GameObject("Now Test Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            var graphic = graphicObject.AddComponent<TestGraphic>();
            var tint = new Color(0.25f, 0.5f, 0.75f, 0.4f);
            graphic.color = tint;
            graphic.Rebuild(CanvasUpdate.PreRender);
            var readbackMesh = graphic.canvasRenderer.GetMesh();

            var colors = readbackMesh.colors;
            Assert.AreEqual(4, colors.Length);

            for (int i = 0; i < colors.Length; ++i)
            {
                Assert.AreEqual(tint.r, colors[i].r, 0.0001f);
                Assert.AreEqual(tint.g, colors[i].g, 0.0001f);
                Assert.AreEqual(tint.b, colors[i].b, 0.0001f);
                Assert.AreEqual(tint.a, colors[i].a, 0.0001f);
            }
        }
        finally
        {
            Object.DestroyImmediate(graphicObject);
        }
    }

    [Test]
    public void GraphicRectangleCanvasMeshPacksTextureAndRawUvs()
    {
        var graphicObject = new GameObject("Now Test Textured Graphic", typeof(RectTransform), typeof(CanvasRenderer));
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            var graphic = graphicObject.AddComponent<TexturedCanvasGraphic>();
            graphic.texture = texture;
            graphic.Rebuild(CanvasUpdate.PreRender);

            var mesh = graphic.canvasRenderer.GetMesh();
            Assert.NotNull(mesh);
            Assert.AreEqual(4, mesh.vertexCount);

            var uv0 = new System.Collections.Generic.List<Vector4>();
            var uv3 = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(0, uv0);
            mesh.GetUVs(3, uv3);

            Assert.AreEqual(4, uv0.Count);
            Assert.AreEqual(4, uv3.Count);

            Assert.AreEqual(0.1666667f, uv0[0].x, 0.0001f);
            Assert.AreEqual(0.4375f, uv0[0].y, 0.0001f);
            Assert.AreEqual(3f, uv0[0].z, 0.0001f);
            Assert.AreEqual(-0.1666667f, uv0[0].w, 0.0001f);
            Assert.AreEqual(0.8333333f, uv0[2].x, 0.0001f);
            Assert.AreEqual(0.8125f, uv0[2].y, 0.0001f);
            Assert.AreEqual(3f, uv0[2].z, 0.0001f);
            Assert.AreEqual(1.1666667f, uv0[2].w, 0.0001f);
            Assert.AreEqual(-0.25f, uv3[0].z, 0.0001f);
            Assert.AreEqual(1.25f, uv3[2].z, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(graphicObject);
        }
    }

    [Test]
    public void GraphicDrawUsesCanvasScaleFactor()
    {
        var canvasObject = new GameObject("Now Test Canvas", typeof(RectTransform), typeof(Canvas));
        var graphicObject = new GameObject("Now Test Scaled Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = 2f;

            graphicObject.transform.SetParent(canvasObject.transform, false);

            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            Now.SetUIScale(1f);

            var graphic = graphicObject.AddComponent<ScaleRecordingGraphic>();
            graphic.Rebuild(CanvasUpdate.PreRender);

            Assert.AreEqual(2f, graphic.recordedScale, 0.0001f);
            Assert.AreEqual(1f, Now.uiScale, 0.0001f);
        }
        finally
        {
            Now.SetUIScale(1f);
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void GraphicLayoutInputMeasuresPreferredSizeBeforeGeometryRebuild()
    {
        var graphicObject = new GameObject("Now Test Layout Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            var graphic = graphicObject.AddComponent<LayoutSizeGraphic>();

            graphic.CalculateLayoutInputHorizontal();
            graphic.CalculateLayoutInputVertical();

            Assert.AreEqual(120f, graphic.preferredWidth, 0.001f);
            Assert.AreEqual(35f, graphic.preferredHeight, 0.001f);
            Assert.AreEqual(new Vector2(120f, 35f), graphic.measuredContentSize);
            Assert.AreEqual(1, graphic.drawCount, "horizontal and vertical layout queries with the same rect should share the cached measurement");
            Assert.IsFalse(NowLayout.isMeasurePass);
            Assert.IsFalse(NowInput.isPassive);

            graphic.childWidth = 180f;
            graphic.SetVerticesDirty();
            graphic.CalculateLayoutInputHorizontal();

            Assert.AreEqual(180f, graphic.preferredWidth, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(graphicObject);
            NowLayout.Reset();
            NowInput.Reset();
        }
    }

    [Test]
    public void GraphicUsesStencilMaterialWhenUnderUnityMask()
    {
        var material = Resources.Load<Material>("NowUI/UIMaterialUGUI");
        Assert.NotNull(material);

        var canvasObject = new GameObject("Now Test Canvas", typeof(RectTransform), typeof(Canvas));
        var maskObject = new GameObject("Now Test Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        var graphicObject = new GameObject("Now Test Masked Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            maskObject.transform.SetParent(canvasObject.transform, false);
            graphicObject.transform.SetParent(maskObject.transform, false);

            var graphic = graphicObject.AddComponent<TestGraphic>();
            var modified = graphic.GetModifiedMaterial(material);

            Assert.NotNull(modified);
            Assert.AreNotSame(material, modified);
            Assert.AreEqual(1f, modified.GetFloat("_Stencil"), 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void UguiMaterialsExposeRectMaskSoftnessProperties()
    {
        AssertUguiMaskProperties(Resources.Load<Material>("NowUI/UIMaterialUGUI"));
        AssertUguiMaskProperties(Resources.Load<Material>("NowUI/GlassMaterialUGUI"));
        AssertUguiMaskProperties(Resources.Load<Material>("NowUI/TxtMaterialUGUI"));
        AssertUguiMaskProperties(Resources.Load<Material>("NowUI/TxtMaterialRGBAUGUI"));
    }

    [Test]
    public void GraphicUsesReplayBackedGlassMaterialAutomatically()
    {
        var canvasMaterial = Resources.Load<Material>("NowUI/GlassMaterialUGUI");
        Assert.NotNull(canvasMaterial);

        var graphicObject = new GameObject("Now Test Glass Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            var graphic = graphicObject.AddComponent<GlassGraphic>();
            graphic.Rebuild(CanvasUpdate.PreRender);

            var glassMaterial = graphic.canvasRenderer.GetMaterial(0);
            Assert.NotNull(glassMaterial);
            Assert.AreNotSame(canvasMaterial, glassMaterial);
            Assert.IsTrue(glassMaterial.HasProperty("_NowUGUIGlassUseBackdrop"));
            Assert.AreEqual(1f, glassMaterial.GetFloat("_NowUGUIGlassUseBackdrop"), 0.0001f);
            Assert.NotNull(glassMaterial.GetTexture("_NowUGUIBackdropTex"));

            Assert.AreEqual(1, graphic.uguiGlassDebugTextureCount);
            Assert.IsTrue(graphic.TryGetUGUIGlassDebugInfo(0, out var info));
            Assert.AreEqual(0, info.batchIndex);
            Assert.AreEqual(0, info.replayBatchCount);
            Assert.Greater(info.width, 0);
            Assert.Greater(info.height, 0);
            Assert.Less(info.width, 64);
            Assert.AreEqual(NowGlassBlurQuality.Balanced, info.blurQuality);
            Assert.AreEqual(NowGlassFallbackReason.None, info.fallbackReason);
            Assert.IsTrue(info.hasSourceTexture);
            Assert.IsTrue(info.hasBlurredTexture);
            Assert.NotNull(graphic.GetUGUIGlassDebugSourceTexture(0));
            Assert.NotNull(graphic.GetUGUIGlassDebugBlurredTexture(0));
        }
        finally
        {
            Object.DestroyImmediate(graphicObject);
        }
    }

    [Test]
    public void GraphicGlassDiagnosticsReportsUGUIReplay()
    {
        var previous = NowGlassSettings.diagnosticsEnabled;
        NowGlassSettings.diagnosticsEnabled = true;

        var graphicObject = new GameObject("Now Test Glass Diagnostics Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            var graphic = graphicObject.AddComponent<GlassGraphic>();
            graphic.Rebuild(CanvasUpdate.PreRender);

            var diagnostics = NowGlassSettings.lastFrameDiagnostics;
            Assert.GreaterOrEqual(diagnostics.paneCount, 1);
            Assert.Greater(diagnostics.blurredPixels, 0);
            Assert.IsTrue(DiagnosticsContainHost("UGUI"));
        }
        finally
        {
            NowGlassSettings.diagnosticsEnabled = previous;
            Object.DestroyImmediate(graphicObject);
        }
    }

    [Test]
    public void GlassDiagnosticsUseBoundedNonAllocEntryStorage()
    {
        var previous = NowGlassSettings.diagnosticsEnabled;
        int previousCapacity = NowGlassSettings.diagnosticEntryCapacity;
        NowGlassSettings.ReserveDiagnostics(1);
        NowGlassSettings.diagnosticsEnabled = true;

        var drawList = new NowDrawList();
        var commandBuffer = new UnityEngine.Rendering.CommandBuffer();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            {
                Now.Glass(new NowRect(8, 8, 32, 24))
                    .SetBlurRadius(8f)
                    .Draw();
                Now.Glass(new NowRect(48, 8, 32, 24))
                    .SetBlurRadius(16f)
                    .Draw();
            }

            NowRenderer.Draw(commandBuffer, drawList);

            var diagnostics = NowGlassSettings.lastFrameDiagnostics;
            Assert.GreaterOrEqual(diagnostics.paneCount, 2);
            Assert.AreEqual(1, diagnostics.entryCount);
            Assert.AreEqual(diagnostics.paneCount - diagnostics.entryCount, diagnostics.droppedEntryCount);
            Assert.IsTrue(NowGlassSettings.TryGetLastFrameDiagnostic(0, out var entry));
            Assert.AreEqual("NowRendererSelfReplay", entry.host);
            Assert.IsFalse(NowGlassSettings.TryGetLastFrameDiagnostic(1, out _));

            var copied = new NowGlassDiagnosticEntry[2];
            Assert.AreEqual(1, NowGlassSettings.CopyLastFrameDiagnosticsTo(copied));
            Assert.AreEqual(entry.host, copied[0].host);
        }
        finally
        {
            commandBuffer.Release();
            drawList.Dispose();
            NowGlassSettings.diagnosticsEnabled = previous;
            NowGlassSettings.ReserveDiagnostics(previousCapacity);
        }
    }

    [Test]
    public void CanvasDrawListAutomaticallyRetainsRenderReplayForUGUIGlass()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));

        var drawList = new NowDrawList(NowMeshLayout.Canvas, "Now Test Canvas Replay");

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            {
                Now.Rectangle(new NowRect(0, 0, 128, 64))
                    .SetColor(Color.red)
                    .Draw();
                Now.Glass(new NowRect(16, 12, 64, 32))
                    .SetBlurRadius(12f)
                    .Draw();
                Now.Rectangle(new NowRect(24, 20, 16, 16))
                    .SetColor(Color.blue)
                    .Draw();
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.IsTrue(drawList.hasRenderReplay);
            Assert.AreEqual(drawList.batchCount, drawList.renderReplayBatches.Count);
            Assert.AreEqual(drawList.mesh.vertexCount, drawList.renderReplayMesh.vertexCount);
            Assert.AreEqual(NowMeshKind.Glass, drawList.renderReplayBatches[1].kind);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void CanvasDrawListSkipsRenderReplayWithoutGlass()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowDrawList(NowMeshLayout.Canvas, "Now Test Canvas No Replay");

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
            {
                Now.Rectangle(new NowRect(0, 0, 128, 64))
                    .SetColor(Color.red)
                    .Draw();
                Now.Rectangle(new NowRect(24, 20, 16, 16))
                    .SetColor(Color.blue)
                    .Draw();
            }

            Assert.IsTrue(drawList.hasGeometry);
            Assert.IsFalse(drawList.hasRenderReplay);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test]
    public void GraphicCanUseReplayBackedGlassMaterial()
    {
        var canvasMaterial = Resources.Load<Material>("NowUI/GlassMaterialUGUI");
        Assert.NotNull(canvasMaterial);

        var graphicObject = new GameObject("Now Test Replay Glass Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 96f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 64f);

            var graphic = graphicObject.AddComponent<ReplayGlassGraphic>();
            graphic.Rebuild(CanvasUpdate.PreRender);

            var glassMaterial = graphic.canvasRenderer.GetMaterial(1);
            Assert.NotNull(glassMaterial);
            Assert.AreNotSame(canvasMaterial, glassMaterial);
            Assert.IsTrue(glassMaterial.HasProperty("_NowUGUIGlassUseBackdrop"));
            Assert.AreEqual(1f, glassMaterial.GetFloat("_NowUGUIGlassUseBackdrop"), 0.0001f);
            Assert.NotNull(glassMaterial.GetTexture("_NowUGUIBackdropTex"));

            Assert.AreEqual(1, graphic.uguiGlassDebugTextureCount);
            Assert.IsTrue(graphic.TryGetUGUIGlassDebugInfo(0, out var info));
            Assert.AreEqual(1, info.batchIndex);
            Assert.AreEqual(1, info.replayBatchCount);
            Assert.Greater(info.width, 0);
            Assert.Greater(info.height, 0);
            Assert.IsTrue(info.hasSourceTexture);
            Assert.IsTrue(info.hasBlurredTexture);
            Assert.NotNull(graphic.GetUGUIGlassDebugSourceTexture(0));
            Assert.NotNull(graphic.GetUGUIGlassDebugBlurredTexture(0));
        }
        finally
        {
            Object.DestroyImmediate(graphicObject);
        }
    }

    [Test]
    public void GraphicUsesCustomCanvasRectangleMaterial()
    {
        var baseMaterial = Resources.Load<Material>("NowUI/UIMaterial");
        var baseCanvasMaterial = Resources.Load<Material>("NowUI/UIMaterialUGUI");
        Assert.NotNull(baseMaterial);
        Assert.NotNull(baseCanvasMaterial);

        var material = new Material(baseMaterial);
        var canvasMaterial = new Material(baseCanvasMaterial);
        var graphicObject = new GameObject("Now Test Custom Material Graphic", typeof(RectTransform), typeof(CanvasRenderer));

        try
        {
            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 64f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);

            var graphic = graphicObject.AddComponent<CustomMaterialGraphic>();
            graphic.materialOverride = material;
            graphic.canvasMaterialOverride = canvasMaterial;
            graphic.Rebuild(CanvasUpdate.PreRender);

            Assert.AreSame(canvasMaterial, graphic.canvasRenderer.GetMaterial(0));
        }
        finally
        {
            Object.DestroyImmediate(graphicObject);
            Object.DestroyImmediate(canvasMaterial);
            Object.DestroyImmediate(material);
        }
    }

    static void AssertUguiMaskProperties(Material material)
    {
        Assert.NotNull(material);
        Assert.IsTrue(material.HasProperty("_ClipRect"));
        Assert.IsTrue(material.HasProperty("_UIMaskSoftnessX"));
        Assert.IsTrue(material.HasProperty("_UIMaskSoftnessY"));
        Assert.IsTrue(material.HasProperty("_UseUIAlphaClip"));
    }

    static bool DiagnosticsContainHost(string host)
    {
        var diagnostics = NowGlassSettings.lastFrameDiagnostics;

        for (int i = 0; i < diagnostics.entryCount; ++i)
        {
            if (NowGlassSettings.TryGetLastFrameDiagnostic(i, out var entry) &&
                entry.host == host)
            {
                return true;
            }
        }

        return false;
    }

    static bool DiagnosticsContainFallback(NowGlassFallbackReason fallback)
    {
        var diagnostics = NowGlassSettings.lastFrameDiagnostics;

        for (int i = 0; i < diagnostics.entryCount; ++i)
        {
            if (NowGlassSettings.TryGetLastFrameDiagnostic(i, out var entry) &&
                entry.fallbackReason == fallback)
            {
                return true;
            }
        }

        return false;
    }

    sealed class TestPipelineGraphic : NowPipelineGraphic
    {
        public int drawCount;

        protected override void DrawNowUI(Camera camera, Rect rect)
        {
            ++drawCount;

            Now.Rectangle(new Vector4(2, 2, 12, 8))
                .SetColor(Color.white)
                .Draw();
        }
    }

    sealed class TestGraphic : NowGraphic
    {
        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(new Vector4(2, 2, 12, 8))
                .SetColor(Color.white)
                .Draw();
        }
    }

    sealed class LayoutSizeGraphic : NowGraphic
    {
        public float childWidth = 120f;

        public int drawCount;

        protected override void DrawNowUI(NowRect rect)
        {
            ++drawCount;

            using (NowLayout.Area(new NowRect(0f, 0f, rect.width, rect.height)))
                NowLayout.Rect(childWidth, 35f);
        }
    }

    sealed class CustomMaterialGraphic : NowGraphic
    {
        public Material materialOverride;

        public Material canvasMaterialOverride;

        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(new Vector4(2, 2, 12, 8))
                .SetMaterial(materialOverride, canvasMaterialOverride)
                .Draw();
        }
    }

    sealed class TexturedCanvasGraphic : NowGraphic
    {
        public Texture texture;

        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(new NowRect(2, 2, 12, 8))
                .SetTexture(texture)
                .SetUV(new Vector4(0.25f, 0.5f, 0.5f, 0.25f))
                .SetRadius(4f, 2f, 1f, 3f)
                .Draw();
        }
    }

    sealed class GlassGraphic : NowGraphic
    {
        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Glass(new NowRect(2, 2, 12, 8))
                .SetBlurRadius(10f)
                .Draw();
        }
    }

    sealed class ReplayGlassGraphic : NowGraphic
    {
        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(new NowRect(0, 0, rect.width, rect.height))
                .SetColor(Color.red)
                .Draw();
            Now.Glass(new NowRect(8, 8, 44, 32))
                .SetBlurRadius(12f)
                .Draw();
            Now.Rectangle(new NowRect(56, 8, 24, 24))
                .SetColor(Color.blue)
                .Draw();
        }
    }

    sealed class ScaleRecordingGraphic : NowGraphic
    {
        public float recordedScale;

        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            recordedScale = Now.uiScale;
        }
    }
}
