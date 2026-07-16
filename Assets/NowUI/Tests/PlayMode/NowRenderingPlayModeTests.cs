using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using NowUI;
using Object = UnityEngine.Object;

/// <summary>
/// Play-mode tests that render real frames and assert on the pixels that come
/// back: the GPU draw path, shader output, font atlas uploads, and Lottie
/// tessellation — everything the edit-mode capture tests cannot see.
/// </summary>
public class NowRenderingPlayModeTests
{
    const int Side = 128;

    RenderTexture _target;
    NowRenderer _renderer;

    [SetUp]
    public void SetUp()
    {
        _target = new RenderTexture(Side, Side, 0, RenderTextureFormat.ARGB32);
        _target.Create();
        _renderer = new NowRenderer();
        NowFontCompiler.forceManagedCompiler = false;
    }

    [TearDown]
    public void TearDown()
    {
        NowFontCompiler.forceManagedCompiler = false;
        _renderer.Dispose();
        _target.Release();
        Object.DestroyImmediate(_target);
    }

    static Color32[] ReadPixels(RenderTexture target)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;

        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();

        RenderTexture.active = previous;

        var pixels = texture.GetPixels32();
        Object.DestroyImmediate(texture);
        return pixels;
    }

    static int CountPixels(Color32[] pixels, System.Predicate<Color32> predicate)
    {
        int count = 0;

        for (int i = 0; i < pixels.Length; ++i)
        {
            if (predicate(pixels[i]))
                ++count;
        }

        return count;
    }

    static GameObject CreateSkinnedCube(
        Material material,
        out Transform animatedBone,
        out Mesh ownedMesh)
    {
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive);

        var root = new GameObject("Skinned Preview Pixel Source");
        var boneRoot = new GameObject("Bone Root").transform;
        boneRoot.SetParent(root.transform, false);
        var boneTip = new GameObject("Bone Tip").transform;
        boneTip.SetParent(boneRoot, false);
        boneTip.localPosition = Vector3.up * 0.5f;
        var rendererObject = new GameObject("Skinned Cube");
        rendererObject.transform.SetParent(root.transform, false);
        var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();

        ownedMesh = Object.Instantiate(cubeMesh);
        ownedMesh.name = "Now Preview Pixel Skinned Cube";
        var vertices = ownedMesh.vertices;
        var weights = new BoneWeight[vertices.Length];

        for (int i = 0; i < vertices.Length; ++i)
        {
            weights[i] = new BoneWeight
            {
                boneIndex0 = vertices[i].y > 0f ? 1 : 0,
                weight0 = 1f
            };
        }

        ownedMesh.boneWeights = weights;
        ownedMesh.bindposes = new[]
        {
            boneRoot.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix,
            boneTip.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix
        };
        ownedMesh.RecalculateBounds();
        renderer.sharedMesh = ownedMesh;
        renderer.sharedMaterial = material;
        renderer.bones = new[] { boneRoot, boneTip };
        renderer.rootBone = boneRoot;
        renderer.localBounds = ownedMesh.bounds;
        renderer.updateWhenOffscreen = true;
        animatedBone = boneTip;
        return root;
    }

    static void AssertTargetOpaque(Color32[] pixels, string message)
    {
        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                var pixel = pixels[y * Side + x];
                Assert.GreaterOrEqual(pixel.a, 250, $"{message} at {x},{y}.");
            }
        }
    }

    static NowFont ResolveDefaultNowFont()
    {
        var fallback = Now.defaultFont;
        Assert.NotNull(fallback, "Default font resource is missing.");
        Assert.IsTrue(
            fallback.TryResolveGlyph('A', 32f, NowFontStyle.Regular, out NowFont resolved, out _, out _),
            "Default font could not resolve a glyph.");
        return resolved;
    }

    [Test]
    public void RectangleRendersExpectedCoverage()
    {
        // Centered square so the assertion is immune to platform UV flips.
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(32, 32, 64, 64))
                .SetColor(Color.red)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        int red = CountPixels(pixels, p => p.r > 200 && p.g < 50 && p.a > 200);

        Assert.GreaterOrEqual(red, 64 * 64 - 300, "Rectangle covered fewer pixels than expected.");
        Assert.LessOrEqual(red, 64 * 64 + 300, "Rectangle covered more pixels than expected.");

        Assert.Greater(pixels[(Side / 2) * Side + Side / 2].r, 200);
        Assert.AreEqual(0, pixels[4 * Side + 4].a);
    }

    [Test]
    public void PremultipliedTextureAvoidsMultiplyingAlphaTwice()
    {
        var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);

        try
        {
            source.SetPixel(0, 0, new Color(0.5f, 0f, 0f, 0.5f));
            source.Apply(false, false);

            using (_renderer.Begin(_target))
            {
                Now.Rectangle(new NowRect(8f, 32f, 48f, 64f))
                    .SetTexture(source, premultipliedAlpha: false)
                    .Draw();
                Now.Rectangle(new NowRect(72f, 32f, 48f, 64f))
                    .SetTexture(source, premultipliedAlpha: true)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            Color32 straight = pixels[64 * Side + 32];
            Color32 premultiplied = pixels[64 * Side + 96];

            Assert.AreEqual(straight.a, premultiplied.a, 4);
            Assert.Greater(
                premultiplied.r,
                straight.r + 30,
                $"PMA branch did not preserve RGB (straight {straight}, PMA {premultiplied}).");
        }
        finally
        {
            Now.ReleaseTextureMaterials(source);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator ModelPreviewRendersThroughTextureBackedEffect()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var preview = new NowModelPreview(source)
                .SetFixedResolution(64, 64);

            Assert.IsTrue(preview.RenderNow(), "The preview camera request did not render.");
            yield return null;

            using (_renderer.Begin(_target))
                Now.Model(new NowRect(32f, 32f, 64f, 64f), preview).Draw();

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            yield return null;

            var direct = ReadPixels(_target);
            Assert.Greater(direct[64 * Side + 64].a, 150);
            Assert.Greater(
                CountPixels(direct, pixel => pixel.a < 20),
                Side * Side / 2,
                $"Direct model draw did not preserve transparency; corners were " +
                $"{direct[0]}, {direct[Side - 1]}, {direct[(Side - 1) * Side]}, {direct[^1]}.");
            _renderer.Clear();

            using (_renderer.Begin(_target))
            {
                using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
                    .SetRenderToTexture()
                    .SetSourceRect(new NowRect(32f, 32f, 64f, 64f))
                    .Begin())
                {
                    Now.Model(new NowRect(32f, 32f, 64f, 64f), preview).Draw();
                }
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            yield return null;

            var output = ReadPixels(_target);
            Assert.Greater(output[64 * Side + 64].a, 150);
            Assert.Greater(CountPixels(output, pixel => pixel.a < 20), Side * Side / 2);
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator IsolatedRenderMeshMatchesCloneBaselineCoverage()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.localPosition = new Vector3(3f, -2f, 5f);
        source.transform.localRotation = Quaternion.Euler(17f, 31f, -8f);
        source.transform.localScale = new Vector3(2.5f, 0.8f, 1.4f);

        try
        {
            using var clone = new NowModelPreview(
                    source,
                    31,
                    NowModelPreviewBackend.RendererClone)
                .SetFixedResolution(96, 96);
            using var raw = new NowModelPreview(source)
                .SetFixedResolution(96, 96);

            Assert.IsTrue(clone.RenderNow(), "The clone preview did not render.");
            Assert.IsTrue(raw.RenderNow(), "The RenderMesh preview did not render.");
            yield return null;

            var clonePixels = ReadPixels(clone.texture);
            var rawPixels = ReadPixels(raw.texture);
            int cloneCoverage = CountPixels(clonePixels, pixel => pixel.a > 32);
            int rawCoverage = CountPixels(rawPixels, pixel => pixel.a > 32);
            Color32 cloneCenter = clonePixels[48 * 96 + 48];
            Color32 rawCenter = rawPixels[48 * 96 + 48];

            Assert.Greater(rawCoverage, 500, "RenderMesh produced no useful model coverage.");
            Assert.AreEqual(cloneCoverage, rawCoverage, 96,
                "RenderMesh and clone framing diverged by more than about one scanline.");
            Assert.AreEqual(cloneCenter.r, rawCenter.r, 12);
            Assert.AreEqual(cloneCenter.g, rawCenter.g, 12);
            Assert.AreEqual(cloneCenter.b, rawCenter.b, 12);
            Assert.AreEqual(cloneCenter.a, rawCenter.a, 4);
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator IsolatedRenderMeshSubmissionIsRestrictedToItsPreviewCamera()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cameraObject = new GameObject("RenderMesh Leak Probe Camera");
        var gameTarget = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);

        try
        {
            gameTarget.Create();

            using var raw = new NowModelPreview(source)
                .SetFixedResolution(96, 96);
            Assert.IsTrue(raw.RenderNow(), "The RenderMesh preview did not render.");

            var gameCamera = cameraObject.AddComponent<Camera>();
            gameCamera.CopyFrom(raw.camera);
            gameCamera.cameraType = CameraType.Game;
            gameCamera.scene = default;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = Color.magenta;
            gameCamera.targetTexture = gameTarget;
            gameCamera.Render();
            yield return null;

            var pixels = ReadPixels(gameTarget);
            int changed = CountPixels(
                pixels,
                pixel => pixel.r < 240 || pixel.b < 240 || pixel.g > 20 || pixel.a < 240);

            Assert.Less(changed, 32,
                "A camera-targeted RenderMesh submission became visible to a gameplay camera.");
        }
        finally
        {
            var gameCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;

            if (gameCamera != null)
                gameCamera.targetTexture = null;

            gameTarget.Release();
            Object.DestroyImmediate(gameTarget);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator SceneObjectPreviewBorrowsInPlaceAndLeavesGameCameraCullingToCaller()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Caller-Owned Scene Preview";
        source.layer = 30;
        source.transform.position = new Vector3(123f, 7f, -41f);
        var sourceRenderer = source.GetComponent<Renderer>();
        var originalPosition = source.transform.position;
        var cameraObject = new GameObject("Scene Preview Culling Probe");
        var gameTarget = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);

        try
        {
            gameTarget.Create();

            using (var preview = NowModelPreview.FromSceneObject(source, 1 << 30)
                .SetFixedResolution(96, 96))
            {
                Assert.IsTrue(preview.RenderNow(), "The borrowed scene-object preview did not render.");
                yield return null;

                int previewCoverage = CountPixels(ReadPixels(preview.texture), pixel => pixel.a > 32);
                Assert.Greater(previewCoverage, 500, "The scene object produced no useful preview coverage.");
                Assert.AreEqual(originalPosition, source.transform.position);
                Assert.AreEqual(30, source.layer);
                Assert.IsFalse(sourceRenderer.forceRenderingOff);

                var gameCamera = cameraObject.AddComponent<Camera>();
                gameCamera.CopyFrom(preview.camera);
                gameCamera.cameraType = CameraType.Game;
                gameCamera.scene = default;
                gameCamera.cullingMask = 0;
                gameCamera.clearFlags = CameraClearFlags.SolidColor;
                gameCamera.backgroundColor = Color.magenta;
                gameCamera.targetTexture = gameTarget;
                gameCamera.Render();
                yield return null;

                var pixels = ReadPixels(gameTarget);
                int changed = CountPixels(
                    pixels,
                    pixel => pixel.r < 240 || pixel.b < 240 || pixel.g > 20 || pixel.a < 240);
                Assert.Less(changed, 32,
                    "The caller's game-camera culling mask should remain authoritative.");
            }

            Assert.IsTrue(source != null);
            Assert.AreEqual(originalPosition, source.transform.position);
            Assert.AreEqual(30, source.layer);
            Assert.IsFalse(sourceRenderer.forceRenderingOff);
        }
        finally
        {
            var gameCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;

            if (gameCamera != null)
                gameCamera.targetTexture = null;

            gameTarget.Release();
            Object.DestroyImmediate(gameTarget);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator IsolatedSkinnedSnapshotMatchesCloneBaselineCoverage()
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        Assert.NotNull(shader);
        var material = new Material(shader) { color = Color.white };
        var source = CreateSkinnedCube(material, out var bone, out var mesh);

        try
        {
            bone.localRotation = Quaternion.Euler(0f, 0f, 18f);

            using var clone = new NowModelPreview(
                    source,
                    31,
                    NowModelPreviewBackend.RendererClone)
                .SetFixedResolution(96, 96);
            using var snapshot = new NowModelPreview(source)
                .SetFixedResolution(96, 96);

            Assert.AreEqual(1, snapshot.ownedBakedMeshCount);
            Assert.AreEqual(0, snapshot.unsupportedRendererCount);
            Assert.IsTrue(clone.RenderNow());
            Assert.IsTrue(snapshot.RenderNow());
            yield return null;

            int cloneCoverage = CountPixels(ReadPixels(clone.texture), pixel => pixel.a > 32);
            int snapshotCoverage = CountPixels(ReadPixels(snapshot.texture), pixel => pixel.a > 32);

            Assert.Greater(snapshotCoverage, 500, "The CPU-baked snapshot rendered no visible mesh.");
            Assert.AreEqual(cloneCoverage, snapshotCoverage, Mathf.CeilToInt(cloneCoverage * 0.15f),
                "The baked pose uses its exact mesh bounds while SkinnedMeshRenderer framing uses localBounds; " +
                "coverage should remain comparable even though those bounds are not pixel-identical.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }
    }

    [UnityTest]
    public IEnumerator ModelPreviewSourceModesRenderUnderInstalledUrp()
    {
        var assetType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);
        var rendererDataType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.ScriptableRendererData, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);

        if (assetType == null || rendererDataType == null)
        {
            Assert.Ignore("URP is not installed in this validation project.");
            yield break;
        }

        var create = assetType.GetMethod(
            "Create",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { rendererDataType },
            null);
        Assert.NotNull(create, "URP no longer exposes its runtime pipeline-asset factory.");

        var previousDefault = GraphicsSettings.defaultRenderPipeline;
        var previousQuality = QualitySettings.renderPipeline;
        var pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { null });
        GameObject source = null;
        Material material = null;

        try
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            yield return null;
            yield return null;
            Assert.AreSame(pipeline, GraphicsSettings.currentRenderPipeline);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.NotNull(shader, "URP Lit shader was not available after installing URP.");
            material = new Material(shader) { color = Color.white };
            source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.GetComponent<Renderer>().sharedMaterial = material;

            using var clone = new NowModelPreview(
                    source,
                    31,
                    NowModelPreviewBackend.RendererClone)
                .SetFixedResolution(96, 96);
            using var raw = new NowModelPreview(source)
                .SetFixedResolution(96, 96);
            Assert.IsTrue(clone.RenderNow(), "URP rejected the clone preview request.");
            Assert.IsTrue(raw.RenderNow(), "URP rejected the RenderMesh preview request.");
            yield return null;

            int cloneCoverage = CountPixels(ReadPixels(clone.texture), pixel => pixel.a > 32);
            int rawCoverage = CountPixels(ReadPixels(raw.texture), pixel => pixel.a > 32);
            Assert.Greater(rawCoverage, 500, "URP rendered no camera-targeted mesh coverage.");
            Assert.AreEqual(cloneCoverage, rawCoverage, 128,
                "URP clone and RenderMesh framing diverged.");

            source.layer = 30;
            using var scene = NowModelPreview.FromSceneObject(source, 1 << 30)
                .SetFixedResolution(96, 96);
            Assert.IsTrue(scene.RenderNow(), "URP rejected the borrowed scene-object request.");
            yield return null;
            int sceneCoverage = CountPixels(ReadPixels(scene.texture), pixel => pixel.a > 32);
            Assert.Greater(sceneCoverage, 500, "URP rendered no borrowed scene-object coverage.");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = previousDefault;
            QualitySettings.renderPipeline = previousQuality;
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pipeline);
        }
    }

    [UnityTest]
    public IEnumerator InstalledUrpRendererFeatureDrawsNowUi()
    {
        var assetType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);
        var rendererDataType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.ScriptableRendererData, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);
        var featureType = System.Type.GetType(
            "NowUI.NowUniversalRendererFeature, NowUI.URP",
            throwOnError: false);

        if (assetType == null || rendererDataType == null || featureType == null)
        {
            Assert.Ignore("URP and NowUI.URP are not installed in this validation project.");
            yield break;
        }

        var create = assetType.GetMethod(
            "Create",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { rendererDataType },
            null);
        Assert.NotNull(create);

        var previousDefault = GraphicsSettings.defaultRenderPipeline;
        var previousQuality = QualitySettings.renderPipeline;
        var pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { null });
        var rendererDataField = assetType.GetField(
            "m_RendererDataList",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(rendererDataField);
        var rendererDataArray = (System.Array)rendererDataField.GetValue(pipeline);
        var rendererData = (ScriptableObject)rendererDataArray.GetValue(0);
        var feature = (ScriptableObject)ScriptableObject.CreateInstance(featureType);
        var features = (System.Collections.IList)rendererDataType
            .GetProperty("rendererFeatures")
            .GetValue(rendererData);
        features.Add(feature);
        rendererDataType.GetMethod("SetDirty").Invoke(rendererData, null);
        var cameraObject = new GameObject("URP NowUI Feature Test Camera");
        var graphicObject = new GameObject("URP NowUI Feature Test Graphic");
        var target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);

        try
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            yield return null;
            yield return null;

            target.Create();
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 0;
            var graphic = graphicObject.AddComponent<NowPipelineGraphic>();
            graphic.targetCamera = camera;
            graphic.rebuildNowUI += (_, _, rect) =>
                Now.Rectangle(rect).SetColor(Color.green).Draw();

            var request = new RenderPipeline.StandardRequest
            {
                destination = target,
                mipLevel = 0,
                slice = 0,
                face = CubemapFace.Unknown
            };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request));
            RenderPipeline.SubmitRenderRequest(camera, request);
            yield return null;

            var pixels = ReadPixels(target);
            int green = CountPixels(
                pixels,
                pixel => pixel.g > 180 && pixel.r < 80 && pixel.b < 80 && pixel.a > 180);
            Assert.Greater(green, 96 * 96 - 500,
                "The installed NowUI URP renderer feature did not composite its RenderGraph pass.");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = previousDefault;
            QualitySettings.renderPipeline = previousQuality;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(graphicObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(rendererData);
            Object.DestroyImmediate(pipeline);
        }
    }

    [UnityTest]
    public IEnumerator ModelPreviewSceneLightingIsOptIn()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var sceneLightObject = new GameObject("Scene Directional Light");
        var sceneLight = sceneLightObject.AddComponent<Light>();
        sceneLight.type = LightType.Directional;
        sceneLight.color = Color.red;
        sceneLight.intensity = 4f;
        sceneLight.cullingMask = 1 << 31;
        sceneLight.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        sceneLight.enabled = false;

        try
        {
            using var preview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black)
                .SetLight(Quaternion.identity, 0f, Color.white);

            Assert.IsTrue(preview.RenderNow());
            yield return null;
            Color32 noSceneLight = ReadPixels(preview.texture)[32 * 64 + 32];

            sceneLight.enabled = true;
            preview.MarkDirty();
            Assert.IsTrue(preview.RenderNow());
            yield return null;
            Color32 isolated = ReadPixels(preview.texture)[32 * 64 + 32];

            preview.SetSceneLightingEnabled();
            Assert.IsTrue(preview.RenderNow());
            yield return null;
            Color32 sceneLit = ReadPixels(preview.texture)[32 * 64 + 32];

            Assert.AreEqual(
                noSceneLight.r,
                isolated.r,
                4,
                $"A scene directional crossed into the private preview scene ({noSceneLight} -> {isolated}).");
            Assert.Greater(
                sceneLit.r,
                isolated.r + 40,
                $"Scene light did not stay behind the opt-in (isolated {isolated}, scene {sceneLit}).");
            Assert.Greater(sceneLit.r, sceneLit.g + 40);
        }
        finally
        {
            Object.DestroyImmediate(sceneLightObject);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator SharedPreviewLightReappliesSettingsWhenPreviewsAlternate()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var redPreview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black)
                .SetLight(Quaternion.Euler(35f, 145f, 0f), 3f, Color.red);
            using var bluePreview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black)
                .SetLight(Quaternion.Euler(35f, 145f, 0f), 3f, Color.blue);

            Assert.IsTrue(redPreview.RenderNow());
            yield return null;
            Color32 firstRed = ReadPixels(redPreview.texture)[32 * 64 + 32];

            Assert.IsTrue(bluePreview.RenderNow());
            yield return null;
            Color32 blue = ReadPixels(bluePreview.texture)[32 * 64 + 32];

            Assert.IsTrue(redPreview.RenderNow());
            yield return null;
            Color32 secondRed = ReadPixels(redPreview.texture)[32 * 64 + 32];

            Assert.Greater(firstRed.r, firstRed.b + 20, $"First red render was {firstRed}.");
            Assert.Greater(blue.b, blue.r + 20, $"Blue render inherited another preview's light: {blue}.");
            Assert.Greater(secondRed.r, secondRed.b + 20,
                $"Alternating back to red did not restore its light settings: {secondRed}.");
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void ModelPreviewNeverOwnsCallerParticleSimulation()
    {
        var source = new GameObject("Particle Preview Source");
        var sourceParticles = source.AddComponent<ParticleSystem>();
        var main = sourceParticles.main;
        main.playOnAwake = true;
        sourceParticles.Play();
        bool originallyPlaying = sourceParticles.isPlaying;

        try
        {
            using (var preview = new NowModelPreview(source)
                .SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame))
            {
                preview.Prepare(32f, 32f);
                preview.SetRenderingEnabled(false);
                Assert.AreEqual(originallyPlaying, sourceParticles.isPlaying);
            }

            using (var preview = NowModelPreview.FromSceneObject(source)
                .SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame))
            {
                preview.Prepare(32f, 32f);
                preview.SetRenderingEnabled(false);
                Assert.AreEqual(originallyPlaying, sourceParticles.isPlaying);
            }
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void RoundedCornersClipPixels()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(32, 32, 64, 64))
                .SetColor(Color.white)
                .SetRadius(24)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        int filled = CountPixels(pixels, p => p.a > 200);

        int square = 64 * 64;
        int expectedRemoved = Mathf.RoundToInt(24 * 24 * (4f - Mathf.PI));

        Assert.Less(filled, square - expectedRemoved / 2, "Corners do not appear rounded.");
        Assert.Greater(filled, square - expectedRemoved * 2, "Far too few pixels for the rounded rect.");
    }

    [Test]
    public void RectangleZeroOutlineDoesNotCutAlphaFromOpaqueBackdrop()
    {
        using (_renderer.Begin(_target))
        {
            // Overscan the opaque backdrop so the rectangle's intentional
            // half-pixel AA edge lies outside the render target.
            Now.Rectangle(new NowRect(-1, -1, Side + 2, Side + 2))
                .SetColor(new Color(0.18f, 0.2f, 0.24f, 1f))
                .Draw();
            Now.Rectangle(new NowRect(32, 24, 64, 80))
                .SetRadius(18f)
                .SetColor(new Color(0.12f, 0.66f, 0.95f, 0.36f))
                .SetOutline(0f)
                .SetOutlineColor(Color.black)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        AssertTargetOpaque(ReadPixels(_target), "Zero-outline rectangle reduced target alpha");
    }

    [Test]
    public void RectangleOutlineVariantsStayOpaqueAndVisible()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(-1, -1, Side + 2, Side + 2))
                .SetColor(new Color(0.18f, 0.2f, 0.24f, 1f))
                .Draw();
            Now.Rectangle(new NowRect(24, 24, 80, 80))
                .SetRadius(18f)
                .SetColor(new Color(1f, 1f, 1f, 0f))
                .SetOutline(4f)
                .SetOutlineColor(Color.green)
                .Draw();
            Now.Rectangle(new NowRect(44, 44, 40, 40))
                .SetRadius(10f)
                .SetColor(new Color(1f, 0.26f, 0.32f, 0.34f))
                .SetOutline(1f)
                .SetOutlineColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        AssertTargetOpaque(pixels, "Outlined rectangle variant reduced target alpha");
        Assert.Greater(
            CountPixels(pixels, p => p.g > 160 && p.r < 80 && p.b < 120),
            120,
            "Outline-only rectangle did not render a visible green outline.");
    }

    [Test]
    public void GlassBlursPreviouslyRenderedRenderTextureContent()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(-1, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.blue)
                .Draw();
            Now.Glass(new NowRect(48, 16, 32, 96))
                .SetBlurRadius(24f)
                .SetTint(new Color(1f, 1f, 1f, 0f))
                .SetVibrancy(1f, 1f)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        Color32 redOutside = pixels[(Side / 2) * Side + 32];
        Color32 blueOutside = pixels[(Side / 2) * Side + 96];
        Color32 blurredInside = pixels[(Side / 2) * Side + 56];

        Assert.Greater(redOutside.r, 180, "Left background should stay red outside the glass pane.");
        Assert.Less(redOutside.b, 60, "Left background should stay sharp outside the glass pane.");
        Assert.Greater(blueOutside.b, 180, "Right background should stay blue outside the glass pane.");
        Assert.Less(blueOutside.r, 60, "Right background should stay sharp outside the glass pane.");
        Assert.Greater(blurredInside.r, 80, "Glass should retain red contribution from the backdrop.");
        Assert.Greater(blurredInside.b, 25, "Glass should blur blue contribution across the boundary.");
    }

    [Test]
    public void GlassZeroOutlineDoesNotCutAlphaFromOpaqueBackdrop()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(0, 0, Side / 2f, Side))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, 0, Side / 2f, Side))
                .SetColor(Color.blue)
                .Draw();
            Now.Glass(new NowRect(32, 24, 64, 80))
                .SetRadius(18f)
                .SetBlurRadius(16f)
                .SetTint(new Color(1f, 1f, 1f, 0.15f))
                .SetOutline(0f)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        for (int y = 24; y < 104; ++y)
        {
            for (int x = 32; x < 96; ++x)
            {
                var pixel = pixels[y * Side + x];
                Assert.GreaterOrEqual(pixel.a, 250, $"Glass edge reduced target alpha at {x},{y}.");
            }
        }
    }

    [Test]
    public void GlassOutlineVariantsDoNotCutAlphaFromOpaqueBackdrop()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(-1, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.blue)
                .Draw();
            Now.Glass(new NowRect(32, 24, 64, 80))
                .SetRadius(18f)
                .SetBlurRadius(16f)
                .SetTint(new Color(1f, 1f, 1f, 0f))
                .SetOutline(3f)
                .SetOutlineColor(new Color(1f, 0.74f, 0.24f, 0.9f))
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        AssertTargetOpaque(pixels, "Outlined glass variant reduced target alpha");
        Assert.Greater(
            CountPixels(pixels, p => p.r > 180 && p.g > 120 && p.b < 120),
            100,
            "Glass outline variant did not render a visible warm outline.");
    }

    [Test]
    public void TextRendersInkWithNativeCompiler()
    {
        AssertTextRendersInk(ResolveDefaultNowFont());
    }

    [Test]
    public void TextRendersInkWithManagedCompiler()
    {
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont managedFont, out string error), error);

        try
        {
            AssertTextRendersInk(managedFont);
        }
        finally
        {
            Object.DestroyImmediate(managedFont);
        }
    }

    void AssertTextRendersInk(NowFontAsset font)
    {
        using (_renderer.Begin(_target))
        {
            Now.Text(new NowRect(8, 24, Side - 16, 80), font)
                .SetFontSize(56)
                .SetColor(Color.white)
                .Draw("HM");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        int ink = CountPixels(pixels, p => p.a > 128);

        Assert.Greater(ink, 500, "Text produced almost no ink.");
        Assert.Less(ink, Side * Side / 2, "Text ink coverage is implausibly large.");
    }

    [Test]
    public void LottieRendersGeometry()
    {
        const string RedSquare = @"{
            ""v"": ""5.5.0"", ""fr"": 30, ""ip"": 0, ""op"": 30, ""w"": 100, ""h"": 100,
            ""layers"": [{
                ""ddd"": 0, ""ty"": 4, ""ip"": 0, ""op"": 30, ""st"": 0,
                ""ks"": {
                    ""o"": { ""a"": 0, ""k"": 100 },
                    ""p"": { ""a"": 0, ""k"": [50, 50] },
                    ""a"": { ""a"": 0, ""k"": [0, 0] },
                    ""s"": { ""a"": 0, ""k"": [100, 100] },
                    ""r"": { ""a"": 0, ""k"": 0 }
                },
                ""shapes"": [{
                    ""ty"": ""gr"",
                    ""it"": [
                        { ""ty"": ""rc"", ""p"": { ""a"": 0, ""k"": [0, 0] }, ""s"": { ""a"": 0, ""k"": [60, 60] }, ""r"": { ""a"": 0, ""k"": 0 } },
                        { ""ty"": ""fl"", ""c"": { ""a"": 0, ""k"": [1, 0, 0, 1] }, ""o"": { ""a"": 0, ""k"": 100 } },
                        {
                            ""ty"": ""tr"",
                            ""p"": { ""a"": 0, ""k"": [0, 0] }, ""a"": { ""a"": 0, ""k"": [0, 0] },
                            ""s"": { ""a"": 0, ""k"": [100, 100] }, ""r"": { ""a"": 0, ""k"": 0 }, ""o"": { ""a"": 0, ""k"": 100 }
                        }
                    ]
                }]
            }]
        }";

        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource(RedSquare);

            using (_renderer.Begin(_target))
            {
                Now.Lottie(new NowRect(14, 14, 100, 100), asset)
                    .SetTime(0f)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);

            int red = CountPixels(pixels, p => p.r > 150 && p.g < 80 && p.a > 128);

            Assert.Greater(red, 2000, "Lottie shape produced too few red pixels.");
            Assert.Less(red, 5000, "Lottie shape produced implausibly many red pixels.");
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [UnityTest]
    public IEnumerator UGUIElementsOccludeNowUIPointer()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var hostObject = new GameObject("Host", typeof(NowGraphic));
        hostObject.transform.SetParent(canvasObject.transform, false);
        var hostRect = hostObject.GetComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.zero;
        hostRect.pivot = Vector2.zero;
        hostRect.anchoredPosition = Vector2.zero;
        hostRect.sizeDelta = new Vector2(200, 200);
        var host = hostObject.GetComponent<NowGraphic>();

        var blockerObject = new GameObject("Blocker", typeof(UnityEngine.UI.Image));
        blockerObject.transform.SetParent(canvasObject.transform, false);
        var blockerRect = blockerObject.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.zero;
        blockerRect.pivot = Vector2.zero;
        blockerRect.anchoredPosition = Vector2.zero;
        blockerRect.sizeDelta = new Vector2(100, 100);

        try
        {
            yield return null;

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, new Vector2(50, 50)),
                "A UGUI element above the host must occlude the pointer.");
            Assert.IsTrue(
                NowRaycastGate.IsPointerAllowed(host, new Vector2(150, 150)),
                "The pointer over the host itself must pass.");
        }
        finally
        {
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator HostOwnedOverlayExtendingPastHostAllowsOnlyLowerUGUI()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var backgroundObject = new GameObject("Background", typeof(UnityEngine.UI.Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.zero;
        backgroundRect.pivot = Vector2.zero;
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(300, 300);

        var hostObject = new GameObject("Host", typeof(NowGraphic));
        hostObject.transform.SetParent(canvasObject.transform, false);
        var hostRect = hostObject.GetComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.zero;
        hostRect.pivot = Vector2.zero;
        hostRect.anchoredPosition = Vector2.zero;
        hostRect.sizeDelta = new Vector2(100, 100);
        var host = hostObject.GetComponent<NowGraphic>();

        GameObject blockerObject = null;

        try
        {
            yield return null;

            var popupPointOutsideHost = new Vector2(150, 50);

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost),
                "Strict gating still requires the EventSystem hit to be the host.");
            Assert.IsTrue(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "Host-owned overlays may receive input over lower UGUI behind the host.");

            blockerObject = new GameObject("Higher Blocker", typeof(UnityEngine.UI.Image));
            blockerObject.transform.SetParent(canvasObject.transform, false);
            var blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.zero;
            blockerRect.pivot = Vector2.zero;
            blockerRect.anchoredPosition = new Vector2(125, 25);
            blockerRect.sizeDelta = new Vector2(50, 50);

            yield return null;

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "UGUI drawn above the host must still occlude host-owned overlays.");
        }
        finally
        {
            if (blockerObject != null)
                Object.DestroyImmediate(blockerObject);

            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(backgroundObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator HostOwnedWorldOverlayIgnoresSeparateCanvasBehindHost()
    {
        NowOverlay.Reset();

        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var cameraObject = new GameObject("Event Camera");
        var behindCanvasObject = new GameObject("Behind Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var behindObject = new GameObject("Behind", typeof(NowGraphic));
        var hostCanvasObject = new GameObject("Host Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var hostObject = new GameObject("Host", typeof(NowGraphic));
        GameObject frontCanvasObject = null;
        GameObject frontObject = null;

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.pixelRect = new Rect(0, 0, 400, 400);
            cameraObject.transform.position = new Vector3(0, 0, -10);

            var behindCanvas = behindCanvasObject.GetComponent<Canvas>();
            behindCanvas.renderMode = RenderMode.WorldSpace;
            behindCanvas.worldCamera = camera;
            behindCanvasObject.transform.position = new Vector3(0, 0, 1f);

            behindObject.transform.SetParent(behindCanvasObject.transform, false);
            var behindRect = behindObject.GetComponent<RectTransform>();
            behindRect.anchorMin = new Vector2(0.5f, 0.5f);
            behindRect.anchorMax = new Vector2(0.5f, 0.5f);
            behindRect.pivot = new Vector2(0.5f, 0.5f);
            behindRect.anchoredPosition = Vector2.zero;
            behindRect.sizeDelta = new Vector2(4f, 4f);
            var behindHost = behindObject.GetComponent<NowGraphic>();

            var hostCanvas = hostCanvasObject.GetComponent<Canvas>();
            hostCanvas.renderMode = RenderMode.WorldSpace;
            hostCanvas.worldCamera = camera;
            hostCanvasObject.transform.position = Vector3.zero;

            hostObject.transform.SetParent(hostCanvasObject.transform, false);
            var hostRect = hostObject.GetComponent<RectTransform>();
            hostRect.anchorMin = new Vector2(0.5f, 0.5f);
            hostRect.anchorMax = new Vector2(0.5f, 0.5f);
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.anchoredPosition = Vector2.zero;
            hostRect.sizeDelta = new Vector2(1f, 1f);
            var host = hostObject.GetComponent<NowGraphic>();

            yield return null;

            var popupPointOutsideHost = (Vector2)camera.WorldToScreenPoint(new Vector3(1.5f, 0f, 0f));

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost),
                "Strict gating still requires the world-space hit to be the host.");
            Assert.IsTrue(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "Host-owned overlays must ignore separate world-space canvases behind the NowUI host.");

            using (NowOverlay.Host(host, hostRect, camera))
                NowOverlay.DeferScreen(new NowRect(1.75f, 0.25f, 0.5f, 0.5f), 606, _ => { });

            Assert.IsFalse(
                NowOverlay.IsPointerBlockedByForeignOverlay(host, popupPointOutsideHost),
                "A host-owned overlay must not block its own host.");
            Assert.IsTrue(
                NowOverlay.IsPointerBlockedByForeignOverlay(behindHost, popupPointOutsideHost),
                "A host-owned overlay on the front canvas must block NowUI hosts behind it.");

            frontCanvasObject = new GameObject("Front Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            var frontCanvas = frontCanvasObject.GetComponent<Canvas>();
            frontCanvas.renderMode = RenderMode.WorldSpace;
            frontCanvas.worldCamera = camera;
            frontCanvasObject.transform.position = new Vector3(0, 0, -1f);

            frontObject = new GameObject("Front", typeof(UnityEngine.UI.Image));
            frontObject.transform.SetParent(frontCanvasObject.transform, false);
            var frontRect = frontObject.GetComponent<RectTransform>();
            frontRect.anchorMin = new Vector2(0.5f, 0.5f);
            frontRect.anchorMax = new Vector2(0.5f, 0.5f);
            frontRect.pivot = new Vector2(0.5f, 0.5f);
            frontRect.anchoredPosition = Vector2.zero;
            frontRect.sizeDelta = new Vector2(4f, 4f);

            yield return null;

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "World-space UGUI in front of the NowUI host must still occlude host-owned overlays.");
        }
        finally
        {
            NowOverlay.Reset();

            if (frontObject != null)
                Object.DestroyImmediate(frontObject);

            if (frontCanvasObject != null)
                Object.DestroyImmediate(frontCanvasObject);

            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(hostCanvasObject);
            Object.DestroyImmediate(behindObject);
            Object.DestroyImmediate(behindCanvasObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator ScreenSpaceUGUIOccludesWorldGraphicPointer()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var blockerObject = new GameObject("Screen UI Blocker", typeof(UnityEngine.UI.Image));
        var cameraObject = new GameObject("Now World Camera");
        var panelObject = new GameObject("Now World Panel");

        try
        {
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            blockerObject.transform.SetParent(canvasObject.transform, false);
            var blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.zero;
            blockerRect.pivot = Vector2.zero;
            blockerRect.anchoredPosition = Vector2.zero;
            blockerRect.sizeDelta = new Vector2(200, 200);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);

            var panel = panelObject.AddComponent<WorldPanelDriver>();
            panel.facingMode = NowWorldFacingMode.None;
            panel.targetCamera = camera;
            panel.size = new Vector2(100f, 50f);
            panel.pixelsPerUnit = 100f;
            panel.pivot = new Vector2(0.5f, 0.5f);

            yield return null;

            var provider = panel.GetWorldInputProviderForTest();
            var surface = new NowInputSurface(panel.size);
            var press = new NowMouseInput
            {
                hasPointer = true,
                screenPosition = new Vector2(100, 100),
                pointerButtonsDown = NowPointerButtons.Primary,
                pointerButtonsPressed = NowPointerButtons.Primary
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, press, out var blockedSnapshot));
            Assert.IsFalse(blockedSnapshot.hasPointer, "Screen-space UGUI must block world-space NowUI input beneath it.");

            panel.blockedWhenPointerOverUGUI = false;
            provider = panel.GetWorldInputProviderForTest();
            provider.ResetPosition();

            Assert.IsTrue(provider.TryGetSnapshot(surface, press, out var allowedSnapshot));
            Assert.IsTrue(allowedSnapshot.hasPointer, "World-space NowUI can opt out of screen-space UGUI blocking.");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator EventSystemSelectionSuspendsNowFocus()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var selectable = new GameObject("Selected");

        try
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Assert.NotNull(eventSystem, "EventSystem must be live in play mode.");

            NowFocus.Focus(7);
            eventSystem.SetSelectedGameObject(selectable);
            Assert.NotNull(eventSystem.currentSelectedGameObject);

            yield return null;
            NowFocus.Register(1, new NowRect(0, 0, 10, 10));

            Assert.AreEqual(0, NowFocus.focusedId, "UGUI selection must clear NowUI focus.");

            eventSystem.SetSelectedGameObject(selectable);
            NowFocus.Focus(9);
            Assert.IsNull(eventSystem.currentSelectedGameObject, "NowUI focus must deselect the EventSystem.");
            Assert.AreEqual(9, NowFocus.focusedId);
        }
        finally
        {
            NowFocus.Reset();
            Object.DestroyImmediate(selectable);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    /// <summary>Draws through the legacy GL/DrawMeshNow camera path on OnPostRender.</summary>
    class GLPathDriver : MonoBehaviour
    {
        public bool drew;

        void OnPostRender()
        {
            using (Now.StartUI(new NowRect(0, 0, Side, Side)))
            {
                Now.Rectangle(new NowRect(32, 32, 64, 64))
                    .SetColor(Color.green)
                    .Draw();
            }

            drew = true;
        }
    }

    [UnityTest]
    public IEnumerator CameraGLPathRendersThroughOnPostRender()
    {
        var go = new GameObject("Now GL Test Camera");

        try
        {
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 0;
            camera.targetTexture = _target;

            var driver = go.AddComponent<GLPathDriver>();

            camera.Render();
            yield return null;

            Assert.IsTrue(driver.drew, "OnPostRender did not run.");

            var pixels = ReadPixels(_target);
            int green = CountPixels(pixels, p => p.g > 150 && p.a > 128);

            Assert.Greater(green, 64 * 64 - 600, "GL path rendered too few pixels.");
            Assert.Greater(pixels[(Side / 2) * Side + Side / 2].g, 150);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    class WorldPanelDriver : NowWorldGraphic
    {
        public Color color = Color.red;

        public NowWorldInputProvider GetWorldInputProviderForTest()
        {
            return (NowWorldInputProvider)GetInputProvider();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(color)
                .Draw();
        }
    }

    [UnityTest]
    public IEnumerator WorldGraphicRendersThroughCamera()
    {
        var cameraObject = new GameObject("Now World Test Camera");
        var panelObject = new GameObject("Now World Test Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.targetTexture = _target;
            cameraObject.transform.position = new Vector3(0f, 0f, -2f);

            var panel = panelObject.AddComponent<WorldPanelDriver>();
            panel.targetCamera = camera;
            panel.size = new Vector2(64f, 64f);
            panel.pixelsPerUnit = 100f;
            panel.depthMode = NowWorldDepthMode.AlwaysVisible;
            panel.RebuildNowUI();

            camera.Render();
            yield return null;

            var pixels = ReadPixels(_target);
            int red = CountPixels(pixels, p => p.r > 180 && p.g < 60 && p.a > 180);

            Assert.Greater(red, 1000, "World-space NowUI produced too few red pixels.");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [UnityTest]
    public IEnumerator WorldGraphicDepthModeCanRespectSceneDepth()
    {
        var cameraObject = new GameObject("Now World Depth Camera");
        var panelObject = new GameObject("Now World Depth Panel");
        var blockerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.targetTexture = _target;
            cameraObject.transform.position = new Vector3(0f, 0f, -2f);

            blockerObject.name = "Now World Depth Blocker";
            blockerObject.transform.position = new Vector3(0f, 0f, -0.2f);
            blockerObject.transform.localScale = new Vector3(1.4f, 1.4f, 0.1f);

            var panel = panelObject.AddComponent<WorldPanelDriver>();
            panel.targetCamera = camera;
            panel.size = new Vector2(64f, 64f);
            panel.pixelsPerUnit = 100f;

            panel.depthMode = NowWorldDepthMode.SceneOccluded;
            panel.RebuildNowUI();
            camera.Render();
            yield return null;

            int occludedRed = CountPixels(ReadPixels(_target), p => p.r > 180 && p.g < 60 && p.a > 180);

            panel.depthMode = NowWorldDepthMode.AlwaysVisible;
            panel.RebuildNowUI();
            camera.Render();
            yield return null;

            int visibleRed = CountPixels(ReadPixels(_target), p => p.r > 180 && p.g < 60 && p.a > 180);

            Assert.Less(occludedRed, 200, "Scene-occluded world UI should be hidden by nearer geometry.");
            Assert.Greater(visibleRed, 1000, "Always-visible world UI should draw over nearer geometry.");
        }
        finally
        {
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
