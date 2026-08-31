using NUnit.Framework;
using NowUI;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowGlassStereoLayoutTests
{
    [TestCase(true, false, true, true, 4, true)]
    [TestCase(true, false, true, true, 1, false)]
    [TestCase(true, false, true, false, 4, false)]
    [TestCase(true, false, false, true, 4, false)]
    [TestCase(true, true, true, true, 4, false)]
    [TestCase(false, false, true, true, 4, false)]
    public void BuiltinXrMsaaFallbackRequiresTheLiveUnresolvedCameraTarget(
        bool isBuiltInPipeline,
        bool hasExplicitTargetTexture,
        bool stereoEnabled,
        bool xrDisplayActive,
        int msaaSamples,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            NowWorldGlassBackdrop.RequiresBuiltinXrMsaaFallback(
                isBuiltInPipeline,
                hasExplicitTargetTexture,
                stereoEnabled,
                xrDisplayActive,
                msaaSamples));
    }

    [TestCase(true, TextureDimension.Tex2D, true)]
    [TestCase(true, TextureDimension.Tex2DArray, false)]
    [TestCase(false, TextureDimension.Tex2D, false)]
    public void BuiltinXrColorOverrideOnlyAppliesToTheFlatMultiPassIntermediate(
        bool requiresXrMsaaFallback,
        TextureDimension sourceDimension,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            NowWorldGlassBackdrop.RequiresBuiltinXrColorMsaaOverride(
                requiresXrMsaaFallback,
                sourceDimension));
    }

    [TestCase(true, RenderingPath.Forward, 1, true)]
    [TestCase(true, RenderingPath.VertexLit, 1, true)]
    [TestCase(false, RenderingPath.Forward, 1, false)]
    [TestCase(true, RenderingPath.Forward, 0, false)]
    [TestCase(true, (RenderingPath)2, 1, false)]
    [TestCase(true, RenderingPath.DeferredShading, 1, false)]
    public void BuiltinCameraMsaaRequiresACompatibleRenderingPathAndDevice(
        bool allowMsaa,
        RenderingPath actualRenderingPath,
        int supportedMultisampledTextureCount,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            NowWorldGlassBackdrop.RenderingPathSupportsMsaa(
                allowMsaa,
                actualRenderingPath,
                supportedMultisampledTextureCount));
    }

    [Test]
    public void XrBackdropKeepsBothArraySlicesButDropsSourceMsaa()
    {
        var source = new RenderTextureDescriptor(
            640,
            480,
            RenderTextureFormat.ARGB32,
            24)
        {
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = VRTextureUsage.TwoEyes,
            msaaSamples = 4,
            bindMS = true,
            useMipMap = true,
            autoGenerateMips = true
        };

        NowGlassTextureLayout layout = NowGlassTextureLayout.FromDescriptor(source);
        RenderTextureDescriptor backdrop = NowGlassBackdropSurface.CreateDescriptor(
            source.width,
            source.height,
            layout);

        Assert.IsTrue(layout.isArray);
        Assert.AreEqual(2, layout.sliceCount);
        Assert.AreEqual(4, layout.sourceMsaaSamples);
        Assert.IsTrue(layout.sourceIsMultisampled);
        Assert.IsTrue(layout.sourceBindMS);
        Assert.IsTrue(layout.sourceRequiresExplicitResolve);

        Assert.AreEqual(source.width, backdrop.width);
        Assert.AreEqual(source.height, backdrop.height);
        Assert.AreEqual(TextureDimension.Tex2DArray, backdrop.dimension);
        Assert.AreEqual(2, backdrop.volumeDepth);
        Assert.AreEqual(VRTextureUsage.TwoEyes, backdrop.vrUsage);
        Assert.AreEqual(1, backdrop.msaaSamples, "A sampled glass backdrop must resolve the camera's MSAA surface.");
        Assert.IsFalse(backdrop.bindMS);
        Assert.AreEqual(0, backdrop.depthBufferBits);
        Assert.IsFalse(backdrop.useMipMap);
        Assert.IsFalse(backdrop.autoGenerateMips);
    }

    [Test]
    public void OneSliceArrayBackdropKeepsItsArraySamplerShape()
    {
        var source = new RenderTextureDescriptor(
            320,
            180,
            RenderTextureFormat.ARGB32,
            0)
        {
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = 1,
            vrUsage = VRTextureUsage.DeviceSpecific,
            msaaSamples = 1
        };

        NowGlassTextureLayout layout = NowGlassTextureLayout.FromDescriptor(source);
        RenderTextureDescriptor backdrop = NowGlassBackdropSurface.CreateDescriptor(
            source.width,
            source.height,
            layout);
        var texture = new RenderTexture(backdrop);

        try
        {
            Assert.IsTrue(layout.isArray);
            Assert.AreEqual(TextureDimension.Tex2DArray, layout.dimension);
            Assert.AreEqual(1, layout.volumeDepth);
            Assert.AreEqual(1, layout.sliceCount);
            Assert.AreEqual(VRTextureUsage.DeviceSpecific, layout.vrUsage);

            Assert.AreEqual(TextureDimension.Tex2DArray, backdrop.dimension);
            Assert.AreEqual(1, backdrop.volumeDepth);
            Assert.AreEqual(VRTextureUsage.DeviceSpecific, backdrop.vrUsage);
            Assert.IsTrue(
                NowGlassBackdropSurface.Matches(texture, source.width, source.height, layout),
                "A one-slice array backdrop must not be recreated as a flat Texture2D.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void LogicalCaptureComposesWithDynamicResolutionAllocationScale()
    {
        Vector4 composed = NowGlassRenderer.ComposeSourceScaleOffset(
            new Vector4(0.5f, 0.25f, 0.1f, 0.2f),
            new Vector4(0.75f, 0.5f, 0.02f, 0.03f));

        Assert.AreEqual(0.375f, composed.x, 0.0001f);
        Assert.AreEqual(0.125f, composed.y, 0.0001f);
        Assert.AreEqual(0.095f, composed.z, 0.0001f);
        Assert.AreEqual(0.13f, composed.w, 0.0001f);
    }

    [Test]
    public void ResizeKeepsTheMaterialBoundAllocationUntilItsReplacementIsReady()
    {
        RenderTexture current = new RenderTexture(16, 16, 0);
        RenderTexture retired = null;
        RenderTexture replacement = null;

        try
        {
            current.Create();
            RenderTexture originallyBound = current;

            NowWorldGlassBackdrop.RetireTexture(ref current, ref retired);

            Assert.IsNull(current);
            Assert.AreSame(originallyBound, retired);
            Assert.IsTrue(retired.IsCreated(), "The texture still bound to the material was released during resize.");

            replacement = new RenderTexture(32, 32, 0);
            replacement.Create();
            current = replacement;
            NowWorldGlassBackdrop.RetireTexture(ref current, ref retired);

            Assert.IsNull(current);
            Assert.AreSame(
                originallyBound,
                retired,
                "A second resize must retain the original material-bound allocation.");
            Assert.IsTrue(retired.IsCreated());
        }
        finally
        {
            if (current != null)
                NowGlassBackdropSurface.ReleaseTexture(ref current);

            if (retired != null)
                NowGlassBackdropSurface.ReleaseTexture(ref retired);

            if (replacement != null)
            {
                replacement.Release();
                Object.DestroyImmediate(replacement);
            }
        }
    }

}
