using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Time-of-day field with a popup of hour/minute spinner fields (plus AM/PM
    /// in 12-hour mode). The value is caller-owned:
    /// <code>NowLayout.TimePicker().Draw(ref alarmTime);</code>
    /// </summary>
    [NowBuilder]
    public struct NowTimePicker
    {
        NowId _id;
        readonly int _site;
        NowFocusNavigation _navigation;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _fitToView;
        bool _twentyFourHour;
        int _minuteStep;

        struct TimeParts
        {
            public int hour;
            public int minute;
            public byte initialized;
        }

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public int id;
            public int hourFieldId;
            public int minuteFieldId;
            public int amId;
            public int pmId;
            public bool twentyFourHour;
            public int minuteStep;
            public NowRect field;
            public NowRect popupRect;
        }

        sealed class FieldLabel
        {
            public long ticks = long.MinValue;
            public bool twentyFourHour;
            public string text = string.Empty;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(4);

        static readonly Dictionary<int, FieldLabel> _fieldLabels = new Dictionary<int, FieldLabel>(8);

        internal NowTimePicker(NowId id, int site)
        {
            _id = id;
            _site = site;
            _navigation = default;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
            _fitToView = true;
            _twentyFourHour = true;
            _minuteStep = 1;
        }

        internal NowTimePicker(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowTimePicker SetOptions(NowLayoutOptions options) { _layoutOptions = options; return this; }

        public NowTimePicker SetWidth(float width) { _layoutOptions = _layoutOptions.SetWidth(width); return this; }

        public NowTimePicker SetStretchWidth(float weight = 1f) { _layoutOptions = _layoutOptions.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTimePicker SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowTimePicker SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>When true (default), moves the popup to stay inside the visible surface or world camera view.</summary>
        public NowTimePicker SetFitToView(bool fitToView = true) { _fitToView = fitToView; return this; }

        /// <summary>24-hour clock (default true); false adds AM/PM chips.</summary>
        public NowTimePicker Set24Hour(bool twentyFourHour) { _twentyFourHour = twentyFourHour; return this; }

        /// <summary>Minute increment used by the popup's minute spinner.</summary>
        public NowTimePicker SetMinuteStep(int step) { _minuteStep = Mathf.Clamp(step, 1, 30); return this; }

        /// <summary>Edits the time-of-day component; the date part is preserved.</summary>
        public bool Draw(ref DateTime value)
        {
            var time = value.TimeOfDay;
            bool changed = Draw(ref time);

            if (changed)
                value = value.Date + time;

            return changed;
        }

        public bool Draw(ref TimeSpan value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);

            ref var parts = ref NowControlState.Get<TimeParts>(id, "time-parts");
            ref bool open = ref NowControlState.Get<bool>(id);
            bool changed = false;

            if (open && parts.initialized != 0)
            {
                int previousHour = value.Hours;
                int previousMinute = value.Minutes;

                if (parts.hour != previousHour || parts.minute != previousMinute)
                {
                    long subMinuteTicks = value.Ticks % TimeSpan.TicksPerMinute;
                    value = new TimeSpan(
                        parts.hour * TimeSpan.TicksPerHour +
                        parts.minute * TimeSpan.TicksPerMinute +
                        subMinuteTicks);
                    changed = true;
                }
            }

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = textStyle.Measure("Ag").y;
            if (lineHeight <= 0f)
                lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);

            if (interaction.clicked || submitted)
            {
                open = !open;

                if (open)
                {
                    parts.hour = value.Hours;
                    parts.minute = value.Minutes;
                    parts.initialized = 1;
                }
            }

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            renderer.DrawDropdownField(new NowDropdownFieldRenderContext(
                theme, rect, FieldText(id, value, _twentyFourHour), open, interaction, focused, hoverT));

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, id, rect);
            return changed;
        }

        static string FieldText(int id, TimeSpan value, bool twentyFourHour)
        {
            if (!_fieldLabels.TryGetValue(id, out var label))
            {
                label = new FieldLabel();
                _fieldLabels[id] = label;
            }

            long ticks = value.Hours * 60L + value.Minutes;

            if (label.ticks != ticks || label.twentyFourHour != twentyFourHour)
            {
                label.ticks = ticks;
                label.twentyFourHour = twentyFourHour;

                if (twentyFourHour)
                {
                    label.text = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", value.Hours, value.Minutes);
                }
                else
                {
                    int hour = value.Hours % 12;

                    if (hour == 0)
                        hour = 12;

                    label.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1:00} {2}",
                        hour,
                        value.Minutes,
                        value.Hours < 12 ? "AM" : "PM");
                }
            }

            return label.text;
        }

        void DeferPopup(NowThemeAsset theme, int id, NowRect field)
        {
            var styles = theme.controlStyles;
            float width = Mathf.Max(field.width, 200f);
            float height = styles.dropdownFieldMinHeight + styles.popupPadding * 2f + (_twentyFourHour ? 16f : 56f);
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, width, height);

            if (_fitToView)
                popupRect = NowOverlay.FitToView(popupRect);

            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            state.themeAsset = theme;
            state.id = id;
            state.hourFieldId = NowInput.GetId(id, "hour");
            state.minuteFieldId = NowInput.GetId(id, "minute");
            state.amId = NowInput.GetId(id, "am");
            state.pmId = NowInput.GetId(id, "pm");
            state.twentyFourHour = _twentyFourHour;
            state.minuteStep = _minuteStep;
            state.field = Now.TransformScreenRect(field);
            state.popupRect = popupRect;

            NowOverlay.BlockAllSurfaces(id);
            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        static void DrawPopup(int stateId)
        {
            if (!_popupStates.TryGetValue(stateId, out var state) || state.themeAsset == null)
                return;

            var theme = state.themeAsset;
            var renderer = theme.controlRenderer;
            var styles = theme.controlStyles;
            var popupRect = state.popupRect;

            renderer.DrawPopupBackground(theme, popupRect, menu: false);

            ref var parts = ref NowControlState.Get<TimeParts>(state.id, "time-parts");

            var inner = popupRect.Inset(styles.popupPadding + 4f);
            float fieldHeight = Mathf.Max(styles.dropdownFieldMinHeight, 32f);
            float separator = 14f;
            float fieldWidth = (inner.width - separator) * 0.5f;

            var hourRect = new NowRect(inner.x, inner.y, fieldWidth, fieldHeight);
            var minuteRect = new NowRect(inner.x + fieldWidth + separator, inner.y, fieldWidth, fieldHeight);

            NowControls.DrawCenteredLabel(
                theme,
                new NowRect(inner.x + fieldWidth, inner.y, separator, fieldHeight),
                ":",
                NowTextStyle.BodyStrong,
                popupRect);

            if (state.twentyFourHour)
            {
                int hour = parts.hour;

                new NowTextField(hourRect, new NowId(state.hourFieldId), 0)
                    .SetRange(0, 23)
                    .SetSpinner(1f)
                    .Draw(ref hour);
                parts.hour = hour;
            }
            else
            {
                int displayHour = parts.hour % 12;

                if (displayHour == 0)
                    displayHour = 12;

                int editedHour = displayHour;

                new NowTextField(hourRect, new NowId(state.hourFieldId), 0)
                    .SetRange(1, 12)
                    .SetSpinner(1f)
                    .Draw(ref editedHour);

                if (editedHour != displayHour)
                {
                    bool pm = parts.hour >= 12;
                    parts.hour = (editedHour % 12) + (pm ? 12 : 0);
                }
            }

            int minute = parts.minute;

            new NowTextField(minuteRect, new NowId(state.minuteFieldId), 0)
                .SetRange(0, 59)
                .SetSpinner(state.minuteStep)
                .Draw(ref minute);
            parts.minute = minute;

            if (!state.twentyFourHour)
            {
                float chipY = inner.y + fieldHeight + 8f;
                float chipWidth = (inner.width - 8f) * 0.5f;
                var amRect = new NowRect(inner.x, chipY, chipWidth, styles.chipHeight);
                var pmRect = new NowRect(inner.x + chipWidth + 8f, chipY, chipWidth, styles.chipHeight);
                bool pm = parts.hour >= 12;

                if (new NowChip(amRect, "AM", 0).SetId(new NowId(state.amId)).SetSelected(!pm).Draw() && pm)
                    parts.hour -= 12;

                if (new NowChip(pmRect, "PM", 0).SetId(new NowId(state.pmId)).SetSelected(pm).Draw() && !pm)
                    parts.hour += 12;
            }

            var snapshot = NowInput.current;
            bool fieldPressClaimedByField = state.field.Contains(snapshot.pointerPosition) &&
                NowInput.activeId == state.id;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !fieldPressClaimedByField;

            if (pressedOutside ||
                (snapshot.cancelPressed && !NowInput.cancelConsumed && !NowOverlay.HasNestedOverlay(state.id)))
            {
                NowControlState.Get<bool>(state.id) = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            _fieldLabels.Clear();
        }
    }
}
