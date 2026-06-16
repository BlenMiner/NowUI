using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using NowUI;
using Object = UnityEngine.Object;

public class NowWorldGraphicTests
{
    sealed class RectWorldGraphic : NowWorldGraphic
    {
        public bool drawTextured;

        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(new NowRect(0, 0, rect.width, rect.height))
                .SetColor(Color.white)
                .Draw();

            if (drawTextured)
            {
                Now.Rectangle(new NowRect(4, 4, 12, 12))
                    .SetTexture(Texture2D.whiteTexture)
                    .Draw();
            }
        }
    }

    sealed class ClickWorldGraphic : NowWorldGraphic
    {
        public INowInputProvider provider;
        public bool clicked;

        protected override bool useLayoutMeasurePass => false;

        protected override INowInputProvider GetInputProvider()
        {
            return provider;
        }

        protected override void DrawNowUI(NowRect rect)
        {
            clicked = Now.Button(new NowRect(0, 0, rect.width, rect.height), "Same").Draw();
        }
    }

    sealed class LayoutWorldGraphic : NowWorldGraphic
    {
        public bool stretchItems;
        public int itemCount = 2;

        protected override void DrawNowUI(NowRect rect)
        {
            using (NowLayout.Area(new NowRect(0, 0, rect.width, rect.height)))
            {
                if (stretchItems)
                {
                    for (int i = 0; i < itemCount; ++i)
                        NowLayout.Rect(NowLayout.StretchWidth().SetHeight(24f));

                    return;
                }

                NowLayout.Rect(80f, 30f);
                NowLayout.Rect(120f, 40f);
            }
        }
    }

    sealed class CountingWorldGraphic : NowWorldGraphic
    {
        public int drawCount;

        protected override bool useLayoutMeasurePass => false;

        public void TickLateUpdate()
        {
            base.LateUpdate();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            ++drawCount;

            Now.Rectangle(new NowRect(0, 0, rect.width, rect.height))
                .SetColor(Color.white)
                .Draw();
        }
    }

    sealed class GlassWorldGraphic : NowWorldGraphic
    {
        protected override bool useLayoutMeasurePass => false;

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Glass(new NowRect(0, 0, rect.width, rect.height))
                .SetBlurRadius(18f)
                .Draw();
        }
    }

    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class ZDeformer : NowWorldDeformer
    {
        public override Vector3 Deform(in NowWorldVertex vertex)
        {
            var position = vertex.localPosition;
            position.z = vertex.normalized.x;
            return position;
        }
    }

    static long AllocatedBytesOrIgnore()
    {
        try
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return 0;
        }
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    [Test]
    public void WorldMeshMapsUiCoordinatesToLocalSpace()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var go = new GameObject("Now World Graphic");

        try
        {
            var graphic = go.AddComponent<RectWorldGraphic>();
            graphic.size = new Vector2(100f, 50f);
            graphic.pixelsPerUnit = 100f;
            graphic.pivot = new Vector2(0.5f, 0.5f);

            Assert.AreEqual(new Vector3(-0.5f, 0.25f, 0f), graphic.UIToLocal(Vector2.zero));
            Assert.AreEqual(new Vector3(0.5f, -0.25f, 0f), graphic.UIToLocal(graphic.size));

            graphic.RebuildNowUI();

            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;

            Assert.AreEqual(4, vertices.Length);
            Assert.AreEqual(new Vector3(-0.5f, -0.25f, 0f), vertices[0]);
            Assert.AreEqual(new Vector3(-0.5f, 0.25f, 0f), vertices[1]);
            Assert.AreEqual(new Vector3(0.5f, 0.25f, 0f), vertices[2]);
            Assert.AreEqual(new Vector3(0.5f, -0.25f, 0f), vertices[3]);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldRendererPreservesMaterialBatches()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var go = new GameObject("Now World Batches");

        try
        {
            var graphic = go.AddComponent<RectWorldGraphic>();
            graphic.drawTextured = true;
            graphic.RebuildNowUI();

            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.GetComponent<MeshRenderer>();

            Assert.AreEqual(2, graphic.batchCount);
            Assert.AreEqual(2, mesh.subMeshCount);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldGraphicCanAutoSizeToLayoutContent()
    {
        var go = new GameObject("Now World Auto Size");

        try
        {
            var graphic = go.AddComponent<LayoutWorldGraphic>();
            graphic.size = new Vector2(300f, 200f);
            graphic.layoutAutoSizeAxes = NowWorldAutoSizeAxes.Both;

            graphic.RebuildNowUI();

            Assert.AreEqual(120f, graphic.size.x, 0.001f);
            Assert.AreEqual(70f, graphic.size.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldGraphicCanAutoSizeLayoutHeightFromFixedWidth()
    {
        var go = new GameObject("Now World Auto Height");

        try
        {
            var graphic = go.AddComponent<LayoutWorldGraphic>();
            graphic.stretchItems = true;
            graphic.itemCount = 3;
            graphic.size = new Vector2(180f, 20f);
            graphic.layoutAutoSizeAxes = NowWorldAutoSizeAxes.Height;

            graphic.RebuildNowUI();

            Assert.AreEqual(180f, graphic.size.x, 0.001f);
            Assert.AreEqual(72f, graphic.size.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldGraphicSkipsRebuildWhenOutsideTargetCameraFrustum()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var cameraObject = new GameObject("Now World Camera");
        var panelObject = new GameObject("Now World Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);
            cameraObject.transform.rotation = Quaternion.identity;

            panelObject.transform.position = new Vector3(5f, 0f, 0f);
            var graphic = panelObject.AddComponent<CountingWorldGraphic>();
            graphic.facingMode = NowWorldFacingMode.None;
            graphic.targetCamera = camera;
            graphic.frustumCullRebuilds = true;
            graphic.size = new Vector2(100f, 50f);
            graphic.pixelsPerUnit = 100f;

            graphic.TickLateUpdate();

            Assert.AreEqual(0, graphic.drawCount);
            Assert.IsFalse(graphic.hasGeometry);

            panelObject.transform.position = Vector3.zero;
            graphic.TickLateUpdate();

            Assert.AreEqual(1, graphic.drawCount);
            Assert.IsTrue(graphic.hasGeometry);
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldGraphicRebuildIsAllocationFreeAfterWarmup()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var go = new GameObject("Now World Alloc");

        try
        {
            var graphic = go.AddComponent<RectWorldGraphic>();
            graphic.RebuildNowUI();
            graphic.RebuildNowUI();
            graphic.RebuildNowUI();

            long before = AllocatedBytesOrIgnore();
            graphic.RebuildNowUI();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "steady-state world graphic rebuild must not allocate");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldInputMapsCameraRayToSurfaceCoordinates()
    {
        var cameraObject = new GameObject("Now World Camera");
        var panelObject = new GameObject("Now World Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);
            cameraObject.transform.rotation = Quaternion.identity;

            var graphic = panelObject.AddComponent<RectWorldGraphic>();
            graphic.facingMode = NowWorldFacingMode.None;
            graphic.targetCamera = camera;
            graphic.size = new Vector2(100f, 50f);
            graphic.pixelsPerUnit = 100f;
            graphic.pivot = new Vector2(0.5f, 0.5f);

            var provider = new NowWorldInputProvider { graphic = graphic, camera = camera };

            Assert.IsTrue(provider.TryScreenPointToSurface(new Vector2(100, 100), out var position));
            Assert.AreEqual(50f, position.x, 0.001f);
            Assert.AreEqual(25f, position.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldInputUsesFrontMostGraphicAtScreenPoint()
    {
        var cameraObject = new GameObject("Now World Camera");
        var backObject = new GameObject("Now World Back Panel");
        var frontObject = new GameObject("Now World Front Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);
            cameraObject.transform.rotation = Quaternion.identity;

            var back = backObject.AddComponent<RectWorldGraphic>();
            back.facingMode = NowWorldFacingMode.None;
            back.targetCamera = camera;
            back.size = new Vector2(100f, 50f);
            back.pixelsPerUnit = 100f;
            back.pivot = new Vector2(0.5f, 0.5f);

            frontObject.transform.position = new Vector3(0, 0, -1f);
            var front = frontObject.AddComponent<RectWorldGraphic>();
            front.facingMode = NowWorldFacingMode.None;
            front.targetCamera = camera;
            front.size = new Vector2(100f, 50f);
            front.pixelsPerUnit = 100f;
            front.pivot = new Vector2(0.5f, 0.5f);

            var screenCenter = new Vector2(100, 100);
            var backProvider = new NowWorldInputProvider { graphic = back, camera = camera };
            var frontProvider = new NowWorldInputProvider { graphic = front, camera = camera };

            Assert.IsFalse(backProvider.TryScreenPointToSurface(screenCenter, out _));
            Assert.IsTrue(frontProvider.TryScreenPointToSurface(screenCenter, out var position));
            Assert.AreEqual(50f, position.x, 0.001f);
            Assert.AreEqual(25f, position.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(frontObject);
            Object.DestroyImmediate(backObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldInputBlockedFreshPressDoesNotReusePreviousHover()
    {
        var cameraObject = new GameObject("Now World Camera");
        var backObject = new GameObject("Now World Back Panel");
        var frontObject = new GameObject("Now World Front Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);
            cameraObject.transform.rotation = Quaternion.identity;

            var back = backObject.AddComponent<RectWorldGraphic>();
            back.facingMode = NowWorldFacingMode.None;
            back.targetCamera = camera;
            back.size = new Vector2(100f, 50f);
            back.pixelsPerUnit = 100f;
            back.pivot = new Vector2(0.5f, 0.5f);

            frontObject.SetActive(false);
            frontObject.transform.position = new Vector3(0, 0, -1f);
            var front = frontObject.AddComponent<RectWorldGraphic>();
            front.facingMode = NowWorldFacingMode.None;
            front.targetCamera = camera;
            front.size = new Vector2(100f, 50f);
            front.pixelsPerUnit = 100f;
            front.pivot = new Vector2(0.5f, 0.5f);

            var provider = new NowWorldInputProvider { graphic = back, camera = camera };
            var surface = new NowInputSurface(back.size);
            var screenCenter = new Vector2(100, 100);

            provider.TryGetSnapshot(surface, new NowMouseInput
            {
                hasPointer = true,
                screenPosition = screenCenter
            }, out var hoverSnapshot);

            Assert.IsTrue(hoverSnapshot.hasPointer);

            frontObject.SetActive(true);

            provider.TryGetSnapshot(surface, new NowMouseInput
            {
                hasPointer = true,
                screenPosition = screenCenter,
                pointerButtonsDown = NowPointerButtons.Primary,
                pointerButtonsPressed = NowPointerButtons.Primary
            }, out var blockedPressSnapshot);

            Assert.IsFalse(blockedPressSnapshot.hasPointer);
        }
        finally
        {
            Object.DestroyImmediate(frontObject);
            Object.DestroyImmediate(backObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldInputBlockedReleaseKeepsCapturedPointer()
    {
        var cameraObject = new GameObject("Now World Camera");
        var backObject = new GameObject("Now World Back Panel");
        var frontObject = new GameObject("Now World Front Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);
            cameraObject.transform.rotation = Quaternion.identity;

            var back = backObject.AddComponent<RectWorldGraphic>();
            back.facingMode = NowWorldFacingMode.None;
            back.targetCamera = camera;
            back.size = new Vector2(100f, 50f);
            back.pixelsPerUnit = 100f;
            back.pivot = new Vector2(0.5f, 0.5f);

            frontObject.SetActive(false);
            frontObject.transform.position = new Vector3(0, 0, -1f);
            var front = frontObject.AddComponent<RectWorldGraphic>();
            front.facingMode = NowWorldFacingMode.None;
            front.targetCamera = camera;
            front.size = new Vector2(100f, 50f);
            front.pixelsPerUnit = 100f;
            front.pivot = new Vector2(0.5f, 0.5f);

            var provider = new NowWorldInputProvider { graphic = back, camera = camera };
            var surface = new NowInputSurface(back.size);
            var screenCenter = new Vector2(100, 100);

            provider.TryGetSnapshot(surface, new NowMouseInput
            {
                hasPointer = true,
                screenPosition = screenCenter,
                pointerButtonsDown = NowPointerButtons.Primary,
                pointerButtonsPressed = NowPointerButtons.Primary
            }, out var pressSnapshot);

            Assert.IsTrue(pressSnapshot.hasPointer);

            frontObject.SetActive(true);

            provider.TryGetSnapshot(surface, new NowMouseInput
            {
                hasPointer = true,
                screenPosition = screenCenter,
                pointerButtonsReleased = NowPointerButtons.Primary
            }, out var releaseSnapshot);

            Assert.IsTrue(releaseSnapshot.hasPointer);
            Assert.AreEqual(NowPointerButtons.Primary, releaseSnapshot.pointerButtonsReleased);
            Assert.AreEqual(pressSnapshot.pointerPosition, releaseSnapshot.pointerPosition);
        }
        finally
        {
            Object.DestroyImmediate(frontObject);
            Object.DestroyImmediate(backObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldInputSceneOccludedRejectsSceneBlockedRay()
    {
        var cameraObject = new GameObject("Now World Camera");
        var panelObject = new GameObject("Now World Panel");
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Now World Input Blocker";

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);
            cameraObject.transform.rotation = Quaternion.identity;

            var graphic = panelObject.AddComponent<RectWorldGraphic>();
            graphic.facingMode = NowWorldFacingMode.None;
            graphic.targetCamera = camera;
            graphic.size = new Vector2(100f, 50f);
            graphic.pixelsPerUnit = 100f;
            graphic.pivot = new Vector2(0.5f, 0.5f);

            blocker.transform.position = new Vector3(0, 0, -0.5f);
            blocker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            Physics.SyncTransforms();

            var provider = new NowWorldInputProvider { graphic = graphic, camera = camera };
            var screenCenter = new Vector2(100, 100);

            graphic.depthMode = NowWorldDepthMode.AlwaysVisible;
            Assert.IsTrue(provider.TryScreenPointToSurface(screenCenter, out _));

            graphic.depthMode = NowWorldDepthMode.SceneOccluded;
            Assert.IsFalse(provider.TryScreenPointToSurface(screenCenter, out _));
        }
        finally
        {
            Object.DestroyImmediate(blocker);
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldInputReleaseOutsideSurfaceClearsActiveInteraction()
    {
        var cameraObject = new GameObject("Now World Camera");
        var panelObject = new GameObject("Now World Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);

            var graphic = panelObject.AddComponent<RectWorldGraphic>();
            graphic.facingMode = NowWorldFacingMode.None;
            graphic.targetCamera = camera;
            graphic.size = new Vector2(100f, 50f);
            graphic.pixelsPerUnit = 100f;
            graphic.pivot = new Vector2(0.5f, 0.5f);

            var provider = new NowWorldInputProvider { graphic = graphic, camera = camera };
            var surface = new NowInputSurface(graphic.size);

            provider.TryGetSnapshot(surface, new NowMouseInput
            {
                hasPointer = true,
                screenPosition = new Vector2(100, 100),
                pointerButtonsDown = NowPointerButtons.Primary,
                pointerButtonsPressed = NowPointerButtons.Primary
            }, out var pressSnapshot);

            using (NowInput.Begin(new FakeProvider { snapshot = pressSnapshot }, surface))
                Assert.IsTrue(NowInput.Interact(7, new NowRect(0, 0, 100, 50)).pressed);

            Assert.AreEqual(7, NowInput.activeId);

            provider.TryGetSnapshot(surface, new NowMouseInput
            {
                hasPointer = true,
                screenPosition = new Vector2(199, 199),
                pointerButtonsReleased = NowPointerButtons.Primary
            }, out var releaseSnapshot);

            using (NowInput.Begin(new FakeProvider { snapshot = releaseSnapshot }, surface))
                Assert.IsTrue(NowInput.Interact(7, new NowRect(0, 0, 100, 50)).released);

            Assert.AreEqual(0, NowInput.activeId);
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldGraphicScopesControlsPerInstance()
    {
        var providerA = new FakeProvider();
        var providerB = new FakeProvider();
        var objectA = new GameObject("Now World A");
        var objectB = new GameObject("Now World B");

        try
        {
            var a = objectA.AddComponent<ClickWorldGraphic>();
            var b = objectB.AddComponent<ClickWorldGraphic>();
            a.provider = providerA;
            b.provider = providerB;

            providerA.snapshot = new NowInputSnapshot(new Vector2(10, 10), true, true, false);
            a.RebuildNowUI();

            providerA.snapshot = new NowInputSnapshot(new Vector2(10, 10), false, false, true);
            providerB.snapshot = new NowInputSnapshot(new Vector2(10, 10), false, false, true);
            b.RebuildNowUI();
            a.RebuildNowUI();

            Assert.IsFalse(b.clicked, "A release on another identical world graphic must not inherit the first graphic's active id.");
            Assert.IsTrue(a.clicked);
        }
        finally
        {
            Object.DestroyImmediate(objectA);
            Object.DestroyImmediate(objectB);
        }
    }

    [Test]
    public void DepthModeUsesMaterialClone()
    {
        var baseMaterial = Resources.Load<Material>("NowUI/UIMaterial");
        Assert.NotNull(baseMaterial);
        var go = new GameObject("Now World Depth");

        try
        {
            float baseZTest = baseMaterial.GetFloat("_ZTest");
            var graphic = go.AddComponent<RectWorldGraphic>();
            graphic.depthMode = NowWorldDepthMode.SceneOccluded;
            graphic.RebuildNowUI();

            var material = go.GetComponent<MeshRenderer>().sharedMaterial;

            Assert.NotNull(material);
            Assert.AreNotSame(baseMaterial, material);
            Assert.AreEqual((float)CompareFunction.LessEqual, material.GetFloat("_ZTest"), 0.001f);
            Assert.AreEqual(baseZTest, baseMaterial.GetFloat("_ZTest"), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldGlassMaterialFollowsBackdropMode()
    {
        var baseMaterial = Resources.Load<Material>("NowUI/GlassMaterial");
        Assert.NotNull(baseMaterial);
        var go = new GameObject("Now World Glass");

        try
        {
            var graphic = go.AddComponent<GlassWorldGraphic>();
            graphic.glassBackdropMode = NowWorldGlassBackdropMode.CameraBlurred;
            graphic.RebuildNowUI();

            var material = go.GetComponent<MeshRenderer>().sharedMaterial;

            Assert.NotNull(material);
            Assert.AreNotSame(baseMaterial, material);
            Assert.AreEqual(0f, material.GetFloat("_NowGlassUseBackdrop"), 0.001f);
            Assert.AreEqual(1f, material.GetFloat("_NowGlassUseSceneDepth"), 0.001f);

            graphic.ApplyGlassBackdropTexture(Texture2D.whiteTexture);
            material = go.GetComponent<MeshRenderer>().sharedMaterial;

            Assert.NotNull(material);
            Assert.AreEqual(1f, material.GetFloat("_NowGlassUseBackdrop"), 0.001f);

            graphic.glassDepthMode = NowWorldGlassDepthMode.Disabled;
            material = go.GetComponent<MeshRenderer>().sharedMaterial;

            Assert.NotNull(material);
            Assert.AreEqual(0f, material.GetFloat("_NowGlassUseSceneDepth"), 0.001f);

            graphic.glassBackdropMode = NowWorldGlassBackdropMode.TintOnly;
            material = go.GetComponent<MeshRenderer>().sharedMaterial;

            Assert.NotNull(material);
            Assert.AreEqual(0f, material.GetFloat("_NowGlassUseBackdrop"), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldGlassBackdropCollectsWorldGraphicsBehindRequester()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        Assert.NotNull(Resources.Load<Material>("NowUI/GlassMaterial"));
        var cameraObject = new GameObject("Now World Glass Camera", typeof(Camera));
        var backObject = new GameObject("Now World Backdrop Back");
        var frontObject = new GameObject("Now World Backdrop Front");
        var inFrontObject = new GameObject("Now World Backdrop In Front");
        var contributors = new System.Collections.Generic.List<NowWorldGraphic>();

        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;

            var back = backObject.AddComponent<RectWorldGraphic>();
            back.targetCamera = camera;
            back.transform.position = new Vector3(0f, 0f, 4f);
            back.RebuildNowUI();

            var front = frontObject.AddComponent<GlassWorldGraphic>();
            front.targetCamera = camera;
            front.transform.position = new Vector3(0f, 0f, 3f);
            front.RebuildNowUI();

            var inFront = inFrontObject.AddComponent<RectWorldGraphic>();
            inFront.targetCamera = camera;
            inFront.transform.position = new Vector3(0f, 0f, 2f);
            inFront.RebuildNowUI();

            NowWorldGraphic.CollectBackdropContributors(camera, front, front.GetCameraDepth(camera), contributors);

            Assert.Contains(back, contributors);
            Assert.IsFalse(contributors.Contains(front));
            Assert.IsFalse(contributors.Contains(inFront));

            NowWorldGraphic.CollectBackdropContributors(camera, back, back.GetCameraDepth(camera), contributors);

            Assert.IsFalse(contributors.Contains(back));
            Assert.IsFalse(contributors.Contains(front));
            Assert.IsFalse(contributors.Contains(inFront));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(backObject);
            Object.DestroyImmediate(frontObject);
            Object.DestroyImmediate(inFrontObject);
        }
    }

    [Test]
    public void WorldDeformerCanMoveVerticesInLocalZ()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        var go = new GameObject("Now World Deformed");

        try
        {
            var graphic = go.AddComponent<RectWorldGraphic>();
            graphic.deformer = go.AddComponent<ZDeformer>();
            graphic.RebuildNowUI();

            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;

            Assert.Greater(vertices[2].z, 0.9f);
            Assert.Greater(mesh.bounds.extents.z, 0.4f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
