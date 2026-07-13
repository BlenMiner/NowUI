using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Time-of-day field with a clock-dial popup: the header shows the hour and
    /// minute as clickable segments and the dial below sets the active one by
    /// tap or drag, auto-advancing from hour to minute on release. 24-hour mode
    /// uses a dual ring (outer 1–12, inner 13–00); 12-hour mode adds AM/PM
    /// chips. The value is caller-owned:
    /// <code>NowLayout.TimePicker().Draw(ref alarmTime);</code>
    /// </summary>
    [NowBuilder]
    public struct NowTimePicker
    {
        NowId _id;
        readonly int _site;

        const int TimePartsSeed = 0x4e545054;
        const int ClockModeSeed = 0x4e54434d;

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
            public int dialId;
            public int hourHeaderId;
            public int minuteHeaderId;
            public int amId;
            public int pmId;
            public bool twentyFourHour;
            public int minuteStep;
            public int openedFrame;
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

        static string[] s_outerHourLabels;

        static string[] s_innerHourLabels;

        static string[] s_minuteLabels;

        static string[] s_twoDigitLabels;

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

        /// <summary>Minute increment the dial snaps to.</summary>
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

            ref var parts = ref NowControlState.Get<TimeParts>(NowInput.CombineId(id, TimePartsSeed));
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
            float lineHeight = textStyle.font != null
                ? textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize
                : 20f;

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
                    NowControlState.Get<int>(NowInput.CombineId(id, ClockModeSeed)) = 0;
                    GetState(id).openedFrame = NowInput.current.frame;
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
            float padding = styles.popupPadding + 4f;
            float dialSize = styles.clockDialSize;
            float width = Mathf.Max(field.width, dialSize + padding * 2f);
            float height = padding * 2f + styles.clockHeaderHeight + 10f + dialSize + (_twentyFourHour ? 0f : styles.chipHeight + 8f);
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, width, height);

            if (_fitToView)
                popupRect = NowOverlay.FitToView(popupRect);

            var state = GetState(id);

            state.themeAsset = theme;
            state.id = id;
            state.dialId = NowInput.GetId(id, "dial");
            state.hourHeaderId = NowInput.GetId(id, "hour");
            state.minuteHeaderId = NowInput.GetId(id, "minute");
            state.amId = NowInput.GetId(id, "am");
            state.pmId = NowInput.GetId(id, "pm");
            state.twentyFourHour = _twentyFourHour;
            state.minuteStep = _minuteStep;
            state.field = Now.TransformScreenRect(field);
            state.popupRect = popupRect;

            NowOverlay.BlockAllSurfaces(id);
            NowOverlay.Defer(popupRect, id, DrawPopup);
        }

        static PopupState GetState(int id)
        {
            if (!_popupStates.TryGetValue(id, out var state))
            {
                state = new PopupState();
                _popupStates[id] = state;
            }

            return state;
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
            EnsureStaticLabels();

            ref var parts = ref NowControlState.Get<TimeParts>(NowInput.CombineId(state.id, TimePartsSeed));
            ref int mode = ref NowControlState.Get<int>(NowInput.CombineId(state.id, ClockModeSeed));

            UpdateKeyboard(state, ref parts, ref mode);

            var inner = popupRect.Inset(styles.popupPadding + 4f);
            var headerRect = new NowRect(inner.x, inner.y, inner.width, styles.clockHeaderHeight);

            DrawPopupHeader(state, headerRect, ref parts, ref mode);

            float dialSize = styles.clockDialSize;
            var dialRect = new NowRect(inner.x + (inner.width - dialSize) * 0.5f, headerRect.yMax + 10f, dialSize, dialSize);
            var metrics = renderer.CalculateClockDialMetrics(theme, dialRect);
            var interaction = NowInput.Interact(state.dialId, dialRect);

            if (interaction.held)
                ApplyDialPointer(state, in metrics, interaction.pointerPosition, ref parts, mode);

            if (mode == 0 && interaction.released)
            {
                mode = 1;
                NowControlState.RequestRepaint();
            }

            renderer.DrawClockDial(BuildDialContext(state, dialRect, in metrics, in parts, mode));

            if (!state.twentyFourHour)
            {
                float chipY = dialRect.yMax + 8f;
                float chipWidth = (inner.width - 8f) * 0.5f;
                var amRect = new NowRect(inner.x, chipY, chipWidth, styles.chipHeight);
                var pmRect = new NowRect(inner.x + chipWidth + 8f, chipY, chipWidth, styles.chipHeight);
                bool pm = parts.hour >= 12;

                if (new NowChip(amRect, "AM", 0).SetId(NowId.Resolved(state.amId)).SetSelected(!pm).Draw() && pm)
                    parts.hour -= 12;

                if (new NowChip(pmRect, "PM", 0).SetId(NowId.Resolved(state.pmId)).SetSelected(pm).Draw() && !pm)
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

        static void ApplyDialPointer(PopupState state, in NowClockDialMetrics metrics, Vector2 pointer, ref TimeParts parts, int mode)
        {
            Vector2 delta = pointer - metrics.center;

            if (delta.magnitude < metrics.faceRadius * 0.15f)
                return;

            float angle = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg;

            if (angle < 0f)
                angle += 360f;

            if (mode == 0)
            {
                int index = Mathf.RoundToInt(angle / 30f) % 12;
                int hour;

                if (state.twentyFourHour)
                {
                    bool innerRing = delta.magnitude < (metrics.outerRadius + metrics.innerRadius) * 0.5f;
                    hour = innerRing
                        ? (index == 0 ? 0 : index + 12)
                        : (index == 0 ? 12 : index);
                }
                else
                {
                    hour = index + (parts.hour >= 12 ? 12 : 0);
                }

                if (hour != parts.hour)
                {
                    parts.hour = hour;
                    NowControlState.RequestRepaint();
                }
            }
            else
            {
                int step = Mathf.Max(1, state.minuteStep);
                int minute = Mathf.RoundToInt(angle / (6f * step)) * step % 60;

                if (minute != parts.minute)
                {
                    parts.minute = minute;
                    NowControlState.RequestRepaint();
                }
            }
        }

        static NowClockDialRenderContext BuildDialContext(PopupState state, NowRect dialRect, in NowClockDialMetrics metrics, in TimeParts parts, int mode)
        {
            string[] outerLabels;
            string[] innerLabels = null;
            int selectedOuter = -1;
            int selectedInner = -1;
            float handRadius = metrics.outerRadius;
            float handAngle;
            bool handOnLabel = true;

            if (mode == 0)
            {
                outerLabels = s_outerHourLabels;

                if (state.twentyFourHour)
                {
                    innerLabels = s_innerHourLabels;
                    int hour = parts.hour;

                    if (hour == 12)
                    {
                        selectedOuter = 0;
                    }
                    else if (hour >= 1 && hour <= 11)
                    {
                        selectedOuter = hour;
                    }
                    else
                    {
                        selectedInner = hour == 0 ? 0 : hour - 12;
                        handRadius = metrics.innerRadius;
                    }

                    handAngle = (selectedOuter >= 0 ? selectedOuter : selectedInner) * 30f;
                }
                else
                {
                    selectedOuter = parts.hour % 12;
                    handAngle = selectedOuter * 30f;
                }
            }
            else
            {
                outerLabels = s_minuteLabels;
                handAngle = parts.minute * 6f;

                if (parts.minute % 5 == 0)
                    selectedOuter = parts.minute / 5;
                else
                    handOnLabel = false;
            }

            return new NowClockDialRenderContext(
                state.themeAsset,
                dialRect,
                metrics,
                outerLabels,
                innerLabels,
                selectedOuter,
                selectedInner,
                handAngle,
                handRadius,
                handOnLabel);
        }

        static void DrawPopupHeader(PopupState state, NowRect headerRect, ref TimeParts parts, ref int mode)
        {
            var theme = state.themeAsset;
            float boxWidth = 56f;
            float colonWidth = 20f;
            float x = headerRect.x + (headerRect.width - boxWidth * 2f - colonWidth) * 0.5f;

            var hourRect = new NowRect(x, headerRect.y, boxWidth, headerRect.height);
            var colonRect = new NowRect(hourRect.xMax, headerRect.y, colonWidth, headerRect.height);
            var minuteRect = new NowRect(colonRect.xMax, headerRect.y, boxWidth, headerRect.height);

            var hour = NowInput.Interact(state.hourHeaderId, hourRect);
            var minute = NowInput.Interact(state.minuteHeaderId, minuteRect);

            if (hour.clicked && mode != 0)
            {
                mode = 0;
                NowControlState.RequestRepaint();
            }

            if (minute.clicked && mode != 1)
            {
                mode = 1;
                NowControlState.RequestRepaint();
            }

            string hourText = state.twentyFourHour
                ? s_twoDigitLabels[parts.hour]
                : s_outerHourLabels[parts.hour % 12];

            DrawHeaderCell(theme, hourRect, hourText, mode == 0, hour);
            NowControls.DrawCenteredLabel(theme, colonRect, ":", NowTextStyle.Subheading, colonRect);
            DrawHeaderCell(theme, minuteRect, s_twoDigitLabels[parts.minute], mode == 1, minute);
        }

        static void DrawHeaderCell(NowThemeAsset theme, NowRect rect, string label, bool active, in NowInteraction interaction)
        {
            Color background = active
                ? theme.GetColor(interaction.held ? NowColorToken.AccentPressed : NowColorToken.Accent)
                : theme.GetColor(interaction.hovered || interaction.held ? NowColorToken.SurfaceHover : NowColorToken.SurfaceMuted);

            Now.Rectangle(rect).SetRadius(8f).SetColor(background).Draw();

            Color textColor = active
                ? theme.GetColor(NowColorToken.AccentText)
                : theme.GetColor(NowColorToken.Text);

            NowControls.DrawCenteredLabel(theme, rect, label, NowTextStyle.Subheading, rect, textColor);
        }

        /// <summary>
        /// Keyboard editing for the open popup: left/right switch between hour
        /// and minute, up/down step the active unit (minutes by the configured
        /// step), submit advances from hour to minute and then closes. Base
        /// focus navigation is locked while the popup is open.
        /// </summary>
        static void UpdateKeyboard(PopupState state, ref TimeParts parts, ref int mode)
        {
            if (NowInput.isPassive)
                return;

            NowFocus.LockNavigation();

            var snapshot = NowInput.current;
            var navigation = snapshot.navigation;

            if (NowControlState.Repeat(state.id, "nav-x", Mathf.Abs(navigation.x) > 0.55f, 0.35f, 0.2f))
            {
                int next = navigation.x > 0f ? 1 : 0;

                if (next != mode)
                {
                    mode = next;
                    NowControlState.RequestRepaint();
                }
            }

            if (NowControlState.Repeat(state.id, "nav-y", Mathf.Abs(navigation.y) > 0.55f, 0.35f, 0.08f))
            {
                int direction = navigation.y > 0f ? 1 : -1;

                if (mode == 0)
                {
                    parts.hour = (parts.hour + direction + 24) % 24;
                }
                else
                {
                    int step = Mathf.Max(1, state.minuteStep);
                    parts.minute = (parts.minute + direction * step + 60) % 60;
                }

                NowControlState.RequestRepaint();
            }

            if (snapshot.submitPressed && snapshot.frame != state.openedFrame)
            {
                if (mode == 0)
                {
                    mode = 1;
                    NowControlState.RequestRepaint();
                }
                else
                {
                    NowControlState.Get<bool>(state.id) = false;
                }
            }
        }

        static void EnsureStaticLabels()
        {
            if (s_outerHourLabels == null)
            {
                s_outerHourLabels = new string[12];
                s_outerHourLabels[0] = "12";

                for (int i = 1; i < 12; ++i)
                    s_outerHourLabels[i] = i.ToString(CultureInfo.InvariantCulture);
            }

            if (s_innerHourLabels == null)
            {
                s_innerHourLabels = new string[12];
                s_innerHourLabels[0] = "00";

                for (int i = 1; i < 12; ++i)
                    s_innerHourLabels[i] = (i + 12).ToString(CultureInfo.InvariantCulture);
            }

            if (s_minuteLabels == null)
            {
                s_minuteLabels = new string[12];

                for (int i = 0; i < 12; ++i)
                    s_minuteLabels[i] = (i * 5).ToString("00", CultureInfo.InvariantCulture);
            }

            if (s_twoDigitLabels == null)
            {
                s_twoDigitLabels = new string[60];

                for (int i = 0; i < 60; ++i)
                    s_twoDigitLabels[i] = i.ToString("00", CultureInfo.InvariantCulture);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            _fieldLabels.Clear();
            s_outerHourLabels = null;
            s_innerHourLabels = null;
            s_minuteLabels = null;
            s_twoDigitLabels = null;
        }
    }
}
