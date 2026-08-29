using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using NowUI.Docking;
using NowUI.Markdown;
using NowUI.NodeGraph;
using NowUI.Sdf;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
#if NOWUI_UGUI
using UnityEngine.UI;
#endif

namespace NowUI.Editor
{
    internal sealed class NowHarnessScenario
    {
        public string name;
        public int width;
        public int height;
        public bool includeInGoldens;
        public bool includeInPerf = true;
        public bool darkTheme;
        public string themePath;
        public bool suppressBadge;
        public Action<string> prepare;
        public Func<INowInputProvider> createInputProvider;
        public Func<NowHarnessScenario, string, NowHarnessCapture> capture;
        public int warmupFrames;
        public Action afterWarmup;
        public Action<NowRect> draw;
    }

    internal sealed class NowHarnessCapture
    {
        public string name;
        public int width;
        public int height;
        public string path;
        public int batchCount;
        public int vertexCount;
        public long elapsedMilliseconds;
    }

    internal static class NowHarnessScenarios
    {
        /// <summary>
        /// Supersampling factor for captures. Shader feather AA resolves per
        /// pixel, so rendering at 2x matches what a high-DPI game view shows;
        /// golden comparisons pin this to 1 to keep baselines small and stable.
        /// </summary>
        public static int renderScale = 1;

        /// <summary>
        /// Stamps a small "Rendered with NowUI" chip on captures. Only the
        /// visual (README) runner enables this; goldens and perf never brand so
        /// baselines and timings stay comparable.
        /// </summary>
        public static bool brandCaptures;

        const string MarkdownSample =
            "# Harness markdown\n\n" +
            "NowUI renders **layout**, `inline code`, lists, links, and code fences through the same immediate-mode frame.\n\n" +
            "- deterministic offscreen target\n" +
            "- real font atlas upload\n" +
            "- reusable screenshot artifact\n\n" +
            "```csharp\nNowLayout.Button(\"Apply\").Draw();\n```";

        static readonly string[] QualityOptions = { "Low", "Medium", "High", "Ultra" };
        static readonly string[] HierarchyObjects = { "Camera", "Directional Light", "Player", "Environment" };
        static readonly NowRectangleStyle[] ThemeRectangleStyles =
            (NowRectangleStyle[])Enum.GetValues(typeof(NowRectangleStyle));
        static readonly NowTextStyle[] ThemeTextStyles =
            (NowTextStyle[])Enum.GetValues(typeof(NowTextStyle));
        static readonly NowColorToken[] ThemeColorTokens =
            (NowColorToken[])Enum.GetValues(typeof(NowColorToken));

        static readonly MethodInfo RepaintImmediatelyMethod = typeof(EditorWindow).GetMethod(
            "RepaintImmediately",
            BindingFlags.Instance | BindingFlags.NonPublic);

        const string ThemesFolder = "Assets/NowUI/Assets/Themes";
        const string ThemeReviewPrefix = "theme-review-";

        static readonly IdleInputProvider Input = new IdleInputProvider();

        static NowDockSpace _dock;
        static NowNodeGraphSchema _nodeSchema;
        static NowNodeGraph _nodeGraph;
        static NowNodeGraphHistory _nodeHistory;
        static NowLottieAsset _lottie;
        static Material _sdfAuroraMaterial;
        static Material _sdfTopographicMaterial;
        static Material _sdfPaperCutoutMaterial;
        static string _filePickerFixtureDirectory;
        static string _filePickerPreviewPath;
        static string _filePickerSavePath;
        static Texture2D _filePickerPreviewTexture;

        sealed class IdleInputProvider : INowInputProvider
        {
            public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
            {
                snapshot = new NowInputSnapshot(
                    false,
                    default,
                    default,
                    default,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    default,
                    default,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    Time.frameCount,
                    Time.realtimeSinceStartup);
                return true;
            }
        }

        sealed class StaticPointerInputProvider : INowInputProvider
        {
            readonly Vector2 _pointer;
            int _frame;

            public StaticPointerInputProvider(Vector2 pointer)
            {
                _pointer = pointer;
            }

            public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
            {
                ++_frame;
                snapshot = new NowInputSnapshot(
                    true,
                    _pointer,
                    _pointer,
                    Vector2.zero,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    default,
                    default,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    _frame,
                    _frame * 0.05f);
                return true;
            }
        }

        sealed class SequencePointerInputProvider : INowInputProvider
        {
            readonly Vector2[] _points;
            int _frame;

            public SequencePointerInputProvider(Vector2[] points)
            {
                _points = points ?? Array.Empty<Vector2>();
            }

            public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
            {
                int index = _points.Length > 0 ? Mathf.Min(_frame, _points.Length - 1) : 0;
                int previousIndex = _points.Length > 0 ? Mathf.Max(0, index - 1) : 0;
                Vector2 pointer = _points.Length > 0 ? _points[index] : Vector2.zero;
                Vector2 previous = _points.Length > 0 ? _points[previousIndex] : pointer;
                Vector2 delta = pointer - previous;

                ++_frame;
                snapshot = new NowInputSnapshot(
                    _points.Length > 0,
                    pointer,
                    previous,
                    delta,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    NowPointerButtons.None,
                    default,
                    default,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    _frame,
                    _frame * 0.05f);
                return true;
            }
        }

        sealed class ClickThenIdleInputProvider : INowInputProvider
        {
            readonly Vector2 _clickPointer;
            readonly Vector2 _restingPointer;
            readonly Vector2 _scrollDelta;
            readonly Vector2? _followupClickPointer;
            int _frame;

            public ClickThenIdleInputProvider(Vector2 pointer)
                : this(pointer, pointer, Vector2.zero, null)
            {
            }

            public ClickThenIdleInputProvider(
                Vector2 clickPointer,
                Vector2 restingPointer,
                Vector2 scrollDelta)
                : this(clickPointer, restingPointer, scrollDelta, null)
            {
            }

            public ClickThenIdleInputProvider(
                Vector2 clickPointer,
                Vector2 restingPointer,
                Vector2 scrollDelta,
                Vector2? followupClickPointer)
            {
                _clickPointer = clickPointer;
                _restingPointer = restingPointer;
                _scrollDelta = scrollDelta;
                _followupClickPointer = followupClickPointer;
            }

            public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
            {
                bool pressed = _frame == 0 || (_followupClickPointer.HasValue && _frame == 3);
                bool released = _frame == 1 || (_followupClickPointer.HasValue && _frame == 4);
                bool scrolling = _frame == 2 && _scrollDelta != Vector2.zero;
                Vector2 pointer = _followupClickPointer.HasValue && _frame >= 3
                    ? _followupClickPointer.Value
                    : scrolling ? _restingPointer : _clickPointer;
                Vector2 previous = _frame == 2
                    ? _clickPointer
                    : _frame == 3 && _scrollDelta != Vector2.zero
                        ? _restingPointer
                        : pointer;
                NowPointerButtons down = pressed ? NowPointerButtons.Primary : NowPointerButtons.None;
                NowPointerButtons pressedButtons = pressed ? NowPointerButtons.Primary : NowPointerButtons.None;
                NowPointerButtons releasedButtons = released ? NowPointerButtons.Primary : NowPointerButtons.None;

                ++_frame;
                snapshot = new NowInputSnapshot(
                    true,
                    pointer,
                    previous,
                    pointer - previous,
                    down,
                    pressedButtons,
                    releasedButtons,
                    scrolling ? _scrollDelta : Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    _frame,
                    _frame * 0.05f);
                return true;
            }
        }

        sealed class FixedWorldInputProvider : INowInputProvider
        {
            public NowWorldInputProvider inner;
            public NowMouseInput raw;

            public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
            {
                return inner.TryGetSnapshot(surface, raw, out snapshot);
            }
        }

#if NOWUI_UGUI
        sealed class LayoutCallbackGraphic : NowLayoutGraphic
        {
            public Action<NowRect> draw;

            protected override void DrawNowUI(NowRect view)
            {
                draw?.Invoke(view);
            }
        }
#endif

        sealed class WorldHarnessPanel : NowWorldGraphic
        {
            public INowInputProvider inputProvider;
            public NowThemeAsset theme;
            public Action<NowRect> draw;

            protected override INowInputProvider GetInputProvider()
            {
                return inputProvider ?? base.GetInputProvider();
            }

            protected override void DrawNowUI(NowRect rect)
            {
                if (draw == null)
                    return;

                if (theme != null)
                {
                    using (NowControls.Theme(theme))
                        draw(rect);
                    return;
                }

                draw(rect);
            }
        }

        public static IReadOnlyList<NowHarnessScenario> All(bool includeThemeReviews = true)
        {
            EnsureSharedState();

            var scenarios = new List<NowHarnessScenario>
            {
                new NowHarnessScenario { name = "controls", width = 960, height = 540, includeInGoldens = true, draw = DrawControls },
                new NowHarnessScenario { name = "controls-dark", width = 960, height = 540, includeInGoldens = true, darkTheme = true, draw = DrawControlsDark },
                new NowHarnessScenario { name = "editorgui-unity-editor-dark", width = 1100, height = 660, includeInGoldens = false, includeInPerf = false, suppressBadge = true, capture = CaptureEditorGUIUnityEditorDark },
                new NowHarnessScenario { name = "elevation", width = 840, height = 420, includeInGoldens = true, draw = DrawElevation },
                new NowHarnessScenario { name = "context-menu", width = 640, height = 420, includeInGoldens = true, draw = DrawContextMenu },
                new NowHarnessScenario { name = "context-submenus", width = 720, height = 420, includeInGoldens = true, createInputProvider = () => new StaticPointerInputProvider(new Vector2(80f, 136f)), draw = DrawContextSubmenus },
                new NowHarnessScenario { name = "context-edge-submenu", width = 520, height = 300, includeInGoldens = true, createInputProvider = () => new StaticPointerInputProvider(new Vector2(336f, 134f)), draw = DrawContextEdgeSubmenu },
                new NowHarnessScenario { name = "context-ping-pong-submenus", width = 512, height = 320, includeInGoldens = true, warmupFrames = 3, createInputProvider = () => new SequencePointerInputProvider(new[] { new Vector2(266f, 134f), new Vector2(266f, 134f), new Vector2(128f, 162f), new Vector2(128f, 162f) }), draw = DrawContextPingPongSubmenus },
                new NowHarnessScenario { name = "world-context-ping-pong-submenus", width = 512, height = 320, includeInGoldens = true, warmupFrames = 3, capture = CaptureWorldContextPingPongSubmenus },
                new NowHarnessScenario { name = "world-multi-surface-overlap", width = 640, height = 360, includeInGoldens = true, warmupFrames = 3, capture = CaptureWorldMultiSurfaceOverlap },
                new NowHarnessScenario { name = "text-layout", width = 960, height = 540, includeInGoldens = true, draw = DrawTextLayout },
                new NowHarnessScenario { name = "glass", width = 640, height = 360, includeInGoldens = true, draw = DrawGlass },
                new NowHarnessScenario { name = "shader-variants", width = 840, height = 420, includeInGoldens = true, draw = DrawShaderVariants },
#if NOWUI_UGUI
                new NowHarnessScenario { name = "quick-start-overlay", width = 500, height = 400, includeInGoldens = true, draw = DrawQuickStartOverlay, capture = CaptureQuickStartOverlay },
                new NowHarnessScenario { name = "quick-start-score", width = 300, height = 120, includeInGoldens = false, darkTheme = true, suppressBadge = true, draw = DrawQuickStartScore, capture = CaptureQuickStartScore },
                new NowHarnessScenario { name = "quick-start-settings", width = 360, height = 190, includeInGoldens = false, darkTheme = true, suppressBadge = true, draw = DrawQuickStartSettings, capture = CaptureQuickStartSettings },
#endif
                new NowHarnessScenario { name = "sdf-mask-glow-clip", width = 640, height = 640, includeInGoldens = true, warmupFrames = 2, draw = DrawSdfMaskGlowClip },
                new NowHarnessScenario { name = "sdf-mask-gallery", width = 960, height = 520, includeInGoldens = true, warmupFrames = 2, draw = DrawSdfMaskGallery },
                new NowHarnessScenario { name = "sdf-planar-primitives", width = 960, height = 390, includeInGoldens = true, warmupFrames = 2, draw = DrawSdfPlanarPrimitives },
                new NowHarnessScenario { name = "sdf-radial-primitives", width = 840, height = 360, includeInGoldens = true, warmupFrames = 2, draw = DrawSdfRadialPrimitives },
                new NowHarnessScenario { name = "sdf-custom-shaders", width = 960, height = 430, includeInGoldens = false, warmupFrames = 2, draw = DrawSdfCustomShaders },
                new NowHarnessScenario { name = "lottie", width = 512, height = 512, includeInGoldens = true, draw = DrawLottie },
                new NowHarnessScenario { name = "logo", width = 960, height = 240, includeInGoldens = false, warmupFrames = 2, draw = DrawLogo },
                new NowHarnessScenario { name = "model-preview-effects", width = 720, height = 420, includeInGoldens = false, warmupFrames = 2, capture = CaptureModelPreviewEffects },
                new NowHarnessScenario { name = "file-picker-open-image-preview", width = 1024, height = 640, includeInGoldens = false, warmupFrames = 4, prepare = PrepareFilePickerFixture, createInputProvider = () => new ClickThenIdleInputProvider(new Vector2(208f, 33f)), afterWarmup = InjectFilePickerPreviewFixture, draw = DrawFilePickerOpenImagePreview },
                new NowHarnessScenario { name = "file-picker-save-no-preview", width = 1024, height = 640, includeInGoldens = false, warmupFrames = 2, prepare = PrepareFilePickerFixture, createInputProvider = () => new ClickThenIdleInputProvider(new Vector2(208f, 33f)), draw = DrawFilePickerSaveNoPreview },
                new NowHarnessScenario { name = "file-picker-directory-places", width = 1024, height = 640, includeInGoldens = false, warmupFrames = 3, prepare = PrepareFilePickerFixture, createInputProvider = () => new ClickThenIdleInputProvider(new Vector2(208f, 33f), new Vector2(160f, 260f), new Vector2(0f, 100f)), draw = DrawFilePickerDirectoryPlaces },
                new NowHarnessScenario { name = "file-picker-place-navigation", width = 1024, height = 640, includeInGoldens = false, warmupFrames = 5, prepare = PrepareFilePickerFixture, createInputProvider = () => new ClickThenIdleInputProvider(new Vector2(208f, 33f), new Vector2(160f, 260f), new Vector2(0f, 100f), new Vector2(160f, 262f)), draw = DrawFilePickerPlaceNavigation },
#if NOWUI_UGUI
                new NowHarnessScenario { name = "docs-model-preview-demo", width = 1280, height = 720, includeInGoldens = false, warmupFrames = 3, capture = CaptureDocsModelPreviewDemo },
                new NowHarnessScenario { name = "landing-page-now", width = 1280, height = 720, includeInGoldens = true, warmupFrames = 2, suppressBadge = true, capture = CaptureLandingPageNow },
                new NowHarnessScenario { name = "landing-page-now-layout", width = 1280, height = 720, includeInGoldens = true, warmupFrames = 2, suppressBadge = true, capture = CaptureLandingPageNowLayout },
                new NowHarnessScenario { name = "landing-page-now-compact", width = 360, height = 640, includeInGoldens = true, warmupFrames = 2, suppressBadge = true, capture = CaptureLandingPageNow },
                new NowHarnessScenario { name = "landing-page-now-layout-compact", width = 360, height = 640, includeInGoldens = true, warmupFrames = 2, suppressBadge = true, capture = CaptureLandingPageNowLayout },
#endif
                new NowHarnessScenario { name = "markdown-code", width = 960, height = 540, includeInGoldens = false, draw = DrawMarkdown },
                new NowHarnessScenario { name = "docking", width = 960, height = 540, includeInGoldens = false, darkTheme = true, draw = DrawDocking },
                new NowHarnessScenario { name = "node-graph", width = 960, height = 540, includeInGoldens = false, darkTheme = true, draw = DrawNodeGraph }
            };

            if (includeThemeReviews)
                scenarios.AddRange(ThemeReviewScenarios());

            return scenarios;
        }

