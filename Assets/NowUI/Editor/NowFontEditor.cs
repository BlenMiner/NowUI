using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NowFont))]
[CanEditMultipleObjects]
public sealed class NowFontEditor : Editor
{
    const float PREVIEW_FONT_SIZE = 28f;
    const float INLINE_GLYPH_EXPLORER_HEIGHT = 260f;
    const string TEXT_PREVIEW_SAMPLE = "The quick brown fox jumps over lazy text.\nNowUI font preview 12345";
    const int MIN_TEXT_PREVIEW_GLYPHS = 24;
    const int GLYPH_PREVIEW_LINE_LENGTH = 24;
    const int GLYPH_PREVIEW_MAX_GLYPHS = 72;
    static readonly Color PREVIEW_TEXT_COLOR = Color.white;
    static readonly Color PREVIEW_BACKGROUND_COLOR = new Color(0.22f, 0.22f, 0.22f, 1f);

    SerializedProperty _dynamicAtlasSize;
    SerializedProperty _dynamicPixelRange;
    SerializedProperty _dynamicPageSize;
    SerializedProperty _dynamicMaxAtlasSize;
    SerializedProperty _dynamicMaxAtlasBytes;
    SerializedProperty _fallbacks;

    readonly NowFontGlyphPickerControl _glyphPicker = new NowFontGlyphPickerControl();
    NowFont _previewFont;
    int _previewSourceBytes = -1;
    int _previewAtlasGlyphs = -1;
    string _previewText;

    void OnEnable()
    {
        _dynamicAtlasSize = serializedObject.FindProperty("dynamicAtlasSize");
        _dynamicPixelRange = serializedObject.FindProperty("dynamicPixelRange");
        _dynamicPageSize = serializedObject.FindProperty("dynamicPageSize");
        _dynamicMaxAtlasSize = serializedObject.FindProperty("dynamicMaxAtlasSize");
        _dynamicMaxAtlasBytes = serializedObject.FindProperty("dynamicMaxAtlasBytes");
        _fallbacks = serializedObject.FindProperty("_fallbacks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSummary();
        EditorGUILayout.Space(8f);
        EditorGUI.BeginChangeCheck();
        DrawSettings();
        bool settingsChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.Space(8f);
        DrawGlyphExplorer();

        serializedObject.ApplyModifiedProperties();

        if (settingsChanged)
            ClearTargetCaches();

    }

    public override bool HasPreviewGUI()
    {
        return true;
    }

    public override void OnPreviewGUI(Rect rect, GUIStyle background)
    {
        using (var preview = NowUIEditorGUI.Auto(rect, PREVIEW_BACKGROUND_COLOR))
            DrawPreview((NowFont)target, preview.rect);
    }

    void DrawSummary()
    {
        EditorGUILayout.LabelField("Font", EditorStyles.boldLabel);

        int fontCount = 0;
        int sourceCount = 0;
        int totalSourceBytes = 0;
        int totalCachedPages = 0;
        int totalCachedGlyphs = 0;

        for (int i = 0; i < targets.Length; ++i)
        {
            if (!(targets[i] is NowFont font))
                continue;

            ++fontCount;

            if (font.HasEmbeddedSource)
                ++sourceCount;

            totalSourceBytes += font.GetSourceByteCount();
            totalCachedPages += font.GetCachedDynamicPageCount();
            totalCachedGlyphs += font.GetCachedDynamicGlyphCount();
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Selected Fonts", fontCount);

            EditorGUI.showMixedValue = sourceCount > 0 && sourceCount < fontCount;
            EditorGUILayout.Toggle("Embedded Source", sourceCount == fontCount && fontCount > 0);
            EditorGUI.showMixedValue = false;

            EditorGUILayout.IntField("Source Bytes", totalSourceBytes);
            EditorGUILayout.IntField("Cached Atlas Pages", totalCachedPages);
            EditorGUILayout.IntField("Cached Glyphs", totalCachedGlyphs);
        }
    }

    void DrawSettings()
    {
        EditorGUILayout.LabelField("Dynamic Atlas", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_dynamicAtlasSize, new GUIContent("Glyph Size"));
        EditorGUILayout.PropertyField(_dynamicPixelRange, new GUIContent("Pixel Range"));
        EditorGUILayout.PropertyField(_dynamicPageSize, new GUIContent("Page Size"));
        EditorGUILayout.PropertyField(_dynamicMaxAtlasSize, new GUIContent("Max Page Size"));
        EditorGUILayout.PropertyField(_dynamicMaxAtlasBytes, new GUIContent("Max Page Bytes"));
        if (_fallbacks != null)
            EditorGUILayout.PropertyField(_fallbacks, new GUIContent("Fallback Fonts"));

        if (GUILayout.Button("Clear Preview Cache"))
            ClearTargetCaches();
    }

    void DrawGlyphExplorer()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Glyph Explorer", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(targets.Length != 1 || target == null))
            {
                if (GUILayout.Button("Open Window", GUILayout.Width(104f)))
                    NowFontGlyphPickerWindow.Show((NowFont)target);

                if (GUILayout.Button("Reload", GUILayout.Width(72f)))
                    _glyphPicker.Reload();
            }
        }

        if (targets.Length != 1)
        {
            EditorGUILayout.HelpBox("Select one font at a time to explore glyphs.", MessageType.Info);
            return;
        }

