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
            int areaKey = _id != null ? NowUIInput.GetId(_id) : _site;

            NowLayout.TryGetCachedContentSize(areaKey, out Vector2 content);
            bool barVisible = content.y > viewport.height + 0.5f;
            float maxScroll = Mathf.Max(0f, content.y - viewport.height);

            ref Vector2 scroll = ref NowUIControlState.Get<Vector2>(id);

            if (!NowUIInput.isPassive && NowUIInput.IsHovered(viewport))
            {
                float wheel = NowUIInput.current.scrollDelta.y;

                if (wheel != 0f)
                {
                    scroll.y -= wheel * 40f;
                    NowUIControlState.RequestRepaint();
                }
            }

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

            if (!_barVisible)
                return;

            var theme = NowControls.theme;
            const float BarWidth = 8f;

            var track = new NowRect(
                _viewport.xMax - BarWidth - 2f,
                _viewport.y + 2f,
                BarWidth,
                _viewport.height - 4f);

            ref Vector2 scroll = ref NowUIControlState.Get<Vector2>(_id);

            float visibleFraction = _viewport.height / (_viewport.height + _maxScroll);
            float thumbHeight = Mathf.Max(24f, track.height * visibleFraction);
            float travel = track.height - thumbHeight;
            float normalized = _maxScroll > 0f ? scroll.y / _maxScroll : 0f;

            int thumbId = NowUIInput.GetId(_id, "thumb");
            var thumbRect = new NowRect(track.x, track.y + travel * normalized, BarWidth, thumbHeight);
            var interaction = NowUIInput.Interact(thumbId, thumbRect);

            if (interaction.held && travel > 0f)
            {
                float t = Mathf.Clamp01((interaction.pointerPosition.y - track.y - thumbHeight * 0.5f) / travel);
                scroll.y = t * _maxScroll;
                thumbRect.y = track.y + travel * t;
                NowUIControlState.RequestRepaint();
            }

            float hoverT = NowUIControlState.Transition(thumbId, interaction.hovered || interaction.held);

            var trackRectangle = theme.Rectangle(track, NowRectangleStyle.Muted);
            trackRectangle.radius = new Vector4(BarWidth, BarWidth, BarWidth, BarWidth) * 0.5f;
            trackRectangle.Draw();

            var thumb = theme.Rectangle(thumbRect, NowRectangleStyle.Accent);
            thumb.radius = trackRectangle.radius;
            thumb.color = NowControls.StateTint(thumb.color, hoverT, interaction.held);
            thumb.Draw();
        }
    }
}
