using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NowUI.Docking;
using NowUI.Markdown;
using NowUI.NodeGraph;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    internal sealed class NowHarnessScenario
    {
        public string name;
        public int width;
        public int height;
        public bool includeInGoldens;
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
        const string MarkdownSample =
            "# Harness markdown\n\n" +
            "NowUI renders **layout**, `inline code`, lists, links, and code fences through the same immediate-mode frame.\n\n" +
            "- deterministic offscreen target\n" +
            "- real font atlas upload\n" +
            "- reusable screenshot artifact\n\n" +
            "```csharp\nNowLayout.Button(\"Apply\").Draw();\n```";

        static readonly string[] QualityOptions = { "Low", "Medium", "High", "Ultra" };
        static readonly string[] GraphLog = { "compile shader", "bake preview", "upload material" };

        static readonly IdleInputProvider Input = new IdleInputProvider();

        static NowDockSpace _dock;
        static NowNodeGraphSchema _nodeSchema;
        static NowNodeGraph _nodeGraph;
        static NowNodeGraphHistory _nodeHistory;
        static NowLottieAsset _lottie;

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

        public static IReadOnlyList<NowHarnessScenario> All()
        {
            EnsureSharedState();

            return new[]
            {
                new NowHarnessScenario { name = "controls", width = 960, height = 540, includeInGoldens = true, draw = DrawControls },
                new NowHarnessScenario { name = "controls-dark", width = 960, height = 540, includeInGoldens = true, draw = DrawControlsDark },
                new NowHarnessScenario { name = "elevation", width = 840, height = 420, includeInGoldens = true, draw = DrawElevation },
                new NowHarnessScenario { name = "context-menu", width = 640, height = 420, includeInGoldens = true, draw = DrawContextMenu },
                new NowHarnessScenario { name = "text-layout", width = 960, height = 540, includeInGoldens = true, draw = DrawTextLayout },
                new NowHarnessScenario { name = "glass", width = 640, height = 360, includeInGoldens = true, draw = DrawGlass },
                new NowHarnessScenario { name = "shader-variants", width = 840, height = 420, includeInGoldens = true, draw = DrawShaderVariants },
                new NowHarnessScenario { name = "lottie", width = 512, height = 512, includeInGoldens = true, draw = DrawLottie },
                new NowHarnessScenario { name = "markdown-code", width = 960, height = 540, includeInGoldens = false, draw = DrawMarkdown },
                new NowHarnessScenario { name = "docking-nodegraph", width = 960, height = 540, includeInGoldens = false, draw = DrawDockingAndNodeGraph }
            };
        }

        public static NowHarnessCapture Capture(NowHarnessScenario scenario, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            ResetFrameState();

            var stopwatch = Stopwatch.StartNew();
            using var renderer = new NowRenderer();
            var target = new RenderTexture(scenario.width, scenario.height, 0, RenderTextureFormat.ARGB32)
            {
                name = "NowUI Harness Target",
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();

            try
            {
                var surface = new NowInputSurface(new Vector2(scenario.width, scenario.height));
                renderer.Warmup(surface, Input, () => DrawScenarioFrame(scenario));

                using (NowInput.Begin(Input, surface))
                using (renderer.Begin(target))
                {
                    DrawScenarioFrame(scenario);
                }

                renderer.Render(target, clear: true, clearColor: new Color(0.04f, 0.045f, 0.055f, 1f));
                WritePng(target, outputPath);
                stopwatch.Stop();

                return new NowHarnessCapture
                {
                    name = scenario.name,
                    width = scenario.width,
                    height = scenario.height,
                    path = outputPath,
                    batchCount = renderer.batchCount,
                    vertexCount = renderer.mesh != null ? renderer.mesh.vertexCount : 0,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                target.Release();
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

        static void DrawScenarioFrame(NowHarnessScenario scenario)
        {
            Now.defaultFont = Resources.Load<NowFontAsset>("NowUI/NotoSans");
            string themePath = scenario.name == "controls-dark"
                ? "Assets/NowUI/Assets/Themes/DefaultDark.asset"
                : "Assets/NowUI/Assets/Themes/Default.asset";
            var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);

            if (theme != null)
            {
                using (NowControls.Theme(theme))
                    scenario.draw(new NowRect(0, 0, scenario.width, scenario.height));
            }
            else
            {
                scenario.draw(new NowRect(0, 0, scenario.width, scenario.height));
            }
        }

        static void DrawControlsDark(NowRect rect)
        {
            DrawControls(rect);
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

            int menuId = NowInput.GetId("harness-context-menu");

            if (!NowContextMenu.isOpen)
                NowContextMenu.Open(menuId, new Vector2(64f, 48f));

            if (NowContextMenu.Begin(menuId))
            {
                NowContextMenu.Label("Harness Menu");
                NowContextMenu.Separator();

                for (int i = 0; i < 40; ++i)
                    NowContextMenu.Item($"Overflow Option {i + 1}");

                NowContextMenu.End();
                NowControlState.Get<float>(menuId, "ctx-scroll") = 180f;
            }
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
                using (NowLayout.Horizontal(spacing: 8f))
                {
                    NowLayout.Button("Primary").SetWidth(104f).Draw();
                    NowLayout.Button("Outline").SetStyle(NowRectangleStyle.Outline).SetWidth(104f).Draw();
                    NowLayout.Button("Muted").SetStyle(NowRectangleStyle.Muted).SetWidth(104f).Draw();
                }

                Section("Toggles");
                NowLayout.Checkbox("Enable glass").Draw(ref checkedValue);
                NowLayout.Checkbox("Use compact rows").Draw(ref otherValue);

                using (NowLayout.Horizontal(spacing: 8f))
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
                var markdownRect = NowLayout.Rect(height: 390f, stretchWidth: true);
                NowTheme.themeAsset.Rectangle(markdownRect, NowRectangleStyle.Surface).SetRadius(10f).Draw();
                NowMarkdown.Document(MarkdownSample).SetFontSize(15f).Draw(markdownRect.Inset(18f));
            }
        }

        static void DrawDockingAndNodeGraph(NowRect rect)
        {
            DrawSurface(rect);

            using (NowLayout.Area(rect.Inset(24f), spacing: 12f))
            {
                Header("Docking and Node Graph", "Two extension surfaces rendered from generated harness state.");

                using (NowLayout.Horizontal(spacing: 14f))
                {
                    var dockRect = NowLayout.Rect(width: 430f, height: 390f);
                    SubmitDockWindows();
                    NowDock.Space(_dock, dockRect, "harness-dock").SetMinPaneSize(120f).Draw();

                    var graphRect = NowLayout.Rect(height: 390f, stretchWidth: true);
                    NowNodes.Canvas(_nodeGraph, graphRect, "harness-graph")
                        .SetSchema(_nodeSchema)
                        .SetHistory(_nodeHistory)
                        .Draw();
                }
            }
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
            using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
            {
                NowLayout.Label(label).SetWidth(92f).Draw();
                var sliderRect = NowLayout.Rect(190f, 30f, align: NowLayoutAlign.Center);
                Now.Slider(sliderRect, min, max).Draw(ref value);
                NowLayout.Label($"{Mathf.RoundToInt(value * 100f)}%").SetWidth(46f).SetFontSize(12f).Draw();
            }
        }

        static void SubmitDockWindows()
        {
            _dock.Window("Scene", rect =>
            {
                Now.Rectangle(rect).SetColor(new Color(0.08f, 0.09f, 0.11f, 1f)).SetRadius(3f).Draw();
                for (float x = rect.x + 24f; x < rect.xMax; x += 28f)
                    Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(new Color(1f, 1f, 1f, 0.06f)).Draw();
                for (float y = rect.y + 24f; y < rect.yMax; y += 28f)
                    Now.Rectangle(new NowRect(rect.x, y, rect.width, 1f)).SetColor(new Color(1f, 1f, 1f, 0.06f)).Draw();
                Now.Text(rect.Inset(18f)).SetFontSize(20f).SetColor(Color.white).Draw("Scene View");
            }, id: "Scene");

            _dock.Window("Hierarchy", rect =>
            {
                using (NowLayout.Area(rect, spacing: 6f))
                {
                    NowLayout.Label("Objects").SetFontSize(16f).Draw();
                    for (int i = 0; i < GraphLog.Length; ++i)
                        NowLayout.Label(GraphLog[i]).SetFontSize(13f).Draw();
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

            if (_dock == null)
            {
                _dock = new NowDockSpace();
                SubmitDockWindows();
                _dock.Dock("Inspector", "Scene", NowDockSide.Right);
                _dock.Dock("Hierarchy", "Scene", NowDockSide.Left);
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
