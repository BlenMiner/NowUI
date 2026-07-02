using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Non-interactive pill label for counts and statuses.
    /// <code>NowLayout.Badge("3").SetStyle(NowRectangleStyle.Danger).Draw();</code>
    /// </summary>
    [NowBuilder]
    public struct NowBadge
    {
        readonly string _label;
        readonly int _site;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowRectangleStyle _rectPreset;
        NowTextStyle _textPreset;

        internal NowBadge(string label, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _options = default;
            _rect = default;
            _hasRect = false;
            _rectPreset = NowRectangleStyle.Accent;
            _textPreset = NowTextStyle.Caption;
        }

        internal NowBadge(NowRect rect, string label, int site) : this(label, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowBadge SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowBadge SetStyle(NowRectangleStyle style) { _rectPreset = style; return this; }

        public NowBadge SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        public void Draw()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, renderer.MeasureBadge(theme, _label, _textPreset));
            renderer.DrawBadge(new NowBadgeRenderContext(theme, rect, _label, _rectPreset, _textPreset));
        }
    }

    /// <summary>
    /// Selectable pill, optionally removable. <see cref="Draw()"/> returns true on
    /// click/submit; the removable form reports the remove button separately:
    /// <code>
    /// if (NowLayout.Chip(tag).SetRemovable().Draw(out bool removed) &amp;&amp; !removed) Select(tag);
    /// if (removed) Remove(tag);
    /// </code>
    /// </summary>
    [NowBuilder]
    public struct NowChip
    {
        readonly string _label;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        bool _selected;
        bool _removable;

        const int RemoveKeySeed = 0x4e435872;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowChip(string label, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Label;
            _selected = false;
            _removable = false;
        }

        internal NowChip(NowRect rect, string label, int site) : this(label, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowChip SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Explicit control id, decoupling identity from the rendered label.</summary>
        public NowChip SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowChip SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        public NowChip SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Draws the chip in its selected (accent-tinted) state.</summary>
        public NowChip SetSelected(bool selected) { _selected = selected; return this; }

        /// <summary>Adds a remove button; read it via <see cref="Draw(out bool)"/>.</summary>
        public NowChip SetRemovable(bool removable = true) { _removable = removable; return this; }

        public bool Draw()
        {
            return Draw(out _);
        }

        public bool Draw(out bool removed)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();

            var contentSize = renderer.MeasureChip(theme, _label, _textPreset, _removable);
            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);

            NowRect removeRect = default;
            bool removeHovered = false;
            removed = false;

            if (_removable)
            {
                removeRect = renderer.ChipRemoveRect(theme, rect);
                var removeInteraction = NowInput.Interact(NowInput.CombineId(id, RemoveKeySeed), removeRect);
                removeHovered = removeInteraction.hovered;
                removed = removeInteraction.clicked;
            }

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

            renderer.DrawChip(new NowChipRenderContext(
                theme, rect, _label, _selected, _removable, removeRect, removeHovered, _textPreset, interaction, focused, hoverT));

            return removed || interaction.clicked || submitted;
        }
    }
}
