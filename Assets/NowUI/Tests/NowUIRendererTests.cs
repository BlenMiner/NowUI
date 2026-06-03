using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class NowUIRendererTests
{
    [Test]
    public void DrawListBuildCapturesRectangleGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var drawList = new NowUIDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                NowUI.Rectangle(new Vector4(4, 6, 32, 20))
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
        var drawList = new NowUIDrawList();

        try
        {
            using (drawList.Begin(new Vector2(128, 64)))
                NowUI.Rectangle(new Vector4(4, 6, 32, 20)).Draw();

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

        var renderer = new NowUIRenderer();

        try
        {
            using (renderer.Begin(new Vector2(128, 64)))
                NowUI.Rectangle(new Vector4(4, 6, 32, 20))
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
        var renderer = new NowUIRenderer();

        try
        {
            using (renderer.Begin(Vector2.zero))
                NowUI.Rectangle(new Vector4(4, 6, 32, 20)).Draw();

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
        var renderer = new NowUIRenderer();

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
            Assert.Throws<ArgumentNullException>(() => NowUIRenderer.Draw(commandBuffer, null));
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
        var drawList = new NowUIDrawList();
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

    sealed class TestPipelineGraphic : NowUIPipelineGraphic
    {
        public int drawCount;

        protected override void DrawNowUI(Camera camera, Rect rect)
        {
            ++drawCount;

            NowUI.Rectangle(new Vector4(2, 2, 12, 8))
                .SetColor(Color.white)
                .Draw();
        }
    }
}
