using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [CustomEditor(typeof(NowFont))]
    [CanEditMultipleObjects]
    public sealed class NowFontEditor : UnityEditor.Editor
    {
        const float PREVIEW_FONT_SIZE = 28f;
        const float INLINE_GLYPH_EXPLORER_MIN_HEIGHT = 260f;
        const float ATLAS_THUMBNAIL_SIZE = 82f;
        const float ATLAS_THUMBNAIL_GAP = 8f;
        const float ATLAS_STRIP_HEIGHT = 82f;
        const string TEXT_PREVIEW_SAMPLE = "The quick brown fox jumps over lazy text.\nNowUI font preview 12345";
        const int MIN_TEXT_PREVIEW_GLYPHS = 24;
        const int GLYPH_PREVIEW_LINE_LENGTH = 24;
        const int GLYPH_PREVIEW_MAX_GLYPHS = 72;
        const float BAKED_CHARACTERS_MIN_HEIGHT = 48f;
        static readonly Color PREVIEW_TEXT_COLOR = Color.white;
        static readonly Color PREVIEW_BACKGROUND_COLOR = new Color(0.22f, 0.22f, 0.22f, 1f);

        SerializedProperty _dynamicAtlasSize;
        SerializedProperty _dynamicPixelRange;
        SerializedProperty _dynamicPageSize;
        SerializedProperty _dynamicMaxAtlasSize;
        SerializedProperty _dynamicMaxAtlasBytes;
        SerializedProperty _dynamicMaxGlyphSize;
        SerializedProperty _fallbacks;
        SerializedProperty _bakedCharacters;
        static bool s_bakedPagesExpanded;

        readonly NowFontGlyphPickerControl _glyphPicker = new NowFontGlyphPickerControl();
        NowFont _previewFont;
        int _previewSourceBytes = -1;
        int _previewAtlasGlyphs = -1;
        string _previewText;
        readonly List<Texture2D> _dynamicAtlasTextures = new List<Texture2D>();
        Vector2 _atlasScroll;

        void OnEnable()
        {
            _dynamicAtlasSize = serializedObject.FindProperty("dynamicAtlasSize");
            _dynamicPixelRange = serializedObject.FindProperty("dynamicPixelRange");
            _dynamicPageSize = serializedObject.FindProperty("dynamicPageSize");
            _dynamicMaxAtlasSize = serializedObject.FindProperty("dynamicMaxAtlasSize");
            _dynamicMaxAtlasBytes = serializedObject.FindProperty("dynamicMaxAtlasBytes");
            _dynamicMaxGlyphSize = serializedObject.FindProperty("dynamicMaxGlyphSize");
            _fallbacks = serializedObject.FindProperty("_fallbacks");
            _bakedCharacters = serializedObject.FindProperty("_bakedCharacters");
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
            DrawBakedPages();
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
            using (var preview = NowEditorGUI.Auto(rect, PREVIEW_BACKGROUND_COLOR))
                DrawPreview((NowFont)target, preview.rect);
        }

        void DrawSummary()
        {
            EditorGUILayout.LabelField("Font", EditorStyles.boldLabel);

            int fontCount = 0;
            int totalSourceBytes = 0;
            int totalCachedPages = 0;

            for (int i = 0; i < targets.Length; ++i)
            {
                if (targets[i] is not NowFont font)
                    continue;

                ++fontCount;

                totalSourceBytes += font.GetSourceByteCount();
                totalCachedPages += font.GetCachedDynamicPageCount();
            }

            if (fontCount == 0)
            {
                EditorGUILayout.HelpBox("Select a NowFont asset to inspect atlases.", MessageType.Info);
                return;
            }

            if (targets.Length != 1)
                return;

            DrawAtlasStrip((NowFont)target, totalSourceBytes, totalCachedPages);
        }

        void DrawAtlasStrip(NowFont font, int sourceBytes, int cachedPages)
        {
            if (font == null)
                return;

            font.GetCachedDynamicAtlasTextures(_dynamicAtlasTextures);

            int atlasCount = (font.atlas != null ? 1 : 0) + _dynamicAtlasTextures.Count;

            if (atlasCount == 0)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("No atlas pages are currently resident.", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    if (sourceBytes > 0)
                        GUILayout.Label("Glyphs will populate dynamically as text is requested.", EditorStyles.miniLabel);
                }

                return;
            }

            Rect stripRect = GUILayoutUtility.GetRect(
                0f,
                float.MaxValue,
                ATLAS_STRIP_HEIGHT,
                ATLAS_STRIP_HEIGHT,
                GUILayout.ExpandWidth(true));

            float contentWidth = atlasCount * ATLAS_THUMBNAIL_SIZE + Mathf.Max(0, atlasCount - 1) * ATLAS_THUMBNAIL_GAP;
            var viewRect = new Rect(0f, 0f, Mathf.Max(stripRect.width, contentWidth), ATLAS_STRIP_HEIGHT - 18f);
            _atlasScroll = GUI.BeginScrollView(stripRect, _atlasScroll, viewRect, false, false);

            float x = 0f;

            if (font.atlas != null)
            {
                DrawAtlasThumbnail(new Rect(x, 0f, ATLAS_THUMBNAIL_SIZE, ATLAS_THUMBNAIL_SIZE), font.atlas, "Base");
                x += ATLAS_THUMBNAIL_SIZE + ATLAS_THUMBNAIL_GAP;
            }

            int bakedCount = 0;
            int cachedCount = 0;

            for (int i = 0; i < _dynamicAtlasTextures.Count; ++i)
            {
                var texture = _dynamicAtlasTextures[i];
                string label = font.IsBakedAtlasTexture(texture)
                    ? $"Baked {++bakedCount}"
                    : $"Cache {++cachedCount}";
                DrawAtlasThumbnail(
                    new Rect(x, 0f, ATLAS_THUMBNAIL_SIZE, ATLAS_THUMBNAIL_SIZE),
                    texture,
                    label);
                x += ATLAS_THUMBNAIL_SIZE + ATLAS_THUMBNAIL_GAP;
            }

            GUI.EndScrollView();

            if (cachedPages > _dynamicAtlasTextures.Count)
                EditorGUILayout.LabelField("Some cached atlas pages were stale and were skipped.", EditorStyles.miniLabel);
        }

        static void DrawAtlasThumbnail(Rect rect, Texture2D atlas, string label)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0.11f, 0.11f, 0.11f, 1f));

            if (atlas != null)
            {
                var imageRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 22f);
                GUI.DrawTexture(imageRect, atlas, ScaleMode.ScaleToFit, true);
            }

            var labelRect = new Rect(rect.x + 4f, rect.yMax - 18f, rect.width - 8f, 16f);
            GUI.Label(labelRect, label, CenteredMiniLabel);
        }

        static GUIStyle _centeredMiniLabel;

        static GUIStyle CenteredMiniLabel
        {
            get
            {
                _centeredMiniLabel ??= new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };

                return _centeredMiniLabel;
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
            EditorGUILayout.PropertyField(_dynamicMaxGlyphSize, new GUIContent(
                "Max Glyph Size",
                "Largest glyph cell used for large text. Equal to Glyph Size disables resolution tiers."));
            if (_fallbacks != null)
                EditorGUILayout.PropertyField(_fallbacks, new GUIContent("Fallback Fonts"));

            if (GUILayout.Button("Clear Preview Cache"))
                ClearTargetCaches();
        }

        void DrawBakedPages()
        {
            if (_bakedCharacters == null)
                return;

            NowFont single = targets.Length == 1 ? target as NowFont : null;
            s_bakedPagesExpanded = EditorGUILayout.Foldout(
                s_bakedPagesExpanded,
                BakedPagesHeader(single),
                true,
                EditorStyles.foldoutHeader);

            if (!s_bakedPagesExpanded)
                return;

            if (single != null)
                DrawBakedStatus(single);

            int bakedFonts = 0;

            for (int i = 0; i < targets.Length; ++i)
            {
                if (targets[i] is NowFont font && font.bakedPageCount > 0)
                    ++bakedFonts;
            }

            if (GUILayout.Button("Bake All Glyphs"))
                BakeAllTargets();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"Characters (subset instead of all glyphs): {CountCodepoints(_bakedCharacters.stringValue)}",
                EditorStyles.miniLabel);
            EditorGUI.showMixedValue = _bakedCharacters.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            string characters = EditorGUILayout.TextArea(
                _bakedCharacters.stringValue,
                GUILayout.MinHeight(BAKED_CHARACTERS_MIN_HEIGHT));

            if (EditorGUI.EndChangeCheck())
                _bakedCharacters.stringValue = characters;

            EditorGUI.showMixedValue = false;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ASCII"))
                    AppendBakedPreset(NowFontBaker.ASCII);

                if (GUILayout.Button("Latin-1"))
                    AppendBakedPreset(NowFontBaker.LATIN_1);

                if (GUILayout.Button("Latin Ext-A"))
                    AppendBakedPreset(NowFontBaker.LATIN_EXTENDED_A);

                if (GUILayout.Button("Clear"))
                    SetBakedCharacters(string.Empty);
            }

            bool hasCharacters = _bakedCharacters.hasMultipleDifferentValues ||
                !string.IsNullOrEmpty(_bakedCharacters.stringValue);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasCharacters))
                {
                    if (GUILayout.Button("Bake Characters"))
                        BakeCharacterTargets();
                }

                using (new EditorGUI.DisabledScope(bakedFonts == 0))
                {
                    if (GUILayout.Button("Remove Baked Pages"))
                        ClearTargetBakes();
                }
            }
        }

        void AppendBakedPreset(string preset)
        {
            SetBakedCharacters(NowFontBaker.Merge(_bakedCharacters.stringValue, preset));
        }

        void SetBakedCharacters(string characters)
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            _bakedCharacters.stringValue = characters;
        }

        static string CountCodepoints(string characters)
        {
            if (string.IsNullOrEmpty(characters))
                return "empty";

            int count = 0;

            for (int i = 0; i < characters.Length; ++i)
            {
                if (NowFont.ReadCodepoint(characters, ref i) > 0)
                    ++count;
            }

            return count == 1 ? "1 character" : $"{count} characters";
        }

        static string BakedPagesHeader(NowFont font)
        {
            if (font == null || font.bakedPageCount == 0)
                return "Baked Pages";

            return $"Baked Pages ({font.bakedPageCount} page(s), {BakedBytes(font) / (1024f * 1024f):0.0} MB)";
        }

        static long BakedBytes(NowFont font)
        {
            long bytes = 0;
            var pages = font.GetBakedPages();

            if (pages == null)
                return 0;

            for (int i = 0; i < pages.Length; ++i)
            {
                var texture = pages[i].texture;

                if (texture != null)
                    bytes += (long)texture.width * texture.height * 4;
            }

            return bytes;
        }

        static void DrawBakedStatus(NowFont font)
        {
            if (font.bakedPageCount == 0)
            {
                EditorGUILayout.LabelField("No baked pages; glyphs bake on first use.", EditorStyles.miniLabel);
                return;
            }

            string coverage = font.bakedAllGlyphs ? "All glyphs" : "Characters";
            EditorGUILayout.LabelField(
                $"{coverage}: {font.bakedPageCount} page(s), {font.bakedGlyphCount} glyph record(s), {BakedBytes(font) / (1024f * 1024f):0.0} MB",
                EditorStyles.miniLabel);

            if (font.bakedPagesStale)
            {
                EditorGUILayout.HelpBox(
                    "The baked pages were made for a different glyph size or pixel range and are ignored at runtime. Bake again to match the current settings.",
                    MessageType.Warning);
            }
        }

        void BakeAllTargets()
        {
            var fonts = new List<NowFont>();

            for (int i = 0; i < targets.Length; ++i)
            {
                if (targets[i] is NowFont font)
                    fonts.Add(font);
            }

            if (!NowFontBaker.ConfirmBakeAll(fonts))
                return;

            BakeTargets(fonts, true);
        }

        void BakeCharacterTargets()
        {
            serializedObject.ApplyModifiedProperties();
            var fonts = new List<NowFont>();

            for (int i = 0; i < targets.Length; ++i)
            {
                if (targets[i] is NowFont font)
                    fonts.Add(font);
            }

            BakeTargets(fonts, false);
        }

        void BakeTargets(List<NowFont> fonts, bool allGlyphs)
        {
            try
            {
                for (int i = 0; i < fonts.Count; ++i)
                {
                    var font = fonts[i];
                    EditorUtility.DisplayProgressBar("Bake Font Glyphs", font.name, i / (float)fonts.Count);

                    bool baked = allGlyphs
                        ? NowFontBaker.TryBakeAll(font, out string error)
                        : NowFontBaker.TryBake(font, font.bakedCharacters, out error);

                    if (!baked)
                        Debug.LogError($"NowUI: failed to bake {font.name}\n{error}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            serializedObject.Update();
            Repaint();
        }

        void ClearTargetBakes()
        {
            for (int i = 0; i < targets.Length; ++i)
            {
                if (targets[i] is NowFont font)
                    NowFontBaker.Clear(font);
            }

            serializedObject.Update();
            Repaint();
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
                INLINE_GLYPH_EXPLORER_MIN_HEIGHT,
                true,
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

            Now.Rectangle(panel)
                .SetColor(PREVIEW_BACKGROUND_COLOR)
                .Draw();

            Now.Text(previewRect, font)
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

                switch (codepoint)
                {
                    case '\n':
                    {
                        TrimTrailingSpace(builder);

                        if (builder.Length > 0 && builder[^1] != '\n')
                            builder.Append('\n');

                        continue;
                    }
                    case ' ':
                    {
                        if (codepoints.Contains(' ') &&
                            builder.Length > 0 &&
                            builder[^1] != ' ' &&
                            builder[^1] != '\n')
                        {
                            builder.Append(' ');
                        }

                        continue;
                    }
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
            while (builder.Length > 0 && builder[^1] == ' ')
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
}
