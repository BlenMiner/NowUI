using System;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NowModelPreviewTests
{
    sealed class FakeDynamicTextureHost : INowDynamicTextureHost
    {
        public int dynamicTextureBuildVersion { get; private set; }
        public bool isDynamicTextureHostValid { get; set; } = true;
        public int rebuildRequests { get; private set; }

        public void BeginDynamicTextureBuild()
        {
            ++dynamicTextureBuildVersion;
        }

        public void RequestDynamicTextureRebuild()
        {
            ++rebuildRequests;
        }
    }

    NowDrawList _drawList;
    float _previousScale;

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        _previousScale = Now.uiScale;
        Now.SetUIScale(1f);
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        Now.SetUIScale(_previousScale);
    }

    [Test]
    public void ModelDrawPreparesPremultipliedTexturedRectangle()
    {
        using var preview = new NowModelPreview();

        using (_drawList.Begin(new Vector2(256f, 128f)))
        {
            Now.Model(new NowRect(10f, 12f, 80f, 40f), preview)
                .SetRadius(8f)
                .Draw();
        }

        Assert.NotNull(preview.texture);
        Assert.AreEqual(80, preview.texture.width);
        Assert.AreEqual(40, preview.texture.height);
        Assert.Greater(preview.texture.depth, 0, "The preview target needs a depth buffer for 3D geometry.");
        Assert.AreEqual(RenderTextureFormat.ARGB32, preview.texture.format);
        Assert.AreEqual(FilterMode.Bilinear, preview.texture.filterMode);
        Assert.AreEqual(TextureWrapMode.Clamp, preview.texture.wrapMode);
        Assert.IsFalse(preview.texture.useMipMap);
        Assert.AreEqual(1, preview.texture.antiAliasing);
        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(NowMeshKind.TexturedRectangle, _drawList.batches[0].kind);
        Assert.AreSame(preview.texture, _drawList.batches[0].material.mainTexture);
        Assert.AreEqual(
            1f,
            _drawList.batches[0].material.GetFloat("_NowPremultipliedTexture"),
            0.001f);
    }

    [Test]
    public void AutomaticResizeKeepsTextureIdentity()
    {
        using var preview = new NowModelPreview();

        using (_drawList.Begin(new Vector2(256f, 128f)))
            Now.Model(new NowRect(0f, 0f, 32f, 16f), preview).Draw();

        var original = preview.texture;

        using (_drawList.Begin(new Vector2(256f, 128f)))
            Now.Model(new NowRect(0f, 0f, 96f, 48f), preview).Draw();

        Assert.AreSame(original, preview.texture);
        Assert.AreEqual(96, preview.texture.width);
        Assert.AreEqual(48, preview.texture.height);
    }

    [Test]
    public void FixedResolutionCapsLongestEdgeWithoutChangingAspect()
    {
        using var preview = new NowModelPreview()
            .SetMaxResolution(64)
            .SetFixedResolution(128, 32);

        Assert.AreEqual(64, preview.texture.width);
        Assert.AreEqual(16, preview.texture.height);

        using (_drawList.Begin(new Vector2(512f, 512f)))
            Now.Model(new NowRect(0f, 0f, 400f, 300f), preview).Draw();

        Assert.AreEqual(64, preview.texture.width);
        Assert.AreEqual(16, preview.texture.height);
    }

    [Test]
    public void FixedResolutionAndMaximumAreOrderIndependent()
    {
        using var preview = new NowModelPreview()
            .SetFixedResolution(128, 32)
            .SetMaxResolution(64);

        Assert.AreEqual(64, preview.texture.width);
        Assert.AreEqual(16, preview.texture.height);
    }

    [Test]
    public void AutomaticResolutionSettingsResizeWithoutAnotherDraw()
    {
        using var preview = new NowModelPreview();

        using (_drawList.Begin(new Vector2(512f, 256f)))
            Now.Model(new NowRect(0f, 0f, 160f, 80f), preview).Draw();

        var target = preview.texture;
        preview.SetResolutionScale(0.5f);

        Assert.AreSame(target, preview.texture);
        Assert.AreEqual(80, preview.texture.width);
        Assert.AreEqual(40, preview.texture.height);
    }

    [Test]
    public void FullyClippedModelDoesNotAllocateTargetOrGeometry()
    {
        using var preview = new NowModelPreview();

        using (_drawList.Begin(new Vector2(256f, 128f)))
        using (Now.Mask(new NowRect(0f, 0f, 16f, 16f)))
            Now.Model(new NowRect(100f, 80f, 40f, 30f), preview).Draw();

        Assert.IsNull(preview.texture);
        Assert.IsFalse(_drawList.hasGeometry);
    }

    [Test]
    public void EveryFramePreviewContinuesUntilExplicitlyPaused()
    {
        using var preview = new NowModelPreview()
            .SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame);

        using (_drawList.Begin(new Vector2(256f, 128f)))
            Now.Model(new NowRect(16f, 16f, 64f, 32f), preview).Draw();

        Assert.IsTrue(preview.requiresDeferredTick);

        using (_drawList.Begin(new Vector2(256f, 128f)))
        using (Now.Mask(new NowRect(0f, 0f, 8f, 8f)))
            Now.Model(new NowRect(100f, 80f, 64f, 32f), preview).Draw();

        Assert.IsTrue(preview.requiresDeferredTick);
        preview.SetRenderingEnabled(false);
        Assert.IsFalse(preview.requiresDeferredTick);
        preview.SetRenderingEnabled(true);
        Assert.IsTrue(preview.requiresDeferredTick);
    }

    [Test]
    public void ManualPreviewSchedulesOnlyExplicitRequests()
    {
        using var preview = new NowModelPreview()
            .SetUpdateMode(NowModelPreviewUpdateMode.Manual);

        using (_drawList.Begin(new Vector2(128f, 64f)))
            Now.Model(new NowRect(0f, 0f, 64f, 32f), preview).Draw();

        Assert.IsFalse(preview.requiresDeferredTick);
        preview.RequestRender();
        Assert.IsTrue(preview.requiresDeferredTick);
    }

    [Test]
    public void TranslucentBackgroundIsStoredPremultiplied()
    {
        using var preview = new NowModelPreview()
            .SetBackground(new Color(0.8f, 0.4f, 0.2f, 0.25f));

        var expected = new Color(0.8f, 0.4f, 0.2f, 0.25f);

        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            expected = expected.linear;
            expected.r *= expected.a;
            expected.g *= expected.a;
            expected.b *= expected.a;
            expected = expected.gamma;
            expected.a = 0.25f;
        }
        else
        {
            expected.r *= expected.a;
            expected.g *= expected.a;
            expected.b *= expected.a;
        }

        Assert.AreEqual(expected.r, preview.camera.backgroundColor.r, 0.001f);
        Assert.AreEqual(expected.g, preview.camera.backgroundColor.g, 0.001f);
        Assert.AreEqual(expected.b, preview.camera.backgroundColor.b, 0.001f);
        Assert.AreEqual(0.25f, preview.camera.backgroundColor.a, 0.001f);
    }

    [Test]
    public void SourceIsClonedWithoutMutatingCallerObject()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Caller Cube";
        int originalLayer = source.layer;
        bool originalActive = source.activeSelf;

        try
        {
            using (var preview = new NowModelPreview(
                source,
                31,
                NowModelPreviewBackend.RendererClone))
            {
                Assert.AreSame(source, preview.source);
                Assert.NotNull(preview.presentationInstance);
                Assert.AreNotSame(source, preview.presentationInstance);
                Assert.AreEqual("Caller Cube (Preview)", preview.presentationInstance.name);
                Assert.AreEqual(preview.previewLayer, preview.presentationInstance.layer);
                Assert.AreEqual(originalLayer, source.layer);
                Assert.AreEqual(originalActive, source.activeSelf);
            }

            Assert.IsTrue(source != null, "Disposing the preview must not destroy its caller-owned source.");
            Assert.AreEqual(originalLayer, source.layer);
            Assert.AreEqual(originalActive, source.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void DefaultSourceUsesIsolatedRawSubmissionWithoutInstantiation()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Caller-Owned Raw Cube";
        int originalLayer = source.layer;
        bool originalActive = source.activeSelf;

        try
        {
            using (var preview = new NowModelPreview(source))
            {
                Assert.AreEqual(NowModelPreviewSourceMode.Isolated, preview.sourceMode);
                Assert.AreEqual(NowModelPreviewBackend.RenderMesh, preview.backend);
                Assert.AreSame(source, preview.source);
                Assert.IsNull(preview.presentationInstance);
                Assert.AreEqual(1, preview.renderMeshSourceCount);
                Assert.GreaterOrEqual(preview.renderMeshDrawCount, 1);
                Assert.AreEqual(1, preview.stagingRigGameObjectCount);
                Assert.AreEqual(0, preview.unsupportedRendererCount);
                Assert.AreEqual(originalLayer, source.layer);
                Assert.AreEqual(originalActive, source.activeSelf);
            }

            Assert.IsTrue(source != null);
            Assert.AreEqual(originalLayer, source.layer);
            Assert.AreEqual(originalActive, source.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void SceneObjectModeBorrowsHierarchyAndDerivesRendererLayers()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.layer = 8;
        var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        child.layer = 9;
        child.transform.SetParent(source.transform, false);
        var renderer = source.GetComponent<Renderer>();
        renderer.forceRenderingOff = true;
        uint originalRenderingLayers = renderer.renderingLayerMask;

        try
        {
            using (var preview = NowModelPreview.FromSceneObject(source))
            {
                Assert.AreEqual(NowModelPreviewSourceMode.SceneObject, preview.sourceMode);
                Assert.AreSame(source, preview.source);
                Assert.IsNull(preview.presentationInstance);
                Assert.AreEqual(0, preview.renderMeshSourceCount);
                Assert.AreEqual(1, preview.stagingRigGameObjectCount);
                Assert.AreEqual((1 << 8) | (1 << 9), preview.cullingMask.value);
                Assert.AreEqual(preview.cullingMask.value, preview.camera.cullingMask);
                Assert.IsTrue(preview.sceneLightingEnabled);
                Assert.IsFalse(preview.camera.scene.IsValid());
                Assert.IsTrue(renderer.forceRenderingOff);
                Assert.AreEqual(originalRenderingLayers, renderer.renderingLayerMask);
            }

            Assert.IsTrue(source != null, "Disposing a scene-object preview must not destroy its source.");
            Assert.IsTrue(renderer.forceRenderingOff);
            Assert.AreEqual(originalRenderingLayers, renderer.renderingLayerMask);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void SourceSpecificControlsFailClearlyInTheWrongMode()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var isolated = new NowModelPreview(source);
            using var scene = NowModelPreview.FromSceneObject(source, 1 << source.layer);

            Assert.Throws<InvalidOperationException>(() =>
                isolated.SetSceneCullingMask(1 << source.layer));
            Assert.Throws<InvalidOperationException>(() =>
                scene.SetRotation(Quaternion.Euler(0f, 20f, 0f)));
            Assert.Throws<InvalidOperationException>(() =>
                scene.SetSceneLightingEnabled(false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                scene.SetSceneCullingMask(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void IsolatedRenderMeshCapturesStaticDrawsWithoutAClone()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Raw Caller Cube";
        int originalLayer = source.layer;

        try
        {
            using var preview = new NowModelPreview(source);

            Assert.AreEqual(NowModelPreviewBackend.RenderMesh, preview.backend);
            Assert.AreSame(source, preview.source);
            Assert.IsNull(preview.presentationInstance);
            Assert.AreEqual(1, preview.renderMeshSourceCount);
            Assert.GreaterOrEqual(preview.renderMeshDrawCount, 1);
            Assert.AreEqual(0, preview.unsupportedRendererCount);
            Assert.AreEqual(originalLayer, source.layer);
            Assert.AreEqual("Raw Caller Cube", source.name);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void IsolatedRenderMeshRefreshesItsStaticSnapshotOnReframe()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var preview = new NowModelPreview(source);
            Assert.AreEqual(1, preview.renderMeshSourceCount);

            var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.transform.SetParent(source.transform, false);
            preview.Reframe();

            Assert.AreEqual(2, preview.renderMeshSourceCount);
            Assert.GreaterOrEqual(preview.renderMeshDrawCount, 2);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void RepeatedRawReframeDoesNotCompoundNormalization()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.localScale = new Vector3(8f, 3f, 2f);

        try
        {
            using var preview = new NowModelPreview(source);
            float initialScale = preview.normalizedContentScale;

            preview.Reframe();
            float firstReframeScale = preview.normalizedContentScale;
            preview.Reframe();

            Assert.AreEqual(initialScale, firstReframeScale, 0.0001f);
            Assert.AreEqual(initialScale, preview.normalizedContentScale, 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void IsolatedModeUsesHighestDetailLodAndReportsUnsupportedRenderers()
    {
        var source = new GameObject("LOD Preview Source");
        var high = GameObject.CreatePrimitive(PrimitiveType.Cube);
        high.transform.SetParent(source.transform, false);
        var low = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        low.transform.SetParent(source.transform, false);
        var particles = new GameObject("Unsupported Particles");
        particles.transform.SetParent(source.transform, false);
        particles.AddComponent<ParticleSystem>();
        var lodGroup = source.AddComponent<LODGroup>();
        lodGroup.SetLODs(new[]
        {
            new LOD(0.5f, new[] { high.GetComponent<Renderer>() }),
            new LOD(0.1f, new[] { low.GetComponent<Renderer>() })
        });
        lodGroup.RecalculateBounds();

        try
        {
            using var preview = new NowModelPreview(source);

            Assert.AreEqual(1, preview.renderMeshSourceCount);
            Assert.GreaterOrEqual(preview.renderMeshDrawCount, 1);
            Assert.AreEqual(1, preview.unsupportedRendererCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void SourceIsCenteredAndNormalizedWhileStagingRigIsInactive()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.position = new Vector3(4f, 2f, -3f);
        source.transform.localScale = new Vector3(3f, 1f, 1f);

        try
        {
            using var preview = new NowModelPreview(
                source,
                31,
                NowModelPreviewBackend.RendererClone);
            var cloneRenderer = preview.presentationInstance.GetComponent<Renderer>();
            var contentPivot = preview.presentationInstance.transform.parent.parent;
            float largest = Mathf.Max(
                cloneRenderer.bounds.size.x,
                Mathf.Max(cloneRenderer.bounds.size.y, cloneRenderer.bounds.size.z));

            Assert.AreEqual(2f, largest, 0.02f);
            Assert.Less(Vector3.Distance(cloneRenderer.bounds.center, contentPivot.position), 0.02f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void InitiallyDisabledCloneRendererCanBeEnabledForEquipmentVariants()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.GetComponent<Renderer>().enabled = false;

        try
        {
            using var preview = new NowModelPreview(
                source,
                31,
                NowModelPreviewBackend.RendererClone);
            var cloneRenderer = preview.presentationInstance.GetComponent<Renderer>();
            cloneRenderer.enabled = true;

            preview.SetRenderersVisible(true);
            Assert.IsFalse(cloneRenderer.forceRenderingOff);
            preview.SetRenderersVisible(false);
            Assert.IsTrue(cloneRenderer.forceRenderingOff);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void ReframeDiscoversAndIsolatesNewEquipmentRenderers()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var preview = new NowModelPreview(
                source,
                31,
                NowModelPreviewBackend.RendererClone);
            var equipment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            equipment.name = "Runtime Equipment";
            equipment.transform.SetParent(preview.presentationInstance.transform, false);
            var equipmentRenderer = equipment.GetComponent<Renderer>();
            equipmentRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

            preview.Reframe();

            Assert.AreEqual(preview.previewLayer, equipment.layer);
            Assert.IsTrue(equipmentRenderer.forceRenderingOff);
            Assert.AreEqual(
                UnityEngine.Rendering.LightProbeUsage.Off,
                equipmentRenderer.lightProbeUsage);

            preview.SetRenderersVisible(true);
            Assert.IsFalse(equipmentRenderer.forceRenderingOff);
            preview.SetRenderersVisible(false);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void ReplacingSourceDestroysOnlyThePreviousClone()
    {
        var first = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var second = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        try
        {
            using var preview = new NowModelPreview(
                first,
                31,
                NowModelPreviewBackend.RendererClone);
            var firstClone = preview.presentationInstance;

            preview.SetSource(second);

            Assert.IsTrue(firstClone == null);
            Assert.AreSame(second, preview.source);
            Assert.NotNull(preview.presentationInstance);
            Assert.AreNotSame(second, preview.presentationInstance);
            Assert.IsTrue(first != null);
            Assert.IsTrue(second != null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void PipelineGraphicRejectsPreviewCamera()
    {
        var hostObject = new GameObject("Pipeline Host");

        try
        {
            var host = hostObject.AddComponent<NowPipelineGraphic>();

            using var preview = new NowModelPreview();
            Assert.IsFalse(host.CanRender(preview.camera));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostObject);
        }
    }

    [Test]
    public void PreviewCameraIsRegisteredAndSanitizesNonVisualRuntimeComponents()
    {
        var source = new GameObject("Animated Source");
        var animator = source.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var collider = source.AddComponent<BoxCollider>();
        var listener = source.AddComponent<AudioListener>();

        try
        {
            using var preview = new NowModelPreview(
                source,
                31,
                NowModelPreviewBackend.RendererClone);
            Assert.AreEqual(CameraType.Preview, preview.camera.cameraType);
            Assert.IsTrue(NowModelPreview.IsPreviewCamera(preview.camera));
            Assert.IsFalse(preview.presentationInstance.GetComponent<Animator>().enabled);
            Assert.AreEqual(
                AnimatorCullingMode.AlwaysAnimate,
                preview.presentationInstance.GetComponent<Animator>().cullingMode);
            Assert.IsFalse(preview.presentationInstance.GetComponent<BoxCollider>().enabled);
            Assert.IsFalse(preview.presentationInstance.GetComponent<AudioListener>().enabled);
            Assert.IsTrue(collider.enabled, "The caller-owned source must stay untouched.");
            Assert.IsTrue(listener.enabled, "The caller-owned source must stay untouched.");

            preview.SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame);

            using (_drawList.Begin(new Vector2(64f, 64f)))
                Now.Model(new NowRect(0f, 0f, 64f, 64f), preview).Draw();

            Assert.AreEqual(
                AnimatorCullingMode.AlwaysAnimate,
                preview.presentationInstance.GetComponent<Animator>().cullingMode);
            Assert.IsTrue(preview.presentationInstance.GetComponent<Animator>().enabled);

            preview.SetRenderingEnabled(false);
            Assert.IsFalse(preview.presentationInstance.GetComponent<Animator>().enabled);
            Assert.AreEqual(
                AnimatorCullingMode.AlwaysAnimate,
                preview.presentationInstance.GetComponent<Animator>().cullingMode);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void PreviewCameraUsesSharedPrivateSceneUnlessSceneLightingIsEnabled()
    {
        using var first = new NowModelPreview();
        using var second = new NowModelPreview();
        Scene isolatedScene = first.camera.scene;

        Assert.IsFalse(first.sceneLightingEnabled);
        Assert.IsTrue(isolatedScene.IsValid());
        Assert.IsTrue(isolatedScene.isLoaded);
        Assert.AreEqual(isolatedScene, second.camera.scene);
        Assert.AreNotEqual(SceneManager.GetActiveScene(), isolatedScene);

        first.SetSceneLightingEnabled();
        Assert.IsTrue(first.sceneLightingEnabled);
        Assert.IsFalse(first.camera.scene.IsValid());
        Assert.AreEqual(isolatedScene, second.camera.scene);

        first.SetSceneLightingEnabled(false);
        Assert.AreEqual(isolatedScene, first.camera.scene);
    }

    [Test]
    public void PreviewsShareOneSerializedKeyLightWithoutSharingOwnership()
    {
        var first = new NowModelPreview();
        using var second = new NowModelPreview();
        var sharedLight = first.sharedKeyLight;

        Assert.NotNull(sharedLight);
        Assert.AreSame(sharedLight, second.sharedKeyLight);

        first.SetLight(Quaternion.identity, 0.6f, Color.red);
        Assert.AreEqual(Color.red, sharedLight.color);
        second.SetLight(Quaternion.identity, 1.2f, Color.blue);
        Assert.AreEqual(Color.blue, sharedLight.color);

        first.Dispose();
        Assert.IsTrue(sharedLight != null,
            "Disposing one preview must not destroy the shared light while another lease remains.");
        Assert.AreSame(sharedLight, second.sharedKeyLight);
    }

    [Test]
    public void PostProcessingIsAnIndependentOptIn()
    {
        using var preview = new NowModelPreview();

        Assert.IsFalse(preview.postProcessingEnabled);
        Assert.AreEqual(~0, preview.postProcessingVolumeMask.value);
        Assert.IsFalse(preview.camera.allowHDR);

        preview.SetPostProcessingEnabled(true, (LayerMask)(1 << 12));

        Assert.IsTrue(preview.postProcessingEnabled);
        Assert.AreEqual(1 << 12, preview.postProcessingVolumeMask.value);
        Assert.IsTrue(preview.camera.allowHDR);
        Assert.IsFalse(preview.sceneLightingEnabled);
    }

    [Test]
    public void BuiltInDirectionalDiscoveryStaysCachedUntilInvalidated()
    {
        using var preview = new NowModelPreview();
        NowModelPreviewManager.InvalidateSceneDirectionalLights();
        int before = NowModelPreviewManager.sceneDirectionalRefreshCount;

        NowModelPreviewManager.GetSceneDirectionalLights();
        int warmed = NowModelPreviewManager.sceneDirectionalRefreshCount;
        NowModelPreviewManager.GetSceneDirectionalLights();

        Assert.AreEqual(before + 1, warmed);
        Assert.AreEqual(warmed, NowModelPreviewManager.sceneDirectionalRefreshCount);

        preview.RefreshSceneLighting();
        NowModelPreviewManager.GetSceneDirectionalLights();
        Assert.AreEqual(warmed + 1, NowModelPreviewManager.sceneDirectionalRefreshCount);
    }

    [Test]
    public void DisposingPreviewReleasesHostLocalUguiMaterial()
    {
        var hostObject = new GameObject("UGUI Host", typeof(RectTransform), typeof(CanvasRenderer));
        var preview = new NowModelPreview();

        try
        {
            var host = hostObject.AddComponent<NowGraphic>();

            using (_drawList.Begin(new Vector2(128f, 64f)))
                Now.Model(new NowRect(0f, 0f, 64f, 32f), preview).Draw();

            var canvasMaterial = host.GetCanvasMaterial(_drawList.batches[0]);
            Assert.NotNull(canvasMaterial);
            Assert.AreEqual(1f, canvasMaterial.GetFloat("_NowPremultipliedTexture"), 0.001f);
            Assert.AreEqual(1, host.cachedCanvasMaterialCount);

            preview.Dispose();

            Assert.AreEqual(0, host.cachedCanvasMaterialCount);
            Assert.IsTrue(canvasMaterial == null, "The native UGUI clone should be destroyed in edit mode.");
        }
        finally
        {
            preview.Dispose();
            UnityEngine.Object.DestroyImmediate(hostObject);
        }
    }

    [Test]
    public void DisposingPreviewReleasesWorldHostMaterialClone()
    {
        var hostObject = new GameObject("World Host");
        var preview = new NowModelPreview();

        try
        {
            var host = hostObject.AddComponent<NowWorldGraphic>();

            using (_drawList.Begin(new Vector2(128f, 64f)))
                Now.Model(new NowRect(0f, 0f, 64f, 32f), preview).Draw();

            var worldMaterial = host.GetMaterial(_drawList.batches[0]);
            Assert.NotNull(worldMaterial);
            Assert.AreEqual(1, host.cachedMaterialCount);

            preview.Dispose();

            Assert.AreEqual(0, host.cachedMaterialCount);
            Assert.IsTrue(worldMaterial == null);
        }
        finally
        {
            preview.Dispose();
            UnityEngine.Object.DestroyImmediate(hostObject);
        }
    }

    [Test]
    public void TextureBackedEffectKeepsPremultipliedAlphaContract()
    {
        using (_drawList.Begin(new Vector2(128f, 64f)))
        {
            using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
                .SetRenderToTexture()
                .SetSourceRect(new NowRect(0f, 0f, 64f, 32f))
                .Begin())
            {
                Now.Rectangle(new NowRect(0f, 0f, 64f, 32f))
                    .SetColor(Color.white)
                    .Draw();
            }
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(NowMeshKind.TexturedRectangle, _drawList.batches[0].kind);
        Assert.AreEqual(
            1f,
            _drawList.batches[0].material.GetFloat("_NowPremultipliedTexture"),
            0.001f);
    }

    [Test]
    public void PendingModelInsideTextureEffectRequestsRetainedHostRepaint()
    {
        using var preview = new NowModelPreview();
        NowControlState.BeginRepaintTracking();
        bool requested;

        try
        {
            using (_drawList.Begin(new Vector2(128f, 64f)))
            {
                using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
                    .SetRenderToTexture()
                    .SetSourceRect(new NowRect(0f, 0f, 64f, 32f))
                    .Begin())
                {
                    Now.Model(new NowRect(0f, 0f, 64f, 32f), preview).Draw();
                }
            }
        }
        finally
        {
            requested = NowControlState.EndRepaintTracking();
        }

        Assert.IsTrue(requested);
    }

    [Test]
    public void LaterModelChangesInvalidateItsLastTextureCaptureHost()
    {
        using var preview = new NowModelPreview();
        var host = new FakeDynamicTextureHost();

        using (NowFrame.Begin(1f, dynamicTextureHost: host))
        using (_drawList.Begin(new Vector2(128f, 64f)))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
            .SetRenderToTexture()
            .SetSourceRect(new NowRect(0f, 0f, 64f, 32f))
            .Begin())
        {
            Now.Model(new NowRect(0f, 0f, 64f, 32f), preview).Draw();
        }

        preview.MarkDirty();
        Assert.AreEqual(1, host.rebuildRequests);

        host.BeginDynamicTextureBuild();
        preview.MarkDirty();
        Assert.AreEqual(
            1,
            host.rebuildRequests,
            "A host that rebuilt without the model must no longer be invalidated by it.");
    }

    [Test]
    public void DisposingModelInvalidatesItsLastTextureCaptureHost()
    {
        var preview = new NowModelPreview();
        var host = new FakeDynamicTextureHost();

        using (NowFrame.Begin(1f, dynamicTextureHost: host))
        using (_drawList.Begin(new Vector2(128f, 64f)))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
            .SetRenderToTexture()
            .SetSourceRect(new NowRect(0f, 0f, 64f, 32f))
            .Begin())
        {
            Now.Model(new NowRect(0f, 0f, 64f, 32f), preview).Draw();
        }

        preview.Dispose();
        Assert.AreEqual(1, host.rebuildRequests);
    }

    [Test]
    public void FramingProducesFiniteClipPlanesAndFitsOrthographicAspect()
    {
        var bounds = new Bounds(Vector3.zero, new Vector3(4f, 2f, 1f));

        NowModelPreview.CalculateFraming(
            bounds,
            Vector3.forward,
            2f,
            30f,
            1f,
            true,
            out var position,
            out var rotation,
            out float orthographicSize,
            out float nearClip,
            out float farClip);

        Assert.AreEqual(1f, orthographicSize, 0.001f);
        Assert.Greater(nearClip, 0f);
        Assert.Greater(farClip, nearClip);
        Assert.IsFalse(float.IsNaN(position.x));
        Assert.IsFalse(float.IsNaN(rotation.w));
    }

    [Test]
    public void SphereFramingFitsAnyContentRotation()
    {
        const float radius = 1.75f;
        const float fieldOfView = 30f;

        NowModelPreview.CalculateSphereFraming(
            Vector3.zero,
            radius,
            Vector3.forward,
            1f,
            fieldOfView,
            1.1f,
            false,
            out var position,
            out _,
            out _,
            out float nearClip,
            out float farClip);

        float requiredDistance = radius * 1.1f /
            Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad);
        Assert.AreEqual(requiredDistance, position.magnitude, 0.001f);
        Assert.Greater(nearClip, 0f);
        Assert.Greater(farClip, nearClip);
        Assert.GreaterOrEqual(position.magnitude - radius, nearClip - 0.02f);
        Assert.LessOrEqual(position.magnitude + radius, farClip + 0.02f);
    }

    [Test]
    public void PipelineBuildDefersTargetMutationUntilTheRenderPassEnds()
    {
        using var preview = new NowModelPreview();
        NowModelPreviewManager.BeginPipelineBuild();

        try
        {
            preview.SetFixedResolution(64, 32);
            Assert.IsNull(preview.texture);
            Assert.IsTrue(preview.requiresDeferredTick);
        }
        finally
        {
            NowModelPreviewManager.EndPipelineBuild();
        }

        preview.ApplyDeferredMutations();
        Assert.NotNull(preview.texture);
        Assert.AreEqual(64, preview.texture.width);
        Assert.AreEqual(32, preview.texture.height);
    }

    [Test]
    public void InvalidSettingsFailAtCallSite()
    {
        using var preview = new NowModelPreview();

        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetResolutionScale(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetResolutionScale(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetMaxResolution(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetFixedResolution(0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetViewDirection(Vector3.zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetRotation(new Quaternion(0f, 0f, 0f, 0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            preview.SetLight(new Quaternion(0f, 0f, 0f, 0f), 1f, Color.white));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetFieldOfView(180f));
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetFramingPadding(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            preview.SetUpdateMode((NowModelPreviewUpdateMode)100));
    }

    [Test]
    public void DisposeIsIdempotentAndRejectsLaterUse()
    {
        var preview = new NowModelPreview().SetFixedResolution(32, 16);
        var target = preview.texture;

        preview.Dispose();
        Assert.DoesNotThrow(preview.Dispose);
        Assert.IsTrue(preview.isDisposed);
        Assert.IsNull(preview.texture);
        Assert.IsTrue(target == null, "The owned RenderTexture should be destroyed immediately in edit mode.");
        Assert.Throws<ObjectDisposedException>(() => preview.MarkDirty());
    }

    [Test]
    public void DisposeDefersWhileItsCameraRenderIsActive()
    {
        var preview = new NowModelPreview().SetFixedResolution(32, 16);
        var target = preview.texture;

        Assert.IsTrue(NowModelPreviewManager.TryBeginRender(preview));

        try
        {
            preview.Dispose();
            Assert.IsTrue(preview.isDisposed);
            Assert.AreSame(target, preview.texture, "Active render resources must survive until the camera callback unwinds.");
        }
        finally
        {
            NowModelPreviewManager.EndRender(preview);
        }

        Assert.IsNull(preview.texture);
        Assert.IsTrue(target == null);
    }

    [Test]
    public void WarmedManualModelDrawIsAllocationFree()
    {
        using var preview = new NowModelPreview()
            .SetFixedResolution(64, 32)
            .SetUpdateMode(NowModelPreviewUpdateMode.Manual);

        void DrawFrame()
        {
            using (_drawList.Begin(new Vector2(256f, 128f)))
            {
                Now.Model(new NowRect(8f, 8f, 64f, 32f), preview)
                    .SetRadius(6f)
                    .Draw();
            }
        }

        DrawFrame();
        DrawFrame();
        DrawFrame();

        long before;

        try
        {
            before = GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return;
        }

        DrawFrame();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0, allocated, "steady-state model builder/draw-list work must not allocate");
    }

}
