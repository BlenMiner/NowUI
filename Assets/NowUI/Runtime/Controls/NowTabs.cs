using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Tab strip. The selected index is caller-owned:
    /// <code>NowLayout.TabBar(pageNames).Draw(ref page);</code>
    /// While the bar is focused, left/right navigation moves the selection.
    /// </summary>
    [NowBuilder]
    public struct NowTabBar
    {
        readonly IReadOnlyList<string> _labels;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _stretchTabs;

        const int TabSeed = 0x4e544162;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowTabBar(IReadOnlyList<string> labels, int site)
        {
            _labels = labels;
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _stretchTabs = false;
        }

        internal NowTabBar(NowRect rect, IReadOnlyList<string> labels, int site) : this(labels, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowTabBar SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowTabBar SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowTabBar SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTabBar SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowTabBar SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Divides the full bar width evenly between tabs (mobile-style segmented bar).</summary>
        public NowTabBar SetStretchTabs(bool stretch = true) { _stretchTabs = stretch; return this; }

        public bool Draw(ref int selected)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();
            int count = _labels?.Count ?? 0;

            float totalWidth = 0f;

            for (int i = 0; i < count; ++i)
                totalWidth += renderer.MeasureTab(theme, _labels[i]).x + (i > 0 ? theme.controlStyles.tabSpacing : 0f);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(totalWidth, renderer.TabBarHeight(theme)));

            int previous = selected;
            bool focused = NowFocus.IsFocused(id);

            renderer.DrawTabBarBackground(theme, rect);

            float stretchWidth = count > 0 ? (rect.width - theme.controlStyles.tabSpacing * (count - 1)) / count : 0f;
            float x = rect.x;
            bool tabClicked = false;

            for (int i = 0; i < count; ++i)
            {
                float tabWidth = _stretchTabs ? stretchWidth : renderer.MeasureTab(theme, _labels[i]).x;
                var tabRect = new NowRect(x, rect.y, tabWidth, rect.height);
                int tabId = NowInput.CombineId(NowInput.CombineId(id, TabSeed), i + 1);
                var tabInteraction = NowInput.Interact(tabId, tabRect);

                if (tabInteraction.clicked)
                {
                    selected = i;
                    tabClicked = true;
                }

                bool isSelected = i == selected;
                float selectedT = NowControlState.Transition(tabId, isSelected, 14f);
                float hoverT = NowControlState.Transition(tabInteraction, "hover", tabInteraction.hovered || tabInteraction.held);

                renderer.DrawTab(new NowTabRenderContext(
                    theme, tabRect, _labels[i], isSelected, selectedT, tabInteraction, focused && isSelected, hoverT));

                x += tabWidth + theme.controlStyles.tabSpacing;
            }

            NowControls.Interact(id, rect, _navigation, out focused, out _);

            if (tabClicked)
                NowFocus.Focus(id);

            if (focused && !NowInput.isPassive && count > 0)
            {
                float navX = NowInput.current.navigation.x;

                if (NowControlState.Repeat(id, "nav", Mathf.Abs(navX) > 0.55f, 0.35f, 0.15f))
                    selected = Mathf.Clamp(selected + (navX > 0f ? 1 : -1), 0, count - 1);
            }

            if (selected != previous)
                NowControlState.RequestRepaint();

            return selected != previous;
        }
    }

    /// <summary>
    /// Tab bar plus a masked page area in one scope; only the selected page's
    /// content should be drawn inside.
    /// <code>
    /// using (var view = NowLayout.TabView(pageNames).SetStretchWidth().SetHeight(320).Begin(ref page))
    /// {
    ///     switch (view.selected)
    ///     {
    ///         case 0: DrawGeneral(); break;
    ///         case 1: DrawAudio(); break;
    ///     }
    /// }
    /// </code>
    /// </summary>
    [NowBuilder]
    public struct NowTabView
    {
        readonly IReadOnlyList<string> _labels;
        readonly int _site;
        NowId _id;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _stretchTabs;

        const int AreaKeySeed = 0x4e545661;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowTabView(IReadOnlyList<string> labels, int site)
        {
            _labels = labels;
            _site = site;
            _id = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _stretchTabs = false;
        }

        internal NowTabView(NowRect rect, IReadOnlyList<string> labels, int site) : this(labels, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowTabView SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowTabView SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowTabView SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        public NowTabView SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowTabView SetStretchHeight(float weight = 1f) { _options = _options.SetStretchHeight(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTabView SetId(NowId id) { _id = id; return this; }

        /// <summary>Divides the full bar width evenly between tabs (mobile-style segmented bar).</summary>
        public NowTabView SetStretchTabs(bool stretch = true) { _stretchTabs = stretch; return this; }

        public NowTabViewScope Begin(ref int selected)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();
            int areaKey = NowInput.CombineId(id, AreaKeySeed);
            float barHeight = renderer.TabBarHeight(theme);

            if (!_options.Has(NowLayoutOptions.Field.Height) && !_options.Has(NowLayoutOptions.Field.StretchHeight))
                _options = _options.SetStretchHeight(1f);

            if (!_options.Has(NowLayoutOptions.Field.Width) && !_options.Has(NowLayoutOptions.Field.StretchWidth))
                _options = _options.SetStretchWidth(1f);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(200f, barHeight + 80f));
            var barRect = new NowRect(rect.x, rect.y, rect.width, barHeight);
            var pageRect = new NowRect(rect.x, rect.y + barHeight, rect.width, Mathf.Max(0f, rect.height - barHeight));

            bool changed = new NowTabBar(barRect, _labels, _site)
                .SetId(_id)
                .SetStretchTabs(_stretchTabs)
                .Draw(ref selected);

            var mask = Now.Mask(pageRect);
            var area = NowLayout.Area(areaKey, pageRect);

            return new NowTabViewScope(mask, area, selected, changed, pageRect);
        }
    }

    [NowScope]
    public struct NowTabViewScope : System.IDisposable
    {
        /// <summary>The selected page index after this frame's tab interaction.</summary>
        public readonly int selected;

        /// <summary>True when the selection changed this frame.</summary>
        public readonly bool changed;

        public readonly NowRect pageRect;

        NowLayoutScope _area;
        NowMaskScope _mask;
        bool _disposed;

        internal NowTabViewScope(NowMaskScope mask, NowLayoutScope area, int selected, bool changed, NowRect pageRect)
        {
            _mask = mask;
            _area = area;
            this.selected = selected;
            this.changed = changed;
            this.pageRect = pageRect;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _area.Dispose();
            _mask.Dispose();
        }
    }
}
