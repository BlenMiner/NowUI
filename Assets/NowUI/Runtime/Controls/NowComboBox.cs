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
        NowFocusNavigation _navigation;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _fitToView;
        string _placeholder;

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public IReadOnlyList<string> options;
            public readonly List<int> filteredIndices = new List<int>(16);
            public int id;
            public int selected;
            public int highlight;
            public bool highlightMovedByKeyboard;
            public int pendingId;
            public int filterId;
            public int itemSeed;
            public int scrollId;
            public bool scrolls;
            public float itemHeight;
            public string filter = string.Empty;
            public string placeholder;
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
            _navigation = default;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
            _fitToView = true;
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
            Filter(state, _options, optionCount, state.filter ?? string.Empty);

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
            DeferPopup(theme, _options, id, filterId, rect, selected, optionCount, _fitToView, _placeholder);
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

        static void Filter(PopupState state, IReadOnlyList<string> options, int optionCount, string filter)
        {
            state.filteredIndices.Clear();

            for (int i = 0; i < optionCount; ++i)
            {
                string option = options[i] ?? string.Empty;

                if (filter.Length == 0 || option.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    state.filteredIndices.Add(i);
            }

            if (state.highlight >= state.filteredIndices.Count)
                state.highlight = state.filteredIndices.Count - 1;
        }

        static void DeferPopup(
            NowThemeAsset themeAsset,
            IReadOnlyList<string> options,
            int id,
            int filterId,
            NowRect field,
            int selected,
            int optionCount,
            bool fitToView,
            string placeholder)
        {
            var state = GetState(id);
            var styles = themeAsset.controlStyles;
            float itemHeight = styles.dropdownItemHeight;
            float popupPadding = styles.popupPadding;
            int rowCount = Mathf.Max(1, state.filteredIndices.Count);
            float contentHeight = rowCount * itemHeight + popupPadding * 2f;
            float popupHeight = Mathf.Min(contentHeight, styles.dropdownMaxPopupHeight);
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, field.width, popupHeight);

            if (fitToView)
                popupRect = NowOverlay.ClampToView(popupRect);

            state.themeAsset = themeAsset;
            state.options = options;
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

            if (state.filteredIndices.Count == 0)
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
            for (int row = 0; row < state.filteredIndices.Count; ++row)
            {
                int optionIndex = state.filteredIndices[row];
                NowRect itemRect = state.scrolls
                    ? NowLayout.Rect(height: state.itemHeight, stretchWidth: true)
                    : new NowRect(
                        state.itemArea.x,
                        state.itemArea.y + row * state.itemHeight,
                        state.itemArea.width,
                        state.itemHeight);

                var itemInteraction = NowInput.Interact(NowInput.CombineId(state.itemSeed, optionIndex + 1), itemRect);
                bool highlighted = row == state.highlight || optionIndex == state.selected;
                state.themeAsset.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    state.themeAsset,
                    itemRect,
                    state.options[optionIndex],
                    highlighted,
                    itemInteraction));

                if (itemInteraction.clicked)
                {
                    NowControlState.Get<int>(state.pendingId) = optionIndex + 1;
                    NowControlState.Get<bool>(state.id) = false;
                    NowFocus.Clear();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
        }
    }
}
