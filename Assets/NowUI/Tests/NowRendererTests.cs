using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using NowUI;

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
            Assert.AreEqual(1, graphic.drawCount);
            Assert.IsTrue(drawList.hasGeometry);
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
        AssertUguiMaskProperties(Resources.Load<Material>("NowUI/TxtMaterialUGUI"));
        AssertUguiMaskProperties(Resources.Load<Material>("NowUI/TxtMaterialRGBAUGUI"));
    }

    static void AssertUguiMaskProperties(Material material)
    {
        Assert.NotNull(material);
        Assert.IsTrue(material.HasProperty("_ClipRect"));
        Assert.IsTrue(material.HasProperty("_UIMaskSoftnessX"));
        Assert.IsTrue(material.HasProperty("_UIMaskSoftnessY"));
        Assert.IsTrue(material.HasProperty("_UseUIAlphaClip"));
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
