using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace NowUI
{
    [NowBuilder]
    public struct NowVectorField
    {
        readonly NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowVectorFieldSettings _settings;

        internal NowVectorField(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _settings = NowVectorFieldSettings.Default;
        }

        internal NowVectorField(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowVectorField SetOptions(NowLayoutOptions options) { _settings.options = options; return this; }

        /// <summary>Fixed row width; components stretch to share it.</summary>
        public NowVectorField SetWidth(float width)
        {
            _settings.options = _settings.options.SetWidth(width);
            _settings.stretchComponents = true;
            return this;
        }

        /// <summary>Stretches the row to fill available width, weighted against stretching siblings.</summary>
        public NowVectorField SetStretchWidth(float weight = 1f)
        {
            _settings.options = _settings.options.SetStretchWidth(weight);
            _settings.stretchComponents = true;
            return this;
        }

        /// <summary>Fixed width for each component field instead of stretching.</summary>
        public NowVectorField SetComponentWidth(float width)
        {
            _settings.componentWidth = Mathf.Max(1f, width);
            _settings.stretchComponents = false;
            return this;
        }

        /// <summary>Width reserved for each axis label; 0 hides the labels.</summary>
        public NowVectorField SetLabelWidth(float width) { _settings.labelWidth = Mathf.Max(0f, width); return this; }

        /// <summary>Gap between the label/field pairs on the row.</summary>
        public NowVectorField SetSpacing(float spacing) { _settings.spacing = Mathf.Max(0f, spacing); return this; }

        /// <summary>Clamps every component to this inclusive range.</summary>
        public NowVectorField SetRange(float min, float max)
        {
            if (max < min)
                (min, max) = (max, min);

            _settings.hasRange = true;
            _settings.min = min;
            _settings.max = max;
            return this;
        }

        /// <summary>Clamps every component to this inclusive integer range.</summary>
        public NowVectorField SetRange(int min, int max)
        {
            return SetRange((float)min, max);
        }

        /// <summary>Numeric display format for the component fields (e.g. <c>"0.###"</c>).</summary>
        public NowVectorField SetFormat(string format) { _settings.format = string.IsNullOrEmpty(format) ? null : format; return this; }

        /// <summary>Themed text style for the component fields.</summary>
        public NowVectorField SetTextStyle(NowTextStyle style) { _settings.textStyle = style; return this; }

        public bool Draw(ref Vector2 value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return NowVectorFieldUtility.DrawFloatComponents(id, _hasRect, _rect, _settings, ref value.x, ref value.y);
        }

        public bool Draw(ref Vector3 value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return NowVectorFieldUtility.DrawFloatComponents(id, _hasRect, _rect, _settings, ref value.x, ref value.y, ref value.z);
        }

        public bool Draw(ref Vector4 value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return NowVectorFieldUtility.DrawFloatComponents(id, _hasRect, _rect, _settings, ref value.x, ref value.y, ref value.z, ref value.w);
        }

        public bool Draw(ref Vector2Int value)
        {
            int x = value.x;
            int y = value.y;
            int id = NowControls.GetControlId(_id, _site);
            bool changed = NowVectorFieldUtility.DrawIntComponents(id, _hasRect, _rect, _settings, ref x, ref y);

            if (changed)
                value = new Vector2Int(x, y);

            return changed;
        }

        /// <summary>Draws X, Y, W, H fields for a rect on one row.</summary>
        public bool Draw(ref Rect value)
        {
            float x = value.x;
            float y = value.y;
            float w = value.width;
            float h = value.height;
            int id = NowControls.GetControlId(_id, _site);
            bool changed = NowVectorFieldUtility.DrawFloatComponents(
                id, _hasRect, _rect, _settings, NowVectorFieldUtility.XYWH, ref x, ref y, ref w, ref h);

            if (changed)
                value = new Rect(x, y, w, h);

            return changed;
        }

        /// <summary>Draws X, Y, W, H fields for an integer rect on one row.</summary>
        public bool Draw(ref RectInt value)
        {
            int x = value.x;
            int y = value.y;
            int w = value.width;
            int h = value.height;
            int id = NowControls.GetControlId(_id, _site);
            bool changed = NowVectorFieldUtility.DrawIntComponents(
                id, _hasRect, _rect, _settings, NowVectorFieldUtility.XYWH, ref x, ref y, ref w, ref h);

            if (changed)
                value = new RectInt(x, y, w, h);

            return changed;
        }

        public bool Draw(ref Vector3Int value)
        {
            int x = value.x;
            int y = value.y;
            int z = value.z;
            int id = NowControls.GetControlId(_id, _site);
            bool changed = NowVectorFieldUtility.DrawIntComponents(id, _hasRect, _rect, _settings, ref x, ref y, ref z);

            if (changed)
                value = new Vector3Int(x, y, z);

            return changed;
        }
    }

    [NowBuilder]
    public struct NowEnumDropdown<TEnum> where TEnum : struct, Enum
    {
        NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;

        internal NowEnumDropdown(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _navigation = default;
            _options = default;
        }

        internal NowEnumDropdown(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowEnumDropdown<TEnum> SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowEnumDropdown<TEnum> SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowEnumDropdown<TEnum> SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowEnumDropdown<TEnum> SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowEnumDropdown<TEnum> SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        public bool Draw(ref TEnum value)
        {
            int selected = NowEnumCache<TEnum>.IndexOf(value);
            var dropdown = _hasRect
                ? new NowDropdown(_rect, _id, NowEnumCache<TEnum>.names, _site)
                : new NowDropdown(_id, NowEnumCache<TEnum>.names, _site);

            dropdown = dropdown.SetOptions(_options).SetNavigation(_navigation);

            if (!dropdown.Draw(ref selected) || selected < 0 || selected >= NowEnumCache<TEnum>.values.Length)
                return false;

            value = NowEnumCache<TEnum>.values[selected];
            return true;
        }
    }

    [NowBuilder]
    public struct NowEnumFlags<TEnum> where TEnum : struct, Enum
    {
        NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowLayoutOptions _options;
        float _spacing;
        NowTextStyle _textStyle;

        internal NowEnumFlags(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _options = default;
            _spacing = 2f;
            _textStyle = NowTextStyle.Body;
        }

        internal NowEnumFlags(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowEnumFlags<TEnum> SetOptions(NowLayoutOptions options) { _options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowEnumFlags<TEnum> SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowEnumFlags<TEnum> SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        /// <summary>Vertical gap between the flag checkboxes.</summary>
        public NowEnumFlags<TEnum> SetSpacing(float spacing) { _spacing = Mathf.Max(0f, spacing); return this; }

        /// <summary>Themed text style for the flag labels.</summary>
        public NowEnumFlags<TEnum> SetTextStyle(NowTextStyle style) { _textStyle = style; return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowEnumFlags<TEnum> SetId(NowId id) { _id = id; return this; }

        public bool Draw(ref TEnum value)
        {
            int id = NowControls.GetControlId(_id, _site);
            ulong bits = NowEnumBits.ToUInt64(value);
            ulong original = bits;

            if (_hasRect)
            {
                using (NowLayout.Area(NowId.Resolved(NowInput.CombineId(id, 0x4e464172)), _rect, spacing: _spacing))
                    DrawFlags(id, ref bits);
            }
            else
            {
                using (NowLayout.Vertical(GroupOptions()))
                    DrawFlags(id, ref bits);
            }

            if (bits == original)
                return false;

            value = NowEnumBits.FromUInt64<TEnum>(bits);
            return true;
        }

        NowLayoutOptions GroupOptions()
        {
            var options = _options;

            if (!options.Has(NowLayoutOptions.Field.Spacing))
                options = options.SetSpacing(_spacing);

            return options;
        }

        void DrawFlags(int id, ref ulong bits)
        {
            var names = NowEnumCache<TEnum>.flagNames;
            var values = NowEnumCache<TEnum>.flagBits;

            for (int i = 0; i < values.Length; ++i)
            {
                ulong flag = values[i];
                bool on = (bits & flag) == flag;

                if (!NowLayout.Checkbox(names[i]).SetId(NowId.Resolved(NowInput.CombineId(id, i + 1))).SetTextStyle(_textStyle).Draw(ref on))
                    continue;

                if (on)
                    bits |= flag;
                else
                    bits &= ~flag;
            }
        }
    }

    [NowBuilder]
    public struct NowColorPicker
    {
        readonly NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowColorPickerSettings _settings;
        NowFocusNavigation _navigation;

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public NowColorPickerSettings settings;
            public int id;
            public int pendingId;
            public Color value;
            public NowRect fieldRect;
            public NowRect popupRect;
            public NowRect saturationValueRect;
            public NowRect hueRect;
            public NowRect alphaRect;
            public NowRect hexRect;
            public NowRect copyRect;
            public NowRect pasteRect;
            public NowRect channelsRect;
            public bool compactHexButtons;
            public string hexText;
            public Color hexTextValue;
            public byte hexTextAlpha;
        }

        struct PendingColor
        {
            public byte hasValue;
            public Color value;
        }

        struct LabelCache
        {
            public byte initialized;
            public byte showAlpha;
            public Color value;
            public string label;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(4);

        const int SaturationValueSeed = 0x43535631;
        const int HueSeed = 0x43485531;
        const int AlphaSeed = 0x43414c31;
        const int LabelSeed = 0x434c4231;
        const int HexInputSeed = 0x43484558;
        const int CopySeed = 0x43435059;
        const int PasteSeed = 0x43505354;
        const int RedSeed = 0x43524431;
        const int GreenSeed = 0x43475231;
        const int BlueSeed = 0x43424c31;
        const int AlphaChannelSeed = 0x43414331;
        const int ChannelValueHitSeed = 0x43564831;
        const int ColorPendingSeed = 0x43504e44;

        static Material _saturationValueMaterial;
        static Material _hueMaterial;
        static Material _alphaMaterial;
        static Material _saturationValueCanvasMaterial;
        static Material _hueCanvasMaterial;
        static Material _alphaCanvasMaterial;

        static readonly int s_modeProperty = Shader.PropertyToID("_Mode");

        internal NowColorPicker(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _settings = NowColorPickerSettings.Default;
            _navigation = default;
        }

        internal NowColorPicker(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowColorPicker SetOptions(NowLayoutOptions options) { _settings.options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowColorPicker SetWidth(float width) { _settings.options = _settings.options.SetWidth(width); return this; }

        /// <summary>Fixed field height in layout flow.</summary>
        public NowColorPicker SetHeight(float height)
        {
            _settings.options = _settings.options.SetHeight(height);
            _settings.fieldHeight = Mathf.Max(1f, height);
            return this;
        }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowColorPicker SetStretchWidth(float weight = 1f) { _settings.options = _settings.options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowColorPicker SetId(NowId id)
        {
            _settings.idOverride = id;
            return this;
        }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowColorPicker SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Shows or hides the alpha strip and alpha channel slider.</summary>
        public NowColorPicker SetShowAlpha(bool showAlpha) { _settings.showAlpha = showAlpha; return this; }

        /// <summary>Edge size of the popup's saturation/value square.</summary>
        public NowColorPicker SetPopupSize(float saturationValueSize)
        {
            _settings.saturationValueSize = Mathf.Max(64f, saturationValueSize);
            return this;
        }

        /// <summary>Total popup width.</summary>
        public NowColorPicker SetPopupWidth(float width)
        {
            _settings.popupWidth = Mathf.Max(140f, width);
            return this;
        }

        /// <summary>Keeps the popup inside the view bounds (on by default).</summary>
        public NowColorPicker SetFitToView(bool fitToView = true)
        {
            _settings.fitToView = fitToView;
            return this;
        }

        public bool Draw(ref Vector4 value)
        {
            var color = new Color(value.x, value.y, value.z, value.w);
            bool changed = Draw(ref color);

            if (changed)
                value = new Vector4(color.r, color.g, color.b, color.a);

            return changed;
        }

        public bool Draw(ref Color value)
        {
            var theme = NowTheme.themeAsset;
            int id = ResolveControlId();
            int pendingId = NowInput.CombineId(id, ColorPendingSeed);
            ref var pending = ref NowControlState.Get<PendingColor>(pendingId);
            bool changed = false;

            if (pending.hasValue != 0)
            {
                var pendingValue = Clamp01(pending.value);
                pending.hasValue = 0;

                if (!SameColor(value, pendingValue))
                {
                    value = pendingValue;
                    changed = true;
                }
            }

            var displayValue = Clamp01(value);

            var rect = NowControls.ReserveRect(_hasRect, _rect, _settings.options, MeasureField(_settings));
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
                open = !open;

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            string label = FieldLabel(id, displayValue, _settings.showAlpha);
            DrawField(theme, rect, displayValue, label, open, focused, interaction.held, hoverT);

            if (open)
            {
                NowControlState.RequestRepaint();
                DeferPopup(theme, id, pendingId, rect, displayValue, _settings);
            }

            return changed;
        }

        int ResolveControlId()
        {
            return _settings.idOverride.hasValue
                ? NowControls.GetControlId(_settings.idOverride, _site)
                : NowControls.GetControlId(_id, _site);
        }

        static Vector2 MeasureField(NowColorPickerSettings settings)
        {
            return new Vector2(168f, Mathf.Max(24f, settings.fieldHeight));
        }

        static void DrawField(
            NowThemeAsset theme,
            NowRect rect,
            Color value,
            string label,
            bool open,
            bool focused,
            bool held,
            float hoverT)
        {
            theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused || open));

            float padding = 4f;
            var inner = rect.Inset(padding);
            float swatchSize = Mathf.Min(inner.height, 22f);
            var swatch = new NowRect(inner.x, inner.y + (inner.height - swatchSize) * 0.5f, swatchSize, swatchSize);
            DrawChecker(swatch, 4f, theme);
            Now.Rectangle(swatch)
                .SetColor(value)
                .SetRadius(3f)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                .Draw();

            float labelX = swatch.xMax + 7f;
            float labelRight = rect.xMax - 20f;

            if (labelRight - labelX > 42f)
            {
                NowControls.DrawLeftLabel(
                    theme,
                    new NowRect(labelX, rect.y, labelRight - labelX, rect.height),
                    label,
                    NowTextStyle.Muted);
            }

            DropdownArrowDraw.Draw(
                theme,
                new NowRect(rect.xMax - 18f, rect.y, 14f, rect.height),
                open);
        }

        static void DeferPopup(
            NowThemeAsset theme,
            int id,
            int pendingId,
            NowRect field,
            Color value,
            NowColorPickerSettings settings)
        {
            var popupRect = CalculatePopupRect(theme, field, settings);

            if (settings.fitToView)
                popupRect = NowOverlay.FitToView(popupRect);

            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = theme;
            state.settings = settings;
            state.id = id;
            state.pendingId = pendingId;
            state.value = value;
            state.fieldRect = Now.TransformScreenRect(field);
            ApplyEditorLayout(state, popupRect, settings);

            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        internal static float CalculateEditorWidth(NowColorPickerSettings settings)
        {
            return Mathf.Max(
                settings.popupWidth,
                settings.popupPadding * 2f + settings.saturationValueSize + settings.popupGap + settings.stripWidth);
        }

        internal static float CalculateEditorHeight(NowColorPickerSettings settings)
        {
            float alphaHeight = settings.showAlpha ? settings.stripWidth : 0f;
            int channelCount = settings.showAlpha ? 4 : 3;
            float channelsHeight = channelCount * settings.channelSliderHeight + (channelCount - 1) * settings.channelSpacing;

            return settings.popupPadding * 2f +
                settings.saturationValueSize +
                (settings.showAlpha ? settings.popupGap + alphaHeight : 0f) +
                settings.controlGap +
                settings.hexRowHeight +
                settings.channelSpacing +
                channelsHeight;
        }

        internal static NowRect CalculatePopupRect(NowThemeAsset theme, NowRect field, NowColorPickerSettings settings)
        {
            return new NowRect(
                field.x,
                field.yMax + theme.controlStyles.dropdownPopupGap,
                CalculateEditorWidth(settings),
                CalculateEditorHeight(settings));
        }

        static void ApplyEditorLayout(PopupState state, NowRect editorRect, NowColorPickerSettings settings)
        {
            float padding = settings.popupPadding;
            float gap = settings.popupGap;
            float sv = settings.saturationValueSize;
            float strip = settings.stripWidth;
            float alphaHeight = settings.showAlpha ? strip : 0f;
            float contentWidth = editorRect.width - padding * 2f;
            var svRect = new NowRect(editorRect.x + padding, editorRect.y + padding, sv, sv);
            var hueRect = new NowRect(svRect.xMax + gap, svRect.y, strip, sv);
            var alphaRect = settings.showAlpha
                ? new NowRect(svRect.x, svRect.yMax + gap, sv, alphaHeight)
                : default;
            float controlGap = settings.controlGap;
            float hexRowHeight = settings.hexRowHeight;
            float hexButtonGap = settings.hexButtonGap;
            float channelSpacing = settings.channelSpacing;
            float controlsY = settings.showAlpha ? alphaRect.yMax + controlGap : svRect.yMax + controlGap;
            float minHexWidth = settings.showAlpha ? 92f : 76f;
            float fullButtonWidth = settings.hexButtonWidth;
            float compactButtonWidth = 34f;
            bool compactButtons = contentWidth < minHexWidth + fullButtonWidth * 2f + hexButtonGap * 2f;
            float hexButtonWidth = compactButtons ? compactButtonWidth : fullButtonWidth;
            var copyRect = new NowRect(editorRect.xMax - padding - hexButtonWidth * 2f - hexButtonGap, controlsY, hexButtonWidth, hexRowHeight);
            var pasteRect = new NowRect(copyRect.xMax + hexButtonGap, controlsY, hexButtonWidth, hexRowHeight);
            float hexWidth = Mathf.Max(1f, copyRect.x - editorRect.x - padding - hexButtonGap);

            if (hexWidth < minHexWidth)
            {
                hexButtonWidth = compactButtonWidth;
                compactButtons = true;
                copyRect = new NowRect(editorRect.xMax - padding - hexButtonWidth * 2f - hexButtonGap, controlsY, hexButtonWidth, hexRowHeight);
                pasteRect = new NowRect(copyRect.xMax + hexButtonGap, controlsY, hexButtonWidth, hexRowHeight);
                hexWidth = Mathf.Max(1f, copyRect.x - editorRect.x - padding - hexButtonGap);
            }

            var hexRect = new NowRect(editorRect.x + padding, controlsY, hexWidth, hexRowHeight);
            var channelsRect = new NowRect(editorRect.x + padding, controlsY + hexRowHeight + channelSpacing, contentWidth, CalculateChannelsHeight(settings));

            state.popupRect = editorRect;
            state.saturationValueRect = svRect;
            state.hueRect = hueRect;
            state.alphaRect = alphaRect;
            state.hexRect = hexRect;
            state.copyRect = copyRect;
            state.pasteRect = pasteRect;
            state.channelsRect = channelsRect;
            state.compactHexButtons = compactButtons;
        }

        static float CalculateChannelsHeight(NowColorPickerSettings settings)
        {
            int channelCount = settings.showAlpha ? 4 : 3;
            return channelCount * settings.channelSliderHeight + (channelCount - 1) * settings.channelSpacing;
        }

        internal static bool DrawInlineEditor(
            NowThemeAsset theme,
            int id,
            NowRect rect,
            NowColorPickerSettings settings,
            Color value,
            ref Color next)
        {
            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = theme;
            state.settings = settings;
            state.id = id;
            state.pendingId = 0;
            state.value = value;
            state.fieldRect = rect;
            ApplyEditorLayout(state, rect, settings);

            bool changed;
            Color edited;

            changed = DrawEditorContent(state, Clamp01(value), out edited);

            if (changed)
                next = edited;

            return changed;
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state))
                return;

            var theme = state.themeAsset;
            var value = Clamp01(state.value);

            bool changed;
            Color next;

            theme.controlRenderer.DrawPopupBackground(theme, state.popupRect, menu: false);
            changed = DrawEditorContent(state, value, out next);

            if (changed)
            {
                ref var pending = ref NowControlState.Get<PendingColor>(state.pendingId);
                pending.hasValue = 1;
                pending.value = Clamp01(next);
                NowControlState.RequestRepaint();
            }

            var snapshot = NowInput.current;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !state.fieldRect.Contains(snapshot.pointerPosition);

            if (pressedOutside || (snapshot.cancelPressed && !NowOverlay.HasNestedOverlay(state.id)))
                NowControlState.Get<bool>(state.id) = false;
        }

        static bool DrawEditorContent(PopupState state, Color value, out Color next)
        {
            var theme = state.themeAsset;
            var settings = state.settings;
            Color.RGBToHSV(value, out float hue, out float saturation, out float brightness);

            DrawSaturationValue(state.saturationValueRect, hue, settings.saturationValueSegments);
            DrawHueStrip(state.hueRect, settings.hueSegments);

            if (settings.showAlpha)
                DrawAlphaStrip(state.alphaRect, value, settings.alphaSegments, theme);

            bool changed = false;
            next = value;

            var svInteraction = NowInput.Interact(NowInput.CombineId(state.id, SaturationValueSeed), state.saturationValueRect);
            if (svInteraction.held)
            {
                saturation = Mathf.Clamp01((svInteraction.pointerPosition.x - state.saturationValueRect.x) / Mathf.Max(1f, state.saturationValueRect.width));
                brightness = 1f - Mathf.Clamp01((svInteraction.pointerPosition.y - state.saturationValueRect.y) / Mathf.Max(1f, state.saturationValueRect.height));
                next = Color.HSVToRGB(hue, saturation, brightness);
                next.a = value.a;
                changed = true;
            }

            var hueInteraction = NowInput.Interact(NowInput.CombineId(state.id, HueSeed), state.hueRect);
            if (hueInteraction.held)
            {
                hue = Mathf.Clamp01((hueInteraction.pointerPosition.y - state.hueRect.y) / Mathf.Max(1f, state.hueRect.height));
                next = Color.HSVToRGB(hue, saturation, brightness);
                next.a = value.a;
                changed = true;
            }

            if (settings.showAlpha)
            {
                var alphaInteraction = NowInput.Interact(NowInput.CombineId(state.id, AlphaSeed), state.alphaRect);
                if (alphaInteraction.held)
                {
                    next.a = Mathf.Clamp01((alphaInteraction.pointerPosition.x - state.alphaRect.x) / Mathf.Max(1f, state.alphaRect.width));
                    changed = true;
                }
            }

            DrawSaturationValueHandle(state.saturationValueRect, saturation, brightness);
            DrawHueHandle(state.hueRect, hue);

            if (settings.showAlpha)
                DrawAlphaHandle(state.alphaRect, next.a);

            changed |= DrawHexRow(theme, state, settings, value, ref next);
            changed |= DrawChannelRows(theme, state, settings, ref next);
            return changed;
        }

        static void DrawSaturationValue(NowRect rect, float hue, int segments)
        {
            if (DrawColorPickerShaderRect(rect, new Color(hue, 0f, 0f, 1f), ColorPickerShaderMode.SaturationValue))
                return;

            segments = Mathf.Clamp(segments, 4, 32);
            float cellW = rect.width / segments;
            float cellH = rect.height / segments;

            for (int y = 0; y < segments; ++y)
            {
                float v = 1f - (y + 0.5f) / segments;
                float y0 = rect.y + y * cellH;
                float y1 = y == segments - 1 ? rect.yMax : y0 + cellH;

                for (int x = 0; x < segments; ++x)
                {
                    float s = (x + 0.5f) / segments;
                    float x0 = rect.x + x * cellW;
                    float x1 = x == segments - 1 ? rect.xMax : x0 + cellW;
                    Now.Rectangle(new NowRect(x0, y0, x1 - x0, y1 - y0))
                        .SetColor(Color.HSVToRGB(hue, s, v))
                        .Draw();
                }
            }
        }

        static void DrawHueStrip(NowRect rect, int segments)
        {
            if (DrawColorPickerShaderRect(rect, Color.white, ColorPickerShaderMode.Hue))
                return;

            segments = Mathf.Clamp(segments, 6, 48);
            float cellH = rect.height / segments;

            for (int i = 0; i < segments; ++i)
            {
                float h = (i + 0.5f) / segments;
                float y0 = rect.y + i * cellH;
                float y1 = i == segments - 1 ? rect.yMax : y0 + cellH;
                Now.Rectangle(new NowRect(rect.x, y0, rect.width, y1 - y0))
                    .SetColor(Color.HSVToRGB(h, 1f, 1f))
                    .Draw();
            }
        }

        static void DrawAlphaStrip(NowRect rect, Color value, int segments, NowThemeAsset theme)
        {
            if (DrawColorPickerShaderRect(rect, value, ColorPickerShaderMode.Alpha))
                return;

            DrawChecker(rect, 5f, theme);
            segments = Mathf.Clamp(segments, 4, 32);
            float cellW = rect.width / segments;
            var color = value;

            for (int i = 0; i < segments; ++i)
            {
                color.a = (i + 0.5f) / segments;
                float x0 = rect.x + i * cellW;
                float x1 = i == segments - 1 ? rect.xMax : x0 + cellW;
                Now.Rectangle(new NowRect(x0, rect.y, x1 - x0, rect.height)).SetColor(color).Draw();
            }
        }

        enum ColorPickerShaderMode
        {
            SaturationValue,
            Hue,
            Alpha
        }

        static bool DrawColorPickerShaderRect(NowRect rect, Color data, ColorPickerShaderMode mode)
        {
            var material = GetColorPickerMaterial(mode, canvas: false);
            var canvasMaterial = GetColorPickerMaterial(mode, canvas: true);

            if (material == null)
                return false;

            Now.Rectangle(rect)
                .SetColor(data)
                .SetMaterial(material, canvasMaterial)
                .Draw();
            return true;
        }

        static Material GetColorPickerMaterial(ColorPickerShaderMode mode, bool canvas)
        {
            ref Material material = ref MaterialSlot(mode, canvas);

            if (material != null)
                return material;

            var shader = Shader.Find(canvas ? "NowUI/Color Picker UGUI" : "NowUI/Color Picker");

            if (shader == null)
                return null;

            material = new Material(shader)
            {
                name = canvas ? $"Now Color Picker {mode} UGUI" : $"Now Color Picker {mode}",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat(s_modeProperty, (float)mode);
            return material;
        }

        static ref Material MaterialSlot(ColorPickerShaderMode mode, bool canvas)
        {
            if (canvas)
            {
                switch (mode)
                {
                    case ColorPickerShaderMode.Hue:
                        return ref _hueCanvasMaterial;
                    case ColorPickerShaderMode.Alpha:
                        return ref _alphaCanvasMaterial;
                    default:
                        return ref _saturationValueCanvasMaterial;
                }
            }

            switch (mode)
            {
                case ColorPickerShaderMode.Hue:
                    return ref _hueMaterial;
                case ColorPickerShaderMode.Alpha:
                    return ref _alphaMaterial;
                default:
                    return ref _saturationValueMaterial;
            }
        }

        static bool DrawHexRow(
            NowThemeAsset theme,
            PopupState state,
            NowColorPickerSettings settings,
            Color value,
            ref Color next)
        {
            bool changed = false;
            int hexInputId = NowInput.CombineId(state.id, HexInputSeed);
            byte showAlpha = settings.showAlpha ? (byte)1 : (byte)0;

            if (state.hexText == null ||
                state.hexTextAlpha != showAlpha ||
                (!NowFocus.IsFocused(hexInputId) && !SameColor(state.hexTextValue, value)))
            {
                state.hexText = FormatColor(value, settings.showAlpha);
                state.hexTextValue = value;
                state.hexTextAlpha = showAlpha;
            }

            string hex = state.hexText;

            if (Now.TextField(state.hexRect, NowId.Resolved(hexInputId))
                    .SetPlaceholder(settings.showAlpha ? "#RRGGBBAA" : "#RRGGBB")
                    .Draw(ref hex))
            {
                state.hexText = hex;

                if (TryParseHexColor(hex, settings.showAlpha, value.a, out var typed))
                {
                    next = typed;
                    state.hexTextValue = typed;
                    changed = true;
                }
            }

            string copyLabel = state.compactHexButtons ? "C" : "Copy";
            string pasteLabel = state.compactHexButtons ? "P" : "Paste";

            if (Now.Button(state.copyRect, copyLabel).SetId(NowId.Resolved(NowInput.CombineId(state.id, CopySeed))).Draw())
                NowClipboard.Copy(FormatColor(value, settings.showAlpha));

            if (Now.Button(state.pasteRect, pasteLabel).SetId(NowId.Resolved(NowInput.CombineId(state.id, PasteSeed))).Draw() &&
                TryParseHexColor(NowClipboard.Paste(), settings.showAlpha, value.a, out var pasted))
            {
                next = pasted;
                state.hexText = FormatColor(pasted, settings.showAlpha);
                state.hexTextValue = pasted;
                state.hexTextAlpha = showAlpha;
                changed = true;
            }

            return changed;
        }

        static bool DrawChannelRows(
            NowThemeAsset theme,
            PopupState state,
            NowColorPickerSettings settings,
            ref Color next)
        {
            bool changed = false;
            float y = state.channelsRect.y;
            float sliderHeight = settings.channelSliderHeight;
            float spacing = settings.channelSpacing;
            changed |= DrawChannelRow(theme, state.id, new NowRect(state.channelsRect.x, y, state.channelsRect.width, sliderHeight), "R", RedSeed, ref next.r);
            y += sliderHeight + spacing;
            changed |= DrawChannelRow(theme, state.id, new NowRect(state.channelsRect.x, y, state.channelsRect.width, sliderHeight), "G", GreenSeed, ref next.g);
            y += sliderHeight + spacing;
            changed |= DrawChannelRow(theme, state.id, new NowRect(state.channelsRect.x, y, state.channelsRect.width, sliderHeight), "B", BlueSeed, ref next.b);

            if (settings.showAlpha)
            {
                y += sliderHeight + spacing;
                changed |= DrawChannelRow(theme, state.id, new NowRect(state.channelsRect.x, y, state.channelsRect.width, sliderHeight), "A", AlphaChannelSeed, ref next.a);
            }

            return changed;
        }

        static bool DrawChannelRow(NowThemeAsset theme, int id, NowRect rect, string label, int seed, ref float value)
        {
            float labelWidth = 16f;
            float valueWidth = 34f;
            float gap = 6f;
            var labelRect = new NowRect(rect.x, rect.y, labelWidth, rect.height);
            var valueRect = new NowRect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height);
            var sliderRect = new NowRect(labelRect.xMax + gap, rect.y, Mathf.Max(1f, valueRect.x - labelRect.xMax - gap * 2f), rect.height);

            NowControls.DrawLeftLabel(theme, labelRect, label, NowTextStyle.Muted);
            bool changed = Now.Slider(sliderRect, 0f, 1f).SetId(NowId.Resolved(NowInput.CombineId(id, seed))).Draw(ref value);
            var valueHitRect = new NowRect(sliderRect.xMax, rect.y, Mathf.Max(1f, rect.xMax - sliderRect.xMax), rect.height);
            var valueInteraction = NowInput.Interact(NowInput.CombineId(NowInput.CombineId(id, ChannelValueHitSeed), seed), valueHitRect);

            if (valueInteraction.held)
            {
                float previous = value;
                float knobSize = theme.controlStyles.sliderKnobSize;
                float t = sliderRect.width > knobSize
                    ? Mathf.Clamp01((valueInteraction.pointerPosition.x - sliderRect.x - knobSize * 0.5f) / (sliderRect.width - knobSize))
                    : Mathf.Clamp01((valueInteraction.pointerPosition.x - sliderRect.x) / Mathf.Max(1f, sliderRect.width));
                value = t;
                changed |= !Mathf.Approximately(previous, value);
            }

            NowControls.DrawLeftLabel(theme, valueRect, Mathf.RoundToInt(Mathf.Clamp01(value) * 255f).ToString(), NowTextStyle.Muted);
            return changed;
        }

        static void DrawChecker(NowRect rect, float cellSize, NowThemeAsset theme)
        {
            CheckerDraw.Draw(rect, cellSize, theme);
        }

        static void DrawSaturationValueHandle(NowRect rect, float saturation, float brightness)
        {
            float x = rect.x + Mathf.Clamp01(saturation) * rect.width;
            float y = rect.y + (1f - Mathf.Clamp01(brightness)) * rect.height;
            float size = 10f;
            DrawHandle(new NowRect(x - size * 0.5f, y - size * 0.5f, size, size), size * 0.5f);
        }

        static void DrawHueHandle(NowRect rect, float hue)
        {
            float y = rect.y + Mathf.Clamp01(hue) * rect.height;
            DrawHandle(new NowRect(rect.x - 3f, y - 2f, rect.width + 6f, 4f), 2f);
        }

        static void DrawAlphaHandle(NowRect rect, float alpha)
        {
            float x = rect.x + Mathf.Clamp01(alpha) * rect.width;
            DrawHandle(new NowRect(x - 2f, rect.y - 3f, 4f, rect.height + 6f), 2f);
        }

        static void DrawHandle(NowRect rect, float radius)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0f, 0f, 0f, 0.15f))
                .SetRadius(radius)
                .SetOutline(3f)
                .SetOutlineColor(new Color(0f, 0f, 0f, 0.55f))
                .Draw();
            Now.Rectangle(rect)
                .SetColor(Color.clear)
                .SetRadius(radius)
                .SetOutline(1.5f)
                .SetOutlineColor(Color.white)
                .Draw();
        }

        static Color Clamp01(Color color)
        {
            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = Mathf.Clamp01(color.a);
            return color;
        }

        static bool SameColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                Mathf.Approximately(a.g, b.g) &&
                Mathf.Approximately(a.b, b.b) &&
                Mathf.Approximately(a.a, b.a);
        }

        static string FormatColor(Color color, bool alpha)
        {
            return alpha
                ? "#" + ColorUtility.ToHtmlStringRGBA(color)
                : "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        static bool TryParseHexColor(string text, bool showAlpha, float fallbackAlpha, out Color color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string value = text.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(2);

            if (value.StartsWith("#", StringComparison.Ordinal))
                value = value.Substring(1);

            if (value.Length == 3 || value.Length == 4)
            {
                value = ExpandShortHex(value);
            }
            else if (value.Length != 6 && value.Length != 8)
            {
                return false;
            }

            bool hasAlpha = value.Length == 8;

            if (!ColorUtility.TryParseHtmlString("#" + value, out color))
                return false;

            if (!showAlpha || !hasAlpha)
                color.a = fallbackAlpha;

            color = Clamp01(color);
            return true;
        }

        static string ExpandShortHex(string value)
        {
            var chars = new char[value.Length * 2];

            for (int i = 0; i < value.Length; ++i)
            {
                chars[i * 2] = value[i];
                chars[i * 2 + 1] = value[i];
            }

            return new string(chars);
        }

        static string FieldLabel(int id, Color color, bool showAlpha)
        {
            ref var cache = ref NowControlState.Get<LabelCache>(NowInput.CombineId(id, LabelSeed));
            byte alpha = showAlpha ? (byte)1 : (byte)0;

            if (cache.initialized == 0 ||
                cache.showAlpha != alpha ||
                !SameColor(cache.value, color) ||
                string.IsNullOrEmpty(cache.label))
            {
                cache.initialized = 1;
                cache.showAlpha = alpha;
                cache.value = color;
                cache.label = FormatColor(color, showAlpha);
            }

            return cache.label;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            _saturationValueMaterial = null;
            _hueMaterial = null;
            _alphaMaterial = null;
            _saturationValueCanvasMaterial = null;
            _hueCanvasMaterial = null;
            _alphaCanvasMaterial = null;
        }
    }

    [NowBuilder]
    public struct NowGradientField
    {
        readonly NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowGradientFieldSettings _settings;
        NowFocusNavigation _navigation;

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public NowGradientFieldSettings settings;
            public int id;
            public int pendingId;
            public int selectedColorId;
            public int selectedAlphaId;
            public int selectedKindId;
            public int draggedColorId;
            public int draggedAlphaId;
            public NowRect fieldRect;
            public NowRect popupRect;
            public NowRect gradientRect;
            public NowRect alphaRect;
            public NowRect alphaLabelRect;
            public NowRect colorLabelRect;
            public NowRect keyEditorRect;
            public NowRect colorPickerRect;
            public NowColorPickerSettings colorPickerSettings;
            public GradientColorKey[] colorKeys;
            public GradientAlphaKey[] alphaKeys;
            public GradientMode mode;

            /// <summary>
            /// Re-clone guard: the key arrays refresh only when the source
            /// gradient instance changes or an edit was applied — Unity's key
            /// getters allocate fresh arrays on every access, so open frames
            /// must not read them unconditionally.
            /// </summary>
            public Gradient source;

            public byte keysInitialized;
        }

        struct PendingGradient
        {
            public byte hasValue;
            public GradientColorKey[] colorKeys;
            public GradientAlphaKey[] alphaKeys;
            public GradientMode mode;
        }

        /// <summary>
        /// Per-control mirror of the gradient's keys. Unity's key getters return
        /// fresh array copies on every access, so the field preview reads this
        /// mirror and refreshes it only when a change is detected, keeping the
        /// steady-state draw allocation-free.
        /// </summary>
        struct KeyMirror
        {
            public byte initialized;
            public Gradient source;
            public GradientColorKey[] colorKeys;
            public GradientAlphaKey[] alphaKeys;
            public GradientMode mode;
        }

        sealed class GradientTextureCache
        {
            public Texture2D texture;
            public Color32[] pixels;
            public int hash;
            public byte initialized;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(4);
        static readonly Dictionary<int, GradientTextureCache> _textureCaches = new Dictionary<int, GradientTextureCache>(8);
        static readonly Gradient PreviewGradient = new Gradient();

        const int TextureWidth = 1024;
        const int ColorMarkerSeed = 0x47524331;
        const int AlphaMarkerSeed = 0x47524131;
        const int StripSeed = 0x47525331;
        const int AlphaStripSeed = 0x47524153;
        const int ColorPickerSeed = 0x47524350;
        const int SelectedKindColor = 0;
        const int SelectedKindAlpha = 1;
        const int ColorTimeFieldSeed = 0x47524354;
        const int AlphaTimeFieldSeed = 0x47524154;
        const int AlphaValueSliderSeed = 0x47524156;
        const int AlphaValueFieldSeed = 0x47524146;
        const int DeleteColorSeed = 0x47524443;
        const int DeleteAlphaSeed = 0x47524441;
        const int DeleteShortcutSeed = 0x47524453;
        const int ColorPickerHexInputSeed = 0x43484558;
        const int KeyMirrorSeed = 0x47524b4d;
        const int GradientPendingSeed = 0x47504e44;
        const int SelectedKindSeed = 0x4753454b;
        const int SelectedColorSeed = 0x47534343;
        const int SelectedAlphaSeed = 0x47534341;
        const int DraggedColorSeed = 0x47444743;
        const int DraggedAlphaSeed = 0x47444741;
        const string DeleteGlyph = "🗑";

        internal NowGradientField(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _settings = NowGradientFieldSettings.Default;
            _navigation = default;
        }

        internal NowGradientField(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowGradientField SetOptions(NowLayoutOptions options) { _settings.options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowGradientField SetWidth(float width) { _settings.options = _settings.options.SetWidth(width); return this; }

        /// <summary>Fixed field height in layout flow.</summary>
        public NowGradientField SetHeight(float height)
        {
            _settings.options = _settings.options.SetHeight(height);
            _settings.fieldHeight = Mathf.Max(1f, height);
            return this;
        }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowGradientField SetStretchWidth(float weight = 1f) { _settings.options = _settings.options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowGradientField SetId(NowId id)
        {
            _settings.idOverride = id;
            return this;
        }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowGradientField SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Total popup width.</summary>
        public NowGradientField SetPopupWidth(float width)
        {
            _settings.popupWidth = Mathf.Max(180f, width);
            return this;
        }

        /// <summary>Keeps the popup inside the view bounds (on by default).</summary>
        public NowGradientField SetFitToView(bool fitToView = true)
        {
            _settings.fitToView = fitToView;
            return this;
        }

        public bool Draw(ref Gradient value)
        {
            if (value == null)
                value = DefaultGradient();

            int id = ResolveControlId();
            int pendingId = NowInput.CombineId(id, GradientPendingSeed);
            ref var pending = ref NowControlState.Get<PendingGradient>(pendingId);
            bool changed = false;

            if (pending.hasValue != 0)
            {
                pending.hasValue = 0;
                ApplyGradient(value, pending.colorKeys, pending.alphaKeys, pending.mode);
                changed = true;
            }

            ref var mirror = ref NowControlState.Get<KeyMirror>(NowInput.CombineId(id, KeyMirrorSeed));

            if (mirror.initialized == 0 || changed ||
                !ReferenceEquals(mirror.source, value) || mirror.mode != value.mode)
            {
                mirror.initialized = 1;
                mirror.source = value;
                mirror.colorKeys = value.colorKeys;
                mirror.alphaKeys = value.alphaKeys;
                mirror.mode = value.mode;
                Normalize(ref mirror.colorKeys, ref mirror.alphaKeys);
            }

            var theme = NowTheme.themeAsset;
            var rect = NowControls.ReserveRect(_hasRect, _rect, _settings.options, MeasureField(_settings));
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
                open = !open;

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            DrawField(id, theme, rect, mirror.colorKeys, mirror.alphaKeys, mirror.mode, open, focused, interaction.held, hoverT);

            if (open)
            {
                NowControlState.RequestRepaint();
                DeferPopup(theme, id, pendingId, rect, value, _settings, changed);
            }

            return changed;
        }

        int ResolveControlId()
        {
            return _settings.idOverride.hasValue
                ? NowControls.GetControlId(_settings.idOverride, _site)
                : NowControls.GetControlId(_id, _site);
        }

        static Vector2 MeasureField(NowGradientFieldSettings settings)
        {
            return new Vector2(190f, Mathf.Max(24f, settings.fieldHeight));
        }

        static void DrawField(
            int id,
            NowThemeAsset theme,
            NowRect rect,
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys,
            GradientMode mode,
            bool open,
            bool focused,
            bool held,
            float hoverT)
        {
            theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused || open));

            var strip = rect.Inset(5f, 7f, 22f, 7f);
            DrawGradientStrip(strip, NowInput.CombineId(id, StripSeed), colorKeys, alphaKeys, mode, 24, theme);
            DropdownArrowDraw.Draw(
                theme,
                new NowRect(rect.xMax - 18f, rect.y, 14f, rect.height),
                open);
        }

        static void DeferPopup(
            NowThemeAsset theme,
            int id,
            int pendingId,
            NowRect field,
            Gradient value,
            NowGradientFieldSettings settings,
            bool changed)
        {
            float padding = settings.popupPadding;
            var colorPickerSettings = NowColorPickerSettings.Default;
            colorPickerSettings.showAlpha = false;
            float colorPickerWidth = NowColorPicker.CalculateEditorWidth(colorPickerSettings);
            float popupWidth = Mathf.Max(settings.popupWidth, field.width, colorPickerWidth + padding * 2f);
            colorPickerSettings.popupWidth = popupWidth - padding * 2f;
            int selectedKindId = NowInput.CombineId(id, SelectedKindSeed);
            float stripHeight = settings.stripHeight;
            float markerLaneHeight = settings.alphaStripHeight;
            float keyEditorHeight = settings.keyEditorHeight;
            float popupGap = settings.popupGap;
            float labelWidth = settings.rampLabelWidth;
            float pickerHeight = Mathf.Max(settings.colorPickerHeight, NowColorPicker.CalculateEditorHeight(colorPickerSettings));
            float rampBlockHeight = markerLaneHeight + stripHeight + markerLaneHeight;
            float popupHeight = padding * 2f + rampBlockHeight + popupGap + keyEditorHeight + popupGap + pickerHeight;

            var popupRect = new NowRect(field.x, field.yMax + theme.controlStyles.dropdownPopupGap, popupWidth, popupHeight);

            if (settings.fitToView)
                popupRect = NowOverlay.FitToView(popupRect);

            float contentX = popupRect.x + padding;
            float contentWidth = popupWidth - padding * 2f;
            float trackX = contentX + labelWidth + popupGap;
            float trackWidth = Mathf.Max(80f, contentWidth - labelWidth - popupGap);
            var alphaLabelRect = new NowRect(contentX, popupRect.y + padding, labelWidth, markerLaneHeight);
            var alphaRect = new NowRect(trackX, popupRect.y + padding, trackWidth, markerLaneHeight);
            var gradientRect = new NowRect(alphaRect.x, alphaRect.yMax, alphaRect.width, stripHeight);
            var colorLabelRect = new NowRect(contentX, gradientRect.yMax, labelWidth, markerLaneHeight);
            var keyEditorRect = new NowRect(contentX, gradientRect.yMax + markerLaneHeight + popupGap, contentWidth, keyEditorHeight);
            var colorPickerRect = new NowRect(contentX, keyEditorRect.yMax + popupGap, contentWidth, pickerHeight);

            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = theme;
            state.settings = settings;
            state.id = id;
            state.pendingId = pendingId;
            state.selectedColorId = NowInput.CombineId(id, SelectedColorSeed);
            state.selectedAlphaId = NowInput.CombineId(id, SelectedAlphaSeed);
            state.selectedKindId = selectedKindId;
            state.draggedColorId = NowInput.CombineId(id, DraggedColorSeed);
            state.draggedAlphaId = NowInput.CombineId(id, DraggedAlphaSeed);
            state.fieldRect = Now.TransformScreenRect(field);
            state.popupRect = popupRect;
            state.gradientRect = gradientRect;
            state.alphaRect = alphaRect;
            state.alphaLabelRect = alphaLabelRect;
            state.colorLabelRect = colorLabelRect;
            state.keyEditorRect = keyEditorRect;
            state.colorPickerRect = colorPickerRect;
            state.colorPickerSettings = colorPickerSettings;

            if (state.keysInitialized == 0 || changed ||
                !ReferenceEquals(state.source, value) || state.mode != value.mode)
            {
                state.keysInitialized = 1;
                state.source = value;
                state.colorKeys = Clone(value.colorKeys);
                state.alphaKeys = Clone(value.alphaKeys);
                state.mode = value.mode;
            }

            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state))
                return;

            var theme = state.themeAsset;
            var settings = state.settings;
            Normalize(ref state.colorKeys, ref state.alphaKeys);
            theme.controlRenderer.DrawPopupBackground(theme, state.popupRect, menu: false);
            DrawAlphaStrip(
                state.alphaRect,
                NowInput.CombineId(state.id, AlphaStripSeed),
                state.alphaKeys,
                settings.previewSegments,
                theme);
            DrawGradientStrip(
                state.gradientRect,
                NowInput.CombineId(state.id, StripSeed),
                state.colorKeys,
                state.alphaKeys,
                state.mode,
                settings.previewSegments,
                theme);
            int selectedColor = Mathf.Clamp(NowControlState.Get<int>(state.selectedColorId), 0, state.colorKeys.Length - 1);
            int selectedAlpha = Mathf.Clamp(NowControlState.Get<int>(state.selectedAlphaId), 0, state.alphaKeys.Length - 1);
            int selectedKind = Mathf.Clamp(NowControlState.Get<int>(state.selectedKindId), SelectedKindColor, SelectedKindAlpha);
            bool changed = false;
            ref int draggedColor = ref NowControlState.Get<int>(state.draggedColorId);
            ref int draggedAlpha = ref NowControlState.Get<int>(state.draggedAlphaId);

                DrawRampLabels(state, selectedKind);

                for (int i = 0; i < state.colorKeys.Length; ++i)
                {
                    var marker = ColorMarkerRect(state.gradientRect, state.colorKeys[i].time);
                    var interaction = NowInput.Interact(NowInput.CombineId(NowInput.CombineId(state.id, ColorMarkerSeed), i + 1), marker);

                    if (interaction.pressed)
                    {
                        selectedColor = i;
                        selectedKind = SelectedKindColor;
                        draggedColor = i + 1;
                        draggedAlpha = 0;
                        NowControlState.Get<int>(state.selectedColorId) = selectedColor;
                        NowControlState.Get<int>(state.selectedKindId) = selectedKind;
                    }

                    if (interaction.dragging)
                    {
                        selectedKind = SelectedKindColor;
                        int dragIndex = Mathf.Clamp(draggedColor > 0 ? draggedColor - 1 : i, 0, state.colorKeys.Length - 1);
                        float movedTime = PointerTime(state.gradientRect, interaction.pointerPosition);
                        var key = state.colorKeys[dragIndex];
                        key.time = movedTime;
                        state.colorKeys[dragIndex] = key;
                        SortColorKeys(state.colorKeys);
                        selectedColor = IndexOfNearestColorKey(state.colorKeys, movedTime);
                        draggedColor = selectedColor + 1;
                        changed = true;
                    }

                    if (interaction.dragEnded)
                        draggedColor = 0;
                }

                var stripInteraction = NowInput.Interact(NowInput.CombineId(state.id, StripSeed), state.gradientRect);
                if (NowControlState.DetectDoubleClick(stripInteraction.id, stripInteraction.clicked))
                {
                    float time = PointerTime(state.gradientRect, stripInteraction.pointerPosition);
                    InsertColorKey(ref state.colorKeys, time, Evaluate(state.colorKeys, state.alphaKeys, state.mode, time));
                    selectedColor = IndexOfNearestColorKey(state.colorKeys, time);
                    selectedKind = SelectedKindColor;
                    changed = true;
                }

                for (int i = 0; i < state.alphaKeys.Length; ++i)
                {
                    var marker = AlphaMarkerRect(state.alphaRect, state.alphaKeys[i].time);
                    var interaction = NowInput.Interact(NowInput.CombineId(NowInput.CombineId(state.id, AlphaMarkerSeed), i + 1), marker);

                    if (interaction.pressed)
                    {
                        selectedAlpha = i;
                        selectedKind = SelectedKindAlpha;
                        draggedAlpha = i + 1;
                        draggedColor = 0;
                        NowControlState.Get<int>(state.selectedAlphaId) = selectedAlpha;
                        NowControlState.Get<int>(state.selectedKindId) = selectedKind;
                    }

                    if (interaction.dragging)
                    {
                        selectedKind = SelectedKindAlpha;
                        int dragIndex = Mathf.Clamp(draggedAlpha > 0 ? draggedAlpha - 1 : i, 0, state.alphaKeys.Length - 1);
                        float movedTime = PointerTime(state.alphaRect, interaction.pointerPosition);
                        var key = state.alphaKeys[dragIndex];
                        key.time = movedTime;
                        state.alphaKeys[dragIndex] = key;
                        SortAlphaKeys(state.alphaKeys);
                        selectedAlpha = IndexOfNearestAlphaKey(state.alphaKeys, movedTime);
                        draggedAlpha = selectedAlpha + 1;
                        changed = true;
                    }

                    if (interaction.dragEnded)
                        draggedAlpha = 0;
                }

                var alphaStripInteraction = NowInput.Interact(NowInput.CombineId(state.id, AlphaStripSeed), state.alphaRect);
                if (NowControlState.DetectDoubleClick(alphaStripInteraction.id, alphaStripInteraction.clicked))
                {
                    float time = PointerTime(state.alphaRect, alphaStripInteraction.pointerPosition);
                    InsertAlphaKey(ref state.alphaKeys, time, EvaluateAlpha(state.alphaKeys, time));
                    selectedAlpha = IndexOfNearestAlphaKey(state.alphaKeys, time);
                    selectedKind = SelectedKindAlpha;
                    changed = true;
                }

                selectedColor = Mathf.Clamp(selectedColor, 0, state.colorKeys.Length - 1);
                selectedAlpha = Mathf.Clamp(selectedAlpha, 0, state.alphaKeys.Length - 1);
                changed |= DrawKeyInspector(state, ref selectedColor, ref selectedAlpha, ref selectedKind);
                changed |= HandleDeleteShortcut(state, ref selectedColor, ref selectedAlpha, selectedKind);
                selectedColor = Mathf.Clamp(selectedColor, 0, state.colorKeys.Length - 1);
                selectedAlpha = Mathf.Clamp(selectedAlpha, 0, state.alphaKeys.Length - 1);

                if (selectedKind == SelectedKindColor && selectedColor >= 0 && selectedColor < state.colorKeys.Length)
                {
                    Color color = state.colorKeys[selectedColor].color;

                    if (NowColorPicker.DrawInlineEditor(
                        theme,
                        NowInput.CombineId(state.id, ColorPickerSeed),
                        state.colorPickerRect,
                        state.colorPickerSettings,
                        state.colorKeys[selectedColor].color,
                        ref color))
                    {
                        state.colorKeys[selectedColor].color = color;
                        changed = true;
                    }
                }

                selectedColor = Mathf.Clamp(selectedColor, 0, state.colorKeys.Length - 1);
                selectedAlpha = Mathf.Clamp(selectedAlpha, 0, state.alphaKeys.Length - 1);

                NowControlState.Get<int>(state.selectedColorId) = selectedColor;
                NowControlState.Get<int>(state.selectedAlphaId) = selectedAlpha;
                NowControlState.Get<int>(state.selectedKindId) = selectedKind;

                for (int i = 0; i < state.alphaKeys.Length; ++i)
                {
                    var color = new Color(1f, 1f, 1f, state.alphaKeys[i].alpha);
                    DrawMarker(AlphaMarkerRect(state.alphaRect, state.alphaKeys[i].time), color, selectedKind == SelectedKindAlpha && i == selectedAlpha, theme);
                }

                for (int i = 0; i < state.colorKeys.Length; ++i)
                    DrawMarker(ColorMarkerRect(state.gradientRect, state.colorKeys[i].time), state.colorKeys[i].color, selectedKind == SelectedKindColor && i == selectedColor, theme);

                if (changed)
                    SetPending(state.pendingId, state.colorKeys, state.alphaKeys, state.mode);

            var snapshot = NowInput.current;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !state.fieldRect.Contains(snapshot.pointerPosition);

            if (pressedOutside || (snapshot.cancelPressed && !NowOverlay.HasNestedOverlay(state.id)))
                NowControlState.Get<bool>(state.id) = false;
        }

        static bool DrawKeyInspector(PopupState state, ref int selectedColor, ref int selectedAlpha, ref int selectedKind)
        {
            return selectedKind == SelectedKindAlpha
                ? DrawAlphaKeyInspector(state, ref selectedAlpha)
                : DrawColorKeyInspector(state, ref selectedColor);
        }

        static void DrawRampLabels(PopupState state, int selectedKind)
        {
            var theme = state.themeAsset;
            DrawRampLabel(theme, state.alphaLabelRect, "Alpha", selectedKind == SelectedKindAlpha);
            DrawRampLabel(theme, state.colorLabelRect, "Color", selectedKind == SelectedKindColor);
        }

        static void DrawRampLabel(NowThemeAsset theme, NowRect rect, string label, bool selected)
        {
            var background = theme.GetColor(NowColorToken.SurfaceMuted);

            if (selected)
                background = Color.Lerp(background, theme.GetColor(NowColorToken.Accent), 0.16f);

            Now.Rectangle(rect)
                .SetColor(background)
                .SetRadius(3f)
                .SetOutline(selected ? 1f : 0f)
                .SetOutlineColor(theme.GetColor(NowColorToken.Accent))
                .Draw();

            NowControls.DrawCenteredLabel(
                theme,
                rect,
                label,
                selected ? NowTextStyle.Body : NowTextStyle.Muted,
                rect);
        }

        static bool HandleDeleteShortcut(PopupState state, ref int selectedColor, ref int selectedAlpha, int selectedKind)
        {
            if (IsGradientTextInputFocused(state, selectedKind) ||
                !DeleteKeyPressed(NowInput.CombineId(state.id, DeleteShortcutSeed)))
            {
                return false;
            }

            if (selectedKind == SelectedKindAlpha)
            {
                if (state.alphaKeys == null || state.alphaKeys.Length <= 2)
                    return false;

                DeleteAlphaKey(ref state.alphaKeys, selectedAlpha);
                selectedAlpha = Mathf.Clamp(selectedAlpha, 0, state.alphaKeys.Length - 1);
                return true;
            }

            if (state.colorKeys == null || state.colorKeys.Length <= 1)
                return false;

            DeleteColorKey(ref state.colorKeys, selectedColor);
            selectedColor = Mathf.Clamp(selectedColor, 0, state.colorKeys.Length - 1);
            return true;
        }

        static bool IsGradientTextInputFocused(PopupState state, int selectedKind)
        {
            int focused = NowFocus.focusedId;

            if (focused == 0 || !NowFocus.IsFocused(focused))
                return false;

            if (selectedKind == SelectedKindAlpha)
                return focused == NowInput.CombineId(state.id, AlphaValueFieldSeed) ||
                    focused == NowInput.CombineId(state.id, AlphaTimeFieldSeed);

            int colorPickerId = NowInput.CombineId(state.id, ColorPickerSeed);
            return focused == NowInput.CombineId(state.id, ColorTimeFieldSeed) ||
                focused == NowInput.CombineId(colorPickerId, ColorPickerHexInputSeed);
        }

        static bool DrawColorKeyInspector(PopupState state, ref int selected)
        {
            if (state.colorKeys == null || state.colorKeys.Length == 0)
                return false;

            var theme = state.themeAsset;
            var rect = state.keyEditorRect;
            DrawKeyEditorBackground(theme, rect);

            float rowHeight = Mathf.Min(24f, rect.height);
            float y = rect.y + (rect.height - rowHeight) * 0.5f;
            float gap = 6f;
            float majorGap = 10f;
            float outerPadding = 8f;
            float contentWidth = Mathf.Max(1f, rect.width - outerPadding * 2f);
            float deleteWidth = Mathf.Min(
                state.settings.keyEditorButtonWidth,
                Mathf.Max(44f, contentWidth * 0.25f));
            float labelWidth = contentWidth < 190f
                ? 26f
                : state.settings.keyEditorLabelWidth;
            float fieldWidth = Mathf.Min(
                state.settings.keyEditorFieldWidth,
                Mathf.Max(42f, contentWidth - labelWidth - gap - majorGap - deleteWidth));

            selected = Mathf.Clamp(selected, 0, state.colorKeys.Length - 1);
            var key = state.colorKeys[selected];
            float time = key.time;
            bool changed = false;

            var labelRect = new NowRect(rect.x + outerPadding, y, labelWidth, rowHeight);
            var timeRect = new NowRect(labelRect.xMax + gap, y, fieldWidth, rowHeight);
            var deleteRect = new NowRect(rect.xMax - outerPadding - deleteWidth, y, deleteWidth, rowHeight);
            NowControls.DrawLeftLabel(theme, labelRect, contentWidth < 190f ? "Loc" : "Location", NowTextStyle.Muted);

            if (Now.FloatField(timeRect, NowId.Resolved(NowInput.CombineId(state.id, ColorTimeFieldSeed)))
                    .SetRange(0f, 1f)
                    .SetFormat("0.###")
                    .Draw(ref time))
            {
                key.time = Mathf.Clamp01(time);
                state.colorKeys[selected] = key;
                SortColorKeys(state.colorKeys);
                selected = IndexOfNearestColorKey(state.colorKeys, key.time);
                changed = true;
            }

            if (state.colorKeys.Length > 1)
            {
                if (Now.Button(deleteRect, DeleteGlyph)
                        .SetId(NowId.Resolved(NowInput.CombineId(state.id, DeleteColorSeed)))
                        .SetStyle(NowRectangleStyle.Outline)
                        .Draw())
                {
                    DeleteColorKey(ref state.colorKeys, selected);
                    selected = Mathf.Clamp(selected, 0, state.colorKeys.Length - 1);
                    changed = true;
                }
            }
            else
            {
                DrawDisabledDelete(theme, deleteRect);
            }

            return changed;
        }

        static bool DrawAlphaKeyInspector(PopupState state, ref int selected)
        {
            if (state.alphaKeys == null || state.alphaKeys.Length == 0)
                return false;

            var theme = state.themeAsset;
            var rect = state.keyEditorRect;
            DrawKeyEditorBackground(theme, rect);

            float rowHeight = Mathf.Min(24f, rect.height);
            float gap = 6f;
            float outerPadding = 8f;
            bool hasLocationRow = rect.height >= 50f;
            float rowsHeight = hasLocationRow ? rowHeight * 2f + gap : rowHeight;
            float y = rect.y + Mathf.Max(0f, (rect.height - rowsHeight) * 0.5f);
            float locationY = hasLocationRow ? y + rowHeight + gap : y;
            float majorGap = 10f;
            float contentWidth = Mathf.Max(1f, rect.width - outerPadding * 2f);
            float labelWidth = contentWidth < 210f ? 26f : state.settings.keyEditorLabelWidth;
            float deleteWidth = Mathf.Min(
                state.settings.keyEditorButtonWidth,
                Mathf.Max(44f, contentWidth * 0.2f));
            float valueWidth = Mathf.Min(
                state.settings.keyEditorFieldWidth,
                Mathf.Max(42f, contentWidth * 0.24f));

            selected = Mathf.Clamp(selected, 0, state.alphaKeys.Length - 1);
            var key = state.alphaKeys[selected];
            float alpha = key.alpha;
            float time = key.time;
            bool changed = false;

            var alphaLabelRect = new NowRect(rect.x + outerPadding, y, labelWidth, rowHeight);
            var alphaValueRect = new NowRect(rect.xMax - outerPadding - valueWidth, y, valueWidth, rowHeight);
            var alphaSliderRect = new NowRect(
                alphaLabelRect.xMax + gap,
                y,
                Mathf.Max(1f, alphaValueRect.x - alphaLabelRect.xMax - gap * 2f),
                rowHeight);

            NowControls.DrawLeftLabel(theme, alphaLabelRect, contentWidth < 210f ? "A" : "Alpha", NowTextStyle.Muted);

            if (Now.Slider(alphaSliderRect, 0f, 1f)
                    .SetId(NowId.Resolved(NowInput.CombineId(state.id, AlphaValueSliderSeed)))
                    .Draw(ref alpha))
                changed = true;

            if (Now.FloatField(alphaValueRect, NowId.Resolved(NowInput.CombineId(state.id, AlphaValueFieldSeed)))
                    .SetRange(0f, 1f)
                    .SetFormat("0.###")
                    .Draw(ref alpha))
                changed = true;

            var deleteRect = new NowRect(rect.xMax - outerPadding - deleteWidth, hasLocationRow ? locationY : y, deleteWidth, rowHeight);

            if (hasLocationRow)
            {
                var locationLabelRect = new NowRect(rect.x + outerPadding, locationY, labelWidth, rowHeight);
                var timeRect = new NowRect(
                    locationLabelRect.xMax + gap,
                    locationY,
                    Mathf.Min(
                        valueWidth,
                        Mathf.Max(42f, deleteRect.x - locationLabelRect.xMax - gap - majorGap)),
                    rowHeight);

                NowControls.DrawLeftLabel(theme, locationLabelRect, contentWidth < 210f ? "Loc" : "Location", NowTextStyle.Muted);

                if (Now.FloatField(timeRect, NowId.Resolved(NowInput.CombineId(state.id, AlphaTimeFieldSeed)))
                        .SetRange(0f, 1f)
                        .SetFormat("0.###")
                        .Draw(ref time))
                    changed = true;
            }

            if (changed)
            {
                key.alpha = Mathf.Clamp01(alpha);
                key.time = Mathf.Clamp01(time);
                state.alphaKeys[selected] = key;
                SortAlphaKeys(state.alphaKeys);
                selected = IndexOfNearestAlphaKey(state.alphaKeys, key.time);
            }

            if (state.alphaKeys.Length > 2)
            {
                if (Now.Button(deleteRect, DeleteGlyph)
                        .SetId(NowId.Resolved(NowInput.CombineId(state.id, DeleteAlphaSeed)))
                        .SetStyle(NowRectangleStyle.Outline)
                        .Draw())
                {
                    DeleteAlphaKey(ref state.alphaKeys, selected);
                    selected = Mathf.Clamp(selected, 0, state.alphaKeys.Length - 1);
                    changed = true;
                }
            }
            else
            {
                DrawDisabledDelete(theme, deleteRect);
            }

            return changed;
        }

        static void DrawKeyEditorBackground(NowThemeAsset theme, NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(theme.GetColor(NowColorToken.SurfaceMuted))
                .SetRadius(4f)
                .Draw();
        }

        static void DrawDisabledDelete(NowThemeAsset theme, NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(Color.clear)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                .SetRadius(4f)
                .Draw();
            NowControls.DrawLeftLabel(
                theme,
                new NowRect(rect.x + 6f, rect.y, Mathf.Max(1f, rect.width - 12f), rect.height),
                DeleteGlyph,
                NowTextStyle.Muted);
        }

        static bool DeleteKeyPressed(int id)
        {
            if (NowInput.isPassive)
                return false;

            var frame = NowTextInput.current;
            return NowControlState.Repeat(id, frame.deleteHeld);
        }

        static void DrawGradientStrip(
            NowRect rect,
            int textureId,
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys,
            GradientMode mode,
            int segments,
            NowThemeAsset theme)
        {
            DrawChecker(rect, 6f, theme);

            if (DrawGradientTexture(rect, textureId, colorKeys, alphaKeys, mode, alphaOnly: false))
            {
                DrawStripOutline(rect, theme);
                return;
            }

            segments = Mathf.Clamp(segments, 2, 64);
            float cellW = rect.width / segments;

            for (int i = 0; i < segments; ++i)
            {
                float t = segments <= 1 ? 0f : (i + 0.5f) / segments;
                float x0 = rect.x + i * cellW;
                float x1 = i == segments - 1 ? rect.xMax : x0 + cellW;
                Now.Rectangle(new NowRect(x0, rect.y, x1 - x0, rect.height))
                    .SetColor(Evaluate(colorKeys, alphaKeys, mode, t))
                    .Draw();
            }

            DrawStripOutline(rect, theme);
        }

        static void DrawAlphaStrip(NowRect rect, int textureId, GradientAlphaKey[] alphaKeys, int segments, NowThemeAsset theme)
        {
            DrawChecker(rect, 6f, theme);

            if (DrawGradientTexture(rect, textureId, null, alphaKeys, GradientMode.Blend, alphaOnly: true))
            {
                DrawStripOutline(rect, theme);
                return;
            }

            segments = Mathf.Clamp(segments, 2, 64);
            float cellW = rect.width / segments;

            for (int i = 0; i < segments; ++i)
            {
                float t = segments <= 1 ? 0f : (i + 0.5f) / segments;
                float x0 = rect.x + i * cellW;
                float x1 = i == segments - 1 ? rect.xMax : x0 + cellW;
                float alpha = EvaluateAlpha(alphaKeys, t);
                Now.Rectangle(new NowRect(x0, rect.y, x1 - x0, rect.height))
                    .SetColor(new Color(1f, 1f, 1f, alpha))
                    .Draw();
            }

            DrawStripOutline(rect, theme);
        }

        static void DrawStripOutline(NowRect rect, NowThemeAsset theme)
        {
            Now.Rectangle(rect)
                .SetColor(Color.clear)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                .SetRadius(3f)
                .Draw();
        }

        static bool DrawGradientTexture(
            NowRect rect,
            int textureId,
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys,
            GradientMode mode,
            bool alphaOnly)
        {
            var texture = GetGradientTexture(textureId, colorKeys, alphaKeys, mode, alphaOnly);

            if (texture == null)
                return false;

            Now.Rectangle(rect)
                .SetTexture(texture)
                .SetRadius(3f)
                .Draw();
            return true;
        }

        static Texture2D GetGradientTexture(
            int textureId,
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys,
            GradientMode mode,
            bool alphaOnly)
        {
            if (!_textureCaches.TryGetValue(textureId, out var cache))
            {
                cache = new GradientTextureCache();
                _textureCaches[textureId] = cache;
            }

            EnsureGradientTexture(cache, alphaOnly);

            if (cache.texture == null || cache.pixels == null)
                return null;

            if (alphaOnly)
            {
                EnsureAlphaKeys(ref alphaKeys);
                SortAlphaKeys(alphaKeys);
            }
            else
            {
                Normalize(ref colorKeys, ref alphaKeys);
            }

            int hash = HashGradientTexture(colorKeys, alphaKeys, mode, alphaOnly);

            if (cache.initialized != 0 && cache.hash == hash)
                return cache.texture;

            FillGradientTexture(cache, colorKeys, alphaKeys, mode, alphaOnly);
            cache.hash = hash;
            cache.initialized = 1;
            return cache.texture;
        }

        static void EnsureGradientTexture(GradientTextureCache cache, bool alphaOnly)
        {
            if (cache.texture != null && cache.texture.width == TextureWidth && cache.texture.height == 1)
            {
                cache.texture.filterMode = alphaOnly ? FilterMode.Bilinear : cache.texture.filterMode;
                cache.pixels ??= new Color32[TextureWidth];
                return;
            }

            if (cache.texture != null)
                DestroyGradientTexture(cache.texture);

            cache.texture = new Texture2D(TextureWidth, 1, TextureFormat.RGBA32, false, true)
            {
                name = alphaOnly ? "Now Gradient Alpha Texture" : "Now Gradient Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            cache.pixels = new Color32[TextureWidth];
            cache.hash = 0;
            cache.initialized = 0;
        }

        static void FillGradientTexture(
            GradientTextureCache cache,
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys,
            GradientMode mode,
            bool alphaOnly)
        {
            cache.texture.filterMode = mode == GradientMode.Fixed && !alphaOnly
                ? FilterMode.Point
                : FilterMode.Bilinear;

            if (!alphaOnly)
            {
                PreviewGradient.mode = mode;
                PreviewGradient.SetKeys(colorKeys, alphaKeys);
            }

            for (int i = 0; i < TextureWidth; ++i)
            {
                float t = TextureWidth <= 1 ? 0f : i / (TextureWidth - 1f);
                Color color = alphaOnly
                    ? new Color(1f, 1f, 1f, EvaluateAlphaSorted(alphaKeys, t))
                    : PreviewGradient.Evaluate(t);
                cache.pixels[i] = color;
            }

            cache.texture.SetPixels32(cache.pixels);
            cache.texture.Apply(false, false);
        }

        static int HashGradientTexture(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys, GradientMode mode, bool alphaOnly)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (alphaOnly ? 1 : 0);
                hash = hash * 31 + (int)mode;

                if (!alphaOnly && colorKeys != null)
                {
                    hash = hash * 31 + colorKeys.Length;

                    for (int i = 0; i < colorKeys.Length; ++i)
                    {
                        var color = (Color32)colorKeys[i].color;
                        hash = hash * 31 + colorKeys[i].time.GetHashCode();
                        hash = hash * 31 + color.r;
                        hash = hash * 31 + color.g;
                        hash = hash * 31 + color.b;
                        hash = hash * 31 + color.a;
                    }
                }

                if (alphaKeys != null)
                {
                    hash = hash * 31 + alphaKeys.Length;

                    for (int i = 0; i < alphaKeys.Length; ++i)
                    {
                        hash = hash * 31 + alphaKeys[i].time.GetHashCode();
                        hash = hash * 31 + alphaKeys[i].alpha.GetHashCode();
                    }
                }

                return hash;
            }
        }

        static void DrawChecker(NowRect rect, float cellSize, NowThemeAsset theme)
        {
            CheckerDraw.Draw(rect, cellSize, theme);
        }

        static void DrawMarker(NowRect rect, Color color, bool selected, NowThemeAsset theme)
        {
            if (selected)
            {
                Color halo = theme.GetColor(NowColorToken.Accent);
                halo.a *= 0.18f;
                Now.Rectangle(rect.Outset(3f))
                    .SetColor(halo)
                    .SetRadius(5f)
                    .Draw();
            }

            DrawChecker(rect, 4f, theme);
            Now.Rectangle(rect)
                .SetColor(color)
                .SetRadius(3f)
                .SetOutline(selected ? 2f : 1f)
                .SetOutlineColor(selected ? theme.GetColor(NowColorToken.Accent) : theme.GetColor(NowColorToken.Border))
                .Draw();
        }

        static NowRect ColorMarkerRect(NowRect strip, float time)
        {
            float x = strip.x + Mathf.Clamp01(time) * strip.width;
            return new NowRect(x - 5f, strip.yMax + 3f, 10f, 12f);
        }

        static NowRect AlphaMarkerRect(NowRect lane, float time)
        {
            float width = 10f;
            float height = Mathf.Min(12f, Mathf.Max(6f, lane.height - 2f));
            float x = lane.x + Mathf.Clamp01(time) * lane.width;
            float y = lane.y + (lane.height - height) * 0.5f;
            return new NowRect(x - width * 0.5f, y, width, height);
        }

        static float PointerTime(NowRect rect, Vector2 pointer)
        {
            return Mathf.Clamp01((pointer.x - rect.x) / Mathf.Max(1f, rect.width));
        }

        static void SetPending(int pendingId, GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys, GradientMode mode)
        {
            ref var pending = ref NowControlState.Get<PendingGradient>(pendingId);
            pending.hasValue = 1;
            pending.colorKeys = Clone(colorKeys);
            pending.alphaKeys = Clone(alphaKeys);
            pending.mode = mode;
            NowControlState.RequestRepaint();
        }

        static Gradient DefaultGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        static void ApplyGradient(Gradient gradient, GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys, GradientMode mode)
        {
            Normalize(ref colorKeys, ref alphaKeys);
            gradient.mode = mode;
            gradient.SetKeys(colorKeys, alphaKeys);
        }

        static Color Evaluate(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys, GradientMode mode, float time)
        {
            Normalize(ref colorKeys, ref alphaKeys);
            PreviewGradient.mode = mode;
            PreviewGradient.SetKeys(colorKeys, alphaKeys);
            return PreviewGradient.Evaluate(Mathf.Clamp01(time));
        }

        static float EvaluateAlpha(GradientAlphaKey[] alphaKeys, float time)
        {
            if (alphaKeys == null || alphaKeys.Length == 0)
                return 1f;

            time = Mathf.Clamp01(time);
            SortAlphaKeys(alphaKeys);
            return EvaluateAlphaSorted(alphaKeys, time);
        }

        static float EvaluateAlphaSorted(GradientAlphaKey[] alphaKeys, float time)
        {
            if (alphaKeys == null || alphaKeys.Length == 0)
                return 1f;

            time = Mathf.Clamp01(time);

            if (time <= alphaKeys[0].time)
                return Mathf.Clamp01(alphaKeys[0].alpha);

            int last = alphaKeys.Length - 1;
            if (time >= alphaKeys[last].time)
                return Mathf.Clamp01(alphaKeys[last].alpha);

            for (int i = 1; i < alphaKeys.Length; ++i)
            {
                if (time > alphaKeys[i].time)
                    continue;

                float span = Mathf.Max(0.0001f, alphaKeys[i].time - alphaKeys[i - 1].time);
                float t = (time - alphaKeys[i - 1].time) / span;
                return Mathf.Lerp(alphaKeys[i - 1].alpha, alphaKeys[i].alpha, t);
            }

            return 1f;
        }

        static void InsertColorKey(ref GradientColorKey[] keys, float time, Color color)
        {
            var next = new GradientColorKey[(keys?.Length ?? 0) + 1];

            if (keys != null)
                Array.Copy(keys, next, keys.Length);

            next[next.Length - 1] = new GradientColorKey(color, Mathf.Clamp01(time));
            SortColorKeys(next);
            keys = next;
        }

        static void InsertAlphaKey(ref GradientAlphaKey[] keys, float time, float alpha)
        {
            var next = new GradientAlphaKey[(keys?.Length ?? 0) + 1];

            if (keys != null)
                Array.Copy(keys, next, keys.Length);

            next[next.Length - 1] = new GradientAlphaKey(Mathf.Clamp01(alpha), Mathf.Clamp01(time));
            SortAlphaKeys(next);
            keys = next;
        }

        static void DeleteColorKey(ref GradientColorKey[] keys, int index)
        {
            if (keys == null || keys.Length <= 1)
                return;

            index = Mathf.Clamp(index, 0, keys.Length - 1);
            var next = new GradientColorKey[keys.Length - 1];

            for (int source = 0, target = 0; source < keys.Length; ++source)
            {
                if (source == index)
                    continue;

                next[target++] = keys[source];
            }

            SortColorKeys(next);
            keys = next;
        }

        static void DeleteAlphaKey(ref GradientAlphaKey[] keys, int index)
        {
            if (keys == null || keys.Length <= 2)
                return;

            index = Mathf.Clamp(index, 0, keys.Length - 1);
            var next = new GradientAlphaKey[keys.Length - 1];

            for (int source = 0, target = 0; source < keys.Length; ++source)
            {
                if (source == index)
                    continue;

                next[target++] = keys[source];
            }

            SortAlphaKeys(next);
            keys = next;
        }

        static int IndexOfNearestColorKey(GradientColorKey[] keys, float time)
        {
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < keys.Length; ++i)
            {
                float distance = Mathf.Abs(keys[i].time - time);

                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        static int IndexOfNearestAlphaKey(GradientAlphaKey[] keys, float time)
        {
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < keys.Length; ++i)
            {
                float distance = Mathf.Abs(keys[i].time - time);

                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        static void Normalize(ref GradientColorKey[] colorKeys, ref GradientAlphaKey[] alphaKeys)
        {
            if (colorKeys == null || colorKeys.Length == 0)
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.white, 1f)
                };
            }

            EnsureAlphaKeys(ref alphaKeys);

            SortColorKeys(colorKeys);
            SortAlphaKeys(alphaKeys);
        }

        static void EnsureAlphaKeys(ref GradientAlphaKey[] alphaKeys)
        {
            if (alphaKeys != null && alphaKeys.Length > 0)
                return;

            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            };
        }

        static GradientColorKey[] Clone(GradientColorKey[] keys)
        {
            if (keys == null)
                return null;

            var clone = new GradientColorKey[keys.Length];
            Array.Copy(keys, clone, keys.Length);
            return clone;
        }

        static GradientAlphaKey[] Clone(GradientAlphaKey[] keys)
        {
            if (keys == null)
                return null;

            var clone = new GradientAlphaKey[keys.Length];
            Array.Copy(keys, clone, keys.Length);
            return clone;
        }

        /// <summary>
        /// In-place insertion sort by time: stable and allocation-free, unlike
        /// Array.Sort with a comparison delegate, so drag frames stay clean.
        /// </summary>
        static void SortColorKeys(GradientColorKey[] keys)
        {
            if (keys == null || IsSortedByTime(keys))
                return;

            for (int i = 1; i < keys.Length; ++i)
            {
                var key = keys[i];
                int j = i - 1;

                while (j >= 0 && keys[j].time > key.time)
                {
                    keys[j + 1] = keys[j];
                    --j;
                }

                keys[j + 1] = key;
            }
        }

        /// <summary>
        /// In-place insertion sort by time: stable and allocation-free, unlike
        /// Array.Sort with a comparison delegate, so drag frames stay clean.
        /// </summary>
        static void SortAlphaKeys(GradientAlphaKey[] keys)
        {
            if (keys == null || IsSortedByTime(keys))
                return;

            for (int i = 1; i < keys.Length; ++i)
            {
                var key = keys[i];
                int j = i - 1;

                while (j >= 0 && keys[j].time > key.time)
                {
                    keys[j + 1] = keys[j];
                    --j;
                }

                keys[j + 1] = key;
            }
        }

        static bool IsSortedByTime(GradientColorKey[] keys)
        {
            for (int i = 1; i < keys.Length; ++i)
            {
                if (keys[i - 1].time > keys[i].time)
                    return false;
            }

            return true;
        }

        static bool IsSortedByTime(GradientAlphaKey[] keys)
        {
            for (int i = 1; i < keys.Length; ++i)
            {
                if (keys[i - 1].time > keys[i].time)
                    return false;
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            DestroyTextureCache();
        }

        static void DestroyTextureCache()
        {
            foreach (var pair in _textureCaches)
            {
                if (pair.Value?.texture != null)
                    DestroyGradientTexture(pair.Value.texture);
            }

            _textureCaches.Clear();
        }

        static void DestroyGradientTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [NowBuilder]
    public struct NowAnimationCurveField
    {
        readonly NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowAnimationCurveFieldSettings _settings;
        NowFocusNavigation _navigation;

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public NowAnimationCurveFieldSettings settings;
            public int id;
            public int pendingId;
            public int selectedKeyId;
            public int draggedKeyId;
            public NowRect fieldRect;
            public NowRect popupRect;
            public NowRect plotRect;
            public NowRect inspectorRect;
            public Keyframe[] keys;
            public WrapMode preWrapMode;
            public WrapMode postWrapMode;
            public CurveBounds bounds;

            /// <summary>
            /// Re-clone guard: the keyframe array refreshes only when the source
            /// curve instance or key count changes or an edit was applied —
            /// Unity's keys getter allocates a fresh array on every access, so
            /// open frames must not read it unconditionally.
            /// </summary>
            public AnimationCurve source;

            public int sourceLength;

            public byte keysInitialized;
        }

        struct PendingCurve
        {
            public byte hasValue;
            public Keyframe[] keys;
            public WrapMode preWrapMode;
            public WrapMode postWrapMode;
        }

        /// <summary>
        /// Per-control mirror of the curve's keyframes. Unity's keys getter
        /// returns a fresh array copy on every access, so the field preview reads
        /// this mirror and refreshes it only on a detected change (new instance,
        /// key count change, or a popup edit), keeping the steady-state draw
        /// allocation-free.
        /// </summary>
        struct KeyMirror
        {
            public byte initialized;
            public int sourceLength;
            public AnimationCurve source;
            public Keyframe[] keys;
        }

        struct CurveBounds
        {
            public float timeMin;
            public float timeMax;
            public float valueMin;
            public float valueMax;
        }

        enum TangentSide
        {
            In,
            Out
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(4);
        static readonly AnimationCurve PreviewCurve = new AnimationCurve();

        const int KeySeed = 0x41434b31;
        const int PlotSeed = 0x41435031;
        const int TimeFieldSeed = 0x41435431;
        const int ValueFieldSeed = 0x41435631;
        const int DeleteKeySeed = 0x41434431;
        const int InTangentSeed = 0x41434954;
        const int OutTangentSeed = 0x41434f54;
        const int SmoothTangentSeed = 0x4143534d;
        const int LinearTangentSeed = 0x41434c4e;
        const int StepTangentSeed = 0x41435354;
        const int FlatTangentSeed = 0x4143464c;
        const int TangentContextSeed = 0x41434354;
        const int DeleteShortcutSeed = 0x41434453;
        const int KeyMirrorSeed = 0x41434b4d;
        const int CurvePendingSeed = 0x41435044;
        const int SelectedKeySeed = 0x4143534b;
        const int DraggedKeySeed = 0x4143444b;
        const float VerticalTangentPixelThreshold = 2f;
        const float StepTangentHandlePixels = 36f;
        const string DeleteGlyph = "🗑";

        internal NowAnimationCurveField(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _settings = NowAnimationCurveFieldSettings.Default;
            _navigation = default;
        }

        internal NowAnimationCurveField(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        /// <summary>Explicit layout options, overriding the content-derived size.</summary>
        public NowAnimationCurveField SetOptions(NowLayoutOptions options) { _settings.options = options; return this; }

        /// <summary>Fixed width in layout flow.</summary>
        public NowAnimationCurveField SetWidth(float width) { _settings.options = _settings.options.SetWidth(width); return this; }

        /// <summary>Fixed field height in layout flow.</summary>
        public NowAnimationCurveField SetHeight(float height)
        {
            _settings.options = _settings.options.SetHeight(height);
            _settings.fieldHeight = Mathf.Max(1f, height);
            return this;
        }

        /// <summary>Stretches to fill available width, weighted against stretching siblings.</summary>
        public NowAnimationCurveField SetStretchWidth(float weight = 1f) { _settings.options = _settings.options.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowAnimationCurveField SetId(NowId id)
        {
            _settings.idOverride = id;
            return this;
        }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowAnimationCurveField SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Total popup size for the curve editor.</summary>
        public NowAnimationCurveField SetPopupSize(float width, float height)
        {
            _settings.popupWidth = Mathf.Max(240f, width);
            _settings.popupHeight = Mathf.Max(160f, height);
            return this;
        }

        /// <summary>Keeps the popup inside the view bounds (on by default).</summary>
        public NowAnimationCurveField SetFitToView(bool fitToView = true)
        {
            _settings.fitToView = fitToView;
            return this;
        }

        /// <summary>Fixed horizontal plot range instead of auto-fitting the keys.</summary>
        public NowAnimationCurveField SetTimeRange(float min, float max)
        {
            if (max < min)
                (min, max) = (max, min);

            _settings.hasTimeRange = true;
            _settings.timeMin = min;
            _settings.timeMax = Mathf.Max(min + 0.0001f, max);
            return this;
        }

        /// <summary>Fixed vertical plot range instead of auto-fitting the keys.</summary>
        public NowAnimationCurveField SetValueRange(float min, float max)
        {
            if (max < min)
                (min, max) = (max, min);

            _settings.hasValueRange = true;
            _settings.valueMin = min;
            _settings.valueMax = Mathf.Max(min + 0.0001f, max);
            return this;
        }

        public bool Draw(ref AnimationCurve value)
        {
            if (value == null)
                value = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            int id = ResolveControlId();
            int pendingId = NowInput.CombineId(id, CurvePendingSeed);
            ref var pending = ref NowControlState.Get<PendingCurve>(pendingId);
            bool changed = false;

            if (pending.hasValue != 0)
            {
                pending.hasValue = 0;
                ApplyCurve(value, pending.keys, pending.preWrapMode, pending.postWrapMode);
                changed = true;
            }

            ref var mirror = ref NowControlState.Get<KeyMirror>(NowInput.CombineId(id, KeyMirrorSeed));

            if (mirror.initialized == 0 || changed ||
                !ReferenceEquals(mirror.source, value) || mirror.sourceLength != value.length)
            {
                mirror.initialized = 1;
                mirror.source = value;
                mirror.sourceLength = value.length;
                mirror.keys = value.keys;
                Normalize(ref mirror.keys);
            }

            var theme = NowTheme.themeAsset;
            var rect = NowControls.ReserveRect(_hasRect, _rect, _settings.options, MeasureField(_settings));
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
                open = !open;

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            DrawField(theme, rect, mirror.keys, value.preWrapMode, value.postWrapMode, _settings, open, focused, interaction.held, hoverT);

            if (open)
            {
                NowControlState.RequestRepaint();
                DeferPopup(theme, id, pendingId, rect, value, _settings, changed);
            }

            return changed;
        }

        int ResolveControlId()
        {
            return _settings.idOverride.hasValue
                ? NowControls.GetControlId(_settings.idOverride, _site)
                : NowControls.GetControlId(_id, _site);
        }

        static Vector2 MeasureField(NowAnimationCurveFieldSettings settings)
        {
            return new Vector2(190f, Mathf.Max(32f, settings.fieldHeight));
        }

        static void DrawField(
            NowThemeAsset theme,
            NowRect rect,
            Keyframe[] keys,
            WrapMode preWrapMode,
            WrapMode postWrapMode,
            NowAnimationCurveFieldSettings settings,
            bool open,
            bool focused,
            bool held,
            float hoverT)
        {
            theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused || open));

            var plot = rect.Inset(7f, 6f, 22f, 6f);
            DrawCurve(plot, keys, preWrapMode, postWrapMode, ResolveBounds(keys, settings), settings, theme);
            DropdownArrowDraw.Draw(
                theme,
                new NowRect(rect.xMax - 18f, rect.y, 14f, rect.height),
                open);
        }

        static void DeferPopup(
            NowThemeAsset theme,
            int id,
            int pendingId,
            NowRect field,
            AnimationCurve value,
            NowAnimationCurveFieldSettings settings,
            bool changed)
        {
            NowRect fieldScreen = Now.TransformScreenRect(field);
            float padding = settings.popupPadding;
            float popupWidth = Mathf.Max(settings.popupWidth, field.width);
            float popupHeight = Mathf.Max(
                settings.popupHeight,
                padding * 2f + settings.inspectorHeight + settings.inspectorGap + 56f);
            var popupRect = new NowRect(field.x, field.yMax + theme.controlStyles.dropdownPopupGap, popupWidth, popupHeight);

            if (settings.fitToView)
                popupRect = NowOverlay.FitToView(popupRect);

            var contentRect = popupRect.Inset(padding);
            float inspectorHeight = Mathf.Min(settings.inspectorHeight, Mathf.Max(24f, contentRect.height * 0.4f));
            float inspectorGap = Mathf.Min(settings.inspectorGap, Mathf.Max(0f, contentRect.height - inspectorHeight - 40f));
            float plotHeight = Mathf.Max(40f, contentRect.height - inspectorHeight - inspectorGap);
            var plotRect = new NowRect(contentRect.x, contentRect.y, contentRect.width, plotHeight);
            var inspectorRect = new NowRect(contentRect.x, plotRect.yMax + inspectorGap, contentRect.width, inspectorHeight);

            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = theme;
            state.settings = settings;
            state.id = id;
            state.pendingId = pendingId;
            state.selectedKeyId = NowInput.CombineId(id, SelectedKeySeed);
            state.draggedKeyId = NowInput.CombineId(id, DraggedKeySeed);
            state.fieldRect = fieldScreen;
            state.popupRect = popupRect;
            state.plotRect = plotRect;
            state.inspectorRect = inspectorRect;

            if (state.keysInitialized == 0 || changed ||
                !ReferenceEquals(state.source, value) || state.sourceLength != value.length)
            {
                state.keysInitialized = 1;
                state.source = value;
                state.sourceLength = value.length;
                state.keys = Clone(value.keys);
            }

            state.preWrapMode = value.preWrapMode;
            state.postWrapMode = value.postWrapMode;

            if (NowControlState.Get<int>(state.draggedKeyId) == 0 || !NowInput.IsPointerDown(NowPointerButton.Primary))
                state.bounds = ResolveBounds(state.keys, settings);

            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state))
                return;

            Normalize(ref state.keys);
            var theme = state.themeAsset;
            theme.controlRenderer.DrawPopupBackground(theme, state.popupRect, menu: false);

            int selected = Mathf.Clamp(NowControlState.Get<int>(state.selectedKeyId), 0, state.keys.Length - 1);
            bool changed = false;
            ref int draggedKey = ref NowControlState.Get<int>(state.draggedKeyId);
            bool keyDragActive = draggedKey != 0 && NowInput.IsPointerDown(NowPointerButton.Primary);

            if (!keyDragActive)
                state.bounds = ResolveBounds(state.keys, state.settings);

            for (int i = 0; i < state.keys.Length; ++i)
            {
                var marker = KeyMarkerRect(state.plotRect, state.bounds, state.keys[i]);
                var interaction = NowInput.Interact(NowInput.CombineId(NowInput.CombineId(state.id, KeySeed), i + 1), marker);

                if (interaction.pressed)
                {
                    selected = i;
                    draggedKey = i + 1;
                }

                if (interaction.dragging)
                {
                    int dragIndex = Mathf.Clamp(draggedKey > 0 ? draggedKey - 1 : i, 0, state.keys.Length - 1);
                    float movedTime = PointerTime(state.plotRect, state.bounds, interaction.pointerPosition);
                    var key = state.keys[dragIndex];
                    key.time = movedTime;
                    key.value = PointerValue(state.plotRect, state.bounds, interaction.pointerPosition);
                    state.keys[dragIndex] = key;
                    SortKeys(state.keys);
                    selected = IndexOfNearestKey(state.keys, movedTime);
                    draggedKey = selected + 1;
                    changed = true;
                }

                if (interaction.dragEnded)
                    draggedKey = 0;
            }

            keyDragActive = draggedKey != 0 && NowInput.IsPointerDown(NowPointerButton.Primary);
            selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);
            changed |= InteractSelectedTangents(state, selected);
            changed |= DrawTangentContextMenu(state, ref selected);

            var plotInteraction = NowInput.Interact(NowInput.CombineId(state.id, PlotSeed), state.plotRect);
            if (NowControlState.DetectDoubleClick(plotInteraction.id, plotInteraction.clicked))
            {
                float time = PointerTime(state.plotRect, state.bounds, plotInteraction.pointerPosition);
                float curveValue = PointerValue(state.plotRect, state.bounds, plotInteraction.pointerPosition);
                InsertKey(ref state.keys, CreateKey(time, curveValue));
                selected = IndexOfNearestKey(state.keys, time);
                ApplySmoothTangents(state.keys, selected);
                changed = true;
            }

            selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);
            changed |= DrawKeyInspector(state, ref selected);
            changed |= HandleDeleteShortcut(state, ref selected);

            if (!keyDragActive)
                state.bounds = ResolveBounds(state.keys, state.settings);

            DrawGrid(state.plotRect, state.bounds, theme);
            DrawCurve(state.plotRect, state.keys, state.preWrapMode, state.postWrapMode, state.bounds, state.settings, theme);
            DrawSelectedKeyGuides(state.plotRect, state.bounds, state.keys[selected], theme);
            DrawSelectedTangents(state.plotRect, state.bounds, state.keys, selected, theme);

            for (int i = 0; i < state.keys.Length; ++i)
                DrawKey(KeyMarkerRect(state.plotRect, state.bounds, state.keys[i]), i == selected, theme);

            NowControlState.Get<int>(state.selectedKeyId) = selected;

            if (changed)
                SetPending(state.pendingId, state.keys, state.preWrapMode, state.postWrapMode);
            var snapshot = NowInput.current;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !state.fieldRect.Contains(snapshot.pointerPosition);

            if (pressedOutside || (snapshot.cancelPressed && !NowOverlay.HasNestedOverlay(state.id)))
                NowControlState.Get<bool>(state.id) = false;
        }

        static bool DrawKeyInspector(PopupState state, ref int selected)
        {
            if (state.keys == null || state.keys.Length == 0)
                return false;

            var theme = state.themeAsset;
            var rect = state.inspectorRect;

            Now.Rectangle(rect)
                .SetColor(theme.GetColor(NowColorToken.SurfaceMuted))
                .SetRadius(4f)
                .Draw();

            float rowHeight = Mathf.Min(24f, rect.height);
            float gap = 6f;
            float majorGap = 10f;
            float outerPadding = 8f;
            bool hasTangentRow = rect.height >= 54f;
            float rowsHeight = hasTangentRow ? rowHeight * 2f + gap : rowHeight;
            float y = rect.y + Mathf.Max(0f, (rect.height - rowsHeight) * 0.5f);
            float tangentY = y + rowHeight + gap;
            float contentWidth = Mathf.Max(1f, rect.width - outerPadding * 2f);
            float deleteWidth = Mathf.Min(state.settings.inspectorButtonWidth, Mathf.Max(48f, contentWidth * 0.24f));
            float numericWidth = Mathf.Max(1f, contentWidth - deleteWidth - majorGap);
            float labelWidth = numericWidth < 170f ? 18f : state.settings.inspectorLabelWidth;
            float fieldWidth = Mathf.Min(
                state.settings.inspectorFieldWidth,
                Mathf.Max(30f, (numericWidth - labelWidth * 2f - gap * 2f - majorGap) * 0.5f));
            string timeLabel = numericWidth < 170f ? "T" : "Time";
            string valueLabel = numericWidth < 170f ? "V" : "Value";

            float x = rect.x + outerPadding;
            var timeLabelRect = new NowRect(x, y, labelWidth, rowHeight);
            var timeRect = new NowRect(timeLabelRect.xMax + gap, y, fieldWidth, rowHeight);
            x = timeRect.xMax + majorGap;
            var valueLabelRect = new NowRect(x, y, labelWidth, rowHeight);
            var valueRect = new NowRect(valueLabelRect.xMax + gap, y, fieldWidth, rowHeight);
            var deleteRect = new NowRect(rect.xMax - outerPadding - deleteWidth, y, deleteWidth, rowHeight);

            selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);
            var key = state.keys[selected];
            float time = key.time;
            float value = key.value;
            bool changed = false;

            NowControls.DrawLeftLabel(theme, timeLabelRect, timeLabel, NowTextStyle.Muted);
            var timeField = Now.FloatField(timeRect, NowId.Resolved(NowInput.CombineId(state.id, TimeFieldSeed)))
                .SetFormat("0.###");

            if (state.settings.hasTimeRange)
                timeField = timeField.SetRange(state.bounds.timeMin, state.bounds.timeMax);

            if (timeField.Draw(ref time))
            {
                key.time = time;
                changed = true;
            }

            NowControls.DrawLeftLabel(theme, valueLabelRect, valueLabel, NowTextStyle.Muted);
            var valueField = Now.FloatField(valueRect, NowId.Resolved(NowInput.CombineId(state.id, ValueFieldSeed)))
                .SetFormat("0.###");

            if (state.settings.hasValueRange)
                valueField = valueField.SetRange(state.bounds.valueMin, state.bounds.valueMax);

            if (valueField.Draw(ref value))
            {
                key.value = value;
                changed = true;
            }

            if (changed)
            {
                state.keys[selected] = key;
                SortKeys(state.keys);
                selected = IndexOfNearestKey(state.keys, key.time);
            }

            if (state.keys.Length > 1)
            {
                if (Now.Button(deleteRect, DeleteGlyph)
                        .SetId(NowId.Resolved(NowInput.CombineId(state.id, DeleteKeySeed)))
                        .SetStyle(NowRectangleStyle.Outline)
                        .Draw())
                {
                    DeleteKey(ref state.keys, selected);
                    selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);
                    changed = true;
                }
            }
            else
            {
                Now.Rectangle(deleteRect)
                    .SetColor(Color.clear)
                    .SetOutline(1f)
                    .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                    .SetRadius(4f)
                    .Draw();
                NowControls.DrawCenteredLabel(theme, deleteRect, DeleteGlyph, NowTextStyle.Muted, deleteRect);
            }

            if (hasTangentRow && state.keys.Length > 0)
                changed |= DrawTangentModeButtons(state, ref selected, new NowRect(rect.x + outerPadding, tangentY, contentWidth, rowHeight));

            return changed;
        }

        static bool HandleDeleteShortcut(PopupState state, ref int selected)
        {
            if (state.keys == null || state.keys.Length <= 1 ||
                IsCurveTextInputFocused(state) ||
                NowContextMenu.isOpen ||
                !DeleteKeyPressed(NowInput.CombineId(state.id, DeleteShortcutSeed)))
            {
                return false;
            }

            DeleteKey(ref state.keys, selected);
            selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);
            return true;
        }

        static bool IsCurveTextInputFocused(PopupState state)
        {
            int focused = NowFocus.focusedId;
            return (focused == NowInput.CombineId(state.id, TimeFieldSeed) ||
                focused == NowInput.CombineId(state.id, ValueFieldSeed)) &&
                NowFocus.IsFocused(focused);
        }

        static bool DeleteKeyPressed(int id)
        {
            if (NowInput.isPassive)
                return false;

            var frame = NowTextInput.current;
            return NowControlState.Repeat(id, frame.deleteHeld);
        }

        static bool DrawTangentModeButtons(PopupState state, ref int selected, NowRect rect)
        {
            selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);

            float gap = 6f;
            float width = Mathf.Max(1f, (rect.width - gap * 3f) * 0.25f);
            bool changed = false;

            var smoothRect = new NowRect(rect.x, rect.y, width, rect.height);
            var linearRect = new NowRect(smoothRect.xMax + gap, rect.y, width, rect.height);
            var stepRect = new NowRect(linearRect.xMax + gap, rect.y, width, rect.height);
            var flatRect = new NowRect(stepRect.xMax + gap, rect.y, Mathf.Max(1f, rect.xMax - stepRect.xMax - gap), rect.height);

            if (Now.Button(smoothRect, "Smooth")
                    .SetId(NowId.Resolved(NowInput.CombineId(state.id, SmoothTangentSeed)))
                    .SetStyle(NowRectangleStyle.Outline)
                    .Draw())
            {
                ApplySmoothTangents(state.keys, selected);
                changed = true;
            }

            if (Now.Button(linearRect, "Linear")
                    .SetId(NowId.Resolved(NowInput.CombineId(state.id, LinearTangentSeed)))
                    .SetStyle(NowRectangleStyle.Outline)
                    .Draw())
            {
                ApplyLinearTangents(state.keys, selected);
                changed = true;
            }

            if (Now.Button(stepRect, "Step")
                    .SetId(NowId.Resolved(NowInput.CombineId(state.id, StepTangentSeed)))
                    .SetStyle(NowRectangleStyle.Outline)
                    .Draw())
            {
                ApplyStepTangents(state.keys, selected);
                changed = true;
            }

            if (Now.Button(flatRect, "Flat")
                    .SetId(NowId.Resolved(NowInput.CombineId(state.id, FlatTangentSeed)))
                    .SetStyle(NowRectangleStyle.Outline)
                    .Draw())
            {
                ApplyFlatTangents(state.keys, selected);
                changed = true;
            }

            return changed;
        }

        static bool InteractSelectedTangents(PopupState state, int selected)
        {
            if (state.keys == null || state.keys.Length == 0 || selected < 0 || selected >= state.keys.Length)
                return false;

            bool changed = false;

            if (selected > 0)
                changed |= InteractTangentHandle(state, selected, TangentSide.In);

            if (selected < state.keys.Length - 1)
                changed |= InteractTangentHandle(state, selected, TangentSide.Out);

            return changed;
        }

        static bool InteractTangentHandle(PopupState state, int selected, TangentSide side)
        {
            if (!TryTangentHandleRect(state.plotRect, state.bounds, state.keys, selected, side, out var rect))
                return false;

            int seed = side == TangentSide.In ? InTangentSeed : OutTangentSeed;
            var interaction = NowInput.Interact(NowInput.CombineId(NowInput.CombineId(state.id, seed), selected + 1), rect);

            if (!interaction.dragging)
                return false;

            var key = state.keys[selected];
            float pointerTime = PointerTime(state.plotRect, state.bounds, interaction.pointerPosition);
            float pointerValue = PointerValue(state.plotRect, state.bounds, interaction.pointerPosition);
            float verticalThreshold = (state.bounds.timeMax - state.bounds.timeMin) *
                VerticalTangentPixelThreshold / Mathf.Max(1f, state.plotRect.width);
            ApplyDraggedTangent(ref key, state.keys, selected, side, pointerTime, pointerValue, verticalThreshold);
            state.keys[selected] = key;
            return true;
        }

        static bool DrawTangentContextMenu(PopupState state, ref int selected)
        {
            var snapshot = NowInput.current;
            int menuId = NowInput.CombineId(state.id, TangentContextSeed);
            Vector2 localPointer = Now.InverseTransformScreenPoint(snapshot.pointerPosition);

            if (NowInput.WasRightClicked(state.plotRect))
            {
                selected = HitKey(state.plotRect, state.bounds, state.keys, localPointer, selected);
                NowContextMenu.Open(menuId, snapshot.pointerPosition);
            }

            bool changed = false;

            if (NowContextMenu.Begin(menuId))
            {
                selected = Mathf.Clamp(selected, 0, state.keys.Length - 1);

                if (NowContextMenu.Item("Smooth Tangents"))
                {
                    ApplySmoothTangents(state.keys, selected);
                    changed = true;
                }

                if (NowContextMenu.Item("Linear Tangents"))
                {
                    ApplyLinearTangents(state.keys, selected);
                    changed = true;
                }

                if (NowContextMenu.Item("Step Tangents"))
                {
                    ApplyStepTangents(state.keys, selected);
                    changed = true;
                }

                if (NowContextMenu.Item("Flat Tangents"))
                {
                    ApplyFlatTangents(state.keys, selected);
                    changed = true;
                }

                NowContextMenu.End();
            }

            return changed;
        }

        static int HitKey(NowRect plot, CurveBounds bounds, Keyframe[] keys, Vector2 pointer, int fallback)
        {
            if (keys == null || keys.Length == 0)
                return 0;

            for (int i = 0; i < keys.Length; ++i)
            {
                if (KeyMarkerRect(plot, bounds, keys[i]).Contains(pointer))
                    return i;
            }

            return Mathf.Clamp(fallback, 0, keys.Length - 1);
        }

        static void DrawGrid(NowRect rect, CurveBounds bounds, NowThemeAsset theme)
        {
            Now.Rectangle(rect)
                .SetColor(theme.GetColor(NowColorToken.Surface))
                .SetRadius(4f)
                .Draw();

            Color grid = theme.GetColor(NowColorToken.Border);
            grid.a *= 0.38f;

            for (int i = 1; i < 6; ++i)
            {
                float x = rect.x + rect.width * i / 6f;
                float y = rect.y + rect.height * i / 6f;
                Now.Line(new Vector2(x, rect.y), new Vector2(x, rect.yMax)).SetColor(grid).SetWidth(1f).Draw();
                Now.Line(new Vector2(rect.x, y), new Vector2(rect.xMax, y)).SetColor(grid).SetWidth(1f).Draw();
            }

            Color axis = theme.GetColor(NowColorToken.TextMuted);
            axis.a *= 0.55f;

            if (bounds.timeMin < 0f && bounds.timeMax > 0f)
            {
                float x = CurvePoint(rect, bounds, 0f, bounds.valueMin).x;
                Now.Line(new Vector2(x, rect.y), new Vector2(x, rect.yMax)).SetColor(axis).SetWidth(1f).Draw();
            }

            if (bounds.valueMin < 0f && bounds.valueMax > 0f)
            {
                float y = CurvePoint(rect, bounds, bounds.timeMin, 0f).y;
                Now.Line(new Vector2(rect.x, y), new Vector2(rect.xMax, y)).SetColor(axis).SetWidth(1f).Draw();
            }

            Now.Rectangle(rect)
                .SetColor(Color.clear)
                .SetOutline(1f)
                .SetOutlineColor(theme.GetColor(NowColorToken.Border))
                .Draw();
        }

        static void DrawSelectedKeyGuides(NowRect plot, CurveBounds bounds, Keyframe key, NowThemeAsset theme)
        {
            var point = CurvePoint(plot, bounds, key.time, key.value);
            Color guide = theme.GetColor(NowColorToken.Accent);
            guide.a *= 0.32f;

            Now.Line(new Vector2(point.x, plot.y), new Vector2(point.x, plot.yMax))
                .SetColor(guide)
                .SetWidth(1f)
                .Draw();
            Now.Line(new Vector2(plot.x, point.y), new Vector2(plot.xMax, point.y))
                .SetColor(guide)
                .SetWidth(1f)
                .Draw();
        }

        static void DrawSelectedTangents(NowRect plot, CurveBounds bounds, Keyframe[] keys, int selected, NowThemeAsset theme)
        {
            if (keys == null || keys.Length == 0 || selected < 0 || selected >= keys.Length)
                return;

            if (selected > 0)
                DrawTangentHandle(plot, bounds, keys, selected, TangentSide.In, theme);

            if (selected < keys.Length - 1)
                DrawTangentHandle(plot, bounds, keys, selected, TangentSide.Out, theme);
        }

        static void DrawTangentHandle(NowRect plot, CurveBounds bounds, Keyframe[] keys, int selected, TangentSide side, NowThemeAsset theme)
        {
            if (!TryTangentHandlePoint(plot, bounds, keys, selected, side, out var handle))
                return;

            var keyPoint = CurvePoint(plot, bounds, keys[selected].time, keys[selected].value);
            Color color = theme.GetColor(NowColorToken.Accent);
            Color handleColor = color;
            handleColor.a = Mathf.Clamp01(handleColor.a * 0.92f);

            Now.Line(keyPoint, handle)
                .SetColor(handleColor)
                .SetWidth(1f)
                .Draw();

            float size = 8f;
            var rect = new NowRect(handle.x - size * 0.5f, handle.y - size * 0.5f, size, size);

            Now.Rectangle(rect)
                .SetColor(theme.GetColor(NowColorToken.Surface))
                .SetRadius(size * 0.5f)
                .SetOutline(1.5f)
                .SetOutlineColor(color)
                .Draw();
        }

        static void DrawCurve(
            NowRect rect,
            Keyframe[] keys,
            WrapMode preWrapMode,
            WrapMode postWrapMode,
            CurveBounds bounds,
            NowAnimationCurveFieldSettings settings,
            NowThemeAsset theme)
        {
            Normalize(ref keys);
            PreviewCurve.keys = keys;
            PreviewCurve.preWrapMode = preWrapMode;
            PreviewCurve.postWrapMode = postWrapMode;
            Color color = theme.GetColor(NowColorToken.Accent);
            int samples = Mathf.Clamp(settings.samples, 8, 128);

            if (keys == null || keys.Length == 0)
                return;

            if (keys.Length == 1)
            {
                var point = CurvePoint(rect, bounds, keys[0].time, keys[0].value);
                Now.Line(new Vector2(rect.x, point.y), new Vector2(rect.xMax, point.y))
                    .SetColor(color)
                    .SetWidth(2f)
                    .SetCap(NowLineCap.Round)
                    .Draw();
                return;
            }

            float timeMin = bounds.timeMin;
            float timeMax = bounds.timeMax;
            var first = keys[0];
            var last = keys[keys.Length - 1];

            if (timeMin < first.time)
                DrawSampledCurveRange(rect, bounds, timeMin, Mathf.Min(first.time, timeMax), samples, color);

            for (int i = 0; i < keys.Length - 1; ++i)
            {
                var left = keys[i];
                var right = keys[i + 1];
                float start = Mathf.Max(timeMin, left.time);
                float end = Mathf.Min(timeMax, right.time);

                if (end <= start)
                    continue;

                if (IsStepSegment(left, right))
                    DrawStepCurveSegment(rect, bounds, left, right, start, end, color);
                else if (CanDrawUnweightedBezier(left, right) && start <= left.time + 0.0001f && end >= right.time - 0.0001f)
                    DrawCurveSegmentBezier(rect, bounds, left, right, color);
                else
                    DrawSampledCurveRange(rect, bounds, start, end, SegmentSampleCount(samples, start, end, bounds), color);
            }

            if (timeMax > last.time)
                DrawSampledCurveRange(rect, bounds, Mathf.Max(last.time, timeMin), timeMax, samples, color);
        }

        static void DrawSampledCurveRange(NowRect rect, CurveBounds bounds, float start, float end, int samples, Color color)
        {
            if (end <= start)
                return;

            samples = Mathf.Clamp(samples, 2, 128);
            Span<Vector2> points = stackalloc Vector2[samples];

            for (int i = 0; i < samples; ++i)
            {
                float t = samples <= 1 ? 0f : i / (samples - 1f);
                float time = Mathf.Lerp(start, end, t);
                points[i] = CurvePoint(rect, bounds, time, PreviewCurve.Evaluate(time));
            }

            DrawCurvePolyline(points, color);
        }

        static int SegmentSampleCount(int totalSamples, float start, float end, CurveBounds bounds)
        {
            float span = Mathf.Max(0.0001f, bounds.timeMax - bounds.timeMin);
            float t = Mathf.Clamp01(Mathf.Abs(end - start) / span);
            return Mathf.Clamp(Mathf.CeilToInt(totalSamples * t) + 1, 4, 64);
        }

        static bool IsStepSegment(Keyframe left, Keyframe right)
        {
            return !IsFinite(left.outTangent) || !IsFinite(right.inTangent);
        }

        static bool CanDrawUnweightedBezier(Keyframe left, Keyframe right)
        {
            return !HasWeightedMode(left.weightedMode, WeightedMode.Out) &&
                !HasWeightedMode(right.weightedMode, WeightedMode.In);
        }

        static void DrawStepCurveSegment(NowRect rect, CurveBounds bounds, Keyframe left, Keyframe right, float start, float end, Color color)
        {
            float leftValue = left.value;
            float rightValue = right.value;
            var startPoint = CurvePoint(rect, bounds, start, leftValue);
            float cornerTime = right.time <= end + 0.0001f ? right.time : end;
            var corner = CurvePoint(rect, bounds, cornerTime, leftValue);
            Span<Vector2> points = stackalloc Vector2[3];
            int count = 0;

            points[count++] = startPoint;
            points[count++] = corner;

            if (right.time <= end + 0.0001f)
            {
                var endPoint = CurvePoint(rect, bounds, right.time, rightValue);
                points[count++] = endPoint;
            }

            DrawCurvePolyline(points.Slice(0, count), color);
        }

        static void DrawCurvePolyline(ReadOnlySpan<Vector2> points, Color color)
        {
            Now.DrawPolyline(points, 2f, NowLineCap.Round, color);
        }

        static void DrawCurveLine(Vector2 a, Vector2 b, Color color)
        {
            Now.Line(a, b)
                .SetColor(color)
                .SetWidth(2f)
                .SetCap(NowLineCap.Round)
                .Draw();
        }

        static void DrawCurveSegmentBezier(NowRect rect, CurveBounds bounds, Keyframe left, Keyframe right, Color color)
        {
            float dt = right.time - left.time;

            if (dt <= 0f)
                return;

            var p0 = CurvePoint(rect, bounds, left.time, left.value);
            var p1 = CurvePoint(rect, bounds, left.time + dt / 3f, left.value + dt / 3f * left.outTangent);
            var p2 = CurvePoint(rect, bounds, right.time - dt / 3f, right.value - dt / 3f * right.inTangent);
            var p3 = CurvePoint(rect, bounds, right.time, right.value);

            Now.Bezier(p0, p1, p2, p3)
                .SetColor(color)
                .SetWidth(2f)
                .SetCap(NowLineCap.Round)
                .Draw();
        }

        static void DrawKey(NowRect rect, bool selected, NowThemeAsset theme)
        {
            Now.Rectangle(rect)
                .SetColor(theme.GetColor(NowColorToken.Surface))
                .SetRadius(rect.width * 0.5f)
                .SetOutline(selected ? 2f : 1f)
                .SetOutlineColor(selected ? theme.GetColor(NowColorToken.Accent) : theme.GetColor(NowColorToken.Border))
                .Draw();
        }

        static NowRect KeyMarkerRect(NowRect plot, CurveBounds bounds, Keyframe key)
        {
            var point = CurvePoint(plot, bounds, key.time, key.value);
            float size = 10f;
            return new NowRect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
        }

        static bool TryTangentHandleRect(NowRect plot, CurveBounds bounds, Keyframe[] keys, int selected, TangentSide side, out NowRect rect)
        {
            rect = default;

            if (!TryTangentHandlePoint(plot, bounds, keys, selected, side, out var point))
                return false;

            float size = 14f;
            rect = new NowRect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            return true;
        }

        static bool TryTangentHandlePoint(NowRect plot, CurveBounds bounds, Keyframe[] keys, int selected, TangentSide side, out Vector2 point)
        {
            point = default;

            if (keys == null || selected < 0 || selected >= keys.Length)
                return false;

            int neighbor = side == TangentSide.In ? selected - 1 : selected + 1;

            if (neighbor < 0 || neighbor >= keys.Length)
                return false;

            var key = keys[selected];
            float tangent = side == TangentSide.In ? key.inTangent : key.outTangent;

            if (!IsFinite(tangent))
            {
                point = StepTangentHandlePoint(plot, bounds, keys, selected, neighbor, side);
                return true;
            }

            float span = Mathf.Abs(keys[neighbor].time - key.time);

            if (span <= 0.0001f)
                return false;

            float weight = side == TangentSide.In ? key.inWeight : key.outWeight;
            WeightedMode flag = side == TangentSide.In ? WeightedMode.In : WeightedMode.Out;

            if (!HasWeightedMode(key.weightedMode, flag) || !IsFinite(weight) || weight <= 0f)
                weight = 1f / 3f;

            float boundsSpan = Mathf.Max(0.0001f, bounds.timeMax - bounds.timeMin);
            float dt = Mathf.Clamp(span * weight, boundsSpan * 0.015f, span * 0.85f);
            float time = side == TangentSide.In ? key.time - dt : key.time + dt;
            float value = side == TangentSide.In ? key.value - tangent * dt : key.value + tangent * dt;
            point = CurvePoint(plot, bounds, time, value);
            return true;
        }

        static Vector2 StepTangentHandlePoint(
            NowRect plot,
            CurveBounds bounds,
            Keyframe[] keys,
            int selected,
            int neighbor,
            TangentSide side)
        {
            var key = keys[selected];
            float valueDirection = keys[neighbor].value - key.value;

            if (Mathf.Abs(valueDirection) <= 0.0001f)
                valueDirection = side == TangentSide.Out ? 1f : -1f;

            var keyPoint = CurvePoint(plot, bounds, key.time, key.value);
            return new Vector2(keyPoint.x, keyPoint.y - Mathf.Sign(valueDirection) * StepTangentHandlePixels);
        }

        static Vector2 CurvePoint(NowRect plot, CurveBounds bounds, float time, float value)
        {
            float x = Mathf.InverseLerp(bounds.timeMin, bounds.timeMax, time);
            float y = Mathf.InverseLerp(bounds.valueMin, bounds.valueMax, value);
            return new Vector2(plot.x + x * plot.width, plot.yMax - y * plot.height);
        }

        static float PointerTime(NowRect plot, CurveBounds bounds, Vector2 pointer)
        {
            float t = Mathf.Clamp01((pointer.x - plot.x) / Mathf.Max(1f, plot.width));
            return Mathf.Lerp(bounds.timeMin, bounds.timeMax, t);
        }

        static float PointerValue(NowRect plot, CurveBounds bounds, Vector2 pointer)
        {
            float t = 1f - Mathf.Clamp01((pointer.y - plot.y) / Mathf.Max(1f, plot.height));
            return Mathf.Lerp(bounds.valueMin, bounds.valueMax, t);
        }

        static void ApplyDraggedTangent(
            ref Keyframe key,
            Keyframe[] keys,
            int selected,
            TangentSide side,
            float pointerTime,
            float pointerValue,
            float verticalThreshold)
        {
            int neighbor = side == TangentSide.In ? selected - 1 : selected + 1;

            if (keys == null || neighbor < 0 || neighbor >= keys.Length)
                return;

            float span = Mathf.Abs(keys[neighbor].time - key.time);

            if (span <= 0.0001f)
                return;

            verticalThreshold = Mathf.Clamp(verticalThreshold, 0.0001f, span * 0.05f);

            if (side == TangentSide.In)
            {
                float rawDt = key.time - pointerTime;

                if (rawDt <= verticalThreshold)
                {
                    key.inTangent = float.PositiveInfinity;
                    key.inWeight = 1f / 3f;
                    key.weightedMode = RemoveWeightedMode(key.weightedMode, WeightedMode.In);
                    return;
                }

                float dt = Mathf.Clamp(rawDt, verticalThreshold, span);
                key.inTangent = (key.value - pointerValue) / dt;
                key.inWeight = Mathf.Clamp(dt / span, 0.01f, 1f);
                key.weightedMode = AddWeightedMode(key.weightedMode, WeightedMode.In);
            }
            else
            {
                float rawDt = pointerTime - key.time;

                if (rawDt <= verticalThreshold)
                {
                    key.outTangent = float.PositiveInfinity;
                    key.outWeight = 1f / 3f;
                    key.weightedMode = RemoveWeightedMode(key.weightedMode, WeightedMode.Out);
                    return;
                }

                float dt = Mathf.Clamp(rawDt, verticalThreshold, span);
                key.outTangent = (pointerValue - key.value) / dt;
                key.outWeight = Mathf.Clamp(dt / span, 0.01f, 1f);
                key.weightedMode = AddWeightedMode(key.weightedMode, WeightedMode.Out);
            }
        }

        static void ApplySmoothTangents(Keyframe[] keys, int index)
        {
            if (!TryGetKey(keys, index, out var key))
                return;

            float tangent = SmoothSlope(keys, index);
            key.inTangent = tangent;
            key.outTangent = tangent;
            ResetWeights(ref key);
            keys[index] = key;
        }

        static void ApplyLinearTangents(Keyframe[] keys, int index)
        {
            if (!TryGetKey(keys, index, out var key))
                return;

            float inTangent = index > 0 ? Slope(keys[index - 1], key) : 0f;
            float outTangent = index < keys.Length - 1 ? Slope(key, keys[index + 1]) : inTangent;

            if (index == 0)
                inTangent = outTangent;

            key.inTangent = inTangent;
            key.outTangent = outTangent;
            ResetWeights(ref key);
            keys[index] = key;
        }

        static void ApplyStepTangents(Keyframe[] keys, int index)
        {
            if (!TryGetKey(keys, index, out var key))
                return;

            key.inTangent = float.PositiveInfinity;
            key.outTangent = float.PositiveInfinity;
            ResetWeights(ref key);
            keys[index] = key;
        }

        static void ApplyFlatTangents(Keyframe[] keys, int index)
        {
            if (!TryGetKey(keys, index, out var key))
                return;

            key.inTangent = 0f;
            key.outTangent = 0f;
            ResetWeights(ref key);
            keys[index] = key;
        }

        static bool TryGetKey(Keyframe[] keys, int index, out Keyframe key)
        {
            key = default;

            if (keys == null || index < 0 || index >= keys.Length)
                return false;

            key = keys[index];
            return true;
        }

        static float SmoothSlope(Keyframe[] keys, int index)
        {
            if (keys == null || keys.Length <= 1)
                return 0f;

            if (index > 0 && index < keys.Length - 1)
                return Slope(keys[index - 1], keys[index + 1]);

            if (index == 0)
                return Slope(keys[index], keys[index + 1]);

            return Slope(keys[index - 1], keys[index]);
        }

        static float Slope(Keyframe a, Keyframe b)
        {
            float dt = b.time - a.time;

            if (Mathf.Abs(dt) <= 0.0001f)
                return 0f;

            return (b.value - a.value) / dt;
        }

        static Keyframe CreateKey(float time, float value)
        {
            var key = new Keyframe(time, value)
            {
                inWeight = 1f / 3f,
                outWeight = 1f / 3f,
                weightedMode = WeightedMode.None
            };
            return key;
        }

        static void ResetWeights(ref Keyframe key)
        {
            key.inWeight = 1f / 3f;
            key.outWeight = 1f / 3f;
            key.weightedMode = WeightedMode.None;
        }

        static bool HasWeightedMode(WeightedMode mode, WeightedMode flag)
        {
            return ((int)mode & (int)flag) != 0;
        }

        static WeightedMode AddWeightedMode(WeightedMode mode, WeightedMode flag)
        {
            return (WeightedMode)((int)mode | (int)flag);
        }

        static WeightedMode RemoveWeightedMode(WeightedMode mode, WeightedMode flag)
        {
            return (WeightedMode)((int)mode & ~(int)flag);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static void SetPending(int pendingId, Keyframe[] keys, WrapMode preWrapMode, WrapMode postWrapMode)
        {
            ref var pending = ref NowControlState.Get<PendingCurve>(pendingId);
            pending.hasValue = 1;
            pending.keys = Clone(keys);
            pending.preWrapMode = preWrapMode;
            pending.postWrapMode = postWrapMode;
            NowControlState.RequestRepaint();
        }

        static void ApplyCurve(AnimationCurve curve, Keyframe[] keys, WrapMode preWrapMode, WrapMode postWrapMode)
        {
            Normalize(ref keys);
            curve.keys = keys;
            curve.preWrapMode = preWrapMode;
            curve.postWrapMode = postWrapMode;
        }

        static CurveBounds ResolveBounds(Keyframe[] keys, NowAnimationCurveFieldSettings settings)
        {
            Normalize(ref keys);
            var bounds = new CurveBounds
            {
                timeMin = settings.hasTimeRange ? settings.timeMin : keys[0].time,
                timeMax = settings.hasTimeRange ? settings.timeMax : keys[keys.Length - 1].time,
                valueMin = settings.hasValueRange ? settings.valueMin : keys[0].value,
                valueMax = settings.hasValueRange ? settings.valueMax : keys[0].value
            };

            if (!settings.hasValueRange)
            {
                for (int i = 1; i < keys.Length; ++i)
                {
                    bounds.valueMin = Mathf.Min(bounds.valueMin, keys[i].value);
                    bounds.valueMax = Mathf.Max(bounds.valueMax, keys[i].value);
                }

                float padding = Mathf.Max(0.1f, (bounds.valueMax - bounds.valueMin) * 0.12f);
                bounds.valueMin -= padding;
                bounds.valueMax += padding;
            }

            if (!settings.hasTimeRange)
            {
                if (Mathf.Abs(bounds.timeMax - bounds.timeMin) < 0.0001f)
                {
                    bounds.timeMin -= 0.5f;
                    bounds.timeMax += 0.5f;
                }
            }

            if (Mathf.Abs(bounds.valueMax - bounds.valueMin) < 0.0001f)
            {
                bounds.valueMin -= 0.5f;
                bounds.valueMax += 0.5f;
            }

            return bounds;
        }

        static void InsertKey(ref Keyframe[] keys, Keyframe key)
        {
            var next = new Keyframe[(keys?.Length ?? 0) + 1];

            if (keys != null)
                Array.Copy(keys, next, keys.Length);

            next[next.Length - 1] = key;
            SortKeys(next);
            keys = next;
        }

        static void DeleteKey(ref Keyframe[] keys, int index)
        {
            if (keys == null || keys.Length <= 1 || index < 0 || index >= keys.Length)
                return;

            var next = new Keyframe[keys.Length - 1];

            for (int i = 0, write = 0; i < keys.Length; ++i)
            {
                if (i == index)
                    continue;

                next[write++] = keys[i];
            }

            keys = next;
        }

        static int IndexOfNearestKey(Keyframe[] keys, float time)
        {
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < keys.Length; ++i)
            {
                float distance = Mathf.Abs(keys[i].time - time);

                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        static void Normalize(ref Keyframe[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                keys = new[]
                {
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 1f)
                };
            }

            SortKeys(keys);
        }

        static Keyframe[] Clone(Keyframe[] keys)
        {
            if (keys == null)
                return null;

            var clone = new Keyframe[keys.Length];
            Array.Copy(keys, clone, keys.Length);
            return clone;
        }

        /// <summary>
        /// In-place insertion sort by time: stable and allocation-free, unlike
        /// Array.Sort with a comparison delegate, so drag frames stay clean.
        /// </summary>
        static void SortKeys(Keyframe[] keys)
        {
            if (keys == null || IsSortedByTime(keys))
                return;

            for (int i = 1; i < keys.Length; ++i)
            {
                var key = keys[i];
                int j = i - 1;

                while (j >= 0 && keys[j].time > key.time)
                {
                    keys[j + 1] = keys[j];
                    --j;
                }

                keys[j + 1] = key;
            }
        }

        static bool IsSortedByTime(Keyframe[] keys)
        {
            for (int i = 1; i < keys.Length; ++i)
            {
                if (keys[i - 1].time > keys[i].time)
                    return false;
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
        }
    }

    public static partial class Now
    {
        public static NowTextField FloatField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowTextField IntField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowVectorField VectorField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowVectorField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowVectorField Vector2Field(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(rect, id, file, line);
        }

        public static NowVectorField Vector3Field(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(rect, id, file, line);
        }

        public static NowVectorField Vector4Field(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(rect, id, file, line);
        }

        public static NowVectorField Vector2IntField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(rect, id, file, line);
        }

        public static NowVectorField Vector3IntField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(rect, id, file, line);
        }

        public static NowEnumDropdown<TEnum> EnumDropdown<TEnum>(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
            where TEnum : struct, Enum
        {
            return new NowEnumDropdown<TEnum>(rect, id, NowControls.SiteId(file, line));
        }

        public static NowEnumFlags<TEnum> EnumFlags<TEnum>(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
            where TEnum : struct, Enum
        {
            return new NowEnumFlags<TEnum>(rect, id, NowControls.SiteId(file, line));
        }

        public static NowColorPicker ColorPicker(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowColorPicker(rect, id, NowControls.SiteId(file, line));
        }

        public static NowGradientField GradientField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowGradientField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowAnimationCurveField AnimationCurveField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowAnimationCurveField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowAnimationCurveField CurveField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return AnimationCurveField(rect, id, file, line);
        }
    }

    public static partial class NowLayout
    {
        public static NowTextField FloatField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(id, NowControls.SiteId(file, line));
        }

        public static NowTextField IntField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(id, NowControls.SiteId(file, line));
        }

        public static NowVectorField VectorField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowVectorField(id, NowControls.SiteId(file, line));
        }

        public static NowVectorField Vector2Field(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(id, file, line);
        }

        public static NowVectorField Vector3Field(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(id, file, line);
        }

        public static NowVectorField Vector4Field(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(id, file, line);
        }

        public static NowVectorField Vector2IntField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(id, file, line);
        }

        public static NowVectorField Vector3IntField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return VectorField(id, file, line);
        }

        public static NowEnumDropdown<TEnum> EnumDropdown<TEnum>(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
            where TEnum : struct, Enum
        {
            return new NowEnumDropdown<TEnum>(id, NowControls.SiteId(file, line));
        }

        public static NowEnumFlags<TEnum> EnumFlags<TEnum>(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
            where TEnum : struct, Enum
        {
            return new NowEnumFlags<TEnum>(id, NowControls.SiteId(file, line));
        }

        public static NowColorPicker ColorPicker(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowColorPicker(id, NowControls.SiteId(file, line));
        }

        public static NowGradientField GradientField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowGradientField(id, NowControls.SiteId(file, line));
        }

        public static NowAnimationCurveField AnimationCurveField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowAnimationCurveField(id, NowControls.SiteId(file, line));
        }

        public static NowAnimationCurveField CurveField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return AnimationCurveField(id, file, line);
        }
    }

    struct NowColorPickerSettings
    {
        public NowLayoutOptions options;
        public NowId idOverride;
        public bool showAlpha;
        public bool fitToView;
        public float fieldHeight;
        public float popupWidth;
        public float popupPadding;
        public float popupGap;
        public float saturationValueSize;
        public float stripWidth;
        public int saturationValueSegments;
        public int hueSegments;
        public int alphaSegments;
        public float controlGap;
        public float hexRowHeight;
        public float hexButtonWidth;
        public float hexButtonGap;
        public float channelSliderHeight;
        public float channelSpacing;

        public static NowColorPickerSettings Default => new NowColorPickerSettings
        {
            showAlpha = true,
            fitToView = true,
            fieldHeight = 30f,
            popupWidth = 212f,
            popupPadding = 10f,
            popupGap = 8f,
            saturationValueSize = 160f,
            stripWidth = 16f,
            saturationValueSegments = 16,
            hueSegments = 24,
            alphaSegments = 16,
            controlGap = 10f,
            hexRowHeight = 24f,
            hexButtonWidth = 50f,
            hexButtonGap = 6f,
            channelSliderHeight = 18f,
            channelSpacing = 5f
        };
    }

    struct NowGradientFieldSettings
    {
        public NowLayoutOptions options;
        public NowId idOverride;
        public bool fitToView;
        public float fieldHeight;
        public float popupWidth;
        public float popupPadding;
        public float popupGap;
        public float stripHeight;
        public float alphaStripHeight;
        public float keyEditorHeight;
        public float keyEditorLabelWidth;
        public float keyEditorFieldWidth;
        public float keyEditorButtonWidth;
        public float colorPickerHeight;
        public float rampLabelWidth;
        public int previewSegments;

        public static NowGradientFieldSettings Default => new NowGradientFieldSettings
        {
            fieldHeight = 30f,
            fitToView = true,
            popupWidth = 320f,
            popupPadding = 10f,
            popupGap = 8f,
            stripHeight = 32f,
            alphaStripHeight = 18f,
            keyEditorHeight = 58f,
            keyEditorLabelWidth = 58f,
            keyEditorFieldWidth = 66f,
            keyEditorButtonWidth = 52f,
            colorPickerHeight = 214f,
            rampLabelWidth = 42f,
            previewSegments = 32
        };
    }

    struct NowAnimationCurveFieldSettings
    {
        public NowLayoutOptions options;
        public NowId idOverride;
        public bool fitToView;
        public float fieldHeight;
        public float popupWidth;
        public float popupHeight;
        public float popupPadding;
        public float inspectorHeight;
        public float inspectorGap;
        public float inspectorLabelWidth;
        public float inspectorFieldWidth;
        public float inspectorButtonWidth;
        public int samples;
        public bool hasTimeRange;
        public float timeMin;
        public float timeMax;
        public bool hasValueRange;
        public float valueMin;
        public float valueMax;

        public static NowAnimationCurveFieldSettings Default => new NowAnimationCurveFieldSettings
        {
            fieldHeight = 34f,
            fitToView = true,
            popupWidth = 320f,
            popupHeight = 258f,
            popupPadding = 12f,
            inspectorHeight = 66f,
            inspectorGap = 10f,
            inspectorLabelWidth = 34f,
            inspectorFieldWidth = 66f,
            inspectorButtonWidth = 58f,
            samples = 80
        };
    }

    struct NowVectorFieldSettings
    {
        public NowLayoutOptions options;
        public float componentWidth;
        public float labelWidth;
        public float spacing;
        public bool stretchComponents;
        public bool hasRange;
        public float min;
        public float max;
        public string format;
        public NowTextStyle textStyle;

        public static NowVectorFieldSettings Default => new NowVectorFieldSettings
        {
            componentWidth = 64f,
            labelWidth = 12f,
            spacing = 4f,
            textStyle = NowTextStyle.Body
        };
    }

    static class NowVectorFieldUtility
    {
        static readonly string[] XY = { "X", "Y" };
        static readonly string[] XYZ = { "X", "Y", "Z" };
        static readonly string[] XYZW = { "X", "Y", "Z", "W" };
        internal static readonly string[] XYWH = { "X", "Y", "W", "H" };

        public static bool DrawFloatComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, ref float x, ref float y)
        {
            bool changed = false;

            using (BeginScope(id, hasRect, rect, settings))
            {
                bool stretch = ShouldStretchComponents(hasRect, settings);
                changed |= DrawFloatComponent(id, 0, XY[0], settings, stretch, ref x);
                changed |= DrawFloatComponent(id, 1, XY[1], settings, stretch, ref y);
            }

            return changed;
        }

        public static bool DrawFloatComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, ref float x, ref float y, ref float z)
        {
            bool changed = false;

            using (BeginScope(id, hasRect, rect, settings))
            {
                bool stretch = ShouldStretchComponents(hasRect, settings);
                changed |= DrawFloatComponent(id, 0, XYZ[0], settings, stretch, ref x);
                changed |= DrawFloatComponent(id, 1, XYZ[1], settings, stretch, ref y);
                changed |= DrawFloatComponent(id, 2, XYZ[2], settings, stretch, ref z);
            }

            return changed;
        }

        public static bool DrawFloatComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, ref float x, ref float y, ref float z, ref float w)
        {
            return DrawFloatComponents(id, hasRect, rect, settings, XYZW, ref x, ref y, ref z, ref w);
        }

        public static bool DrawFloatComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, string[] labels, ref float x, ref float y, ref float z, ref float w)
        {
            bool changed = false;

            using (BeginScope(id, hasRect, rect, settings))
            {
                bool stretch = ShouldStretchComponents(hasRect, settings);
                changed |= DrawFloatComponent(id, 0, labels[0], settings, stretch, ref x);
                changed |= DrawFloatComponent(id, 1, labels[1], settings, stretch, ref y);
                changed |= DrawFloatComponent(id, 2, labels[2], settings, stretch, ref z);
                changed |= DrawFloatComponent(id, 3, labels[3], settings, stretch, ref w);
            }

            return changed;
        }

        public static bool DrawIntComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, string[] labels, ref int x, ref int y, ref int z, ref int w)
        {
            bool changed = false;

            using (BeginScope(id, hasRect, rect, settings))
            {
                bool stretch = ShouldStretchComponents(hasRect, settings);
                changed |= DrawIntComponent(id, 0, labels[0], settings, stretch, ref x);
                changed |= DrawIntComponent(id, 1, labels[1], settings, stretch, ref y);
                changed |= DrawIntComponent(id, 2, labels[2], settings, stretch, ref z);
                changed |= DrawIntComponent(id, 3, labels[3], settings, stretch, ref w);
            }

            return changed;
        }

        public static bool DrawIntComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, ref int x, ref int y)
        {
            bool changed = false;

            using (BeginScope(id, hasRect, rect, settings))
            {
                bool stretch = ShouldStretchComponents(hasRect, settings);
                changed |= DrawIntComponent(id, 0, XY[0], settings, stretch, ref x);
                changed |= DrawIntComponent(id, 1, XY[1], settings, stretch, ref y);
            }

            return changed;
        }

        public static bool DrawIntComponents(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings, ref int x, ref int y, ref int z)
        {
            bool changed = false;

            using (BeginScope(id, hasRect, rect, settings))
            {
                bool stretch = ShouldStretchComponents(hasRect, settings);
                changed |= DrawIntComponent(id, 0, XYZ[0], settings, stretch, ref x);
                changed |= DrawIntComponent(id, 1, XYZ[1], settings, stretch, ref y);
                changed |= DrawIntComponent(id, 2, XYZ[2], settings, stretch, ref z);
            }

            return changed;
        }

        static NowVectorFieldScope BeginScope(int id, bool hasRect, NowRect rect, NowVectorFieldSettings settings)
        {
            if (hasRect)
            {
                var area = NowLayout.Area(NowId.Resolved(NowInput.CombineId(id, 0x4e564172)), rect);
                var row = NowLayout.Horizontal(
                    spacing: settings.spacing,
                    alignItems: NowLayoutAlign.Center,
                    stretchWidth: true);
                return new NowVectorFieldScope(area, row, true);
            }

            return new NowVectorFieldScope(default, NowLayout.Horizontal(GroupOptions(settings)), false);
        }

        static bool ShouldStretchComponents(bool hasRect, NowVectorFieldSettings settings)
        {
            return hasRect ||
                settings.stretchComponents ||
                settings.options.Has(NowLayoutOptions.Field.Width) ||
                settings.options.Has(NowLayoutOptions.Field.StretchWidth);
        }

        static NowLayoutOptions GroupOptions(NowVectorFieldSettings settings)
        {
            var options = settings.options;

            if (!options.Has(NowLayoutOptions.Field.Spacing))
                options = options.SetSpacing(settings.spacing);

            if (!options.Has(NowLayoutOptions.Field.AlignItems))
                options = options.SetAlignItems(NowLayoutAlign.Center);

            return options;
        }

        static bool DrawFloatComponent(int parentId, int index, string label, NowVectorFieldSettings settings, bool stretch, ref float value)
        {
            DrawComponentLabel(label, settings);
            var field = ConfigureField(NowLayout.TextField(ComponentId(parentId, index)), settings, stretch);
            return field.Draw(ref value);
        }

        static bool DrawIntComponent(int parentId, int index, string label, NowVectorFieldSettings settings, bool stretch, ref int value)
        {
            DrawComponentLabel(label, settings);
            var field = ConfigureField(NowLayout.TextField(ComponentId(parentId, index)), settings, stretch);
            return field.Draw(ref value);
        }

        static NowTextField ConfigureField(NowTextField field, NowVectorFieldSettings settings, bool stretch)
        {
            field = field.SetTextStyle(settings.textStyle);

            if (settings.hasRange)
                field = field.SetRange(settings.min, settings.max);

            if (!string.IsNullOrEmpty(settings.format))
                field = field.SetFormat(settings.format);

            return stretch ? field.SetStretchWidth() : field.SetWidth(settings.componentWidth);
        }

        static void DrawComponentLabel(string label, NowVectorFieldSettings settings)
        {
            if (settings.labelWidth <= 0f)
                return;

            NowLayout.Label(label).SetWidth(settings.labelWidth).SetAlign(NowLayoutAlign.Center).Draw();
        }

        static NowId ComponentId(int parentId, int index)
        {
            return NowId.Resolved(NowInput.CombineId(parentId, 0x4e564300 + index + 1));
        }

        struct NowVectorFieldScope : IDisposable
        {
            NowLayoutScope _area;
            NowLayoutScope _row;
            bool _hasArea;
            bool _disposed;

            public NowVectorFieldScope(NowLayoutScope area, NowLayoutScope row, bool hasArea)
            {
                _area = area;
                _row = row;
                _hasArea = hasArea;
                _disposed = false;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _row.Dispose();

                if (_hasArea)
                    _area.Dispose();
            }
        }
    }

    static class NowEnumCache<TEnum> where TEnum : struct, Enum
    {
        public static readonly string[] names;
        public static readonly TEnum[] values;
        public static readonly string[] flagNames;
        public static readonly ulong[] flagBits;

        static NowEnumCache()
        {
            var type = typeof(TEnum);
            names = Enum.GetNames(type);
            values = new TEnum[names.Length];

            var flagNameList = new List<string>();
            var flagBitList = new List<ulong>();
            var seenFlags = new HashSet<ulong>();

            for (int i = 0; i < names.Length; ++i)
            {
                var value = (TEnum)Enum.Parse(type, names[i]);
                values[i] = value;

                ulong bits = NowEnumBits.ToUInt64(value);

                if (IsSingleBit(bits) && seenFlags.Add(bits))
                {
                    flagNameList.Add(names[i]);
                    flagBitList.Add(bits);
                }
            }

            flagNames = flagNameList.ToArray();
            flagBits = flagBitList.ToArray();
        }

        public static int IndexOf(TEnum value)
        {
            var comparer = EqualityComparer<TEnum>.Default;

            for (int i = 0; i < values.Length; ++i)
                if (comparer.Equals(values[i], value))
                    return i;

            return -1;
        }

        static bool IsSingleBit(ulong value)
        {
            return value != 0UL && (value & (value - 1UL)) == 0UL;
        }
    }

    /// <summary>
    /// Caches the enum's underlying type code per closed generic. Lives outside
    /// <see cref="NowEnumCache{TEnum}"/> because that type's static constructor
    /// calls <see cref="NowEnumBits.ToUInt64{TEnum}"/> — a field on the same
    /// type would be observed mid-initialization as its zeroed default.
    /// </summary>
    static class NowEnumTypeCode<TEnum> where TEnum : struct, Enum
    {
        public static readonly TypeCode value = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));
    }

    /// <summary>
    /// Enum-to-bits conversion via reinterpretation instead of boxing: the
    /// type-code switch guarantees the reinterpreted primitive has exactly the
    /// enum's underlying size.
    /// </summary>
    static class NowEnumBits
    {
        public static ulong ToUInt64<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            switch (NowEnumTypeCode<TEnum>.value)
            {
                case TypeCode.SByte: return unchecked((ulong)UnsafeUtility.As<TEnum, sbyte>(ref value));
                case TypeCode.Byte: return UnsafeUtility.As<TEnum, byte>(ref value);
                case TypeCode.Int16: return unchecked((ulong)UnsafeUtility.As<TEnum, short>(ref value));
                case TypeCode.UInt16: return UnsafeUtility.As<TEnum, ushort>(ref value);
                case TypeCode.Int32: return unchecked((ulong)UnsafeUtility.As<TEnum, int>(ref value));
                case TypeCode.UInt32: return UnsafeUtility.As<TEnum, uint>(ref value);
                case TypeCode.Int64: return unchecked((ulong)UnsafeUtility.As<TEnum, long>(ref value));
                case TypeCode.UInt64: return UnsafeUtility.As<TEnum, ulong>(ref value);
                default: return 0UL;
            }
        }

        public static TEnum FromUInt64<TEnum>(ulong value) where TEnum : struct, Enum
        {
            switch (NowEnumTypeCode<TEnum>.value)
            {
                case TypeCode.SByte:
                {
                    sbyte narrowed = unchecked((sbyte)value);
                    return UnsafeUtility.As<sbyte, TEnum>(ref narrowed);
                }
                case TypeCode.Byte:
                {
                    byte narrowed = (byte)value;
                    return UnsafeUtility.As<byte, TEnum>(ref narrowed);
                }
                case TypeCode.Int16:
                {
                    short narrowed = unchecked((short)value);
                    return UnsafeUtility.As<short, TEnum>(ref narrowed);
                }
                case TypeCode.UInt16:
                {
                    ushort narrowed = (ushort)value;
                    return UnsafeUtility.As<ushort, TEnum>(ref narrowed);
                }
                case TypeCode.Int32:
                {
                    int narrowed = unchecked((int)value);
                    return UnsafeUtility.As<int, TEnum>(ref narrowed);
                }
                case TypeCode.UInt32:
                {
                    uint narrowed = (uint)value;
                    return UnsafeUtility.As<uint, TEnum>(ref narrowed);
                }
                case TypeCode.Int64:
                {
                    long narrowed = unchecked((long)value);
                    return UnsafeUtility.As<long, TEnum>(ref narrowed);
                }
                case TypeCode.UInt64:
                    return UnsafeUtility.As<ulong, TEnum>(ref value);
                default:
                    return default;
            }
        }
    }

    /// <summary>
    /// Transparency checkerboard as a single textured quad: a shared 2x2
    /// point-filtered repeat texture tiled to the cell size, replacing one
    /// rectangle per cell. Cells anchor at the rect's top-left with color A
    /// first, matching the per-cell layout this draws instead of.
    /// </summary>
    static class CheckerDraw
    {
        static Texture2D _texture;
        static Color32 _colorA;
        static Color32 _colorB;
        static readonly Color32[] _pixels = new Color32[4];

        public static void Draw(NowRect rect, float cellSize, NowThemeAsset theme)
        {
            cellSize = Mathf.Max(2f, cellSize);
            Color32 a = theme.GetColor(NowColorToken.Surface);
            Color32 b = theme.GetColor(NowColorToken.SurfaceMuted);
            float cellsU = rect.width / (cellSize * 2f);
            float cellsV = rect.height / (cellSize * 2f);

            Now.Rectangle(rect)
                .SetTexture(GetTexture(a, b))
                .SetUV(new Vector4(0f, 1f - cellsV, cellsU, cellsV))
                .Draw();
        }

        static Texture2D GetTexture(Color32 a, Color32 b)
        {
            if (_texture != null && SameColor(_colorA, a) && SameColor(_colorB, b))
                return _texture;

            if (_texture == null)
            {
                _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
                {
                    name = "Now Checker Texture",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
            }

            _pixels[0] = b;
            _pixels[1] = a;
            _pixels[2] = a;
            _pixels[3] = b;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            _colorA = a;
            _colorB = b;
            return _texture;
        }

        static bool SameColor(Color32 x, Color32 y)
        {
            return x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            if (_texture != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_texture);
                else
                    UnityEngine.Object.DestroyImmediate(_texture);
            }

            _texture = null;
        }
    }

    static class DropdownArrowDraw
    {
        public static void Draw(NowThemeAsset theme, NowRect rect, bool open)
        {
            float size = Mathf.Min(rect.width, rect.height);
            float halfSize = size * 0.5f;
            Vector2 center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
            float offset = halfSize * 0.35f;
            Color color = theme.GetColor(NowColorToken.TextMuted);

            Vector2 a, b, c;
            if (open)
            {
                a = new Vector2(center.x, center.y + offset);
                b = new Vector2(center.x - offset, center.y - offset);
                c = new Vector2(center.x + offset, center.y - offset);
            }
            else
            {
                a = new Vector2(center.x, center.y - offset);
                b = new Vector2(center.x - offset, center.y + offset);
                c = new Vector2(center.x + offset, center.y + offset);
            }

            Now.Triangle(a, b, c).SetColor(color).Draw();
        }
    }
}