        internal static IReadOnlyList<NowHarnessScenario> ThemeReviewScenarios()
        {
            ValidateThemeReviewLayout();

            string[] guids = AssetDatabase.FindAssets("t:NowThemeAsset", new[] { ThemesFolder });
            var paths = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; ++i)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path.Replace('\\', '/'));
            }

            paths.Sort(StringComparer.Ordinal);

            var scenarios = new List<NowHarnessScenario>(paths.Count);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < paths.Count; ++i)
            {
                string path = paths[i];
                var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(path);
                if (theme == null)
                    throw new InvalidOperationException($"Theme review discovery could not load '{path}' as a NowThemeAsset.");

                string name = ThemeReviewPrefix + ToKebabCase(Path.GetFileNameWithoutExtension(path));
                if (names.TryGetValue(name, out string previousPath))
                {
                    throw new InvalidOperationException(
                        $"Duplicate theme review scenario name '{name}' for '{previousPath}' and '{path}'.");
                }

                names.Add(name, path);

                scenarios.Add(new NowHarnessScenario
                {
                    name = name,
                    width = 1280,
                    height = 960,
                    includeInGoldens = false,
                    includeInPerf = false,
                    themePath = path,
                    suppressBadge = true,
                    warmupFrames = 1,
                    draw = DrawThemeReview
                });
            }

            if (scenarios.Count == 0)
                throw new InvalidOperationException($"No NowThemeAsset instances were found under '{ThemesFolder}'.");

            return scenarios;
        }

        static void ValidateThemeReviewLayout()
        {
            if (ThemeRectangleStyles.Length > 8 || ThemeTextStyles.Length > 10 || ThemeColorTokens.Length > 27)
            {
                throw new InvalidOperationException(
                    "The theme review sheet layout must be expanded for newly added theme enums " +
                    $"(rectangles={ThemeRectangleStyles.Length}/8, text={ThemeTextStyles.Length}/10, colors={ThemeColorTokens.Length}/27).");
            }
        }

        static string ToKebabCase(string value)
        {
            var builder = new StringBuilder(value != null ? value.Length + 8 : 0);
            bool separatorPending = false;

            for (int i = 0; i < (value?.Length ?? 0); ++i)
            {
                char current = value[i];
                if (!char.IsLetterOrDigit(current))
                {
                    separatorPending = builder.Length > 0;
                    continue;
                }

                bool uppercaseBoundary = char.IsUpper(current) && builder.Length > 0 &&
                    i > 0 && char.IsLetterOrDigit(value[i - 1]) && !char.IsUpper(value[i - 1]);
                if ((separatorPending || uppercaseBoundary) && builder[builder.Length - 1] != '-')
                    builder.Append('-');

                builder.Append(char.ToLowerInvariant(current));
                separatorPending = false;
            }

            return builder.ToString().Trim('-');
        }

        public static NowHarnessCapture Capture(NowHarnessScenario scenario, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            ResetFrameState();
            scenario.prepare?.Invoke(outputPath);

            if (scenario.capture != null)
                return scenario.capture(scenario, outputPath);

            var stopwatch = Stopwatch.StartNew();
            int scale = Mathf.Max(1, renderScale);
            using var renderer = new NowRenderer();
            var target = new RenderTexture(scenario.width * scale, scenario.height * scale, 0, RenderTextureFormat.ARGB32)
            {
                name = "NowUI Harness Target",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();

            try
            {
                var surface = new NowInputSurface(new Vector2(scenario.width, scenario.height));
                var inputProvider = scenario.createInputProvider != null ? scenario.createInputProvider() : Input;
                int warmupFrames = Mathf.Max(1, scenario.warmupFrames);

                Now.SetUIScale(scale);

                for (int i = 0; i < warmupFrames; ++i)
                    renderer.Warmup(surface, inputProvider, () => DrawScenarioFrame(scenario));

                scenario.afterWarmup?.Invoke();

                using (NowInput.Begin(inputProvider, surface))
                using (renderer.Begin(new Vector2(scenario.width, scenario.height)))
                {
                    DrawScenarioFrame(scenario);
                }

                renderer.Render(target, clear: true, clearColor: new Color(0.04f, 0.045f, 0.055f, 1f));
                WritePng(target, outputPath);
                stopwatch.Stop();

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    path = outputPath,
                    batchCount = renderer.batchCount,
                    vertexCount = renderer.mesh != null ? renderer.mesh.vertexCount : 0,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                Now.SetUIScale(1f);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static NowHarnessCapture CaptureEditorGUIUnityEditorDark(
            NowHarnessScenario scenario,
            string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            NowEditorThemeComparisonWindow window = null;
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                NowEditorGUI.DisposeAll();

                NowThemeAsset theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(
                    NowEditorThemeComparisonWindow.DefaultThemePath);
                if (theme == null)
                {
                    throw new InvalidOperationException(
                        $"Editor comparison theme is missing at '{NowEditorThemeComparisonWindow.DefaultThemePath}'.");
                }

                if (RepaintImmediatelyMethod == null)
                {
                    throw new MissingMethodException(
                        typeof(EditorWindow).FullName,
                        "RepaintImmediately");
                }

                window = ScriptableObject.CreateInstance<NowEditorThemeComparisonWindow>();
                window.titleContent = new GUIContent("Editor Theme Comparison");
                window.ConfigureForCapture(theme);
                window.minSize = window.maxSize = new Vector2(scenario.width, scenario.height);
                window.position = new Rect(64f, 64f, scenario.width, scenario.height);
                window.ShowUtility();
                window.position = new Rect(64f, 64f, scenario.width, scenario.height);
                window.Focus();

                if (!window.hasFocus)
                    throw new InvalidOperationException("The editor comparison capture window did not receive focus.");

                // First pass warms the IMGUI-hosted NowUI texture/font state;
                // the second pass is the stable backing image that we capture.
                RepaintImmediatelyMethod.Invoke(window, null);
                RepaintImmediatelyMethod.Invoke(window, null);

                float pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
                int width = Mathf.CeilToInt(window.position.width * pixelsPerPoint);
                int height = Mathf.CeilToInt(window.position.height * pixelsPerPoint);
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "NowUI EditorGUI comparison capture",
                    antiAliasing = 1,
                    hideFlags = HideFlags.HideAndDontSave
                };
                target.Create();

                if (!InternalEditorUtility.CaptureEditorWindow(window, target))
                    throw new InvalidOperationException("Unity failed to capture the focused editor comparison window.");

                WritePng(target, outputPath);
                stopwatch.Stop();

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = width,
                    height = height,
                    path = outputPath,
                    batchCount = 0,
                    vertexCount = 0,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                if (target != null)
                {
                    RenderTexture.active = previousActive;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }

                if (window != null)
                {
                    if (window)
                        window.Close();
                    if (window)
                        UnityEngine.Object.DestroyImmediate(window);
                }

                NowEditorGUI.DisposeAll();
            }
        }

#if NOWUI_UGUI
        static NowHarnessCapture CaptureLandingPageNow(NowHarnessScenario scenario, string outputPath)
        {
            return CaptureCanvasHost(scenario, outputPath, layout: false);
        }

        static NowHarnessCapture CaptureLandingPageNowLayout(NowHarnessScenario scenario, string outputPath)
        {
            return CaptureCanvasHost(scenario, outputPath, layout: true);
        }

        static NowHarnessCapture CaptureQuickStartOverlay(NowHarnessScenario scenario, string outputPath)
        {
            return CaptureCanvasHost(scenario, outputPath, layout: false, draw: _ => DrawScenarioFrame(scenario));
        }

        static NowHarnessCapture CaptureQuickStartScore(NowHarnessScenario scenario, string outputPath)
        {
            return CaptureCanvasHost(scenario, outputPath, layout: false, draw: _ => DrawScenarioFrame(scenario));
        }

        static NowHarnessCapture CaptureQuickStartSettings(NowHarnessScenario scenario, string outputPath)
        {
            return CaptureCanvasHost(scenario, outputPath, layout: true, draw: _ => DrawScenarioFrame(scenario));
        }

        /// <summary>
        /// The README Quick Start explicit-placement snippet, drawn verbatim over
        /// a gradient standing in for the game view behind the overlay.
        /// </summary>
        static void DrawQuickStartScore(NowRect rect)
        {
            Now.Gradient(rect, new Color(0.10f, 0.14f, 0.24f, 1f), new Color(0.05f, 0.06f, 0.10f, 1f))
                .SetLinear(115f)
                .Draw();

            var panel = new NowRect(rect.x + 20, rect.y + 20, 260, 80);
            Now.Rectangle(panel)
                .SetColor(new Color(0, 0, 0, 0.8f))
                .SetRadius(10)
                .Draw();

            Now.Text(panel.Inset(16))
                .SetFontSize(32)
                .SetColor(Color.white)
                .Draw("Score: 1200");
        }

        /// <summary>The README Quick Start measured-layout snippet, drawn verbatim.</summary>
        static void DrawQuickStartSettings(NowRect rect)
        {
            using (NowLayout.Column(rect).Padding(16).Gap(8).Begin())
            {
                NowLayout.Label("Hello Now-UI").SetFontSize(32).Draw();

                using (NowLayout.Row()
                    .FillWidth()
                    .AlignChildren(NowLayoutAlign.Center)
                    .Begin())
                {
                    NowLayout.Label("Status").Draw();
                    NowLayout.Spacer();
                    NowLayout.Label("Ready").Draw();
                }

                NowLayout.Button("Sample Button").Draw();
            }
        }
#endif

        static NowHarnessCapture CaptureModelPreviewEffects(
            NowHarnessScenario scenario,
            string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            int scale = Mathf.Max(1, renderScale);
            using var renderer = new NowRenderer();
            var target = new RenderTexture(scenario.width * scale, scenario.height * scale, 0, RenderTextureFormat.ARGB32)
            {
                name = "NowUI Model Preview Harness Target",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            NowModelPreviewDemoRig demoRig = null;

            try
            {
                target.Create();
                demoRig = new NowModelPreviewDemoRig();
                var preview = demoRig.preview
                    .SetFixedResolution(256 * scale, 224 * scale)
                    .SetBackground(Color.clear);

                if (!preview.RenderNow())
                    throw new InvalidOperationException("The model-preview harness camera did not complete its offscreen render.");

                Now.defaultFont = Resources.Load<NowFontAsset>("NowUI/NotoSans");
                var surface = new NowInputSurface(new Vector2(scenario.width, scenario.height));
                int warmupFrames = Mathf.Max(1, scenario.warmupFrames);

                Now.SetUIScale(scale);

                for (int i = 0; i < warmupFrames; ++i)
                    renderer.Warmup(surface, Input, () => DrawModelPreviewEffectsFrame(scenario, preview));

                for (int renderPass = 0; renderPass < 2; ++renderPass)
                {
                    if (renderPass > 0)
                        renderer.Clear();

                    using (NowInput.Begin(Input, surface))
                    using (renderer.Begin(new Vector2(scenario.width, scenario.height)))
                    {
                        DrawModelPreviewEffectsFrame(scenario, preview);
                    }

                    // The first submitted frame warms actual GPU variants. The
                    // captured frame rebuilds the effect commands and temporary
                    // targets exactly as a normal subsequent UI frame would.
                    renderer.Render(target, clear: true, clearColor: new Color(0.025f, 0.03f, 0.045f, 1f));
                }

                WritePng(target, outputPath);
                stopwatch.Stop();

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    path = outputPath,
                    batchCount = renderer.batchCount,
                    vertexCount = renderer.mesh != null ? renderer.mesh.vertexCount : 0,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                Now.SetUIScale(1f);
                demoRig?.Dispose();
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

#if NOWUI_UGUI
        // Captures the actual in-app docs page, including its first deferred
        // model render and the retained texture-effect copy.
        static NowHarnessCapture CaptureDocsModelPreviewDemo(
            NowHarnessScenario scenario,
            string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            int scale = Mathf.Max(1, renderScale);
            var target = new RenderTexture(scenario.width * scale, scenario.height * scale, 24, RenderTextureFormat.ARGB32)
            {
                name = "NowUI Docs Model Preview Target",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            var cameraObject = new GameObject("NowUI Docs Model Preview Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var canvasObject = new GameObject(
                "NowUI Docs Model Preview Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                target.Create();

                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.045f, 0.055f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = scenario.height * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.targetTexture = target;
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.pixelPerfect = true;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = scale;

                var panelObject = new GameObject(
                    "NowUI Docs Model Preview Host",
                    typeof(RectTransform),
                    typeof(CanvasRenderer))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                panelObject.transform.SetParent(canvasObject.transform, false);

                var panelRect = panelObject.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                var graphic = panelObject.AddComponent<NowDocsExample>();
                graphic.raycastTarget = false;
                graphic.ConfigureModelPreviewsDemoHarness(
                    AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/DefaultDark.asset"),
                    Resources.Load<NowFontAsset>("NowUI/NotoSans"));
                AddCanvasBrandBadge(scenario, canvasObject);

                int warmupFrames = Mathf.Max(2, scenario.warmupFrames);

                for (int i = 0; i < warmupFrames; ++i)
                {
                    graphic.SetVerticesDirty();
                    Canvas.ForceUpdateCanvases();

                    if (i == 0 && !graphic.RenderModelPreviewsDemoNowForHarness())
                    {
                        throw new InvalidOperationException(
                            "The docs model-preview target was not prepared by its first UI rebuild.");
                    }
                }

                graphic.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                WritePng(target, outputPath);
                stopwatch.Stop();

                int vertexCount = 0;
                for (int i = 0; i < graphic.canvasPageCount; ++i)
                {
                    var mesh = graphic.GetCanvasPageMesh(i);
                    vertexCount += mesh != null ? mesh.vertexCount : 0;
                }

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    path = outputPath,
                    batchCount = graphic.canvasRenderer.materialCount,
                    vertexCount = vertexCount,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
#endif

        static void DrawModelPreviewEffectsFrame(
            NowHarnessScenario scenario,
            NowModelPreview preview)
        {
            var rect = new NowRect(0f, 0f, scenario.width, scenario.height);
            var background = new Color(0.025f, 0.032f, 0.052f, 1f);
            var card = new Color(0.055f, 0.072f, 0.108f, 1f);
            var line = new Color(0.3f, 0.42f, 0.62f, 0.34f);
            var text = new Color(0.93f, 0.96f, 1f, 1f);
            var muted = new Color(0.54f, 0.63f, 0.76f, 1f);
            var blue = new Color(0.08f, 0.62f, 1f, 1f);
            var orange = new Color(1f, 0.32f, 0.08f, 1f);

            Now.Rectangle(rect).SetColor(background).Draw();
            Now.Rectangle(new NowRect(0f, 0f, rect.width, 112f))
                .SetColor(new Color(0.045f, 0.06f, 0.095f, 1f))
                .Draw();
            Now.Text(new NowRect(36f, 24f, rect.width - 72f, 34f))
                .SetFontSize(26f)
                .SetBold()
                .SetColor(text)
                .Draw("3D models, composed like textures");
            Now.Text(new NowRect(36f, 66f, rect.width - 72f, 24f))
                .SetFontSize(14f)
                .SetColor(muted)
                .Draw("Same camera-backed preview • rounded mask • texture-backed deformation");

            DrawModelPreviewCard(
                new NowRect(38f, 128f, 304f, 256f),
                preview,
                "LIVE RENDER TEXTURE",
                "Direct Now.Model draw",
                blue,
                textureEffect: false);
            DrawModelPreviewCard(
                new NowRect(378f, 128f, 304f, 256f),
                preview,
                "TEXTURE-BACKED WAVE",
                "Captured, then deformed",
                orange,
                textureEffect: true);

            if (brandCaptures && !scenario.suppressBadge)
                DrawBrandBadge(rect);

            void DrawModelPreviewCard(
                NowRect cardRect,
                NowModelPreview modelPreview,
                string title,
                string subtitle,
                Color accent,
                bool textureEffect)
            {
                Now.Rectangle(cardRect)
                    .SetColor(card)
                    .SetRadius(22f)
                    .SetOutline(1f, line)
                    .Draw();
                Now.Circle(new Vector2(cardRect.x + 24f, cardRect.y + 25f), 5f)
                    .SetColor(accent)
                    .Draw();
                Now.Text(new NowRect(cardRect.x + 38f, cardRect.y + 14f, cardRect.width - 54f, 22f))
                    .SetFontSize(14f)
                    .SetBold()
                    .SetColor(text)
                    .Draw(title);
                Now.Text(new NowRect(cardRect.x + 20f, cardRect.y + 40f, cardRect.width - 40f, 20f))
                    .SetFontSize(12f)
                    .SetColor(muted)
                    .Draw(subtitle);

                var modelRect = new NowRect(cardRect.x + 44f, cardRect.y + 64f, 216f, 168f);
                Now.Rectangle(modelRect)
                    .SetColor(new Color(accent.r * 0.08f, accent.g * 0.08f, accent.b * 0.08f, 1f))
                    .SetRadius(28f)
                    .Draw();
                Now.Circle(modelRect.center, 70f)
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.12f))
                    .Draw();

                using (Now.Mask(cardRect.Inset(12f)))
                {
                    if (textureEffect)
                    {
                        using (NowEffects.Modifier(NowDeformers.Wave(0.08f, 6f, 52f, NowWaveAxis.Y))
                            .SetId(0x4D504556)
                            .SetSubdivision(12)
                            .SetRenderToTexture()
                            .SetSourceRect(modelRect)
                            .Begin())
                        {
                            DrawPreview();
                        }
                    }
                    else
                    {
                        DrawPreview();
                    }
                }

                Now.Rectangle(new NowRect(cardRect.x + 20f, cardRect.yMax - 12f, 72f, 4f))
                    .SetColor(accent)
                    .SetRadius(2f)
                    .Draw();

                void DrawPreview()
                {
                    Now.Model(modelRect, modelPreview)
                        .SetRadius(28f)
                        .SetOutline(1f, new Color(1f, 1f, 1f, 0.18f))
                        .Draw();
                }
            }
        }

#if NOWUI_UGUI
        static NowHarnessCapture CaptureCanvasHost(
            NowHarnessScenario scenario,
            string outputPath,
            bool layout,
            Action<NowRect> draw = null)
        {
            var stopwatch = Stopwatch.StartNew();
            int scale = Mathf.Max(1, renderScale);
            var target = new RenderTexture(scenario.width * scale, scenario.height * scale, 24, RenderTextureFormat.ARGB32)
            {
                name = $"NowUI Canvas Harness Target ({scenario.name})",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();

            var cameraObject = new GameObject("NowUI Canvas Harness Camera") { hideFlags = HideFlags.HideAndDontSave };
            var canvasObject = new GameObject(
                "NowUI Canvas Harness Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.045f, 0.055f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = scenario.height * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.targetTexture = target;
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.pixelPerfect = true;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = scale;

                var panelObject = new GameObject(
                    $"NowUI Canvas Harness Host ({scenario.name})",
                    typeof(RectTransform),
                    typeof(CanvasRenderer))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                panelObject.transform.SetParent(canvasObject.transform, false);

                var panelRect = panelObject.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                NowGraphic graphic;

                if (draw != null && layout)
                {
                    var layoutGraphic = panelObject.AddComponent<LayoutCallbackGraphic>();
                    layoutGraphic.draw = draw;
                    graphic = layoutGraphic;
                }
                else if (draw != null)
                {
                    graphic = panelObject.AddComponent<NowGraphic>();
                    graphic.rebuildNowUI += (_, hostRect) => draw(hostRect);
                }
                else if (layout)
                {
                    graphic = panelObject.AddComponent<NowLayoutLandingPageExample>();
                }
                else
                {
                    graphic = panelObject.AddComponent<NowLandingPageExample>();
                }

                graphic.raycastTarget = false;

                if (draw == null)
                    AddCanvasBrandBadge(scenario, canvasObject);

                int warmupFrames = Mathf.Max(1, scenario.warmupFrames);
                for (int i = 0; i < warmupFrames; ++i)
                {
                    graphic.SetVerticesDirty();
                    Canvas.ForceUpdateCanvases();
                }

                graphic.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                WritePng(target, outputPath);
                stopwatch.Stop();

                int vertexCount = 0;
                for (int i = 0; i < graphic.canvasPageCount; ++i)
                {
                    var mesh = graphic.GetCanvasPageMesh(i);
                    vertexCount += mesh != null ? mesh.vertexCount : 0;
                }

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    path = outputPath,
                    batchCount = graphic.canvasRenderer.materialCount,
                    vertexCount = vertexCount,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
#endif

        static NowHarnessCapture CaptureWorldContextPingPongSubmenus(NowHarnessScenario scenario, string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            int scale = Mathf.Max(1, renderScale);
            var target = new RenderTexture(scenario.width * scale, scenario.height * scale, 24, RenderTextureFormat.ARGB32)
            {
                name = "NowUI World Harness Target",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();

            var cameraObject = new GameObject("NowUI World Harness Camera") { hideFlags = HideFlags.HideAndDontSave };
            var panelObject = new GameObject("NowUI World Harness Panel") { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                float pixelsPerUnit = 400f;
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.045f, 0.055f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = scenario.height / (pixelsPerUnit * 2f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.targetTexture = target;
                cameraObject.transform.position = new Vector3(0f, 0f, -2f);

                var panel = panelObject.AddComponent<WorldHarnessPanel>();
                panel.targetCamera = camera;
                panel.facingMode = NowWorldFacingMode.None;
                panel.depthMode = NowWorldDepthMode.AlwaysVisible;
                panel.glassBackdropMode = NowWorldGlassBackdropMode.TintOnly;
                panel.size = new Vector2(scenario.width, scenario.height);
                panel.pixelsPerUnit = pixelsPerUnit;
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.inputProvider = new SequencePointerInputProvider(new[]
                {
                    new Vector2(266f, 134f),
                    new Vector2(266f, 134f),
                    new Vector2(128f, 162f),
                    new Vector2(128f, 162f)
                });
                panel.theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Default.asset");
                panel.draw = DrawWorldContextPingPongSubmenus;

                int warmupFrames = Mathf.Max(1, scenario.warmupFrames);
                for (int i = 0; i < warmupFrames; ++i)
                    panel.RebuildNowUI();

                panel.RebuildNowUI();
                camera.Render();
                WritePng(target, outputPath);
                stopwatch.Stop();

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    path = outputPath,
                    batchCount = panel.batchCount,
                    vertexCount = panel.mesh != null ? panel.mesh.vertexCount : 0,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(panelObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static NowHarnessCapture CaptureWorldMultiSurfaceOverlap(NowHarnessScenario scenario, string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            int scale = Mathf.Max(1, renderScale);
            var target = new RenderTexture(scenario.width * scale, scenario.height * scale, 24, RenderTextureFormat.ARGB32)
            {
                name = "NowUI World Multi Surface Target",
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();

            var cameraObject = new GameObject("NowUI World Multi Surface Camera") { hideFlags = HideFlags.HideAndDontSave };
            var backObject = new GameObject("NowUI World Multi Surface Back") { hideFlags = HideFlags.HideAndDontSave };
            var frontObject = new GameObject("NowUI World Multi Surface Front") { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                float pixelsPerUnit = 400f;
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.045f, 0.055f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = scenario.height / (pixelsPerUnit * 2f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.targetTexture = target;
                cameraObject.transform.position = new Vector3(0f, 0f, -2f);

                var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Default.asset");
                var rawPointer = new NowMouseInput
                {
                    hasPointer = true,
                    screenPosition = new Vector2(scenario.width * 0.5f, scenario.height * 0.455f)
                };

                var back = backObject.AddComponent<WorldHarnessPanel>();
                backObject.transform.position = new Vector3(-0.12f, 0.04f, 0.1f);
                ConfigureWorldHarnessPanel(back, camera, pixelsPerUnit, new Vector2(280f, 170f), theme);
                back.inputProvider = new FixedWorldInputProvider
                {
                    inner = new NowWorldInputProvider { graphic = back, camera = camera },
                    raw = rawPointer
                };
                back.draw = DrawWorldBackSurface;

                var front = frontObject.AddComponent<WorldHarnessPanel>();
                frontObject.transform.position = new Vector3(0.12f, -0.04f, -0.25f);
                ConfigureWorldHarnessPanel(front, camera, pixelsPerUnit, new Vector2(240f, 150f), theme);
                front.inputProvider = new FixedWorldInputProvider
                {
                    inner = new NowWorldInputProvider { graphic = front, camera = camera },
                    raw = rawPointer
                };
                front.draw = DrawWorldFrontSurface;

                int warmupFrames = Mathf.Max(2, scenario.warmupFrames);
                for (int i = 0; i < warmupFrames; ++i)
                    RebuildWorldOverlapFrame(back, front);

                RebuildWorldOverlapFrame(back, front);
                camera.Render();
                WritePng(target, outputPath);
                stopwatch.Stop();

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = target.width,
                    height = target.height,
                    path = outputPath,
                    batchCount = back.batchCount + front.batchCount,
                    vertexCount = (back.mesh != null ? back.mesh.vertexCount : 0) +
                                  (front.mesh != null ? front.mesh.vertexCount : 0),
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(frontObject);
                UnityEngine.Object.DestroyImmediate(backObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public static byte[] CapturePngBytes(NowHarnessScenario scenario)
        {
            string temp = Path.Combine(Path.GetTempPath(), $"nowui-{scenario.name}-{Guid.NewGuid():N}.png");
            try
            {
                Capture(scenario, temp);
                return File.ReadAllBytes(temp);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        public static string BuildManifest(IEnumerable<NowHarnessCapture> captures)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"captures\": [");

            bool first = true;
            foreach (var capture in captures)
            {
                if (!first)
                    json.AppendLine(",");

                first = false;
                json.Append("    { ");
                json.AppendFormat("\"name\": \"{0}\", ", Escape(capture.name));
                json.AppendFormat("\"width\": {0}, \"height\": {1}, ", capture.width, capture.height);
                json.AppendFormat("\"batchCount\": {0}, \"vertexCount\": {1}, ", capture.batchCount, capture.vertexCount);
                json.AppendFormat("\"elapsedMilliseconds\": {0}, ", capture.elapsedMilliseconds);
                json.AppendFormat("\"path\": \"{0}\"", Escape(capture.path.Replace('\\', '/')));
                json.Append(" }");
            }

            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        public static string ProjectPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        public static string ReadArgument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; ++i)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return fallback;
        }

        public static bool HasArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; ++i)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static void ConfigureWorldHarnessPanel(
            WorldHarnessPanel panel,
            Camera camera,
            float pixelsPerUnit,
            Vector2 size,
            NowThemeAsset theme)
        {
            panel.targetCamera = camera;
            panel.facingMode = NowWorldFacingMode.None;
            panel.depthMode = NowWorldDepthMode.AlwaysVisible;
            panel.glassBackdropMode = NowWorldGlassBackdropMode.TintOnly;
            panel.size = size;
            panel.pixelsPerUnit = pixelsPerUnit;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.theme = theme;
        }

        static void RebuildWorldOverlapFrame(WorldHarnessPanel back, WorldHarnessPanel front)
        {
            NowOverlay.ForceNewFrame();
            NowPointerArbiter.ForceNewFrame();
            back.MarkDirty();
            front.MarkDirty();
            back.RebuildNowUI();
            front.RebuildNowUI();
        }

        static void DrawScenarioFrame(NowHarnessScenario scenario)
        {
            Now.defaultFont = Resources.Load<NowFontAsset>("NowUI/NotoSans");
            bool hasExplicitTheme = !string.IsNullOrWhiteSpace(scenario.themePath);
            string themePath = hasExplicitTheme
                ? scenario.themePath
                : scenario.darkTheme
                    ? "Assets/NowUI/Assets/Themes/DefaultDark.asset"
                    : "Assets/NowUI/Assets/Themes/Default.asset";
            var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
            var frame = new NowRect(0, 0, scenario.width, scenario.height);

            if (hasExplicitTheme && theme == null)
                throw new InvalidOperationException($"Theme review scenario '{scenario.name}' could not load '{themePath}'.");

            if (theme != null)
            {
                using (NowControls.Theme(theme))
                    scenario.draw(frame);
            }
            else
            {
                scenario.draw(frame);
            }

            if (brandCaptures && !scenario.suppressBadge)
                DrawBrandBadge(frame);
        }

#if NOWUI_UGUI
        /// <summary>Overlays the brand chip on component-hosted canvas captures.</summary>
        static void AddCanvasBrandBadge(NowHarnessScenario scenario, GameObject canvasObject)
        {
            if (!brandCaptures || scenario.suppressBadge)
                return;

            var badgeObject = new GameObject(
                "NowUI Harness Badge",
                typeof(RectTransform),
                typeof(CanvasRenderer))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            badgeObject.transform.SetParent(canvasObject.transform, false);

            var badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.anchorMin = Vector2.zero;
            badgeRect.anchorMax = Vector2.one;
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;

            var badge = badgeObject.AddComponent<NowGraphic>();
            badge.raycastTarget = false;
            badge.rebuildNowUI += (_, hostRect) => DrawBrandBadge(hostRect);
        }
#endif

        /// <summary>A small self-referential watermark: the chip is itself NowUI draws.</summary>
        static void DrawBrandBadge(NowRect rect)
        {
            var chip = new NowRect(rect.xMax - 168f, rect.yMax - 34f, 156f, 22f);
            Now.Rectangle(chip)
                .SetColor(new Color(0.02f, 0.03f, 0.06f, 0.66f))
                .SetRadius(11f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.14f))
                .Draw();

            float cy = chip.y + chip.height * 0.5f;
            float gx = chip.x + 10f;
            Span<Vector2> pulse = stackalloc Vector2[]
            {
                new Vector2(gx, cy),
                new Vector2(gx + 5f, cy),
                new Vector2(gx + 8f, cy - 4f),
                new Vector2(gx + 12f, cy + 4f),
                new Vector2(gx + 15f, cy),
                new Vector2(gx + 20f, cy)
            };

            for (int i = 0; i < pulse.Length - 1; ++i)
            {
                Now.Line(pulse[i], pulse[i + 1])
                    .SetWidth(1.6f)
                    .SetColor(new Color(0.55f, 0.60f, 0.95f, 1f))
                    .Draw();
            }

            Now.Text(new NowRect(chip.x + 34f, chip.y + 4f, chip.width - 38f, 15f))
                .SetFontSize(11f)
                .SetColor(new Color(1f, 1f, 1f, 0.78f))
                .Draw("Rendered with NowUI");
        }

        static void PrepareFilePickerFixture(string outputPath)
        {
            _ = outputPath;

            if (string.IsNullOrEmpty(_filePickerFixtureDirectory))
            {
                _filePickerFixtureDirectory = Path.Combine(
                    ProjectPath(),
                    "Library",
                    "NowUIHarness",
                    "FilePickerVisualV1");
                _filePickerPreviewPath = Path.Combine(_filePickerFixtureDirectory, "aurora-preview.png");
                _filePickerSavePath = Path.Combine(_filePickerFixtureDirectory, "layout.nowui");
            }

            Directory.CreateDirectory(_filePickerFixtureDirectory);
            Directory.CreateDirectory(Path.Combine(_filePickerFixtureDirectory, "Concepts"));
            Directory.CreateDirectory(Path.Combine(_filePickerFixtureDirectory, "Exports"));

            if (!File.Exists(_filePickerSavePath))
                File.WriteAllText(_filePickerSavePath, "{ \"name\": \"NowUI visual fixture\" }\n", new UTF8Encoding(false));

            string notesPath = Path.Combine(_filePickerFixtureDirectory, "readme.txt");
            if (!File.Exists(notesPath))
                File.WriteAllText(notesPath, "Deterministic NowUI file-picker fixture.\n", new UTF8Encoding(false));

            if (!File.Exists(_filePickerPreviewPath))
                WriteFilePickerPreviewFixture(_filePickerPreviewPath);

            if (_filePickerPreviewTexture == null)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "NowUI File Picker Loaded Fixture",
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (!texture.LoadImage(File.ReadAllBytes(_filePickerPreviewPath), markNonReadable: true))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidOperationException("Could not decode the file-picker preview fixture.");
                }

                _filePickerPreviewTexture = texture;
            }
        }

        static void WriteFilePickerPreviewFixture(string path)
        {
            const int width = 320;
            const int height = 180;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "NowUI File Picker Fixture",
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                var pixels = new Color32[width * height];

                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        byte red = (byte)(24 + x * 152 / (width - 1));
                        byte green = (byte)(38 + y * 128 / (height - 1));
                        byte blue = (byte)(112 + (width - 1 - x) * 92 / (width - 1));
                        bool ribbon = Mathf.Abs(y - (height - 1 - x * height / width)) < 12;

                        if (ribbon)
                        {
                            red = 244;
                            green = 190;
                            blue = 92;
                        }

                        pixels[y * width + x] = new Color32(red, green, blue, 255);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void InjectFilePickerPreviewFixture()
        {
            // ExecuteMethod captures do not advance the Editor player loop while warming up.
            // Inject the already-decoded deterministic fixture after the picker has requested it.
            const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;
            FieldInfo popupStatesField = typeof(NowFilePicker).GetField("_popupStates", StaticPrivate);
            var popupStates = popupStatesField?.GetValue(null) as IDictionary;

            if (popupStates == null)
                throw new InvalidOperationException("File-picker popup state was not available to the visual harness.");

            foreach (object popupState in popupStates.Values)
            {
                Type popupStateType = popupState.GetType();
                var thumbnails = popupStateType.GetField("thumbnails", InstancePublic)?.GetValue(popupState) as IDictionary;

                if (thumbnails == null)
                    continue;

                foreach (object thumbnail in thumbnails.Values)
                {
                    Type thumbnailType = thumbnail.GetType();
                    string path = thumbnailType.GetField("path", InstancePublic)?.GetValue(thumbnail) as string;

                    if (!string.Equals(path, _filePickerPreviewPath, StringComparison.Ordinal))
                        continue;

                    var requestField = thumbnailType.GetField("request", InstancePublic);
                    var request = requestField?.GetValue(thumbnail) as UnityEngine.Networking.UnityWebRequest;
                    request?.Abort();
                    request?.Dispose();
                    requestField?.SetValue(thumbnail, null);
                    thumbnailType.GetField("operation", InstancePublic)?.SetValue(thumbnail, null);
                    thumbnailType.GetField("texture", InstancePublic)?.SetValue(thumbnail, _filePickerPreviewTexture);

                    FieldInfo thumbnailStateField = thumbnailType.GetField("state", InstancePublic);
                    thumbnailStateField?.SetValue(
                        thumbnail,
                        Enum.Parse(thumbnailStateField.FieldType, "Loaded"));
                    popupStateType.GetField("activeThumbnailRequests", InstancePublic)?.SetValue(popupState, 0);
                    return;
                }
            }

            throw new InvalidOperationException("The selected preview fixture was not requested during harness warmup.");
        }

        static void DrawFilePickerOpenImagePreview(NowRect rect)
        {
            DrawSurface(rect);

            string path = _filePickerPreviewPath ?? string.Empty;
            Now.OpenFileField(
                    new NowRect(28f, 18f, 360f, 30f),
                    "harness-file-picker-open-image")
                .SetTitle("Open Image — Preview Enabled")
                .SetStartDirectory(_filePickerFixtureDirectory)
                .SetInitialView(NowFilePickerView.Details)
                .SetFilters(
                    new NowFileFilter("Images", "png", "jpg", "jpeg"),
                    new NowFileFilter("Text", "txt"))
                .SetPopupSize(920f, 560f)
                .Draw(ref path);
        }

        static void DrawFilePickerSaveNoPreview(NowRect rect)
        {
            DrawSurface(rect);

            string path = _filePickerSavePath ?? string.Empty;
            Now.SaveFileField(
                    new NowRect(28f, 18f, 360f, 30f),
                    "harness-file-picker-save")
                .SetTitle("Save Layout — Preview Suppressed")
                .SetStartDirectory(_filePickerFixtureDirectory)
                .SetInitialView(NowFilePickerView.Details)
                .SetFilters(
                    new NowFileFilter("NowUI layouts", "nowui"),
                    new NowFileFilter("Text", "txt"))
                .SetPopupSize(920f, 560f)
                .Draw(ref path);
        }

        static void DrawFilePickerDirectoryPlaces(NowRect rect)
        {
            DrawFilePickerDirectory(rect, "Select Folder — Places and Folders", "harness-file-picker-directory");
        }

        static void DrawFilePickerPlaceNavigation(NowRect rect)
        {
            DrawFilePickerDirectory(rect, "Select Folder — Place Stays Visible", "harness-file-picker-place-navigation");
        }

        static void DrawFilePickerDirectory(NowRect rect, string title, string id)
        {
            DrawSurface(rect);

            string path = _filePickerFixtureDirectory ?? string.Empty;
            Now.DirectoryField(
                    new NowRect(28f, 18f, 360f, 30f),
                    id)
                .SetTitle(title)
                .SetStartDirectory(_filePickerFixtureDirectory)
                .SetInitialView(NowFilePickerView.Details)
                .SetPopupSize(920f, 560f)
                .Draw(ref path);
        }

        static void DrawControlsDark(NowRect rect)
        {
            DrawControls(rect);
        }

        static void DrawThemeReview(NowRect rect)
        {
            DrawSurface(rect);

            var theme = NowTheme.themeAsset;
            string rendererName = theme.controlRenderer.GetType().Name;
            string mode = theme.isDark ? "Dark" : "Light";
            HeaderBlock(
                rect,
                $"{theme.name} Theme",
                $"{mode} • {rendererName} • all palette roles, presets, and representative controls.");

            DrawThemeSectionTitle(28f, 116f, "Rectangle presets", "Every built-in button style through this theme's control renderer.");
            DrawThemeRectangleStyles();

            DrawThemeSectionTitle(28f, 216f, "Palette roles", "Authored display/sRGB values; labels use an independent contrast color.");
            DrawThemePalette(theme);

            DrawThemeSectionTitle(28f, 464f, "Text presets", "The complete themed type scale over the theme surface.");
            DrawThemeTextStyles(theme);

            DrawThemeSectionTitle(28f, 648f, "Controls and popup states", "Deterministic idle, hover, pressed, selected, field, and popup states.");
            DrawThemeControls(theme);
        }

        static void DrawThemeRectangleStyles()
        {
            const float left = 28f;
            const float top = 158f;
            const float gap = 10f;
            const float width = 144.25f;
            const float height = 44f;

            for (int i = 0; i < ThemeRectangleStyles.Length; ++i)
            {
                float x = left + i * (width + gap);
                Now.Button(new NowRect(x, top, width, height), ThemeRectangleStyles[i].ToString())
                    .SetId(new NowId(100 + i))
                    .SetStyle(ThemeRectangleStyles[i])
                    .Draw();
            }
        }

        static void DrawThemePalette(NowThemeAsset theme)
        {
            const float left = 28f;
            const float top = 258f;
            const float gapX = 8f;
            const float gapY = 8f;
            const float width = 128.88f;
            const float height = 58f;
            const int columns = 9;

            for (int i = 0; i < ThemeColorTokens.Length; ++i)
            {
                int column = i % columns;
                int row = i / columns;
                var swatch = new NowRect(
                    left + column * (width + gapX),
                    top + row * (height + gapY),
                    width,
                    height);
                Color color = theme.GetColor(ThemeColorTokens[i], Color.magenta);

                Now.Rectangle(swatch)
                    .SetColor(color)
                    .SetRadius(6f)
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(0f, 0f, 0f, 0.16f))
                    .Draw();
                Now.Text(swatch.Inset(7f, 7f, 7f, 7f))
                    .SetFontSize(10f)
                    .SetBold()
                    .SetColor(ReadableSwatchText(color))
                    .Draw($"{ThemeColorTokens[i]}\n#{ColorUtility.ToHtmlStringRGBA(color)}");
            }
        }

        static void DrawThemeTextStyles(NowThemeAsset theme)
        {
            var panel = new NowRect(28f, 500f, 1224f, 136f);
            DrawThemePanelBackground(theme, panel);

            const int columns = 5;
            float cellWidth = (panel.width - 28f) / columns;
            float cellHeight = (panel.height - 16f) / 2f;

            for (int i = 0; i < ThemeTextStyles.Length; ++i)
            {
                int column = i % columns;
                int row = i / columns;
                var cell = new NowRect(
                    panel.x + 14f + column * cellWidth,
                    panel.y + 8f + row * cellHeight,
                    cellWidth - 10f,
                    cellHeight - 4f);

                Now.Text(new NowRect(cell.x, cell.y, cell.width, 14f))
                    .SetFontSize(10f)
                    .SetBold()
                    .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
                    .Draw(ThemeTextStyles[i].ToString());
                var sampleRect = new NowRect(cell.x, cell.y + 17f, cell.width, cell.height - 17f);
                if (ThemeTextStyles[i] == NowTextStyle.Button)
                {
                    var buttonSample = new NowRect(sampleRect.x, sampleRect.y, 126f, 40f);
                    theme.Rectangle(buttonSample, NowRectangleStyle.Accent).SetRadius(8f).Draw();
                    theme.Text(buttonSample.Inset(10f, 6f, 10f, 6f), ThemeTextStyles[i]).Draw("Aa 123");
                }
                else
                {
                    theme.Text(sampleRect, ThemeTextStyles[i]).Draw("Aa 123");
                }
            }
        }

        static void DrawThemeControls(NowThemeAsset theme)
        {
            var togglesPanel = new NowRect(28f, 688f, 270f, 244f);
            var statesPanel = new NowRect(310f, 688f, 314f, 244f);
            var fieldsPanel = new NowRect(636f, 688f, 288f, 244f);
            var popupPanel = new NowRect(936f, 688f, 316f, 244f);
            DrawThemePanelBackground(theme, togglesPanel);
            DrawThemePanelBackground(theme, statesPanel);
            DrawThemePanelBackground(theme, fieldsPanel);
            DrawThemePanelBackground(theme, popupPanel);

            DrawThemePanelLabel(theme, togglesPanel, "Core controls");
            DrawThemePanelLabel(theme, statesPanel, "Shared state roles");
            DrawThemePanelLabel(theme, fieldsPanel, "Fields and activity");
            DrawThemePanelLabel(theme, popupPanel, "Popup renderer");

            bool checkedValue = true;
            bool uncheckedValue = false;
            float sliderValue = 0.68f;
            string textValue = "NowUI";
            int dropdownValue = 2;

            Now.Checkbox(new NowRect(44f, 730f, 112f, 30f), "Checked")
                .SetId(new NowId(200))
                .Draw(ref checkedValue);
            Now.Checkbox(new NowRect(164f, 730f, 118f, 30f), "Unchecked")
                .SetId(new NowId(201))
                .Draw(ref uncheckedValue);
            Now.Radio(new NowRect(44f, 768f, 112f, 30f), "Selected", true)
                .SetId(new NowId(202))
                .Draw();
            Now.Radio(new NowRect(164f, 768f, 118f, 30f), "Unselected", false)
                .SetId(new NowId(203))
                .Draw();
            DrawThemeSwitchSample(theme, new NowRect(44f, 806f, 112f, 32f), "Off", false, hovered: true, held: false);
            DrawThemeSwitchSample(theme, new NowRect(164f, 806f, 118f, 32f), "On", true, hovered: true, held: true);
            Now.Badge(new NowRect(44f, 852f, 72f, 28f), "Accent")
                .SetStyle(NowRectangleStyle.Accent)
                .Draw();
            Now.Badge(new NowRect(126f, 852f, 88f, 28f), "Danger")
                .SetStyle(NowRectangleStyle.Danger)
                .Draw();

            DrawThemeSharedStates(theme, statesPanel);

            Now.TextField(new NowRect(652f, 730f, 256f, 44f), "theme-review-text")
                .SetPlaceholder("Name")
                .Draw(ref textValue);
            Now.Dropdown(new NowRect(652f, 786f, 256f, 40f), "theme-review-dropdown", QualityOptions)
                .Draw(ref dropdownValue);
            Now.Slider(new NowRect(652f, 846f, 164f, 32f), 0f, 1f)
                .SetId(new NowId(205))
                .Draw(ref sliderValue);
            Now.ProgressBar(new NowRect(828f, 857f, 80f, 10f), sliderValue).Draw();

            var popup = new NowRect(952f, 728f, 284f, 188f);
            theme.controlRenderer.DrawPopupBackground(theme, popup, menu: false);
            float itemHeight = 42f;
            theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                theme,
                new NowRect(popup.x + 8f, popup.y + 9f, popup.width - 16f, itemHeight),
                "Normal option",
                selected: false,
                interaction: default));
            theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                theme,
                new NowRect(popup.x + 8f, popup.y + 9f + itemHeight, popup.width - 16f, itemHeight),
                "Hovered option",
                selected: false,
                interaction: ThemeReviewInteraction(popup, held: false)));
            theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                theme,
                new NowRect(popup.x + 8f, popup.y + 9f + itemHeight * 2f, popup.width - 16f, itemHeight),
                "Selected option",
                selected: true,
                interaction: default));
            theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                theme,
                new NowRect(popup.x + 8f, popup.y + 9f + itemHeight * 3f, popup.width - 16f, itemHeight),
                "Open commands",
                "Shortcut  Ctrl+K",
                selected: false,
                interaction: default));
        }

        static void DrawThemePanelBackground(NowThemeAsset theme, NowRect panel)
        {
            // Material filled cards use a tonal container rather than adding an
            // outline to every surface. Muted is NowUI's closest container role.
            NowRectangleStyle style = theme.controlRenderer is NowMaterialControlRenderer
                ? NowRectangleStyle.Muted
                : NowRectangleStyle.Surface;
            theme.Rectangle(panel, style).SetRadius(10f).Draw();
        }

        static void DrawThemeSwitchSample(
            NowThemeAsset theme,
            NowRect rect,
            string label,
            bool value,
            bool hovered,
            bool held)
        {
            var renderer = theme.controlRenderer;
            var interaction = hovered ? ThemeReviewInteraction(rect, held) : default;
            var glyphRect = renderer.SwitchGlyphRect(theme, rect);
            renderer.DrawSwitch(new NowSwitchRenderContext(
                theme,
                rect,
                glyphRect,
                value,
                value ? 1f : 0f,
                interaction,
                focused: false,
                hoverT: hovered ? 1f : 0f));
            NowControls.DrawLeftLabel(theme, renderer.SwitchContentRect(theme, rect), label, NowTextStyle.Body);
        }

        static void DrawThemeSharedStates(NowThemeAsset theme, NowRect panel)
        {
            var renderer = theme.controlRenderer;
            const float labelWidth = 44f;
            float labelX = panel.x + 16f;
            float sampleX = labelX + labelWidth;
            float sampleWidth = panel.xMax - 16f - sampleX;

            float y = panel.y + 42f;
            DrawThemeStateLabel(theme, new NowRect(labelX, y, labelWidth, 30f), "Chip");
            const float chipGap = 4f;
            float chipWidth = (sampleWidth - chipGap * 2f) / 3f;
            for (int i = 0; i < 3; ++i)
            {
                var chip = new NowRect(sampleX + i * (chipWidth + chipGap), y, chipWidth, 30f);
                bool hovered = i == 1;
                bool selected = i == 2;
                renderer.DrawChip(new NowChipRenderContext(
                    theme,
                    chip,
                    i == 0 ? "Idle" : hovered ? "Hover" : "Selected",
                    selected,
                    removable: false,
                    removeRect: default,
                    removeHovered: false,
                    textStyle: NowTextStyle.Label,
                    interaction: hovered ? ThemeReviewInteraction(chip, held: false) : default,
                    focused: false,
                    hoverT: hovered ? 1f : 0f));
            }

            y += 38f;
            DrawThemeStateLabel(theme, new NowRect(labelX, y, labelWidth, 32f), "Tab");
            renderer.DrawTabBarBackground(theme, new NowRect(sampleX, y, sampleWidth, 32f));
            float tabWidth = (sampleWidth - chipGap * 2f) / 3f;
            for (int i = 0; i < 3; ++i)
            {
                var tab = new NowRect(sampleX + i * (tabWidth + chipGap), y, tabWidth, 32f);
                bool pressed = i == 1;
                bool selected = i == 2;
                renderer.DrawTab(new NowTabRenderContext(
                    theme,
                    tab,
                    i == 0 ? "Hover" : pressed ? "Pressed" : "Selected",
                    selected,
                    selected ? 1f : 0f,
                    selected ? default : ThemeReviewInteraction(tab, pressed),
                    focused: false,
                    hoverT: selected ? 0f : 1f));
            }

            y += 40f;
            DrawThemeStateLabel(theme, new NowRect(labelX, y, labelWidth, 30f), "Tree");
            float treeWidth = (sampleWidth - 6f) * 0.5f;
            for (int i = 0; i < 2; ++i)
            {
                var row = new NowRect(sampleX + i * (treeWidth + 6f), y, treeWidth, 30f);
                var disclosure = new NowRect(row.x + 4f, row.y + 5f, 18f, 20f);
                bool selected = i == 1;
                renderer.DrawTreeRow(new NowTreeRowRenderContext(
                    theme,
                    row,
                    selected ? "Selected" : "Hover",
                    depth: 0,
                    hasChildren: true,
                    expanded: selected,
                    selected: selected,
                    disclosureRect: disclosure,
                    interaction: selected ? default : ThemeReviewInteraction(row, held: false),
                    focused: false,
                    hoverT: selected ? 0f : 1f));
            }

            y += 38f;
            DrawThemeStateLabel(theme, new NowRect(labelX, y, labelWidth, 32f), "Spin");
            var spinner = new NowRect(sampleX, y, 78f, 32f);
            theme.Rectangle(spinner, NowRectangleStyle.Surface)
                .SetRadius(4f)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                .Draw();
            Now.Text(spinner.Inset(8f, 6f, 28f, 4f))
                .SetStyle(theme, NowTextStyle.Label)
                .Draw("12");
            var up = new NowRect(spinner.xMax - 24f, spinner.y, 24f, 16f);
            var down = new NowRect(spinner.xMax - 24f, spinner.y + 16f, 24f, 16f);
            renderer.DrawSpinnerButtons(new NowSpinnerRenderContext(
                theme,
                spinner,
                up,
                down,
                upHovered: true,
                upHeld: false,
                downHovered: true,
                downHeld: true,
                focused: false));

            DrawThemeStateLabel(theme, new NowRect(sampleX + 86f, y, 34f, 32f), "Day");
            float dayX = sampleX + 120f;
            const float dayGap = 2f;
            float dayWidth = (sampleWidth - 120f - dayGap * 3f) / 4f;
            for (int i = 0; i < 4; ++i)
            {
                var day = new NowRect(dayX + i * (dayWidth + dayGap), y, dayWidth, 32f);
                bool selected = i >= 2;
                bool pressed = i == 1 || i == 3;
                var interaction = i == 2 ? default : ThemeReviewInteraction(day, pressed);
                renderer.DrawCalendarDay(new NowCalendarDayRenderContext(
                    theme,
                    day,
                    i == 0 ? "H" : i == 1 ? "P" : i == 2 ? "S" : "SP",
                    inMonth: true,
                    isToday: false,
                    selected: selected,
                    disabled: false,
                    interaction: interaction,
                    focused: false,
                    hoverT: i == 2 ? 0f : 1f));
            }
        }

        static void DrawThemeStateLabel(NowThemeAsset theme, NowRect rect, string label)
        {
            Now.Text(rect)
                .SetFontSize(9f)
                .SetBold()
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw(label);
        }

        static NowInteraction ThemeReviewInteraction(NowRect rect, bool held)
        {
            return new NowInteraction(
                id: default,
                rect: rect,
                button: NowPointerButton.Primary,
                hasPointer: true,
                pointerPosition: rect.center,
                pointerDelta: default,
                dragDelta: default,
                hovered: true,
                pressed: false,
                held: held,
                released: false,
                clicked: false,
                active: held,
                dragging: false,
                dragStarted: false,
                dragEnded: false,
                cancelled: false,
                dragCancelled: false);
        }

        static void DrawThemeSectionTitle(float x, float y, string title, string subtitle)
        {
            var theme = NowTheme.themeAsset;
            Now.Text(new NowRect(x, y, 240f, 24f))
                .SetFontSize(15f)
                .SetBold()
                .SetColor(theme.GetColor(NowColorToken.Text, Color.white))
                .Draw(title);
            Now.Text(new NowRect(x + 250f, y + 1f, 950f, 22f))
                .SetFontSize(12f)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw(subtitle);
        }

        static void DrawThemePanelLabel(NowThemeAsset theme, NowRect panel, string label)
        {
            Now.Text(new NowRect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 22f))
                .SetFontSize(12f)
                .SetBold()
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw(label);
        }

        static Color ReadableSwatchText(Color background)
        {
            Color page = NowTheme.themeAsset.GetColor(NowColorToken.Background, Color.white);
            Color composited = new Color(
                background.r * background.a + page.r * (1f - background.a),
                background.g * background.a + page.g * (1f - background.a),
                background.b * background.a + page.b * (1f - background.a),
                1f);
            var dark = new Color(0.04f, 0.04f, 0.04f, 1f);
            return ThemeReviewContrast(composited, dark) >= ThemeReviewContrast(composited, Color.white)
                ? dark
                : Color.white;
        }

        static float ThemeReviewContrast(Color a, Color b)
        {
            float lighter = Mathf.Max(ThemeReviewLuminance(a), ThemeReviewLuminance(b));
            float darker = Mathf.Min(ThemeReviewLuminance(a), ThemeReviewLuminance(b));
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        static float ThemeReviewLuminance(Color color)
        {
            return ThemeReviewLinear(color.r) * 0.2126f +
                ThemeReviewLinear(color.g) * 0.7152f +
                ThemeReviewLinear(color.b) * 0.0722f;
        }

        static float ThemeReviewLinear(float value)
        {
            return value <= 0.04045f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        static void DrawElevation(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Elevation", "Raised, overlay, and modal shadow presets over the themed background.");

            var theme = NowControls.themeAsset;
            var levels = new[] { NowElevationToken.Raised, NowElevationToken.Overlay, NowElevationToken.Modal };
            float cardWidth = 200f;
            float cardHeight = 140f;
            float gap = 48f;
            float x = rect.x + 60f;
            float y = rect.y + 150f;

            for (int i = 0; i < levels.Length; ++i)
            {
                var cardRect = new NowRect(x + i * (cardWidth + gap), y, cardWidth, cardHeight);
                theme.Rectangle(cardRect, NowRectangleStyle.Elevated).DrawElevated(theme, levels[i]);
                Now.Text(cardRect.Inset(16f, 16f, 16f, 16f))
                    .SetFontSize(15f)
                    .SetBold()
                    .SetColor(theme.GetColor(NowColorToken.Text))
                    .Draw(levels[i].ToString());
            }
        }

        /// <summary>
        /// A context menu taller than the view: it clamps, scrolls to the middle,
        /// and shows both hover scroll strips so the strip corners are pinned
        /// against the popup's rounded silhouette.
        /// </summary>
        static void DrawContextMenu(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Context Menu", "Clamped tall menu, scrolled, with edge scroll strips.");

            NowResolvedId menuId = NowControls.GetControlId("harness-context-menu");

            if (!NowContextMenu.isOpen)
                NowContextMenu.Open(menuId, new Vector2(64f, 48f));

            if (NowContextMenu.Begin(menuId))
            {
                NowContextMenu.Label("Harness Menu");
                NowContextMenu.Separator();

                for (int i = 0; i < 40; ++i)
                    NowContextMenu.Item($"Overflow Option {i + 1}", id: i + 1);

                NowContextMenu.End();
                NowControlState.Get<float>(menuId, "ctx-scroll") = 180f;
            }
        }

        static void DrawContextSubmenus(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Context Submenus", "Sibling submenu hover state with the active child drawn beside the root.");

            NowResolvedId menuId = NowControls.GetControlId("harness-context-submenus");
            var anchor = new Vector2(64f, 118f);

            if (!NowContextMenu.isOpen)
                NowContextMenu.Open(menuId, anchor);

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.BeginSubmenu("Arrange", id: "arrange"))
                {
                    NowContextMenu.Item("Bring Forward", id: "bring-forward");
                    NowContextMenu.Item("Send Backward", id: "send-backward");
                    NowContextMenu.Separator();
                    NowContextMenu.Item("Align Left", id: "align-left");
                    NowContextMenu.Item("Align Center", id: "align-center");
                    NowContextMenu.EndSubmenu();
                }

                if (NowContextMenu.BeginSubmenu("Export", id: "export"))
                {
                    NowContextMenu.Item("PNG", id: "png");
                    NowContextMenu.Item("SVG", id: "svg");
                    NowContextMenu.Item("Copy JSON", id: "copy-json");
                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.Separator();
                NowContextMenu.Item("Duplicate", id: "duplicate");
                NowContextMenu.Item("Rename", id: "rename");
                NowContextMenu.Item("Delete", id: "delete", enabled: false);
                NowContextMenu.End();
            }
        }

        static void DrawContextEdgeSubmenu(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Edge Submenu", "Right-edge submenu clamping in a constrained surface.");

            NowResolvedId menuId = NowControls.GetControlId("harness-context-edge-submenu");
            var anchor = new Vector2(320f, 116f);

            if (!NowContextMenu.isOpen)
                NowContextMenu.Open(menuId, anchor);

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.BeginSubmenu("More Actions", id: "more-actions"))
                {
                    NowContextMenu.Item("Open Details", id: "open-details");
                    NowContextMenu.Item("Pin", id: "pin");
                    NowContextMenu.Item("Duplicate", id: "duplicate");
                    NowContextMenu.Separator();
                    NowContextMenu.Item("Move Up", id: "move-up");
                    NowContextMenu.Item("Move Down", id: "move-down");
                    NowContextMenu.Item("Archive", id: "archive");
                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.Item("Edit", id: "edit");
                NowContextMenu.Item("Copy", id: "copy");
                NowContextMenu.Item("Delete", id: "delete", enabled: false);
                NowContextMenu.End();
            }
        }

        static void DrawContextPingPongSubmenus(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Ping Pong Submenus", "Submenus flip left, then back right, when space runs out.");

            NowResolvedId menuId = NowControls.GetControlId("harness-context-ping-pong-submenus");
            var anchor = new Vector2(250f, 116f);

            if (!NowContextMenu.isOpen)
                NowContextMenu.Open(menuId, anchor);

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.BeginSubmenu("Level 1", id: "level-1"))
                {
                    NowContextMenu.Item("Level 1 Action", id: "level-1-action");

                    if (NowContextMenu.BeginSubmenu("Level 2", id: "level-2"))
                    {
                        NowContextMenu.Item("Deep Action", id: "deep-action");
                        NowContextMenu.Item("Deep Settings", id: "deep-settings");
                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.Separator();
                    NowContextMenu.Item("Inspect Chain", id: "inspect-chain");
                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.Item("Root Action", id: "root-action");
                NowContextMenu.Item("Rename Chain", id: "rename-chain");
                NowContextMenu.Item("Delete Chain", id: "delete-chain", enabled: false);
                NowContextMenu.End();
            }
        }

        static void DrawWorldContextPingPongSubmenus(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "World Ping Pong Submenus", "World-space camera fitting flips left, then back right.");

            NowResolvedId menuId = NowControls.GetControlId("harness-world-context-ping-pong-submenus");
            var anchor = new Vector2(250f, 116f);

            if (!NowContextMenu.isOpen)
                NowContextMenu.Open(menuId, anchor);

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.BeginSubmenu("Level 1", id: "level-1"))
                {
                    NowContextMenu.Item("Level 1 Action", id: "level-1-action");

                    if (NowContextMenu.BeginSubmenu("Level 2", id: "level-2"))
                    {
                        NowContextMenu.Item("Deep Action", id: "deep-action");
                        NowContextMenu.Item("Deep Settings", id: "deep-settings");
                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.Separator();
                    NowContextMenu.Item("Inspect Chain", id: "inspect-chain");
                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.Item("Root Action", id: "root-action");
                NowContextMenu.Item("Rename Chain", id: "rename-chain");
                NowContextMenu.Item("Delete Chain", id: "delete-chain", enabled: false);
                NowContextMenu.End();
            }
        }

        static void DrawWorldBackSurface(NowRect rect)
        {
            DrawWorldSurfacePanel(
                rect,
                "Back Surface",
                "Visible area remains interactive",
                new Color(0.1f, 0.19f, 0.29f, 0.94f),
                new Color(0.32f, 0.62f, 0.95f, 0.9f),
                new NowRect(22f, 96f, 96f, 42f),
                "Back");
        }

        static void DrawWorldFrontSurface(NowRect rect)
        {
            DrawWorldSurfacePanel(
                rect,
                "Front Surface",
                "Hover at overlap is owned here",
                new Color(0.24f, 0.16f, 0.13f, 0.96f),
                new Color(1f, 0.64f, 0.28f, 0.96f),
                new NowRect(46f, 62f, 160f, 42f),
                "Front Button");
        }

        static void DrawWorldSurfacePanel(
            NowRect rect,
            string title,
            string subtitle,
            Color fill,
            Color outline,
            NowRect buttonRect,
            string buttonText)
        {
            Now.Rectangle(rect)
                .SetColor(fill)
                .SetRadius(14f)
                .SetOutline(2f)
                .SetOutlineColor(outline)
                .Draw();

            Now.Text(new NowRect(18f, 14f, rect.width - 36f, 26f))
                .SetFontSize(18f)
                .SetBold()
                .SetColor(Color.white)
                .Draw(title);
            Now.Text(new NowRect(18f, rect.height - 34f, rect.width - 36f, 22f))
                .SetFontSize(12f)
                .SetColor(new Color(1f, 1f, 1f, 0.72f))
                .Draw(subtitle);

            Now.Button(buttonRect, buttonText).Draw();
        }

        static void DrawControls(NowRect rect)
        {
            DrawSurface(rect);

            bool checkedValue = true;
            bool otherValue = false;
            int quality = 2;
            float volume = 0.68f;
            float temperature = 0.42f;
            string name = "NowUI";
            string notes = "Screenshot harness\nText field replay target";

            HeaderBlock(rect, "Controls Gallery", "Buttons, toggles, fields, dropdowns, sliders, and scroll views.");

            var left = new NowRect(28f, 116f, 420f, 378f);
            var right = new NowRect(500f, 116f, 420f, 378f);
            Panel(left);
            Panel(right);

            using (NowLayout.Area(left.Inset(18f), spacing: 12f))
            {
                Section("Actions");
                using (NowLayout.HorizontalScope(spacing: 8f))
                {
                    NowLayout.Button("Primary").SetWidth(104f).Draw();
                    NowLayout.Button("Outline").SetStyle(NowRectangleStyle.Outline).SetWidth(104f).Draw();
                    NowLayout.Button("Muted").SetStyle(NowRectangleStyle.Muted).SetWidth(104f).Draw();
                }

                Section("Toggles");
                NowLayout.Checkbox("Enable glass").Draw(ref checkedValue);
                NowLayout.Checkbox("Use compact rows").Draw(ref otherValue);

                using (NowLayout.HorizontalScope(spacing: 8f))
                {
                    NowLayout.Radio("Low", quality == 0).Draw();
                    NowLayout.Radio("Medium", quality == 1).Draw();
                    NowLayout.Radio("High", quality == 2).Draw();
                }

                Section("Sliders");
                SliderRow("Volume", ref volume, 0f, 1f);
                SliderRow("Temperature", ref temperature, 0f, 1f);
            }

            using (NowLayout.Area(right.Inset(18f), spacing: 12f))
            {
                Section("Inputs");
                NowLayout.TextField("name").SetPlaceholder("Name").SetStretchWidth().Draw(ref name);
                NowLayout.Dropdown("quality", QualityOptions).SetStretchWidth().Draw(ref quality);
                NowLayout.TextArea("notes").SetLines(3, 6).SetStretchWidth().Draw(ref notes);

                Section("Activity");
                using (NowLayout.ScrollView("activity").SetHeight(56f).Begin())
                {
                    for (int i = 0; i < 12; ++i)
                        NowLayout.Label($"Frame event {i:00}: stable harness row").SetFontSize(13f).Draw();
                }
            }
        }

        static void DrawTextLayout(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Text and Layout", "Typography, wrapping, proportional groups, and repeated rows.");

            var left = new NowRect(28f, 116f, 440f, 378f);
            var right = new NowRect(500f, 116f, 420f, 378f);
            Panel(left);
            Panel(right);

            Now.Text(new NowRect(left.x + 18f, left.y + 18f, left.width - 36f, 46f))
                .SetFontSize(34f)
                .SetBold()
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Text, Color.white))
                .Draw("Large title");
            Now.Text(new NowRect(left.x + 18f, left.y + 72f, left.width - 36f, 70f))
                .SetFontSize(16f)
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Text, Color.white))
                .Draw("Fixed panels, stable rows, and measured text.");

            var table = new NowRect(left.x + 18f, left.y + 158f, left.width - 36f, 178f);
            NowTheme.themeAsset.Rectangle(table, NowRectangleStyle.Muted).SetRadius(8f).Draw();

            for (int i = 0; i < 5; ++i)
            {
                float y = table.y + 18f + i * 30f;
                Now.Text(new NowRect(table.x + 16f, y, 60f, 22f)).SetFontSize(13f).Draw($"Row {i + 1}");
                Now.Text(new NowRect(table.x + 86f, y, table.width - 104f, 22f)).SetFontSize(13f)
                    .Draw("Stable text beside a fixed label.");
            }

            Now.Text(new NowRect(right.x + 18f, right.y + 18f, right.width - 36f, 24f))
                .SetFontSize(14f)
                .SetBold()
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Accent, Color.cyan))
                .Draw("Cards");

            for (int i = 0; i < 4; ++i)
            {
                var card = new NowRect(right.x + 18f, right.y + 58f + i * 76f, right.width - 36f, 62f);
                NowTheme.themeAsset.Rectangle(card, i == 1 ? NowRectangleStyle.Accent : NowRectangleStyle.Muted).SetRadius(8f).Draw();
                var textColor = i == 1
                    ? NowTheme.themeAsset.GetColor(NowColorToken.AccentText, Color.white)
                    : NowTheme.themeAsset.GetColor(NowColorToken.Text, Color.white);
                Now.Text(card.Inset(16f, 10f)).SetFontSize(15f).SetColor(textColor).Draw($"Stable layout item {i + 1}");
            }
        }

        static void DrawGlass(NowRect rect)
        {
            for (int y = 0; y < rect.height; y += 36)
            {
                for (int x = 0; x < rect.width; x += 36)
                {
                    var tint = ((x + y) / 36) % 2 == 0
                        ? new Color(0.95f, 0.22f, 0.28f, 1f)
                        : new Color(0.12f, 0.56f, 0.96f, 1f);
                    Now.Rectangle(new NowRect(x, y, 36f, 36f)).SetColor(tint).Draw();
                }
            }

            Now.Glass(rect.Inset(96f, 62f))
                .SetBlurRadius(22f)
                .SetTint(new Color(1f, 1f, 1f, 0.15f))
                .SetVibrancy(1f, 1f)
                .SetRadius(18f)
                .Draw();

            Now.Text(rect.Inset(126f, 112f))
                .SetFontSize(30f)
                .SetColor(Color.white)
                .Draw("Glass Backdrop");
        }

        static void DrawShaderVariants(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Shader Variants", "Rectangle and glass edge cases with zero and explicit outlines.");

            var left = new NowRect(28f, 116f, 380f, 262f);
            var right = new NowRect(432f, 116f, 380f, 262f);
            Panel(left);
            Panel(right);

            Now.Text(new NowRect(left.x + 18f, left.y + 18f, left.width - 36f, 24f))
                .SetFontSize(15f)
                .SetBold()
                .Draw("Rectangles");
            Now.Text(new NowRect(right.x + 18f, right.y + 18f, right.width - 36f, 24f))
                .SetFontSize(15f)
                .SetBold()
                .Draw("Glass");

            var rectTiles = new[]
            {
                new NowRect(left.x + 18f, left.y + 58f, 158f, 78f),
                new NowRect(left.x + 204f, left.y + 58f, 158f, 78f),
                new NowRect(left.x + 18f, left.y + 158f, 158f, 78f),
                new NowRect(left.x + 204f, left.y + 158f, 158f, 78f)
            };

            DrawVariantTile(rectTiles[0], "fill");
            Now.Rectangle(rectTiles[0].Inset(26f, 16f))
                .SetColor(new Color(0.92f, 0.26f, 0.32f, 0.78f))
                .SetRadius(16f)
                .Draw();

            DrawVariantTile(rectTiles[1], "zero outline");
            Now.Rectangle(rectTiles[1].Inset(26f, 16f))
                .SetColor(new Color(0.2f, 0.66f, 0.95f, 0.45f))
                .SetRadius(16f)
                .SetOutline(0f)
                .SetOutlineColor(Color.black)
                .Draw();

            DrawVariantTile(rectTiles[2], "thin outline");
            Now.Rectangle(rectTiles[2].Inset(26f, 16f))
                .SetColor(new Color(0.24f, 0.82f, 0.58f, 0.34f))
                .SetRadius(16f)
                .SetOutline(1f)
                .SetOutlineColor(new Color(1f, 1f, 1f, 0.86f))
                .Draw();

            DrawVariantTile(rectTiles[3], "outline only");
            Now.Rectangle(rectTiles[3].Inset(26f, 16f))
                .SetColor(new Color(1f, 1f, 1f, 0f))
                .SetRadius(16f)
                .SetOutline(4f)
                .SetOutlineColor(new Color(1f, 0.74f, 0.24f, 1f))
                .Draw();

            var glassTiles = new[]
            {
                new NowRect(right.x + 18f, right.y + 58f, 158f, 78f),
                new NowRect(right.x + 204f, right.y + 58f, 158f, 78f),
                new NowRect(right.x + 18f, right.y + 158f, 158f, 78f),
                new NowRect(right.x + 204f, right.y + 158f, 158f, 78f)
            };

            DrawGlassVariant(glassTiles[0], "zero outline", 0f, default, 0.15f);
            DrawGlassVariant(glassTiles[1], "thin outline", 1f, new Color(1f, 1f, 1f, 0.58f), 0.18f);
            DrawGlassVariant(glassTiles[2], "thick outline", 4f, new Color(1f, 0.74f, 0.24f, 0.72f), 0.16f);
            DrawGlassVariant(glassTiles[3], "clear tint", 2f, new Color(0.45f, 0.88f, 1f, 0.72f), 0f);
        }

        static void DrawQuickStartOverlay(NowRect rect)
        {
            DrawSurface(rect);
            HeaderBlock(rect, "Quick Start Overlay", "Sample layout with gradients, masks, SDF cutout, text, and a button.");

            using var area = NowLayout.Area(rect.TakeBottom(310f).Centered(width: 180f, height: 240f));

            DrawCheckerboard(area.rect, 6f);

            NowLayout.Label("NowUI", 28f).Draw();

            var gradientRect = NowLayout.ReserveRect(width: 180f, height: 36f);
            Now.Gradient(
                    gradientRect,
                    new Color(0.12f, 0.5f, 1f),
                    new Color(0.72f, 0.22f, 0.95f))
                .SetLinear(110f)
                .SetRadius(10f)
                .Draw();

            var maskRect = NowLayout.ReserveRect(width: 180f, height: 44f);
            var softMask = NowMaskShape.Capsule(maskRect).SetFeather(1f);
            using (Now.Mask(softMask))
            {
                Now.Gradient(
                        new NowRect(maskRect.x - 24f, maskRect.y, maskRect.width + 48f, maskRect.height),
                        new Color(0.1f, 0.72f, 0.62f),
                        new Color(0.08f, 0.28f, 0.55f))
                    .SetLinear(90f)
                    .Draw();

                Now.Text(new NowRect(maskRect.x + 14f, maskRect.y + 8f, maskRect.width - 28f, 28f))
                    .SetFontSize(18f)
                    .SetColor(Color.white)
                    .Draw("Soft capsule mask");
            }

            var sdfMaskRect = NowLayout.ReserveRect(width: 180f, height: 44f);
            var sdfMask = NowSdf.Scene(sdfMaskRect, 4101)
                .SetMaskResolutionScale(0.5f)
                .SetFeather(1f)
                .Circle(new Vector2(30f, 22f), 21f)
                .SmoothUnion(10f)
                .RoundedBox(new NowRect(28f, 2f, 146f, 40f), 18f)
                .Subtract()
                .Circle(new Vector2(154f, 22f), 8f);

            using (sdfMask.BeginMask())
            {
                Now.Gradient(
                        sdfMaskRect,
                        new Color(0.96f, 0.42f, 0.18f),
                        new Color(0.68f, 0.16f, 0.76f))
                    .SetLinear(90f)
                    .Draw();

                Now.Text(new NowRect(sdfMaskRect.x + 14f, sdfMaskRect.y + 8f, sdfMaskRect.width - 28f, 28f))
                    .SetFontSize(18f)
                    .SetColor(Color.white)
                    .Draw("SDF cutout mask");
            }

            var buttonRect = NowLayout.ReserveRect(width: 180f, height: 44f);
            bool clicked = Now.Button(buttonRect, "Sample Button").Draw();

            NowLayout.Label(clicked ? "Clicked" : "Ready", 16f).Draw();
        }

        static void DrawSdfMaskGlowClip(NowRect rect)
        {
            var background = new Color(0.018f, 0.024f, 0.045f, 1f);
            var grid = new Color(0.18f, 0.42f, 0.62f, 0.10f);
            var cyan = new Color(0.10f, 0.88f, 1f, 1f);

            Now.Rectangle(rect).SetColor(background).Draw();

            for (float x = rect.x; x < rect.xMax; x += 32f)
                Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(grid).Draw();

            for (float y = rect.y; y < rect.yMax; y += 32f)
                Now.Rectangle(new NowRect(rect.x, y, rect.width, 1f)).SetColor(grid).Draw();

            Now.Text(new NowRect(28f, 20f, rect.width - 56f, 30f))
                .SetFontSize(22f)
                .SetBold()
                .SetColor(Color.white)
                .Draw("SDF parent mask · glowing progress");
            Now.Text(new NowRect(28f, 52f, rect.width - 56f, 22f))
                .SetFontSize(13f)
                .SetColor(new Color(0.66f, 0.76f, 0.88f, 1f))
                .Draw("The oversized cyan halo must stop exactly at the circle boundary.");

            var circleRect = new NowRect(64f, 88f, 512f, 512f);
            var localCenter = circleRect.size * 0.5f;
            const float circleRadius = 244f;
            const float progress = 0.92f;
            var bar = new NowRect(circleRect.x - 28f, circleRect.y + 312f, circleRect.width + 56f, 44f);
            var fill = new NowRect(bar.x, bar.y, bar.width * progress, bar.height);

            using (NowSdf.Scene(circleRect, "visual-sdf-glow-parent")
                .SetColor(Color.white)
                .SetFeather(1f)
                .Circle(localCenter, circleRadius)
                .BeginMask())
            {
                Now.Gradient(
                        circleRect,
                        new Color(0.025f, 0.075f, 0.14f, 1f),
                        new Color(0.13f, 0.035f, 0.20f, 1f))
                    .SetRadial(circleRect.center, circleRadius * 1.15f)
                    .Draw();

                for (float x = circleRect.x - 48f; x < circleRect.xMax + 48f; x += 28f)
                {
                    Now.Line(
                            new Vector2(x, circleRect.y - 12f),
                            new Vector2(x + 170f, circleRect.yMax + 12f))
                        .SetWidth(1f)
                        .SetColor(new Color(0.32f, 0.78f, 1f, 0.10f))
                        .Draw();
                }

                Now.Text(new NowRect(circleRect.x + 120f, circleRect.y + 98f, 280f, 80f))
                    .SetFontSize(62f)
                    .SetBold()
                    .SetColor(Color.white)
                    .Draw("92%");
                Now.Text(new NowRect(circleRect.x + 164f, circleRect.y + 176f, 240f, 24f))
                    .SetFontSize(14f)
                    .SetColor(new Color(0.68f, 0.82f, 0.94f, 1f))
                    .Draw("GPU mask coverage");

                // This halo is intentionally much wider than the progress bar.
                // Its full-scene SDF quad can reach beyond the parent circle, so
                // the clean circular cutoff is produced by BeginMask(), not by
                // the child's geometry bounds.
                NowSdf.Scene(rect, "visual-sdf-glowing-progress")
                    .SetColor(cyan)
                    .SetGlow(72f, new Color(0.02f, 0.78f, 1f, 0.94f), 1.25f)
                    .SetOutline(2f, new Color(0.72f, 0.98f, 1f, 1f), 0f)
                    .RoundedBox(fill, bar.height * 0.5f)
                    .Draw();

                Now.ProgressBar(bar, progress)
                    .SetId("visual-sdf-glow-progress-control")
                    .Draw();

                Now.Text(new NowRect(circleRect.x + 104f, circleRect.y + 392f, 330f, 42f))
                    .SetFontSize(13f)
                    .SetColor(new Color(0.72f, 0.84f, 0.96f, 1f))
                    .Draw("Glow, control, text, gradient, and lines\nshare the same soft circular clip.");
            }

            Now.Circle(circleRect.center, circleRadius)
                .SetColor(Color.clear)
                .SetOutline(2f, new Color(0.22f, 0.86f, 1f, 0.82f))
                .Draw();
        }

        static void DrawSdfMaskGallery(NowRect rect)
        {
            Now.Rectangle(rect).SetColor(new Color(0.022f, 0.028f, 0.048f, 1f)).Draw();
            Now.Text(new NowRect(28f, 20f, rect.width - 56f, 30f))
                .SetFontSize(22f)
                .SetBold()
                .SetColor(Color.white)
                .Draw("SDF mask visual regression gallery");
            Now.Text(new NowRect(28f, 52f, rect.width - 56f, 22f))
                .SetFontSize(13f)
                .SetColor(new Color(0.66f, 0.76f, 0.88f, 1f))
                .Draw("Boolean cutouts · SDF text masks · crisp and feathered coverage");

            var booleanCard = new NowRect(28f, 96f, 284f, 388f);
            var textCard = new NowRect(338f, 96f, 284f, 388f);
            var edgeCard = new NowRect(648f, 96f, 284f, 388f);
            DrawSdfGalleryCard(booleanCard, "BOOLEAN + MIXED CONTENT");
            DrawSdfGalleryCard(textCard, "TEXT-SHAPED MASK");
            DrawSdfGalleryCard(edgeCard, "EDGE COVERAGE");

            var ticket = new NowRect(booleanCard.x + 20f, booleanCard.y + 66f, booleanCard.width - 40f, 274f);
            var ticketShape = new NowRect(8f, 8f, ticket.width - 16f, ticket.height - 16f);

            using (NowSdf.Scene(ticket, "visual-sdf-boolean-ticket")
                .SetColor(Color.white)
                .SetFeather(1f)
                .RoundedBox(ticketShape, 22f)
                .Subtract()
                .Circle(new Vector2(8f, ticket.height * 0.5f), 14f)
                .Subtract()
                .Circle(new Vector2(ticket.width - 8f, ticket.height * 0.5f), 14f)
                .BeginMask())
            {
                Now.Gradient(
                        ticket,
                        new Color(1f, 0.28f, 0.12f, 1f),
                        new Color(0.72f, 0.08f, 0.82f, 1f))
                    .SetLinear(110f)
                    .Draw();
                Now.Circle(new Vector2(ticket.x + 54f, ticket.y + 62f), 28f)
                    .SetColor(new Color(1f, 1f, 1f, 0.22f))
                    .Draw();
                Now.Line(
                        new Vector2(ticket.x - 12f, ticket.center.y),
                        new Vector2(ticket.xMax + 12f, ticket.center.y))
                    .SetWidth(2f)
                    .SetDash(8f, 6f)
                    .SetColor(new Color(1f, 1f, 1f, 0.76f))
                    .Draw();
                Now.Text(new NowRect(ticket.x + 28f, ticket.y + 34f, ticket.width - 56f, 42f))
                    .SetFontSize(25f)
                    .SetBold()
                    .SetColor(Color.white)
                    .Draw("BOARDING");
                Now.Text(new NowRect(ticket.x + 28f, ticket.y + 178f, ticket.width - 56f, 52f))
                    .SetFontSize(14f)
                    .SetColor(new Color(1f, 1f, 1f, 0.92f))
                    .Draw("Gradient · circle · dashed line\n· ordinary text");
            }

            var wordRect = new NowRect(textCard.x + 22f, textCard.y + 90f, textCard.width - 44f, 126f);

            using (NowSdf.Scene(wordRect, "visual-sdf-word-mask")
                .SetColor(Color.white)
                .SetFeather(0.75f)
                .Text(new Vector2(8f, 18f), "MASK", 64f, NowFontStyle.Bold)
                .BeginMask())
            {
                Now.Gradient(
                        wordRect,
                        new Color(0.05f, 0.92f, 1f, 1f),
                        new Color(1f, 0.12f, 0.72f, 1f))
                    .SetConic(wordRect.center, 28f)
                    .SetSpread(NowGradientSpread.Repeat)
                    .SetRepetitions(2f)
                    .Draw();

                for (float y = wordRect.y; y < wordRect.yMax; y += 14f)
                {
                    Now.Line(new Vector2(wordRect.x, y), new Vector2(wordRect.xMax, y + 24f))
                        .SetWidth(2f)
                        .SetColor(new Color(1f, 1f, 1f, 0.48f))
                        .Draw();
                }
            }

            Now.Text(new NowRect(textCard.x + 30f, textCard.y + 246f, textCard.width - 60f, 84f))
                .SetFontSize(13f)
                .SetColor(new Color(0.74f, 0.84f, 0.96f, 1f))
                .Draw("SDF glyph coverage masks a conic\ngradient and crossing line primitives.");

            var crispRect = new NowRect(edgeCard.x + 24f, edgeCard.y + 68f, 112f, 112f);
            var softRect = new NowRect(edgeCard.x + 148f, edgeCard.y + 68f, 112f, 112f);
            DrawSdfCoverageSwatch(crispRect, 0f, "visual-sdf-crisp-edge");
            DrawSdfCoverageSwatch(softRect, 10f, "visual-sdf-soft-edge");
            Now.Text(new NowRect(crispRect.x + 24f, crispRect.yMax + 12f, 84f, 20f))
                .SetFontSize(12f)
                .SetColor(Color.white)
                .Draw("1px AA");
            Now.Text(new NowRect(softRect.x + 12f, softRect.yMax + 12f, 100f, 20f))
                .SetFontSize(12f)
                .SetColor(Color.white)
                .Draw("10px feather");

            var donutRect = new NowRect(edgeCard.x + 72f, edgeCard.y + 240f, 140f, 116f);
            using (NowSdf.Scene(donutRect, "visual-sdf-donut-edge")
                .SetColor(Color.white)
                .SetFeather(1.5f)
                .Circle(donutRect.size * 0.5f, 52f)
                .Subtract()
                .Circle(donutRect.size * 0.5f, 25f)
                .BeginMask())
            {
                Now.Gradient(
                        donutRect,
                        new Color(0.18f, 0.92f, 0.55f, 1f),
                        new Color(0.10f, 0.42f, 1f, 1f))
                    .SetLinear(35f)
                    .Draw();
            }
        }

        static void DrawSdfPlanarPrimitives(NowRect rect)
        {
            Now.Rectangle(rect).SetColor(new Color(0.018f, 0.026f, 0.050f, 1f)).Draw();
            Now.Text(new NowRect(26f, 18f, rect.width - 52f, 30f))
                .SetFontSize(23f)
                .SetBold()
                .SetColor(Color.white)
                .Draw("SDF planar primitives");
            Now.Text(new NowRect(26f, 50f, rect.width - 52f, 21f))
                .SetFontSize(13f)
                .SetColor(new Color(0.65f, 0.76f, 0.89f, 1f))
                .Draw("Chamfered boxes, winding-independent triangles, and round-capped full-width lines.");

            const float cardWidth = 219f;
            const float cardGap = 12f;
            var chamferCard = new NowRect(24f, 92f, cardWidth, 270f);
            var triangleCard = new NowRect(chamferCard.xMax + cardGap, 92f, cardWidth, 270f);
            var lineCard = new NowRect(triangleCard.xMax + cardGap, 92f, cardWidth, 270f);
            var composedCard = new NowRect(lineCard.xMax + cardGap, 92f, cardWidth, 270f);

            DrawSdfPlanarCard(chamferCard, "CHAMFERED BOX", "22.5° clockwise · RotateNext");
            DrawSdfPlanarCard(triangleCard, "TRIANGLE", "Either winding · same field");
            DrawSdfPlanarCard(lineCard, "LINE", "28px full width · round caps");
            DrawSdfPlanarCard(composedCard, "COMPOSED", "8° cut · next line stays unrotated");

            var chamferScene = new NowRect(chamferCard.x + 14f, chamferCard.y + 50f, chamferCard.width - 28f, 146f);
            NowSdf.Scene(chamferScene, "visual-sdf-planar-chamfer")
                .SetColor(new Color(0.12f, 0.82f, 1f, 1f))
                .SetFeather(0.5f)
                .SetOutline(2f, new Color(0.72f, 0.94f, 1f, 0.72f), 0.5f)
                .RotateNext(22.5f)
                .ChamferedBox(new NowRect(28f, 30f, 135f, 86f), 18f)
                .Draw();

            var triangleScene = new NowRect(triangleCard.x + 14f, triangleCard.y + 50f, triangleCard.width - 28f, 146f);
            NowSdf.Scene(triangleScene, "visual-sdf-planar-triangle")
                .SetColor(new Color(1f, 0.28f, 0.67f, 1f))
                .SetFeather(0.5f)
                .SetGlow(12f, new Color(1f, 0.16f, 0.58f, 0.24f), 1.5f)
                .Triangle(
                    new Vector2(triangleScene.width * 0.5f, 18f),
                    new Vector2(triangleScene.width - 18f, triangleScene.height - 18f),
                    new Vector2(18f, triangleScene.height - 34f))
                .Draw();

            var lineScene = new NowRect(lineCard.x + 14f, lineCard.y + 50f, lineCard.width - 28f, 146f);
            NowSdf.Scene(lineScene, "visual-sdf-planar-line")
                .SetColor(new Color(0.30f, 1f, 0.58f, 1f))
                .SetFeather(0.5f)
                .SetGlow(14f, new Color(0.16f, 1f, 0.54f, 0.22f), 1.4f)
                .Line(
                    new Vector2(30f, lineScene.height - 32f),
                    new Vector2(lineScene.width - 30f, 32f),
                    28f)
                .Draw();

            var composedScene = new NowRect(composedCard.x + 14f, composedCard.y + 50f, composedCard.width - 28f, 146f);
            NowSdf.Scene(composedScene, "visual-sdf-planar-composed")
                .SetColor(new Color(0.36f, 0.46f, 1f, 1f))
                .SetFeather(0.5f)
                .ChamferedBox(new NowRect(8f, 14f, composedScene.width - 16f, composedScene.height - 28f), 20f)
                .Subtract()
                .RotateNext(8f)
                .Triangle(
                    new Vector2(composedScene.width * 0.5f, 34f),
                    new Vector2(composedScene.width - 42f, composedScene.height - 30f),
                    new Vector2(40f, composedScene.height - 30f))
                .SetColor(new Color(1f, 0.68f, 0.18f, 1f))
                .SmoothUnion(7f)
                .Line(
                    new Vector2(22f, composedScene.height - 20f),
                    new Vector2(composedScene.width - 18f, 20f),
                    12f)
                .Draw();
        }

        static void DrawSdfPlanarCard(NowRect rect, string title, string note)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.045f, 0.060f, 0.095f, 1f))
                .SetRadius(16f)
                .SetOutline(1f, new Color(0.24f, 0.42f, 0.62f, 0.55f))
                .Draw();
            Now.Text(new NowRect(rect.x + 16f, rect.y + 16f, rect.width - 32f, 22f))
                .SetFontSize(13f)
                .SetBold()
                .SetColor(new Color(0.56f, 0.82f, 1f, 1f))
                .Draw(title);
            Now.Text(new NowRect(rect.x + 16f, rect.yMax - 52f, rect.width - 32f, 34f))
                .SetFontSize(11f)
                .SetColor(new Color(0.70f, 0.80f, 0.92f, 1f))
                .Draw(note);
        }

        static void DrawSdfRadialPrimitives(NowRect rect)
        {
            Now.Rectangle(rect).SetColor(new Color(0.018f, 0.026f, 0.050f, 1f)).Draw();
            Now.Text(new NowRect(26f, 18f, rect.width - 52f, 30f))
                .SetFontSize(23f)
                .SetBold()
                .SetColor(Color.white)
                .Draw("SDF radial primitives");
            Now.Text(new NowRect(26f, 50f, rect.width - 52f, 21f))
                .SetFontSize(13f)
                .SetColor(new Color(0.65f, 0.76f, 0.89f, 1f))
                .Draw("Signed quarter turns, a seamless full ring, and a clamped over-turn disc.");

            DrawSdfRadialCard(new NowRect(26f, 92f, 184f, 238f), "+90° PIE", 0);
            DrawSdfRadialCard(new NowRect(228f, 92f, 184f, 238f), "-90° PIE", 1);
            DrawSdfRadialCard(new NowRect(430f, 92f, 184f, 238f), "360° ARC", 2);
            DrawSdfRadialCard(new NowRect(632f, 92f, 182f, 238f), "720° PIE", 3);
        }

        static void DrawSdfRadialCard(NowRect card, string label, int variant)
        {
            Now.Rectangle(card)
                .SetColor(new Color(0.045f, 0.060f, 0.095f, 1f))
                .SetRadius(16f)
                .SetOutline(1f, new Color(0.24f, 0.42f, 0.62f, 0.55f))
                .Draw();

            var shapeRect = new NowRect(card.x + 14f, card.y + 14f, card.width - 28f, 162f);
            var center = shapeRect.size * 0.5f;
            var scene = NowSdf.Scene(shapeRect, 4600 + variant)
                .SetFeather(0.5f);

            switch (variant)
            {
                case 0:
                    scene.SetColor(new Color(0.15f, 0.86f, 1f, 1f))
                        .Pie(center, 62f, 0f, Mathf.PI * 0.5f)
                        .Draw();
                    break;
                case 1:
                    scene.SetColor(new Color(1f, 0.35f, 0.72f, 1f))
                        .Pie(center, 62f, 0f, -Mathf.PI * 0.5f)
                        .Draw();
                    break;
                case 2:
                    scene.SetColor(new Color(0.32f, 1f, 0.62f, 1f))
                        .Arc(center, 48f, 10f, 0f, Mathf.PI * 2f)
                        .Draw();
                    break;
                default:
                    scene.SetColor(new Color(1f, 0.66f, 0.18f, 1f))
                        .Pie(center, 62f, 1.2f, Mathf.PI * 4f)
                        .Draw();
                    break;
            }

            Now.Text(new NowRect(card.x + 16f, card.yMax - 44f, card.width - 32f, 24f))
                .SetFontSize(14f)
                .SetBold()
                .SetColor(Color.white)
                .Draw(label);
        }

        static void DrawSdfCustomShaders(NowRect rect)
        {
            var background = new Color(0.018f, 0.026f, 0.050f, 1f);
            var grid = new Color(0.30f, 0.58f, 0.82f, 0.055f);
            Now.Rectangle(rect).SetColor(background).Draw();

            for (float x = rect.x + 24f; x < rect.xMax; x += 48f)
                Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(grid).Draw();

            Now.Text(new NowRect(26f, 15f, rect.width - 52f, 30f))
                .SetFontSize(23f)
                .SetBold()
                .SetColor(Color.white)
                .Draw("Custom SDF shader gallery");
            Now.Text(new NowRect(26f, 48f, rect.width - 52f, 21f))
                .SetFontSize(13f)
                .SetColor(new Color(0.65f, 0.76f, 0.89f, 1f))
                .Draw("One shape upload contract, three final-shading functions — animation, distance fields, and relighting.");

            var auroraCard = new NowRect(26f, 86f, 290f, 318f);
            var topoCard = new NowRect(335f, 86f, 290f, 318f);
            var paperCard = new NowRect(644f, 86f, 290f, 318f);
            DrawSdfCustomShaderCard(
                auroraCard,
                "AURORA",
                "ANIMATED",
                "Procedural bands and a distance halo\nwithout additional UI geometry.",
                new Color(0.18f, 0.94f, 1f, 1f));
            DrawSdfCustomShaderCard(
                topoCard,
                "TOPOGRAPHIC",
                "FIELD",
                "Repeating signed-distance contours\nrender both inside and outside.",
                new Color(0.32f, 1f, 0.68f, 1f));
            DrawSdfCustomShaderCard(
                paperCard,
                "PAPER CUTOUT",
                "RELIT",
                "Derivative bevel lighting plus a second\nSDF evaluation for the soft shadow.",
                new Color(1f, 0.68f, 0.30f, 1f));

            var auroraScene = new NowRect(auroraCard.x + 14f, auroraCard.y + 52f, auroraCard.width - 28f, 176f);
            DrawSdfCustomShaderCanvas(
                auroraScene,
                new Color(0.035f, 0.050f, 0.110f, 1f),
                new Color(0.075f, 0.020f, 0.115f, 1f));
            NowSdf.Scene(auroraScene, "visual-sdf-custom-aurora")
                .SetMaterial(_sdfAuroraMaterial)
                .SetColor(Color.white)
                .SetFeather(1f)
                .Circle(new Vector2(80f, 91f), 46f)
                .SmoothUnion(22f)
                .Circle(new Vector2(132f, 75f), 43f)
                .SmoothUnion(20f)
                .Circle(new Vector2(183f, 94f), 47f)
                .Draw();

            var topoScene = new NowRect(topoCard.x + 14f, topoCard.y + 52f, topoCard.width - 28f, 176f);
            DrawSdfCustomShaderCanvas(
                topoScene,
                new Color(0.020f, 0.075f, 0.085f, 1f),
                new Color(0.080f, 0.025f, 0.105f, 1f));
            NowSdf.Scene(topoScene, "visual-sdf-custom-topographic")
                .SetMaterial(_sdfTopographicMaterial)
                .SetColor(new Color(0.75f, 1f, 0.92f, 1f))
                .SetFeather(0.8f)
                .RoundedBox(new NowRect(42f, 43f, 178f, 92f), 34f)
                .SmoothUnion(16f)
                .Circle(new Vector2(76f, 104f), 36f)
                .Subtract(8f)
                .Circle(new Vector2(132f, 89f), 27f)
                .Subtract(5f)
                .RoundedBox(new NowRect(164f, 68f, 31f, 43f), 10f)
                .Draw();

            var paperScene = new NowRect(paperCard.x + 14f, paperCard.y + 52f, paperCard.width - 28f, 176f);
            DrawSdfCustomShaderCanvas(
                paperScene,
                new Color(0.110f, 0.042f, 0.045f, 1f),
                new Color(0.045f, 0.025f, 0.055f, 1f));
            NowSdf.Scene(paperScene, "visual-sdf-custom-paper-cutout")
                .SetMaterial(_sdfPaperCutoutMaterial)
                .SetColor(Color.white)
                .SetFeather(1f)
                .RoundedBox(new NowRect(40f, 31f, 181f, 110f), 20f)
                .Subtract()
                .Circle(new Vector2(40f, 86f), 11f)
                .Subtract()
                .Circle(new Vector2(221f, 86f), 11f)
                .Subtract()
                .Circle(new Vector2(131f, 86f), 27f)
                .Draw();
        }

        static void DrawSdfCustomShaderCard(
            NowRect rect,
            string title,
            string tag,
            string description,
            Color accent)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.044f, 0.055f, 0.086f, 0.98f))
                .SetRadius(17f)
                .SetOutline(1f, new Color(accent.r, accent.g, accent.b, 0.26f))
                .Draw();
            Now.Text(new NowRect(rect.x + 15f, rect.y + 16f, rect.width - 112f, 21f))
                .SetFontSize(13f)
                .SetBold()
                .SetColor(new Color(0.90f, 0.95f, 1f, 1f))
                .Draw(title);

            var tagRect = new NowRect(rect.xMax - 90f, rect.y + 13f, 74f, 22f);
            Now.Rectangle(tagRect)
                .SetColor(new Color(accent.r, accent.g, accent.b, 0.12f))
                .SetRadius(7f)
                .SetOutline(1f, new Color(accent.r, accent.g, accent.b, 0.30f))
                .Draw();
            Now.Text(new NowRect(tagRect.x + 7f, tagRect.y + 4f, tagRect.width - 14f, 14f))
                .SetFontSize(9f)
                .SetBold()
                .SetColor(accent)
                .Draw(tag);
            Now.Text(new NowRect(rect.x + 16f, rect.y + 247f, rect.width - 32f, 48f))
                .SetFontSize(12f)
                .SetColor(new Color(0.68f, 0.78f, 0.90f, 1f))
                .Draw(description);
        }

        static void DrawSdfCustomShaderCanvas(NowRect rect, Color top, Color bottom)
        {
            Now.Gradient(rect, top, bottom)
                .SetLinear(90f)
                .SetRadius(12f)
                .Draw();
            Now.Rectangle(rect)
                .SetColor(Color.clear)
                .SetRadius(12f)
                .SetOutline(1f, new Color(0.55f, 0.76f, 1f, 0.16f))
                .Draw();
        }

        static void DrawSdfGalleryCard(NowRect rect, string title)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.055f, 0.068f, 0.108f, 1f))
                .SetRadius(18f)
                .SetOutline(1f, new Color(0.32f, 0.48f, 0.70f, 0.34f))
                .Draw();
            Now.Text(new NowRect(rect.x + 18f, rect.y + 18f, rect.width - 36f, 22f))
                .SetFontSize(12f)
                .SetBold()
                .SetColor(new Color(0.56f, 0.82f, 1f, 1f))
                .Draw(title);
        }

        static void DrawSdfCoverageSwatch(NowRect rect, float feather, NowId id)
        {
            using (NowSdf.Scene(rect, id)
                .SetColor(Color.white)
                .SetFeather(feather)
                .Circle(rect.size * 0.5f, 49f)
                .BeginMask())
            {
                DrawCheckerboard(rect, 14f);
            }
        }

        static void DrawVariantTile(NowRect rect, string label)
        {
            DrawCheckerboard(rect, 18f);
            Now.Rectangle(rect)
                .SetColor(new Color(0.08f, 0.09f, 0.11f, 0.2f))
                .SetRadius(8f)
                .SetOutline(1f)
                .SetOutlineColor(new Color(1f, 1f, 1f, 0.12f))
                .Draw();
            Now.Text(new NowRect(rect.x + 10f, rect.y + rect.height - 20f, rect.width - 20f, 16f))
                .SetFontSize(11f)
                .SetColor(new Color(1f, 1f, 1f, 0.72f))
                .Draw(label);
        }

        static void DrawGlassVariant(NowRect rect, string label, float outline, Color outlineColor, float tintAlpha)
        {
            DrawCheckerboard(rect, 18f);
            Now.Glass(rect.Inset(20f, 12f, 20f, 24f))
                .SetBlurRadius(12f)
                .SetTint(new Color(1f, 1f, 1f, tintAlpha))
                .SetVibrancy(1f, 1f)
                .SetRadius(14f)
                .SetOutline(outline)
                .SetOutlineColor(outlineColor)
                .Draw();
            Now.Text(new NowRect(rect.x + 10f, rect.y + rect.height - 20f, rect.width - 20f, 16f))
                .SetFontSize(11f)
                .SetColor(new Color(1f, 1f, 1f, 0.78f))
                .Draw(label);
        }

        static void DrawCheckerboard(NowRect rect, float cellSize)
        {
            int cols = Mathf.CeilToInt(rect.width / cellSize);
            int rows = Mathf.CeilToInt(rect.height / cellSize);

            for (int row = 0; row < rows; ++row)
            {
                for (int col = 0; col < cols; ++col)
                {
                    var color = (row + col) % 2 == 0
                        ? new Color(0.18f, 0.42f, 0.68f, 1f)
                        : new Color(0.78f, 0.22f, 0.28f, 1f);
                    Now.Rectangle(new NowRect(
                            rect.x + col * cellSize,
                            rect.y + row * cellSize,
                            Mathf.Min(cellSize, rect.xMax - (rect.x + col * cellSize)),
                            Mathf.Min(cellSize, rect.yMax - (rect.y + row * cellSize))))
                        .SetColor(color)
                        .Draw();
                }
            }
        }

        static void DrawLottie(NowRect rect)
        {
            DrawSurface(rect);
            var target = rect.Inset(92f);

            if (_lottie != null)
            {
                Now.Lottie(target, _lottie)
                    .SetTime(0.35f)
                    .Draw();
            }
            else
            {
                Now.Circle(new Vector2(rect.width * 0.5f, rect.height * 0.5f), 124f)
                    .SetColor(new Color(0.95f, 0.22f, 0.33f, 1f))
                    .Draw();
            }

            Now.Text(new NowRect(24f, rect.height - 64f, rect.width - 48f, 40f))
                .SetFontSize(18f)
                .SetColor(Color.white)
                .Draw("Lottie vector frame");
        }

        static void DrawMarkdown(NowRect rect)
        {
            DrawSurface(rect);

            using (NowLayout.Area(rect.Inset(30f), spacing: 14f))
            {
                Header("Markdown and Code", "GitHub-flavored document rendering with syntax-shaped text.");
                var markdownRect = NowLayout.ReserveRect(height: 390f, stretchWidth: true);
                NowTheme.themeAsset.Rectangle(markdownRect, NowRectangleStyle.Surface).SetRadius(10f).Draw();
                NowMarkdown.Document(MarkdownSample).SetFontSize(15f).Draw(markdownRect.Inset(18f));
            }
        }

        static void DrawDocking(NowRect rect)
        {
            DrawShowcaseBackdrop(rect, "Docking", "Dockable tabbed windows with side splits, splitter resizing, and drag-to-dock guides.");

            var panel = new NowRect(26f, 86f, rect.width - 52f, rect.height - 112f);
            DrawShowcasePanel(panel, new Color(0.36f, 0.70f, 1f, 1f));
            SubmitDockWindows();
            NowDock.Space(_dock, panel.Inset(12f), "harness-dock").SetMinPaneSize(150f).Draw();
        }

        static void DrawNodeGraph(NowRect rect)
        {
            DrawShowcaseBackdrop(rect, "Node Graph", "Schema-driven nodes with typed ports, bezier links, selection, and undo history.");

            var panel = new NowRect(26f, 86f, rect.width - 52f, rect.height - 112f);
            DrawShowcasePanel(panel, new Color(0.32f, 1f, 0.68f, 1f));
            NowNodes.Canvas(_nodeGraph, panel.Inset(12f), "harness-graph")
                .SetSchema(_nodeSchema)
                .SetHistory(_nodeHistory)
                .Draw();
        }

        /// <summary>
        /// The README banner: NowUI's logo drawn by NowUI — SDF glow, gradient
        /// tile, line-and-circle pulse mark, and MSDF wordmark.
        /// </summary>
        static void DrawLogo(NowRect rect)
        {
            var background = new Color(0.018f, 0.026f, 0.050f, 1f);
            var grid = new Color(0.30f, 0.58f, 0.82f, 0.05f);
            var indigo = new Color(0.369f, 0.416f, 0.824f, 1f);
            var violet = new Color(0.545f, 0.361f, 0.965f, 1f);
            var muted = new Color(0.65f, 0.76f, 0.89f, 1f);

            Now.Rectangle(rect).SetColor(background).Draw();

            for (float x = rect.x + 24f; x < rect.xMax; x += 48f)
                Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(grid).Draw();

            var tile = new NowRect(rect.width * 0.5f - 188f, rect.height * 0.5f - 60f, 120f, 120f);
            var halo = new NowRect(tile.x - 70f, tile.y - 70f, tile.width + 140f, tile.height + 140f);

            Now.Gradient(
                    halo,
                    new Color(indigo.r, indigo.g, indigo.b, 0.34f),
                    new Color(indigo.r, indigo.g, indigo.b, 0f))
                .SetRadial(halo.center, halo.width * 0.5f)
                .Draw();

            Now.Gradient(tile, indigo, violet).SetLinear(135f).SetRadius(30f).Draw();
            Now.Rectangle(tile)
                .SetColor(Color.clear)
                .SetRadius(30f)
                .SetOutline(1.5f, new Color(1f, 1f, 1f, 0.28f))
                .Draw();

            Span<Vector2> pulse = stackalloc Vector2[]
            {
                new Vector2(tile.x + 22f, tile.center.y),
                new Vector2(tile.x + 44f, tile.center.y),
                new Vector2(tile.x + 56f, tile.center.y - 24f),
                new Vector2(tile.x + 72f, tile.center.y + 26f),
                new Vector2(tile.x + 84f, tile.center.y),
                new Vector2(tile.x + 98f, tile.center.y)
            };

            for (int i = 0; i < pulse.Length - 1; ++i)
                Now.Line(pulse[i], pulse[i + 1]).SetWidth(7f).SetColor(Color.white).Draw();

            for (int i = 0; i < pulse.Length; ++i)
                Now.Circle(pulse[i], 3.5f).SetColor(Color.white).Draw();

            Now.Text(new NowRect(tile.xMax + 36f, rect.height * 0.5f - 46f, 340f, 78f))
                .SetFontSize(64f)
                .SetBold()
                .SetColor(Color.white)
                .Draw("NowUI");
            Now.Text(new NowRect(tile.xMax + 39f, rect.height * 0.5f + 28f, 380f, 26f))
                .SetFontSize(16f)
                .SetColor(muted)
                .Draw("Immediate-mode UI for Unity");
        }

        static void DrawShowcaseBackdrop(NowRect rect, string title, string subtitle)
        {
            var background = new Color(0.018f, 0.026f, 0.050f, 1f);
            var grid = new Color(0.30f, 0.58f, 0.82f, 0.055f);
            Now.Rectangle(rect).SetColor(background).Draw();

            for (float x = rect.x + 24f; x < rect.xMax; x += 48f)
                Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(grid).Draw();

            Now.Text(new NowRect(26f, 15f, rect.width - 52f, 30f))
                .SetFontSize(23f)
                .SetBold()
                .SetColor(Color.white)
                .Draw(title);
            Now.Text(new NowRect(26f, 48f, rect.width - 52f, 21f))
                .SetFontSize(13f)
                .SetColor(new Color(0.65f, 0.76f, 0.89f, 1f))
                .Draw(subtitle);
        }

        static void DrawShowcasePanel(NowRect rect, Color accent)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.044f, 0.055f, 0.086f, 0.98f))
                .SetRadius(17f)
                .SetOutline(1f, new Color(accent.r, accent.g, accent.b, 0.26f))
                .Draw();
        }

        static void DrawSurface(NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Background))
                .Draw();
        }

        static void Header(string title, string subtitle)
        {
            NowLayout.Label(title).SetFontSize(28f).SetBold().Draw();
            NowLayout.Label(subtitle).SetFontSize(14f)
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw();
        }

        static void HeaderBlock(NowRect rect, string title, string subtitle)
        {
            Now.Text(new NowRect(28f, 30f, rect.width - 56f, 38f))
                .SetFontSize(28f)
                .SetBold()
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Text, Color.white))
                .Draw(title);
            Now.Text(new NowRect(28f, 76f, rect.width - 56f, 28f))
                .SetFontSize(14f)
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw(subtitle);
        }

        static void Panel(NowRect rect)
        {
            NowTheme.themeAsset.Rectangle(rect, NowRectangleStyle.Surface)
                .SetRadius(10f)
                .Draw();
        }

        static void Section(string title)
        {
            NowLayout.Label(title).SetFontSize(14f).SetBold()
                .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Accent, Color.cyan))
                .Draw();
        }

        static void SliderRow(string label, ref float value, float min, float max)
        {
            using (NowLayout.HorizontalScope(spacing: 8f, alignItems: NowLayoutAlign.Center))
            {
                NowLayout.Label(label).SetWidth(92f).Draw();
                var sliderRect = NowLayout.ReserveRect(190f, 30f, align: NowLayoutAlign.Center);
                Now.Slider(sliderRect, min, max).Draw(ref value);
                NowLayout.Label($"{Mathf.RoundToInt(value * 100f)}%").SetWidth(46f).SetFontSize(12f).Draw();
            }
        }

        static void SubmitDockWindows()
        {
            _dock.Window("Scene", rect =>
            {
                Now.Gradient(rect, new Color(0.035f, 0.050f, 0.110f, 1f), new Color(0.075f, 0.020f, 0.115f, 1f))
                    .SetLinear(90f)
                    .SetRadius(3f)
                    .Draw();

                for (float x = rect.x + 24f; x < rect.xMax; x += 28f)
                    Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(new Color(0.55f, 0.76f, 1f, 0.07f)).Draw();
                for (float y = rect.y + 24f; y < rect.yMax; y += 28f)
                    Now.Rectangle(new NowRect(rect.x, y, rect.width, 1f)).SetColor(new Color(0.55f, 0.76f, 1f, 0.07f)).Draw();

                var focus = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.52f);
                Now.Circle(focus, 52f).SetColor(new Color(0.18f, 0.94f, 1f, 0.10f)).Draw();
                Now.Circle(focus, 30f).SetColor(new Color(0.18f, 0.94f, 1f, 0.18f)).Draw();
                Now.Circle(focus, 12f).SetColor(new Color(0.18f, 0.94f, 1f, 0.95f)).Draw();

                Now.Text(new NowRect(rect.x + 12f, rect.yMax - 26f, rect.width - 24f, 18f))
                    .SetFontSize(12f)
                    .SetColor(new Color(1f, 1f, 1f, 0.55f))
                    .Draw("Scene");
            }, id: "Scene");

            _dock.Window("Hierarchy", rect =>
            {
                using (NowLayout.Area(rect, spacing: 6f))
                {
                    NowLayout.Label("Objects").SetFontSize(16f).Draw();
                    for (int i = 0; i < HierarchyObjects.Length; ++i)
                        NowLayout.Label(HierarchyObjects[i]).SetFontSize(13f).Draw();
                }
            }, id: "Hierarchy");

            _dock.Window("Inspector", rect =>
            {
                bool value = true;
                float exposure = 0.58f;
                using (NowLayout.Area(rect, spacing: 8f))
                {
                    NowLayout.Label("Inspector").SetFontSize(16f).Draw();
                    NowLayout.Checkbox("Visible").Draw(ref value);
                    NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref exposure);
                }
            }, id: "Inspector");
        }

        static void EnsureSharedState()
        {
            _lottie ??= AssetDatabase.LoadAssetAtPath<NowLottieAsset>("Assets/NowUI/Assets/AnimatedEmoji/2764.lottie");
            _sdfAuroraMaterial ??= Resources.Load<Material>("NowUI/SdfExamples/Aurora");
            _sdfTopographicMaterial ??= Resources.Load<Material>("NowUI/SdfExamples/Topographic");
            _sdfPaperCutoutMaterial ??= Resources.Load<Material>("NowUI/SdfExamples/PaperCutout");

            if (_dock == null)
            {
                _dock = new NowDockSpace();
                SubmitDockWindows();
                _dock.Dock("Inspector", "Scene", NowDockSide.Right, ratio: 0.26f);
                _dock.Dock("Hierarchy", "Scene", NowDockSide.Left, ratio: 0.24f);
            }

            if (_nodeSchema == null)
            {
                _nodeSchema = new NowNodeGraphSchema();
                _nodeSchema.Node(1, "Texture").SetSize(168f, 100f).Output(10, "RGBA", 4).Output(11, "A", 1);
                _nodeSchema.Node(2, "Tint").SetSize(156f, 92f).Output(20, "Color", 4);
                _nodeSchema.Node(3, "Multiply").SetSize(176f, 118f).Input(30, "A", 4).Input(31, "B", 4).Output(32, "Result", 4);
                _nodeSchema.Node(4, "Output").SetSize(176f, 100f).Input(40, "Base", 4);
                _nodeSchema.AllowSameTypes();
            }

            if (_nodeGraph == null)
            {
                _nodeGraph = new NowNodeGraph().SetSchema(_nodeSchema);
                _nodeGraph.AddNode(_nodeSchema, 1, new Vector2(70f, 90f), id: "texture");
                _nodeGraph.AddNode(_nodeSchema, 2, new Vector2(70f, 250f), id: "tint");
                _nodeGraph.AddNode(_nodeSchema, 3, new Vector2(330f, 150f), id: "multiply");
                _nodeGraph.AddNode(_nodeSchema, 4, new Vector2(610f, 170f), id: "output");
                _nodeGraph.TryAddLink("texture", 10, "multiply", 30);
                _nodeGraph.TryAddLink("tint", 20, "multiply", 31);
                _nodeGraph.TryAddLink("multiply", 32, "output", 40);
            }

            _nodeHistory ??= new NowNodeGraphHistory();
        }

        static void ResetFrameState()
        {
            NowSdf.Reset();
            NowTheme.Reset();
            NowInput.Reset();
            NowFocus.Reset();
            NowControlState.Reset();
            NowControls.Reset();
            NowLayout.Reset();
            NowOverlay.Reset();
            NowContextMenu.Reset();
            NowMarkdown.Reset();
        }

        static void WritePng(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;

            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
