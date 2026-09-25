using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Evenly spaced hairlines across a rect, the backdrop grid of editors, charts
    /// and scene views. Build it with <see cref="Now.GridLines(NowRect, float)"/>:
    /// <code>
    /// Now.GridLines(canvas, 48f).SetColor(Color.white.WithAlpha(0.05f)).Draw();
    /// Now.GridLines(canvas, 48f).SetOffset(pan).SetThickness(1f).Draw();   // scrolls with the content
    /// </code>
    /// Lines sit strictly inside the rect (none on its edges), at
    /// <c>rect.position + offset + k * spacing</c>; the offset wraps by the spacing.
    /// </summary>
    [NowBuilder]
    public struct NowGridLines
    {
        /// <summary>Most lines drawn per axis; smaller spacings are widened to fit.</summary>
        public const int MaxLinesPerAxis = 512;

        NowRect _rect;
        Vector2 _spacing;
        Vector2 _offset;
        float _thickness;
        Color _color;
        bool _vertical;
        bool _horizontal;

        internal NowGridLines(NowRect rect, float spacing)
        {
            _rect = rect;
            _spacing = new Vector2(spacing, spacing);
            _offset = default;
            _thickness = 1f;
            _color = new Color(1f, 1f, 1f, 0.06f);
            _vertical = true;
            _horizontal = true;
        }

        /// <summary>Same spacing on both axes, in UI units.</summary>
        public NowGridLines SetSpacing(float spacing)
        {
            _spacing = new Vector2(spacing, spacing);
            return this;
        }

        /// <summary>Horizontal distance between vertical lines and vertical distance between horizontal lines.</summary>
        public NowGridLines SetSpacing(float x, float y)
        {
            _spacing = new Vector2(x, y);
            return this;
        }

        /// <summary>Shifts the grid, for example by the content's scroll or pan; wraps by the spacing.</summary>
        public NowGridLines SetOffset(Vector2 offset)
        {
            _offset = offset;
            return this;
        }

        /// <summary>Line width in UI units (default 1).</summary>
        public NowGridLines SetThickness(float thickness)
        {
            _thickness = Mathf.Max(0f, thickness);
            return this;
        }

        public NowGridLines SetColor(Color color)
        {
            _color = color;
            return this;
        }

        /// <summary>Draws only the vertical and/or horizontal family of lines.</summary>
        public NowGridLines SetAxes(bool vertical, bool horizontal)
        {
            _vertical = vertical;
            _horizontal = horizontal;
            return this;
        }

        [NowConsumer]
        public NowGridLines Draw()
        {
            if (_rect.isEmpty || _thickness <= 0f || _color.a <= 0f)
                return this;

            if (_vertical)
                DrawAxis(true);

            if (_horizontal)
                DrawAxis(false);

            return this;
        }

        readonly void DrawAxis(bool vertical)
        {
            float extent = vertical ? _rect.width : _rect.height;
            float spacing = vertical ? _spacing.x : _spacing.y;

            if (!(spacing > 0f) || float.IsInfinity(spacing) || float.IsNaN(spacing))
                return;

            spacing = Mathf.Max(spacing, extent / MaxLinesPerAxis);
            float phase = Mathf.Repeat(vertical ? _offset.x : _offset.y, spacing);
            float first = phase > 0f ? phase : spacing;

            for (float position = first; position < extent; position += spacing)
            {
                var line = vertical
                    ? new NowRect(_rect.x + position, _rect.y, _thickness, _rect.height)
                    : new NowRect(_rect.x, _rect.y + position, _rect.width, _thickness);
                Now.Rectangle(line).SetColor(_color).Draw();
            }
        }
    }

    public static partial class Now
    {
        /// <summary>Starts a grid of hairlines across <paramref name="rect"/>; see <see cref="NowGridLines"/>.</summary>
        public static NowGridLines GridLines(NowRect rect, float spacing)
        {
            return new NowGridLines(rect, spacing);
        }
    }
}
