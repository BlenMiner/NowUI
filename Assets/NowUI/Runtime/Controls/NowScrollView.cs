using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Scroll container:
    /// <code>
    /// using (NowLayout.ScrollView().Begin())
    ///     foreach (var item in items)
    ///         NowLayout.Button(item.name).Draw();
    /// </code>
    /// Content lays out in a vertical group clipped to the viewport; the wheel
    /// scrolls while hovered and scrollbar thumbs drag. Content size is the
    /// group's measured extent (one frame late, like all layout measurement).
    /// </summary>
    [NowBuilder]
    public struct NowScrollView
    {
        NowId _id;
        readonly int _site;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;

        internal NowScrollView(NowId id, int site)
        {
            _id = id;
            _site = site;
            _options = default;
            _rect = default;
            _hasRect = false;
        }

        internal NowScrollView(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        internal NowScrollView(NowRect rect, int identity) : this(default(NowId), identity)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the default stretch-to-fill sizing.</summary>
        public NowScrollView SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed viewport width in layout flow.</summary>
        public NowScrollView SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Fixed viewport height in layout flow.</summary>
        public NowScrollView SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        /// <summary>Stretches the viewport to fill available width, weighted against stretching siblings.</summary>
        public NowScrollView SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowScrollView SetId(NowId id) { _id = id; return this; }

        public NowScrollScope Begin()
        {
            var options = _options;

            if (!_hasRect)
            {
                if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                    options = options.SetStretchWidth();

                if (!options.Has(NowLayoutOptions.Field.Height) && !options.Has(NowLayoutOptions.Field.StretchHeight))
                    options = options.SetStretchHeight();
            }

            NowRect viewport = NowControls.ReserveRect(_hasRect, _rect, options, new Vector2(200f, 200f));
            int id = NowControls.GetControlId(_id, _site);
            int areaKey = NowInput.CombineId(id, 0x4e535641);

            NowLayout.TryGetCachedContentSize(areaKey, out Vector2 content);
            var styles = NowTheme.themeAsset.controlStyles;
            var scrollLayout = ResolveScrollLayout(viewport, content, styles.scrollbarWidth + styles.scrollbarPadding);

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(id);

            scroll.x = Mathf.Clamp(scroll.x, 0f, scrollLayout.maxScrollX);
            scroll.y = Mathf.Clamp(scroll.y, 0f, scrollLayout.maxScrollY);
            EnsureFocusedControlVisible(id, scrollLayout.contentViewport, scrollLayout.maxScrollX, scrollLayout.maxScrollY, ref scroll);

            var mask = Now.Mask(scrollLayout.contentViewport);
            var layout = NowLayout.Area(areaKey, new NowRect(
                viewport.x - scroll.x,
                viewport.y - scroll.y,
                scrollLayout.contentViewport.width,
                Mathf.Max(content.y, scrollLayout.contentViewport.height)));
            var focus = NowFocus.BeginScrollRegion(id);

            return new NowScrollScope(layout, mask, focus, viewport, scrollLayout.contentViewport, id,
                scrollLayout.maxScrollX, scrollLayout.maxScrollY,
                scrollLayout.verticalBarVisible, scrollLayout.horizontalBarVisible);
        }

        struct ScrollLayout
        {
            public NowRect contentViewport;
            public float maxScrollX;
            public float maxScrollY;
            public bool verticalBarVisible;
            public bool horizontalBarVisible;
        }

        struct FocusRevealState
        {
            public int focusedId;
            public int focusRevision;
        }

        static ScrollLayout ResolveScrollLayout(NowRect viewport, Vector2 content, float barReserve)
        {
            const float Epsilon = 0.5f;
            barReserve = Mathf.Max(0f, barReserve);

            bool vertical = content.y > viewport.height + Epsilon;
            bool horizontal = content.x > viewport.width + Epsilon;

            for (int i = 0; i < 4; ++i)
            {
                float availableWidth = Mathf.Max(0f, viewport.width - (vertical ? barReserve : 0f));
                float availableHeight = Mathf.Max(0f, viewport.height - (horizontal ? barReserve : 0f));
                bool nextVertical = content.y > availableHeight + Epsilon;
                bool nextHorizontal = content.x > availableWidth + Epsilon;

                if (nextVertical == vertical && nextHorizontal == horizontal)
                    break;

                vertical = nextVertical;
                horizontal = nextHorizontal;
            }

            float contentWidth = Mathf.Max(0f, viewport.width - (vertical ? barReserve : 0f));
            float contentHeight = Mathf.Max(0f, viewport.height - (horizontal ? barReserve : 0f));

            return new ScrollLayout
            {
                contentViewport = new NowRect(viewport.x, viewport.y, contentWidth, contentHeight),
                maxScrollX = Mathf.Max(0f, content.x - contentWidth),
                maxScrollY = Mathf.Max(0f, content.y - contentHeight),
                verticalBarVisible = vertical,
                horizontalBarVisible = horizontal
            };
        }

        static void EnsureFocusedControlVisible(int id, NowRect viewport, float maxScrollX, float maxScrollY, ref Vector2 scroll)
        {
            if ((maxScrollX <= 0f && maxScrollY <= 0f) || NowInput.isPassive ||
                !NowFocus.TryGetFocusedRectInScrollRegion(id, out var focused))
            {
                return;
            }

            ref var reveal = ref NowControlState.Get<FocusRevealState>(id, "focus-reveal");
            int focusedId = NowFocus.focusedId;
            int focusRevision = NowFocus.focusRevision;

            if (reveal.focusedId == focusedId && reveal.focusRevision == focusRevision)
                return;

            reveal.focusedId = focusedId;
            reveal.focusRevision = focusRevision;

            const float Padding = 4f;
            float left = viewport.x + Padding;
            float right = Mathf.Max(left, viewport.xMax - Padding);
            float top = viewport.y + Padding;
            float bottom = Mathf.Max(top, viewport.yMax - Padding);
            Vector2 previous = scroll;

            if (maxScrollX > 0f)
            {
                if (focused.x < left)
                    scroll.x -= left - focused.x;
                else if (focused.xMax > right)
                    scroll.x += focused.xMax - right;

                scroll.x = Mathf.Clamp(scroll.x, 0f, maxScrollX);
            }

            if (maxScrollY > 0f)
            {
                if (focused.y < top)
                    scroll.y -= top - focused.y;
                else if (focused.yMax > bottom)
                    scroll.y += focused.yMax - bottom;

                scroll.y = Mathf.Clamp(scroll.y, 0f, maxScrollY);
            }

            if (!Mathf.Approximately(previous.x, scroll.x) || !Mathf.Approximately(previous.y, scroll.y))
                NowControlState.RequestRepaint();
        }
    }

    [NowScope]
    public struct NowScrollScope : IDisposable
    {
        NowLayoutScope _layout;
        NowMaskScope _mask;
        NowFocusScrollRegionScope _focus;
        NowRect _viewport;
        NowRect _contentViewport;
        readonly int _id;
        readonly float _maxScrollX;
        readonly float _maxScrollY;
        readonly bool _verticalBarVisible;
        readonly bool _horizontalBarVisible;
        bool _disposed;

        internal NowScrollScope(
            NowLayoutScope layout,
            NowMaskScope mask,
            NowFocusScrollRegionScope focus,
            NowRect viewport,
            NowRect contentViewport,
            int id,
            float maxScrollX,
            float maxScrollY,
            bool verticalBarVisible,
            bool horizontalBarVisible)
        {
            _layout = layout;
            _mask = mask;
            _focus = focus;
            _viewport = viewport;
            _contentViewport = contentViewport;
            _id = id;
            _maxScrollX = maxScrollX;
            _maxScrollY = maxScrollY;
            _verticalBarVisible = verticalBarVisible;
            _horizontalBarVisible = horizontalBarVisible;
            _disposed = false;
        }

        /// <summary>The clipped viewport rect this scroll view occupies.</summary>
        public NowRect viewport => _viewport;

        /// <summary>
        /// Current scroll offset in pixels; setting clamps to the valid range.
        /// Content size is measured a frame late, so on the very first frame the
        /// range is still zero — the repaint loop settles it on the next frame.
        /// </summary>
        public Vector2 scrollOffset
        {
            get => NowControlState.Get<Vector2>(_id);
            set
            {
                ref Vector2 scroll = ref NowControlState.Get<Vector2>(_id);
                var clamped = new Vector2(
                    _maxScrollX > 0f ? Mathf.Clamp(value.x, 0f, _maxScrollX) : 0f,
                    _maxScrollY > 0f ? Mathf.Clamp(value.y, 0f, _maxScrollY) : 0f);

                if (scroll == clamped)
                    return;

                scroll = clamped;
                NowControlState.RequestRepaint();
            }
        }

        /// <summary>The largest valid scroll offset this frame (content minus viewport).</summary>
        public Vector2 maxScrollOffset => new Vector2(_maxScrollX, _maxScrollY);

        /// <summary>
        /// Scrolls to the end of the content on both axes — the chat/log
        /// stick-to-bottom pattern:
        /// <code>
        /// using (var scroll = NowLayout.ScrollView().Begin())
        /// {
        ///     foreach (var message in messages)
        ///         NowLayout.Label(message).Draw();
        ///
        ///     if (pinToBottom)
        ///         scroll.ScrollToEnd();
        /// }
        /// </code>
        /// </summary>
        public void ScrollToEnd()
        {
            scrollOffset = new Vector2(_maxScrollX, _maxScrollY);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _focus.Dispose();
            _layout.Dispose();
            _mask.Dispose();

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(_id);

            if (_maxScrollX > 0f || _maxScrollY > 0f)
            {
                Vector2 pendingWheel = NowInput.current.scrollDelta;

                if (WouldWheelMove(scroll, _maxScrollX, _maxScrollY, pendingWheel))
                {
                    Vector2 wheel = NowInput.ConsumeScrollDelta(_viewport);

                    if (wheel != Vector2.zero)
                    {
                        float step = NowTheme.themeAsset.controlStyles.scrollWheelStep;
                        scroll.x += wheel.x * step;
                        scroll.y -= wheel.y * step;
                        NowControlState.RequestRepaint();
                    }
                }
            }

            scroll.x = _maxScrollX > 0f ? Mathf.Clamp(scroll.x, 0f, _maxScrollX) : 0f;
            scroll.y = _maxScrollY > 0f ? Mathf.Clamp(scroll.y, 0f, _maxScrollY) : 0f;

            if (!_verticalBarVisible && !_horizontalBarVisible)
                return;

            var theme = NowTheme.themeAsset;
            float barWidth = theme.controlStyles.scrollbarWidth;

            if (_verticalBarVisible)
            {
                var track = new NowRect(
                    _viewport.xMax - barWidth - 2f,
                    _contentViewport.y + 2f,
                    barWidth,
                    Mathf.Max(0f, _contentViewport.height - 4f));

                DrawScrollbar(theme, NowScrollbarAxis.Vertical, track,
                    _contentViewport.height, _contentViewport.height + _maxScrollY,
                    ref scroll.y, "thumb");
            }

            if (_horizontalBarVisible)
            {
                var track = new NowRect(
                    _contentViewport.x + 2f,
                    _viewport.yMax - barWidth - 2f,
                    Mathf.Max(0f, _contentViewport.width - 4f),
                    barWidth);

                DrawScrollbar(theme, NowScrollbarAxis.Horizontal, track,
                    _contentViewport.width, _contentViewport.width + _maxScrollX,
                    ref scroll.x, "hthumb");
            }
        }

        void DrawScrollbar(
            NowThemeAsset theme,
            NowScrollbarAxis axis,
            NowRect track,
            float viewportSize,
            float contentSize,
            ref float value,
            string key)
        {
            if (track.width <= 0f || track.height <= 0f || viewportSize <= 0f || contentSize <= 0f)
                return;

            int thumbId = NowInput.GetId(_id, key);
            var metrics = NowScrollbar.Calculate(
                axis,
                track,
                viewportSize,
                contentSize,
                value,
                theme.controlStyles.scrollbarMinThumbSize);

            // The whole (slightly widened) track is the grab target: clicking
            // anywhere on it jumps the thumb there and keeps dragging.
            bool dragging = NowScrollbar.Interact(thumbId, axis, metrics, ref value);
            metrics = NowScrollbar.Calculate(
                axis,
                track,
                viewportSize,
                contentSize,
                value,
                theme.controlStyles.scrollbarMinThumbSize);

            NowRect hoverRect = axis == NowScrollbarAxis.Vertical
                ? track.Outset(4f, 2f)
                : track.Outset(2f, 4f);
            float hoverT = NowControlState.Transition(thumbId, NowInput.IsHovered(hoverRect));
            theme.controlRenderer.DrawScrollbar(new NowScrollbarRenderContext(
                theme,
                axis,
                metrics,
                dragging,
                hoverT));
        }

        static bool WouldWheelMove(Vector2 scroll, float maxScrollX, float maxScrollY, Vector2 wheel)
        {
            if (wheel == Vector2.zero)
                return false;

            float step = NowTheme.themeAsset.controlStyles.scrollWheelStep;
            float nextX = maxScrollX > 0f ? Mathf.Clamp(scroll.x + wheel.x * step, 0f, maxScrollX) : 0f;
            float nextY = maxScrollY > 0f ? Mathf.Clamp(scroll.y - wheel.y * step, 0f, maxScrollY) : 0f;

            return !Mathf.Approximately(nextX, scroll.x) || !Mathf.Approximately(nextY, scroll.y);
        }
    }
}
