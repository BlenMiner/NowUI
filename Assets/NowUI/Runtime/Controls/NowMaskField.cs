using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Multi-select dropdown over bit flags, mirroring Unity's mask fields:
    /// <code>NowLayout.MaskField(channelNames).Draw(ref channelMask);</code>
    /// Option i toggles bit <c>1 &lt;&lt; i</c>. The popup lists Nothing and
    /// Everything above the options and stays open while toggling; it closes on
    /// any press outside or on cancel. The field summarizes the mask as
    /// Nothing, Everything, the single set option, or "Mixed (n)".
    /// <see cref="Draw(ref LayerMask)"/> edits a <see cref="LayerMask"/> against
    /// the project's named layers (Everything stores -1, like Unity).
    /// </summary>
    [NowBuilder]
    public struct NowMaskField
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
            public int mask;
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

        struct PendingMask
        {
            public byte hasValue;
            public int value;
        }

        struct SummaryCache
        {
            public byte initialized;
            public int mask;
            public int optionCount;
            public string label;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(8);

        static string[] _layerNames;
        static int[] _layerIndices;

        internal NowMaskField(NowId id, IReadOnlyList<string> options, int site)
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

        internal NowMaskField(NowRect rect, NowId id, IReadOnlyList<string> options, int site) : this(id, options, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowMaskField SetOptions(NowLayoutOptions options) { _layoutOptions = options; return this; }

        public NowMaskField SetWidth(float width) { _layoutOptions = _layoutOptions.SetWidth(width); return this; }

        public NowMaskField SetStretchWidth(float weight = 1f) { _layoutOptions = _layoutOptions.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowMaskField SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowMaskField SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>When true (default), moves the popup to stay inside the visible surface or world camera view.</summary>
        public NowMaskField SetFitToView(bool fitToView = true) { _fitToView = fitToView; return this; }

        public bool Draw(ref int mask)
        {
            return DrawMask(_options, ref mask);
        }

        /// <summary>
        /// Edits a <see cref="LayerMask"/> against the project's named layers,
        /// ignoring any options the field was created with. Everything stores -1.
        /// </summary>
        public bool Draw(ref LayerMask value)
        {
            EnsureLayerCache();
            int compact = CompactFromLayerMask(value.value);
            int allBits = AllBits(_layerNames.Length);

            if (!DrawMask(_layerNames, ref compact))
                return false;

            value = (compact & allBits) == allBits ? -1 : LayerMaskFromCompact(compact);
            return true;
        }

        bool DrawMask(IReadOnlyList<string> options, ref int mask)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);
            int optionCount = Mathf.Min(options?.Count ?? 0, 32);
            int allBits = AllBits(optionCount);

            ref var pending = ref NowControlState.Get<PendingMask>(id, "pending");
            bool changed = false;

            if (pending.hasValue != 0)
            {
                int next = pending.value & allBits;
                changed = next != mask;
                mask = next;
            }

            pending.hasValue = 0;

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
            string summary = Summary(id, options, optionCount, mask, allBits);
            renderer.DrawDropdownField(new NowDropdownFieldRenderContext(theme, rect, summary, open, interaction, focused, hoverT));

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, options, id, rect, mask, optionCount, _fitToView);
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
            int mask,
            int optionCount,
            bool fitToView)
        {
            var styles = themeAsset.controlStyles;
            float itemHeight = styles.dropdownItemHeight;
            float popupPadding = styles.popupPadding;
            int rowCount = optionCount + 2;
            float contentHeight = rowCount * itemHeight + popupPadding * 2f;
            float popupHeight = Mathf.Min(contentHeight, styles.dropdownMaxPopupHeight);
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, field.width, popupHeight);

            if (fitToView)
                popupRect = NowOverlay.ClampToView(popupRect);

            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = themeAsset;
            state.options = options;
            state.id = id;
            state.mask = mask;
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

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state) || state.options == null)
                return;

            var themeAsset = state.themeAsset;

            themeAsset.controlRenderer.DrawPopupBackground(themeAsset, state.popupRect, menu: false);

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

        static void DrawItems(PopupState state)
        {
            int allBits = AllBits(state.optionCount);
            int rowCount = state.optionCount + 2;

            for (int row = 0; row < rowCount; ++row)
            {
                NowRect itemRect = state.scrolls
                    ? NowLayout.Rect(height: state.itemHeight, stretchWidth: true)
                    : new NowRect(
                        state.itemArea.x,
                        state.itemArea.y + row * state.itemHeight,
                        state.itemArea.width,
                        state.itemHeight);

                string label;
                bool selected;
                int next;

                if (row == 0)
                {
                    label = "Nothing";
                    selected = state.mask == 0;
                    next = 0;
                }
                else if (row == 1)
                {
                    label = "Everything";
                    selected = (state.mask & allBits) == allBits && allBits != 0;
                    next = allBits;
                }
                else
                {
                    int bit = 1 << (row - 2);
                    label = state.options[row - 2];
                    selected = (state.mask & bit) != 0;
                    next = state.mask ^ bit;
                }

                var itemInteraction = NowInput.Interact(NowInput.CombineId(state.itemSeed, row + 1), itemRect);
                state.themeAsset.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    state.themeAsset,
                    itemRect,
                    label,
                    selected,
                    itemInteraction));

                if (itemInteraction.clicked)
                {
                    ref var pending = ref NowControlState.Get<PendingMask>(state.pendingId);
                    pending.hasValue = 1;
                    pending.value = next;
                    state.mask = next;
                    NowControlState.RequestRepaint();
                }
            }
        }

        static string Summary(int id, IReadOnlyList<string> options, int optionCount, int mask, int allBits)
        {
            ref var cache = ref NowControlState.Get<SummaryCache>(NowInput.GetId(id, "summary"));

            if (cache.initialized != 0 && cache.mask == mask && cache.optionCount == optionCount && cache.label != null)
                return cache.label;

            cache.initialized = 1;
            cache.mask = mask;
            cache.optionCount = optionCount;
            cache.label = BuildSummary(options, optionCount, mask, allBits);
            return cache.label;
        }

        static string BuildSummary(IReadOnlyList<string> options, int optionCount, int mask, int allBits)
        {
            int visible = mask & allBits;

            if (visible == 0)
                return "Nothing";

            if (visible == allBits && allBits != 0)
                return "Everything";

            int count = 0;
            int single = -1;

            for (int i = 0; i < optionCount; ++i)
            {
                if ((visible & (1 << i)) == 0)
                    continue;

                ++count;
                single = i;
            }

            return count == 1 ? options[single] : $"Mixed ({count})";
        }

        internal static int AllBits(int optionCount)
        {
            return optionCount >= 32 ? -1 : (1 << optionCount) - 1;
        }

        /// <summary>
        /// Layer names are cached after the first LayerMask draw; call this if
        /// layers are renamed at runtime.
        /// </summary>
        public static void InvalidateLayerCache()
        {
            _layerNames = null;
            _layerIndices = null;
        }

        static void EnsureLayerCache()
        {
            if (_layerNames != null)
                return;

            int count = 0;
            var names = new string[32];
            var indices = new int[32];

            for (int layer = 0; layer < 32; ++layer)
            {
                string name = LayerMask.LayerToName(layer);

                if (string.IsNullOrEmpty(name))
                    continue;

                names[count] = name;
                indices[count] = layer;
                ++count;
            }

            System.Array.Resize(ref names, count);
            System.Array.Resize(ref indices, count);
            _layerNames = names;
            _layerIndices = indices;
        }

        static int CompactFromLayerMask(int layerMask)
        {
            int compact = 0;

            for (int i = 0; i < _layerIndices.Length; ++i)
            {
                if ((layerMask & (1 << _layerIndices[i])) != 0)
                    compact |= 1 << i;
            }

            return compact;
        }

        static int LayerMaskFromCompact(int compact)
        {
            int layerMask = 0;

            for (int i = 0; i < _layerIndices.Length; ++i)
            {
                if ((compact & (1 << i)) != 0)
                    layerMask |= 1 << _layerIndices[i];
            }

            return layerMask;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            _layerNames = null;
            _layerIndices = null;
        }
    }

    public static partial class Now
    {
        public static NowMaskField MaskField(NowRect rect, IReadOnlyList<string> options, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowMaskField(rect, id, options, NowControls.SiteId(file, line));
        }

        public static NowMaskField LayerMaskField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowMaskField(rect, id, null, NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowMaskField MaskField(IReadOnlyList<string> options, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowMaskField(id, options, NowControls.SiteId(file, line));
        }

        public static NowMaskField LayerMaskField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowMaskField(id, null, NowControls.SiteId(file, line));
        }
    }
}
