using UnityEngine;

namespace NowUI
{
    public enum NowSplitAxis
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Two resizable panes separated by a draggable divider. The split ratio is
    /// caller-owned (0..1 fraction of the first pane):
    /// <code>
    /// var split = Now.SplitView(rect).Begin(ref ratio);
    /// using (split.BeginFirst()) DrawSidebar();
    /// using (split.BeginSecond()) DrawContent();
    /// </code>
    /// The divider is focusable; arrow navigation nudges the ratio.
    /// </summary>
    [NowBuilder]
    public struct NowSplitView
    {
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowSplitAxis _axis;
        float _minFirst;
        float _minSecond;

        const int DividerSeed = 0x4e535664;
        const int FirstAreaSeed = 0x4e535631;
        const int SecondAreaSeed = 0x4e535632;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowSplitView(int site)
        {
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _axis = NowSplitAxis.Horizontal;
            _minFirst = 48f;
            _minSecond = 48f;
        }

        internal NowSplitView(NowRect rect, int site) : this(site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowSplitView SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowSplitView SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for the divider.</summary>
        public NowSplitView SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Horizontal splits side by side (default); vertical stacks panes.</summary>
        public NowSplitView SetAxis(NowSplitAxis axis) { _axis = axis; return this; }

        /// <summary>Minimum pixel sizes the drag clamps each pane to.</summary>
        public NowSplitView SetMinPaneSize(float first, float second)
        {
            _minFirst = Mathf.Max(0f, first);
            _minSecond = Mathf.Max(0f, second);
            return this;
        }

        public NowSplitViewResult Begin(ref float ratio)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            var styles = theme.controlStyles;
            int id = ResolveControlId();

            if (!_options.Has(NowLayoutOptions.Field.Height) && !_options.Has(NowLayoutOptions.Field.StretchHeight))
                _options = _options.SetStretchHeight(1f);

            if (!_options.Has(NowLayoutOptions.Field.Width) && !_options.Has(NowLayoutOptions.Field.StretchWidth))
                _options = _options.SetStretchWidth(1f);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(240f, 160f));

            bool horizontal = _axis == NowSplitAxis.Horizontal;
            float total = horizontal ? rect.width : rect.height;
            float thickness = styles.splitterThickness;
            float usable = Mathf.Max(0f, total - thickness);

            float minRatio = usable > 0f ? Mathf.Clamp01(_minFirst / usable) : 0f;
            float maxRatio = usable > 0f ? Mathf.Clamp01(1f - _minSecond / usable) : 1f;

            if (maxRatio < minRatio)
                maxRatio = minRatio;

            ratio = Mathf.Clamp(Mathf.Clamp01(ratio), minRatio, maxRatio);

            float firstSize = usable * ratio;
            var dividerRect = horizontal
                ? new NowRect(rect.x + firstSize, rect.y, thickness, rect.height)
                : new NowRect(rect.x, rect.y + firstSize, rect.width, thickness);

            float hitOutset = Mathf.Max(styles.splitterHitOutset, (styles.controlMinTouchTarget - thickness) * 0.5f);
            var hitRect = horizontal
                ? new NowRect(dividerRect.x - hitOutset, dividerRect.y, thickness + hitOutset * 2f, dividerRect.height)
                : new NowRect(dividerRect.x, dividerRect.y - hitOutset, dividerRect.width, thickness + hitOutset * 2f);

            int dividerId = NowInput.CombineId(id, DividerSeed);
            var interaction = NowControls.Interact(dividerId, hitRect, _navigation, out bool focused, out _);
            ref float grabOffset = ref NowControlState.Get<float>(dividerId, "grab-offset");

            if (interaction.pressed)
            {
                float pressPointer = horizontal ? interaction.pointerPosition.x : interaction.pointerPosition.y;
                float dividerCenter = horizontal
                    ? dividerRect.x + thickness * 0.5f
                    : dividerRect.y + thickness * 0.5f;
                grabOffset = pressPointer - dividerCenter;
            }

