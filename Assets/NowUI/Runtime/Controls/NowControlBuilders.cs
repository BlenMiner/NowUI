using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Push button. <see cref="Draw"/> returns true on click or on submit while
    /// focused (keyboard/gamepad).
    /// </summary>
    public struct NowButton
    {
        readonly string _label;
        NowLayoutOptions _options;
        string _rectPreset;
        string _textPreset;
        NowRect _rect;
        bool _hasRect;

        internal NowButton(string label)
        {
            _label = label ?? string.Empty;
            _options = default;
            _rectPreset = "accent";
            _textPreset = "button";
            _rect = default;
            _hasRect = false;
        }

        internal NowButton(NowRect rect, string label) : this(label)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowButton SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowButton SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowButton SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        public NowButton SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowButton SetPreset(string rectanglePreset) { _rectPreset = rectanglePreset; return this; }

        public NowButton SetTextPreset(string textPreset) { _textPreset = textPreset; return this; }

        /// <summary>
        /// Opens the button as a container for custom content — icons, sub-labels,
        /// anything drawn with layout calls. Interaction runs immediately, so the
        /// result is readable inside the scope; children flow in a horizontal row.
        /// In layout flow the button sizes to the previous frame's content, like all
        /// scope-form layout.
        /// <code>
        /// using (var save = NowLayout.Button("save-btn").Begin())
        /// {
        ///     if (save.clicked) Save();
        ///     NowLayout.Lottie(spinner).SetHeight(18).Draw();
        ///     NowLayout.Label("Save").Draw();
        /// }
        /// </code>
        /// </summary>
        public NowControlScope Begin()
        {
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_label);

            Vector4 padding = theme.GetSpacing("md", new Vector4(12f, 12f, 12f, 12f));
            NowLayout.TryGetCachedContentSize(_label, out Vector2 cached);
            var fallback = new Vector2(padding.x + padding.z + 40f, padding.y + padding.w + 20f);
            var contentSize = cached.x > 0f ? cached : fallback;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);

            var rectangle = theme.Rectangle(rect, _rectPreset);
            rectangle.color = NowControls.StateTint(rectangle.color, hoverT, interaction.held);

            if (focused)
            {
                rectangle.outline = Mathf.Max(rectangle.outline, 2f);
                rectangle.outlineColor = theme.GetColor("text", Color.black);
            }

            rectangle.Draw();

            var area = NowLayout.Area(_label, rect, new NowLayoutOptions().SetPadding(padding));
            var row = NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(6f));

            return new NowControlScope(area, row, rect, interaction, focused, interaction.clicked || submitted);
        }

        public bool Draw()
        {
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_label);

            var text = theme.Text(default, _textPreset);
            Vector2 labelSize = text.Measure(_label);
            Vector4 padding = theme.GetSpacing("md", new Vector4(12f, 12f, 12f, 12f));
            var contentSize = new Vector2(
                labelSize.x + padding.x + padding.z,
                labelSize.y + (padding.y + padding.w) * 0.5f);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);

            var rectangle = theme.Rectangle(rect, _rectPreset);
            rectangle.color = NowControls.StateTint(rectangle.color, hoverT, interaction.held);

            if (focused)
            {
                rectangle.outline = Mathf.Max(rectangle.outline, 2f);
                rectangle.outlineColor = theme.GetColor("text", Color.black);
            }

            rectangle.Draw();
            NowControls.DrawCenteredLabel(theme, rect, _label, _textPreset, rect);

            return interaction.clicked || submitted;
        }
    }

    /// <summary>
    /// Scope returned by the controls' Begin() methods; interaction results are
    /// readable inside the scope while custom content draws as layout children.
    /// For toggling controls (checkbox, radio), <see cref="clicked"/> doubles as
    /// "the value changed this frame".
    /// </summary>
    public struct NowControlScope : System.IDisposable
    {
        public readonly NowUIInteraction interaction;

        public readonly NowRect rect;

        /// <summary>True on click or on submit while focused.</summary>
        public readonly bool clicked;

        public readonly bool focused;

        NowLayoutScope _area;
        NowLayoutScope _row;
        bool _disposed;

        internal NowControlScope(NowLayoutScope area, NowLayoutScope row, NowRect rect, NowUIInteraction interaction, bool focused, bool clicked)
        {
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
        }
    }

    /// <summary>
    /// Checkbox with a label. <see cref="Draw(ref bool)"/> toggles the caller's
    /// value on click/submit and returns true when it changed.
    /// </summary>
    public struct NowCheckbox
    {
        string _label;
        NowLayoutOptions _options;
        NowRect _rect;
        bool _hasRect;
        string _textPreset;

        internal NowCheckbox(string label)
        {
            _label = label ?? string.Empty;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = "body";
        }

        internal NowCheckbox(NowRect rect, string label) : this(label)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowCheckbox SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowCheckbox SetTextPreset(string textPreset) { _textPreset = textPreset; return this; }

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
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_label);

            const float Box = 18f;
            const float Gap = 8f;

            NowLayout.TryGetCachedContentSize(_label, out Vector2 cached);
            var contentSize = new Vector2(
                Box + Gap + Mathf.Max(cached.x, 40f),
                Mathf.Max(Box, cached.y));

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
            bool clicked = interaction.clicked || submitted;

            if (clicked)
                value = !value;

            DrawCheckboxGlyph(theme, rect, id, value, focused, interaction);

            var area = NowLayout.Area(_label, new NowRect(rect.x + Box + Gap, rect.y, rect.width - Box - Gap, rect.height));
            var row = NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(6f));

            return new NowControlScope(area, row, rect, interaction, focused, clicked);
        }

        void DrawCheckboxGlyph(NowUITheme theme, NowRect rect, int id, bool value, bool focused, in NowUIInteraction interaction)
        {
            const float Box = 18f;
            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);
            var boxRect = new NowRect(rect.x, rect.y + (rect.height - Box) * 0.5f, Box, Box);

            var frame = theme.Rectangle(boxRect, value ? "accent" : "outline");
            frame.color = NowControls.StateTint(frame.color, hoverT, interaction.held);

            if (focused)
            {
                frame.outline = Mathf.Max(frame.outline, 2f);
                frame.outlineColor = theme.GetColor("text", Color.black);
            }

            frame.Draw();

            if (value)
            {
                float inset = Box * 0.3f;
                Now.Rectangle(new NowRect(boxRect.x + inset, boxRect.y + inset, Box - inset * 2f, Box - inset * 2f))
                    .SetColor(theme.GetColor("accent-text", Color.white))
                    .SetRadius(2f)
                    .Draw();
            }
        }

        public bool Draw(ref bool value)
        {
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_label);

            var text = theme.Text(default, _textPreset);
            Vector2 labelSize = text.Measure(_label);
            float box = Mathf.Max(18f, labelSize.y);
            const float Gap = 8f;
            var contentSize = new Vector2(box + Gap + labelSize.x, Mathf.Max(box, labelSize.y));

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
            bool changed = interaction.clicked || submitted;

            if (changed)
                value = !value;

            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);
            var boxRect = new NowRect(rect.x, rect.y + (rect.height - box) * 0.5f, box, box);

            var frame = theme.Rectangle(boxRect, value ? "accent" : "outline");
            frame.color = NowControls.StateTint(frame.color, hoverT, interaction.held);

            if (focused)
            {
                frame.outline = Mathf.Max(frame.outline, 2f);
                frame.outlineColor = theme.GetColor("text", Color.black);
            }

            frame.Draw();

            if (value)
            {
                float inset = box * 0.3f;
                Now.Rectangle(new NowRect(boxRect.x + inset, boxRect.y + inset, box - inset * 2f, box - inset * 2f))
                    .SetColor(theme.GetColor("accent-text", Color.white))
                    .SetRadius(2f)
                    .Draw();
            }

            NowControls.DrawLeftLabel(theme, new NowRect(rect.x + box + Gap, rect.y, rect.width - box - Gap, rect.height), _label, _textPreset);
            return changed;
        }
    }

    /// <summary>
    /// Radio option; pass whether it is the selected one and set your selection when
    /// <see cref="Draw"/> returns true:
    /// <code>if (NowLayout.Radio("High", quality == 2).Draw()) quality = 2;</code>
    /// </summary>
    public struct NowRadio
    {
        string _label;
        bool _isOn;
        NowLayoutOptions _options;
        NowRect _rect;
        bool _hasRect;
        string _textPreset;

        internal NowRadio(string label, bool isOn)
        {
            _label = label ?? string.Empty;
            _isOn = isOn;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = "body";
        }

        internal NowRadio(NowRect rect, string label, bool isOn) : this(label, isOn)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowRadio SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowRadio SetTextPreset(string textPreset) { _textPreset = textPreset; return this; }

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
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_label);

            const float Circle = 18f;
            const float Gap = 8f;

            NowLayout.TryGetCachedContentSize(_label, out Vector2 cached);
            var contentSize = new Vector2(
                Circle + Gap + Mathf.Max(cached.x, 40f),
                Mathf.Max(Circle, cached.y));

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);

            DrawRadioGlyph(theme, rect, id, focused, interaction);

            var area = NowLayout.Area(_label, new NowRect(rect.x + Circle + Gap, rect.y, rect.width - Circle - Gap, rect.height));
            var row = NowLayout.Horizontal(new NowLayoutOptions().SetSpacing(6f));

            return new NowControlScope(area, row, rect, interaction, focused, interaction.clicked || submitted);
        }

        void DrawRadioGlyph(NowUITheme theme, NowRect rect, int id, bool focused, in NowUIInteraction interaction)
        {
            const float Circle = 18f;
            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);
            var circleRect = new NowRect(rect.x, rect.y + (rect.height - Circle) * 0.5f, Circle, Circle);

            var frame = theme.Rectangle(circleRect, _isOn ? "accent" : "outline");
            frame.radius = new Vector4(Circle, Circle, Circle, Circle) * 0.5f;
            frame.color = NowControls.StateTint(frame.color, hoverT, interaction.held);

            if (focused)
            {
                frame.outline = Mathf.Max(frame.outline, 2f);
                frame.outlineColor = theme.GetColor("text", Color.black);
            }

            frame.Draw();

            if (_isOn)
            {
                float inset = Circle * 0.32f;
                float dot = Circle - inset * 2f;
                Now.Rectangle(new NowRect(circleRect.x + inset, circleRect.y + inset, dot, dot))
                    .SetColor(theme.GetColor("accent-text", Color.white))
                    .SetRadius(dot * 0.5f)
                    .Draw();
            }
        }

        public bool Draw()
        {
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_label);

            var text = theme.Text(default, _textPreset);
            Vector2 labelSize = text.Measure(_label);
            float circle = Mathf.Max(18f, labelSize.y);
            const float Gap = 8f;
            var contentSize = new Vector2(circle + Gap + labelSize.x, Mathf.Max(circle, labelSize.y));

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, contentSize);
            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);

            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);
            var circleRect = new NowRect(rect.x, rect.y + (rect.height - circle) * 0.5f, circle, circle);

            var frame = theme.Rectangle(circleRect, _isOn ? "accent" : "outline");
            frame.radius = new Vector4(circle, circle, circle, circle) * 0.5f;
            frame.color = NowControls.StateTint(frame.color, hoverT, interaction.held);

            if (focused)
            {
                frame.outline = Mathf.Max(frame.outline, 2f);
                frame.outlineColor = theme.GetColor("text", Color.black);
            }

            frame.Draw();

            if (_isOn)
            {
                float inset = circle * 0.32f;
                float dot = circle - inset * 2f;
                Now.Rectangle(new NowRect(circleRect.x + inset, circleRect.y + inset, dot, dot))
                    .SetColor(theme.GetColor("accent-text", Color.white))
                    .SetRadius(dot * 0.5f)
                    .Draw();
            }

            NowControls.DrawLeftLabel(theme, new NowRect(rect.x + circle + Gap, rect.y, rect.width - circle - Gap, rect.height), _label, _textPreset);
            return interaction.clicked || submitted;
        }
    }

    /// <summary>
    /// Horizontal slider. <see cref="Draw(ref float)"/> updates the caller's value
    /// from pointer drags (or navigation steps while focused) and returns true when
    /// it changed.
    /// </summary>
    public struct NowSlider
    {
        float _min;
        float _max;
        NowLayoutOptions _options;
        NowRect _rect;
        bool _hasRect;
        string _id;

        internal NowSlider(float min, float max)
        {
            _min = min;
            _max = max;
            _options = default;
            _rect = default;
            _hasRect = false;
            _id = "slider";
        }

        internal NowSlider(NowRect rect, float min, float max) : this(min, max)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowSlider SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowSlider SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowSlider SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Sliders have no label; give ones that coexist a distinct id.</summary>
        public NowSlider SetId(string id) { _id = id; return this; }

        public bool Draw(ref float value)
        {
            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_id);

            const float Height = 20f;
            const float Knob = 16f;
            const float Track = 6f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(160f, Height));
            var interaction = NowControls.Interact(id, rect, out bool focused, out _);

            float range = Mathf.Max(_max - _min, 0.0001f);
            float previous = value;

            if (interaction.held && rect.width > Knob)
            {
                float t = Mathf.Clamp01((interaction.pointerPosition.x - rect.x - Knob * 0.5f) / (rect.width - Knob));
                value = _min + t * range;
            }

            if (focused && !NowUIInput.isPassive)
            {
                float navX = NowUIInput.current.navigation.x;

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "nav"), Mathf.Abs(navX) > 0.55f, 0.35f, 0.08f))
                    value += Mathf.Sign(navX) * range * 0.05f;
            }

            value = Mathf.Clamp(value, _min, _max);

            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);
            float normalized = (value - _min) / range;
            float knobX = rect.x + normalized * (rect.width - Knob);
            float trackY = rect.y + (rect.height - Track) * 0.5f;

            var track = theme.Rectangle(new NowRect(rect.x, trackY, rect.width, Track), "muted");
            track.radius = new Vector4(Track, Track, Track, Track) * 0.5f;
            track.Draw();

            var fill = theme.Rectangle(new NowRect(rect.x, trackY, knobX - rect.x + Knob * 0.5f, Track), "accent");
            fill.radius = track.radius;
            fill.Draw();

            var knob = theme.Rectangle(new NowRect(knobX, rect.y + (rect.height - Knob) * 0.5f, Knob, Knob), "accent");
            knob.radius = new Vector4(Knob, Knob, Knob, Knob) * 0.5f;
            knob.color = NowControls.StateTint(knob.color, hoverT, interaction.held);

            if (focused)
            {
                knob.outline = 2f;
                knob.outlineColor = theme.GetColor("text", Color.black);
            }

            knob.Draw();
            return !Mathf.Approximately(previous, value);
        }
    }
}
