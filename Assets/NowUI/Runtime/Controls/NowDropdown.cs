using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Dropdown selector:
    /// <code>NowLayout.Dropdown(qualityNames).Draw(ref qualityIndex);</code>
    /// The popup draws through <see cref="NowOverlay"/> — above everything, with
    /// the controls underneath pointer-blocked — and closes on selection, on a
    /// click outside, or on cancel. Long lists scroll. Selection from the popup
    /// applies on the next frame's Draw (deferred draws run after Draw returns).
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
            float lineHeight = textStyle.Measure("Ag").y;
            if (lineHeight <= 0f)
                lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
                open = !open;

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
                popupRect = NowOverlay.FitToView(popupRect);

            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = themeAsset;
            state.options = options;
            state.id = id;
            state.selected = selected;
            state.optionCount = optionCount;
            state.pendingId = NowInput.GetId(id, "pending");
            state.itemSeed = NowInput.GetId(id, "item");
            state.scrollId = NowInput.GetId(id, "popup-scroll");
            state.scrolls = contentHeight > styles.dropdownMaxPopupHeight;
            state.itemHeight = itemHeight;
            state.field = Now.TransformScreenRect(field);
            state.popupRect = popupRect;
            state.itemArea = popupRect.Inset(popupPadding);

            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state) || state.options == null)
                return;

            var themeAsset = state.themeAsset;
            var popupRect = state.popupRect;

            themeAsset.controlRenderer.DrawPopupBackground(themeAsset, popupRect, menu: false);

            if (state.scrolls)
            {
                using (new NowScrollView(state.itemArea, state.scrollId).Begin())
                    DrawItems(state);
            }
            else
            {
                DrawItems(state);
            }

            var snapshot = NowInput.current;
            bool pressedOutside = snapshot.primaryPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !state.field.Contains(snapshot.pointerPosition);

            if (pressedOutside || (snapshot.cancelPressed && !NowOverlay.HasNestedOverlay(state.id)))
                NowControlState.Get<bool>(state.id) = false;
        }

        static void DrawItems(PopupState state)
        {
            for (int i = 0; i < state.optionCount; ++i)
            {
                NowRect itemRect = state.scrolls
                    ? NowLayout.Rect(height: state.itemHeight, stretchWidth: true)
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
                    i == state.selected,
                    itemInteraction));

                if (itemInteraction.clicked)
                {
                    NowControlState.Get<int>(state.pendingId) = i + 1;
                    NowControlState.Get<bool>(state.id) = false;
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
