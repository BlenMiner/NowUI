using System;
using NUnit.Framework;
using UnityEngine;

public class NowUIRendererTests
{
    [Test]
    public void RendererBuildCapturesRectangleGeometry()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));

        var renderer = new NowUIRenderer();

        try
        {
            renderer.Build(new Vector2(128, 64), _ =>
            {
                NowUI.Rectangle(new Vector4(4, 6, 32, 20))
                    .SetColor(Color.white)
                    .Draw();
            });

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
            renderer.Build(Vector2.zero, _ =>
            {
                NowUI.Rectangle(new Vector4(4, 6, 32, 20)).Draw();
            });

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
}
