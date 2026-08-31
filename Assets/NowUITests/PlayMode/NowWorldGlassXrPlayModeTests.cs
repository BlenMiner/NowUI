using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Unity.XR.MockHMD;

public sealed class NowWorldGlassXrPlayModeTests
{
    static readonly Color LeftEyeBackground = new Color(0.4f, 0.1f, 0.05f, 1f);
    static readonly Color RightEyeBackground = new Color(0.05f, 0.1f, 0.4f, 1f);

    sealed class XrRenderPassProbe : MonoBehaviour
    {
        readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();
        CommandBuffer _arrayEyeBackgroundBuffer;
        Camera _arrayEyeBackgroundCamera;

        public bool captured;
        public bool sawLeftEye;
        public bool sawRightEye;
        public bool encodeEyeInBackground;
        public bool usesTextureArray;
        public string state;

        void OnPreCull()
        {
            var camera = GetComponent<Camera>();
            _displays.Clear();
            SubsystemManager.GetSubsystems(_displays);

            for (int displayIndex = 0; displayIndex < _displays.Count; ++displayIndex)
            {
                var display = _displays[displayIndex];

                if (display == null || !display.running || display.GetRenderPassCount() == 0)
                    continue;

                display.GetRenderPass(0, out var renderPass);
                usesTextureArray = renderPass.renderTargetDesc.dimension == TextureDimension.Tex2DArray &&
                    renderPass.renderTargetDesc.volumeDepth > 1;

                if (encodeEyeInBackground && usesTextureArray)
                {
                    EnsureArrayEyeBackground(camera);
                }
                else if (encodeEyeInBackground)
                {
                    if (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                        camera.backgroundColor = LeftEyeBackground;
                    else if (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
                        camera.backgroundColor = RightEyeBackground;
                }

                var backingColor = display.GetRenderTextureForRenderPass(0);
                var backingDepth = display.GetSharedDepthTextureForRenderPass(0);
                var sourceDescriptor = NowWorldGlassBackdrop.GetCameraSourceDescriptor(
                    camera,
                    camera.pixelWidth,
                    camera.pixelHeight);

                state = DescribeXrState(
                    renderPass.renderTargetDesc,
                    backingColor,
                    backingDepth,
                    sourceDescriptor,
                    camera);
                sawLeftEye |= camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left;
                sawRightEye |= camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right;
                captured = true;
                return;
            }
        }

        void EnsureArrayEyeBackground(Camera camera)
        {
            if (_arrayEyeBackgroundBuffer != null)
                return;

            _arrayEyeBackgroundCamera = camera;
            _arrayEyeBackgroundBuffer = new CommandBuffer
            {
                name = "NowUI XR distinct-eye test background"
            };
            _arrayEyeBackgroundBuffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                0,
                CubemapFace.Unknown,
                0);
            _arrayEyeBackgroundBuffer.ClearRenderTarget(false, true, LeftEyeBackground);
            _arrayEyeBackgroundBuffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                0,
                CubemapFace.Unknown,
                1);
            _arrayEyeBackgroundBuffer.ClearRenderTarget(false, true, RightEyeBackground);
            _arrayEyeBackgroundBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, _arrayEyeBackgroundBuffer);
        }

