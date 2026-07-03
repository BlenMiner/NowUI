using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    public enum NowFileDialogMode
    {
        OpenFile,
        SaveFile,
        Directory
    }

    public readonly struct NowFileFilter
    {
        public readonly string name;

        public readonly string[] extensions;

        public NowFileFilter(string name, params string[] extensions)
        {
            this.name = name;
            this.extensions = extensions ?? Array.Empty<string>();
        }
    }

    [NowBuilder]
    public struct NowFilePicker
    {
        readonly NowFileDialogMode _mode;
        NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowFocusNavigation _navigation;
        NowFilePickerSettings _settings;

        sealed class BrowserEntry
        {
            public string path;
            public string name;
            public string icon;
            public string type;
            public bool directory;
            public bool parent;
        }

        sealed class FolderTreeEntry
        {
            public string path;
            public string key;
            public string name;
            public int depth;
            public bool current;
            public bool ancestor;
            public bool hasChildren;
            public bool expanded;
        }

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public NowFileDialogMode mode;
            public NowFilePickerSettings settings;
            public NowFileFilter[] filters = Array.Empty<NowFileFilter>();
            public readonly List<string> filterLabels = new List<string>(4);
            public readonly List<BrowserEntry> entries = new List<BrowserEntry>(32);
            public readonly List<FolderTreeEntry> treeEntries = new List<FolderTreeEntry>(32);
            public readonly HashSet<string> expandedTreePaths = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            public int id;
            public int areaId;
            public int pathFieldId;
            public int fileNameFieldId;
            public int filterId;
            public int scrollId;
            public int treeScrollId;
            public int entrySeed;
            public int treeSeed;
            public int selectButtonId;
            public int cancelButtonId;
            public int goButtonId;
            public int upButtonId;
            public int filterIndex;
            public string currentDirectory;
            public string currentDirectoryKey;
            public string parentDirectory;
            public string selectedDirectory;
            public string selectedDirectoryKey;
            public string directoryText;
            public string fileName;
            public string error;
            public string errorLabel;
            public bool actionError;
            public string pendingPath;
            public bool hasPendingPath;
            public string pendingTreeFocusKey;
            public bool entriesDirty;
            public bool treeDirty;
            public NowRect fieldRect;
            public NowRect popupRect;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(4);

        const int AreaSeed = 0x4e464141;
        const int PathFieldSeed = 0x4e464150;
        const int FileNameSeed = 0x4e464146;
        const int FilterSeed = 0x4e46414c;
        const int ScrollSeed = 0x4e464153;
        const int TreeScrollSeed = 0x4e464154;
        const int EntrySeed = 0x4e464145;
        const int TreeSeed = 0x4e464152;
        const int SelectSeed = 0x4e46414f;
        const int CancelSeed = 0x4e464143;
        const int GoSeed = 0x4e464147;
        const int UpSeed = 0x4e464155;

        internal NowFilePicker(NowFileDialogMode mode, NowId id, int site)
        {
            _mode = mode;
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _navigation = default;
            _settings = NowFilePickerSettings.Default(mode);
        }

        internal NowFilePicker(NowRect rect, NowFileDialogMode mode, NowId id, int site) : this(mode, id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowFilePicker SetOptions(NowLayoutOptions options) { _settings.options = options; return this; }

        public NowFilePicker SetWidth(float width) { _settings.options = _settings.options.SetWidth(width); return this; }

        public NowFilePicker SetHeight(float height)
        {
            _settings.options = _settings.options.SetHeight(height);
            _settings.fieldHeight = Mathf.Max(1f, height);
            return this;
        }

        public NowFilePicker SetStretchWidth(float weight = 1f) { _settings.options = _settings.options.SetStretchWidth(weight); return this; }

        public NowFilePicker SetId(NowId id) { _id = id; return this; }

        public NowFilePicker SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        public NowFilePicker SetTitle(string title) { _settings.title = title; return this; }

        public NowFilePicker SetPlaceholder(string placeholder) { _settings.placeholder = placeholder; return this; }

        public NowFilePicker SetStartDirectory(string directory) { _settings.startDirectory = directory; return this; }

        public NowFilePicker SetDefaultFileName(string fileName) { _settings.defaultFileName = fileName; return this; }

        public NowFilePicker SetDefaultExtension(string extension)
        {
            _settings.defaultExtension = NowFilePickerUtility.NormalizeExtension(extension);
            return this;
        }

        public NowFilePicker SetShowHidden(bool showHidden = true) { _settings.showHidden = showHidden; return this; }

        public NowFilePicker SetPopupSize(float width, float height)
        {
            _settings.popupWidth = Mathf.Max(220f, width);
            _settings.popupHeight = Mathf.Max(180f, height);
            return this;
        }

        public NowFilePicker SetFitToView(bool fitToView = true)
        {
            _settings.fitToView = fitToView;
            return this;
        }

        public NowFilePicker SetExtensions(params string[] extensions)
        {
            _settings.filters = new[] { new NowFileFilter(null, extensions) };
            SetDefaultExtensionFromFilters();
            return this;
        }

        public NowFilePicker SetFilter(string name, params string[] extensions)
        {
            _settings.filters = new[] { new NowFileFilter(name, extensions) };
            SetDefaultExtensionFromFilters();
            return this;
        }

        public NowFilePicker SetFilters(params NowFileFilter[] filters)
        {
            if (filters == null || filters.Length == 0)
            {
                _settings.filters = Array.Empty<NowFileFilter>();
                return this;
            }

            _settings.filters = new NowFileFilter[filters.Length];
            Array.Copy(filters, _settings.filters, filters.Length);
            SetDefaultExtensionFromFilters();
            return this;
        }

        public bool Draw(ref string path)
        {
            path ??= string.Empty;

            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);
            var state = GetState(id);
            bool changed = ApplyPending(state, ref path);

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = ResolveLineHeight(textStyle);
            Vector2 measured = renderer.MeasureTextField(theme, lineHeight);
            measured.x = Mathf.Max(measured.x, 260f);
            measured.y = Mathf.Max(measured.y, _settings.fieldHeight);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _settings.options, measured);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);
            bool wasOpen = open;

            if (interaction.clicked || submitted)
            {
                open = !open;

                if (open)
                    InitializeStateForOpen(state, id, path, _mode, _settings);
            }

            if (open && !wasOpen && string.IsNullOrEmpty(state.currentDirectory))
                InitializeStateForOpen(state, id, path, _mode, _settings);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            DrawField(theme, rect, path, _mode, _settings, open, focused, interaction.held, hoverT, lineHeight);

            if (open)
            {
                NowControlState.RequestRepaint();
                DeferPopup(theme, id, rect, _settings);
            }

            return changed;
        }

        void SetDefaultExtensionFromFilters()
        {
            SetDefaultExtensionFromFilters(ref _settings);
        }

        static void SetDefaultExtensionFromFilters(ref NowFilePickerSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.defaultExtension) || settings.filters == null || settings.filters.Length == 0)
                return;

            settings.defaultExtension = NowFilePickerUtility.FirstConcreteExtension(settings.filters, 0);
        }

        static PopupState GetState(int id)
        {
            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            return state;
        }

        static bool ApplyPending(PopupState state, ref string path)
        {
            if (!state.hasPendingPath)
                return false;

            state.hasPendingPath = false;
            string next = state.pendingPath ?? string.Empty;
            state.pendingPath = null;

            if (path == next)
                return false;

            path = next;
            return true;
        }

        static void InitializeStateForOpen(
            PopupState state,
            int id,
            string value,
            NowFileDialogMode mode,
            NowFilePickerSettings settings)
        {
            state.id = id;
            state.mode = mode;
            state.settings = settings;
            state.filters = NowFilePickerUtility.NormalizeFilters(settings.filters);
            RebuildFilterLabels(state);
            state.filterIndex = Mathf.Clamp(state.filterIndex, 0, Mathf.Max(0, state.filters.Length - 1));
            state.areaId = NowInput.CombineId(id, AreaSeed);
            state.pathFieldId = NowInput.CombineId(id, PathFieldSeed);
            state.fileNameFieldId = NowInput.CombineId(id, FileNameSeed);
            state.filterId = NowInput.CombineId(id, FilterSeed);
            state.scrollId = NowInput.CombineId(id, ScrollSeed);
            state.treeScrollId = NowInput.CombineId(id, TreeScrollSeed);
            state.entrySeed = NowInput.CombineId(id, EntrySeed);
            state.treeSeed = NowInput.CombineId(id, TreeSeed);
            state.selectButtonId = NowInput.CombineId(id, SelectSeed);
            state.cancelButtonId = NowInput.CombineId(id, CancelSeed);
            state.goButtonId = NowInput.CombineId(id, GoSeed);
            state.upButtonId = NowInput.CombineId(id, UpSeed);
            SetCurrentDirectory(state, ResolveInitialDirectory(value, settings));
            SetSelectedDirectory(state, null);
            state.directoryText = state.currentDirectory;
            state.fileName = ResolveInitialFileName(value, settings, mode);
            ClearError(state);
            state.entries.Clear();
            state.treeEntries.Clear();
            state.expandedTreePaths.Clear();
            state.pendingTreeFocusKey = null;
            MarkListsDirty(state);
            RevealFolderInTree(state, state.currentDirectory, focus: true, expandTarget: true);
        }

        static void SetCurrentDirectory(PopupState state, string directory)
        {
            state.currentDirectory = directory;
            state.currentDirectoryKey = TreePathKey(directory);
            state.parentDirectory = ParentDirectory(directory);
        }

        static void SetSelectedDirectory(PopupState state, string directory)
        {
            state.selectedDirectory = directory;
            state.selectedDirectoryKey = TreePathKey(directory);
        }

        /// <summary>
        /// The entry list and folder tree rebuild only when marked dirty —
        /// navigation, filter changes and expand/collapse invalidate them
        /// explicitly so the open popup never touches the disk per frame.
        /// </summary>
        static void MarkListsDirty(PopupState state)
        {
            state.entriesDirty = true;
            state.treeDirty = true;
        }

        static bool KeyEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
                string.Equals(left, right, StringComparison.CurrentCultureIgnoreCase);
        }

        static void RebuildFilterLabels(PopupState state)
        {
            state.filterLabels.Clear();

            for (int i = 0; i < state.filters.Length; ++i)
                state.filterLabels.Add(NowFilePickerUtility.FormatFilterLabel(state.filters[i]));
        }

        static string ResolveInitialDirectory(string value, NowFilePickerSettings settings)
        {
            if (!string.IsNullOrEmpty(value))
            {
                string valueDirectory = TryResolveValueDirectory(value);

                if (!string.IsNullOrEmpty(valueDirectory))
                    return valueDirectory;
            }

            if (!string.IsNullOrEmpty(settings.startDirectory))
            {
                string start = NowFilePickerUtility.TryGetFullPath(settings.startDirectory);

                if (!string.IsNullOrEmpty(start) && Directory.Exists(start))
                    return start;
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (!string.IsNullOrEmpty(documents) && Directory.Exists(documents))
                return documents;

            if (!string.IsNullOrEmpty(Application.persistentDataPath) && Directory.Exists(Application.persistentDataPath))
                return Application.persistentDataPath;

            return NowFilePickerUtility.TryGetFullPath(".") ?? string.Empty;
        }

        static string TryResolveValueDirectory(string value)
        {
            string full = NowFilePickerUtility.TryGetFullPath(value);

            if (string.IsNullOrEmpty(full))
                return null;

            if (Directory.Exists(full))
                return full;

            string directory;

            try
            {
                directory = Path.GetDirectoryName(full);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (string.IsNullOrEmpty(directory))
                return null;

            return Directory.Exists(directory) ? directory : null;
        }

        static string ResolveInitialFileName(string value, NowFilePickerSettings settings, NowFileDialogMode mode)
        {
            if (mode == NowFileDialogMode.Directory)
                return string.Empty;

            if (!string.IsNullOrEmpty(value))
            {
                string fileName = null;

                try
                {
                    if (!Directory.Exists(value))
                        fileName = Path.GetFileName(value);
                }
                catch (ArgumentException)
                {
                    fileName = null;
                }

                if (!string.IsNullOrEmpty(fileName))
                    return fileName;
            }

            if (mode == NowFileDialogMode.SaveFile)
                return string.IsNullOrEmpty(settings.defaultFileName) ? "Untitled" : settings.defaultFileName;

            return string.Empty;
        }

        static float ResolveLineHeight(NowText textStyle)
        {
            float lineHeight = textStyle.Measure("Ag").y;

            if (lineHeight > 0f)
                return lineHeight;

            return textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;
        }

        static void DrawField(
            NowThemeAsset theme,
            NowRect rect,
            string path,
            NowFileDialogMode mode,
            NowFilePickerSettings settings,
            bool open,
            bool focused,
            bool held,
            float hoverT,
            float lineHeight)
        {
            theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused || open));

            if (hoverT > 0f || held)
            {
                Color overlay = theme.GetColor(NowColorToken.Accent);
                overlay.a = Mathf.Lerp(0f, held ? 0.14f : 0.08f, hoverT);
                Now.Rectangle(rect.Inset(1f)).SetRadius(4f).SetColor(overlay).Draw();
            }

            NowRect inner = theme.controlRenderer.TextFieldInnerRect(theme, rect, lineHeight);
            string icon = FieldIcon(mode);
            float iconWidth = Mathf.Min(24f, inner.width);

            NowControls.DrawLeftLabel(theme, new NowRect(inner.x, rect.y, iconWidth, rect.height), icon, NowTextStyle.Body, Color.white);

            string display = string.IsNullOrEmpty(path)
                ? Placeholder(mode, settings)
                : path;
            var textStyle = string.IsNullOrEmpty(path) ? NowTextStyle.Muted : NowTextStyle.Body;
            float rightInset = 20f;
            var labelRect = new NowRect(
                inner.x + iconWidth + 6f,
                rect.y,
                Mathf.Max(0f, inner.width - iconWidth - rightInset - 8f),
                rect.height);
            NowControls.DrawLeftLabel(theme, labelRect, display, textStyle);

            DropdownArrowDraw.Draw(theme, new NowRect(rect.xMax - 20f, rect.y, 16f, rect.height), open);
        }

        static string FieldIcon(NowFileDialogMode mode)
        {
            switch (mode)
            {
                case NowFileDialogMode.SaveFile:
                    return "💾";
                case NowFileDialogMode.Directory:
                    return "📁";
                default:
                    return "📂";
            }
        }

        static string Placeholder(NowFileDialogMode mode, NowFilePickerSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.placeholder))
                return settings.placeholder;

            switch (mode)
            {
                case NowFileDialogMode.SaveFile:
                    return "Choose save path...";
                case NowFileDialogMode.Directory:
                    return "Choose directory...";
                default:
                    return "Choose file...";
            }
        }

        static void DeferPopup(NowThemeAsset theme, int id, NowRect field, NowFilePickerSettings settings)
        {
            var state = GetState(id);
            state.themeAsset = theme;
            state.fieldRect = Now.TransformScreenRect(field);
            state.popupRect = CalculatePopupRect(theme, field, settings);

            NowOverlay.BlockAllSurfaces(id);
            NowOverlay.Defer(state.popupRect, id, DrawPopup);
        }

        static NowRect CalculatePopupRect(NowThemeAsset theme, NowRect field, NowFilePickerSettings settings)
        {
            var rect = new NowRect(
                field.x,
                field.yMax + theme.controlStyles.dropdownPopupGap,
                Mathf.Max(field.width, settings.popupWidth),
                settings.popupHeight);

            return settings.fitToView ? NowOverlay.FitToView(rect) : rect;
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state))
                return;

            var theme = state.themeAsset;
            theme.controlRenderer.DrawPopupBackground(theme, state.popupRect, menu: false);
            DrawPopupContent(state);
            HandleDismiss(state);
        }

        static void DrawPopupContent(PopupState state)
        {
            RefreshEntries(state);

            float padding = state.settings.popupPadding;
            float spacing = state.settings.popupSpacing;
            bool hasFilter = state.mode != NowFileDialogMode.Directory && state.filters.Length > 1;
            bool hasFileName = state.mode != NowFileDialogMode.Directory;
            const float titleHeight = 30f;
            const float addressHeight = 32f;
            const float headerHeight = 24f;
            const float fileNameHeight = 30f;
            const float filterHeight = 30f;
            const float footerHeight = 34f;
            float fixedHeight = titleHeight + addressHeight + footerHeight;
            int fixedRows = 3;

            if (hasFileName)
            {
                fixedHeight += fileNameHeight;
                ++fixedRows;
            }

            if (hasFilter)
            {
                fixedHeight += filterHeight;
                ++fixedRows;
            }

            float browserHeight = state.popupRect.height - padding * 2f - fixedHeight - spacing * fixedRows;

            if (browserHeight < headerHeight + 96f)
                browserHeight = headerHeight + 96f;

            using (NowLayout.Area(state.areaId, state.popupRect, spacing: spacing, padding: padding, alignItems: NowLayoutAlign.Start))
            {
                using (NowLayout.Horizontal(height: titleHeight, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 6f))
                {
                    NowLayout.Label(NowControls.Text(state.themeAsset, NowTextStyle.Title), Title(state.mode, state.settings))
                        .SetStretchWidth()
                        .Draw();
                }

                using (NowLayout.Horizontal(height: addressHeight, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 6f))
                {
                    string parent = state.parentDirectory;

                    if (!string.IsNullOrEmpty(parent))
                    {
                        if (NowLayout.Button("Up").SetId(state.upButtonId).SetStyle(NowRectangleStyle.Outline).SetWidth(48f).Draw())
                            NavigateTo(state, parent);
                    }
                    else
                    {
                        NowLayout.Label("").SetWidth(48f).Draw();
                    }

                    if (NowLayout.TextField(state.pathFieldId)
                        .SetStretchWidth()
                        .SetPlaceholder("Address")
                        .Draw(ref state.directoryText))
                    {
                        SetSelectedDirectory(state, null);
                        ClearError(state);
                    }

                    if (NowLayout.Button("Go").SetId(state.goButtonId).SetStyle(NowRectangleStyle.Outline).SetWidth(44f).Draw())
                        NavigateTo(state, state.directoryText);
                }

                NowRect browserRect = NowLayout.Rect(height: browserHeight, stretchWidth: true);
                DrawBrowser(state, browserRect, headerHeight);

                if (hasFileName)
                {
                    using (NowLayout.Horizontal(height: fileNameHeight, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
                    {
                        NowLayout.Label("File name:").SetWidth(78f).Draw();
                        if (NowLayout.TextField(state.fileNameFieldId)
                            .SetStretchWidth()
                            .SetPlaceholder("File name...")
                            .Draw(ref state.fileName))
                        {
                            SetSelectedDirectory(state, null);
                            ClearError(state);
                        }
                    }
                }

                if (hasFilter)
                {
                    using (NowLayout.Horizontal(height: filterHeight, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
                    {
                        NowLayout.Label("File type:").SetWidth(78f).Draw();
                        int filter = state.filterIndex;

                        if (NowLayout.Dropdown(state.filterId, state.filterLabels).SetStretchWidth().Draw(ref filter))
                        {
                            state.filterIndex = Mathf.Clamp(filter, 0, state.filters.Length - 1);
                            SetSelectedDirectory(state, null);
                            state.entriesDirty = true;
                            ClearError(state);
                        }
                    }
                }

                using (NowLayout.Horizontal(height: footerHeight, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
                {
                    if (!string.IsNullOrEmpty(state.error))
                    {
                        NowLayout.Label(NowControls.Text(state.themeAsset, NowTextStyle.Body), state.errorLabel)
                            .SetStretchWidth()
                            .SetColor(new Color(0.86f, 0.24f, 0.24f))
                            .Draw();
                    }
                    else
                    {
                        NowLayout.FlexibleSpace();
                    }

                    if (NowLayout.Button(ActionLabel(state.mode)).SetId(state.selectButtonId).SetStyle(NowRectangleStyle.Accent).Draw())
                        CommitAction(state);

                    if (NowLayout.Button("Cancel").SetId(state.cancelButtonId).SetStyle(NowRectangleStyle.Surface).SetWidth(78f).Draw())
                        NowControlState.Get<bool>(state.id) = false;
                }
            }
        }

        static string Title(NowFileDialogMode mode, NowFilePickerSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.title))
                return settings.title;

            switch (mode)
            {
                case NowFileDialogMode.SaveFile:
                    return "Save File";
                case NowFileDialogMode.Directory:
                    return "Select Directory";
                default:
                    return "Open File";
            }
        }

        static string ActionLabel(NowFileDialogMode mode)
        {
            switch (mode)
            {
                case NowFileDialogMode.SaveFile:
                    return "Save";
                case NowFileDialogMode.Directory:
                    return "Select Folder";
                default:
                    return "Open";
            }
        }

        static void DrawBrowser(PopupState state, NowRect rect, float headerHeight)
        {
            bool showTree = rect.width >= 560f;
            float treeWidth = showTree ? Mathf.Clamp(rect.width * 0.30f, 168f, 220f) : 0f;
            float gap = showTree ? 8f : 0f;
            float listX = rect.x;
            float listWidth = rect.width;

            if (showTree)
            {
                var treeRect = new NowRect(rect.x, rect.y, treeWidth, rect.height);
                DrawFolderTree(state, treeRect, headerHeight);
                listX = treeRect.xMax + gap;
                listWidth = Mathf.Max(0f, rect.xMax - listX);
            }

            var headerRect = new NowRect(listX, rect.y, listWidth, headerHeight);
            var listRect = new NowRect(listX, rect.y + headerHeight, listWidth, Mathf.Max(0f, rect.height - headerHeight));

            DrawListHeader(state.themeAsset, headerRect);
            DrawListFrame(state.themeAsset, listRect);

            using (Now.ScrollView(listRect.Inset(1f), state.scrollId).Begin())
                DrawEntries(state);
        }

        static void DrawFolderTree(PopupState state, NowRect rect, float headerHeight)
        {
            var theme = state.themeAsset;
            Color surface = theme.GetColor(NowColorToken.Surface);
            Color surfaceMuted = theme.GetColor(NowColorToken.SurfaceMuted);
            Color border = theme.GetColor(NowColorToken.Border);
            Color muted = theme.GetColor(NowColorToken.TextMuted);

            Now.Rectangle(rect)
                .SetRadius(4f)
                .SetColor(surface)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();

            var headerRect = new NowRect(rect.x, rect.y, rect.width, headerHeight);
            Now.Rectangle(headerRect)
                .SetRadius(4f, 4f, 0f, 0f)
                .SetColor(surfaceMuted)
                .Draw();

            NowControls.DrawLeftLabel(theme, headerRect.Inset(8f, 0f), "Folders", NowTextStyle.Muted, muted);

            var contentRect = new NowRect(rect.x, rect.y + headerHeight, rect.width, Mathf.Max(0f, rect.height - headerHeight));
            BuildFolderTree(state);

            using (Now.ScrollView(contentRect.Inset(1f), state.treeScrollId).Begin())
                DrawFolderTreeEntries(state);
        }

        static void DrawFolderTreeEntries(PopupState state)
        {
            if (state.treeEntries.Count == 0)
            {
                NowLayout.Space(8f);
                NowLayout.Label(NowControls.Text(state.themeAsset, NowTextStyle.Muted), "No folders")
                    .SetStretchWidth()
                    .Draw();
                return;
            }

            for (int i = 0; i < state.treeEntries.Count; ++i)
            {
                NowRect row = NowLayout.Rect(height: 26f, stretchWidth: true);
                DrawFolderTreeRow(state, row, state.treeEntries[i], i);
            }
        }

        static void DrawFolderTreeRow(PopupState state, NowRect row, FolderTreeEntry entry, int index)
        {
            var theme = state.themeAsset;
            int id = FolderTreeRowId(state, entry, index);
            bool revealFocus = KeyEquals(state.pendingTreeFocusKey, entry.key);

            if (revealFocus && !NowInput.isPassive)
            {
                NowFocus.Focus(id);
                state.pendingTreeFocusKey = null;
            }

            var interaction = NowControls.Interact(id, row, out bool focused, out bool submitted);
            bool selected = entry.current || KeyEquals(state.selectedDirectoryKey, entry.key);
            NowRect visual = row.Inset(2f, 1f);

            if (selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(new Color(accent.r, accent.g, accent.b, entry.current ? 0.20f : 0.12f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, focused ? 0.70f : entry.current ? 0.52f : 0.34f))
                    .Draw();
            }
            else if (focused)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.07f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.42f))
                    .Draw();
            }
            else if (interaction.hovered || interaction.held)
            {
                Color mutedSurface = theme.GetColor(NowColorToken.SurfaceMuted);
                mutedSurface = NowControls.StateColor(theme, mutedSurface, 1f, interaction.held);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(mutedSurface)
                    .Draw();
            }

            Color muted = theme.GetColor(NowColorToken.TextMuted);
            float indent = Mathf.Min(Mathf.Max(0, entry.depth) * 14f, 84f);
            var toggleRect = new NowRect(row.x + 5f + indent, row.y, 16f, row.height);
            var iconRect = new NowRect(toggleRect.xMax + 2f, row.y, 20f, row.height);
            var nameRect = new NowRect(iconRect.xMax + 4f, row.y, Mathf.Max(0f, row.xMax - iconRect.xMax - 10f), row.height);
            Color text = entry.ancestor && !entry.current
                ? muted
                : theme.GetColor(NowColorToken.Text);

            if (entry.hasChildren)
                NowControls.DrawLeftLabel(theme, toggleRect, entry.expanded ? "▾" : "▸", NowTextStyle.Muted, muted);

            NowControls.DrawLeftLabel(theme, iconRect, entry.current ? "📂" : "📁", NowTextStyle.Body, Color.white);
            NowControls.DrawLeftLabel(theme, nameRect, entry.name, NowTextStyle.Body, text);

            if (interaction.clicked && entry.hasChildren && toggleRect.Contains(interaction.pointerPosition))
            {
                SetFolderTreeExpanded(state, entry.path, !entry.expanded);
                NowControlState.RequestRepaint();
                return;
            }

            if ((interaction.clicked || submitted) && !KeyEquals(state.currentDirectoryKey, entry.key))
                NavigateTo(state, entry.path);
        }

        static void DrawListHeader(NowThemeAsset theme, NowRect rect)
        {
            Color surfaceMuted = theme.GetColor(NowColorToken.SurfaceMuted);
            Color border = theme.GetColor(NowColorToken.Border);
            Color muted = theme.GetColor(NowColorToken.TextMuted);
            float typeWidth = TypeColumnWidth(rect);

            Now.Rectangle(rect)
                .SetRadius(4f, 4f, 0f, 0f)
                .SetColor(surfaceMuted)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();

            var nameRect = new NowRect(rect.x + 34f, rect.y, Mathf.Max(0f, rect.width - typeWidth - 42f), rect.height);
            var typeRect = new NowRect(rect.xMax - typeWidth - 8f, rect.y, typeWidth, rect.height);

            NowControls.DrawLeftLabel(theme, nameRect, "Name", NowTextStyle.Muted, muted);
            NowControls.DrawLeftLabel(theme, typeRect, "Type", NowTextStyle.Muted, muted);
        }

        static void DrawListFrame(NowThemeAsset theme, NowRect rect)
        {
            Color surface = theme.GetColor(NowColorToken.Surface);
            Color border = theme.GetColor(NowColorToken.Border);

            Now.Rectangle(rect)
                .SetRadius(0f, 0f, 4f, 4f)
                .SetColor(surface)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();
        }

        static float TypeColumnWidth(NowRect rect)
        {
            return rect.width >= 430f ? 118f : 92f;
        }

        static void DrawEntries(PopupState state)
        {
            if (state.entries.Count == 0)
            {
                NowLayout.Space(8f);
                NowLayout.Label(NowControls.Text(state.themeAsset, NowTextStyle.Muted), "No matching items")
                    .SetStretchWidth()
                    .Draw();
                return;
            }

            for (int i = 0; i < state.entries.Count; ++i)
            {
                var entry = state.entries[i];
                NowRect row = NowLayout.Rect(height: 28f, stretchWidth: true);
                DrawEntryRow(state, row, entry, i);
            }
        }

        static void DrawEntryRow(PopupState state, NowRect row, BrowserEntry entry, int index)
        {
            var theme = state.themeAsset;
            int id = NowInput.CombineId(state.entrySeed, index + 1);
            var interaction = NowInput.Interact(id, row);
            bool selected = IsSelectedEntry(state, entry);
            NowRect visual = row.Inset(2f, 1f);

            if (selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.18f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.48f))
                    .Draw();
            }
            else if (interaction.hovered || interaction.held)
            {
                Color mutedSurface = theme.GetColor(NowColorToken.SurfaceMuted);
                mutedSurface = NowControls.StateColor(theme, mutedSurface, 1f, interaction.held);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(mutedSurface)
                    .Draw();
            }

            float typeWidth = TypeColumnWidth(row);
            var iconRect = new NowRect(row.x + 9f, row.y, 22f, row.height);
            var nameRect = new NowRect(iconRect.xMax + 6f, row.y, Mathf.Max(0f, row.width - typeWidth - 46f), row.height);
            var typeRect = new NowRect(row.xMax - typeWidth - 8f, row.y, typeWidth, row.height);
            Color text = theme.GetColor(NowColorToken.Text);
            Color muted = selected
                ? text
                : theme.GetColor(NowColorToken.TextMuted);

            NowControls.DrawLeftLabel(theme, iconRect, string.IsNullOrEmpty(entry.icon) ? "📄" : entry.icon, NowTextStyle.Body, Color.white);
            NowControls.DrawLeftLabel(theme, nameRect, entry.name, NowTextStyle.Body, text);
            NowControls.DrawLeftLabel(theme, typeRect, entry.type, NowTextStyle.Muted, muted);

            if (!interaction.clicked)
                return;

            int streak = NowControlState.ClickStreak(id, true, interaction.pointerPosition);

            if (entry.directory)
            {
                if (entry.parent || streak >= 2)
                {
                    NavigateTo(state, entry.path);
                    return;
                }

                SetSelectedDirectory(state, entry.path);
                RevealFolderInTree(state, entry.path, focus: true, expandTarget: false);

                if (state.mode == NowFileDialogMode.OpenFile)
                    state.fileName = string.Empty;

                ClearError(state);
                NowControlState.RequestRepaint();
                return;
            }

            if (state.mode != NowFileDialogMode.Directory)
            {
                state.fileName = Path.GetFileName(entry.path);
                SetSelectedDirectory(state, null);
                ClearError(state);

                if ((state.mode == NowFileDialogMode.OpenFile || state.mode == NowFileDialogMode.SaveFile) && streak >= 2)
                {
                    CommitAction(state);
                    return;
                }

                NowControlState.RequestRepaint();
            }
        }

        static bool IsSelectedEntry(PopupState state, BrowserEntry entry)
        {
            if (entry.directory)
                return !string.IsNullOrEmpty(state.selectedDirectory) &&
                    string.Equals(state.selectedDirectory, entry.path, StringComparison.CurrentCultureIgnoreCase);

            if (state.mode == NowFileDialogMode.Directory || string.IsNullOrEmpty(state.fileName))
                return false;

            return string.Equals(state.fileName, entry.name, StringComparison.CurrentCultureIgnoreCase);
        }

        static void BuildFolderTree(PopupState state)
        {
            if (!state.treeDirty)
                return;

            state.treeDirty = false;
            state.treeEntries.Clear();

            string current = NowFilePickerUtility.TryGetFullPath(state.currentDirectory);

            if (string.IsNullOrEmpty(current) || !Directory.Exists(current))
                return;

            var chain = new List<string>(8);
            BuildDirectoryChain(current, chain);

            if (chain.Count == 0)
                return;

            var visited = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            AddFolderTreeBranch(state, chain[0], 0, current, TreePathKey(current), visited);
        }

        static void BuildDirectoryChain(string directory, List<string> chain)
        {
            chain.Clear();
            string current = NowFilePickerUtility.TryGetFullPath(directory);

            if (string.IsNullOrEmpty(current))
                return;

            var reversed = new List<string>(8);

            while (!string.IsNullOrEmpty(current))
            {
                reversed.Add(current);
                string parent = ParentDirectory(current);

                if (string.IsNullOrEmpty(parent) || PathEquals(parent, current))
                    break;

                current = parent;
            }

            for (int i = reversed.Count - 1; i >= 0; --i)
                chain.Add(reversed[i]);
        }

        static void AddFolderTreeBranch(
            PopupState state,
            string path,
            int depth,
            string currentDirectory,
            string currentKey,
            HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(path) || depth > 64)
                return;

            string key = TreePathKey(path);

            if (string.IsNullOrEmpty(key) || !visited.Add(key))
                return;

            bool expanded = state.expandedTreePaths.Contains(key);
            string[] children = expanded
                ? GetVisibleDirectories(path, state.settings.showHidden)
                : Array.Empty<string>();
            bool hasChildren = expanded
                ? children.Length > 0
                : HasVisibleDirectory(path, state.settings.showHidden);
            expanded &= children.Length > 0;
            bool current = KeyEquals(key, currentKey);
            bool ancestor = !current && IsAncestorDirectory(path, currentDirectory);

            state.treeEntries.Add(new FolderTreeEntry
            {
                path = path,
                key = key,
                name = NowFilePickerUtility.DisplayName(path),
                depth = Mathf.Max(0, depth),
                current = current,
                ancestor = ancestor,
                hasChildren = hasChildren,
                expanded = expanded
            });

            if (!expanded)
                return;

            for (int i = 0; i < children.Length; ++i)
                AddFolderTreeBranch(state, children[i], depth + 1, currentDirectory, currentKey, visited);
        }

        static string[] GetVisibleDirectories(string directory, bool showHidden)
        {
            try
            {
                var directories = Directory.GetDirectories(directory);
                Array.Sort(directories, StringComparer.CurrentCultureIgnoreCase);

                if (showHidden)
                    return directories;

                int write = 0;

                for (int read = 0; read < directories.Length; ++read)
                {
                    if (NowFilePickerUtility.IsHidden(directories[read]))
                        continue;

                    directories[write++] = directories[read];
                }

                if (write == directories.Length)
                    return directories;

                var visible = new string[write];
                Array.Copy(directories, visible, write);
                return visible;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                // The main list owns user-visible directory errors; keep the tree best-effort.
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Early-exit probe for the collapsed-node expand toggle, so building the
        /// tree never fully enumerates directories that are not expanded.
        /// </summary>
        static bool HasVisibleDirectory(string directory, bool showHidden)
        {
            try
            {
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    if (showHidden || !NowFilePickerUtility.IsHidden(child))
                        return true;
                }

                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return false;
            }
        }

        static void RevealFolderInTree(PopupState state, string directory, bool focus, bool expandTarget)
        {
            string full = NowFilePickerUtility.TryGetFullPath(directory);

            if (string.IsNullOrEmpty(full) || !Directory.Exists(full))
                return;

            var chain = new List<string>(8);
            BuildDirectoryChain(full, chain);
            int expandCount = expandTarget ? chain.Count : Mathf.Max(0, chain.Count - 1);

            for (int i = 0; i < expandCount; ++i)
                SetFolderTreeExpanded(state, chain[i], true);

            if (focus)
                state.pendingTreeFocusKey = TreePathKey(full);
        }

        static void SetFolderTreeExpanded(PopupState state, string path, bool expanded)
        {
            string key = TreePathKey(path);

            if (string.IsNullOrEmpty(key))
                return;

            bool changed = expanded
                ? state.expandedTreePaths.Add(key)
                : state.expandedTreePaths.Remove(key);

            if (changed)
                state.treeDirty = true;
        }

        static int FolderTreeRowId(PopupState state, FolderTreeEntry entry, int fallbackIndex)
        {
            return string.IsNullOrEmpty(entry.key)
                ? NowInput.CombineId(state.treeSeed, fallbackIndex + 1)
                : NowInput.GetId(state.treeSeed, entry.key);
        }

        static string TreePathKey(string path)
        {
            return string.IsNullOrEmpty(path) ? null : NormalizePathForCompare(path);
        }

        static void RefreshEntries(PopupState state)
        {
            if (!state.entriesDirty)
                return;

            state.entriesDirty = false;
            state.entries.Clear();

            if (string.IsNullOrEmpty(state.currentDirectory) || !Directory.Exists(state.currentDirectory))
            {
                SetErrorText(state, "Directory not found");
                return;
            }

            try
            {
                string parent = state.parentDirectory;

                if (!string.IsNullOrEmpty(parent))
                {
                    state.entries.Add(new BrowserEntry
                    {
                        path = parent,
                        name = "...",
                        icon = "📁",
                        type = "Folder",
                        directory = true,
                        parent = true
                    });
                }

                var directories = Directory.GetDirectories(state.currentDirectory);
                Array.Sort(directories, StringComparer.CurrentCultureIgnoreCase);

                for (int i = 0; i < directories.Length; ++i)
                {
                    if (!state.settings.showHidden && NowFilePickerUtility.IsHidden(directories[i]))
                        continue;

                    state.entries.Add(new BrowserEntry
                    {
                        path = directories[i],
                        name = NowFilePickerUtility.DisplayName(directories[i]),
                        icon = "📁",
                        type = "Folder",
                        directory = true
                    });
                }

                if (state.mode == NowFileDialogMode.Directory)
                {
                    if (!state.actionError)
                        SetErrorText(state, null);

                    return;
                }

                var files = Directory.GetFiles(state.currentDirectory);
                Array.Sort(files, StringComparer.CurrentCultureIgnoreCase);
                var filter = state.filters.Length > 0 ? state.filters[Mathf.Clamp(state.filterIndex, 0, state.filters.Length - 1)] : default;

                for (int i = 0; i < files.Length; ++i)
                {
                    if (!state.settings.showHidden && NowFilePickerUtility.IsHidden(files[i]))
                        continue;

                    if (state.filters.Length > 0 && !NowFilePickerUtility.MatchesFilter(files[i], filter))
                        continue;

                    state.entries.Add(new BrowserEntry
                    {
                        path = files[i],
                        name = Path.GetFileName(files[i]),
                        icon = FileIcon(files[i]),
                        type = FileTypeLabel(files[i]),
                        directory = false
                    });
                }

                if (!state.actionError)
                    SetErrorText(state, null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                SetErrorText(state, ex.Message);
                state.actionError = false;
            }
        }

        static string FileTypeLabel(string path)
        {
            string extension;

            try
            {
                extension = Path.GetExtension(path);
            }
            catch (ArgumentException)
            {
                extension = null;
            }

            if (string.IsNullOrEmpty(extension))
                return "File";

            return extension.TrimStart('.').ToUpperInvariant() + " File";
        }

        static string FileIcon(string path)
        {
            string extension;

            try
            {
                extension = NowFilePickerUtility.NormalizeExtension(Path.GetExtension(path));
            }
            catch (ArgumentException)
            {
                extension = null;
            }

            return extension switch
            {
                "png" or "jpg" or "jpeg" or "gif" or "bmp" or "webp" or "tga" or "psd" or "svg"
                    => "🖼️",
                "mp3" or "wav" or "ogg" or "flac" or "m4a" or "aiff"
                    => "🎵",
                "mp4" or "mov" or "avi" or "mkv" or "webm"
                    => "🎞️",
                "zip" or "rar" or "7z" or "tar" or "gz" or "unitypackage"
                    => "📦",
                "cs" or "shader" or "hlsl" or "cginc" or "js" or "ts" or "html" or "css" or "py" or "java" or "cpp"
                    or "h"
                    => "💻",
                "json" or "yaml" or "yml" or "xml" or "md" or "txt" or "log" or "csv" or "ini"
                    => "📝",
                "pdf"
                    => "📕",
                "ttf" or "otf" or "woff" or "woff2"
                    => "🔤",
                "unity" or "prefab" or "asset" or "mat" or "controller" or "anim"
                    => "🎮",
                _ => "📄"
            };
        }

        static bool PathEquals(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;

            string normalizedLeft = NormalizePathForCompare(left);
            string normalizedRight = NormalizePathForCompare(right);

            return string.Equals(normalizedLeft, normalizedRight, StringComparison.CurrentCultureIgnoreCase);
        }

        static bool IsAncestorDirectory(string ancestor, string directory)
        {
            if (string.IsNullOrEmpty(ancestor) || string.IsNullOrEmpty(directory) || PathEquals(ancestor, directory))
                return false;

            string ancestorPath = NormalizePathForCompare(ancestor);
            string directoryPath = NormalizePathForCompare(directory);

            if (string.IsNullOrEmpty(ancestorPath) || string.IsNullOrEmpty(directoryPath))
                return false;

            try
            {
                string ancestorRoot = Path.GetPathRoot(ancestorPath);
                string directoryRoot = Path.GetPathRoot(directoryPath);

                if (!string.Equals(ancestorRoot, directoryRoot, StringComparison.CurrentCultureIgnoreCase))
                    return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            ancestorPath = TrimTrailingSeparatorsPreserveRoot(ancestorPath);
            directoryPath = TrimTrailingSeparatorsPreserveRoot(directoryPath);

            if (ancestorPath.Length == 0 || directoryPath.Length <= ancestorPath.Length)
                return false;

            if (IsRootPath(ancestorPath))
                return directoryPath.StartsWith(ancestorPath, StringComparison.CurrentCultureIgnoreCase);

            if (!directoryPath.StartsWith(ancestorPath, StringComparison.CurrentCultureIgnoreCase))
                return false;

            char next = directoryPath[ancestorPath.Length];
            return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
        }

        static string TrimTrailingSeparatorsPreserveRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string root;

            try
            {
                root = Path.GetPathRoot(path);
            }
            catch (ArgumentException)
            {
                root = null;
            }

            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.IsNullOrEmpty(root))
                return trimmed;

            string trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.IsNullOrEmpty(trimmed) ||
                string.Equals(trimmed, trimmedRoot, StringComparison.CurrentCultureIgnoreCase))
            {
                return root;
            }

            return trimmed;
        }

        static bool IsRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                string root = Path.GetPathRoot(path);
                return !string.IsNullOrEmpty(root) &&
                    string.Equals(path, root, StringComparison.CurrentCultureIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        static string NormalizePathForCompare(string path)
        {
            string full = NowFilePickerUtility.TryGetFullPath(path) ?? path;
            string root;

            try
            {
                root = Path.GetPathRoot(full);
            }
            catch (ArgumentException)
            {
                root = null;
            }

            if (!string.IsNullOrEmpty(root))
            {
                string trimmedFull = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(trimmedFull, trimmedRoot, StringComparison.CurrentCultureIgnoreCase))
                    return root;

                full = trimmedFull;
            }

            return full;
        }

        static string ParentDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return null;

            try
            {
                string full = Path.GetFullPath(directory);
                string root = Path.GetPathRoot(full);

                if (!string.IsNullOrEmpty(root))
                {
                    string trimmedFull = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (string.Equals(trimmedFull, trimmedRoot, StringComparison.CurrentCultureIgnoreCase))
                        return null;
                }

                var parent = Directory.GetParent(full);
                return parent?.FullName;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return null;
            }
        }

        static void NavigateTo(PopupState state, string directory)
        {
            string full = NowFilePickerUtility.TryGetFullPath(directory);

            if (string.IsNullOrEmpty(full) || !Directory.Exists(full))
            {
                SetError(state, "Directory not found", focusFileName: false);
                return;
            }

            SetCurrentDirectory(state, full);
            SetSelectedDirectory(state, null);
            state.directoryText = full;
            if (state.mode == NowFileDialogMode.OpenFile)
                state.fileName = string.Empty;

            MarkListsDirty(state);
            RevealFolderInTree(state, full, focus: true, expandTarget: true);
            ClearError(state);
            NowControlState.RequestRepaint();
        }

        static void CommitAction(PopupState state)
        {
            if (!string.IsNullOrEmpty(state.selectedDirectory))
            {
                if (state.mode == NowFileDialogMode.Directory)
                {
                    Commit(state, state.selectedDirectory);
                    return;
                }

                NavigateTo(state, state.selectedDirectory);
                return;
            }

            if (state.mode == NowFileDialogMode.Directory)
            {
                Commit(state, state.currentDirectory);
                return;
            }

            if (state.mode == NowFileDialogMode.OpenFile)
            {
                string openPath = NowFilePickerUtility.BuildOpenPath(
                    state.currentDirectory,
                    state.fileName,
                    state.filters,
                    state.filterIndex,
                    out string openError);

                if (string.IsNullOrEmpty(openPath))
                {
                    SetError(state, openError, focusFileName: true);
                    return;
                }

                Commit(state, openPath);
                return;
            }

            string path = NowFilePickerUtility.BuildSavePath(
                state.currentDirectory,
                state.fileName,
                state.filters,
                state.filterIndex,
                state.settings.defaultExtension,
                out string error);

            if (string.IsNullOrEmpty(path))
            {
                SetError(state, error, focusFileName: true);
                return;
            }

            Commit(state, path);
        }

        static void SetError(PopupState state, string error, bool focusFileName)
        {
            SetErrorText(state, string.IsNullOrWhiteSpace(error) ? "Invalid selection" : error);
            state.actionError = true;

            if (focusFileName && state.fileNameFieldId != 0)
            {
                NowFocus.Focus(state.fileNameFieldId);
                ref var edit = ref NowControlState.Get<NowTextEditState>(state.fileNameFieldId);
                NowTextEdit.SelectAll(ref edit, state.fileName ?? string.Empty);
            }

            NowControlState.RequestRepaint();
        }

        static void ClearError(PopupState state)
        {
            SetErrorText(state, null);
            state.actionError = false;
        }

        /// <summary>
        /// Errors and their "! "-prefixed display label are built together when
        /// the error changes, so the open popup never concatenates per frame.
        /// </summary>
        static void SetErrorText(PopupState state, string error)
        {
            state.error = error;
            state.errorLabel = string.IsNullOrEmpty(error) ? null : "! " + error;
        }

        static void Commit(PopupState state, string path)
        {
            string next = path ?? string.Empty;
            state.pendingPath = next;
            state.hasPendingPath = true;
            ClearError(state);
            NowControlState.Get<bool>(state.id) = false;
        }

        static void HandleDismiss(PopupState state)
        {
            if (NowOverlay.HasNestedOverlay(state.id))
                return;

            var snapshot = NowInput.current;
            bool fieldPressClaimedByField = state.fieldRect.Contains(snapshot.pointerPosition) &&
                NowInput.activeId == state.id;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !fieldPressClaimedByField;

            if (pressedOutside || (snapshot.cancelPressed && !NowInput.cancelConsumed))
                NowControlState.Get<bool>(state.id) = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
        }
    }

    struct NowFilePickerSettings
    {
        public NowLayoutOptions options;
        public string title;
        public string placeholder;
        public string startDirectory;
        public string defaultFileName;
        public string defaultExtension;
        public NowFileFilter[] filters;
        public bool showHidden;
        public bool fitToView;
        public float fieldHeight;
        public float popupWidth;
        public float popupHeight;
        public float popupPadding;
        public float popupSpacing;

        public static NowFilePickerSettings Default(NowFileDialogMode mode)
        {
            return new NowFilePickerSettings
            {
                defaultFileName = mode == NowFileDialogMode.SaveFile ? "Untitled" : null,
                filters = Array.Empty<NowFileFilter>(),
                fitToView = true,
                fieldHeight = 30f,
                popupWidth = 760f,
                popupHeight = 460f,
                popupPadding = 10f,
                popupSpacing = 6f
            };
        }
    }

    static class NowFilePickerUtility
    {
        public static NowFileFilter[] NormalizeFilters(NowFileFilter[] filters)
        {
            if (filters == null || filters.Length == 0)
                return Array.Empty<NowFileFilter>();

            var normalized = new NowFileFilter[filters.Length];

            for (int i = 0; i < filters.Length; ++i)
            {
                string[] source = filters[i].extensions ?? Array.Empty<string>();
                var extensions = new List<string>(source.Length);

                for (int j = 0; j < source.Length; ++j)
                {
                    string extension = NormalizeExtension(source[j]);

                    if (extension == null)
                        continue;

                    if (extension == "*" || !extensions.Contains(extension))
                        extensions.Add(extension);
                }

                normalized[i] = new NowFileFilter(filters[i].name, extensions.ToArray());
            }

            return normalized;
        }

        public static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return null;

            extension = extension.Trim();

            if (extension == "*.*" || extension == "*")
                return "*";

            while (extension.StartsWith(".", StringComparison.Ordinal))
                extension = extension.Substring(1);

            if (extension.StartsWith("*.", StringComparison.Ordinal))
                extension = extension.Substring(2);

            return string.IsNullOrWhiteSpace(extension) ? null : extension.ToLowerInvariant();
        }

        public static bool MatchesFilter(string path, NowFileFilter filter)
        {
            var extensions = filter.extensions;

            if (extensions == null || extensions.Length == 0)
                return true;

            string fileExtension = NormalizeExtension(Path.GetExtension(path));

            for (int i = 0; i < extensions.Length; ++i)
            {
                string extension = NormalizeExtension(extensions[i]);

                if (extension == "*")
                    return true;

                if (extension != null && string.Equals(fileExtension, extension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string FirstConcreteExtension(NowFileFilter[] filters, int filterIndex)
        {
            if (filters == null || filters.Length == 0)
                return null;

            filterIndex = Mathf.Clamp(filterIndex, 0, filters.Length - 1);
            var extensions = filters[filterIndex].extensions;

            if (extensions == null)
                return null;

            for (int i = 0; i < extensions.Length; ++i)
            {
                string extension = NormalizeExtension(extensions[i]);

                if (!string.IsNullOrEmpty(extension) && extension != "*")
                    return extension;
            }

            return null;
        }

        public static string FormatFilterLabel(NowFileFilter filter)
        {
            string extensionList = FormatExtensionList(filter.extensions);

            if (string.IsNullOrEmpty(filter.name))
                return string.IsNullOrEmpty(extensionList) ? "All files" : $"Files ({extensionList})";

            return string.IsNullOrEmpty(extensionList) ? filter.name : $"{filter.name} ({extensionList})";
        }

        static string FormatExtensionList(string[] extensions)
        {
            if (extensions == null || extensions.Length == 0)
                return null;

            var parts = new List<string>(extensions.Length);

            for (int i = 0; i < extensions.Length; ++i)
            {
                string extension = NormalizeExtension(extensions[i]);

                if (extension == null)
                    continue;

                if (extension == "*")
                    return "*.*";

                parts.Add("*." + extension);
            }

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        public static string BuildSavePath(
            string directory,
            string fileName,
            NowFileFilter[] filters,
            int filterIndex,
            string defaultExtension,
            out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                error = "Enter a file name";
                return null;
            }

            string candidate = fileName.Trim();

            if (HasInvalidFileName(candidate))
            {
                error = "Invalid file name";
                return null;
            }

            try
            {
                if (!Path.IsPathRooted(candidate))
                    candidate = Path.Combine(directory ?? string.Empty, candidate);

                if (string.IsNullOrEmpty(Path.GetExtension(candidate)))
                {
                    string extension = NormalizeExtension(defaultExtension) ?? FirstConcreteExtension(filters, filterIndex);

                    if (!string.IsNullOrEmpty(extension) && extension != "*")
                        candidate += "." + extension;
                }

                return Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                error = ex.Message;
                return null;
            }
        }

        public static string BuildOpenPath(
            string directory,
            string fileName,
            NowFileFilter[] filters,
            int filterIndex,
            out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                error = "Choose a file";
                return null;
            }

            string candidate = fileName.Trim();

            if (HasInvalidFileName(candidate))
            {
                error = "Invalid file name";
                return null;
            }

            try
            {
                if (!Path.IsPathRooted(candidate))
                    candidate = Path.Combine(directory ?? string.Empty, candidate);

                candidate = Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                error = ex.Message;
                return null;
            }

            if (!File.Exists(candidate))
            {
                error = "File not found";
                return null;
            }

            if (filters != null && filters.Length > 0)
            {
                var filter = filters[Mathf.Clamp(filterIndex, 0, filters.Length - 1)];

                if (!MatchesFilter(candidate, filter))
                {
                    error = "File does not match filter";
                    return null;
                }
            }

            return candidate;
        }

        static bool HasInvalidFileName(string fileName)
        {
            try
            {
                string leaf = Path.GetFileName(fileName);

                if (string.IsNullOrEmpty(leaf))
                    return true;

                var invalid = Path.GetInvalidFileNameChars();

                for (int i = 0; i < leaf.Length; ++i)
                    for (int j = 0; j < invalid.Length; ++j)
                        if (leaf[i] == invalid[j])
                            return true;

                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        public static string TryGetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        public static bool IsHidden(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.Hidden) != 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return false;
            }
        }

        public static string DisplayName(string path)
        {
            try
            {
                var info = new DirectoryInfo(path);
                return string.IsNullOrEmpty(info.Name) ? info.FullName : info.Name;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return path;
            }
        }
    }

    public static partial class Now
    {
        public static NowFilePicker OpenFileField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, NowFileDialogMode.OpenFile, id, NowControls.SiteId(file, line));
        }

        public static NowFilePicker SaveFileField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, NowFileDialogMode.SaveFile, id, NowControls.SiteId(file, line));
        }

        public static NowFilePicker DirectoryField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, NowFileDialogMode.Directory, id, NowControls.SiteId(file, line));
        }

        public static NowFilePicker FilePicker(NowRect rect, NowFileDialogMode mode, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, mode, id, NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowFilePicker OpenFileField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(NowFileDialogMode.OpenFile, id, NowControls.SiteId(file, line));
        }

        public static NowFilePicker SaveFileField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(NowFileDialogMode.SaveFile, id, NowControls.SiteId(file, line));
        }

        public static NowFilePicker DirectoryField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(NowFileDialogMode.Directory, id, NowControls.SiteId(file, line));
        }

        public static NowFilePicker FilePicker(NowFileDialogMode mode, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(mode, id, NowControls.SiteId(file, line));
        }
    }
}
