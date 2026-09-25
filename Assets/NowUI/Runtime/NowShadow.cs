using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// A soft drop shadow under a rounded rect, drawn before (so beneath) whatever
    /// the caller draws over it: a panel, a glass pane, an SDF scene or an image.
    /// Build it with <see cref="Now.Shadow(NowRect)"/>:
    /// <code>
    /// Now.Shadow(card).SetRadius(18f).SetOffset(0f, 12f).SetBlur(28f).Draw();
    /// Now.Shadow(card).SetRadius(18f).SetElevation(NowElevationToken.Overlay).Draw();
    /// </code>
    /// Opacity, tint, transform, rotation and mask scopes apply as they do to any draw.
    /// </summary>
    [NowBuilder]
    public struct NowShadow
    {
        /// <summary>Default vertical offset, in UI units.</summary>
        public const float DefaultOffsetY = 8f;

        /// <summary>Default blur, in UI units.</summary>
        public const float DefaultBlur = 24f;

        /// <summary>Default shadow opacity applied to the theme's shadow color.</summary>
        public const float DefaultAlpha = 0.35f;

        NowRect _rect;
        Vector4 _radius;
        Vector2 _offset;
        float _blur;
        float _spread;
        Color _color;
        bool _hasColor;
        NowElevationToken _elevation;

        internal NowShadow(NowRect rect)
        {
            _rect = rect;
            _radius = default;
            _offset = new Vector2(0f, DefaultOffsetY);
            _blur = DefaultBlur;
            _spread = 0f;
            _color = default;
            _hasColor = false;
            _elevation = NowElevationToken.None;
        }

        /// <summary>Corner radius of the shape casting the shadow; spread grows it.</summary>
        public NowShadow SetRadius(float radius)
        {
            _radius = new Vector4(radius, radius, radius, radius);
            return this;
        }

        /// <summary>Per-corner radii (top-left, top-right, bottom-right, bottom-left), as <see cref="NowRectangle"/> takes them.</summary>
        public NowShadow SetRadius(Vector4 radius)
        {
            _radius = radius;
            return this;
        }

        /// <summary>Offset of the shadow from the rect, in UI units (positive y is down).</summary>
        public NowShadow SetOffset(Vector2 offset)
        {
            _offset = offset;
            return this;
        }

        /// <summary>Offset of the shadow from the rect, in UI units (positive y is down).</summary>
        public NowShadow SetOffset(float x, float y)
        {
            _offset = new Vector2(x, y);
            return this;
        }

        /// <summary>Softness of the edge, in UI units.</summary>
        public NowShadow SetBlur(float blur)
        {
            _blur = Mathf.Max(0f, blur);
            return this;
        }

        /// <summary>Grows (positive) or shrinks (negative) the shadow on every side before blurring.</summary>
        public NowShadow SetSpread(float spread)
        {
            _spread = spread;
            return this;
        }

        /// <summary>
        /// Shadow color including its alpha. Without it the shadow uses the theme's
        /// shadow color at <see cref="DefaultAlpha"/>.
        /// </summary>
        public NowShadow SetColor(Color color)
        {
            _color = color;
            _hasColor = true;
            return this;
        }

        /// <summary>
        /// Uses the active theme's shadow preset for <paramref name="elevation"/> (its
        /// key and ambient layers and dark-mode scaling) instead of the offset, blur,
        /// spread and color set on this builder. Keeps custom surfaces consistent with
        /// the stock controls.
        /// </summary>
        public NowShadow SetElevation(NowElevationToken elevation)
        {
            _elevation = elevation;
            return this;
        }

        [NowConsumer]
        public NowShadow Draw()
        {
            if (_rect.isEmpty)
                return this;

            var theme = NowTheme.themeAsset;

            if (_elevation != NowElevationToken.None)
            {
                if (theme != null)
                    theme.controlRenderer.DrawElevationShadow(theme, _rect, _radius, _elevation);

                return this;
            }

            Color color = _hasColor
                ? _color
                : (theme != null ? theme.GetColor(NowColorToken.Shadow, Color.black) : Color.black).WithAlpha(DefaultAlpha);

            if (color.a <= 0f)
                return this;

            var shadowRect = new NowRect(_rect.x + _offset.x, _rect.y + _offset.y, _rect.width, _rect.height).Outset(_spread);

            if (shadowRect.width <= 0f || shadowRect.height <= 0f)
                return this;

            var radius = new Vector4(
                Mathf.Max(0f, _radius.x + _spread),
                Mathf.Max(0f, _radius.y + _spread),
                Mathf.Max(0f, _radius.z + _spread),
                Mathf.Max(0f, _radius.w + _spread));

            Now.Rectangle(shadowRect)
                .SetRadius(radius)
                .SetBlur(Mathf.Max(0.01f, _blur))
                .SetColor(color)
                .Draw();

            return this;
        }
    }

    public static partial class Now
    {
        /// <summary>
        /// Starts a soft drop shadow under <paramref name="rect"/>; draw it before the
        /// surface it belongs to. See <see cref="NowShadow"/>.
        /// </summary>
        public static NowShadow Shadow(NowRect rect)
        {
            return new NowShadow(rect);
        }
    }
}
