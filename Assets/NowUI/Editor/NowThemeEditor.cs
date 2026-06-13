using System;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [CustomEditor(typeof(NowThemeAsset))]
    public sealed class NowThemeEditor : UnityEditor.Editor
    {
        const float InspectorPreviewHeight = 260f;

        static bool s_ShowGenerator = true;

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

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);

            Rect rect = GUILayoutUtility.GetRect(1f, InspectorPreviewHeight, GUILayout.ExpandWidth(true));
            DrawPreview((NowThemeAsset)target, rect);
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
        }

        static void DrawPreview(NowThemeAsset themeAsset, Rect rect)
        {
            if (themeAsset == null || Event.current.type != EventType.Repaint)
                return;

            Color background = themeAsset.GetColor("background", new Color(0.18f, 0.18f, 0.18f, 1f));
            EditorGUI.DrawRect(rect, background);

            float pad = Mathf.Clamp(rect.width * 0.05f, 14f, 28f);
            Rect panel = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);
            DrawStyledRect(themeAsset, panel, "surface");

            Rect titleRect = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 30f);
            DrawText(themeAsset, titleRect, "Now Theme", "title");

            Rect bodyRect = new Rect(panel.x + 18f, panel.y + 46f, panel.width - 36f, 22f);
            DrawText(themeAsset, bodyRect, "Palette, spacing, radius, text, and rectangle presets", "muted");

            DrawPalette(themeAsset, new Rect(panel.x + 18f, panel.y + 82f, panel.width - 36f, 56f));
            DrawPresetRows(themeAsset, new Rect(panel.x + 18f, panel.y + 152f, panel.width - 36f, panel.height - 168f));
        }

        static void DrawPalette(NowThemeAsset themeAsset, Rect rect)
        {
            var palette = themeAsset.palette;
            if (palette == null || palette.Count == 0)
                return;

            int count = Mathf.Min(6, palette.Count);
            float gap = 8f;
            float width = (rect.width - gap * (count - 1)) / count;

            for (int i = 0; i < count; ++i)
            {
                var token = palette[i];
                Rect swatch = new Rect(rect.x + i * (width + gap), rect.y, width, 34f);
                EditorGUI.DrawRect(swatch, token.color);
                DrawOutline(swatch, new Color(0f, 0f, 0f, 0.18f), 1);

                Rect label = new Rect(swatch.x, swatch.yMax + 3f, swatch.width, 18f);
                DrawLabel(label, token.id, themeAsset.GetColor("text-muted", Color.gray), 10, TextAnchor.MiddleCenter);
            }
        }

        static void DrawPresetRows(NowThemeAsset themeAsset, Rect rect)
        {
            var presets = themeAsset.rectanglePresets;
            if (presets == null)
                return;

            float x = rect.x;
            float y = rect.y;
            float gap = 8f;
            float height = 36f;

            for (int i = 0; i < presets.Count && i < 4; ++i)
            {
                float width = Mathf.Min(120f, (rect.width - gap * 3f) / 4f);
                Rect item = new Rect(x, y, width, height);
                DrawStyledRect(themeAsset, item, presets[i].id);
                DrawLabel(item, presets[i].id, ResolveReadableTextColor(themeAsset, presets[i].id), 11, TextAnchor.MiddleCenter);
                x += width + gap;
            }

            Rect button = new Rect(rect.x, y + height + 14f, 104f, 30f);
            DrawStyledRect(themeAsset, button, "accent");
            DrawText(themeAsset, button, "Button", "button", TextAnchor.MiddleCenter);

            Rect caption = new Rect(button.xMax + 14f, button.y + 5f, rect.width - button.width - 14f, 22f);
            DrawText(themeAsset, caption, "Code stays fluent: theme.Rectangle(rect, \"accent\")", "body");
        }

        static void DrawStyledRect(NowThemeAsset themeAsset, Rect rect, string presetId)
        {
            NowRectangle styled = themeAsset.Rectangle(new Vector4(rect.x, rect.y, rect.width, rect.height), presetId);
            EditorGUI.DrawRect(rect, styled.color);

            if (styled.outline > 0f && styled.outlineColor.w > 0f)
                DrawOutline(rect, styled.outlineColor, Mathf.Max(1, Mathf.CeilToInt(styled.outline)));
        }

        static void DrawText(NowThemeAsset themeAsset, Rect rect, string text, string presetId, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            NowText styled = themeAsset.Text(new Vector4(rect.x, rect.y, rect.width, rect.height), presetId);
            DrawLabel(rect, text, styled.color, Mathf.RoundToInt(styled.fontSize), alignment);
        }

        static void DrawLabel(Rect rect, string text, Color color, int fontSize, TextAnchor alignment)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = alignment,
                clipping = TextClipping.Clip,
                fontSize = Mathf.Clamp(fontSize, 9, 24),
                normal =
                {
                    textColor = color
                }
            };

            GUI.Label(rect, text, style);
        }

        static Color ResolveReadableTextColor(NowThemeAsset themeAsset, string presetId)
        {
            NowRectangle styled = themeAsset.Rectangle(Vector4.zero, presetId);
            if (styled.color.w < 0.2f)
                return themeAsset.GetColor("text", Color.black);

            Color text = themeAsset.GetColor("text", Color.black);
            Color accentText = themeAsset.GetColor("accent-text", Color.white);

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

            SetColorToken(serializedObject, "background", palette.background);
            SetColorToken(serializedObject, "surface", palette.surface);
            SetColorToken(serializedObject, "surface-muted", palette.surfaceMuted);
            SetColorToken(serializedObject, "text", palette.text);
            SetColorToken(serializedObject, "text-muted", palette.textMuted);
            SetColorToken(serializedObject, "border", palette.border);
            SetColorToken(serializedObject, "accent", palette.accent);
            SetColorToken(serializedObject, "accent-text", palette.accentText);

            SetRectangleFillFallback(serializedObject, "surface", palette.surface);
            SetRectangleFillFallback(serializedObject, "muted", palette.surfaceMuted);
            SetRectangleOutlineFallback(serializedObject, "outline", palette.border);
            SetRectangleFillFallback(serializedObject, "accent", palette.accent);

            SetTextColorFallback(serializedObject, "title", palette.text);
            SetTextColorFallback(serializedObject, "body", palette.text);
            SetTextColorFallback(serializedObject, "muted", palette.textMuted);
            SetTextColorFallback(serializedObject, "button", palette.accentText);
        }

        static void SetColorToken(SerializedObject serializedObject, string id, Color color)
        {
            var token = FindById(serializedObject.FindProperty("_palette"), id);
            if (token == null)
                return;

            token.FindPropertyRelative("_color").colorValue = color;
        }

        static void SetRectangleFillFallback(SerializedObject serializedObject, string id, Color color)
        {
            var preset = FindById(serializedObject.FindProperty("_rectanglePresets"), id);
            if (preset == null)
                return;

            preset.FindPropertyRelative("_fill").FindPropertyRelative("_fallback").colorValue = color;
        }

        static void SetRectangleOutlineFallback(SerializedObject serializedObject, string id, Color color)
        {
            var preset = FindById(serializedObject.FindProperty("_rectanglePresets"), id);
            if (preset == null)
                return;

            preset.FindPropertyRelative("_outlineColor").FindPropertyRelative("_fallback").colorValue = color;
        }

        static void SetTextColorFallback(SerializedObject serializedObject, string id, Color color)
        {
            var preset = FindById(serializedObject.FindProperty("_textPresets"), id);
            if (preset == null)
                return;

            preset.FindPropertyRelative("_color").FindPropertyRelative("_fallback").colorValue = color;
        }

        static SerializedProperty FindById(SerializedProperty array, string id)
        {
            if (array == null || !array.isArray)
                return null;

            for (int i = 0; i < array.arraySize; ++i)
            {
                var element = array.GetArrayElementAtIndex(i);
                var idProperty = element.FindPropertyRelative("_id");
                if (idProperty != null && string.Equals(idProperty.stringValue, id, StringComparison.OrdinalIgnoreCase))
                    return element;
            }

            return null;
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
