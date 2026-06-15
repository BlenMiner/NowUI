using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;

public class NowImageRenderingTests
{
    static readonly Vector2 Surface = new Vector2(256, 256);

    NowDrawList _drawList;
    Texture2D _texture;
    Sprite _sprite;
    Material _material;
    Material _canvasMaterial;

    [SetUp]
    public void SetUp()
    {
        _drawList = new NowDrawList();
        _texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        _sprite = Sprite.Create(
            _texture,
            new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(8f, 8f, 8f, 8f));

        var baseMaterial = Resources.Load<Material>("NowUI/UIMaterial");
        if (baseMaterial != null)
            _material = new Material(baseMaterial);

        var baseCanvasMaterial = Resources.Load<Material>("NowUI/UIMaterialUGUI");
        if (baseCanvasMaterial != null)
            _canvasMaterial = new Material(baseCanvasMaterial);
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        Object.DestroyImmediate(_canvasMaterial);
        Object.DestroyImmediate(_material);
        Object.DestroyImmediate(_sprite);
        Object.DestroyImmediate(_texture);
    }

    [Test]
    public void TexturedRectEmitsGeometry()
    {
        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 100, 60)).SetTexture(_texture).Draw();

        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void SlicedSpriteEmitsNineQuads()
    {
        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 100, 60)).SetSprite(_sprite, sliced: true).Draw();

        var mesh = _drawList.GetCanvasMesh(0);
        Assert.NotNull(mesh);
        Assert.AreEqual(36, mesh.vertexCount, "a sliced sprite is nine quads");
    }

    [Test]
    public void SlicedSpriteCollapsesCellsWhenRectIsSmallerThanBorders()
    {
        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 12, 12)).SetSprite(_sprite, sliced: true).Draw();

        var mesh = _drawList.GetCanvasMesh(0);
        Assert.NotNull(mesh);
        Assert.GreaterOrEqual(mesh.vertexCount, 4);
        Assert.LessOrEqual(mesh.vertexCount, 36);
    }

    [Test]
    public void SpriteWithoutSlicingIsASingleQuad()
    {
        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 100, 60)).SetSprite(_sprite).Draw();

        var mesh = _drawList.GetCanvasMesh(0);
        Assert.NotNull(mesh);
        Assert.AreEqual(4, mesh.vertexCount);
    }

    [Test]
    public void PreserveAspectStillRendersGeometry()
    {
        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(0, 0, 200, 50)).SetTexture(_texture).SetPreserveAspect().Draw();

        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void CustomMaterialRectangleUsesCustomBatch()
    {
        Assert.NotNull(_material);

        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 100, 60)).SetMaterial(_material).Draw();

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreSame(_material, _drawList.batches[0].material);
        Assert.IsNull(_drawList.batches[0].canvasMaterial);
        Assert.AreEqual(NowMeshKind.CustomRectangle, _drawList.batches[0].kind);
    }

    [Test]
    public void CustomMaterialWithTextureUsesTexturedMaterialInstance()
    {
        Assert.NotNull(_material);

        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 100, 60))
                .SetMaterial(_material)
                .SetTexture(_texture)
                .Draw();

        var batch = _drawList.batches[0];
        Assert.AreEqual(NowMeshKind.CustomRectangle, batch.kind);
        Assert.AreNotSame(_material, batch.material);
        Assert.AreSame(_texture, batch.material.mainTexture);
    }

    [Test]
    public void CustomMaterialCanvasOverrideIsCaptured()
    {
        Assert.NotNull(_material);
        Assert.NotNull(_canvasMaterial);

        using (_drawList.Begin(Surface))
            Now.Rectangle(new NowRect(10, 10, 100, 60))
                .SetMaterial(_material, _canvasMaterial)
                .Draw();

        var batch = _drawList.batches[0];
        Assert.AreSame(_material, batch.material);
        Assert.AreSame(_canvasMaterial, batch.canvasMaterial);
        Assert.AreEqual(NowMeshKind.CustomRectangle, batch.kind);
    }
}
