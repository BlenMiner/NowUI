using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Internal;

public class NowMaskTextureTests
{
    [Test]
    public void FactoriesSelectCoverageChannelAndPreserveBounds()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var bounds = new NowRect(10f, 20f, 30f, 40f);

        try
        {
            var alpha = NowMaskTexture.Alpha(texture, bounds);
            var red = NowMaskTexture.Red(texture, bounds).SetInverted();

            Assert.AreSame(texture, alpha.texture);
            Assert.AreEqual(bounds, alpha.bounds);
            Assert.AreEqual(NowMaskTextureChannel.Alpha, alpha.channel);
            Assert.IsFalse(alpha.inverted);
            Assert.IsFalse(alpha.isEmpty);

            Assert.AreSame(texture, red.texture);
            Assert.AreEqual(bounds, red.bounds);
            Assert.AreEqual(NowMaskTextureChannel.Red, red.channel);
            Assert.IsTrue(red.inverted);
            Assert.IsFalse(red.isEmpty);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void InvalidBoundsAreSanitizedAndCannotReachShaderState()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);

        try
        {
            var invalidMasks = new[]
            {
                new NowMaskTexture(texture, new NowRect(float.NaN, 0f, 10f, 10f)),
                new NowMaskTexture(texture, new NowRect(0f, float.PositiveInfinity, 10f, 10f)),
                new NowMaskTexture(texture, new NowRect(0f, 0f, -1f, 10f)),
                new NowMaskTexture(texture, new NowRect(0f, 0f, 10f, -1f))
            };

            for (int i = 0; i < invalidMasks.Length; ++i)
            {
                var mask = invalidMasks[i];
                Assert.IsTrue(mask.isEmpty, $"Invalid mask {i} was not empty.");
                Assert.AreEqual(default(NowRect), mask.bounds, $"Invalid mask {i} retained unsafe bounds.");

                using (Now.Mask(mask))
                {
                    var state = Now.CaptureMaskShaderState();
                    Assert.AreEqual(1, state.textureCount);
                    var descriptor = state.GetTexture(0);
                    Assert.AreEqual(Vector4.zero, descriptor.rect);
                    Assert.AreEqual(0f, descriptor.parameters.z);
                    Assert.IsFalse(Now.IsInsideAmbientMask(Vector2.zero));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void MissingOrDestroyedTextureCullsEvenWhenInverted()
    {
        var bounds = new NowRect(0f, 0f, 20f, 20f);
        var missing = NowMaskTexture.Empty(bounds).SetInverted();

        using (Now.Mask(missing))
        {
            Assert.IsFalse(Now.IsInsideAmbientMask(bounds.center));
            var descriptor = Now.CaptureMaskShaderState().GetTexture(0);
            Assert.AreEqual(0f, descriptor.parameters.z);
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        var destroyed = NowMaskTexture.Alpha(texture, bounds).SetInverted();
        Object.DestroyImmediate(texture);

        Assert.IsTrue(destroyed.isEmpty);
        using (Now.Mask(destroyed))
        {
            Assert.IsFalse(Now.IsInsideAmbientMask(bounds.center));
            var descriptor = Now.CaptureMaskShaderState().GetTexture(0);
            Assert.AreEqual(0f, descriptor.parameters.z);
        }
    }

    [Test]
    public void TextureDestroyedAfterCaptureIsInvalidatedAtBinding()
    {
        const string shaderName = "NowUI/UI Rectangle";
        const string textureProperty = "_NowUITextureMask0";
        const string parametersProperty = "_NowUITextureMaskParams";
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        var mask = NowMaskTexture.Alpha(texture, new NowRect(0f, 0f, 20f, 20f))
            .SetInverted();
        NowMaskShaderState state;

        using (Now.Mask(mask))
        {
            state = Now.CaptureMaskShaderState();
            Assert.AreEqual(1f, state.GetTexture(0).parameters.z);
        }

        Object.DestroyImmediate(texture);

        var propertyBlock = NowMaskShader.GetPropertyBlock(state);
        var blockParameters = propertyBlock.GetVectorArray(parametersProperty);
        Assert.AreEqual(0f, blockParameters[0].z);
        Assert.AreSame(Texture2D.blackTexture, propertyBlock.GetTexture(textureProperty));

        var shader = Shader.Find(shaderName);
        Assert.IsNotNull(shader, $"Missing test shader '{shaderName}'.");
        var material = new Material(shader);

        try
        {
            NowMaskShader.Apply(material, state);
            var materialParameters = material.GetVectorArray(parametersProperty);
            Assert.AreEqual(0f, materialParameters[0].z);
            Assert.AreSame(Texture2D.blackTexture, material.GetTexture(textureProperty));
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void UnknownChannelFallsBackToAlpha()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);

        try
        {
            var mask = new NowMaskTexture(
                texture,
                new NowRect(0f, 0f, 10f, 10f),
                (NowMaskTextureChannel)255);

            Assert.AreEqual(NowMaskTextureChannel.Alpha, mask.channel);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
