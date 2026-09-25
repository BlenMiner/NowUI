using System.Collections.Generic;
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
        NowControlIdentity _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        NowRectangleStyle _rectPreset;
        NowTextStyle _textPreset;
        NowLayoutAlign _alignItems;
        readonly NowRect _rect;
        readonly bool _hasRect;

        const int AreaKeySeed = 0x4e424172;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly HashSet<int> s_warnedBeginLabelSites = new HashSet<int>();
#endif

        NowResolvedId ResolveControlId() => _id.Resolve(_site);

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

        /// <summary>Uses an identity that has already been fully resolved.</summary>
        public NowButton SetId(NowResolvedId id) { _id = id; return this; }

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
        /// An exact layout host sizes the button from content measured in the same
        /// rebuild; a one-pass host uses the previous measurement. The label is
        /// never rendered here — identity comes from the call site, so it can
        /// simply be omitted.
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_label.Length > 0 && s_warnedBeginLabelSites.Add(_site))
                Debug.LogWarning($"NowUI: Button(\"{_label}\").Begin() never renders the label — draw it as content inside the scope (NowLayout.Label(...)), or use Draw() for a plain labeled button.");
#endif

            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            NowResolvedId id = ResolveControlId();
            NowResolvedId areaKey = id.Derive(NowIdDomain.Layout, AreaKeySeed);

            Vector4 padding = theme.controlStyles.buttonPadding;
            NowLayout.TryGetCachedAreaContentSize(areaKey, out Vector2 cached);
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
            var row = NowLayout.HorizontalScope(spacing: theme.controlStyles.buttonContentGap, alignItems: _alignItems);

            return new NowControlScope(mask, area, row, rect, interaction, focused, interaction.clicked || submitted);
        }

        /// <summary>
        /// The size this control takes in layout flow with its current settings: the
        /// content size, replaced by a fixed <c>SetWidth</c>/<c>SetHeight</c> and
        /// clamped by min/max options. Stretching axes report the content size. Use it
        /// to size explicit rects without guessing, e.g. a row of links.
        /// </summary>
        public readonly Vector2 Measure()
        {
            var theme = NowTheme.themeAsset;
            return NowControls.MeasuredSize(_options, theme.controlRenderer.MeasureButton(theme, _label, _textPreset));
        }

        public bool Draw()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            NowResolvedId id = ResolveControlId();

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
    /// Focusable list row with caller-owned selection. <see cref="Draw"/> returns
    /// true on click or submit while focused.
    /// </summary>
    [NowBuilder]
    public struct NowSelectableRow
    {
        readonly string _label;
        readonly int _site;
        NowControlIdentity _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        NowTextStyle _textPreset;
        bool _selected;
        bool _hasTextColor;
        Color _textColor;
        float _paddingX;
        float _paddingY;
        readonly NowRect _rect;
        readonly bool _hasRect;

        const float DefaultPaddingX = 8f;
        const float DefaultPaddingY = 4f;
        const float DefaultMinHeight = 22f;

        NowResolvedId ResolveControlId() => _id.Resolve(_site);

        internal NowSelectableRow(string label, int site)
        {
            _label = label ?? string.Empty;
            _site = site;
            _id = default;
            _navigation = default;
            _options = default;
            _textPreset = NowTextStyle.Body;
            _selected = false;
            _hasTextColor = false;
            _textColor = default;
            _paddingX = DefaultPaddingX;
            _paddingY = DefaultPaddingY;
            _rect = default;
            _hasRect = false;
        }

        internal NowSelectableRow(NowRect rect, string label, int site) : this(label, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowSelectableRow SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowSelectableRow SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Fixed height in layout flow.</summary>
        public NowSelectableRow SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowSelectableRow SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the rendered label.</summary>
        public NowSelectableRow SetId(NowId id) { _id = id; return this; }

        /// <summary>Uses an identity that has already been fully resolved.</summary>
        public NowSelectableRow SetId(NowResolvedId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowSelectableRow SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Caller-owned selection state.</summary>
        public NowSelectableRow SetSelected(bool selected = true) { _selected = selected; return this; }

        /// <summary>Themed text style for the label.</summary>
        public NowSelectableRow SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Overrides the text color while preserving the chosen text style.</summary>
        public NowSelectableRow SetColor(Color color) { _textColor = color; _hasTextColor = true; return this; }

        /// <summary>Horizontal and vertical label padding.</summary>
        public NowSelectableRow SetPadding(float horizontal, float vertical)
        {
            _paddingX = Mathf.Max(0f, horizontal);
            _paddingY = Mathf.Max(0f, vertical);
            return this;
        }

        /// <summary>
        /// The size this control takes in layout flow with its current settings: the
        /// content size, replaced by a fixed <c>SetWidth</c>/<c>SetHeight</c> and
        /// clamped by min/max options. Stretching axes report the content size. Use it
        /// to size explicit rects without guessing, e.g. a row of links.
        /// </summary>
        public readonly Vector2 Measure()
        {
            Vector2 textSize = NowControls.Text(NowTheme.themeAsset, _textPreset).Measure(_label);
            return NowControls.MeasuredSize(_options, new Vector2(
                textSize.x + _paddingX * 2f,
                Mathf.Max(DefaultMinHeight, textSize.y + _paddingY * 2f)));
        }

        public bool Draw()
        {
            var theme = NowTheme.themeAsset;
            NowText text = NowControls.Text(theme, _textPreset);

            if (_hasTextColor)
                text = text.SetColor(_textColor);

            Vector2 textSize = text.Measure(_label);
            var contentSize = new Vector2(
                textSize.x + _paddingX * 2f,
                Mathf.Max(DefaultMinHeight, textSize.y + _paddingY * 2f));

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(ResolveControlId(), rect, _navigation, out bool focused, out bool submitted);

            DrawBackground(theme, rect, interaction, focused);

            NowRect labelRect = rect.Inset(_paddingX, 0f);

            if (_hasTextColor)
                NowControls.DrawLeftLabel(theme, labelRect, _label, _textPreset, _textColor);
            else
                NowControls.DrawLeftLabel(theme, labelRect, _label, _textPreset);

            return interaction.clicked || submitted;
        }

        void DrawBackground(NowThemeAsset theme, NowRect rect, NowInteraction interaction, bool focused)
        {
            NowRect visual = rect.Inset(2f, 1f);

            if (_selected)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.18f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, focused ? 0.70f : 0.48f))
                    .Draw();
                return;
            }

            if (focused)
            {
                Color accent = theme.GetColor(NowColorToken.Accent);
                Now.Rectangle(visual)
                    .SetRadius(3f)
                    .SetColor(new Color(accent.r, accent.g, accent.b, 0.07f))
                    .SetOutline(1f)
                    .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.42f))
                    .Draw();
                return;
            }

            if (!interaction.hovered && !interaction.held)
                return;

            Color surface = theme.GetColor(NowColorToken.SurfaceMuted);
            surface = NowControls.StateColor(theme, surface, 1f, interaction.held);
            Now.Rectangle(visual)
                .SetRadius(3f)
                .SetColor(surface)
                .Draw();
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
        NowControlIdentity _id;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        NowLayoutAlign _alignItems;

        const int AreaKeySeed = 0x4e434172;

        NowResolvedId ResolveControlId() => _id.Resolve(_site);

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

        /// <summary>Uses an identity that has already been fully resolved.</summary>
        public NowCheckbox SetId(NowResolvedId id) { _id = id; return this; }

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
            NowResolvedId id = ResolveControlId();
            NowResolvedId areaKey = id.Derive(NowIdDomain.Layout, AreaKeySeed);

            NowLayout.TryGetCachedAreaContentSize(areaKey, out Vector2 cached);
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
            var row = NowLayout.HorizontalScope(spacing: theme.controlStyles.buttonContentGap, alignItems: _alignItems);

            return new NowControlScope(mask, area, row, rect, interaction, focused, clicked);
        }

        /// <summary>
        /// The size this control takes in layout flow with its current settings: the
        /// content size, replaced by a fixed <c>SetWidth</c>/<c>SetHeight</c> and
        /// clamped by min/max options. Stretching axes report the content size. Use it
        /// to size explicit rects without guessing, e.g. a row of links.
        /// </summary>
        public readonly Vector2 Measure()
        {
            var theme = NowTheme.themeAsset;
            return NowControls.MeasuredSize(_options, theme.controlRenderer.MeasureToggle(theme, _label, _textPreset));
        }

        public bool Draw(ref bool value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            NowResolvedId id = ResolveControlId();

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
        NowControlIdentity _id;
        NowFocusNavigation _navigation;
        bool _isOn;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        NowLayoutAlign _alignItems;

        const int AreaKeySeed = 0x4e524172;

        NowResolvedId ResolveControlId() => _id.Resolve(_site);

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

        /// <summary>Uses an identity that has already been fully resolved.</summary>
        public NowRadio SetId(NowResolvedId id) { _id = id; return this; }

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
            NowResolvedId id = ResolveControlId();
            NowResolvedId areaKey = id.Derive(NowIdDomain.Layout, AreaKeySeed);

            NowLayout.TryGetCachedAreaContentSize(areaKey, out Vector2 cached);
            var contentSize = renderer.MeasureToggleContent(theme, cached);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);

            float glyphSize = theme.controlStyles.toggleSize;
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            var glyphRect = renderer.ToggleGlyphRect(theme, rect, glyphSize);
            renderer.DrawRadio(new NowToggleRenderContext(theme, rect, glyphRect, _isOn, interaction, focused, hoverT));

            var mask = Now.Mask(rect);
            var area = NowLayout.Area(areaKey, renderer.ToggleContentRect(theme, rect, glyphSize));
            var row = NowLayout.HorizontalScope(spacing: theme.controlStyles.buttonContentGap, alignItems: _alignItems);

            return new NowControlScope(mask, area, row, rect, interaction, focused, interaction.clicked || submitted);
        }

        /// <summary>
        /// The size this control takes in layout flow with its current settings: the
        /// content size, replaced by a fixed <c>SetWidth</c>/<c>SetHeight</c> and
        /// clamped by min/max options. Stretching axes report the content size. Use it
        /// to size explicit rects without guessing, e.g. a row of links.
        /// </summary>
        public readonly Vector2 Measure()
        {
            var theme = NowTheme.themeAsset;
            return NowControls.MeasuredSize(_options, theme.controlRenderer.MeasureToggle(theme, _label, _textPreset));
        }

        public bool Draw()
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            NowResolvedId id = ResolveControlId();

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

        /// <summary>
        /// Draws one option of a radio group bound to <paramref name="selected"/>: it
        /// shows as on when <paramref name="selected"/> equals <paramref name="value"/>,
        /// and choosing it assigns <paramref name="value"/>. Returns true when the
        /// selection changed. Works with ints, enums, strings or any value type:
        /// <code>
        /// NowLayout.Radio("Low").Draw(ref quality, Quality.Low);
        /// NowLayout.Radio("High").Draw(ref quality, Quality.High);
        /// </code>
        /// The builder's own on/off argument is ignored.
        /// </summary>
        public bool Draw<TValue>(ref TValue selected, TValue value)
        {
            var option = this;
            option._isOn = EqualityComparer<TValue>.Default.Equals(selected, value);

            if (!option.Draw() || option._isOn)
                return false;

            selected = value;
            return true;
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
        /// <summary>Space between the label, the track and the value readout, in UI units.</summary>
        public const float LabelGap = 12f;

        readonly float _min;
        readonly float _max;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        readonly int _site;
        NowControlIdentity _id;
        NowFocusNavigation _navigation;
        float _step;
        string _label;
        string _valueFormat;

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
            _label = null;
            _valueFormat = null;
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

        /// <summary>Minimum width in layout flow, label and value readout included.</summary>
        public NowSlider SetMinWidth(float width) { _options = _options.SetMinWidth(width); return this; }

        /// <summary>Maximum width in layout flow, label and value readout included; keeps a stretching slider from running to the container's edge.</summary>
        public NowSlider SetMaxWidth(float width) { _options = _options.SetMaxWidth(width); return this; }

        /// <summary>
        /// Draws <paramref name="label"/> in the layout label style to the left of the
        /// track, inside the slider's own rect, separated by <see cref="LabelGap"/>.
        /// The measured width includes it, so labelled sliders never touch their
        /// neighbours.
        /// </summary>
        public NowSlider SetLabel(string label) { _label = string.IsNullOrEmpty(label) ? null : label; return this; }

        /// <summary>
        /// Shows the current value to the right of the track, formatted with a .NET
        /// numeric format string such as <c>"0"</c>, <c>"0.00x"</c> or <c>"0'%'"</c>.
        /// The readout reserves the width of the wider of the range's ends, so the
        /// track does not shift while dragging.
        /// </summary>
        public NowSlider SetValueFormat(string format) { _valueFormat = format ?? ""; return this; }

        /// <summary>Snap values to increments anchored at the slider minimum. Use 1 for integer sliders.</summary>
        public NowSlider SetStep(float step) { _step = Mathf.Max(0f, step); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowSlider SetId(NowId id) { _id = id; return this; }

        /// <summary>Uses an identity that has already been fully resolved.</summary>
        public NowSlider SetId(NowResolvedId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowSlider SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        readonly Vector2 ContentSize(
            NowThemeAsset theme,
            out Vector2 trackSize,
            out NowText textStyle,
            out float labelWidth,
            out float valueWidth)
        {
            float min = Mathf.Min(_min, _max);
            float max = Mathf.Max(_min, _max);
            trackSize = theme.controlRenderer.MeasureSlider(theme);
            bool decorated = _label != null || _valueFormat != null;
            textStyle = decorated ? NowLayout.labelStyle : default;
            labelWidth = 0f;
            valueWidth = 0f;
            float textHeight = 0f;

            if (_label != null)
            {
                Vector2 size = textStyle.Measure(_label);
                labelWidth = size.x;
                textHeight = size.y;
            }

            if (_valueFormat != null)
            {
                Vector2 lower = textStyle.Measure((double)min, _valueFormat);
                Vector2 upper = textStyle.Measure((double)max, _valueFormat);
                valueWidth = Mathf.Max(lower.x, upper.x);
                textHeight = Mathf.Max(textHeight, Mathf.Max(lower.y, upper.y));
            }

            return decorated
                ? new Vector2(
                    labelWidth + (_label != null ? LabelGap : 0f) + trackSize.x + (_valueFormat != null ? LabelGap : 0f) + valueWidth,
                    Mathf.Max(trackSize.y, textHeight))
                : trackSize;
        }

        /// <summary>
        /// The size this control takes in layout flow with its current settings: the
        /// content size, replaced by a fixed <c>SetWidth</c>/<c>SetHeight</c> and
        /// clamped by min/max options. Stretching axes report the content size. Use it
        /// to size explicit rects without guessing, e.g. a row of links.
        /// </summary>
        public readonly Vector2 Measure()
        {
            return NowControls.MeasuredSize(_options, ContentSize(NowTheme.themeAsset, out _, out _, out _, out _));
        }

        public bool Draw(ref float value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            NowResolvedId id = _id.Resolve(_site);

            float knobSize = theme.controlStyles.sliderKnobSize;
            float min = Mathf.Min(_min, _max);
            float max = Mathf.Max(_min, _max);
            bool decorated = _label != null || _valueFormat != null;
            var measured = ContentSize(theme, out Vector2 trackSize, out NowText textStyle, out float labelWidth, out float valueWidth);

            NowRect outer = NowControls.ReserveRect(_hasRect, _rect, _options, measured);
            NowRect rect = outer;
            NowRect labelRect = default;
            NowRect valueRect = default;

            if (decorated)
            {
                if (_label != null)
                {
                    labelRect = rect.TakeLeft(Mathf.Min(labelWidth, rect.width), out rect);
                    rect = rect.TakeRight(Mathf.Max(0f, rect.width - LabelGap));
                }

                if (_valueFormat != null)
                {
                    valueRect = rect.TakeRight(Mathf.Min(valueWidth, rect.width), out rect);
                    rect = rect.TakeLeft(Mathf.Max(0f, rect.width - LabelGap));
                }

                float trackHeight = Mathf.Min(trackSize.y, outer.height);
                rect = new NowRect(rect.x, outer.y + (outer.height - trackHeight) * 0.5f, rect.width, trackHeight);
            }

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out _);

            float range = Mathf.Max(max - min, 0.0001f);
            float previous = value;

            if (interaction.held && rect.width > knobSize)
            {
                float t = Mathf.Clamp01((interaction.pointerPosition.x - rect.x - knobSize * 0.5f) / (rect.width - knobSize));
                value = min + t * range;
            }

            if (focused && !NowInput.isPassive)
            {
                float navX = NowInput.current.navigation.x;

                if (NowControlState.Repeat(id, "nav", Mathf.Abs(navX) > 0.55f, 0.35f, 0.08f))
                    value += Mathf.Sign(navX) * range * theme.controlStyles.sliderNavigationStep;
            }

            value = Mathf.Clamp(value, min, max);

            if (_step > 0f)
                value = Snap(value, min, max, _step);

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            float normalized = (value - min) / range;
            var metrics = renderer.CalculateSliderMetrics(theme, rect, normalized);
            renderer.DrawSlider(new NowSliderRenderContext(theme, rect, metrics, interaction, focused, hoverT));

            if (_label != null && labelRect.width > 0f)
            {
                textStyle
                    .SetPosition(new NowRect(labelRect.x, outer.y, labelRect.width, outer.height))
                    .SetAlign(NowTextAlign.Left, NowTextVerticalAlign.CapMiddle)
                    .Draw(_label);
            }

            if (_valueFormat != null && valueRect.width > 0f)
            {
                textStyle
                    .SetPosition(new NowRect(valueRect.x, outer.y, valueRect.width, outer.height))
                    .SetAlign(NowTextAlign.Right, NowTextVerticalAlign.CapMiddle)
                    .Draw((double)value, _valueFormat);
            }

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
