using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace NowUI
{
    public enum NowFileDialogMode
    {
        OpenFile,
        SaveFile,
        Directory
    }

    /// <summary>Visual density used by the built-in file browser.</summary>
    public enum NowFilePickerView
    {
        Details,
        SmallThumbnails,
        MediumThumbnails,
        LargeThumbnails
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
        NowControlIdentity _id;
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
            public bool previewable;
        }

        enum ThumbnailState : byte
        {
            Pending,
            Loading,
            Loaded,
            Failed
        }

        enum FolderNavigationSource : byte
        {
            Automatic,
            Place,
            Tree
        }

        enum SidebarLocationSource : byte
        {
            Places,
            Tree
        }

        sealed class ThumbnailEntry
        {
            public string path;
            public ThumbnailState state;
            public Texture texture;
            public string dimensions;
            public long lastAccess;
            public UnityWebRequest request;
            public UnityWebRequestAsyncOperation operation;
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
            public readonly List<NowFilePickerUserFolder> userFolders = new List<NowFilePickerUserFolder>(6);
            public readonly List<FolderTreeEntry> treeEntries = new List<FolderTreeEntry>(32);
            public readonly HashSet<string> expandedTreePaths = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            public readonly Dictionary<string, ThumbnailEntry> thumbnails =
                new Dictionary<string, ThumbnailEntry>(StringComparer.Ordinal);
            public NowResolvedId id;
            public NowResolvedId areaId;
            public NowResolvedId footerAreaId;
            public NowResolvedId pathFieldId;
            public NowResolvedId fileNameFieldId;
            public NowResolvedId filterId;
            public NowResolvedId viewSliderId;
            public NowResolvedId errorTooltipId;
            public NowResolvedId scrollId;
            public NowResolvedId treeScrollId;
            public NowResolvedId entrySeed;
            public NowResolvedId userFolderSeed;
            public NowResolvedId treeSeed;
            public NowResolvedId selectButtonId;
            public NowResolvedId cancelButtonId;
            public NowResolvedId goButtonId;
            public NowResolvedId upButtonId;
            public int callbackState;
            public int filterIndex;
            public string currentDirectory;
            public string currentDirectoryCanonical;
            public string currentDirectoryKey;
            public string parentDirectory;
            public string selectedDirectory;
            public string selectedDirectoryKey;
            public string directoryText;
            public string fileName;
            public string error;
            public string errorLabel;
            public bool actionError;
            public NowFilePickerView view;
            public bool viewInitialized;
            public long thumbnailAccess;
            public int activeThumbnailRequests;
            public bool previewResourcesActive;
            public string pendingPath;
            public bool hasPendingPath;
            public int pendingUserFolderFocusId;
            public string pendingTreeFocusKey;
            public SidebarLocationSource sidebarLocationSource;
            public bool entriesDirty;
            public bool treeDirty;
            public NowRect fieldRect;
            public NowRect surfaceRect;
            public NowRect popupRect;
            public object registrationOwner;
        }

        static readonly Dictionary<NowResolvedId, PopupState> _popupStates = new Dictionary<NowResolvedId, PopupState>(4);
        static readonly Dictionary<int, PopupState> _popupStatesByCallback = new Dictionary<int, PopupState>(4);
        static readonly List<Texture> _deferredThumbnailReleases = new List<Texture>(16);
        static readonly List<NowResolvedId> _releasedPopupStateIds = new List<NowResolvedId>(4);
#if UNITY_EDITOR
        static bool s_editorThumbnailReleaseQueued;
#endif
        static int s_nextPopupState = 1;

        const int AreaSeed = 0x4e464141;
        const int FooterAreaSeed = 0x4e464158;
        const int PathFieldSeed = 0x4e464150;
        const int FileNameSeed = 0x4e464146;
        const int FilterSeed = 0x4e46414c;
        const int ViewSliderSeed = 0x4e46565a;
        const int ErrorTooltipSeed = 0x4e464521;
        const int ScrollSeed = 0x4e464153;
        const int TreeScrollSeed = 0x4e464154;
        const int EntrySeed = 0x4e464145;
        const int UserFolderSeed = 0x4e465046;
        const int TreeSeed = 0x4e464152;
        const int SelectSeed = 0x4e46414f;
        const int CancelSeed = 0x4e464143;
        const int GoSeed = 0x4e464147;
        const int UpSeed = 0x4e464155;
        const int MaxThumbnailRequests = 2;
        const int MaxThumbnailEntries = 64;
        const int ThumbnailDimension = 256;
        const long MaxThumbnailFileBytes = 64L * 1024L * 1024L;
        const int MaxThumbnailSourceDimension = 16384;
        const long MaxThumbnailSourcePixels = 24L * 1024L * 1024L;

        static NowFilePicker()
        {
            NowOverlay.registrationOwnerReleased += ReleaseRegistrationOwner;
            NowOverlay.registrationOwnerFootprintExpired += ReleaseExpiredRegistrationOwner;
        }

        internal NowFilePicker(NowFileDialogMode mode, NowControlIdentity id, int site)
        {
            _mode = mode;
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _navigation = default;
            _settings = NowFilePickerSettings.Default(mode);
        }

        internal NowFilePicker(NowRect rect, NowFileDialogMode mode, NowControlIdentity id, int site) : this(mode, id, site)
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

        public NowFilePicker SetId(NowResolvedId id) { _id = id; return this; }

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

        /// <summary>Sets the initial visual density; the user's later choice persists for this control.</summary>
        public NowFilePicker SetInitialView(NowFilePickerView view)
        {
            _settings.initialView = NowFilePickerUtility.ClampView(view);
            return this;
        }

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
            NowResolvedId id = _id.Resolve(_site);
            var state = GetState(id);
            state.registrationOwner = NowOverlay.currentRegistrationOwner;
            bool changed = ApplyPending(state, ref path);

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = ResolveLineHeight(textStyle);
            Vector2 measured = renderer.MeasureTextField(theme, lineHeight);
            measured.x = Mathf.Max(measured.x, 260f);
            float requestedFieldHeight = IsUnityEditorTheme(theme)
                ? Mathf.Min(_settings.fieldHeight, theme.controlStyles.textFieldMinHeight)
                : _settings.fieldHeight;
            measured.y = Mathf.Max(measured.y, requestedFieldHeight);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _settings.options, measured);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);
            bool wasOpen = open;

            if (interaction.clicked || submitted)
            {
                open = !open;

                if (open)
                    InitializeStateForOpen(state, id, path, _mode, _settings);
                else
                    ReleaseThumbnailResources(state, deferLoadedTextures: true);
            }

            if (open && !wasOpen && string.IsNullOrEmpty(state.currentDirectory))
                InitializeStateForOpen(state, id, path, _mode, _settings);

            if (!open && state.previewResourcesActive)
                ReleaseThumbnailResources(state, deferLoadedTextures: true);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            DrawField(theme, rect, path, _mode, _settings, open, focused, interaction.held, hoverT, lineHeight);

            if (open)
            {
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

        static PopupState GetState(NowResolvedId id)
        {
            if (!_popupStates.TryGetValue(id, out var state))
            {
                int callbackState = s_nextPopupState++;

                if (s_nextPopupState == 0)
                    s_nextPopupState = 1;

                state = new PopupState { callbackState = callbackState };
                _popupStates[id] = state;
                _popupStatesByCallback[callbackState] = state;
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
            NowResolvedId id,
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
            state.areaId = id.Derive(NowIdDomain.Layout, AreaSeed);
            state.footerAreaId = id.Derive(NowIdDomain.Layout, FooterAreaSeed);
            state.pathFieldId = id.Child(PathFieldSeed);
            state.fileNameFieldId = id.Child(FileNameSeed);
            state.filterId = id.Child(FilterSeed);
            state.viewSliderId = id.Child(ViewSliderSeed);
            state.errorTooltipId = id.Child(ErrorTooltipSeed);
            state.scrollId = id.Child(ScrollSeed);
            state.treeScrollId = id.Child(TreeScrollSeed);
            state.entrySeed = id.Child(EntrySeed);
            state.userFolderSeed = id.Child(UserFolderSeed);
            state.treeSeed = id.Child(TreeSeed);
            state.selectButtonId = id.Child(SelectSeed);
            state.cancelButtonId = id.Child(CancelSeed);
            state.goButtonId = id.Child(GoSeed);
            state.upButtonId = id.Child(UpSeed);
            ReleaseThumbnailResources(state, deferLoadedTextures: true);
            state.previewResourcesActive = true;

            if (!state.viewInitialized)
            {
                state.view = NowFilePickerUtility.ClampView(settings.initialView);
                state.viewInitialized = true;
            }

            NowControlState.Get<Vector2>(state.scrollId) = Vector2.zero;
            NowControlState.Get<Vector2>(state.treeScrollId) = Vector2.zero;
            SetCurrentDirectory(state, ResolveInitialDirectory(value, settings));
            NowFilePickerUserFolders.Resolve(state.userFolders);
            SetSelectedDirectory(state, null);
            state.directoryText = state.currentDirectory;
            state.fileName = ResolveInitialFileName(value, settings, mode);
            ClearError(state);
            state.entries.Clear();
            state.treeEntries.Clear();
            state.expandedTreePaths.Clear();
            state.pendingUserFolderFocusId = 0;
            state.pendingTreeFocusKey = null;
            MarkListsDirty(state);
            SynchronizeSidebarToDirectory(state, state.currentDirectory, FolderNavigationSource.Automatic);
        }

        static void SetCurrentDirectory(PopupState state, string directory)
        {
            state.currentDirectory = directory;
            state.currentDirectoryCanonical = NowFilePickerUserFolders.CanonicalPath(directory);
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
            return textStyle.font != null
                ? textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize
                : 20f;
        }

        static float ResolveTextFieldLineHeight(NowText textStyle)
        {
            return textStyle.font != null
                ? textStyle.font.GetLineHeight() * textStyle.fontSize
                : textStyle.fontSize * 1.2f;
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
                Now.Rectangle(rect.Inset(1f))
                    .SetRadius(PickerRadius(theme, 4f))
                    .SetColor(overlay)
                    .Draw();
            }

            NowRect inner = theme.controlRenderer.TextFieldInnerRect(theme, rect, lineHeight);
            string icon = FieldIcon(mode);
            float iconWidth = Mathf.Min(24f, inner.width);
            var iconRect = new NowRect(inner.x, rect.y, iconWidth, rect.height);

            if (IsUnityEditorTheme(theme))
            {
                Color iconColor = theme.GetColor(NowColorToken.TextMuted);
                if (mode == NowFileDialogMode.SaveFile)
                    DrawEditorFileIcon(theme, iconRect, iconColor);
                else
                    DrawEditorFolderIcon(theme, iconRect, iconColor);
            }
            else
            {
                NowControls.DrawLeftLabel(theme, iconRect, icon, NowTextStyle.Body, Color.white);
            }

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

            if (IsUnityEditorTheme(theme))
            {
                NowUnityEditorControlRenderer.DrawDropdownTriangle(
                    theme,
                    new NowRect(rect.xMax - 16f, rect.y, 12f, rect.height));
            }
            else
            {
                DropdownArrowDraw.Draw(
                    theme,
                    new NowRect(rect.xMax - 20f, rect.y, 16f, rect.height),
                    open);
            }
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

        static void DeferPopup(NowThemeAsset theme, NowResolvedId id, NowRect field, NowFilePickerSettings settings)
        {
            var state = GetState(id);
            state.themeAsset = theme;
            state.fieldRect = Now.TransformScreenRect(field);
            state.surfaceRect = CalculateLocalSurfaceRect();
            state.popupRect = CalculatePopupRect(theme, field, settings, state.surfaceRect);

            NowOverlay.BlockAllSurfaces(id);
            NowOverlay.Defer(state.popupRect, id, state.callbackState, DrawPopup);
        }

        static NowRect CalculateLocalSurfaceRect()
        {
            Vector2 size = NowInput.surface.size;

            if (size.x <= 0f || size.y <= 0f)
                return default;

            Vector2 a = Now.InverseTransformScreenPoint(Vector2.zero);
            Vector2 b = Now.InverseTransformScreenPoint(size);
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            return new NowRect(xMin, yMin, Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        static NowRect CalculatePopupRect(
            NowThemeAsset theme,
            NowRect field,
            NowFilePickerSettings settings,
            NowRect surfaceRect)
        {
            float width = Mathf.Max(field.width, settings.popupWidth);
            float height = settings.popupHeight;
            var rect = new NowRect(
                field.x,
                field.yMax + theme.controlStyles.dropdownPopupGap,
                width,
                height);

            if (!settings.fitToView)
                return rect;

            rect = NowFilePickerUtility.FitModalRect(rect, surfaceRect, 8f);

            return NowOverlay.ClampToView(rect);
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStatesByCallback.TryGetValue(stateId, out var state))
                return;

            var theme = state.themeAsset;

            if (!state.surfaceRect.isEmpty)
                Now.Rectangle(state.surfaceRect).SetColor(theme.GetColor(NowColorToken.Scrim)).Draw();

            PollThumbnailRequests(state);
            theme.controlRenderer.DrawPopupBackground(theme, state.popupRect, menu: false);
            DrawPopupContent(state);
            HandleDismiss(state);
        }

        static void DrawPopupContent(PopupState state)
        {
            RefreshEntries(state);

            var theme = state.themeAsset;
            bool editorChrome = IsUnityEditorTheme(theme);
            float requestedPadding = editorChrome
                ? Mathf.Min(state.settings.popupPadding, 8f)
                : state.settings.popupPadding;
            float padding = Mathf.Min(
                requestedPadding,
                Mathf.Max(0f, (Mathf.Min(state.popupRect.width, state.popupRect.height) - 1f) * 0.5f));
            float spacing = editorChrome
                ? Mathf.Min(state.settings.popupSpacing, 4f)
                : state.settings.popupSpacing;
            bool hasFilter = state.mode != NowFileDialogMode.Directory && state.filters.Length > 1;
            bool hasFileName = state.mode != NowFileDialogMode.Directory;
            var renderer = theme.controlRenderer;
            var bodyText = NowControls.Text(theme, NowTextStyle.Body);
            var titleText = NowControls.Text(
                theme,
                editorChrome ? NowTextStyle.Subheading : NowTextStyle.Title);
            float bodyLabelHeight = bodyText.Measure("Ag").y;
            float titleLabelHeight = titleText.Measure(Title(state.mode, state.settings)).y;
            float textFieldHeight = renderer.MeasureTextField(theme, ResolveTextFieldLineHeight(bodyText)).y;
            float dropdownHeight = renderer.MeasureDropdownField(theme, ResolveLineHeight(bodyText)).y;
            float upButtonHeight = renderer.MeasureButton(theme, "Up", NowTextStyle.Button).y;
            float goButtonHeight = renderer.MeasureButton(theme, "Go", NowTextStyle.Button).y;
            float actionButtonHeight = renderer.MeasureButton(theme, ActionLabel(state.mode), NowTextStyle.Button).y;
            float cancelButtonHeight = renderer.MeasureButton(theme, "Cancel", NowTextStyle.Button).y;
            float titleHeight = Mathf.Max(editorChrome ? 22f : 30f, titleLabelHeight);
            titleHeight = Mathf.Max(titleHeight, bodyLabelHeight);
            titleHeight = Mathf.Max(titleHeight, renderer.MeasureSlider(theme).y);

            // A compact popup moves the filter into the title row. Reserving its
            // intrinsic height up front keeps that responsive transition from
            // letting a tall themed dropdown bleed into the address row.
            if (hasFilter)
                titleHeight = Mathf.Max(titleHeight, dropdownHeight);

            float addressHeight = Mathf.Max(textFieldHeight, Mathf.Max(upButtonHeight, goButtonHeight));
            float headerHeight = editorChrome ? 20f : 24f;
            float fileNameHeight = Mathf.Max(textFieldHeight, bodyLabelHeight);
            float filterHeight = Mathf.Max(dropdownHeight, bodyLabelHeight);
            float preferredFooterHeight = Mathf.Max(
                bodyLabelHeight,
                Mathf.Max(actionButtonHeight, cancelButtonHeight));
            const float minimumBrowserHeight = 44f;
            NowRect contentRect = state.popupRect.Inset(padding);
            float footerHeight = Mathf.Min(preferredFooterHeight, contentRect.height);
            float footerGap = contentRect.height > footerHeight
                ? Mathf.Min(spacing, contentRect.height - footerHeight)
                : 0f;
            float bodyHeight = Mathf.Max(0f, contentRect.height - footerHeight - footerGap);
            var bodyRect = new NowRect(contentRect.x, contentRect.y, contentRect.width, bodyHeight);
            var footerRect = new NowRect(
                contentRect.x,
                contentRect.yMax - footerHeight,
                contentRect.width,
                footerHeight);
            float resolvedTitleHeight = Mathf.Min(titleHeight, bodyRect.height);
            float browserBudget = Mathf.Max(
                0f,
                bodyRect.height - resolvedTitleHeight - (resolvedTitleHeight > 0f ? spacing : 0f));
            bool showFileName = hasFileName &&
                TryUseResponsiveRow(ref browserBudget, fileNameHeight, spacing, minimumBrowserHeight);
            bool showAddress = contentRect.width >= 124f &&
                TryUseResponsiveRow(ref browserBudget, addressHeight, spacing, minimumBrowserHeight);
            bool showFilter = hasFilter &&
                TryUseResponsiveRow(ref browserBudget, filterHeight, spacing, minimumBrowserHeight);
            bool showCompactFilter = hasFilter && !showFilter;
            float browserHeight = browserBudget;
            float resolvedHeaderHeight = Mathf.Min(headerHeight, browserHeight);

            using (Now.Mask(state.popupRect.Inset(1f)))
            {
                if (!bodyRect.isEmpty)
                {
                    using (NowLayout.Area(
                        state.areaId,
                        bodyRect,
                        spacing: spacing,
                        padding: 0f,
                        alignItems: NowLayoutAlign.Start))
                    {
                        if (resolvedTitleHeight > 0f)
                            DrawPopupTitle(state, resolvedTitleHeight, bodyRect.width, showCompactFilter);

                        if (showAddress)
                            DrawAddressRow(state, addressHeight, bodyRect.width);

                        NowRect browserRect = NowLayout.ReserveRect(height: browserHeight, stretchWidth: true);
                        if (!browserRect.isEmpty)
                            DrawBrowser(state, browserRect, resolvedHeaderHeight);

                        if (showFileName)
                            DrawFileNameRow(state, fileNameHeight, bodyRect.width);

                        if (showFilter)
                            DrawFilterRow(state, filterHeight, bodyRect.width);
                    }
                }

                if (!footerRect.isEmpty)
                {
                    using (NowLayout.Area(
                        state.footerAreaId,
                        footerRect,
                        spacing: 0f,
                        padding: 0f,
                        alignItems: NowLayoutAlign.Start))
                    {
                        DrawPopupFooter(state, footerRect.height, footerRect.width);
                    }
                }
            }
        }

        static bool TryUseResponsiveRow(
            ref float browserBudget,
            float rowHeight,
            float spacing,
            float minimumBrowserHeight)
        {
            float cost = rowHeight + spacing;

            if (browserBudget - cost < minimumBrowserHeight)
                return false;

            browserBudget -= cost;
            return true;
        }

        static void DrawPopupTitle(
            PopupState state,
            float height,
            float availableWidth,
            bool showCompactFilter)
        {
            using (NowLayout.HorizontalScope(height: height, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 6f))
            {
                bool compactError = availableWidth < 340f && !string.IsNullOrEmpty(state.error);

                if (showCompactFilter && availableWidth < 280f)
                {
                    if (compactError && availableWidth >= 48f)
                    {
                        var errorMarker = NowLayout.Label("!")
                            .SetWidth(16f)
                            .SetColor(new Color(0.86f, 0.24f, 0.24f))
                            .Reserve();
                        errorMarker.Draw();
                        NowTooltip.For(state.errorTooltipId, errorMarker.rect, state.error);
                    }

                    DrawPopupFilterControl(state, stretch: true);
                    return;
                }

                string label = compactError ? state.errorLabel : Title(state.mode, state.settings);
                NowTextStyle titleStyle = IsUnityEditorTheme(state.themeAsset)
                    ? NowTextStyle.Subheading
                    : NowTextStyle.Title;
                var title = NowLayout.Label(NowControls.Text(state.themeAsset, titleStyle), label)
                    .SetStretchWidth();

                if (compactError)
                    title = title.SetColor(new Color(0.86f, 0.24f, 0.24f));

                title.Draw();

                if (showCompactFilter)
                {
                    DrawPopupFilterControl(state, stretch: false, width: 140f);
                }
                else if (availableWidth >= 340f)
                {
                    NowLayout.Label("View").SetWidth(34f).Draw();
                    bool editorChrome = IsUnityEditorTheme(state.themeAsset);
                    if (editorChrome)
                    {
                        var detailsIcon = NowLayout.Label("").SetWidth(16f).Reserve();
                        detailsIcon.Draw();
                        DrawEditorViewIcon(
                            detailsIcon.rect,
                            state.themeAsset.GetColor(NowColorToken.TextMuted),
                            grid: false);
                    }
                    else
                    {
                        NowLayout.Label("▤").SetWidth(16f).Draw();
                    }
                    int view = (int)state.view;

                    if (NowLayout.Slider(
                            (float)NowFilePickerView.Details,
                            (float)NowFilePickerView.LargeThumbnails)
                        .SetId(state.viewSliderId)
                        .SetStep(1f)
                        .SetWidth(92f)
                        .Draw(ref view))
                    {
                        state.view = NowFilePickerUtility.ClampView((NowFilePickerView)view);
                        NowControlState.Get<Vector2>(state.scrollId) = Vector2.zero;
                        NowControlState.RequestRepaint();
                    }

                    if (editorChrome)
                    {
                        var gridIcon = NowLayout.Label("").SetWidth(16f).Reserve();
                        gridIcon.Draw();
                        DrawEditorViewIcon(
                            gridIcon.rect,
                            state.themeAsset.GetColor(NowColorToken.TextMuted),
                            grid: true);
                    }
                    else
                    {
                        NowLayout.Label("▦").SetWidth(16f).Draw();
                    }
                }
            }
        }

        static void DrawAddressRow(PopupState state, float height, float availableWidth)
        {
            float upWidth = availableWidth < 260f ? 36f : 48f;
            float goWidth = availableWidth < 260f ? 36f : 44f;

            using (NowLayout.HorizontalScope(height: height, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 6f))
            {
                string parent = state.parentDirectory;

                if (!string.IsNullOrEmpty(parent))
                {
                    if (NowLayout.Button("Up").SetId(state.upButtonId).SetStyle(NowRectangleStyle.Outline).SetWidth(upWidth).Draw())
                        NavigateTo(state, parent);
                }
                else
                {
                    NowLayout.Label("").SetWidth(upWidth).Draw();
                }

                if (NowLayout.TextField(state.pathFieldId)
                    .SetStretchWidth()
                    .SetPlaceholder("Address")
                    .Draw(ref state.directoryText))
                {
                    SetSelectedDirectory(state, null);
                    ClearError(state);
                }

                if (NowLayout.Button("Go").SetId(state.goButtonId).SetStyle(NowRectangleStyle.Outline).SetWidth(goWidth).Draw())
                    NavigateTo(state, state.directoryText);
            }
        }

        static void DrawFileNameRow(PopupState state, float height, float availableWidth)
        {
            using (NowLayout.HorizontalScope(height: height, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                if (availableWidth >= 220f)
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

        static void DrawFilterRow(PopupState state, float height, float availableWidth)
        {
            using (NowLayout.HorizontalScope(height: height, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                if (availableWidth >= 220f)
                    NowLayout.Label("File type:").SetWidth(78f).Draw();

                DrawPopupFilterControl(state, stretch: true);
            }
        }

        static void DrawPopupFilterControl(
            PopupState state,
            bool stretch,
            float width = 0f)
        {
            int filter = state.filterIndex;
            var dropdown = NowLayout.Dropdown(state.filterId, state.filterLabels);
            dropdown = stretch
                ? dropdown.SetStretchWidth()
                : dropdown.SetWidth(width);

            if (!dropdown.Draw(ref filter))
                return;

            state.filterIndex = Mathf.Clamp(filter, 0, state.filters.Length - 1);
            SetSelectedDirectory(state, null);
            state.entriesDirty = true;
            ClearError(state);
        }

        static void DrawPopupFooter(PopupState state, float height, float availableWidth)
        {
            using (NowLayout.HorizontalScope(height: height, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                if (availableWidth < 340f)
                {
                    float buttonWidth = Mathf.Max(1f, (availableWidth - 8f) * 0.5f);

                    if (NowLayout.Button(ActionLabel(state.mode))
                        .SetId(state.selectButtonId)
                        .SetStyle(NowRectangleStyle.Accent)
                        .SetWidth(buttonWidth)
                        .Draw())
                    {
                        CommitAction(state);
                    }

                    if (NowLayout.Button("Cancel")
                        .SetId(state.cancelButtonId)
                        .SetStyle(NowRectangleStyle.Surface)
                        .SetWidth(buttonWidth)
                        .Draw())
                    {
                        ClosePopup(state);
                    }

                    return;
                }

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
                    ClosePopup(state);
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

        static bool IsUnityEditorTheme(NowThemeAsset theme)
        {
            return theme != null && theme.controlRenderer is NowUnityEditorControlRenderer;
        }

        static Color PickerPaneSurface(NowThemeAsset theme)
        {
            return IsUnityEditorTheme(theme)
                ? theme.GetColor(NowColorToken.Background)
                : theme.GetColor(NowColorToken.Surface);
        }

        static Color PickerHeaderSurface(NowThemeAsset theme)
        {
            if (!IsUnityEditorTheme(theme))
                return theme.GetColor(NowColorToken.SurfaceMuted);

            return Color.LerpUnclamped(
                theme.GetColor(NowColorToken.Background),
                theme.GetColor(NowColorToken.SurfaceMuted),
                0.25f);
        }

        static Color PickerRowHover(NowThemeAsset theme, bool held)
        {
            if (!IsUnityEditorTheme(theme))
            {
                Color surface = theme.GetColor(NowColorToken.SurfaceMuted);
                return NowControls.StateColor(theme, surface, 1f, held);
            }

            Color hover = Color.LerpUnclamped(
                theme.GetColor(NowColorToken.Background),
                theme.GetColor(NowColorToken.SurfaceHover),
                held ? 0.72f : 0.42f);
            return hover;
        }

        static float PickerRadius(NowThemeAsset theme, float fallback)
        {
            return IsUnityEditorTheme(theme) ? 1f : fallback;
        }

        static void DrawEditorFolderIcon(NowThemeAsset theme, NowRect rect, Color color)
        {
            float width = Mathf.Min(14f, Mathf.Max(0f, rect.width - 2f));
            float height = Mathf.Min(11f, Mathf.Max(0f, rect.height - 2f));
            float x = Mathf.Round(rect.x + (rect.width - width) * 0.5f);
            float y = Mathf.Round(rect.y + (rect.height - height) * 0.5f);
            float tabWidth = Mathf.Max(3f, Mathf.Round(width * 0.46f));

            Now.Rectangle(new NowRect(x + 1f, y, tabWidth, 4f))
                .SetRadius(1f, 1f, 0f, 0f)
                .SetColor(color)
                .Draw();
            Now.Rectangle(new NowRect(x, y + 3f, width, Mathf.Max(1f, height - 3f)))
                .SetRadius(1f)
                .SetColor(color)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.BorderStrong))
                .Draw();
        }

        static void DrawEditorFileIcon(NowThemeAsset theme, NowRect rect, Color color)
        {
            float width = Mathf.Min(11f, Mathf.Max(0f, rect.width - 2f));
            float height = Mathf.Min(14f, Mathf.Max(0f, rect.height - 2f));
            float x = Mathf.Round(rect.x + (rect.width - width) * 0.5f);
            float y = Mathf.Round(rect.y + (rect.height - height) * 0.5f);
            var page = new NowRect(x, y, width, height);

            Now.Rectangle(page)
                .SetRadius(1f)
                .SetColor(color)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.BorderStrong))
                .Draw();
            Now.Triangle(
                    new Vector2(page.xMax - 4f, page.y),
                    new Vector2(page.xMax, page.y),
                    new Vector2(page.xMax, page.y + 4f))
                .SetColor(PickerPaneSurface(theme))
                .Draw();
        }

        static void DrawEditorDisclosure(NowRect rect, bool expanded, Color color)
        {
            float centerX = Mathf.Round(rect.center.x);
            float centerY = Mathf.Round(rect.center.y);

            if (expanded)
            {
                Now.Triangle(
                        new Vector2(centerX - 4f, centerY - 2.5f),
                        new Vector2(centerX + 4f, centerY - 2.5f),
                        new Vector2(centerX, centerY + 2.5f))
                    .SetColor(color)
                    .Draw();
            }
            else
            {
                Now.Triangle(
                        new Vector2(centerX - 2.5f, centerY - 4f),
                        new Vector2(centerX - 2.5f, centerY + 4f),
                        new Vector2(centerX + 2.5f, centerY))
                    .SetColor(color)
                    .Draw();
            }
        }

        static void DrawEditorViewIcon(NowRect rect, Color color, bool grid)
        {
            float x = Mathf.Round(rect.x + (rect.width - 14f) * 0.5f);
            float y = Mathf.Round(rect.y + (rect.height - 12f) * 0.5f);

            if (grid)
            {
                for (int row = 0; row < 3; ++row)
                {
                    for (int column = 0; column < 3; ++column)
                    {
                        Now.Rectangle(new NowRect(x + column * 5f, y + row * 5f, 3f, 3f))
                            .SetColor(color)
                            .Draw();
                    }
                }

                return;
            }

            for (int row = 0; row < 3; ++row)
            {
                float rowY = y + row * 5f;
                Now.Rectangle(new NowRect(x, rowY, 3f, 3f)).SetColor(color).Draw();
                Now.Rectangle(new NowRect(x + 5f, rowY + 1f, 9f, 1f)).SetColor(color).Draw();
            }
        }

        static void DrawBrowser(PopupState state, NowRect rect, float headerHeight)
        {
            bool showTree = rect.width >= 560f;

            if (!showTree)
            {
                state.pendingUserFolderFocusId = 0;
                state.pendingTreeFocusKey = null;
            }

            bool editorChrome = IsUnityEditorTheme(state.themeAsset);
            float treeWidth = showTree
                ? editorChrome
                    ? Mathf.Clamp(rect.width * 0.25f, 160f, 196f)
                    : Mathf.Clamp(rect.width * 0.30f, 168f, 220f)
                : 0f;
            float paneGap = editorChrome ? 4f : 8f;
            float gap = showTree ? paneGap : 0f;
            float listX = rect.x;
            float listWidth = rect.width;

            if (showTree)
            {
                var treeRect = new NowRect(rect.x, rect.y, treeWidth, rect.height);
                DrawFolderTree(state, treeRect, headerHeight);
                listX = treeRect.xMax + gap;
                listWidth = Mathf.Max(0f, rect.xMax - listX);
            }

            bool showPreview = NowFilePickerUtility.ShouldShowPreviewPanel(
                state.mode,
                state.view,
                listWidth,
                state.filters,
                state.filterIndex);
            BrowserEntry previewEntry = showPreview ? SelectedInspectorEntry(state) : null;
            float previewWidth = showPreview
                ? editorChrome
                    ? Mathf.Clamp(listWidth * 0.33f, 160f, 196f)
                    : Mathf.Clamp(listWidth * 0.36f, 160f, 210f)
                : 0f;

            if (showPreview)
                listWidth = Mathf.Max(0f, listWidth - previewWidth - paneGap);

            var headerRect = new NowRect(listX, rect.y, listWidth, headerHeight);
            var listRect = new NowRect(listX, rect.y + headerHeight, listWidth, Mathf.Max(0f, rect.height - headerHeight));

            if (state.view == NowFilePickerView.Details)
                DrawListHeader(state.themeAsset, headerRect);
            else
                DrawGridHeader(state.themeAsset, headerRect);

            DrawListFrame(state.themeAsset, listRect);

            using (var scroll = Now.ScrollView(listRect.Inset(1f), state.scrollId).Begin())
            {
                if (state.view == NowFilePickerView.Details)
                {
                    DrawEntries(state, scroll.scrollOffset.y, scroll.viewport.height);
                }
                else
                {
                    float contentWidth = scroll.viewport.width;

                    if (scroll.verticalScrollbarVisible)
                    {
                        var styles = state.themeAsset.controlStyles;
                        contentWidth -= styles.scrollbarWidth + styles.scrollbarPadding;
                    }

                    DrawGridEntries(
                        state,
                        scroll.scrollOffset.y,
                        scroll.viewport.height,
                        Mathf.Max(1f, contentWidth));
                }
            }

            if (showPreview)
            {
                var previewRect = new NowRect(
                    listRect.xMax + paneGap,
                    rect.y,
                    previewWidth,
                    rect.height);
                DrawPreviewPanel(state, previewRect, headerHeight, previewEntry);
            }
        }

        static void DrawFolderTree(PopupState state, NowRect rect, float headerHeight)
        {
            var theme = state.themeAsset;
            bool editorChrome = IsUnityEditorTheme(theme);
            Color surface = PickerPaneSurface(theme);
            Color surfaceMuted = PickerHeaderSurface(theme);
            Color border = theme.GetColor(
                editorChrome ? NowColorToken.BorderStrong : NowColorToken.Border);
            Color muted = theme.GetColor(NowColorToken.TextMuted);

            Now.Rectangle(rect)
                .SetRadius(PickerRadius(theme, 4f))
                .SetColor(surface)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();

            BuildFolderTree(state);

            using (Now.ScrollView(rect.Inset(1f), state.treeScrollId).Begin())
            {
                if (state.userFolders.Count > 0)
                {
                    DrawFolderTreeSectionHeader(theme, surfaceMuted, muted, "Places", headerHeight, roundedTop: true);
                    DrawUserFolderEntries(state, editorChrome ? 20f : 27f);
                    DrawFolderTreeSectionHeader(theme, surfaceMuted, muted, "Folders", headerHeight, roundedTop: false);
                }
                else
                {
                    DrawFolderTreeSectionHeader(theme, surfaceMuted, muted, "Folders", headerHeight, roundedTop: true);
                }

                DrawFolderTreeEntries(state);
                NowLayout.Space(4f);
            }
        }

        static void DrawFolderTreeSectionHeader(
            NowThemeAsset theme,
            Color surfaceMuted,
            Color muted,
            string label,
            float height,
            bool roundedTop)
        {
            NowRect headerRect = NowLayout.ReserveRect(height: height, stretchWidth: true);
            var background = Now.Rectangle(headerRect).SetColor(surfaceMuted);

            if (roundedTop)
            {
                float radius = PickerRadius(theme, 3f);
                background = background.SetRadius(radius, radius, 0f, 0f);
            }

            background.Draw();
            NowControls.DrawLeftLabel(theme, headerRect.Inset(8f, 0f), label, NowTextStyle.Muted, muted);
        }

        static void DrawUserFolderEntries(PopupState state, float rowHeight)
        {
            for (int i = 0; i < state.userFolders.Count; ++i)
            {
                NowRect row = NowLayout.ReserveRect(height: rowHeight, stretchWidth: true);
                DrawUserFolderRow(state, row, state.userFolders[i]);
            }
        }

        static void DrawUserFolderRow(PopupState state, NowRect row, NowFilePickerUserFolder folder)
        {
            var theme = state.themeAsset;
            NowResolvedId id = state.userFolderSeed.Child(folder.stableId);
            bool revealFocus = state.pendingUserFolderFocusId == folder.stableId;

            if (revealFocus && !NowInput.isPassive)
            {
                NowFocus.Focus(id);
                state.pendingUserFolderFocusId = 0;
            }

            var interaction = NowControls.Interact(id, row, out bool focused, out bool submitted);
            var platform = NowFilePickerUserFolders.Platform(Application.platform);
            bool currentPath = NowFilePickerUserFolders.PathComparer(platform)
                .Equals(folder.path, state.currentDirectoryCanonical);
            bool current = currentPath && state.sidebarLocationSource == SidebarLocationSource.Places;
            bool selected = current;
            bool editorChrome = IsUnityEditorTheme(theme);
            NowRect visual = editorChrome ? row.Inset(1f, 0f) : row.Inset(2f, 1f);

            if (selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                var selection = Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(editorChrome
                        ? accent
                        : new Color(accent.r, accent.g, accent.b, current ? 0.20f : 0.12f));

                if (!editorChrome)
                {
                    selection = selection
                        .SetOutline(1f)
                        .SetOutlineColor(new Color(accent.r, accent.g, accent.b, focused ? 0.70f : current ? 0.52f : 0.34f));
                }

                selection.Draw();
            }
            else if (focused)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.07f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.42f))
                    .Draw();
            }
            else if (interaction.hovered || interaction.held)
            {
                Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(PickerRowHover(theme, interaction.held))
                    .Draw();
            }

            var iconRect = new NowRect(row.x + 9f, row.y, 20f, row.height);
            var nameRect = new NowRect(iconRect.xMax + 5f, row.y, Mathf.Max(0f, row.xMax - iconRect.xMax - 12f), row.height);
            Color iconColor = selected && editorChrome
                ? theme.GetColor(NowColorToken.AccentText)
                : current
                ? theme.GetColor(NowColorToken.Accent)
                : theme.GetColor(NowColorToken.TextMuted);
            NowControls.DrawLeftLabel(theme, iconRect, folder.icon, NowTextStyle.Body, iconColor);
            NowControls.DrawLeftLabel(
                theme,
                nameRect,
                folder.label,
                NowTextStyle.Body,
                selected && editorChrome
                    ? theme.GetColor(NowColorToken.AccentText)
                    : theme.GetColor(NowColorToken.Text));

            if (interaction.clicked || submitted)
            {
                if (!currentPath)
                {
                    NavigateTo(state, folder.path, FolderNavigationSource.Place);
                }
                else
                {
                    state.sidebarLocationSource = SidebarLocationSource.Places;
                    state.pendingUserFolderFocusId = 0;
                    state.pendingTreeFocusKey = null;
                    NowControlState.RequestRepaint();
                }
            }
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
                float rowHeight = IsUnityEditorTheme(state.themeAsset) ? 20f : 26f;
                NowRect row = NowLayout.ReserveRect(height: rowHeight, stretchWidth: true);
                DrawFolderTreeRow(state, row, state.treeEntries[i], i);
            }
        }

        static void DrawFolderTreeRow(PopupState state, NowRect row, FolderTreeEntry entry, int index)
        {
            var theme = state.themeAsset;
            NowResolvedId id = FolderTreeRowId(state, entry, index);
            bool revealFocus = KeyEquals(state.pendingTreeFocusKey, entry.key);

            if (revealFocus && !NowInput.isPassive)
            {
                NowFocus.Focus(id);
                state.pendingTreeFocusKey = null;
            }

            var interaction = NowControls.Interact(id, row, out bool focused, out bool submitted);
            bool selected = entry.current && state.sidebarLocationSource == SidebarLocationSource.Tree;
            bool editorChrome = IsUnityEditorTheme(theme);
            NowRect visual = editorChrome ? row.Inset(1f, 0f) : row.Inset(2f, 1f);

            if (selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                var selection = Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(editorChrome
                        ? accent
                        : new Color(accent.r, accent.g, accent.b, entry.current ? 0.20f : 0.12f));

                if (!editorChrome)
                {
                    selection = selection
                        .SetOutline(1f)
                        .SetOutlineColor(new Color(accent.r, accent.g, accent.b, focused ? 0.70f : entry.current ? 0.52f : 0.34f));
                }

                selection.Draw();
            }
            else if (focused)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.07f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.42f))
                    .Draw();
            }
            else if (interaction.hovered || interaction.held)
            {
                Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(PickerRowHover(theme, interaction.held))
                    .Draw();
            }

            Color muted = theme.GetColor(NowColorToken.TextMuted);
            float indent = Mathf.Min(Mathf.Max(0, entry.depth) * 14f, 84f);
            var toggleRect = new NowRect(row.x + 5f + indent, row.y, 16f, row.height);
            var iconRect = new NowRect(toggleRect.xMax + 2f, row.y, 20f, row.height);
            var nameRect = new NowRect(iconRect.xMax + 4f, row.y, Mathf.Max(0f, row.xMax - iconRect.xMax - 10f), row.height);
            Color text = selected && editorChrome
                ? theme.GetColor(NowColorToken.AccentText)
                : entry.ancestor && !entry.current
                ? muted
                : theme.GetColor(NowColorToken.Text);

            if (entry.hasChildren)
            {
                if (editorChrome)
                    DrawEditorDisclosure(toggleRect, entry.expanded, selected ? text : muted);
                else
                    NowControls.DrawLeftLabel(theme, toggleRect, entry.expanded ? "▾" : "▸", NowTextStyle.Muted, muted);
            }

            if (editorChrome)
            {
                DrawEditorFolderIcon(
                    theme,
                    iconRect,
                    selected ? theme.GetColor(NowColorToken.AccentText) : theme.GetColor(NowColorToken.TextMuted));
            }
            else
            {
                NowControls.DrawLeftLabel(
                    theme,
                    iconRect,
                    entry.current ? "📂" : "📁",
                    NowTextStyle.Body,
                    Color.white);
            }
            NowControls.DrawLeftLabel(theme, nameRect, entry.name, NowTextStyle.Body, text);

            if (interaction.clicked && entry.hasChildren && toggleRect.Contains(interaction.pointerPosition))
            {
                SetFolderTreeExpanded(state, entry.path, !entry.expanded);
                NowControlState.RequestRepaint();
                return;
            }

            if (interaction.clicked || submitted)
            {
                state.sidebarLocationSource = SidebarLocationSource.Tree;
                state.pendingUserFolderFocusId = 0;
                state.pendingTreeFocusKey = null;

                if (!KeyEquals(state.currentDirectoryKey, entry.key))
                    NavigateTo(state, entry.path, FolderNavigationSource.Tree);
                else
                    NowControlState.RequestRepaint();
            }
        }

        static void DrawListHeader(NowThemeAsset theme, NowRect rect)
        {
            bool editorChrome = IsUnityEditorTheme(theme);
            Color surfaceMuted = PickerHeaderSurface(theme);
            Color border = theme.GetColor(
                editorChrome ? NowColorToken.BorderStrong : NowColorToken.Border);
            Color muted = theme.GetColor(NowColorToken.TextMuted);
            float typeWidth = TypeColumnWidth(rect);
            float radius = PickerRadius(theme, 4f);

            Now.Rectangle(rect)
                .SetRadius(radius, radius, 0f, 0f)
                .SetColor(surfaceMuted)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();

            var nameRect = new NowRect(rect.x + 34f, rect.y, Mathf.Max(0f, rect.width - typeWidth - 42f), rect.height);
            var typeRect = new NowRect(rect.xMax - typeWidth - 8f, rect.y, typeWidth, rect.height);

            NowControls.DrawLeftLabel(theme, nameRect, "Name", NowTextStyle.Muted, muted);
            NowControls.DrawLeftLabel(theme, typeRect, "Type", NowTextStyle.Muted, muted);
        }

        static void DrawGridHeader(NowThemeAsset theme, NowRect rect, string label = "Thumbnails")
        {
            bool editorChrome = IsUnityEditorTheme(theme);
            Color surfaceMuted = PickerHeaderSurface(theme);
            Color border = theme.GetColor(
                editorChrome ? NowColorToken.BorderStrong : NowColorToken.Border);
            Color muted = theme.GetColor(NowColorToken.TextMuted);
            float radius = PickerRadius(theme, 4f);

            Now.Rectangle(rect)
                .SetRadius(radius, radius, 0f, 0f)
                .SetColor(surfaceMuted)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();

            NowControls.DrawLeftLabel(theme, rect.Inset(8f, 0f), label, NowTextStyle.Muted, muted);
        }

        static void DrawListFrame(NowThemeAsset theme, NowRect rect)
        {
            bool editorChrome = IsUnityEditorTheme(theme);
            Color surface = PickerPaneSurface(theme);
            Color border = theme.GetColor(
                editorChrome ? NowColorToken.BorderStrong : NowColorToken.Border);
            float radius = PickerRadius(theme, 4f);

            Now.Rectangle(rect)
                .SetRadius(0f, 0f, radius, radius)
                .SetColor(surface)
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();
        }

        static float TypeColumnWidth(NowRect rect)
        {
            return rect.width >= 430f ? 118f : 92f;
        }

        static void DrawEntries(PopupState state, float scrollY, float viewportHeight)
        {
            if (state.entries.Count == 0)
            {
                NowLayout.Space(8f);
                NowLayout.Label(NowControls.Text(state.themeAsset, NowTextStyle.Muted), "No matching items")
                    .SetStretchWidth()
                    .Draw();
                return;
            }

            float rowHeight = IsUnityEditorTheme(state.themeAsset) ? 20f : 28f;
            int count = state.entries.Count;
            int first = Mathf.Clamp(Mathf.FloorToInt(scrollY / rowHeight), 0, count);
            int end = Mathf.Clamp(
                Mathf.CeilToInt((scrollY + Mathf.Max(0f, viewportHeight)) / rowHeight) + 1,
                first,
                count);

            if (first > 0)
                NowLayout.Space(first * rowHeight);

            for (int i = first; i < end; ++i)
            {
                var entry = state.entries[i];
                NowRect row = NowLayout.ReserveRect(height: rowHeight, stretchWidth: true);
                DrawEntryRow(state, row, entry, i);
            }

            if (end < count)
                NowLayout.Space((count - end) * rowHeight);
        }

        static void DrawEntryRow(PopupState state, NowRect row, BrowserEntry entry, int index)
        {
            var theme = state.themeAsset;
            NowResolvedId id = state.entrySeed.Child(index + 1);
            var interaction = NowInput.Interact(id, row);
            bool selected = IsSelectedEntry(state, entry);
            bool editorChrome = IsUnityEditorTheme(theme);
            NowRect visual = editorChrome ? row.Inset(1f, 0f) : row.Inset(2f, 1f);

            if (selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                var selection = Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(editorChrome
                        ? accent
                        : new Color(accent.r, accent.g, accent.b, 0.18f));

                if (!editorChrome)
                {
                    selection = selection
                        .SetOutline(1f)
                        .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.48f));
                }

                selection.Draw();
            }
            else if (interaction.hovered || interaction.held)
            {
                Now.Rectangle(visual)
                    .SetRadius(PickerRadius(theme, 3f))
                    .SetColor(PickerRowHover(theme, interaction.held))
                    .Draw();
            }

            float typeWidth = TypeColumnWidth(row);
            var iconRect = new NowRect(row.x + 9f, row.y, 22f, row.height);
            var nameRect = new NowRect(iconRect.xMax + 6f, row.y, Mathf.Max(0f, row.width - typeWidth - 46f), row.height);
            var typeRect = new NowRect(row.xMax - typeWidth - 8f, row.y, typeWidth, row.height);
            Color text = selected && editorChrome
                ? theme.GetColor(NowColorToken.AccentText)
                : theme.GetColor(NowColorToken.Text);
            Color muted = selected
                ? text
                : theme.GetColor(NowColorToken.TextMuted);

            DrawEntryIcon(state, iconRect.Inset(1f), entry);
            NowControls.DrawLeftLabel(theme, nameRect, entry.name, NowTextStyle.Body, text);
            NowControls.DrawLeftLabel(theme, typeRect, entry.type, NowTextStyle.Muted, muted);
            HandleEntryClick(state, entry, id, interaction);
        }

        static void DrawGridEntries(
            PopupState state,
            float scrollY,
            float viewportHeight,
            float contentWidth)
        {
            if (state.entries.Count == 0)
            {
                NowLayout.Space(8f);
                NowLayout.Label(NowControls.Text(state.themeAsset, NowTextStyle.Muted), "No matching items")
                    .SetStretchWidth()
                    .Draw();
                return;
            }

            const float outerPadding = 8f;
            const float columnGap = 8f;
            const float rowGap = 10f;
            float preferredWidth = PreferredThumbnailWidth(state.view);
            float usableWidth = Mathf.Max(1f, contentWidth - outerPadding * 2f);
            int columns = NowFilePickerUtility.GridColumnCount(usableWidth, preferredWidth, columnGap);
            float cellWidth = Mathf.Max(1f, (usableWidth - columnGap * (columns - 1)) / columns);
            float previewHeight = Mathf.Clamp(cellWidth * 0.72f, 54f, 132f);
            float cellHeight = previewHeight + 48f;
            float rowHeight = cellHeight + rowGap;
            int rowCount = Mathf.CeilToInt(state.entries.Count / (float)columns);
            float visibleTop = Mathf.Max(0f, scrollY - outerPadding);
            float visibleBottom = Mathf.Max(0f, scrollY + Mathf.Max(0f, viewportHeight) - outerPadding);
            int firstRow = Mathf.Clamp(Mathf.FloorToInt(visibleTop / rowHeight), 0, rowCount);
            int endRow = Mathf.Clamp(
                Mathf.CeilToInt(visibleBottom / rowHeight) + 1,
                firstRow,
                rowCount);

            NowLayout.Space(outerPadding + firstRow * rowHeight);

            for (int rowIndex = firstRow; rowIndex < endRow; ++rowIndex)
            {
                NowRect row = NowLayout.ReserveRect(height: rowHeight, stretchWidth: true);
                float rowContentWidth = Mathf.Max(1f, row.width - outerPadding * 2f);
                float resolvedWidth = Mathf.Max(1f, (rowContentWidth - columnGap * (columns - 1)) / columns);
                int firstEntry = rowIndex * columns;
                int endEntry = Mathf.Min(firstEntry + columns, state.entries.Count);

                for (int entryIndex = firstEntry; entryIndex < endEntry; ++entryIndex)
                {
                    int column = entryIndex - firstEntry;
                    var cell = new NowRect(
                        row.x + outerPadding + column * (resolvedWidth + columnGap),
                        row.y,
                        resolvedWidth,
                        cellHeight);
                    DrawGridEntry(state, cell, state.entries[entryIndex], entryIndex);
                }
            }

            NowLayout.Space((rowCount - endRow) * rowHeight + outerPadding);
        }

        static float PreferredThumbnailWidth(NowFilePickerView view)
        {
            return view switch
            {
                NowFilePickerView.SmallThumbnails => 84f,
                NowFilePickerView.LargeThumbnails => 156f,
                _ => 116f
            };
        }

        static void DrawGridEntry(PopupState state, NowRect cell, BrowserEntry entry, int index)
        {
            var theme = state.themeAsset;
            NowResolvedId id = state.entrySeed.Child(index + 1);
            var interaction = NowInput.Interact(id, cell);
            bool selected = IsSelectedEntry(state, entry);
            bool editorChrome = IsUnityEditorTheme(theme);
            Color border = theme.GetColor(
                editorChrome ? NowColorToken.BorderStrong : NowColorToken.Border);
            Color surface = PickerPaneSurface(theme);
            float radius = PickerRadius(theme, 5f);

            if (selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(cell)
                    .SetRadius(radius)
                    .SetColor(editorChrome
                        ? accent
                        : new Color(accent.r, accent.g, accent.b, 0.18f))
                    .SetOutline(1f)
                    .SetOutlineColor(editorChrome
                        ? theme.GetColor(NowColorToken.FocusRing)
                        : new Color(accent.r, accent.g, accent.b, 0.58f))
                    .Draw();
            }
            else
            {
                Color fill = interaction.hovered || interaction.held
                    ? PickerRowHover(theme, interaction.held)
                    : surface;
                Now.Rectangle(cell)
                    .SetRadius(radius)
                    .SetColor(fill)
                    .SetOutline(1f)
                    .SetOutlineColor(border)
                    .Draw();
            }

            var previewRect = new NowRect(
                cell.x + 7f,
                cell.y + 7f,
                Mathf.Max(0f, cell.width - 14f),
                Mathf.Max(0f, cell.height - 48f));
            DrawThumbnailFrame(state, previewRect, entry);

            var nameRect = new NowRect(
                previewRect.x,
                previewRect.yMax + 6f,
                previewRect.width,
                Mathf.Max(0f, cell.yMax - previewRect.yMax - 13f));
            NowControls.DrawLeftLabel(
                theme,
                nameRect,
                entry.name,
                NowTextStyle.Body,
                selected && editorChrome
                    ? theme.GetColor(NowColorToken.AccentText)
                    : theme.GetColor(NowColorToken.Text));
            HandleEntryClick(state, entry, id, interaction);
        }

        static void HandleEntryClick(
            PopupState state,
            BrowserEntry entry,
            NowResolvedId id,
            NowInteraction interaction)
        {
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

        static BrowserEntry SelectedInspectorEntry(PopupState state)
        {
            if (state.mode != NowFileDialogMode.OpenFile || string.IsNullOrEmpty(state.fileName))
                return null;

            for (int i = 0; i < state.entries.Count; ++i)
            {
                var entry = state.entries[i];

                if (!entry.directory &&
                    string.Equals(entry.name, state.fileName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        static void DrawPreviewPanel(
            PopupState state,
            NowRect rect,
            float headerHeight,
            BrowserEntry entry)
        {
            var theme = state.themeAsset;
            var headerRect = new NowRect(rect.x, rect.y, rect.width, headerHeight);
            var bodyRect = new NowRect(rect.x, rect.y + headerHeight, rect.width, Mathf.Max(0f, rect.height - headerHeight));
            DrawGridHeader(theme, headerRect, "Details");
            DrawListFrame(theme, bodyRect);

            float imageSize = Mathf.Max(0f, Mathf.Min(bodyRect.width - 16f, bodyRect.height - 62f));
            var imageRect = new NowRect(
                bodyRect.x + (bodyRect.width - imageSize) * 0.5f,
                bodyRect.y + 8f,
                imageSize,
                imageSize);

            if (entry == null)
            {
                Now.Rectangle(imageRect)
                    .SetRadius(4f)
                    .SetColor(theme.GetColor(NowColorToken.SurfaceMuted))
                    .SetOutline(1f)
                    .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                    .Draw();
                NowControls.DrawCenteredLabel(
                    theme,
                    imageRect.Inset(4f),
                    "🖼️",
                    NowTextStyle.Title,
                    imageRect,
                    Color.white);
                var emptyLabel = new NowRect(
                    bodyRect.x + 8f,
                    imageRect.yMax + 5f,
                    Mathf.Max(0f, bodyRect.width - 16f),
                    22f);
                NowControls.DrawLeftLabel(
                    theme,
                    emptyLabel,
                    "Select a file",
                    NowTextStyle.Body,
                    theme.GetColor(NowColorToken.Text));
                var emptyDetail = new NowRect(emptyLabel.x, emptyLabel.yMax, emptyLabel.width, 20f);
                NowControls.DrawLeftLabel(
                    theme,
                    emptyDetail,
                    "PNG and JPEG show a preview",
                    NowTextStyle.Muted,
                    theme.GetColor(NowColorToken.TextMuted));
                return;
            }

            ThumbnailEntry thumbnail = DrawThumbnailFrame(state, imageRect, entry);
            var nameRect = new NowRect(
                bodyRect.x + 8f,
                imageRect.yMax + 5f,
                Mathf.Max(0f, bodyRect.width - 16f),
                22f);
            NowControls.DrawLeftLabel(theme, nameRect, entry.name, NowTextStyle.Body, theme.GetColor(NowColorToken.Text));

            string detail = thumbnail != null && !string.IsNullOrEmpty(thumbnail.dimensions)
                ? thumbnail.dimensions
                : entry.type;
            var detailRect = new NowRect(nameRect.x, nameRect.yMax, nameRect.width, 20f);
            NowControls.DrawLeftLabel(theme, detailRect, detail, NowTextStyle.Muted, theme.GetColor(NowColorToken.TextMuted));
        }

        static void DrawEntryIcon(PopupState state, NowRect rect, BrowserEntry entry)
        {
            ThumbnailEntry thumbnail = GetThumbnailEntry(state, entry);

            if (thumbnail?.texture != null)
            {
                Now.Rectangle(rect)
                    .SetTexture(thumbnail.texture)
                    .SetPreserveAspect()
                    .SetColor(Color.white)
                    .Draw();
                return;
            }

            if (IsUnityEditorTheme(state.themeAsset))
            {
                Color color = IsSelectedEntry(state, entry)
                    ? state.themeAsset.GetColor(NowColorToken.AccentText)
                    : state.themeAsset.GetColor(NowColorToken.TextMuted);
                if (entry.directory)
                    DrawEditorFolderIcon(state.themeAsset, rect, color);
                else
                    DrawEditorFileIcon(state.themeAsset, rect, color);
                return;
            }

            NowControls.DrawCenteredLabel(
                state.themeAsset,
                rect,
                string.IsNullOrEmpty(entry.icon) ? "📄" : entry.icon,
                NowTextStyle.Body,
                rect,
                Color.white);
        }

        static ThumbnailEntry DrawThumbnailFrame(PopupState state, NowRect rect, BrowserEntry entry)
        {
            var theme = state.themeAsset;
            bool editorChrome = IsUnityEditorTheme(theme);
            Color border = theme.GetColor(
                editorChrome ? NowColorToken.BorderStrong : NowColorToken.Border);
            Now.Rectangle(rect)
                .SetRadius(PickerRadius(theme, 4f))
                .SetColor(editorChrome
                    ? PickerPaneSurface(theme)
                    : theme.GetColor(NowColorToken.SurfaceMuted))
                .SetOutline(1f)
                .SetOutlineColor(border)
                .Draw();

            ThumbnailEntry thumbnail = GetThumbnailEntry(state, entry);
            NowRect content = rect.Inset(4f);

            if (thumbnail?.texture != null)
            {
                Now.Rectangle(content)
                    .SetTexture(thumbnail.texture)
                    .SetPreserveAspect()
                    .SetColor(Color.white)
                    .Draw();
            }
            else
            {
                if (editorChrome)
                {
                    if (entry.directory)
                        DrawEditorFolderIcon(theme, content, theme.GetColor(NowColorToken.TextMuted));
                    else
                        DrawEditorFileIcon(theme, content, theme.GetColor(NowColorToken.TextMuted));
                }
                else
                {
                    NowControls.DrawCenteredLabel(
                        theme,
                        content,
                        string.IsNullOrEmpty(entry.icon) ? "📄" : entry.icon,
                        NowTextStyle.Title,
                        content,
                        Color.white);
                }
            }

            return thumbnail;
        }

        static ThumbnailEntry GetThumbnailEntry(PopupState state, BrowserEntry browserEntry)
        {
            if (!state.previewResourcesActive ||
                browserEntry == null ||
                !browserEntry.previewable ||
                string.IsNullOrEmpty(browserEntry.path))
                return null;

            if (!state.thumbnails.TryGetValue(browserEntry.path, out var thumbnail))
            {
                thumbnail = new ThumbnailEntry
                {
                    path = browserEntry.path,
                    state = ThumbnailState.Pending
                };
                state.thumbnails[browserEntry.path] = thumbnail;
            }

            thumbnail.lastAccess = ++state.thumbnailAccess;

            if (thumbnail.state == ThumbnailState.Pending)
                StartThumbnailRequest(state, thumbnail);

            if (thumbnail.state == ThumbnailState.Loading)
                NowControlState.RequestRepaint();

            TrimThumbnailCache(state, thumbnail);
            return thumbnail;
        }

        static void StartThumbnailRequest(PopupState state, ThumbnailEntry entry)
        {
            if (entry == null || entry.state != ThumbnailState.Pending)
                return;

            if (state.activeThumbnailRequests >= MaxThumbnailRequests)
            {
                NowControlState.RequestRepaint();
                return;
            }

            try
            {
                var file = new FileInfo(entry.path);

                if (!file.Exists || file.Length <= 0L || file.Length > MaxThumbnailFileBytes)
                {
                    entry.state = ThumbnailState.Failed;
                    return;
                }

                if (!TryReadEncodedImageSize(file.FullName, out int width, out int height) ||
                    !IsThumbnailSourceSizeAllowed(width, height))
                {
                    entry.state = ThumbnailState.Failed;
                    return;
                }

                var uri = new Uri(file.FullName);
                var parameters = DownloadedTextureParams.Default;
                parameters.readable = false;
                parameters.mipmapChain = false;
                parameters.linearColorSpace = false;
                var request = UnityWebRequestTexture.GetTexture(uri, parameters);
                request.timeout = 15;
                entry.request = request;
                entry.operation = request.SendWebRequest();
                entry.state = ThumbnailState.Loading;
                ++state.activeThumbnailRequests;
                NowControlState.RequestRepaint();
            }
            catch (Exception)
            {
                entry.request?.Dispose();
                entry.request = null;
                entry.operation = null;
                entry.state = ThumbnailState.Failed;
            }
        }

        static void PollThumbnailRequests(PopupState state)
        {
            if (state.thumbnails.Count == 0)
                return;

            foreach (var pair in state.thumbnails)
            {
                var entry = pair.Value;

                if (entry.state == ThumbnailState.Loading &&
                    entry.operation != null &&
                    entry.operation.isDone)
                {
                    CompleteThumbnailRequest(state, entry);
                }
            }

            if (state.activeThumbnailRequests > 0)
                NowControlState.RequestRepaint();

            TrimThumbnailCache(state, null);
        }

        static bool IsThumbnailSourceSizeAllowed(int width, int height)
        {
            return width > 0 &&
                   height > 0 &&
                   width <= MaxThumbnailSourceDimension &&
                   height <= MaxThumbnailSourceDimension &&
                   (long)width * height <= MaxThumbnailSourcePixels;
        }

        static bool TryReadEncodedImageSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            try
            {
                using var stream = File.OpenRead(path);
                var header = new byte[24];
                var read = 0;
                while (read < header.Length)
                {
                    var count = stream.Read(header, read, header.Length - read);
                    if (count <= 0)
                        break;

                    read += count;
                }

                if (read >= 24 &&
                    header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                    header[12] == 0x49 && header[13] == 0x48 && header[14] == 0x44 && header[15] == 0x52)
                {
                    width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    return width > 0 && height > 0;
                }

                if (read < 2 || header[0] != 0xFF || header[1] != 0xD8)
                    return false;

                stream.Position = 2;
                var scanLimit = stream.Length;
                while (stream.Position + 4 <= scanLimit)
                {
                    var prefix = stream.ReadByte();
                    if (prefix < 0)
                        return false;

                    if (prefix != 0xFF)
                        return false;

                    int marker;
                    do
                    {
                        marker = stream.ReadByte();
                    }
                    while (marker == 0xFF && stream.Position < scanLimit);

                    if (marker < 0 || marker == 0xD9 || marker == 0xDA)
                        return false;

                    if (marker == 0x00 || marker == 0x01 || marker == 0xD8 ||
                        (marker >= 0xD0 && marker <= 0xD7))
                    {
                        continue;
                    }

                    var segmentLengthHigh = stream.ReadByte();
                    var segmentLengthLow = stream.ReadByte();
                    if (segmentLengthHigh < 0 || segmentLengthLow < 0)
                        return false;

                    var segmentLength = (segmentLengthHigh << 8) | segmentLengthLow;
                    if (segmentLength < 2)
                        return false;

                    var isStartOfFrame = (marker >= 0xC0 && marker <= 0xC3) ||
                                         (marker >= 0xC5 && marker <= 0xC7) ||
                                         (marker >= 0xC9 && marker <= 0xCB) ||
                                         (marker >= 0xCD && marker <= 0xCF);
                    if (isStartOfFrame)
                    {
                        if (segmentLength < 7)
                            return false;

                        var precision = stream.ReadByte();
                        var heightHigh = stream.ReadByte();
                        var heightLow = stream.ReadByte();
                        var widthHigh = stream.ReadByte();
                        var widthLow = stream.ReadByte();
                        if (precision < 0 || heightHigh < 0 || heightLow < 0 || widthHigh < 0 || widthLow < 0)
                            return false;

                        height = (heightHigh << 8) | heightLow;
                        width = (widthHigh << 8) | widthLow;
                        return width > 0 && height > 0;
                    }

                    var skip = segmentLength - 2L;
                    if (stream.Position + skip > scanLimit)
                        return false;

                    stream.Seek(skip, SeekOrigin.Current);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }

            return false;
        }

        static void CompleteThumbnailRequest(PopupState state, ThumbnailEntry entry)
        {
            var request = entry.request;
            entry.request = null;
            entry.operation = null;
            state.activeThumbnailRequests = Mathf.Max(0, state.activeThumbnailRequests - 1);
            Texture2D source = null;

            try
            {
                if (request == null || request.result != UnityWebRequest.Result.Success)
                {
                    entry.state = ThumbnailState.Failed;
                    return;
                }

                source = DownloadHandlerTexture.GetContent(request);

                if (source == null ||
                    source.width < 1 ||
                    source.height < 1 ||
                    !IsThumbnailSourceSizeAllowed(source.width, source.height))
                {
                    entry.state = ThumbnailState.Failed;
                    return;
                }

                entry.dimensions = source.width + " × " + source.height;
                Texture thumbnail = CreateThumbnailTexture(source, ThumbnailDimension);

                if (thumbnail == null)
                {
                    entry.state = ThumbnailState.Failed;
                    return;
                }

                if (!ReferenceEquals(thumbnail, source))
                {
                    DestroyThumbnailTexture(source);
                    source = null;
                }

                entry.texture = thumbnail;
                entry.state = ThumbnailState.Loaded;
            }
            catch (Exception)
            {
                entry.state = ThumbnailState.Failed;
            }
            finally
            {
                request?.Dispose();

                if (entry.state != ThumbnailState.Loaded && source != null)
                    DestroyThumbnailTexture(source);

                NowControlState.RequestRepaint();
            }
        }

        static Texture CreateThumbnailTexture(Texture2D source, int maxDimension)
        {
            Vector2Int size = NowFilePickerUtility.ThumbnailSize(
                source != null ? source.width : 0,
                source != null ? source.height : 0,
                maxDimension);

            if (source == null || size.x <= 0 || size.y <= 0)
                return null;

            source.name = "NowUI File Preview";
            source.hideFlags = HideFlags.HideAndDontSave;
            source.filterMode = FilterMode.Bilinear;
            source.wrapMode = TextureWrapMode.Clamp;

            if (size.x == source.width && size.y == source.height)
                return source;

            RenderTexture result = null;

            try
            {
                result = new RenderTexture(
                    size.x,
                    size.y,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default)
                {
                    name = "NowUI File Preview",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                if (!result.Create())
                {
                    DestroyThumbnailTexture(result);
                    return null;
                }

                Graphics.Blit(source, result);
                return result;
            }
            catch (Exception)
            {
                if (result != null)
                    DestroyThumbnailTexture(result);
                return null;
            }
        }

        static void TrimThumbnailCache(PopupState state, ThumbnailEntry preserve)
        {
            while (state.thumbnails.Count > MaxThumbnailEntries)
            {
                ThumbnailEntry oldest = null;

                foreach (var pair in state.thumbnails)
                {
                    var candidate = pair.Value;

                    if (ReferenceEquals(candidate, preserve) || candidate.state == ThumbnailState.Loading)
                        continue;

                    if (oldest == null || candidate.lastAccess < oldest.lastAccess)
                        oldest = candidate;
                }

                if (oldest == null)
                    break;

                state.thumbnails.Remove(oldest.path);
                oldest.request?.Abort();
                oldest.request?.Dispose();

                if (oldest.texture != null)
                    ReleaseThumbnailTexture(oldest.texture, defer: true);
            }
        }

        static void ReleaseThumbnailResources(PopupState state, bool deferLoadedTextures = false)
        {
            CancelThumbnailRequests(state);

            foreach (var pair in state.thumbnails)
            {
                var thumbnail = pair.Value;

                if (thumbnail.texture != null)
                    ReleaseThumbnailTexture(thumbnail.texture, deferLoadedTextures);
            }

            state.thumbnails.Clear();
            state.activeThumbnailRequests = 0;
            state.thumbnailAccess = 0L;
            state.previewResourcesActive = false;
        }

        static void CancelThumbnailRequests(PopupState state)
        {
            foreach (var pair in state.thumbnails)
            {
                var thumbnail = pair.Value;

                if (thumbnail.state != ThumbnailState.Loading)
                    continue;

                thumbnail.request?.Abort();
                thumbnail.request?.Dispose();
                thumbnail.request = null;
                thumbnail.operation = null;
                thumbnail.state = ThumbnailState.Failed;
            }

            state.activeThumbnailRequests = 0;
        }

        static void ClosePopup(PopupState state)
        {
            ReleaseThumbnailResources(state, deferLoadedTextures: true);
            NowControlState.Get<bool>(state.id) = false;
        }

        static void ReleaseThumbnailTexture(Texture texture, bool defer)
        {
            if (texture == null)
                return;

#if UNITY_EDITOR
            if (defer && !Application.isPlaying)
            {
                _deferredThumbnailReleases.Add(texture);
                NowEditorRebuildQueue.Queue(ref s_editorThumbnailReleaseQueued, FlushDeferredThumbnailReleases);
                return;
            }
#endif

            DestroyThumbnailTexture(texture);
        }

        static void FlushDeferredThumbnailReleases()
        {
#if UNITY_EDITOR
            s_editorThumbnailReleaseQueued = false;
#endif

            for (int i = 0; i < _deferredThumbnailReleases.Count; ++i)
                DestroyThumbnailTexture(_deferredThumbnailReleases[i]);

            _deferredThumbnailReleases.Clear();
        }

        static void ReleaseAllThumbnailResources()
        {
            foreach (var pair in _popupStates)
            {
                var state = pair.Value;
                ReleaseThumbnailResources(state);
                MarkPopupClosed(state);
            }

#if UNITY_EDITOR
            NowEditorRebuildQueue.Cancel(ref s_editorThumbnailReleaseQueued, FlushDeferredThumbnailReleases);
#endif
            FlushDeferredThumbnailReleases();
        }

        static void ClearPopupStateMaps()
        {
            _popupStates.Clear();
            _popupStatesByCallback.Clear();
            _releasedPopupStateIds.Clear();
            s_nextPopupState = 1;
        }

        /// <summary>
        /// Clears a popup's open flag, if it ever had an id to key one by.
        /// </summary>
        /// <remarks>
        /// A popup state is created the first time the picker draws, but the
        /// resolved id is only filled in when the popup opens. Teardown
        /// reaches every state regardless, so asking
        /// <see cref="NowControlState"/> for a default id throws
        /// "a resolved child path requires a non-empty parent" out of
        /// OnDisable, which takes the whole graphic down with it.
        /// </remarks>
        static void MarkPopupClosed(PopupState state)
        {
            if (state.id.hasValue)
                NowControlState.Get<bool>(state.id) = false;
        }

        static void ReleaseRegistrationOwner(object owner)
        {
            if (owner == null || _popupStates.Count == 0)
                return;

            foreach (var pair in _popupStates)
            {
                var state = pair.Value;

                if (!ReferenceEquals(state.registrationOwner, owner))
                    continue;

                ReleaseThumbnailResources(state, deferLoadedTextures: true);
                MarkPopupClosed(state);
                _popupStatesByCallback.Remove(state.callbackState);
                _releasedPopupStateIds.Add(pair.Key);
            }

            for (int i = 0; i < _releasedPopupStateIds.Count; ++i)
                _popupStates.Remove(_releasedPopupStateIds[i]);

            _releasedPopupStateIds.Clear();
        }

        static void ReleaseExpiredRegistrationOwner(object owner)
        {
            if (owner == null)
                return;

            foreach (var pair in _popupStates)
            {
                var state = pair.Value;

                if (!ReferenceEquals(state.registrationOwner, owner))
                    continue;

                state.registrationOwner = null;

                if (!state.previewResourcesActive)
                    continue;

                ReleaseThumbnailResources(state, deferLoadedTextures: true);
                MarkPopupClosed(state);
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorThumbnailCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAllThumbnailResources;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseAllThumbnailResources;
            UnityEditor.EditorApplication.quitting -= ReleaseAllThumbnailResources;
            UnityEditor.EditorApplication.quitting += ReleaseAllThumbnailResources;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode)
                return;

            ReleaseAllThumbnailResources();
            ClearPopupStateMaps();
        }
#endif

        static void DestroyThumbnailTexture(Texture texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }

            if (texture is RenderTexture renderTexture && renderTexture.IsCreated())
                renderTexture.Release();

            UnityEngine.Object.DestroyImmediate(texture);
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

        static void SynchronizeSidebarToDirectory(
            PopupState state,
            string directory,
            FolderNavigationSource source)
        {
            state.pendingUserFolderFocusId = 0;
            state.pendingTreeFocusKey = null;

            if (source == FolderNavigationSource.Place)
            {
                state.sidebarLocationSource = SidebarLocationSource.Places;
                return;
            }

            if (source == FolderNavigationSource.Tree)
            {
                state.sidebarLocationSource = SidebarLocationSource.Tree;
                return;
            }

            var platform = NowFilePickerUserFolders.Platform(Application.platform);
            int userFolderIndex = NowFilePickerUserFolders.IndexOfPath(state.userFolders, directory, platform);

            if (userFolderIndex >= 0)
            {
                state.sidebarLocationSource = SidebarLocationSource.Places;
                state.pendingUserFolderFocusId = state.userFolders[userFolderIndex].stableId;
                return;
            }

            state.sidebarLocationSource = SidebarLocationSource.Tree;
            RevealFolderInTree(state, directory, focus: true, expandTarget: false);
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

        static NowResolvedId FolderTreeRowId(PopupState state, FolderTreeEntry entry, int fallbackIndex)
        {
            return string.IsNullOrEmpty(entry.key)
                ? state.treeSeed.Child(fallbackIndex + 1)
                : state.treeSeed.Child(entry.key);
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
                        directory = false,
                        previewable = NowFilePickerUtility.IsPreviewableImage(files[i])
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

        static void NavigateTo(
            PopupState state,
            string directory,
            FolderNavigationSource source = FolderNavigationSource.Automatic)
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
            SynchronizeSidebarToDirectory(state, full, source);
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

            if (focusFileName && state.fileNameFieldId.hasValue)
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
            ClosePopup(state);
        }

        static void HandleDismiss(PopupState state)
        {
            if (NowOverlay.HasNestedOverlay(state.id))
                return;

            var snapshot = NowInput.current;
            bool fieldPressClaimedByField = state.fieldRect.Contains(snapshot.pointerPosition) &&
                NowInput.IsActiveControl(state.id);
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !fieldPressClaimedByField;

            if (pressedOutside || (snapshot.cancelPressed && !NowInput.cancelConsumed))
            {
                if (pressedOutside)
                    NowInput.ConsumePointerPress();

                if (snapshot.cancelPressed)
                    NowInput.ConsumeKeyActivity();

                ClosePopup(state);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Application.quitting -= ReleaseAllThumbnailResources;
            Application.quitting += ReleaseAllThumbnailResources;
            ReleaseAllThumbnailResources();
            ClearPopupStateMaps();
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
        public NowFilePickerView initialView;
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
                initialView = NowFilePickerView.Details,
                fieldHeight = 30f,
                popupWidth = 760f,
                popupHeight = 460f,
                popupPadding = 10f,
                popupSpacing = 8f
            };
        }
    }

    static class NowFilePickerUtility
    {
        public static NowRect FitModalRect(NowRect preferred, NowRect surface, float margin)
        {
            if (preferred.isEmpty || surface.isEmpty)
                return preferred;

            margin = Mathf.Max(0f, margin);
            float width = Mathf.Min(preferred.width, Mathf.Max(1f, surface.width - margin * 2f));
            float height = Mathf.Min(preferred.height, Mathf.Max(1f, surface.height - margin * 2f));
            return new NowRect(
                surface.x + (surface.width - width) * 0.5f,
                surface.y + (surface.height - height) * 0.5f,
                width,
                height);
        }

        public static NowFilePickerView ClampView(NowFilePickerView view)
        {
            return (NowFilePickerView)Mathf.Clamp(
                (int)view,
                (int)NowFilePickerView.Details,
                (int)NowFilePickerView.LargeThumbnails);
        }

        public static bool IsPreviewableImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string extension;

            try
            {
                extension = NormalizeExtension(Path.GetExtension(path));
            }
            catch (ArgumentException)
            {
                return false;
            }

            return extension == "png" || extension == "jpg" || extension == "jpeg";
        }

        public static bool FilterSupportsImagePreview(NowFileFilter[] filters, int filterIndex)
        {
            if (filters == null || filters.Length == 0)
                return true;

            NowFileFilter filter = filters[Mathf.Clamp(filterIndex, 0, filters.Length - 1)];
            string[] extensions = filter.extensions;

            if (extensions == null || extensions.Length == 0)
                return true;

            for (int i = 0; i < extensions.Length; ++i)
            {
                if (IsPreviewFilterExtension(extensions[i]))
                    return true;
            }

            return false;
        }

        static bool IsPreviewFilterExtension(string extension)
        {
            return string.Equals(extension, "*", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "*.*", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "*.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "*.jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, "*.jpeg", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldShowPreviewPanel(
            NowFileDialogMode mode,
            NowFilePickerView view,
            float listWidth,
            NowFileFilter[] filters,
            int filterIndex)
        {
            return mode == NowFileDialogMode.OpenFile &&
                view == NowFilePickerView.Details &&
                listWidth >= 420f &&
                FilterSupportsImagePreview(filters, filterIndex);
        }

        public static Vector2Int ThumbnailSize(int width, int height, int maxDimension)
        {
            if (width <= 0 || height <= 0 || maxDimension <= 0)
                return Vector2Int.zero;

            float scale = Mathf.Min(1f, maxDimension / (float)Mathf.Max(width, height));
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(height * scale)));
        }

        public static int GridColumnCount(float width, float preferredWidth, float gap)
        {
            if (width <= 0f || preferredWidth <= 0f)
                return 1;

            gap = Mathf.Max(0f, gap);
            return Mathf.Max(1, Mathf.FloorToInt((width + gap) / (preferredWidth + gap)));
        }

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
            return new NowFilePicker(rect, NowFileDialogMode.OpenFile, id, NowControls.SiteToken(file, line));
        }

        public static NowFilePicker SaveFileField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, NowFileDialogMode.SaveFile, id, NowControls.SiteToken(file, line));
        }

        public static NowFilePicker DirectoryField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, NowFileDialogMode.Directory, id, NowControls.SiteToken(file, line));
        }

        public static NowFilePicker FilePicker(NowRect rect, NowFileDialogMode mode, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(rect, mode, id, NowControls.SiteToken(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowFilePicker OpenFileField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(NowFileDialogMode.OpenFile, id, NowControls.SiteToken(file, line));
        }

        public static NowFilePicker SaveFileField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(NowFileDialogMode.SaveFile, id, NowControls.SiteToken(file, line));
        }

        public static NowFilePicker DirectoryField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(NowFileDialogMode.Directory, id, NowControls.SiteToken(file, line));
        }

        public static NowFilePicker FilePicker(NowFileDialogMode mode, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowFilePicker(mode, id, NowControls.SiteToken(file, line));
        }
    }
}
