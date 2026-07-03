using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Push button. <see cref="Draw"/> returns true on click or on submit while
    /// focused (keyboard/gamepad).
    /// </summary>
    [NowBuilder]
    public struct NowButton
    {
        readonly string _label;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        NowRectangleStyle _rectPreset;
        NowTextStyle _textPreset;
        NowLayoutAlign _alignItems;
        readonly NowRect _rect;
        readonly bool _hasRect;

        const int AreaKeySeed = 0x4e424172;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowButton(string label, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _rectPreset = NowRectangleStyle.Accent;
            _textPreset = NowTextStyle.Button;
            _alignItems = NowLayoutAlign.Start;
            _rect = default;
            _hasRect = false;
        }

        internal NowButton(NowRect rect, string label, int site) : this(label, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowButton SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowButton SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Fixed height in layout flow.</summary>
        public NowButton SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowButton SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the rendered label.</summary>
        public NowButton SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowButton SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Themed rectangle style for the background.</summary>
        public NowButton SetStyle(NowRectangleStyle style) { _rectPreset = style; return this; }

        /// <summary>Themed text style for the label.</summary>
        public NowButton SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Cross-axis alignment for children inside <see cref="Begin"/> (vertical centering of icons/labels).</summary>
        public NowButton SetAlignItems(NowLayoutAlign align) { _alignItems = align; return this; }

        /// <summary>
        /// Opens the button as a container for custom content — icons, sub-labels,
        /// anything drawn with layout calls. Interaction runs immediately, so the
        /// result is readable inside the scope; children flow in a horizontal row.
        /// In layout flow the button sizes to the previous frame's content, like all
        /// scope-form layout. The label is never rendered here — identity comes from
        /// the call site, so it can simply be omitted.
        /// <code>
        /// using (var save = NowLayout.Button().Begin())
        /// {
        ///     if (save.clicked) Save();
        ///     NowLayout.Lottie(spinner).SetHeight(18).Draw();
        ///     NowLayout.Label("Save").Draw();
        /// }
        /// </code>
        /// </summary>
        public NowControlScope Begin()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();
            int areaKey = NowInput.CombineId(id, AreaKeySeed);

            Vector4 padding = theme.controlStyles.buttonPadding;
            NowLayout.TryGetCachedContentSize(areaKey, out Vector2 cached);
            var contentSize = renderer.MeasureButtonContent(theme, cached);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

            renderer.DrawButton(new NowButtonRenderContext(
                theme, rect, null, _rectPreset, _textPreset, interaction, focused, submitted, hoverT));

            // Content is clipped to the button: with deferred sizing the first
            // frames can be smaller than the content, and oversized children should
            // never escape the control visually.
            var mask = Now.Mask(rect);
            var area = NowLayout.Area(areaKey, rect, padding);
            var row = NowLayout.Horizontal(spacing: theme.controlStyles.buttonContentGap, alignItems: _alignItems);

            return new NowControlScope(mask, area, row, rect, interaction, focused, interaction.clicked || submitted);
        }

        public bool Draw()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();

            Vector2 contentSize = renderer.MeasureButton(theme, _label, _textPreset);
            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

            renderer.DrawButton(new NowButtonRenderContext(
                theme, rect, _label, _rectPreset, _textPreset, interaction, focused, submitted, hoverT));

            return interaction.clicked || submitted;
        }
    }

    /// <summary>
    /// Scope returned by the controls' Begin() methods; interaction results are
    /// readable inside the scope while custom content draws as layout children.
    /// For toggling controls (checkbox, radio), <see cref="clicked"/> doubles as
    /// "the value changed this frame".
    /// </summary>
    [NowScope]
    public struct NowControlScope : System.IDisposable
    {
        public readonly NowInteraction interaction;

        public readonly NowRect rect;

        /// <summary>True on click or on submit while focused.</summary>
        public readonly bool clicked;

        public readonly bool focused;

        NowLayoutScope _area;
        NowLayoutScope _row;
        NowMaskScope _mask;
        bool _disposed;

        internal NowControlScope(NowMaskScope mask, NowLayoutScope area, NowLayoutScope row, NowRect rect, NowInteraction interaction, bool focused, bool clicked)
        {
            _mask = mask;
            _area = area;
            _row = row;
            this.rect = rect;
            this.interaction = interaction;
            this.focused = focused;
            this.clicked = clicked;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _row.Dispose();
            _area.Dispose();
            _mask.Dispose();
        }
    }

    /// <summary>
    /// Checkbox with a label. <see cref="Draw(ref bool)"/> toggles the caller's
    /// value on click/submit and returns true when it changed.
    /// </summary>
    [NowBuilder]
    public struct NowCheckbox
    {
        readonly string _label;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        NowLayoutAlign _alignItems;

        const int AreaKeySeed = 0x4e434172;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowCheckbox(string label, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
            _alignItems = NowLayoutAlign.Start;
        }

        internal NowCheckbox(NowRect rect, string label, int site) : this(label, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowCheckbox SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowCheckbox SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Fixed height in layout flow.</summary>
        public NowCheckbox SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowCheckbox SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the rendered label.</summary>
        public NowCheckbox SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowCheckbox SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Themed text style for the label.</summary>
        public NowCheckbox SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Cross-axis alignment for children inside <see cref="Begin"/> (vertical centering of icons/labels).</summary>
        public NowCheckbox SetAlignItems(NowLayoutAlign align) { _alignItems = align; return this; }

        /// <summary>
        /// Opens the checkbox as a container: the box draws on the left and custom
        /// content (labels, icons) flows beside it. The toggle happens here, so the
        /// updated value and <see cref="NowControlScope.clicked"/> (= changed) are
        /// readable inside the scope.
        /// <code>
        /// using (var shadowsBox = NowLayout.Checkbox("shadows").Begin(ref shadows))
        /// {
        ///     NowLayout.Label("Shadows").Draw();
        ///     NowLayout.Label("(expensive)").SetFontSize(11).Draw();
        /// }
        /// </code>
        /// </summary>
        public NowControlScope Begin(ref bool value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();
            int areaKey = NowInput.CombineId(id, AreaKeySeed);

            NowLayout.TryGetCachedContentSize(areaKey, out Vector2 cached);
            var contentSize = renderer.MeasureToggleContent(theme, cached);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            bool clicked = interaction.clicked || submitted;

            if (clicked)
                value = !value;

            float glyphSize = theme.controlStyles.toggleSize;
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            var glyphRect = renderer.ToggleGlyphRect(theme, rect, glyphSize);
            renderer.DrawCheckbox(new NowToggleRenderContext(theme, rect, glyphRect, value, interaction, focused, hoverT));

            var mask = Now.Mask(rect);
            var area = NowLayout.Area(areaKey, renderer.ToggleContentRect(theme, rect, glyphSize));
            var row = NowLayout.Horizontal(spacing: theme.controlStyles.buttonContentGap, alignItems: _alignItems);

            return new NowControlScope(mask, area, row, rect, interaction, focused, clicked);
        }

        public bool Draw(ref bool value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();

            var text = NowControls.Text(theme, _textPreset);
            Vector2 labelSize = text.Measure(_label);
            float box = renderer.ToggleGlyphSize(theme, labelSize.y);
            var contentSize = renderer.MeasureToggle(theme, _label, _textPreset);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            bool changed = interaction.clicked || submitted;

            if (changed)
                value = !value;

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            var boxRect = renderer.ToggleGlyphRect(theme, rect, box);

            renderer.DrawCheckbox(new NowToggleRenderContext(theme, rect, boxRect, value, interaction, focused, hoverT));
            NowControls.DrawLeftLabel(theme, renderer.ToggleContentRect(theme, rect, box), _label, _textPreset);
            return changed;
        }
    }

    /// <summary>
    /// Radio option; pass whether it is the selected one and set your selection when
    /// <see cref="Draw"/> returns true:
    /// <code>if (NowLayout.Radio("High", quality == 2).Draw()) quality = 2;</code>
    /// </summary>
    [NowBuilder]
    public struct NowRadio
    {
        readonly string _label;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        readonly bool _isOn;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        NowLayoutAlign _alignItems;

        const int AreaKeySeed = 0x4e524172;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowRadio(string label, bool isOn, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _id = default;
            _navigation = default;
            _isOn = isOn;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
            _alignItems = NowLayoutAlign.Start;
        }

        internal NowRadio(NowRect rect, string label, bool isOn, int site) : this(label, isOn, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowRadio SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowRadio SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Fixed height in layout flow.</summary>
        public NowRadio SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowRadio SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the rendered label.</summary>
        public NowRadio SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowRadio SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Themed text style for the label.</summary>
        public NowRadio SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Cross-axis alignment for children inside <see cref="Begin"/> (vertical centering of icons/labels).</summary>
        public NowRadio SetAlignItems(NowLayoutAlign align) { _alignItems = align; return this; }

        /// <summary>
        /// Opens the radio as a container: the circle draws on the left and custom
        /// content flows beside it; <see cref="NowControlScope.clicked"/> is readable
        /// inside the scope.
        /// <code>
        /// using (var high = NowLayout.Radio("high", quality == 2).Begin())
        /// {
        ///     if (high.clicked) quality = 2;
        ///     NowLayout.Label("High").Draw();
        /// }
        /// </code>
        /// </summary>
        public NowControlScope Begin()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();
            int areaKey = NowInput.CombineId(id, AreaKeySeed);

            NowLayout.TryGetCachedContentSize(areaKey, out Vector2 cached);
            var contentSize = renderer.MeasureToggleContent(theme, cached);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);

            float glyphSize = theme.controlStyles.toggleSize;
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            var glyphRect = renderer.ToggleGlyphRect(theme, rect, glyphSize);
            renderer.DrawRadio(new NowToggleRenderContext(theme, rect, glyphRect, _isOn, interaction, focused, hoverT));

            var mask = Now.Mask(rect);
            var area = NowLayout.Area(areaKey, renderer.ToggleContentRect(theme, rect, glyphSize));
            var row = NowLayout.Horizontal(spacing: theme.controlStyles.buttonContentGap, alignItems: _alignItems);

            return new NowControlScope(mask, area, row, rect, interaction, focused, interaction.clicked || submitted);
        }

        public bool Draw()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = ResolveControlId();

            var text = NowControls.Text(theme, _textPreset);
            Vector2 labelSize = text.Measure(_label);
            float circle = renderer.ToggleGlyphSize(theme, labelSize.y);
            var contentSize = renderer.MeasureToggle(theme, _label, _textPreset);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            var circleRect = renderer.ToggleGlyphRect(theme, rect, circle);

            renderer.DrawRadio(new NowToggleRenderContext(theme, rect, circleRect, _isOn, interaction, focused, hoverT));
            NowControls.DrawLeftLabel(theme, renderer.ToggleContentRect(theme, rect, circle), _label, _textPreset);
            return interaction.clicked || submitted;
        }
    }

    /// <summary>
    /// Horizontal slider. <see cref="Draw(ref float)"/> updates the caller's value
    /// from pointer drags (or navigation steps while focused) and returns true when
    /// it changed.
    /// </summary>
    [NowBuilder]
    public struct NowSlider
    {
        readonly float _min;
        readonly float _max;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        readonly int _site;
        NowId _id;
        NowFocusNavigation _navigation;
        float _step;

        internal NowSlider(float min, float max, int site)
        {
            _min = min;
            _max = max;
            _options = default;
            _rect = default;
            _hasRect = false;
            _site = site;
            _id = default;
            _navigation = default;
            _step = 0f;
        }

        internal NowSlider(NowRect rect, float min, float max, int site) : this(min, max, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowSlider SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowSlider SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Fixed height in layout flow.</summary>
        public NowSlider SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowSlider SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Snap values to increments anchored at the slider minimum. Use 1 for integer sliders.</summary>
        public NowSlider SetStep(float step) { _step = Mathf.Max(0f, step); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowSlider SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowSlider SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        public bool Draw(ref float value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);

            float knobSize = theme.controlStyles.sliderKnobSize;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, renderer.MeasureSlider(theme));
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out _);

            float range = Mathf.Max(_max - _min, 0.0001f);
            float previous = value;

            if (interaction.held && rect.width > knobSize)
            {
                float t = Mathf.Clamp01((interaction.pointerPosition.x - rect.x - knobSize * 0.5f) / (rect.width - knobSize));
                value = _min + t * range;
            }

            if (focused && !NowInput.isPassive)
            {
                float navX = NowInput.current.navigation.x;

                if (NowControlState.Repeat(id, "nav", Mathf.Abs(navX) > 0.55f, 0.35f, 0.08f))
                    value += Mathf.Sign(navX) * range * theme.controlStyles.sliderNavigationStep;
            }

            value = Mathf.Clamp(value, _min, _max);

            if (_step > 0f)
                value = Snap(value, _min, _max, _step);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            float normalized = (value - _min) / range;
            var metrics = renderer.CalculateSliderMetrics(theme, rect, normalized);
            renderer.DrawSlider(new NowSliderRenderContext(theme, rect, metrics, interaction, focused, hoverT));
            return !Mathf.Approximately(previous, value);
        }

        public bool Draw(ref int value)
        {
            int previous = value;
            float scalar = value;
            var slider = this;

            if (slider._step <= 0f)
                slider._step = 1f;

            slider.Draw(ref scalar);

            int min = Mathf.CeilToInt(Mathf.Min(_min, _max));
            int max = Mathf.FloorToInt(Mathf.Max(_min, _max));
            value = Mathf.Clamp(Mathf.RoundToInt(scalar), min, max);
            return previous != value;
        }

        static float Snap(float value, float min, float max, float step)
        {
            if (step <= 0f)
                return Mathf.Clamp(value, min, max);

            float snapped = min + Mathf.Round((value - min) / step) * step;
            return Mathf.Clamp(snapped, min, max);
        }
    }
}
