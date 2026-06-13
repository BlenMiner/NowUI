using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [CustomEditor(typeof(NowTheme))]
    public sealed class NowThemeEditor : UnityEditor.Editor
    {
        const float InspectorPreviewHeight = 260f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            if (changed)
            {
                Repaint();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);

            Rect rect = GUILayoutUtility.GetRect(1f, InspectorPreviewHeight, GUILayout.ExpandWidth(true));
            DrawPreview((NowTheme)target, rect);
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            DrawPreview((NowTheme)target, rect);
        }

        static void DrawPreview(NowTheme theme, Rect rect)
        {
            if (theme == null || Event.current.type != EventType.Repaint)
                return;

            Color background = theme.GetColor("background", new Color(0.18f, 0.18f, 0.18f, 1f));
            EditorGUI.DrawRect(rect, background);

            float pad = Mathf.Clamp(rect.width * 0.05f, 14f, 28f);
            Rect panel = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);
            DrawStyledRect(theme, panel, "surface");

            Rect titleRect = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 30f);
            DrawText(theme, titleRect, "NowUI Theme", "title");

            Rect bodyRect = new Rect(panel.x + 18f, panel.y + 46f, panel.width - 36f, 22f);
            DrawText(theme, bodyRect, "Palette, spacing, radius, text, and rectangle presets", "muted");

            DrawPalette(theme, new Rect(panel.x + 18f, panel.y + 82f, panel.width - 36f, 56f));
            DrawPresetRows(theme, new Rect(panel.x + 18f, panel.y + 152f, panel.width - 36f, panel.height - 168f));
        }

        static void DrawPalette(NowTheme theme, Rect rect)
        {
            var palette = theme.palette;
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
                DrawLabel(label, token.id, theme.GetColor("text-muted", Color.gray), 10, TextAnchor.MiddleCenter);
            }
        }

        static void DrawPresetRows(NowTheme theme, Rect rect)
        {
            var presets = theme.rectanglePresets;
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
                DrawStyledRect(theme, item, presets[i].id);
                DrawLabel(item, presets[i].id, ResolveReadableTextColor(theme, presets[i].id), 11, TextAnchor.MiddleCenter);
                x += width + gap;
            }

            Rect button = new Rect(rect.x, y + height + 14f, 104f, 30f);
            DrawStyledRect(theme, button, "accent");
            DrawText(theme, button, "Button", "button", TextAnchor.MiddleCenter);

            Rect caption = new Rect(button.xMax + 14f, button.y + 5f, rect.width - button.width - 14f, 22f);
            DrawText(theme, caption, "Code stays fluent: theme.Rectangle(rect, \"accent\")", "body");
        }

        static void DrawStyledRect(NowTheme theme, Rect rect, string presetId)
        {
            NowRectangle styled = theme.Rectangle(new Vector4(rect.x, rect.y, rect.width, rect.height), presetId);
            EditorGUI.DrawRect(rect, styled.color);

            if (styled.outline > 0f && styled.outlineColor.w > 0f)
                DrawOutline(rect, styled.outlineColor, Mathf.Max(1, Mathf.CeilToInt(styled.outline)));
        }

        static void DrawText(NowTheme theme, Rect rect, string text, string presetId, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            NowText styled = theme.Text(new Vector4(rect.x, rect.y, rect.width, rect.height), presetId);
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

        static Color ResolveReadableTextColor(NowTheme theme, string presetId)
        {
            NowRectangle styled = theme.Rectangle(Vector4.zero, presetId);
            if (styled.color.w < 0.2f)
                return theme.GetColor("text", Color.black);

            float luminance = styled.color.x * 0.299f + styled.color.y * 0.587f + styled.color.z * 0.114f;
            return luminance < 0.52f
                ? theme.GetColor("accent-text", Color.white)
                : theme.GetColor("text", Color.black);
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
}
