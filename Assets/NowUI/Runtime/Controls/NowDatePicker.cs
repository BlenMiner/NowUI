using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Date field with a calendar popup. The date is caller-owned; the popup
    /// edits only the date component and preserves the value's time of day:
    /// <code>NowLayout.DatePicker().Draw(ref dueDate);</code>
    /// Day cells are keyboard-navigable inside the popup; the optional
    /// <see cref="SetToday"/> highlight is caller-passed — no hidden clock.
    /// </summary>
    [NowBuilder]
    public struct NowDatePicker
    {
        NowId _id;
        readonly int _site;
        NowFocusNavigation _navigation;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;
        bool _fitToView;
        string _format;
        long _minTicks;
        long _maxTicks;
        bool _hasRange;
        long _todayTicks;
        bool _hasToday;

        struct PendingDate
        {
            public byte has;
            public long ticks;
        }

        struct ShownMonth
        {
            public int year;
            public int month;
        }

        sealed class PopupState
        {
            public NowThemeAsset themeAsset;
            public int id;
            public int pendingId;
            public int shownMonthId;
            public int daySeed;
            public int prevId;
            public int nextId;
            public int year;
            public int month;
            public long selectedTicks;
            public bool hasSelected;
            public long todayTicks;
            public bool hasToday;
            public long minTicks;
            public long maxTicks;
            public bool hasRange;
            public long highlightTicks;
            public bool hasHighlight;
            public int openedFrame;
            public NowRect field;
            public NowRect popupRect;
            public int cachedLabelYear;
            public int cachedLabelMonth;
            public string monthLabel;
        }

        sealed class FieldLabel
        {
            public long ticks = long.MinValue;
            public string format;
            public string text = string.Empty;
        }

        static readonly Dictionary<int, PopupState> _popupStates = new Dictionary<int, PopupState>(4);

        static readonly Dictionary<int, FieldLabel> _fieldLabels = new Dictionary<int, FieldLabel>(8);

        static string[] s_dayLabels;

        static string[] s_weekdayLabels;

        internal NowDatePicker(NowId id, int site)
        {
            _id = id;
            _site = site;
            _navigation = default;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
            _fitToView = true;
            _format = "yyyy-MM-dd";
            _minTicks = DateTime.MinValue.Ticks;
            _maxTicks = DateTime.MaxValue.Ticks;
            _hasRange = false;
            _todayTicks = 0;
            _hasToday = false;
        }

        internal NowDatePicker(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowDatePicker SetOptions(NowLayoutOptions options) { _layoutOptions = options; return this; }

        public NowDatePicker SetWidth(float width) { _layoutOptions = _layoutOptions.SetWidth(width); return this; }

        public NowDatePicker SetStretchWidth(float weight = 1f) { _layoutOptions = _layoutOptions.SetStretchWidth(weight); return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowDatePicker SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowDatePicker SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>When true (default), moves the popup to stay inside the visible surface or world camera view.</summary>
        public NowDatePicker SetFitToView(bool fitToView = true) { _fitToView = fitToView; return this; }

        /// <summary>Field display format (default "yyyy-MM-dd").</summary>
        public NowDatePicker SetFormat(string format)
        {
            _format = string.IsNullOrEmpty(format) ? "yyyy-MM-dd" : format;
            return this;
        }

        /// <summary>Inclusive selectable range; days outside it are disabled.</summary>
        public NowDatePicker SetRange(DateTime min, DateTime max)
        {
            if (max < min)
                (min, max) = (max, min);

            _minTicks = min.Date.Ticks;
            _maxTicks = max.Date.Ticks;
            _hasRange = true;
            return this;
        }

        /// <summary>Rings this date as "today" in the calendar. Caller-passed by design — the control has no hidden clock.</summary>
        public NowDatePicker SetToday(DateTime today)
        {
            _todayTicks = today.Date.Ticks;
            _hasToday = true;
            return this;
        }

        public bool Draw(ref DateTime value)
        {
            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);

            ref var pending = ref NowControlState.Get<PendingDate>(id, "pending-date");
            bool changed = false;

            if (pending.has != 0)
            {
                var next = new DateTime(pending.ticks).Date + value.TimeOfDay;
                changed = next != value;
                value = next;
                pending = default;
            }

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = textStyle.Measure("Ag").y;
            if (lineHeight <= 0f)
                lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);
            ref var shown = ref NowControlState.Get<ShownMonth>(id, "shown-month");

            if (interaction.clicked || submitted)
            {
                open = !open;

                if (open)
                {
                    shown.year = value.Year;
                    shown.month = value.Month;
                    ClampShownMonth(ref shown);

                    var openState = GetState(id);
                    openState.highlightTicks = value.Date.Ticks;
                    openState.hasHighlight = true;
                    openState.openedFrame = NowInput.current.frame;
                }
            }

            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);
            renderer.DrawDropdownField(new NowDropdownFieldRenderContext(
                theme, rect, FieldText(id, value, _format), open, interaction, focused, hoverT));

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, id, rect, value, ref shown);
            return changed;
        }

        static string FieldText(int id, DateTime value, string format)
        {
            if (!_fieldLabels.TryGetValue(id, out var label))
            {
                label = new FieldLabel();
                _fieldLabels[id] = label;
            }

            long ticks = value.Date.Ticks;

            if (label.ticks != ticks || !ReferenceEquals(label.format, format))
            {
                label.ticks = ticks;
                label.format = format;
                label.text = value.ToString(format, CultureInfo.CurrentCulture);
            }

            return label.text;
        }

        void DeferPopup(NowThemeAsset theme, int id, NowRect field, DateTime value, ref ShownMonth shown)
        {
            ClampShownMonth(ref shown);

            var styles = theme.controlStyles;
            float cell = styles.calendarCellSize;
            float padding = styles.calendarPadding;
            float width = cell * 7f + padding * 2f;
            float height = styles.calendarHeaderHeight + cell * 7f + padding * 2f;
            var popupRect = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, width, height);

            if (_fitToView)
                popupRect = NowOverlay.FitToView(popupRect);

            var state = GetState(id);

            state.themeAsset = theme;
            state.id = id;
            state.pendingId = id;
            state.shownMonthId = id;
            state.daySeed = NowInput.GetId(id, "day");
            state.prevId = NowInput.GetId(id, "prev-month");
            state.nextId = NowInput.GetId(id, "next-month");
            state.year = shown.year;
            state.month = shown.month;
            state.selectedTicks = value.Date.Ticks;
            state.hasSelected = true;
            state.todayTicks = _todayTicks;
            state.hasToday = _hasToday;
            state.minTicks = _minTicks;
            state.maxTicks = _maxTicks;
            state.hasRange = _hasRange;
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
            UpdateKeyboard(state);

            float padding = styles.calendarPadding;
            float cell = styles.calendarCellSize;
            var inner = popupRect.Inset(padding);
            var headerRect = new NowRect(inner.x, inner.y, inner.width, styles.calendarHeaderHeight);

            DrawHeader(state, headerRect);

            EnsureStaticLabels();

            float gridTop = headerRect.yMax;

            for (int i = 0; i < 7; ++i)
            {
                var dayRect = new NowRect(inner.x + i * cell, gridTop, cell, cell * 0.8f);
                NowControls.DrawCenteredLabel(theme, dayRect, s_weekdayLabels[i], NowTextStyle.Caption, dayRect);
            }

            gridTop += cell * 0.8f;

            var firstOfMonth = new DateTime(state.year, state.month, 1);
            int leading = ((int)firstOfMonth.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7;
            var gridStart = firstOfMonth.AddDays(-leading);

            for (int row = 0; row < 6; ++row)
            {
                for (int column = 0; column < 7; ++column)
                {
                    int index = row * 7 + column;
                    var day = gridStart.AddDays(index);
                    var cellRect = new NowRect(inner.x + column * cell, gridTop + row * cell, cell, cell);

                    long dayTicks = day.Ticks;
                    bool disabled = state.hasRange && (dayTicks < state.minTicks || dayTicks > state.maxTicks);
                    int cellId = NowInput.CombineId(state.daySeed, index + 1);
                    var interaction = NowInput.Interact(cellId, cellRect);

                    if (interaction.clicked && !disabled)
                    {
                        ref var pending = ref NowControlState.Get<PendingDate>(state.pendingId, "pending-date");
                        pending.has = 1;
                        pending.ticks = dayTicks;
                        NowControlState.Get<bool>(state.id) = false;
                    }

                    float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

                    renderer.DrawCalendarDay(new NowCalendarDayRenderContext(
                        theme,
                        cellRect,
                        s_dayLabels[day.Day - 1],
                        day.Month == state.month,
                        state.hasToday && dayTicks == state.todayTicks,
                        state.hasSelected && dayTicks == state.selectedTicks,
                        disabled,
                        interaction,
                        focused: state.hasHighlight && dayTicks == state.highlightTicks,
                        hoverT));
                }
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

        /// <summary>
        /// Keyboard day navigation for the open popup: arrows move a
        /// popup-local highlighted day (never <see cref="NowFocus"/>), left and
        /// right step one day, up and down step a week, rolling the shown month
        /// at grid edges; submit commits the highlighted day. Base focus
        /// navigation is locked while the popup is open.
        /// </summary>
        static void UpdateKeyboard(PopupState state)
        {
            if (NowInput.isPassive)
                return;

            NowFocus.LockNavigation();

            var snapshot = NowInput.current;
            var navigation = snapshot.navigation;
            int dayStep = 0;

            if (NowControlState.Repeat(state.id, "nav-x", Mathf.Abs(navigation.x) > 0.55f, 0.35f, 0.12f))
                dayStep += navigation.x > 0f ? 1 : -1;

            if (NowControlState.Repeat(state.id, "nav-y", Mathf.Abs(navigation.y) > 0.55f, 0.35f, 0.12f))
                dayStep += navigation.y > 0f ? -7 : 7;

            if (dayStep != 0)
                MoveHighlight(state, dayStep);

            if (snapshot.submitPressed && snapshot.frame != state.openedFrame && state.hasHighlight)
                CommitHighlight(state);
        }

        static void MoveHighlight(PopupState state, int dayStep)
        {
            long ticks = state.hasHighlight ? state.highlightTicks : state.selectedTicks;
            long next = ticks + dayStep * TimeSpan.TicksPerDay;

            if (next < DateTime.MinValue.Ticks || next > DateTime.MaxValue.Date.Ticks)
                return;

            state.highlightTicks = next;
            state.hasHighlight = true;

            var day = new DateTime(next);
            ref var shown = ref NowControlState.Get<ShownMonth>(state.shownMonthId, "shown-month");

            if (day.Year != shown.year || day.Month != shown.month)
            {
                shown.year = day.Year;
                shown.month = day.Month;
                ClampShownMonth(ref shown);
            }

            NowControlState.RequestRepaint();
        }

        static void CommitHighlight(PopupState state)
        {
            long ticks = state.highlightTicks;

            if (state.hasRange && (ticks < state.minTicks || ticks > state.maxTicks))
                return;

            ref var pending = ref NowControlState.Get<PendingDate>(state.pendingId, "pending-date");
            pending.has = 1;
            pending.ticks = ticks;
            NowControlState.Get<bool>(state.id) = false;
        }

        static void DrawHeader(PopupState state, NowRect headerRect)
        {
            var theme = state.themeAsset;
            float buttonSize = headerRect.height;

            var prevRect = new NowRect(headerRect.x, headerRect.y, buttonSize, buttonSize);
            var nextRect = new NowRect(headerRect.xMax - buttonSize, headerRect.y, buttonSize, buttonSize);

            var prev = NowInput.Interact(state.prevId, prevRect);
            var next = NowInput.Interact(state.nextId, nextRect);

            ref var shown = ref NowControlState.Get<ShownMonth>(state.shownMonthId, "shown-month");

            if (prev.clicked)
            {
                StepMonth(ref shown, -1);
                NowControlState.RequestRepaint();
            }

            if (next.clicked)
            {
                StepMonth(ref shown, 1);
                NowControlState.RequestRepaint();
            }

            DrawHeaderButton(theme, prevRect, prev, left: true);
            DrawHeaderButton(theme, nextRect, next, left: false);

            if (state.cachedLabelYear != state.year || state.cachedLabelMonth != state.month || state.monthLabel == null)
            {
                state.cachedLabelYear = state.year;
                state.cachedLabelMonth = state.month;
                state.monthLabel = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} {1}",
                    CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(state.month),
                    state.year);
            }

            NowControls.DrawCenteredLabel(theme, headerRect, state.monthLabel, NowTextStyle.BodyStrong, headerRect);
        }

        static void DrawHeaderButton(NowThemeAsset theme, NowRect rect, in NowInteraction interaction, bool left)
        {
            if (interaction.hovered || interaction.held)
            {
                Now.Rectangle(rect.Inset(4f, 4f, 4f, 4f))
                    .SetRadius(6f)
                    .SetColor(interaction.held
                        ? theme.GetColor(NowColorToken.SurfacePressed)
                        : theme.GetColor(NowColorToken.SurfaceHover))
                    .Draw();
            }

            float extent = rect.height * 0.2f;
            Vector2 center = rect.center;
            float direction = left ? -1f : 1f;
            var tip = new Vector2(center.x + extent * 0.5f * direction, center.y);
            var top = new Vector2(center.x - extent * 0.5f * direction, center.y - extent);
            var bottom = new Vector2(center.x - extent * 0.5f * direction, center.y + extent);
            Color color = theme.GetColor(NowColorToken.TextMuted);

            Now.Line(top, tip).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
            Now.Line(tip, bottom).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
        }

        static void StepMonth(ref ShownMonth shown, int delta)
        {
            int month = shown.month + delta;

            if (month < 1)
            {
                month = 12;
                --shown.year;
            }
            else if (month > 12)
            {
                month = 1;
                ++shown.year;
            }

            shown.month = month;
            ClampShownMonth(ref shown);
        }

        /// <summary>
        /// Clamps the shown month to [0001-02 .. 9999-11]: the 42-day grid
        /// reaches into the neighbouring months, so the calendar for the very
        /// first and last month of the DateTime range would step outside it.
        /// </summary>
        static void ClampShownMonth(ref ShownMonth shown)
        {
            shown.month = Mathf.Clamp(shown.month, 1, 12);

            if (shown.year <= 1)
            {
                shown.year = 1;

                if (shown.month < 2)
                    shown.month = 2;
            }
            else if (shown.year >= 9999)
            {
                shown.year = 9999;

                if (shown.month > 11)
                    shown.month = 11;
            }
        }

        static void EnsureStaticLabels()
        {
            if (s_dayLabels == null)
            {
                s_dayLabels = new string[31];

                for (int i = 0; i < 31; ++i)
                    s_dayLabels[i] = (i + 1).ToString(CultureInfo.InvariantCulture);
            }

            if (s_weekdayLabels == null)
            {
                s_weekdayLabels = new string[7];
                var format = CultureInfo.CurrentCulture.DateTimeFormat;
                int first = (int)format.FirstDayOfWeek;

                for (int i = 0; i < 7; ++i)
                    s_weekdayLabels[i] = format.GetShortestDayName((DayOfWeek)((first + i) % 7));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            _fieldLabels.Clear();
            s_dayLabels = null;
            s_weekdayLabels = null;
        }
    }
}
