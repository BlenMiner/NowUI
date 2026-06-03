using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NowFont))]
[CanEditMultipleObjects]
public sealed class NowFontEditor : Editor
{
    const float PREVIEW_FONT_SIZE = 28f;
    const string DEFAULT_PREVIEW_TEXT = "The quick brown fox jumps over lazy text.\nNowUI font preview 12345 😀🚀✨";
    static readonly Color PREVIEW_TEXT_COLOR = Color.white;
    static readonly Color PREVIEW_BACKGROUND_COLOR = new Color(0.22f, 0.22f, 0.22f, 1f);

    SerializedProperty _dynamicAtlasSize;
    SerializedProperty _dynamicPixelRange;
    SerializedProperty _dynamicMaxAtlasSize;
    SerializedProperty _dynamicMaxAtlasBytes;

    string _previewText = DEFAULT_PREVIEW_TEXT;

    void OnEnable()
    {
        _dynamicAtlasSize = serializedObject.FindProperty("dynamicAtlasSize");
        _dynamicPixelRange = serializedObject.FindProperty("dynamicPixelRange");
        _dynamicMaxAtlasSize = serializedObject.FindProperty("dynamicMaxAtlasSize");
        _dynamicMaxAtlasBytes = serializedObject.FindProperty("dynamicMaxAtlasBytes");
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
        DrawPreviewControls();

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
        EditorGUILayout.PropertyField(_dynamicAtlasSize, new GUIContent("Page Size"));
        EditorGUILayout.PropertyField(_dynamicPixelRange, new GUIContent("Pixel Range"));
        EditorGUILayout.PropertyField(_dynamicMaxAtlasSize, new GUIContent("Max Page Size"));
        EditorGUILayout.PropertyField(_dynamicMaxAtlasBytes, new GUIContent("Max Page Bytes"));

        if (GUILayout.Button("Clear Preview Cache"))
            ClearTargetCaches();
    }

    void DrawPreviewControls()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        _previewText = EditorGUILayout.TextArea(_previewText, GUILayout.MinHeight(44f));
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
            .Draw(_previewText);
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
}