        void OnDisable()
        {
            if (_arrayEyeBackgroundCamera != null && _arrayEyeBackgroundBuffer != null)
            {
                _arrayEyeBackgroundCamera.RemoveCommandBuffer(
                    CameraEvent.BeforeForwardOpaque,
                    _arrayEyeBackgroundBuffer);
            }

            _arrayEyeBackgroundBuffer?.Release();
            _arrayEyeBackgroundBuffer = null;
            _arrayEyeBackgroundCamera = null;
        }
    }

    sealed class GlassWorldLayoutGraphic : NowWorldLayoutGraphic
    {
        protected override void DrawNowUI(NowRect rect)
        {
            Now.Glass(new NowRect(0f, 0f, rect.width, rect.height))
                .SetBlurRadius(18f)
                .SetTint(new Color(0f, 0f, 0f, 0.5f))
                .SetVibrancy(1f, 1f)
                .Draw();
        }
    }

    [UnityTest]
    public IEnumerator BuiltinMockHmdMultiPassUsesTheMsaaSafeWorldGlassVariant()
    {
        return RunBuiltinMockHmdWorldGlassTest(MockHMDBuildSettings.RenderMode.MultiPass);
    }

    [UnityTest]
    public IEnumerator BuiltinMockHmdSinglePassKeepsDistinctEyeSlices()
    {
        return RunBuiltinMockHmdWorldGlassTest(MockHMDBuildSettings.RenderMode.SinglePassInstanced);
    }

    IEnumerator RunBuiltinMockHmdWorldGlassTest(MockHMDBuildSettings.RenderMode renderMode)
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            Assert.Ignore("This test exercises the Built-in render-pipeline XR path.");

        bool expectsTextureArray = renderMode == MockHMDBuildSettings.RenderMode.SinglePassInstanced;

        var displays = new List<XRDisplaySubsystem>();
        XRDisplaySubsystem display = null;
        XRManagerSettings xrManager = null;
        bool ownsXrLoader = false;
        GameObject cameraObject = null;
        GameObject graphicObject = null;
        int previousAntiAliasing = QualitySettings.antiAliasing;
        GameViewRenderMode previousGameViewRenderMode = XRSettings.gameViewRenderMode;
        MockHMDBuildSettings.RenderMode previousMockRenderMode =
            MockHMDBuildSettings.Instance != null
                ? MockHMDBuildSettings.Instance.renderMode
                : MockHMDBuildSettings.RenderMode.MultiPass;

        try
        {
            var generalSettings = XRGeneralSettings.Instance;
            if (generalSettings == null || generalSettings.Manager == null)
                Assert.Ignore("No XR Management settings are available for this PlayMode run.");

            xrManager = generalSettings.Manager;
            if (xrManager.activeLoader != null)
                Assert.Ignore("XR was already initialized; this isolated test will not mutate an external XR session.");

            // XR Management requires graphics initialization (and therefore at
            // least one PlayMode frame) before synchronous manual loading.
            yield return null;
            xrManager.InitializeLoaderSync();
            if (xrManager.activeLoader == null)
                Assert.Ignore("The configured MockHMD loader could not initialize on this graphics device.");

            ownsXrLoader = true;
            xrManager.StartSubsystems();
            XRSettings.gameViewRenderMode = GameViewRenderMode.BothEyes;

            // The native API's bool return is not marshalled canonically on all
            // Editor backends; the observed render-pass layout below is the
            // authoritative success check.
            MockHMD.SetRenderMode(renderMode);

            QualitySettings.antiAliasing = 2;

            cameraObject = new GameObject($"NowUI XR Glass {renderMode} Test Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.allowMSAA = true;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.2f, 0.4f, 1f);
            var probe = cameraObject.AddComponent<XrRenderPassProbe>();
            probe.encodeEyeInBackground = true;

            // The display becomes available before its render passes. Request
            // MSAA first, then let the probe inspect the pass from OnPreCull,
            // where Unity exposes the live XR attachments.
            for (int frame = 0; frame < 120 && display == null; ++frame)
            {
                displays.Clear();
                SubsystemManager.GetSubsystems(displays);

                for (int i = 0; i < displays.Count; ++i)
                {
                    if (displays[i] != null &&
                        displays[i].running)
                    {
                        display = displays[i];
                        break;
                    }
                }

                if (display == null)
                    yield return null;
            }

            if (display == null)
                Assert.Ignore("No XR display subsystem became active in this PlayMode run.");

            display.SetMSAALevel(2);

            graphicObject = new GameObject($"NowUI XR Glass {renderMode} Test Graphic");
            graphicObject.transform.position = new Vector3(0f, 0f, 2f);
            var graphic = graphicObject.AddComponent<GlassWorldLayoutGraphic>();
            graphic.targetCamera = camera;
            graphic.size = new Vector2(120f, 80f);
            graphic.pixelsPerUnit = 100f;
            graphic.glassBackdropMode = NowWorldGlassBackdropMode.Camera;
            graphic.RebuildNowUI();

            for (int frame = 0;
                 frame < 120 && (!probe.captured || probe.usesTextureArray != expectsTextureArray);
                 ++frame)
            {
                yield return null;
            }

            if (!probe.captured)
            {
                Assert.Ignore(
                    "The active XR display never produced a camera render pass. Editor batch mode " +
                    "does not schedule a Game View render; use a graphics Editor session or a " +
                    "standalone test Player for this probe.");
            }

            Assert.AreEqual(
                expectsTextureArray,
                probe.usesTextureArray,
                $"MockHMD did not switch to the requested {renderMode} layout. {probe.state}");

            for (int frame = 0; frame < 8; ++frame)
                yield return null;

            string xrState = probe.state;
            TestContext.WriteLine(xrState);
            Assert.IsTrue(camera.stereoEnabled, $"XR camera did not become stereo-enabled. {xrState}");
            Assert.IsFalse(
                NowWorldGlassBackdrop.SupportsSceneDepthSampling(camera),
                $"Built-in XR MSAA must select the depth-sampler-free glass path. {xrState}");

            var renderer = graphicObject.GetComponent<MeshRenderer>();
            Assert.NotNull(renderer);

            Material glassMaterial = null;
            var materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; ++i)
            {
                if (materials[i] != null && materials[i].HasProperty("_NowMaterialGlassUseSceneDepth"))
                {
                    glassMaterial = materials[i];
                    break;
                }
            }

            Assert.NotNull(glassMaterial);
            Assert.Greater(
                glassMaterial.GetFloat("_NowMaterialGlassUseBackdrop"),
                0.5f,
                $"The glass material never received the captured XR backdrop. {xrState}");
            Assert.IsFalse(
                glassMaterial.IsKeywordEnabled("NOWUI_GLASS_SCENE_DEPTH"),
                $"The XR MSAA material still reflects _CameraDepthTexture. {xrState}");
            Assert.AreEqual(
                (float)CompareFunction.LessEqual,
                glassMaterial.GetFloat("_ZTest"),
                0.001f,
                $"XR MSAA foreground protection did not use fixed-function depth. {xrState}");

            bool usesStereoBackdrop = glassMaterial.GetFloat("_NowMaterialGlassUseStereoBackdrop") > 0.5f;
            Assert.AreEqual(
                probe.usesTextureArray,
                usesStereoBackdrop,
                $"The material's backdrop layout does not match the XR render-pass layout. {xrState}");

            if (usesStereoBackdrop)
            {
                var backdrop = glassMaterial.GetTexture("_NowMaterialBackdropArrayTex") as RenderTexture;
                Assert.NotNull(backdrop, $"The material's XR array backdrop is not a RenderTexture. {xrState}");
                Assert.AreEqual(TextureDimension.Tex2DArray, backdrop.dimension, xrState);
                Assert.GreaterOrEqual(backdrop.volumeDepth, 2, xrState);

                Color left = ReadArrayCenter(backdrop, 0);
                Color right = ReadArrayCenter(backdrop, 1);
                TestContext.WriteLine($"backdropLeft={left}; backdropRight={right}");
                Assert.Greater(left.r, left.b, $"The left SPI backdrop slice was collapsed or swapped. {xrState}");
                Assert.Greater(right.b, right.r, $"The right SPI backdrop slice lost the blue camera background. {xrState}");

                // Validate the provider output too, not only the intermediate
                // capture. The glass pane is centered and deliberately darkens
                // the backdrop, while the corner remains the test background.
                yield return new WaitForEndOfFrame();
                var providerColor = display.GetRenderTextureForRenderPass(0);
                Assert.NotNull(providerColor, $"MockHMD did not expose its final color target. {xrState}");
                Assert.AreEqual(TextureDimension.Tex2DArray, providerColor.dimension, xrState);

                Color finalLeft = ReadArrayCenter(providerColor, 0);
                Color finalRight = ReadArrayCenter(providerColor, 1);
                Color outsideLeft = ReadArrayPixel(providerColor, 0, 0.05f, 0.05f);
                Color outsideRight = ReadArrayPixel(providerColor, 1, 0.05f, 0.05f);
                TestContext.WriteLine(
                    $"providerLeft={finalLeft}/{outsideLeft}; providerRight={finalRight}/{outsideRight}");
                Assert.Greater(outsideLeft.r, outsideLeft.b, $"The final left-eye background is wrong. {xrState}");
                Assert.Greater(outsideRight.b, outsideRight.r, $"The final right-eye background is wrong. {xrState}");
                Assert.IsTrue(
                    TryFindDarkenedGlassPixel(
                        providerColor,
                        0,
                        true,
                        outsideLeft,
                        out Color glassLeft,
                        out Vector2 glassLeftUv),
                    $"The glass pane did not render with the left-eye backdrop. {xrState}");
                Assert.IsTrue(
                    TryFindDarkenedGlassPixel(
                        providerColor,
                        1,
                        true,
                        outsideRight,
                        out Color glassRight,
                        out Vector2 glassRightUv),
                    $"The glass pane did not render with the right-eye backdrop. {xrState}");
                TestContext.WriteLine(
                    $"providerGlassLeft={glassLeft}@{glassLeftUv}; providerGlassRight={glassRight}@{glassRightUv}");
            }
            else
            {
                Assert.IsTrue(probe.sawLeftEye, $"MockHMD MultiPass never rendered the left eye. {xrState}");
                Assert.IsTrue(probe.sawRightEye, $"MockHMD MultiPass never rendered the right eye. {xrState}");

                var backdrop = glassMaterial.GetTexture("_NowMaterialBackdropTex") as RenderTexture;
                Assert.NotNull(backdrop, $"The material's captured XR backdrop is not a RenderTexture. {xrState}");
                Color center = ReadCenter(backdrop);
                TestContext.WriteLine($"backdropCenter={center}");
                Assert.Greater(center.b, 0.05f, $"The captured XR backdrop is unexpectedly black. {xrState}");
                Assert.Greater(
                    center.b,
                    center.r,
                    $"The final MultiPass backdrop contains the left eye instead of the right eye. {xrState}");

                // MockHMD reuses its flat provider wrapper across MultiPass
                // eyes, so reading both wrappers after the frame only exposes
                // the last eye. Capture Unity's final side-by-side mirror to
                // validate both completed eye images instead.
                yield return new WaitForEndOfFrame();
                var mirror = new RenderTexture(
                    Mathf.Max(1, Screen.width),
                    Mathf.Max(1, Screen.height),
                    0,
                    RenderTextureFormat.ARGB32);

                try
                {
                    mirror.Create();
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(mirror);
                    var firstEyeRegion = new Vector4(0.5f, 1f, 0f, 0f);
                    var secondEyeRegion = new Vector4(0.5f, 1f, 0.5f, 0f);
                    Color firstOutside = ReadPixel(mirror, 0.05f, 0.05f);
                    Color secondOutside = ReadPixel(mirror, 0.55f, 0.05f);
                    bool firstIsLeft = firstOutside.r > firstOutside.b;
                    bool secondIsLeft = secondOutside.r > secondOutside.b;
                    TestContext.WriteLine(
                        $"mirrorMultiPassOutside={firstOutside}/{secondOutside}");
                    Assert.AreNotEqual(
                        firstIsLeft,
                        secondIsLeft,
                        $"The MultiPass mirror does not contain two distinct eye backgrounds. {xrState}");
                    var leftRegion = firstIsLeft ? firstEyeRegion : secondEyeRegion;
                    var rightRegion = firstIsLeft ? secondEyeRegion : firstEyeRegion;
                    Color leftOutside = firstIsLeft ? firstOutside : secondOutside;
                    Color rightOutside = firstIsLeft ? secondOutside : firstOutside;
                    Assert.IsTrue(
                        TryFindDarkenedGlassPixel(
                            mirror,
                            0,
                            false,
                            leftOutside,
                            leftRegion,
                            out Color leftGlass,
                            out Vector2 leftGlassUv),
                        $"The glass pane did not render into the final MultiPass left eye. {xrState}");
                    Assert.IsTrue(
                        TryFindDarkenedGlassPixel(
                            mirror,
                            0,
                            false,
                            rightOutside,
                            rightRegion,
                            out Color rightGlass,
                            out Vector2 rightGlassUv),
                        $"The glass pane did not render into the final MultiPass right eye. {xrState}");
                    TestContext.WriteLine(
                        $"mirrorMultiPassLeft={leftGlass}/{leftOutside}@{leftGlassUv}; " +
                        $"mirrorMultiPassRight={rightGlass}/{rightOutside}@{rightGlassUv}");
                }
                finally
                {
                    mirror.Release();
                    Object.DestroyImmediate(mirror);
                }
            }

            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            QualitySettings.antiAliasing = previousAntiAliasing;
            XRSettings.gameViewRenderMode = previousGameViewRenderMode;

            if (graphicObject != null)
                Object.DestroyImmediate(graphicObject);

            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);

            // DeinitializeLoader also stops every subsystem owned by this
            // loader, restoring the display/MSAA state changed by the test.
            if (ownsXrLoader && xrManager != null)
            {
                MockHMD.SetRenderMode(previousMockRenderMode);
                xrManager.DeinitializeLoader();
            }
        }
    }

    static Color ReadCenter(RenderTexture texture)
    {
        return ReadPixel(texture, 0.5f, 0.5f);
    }

    static Color ReadPixel(RenderTexture texture, float normalizedX, float normalizedY)
    {
        RenderTexture previous = RenderTexture.active;
        Texture2D readback = null;

        try
        {
            RenderTexture.active = texture;
            readback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            readback.ReadPixels(
                new Rect(
                    Mathf.Clamp(Mathf.RoundToInt((texture.width - 1) * normalizedX), 0, texture.width - 1),
                    Mathf.Clamp(Mathf.RoundToInt((texture.height - 1) * normalizedY), 0, texture.height - 1),
                    1f,
                    1f),
                0,
                0);
            readback.Apply(false, false);
            return readback.GetPixel(0, 0);
        }
        finally
        {
            RenderTexture.active = previous;

            if (readback != null)
                Object.DestroyImmediate(readback);
        }
    }

    static Color ReadArrayCenter(RenderTexture texture, int slice)
    {
        return ReadArrayPixel(texture, slice, 0.5f, 0.5f);
    }

    static Color ReadArrayPixel(
        RenderTexture texture,
        int slice,
        float normalizedX,
        float normalizedY)
    {
        var descriptor = new RenderTextureDescriptor(
            texture.width,
            texture.height,
            RenderTextureFormat.ARGB32,
            0)
        {
            dimension = TextureDimension.Tex2D,
            volumeDepth = 1,
            msaaSamples = 1
        };
        var flat = new RenderTexture(descriptor);

        try
        {
            flat.Create();
            Graphics.CopyTexture(texture, slice, 0, flat, 0, 0);
            return ReadPixel(flat, normalizedX, normalizedY);
        }
        finally
        {
            flat.Release();
            Object.DestroyImmediate(flat);
        }
    }

    static float MaxRgb(Color color)
    {
        return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
    }

    static bool TryFindDarkenedGlassPixel(
        RenderTexture texture,
        int slice,
        bool isArray,
        Color outside,
        out Color glass,
        out Vector2 uv)
    {
        return TryFindDarkenedGlassPixel(
            texture,
            slice,
            isArray,
            outside,
            new Vector4(1f, 1f, 0f, 0f),
            out glass,
            out uv);
    }

    static bool TryFindDarkenedGlassPixel(
        RenderTexture texture,
        int slice,
        bool isArray,
        Color outside,
        Vector4 sampleRegion,
        out Color glass,
        out Vector2 uv)
    {
        // MockHMD may draw a diagnostic/occlusion marker at the exact target
        // center. Probe several points safely inside the projected 1.2 x 0.8 m
        // pane instead of treating that marker as the glass result.
        float[] xs = { 0.35f, 0.42f, 0.58f, 0.65f };
        float[] ys = { 0.40f, 0.50f, 0.60f };
        bool expectRed = outside.r > outside.b;

        for (int y = 0; y < ys.Length; ++y)
        {
            for (int x = 0; x < xs.Length; ++x)
            {
                uv = new Vector2(
                    xs[x] * sampleRegion.x + sampleRegion.z,
                    ys[y] * sampleRegion.y + sampleRegion.w);
                glass = isArray
                    ? ReadArrayPixel(texture, slice, uv.x, uv.y)
                    : ReadPixel(texture, uv.x, uv.y);
                TestContext.WriteLine(
                    $"glassProbe[{(isArray ? slice : 0)}] {uv}={glass}");
                bool correctEye = expectRed
                    ? glass.r > glass.b + 0.01f
                    : glass.b > glass.r + 0.01f;
                bool darkened = MaxRgb(glass) < MaxRgb(outside) * 0.9f;

                if (correctEye && darkened)
                    return true;
            }
        }

        glass = default;
        uv = default;
        return false;
    }

    static string DescribeXrState(
        RenderTextureDescriptor descriptor,
        RenderTexture color,
        RenderTexture depth,
        RenderTextureDescriptor sourceDescriptor,
        Camera camera)
    {
        string colorState = color == null
            ? "color=null"
            : $"color(msaa={color.antiAliasing}, bindMS={color.bindTextureMS}, dim={color.dimension}, slices={color.volumeDepth})";
        string depthState = depth == null
            ? "depth=null"
            : $"depth(msaa={depth.antiAliasing}, bindMS={depth.bindTextureMS}, dim={depth.dimension}, slices={depth.volumeDepth})";

        return $"desc(msaa={descriptor.msaaSamples}, bindMS={descriptor.bindMS}, dim={descriptor.dimension}, " +
            $"slices={descriptor.volumeDepth}, vr={descriptor.vrUsage}); {colorState}; {depthState}; " +
            $"source(msaa={sourceDescriptor.msaaSamples}, bindMS={sourceDescriptor.bindMS}, " +
            $"dim={sourceDescriptor.dimension}, slices={sourceDescriptor.volumeDepth}, vr={sourceDescriptor.vrUsage}); " +
            $"camera(stereo={camera.stereoEnabled}, eye={camera.stereoActiveEye}, allowMSAA={camera.allowMSAA}, " +
            $"qualityMSAA={QualitySettings.antiAliasing}, depthSampling=" +
            $"{NowWorldGlassBackdrop.SupportsSceneDepthSampling(camera)})";
    }
}
