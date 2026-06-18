using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;
using NowUI.Sdf;

public class NowSdfTests
{
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowSdf.Reset();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowSdf.Reset();
    }

    [Test]
    public void SdfSceneEmitsSingleMaterialQuad()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/SdfMaterial"));

        using (_drawList.Begin(new Vector2(160, 100)))
        {
            NowSdf.Scene(new NowRect(0, 0, 160, 100))
                .SetColor(Color.red)
                .Circle(new Vector2(48, 50), 34)
                .SetColor(Color.cyan)
                .SmoothUnion(12)
                .RoundedBox(new NowRect(44, 22, 92, 56), 14)
                .Subtract()
                .Circle(new Vector2(88, 50), 16)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(NowMeshKind.Sdf, _drawList.batches[0].kind);
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
    }

    [Test]
    public void SdfSceneBindsTextureToMaterial()
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);

        try
        {
            using (_drawList.Begin(new Vector2(80, 80)))
            {
                NowSdf.Scene(new NowRect(0, 0, 80, 80))
                    .SetTexture(texture)
                    .RoundedBox(new NowRect(8, 8, 64, 64), 12)
                    .Draw();
            }

            Assert.AreSame(texture, _drawList.batches[0].material.mainTexture);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SdfSceneUploadsEffectSettings()
    {
        using (_drawList.Begin(new Vector2(120, 90)))
        {
            NowSdf.Scene(new NowRect(0, 0, 120, 90))
                .SetColor(Color.white)
                .SetOutline(4f, new Color(0f, 0f, 0f, 0.75f), 1.5f)
                .SetGlow(18f, new Color(0.2f, 0.7f, 1f, 0.45f), 2f)
                .SetShadow(new Vector2(6f, 8f), 12f, new Color(0f, 0f, 0f, 0.35f), 2f)
                .SetInnerShadow(new Vector2(-3f, -4f), 7f, new Color(0f, 0f, 0f, 0.28f), 1f)
                .SetEmboss(new Vector2(-1f, -1f), 0.4f, 5f)
                .SetContours(10f, 1.5f, new Color(1f, 1f, 1f, 0.2f), 3f, 2)
                .SetContourMask(new Vector2(48f, 36f), 22f, 6f)
                .SetWarp(3f, 42f, 0.6f, 9f)
                .RoundedBox(new NowRect(16, 18, 88, 54), 18)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(new Vector4(4f, 1.5f, 0f, 0f), material.GetVector("_SdfOutline"));
        Assert.AreEqual(new Vector4(18f, 2f, 0f, 0f), material.GetVector("_SdfGlow"));
        Assert.AreEqual(new Vector4(6f, 8f, 12f, 2f), material.GetVector("_SdfShadow"));
        Assert.AreEqual(new Vector4(-3f, -4f, 7f, 1f), material.GetVector("_SdfInnerShadow"));
        Assert.AreEqual(new Vector4(10f, 1.5f, 3f, 2f), material.GetVector("_SdfContour"));
        Assert.AreEqual(new Vector4(48f, 36f, 22f, 6f), material.GetVector("_SdfContourMask"));
        Assert.AreEqual(new Vector4(3f, 42f, 0.6f, 9f), material.GetVector("_SdfWarp"));

        var emboss = material.GetVector("_SdfEmboss");
        Assert.AreEqual(5f, emboss.z, 0.0001f);
        Assert.AreEqual(0.4f, emboss.w, 0.0001f);
        Assert.AreEqual(1f, new Vector2(emboss.x, emboss.y).magnitude, 0.0001f);
    }

    [Test]
    public void SdfSceneCanUseTextAsOperationOperand()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font);

        using (_drawList.Begin(new Vector2(180, 96)))
        {
            NowSdf.Scene(new NowRect(0, 0, 180, 96))
                .SetColor(Color.blue)
                .RoundedBox(new NowRect(16, 18, 148, 60), 18)
                .SmoothSubtract(3f)
                .Text(new Vector2(48, 28), "SDF", font, 32, NowFontStyle.Bold)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(1, _drawList.batchCount);

        var material = _drawList.batches[0].material;
        Assert.NotNull(material.mainTexture);
        Assert.AreEqual(1f, material.GetFloat("_SdfLayerCount"), 0.0001f);
        Assert.AreEqual(4f, material.GetFloat("_SdfShapeCount"), 0.0001f);

        var shapeData = material.GetVectorArray("_SdfData0");
        Assert.AreEqual((float)NowSdfOperation.SmoothSubtract, shapeData[1].y, 0.0001f);
        Assert.AreEqual((float)NowSdfOperation.SmoothSubtract, shapeData[2].y, 0.0001f);
        Assert.AreEqual((float)NowSdfOperation.SmoothSubtract, shapeData[3].y, 0.0001f);
    }

    [Test]
    public void SdfSceneComposesReusableGraphs()
    {
        var a = NowSdf.Graph()
            .SetColor(Color.red)
            .Circle(new Vector2(42, 42), 34);
        var b = NowSdf.Graph()
            .SetColor(Color.blue)
            .RoundedBox(new NowRect(24, 18, 74, 48), 12);

        using (_drawList.Begin(new Vector2(120, 90)))
        {
            NowSdf.Scene(new NowRect(0, 0, 120, 90))
                .Graph(a)
                .Subtract()
                .Graph(b)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(2f, material.GetFloat("_SdfLayerCount"), 0.0001f);
        Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f);
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
    }

    [Test]
    public void SdfSceneMorphUploadsBothGraphsAsOneLayer()
    {
        var from = NowSdf.Graph()
            .SetColor(Color.magenta)
            .Circle(new Vector2(45, 45), 36);
        var to = NowSdf.Graph()
            .SetColor(Color.yellow)
            .Capsule(new NowRect(16, 24, 92, 42));

        using (_drawList.Begin(new Vector2(128, 90)))
        {
            NowSdf.Scene(new NowRect(0, 0, 128, 90))
                .Morph(from, to, 0.5f)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(1f, material.GetFloat("_SdfLayerCount"), 0.0001f);
        Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f);
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
    }
}