            if (interaction.dragging && usable > 0f)
            {
                float pointer = horizontal
                    ? interaction.pointerPosition.x - rect.x
                    : interaction.pointerPosition.y - rect.y;
                ratio = Mathf.Clamp((pointer - grabOffset - thickness * 0.5f) / usable, minRatio, maxRatio);
                NowControlState.RequestRepaint();
            }

            if (focused && !NowInput.isPassive && usable > 0f)
            {
                float nav = horizontal ? NowInput.current.navigation.x : -NowInput.current.navigation.y;

                if (NowControlState.Repeat(dividerId, "nav", Mathf.Abs(nav) > 0.55f, 0.35f, 0.05f))
                    ratio = Mathf.Clamp(ratio + Mathf.Sign(nav) * (8f / usable), minRatio, maxRatio);
            }

            firstSize = usable * ratio;
            var firstRect = horizontal
                ? new NowRect(rect.x, rect.y, firstSize, rect.height)
                : new NowRect(rect.x, rect.y, rect.width, firstSize);
            var secondRect = horizontal
                ? new NowRect(rect.x + firstSize + thickness, rect.y, usable - firstSize, rect.height)
                : new NowRect(rect.x, rect.y + firstSize + thickness, rect.width, usable - firstSize);
            dividerRect = horizontal
                ? new NowRect(rect.x + firstSize, rect.y, thickness, rect.height)
                : new NowRect(rect.x, rect.y + firstSize, rect.width, thickness);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            renderer.DrawSplitDivider(new NowSplitDividerRenderContext(
                theme, dividerRect, !horizontal, interaction.dragging, focused, hoverT));

            return new NowSplitViewResult(
                NowInput.CombineId(id, FirstAreaSeed),
                NowInput.CombineId(id, SecondAreaSeed),
                firstRect,
                secondRect,
                interaction.dragging);
        }
    }

    /// <summary>
    /// Pane geometry and factories returned by <see cref="NowSplitView.Begin(ref float)"/>.
    /// This is a result value, not a scope; only the values from
    /// <see cref="BeginFirst"/> and <see cref="BeginSecond"/> require disposal.
    /// </summary>
    public readonly struct NowSplitViewResult
    {
        public readonly NowRect firstRect;

        public readonly NowRect secondRect;

        /// <summary>True while the divider is being dragged.</summary>
        public readonly bool dragging;

        readonly int _firstAreaKey;
        readonly int _secondAreaKey;

        internal NowSplitViewResult(int firstAreaKey, int secondAreaKey, NowRect firstRect, NowRect secondRect, bool dragging)
        {
            _firstAreaKey = firstAreaKey;
            _secondAreaKey = secondAreaKey;
            this.firstRect = firstRect;
            this.secondRect = secondRect;
            this.dragging = dragging;
        }

        /// <summary>Opens the first (left/top) pane as a masked layout area.</summary>
        public NowSplitPaneScope BeginFirst()
        {
            var mask = Now.Mask(firstRect);

            try
            {
                var area = NowLayout.Area(NowId.Resolved(_firstAreaKey), firstRect);
                return new NowSplitPaneScope(mask, area);
            }
            catch
            {
                mask.Dispose();
                throw;
            }
        }

        /// <summary>Opens the second (right/bottom) pane as a masked layout area.</summary>
        public NowSplitPaneScope BeginSecond()
        {
            var mask = Now.Mask(secondRect);

            try
            {
                var area = NowLayout.Area(NowId.Resolved(_secondAreaKey), secondRect);
                return new NowSplitPaneScope(mask, area);
            }
            catch
            {
                mask.Dispose();
                throw;
            }
        }
    }

    [NowScope]
    public struct NowSplitPaneScope : System.IDisposable
    {
        NowLayoutScope _area;
        NowMaskScope _mask;
        bool _disposed;

        internal NowSplitPaneScope(NowMaskScope mask, NowLayoutScope area)
        {
            _mask = mask;
            _area = area;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _area.Dispose();
            _mask.Dispose();
            _disposed = true;
        }
    }
}
