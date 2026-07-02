using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Toggle switch with a sliding knob. <see cref="Draw(ref bool)"/> toggles the
    /// caller's value on click/submit and returns true when it changed. Same
    /// contract as <see cref="NowCheckbox"/>, distinct visual language.
    /// </summary>
    [NowBuilder]
    public struct NowSwitch
    {
        readonly string _label;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowSwitch(string label, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
        }

        internal NowSwitch(NowRect rect, string label, int site) : this(label, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowSwitch SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Explicit control id, decoupling identity from the rendered label.</summary>
        public NowSwitch SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowSwitch SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        public NowSwitch SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        public bool Draw(ref bool value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();

            var contentSize = renderer.MeasureSwitch(theme, _label, _textPreset);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            bool changed = interaction.clicked || submitted;

            if (changed)
                value = !value;

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            float onT = NowControlState.Transition(interaction, "on", value, 14f);
            var glyphRect = renderer.SwitchGlyphRect(theme, rect);

            renderer.DrawSwitch(new NowSwitchRenderContext(theme, rect, glyphRect, value, onT, interaction, focused, hoverT));

            if (!string.IsNullOrEmpty(_label))
                NowControls.DrawLeftLabel(theme, renderer.SwitchContentRect(theme, rect), _label, _textPreset);

            return changed;
        }
    }
}
