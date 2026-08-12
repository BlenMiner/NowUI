using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowCompositionCursorTests
{
    sealed class Provider : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = default;
            return true;
        }
    }

    sealed class MappedProvider : INowInputProvider, INowSurfaceToScreenMapper
    {
        public bool succeeds = true;

        public Vector2 offset;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = default;
            return true;
        }

        public bool TrySurfaceToScreen(
            NowInputSurface surface,
            Vector2 surfacePosition,
            out Vector2 screenPosition)
        {
            screenPosition = surfacePosition + offset;
            return succeeds;
        }
    }

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowTextInput.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
        NowTextInput.Reset();
    }

    [Test]
    public void SurfaceDisplayRectMapsRenderTextureCaretToScreenPixels()
    {
        var surface = new NowInputSurface(
            new Vector2(200f, 100f),
            new Rect(300f, 200f, 400f, 300f));

        using (NowInput.Begin(new Provider(), surface))
        {
            Assert.IsTrue(NowSurfaceToScreenMapper.TryResolveCompositionCursor(
                new Vector2(50f, 25f),
                out var screen));
            Assert.AreEqual(new Vector2(400f, 275f), screen);
        }
    }

    [Test]
    public void ActiveNowTransformIsAppliedBeforeHostMapping()
    {
        var surface = new NowInputSurface(
            new Vector2(200f, 100f),
            new Rect(300f, 200f, 400f, 300f));

        using (NowInput.Begin(new Provider(), surface))
        using (Now.Transform(2f, new Vector2(5f, 7f)))
        {
            Assert.IsTrue(NowSurfaceToScreenMapper.TryResolveCompositionCursor(
                new Vector2(25f, 10f),
                out var screen));
            Assert.AreEqual(new Vector2(410f, 281f), screen);
        }
    }

    [Test]
    public void ExplicitProviderMapperOverridesLinearSurfaceRect()
    {
        var provider = new MappedProvider { offset = new Vector2(700f, 500f) };
        var surface = new NowInputSurface(
            new Vector2(200f, 100f),
            new Rect(300f, 200f, 400f, 300f));

        using (NowInput.Begin(provider, surface))
        {
            Assert.IsTrue(NowSurfaceToScreenMapper.TryResolveCompositionCursor(
                new Vector2(20f, 30f),
                out var screen));
            Assert.AreEqual(new Vector2(720f, 530f), screen);
        }
    }

    [Test]
    public void FailedExplicitProviderMapperDoesNotUseAFalseLinearFallback()
    {
        var provider = new MappedProvider { succeeds = false };
        var surface = new NowInputSurface(
            new Vector2(200f, 100f),
            new Rect(300f, 200f, 400f, 300f));

        using (NowInput.Begin(provider, surface))
        {
            Assert.IsFalse(NowSurfaceToScreenMapper.TryResolveCompositionCursor(
                new Vector2(20f, 30f),
                out _));
        }
    }

    [Test]
    public void IMGUICompositionCursorKeepsItsEstablishedPassthroughCoordinates()
    {
        var surface = new NowInputSurface(
            new Vector2(200f, 100f),
            new Rect(300f, 200f, 400f, 300f));

        using (NowInput.Begin(new NowIMGUIInputProvider(), surface))
        using (Now.Transform(2f, new Vector2(5f, 7f)))
        {
            Assert.IsTrue(NowSurfaceToScreenMapper.TryResolveCompositionCursor(
                new Vector2(20f, 30f),
                out var screen));
            Assert.AreEqual(new Vector2(20f, 30f), screen);
        }
    }

    [Test]
    public void RectTransformProviderProjectsCanvasSurfacePoint()
    {
        var gameObject = new GameObject("IME Canvas Surface", typeof(RectTransform));

        try
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(200f, 100f);
            rectTransform.position = new Vector3(320f, 240f, 0f);
            var provider = new NowRectTransformInputProvider(rectTransform);

            Assert.IsTrue(provider.TrySurfaceToScreen(
                new NowInputSurface(new Vector2(200f, 100f)),
                Vector2.zero,
                out var screen));
            Assert.AreEqual(220f, screen.x, 0.001f);
            Assert.AreEqual(Screen.height - 290f, screen.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

#if NOWUI_UGUI
    [Test]
    public void RectTransformProjectionMatchesUnityWithoutCameraOutsideRect()
    {
        var gameObject = new GameObject("Rect Projection", typeof(RectTransform));

        try
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.position = new Vector3(320f, 240f, 0f);
            rectTransform.rotation = Quaternion.Euler(0f, 0f, 17f);
            rectTransform.localScale = new Vector3(1.3f, 0.7f, 1f);

            AssertRectTransformProjectionParity(
                rectTransform,
                new Vector2(760f, -80f),
                null);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RectTransformProjectionMatchesUnityThroughTransformedHierarchy(bool orthographic)
    {
        var cameraObject = new GameObject("Rect Projection Camera", typeof(Camera));
        var parentObject = new GameObject("Rect Projection Parent");
        var rectObject = new GameObject("Rect Projection Child", typeof(RectTransform));

        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            camera.orthographic = orthographic;
            camera.orthographicSize = 5f;
            camera.fieldOfView = 60f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            parentObject.transform.SetPositionAndRotation(
                new Vector3(0.4f, -0.3f, 5f),
                Quaternion.Euler(13f, 22f, 7f));
            parentObject.transform.localScale = new Vector3(1.7f, 0.65f, 1.2f);

            var rectTransform = rectObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parentObject.transform, false);
            rectTransform.sizeDelta = new Vector2(2f, 1f);
            rectTransform.localPosition = new Vector3(0.2f, 0.1f, 0.3f);
            rectTransform.localRotation = Quaternion.Euler(-4f, 9f, 2f);
            rectTransform.localScale = new Vector3(0.8f, 1.3f, 1f);

            // This point is deliberately outside the rect. Unity's utility
            // tests the plane intersection, not containment in the rectangle.
            Vector3 worldPoint = rectTransform.TransformPoint(new Vector3(1.4f, -0.8f, 0f));
            Vector2 screenPoint = camera.WorldToScreenPoint(worldPoint);
            AssertRectTransformProjectionParity(rectTransform, screenPoint, camera);
        }
        finally
        {
            Object.DestroyImmediate(rectObject);
            Object.DestroyImmediate(parentObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void RectTransformProjectionMatchesUnityWhenRayStartsOnPlane()
    {
        var gameObject = new GameObject("Rect Projection Coincident Plane", typeof(RectTransform));

        try
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.position = new Vector3(10f, 20f, -100f);
            AssertRectTransformProjectionParity(
                rectTransform,
                new Vector2(42f, 53f),
                null);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    static void AssertRectTransformProjectionParity(
        RectTransform rectTransform,
        Vector2 screenPoint,
        Camera camera)
    {
        bool expectedSuccess = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPoint,
            camera,
            out Vector2 expected);
        bool actualSuccess = NowRectTransformProjection.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPoint,
            camera,
            out Vector2 actual);

        Assert.AreEqual(expectedSuccess, actualSuccess);

        if (expectedSuccess)
            Assert.LessOrEqual(Vector2.Distance(expected, actual), 0.001f);
    }
#endif

    [Test]
    public void WorldProviderProjectsSurfacePointThroughItsCamera()
    {
        var cameraObject = new GameObject("IME World Camera", typeof(Camera));
        var panelObject = new GameObject("IME World Surface");

        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            panelObject.transform.position = new Vector3(0f, 0f, 5f);

            var provider = new NowWorldInputProvider
            {
                camera = camera,
                transform = panelObject.transform,
                size = new Vector2(200f, 100f),
                pixelsPerUnit = 100f,
                pivot = new Vector2(0.5f, 0.5f)
            };
            Vector3 projected = camera.WorldToScreenPoint(panelObject.transform.position);

            Assert.IsTrue(provider.TrySurfaceToScreen(
                new NowInputSurface(new Vector2(200f, 100f)),
                new Vector2(100f, 50f),
                out var screen));
            Assert.AreEqual(projected.x, screen.x, 0.001f);
            Assert.AreEqual(Screen.height - projected.y, screen.y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
