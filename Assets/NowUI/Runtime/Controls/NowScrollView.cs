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
        bool _followTail;

        internal NowScrollView(NowId id, int site)
        {
            _id = id;
            _site = site;
            _options = default;
            _rect = default;
            _hasRect = false;
            _followTail = false;
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

        /// <summary>
        /// Keeps the view pinned to the bottom as content grows — the console/log
        /// pattern. The pin holds only while the user is already at the bottom;
        /// scrolling up releases it, and scrolling back down re-engages it.
        /// </summary>
        public NowScrollView SetFollowTail(bool follow = true) { _followTail = follow; return this; }

        static int s_dragScrollRegionId;

        static int s_dragScrollFrame = -1;

        /// <summary>
        /// Marks the current pointer drag as scroll-aware: the innermost scroll
        /// view enclosing the calling control scrolls while the pointer sits
        /// near (or past) its viewport edge, browser-style. Call every dragging
        /// frame — text selection does; custom drag gestures opt in with one call.
        /// </summary>
        public static void RequestDragScroll()
        {
            if (NowInput.isPassive || !NowInput.hasContext)
                return;

            int region = NowFocus.currentScrollRegionId;

            if (region == 0)
                return;

            s_dragScrollRegionId = region;
            s_dragScrollFrame = NowInput.current.frame;
        }

        internal static bool TryConsumeDragScroll(int id)
        {
            if (s_dragScrollRegionId != id ||
                s_dragScrollFrame != NowInput.current.frame ||
                NowInput.activeId == 0)
            {
                return false;
            }

            s_dragScrollRegionId = 0;
            s_dragScrollFrame = -1;
            return true;
        }

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

            ref Vector2 measuredSize = ref NowControlState.Get<Vector2>(id, "layout-size");

            if (measuredSize == Vector2.zero)
                measuredSize = new Vector2(viewport.width, viewport.height);

            var scrollLayout = ResolveScrollLayout(viewport, content, measuredSize, styles.scrollbarWidth + styles.scrollbarPadding);
            measuredSize = new Vector2(scrollLayout.contentViewport.width, scrollLayout.contentViewport.height);

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(id);

            if (_followTail)
            {
                // Pin to the bottom while the user was already there last frame;
                // slack tolerates sub-pixel drift without trapping a deliberate
                // one-line scroll away from the tail.
                const float TailSlack = 2f;
                ref float lastMaxScrollY = ref NowControlState.Get<float>(id, "follow-tail");

                if (scroll.y >= lastMaxScrollY - TailSlack)
                    scroll.y = scrollLayout.maxScrollY;

                lastMaxScrollY = scrollLayout.maxScrollY;
            }

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

        /// <summary>
        /// A bar appears only when the content exceeds both the space available
        /// this frame and the extent it was actually measured against
        /// (<paramref name="measuredSize"/>). The cached content size is one
        /// layout pass old: content that merely filled the width it was given
        /// still reports that width after a vertical bar reserves space, which
        /// the available-space check alone reads as horizontal overflow — a
        /// phantom horizontal bar plus a one-frame rewrap on the frame content
        /// first crosses the viewport height. Genuine overflow created by a
        /// shrink instead shows one re-measure later, like all deferred layout.
        /// </summary>
        static ScrollLayout ResolveScrollLayout(NowRect viewport, Vector2 content, Vector2 measuredSize, float barReserve)
        {
            const float Epsilon = 0.5f;
            barReserve = Mathf.Max(0f, barReserve);

            bool verticalMeasured = content.y > measuredSize.y + Epsilon;
            bool horizontalMeasured = content.x > measuredSize.x + Epsilon;
            bool vertical = verticalMeasured && content.y > viewport.height + Epsilon;
            bool horizontal = horizontalMeasured && content.x > viewport.width + Epsilon;

            for (int i = 0; i < 4; ++i)
            {
                float availableWidth = Mathf.Max(0f, viewport.width - (vertical ? barReserve : 0f));
                float availableHeight = Mathf.Max(0f, viewport.height - (horizontal ? barReserve : 0f));
                bool nextVertical = verticalMeasured && content.y > availableHeight + Epsilon;
                bool nextHorizontal = horizontalMeasured && content.x > availableWidth + Epsilon;

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
                maxScrollX = horizontal ? Mathf.Max(0f, content.x - contentWidth) : 0f,
                maxScrollY = vertical ? Mathf.Max(0f, content.y - contentHeight) : 0f,
                verticalBarVisible = vertical,
                horizontalBarVisible = horizontal
            };
        }

        /// <summary>
        /// Scrolls a newly focused control into the viewport, once per focus
        /// change. Focus gained under the pointer (click/tap) is exempt: the
        /// control is already where the user is looking, and pointer focus can
        /// land on rects far larger than the viewport (a selectable document),
        /// where revealing an edge would yank the scroll position.
        /// </summary>
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

            var snapshot = NowInput.current;

            if (snapshot.hasPointer && focused.Contains(snapshot.pointerPosition))
                return;

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
        const float EdgeScrollMargin = 28f;

        const float EdgeScrollMaxOvershoot = 112f;

        const float EdgeScrollSpeed = 10f;

        const float PanDeadZone = 8f;

        const float PanScrollSpeed = 4f;

        const float MaxAutoScrollDeltaTime = 0.1f;

        struct AutoScrollTime
        {
            public float lastTime;
        }

        struct PanScrollState
        {
            public Vector2 anchor;
            public byte mode;
            public bool dragged;
        }

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

        /// <summary>True when the vertical scrollbar is shown this frame.</summary>
        public bool verticalScrollbarVisible => _verticalBarVisible;

        /// <summary>True when the horizontal scrollbar is shown this frame.</summary>
        public bool horizontalScrollbarVisible => _horizontalBarVisible;

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
            bool panAnchorVisible = false;
            Vector2 panAnchor = default;

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

                if (!NowInput.isPassive)
                {
                    ApplyDragScroll(ref scroll);
                    panAnchorVisible = ApplyPanScroll(ref scroll, out panAnchor);
                }
            }

            scroll.x = _maxScrollX > 0f ? Mathf.Clamp(scroll.x, 0f, _maxScrollX) : 0f;
            scroll.y = _maxScrollY > 0f ? Mathf.Clamp(scroll.y, 0f, _maxScrollY) : 0f;

            if (panAnchorVisible)
            {
                var panTheme = NowTheme.themeAsset;
                panTheme.controlRenderer.DrawScrollPanAnchor(new NowScrollPanAnchorRenderContext(
                    panTheme, panAnchor, _maxScrollX > 0f, _maxScrollY > 0f));
            }

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

        /// <summary>
        /// Scrolls toward the pointer while a drag that called
        /// <see cref="NowScrollView.RequestDragScroll"/> holds it near or past
        /// the viewport edge, speeding up with distance.
        /// </summary>
        void ApplyDragScroll(ref Vector2 scroll)
        {
            if (!NowScrollView.TryConsumeDragScroll(_id))
                return;

            var snapshot = NowInput.current;
            Rect viewport = Now.TransformScreenRect((Rect)_contentViewport);
            Vector2 pointer = snapshot.pointerPosition;
            float dt = ConsumeDeltaTime("drag-scroll-time", snapshot.time);
            float vx = _maxScrollX > 0f ? EdgeScrollVelocity(pointer.x, viewport.xMin, viewport.xMax) : 0f;
            float vy = _maxScrollY > 0f ? EdgeScrollVelocity(pointer.y, viewport.yMin, viewport.yMax) : 0f;

            if (vx == 0f && vy == 0f)
                return;

            scroll.x += vx * dt;
            scroll.y += vy * dt;
            NowControlState.RequestRepaint();
        }

        /// <summary>
        /// Browser-style middle-button autoscroll: a middle press drops an
        /// anchor and scroll speed grows with the pointer's distance from it.
        /// Press-drag-release pans once; a middle click with no drag keeps
        /// panning until any button press or cancel ends it.
        /// </summary>
        bool ApplyPanScroll(ref Vector2 scroll, out Vector2 anchor)
        {
            int panId = NowInput.GetId(_id, "pan");
            var interaction = NowInput.Interact(panId, _contentViewport, NowPointerButton.Middle);
            ref var pan = ref NowControlState.Get<PanScrollState>(panId);
            var snapshot = NowInput.current;

            if (interaction.pressed)
            {
                if (pan.mode == 0)
                {
                    pan.mode = 1;
                    pan.anchor = snapshot.pointerPosition;
                    pan.dragged = false;
                }
                else
                {
                    pan.mode = 0;
                }
            }
            else if (pan.mode == 1)
            {
                pan.dragged |= interaction.dragging;

                if (interaction.released)
                    pan.mode = pan.dragged ? (byte)0 : (byte)2;
                else if (!interaction.active)
                    pan.mode = 0;
            }
            else if (pan.mode == 2 && (snapshot.anyPointerPressed || snapshot.cancelPressed))
            {
                pan.mode = 0;
            }

            anchor = Now.InverseTransformScreenPoint(pan.anchor);

            if (pan.mode == 0)
                return false;

            float dt = ConsumeDeltaTime("pan-scroll-time", snapshot.time);
            Vector2 offset = snapshot.pointerPosition - pan.anchor;

            if (_maxScrollX > 0f)
                scroll.x += PanVelocity(offset.x) * dt;

            if (_maxScrollY > 0f)
                scroll.y += PanVelocity(offset.y) * dt;

            NowControlState.RequestRepaint();
            return true;
        }

        /// <summary>
        /// Frame delta from the input snapshot clock, keyed per feature so an
        /// idle stretch (or the first frame of a gesture) yields zero instead
        /// of one huge step.
        /// </summary>
        float ConsumeDeltaTime(string key, float time)
        {
            ref var state = ref NowControlState.Get<AutoScrollTime>(_id, key);
            float delta = time - state.lastTime;
            state.lastTime = time;
            return delta > 0f && delta <= MaxAutoScrollDeltaTime ? delta : 0f;
        }

        static float EdgeScrollVelocity(float position, float min, float max)
        {
            float margin = Mathf.Min(EdgeScrollMargin, (max - min) * 0.25f);
            float overshoot = 0f;

            if (position < min + margin)
                overshoot = position - (min + margin);
            else if (position > max - margin)
                overshoot = position - (max - margin);

            return Mathf.Clamp(overshoot, -EdgeScrollMaxOvershoot, EdgeScrollMaxOvershoot) * EdgeScrollSpeed;
        }

        static float PanVelocity(float distance)
        {
            float magnitude = Mathf.Abs(distance);

            if (magnitude <= PanDeadZone)
                return 0f;

            return Mathf.Sign(distance) * (magnitude - PanDeadZone) * PanScrollSpeed;
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
