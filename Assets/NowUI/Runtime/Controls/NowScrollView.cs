using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Vertical scroll container:
    /// <code>
    /// using (NowLayout.ScrollView("inventory").Begin())
    ///     foreach (var item in items)
    ///         NowLayout.Button(item.name).Draw();
    /// </code>
    /// Content lays out in a vertical group clipped to the viewport; the wheel
    /// scrolls while hovered and the scrollbar thumb drags. Content height is the
    /// group's measured extent (one frame late, like all layout measurement).
    /// </summary>
    [NowBuilder]
    public struct NowScrollView
    {
        readonly string _id;
        readonly int _site;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;

        internal NowScrollView(string id, int site)
        {
            _id = id;
            _site = site;
            _options = default;
            _rect = default;
            _hasRect = false;
        }

        internal NowScrollView(NowRect rect, string id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        internal NowScrollView(NowRect rect, int identity) : this(null, identity)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowScrollView SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowScrollView SetHeight(float height) { _options = _options.SetHeight(height); return this; }

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
            int id = _id != null ? NowControls.GetControlId(_id) : NowControls.GetControlId(_site);
            int areaKey = _id != null ? NowInput.GetId(_id) : _site;

            NowLayout.TryGetCachedContentSize(areaKey, out Vector2 content);
            bool barVisible = content.y > viewport.height + 0.5f;
            float maxScroll = Mathf.Max(0f, content.y - viewport.height);

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(id);

            scroll.y = Mathf.Clamp(scroll.y, 0f, maxScroll);

            const float BarWidth = 8f;
            const float BarPad = 4f;
            float contentWidth = viewport.width - (barVisible ? BarWidth + BarPad : 0f);

            var mask = Now.Mask(viewport);
            var layout = NowLayout.Area(areaKey, new NowRect(
                viewport.x,
                viewport.y - scroll.y,
                contentWidth,
                Mathf.Max(content.y, viewport.height)));

            return new NowScrollScope(layout, mask, viewport, id, maxScroll, barVisible);
        }
    }

    [NowScope]
    public struct NowScrollScope : IDisposable
    {
        NowLayoutScope _layout;
        NowMaskScope _mask;
        NowRect _viewport;
        readonly int _id;
        readonly float _maxScroll;
        readonly bool _barVisible;
        bool _disposed;

        internal NowScrollScope(NowLayoutScope layout, NowMaskScope mask, NowRect viewport, int id, float maxScroll, bool barVisible)
        {
            _layout = layout;
            _mask = mask;
            _viewport = viewport;
            _id = id;
            _maxScroll = maxScroll;
            _barVisible = barVisible;
            _disposed = false;
        }

        /// <summary>The clipped viewport rect this scroll view occupies.</summary>
        public NowRect viewport => _viewport;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _layout.Dispose();
            _mask.Dispose();

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(_id);

            if (_maxScroll > 0f)
            {
                if (NowInput.current.scrollDelta.y != 0f)
                {
                    float wheel = NowInput.ConsumeScrollDelta(_viewport).y;

                    if (wheel != 0f)
                    {
                        scroll.y -= wheel * 40f;
                        NowControlState.RequestRepaint();
                    }
                }

                scroll.y = Mathf.Clamp(scroll.y, 0f, _maxScroll);
            }
            else
            {
                scroll.y = 0f;
            }

            if (!_barVisible)
                return;

            var theme = NowTheme.themeAsset;
            const float BarWidth = 8f;

            var track = new NowRect(
                _viewport.xMax - BarWidth - 2f,
                _viewport.y + 2f,
                BarWidth,
                _viewport.height - 4f);

            int thumbId = NowInput.GetId(_id, "thumb");
            var metrics = NowScrollbar.Calculate(
                NowScrollbarAxis.Vertical,
                track,
                _viewport.height,
                _viewport.height + _maxScroll,
                scroll.y,
                24f);

            // The whole (slightly widened) track is the grab target: clicking
            // anywhere on it jumps the thumb there and keeps dragging — an 8px
            // thumb alone is a frustrating target.
            bool dragging = NowScrollbar.Interact(thumbId, NowScrollbarAxis.Vertical, metrics, ref scroll.y);
            metrics = NowScrollbar.Calculate(
                NowScrollbarAxis.Vertical,
                track,
                _viewport.height,
                _viewport.height + _maxScroll,
                scroll.y,
                24f);

            float hoverT = NowControlState.Transition(thumbId, NowInput.IsHovered(track.Outset(4f, 2f)));

            var trackRectangle = theme.Rectangle(track, NowRectangleStyle.Muted);
            trackRectangle.radius = new Vector4(BarWidth, BarWidth, BarWidth, BarWidth) * 0.5f;
            trackRectangle.Draw();

            var thumb = theme.Rectangle(metrics.thumb, NowRectangleStyle.Accent);
            thumb.radius = trackRectangle.radius;
            thumb.color = NowControls.StateTint(thumb.color, hoverT, dragging);
            thumb.Draw();
        }
    }
}
