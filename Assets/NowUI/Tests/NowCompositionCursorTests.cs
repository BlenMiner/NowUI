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