        var font = (NowFont)target;

        _glyphPicker.Draw(
            font,
            false,
            INLINE_GLYPH_EXPLORER_HEIGHT,
            false,
            null,
            ShowFocusedNotification,
            Repaint);
    }

    void DrawPreview(NowFont font, Rect rect)
    {
        if (font == null || Event.current.type != EventType.Repaint)
            return;

        float pad = 8f;
        var panel = new Vector4(0, 0, rect.width, rect.height);
        var previewRect = new Vector4(pad, pad, Mathf.Max(0f, rect.width - pad * 2f), Mathf.Max(0f, rect.height - pad * 2f));

        NowUI.Rectangle(panel)
            .SetColor(PREVIEW_BACKGROUND_COLOR)
            .Draw();

        NowUI.Text(previewRect, font)
            .SetFontSize(PREVIEW_FONT_SIZE)
            .SetColor(PREVIEW_TEXT_COLOR)
            .Draw(GetPreviewText(font));
    }

    string GetPreviewText(NowFont font)
    {
        if (font == null)
            return string.Empty;

        int sourceBytes = font.GetSourceByteCount();
        int atlasGlyphs = font.atlasInfo.glyphs?.Length ?? 0;

        if (_previewFont == font &&
            _previewSourceBytes == sourceBytes &&
            _previewAtlasGlyphs == atlasGlyphs &&
            _previewText != null)
        {
            return _previewText;
        }

        var catalog = NowFontGlyphCatalog.Load(font);
        _previewFont = font;
        _previewSourceBytes = sourceBytes;
        _previewAtlasGlyphs = atlasGlyphs;
        _previewText = BuildPreviewText(catalog);
        return _previewText;
    }

    static string BuildPreviewText(NowFontGlyphCatalog catalog)
    {
        if (catalog == null || catalog.glyphs == null || catalog.glyphs.Length == 0)
            return string.Empty;

        var codepoints = new HashSet<int>();
        var glyphs = catalog.glyphs;

        for (int i = 0; i < glyphs.Length; ++i)
            codepoints.Add(glyphs[i].codepoint);

        string textSample = BuildTextPreview(codepoints);

        if (CountVisibleGlyphs(textSample) >= MIN_TEXT_PREVIEW_GLYPHS)
            return textSample;

        return BuildGlyphRunPreview(glyphs, codepoints.Contains(' '));
    }

    static string BuildTextPreview(HashSet<int> codepoints)
    {
        var builder = new StringBuilder(TEXT_PREVIEW_SAMPLE.Length);

        for (int i = 0; i < TEXT_PREVIEW_SAMPLE.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(TEXT_PREVIEW_SAMPLE, ref i);

            if (codepoint == '\n')
            {
                TrimTrailingSpace(builder);

                if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
                    builder.Append('\n');

                continue;
            }

            if (codepoint == ' ')
            {
                if (codepoints.Contains(' ') &&
                    builder.Length > 0 &&
                    builder[builder.Length - 1] != ' ' &&
                    builder[builder.Length - 1] != '\n')
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (!codepoints.Contains(codepoint) || !IsPreviewableCodepoint(codepoint))
                continue;

            builder.Append(char.ConvertFromUtf32(codepoint));
        }

        TrimTrailingSpace(builder);
        return builder.ToString();
    }

    static string BuildGlyphRunPreview(NowFontGlyphInfo[] glyphs, bool includeSpaces)
    {
        var builder = new StringBuilder();
        int visibleGlyphs = 0;

        for (int i = 0; i < glyphs.Length && visibleGlyphs < GLYPH_PREVIEW_MAX_GLYPHS; ++i)
        {
            var glyph = glyphs[i];

            if (!IsPreviewableCodepoint(glyph.codepoint) || !glyph.TryGetCharacter(out var character))
                continue;

            if (visibleGlyphs > 0)
            {
                if (visibleGlyphs % GLYPH_PREVIEW_LINE_LENGTH == 0)
                {
                    builder.Append('\n');
                }
                else if (includeSpaces)
                {
                    builder.Append(' ');
                }
            }

            builder.Append(character);
            ++visibleGlyphs;
        }

        return builder.ToString();
    }

    static int CountVisibleGlyphs(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int count = 0;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            if (IsPreviewableCodepoint(codepoint))
                ++count;
        }

        return count;
    }

    static bool IsPreviewableCodepoint(int codepoint)
    {
        return codepoint > 0 &&
            codepoint != ' ' &&
            codepoint != '\n' &&
            codepoint != '\r' &&
            codepoint != '\t' &&
            (codepoint < 0x7f || codepoint > 0x9f) &&
            codepoint != 0xfe0e &&
            codepoint != 0xfe0f;
    }

    static void TrimTrailingSpace(StringBuilder builder)
    {
        while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            --builder.Length;
    }

    void ClearTargetCaches()
    {
        for (int i = 0; i < targets.Length; ++i)
        {
            if (targets[i] is NowFont font)
            {
                font.ClearDynamicCache();
                EditorUtility.SetDirty(font);
            }
        }

        Repaint();
    }

    static void ShowFocusedNotification(GUIContent content)
    {
        if (EditorWindow.focusedWindow != null)
            EditorWindow.focusedWindow.ShowNotification(content);
    }
}
