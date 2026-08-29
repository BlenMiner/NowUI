using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    internal enum NowEditorThemeComparisonElement
    {
        Section,
        NormalLabel,
        MutedLabel,
        Button,
        ToggleOff,
        ToggleOn,
        TextField,
        IntegerField,
        Popup,
        Slider,
        ProgressBar,
        TextArea,
        Foldout,
        ButtonStates,
        TextFieldStates,
        PopupStates,
        MenuStates,
        Scrollbar
    }

    internal readonly struct NowEditorThemeComparisonRow
    {
        public readonly NowEditorThemeComparisonElement element;
        public readonly string label;
        public readonly Rect rect;

        public NowEditorThemeComparisonRow(
            NowEditorThemeComparisonElement element,
            string label,
            Rect rect)
        {
            this.element = element;
            this.label = label;
            this.rect = rect;
        }
    }

    /// <summary>
    /// One local-coordinate specimen shared by both columns. Keeping geometry
    /// here makes the comparison about rendering, not two drifting layouts.
    /// </summary>
    internal static class NowEditorThemeComparisonSpecimen
    {
        public const float Height = 548f;
        public const float Margin = 14f;
        public const float LabelWidth = 112f;
        public const float LabelGap = 8f;
        public const float StandardRowHeight = 18f;

        public static List<NowEditorThemeComparisonRow> Build(float width)
        {
            var rows = new List<NowEditorThemeComparisonRow>(20);
            float y = 12f;

            AddSection(rows, width, ref y, "Typography");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.NormalLabel, "Label");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.MutedLabel, "Secondary");

            AddSection(rows, width, ref y, "Live controls");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.Button, "Button");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.ToggleOff, "Toggle off");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.ToggleOn, "Toggle on");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.TextField, "Text field");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.IntegerField, "Integer field");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.Popup, "Popup");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.Slider, "Slider");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.ProgressBar, "Progress");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.TextArea, "Text area", 44f);
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.Foldout, "Foldout");

            AddSection(rows, width, ref y, "Forced visual states");
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.ButtonStates, "Button states", 20f);
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.TextFieldStates, "Field states", 20f);
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.PopupStates, "Popup states", 20f);
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.MenuStates, "Menu rows", 36f);
            AddRow(rows, width, ref y, NowEditorThemeComparisonElement.Scrollbar, "Scrollbar", 15f);

            return rows;
        }

        public static Rect LabelRect(in NowEditorThemeComparisonRow row)
        {
            return new Rect(row.rect.x, row.rect.y, LabelWidth, row.rect.height);
        }

        public static Rect ControlRect(in NowEditorThemeComparisonRow row)
        {
            float x = row.rect.x + LabelWidth + LabelGap;
            return new Rect(x, row.rect.y, Mathf.Max(0f, row.rect.xMax - x), row.rect.height);
        }

        static void AddSection(
            List<NowEditorThemeComparisonRow> rows,
            float width,
            ref float y,
            string label)
        {
            if (rows.Count > 0)
                y += 7f;

            rows.Add(new NowEditorThemeComparisonRow(
                NowEditorThemeComparisonElement.Section,
                label,
                new Rect(Margin, y, Mathf.Max(0f, width - Margin * 2f), 20f)));
            y += 24f;
        }

        static void AddRow(
            List<NowEditorThemeComparisonRow> rows,
            float width,
            ref float y,
            NowEditorThemeComparisonElement element,
            string label,
            float height = StandardRowHeight)
        {
            rows.Add(new NowEditorThemeComparisonRow(
                element,
                label,
                new Rect(Margin, y, Mathf.Max(0f, width - Margin * 2f), height)));
            y += height + 3f;
        }
    }

    public sealed class NowEditorThemeComparisonWindow : EditorWindow
    {
        internal const string DefaultThemePath =
            "Assets/NowUI/Assets/Themes/UnityEditorDark.asset";

        const float ToolbarHeight = 22f;
        const float OuterMargin = 12f;
        const float ColumnGap = 12f;
        const float ColumnHeaderHeight = 38f;
        const float MinimumColumnWidth = 390f;

        static readonly string[] QualityOptions = { "Low", "Medium", "High", "Ultra" };
        static readonly string[] StateLabels = { "Normal", "Hover", "Pressed", "Focus" };
        static readonly Color NativeDarkBackground = new Color32(56, 56, 56, 255);

        [SerializeField] NowThemeAsset _theme;
        [SerializeField] Vector2 _scroll;
        [SerializeField] bool _showGuides;
        [SerializeField] bool _toggleOff;
        [SerializeField] bool _toggleOn = true;
        [SerializeField] string _text = "Player Camera";
        [SerializeField] string _notes = "Line one\nLine two";
        [SerializeField] int _integer = 12;
        [SerializeField] int _quality = 2;
        [SerializeField] float _slider = 0.68f;
        [SerializeField] bool _foldout = true;
        [SerializeField] int _buttonClicks;

        [MenuItem("Tools/NowUI/Editor Theme Comparison")]
        public static void Open()
        {
            Open(null);
        }

        public static void Open(NowThemeAsset theme)
        {
            var window = GetWindow<NowEditorThemeComparisonWindow>();
            window.titleContent = new GUIContent("Editor Theme Comparison");
            window.minSize = new Vector2(840f, 620f);
            window._theme = theme != null ? theme : LoadDefaultTheme();
            window.Show();
            window.Focus();
        }

        internal static NowThemeAsset LoadDefaultTheme()
        {
            return AssetDatabase.LoadAssetAtPath<NowThemeAsset>(DefaultThemePath);
        }

        internal void ConfigureForCapture(NowThemeAsset theme)
        {
            _theme = theme != null ? theme : LoadDefaultTheme();
            _scroll = Vector2.zero;
            _showGuides = false;
            _toggleOff = false;
            _toggleOn = true;
            _text = "Player Camera";
            _notes = "Line one\nLine two";
            _integer = 12;
            _quality = 2;
            _slider = 0.68f;
            _foldout = true;
            _buttonClicks = 0;
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Editor Theme Comparison");

            if (_theme == null)
                _theme = LoadDefaultTheme();
        }

        void OnGUI()
        {
            DrawToolbar();

            if (_theme == null)
            {
                EditorGUI.HelpBox(
                    new Rect(12f, ToolbarHeight + 12f, position.width - 24f, 42f),
                    $"Assign a NowUI theme. The default could not be loaded from {DefaultThemePath}.",
                    MessageType.Warning);
                return;
            }

            float viewportY = ToolbarHeight;
            float viewportHeight = Mathf.Max(0f, position.height - viewportY);
            Rect viewport = new Rect(0f, viewportY, position.width, viewportHeight);
            float canvasWidth = Mathf.Max(
                position.width,
                OuterMargin * 2f + MinimumColumnWidth * 2f + ColumnGap);
            float canvasHeight = OuterMargin * 2f + ColumnHeaderHeight +
                NowEditorThemeComparisonSpecimen.Height;
            Rect content = new Rect(0f, 0f, canvasWidth, canvasHeight);

            _scroll = GUI.BeginScrollView(viewport, _scroll, content);
            DrawComparison(content);
            GUI.EndScrollView();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(ToolbarHeight)))
            {
                GUILayout.Label("NowUI theme", GUILayout.Width(76f));
                _theme = (NowThemeAsset)EditorGUILayout.ObjectField(
                    _theme,
                    typeof(NowThemeAsset),
                    false,
                    GUILayout.Width(220f));

                if (GUILayout.Button("Reset values", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    ResetValues();

                _showGuides = GUILayout.Toggle(
                    _showGuides,
                    "Guides",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(58f));

                GUILayout.FlexibleSpace();

                if (!EditorGUIUtility.isProSkin)
                    GUILayout.Label("Switch Unity to the dark editor skin", EditorStyles.miniLabel);

                GUILayout.Label(
                    $"Unity {Application.unityVersion}  |  {EditorGUIUtility.pixelsPerPoint:0.##} px/pt",
                    EditorStyles.miniLabel);
            }
        }

        void DrawComparison(Rect content)
        {
            float columnWidth = CalculateColumnWidth(
                content.width,
                EditorGUIUtility.pixelsPerPoint);
            Rect nativeHeader = new Rect(OuterMargin, OuterMargin, columnWidth, ColumnHeaderHeight);
            Rect nowHeader = new Rect(nativeHeader.xMax + ColumnGap, OuterMargin, columnWidth, ColumnHeaderHeight);
            Rect nativePanel = new Rect(
                nativeHeader.x,
                nativeHeader.yMax,
                columnWidth,
                NowEditorThemeComparisonSpecimen.Height);
            Rect nowPanel = new Rect(
                nowHeader.x,
                nowHeader.yMax,
                columnWidth,
                NowEditorThemeComparisonSpecimen.Height);

            DrawColumnHeader(
                nativeHeader,
                "EditorGUI (native)",
                $"{EditorGUIUtility.singleLineHeight:0.#} pt rows • Unity dark skin");
            DrawColumnHeader(
                nowHeader,
                "NowUI (same rects)",
                $"{_theme.name} • ordinary NowUI controls");

            List<NowEditorThemeComparisonRow> rows =
                NowEditorThemeComparisonSpecimen.Build(columnWidth);

            if (Event.current.type == EventType.MouseDown)
            {
                if (nativePanel.Contains(Event.current.mousePosition))
                    NowFocus.Clear();
                else if (nowPanel.Contains(Event.current.mousePosition))
                    GUI.FocusControl(null);
            }

            GUI.BeginGroup(nativePanel);
            DrawNativeSpecimen(columnWidth, rows);
            GUI.EndGroup();

            Color background = _theme.GetColor(
                NowColorToken.Background,
                NativeDarkBackground);

            using (NowTheme.Scope(_theme))
            using (NowEditorGUI.Auto(nowPanel, background))
            {
                DrawNowUISpecimen(columnWidth, rows);
            }

            DrawPanelOutline(nativePanel);
            DrawPanelOutline(nowPanel);

            if (_showGuides)
                DrawGuides(nativePanel, nowPanel, rows);
        }

        internal static float CalculateColumnWidth(float contentWidth, float pixelsPerPoint)
        {
            pixelsPerPoint = Mathf.Max(1f, pixelsPerPoint);
            float available = Mathf.Max(0f, contentWidth - OuterMargin * 2f - ColumnGap);
            float physicalWidth = Mathf.Floor(available * pixelsPerPoint * 0.5f + 0.0001f);
            return physicalWidth / pixelsPerPoint;
        }

        internal static float CalculateNowColumnOrigin(float contentWidth, float pixelsPerPoint)
        {
            return OuterMargin + CalculateColumnWidth(contentWidth, pixelsPerPoint) + ColumnGap;
        }

        static void DrawColumnHeader(Rect rect, string title, string subtitle)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            GUI.Label(new Rect(rect.x + 10f, rect.y + 3f, rect.width - 20f, 18f), title, EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 19f, rect.width - 20f, 16f), subtitle, EditorStyles.miniLabel);
        }

        void DrawNativeSpecimen(
            float width,
            IReadOnlyList<NowEditorThemeComparisonRow> rows)
        {
            EditorGUI.DrawRect(
                new Rect(0f, 0f, width, NowEditorThemeComparisonSpecimen.Height),
                NativeDarkBackground);

            for (int i = 0; i < rows.Count; ++i)
            {
                NowEditorThemeComparisonRow row = rows[i];

                if (row.element == NowEditorThemeComparisonElement.Section)
                {
                    GUI.Label(row.rect, row.label, EditorStyles.boldLabel);
                    continue;
                }

                Rect labelRect = NowEditorThemeComparisonSpecimen.LabelRect(row);
                Rect controlRect = NowEditorThemeComparisonSpecimen.ControlRect(row);
                EditorGUI.LabelField(labelRect, row.label);

                switch (row.element)
                {
                    case NowEditorThemeComparisonElement.NormalLabel:
                        GUI.Label(controlRect, "The quick brown fox 0123");
                        break;
                    case NowEditorThemeComparisonElement.MutedLabel:
                        GUI.Label(controlRect, "Secondary editor text", EditorStyles.miniLabel);
                        break;
                    case NowEditorThemeComparisonElement.Button:
                        if (GUI.Button(controlRect, $"Apply ({_buttonClicks})"))
                            ++_buttonClicks;
                        break;
                    case NowEditorThemeComparisonElement.ToggleOff:
                        _toggleOff = EditorGUI.ToggleLeft(controlRect, "Enabled", _toggleOff);
                        break;
                    case NowEditorThemeComparisonElement.ToggleOn:
                        _toggleOn = EditorGUI.ToggleLeft(controlRect, "Enabled", _toggleOn);
                        break;
                    case NowEditorThemeComparisonElement.TextField:
                        _text = EditorGUI.TextField(controlRect, _text);
                        break;
                    case NowEditorThemeComparisonElement.IntegerField:
                        _integer = EditorGUI.IntField(controlRect, _integer);
                        break;
                    case NowEditorThemeComparisonElement.Popup:
                        _quality = EditorGUI.Popup(controlRect, _quality, QualityOptions);
                        break;
                    case NowEditorThemeComparisonElement.Slider:
                        _slider = GUI.HorizontalSlider(controlRect, _slider, 0f, 1f);
                        break;
                    case NowEditorThemeComparisonElement.ProgressBar:
                        EditorGUI.ProgressBar(controlRect, _slider, string.Empty);
                        break;
                    case NowEditorThemeComparisonElement.TextArea:
                        _notes = EditorGUI.TextArea(controlRect, _notes);
                        break;
                    case NowEditorThemeComparisonElement.Foldout:
                        _foldout = EditorGUI.Foldout(controlRect, _foldout, "Advanced", true);
                        break;
                    case NowEditorThemeComparisonElement.ButtonStates:
                        DrawNativeButtonStates(controlRect);
                        break;
                    case NowEditorThemeComparisonElement.TextFieldStates:
                        DrawNativeFieldStates(controlRect);
                        break;
                    case NowEditorThemeComparisonElement.PopupStates:
                        DrawNativePopupStates(controlRect);
                        break;
                    case NowEditorThemeComparisonElement.MenuStates:
                        DrawNativeMenuStates(controlRect);
                        break;
                    case NowEditorThemeComparisonElement.Scrollbar:
                        GUI.HorizontalScrollbar(controlRect, 0.32f, 0.28f, 0f, 1f);
                        break;
                }
            }
        }

        void DrawNowUISpecimen(
            float width,
            IReadOnlyList<NowEditorThemeComparisonRow> rows)
        {
            for (int i = 0; i < rows.Count; ++i)
            {
                NowEditorThemeComparisonRow row = rows[i];

                if (row.element == NowEditorThemeComparisonElement.Section)
                {
                    _theme.Text(ToNow(row.rect), NowTextStyle.BodyStrong).Draw(row.label);
                    continue;
                }

                Rect labelRect = NowEditorThemeComparisonSpecimen.LabelRect(row);
                Rect controlRect = NowEditorThemeComparisonSpecimen.ControlRect(row);
                _theme.Text(ToNow(labelRect), NowTextStyle.Body).Draw(row.label);
                NowRect nowRect = ToNow(controlRect);

                switch (row.element)
                {
                    case NowEditorThemeComparisonElement.NormalLabel:
                        _theme.Text(nowRect, NowTextStyle.Body).Draw("The quick brown fox 0123");
                        break;
                    case NowEditorThemeComparisonElement.MutedLabel:
                        _theme.Text(nowRect, NowTextStyle.Muted).Draw("Secondary editor text");
                        break;
                    case NowEditorThemeComparisonElement.Button:
                        if (Now.Button(nowRect, $"Apply ({_buttonClicks})").SetId(new NowId(1101)).Draw())
                            ++_buttonClicks;
                        break;
                    case NowEditorThemeComparisonElement.ToggleOff:
                        Now.Checkbox(nowRect, "Enabled").SetId(new NowId(1102)).Draw(ref _toggleOff);
                        break;
                    case NowEditorThemeComparisonElement.ToggleOn:
                        Now.Checkbox(nowRect, "Enabled").SetId(new NowId(1103)).Draw(ref _toggleOn);
                        break;
                    case NowEditorThemeComparisonElement.TextField:
                        Now.TextField(nowRect, new NowId(1104)).Draw(ref _text);
                        break;
                    case NowEditorThemeComparisonElement.IntegerField:
                        Now.IntField(nowRect, new NowId(1105)).Draw(ref _integer);
                        break;
                    case NowEditorThemeComparisonElement.Popup:
                        Now.Dropdown(nowRect, new NowId(1106), QualityOptions).Draw(ref _quality);
                        break;
                    case NowEditorThemeComparisonElement.Slider:
                        Now.Slider(nowRect, 0f, 1f).SetId(new NowId(1107)).Draw(ref _slider);
                        break;
                    case NowEditorThemeComparisonElement.ProgressBar:
                        Now.ProgressBar(nowRect, _slider).SetId(new NowId(1108)).Draw();
                        break;
                    case NowEditorThemeComparisonElement.TextArea:
                        Now.TextArea(nowRect, new NowId(1109)).Draw(ref _notes);
                        break;
                    case NowEditorThemeComparisonElement.Foldout:
                        Now.Foldout(nowRect, "Advanced", new NowId(1110)).Draw(ref _foldout);
                        break;
                    case NowEditorThemeComparisonElement.ButtonStates:
                        DrawNowUIButtonStates(nowRect);
                        break;
                    case NowEditorThemeComparisonElement.TextFieldStates:
                        DrawNowUIFieldStates(nowRect);
                        break;
                    case NowEditorThemeComparisonElement.PopupStates:
                        DrawNowUIPopupStates(nowRect);
                        break;
                    case NowEditorThemeComparisonElement.MenuStates:
                        DrawNowUIMenuStates(nowRect);
                        break;
                    case NowEditorThemeComparisonElement.Scrollbar:
                        DrawNowUIScrollbar(nowRect);
                        break;
                }
            }
        }

        static void DrawNativeButtonStates(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            GUIStyle style = GUI.skin.button;

            for (int i = 0; i < StateLabels.Length; ++i)
            {
                Rect cell = StateCell(rect, i, StateLabels.Length);
                style.Draw(
                    cell,
                    new GUIContent(StateLabels[i]),
                    i == 1,
                    i == 2,
                    false,
                    i == 3);
            }
        }

        static void DrawNativeFieldStates(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            GUIStyle style = EditorStyles.textField;
            Rect normal = StateCell(rect, 0, 2);
            Rect focused = StateCell(rect, 1, 2);
            style.Draw(normal, new GUIContent("Normal"), false, false, false, false);
            style.Draw(focused, new GUIContent("Focused"), false, false, false, true);
        }

        static void DrawNativePopupStates(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            GUIStyle style = EditorStyles.popup;
            Rect normal = StateCell(rect, 0, 2);
            Rect pressed = StateCell(rect, 1, 2);
            style.Draw(normal, new GUIContent("Normal"), false, false, false, false);
            style.Draw(pressed, new GUIContent("Pressed"), false, true, false, false);
        }

        static void DrawNativeMenuStates(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            GUIStyle style = GUI.skin.FindStyle("MenuItem") ?? EditorStyles.label;
            float height = Mathf.Min(16f, rect.height * 0.5f);
            Rect normal = new Rect(rect.x, rect.y, rect.width, height);
            Rect selected = new Rect(rect.x, rect.y + height, rect.width, height);
            style.Draw(normal, new GUIContent("Normal option"), false, false, false, false);
            style.Draw(selected, new GUIContent("Selected option"), true, false, true, false);
        }

        void DrawNowUIButtonStates(NowRect rect)
        {
            for (int i = 0; i < StateLabels.Length; ++i)
            {
                NowRect cell = ToNow(StateCell(ToRect(rect), i, StateLabels.Length));
                bool hovered = i == 1;
                bool held = i == 2;
                bool focused = i == 3;
                NowInteraction interaction = ForcedInteraction(cell, hovered || held, held);
                _theme.controlRenderer.DrawButton(new NowButtonRenderContext(
                    _theme,
                    cell,
                    StateLabels[i],
                    NowRectangleStyle.Accent,
                    NowTextStyle.Button,
                    interaction,
                    focused,
                    hovered || held ? 1f : 0f));
            }
        }

        void DrawNowUIFieldStates(NowRect rect)
        {
            NowRect normal = ToNow(StateCell(ToRect(rect), 0, 2));
            NowRect focused = ToNow(StateCell(ToRect(rect), 1, 2));
            _theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(_theme, normal, false));
            _theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(_theme, focused, true));
            NowControls.DrawLeftLabel(_theme, normal.Inset(4f, 0f, 4f, 0f), "Normal", NowTextStyle.Body);
            NowControls.DrawLeftLabel(_theme, focused.Inset(4f, 0f, 4f, 0f), "Focused", NowTextStyle.Body);
        }

        void DrawNowUIPopupStates(NowRect rect)
        {
            NowRect normal = ToNow(StateCell(ToRect(rect), 0, 2));
            NowRect pressed = ToNow(StateCell(ToRect(rect), 1, 2));
            _theme.controlRenderer.DrawDropdownField(new NowDropdownFieldRenderContext(
                _theme,
                normal,
                "Normal",
                false,
                default,
                false,
                0f));
            _theme.controlRenderer.DrawDropdownField(new NowDropdownFieldRenderContext(
                _theme,
                pressed,
                "Pressed",
                false,
                ForcedInteraction(pressed, true, true),
                false,
                1f));
        }

        void DrawNowUIMenuStates(NowRect rect)
        {
            float height = Mathf.Min(16f, rect.height * 0.5f);
            NowRect normal = new NowRect(rect.x, rect.y, rect.width, height);
            NowRect selected = new NowRect(rect.x, rect.y + height, rect.width, height);
            _theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                _theme,
                normal,
                "Normal option",
                false,
                default));
            _theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                _theme,
                selected,
                "Selected option",
                true,
                default));
        }

        void DrawNowUIScrollbar(NowRect rect)
        {
            NowScrollbarMetrics metrics = NowScrollbar.Calculate(
                NowScrollbarAxis.Horizontal,
                rect,
                28f,
                100f,
                32f,
                _theme.controlStyles.scrollbarMinThumbSize);
            _theme.controlRenderer.DrawScrollbar(new NowScrollbarRenderContext(
                _theme,
                NowScrollbarAxis.Horizontal,
                metrics,
                false,
                0f));
        }

        static Rect StateCell(Rect rect, int index, int count)
        {
            const float gap = 3f;
            float width = (rect.width - gap * (count - 1)) / count;
            return new Rect(rect.x + index * (width + gap), rect.y, width, rect.height);
        }

        static NowInteraction ForcedInteraction(NowRect rect, bool hovered, bool held)
        {
            Rect unityRect = ToRect(rect);
            bool hasPointer = hovered || held;
            return new NowInteraction(
                default,
                unityRect,
                NowPointerButton.Primary,
                hasPointer,
                hasPointer ? unityRect.center : default,
                default,
                default,
                hovered || held,
                false,
                held,
                false,
                false,
                held,
                false,
                false,
                false,
                false,
                false);
        }

        static void DrawPanelOutline(Rect rect)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0f, 0f, 0f, 0.5f);
            Handles.DrawAAPolyLine(
                1f,
                new Vector3(rect.x, rect.y),
                new Vector3(rect.xMax, rect.y),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.x, rect.yMax),
                new Vector3(rect.x, rect.y));
            Handles.EndGUI();
        }

        static void DrawGuides(
            Rect nativePanel,
            Rect nowPanel,
            IReadOnlyList<NowEditorThemeComparisonRow> rows)
        {
            Color line = new Color(0.15f, 0.65f, 1f, 0.22f);

            for (int i = 0; i < rows.Count; ++i)
            {
                if (rows[i].element == NowEditorThemeComparisonElement.Section)
                    continue;

                float nativeY = nativePanel.y + rows[i].rect.y;
                float nowY = nowPanel.y + rows[i].rect.y;
                EditorGUI.DrawRect(new Rect(nativePanel.x, nativeY, nativePanel.width, 1f), line);
                EditorGUI.DrawRect(new Rect(nowPanel.x, nowY, nowPanel.width, 1f), line);
            }
        }

        void ResetValues()
        {
            _toggleOff = false;
            _toggleOn = true;
            _text = "Player Camera";
            _notes = "Line one\nLine two";
            _integer = 12;
            _quality = 2;
            _slider = 0.68f;
            _foldout = true;
            _buttonClicks = 0;
            GUI.FocusControl(null);
            Repaint();
        }

        static NowRect ToNow(Rect rect)
        {
            return new NowRect(rect.x, rect.y, rect.width, rect.height);
        }

        static Rect ToRect(NowRect rect)
        {
            return new Rect(rect.x, rect.y, rect.width, rect.height);
        }
    }
}
