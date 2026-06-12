using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Push button. <see cref="Draw"/> returns true on click or on submit while
    /// focused (keyboard/gamepad).
    /// </summary>
    public struct NowButton
    {
        string _label;
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

        public NowButton SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowButton SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowButton SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        public NowButton SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Draws at an explicit rect instead of reserving layout space.</summary>
        public NowButton SetPosition(NowRect rect) { _rect = rect; _hasRect = true; return this; }

        public NowButton SetPreset(string rectanglePreset) { _rectPreset = rectanglePreset; return this; }

        public NowButton SetTextPreset(string textPreset) { _textPreset = textPreset; return this; }

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

        public NowCheckbox SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowCheckbox SetPosition(NowRect rect) { _rect = rect; _hasRect = true; return this; }

        public NowCheckbox SetTextPreset(string textPreset) { _textPreset = textPreset; return this; }

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
    /// <code>if (NowControls.Radio("High", quality == 2).Draw()) quality = 2;</code>
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

        public NowRadio SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowRadio SetPosition(NowRect rect) { _rect = rect; _hasRect = true; return this; }

        public NowRadio SetTextPreset(string textPreset) { _textPreset = textPreset; return this; }

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

        public NowSlider SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowSlider SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowSlider SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowSlider SetPosition(NowRect rect) { _rect = rect; _hasRect = true; return this; }

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
