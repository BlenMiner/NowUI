using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Dropdown selector:
    /// <code>NowLayout.Dropdown(qualityNames).Draw(ref qualityIndex);</code>
    /// The popup draws through <see cref="NowOverlay"/> — above everything, and
    /// modal while open: a press outside only dismisses, it never activates the
    /// control beneath. Long lists clamp to the view and scroll. Arrows move a
    /// popup-local highlight from the selected item, submit commits it, typing
    /// jumps to the next option starting with that letter, and cancel closes.
    /// Selection from the popup applies on the next frame's Draw (deferred
    /// draws run after Draw returns).
    /// </summary>
    [NowBuilder]
    public struct NowDropdown
    {
        NowId _id;
        readonly int _site;
        readonly IReadOnlyList<string> _options;
        NowFocusNavigation _navigation;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _fitToView;

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public IReadOnlyList<string> options;
            public int id;
            public int selected;
            public int optionCount;
            public int pendingId;
            public int itemSeed;
            public int scrollId;
            public bool scrolls;
            public float itemHeight;
            public int highlight = -1;
            public bool highlightMovedByKeyboard;
            public int openedFrame;
            public NowRect field;
            public NowRect popupRect;
            public NowRect itemArea;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(8);

        internal NowDropdown(NowId id, IReadOnlyList<string> options, int site)
        {
            _id = id;
            _site = site;
            _options = options;
            _navigation = default;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
            _fitToView = true;
        }

        internal NowDropdown(NowRect rect, NowId id, IReadOnlyList<string> options, int site) : this(id, options, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowDropdown SetOptions(NowLayoutOptions options) { _layoutOptions = options; return this; }

        public NowDropdown SetWidth(float width) { _layoutOptions = _layoutOptions.SetWidth(width); return this; }

        public NowDropdown SetStretchWidth(float weight = 1f) { _layoutOptions = _layoutOptions.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowDropdown SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowDropdown SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>When true (default), moves the popup to stay inside the visible surface or world camera view.</summary>
        public NowDropdown SetFitToView(bool fitToView = true) { _fitToView = fitToView; return this; }

        public bool Draw(ref int selected)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);
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
            float lineHeight = textStyle.font != null
                ? textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize
                : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
            {
                open = !open;

                if (open)
                {
                    var openState = GetState(id);
                    openState.highlight = selected >= 0 && selected < optionCount ? selected : -1;
                    openState.highlightMovedByKeyboard = false;
                    openState.openedFrame = NowInput.current.frame;
                }
            }

            if (open && optionCount == 0)
                open = false;

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

            string current = selected >= 0 && selected < optionCount ? _options[selected] : string.Empty;
            renderer.DrawDropdownField(new NowDropdownFieldRenderContext(theme, rect, current, open, interaction, focused, hoverT));

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, _options, id, rect, selected, optionCount, _fitToView);
            return changed;
        }

        /// <summary>
        /// The popup closure lives here so its display class only allocates while
        /// the popup is open — captured locals in Draw would otherwise allocate at
        /// method entry on every frame, even with the popup closed.
        /// </summary>
        static void DeferPopup(
            NowThemeAsset themeAsset,
            IReadOnlyList<string> options,
            int id,
            NowRect field,
            int selected,
            int optionCount,
            bool fitToView)
        {
            var styles = themeAsset.controlStyles;
            float itemHeight = styles.dropdownItemHeight;
            float popupPadding = styles.popupPadding;
            float contentHeight = optionCount * itemHeight + popupPadding * 2f;
            float popupHeight = Mathf.Min(contentHeight, styles.dropdownMaxPopupHeight);
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, field.width, popupHeight);

            if (fitToView)
                popupRect = NowOverlay.ClampToView(popupRect);

            var state = GetState(id);

            state.themeAsset = themeAsset;
            state.options = options;
            state.id = id;
            state.selected = selected;
            state.optionCount = optionCount;
            state.pendingId = NowInput.GetId(id, "pending");
            state.itemSeed = NowInput.GetId(id, "item");
            state.scrollId = NowInput.GetId(id, "popup-scroll");
            state.scrolls = popupRect.height < contentHeight - 0.5f;
            state.itemHeight = itemHeight;
            state.field = Now.TransformScreenRect(field);
            state.popupRect = popupRect;
            state.itemArea = popupRect.Inset(popupPadding);

            NowOverlay.BlockAllSurfaces(id);
            NowOverlay.Defer(popupRect, id, DrawPopup);
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

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state) || state.options == null)
                return;

            var themeAsset = state.themeAsset;
            var popupRect = state.popupRect;

            themeAsset.controlRenderer.DrawPopupBackground(themeAsset, popupRect, menu: false);
            UpdateKeyboard(state);
            RevealHighlight(state);

            if (state.scrolls)
            {
                using (var scroll = new NowScrollView(state.itemArea, state.scrollId).Begin())
                    DrawVisibleItems(state, scroll.scrollOffset.y, scroll.viewport.height);
            }
            else
            {
                DrawItems(state);
            }

            var snapshot = NowInput.current;
            bool fieldPressClaimedByField = state.field.Contains(snapshot.pointerPosition) &&
                NowInput.activeId == state.id;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !fieldPressClaimedByField;

            if (pressedOutside ||
                (snapshot.cancelPressed && !NowInput.cancelConsumed && !NowOverlay.HasNestedOverlay(state.id)))
            {
                NowControlState.Get<bool>(state.id) = false;
            }
        }

        /// <summary>
        /// Menu-style keyboard driving for the open popup: a popup-local
        /// highlight (never <see cref="NowFocus"/> — items taking focus would
        /// clear focus-owned state elsewhere), arrows wrap, submit commits and
        /// typing a letter jumps to the next option starting with it. Base
        /// focus navigation is locked while the popup is open.
        /// </summary>
        static void UpdateKeyboard(PopupState state)
        {
            if (NowInput.isPassive)
                return;

            NowFocus.LockNavigation();

            var snapshot = NowInput.current;
            float navY = snapshot.navigation.y;

            if (NowControlState.Repeat(state.id, "highlight", Mathf.Abs(navY) > 0.55f, 0.35f, 0.12f) &&
                state.optionCount > 0)
            {
                MoveHighlight(state, navY < 0f ? 1 : -1);
            }

            TypeToSelect(state);

            if (snapshot.submitPressed &&
                snapshot.frame != state.openedFrame &&
                state.highlight >= 0 &&
                state.highlight < state.optionCount)
            {
                NowControlState.Get<int>(state.pendingId) = state.highlight + 1;
                NowControlState.Get<bool>(state.id) = false;
            }
        }

        static void MoveHighlight(PopupState state, int step)
        {
            int count = state.optionCount;
            int next;

            if (state.highlight < 0 || state.highlight >= count)
            {
                next = step > 0 ? 0 : count - 1;
            }
            else
            {
                next = state.highlight + step;

                if (next >= count)
                    next = 0;
                else if (next < 0)
                    next = count - 1;
            }

            SetHighlight(state, next);
        }

        static void TypeToSelect(PopupState state)
        {
            string typed = NowTextInput.current.characters;

            if (string.IsNullOrEmpty(typed) || state.optionCount == 0)
                return;

            char first = char.ToUpperInvariant(typed[0]);
            int start = state.highlight >= 0 && state.highlight < state.optionCount ? state.highlight : -1;

            for (int offset = 1; offset <= state.optionCount; ++offset)
            {
                int index = (start + offset) % state.optionCount;
                string option = state.options[index];

                if (!string.IsNullOrEmpty(option) && char.ToUpperInvariant(option[0]) == first)
                {
                    SetHighlight(state, index);
                    return;
                }
            }
        }

        static void SetHighlight(PopupState state, int index)
        {
            state.highlight = index;
            state.highlightMovedByKeyboard = true;
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
            for (int i = 0; i < state.optionCount; ++i)
            {
                NowRect itemRect = state.scrolls
                    ? NowLayout.ReserveRect(height: state.itemHeight, stretchWidth: true)
                    : new NowRect(
                        state.itemArea.x,
                        state.itemArea.y + i * state.itemHeight,
                        state.itemArea.width,
                        state.itemHeight);

                var itemInteraction = NowInput.Interact(NowInput.CombineId(state.itemSeed, i + 1), itemRect);
                state.themeAsset.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    state.themeAsset,
                    itemRect,
                    state.options[i],
                    i == state.selected || i == state.highlight,
                    itemInteraction));

                if (itemInteraction.clicked)
                {
                    NowControlState.Get<int>(state.pendingId) = i + 1;
                    NowControlState.Get<bool>(state.id) = false;
                }
            }
        }

        /// <summary>
        /// Processes only rows intersecting the scrolling popup. The leading and
        /// trailing spaces preserve the full layout extent, so scrollbar sizing,
        /// wheel movement and keyboard-driven reveal behave exactly like the
        /// unvirtualized list while clipped rows avoid text shaping, geometry and
        /// interaction work.
        /// </summary>
        static void DrawVisibleItems(PopupState state, float scrollY, float viewportHeight)
        {
            float itemHeight = state.itemHeight;

            if (state.optionCount <= 0 || itemHeight <= 0f)
                return;

            int first = Mathf.Clamp(Mathf.FloorToInt(scrollY / itemHeight), 0, state.optionCount);
            int end = Mathf.Clamp(
                Mathf.CeilToInt((scrollY + Mathf.Max(0f, viewportHeight)) / itemHeight),
                first,
                state.optionCount);

            if (first > 0)
                NowLayout.Space(first * itemHeight);

            for (int i = first; i < end; ++i)
            {
                NowRect itemRect = NowLayout.ReserveRect(height: itemHeight, stretchWidth: true);
                var itemInteraction = NowInput.Interact(NowInput.CombineId(state.itemSeed, i + 1), itemRect);
                state.themeAsset.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    state.themeAsset,
                    itemRect,
                    state.options[i],
                    i == state.selected || i == state.highlight,
                    itemInteraction));

                if (itemInteraction.clicked)
                {
                    NowControlState.Get<int>(state.pendingId) = i + 1;
                    NowControlState.Get<bool>(state.id) = false;
                }
            }

            if (end < state.optionCount)
                NowLayout.Space((state.optionCount - end) * itemHeight);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
        }
    }
}
