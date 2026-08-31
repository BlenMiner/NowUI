using NUnit.Framework;
using NowUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class NowGlassTexture2DPlayModeTests
{
    const int Width = 64;
    const int Height = 32;

    [Test]
    public void CopyResolvesBoundMultisampledTexture2DWithoutSamplerWarning()
    {
        var sourceDescriptor = new RenderTextureDescriptor(
            Width,
            Height,
            RenderTextureFormat.ARGB32,
            0)
        {
            dimension = TextureDimension.Tex2D,
            volumeDepth = 1,
            msaaSamples = 2,
            bindMS = true,
            useMipMap = false,
            autoGenerateMips = false
        };
        RenderTexture source = null;
        RenderTexture destination = null;
        Texture2D readback = null;
        CommandBuffer commandBuffer = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            source = new RenderTexture(sourceDescriptor)
            {
                name = "Now Glass MSAA 2D Test Source"
            };

            if (!source.Create() || source.antiAliasing <= 1)
                Assert.Ignore("The active graphics device cannot create a bound multisampled 2D texture.");

            NowGlassTextureLayout layout = NowGlassTextureLayout.FromDescriptor(source.descriptor);
            Assert.IsFalse(layout.isArray);
            Assert.IsTrue(layout.sourceRequiresExplicitResolve);

            destination = NowGlassBackdropSurface.CreateTexture(
                Width,
                Height,
                "Now Glass MSAA 2D Test Destination",
                layout);
            commandBuffer = new CommandBuffer { name = "Now Glass 2D MSAA resolve regression" };
            commandBuffer.SetRenderTarget(source);
            commandBuffer.ClearRenderTarget(false, true, Color.red);

            bool copyAvailable = NowGlassRenderer.CopyBackdropRegion(
                commandBuffer,
                source,
                destination,
                Width,
                Height,
                new Vector4(1f, 1f, 0f, 0f),
                layout);

            Assert.IsTrue(copyAvailable, "The packaged glass copy material did not load.");
            Graphics.ExecuteCommandBuffer(commandBuffer);
            LogAssert.NoUnexpectedReceived();

            RenderTexture.active = destination;
            readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
            readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            readback.Apply(false, false);
            Color32 center = readback.GetPixel(Width / 2, Height / 2);

            Assert.Greater(center.r, 220, "The resolved texture should retain the red MSAA source.");
            Assert.Less(center.g, 20);
            Assert.Less(center.b, 20);
        }
        finally
        {
            RenderTexture.active = previous;
            commandBuffer?.Release();
            Release(source);
            Release(destination);
            Destroy(readback);
        }
    }

    static void Release(RenderTexture texture)
    {
        if (texture == null)
            return;

        texture.Release();
        Destroy(texture);
    }

    static void Destroy(Object value)
    {
        if (value != null)
            Object.DestroyImmediate(value);
    }
}
