using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;

public class NowUIImageRenderingTests
{
    static readonly Vector2 Surface = new Vector2(256, 256);

    NowDrawList _drawList;
    Texture2D _texture;
    Sprite _sprite;

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
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
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
}
