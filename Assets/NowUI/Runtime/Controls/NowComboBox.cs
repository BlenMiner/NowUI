using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Searchable dropdown: closed it looks like a dropdown field, open it becomes
    /// a text filter over the options with a popup of matches.
    /// <code>NowLayout.ComboBox(countryNames).Draw(ref countryIndex);</code>
    /// Up/down move the highlighted match, submit commits it (the first match
    /// when nothing is highlighted), cancel closes. The open filter field draws
    /// on the popup's overlay layer so it stays typable while the popup blocks
    /// the surfaces beneath. Selection applies on the next frame's Draw,
    /// matching dropdown behavior.
    /// </summary>
    [NowBuilder]
    public struct NowComboBox
    {
        NowId _id;
        readonly int _site;
        readonly IReadOnlyList<string> _options;
        IReadOnlyList<string> _optionDetails;
        NowFocusNavigation _navigation;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _fitToView;
        bool _allowCustomValue;
        float _popupMinWidth;
        string _placeholder;

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public IReadOnlyList<string> options;
            public IReadOnlyList<string> optionDetails;
            public readonly List<int> filteredIndices = new List<int>(16);
            public int id;
            public int selected;
            public int highlight;
            public bool highlightMovedByKeyboard;
            public int pendingId;
            public string pendingCustomValue;
            public int filterId;
            public int itemSeed;
            public int scrollId;
            public bool scrolls;
            public float itemHeight;
            public string filter = string.Empty;
            public string placeholder;
            public bool allowCustomValue;
            public NowRect field;
            public NowRect fieldLocal;
            public NowRect popupRect;
            public NowRect itemArea;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(8);

        internal NowComboBox(NowId id, IReadOnlyList<string> options, int site)
        {
            _id = id;
            _site = site;
            _options = options;
            _optionDetails = null;
            _navigation = default;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
            _fitToView = true;
            _allowCustomValue = false;
            _popupMinWidth = 0f;
            _placeholder = "Search...";
        }

        internal NowComboBox(NowRect rect, NowId id, IReadOnlyList<string> options, int site) : this(id, options, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowComboBox SetOptions(NowLayoutOptions options) { _layoutOptions = options; return this; }

        public NowComboBox SetWidth(float width) { _layoutOptions = _layoutOptions.SetWidth(width); return this; }

        public NowComboBox SetStretchWidth(float weight = 1f) { _layoutOptions = _layoutOptions.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowComboBox SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowComboBox SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>When true (default), moves the popup to stay inside the visible surface or world camera view.</summary>
        public NowComboBox SetFitToView(bool fitToView = true) { _fitToView = fitToView; return this; }

        /// <summary>Allows <see cref="Draw(ref string)"/> to commit typed values that are not in the option list.</summary>
        public NowComboBox SetAllowCustomValue(bool allowCustomValue = true) { _allowCustomValue = allowCustomValue; return this; }

        /// <summary>Minimum popup width in local UI units. The closed field width remains unchanged.</summary>
        public NowComboBox SetPopupMinWidth(float width) { _popupMinWidth = Mathf.Max(0f, width); return this; }

        /// <summary>Optional secondary text per option, shown in popup rows and matched by filtering.</summary>
        public NowComboBox SetOptionDetails(IReadOnlyList<string> details) { _optionDetails = details; return this; }

        /// <summary>Placeholder shown in the open filter field.</summary>
        public NowComboBox SetPlaceholder(string placeholder) { _placeholder = placeholder; return this; }

        public bool Draw(ref int selected)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);
            int filterId = NowInput.CombineId(id, 0x4e434246);
            int optionCount = _options?.Count ?? 0;

            ref int pending = ref NowControlState.Get<int>(id, "pending");
            bool changed = false;

            if (pending > 0 && pending - 1 < optionCount)
            {
                int next = pending - 1;
                changed = next != selected;
                selected = next;
            }

            pending = 0;

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = textStyle.Measure("Ag").y;
            if (lineHeight <= 0f)
                lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            ref bool open = ref NowControlState.Get<bool>(id);

            if (!open)
            {
                var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);

                if ((interaction.clicked || submitted) && optionCount > 0)
                {
                    open = true;
                    var openState = GetState(id);
                    openState.filter = string.Empty;
                    openState.highlight = -1;
                    NowFocus.Focus(filterId);
                }

                float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
                string current = selected >= 0 && selected < optionCount ? _options[selected] : string.Empty;
                renderer.DrawDropdownField(new NowDropdownFieldRenderContext(theme, rect, current, false, interaction, focused, hoverT));
                return changed;
            }

            var state = GetState(id);
            state.options = _options;
            state.optionDetails = _optionDetails;
            state.allowCustomValue = false;
            Filter(state, _options, _optionDetails, optionCount, state.filter ?? string.Empty);

            if (!NowInput.isPassive)
            {
                float navY = NowInput.current.navigation.y;

                if (NowControlState.Repeat(id, "highlight", Mathf.Abs(navY) > 0.55f, 0.35f, 0.12f) && state.filteredIndices.Count > 0)
                {
                    int step = navY < 0f ? 1 : -1;
                    state.highlight = Mathf.Clamp(state.highlight + step, 0, state.filteredIndices.Count - 1);
                    state.highlightMovedByKeyboard = true;
                    NowControlState.RequestRepaint();
                }

                var snapshot = NowInput.current;

                if (snapshot.submitPressed && state.filteredIndices.Count > 0)
                {
                    int row = state.highlight >= 0 && state.highlight < state.filteredIndices.Count
                        ? state.highlight
                        : 0;
                    pending = state.filteredIndices[row] + 1;
                    open = false;
                    NowFocus.Clear();
                }

                if (snapshot.cancelPressed && !NowInput.cancelConsumed && !NowOverlay.HasNestedOverlay(id))
                {
                    open = false;
                    NowFocus.Clear();
                }
            }

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, _options, _optionDetails, id, filterId, rect, selected, optionCount, _fitToView, _popupMinWidth, _placeholder, false);
            return changed;
        }

        public bool Draw(ref string value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);
            int filterId = NowInput.CombineId(id, 0x4e434246);
            int optionCount = _options?.Count ?? 0;

            value ??= string.Empty;

            ref int pending = ref NowControlState.Get<int>(id, "pending");
            var state = GetState(id);
            bool changed = false;

            if (pending > 0 && pending - 1 < optionCount)
            {
                string next = _options[pending - 1] ?? string.Empty;
                changed = next != value;
                value = next;
            }

            pending = 0;

            if (state.pendingCustomValue != null)
            {
                changed = state.pendingCustomValue != value;
                value = state.pendingCustomValue;
                state.pendingCustomValue = null;
            }

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = textStyle.Measure("Ag").y;
            if (lineHeight <= 0f)
                lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            ref bool open = ref NowControlState.Get<bool>(id);

            if (!open)
            {
                var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);

                if ((interaction.clicked || submitted) && (optionCount > 0 || _allowCustomValue))
                {
                    open = true;
                    var openState = GetState(id);
                    openState.filter = string.Empty;
                    openState.highlight = -1;
                    NowFocus.Focus(filterId);
                }

                float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
                renderer.DrawDropdownField(new NowDropdownFieldRenderContext(theme, rect, value, false, interaction, focused, hoverT));
                return changed;
            }

            state.options = _options;
            state.optionDetails = _optionDetails;
            state.allowCustomValue = _allowCustomValue;
            Filter(state, _options, _optionDetails, optionCount, state.filter ?? string.Empty);

            if (!NowInput.isPassive)
            {
                int rowCount = RowCount(state);
                float navY = NowInput.current.navigation.y;

                if (NowControlState.Repeat(id, "highlight", Mathf.Abs(navY) > 0.55f, 0.35f, 0.12f) && rowCount > 0)
                {
                    int step = navY < 0f ? 1 : -1;
                    state.highlight = Mathf.Clamp(state.highlight + step, 0, rowCount - 1);
                    state.highlightMovedByKeyboard = true;
                    NowControlState.RequestRepaint();
                }

                var snapshot = NowInput.current;

                if (snapshot.submitPressed && rowCount > 0)
                {
                    int row = state.highlight >= 0 && state.highlight < rowCount
                        ? state.highlight
                        : 0;
                    CommitRow(state, row);
                    open = false;
                    NowFocus.Clear();
                }

                if (snapshot.cancelPressed && !NowInput.cancelConsumed && !NowOverlay.HasNestedOverlay(id))
                {
                    open = false;
                    NowFocus.Clear();
                }
            }

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, _options, _optionDetails, id, filterId, rect, IndexOfOption(_options, value), optionCount, _fitToView, _popupMinWidth, _placeholder, _allowCustomValue);
            return changed;
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

        static void Filter(PopupState state, IReadOnlyList<string> options, IReadOnlyList<string> details, int optionCount, string filter)
        {
            state.filteredIndices.Clear();

            for (int i = 0; i < optionCount; ++i)
            {
                string option = options[i] ?? string.Empty;
                string detail = DetailAt(details, i);

                if (filter.Length == 0 ||
                    option.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(detail) && detail.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    state.filteredIndices.Add(i);
                }
            }

            int rowCount = state.filteredIndices.Count;

            if (state.allowCustomValue &&
                !string.IsNullOrWhiteSpace(filter) &&
                !HasExactOption(options, optionCount, filter.Trim()))
            {
                ++rowCount;
            }

            if (state.highlight >= rowCount)
                state.highlight = rowCount - 1;
        }

        static void DeferPopup(
            NowThemeAsset themeAsset,
            IReadOnlyList<string> options,
            IReadOnlyList<string> optionDetails,
            int id,
            int filterId,
            NowRect field,
            int selected,
            int optionCount,
            bool fitToView,
            float popupMinWidth,
            string placeholder,
            bool allowCustomValue)
        {
            var state = GetState(id);
            var styles = themeAsset.controlStyles;
            float itemHeight = HasAnyDetail(optionDetails, optionCount)
                ? Mathf.Max(styles.dropdownItemHeight, 42f)
                : styles.dropdownItemHeight;
            float popupPadding = styles.popupPadding;
            state.allowCustomValue = allowCustomValue;
            int rowCount = Mathf.Max(1, RowCount(state));
            float contentHeight = rowCount * itemHeight + popupPadding * 2f;
            float popupHeight = Mathf.Min(contentHeight, styles.dropdownMaxPopupHeight);
            float popupWidth = Mathf.Max(field.width, popupMinWidth);
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, popupWidth, popupHeight);

            if (fitToView)
                popupRect = NowOverlay.ClampToView(popupRect);

            state.themeAsset = themeAsset;
            state.options = options;
            state.optionDetails = optionDetails;
            state.id = id;
            state.selected = selected;
            state.pendingId = NowInput.GetId(id, "pending");
            state.filterId = filterId;
            state.itemSeed = NowInput.GetId(id, "item");
            state.scrollId = NowInput.GetId(id, "popup-scroll");
            state.scrolls = popupRect.height < contentHeight - 0.5f;
            state.itemHeight = itemHeight;
            state.placeholder = placeholder;
            state.field = Now.TransformScreenRect(field);
            state.fieldLocal = field;
            state.popupRect = popupRect;
            state.itemArea = popupRect.Inset(popupPadding);

            NowOverlay.BlockAllSurfaces(id);
            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state) || state.options == null)
                return;

            var themeAsset = state.themeAsset;
            var popupRect = state.popupRect;

            DrawFilterField(state);

            themeAsset.controlRenderer.DrawPopupBackground(themeAsset, popupRect, menu: false);
            RevealHighlight(state);

            if (RowCount(state) == 0)
            {
                NowControls.DrawLeftLabel(
                    themeAsset,
                    state.itemArea.Inset(8f, 0f, 4f, 0f),
                    "No matches",
                    NowTextStyle.Muted);
            }
            else if (state.scrolls)
            {
                using (new NowScrollView(state.itemArea, state.scrollId).Begin())
                    DrawItems(state);
            }
            else
            {
                DrawItems(state);
            }

            var snapshot = NowInput.current;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !state.field.Contains(snapshot.pointerPosition);

            if (pressedOutside)
            {
                NowControlState.Get<bool>(state.id) = false;

                if (NowFocus.focusedId == state.filterId)
                    NowFocus.Clear();
            }
        }

        /// <summary>
        /// The open filter draws on the popup's overlay layer, not in the base
        /// pass: the popup's focus layer would otherwise unfocus it after one
        /// frame, and the popup's modal pointer block would eat its clicks.
        /// Edits land in <see cref="PopupState.filter"/> and filter the list on
        /// the next frame's Draw.
        /// </summary>
        static void DrawFilterField(PopupState state)
        {
            string filter = state.filter ?? string.Empty;

            bool filterChanged = new NowTextField(state.fieldLocal, new NowId(state.filterId), 0)
                .SetPlaceholder(state.placeholder)
                .Draw(ref filter);

            if (!filterChanged)
                return;

            state.filter = filter;
            state.highlight = -1;
            NowControlState.RequestRepaint();
        }

        /// <summary>
        /// Shifts a scrolling popup just enough to reveal the keyboard-moved
        /// highlight; hover and free wheel scrolling never trigger it.
        /// </summary>
        static void RevealHighlight(PopupState state)
        {
            if (!state.highlightMovedByKeyboard)
                return;

            state.highlightMovedByKeyboard = false;

            if (!state.scrolls || state.highlight < 0)
                return;

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(state.scrollId);
            float top = state.highlight * state.itemHeight;
            float bottom = top + state.itemHeight;
            float viewHeight = state.itemArea.height;

            if (top < scroll.y)
                scroll.y = top;
            else if (bottom > scroll.y + viewHeight)
                scroll.y = bottom - viewHeight;
        }

        static void DrawItems(PopupState state)
        {
            int row = 0;

            for (int i = 0; i < state.filteredIndices.Count; ++i, ++row)
            {
                int optionIndex = state.filteredIndices[i];
                NowRect itemRect = ItemRect(state, row);
                var itemInteraction = NowInput.Interact(NowInput.CombineId(state.itemSeed, optionIndex + 1), itemRect);
                bool highlighted = row == state.highlight || optionIndex == state.selected;
                state.themeAsset.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    state.themeAsset,
                    itemRect,
                    state.options[optionIndex],
                    DetailAt(state.optionDetails, optionIndex),
                    highlighted,
                    itemInteraction));

                if (itemInteraction.clicked)
                {
                    NowControlState.Get<int>(state.pendingId) = optionIndex + 1;
                    NowControlState.Get<bool>(state.id) = false;
                    NowFocus.Clear();
                }
            }

            if (HasCustomValue(state))
            {
                NowRect customRect = ItemRect(state, row);
                var customInteraction = NowInput.Interact(NowInput.CombineId(state.itemSeed, CustomItemSeed), customRect);
                bool highlighted = row == state.highlight;
                state.themeAsset.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    state.themeAsset,
                    customRect,
                    "Use \"" + state.filter + "\"",
                    highlighted,
                    customInteraction));

                if (customInteraction.clicked)
                {
                    state.pendingCustomValue = state.filter ?? string.Empty;
                    NowControlState.Get<bool>(state.id) = false;
                    NowFocus.Clear();
                }
            }
        }

        const int CustomItemSeed = 0x4e434243;

        static NowRect ItemRect(PopupState state, int row)
        {
            return state.scrolls
                ? NowLayout.Rect(height: state.itemHeight, stretchWidth: true)
                : new NowRect(
                    state.itemArea.x,
                    state.itemArea.y + row * state.itemHeight,
                    state.itemArea.width,
                    state.itemHeight);
        }

        static void CommitRow(PopupState state, int row)
        {
            if (row >= 0 && row < state.filteredIndices.Count)
            {
                NowControlState.Get<int>(state.pendingId) = state.filteredIndices[row] + 1;
                return;
            }

            if (HasCustomValue(state) && row == state.filteredIndices.Count)
                state.pendingCustomValue = state.filter ?? string.Empty;
        }

        static int RowCount(PopupState state)
        {
            if (state == null)
                return 0;

            return state.filteredIndices.Count + (HasCustomValue(state) ? 1 : 0);
        }

        static bool HasAnyDetail(IReadOnlyList<string> details, int optionCount)
        {
            if (details == null)
                return false;

            int count = Mathf.Min(optionCount, details.Count);

            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrWhiteSpace(details[i]))
                    return true;
            }

            return false;
        }

        static string DetailAt(IReadOnlyList<string> details, int index)
        {
            return details != null && index >= 0 && index < details.Count
                ? details[index]
                : null;
        }

        static bool HasCustomValue(PopupState state)
        {
            if (state == null || !state.allowCustomValue || string.IsNullOrWhiteSpace(state.filter))
                return false;

            string filter = state.filter.Trim();
            int optionCount = state.options?.Count ?? 0;

            if (HasExactOption(state.options, optionCount, filter))
                return false;

            return true;
        }

        static bool HasExactOption(IReadOnlyList<string> options, int optionCount, string value)
        {
            if (options == null || value == null)
                return false;

            for (int i = 0; i < optionCount; i++)
            {
                if (string.Equals(options[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static int IndexOfOption(IReadOnlyList<string> options, string value)
        {
            if (options == null || value == null)
                return -1;

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], value, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
        }
    }
}
