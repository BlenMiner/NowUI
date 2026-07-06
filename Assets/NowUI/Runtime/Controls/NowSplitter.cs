using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// A draggable divider between two panes sharing one span of space. The
    /// caller owns the split as a 0..1 fraction of <c>span</c> (the pixels the
    /// two panes divide between them, excluding the splitter itself); Draw
    /// drags it, keeps both panes at least <c>minSize</c> tall/wide, and
    /// returns true when the value changed:
    /// <code>
    /// float first = paneSpace * split;
    /// NowLayout.Splitter().Draw(ref split, paneSpace, minSize: 100f);
    /// </code>
    /// Defaults to a row divider between vertically stacked panes (drags up
    /// and down); <see cref="SetColumn"/> makes it a column divider between
    /// side-by-side panes. The hit area extends slightly past the visual so
    /// thin splitters stay grabbable.
    /// </summary>
    [NowBuilder]
    public struct NowSplitter
    {
        const float DefaultThickness = 12f;

        const float GripLength = 48f;

        const float GripThickness = 4f;

        const float HitSlack = 3f;

        readonly int _site;
        NowId _id;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowLayoutOptions _options;
        float _thickness;
        bool _column;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowSplitter(NowId id, int site)
        {
            _site = site;
            _id = id;
            _rect = default;
            _hasRect = false;
            _options = default;
            _thickness = DefaultThickness;
            _column = false;
        }

        internal NowSplitter(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the derived size.</summary>
        public NowSplitter SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowSplitter SetId(NowId id) { _id = id; return this; }

        /// <summary>Reserved thickness across the split axis in layout flow.</summary>
        public NowSplitter SetThickness(float thickness) { _thickness = thickness; return this; }

        /// <summary>Divides side-by-side panes instead of stacked ones; drags horizontally.</summary>
        public NowSplitter SetColumn(bool column = true) { _column = column; return this; }

        public bool Draw(ref float split, float span, float minSize = 0f)
        {
            var theme = NowTheme.themeAsset;
            int id = ResolveControlId();

            var options = _options;

            if (!_hasRect)
            {
                if (_column)
                {
                    if (!options.Has(NowLayoutOptions.Field.Width))
                        options = options.SetWidth(_thickness);

                    if (!options.Has(NowLayoutOptions.Field.Height) && !options.Has(NowLayoutOptions.Field.StretchHeight))
                        options = options.SetStretchHeight();
                }
                else
                {
                    if (!options.Has(NowLayoutOptions.Field.Height))
                        options = options.SetHeight(_thickness);

                    if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                        options = options.SetStretchWidth();
                }
            }

            Vector2 fallbackSize = _column
                ? new Vector2(_thickness, GripLength * 2f)
                : new Vector2(GripLength * 2f, _thickness);
            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, options, fallbackSize);

            NowRect hitRect = _column ? rect.Outset(HitSlack, 0f) : rect.Outset(0f, HitSlack);
            var interaction = NowInput.Interact(id, hitRect);

            // Clamp so each pane keeps minSize even when span shrank since the
            // split was last stored; a span too small for two minimums centers.
            float minFraction = span > 0f ? Mathf.Clamp01(minSize / span) : 0f;
            float maxFraction = 1f - minFraction;
            float previous = split;

            split = minFraction > maxFraction
                ? 0.5f
                : Mathf.Clamp(split, minFraction, maxFraction);

            if (interaction.dragging && span > 0f && minFraction <= maxFraction)
            {
                float delta = _column ? interaction.dragDelta.x : interaction.dragDelta.y;
                split = Mathf.Clamp(split + delta / span, minFraction, maxFraction);
            }

            bool changed = !Mathf.Approximately(previous, split);

            if (changed)
                NowControlState.RequestRepaint();

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            var palette = theme.palette;
            Color gripColor = interaction.held || interaction.dragging
                ? palette.accent
                : Color.Lerp(palette.border, palette.borderStrong, hoverT);

            float gripLength = Mathf.Min(GripLength, _column ? rect.height : rect.width);
            NowRect grip = _column
                ? new NowRect(
                    rect.x + (rect.width - GripThickness) * 0.5f,
                    rect.y + (rect.height - gripLength) * 0.5f,
                    GripThickness,
                    gripLength)
                : new NowRect(
                    rect.x + (rect.width - gripLength) * 0.5f,
                    rect.y + (rect.height - GripThickness) * 0.5f,
                    gripLength,
                    GripThickness);

            Now.Rectangle(grip)
                .SetColor(gripColor)
                .SetRadius(GripThickness * 0.5f)
                .Draw();

            return changed;
        }
    }
}
