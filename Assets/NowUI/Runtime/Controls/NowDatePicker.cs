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
    /// Clicking the header label zooms out from days to a month grid and then
    /// a 12-year grid; picking a year or month drills back in. Day cells are
    /// keyboard-navigable inside the popup; the optional <see cref="SetToday"/>
    /// highlight is caller-passed — no hidden clock.
    /// </summary>
    [NowBuilder]
    public struct NowDatePicker
    {
        NowId _id;
        readonly int _site;

        const int PendingDateSeed = 0x4e445044;
        const int ShownMonthSeed = 0x4e44534d;
        const int CalendarViewSeed = 0x4e444356;

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
            public int monthSeed;
            public int yearSeed;
            public int prevId;
            public int nextId;
            public int labelId;
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
            public int cachedHeaderYear;
            public string yearHeaderLabel;
            public int cachedRangeStart;
            public string rangeLabel;
            public int cachedYearStart = -1;
            public readonly string[] yearLabels = new string[12];
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

        static string[] s_monthLabels;

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

            ref var pending = ref NowControlState.Get<PendingDate>(NowInput.CombineId(id, PendingDateSeed));
            bool changed = false;

            if (pending.has != 0)
            {
                var next = new DateTime(pending.ticks).Date + value.TimeOfDay;
                changed = next != value;
                value = next;
                pending = default;
            }

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float lineHeight = textStyle.font != null
                ? textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize
                : 20f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, renderer.MeasureDropdownField(theme, lineHeight));

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);
            ref var shown = ref NowControlState.Get<ShownMonth>(NowInput.CombineId(id, ShownMonthSeed));

            if (interaction.clicked || submitted)
            {
                open = !open;

                if (open)
                {
                    shown.year = value.Year;
                    shown.month = value.Month;
                    ClampShownMonth(ref shown);
                    NowControlState.Get<int>(NowInput.CombineId(id, CalendarViewSeed)) = 0;

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
            state.monthSeed = NowInput.GetId(id, "month");
            state.yearSeed = NowInput.GetId(id, "year");
            state.prevId = NowInput.GetId(id, "prev-month");
            state.nextId = NowInput.GetId(id, "next-month");
            state.labelId = NowInput.GetId(id, "header-label");
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

            ref int view = ref NowControlState.Get<int>(NowInput.CombineId(state.id, CalendarViewSeed));

            renderer.DrawPopupBackground(theme, popupRect, menu: false);
            UpdateKeyboard(state, ref view);

            float padding = styles.calendarPadding;
            float cell = styles.calendarCellSize;
            var inner = popupRect.Inset(padding);
            var headerRect = new NowRect(inner.x, inner.y, inner.width, styles.calendarHeaderHeight);

            DrawHeader(state, headerRect, ref view);

            EnsureStaticLabels();

            if (view == 0)
                DrawDayGrid(state, inner, headerRect.yMax, cell);
            else if (view == 1)
                DrawMonthGrid(state, inner, headerRect.yMax, ref view);
            else
                DrawYearGrid(state, inner, headerRect.yMax, ref view);

            var snapshot = NowInput.current;
            bool fieldPressClaimedByField = state.field.Contains(snapshot.pointerPosition) &&
                NowInput.activeId == state.id;
            bool pressedOutside = snapshot.anyPointerPressed &&
                !NowOverlay.IsPointerInsideOverlayTree(state.id, snapshot.pointerPosition) &&
                !fieldPressClaimedByField;
            bool cancelled = snapshot.cancelPressed && !NowInput.cancelConsumed && !NowOverlay.HasNestedOverlay(state.id);

            if (cancelled && view > 0)
            {
                --view;
                NowControlState.RequestRepaint();
            }
            else if (pressedOutside || cancelled)
            {
                NowControlState.Get<bool>(state.id) = false;
            }
        }

        static void DrawDayGrid(PopupState state, NowRect inner, float gridTop, float cell)
        {
            var theme = state.themeAsset;
            var renderer = theme.controlRenderer;

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
                        ref var pending = ref NowControlState.Get<PendingDate>(NowInput.CombineId(state.pendingId, PendingDateSeed));
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
        }

        static void DrawMonthGrid(PopupState state, NowRect inner, float gridTop, ref int view)
        {
            var theme = state.themeAsset;
            var renderer = theme.controlRenderer;
            float columnWidth = inner.width / 3f;
            float rowHeight = (inner.yMax - gridTop) / 4f;
            var selected = new DateTime(state.selectedTicks);
            var today = new DateTime(state.todayTicks);

            for (int month = 1; month <= 12; ++month)
            {
                int index = month - 1;
                var cellRect = new NowRect(
                    inner.x + (index % 3) * columnWidth,
                    gridTop + (index / 3) * rowHeight,
                    columnWidth,
                    rowHeight);

                bool disabled = MonthDisabled(state, state.year, month);
                int cellId = NowInput.CombineId(state.monthSeed, month);
                var interaction = NowInput.Interact(cellId, cellRect);

                if (interaction.clicked && !disabled)
                {
                    ref var shown = ref NowControlState.Get<ShownMonth>(NowInput.CombineId(state.shownMonthId, ShownMonthSeed));
                    shown.year = state.year;
                    shown.month = month;
                    ClampShownMonth(ref shown);
                    view = 0;
                    NowControlState.RequestRepaint();
                }

                float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

                renderer.DrawCalendarDay(new NowCalendarDayRenderContext(
                    theme,
                    cellRect,
                    s_monthLabels[index],
                    inMonth: true,
                    isToday: state.hasToday && today.Year == state.year && today.Month == month,
                    selected: state.hasSelected && selected.Year == state.year && selected.Month == month,
                    disabled,
                    interaction,
                    focused: month == state.month,
                    hoverT));
            }
        }

        static void DrawYearGrid(PopupState state, NowRect inner, float gridTop, ref int view)
        {
            var theme = state.themeAsset;
            var renderer = theme.controlRenderer;
            float columnWidth = inner.width / 3f;
            float rowHeight = (inner.yMax - gridTop) / 4f;
            int start = YearPageStart(state.year);
            var selected = new DateTime(state.selectedTicks);
            var today = new DateTime(state.todayTicks);

            EnsureYearLabels(state, start);

            for (int index = 0; index < 12; ++index)
            {
                int year = start + index;

                if (year > 9999)
                    break;

                var cellRect = new NowRect(
                    inner.x + (index % 3) * columnWidth,
                    gridTop + (index / 3) * rowHeight,
                    columnWidth,
                    rowHeight);

                bool disabled = YearDisabled(state, year);
                int cellId = NowInput.CombineId(state.yearSeed, index + 1);
                var interaction = NowInput.Interact(cellId, cellRect);

                if (interaction.clicked && !disabled)
                {
                    ref var shown = ref NowControlState.Get<ShownMonth>(NowInput.CombineId(state.shownMonthId, ShownMonthSeed));
                    shown.year = year;
                    ClampShownMonth(ref shown);
                    view = 1;
                    NowControlState.RequestRepaint();
                }

                float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

                renderer.DrawCalendarDay(new NowCalendarDayRenderContext(
                    theme,
                    cellRect,
                    state.yearLabels[index],
                    inMonth: true,
                    isToday: state.hasToday && today.Year == year,
                    selected: state.hasSelected && selected.Year == year,
                    disabled,
                    interaction,
                    focused: year == state.year,
                    hoverT));
            }
        }

        static bool MonthDisabled(PopupState state, int year, int month)
        {
            if (!state.hasRange)
                return false;

            long start = new DateTime(year, month, 1).Ticks;
            long end = new DateTime(year, month, DateTime.DaysInMonth(year, month)).Ticks;
            return end < state.minTicks || start > state.maxTicks;
        }

        static bool YearDisabled(PopupState state, int year)
        {
            if (!state.hasRange)
                return false;

            return new DateTime(year, 12, 31).Ticks < state.minTicks ||
                new DateTime(year, 1, 1).Ticks > state.maxTicks;
        }

        static int YearPageStart(int year)
        {
            return year - ((year - 1) % 12);
        }

        static void EnsureYearLabels(PopupState state, int start)
        {
            if (state.cachedYearStart == start)
                return;

            state.cachedYearStart = start;

            for (int i = 0; i < 12; ++i)
            {
                int year = start + i;
                state.yearLabels[i] = year <= 9999 ? year.ToString(CultureInfo.InvariantCulture) : string.Empty;
            }
        }

        /// <summary>
        /// Keyboard navigation for the open popup (never <see cref="NowFocus"/>;
        /// base focus navigation is locked while it is open). In the day view
        /// arrows move a popup-local highlighted day — left and right step one
        /// day, up and down step a week, rolling the shown month at grid edges —
        /// and submit commits it. In the month and year views arrows move the
        /// shown month or year through the 3-wide grid and submit zooms back in.
        /// </summary>
        static void UpdateKeyboard(PopupState state, ref int view)
        {
            if (NowInput.isPassive)
                return;

            NowFocus.LockNavigation();

            var snapshot = NowInput.current;
            var navigation = snapshot.navigation;

            if (view == 0)
            {
                int dayStep = 0;

                if (NowControlState.Repeat(state.id, "nav-x", Mathf.Abs(navigation.x) > 0.55f, 0.35f, 0.12f))
                    dayStep += navigation.x > 0f ? 1 : -1;

                if (NowControlState.Repeat(state.id, "nav-y", Mathf.Abs(navigation.y) > 0.55f, 0.35f, 0.12f))
                    dayStep += navigation.y > 0f ? -7 : 7;

                if (dayStep != 0)
                    MoveHighlight(state, dayStep);

                if (snapshot.submitPressed && snapshot.frame != state.openedFrame && state.hasHighlight)
                    CommitHighlight(state);

                return;
            }

            int step = 0;

            if (NowControlState.Repeat(state.id, "nav-x", Mathf.Abs(navigation.x) > 0.55f, 0.35f, 0.12f))
                step += navigation.x > 0f ? 1 : -1;

            if (NowControlState.Repeat(state.id, "nav-y", Mathf.Abs(navigation.y) > 0.55f, 0.35f, 0.12f))
                step += navigation.y > 0f ? -3 : 3;

            if (step != 0)
            {
                ref var shown = ref NowControlState.Get<ShownMonth>(NowInput.CombineId(state.shownMonthId, ShownMonthSeed));

                if (view == 1)
                    StepMonths(ref shown, step);
                else
                    StepYears(ref shown, step);

                NowControlState.RequestRepaint();
            }

            if (snapshot.submitPressed && snapshot.frame != state.openedFrame)
            {
                --view;
                NowControlState.RequestRepaint();
            }
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
            ref var shown = ref NowControlState.Get<ShownMonth>(NowInput.CombineId(state.shownMonthId, ShownMonthSeed));

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

            ref var pending = ref NowControlState.Get<PendingDate>(NowInput.CombineId(state.pendingId, PendingDateSeed));
            pending.has = 1;
            pending.ticks = ticks;
            NowControlState.Get<bool>(state.id) = false;
        }

        static void DrawHeader(PopupState state, NowRect headerRect, ref int view)
        {
            var theme = state.themeAsset;
            float buttonSize = headerRect.height;

            var prevRect = new NowRect(headerRect.x, headerRect.y, buttonSize, buttonSize);
            var nextRect = new NowRect(headerRect.xMax - buttonSize, headerRect.y, buttonSize, buttonSize);

            var prev = NowInput.Interact(state.prevId, prevRect);
            var next = NowInput.Interact(state.nextId, nextRect);

            ref var shown = ref NowControlState.Get<ShownMonth>(NowInput.CombineId(state.shownMonthId, ShownMonthSeed));
            int arrowStep = view == 0 ? 1 : view == 1 ? 12 : 144;

            if (prev.clicked)
            {
                StepMonths(ref shown, -arrowStep);
                NowControlState.RequestRepaint();
            }

            if (next.clicked)
            {
                StepMonths(ref shown, arrowStep);
                NowControlState.RequestRepaint();
            }

            DrawHeaderButton(theme, prevRect, prev, left: true);
            DrawHeaderButton(theme, nextRect, next, left: false);

            var labelRect = new NowRect(prevRect.xMax, headerRect.y, nextRect.x - prevRect.xMax, headerRect.height);
            var label = NowInput.Interact(state.labelId, labelRect);

            if (label.clicked && view < 2)
            {
                ++view;
                NowControlState.RequestRepaint();
            }

            if (view < 2 && (label.hovered || label.held))
            {
                Now.Rectangle(labelRect.Inset(2f, 4f, 2f, 4f))
                    .SetRadius(6f)
                    .SetColor(label.held
                        ? theme.GetColor(NowColorToken.SurfacePressed)
                        : theme.GetColor(NowColorToken.SurfaceHover))
                    .Draw();
            }

            NowControls.DrawCenteredLabel(theme, headerRect, HeaderLabel(state, view), NowTextStyle.BodyStrong, headerRect);
        }

        static string HeaderLabel(PopupState state, int view)
        {
            if (view == 0)
            {
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

                return state.monthLabel;
            }

            if (view == 1)
            {
                if (state.cachedHeaderYear != state.year || state.yearHeaderLabel == null)
                {
                    state.cachedHeaderYear = state.year;
                    state.yearHeaderLabel = state.year.ToString(CultureInfo.InvariantCulture);
                }

                return state.yearHeaderLabel;
            }

            int start = YearPageStart(state.year);

            if (state.cachedRangeStart != start || state.rangeLabel == null)
            {
                state.cachedRangeStart = start;
                state.rangeLabel = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} – {1}",
                    start,
                    Mathf.Min(start + 11, 9999));
            }

            return state.rangeLabel;
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

        static void StepMonths(ref ShownMonth shown, int delta)
        {
            int index = shown.year * 12 + (shown.month - 1) + delta;
            index = Mathf.Clamp(index, 12, 9999 * 12 + 11);
            shown.year = index / 12;
            shown.month = index % 12 + 1;
            ClampShownMonth(ref shown);
        }

        static void StepYears(ref ShownMonth shown, int delta)
        {
            shown.year = Mathf.Clamp(shown.year + delta, 1, 9999);
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

            if (s_monthLabels == null)
            {
                s_monthLabels = new string[12];
                var format = CultureInfo.CurrentCulture.DateTimeFormat;

                for (int i = 0; i < 12; ++i)
                    s_monthLabels[i] = format.GetAbbreviatedMonthName(i + 1);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _popupStates.Clear();
            _fieldLabels.Clear();
            s_dayLabels = null;
            s_weekdayLabels = null;
            s_monthLabels = null;
        }
    }
}
