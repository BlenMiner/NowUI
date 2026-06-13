using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using NowUI;

public class NowRendererTests
{
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

        var cameraObject = new GameObject("NowUI Test Camera");
        var graphicObject = new GameObject("NowUI Test Pipeline Graphic");
        var targetTexture = new RenderTexture(64, 32, 0);
        var drawList = new NowDrawList();
        Camera camera = null;

        try
        {
            camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = targetTexture;

            var graphic = graphicObject.AddComponent<TestPipelineGraphic>();
            graphic.targetCamera = camera;

            Assert.IsTrue(NowUIPipelineGraphic.BuildDrawList(camera, drawList));
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

        var graphicObject = new GameObject("NowUI Test Graphic", typeof(RectTransform), typeof(CanvasRenderer));

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
    public void GraphicUsesStencilMaterialWhenUnderUnityMask()
    {
        var material = Resources.Load<Material>("NowUI/UIMaterialUGUI");
        Assert.NotNull(material);

        var canvasObject = new GameObject("NowUI Test Canvas", typeof(RectTransform), typeof(Canvas));
        var maskObject = new GameObject("NowUI Test Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        var graphicObject = new GameObject("NowUI Test Masked Graphic", typeof(RectTransform), typeof(CanvasRenderer));

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

    sealed class TestPipelineGraphic : NowUIPipelineGraphic
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
}
