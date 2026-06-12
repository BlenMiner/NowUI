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
public class NowUIRenderingPlayModeTests
{
    const int Side = 128;

    RenderTexture _target;
    NowUIRenderer _renderer;

    [SetUp]
    public void SetUp()
    {
        _target = new RenderTexture(Side, Side, 0, RenderTextureFormat.ARGB32);
        _target.Create();
        _renderer = new NowUIRenderer();
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

        // Center is filled, the border region outside the rect is empty.
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

        // A 24px radius removes roughly 4 - pi/4 of each corner square.
        int square = 64 * 64;
        int expectedRemoved = Mathf.RoundToInt(24 * 24 * (4f - Mathf.PI));

        Assert.Less(filled, square - expectedRemoved / 2, "Corners do not appear rounded.");
        Assert.Greater(filled, square - expectedRemoved * 2, "Far too few pixels for the rounded rect.");
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

        // Two 56px glyphs produce thousands of opaque pixels; a broken atlas,
        // failed bake, or wrong UVs produce none.
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

            // A 60x60 source square inside a 100px composition drawn into 100px.
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

        // Host NowUI graphic: bottom-left 200x200 of the screen.
        var hostObject = new GameObject("Host", typeof(NowUIGraphic));
        hostObject.transform.SetParent(canvasObject.transform, false);
        var hostRect = hostObject.GetComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.zero;
        hostRect.pivot = Vector2.zero;
        hostRect.anchoredPosition = Vector2.zero;
        hostRect.sizeDelta = new Vector2(200, 200);
        var host = hostObject.GetComponent<NowUIGraphic>();

        // UGUI image drawn above the host, covering its bottom-left 100x100.
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
            yield return null; // let the canvas lay out

            Assert.IsFalse(
                NowUIRaycastGate.IsPointerAllowed(host, new Vector2(50, 50)),
                "A UGUI element above the host must occlude the pointer.");
            Assert.IsTrue(
                NowUIRaycastGate.IsPointerAllowed(host, new Vector2(150, 150)),
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
    public IEnumerator EventSystemSelectionSuspendsNowUIFocus()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var selectable = new GameObject("Selected");

        try
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Assert.NotNull(eventSystem, "EventSystem must be live in play mode.");

            // UGUI selection clears NowUI focus once the next frame processes.
            NowUIFocus.Focus(7);
            eventSystem.SetSelectedGameObject(selectable);
            Assert.NotNull(eventSystem.currentSelectedGameObject);

            yield return null;
            NowUIFocus.Register(1, new NowRect(0, 0, 10, 10)); // drives the frame swap

            Assert.AreEqual(0, NowUIFocus.focusedId, "UGUI selection must clear NowUI focus.");

            // Focusing a NowUI control deselects the EventSystem.
            eventSystem.SetSelectedGameObject(selectable);
            NowUIFocus.Focus(9);
            Assert.IsNull(eventSystem.currentSelectedGameObject, "NowUI focus must deselect the EventSystem.");
            Assert.AreEqual(9, NowUIFocus.focusedId);
        }
        finally
        {
            NowUIFocus.Reset();
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
            Now.StartUI(new NowRect(0, 0, Side, Side));

            Now.Rectangle(new NowRect(32, 32, 64, 64))
                .SetColor(Color.green)
                .Draw();

            Now.FlushUI();
            drew = true;
        }
    }

    [UnityTest]
    public IEnumerator CameraGLPathRendersThroughOnPostRender()
    {
        var go = new GameObject("NowUI GL Test Camera");

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
}
