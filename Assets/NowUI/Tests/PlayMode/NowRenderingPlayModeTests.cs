using System.Collections;
using NUnit.Framework;
using UnityEngine;
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
            Now.Rectangle(new NowRect(0, 0, Side, Side))
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
            Now.Rectangle(new NowRect(0, 0, Side, Side))
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
            Now.Rectangle(new NowRect(0, 0, Side / 2f, Side))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, 0, Side / 2f, Side))
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
            Now.Rectangle(new NowRect(0, 0, Side / 2f, Side))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, 0, Side / 2f, Side))
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

        protected override bool useLayoutMeasurePass => false;

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
