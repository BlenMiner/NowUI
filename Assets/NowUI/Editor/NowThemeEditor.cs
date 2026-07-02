using System;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [CustomEditor(typeof(NowThemeAsset))]
    public sealed class NowThemeEditor : UnityEditor.Editor
    {
        static bool s_ShowGenerator = true;

        static GUIStyle s_LabelStyle;

        static readonly NowColorToken[] PreviewPaletteTokens =
        {
            NowColorToken.Background,
            NowColorToken.Surface,
            NowColorToken.SurfaceElevated,
            NowColorToken.Text,
            NowColorToken.Border,
            NowColorToken.Accent,
            NowColorToken.AccentMuted,
            NowColorToken.Success,
            NowColorToken.Warning,
            NowColorToken.Danger
        };

        static readonly NowRectangleStyle[] PreviewRectangleStyles =
        {
            NowRectangleStyle.Surface,
            NowRectangleStyle.Muted,
            NowRectangleStyle.Outline,
            NowRectangleStyle.Accent,
            NowRectangleStyle.AccentSoft,
            NowRectangleStyle.Danger
        };

        SerializedProperty _generatorDark;
        SerializedProperty _generatorKeyColor;
        SerializedProperty _generatorAccentColor;
        SerializedProperty _generatorSeed;

        void OnEnable()
        {
            _generatorDark = serializedObject.FindProperty("_generatorDark");
            _generatorKeyColor = serializedObject.FindProperty("_generatorKeyColor");
            _generatorAccentColor = serializedObject.FindProperty("_generatorAccentColor");
            _generatorSeed = serializedObject.FindProperty("_generatorSeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            DrawGenerator();

            bool changed = EditorGUI.EndChangeCheck();
            bool applied = serializedObject.ApplyModifiedProperties();

            if (changed || applied)
            {
                Repaint();
                SceneView.RepaintAll();
            }
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            DrawPreview((NowThemeAsset)target, rect);
        }

        void DrawGenerator()
        {
            s_ShowGenerator = EditorGUILayout.Foldout(s_ShowGenerator, "Theme Generator", true);
            if (!s_ShowGenerator)
                return;

            if (_generatorDark == null ||
                _generatorKeyColor == null ||
                _generatorAccentColor == null ||
                _generatorSeed == null)
            {
                EditorGUILayout.HelpBox("Theme generator settings are unavailable on this asset.", MessageType.Warning);
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_generatorDark, new GUIContent("Dark"));
                EditorGUILayout.PropertyField(_generatorKeyColor, new GUIContent("Key Color"));
                EditorGUILayout.PropertyField(_generatorAccentColor, new GUIContent("Accent Color"));
                EditorGUILayout.PropertyField(_generatorSeed, new GUIContent("Random Seed"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Derive From Key Colors"))
                    {
                        ApplyGeneratedPalette(NowThemePaletteGenerator.FromKeyColors(
                            _generatorKeyColor.colorValue,
                            _generatorAccentColor.colorValue,
                            _generatorDark.boolValue));
                    }

                    if (GUILayout.Button("Random From Seed"))
                    {
                        NowThemePaletteGenerator.RandomKeyColors(
                            _generatorSeed.intValue,
                            _generatorDark.boolValue,
                            out var key,
                            out var accent);

                        _generatorKeyColor.colorValue = key;
                        _generatorAccentColor.colorValue = accent;
                        ApplyGeneratedPalette(NowThemePaletteGenerator.FromKeyColors(key, accent, _generatorDark.boolValue));
                    }

                    if (GUILayout.Button("New Random"))
                    {
                        _generatorSeed.intValue = System.Environment.TickCount;
                        NowThemePaletteGenerator.RandomKeyColors(
                            _generatorSeed.intValue,
                            _generatorDark.boolValue,
                            out var key,
                            out var accent);

                        _generatorKeyColor.colorValue = key;
                        _generatorAccentColor.colorValue = accent;
                        ApplyGeneratedPalette(NowThemePaletteGenerator.FromKeyColors(key, accent, _generatorDark.boolValue));
                    }
                }
            }
        }

        void ApplyGeneratedPalette(NowThemePaletteGenerator.Palette palette)
        {
            NowThemePaletteGenerator.WriteToSerializedTheme(serializedObject, palette);
            serializedObject.ApplyModifiedProperties();

            var asset = (NowThemeAsset)target;
            asset.RegenerateDerivedRoles();
            EditorUtility.SetDirty(asset);
            serializedObject.Update();
        }

        static void DrawPreview(NowThemeAsset themeAsset, Rect rect)
        {
            if (themeAsset == null || Event.current.type != EventType.Repaint)
                return;

            Color background = themeAsset.GetColor(NowColorToken.Background, new Color(0.18f, 0.18f, 0.18f, 1f));
            EditorGUI.DrawRect(rect, background);

            float pad = Mathf.Clamp(rect.width * 0.05f, 14f, 28f);
            Rect panel = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);
            DrawStyledRect(themeAsset, panel, NowRectangleStyle.Surface);

            Rect titleRect = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 30f);
            DrawText(themeAsset, titleRect, "Now Theme", NowTextStyle.Title);

            Rect bodyRect = new Rect(panel.x + 18f, panel.y + 46f, panel.width - 36f, 22f);
            DrawText(themeAsset, bodyRect, "Palette, spacing, radius, text, and rectangle presets", NowTextStyle.Muted);

            DrawPalette(themeAsset, new Rect(panel.x + 18f, panel.y + 82f, panel.width - 36f, 56f));
            DrawPresetRows(themeAsset, new Rect(panel.x + 18f, panel.y + 152f, panel.width - 36f, panel.height - 168f));
        }

        static void DrawPalette(NowThemeAsset themeAsset, Rect rect)
        {
            int count = PreviewPaletteTokens.Length;
            float gap = 8f;
            float width = (rect.width - gap * (count - 1)) / count;

            for (int i = 0; i < count; ++i)
            {
                NowColorToken token = PreviewPaletteTokens[i];
                Rect swatch = new Rect(rect.x + i * (width + gap), rect.y, width, 34f);
                EditorGUI.DrawRect(swatch, themeAsset.GetColor(token, Color.magenta));
                DrawOutline(swatch, new Color(0f, 0f, 0f, 0.18f), 1);

                Rect label = new Rect(swatch.x, swatch.yMax + 3f, swatch.width, 18f);
                DrawLabel(label, token.ToString(), themeAsset.GetColor(NowColorToken.TextMuted, Color.gray), 10, TextAnchor.MiddleCenter);
            }
        }

        static void DrawPresetRows(NowThemeAsset themeAsset, Rect rect)
        {
            float x = rect.x;
            float y = rect.y;
            float gap = 8f;
            float height = 36f;

            for (int i = 0; i < PreviewRectangleStyles.Length; ++i)
            {
                NowRectangleStyle style = PreviewRectangleStyles[i];
                float width = Mathf.Min(120f, (rect.width - gap * (PreviewRectangleStyles.Length - 1)) / PreviewRectangleStyles.Length);
                Rect item = new Rect(x, y, width, height);
                DrawStyledRect(themeAsset, item, style);
                DrawLabel(item, style.ToString(), ResolveReadableTextColor(themeAsset, style), 11, TextAnchor.MiddleCenter);
                x += width + gap;
            }

            Rect button = new Rect(rect.x, y + height + 14f, 104f, 30f);
            DrawStyledRect(themeAsset, button, NowRectangleStyle.Accent);
            DrawText(themeAsset, button, "Button", NowTextStyle.Button, TextAnchor.MiddleCenter);

            Rect caption = new Rect(button.xMax + 14f, button.y + 5f, rect.width - button.width - 14f, 22f);
            DrawText(themeAsset, caption, "Code stays typed: theme.Rectangle(rect, NowRectangleStyle.Accent)", NowTextStyle.Body);
        }

        static void DrawStyledRect(NowThemeAsset themeAsset, Rect rect, NowRectangleStyle style)
        {
            NowRectangle styled = themeAsset.Rectangle(new Vector4(rect.x, rect.y, rect.width, rect.height), style);
            EditorGUI.DrawRect(rect, styled.color);

            if (styled.outline > 0f && styled.outlineColor.w > 0f)
                DrawOutline(rect, styled.outlineColor, Mathf.Max(1, Mathf.CeilToInt(styled.outline)));
        }

        static void DrawText(NowThemeAsset themeAsset, Rect rect, string text, NowTextStyle style, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            NowText styled = themeAsset.Text(new Vector4(rect.x, rect.y, rect.width, rect.height), style);
            DrawLabel(rect, text, styled.color, Mathf.RoundToInt(styled.fontSize), alignment);
        }

        static void DrawLabel(Rect rect, string text, Color color, int fontSize, TextAnchor alignment)
        {
            s_LabelStyle ??= new GUIStyle(EditorStyles.label)
            {
                clipping = TextClipping.Clip
            };

            s_LabelStyle.alignment = alignment;
            s_LabelStyle.fontSize = Mathf.Clamp(fontSize, 9, 24);
            s_LabelStyle.normal.textColor = color;

            GUI.Label(rect, text, s_LabelStyle);
        }

        static Color ResolveReadableTextColor(NowThemeAsset themeAsset, NowRectangleStyle style)
        {
            NowRectangle styled = themeAsset.Rectangle(Vector4.zero, style);
            if (styled.color.w < 0.2f)
                return themeAsset.GetColor(NowColorToken.Text, Color.black);

            Color text = themeAsset.GetColor(NowColorToken.Text, Color.black);
            Color accentText = themeAsset.GetColor(NowColorToken.AccentText, Color.white);

            return ContrastRatio(styled.color, text) >= ContrastRatio(styled.color, accentText)
                ? text
                : accentText;
        }

        static float ContrastRatio(Color a, Color b)
        {
            float lighter = Mathf.Max(RelativeLuminance(a), RelativeLuminance(b));
            float darker = Mathf.Min(RelativeLuminance(a), RelativeLuminance(b));
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        static float RelativeLuminance(Color color)
        {
            return Linear(color.r) * 0.2126f + Linear(color.g) * 0.7152f + Linear(color.b) * 0.0722f;
        }

        static float Linear(float value)
        {
            return value <= 0.03928f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        static void DrawOutline(Rect rect, Color color, int thickness)
        {
            for (int i = 0; i < thickness; ++i)
            {
                Rect lineRect = new Rect(rect.x + i, rect.y + i, rect.width - i * 2f, 1f);
                EditorGUI.DrawRect(lineRect, color);
                lineRect.y = rect.yMax - i - 1f;
                EditorGUI.DrawRect(lineRect, color);
                lineRect = new Rect(rect.x + i, rect.y + i, 1f, rect.height - i * 2f);
                EditorGUI.DrawRect(lineRect, color);
                lineRect.x = rect.xMax - i - 1f;
                EditorGUI.DrawRect(lineRect, color);
            }
        }
    }

    public static class NowThemePaletteGenerator
    {
        public struct Palette
        {
            public Color background;
            public Color surface;
            public Color surfaceMuted;
            public Color text;
            public Color textMuted;
            public Color border;
            public Color accent;
            public Color accentText;
        }

        public static Palette FromKeyColors(Color key, Color accent, bool dark)
        {
            Color.RGBToHSV(key, out float keyHue, out float keySaturation, out _);
            Color.RGBToHSV(accent, out float accentHue, out float accentSaturation, out float accentValue);

            keySaturation = Mathf.Clamp(keySaturation, 0.05f, dark ? 0.34f : 0.22f);

            var palette = new Palette();

            if (dark)
            {
                float backgroundSaturation = Mathf.Clamp(keySaturation * 0.75f + 0.08f, 0.08f, 0.34f);

                palette.background = Hsv(keyHue, backgroundSaturation, 0.12f);
                palette.surface = Hsv(keyHue, Mathf.Clamp(backgroundSaturation * 0.92f, 0.08f, 0.32f), 0.16f);
                palette.surfaceMuted = Hsv(keyHue, Mathf.Clamp(backgroundSaturation * 0.82f, 0.07f, 0.30f), 0.22f);
                palette.text = Hsv(keyHue, 0.06f, 0.98f);
                palette.textMuted = Hsv(keyHue, 0.16f, 0.72f);
                palette.border = Hsv(keyHue, Mathf.Clamp(backgroundSaturation * 0.75f, 0.08f, 0.28f), 0.34f);
            }
            else
            {
                float backgroundSaturation = Mathf.Clamp(keySaturation * 0.34f + 0.025f, 0.025f, 0.12f);

                palette.background = Hsv(keyHue, backgroundSaturation, 0.975f);
                palette.surface = Hsv(keyHue, Mathf.Clamp(backgroundSaturation * 0.35f, 0.01f, 0.06f), 1f);
                palette.surfaceMuted = Hsv(keyHue, Mathf.Clamp(backgroundSaturation * 0.75f, 0.025f, 0.10f), 0.965f);
                palette.text = Hsv(keyHue, Mathf.Clamp(keySaturation * 1.45f, 0.28f, 0.55f), 0.16f);
                palette.textMuted = Hsv(keyHue, Mathf.Clamp(keySaturation * 0.95f, 0.12f, 0.30f), 0.50f);
                palette.border = Hsv(keyHue, Mathf.Clamp(backgroundSaturation * 1.1f, 0.035f, 0.14f), 0.90f);
            }

            float generatedAccentSaturation = Mathf.Clamp(accentSaturation, 0.54f, 0.92f);
            float generatedAccentValue = dark
                ? Mathf.Clamp(Mathf.Max(accentValue, 0.76f), 0.76f, 1f)
                : Mathf.Clamp(Mathf.Max(accentValue, 0.64f), 0.64f, 0.94f);

            palette.accent = Hsv(accentHue, generatedAccentSaturation, generatedAccentValue);
            palette.accentText = BestTextOn(palette.accent, palette.text, dark
                ? Hsv(keyHue, 0.48f, 0.10f)
                : Color.white);

            return palette;
        }

        public static Palette RandomPalette(int seed, bool dark)
        {
            RandomKeyColors(seed, dark, out var key, out var accent);
            return FromKeyColors(key, accent, dark);
        }

        public static void RandomKeyColors(int seed, bool dark, out Color key, out Color accent)
        {
            var random = new System.Random(seed);
            float keyHue = NextFloat(random);
            float accentHue = Mathf.Repeat(keyHue + 0.36f + NextFloat(random) * 0.32f, 1f);

            key = Hsv(
                keyHue,
                dark ? 0.20f + NextFloat(random) * 0.20f : 0.10f + NextFloat(random) * 0.18f,
                dark ? 0.30f + NextFloat(random) * 0.20f : 0.70f + NextFloat(random) * 0.20f);

            accent = Hsv(
                accentHue,
                0.58f + NextFloat(random) * 0.30f,
                dark ? 0.78f + NextFloat(random) * 0.20f : 0.64f + NextFloat(random) * 0.24f);
        }

        public static void WriteToSerializedTheme(SerializedObject serializedObject, Palette palette)
        {
            if (serializedObject == null)
                throw new ArgumentNullException(nameof(serializedObject));

            SetColorToken(serializedObject, NowColorToken.Background, palette.background);
            SetColorToken(serializedObject, NowColorToken.Surface, palette.surface);
            SetColorToken(serializedObject, NowColorToken.SurfaceMuted, palette.surfaceMuted);
            SetColorToken(serializedObject, NowColorToken.Text, palette.text);
            SetColorToken(serializedObject, NowColorToken.TextMuted, palette.textMuted);
            SetColorToken(serializedObject, NowColorToken.Border, palette.border);
            SetColorToken(serializedObject, NowColorToken.Accent, palette.accent);
            SetColorToken(serializedObject, NowColorToken.AccentText, palette.accentText);

            SetRectangleFillFallback(serializedObject, NowRectangleStyle.Surface, palette.surface);
            SetRectangleFillFallback(serializedObject, NowRectangleStyle.Muted, palette.surfaceMuted);
            SetRectangleOutlineFallback(serializedObject, NowRectangleStyle.Outline, palette.border);
            SetRectangleFillFallback(serializedObject, NowRectangleStyle.Accent, palette.accent);

            SetTextColorFallback(serializedObject, NowTextStyle.Title, palette.text);
            SetTextColorFallback(serializedObject, NowTextStyle.Body, palette.text);
            SetTextColorFallback(serializedObject, NowTextStyle.Muted, palette.textMuted);
            SetTextColorFallback(serializedObject, NowTextStyle.Button, palette.accentText);
        }

        static void SetColorToken(SerializedObject serializedObject, NowColorToken token, Color color)
        {
            var palette = serializedObject.FindProperty("_palette");
            var property = palette?.FindPropertyRelative(ColorFieldName(token));
            if (property == null)
                return;

            property.colorValue = color;
        }

        static void SetRectangleFillFallback(SerializedObject serializedObject, NowRectangleStyle style, Color color)
        {
            var preset = RectanglePresetProperty(serializedObject, style);
            if (preset == null)
                return;

            preset.FindPropertyRelative("_fill").FindPropertyRelative("_fallback").colorValue = color;
        }

        static void SetRectangleOutlineFallback(SerializedObject serializedObject, NowRectangleStyle style, Color color)
        {
            var preset = RectanglePresetProperty(serializedObject, style);
            if (preset == null)
                return;

            preset.FindPropertyRelative("_outlineColor").FindPropertyRelative("_fallback").colorValue = color;
        }

        static void SetTextColorFallback(SerializedObject serializedObject, NowTextStyle style, Color color)
        {
            var preset = TextPresetProperty(serializedObject, style);
            if (preset == null)
                return;

            preset.FindPropertyRelative("_color").FindPropertyRelative("_fallback").colorValue = color;
        }

        static SerializedProperty RectanglePresetProperty(SerializedObject serializedObject, NowRectangleStyle style)
        {
            return serializedObject.FindProperty("_rectanglePresets")?.FindPropertyRelative(RectangleStyleFieldName(style));
        }

        static SerializedProperty TextPresetProperty(SerializedObject serializedObject, NowTextStyle style)
        {
            return serializedObject.FindProperty("_textPresets")?.FindPropertyRelative(TextStyleFieldName(style));
        }

        static string ColorFieldName(NowColorToken token)
        {
            switch (token)
            {
                case NowColorToken.Background:
                    return "_background";
                case NowColorToken.Surface:
                    return "_surface";
                case NowColorToken.SurfaceMuted:
                    return "_surfaceMuted";
                case NowColorToken.Text:
                    return "_text";
                case NowColorToken.TextMuted:
                    return "_textMuted";
                case NowColorToken.Border:
                    return "_border";
                case NowColorToken.Accent:
                    return "_accent";
                case NowColorToken.AccentText:
                    return "_accentText";
                default:
                    return string.Empty;
            }
        }

        static string RectangleStyleFieldName(NowRectangleStyle style)
        {
            switch (style)
            {
                case NowRectangleStyle.Surface:
                    return "_surface";
                case NowRectangleStyle.Muted:
                    return "_muted";
                case NowRectangleStyle.Outline:
                    return "_outline";
                case NowRectangleStyle.Accent:
                    return "_accent";
                default:
                    return string.Empty;
            }
        }

        static string TextStyleFieldName(NowTextStyle style)
        {
            switch (style)
            {
                case NowTextStyle.Title:
                    return "_title";
                case NowTextStyle.Body:
                    return "_body";
                case NowTextStyle.Muted:
                    return "_muted";
                case NowTextStyle.Button:
                    return "_button";
                default:
                    return string.Empty;
            }
        }

        static Color Hsv(float hue, float saturation, float value)
        {
            Color color = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(saturation), Mathf.Clamp01(value));
            color.a = 1f;
            return color;
        }

        static Color BestTextOn(Color background, Color light, Color dark)
        {
            return ContrastRatio(background, light) >= ContrastRatio(background, dark) ? light : dark;
        }

        static float ContrastRatio(Color a, Color b)
        {
            float lighter = Mathf.Max(RelativeLuminance(a), RelativeLuminance(b));
            float darker = Mathf.Min(RelativeLuminance(a), RelativeLuminance(b));
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        static float RelativeLuminance(Color color)
        {
            return Linear(color.r) * 0.2126f + Linear(color.g) * 0.7152f + Linear(color.b) * 0.0722f;
        }

        static float Linear(float value)
        {
            return value <= 0.03928f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        static float NextFloat(System.Random random)
        {
            return (float)random.NextDouble();
        }
    }
}
