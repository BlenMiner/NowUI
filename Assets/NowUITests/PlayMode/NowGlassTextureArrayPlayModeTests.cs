using NUnit.Framework;
using NowUI;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed class NowGlassTextureArrayPlayModeTests
{
    const int Width = 64;
    const int Height = 32;

    [Test]
    public void BlurPreservesDistinctTextureArraySlices()
    {
        if (!SystemInfo.supports2DArrayTextures)
            Assert.Ignore("The active graphics device does not support 2D texture arrays.");

        const CopyTextureSupport requiredCopySupport =
            CopyTextureSupport.TextureToRT |
            CopyTextureSupport.DifferentTypes;
        if ((SystemInfo.copyTextureSupport & requiredCopySupport) != requiredCopySupport)
        {
            Assert.Ignore(
                "The active graphics device cannot upload a Texture2D to a RenderTexture array " +
                "and read an array slice into a 2D RenderTexture.");
        }

        var sourceDescriptor = new RenderTextureDescriptor(
            Width,
            Height,
            RenderTextureFormat.ARGB32,
            0)
        {
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = VRTextureUsage.TwoEyes,
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false
        };
        NowGlassTextureLayout layout = NowGlassTextureLayout.FromDescriptor(sourceDescriptor);
        RenderTexture source = null;
        RenderTexture destination = null;
        Texture2D redPattern = null;
        Texture2D bluePattern = null;
        CommandBuffer commandBuffer = null;

        try
        {
            source = NowGlassBackdropSurface.CreateTexture(Width, Height, "Now Glass Array Test Source", layout);
            destination = NowGlassBackdropSurface.CreateTexture(Width, Height, "Now Glass Array Test Destination", layout);
            redPattern = CreateSplitPattern(new Color32(255, 0, 0, 255));
            bluePattern = CreateSplitPattern(new Color32(0, 0, 255, 255));

            Graphics.CopyTexture(redPattern, 0, 0, source, 0, 0);
            Graphics.CopyTexture(bluePattern, 0, 0, source, 1, 0);

            commandBuffer = new CommandBuffer { name = "Now Glass texture-array blur regression" };
            bool blurAvailable = NowGlassRenderer.CopyAndBlurBackdrop(
                commandBuffer,
                source,
                destination,
                Width,
                Height,
                8f,
                NowGlassBlurQuality.Balanced,
                "TextureArrayTest",
                new NowRect(0f, 0f, Width, Height),
                layout,
                out NowGlassBlurPlan plan);

            Assert.IsTrue(blurAvailable, "The packaged glass blur material did not load.");
            Assert.Greater(plan.iterations, 0, "The test must execute the blur passes, not only the copy path.");
            Graphics.ExecuteCommandBuffer(commandBuffer);

            Color32[] redSlice = ReadSlice(destination, 0);
            Color32[] blueSlice = ReadSlice(destination, 1);
            Color32 redInterior = Pixel(redSlice, Width / 4, Height / 2);
            Color32 blueInterior = Pixel(blueSlice, Width / 4, Height / 2);
            Color32 redEdge = Pixel(redSlice, Width / 2, Height / 2);
            Color32 blueEdge = Pixel(blueSlice, Width / 2, Height / 2);
            Color32 redExterior = Pixel(redSlice, Width * 3 / 4, Height / 2);
            Color32 blueExterior = Pixel(blueSlice, Width * 3 / 4, Height / 2);

            AssertPrimary(redInterior.r, redInterior.g, redInterior.b, "slice 0 red interior");
            AssertPrimary(blueInterior.b, blueInterior.r, blueInterior.g, "slice 1 blue interior");
            AssertBlurredPrimary(redEdge.r, redEdge.g, redEdge.b, "slice 0 red edge");
            AssertBlurredPrimary(blueEdge.b, blueEdge.r, blueEdge.g, "slice 1 blue edge");
            AssertDark(redExterior, "slice 0 exterior");
            AssertDark(blueExterior, "slice 1 exterior");
        }
        finally
        {
            commandBuffer?.Release();
            Release(source);
            Release(destination);
            Destroy(redPattern);
            Destroy(bluePattern);
        }
    }

    [Test]
    public void CopyResolvesMultisampledTextureArrayWithoutCollapsingSlices()
    {
        if (!SystemInfo.supports2DArrayTextures)
            Assert.Ignore("The active graphics device does not support 2D texture arrays.");

        if ((SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) == 0)
        {
            Assert.Ignore(
                "The active graphics device cannot copy a RenderTexture array slice " +
                "into a 2D RenderTexture for validation.");
        }

        var sourceDescriptor = new RenderTextureDescriptor(
            Width,
            Height,
            RenderTextureFormat.ARGB32,
            0)
        {
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = VRTextureUsage.TwoEyes,
            msaaSamples = 2,
            bindMS = true,
            useMipMap = false,
            autoGenerateMips = false
        };
        NowGlassTextureLayout layout = NowGlassTextureLayout.FromDescriptor(sourceDescriptor);
        RenderTexture source = null;
        RenderTexture destination = null;
        CommandBuffer commandBuffer = null;

        try
        {
            source = new RenderTexture(sourceDescriptor)
            {
                name = "Now Glass MSAA Array Test Source"
            };

            if (!source.Create() || source.antiAliasing <= 1)
                Assert.Ignore("The active graphics device cannot create a multisampled 2D texture array.");

            destination = NowGlassBackdropSurface.CreateTexture(
                Width,
                Height,
                "Now Glass MSAA Array Test Destination",
                layout);
            commandBuffer = new CommandBuffer { name = "Now Glass texture-array MSAA resolve regression" };
            commandBuffer.SetRenderTarget(source, 0, CubemapFace.Unknown, 0);
            commandBuffer.ClearRenderTarget(false, true, Color.red);
            commandBuffer.SetRenderTarget(source, 0, CubemapFace.Unknown, 1);
            commandBuffer.ClearRenderTarget(false, true, Color.blue);

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

            Color32 red = Pixel(ReadSlice(destination, 0), Width / 2, Height / 2);
            Color32 blue = Pixel(ReadSlice(destination, 1), Width / 2, Height / 2);
            AssertPrimary(red.r, red.g, red.b, "resolved slice 0 red");
            AssertPrimary(blue.b, blue.r, blue.g, "resolved slice 1 blue");
        }
        finally
        {
            commandBuffer?.Release();
            Release(source);
            Release(destination);
        }
    }

    static Texture2D CreateSplitPattern(Color32 primary)
    {
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true)
        {
            name = "Now Glass Array Test Pattern"
        };
        var pixels = new Color32[Width * Height];

        for (int y = 0; y < Height; ++y)
        {
            for (int x = 0; x < Width / 2; ++x)
                pixels[y * Width + x] = primary;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    static Color32[] ReadSlice(RenderTexture source, int slice)
    {
        var descriptor = new RenderTextureDescriptor(
            source.width,
            source.height,
            RenderTextureFormat.ARGB32,
            0)
        {
            dimension = TextureDimension.Tex2D,
            volumeDepth = 1,
            msaaSamples = 1
        };
        var flat = new RenderTexture(descriptor);
        Texture2D readback = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            flat.Create();
            Graphics.CopyTexture(source, slice, 0, flat, 0, 0);
            RenderTexture.active = flat;
            readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            readback.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            readback.Apply(false, false);
            return readback.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            flat.Release();
            Destroy(flat);
            Destroy(readback);
        }
    }

    static Color32 Pixel(Color32[] pixels, int x, int y)
    {
        return pixels[y * Width + x];
    }

    static void AssertPrimary(byte primary, byte otherA, byte otherB, string label)
    {
        Assert.Greater(primary, 220, label);
        Assert.Less(otherA, 20, label);
        Assert.Less(otherB, 20, label);
    }

    static void AssertBlurredPrimary(byte primary, byte otherA, byte otherB, string label)
    {
        Assert.That((int)primary, Is.InRange(20, 235), label);
        Assert.Less(otherA, 20, label);
        Assert.Less(otherB, 20, label);
    }

    static void AssertDark(Color32 color, string label)
    {
        Assert.Less(color.r, 20, label);
        Assert.Less(color.g, 20, label);
        Assert.Less(color.b, 20, label);
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
