using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    internal sealed class NowFontGlyphPickerControl
    {
        const float ROW_HEIGHT = 44f;
        const float PREVIEW_SIZE = 34f;
        const float ROW_PREVIEW_FONT_SIZE = 28f;
        const float SELECTED_PREVIEW_SIZE = 56f;
        const float SELECTED_PREVIEW_FONT_SIZE = 44f;
        const float EXPANDED_LIST_BOTTOM_PADDING = 10f;
        const float LIST_HEIGHT_EPSILON = 0.5f;

        static readonly Color RowEven = new Color(0.19f, 0.19f, 0.19f, 1f);
        static readonly Color RowOdd = new Color(0.16f, 0.16f, 0.16f, 1f);
        static readonly Color RowSelected = new Color(0.22f, 0.36f, 0.54f, 1f);
        static readonly Color PreviewBackground = new Color(0.08f, 0.08f, 0.08f, 1f);
        static readonly Color PreviewColor = Color.white;
        static readonly PropertyInfo GUIClipVisibleRectProperty = typeof(GUI).Assembly
            .GetType("UnityEngine.GUIClip")
            ?.GetProperty("visibleRect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        const float ScrollbarWidth = 16f;

        NowFont _font;
        string _search;
        bool _sortByName;
        Vector2 _scroll;
        NowFontGlyphCatalog _catalog;
        readonly List<NowFontGlyphInfo> _filteredGlyphs = new List<NowFontGlyphInfo>();
        bool _filterDirty = true;
        bool _hasSelectedGlyph;
        NowFontGlyphInfo _selectedGlyph;
        float _expandedListHeight;
        System.Text.StringBuilder _prebakeBuilder;

        public NowFont font => _font;

        public void SetFont(NowFont font)
        {
            if (font == _font && _catalog != null)
                return;

            _font = font;
            _scroll = default;
            _expandedListHeight = 0f;
            _hasSelectedGlyph = false;
            Reload();
        }

        public void Reload()
        {
            _catalog = _font != null ? NowFontGlyphCatalog.Load(_font) : null;
            MarkFilterDirty();
        }

        public void Draw(
            NowFont font,
            bool showFontField,
            float listHeight,
            bool expandList,
            Action<NowFont> onFontChanged,
            Action<GUIContent> showNotification,
            Action repaint)
        {
            SetFont(font);

            if (showFontField)
                DrawHeader(onFontChanged, repaint);

            if (_font == null)
            {
                EditorGUILayout.HelpBox("Select a NowFont asset to inspect glyphs.", MessageType.Info);
                return;
            }

            if (_catalog == null)
                Reload();

            if (_catalog == null)
                return;

            DrawToolbar();
            DrawSelectedGlyph(showNotification);
            DrawGlyphList(listHeight, expandList, showNotification, repaint);
        }

        void DrawHeader(Action<NowFont> onFontChanged, Action repaint)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var nextFont = (NowFont)EditorGUILayout.ObjectField("Font", _font, typeof(NowFont), false);

                if (nextFont != _font)
                {
                    SetFont(nextFont);
                    onFontChanged?.Invoke(nextFont);
                    repaint?.Invoke();
                }

                if (GUILayout.Button("Reload", GUILayout.Width(72f)))
                {
                    Reload();
                    repaint?.Invoke();
                }
            }
        }

        void DrawToolbar()
        {
            EnsureFilteredGlyphs();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
                string nextSearch = GUILayout.TextField(_search ?? string.Empty, searchStyle, GUILayout.ExpandWidth(true));

                if (nextSearch != _search)
                {
                    _search = nextSearch;
                    MarkFilterDirty();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    {
                        _search = string.Empty;
                        MarkFilterDirty();
                        GUI.FocusControl(string.Empty);
                    }
                }

                bool nextSortByName = GUILayout.Toggle(_sortByName, "Name", EditorStyles.toolbarButton, GUILayout.Width(52f));

                if (nextSortByName != _sortByName)
                {
                    _sortByName = nextSortByName;
                    MarkFilterDirty();
                }

                GUILayout.Label($"{GetVisibleGlyphCount()}/{_catalog.glyphs.Length}", EditorStyles.miniLabel, GUILayout.Width(78f));
            }
        }

        void DrawSelectedGlyph(Action<GUIContent> showNotification)
        {
            if (!_hasSelectedGlyph)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect previewRect = GUILayoutUtility.GetRect(
                        SELECTED_PREVIEW_SIZE,
                        SELECTED_PREVIEW_SIZE,
                        GUILayout.Width(SELECTED_PREVIEW_SIZE),
                        GUILayout.Height(SELECTED_PREVIEW_SIZE));

                    DrawGlyphPreview(previewRect, _selectedGlyph, SELECTED_PREVIEW_FONT_SIZE);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(_selectedGlyph.displayName, EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Copy", GUILayout.Width(60f)))
                                CopyGlyph(_selectedGlyph, showNotification);
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Codepoint", _selectedGlyph.codepointLabel, GUILayout.MinWidth(160f));

                            string glyphIndexLabel = _selectedGlyph.glyphIndex >= 0
                                ? _selectedGlyph.glyphIndex.ToString()
                                : "unknown";

                            EditorGUILayout.LabelField("Glyph ID", glyphIndexLabel, GUILayout.MinWidth(120f));
                        }

                        if (_selectedGlyph.TryGetCharacter(out var character))
                            EditorGUILayout.SelectableLabel(character, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                }
            }
        }

        void DrawGlyphList(float listHeight, bool expandList, Action<GUIContent> showNotification, Action repaint)
        {
            EnsureFilteredGlyphs();

            int glyphCount = GetVisibleGlyphCount();

            if (glyphCount == 0)
            {
                EditorGUILayout.HelpBox("No glyphs match the current search.", MessageType.Info);
                return;
            }

            float resolvedListHeight = ResolveListHeight(listHeight, expandList, repaint);
            Rect listRect = GUILayoutUtility.GetRect(
                0f,
                100000f,
                resolvedListHeight,
                resolvedListHeight,
                GUILayout.ExpandWidth(true));

            float contentHeight = glyphCount * ROW_HEIGHT;
            float maxScrollY = Mathf.Max(0f, contentHeight - listRect.height);
            _scroll.y = Mathf.Clamp(_scroll.y, 0f, maxScrollY);

            if (Event.current.type == EventType.ScrollWheel && listRect.Contains(Event.current.mousePosition))
            {
                _scroll.y = Mathf.Clamp(_scroll.y + Event.current.delta.y * ROW_HEIGHT * 0.5f, 0f, maxScrollY);
                Event.current.Use();
                repaint?.Invoke();
            }

            bool showScrollbar = contentHeight > listRect.height;
            float scrollbarWidth = showScrollbar ? ScrollbarWidth : 0f;
            var contentRect = new Rect(listRect.x, listRect.y, Mathf.Max(1f, listRect.width - scrollbarWidth), listRect.height);

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / ROW_HEIGHT));
            int lastRow = Mathf.Min(
                glyphCount,
                Mathf.CeilToInt((_scroll.y + listRect.height) / ROW_HEIGHT) + 1);

            PrebakeVisibleGlyphs(firstRow, lastRow);

            GUI.BeginGroup(contentRect);

            for (int i = firstRow; i < lastRow; ++i)
            {
                var row = new Rect(0f, i * ROW_HEIGHT - _scroll.y, contentRect.width, ROW_HEIGHT);
                DrawGlyphRow(row, GetVisibleGlyph(i), i, showNotification, repaint);
            }

            GUI.EndGroup();

            if (showScrollbar)
            {
                var scrollbarRect = new Rect(contentRect.xMax, listRect.y, ScrollbarWidth, listRect.height);
                _scroll.y = GUI.VerticalScrollbar(scrollbarRect, _scroll.y, listRect.height, 0f, contentHeight);
            }
        }

        // Bakes every glyph visible in the list with a single EnsureGlyphs request instead of
        // letting each row preview trigger its own native compile while scrolling.
        void PrebakeVisibleGlyphs(int firstRow, int lastRow)
        {
            if (_font == null || Event.current.type != EventType.Repaint)
                return;

            _prebakeBuilder ??= new System.Text.StringBuilder();
            _prebakeBuilder.Length = 0;

            if (_hasSelectedGlyph && _selectedGlyph.TryGetCharacter(out var selectedCharacter))
                _prebakeBuilder.Append(selectedCharacter);

            for (int i = firstRow; i < lastRow; ++i)
            {
                if (GetVisibleGlyph(i).TryGetCharacter(out var character))
                    _prebakeBuilder.Append(character);
            }

            if (_prebakeBuilder.Length > 0)
                _font.EnsureGlyphs(_prebakeBuilder.ToString(), ROW_PREVIEW_FONT_SIZE);
        }

        void DrawGlyphRow(
            Rect row,
            NowFontGlyphInfo glyph,
            int index,
            Action<GUIContent> showNotification,
            Action repaint)
        {
            bool selected = _hasSelectedGlyph &&
                _selectedGlyph.codepoint == glyph.codepoint &&
                _selectedGlyph.glyphIndex == glyph.glyphIndex;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(row, selected ? RowSelected : index % 2 == 0 ? RowEven : RowOdd);

            var copyRect = new Rect(row.xMax - 62f, row.y + 11f, 54f, 22f);
            var selectRect = new Rect(row.x, row.y, row.width - 70f, row.height);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                selectRect.Contains(Event.current.mousePosition))
            {
                _selectedGlyph = glyph;
                _hasSelectedGlyph = true;
                Event.current.Use();
                repaint?.Invoke();
            }

            var previewRect = new Rect(row.x + 6f, row.y + 5f, PREVIEW_SIZE, PREVIEW_SIZE);
            DrawGlyphPreview(previewRect, glyph, ROW_PREVIEW_FONT_SIZE);

            var codeRect = new Rect(row.x + 50f, row.y + 5f, 86f, 16f);
            GUI.Label(codeRect, glyph.codepointLabel, EditorStyles.miniLabel);

            var glyphId = glyph.glyphIndex >= 0 ? $"gid:{glyph.glyphIndex}" : "gid:?";
            var glyphRect = new Rect(row.x + 50f, row.y + 23f, 86f, 16f);
            GUI.Label(glyphRect, glyphId, EditorStyles.miniLabel);

            var nameRect = new Rect(row.x + 138f, row.y + 6f, Mathf.Max(1f, copyRect.x - row.x - 146f), 18f);
            GUI.Label(nameRect, glyph.displayName, EditorStyles.label);

            if (glyph.TryGetCharacter(out var character))
            {
                var characterRect = new Rect(row.x + 138f, row.y + 24f, Mathf.Max(1f, copyRect.x - row.x - 146f), 16f);
                GUI.Label(characterRect, character, EditorStyles.miniLabel);
            }

            if (GUI.Button(copyRect, "Copy", EditorStyles.miniButton))
            {
                _selectedGlyph = glyph;
                _hasSelectedGlyph = true;
                CopyGlyph(glyph, showNotification);
            }
        }

        float ResolveListHeight(float minHeight, bool expandList, Action repaint)
        {
            if (!expandList)
                return minHeight;

            if (_expandedListHeight <= 0f)
                _expandedListHeight = minHeight;

            float height = Mathf.Max(minHeight, _expandedListHeight);

            if (Event.current.type == EventType.Repaint)
            {
                float nextHeight = GetExpandedListHeight(minHeight);

                if (Mathf.Abs(nextHeight - _expandedListHeight) > LIST_HEIGHT_EPSILON)
                {
                    _expandedListHeight = nextHeight;
                    repaint?.Invoke();
                }
            }

            return height;
        }

        static float GetExpandedListHeight(float minHeight)
        {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            float viewportBottom = GetCurrentViewportBottom();
            float remainingHeight = viewportBottom - lastRect.yMax - EXPANDED_LIST_BOTTOM_PADDING;
            return remainingHeight > minHeight ? remainingHeight : minHeight;
        }

        static float GetCurrentViewportBottom()
        {
            if (GUIClipVisibleRectProperty != null)
            {
                object value = GUIClipVisibleRectProperty.GetValue(null);

                if (value is Rect visibleRect && visibleRect.height > 0f)
                    return visibleRect.yMax;
            }

            return EditorWindow.focusedWindow != null
                ? EditorWindow.focusedWindow.position.height
                : 0f;
        }

        void DrawGlyphPreview(Rect rect, NowFontGlyphInfo glyph, float fontSize)
        {
            if (_font == null)
                return;

            if (Event.current.type != EventType.Repaint)
                return;

            using var preview = NowUIEditorGUI.Auto(rect, PreviewBackground);
            var panel = new Vector4(0f, 0f, preview.rect.width, preview.rect.height);
            Now.Rectangle(panel)
                .SetColor(PreviewBackground)
                .Draw();

            if (!_font.GetGlyph(glyph.codepoint, fontSize, out var drawGlyph))
                return;

            var bounds = GetSingleGlyphBounds(_font, drawGlyph, fontSize);
            float x = (preview.rect.width - bounds.z) * 0.5f - bounds.x;
            float y = (preview.rect.height - bounds.w) * 0.5f - bounds.y;

            Now.Text(new Vector4(x, y, preview.rect.width, preview.rect.height), _font)
                .SetFontSize(fontSize)
                .SetColor(PreviewColor)
                .Draw(drawGlyph);
        }

        static Vector4 GetSingleGlyphBounds(NowFont font, NowFontAtlasInfo.Glyph glyph, float fontSize)
        {
            float lineHeight = font.GetLineHeight() * fontSize;
            float left = glyph.planeBounds.left * fontSize;
            float right = glyph.planeBounds.right * fontSize;
            float top = lineHeight - glyph.planeBounds.top * fontSize;
            float bottom = lineHeight - glyph.planeBounds.bottom * fontSize;

            return new Vector4(left, top, right - left, bottom - top);
        }

        void CopyGlyph(NowFontGlyphInfo glyph, Action<GUIContent> showNotification)
        {
            if (!glyph.TryGetCharacter(out var character))
                return;

            EditorGUIUtility.systemCopyBuffer = character;
            showNotification?.Invoke(new GUIContent($"Copied {glyph.codepointLabel}"));
        }

        void EnsureFilteredGlyphs()
        {
            if (!_filterDirty || _catalog == null)
                return;

            _filteredGlyphs.Clear();

            if (!UsesFilteredGlyphs())
            {
                _filterDirty = false;
                return;
            }

            var glyphs = _catalog.glyphs;

            for (int i = 0; i < glyphs.Length; ++i)
            {
                if (glyphs[i].Matches(_search))
                    _filteredGlyphs.Add(glyphs[i]);
            }

            if (_sortByName)
            {
                _filteredGlyphs.Sort((a, b) =>
                {
                    int nameCompare = string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
                    return nameCompare != 0 ? nameCompare : a.codepoint.CompareTo(b.codepoint);
                });
            }

            _filterDirty = false;
        }

        bool UsesFilteredGlyphs()
        {
            return _sortByName || !string.IsNullOrWhiteSpace(_search);
        }

        int GetVisibleGlyphCount()
        {
            if (_catalog == null || _catalog.glyphs == null)
                return 0;

            return UsesFilteredGlyphs() ? _filteredGlyphs.Count : _catalog.glyphs.Length;
        }

        NowFontGlyphInfo GetVisibleGlyph(int index)
        {
            return UsesFilteredGlyphs() ? _filteredGlyphs[index] : _catalog.glyphs[index];
        }

        void MarkFilterDirty()
        {
            _filterDirty = true;
            _scroll = default;
        }
    }
}
