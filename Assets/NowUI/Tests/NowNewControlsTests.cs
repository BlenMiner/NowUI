using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// New-control tests (switch, progress, badge/chip, tooltip, spinner, tabs,
/// split view, tree view, combo box, date/time pickers), driven through fake
/// input providers like the other control fixtures.
/// </summary>
public class NowNewControlsTests
{
    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowTextInputSource
    {
        public NowTextInputFrame frame;

        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    sealed class RecordingRenderer : NowControlRenderer
    {
        public readonly List<float> progressPhases = new List<float>();
        public int tooltips;
        public readonly Dictionary<string, NowRect> rowRects = new Dictionary<string, NowRect>();
        public readonly Dictionary<string, NowRect> disclosureRects = new Dictionary<string, NowRect>();

        public override void DrawProgressBar(in NowProgressBarRenderContext context)
        {
            progressPhases.Add(context.phase01);
            base.DrawProgressBar(context);
        }

        public override void DrawTooltip(in NowTooltipRenderContext context)
        {
            ++tooltips;
            base.DrawTooltip(context);
        }

        public override void DrawTreeRow(in NowTreeRowRenderContext context)
        {
            rowRects[context.label] = context.rect;
            disclosureRects[context.label] = context.disclosureRect;
            base.DrawTreeRow(context);
        }
    }

    static void SetRenderer(NowThemeAsset theme, NowControlRenderer renderer)
    {
        typeof(NowThemeAsset)
            .GetField("_controlRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(theme, renderer);
    }

    static readonly Vector2 Surface = new Vector2(600, 600);
    static readonly NowRect SwitchRect = new NowRect(20, 20, 160, 28);
    static readonly NowRect ChipRect = new NowRect(20, 20, 140, 28);
    static readonly NowRect TooltipAnchorRect = new NowRect(50, 50, 100, 30);
    static readonly NowRect SpinnerFieldRect = new NowRect(20, 20, 180, 32);
    static readonly NowRect TabsRect = new NowRect(10, 10, 400, 40);
    static readonly NowRect SplitRect = new NowRect(10, 10, 310, 120);
    static readonly NowRect ComboRect = new NowRect(20, 20, 180, 36);
    static readonly NowRect DateRect = new NowRect(20, 20, 200, 36);
    static readonly NowRect TimeRect = new NowRect(20, 20, 200, 36);
    static readonly string[] TabLabels = { "One", "Two", "Three" };
    static readonly List<string> ComboOptions = new List<string> { "Low", "Medium", "High" };

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowTextInput.Reset();
        NowLayout.Reset();
        NowTooltip.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowTextInput.source = _keyboard;
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        NowTooltip.Reset();
    }

    static NowInputSnapshot TimedPointer(Vector2 position, float time, int frame)
    {
        return new NowInputSnapshot(
            true, position, position, Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false, false, false, false,
            frame, time);
    }

    static NowInputSnapshot SubmitSnapshot()
    {
        return new NowInputSnapshot(
            true, new Vector2(500, 500), new Vector2(500, 500), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 1f);
    }

    bool DrawSwitchFrame(ref bool value)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.Switch(SwitchRect, "Enable").SetId("switch").Draw(ref value);
    }

    bool DrawChipFrame(out bool removed)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.Chip(ChipRect, "Tag").SetId("chip").SetRemovable().Draw(out removed);
    }

    bool DrawPlainChipFrame()
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.Chip(ChipRect, "Tag").SetId("plain-chip").Draw();
    }

    void DrawTooltipFrame(Vector2 pointer, float time, int frame)
    {
        _pointer.snapshot = TimedPointer(pointer, time, frame);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowTooltip.For(4242, TooltipAnchorRect, "Hint");
            NowOverlay.Flush();
        }
    }

    void DrawProgressFrame(float time)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Now.ProgressBar(new NowRect(20, 20, 200, 8))
                .SetId("progress")
                .SetIndeterminate()
                .SetTime(time)
                .Draw();
        }
    }

    bool DrawSpinnerFieldFrame(ref float value)
    {
        _keyboard.frame = default;
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            return Now.TextField(SpinnerFieldRect, "spin")
                .SetSpinner(2f)
                .SetRange(0f, 10f)
                .Draw(ref value);
        }
    }

    bool DrawTabsFrame(ref int selected)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            return Now.TabBar(TabsRect, TabLabels).SetId("tabs").Draw(ref selected);
    }

    float DrawSplitFrame(float ratio, float minFirst = 48f, float minSecond = 48f)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.SplitView(SplitRect).SetId("split").SetMinPaneSize(minFirst, minSecond).Begin(ref ratio))
        {
        }

        return ratio;
    }

    (bool activated, bool selectionChanged) DrawTreeFrame(NowTreeViewState state, RecordingRenderer renderer)
    {
        renderer.rowRects.Clear();
        renderer.disclosureRects.Clear();
        bool activated = false;
        bool selectionChanged = false;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (NowLayout.Area(new Vector4(0f, 0f, 320f, 240f)))
        using (var tree = NowLayout.TreeView(state).SetId("tree").Begin())
        {
            if (tree.BeginNode("Root"))
            {
                activated = tree.Node("Leaf");
                tree.EndNode();
            }

            selectionChanged = tree.selectionChanged;
        }

        return (activated, selectionChanged);
    }

    bool DrawComboFrame(ref int selected)
    {
        _keyboard.frame = default;
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.ComboBox(ComboRect, ComboOptions).SetId("combo").Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    bool DrawDateFrame(ref DateTime value, DateTime? rangeMin = null, DateTime? rangeMax = null)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var picker = Now.DatePicker(DateRect).SetId("date");

            if (rangeMin.HasValue)
                picker = picker.SetRange(rangeMin.Value, rangeMax.Value);

            bool changed = picker.Draw(ref value);
            NowOverlay.Flush();
            return changed;
        }
    }

    bool DrawTimeFrame(ref TimeSpan value)
    {
        _keyboard.frame = default;
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.TimePicker(TimeRect).SetId("time").Draw(ref value);
            NowOverlay.Flush();
            return changed;
        }
    }

    void ClickAt(Vector2 point, Func<bool> drawFrame, out bool pressResult, out bool releaseResult)
    {
        _pointer.snapshot = new NowInputSnapshot(point, true, true, false);
        pressResult = drawFrame();
        _pointer.snapshot = new NowInputSnapshot(point, false, false, true);
        releaseResult = drawFrame();
    }

    static NowRect CalendarPopupRect(NowRect field)
    {
        var styles = NowTheme.themeAsset.controlStyles;
        float cell = styles.calendarCellSize;
        float padding = styles.calendarPadding;
        return new NowRect(
            field.x,
            field.yMax + styles.dropdownPopupGap,
            cell * 7f + padding * 2f,
            styles.calendarHeaderHeight + cell * 7f + padding * 2f);
    }

    static NowRect CalendarDayCellRect(NowRect field, int shownYear, int shownMonth, DateTime day)
    {
        var styles = NowTheme.themeAsset.controlStyles;
        float cell = styles.calendarCellSize;
        var inner = CalendarPopupRect(field).Inset(styles.calendarPadding);
        float gridTop = inner.y + styles.calendarHeaderHeight + cell * 0.8f;

        var firstOfMonth = new DateTime(shownYear, shownMonth, 1);
        int leading = ((int)firstOfMonth.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7;
        var gridStart = firstOfMonth.AddDays(-leading);
        int index = (int)(day.Date - gridStart).TotalDays;

        return new NowRect(
            inner.x + (index % 7) * cell,
            gridTop + (index / 7) * cell,
            cell,
            cell);
    }

    static NowRect CalendarPrevButtonRect(NowRect field)
    {
        var styles = NowTheme.themeAsset.controlStyles;
        var inner = CalendarPopupRect(field).Inset(styles.calendarPadding);
        return new NowRect(inner.x, inner.y, styles.calendarHeaderHeight, styles.calendarHeaderHeight);
    }

    static NowRect TimePickerHourUpRect(NowRect field)
    {
        var styles = NowTheme.themeAsset.controlStyles;
        float width = Mathf.Max(field.width, 200f);
        float height = styles.dropdownFieldMinHeight + styles.popupPadding * 2f + 16f;
        var popup = new NowRect(field.x, field.yMax + styles.dropdownPopupGap, width, height);
        var inner = popup.Inset(styles.popupPadding + 4f);
        float fieldHeight = Mathf.Max(styles.dropdownFieldMinHeight, 32f);
        float fieldWidth = (inner.width - 14f) * 0.5f;
        var hourRect = new NowRect(inner.x, inner.y, fieldWidth, fieldHeight);

        return new NowRect(
            hourRect.xMax - styles.spinnerButtonWidth - 1f,
            hourRect.y + 1f,
            styles.spinnerButtonWidth,
            hourRect.height * 0.5f - 1f);
    }

    [Test]
    public void SwitchClickTogglesValueAndReportsChangeOnce()
    {
        bool value = false;
        Vector2 inside = new Vector2(40, 34);

        _pointer.snapshot = new NowInputSnapshot(inside, true, true, false);
        Assert.IsFalse(DrawSwitchFrame(ref value));
        Assert.IsFalse(value);

        _pointer.snapshot = new NowInputSnapshot(inside, false, false, true);
        Assert.IsTrue(DrawSwitchFrame(ref value));
        Assert.IsTrue(value);

        _pointer.snapshot = new NowInputSnapshot(inside, false, false, false);
        Assert.IsFalse(DrawSwitchFrame(ref value));
        Assert.IsTrue(value);
        Assert.IsTrue(_drawList.hasGeometry, "Switch drew no visuals.");
    }

    [Test]
    public void SwitchSubmitWhileFocusedToggles()
    {
        bool value = false;
        int id;

        using (NowInput.Begin(_pointer, Surface))
            id = NowControls.GetControlId("switch");

        NowFocus.Focus(id);
        _pointer.snapshot = SubmitSnapshot();

        Assert.IsTrue(DrawSwitchFrame(ref value));
        Assert.IsTrue(value);
    }

    [Test]
    public void ProgressBarIndeterminatePhaseIsDeterministicFromCallerTime()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            using (NowTheme.Scope(theme))
            {
                DrawProgressFrame(0.9f);
                DrawProgressFrame(0.9f);
                DrawProgressFrame(0.3f);
            }

            Assert.AreEqual(3, renderer.progressPhases.Count);

            float period = theme.controlStyles.progressBarPeriod;
            float expected = Mathf.Repeat(0.9f / period, 1f);

            Assert.AreEqual(expected, renderer.progressPhases[0], 0.0001f);
            Assert.AreEqual(renderer.progressPhases[0], renderer.progressPhases[1], 0.0001f,
                "Same caller time must produce the same sweep phase.");
            Assert.AreEqual(Mathf.Repeat(0.3f / period, 1f), renderer.progressPhases[2], 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void BadgeDrawsWithoutInteraction()
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Badge(new NowRect(20, 20, 40, 20), "3").Draw();

        Assert.IsTrue(_drawList.hasGeometry, "Badge drew no visuals.");
    }

    [Test]
    public void ChipClickReportsSelection()
    {
        Vector2 inside = new Vector2(40, 34);

        bool pressResult, releaseResult;
        ClickAt(inside, DrawPlainChipFrame, out pressResult, out releaseResult);

        Assert.IsFalse(pressResult);
        Assert.IsTrue(releaseResult);
    }

    [Test]
    public void ChipRemoveClickReportsRemoved()
    {
        var theme = NowTheme.themeAsset;
        var removeCenter = theme.controlRenderer.ChipRemoveRect(theme, ChipRect).center;
        bool removed = false;

        _pointer.snapshot = new NowInputSnapshot(removeCenter, true, true, false);
        Assert.IsFalse(DrawChipFrame(out removed));
        Assert.IsFalse(removed);

        _pointer.snapshot = new NowInputSnapshot(removeCenter, false, false, true);
        Assert.IsTrue(DrawChipFrame(out removed));
        Assert.IsTrue(removed, "Clicking the remove glyph must report removal.");
    }

    [Test]
    public void TooltipAppearsOnlyAfterHoverDelay()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            float delay = theme.controlStyles.tooltipDelay;
            var hover = new Vector2(100, 65);

            using (NowTheme.Scope(theme))
            {
                DrawTooltipFrame(hover, 1f, 1);
                Assert.AreEqual(0, renderer.tooltips, "Tooltip must wait for the hover delay.");

                DrawTooltipFrame(hover, 1f + delay + 0.05f, 2);
                Assert.AreEqual(1, renderer.tooltips);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void TooltipHoverStateResetsWhenPointerLeaves()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            float delay = theme.controlStyles.tooltipDelay;
            var hover = new Vector2(100, 65);
            var outside = new Vector2(400, 400);

            using (NowTheme.Scope(theme))
            {
                DrawTooltipFrame(hover, 1f, 1);
                DrawTooltipFrame(outside, 1.2f, 2);

                DrawTooltipFrame(hover, 1.3f, 3);
                DrawTooltipFrame(hover, 1.3f + delay - 0.1f, 4);
                Assert.AreEqual(0, renderer.tooltips, "Leaving the rect must restart the hover delay.");

                DrawTooltipFrame(hover, 1.3f + delay + 0.05f, 5);
                Assert.AreEqual(1, renderer.tooltips);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void TextFieldSpinnerStepsValueByStep()
    {
        var styles = NowTheme.themeAsset.controlStyles;
        float width = styles.spinnerButtonWidth;
        var upCenter = new Vector2(
            SpinnerFieldRect.xMax - 1f - width * 0.5f,
            SpinnerFieldRect.y + SpinnerFieldRect.height * 0.25f);
        var downCenter = new Vector2(
            SpinnerFieldRect.xMax - 1f - width * 0.5f,
            SpinnerFieldRect.y + SpinnerFieldRect.height * 0.75f);
        float value = 5f;

        _pointer.snapshot = new NowInputSnapshot(upCenter, true, true, false);
        Assert.IsTrue(DrawSpinnerFieldFrame(ref value));
        Assert.AreEqual(7f, value, 0.0001f);

        _pointer.snapshot = new NowInputSnapshot(upCenter, false, false, true);
        Assert.IsFalse(DrawSpinnerFieldFrame(ref value));
        Assert.AreEqual(7f, value, 0.0001f);

        _pointer.snapshot = new NowInputSnapshot(downCenter, true, true, false);
        Assert.IsTrue(DrawSpinnerFieldFrame(ref value));
        Assert.AreEqual(5f, value, 0.0001f);

        _pointer.snapshot = new NowInputSnapshot(downCenter, false, false, true);
        Assert.IsFalse(DrawSpinnerFieldFrame(ref value));
        Assert.AreEqual(5f, value, 0.0001f);
    }

    [Test]
    public void TextFieldSpinnerClampsToRange()
    {
        var styles = NowTheme.themeAsset.controlStyles;
        var upCenter = new Vector2(
            SpinnerFieldRect.xMax - 1f - styles.spinnerButtonWidth * 0.5f,
            SpinnerFieldRect.y + SpinnerFieldRect.height * 0.25f);
        float value = 9f;

        _pointer.snapshot = new NowInputSnapshot(upCenter, true, true, false);
        Assert.IsTrue(DrawSpinnerFieldFrame(ref value));
        Assert.AreEqual(10f, value, 0.0001f);

        _pointer.snapshot = new NowInputSnapshot(upCenter, false, false, true);
        DrawSpinnerFieldFrame(ref value);

        _pointer.snapshot = new NowInputSnapshot(upCenter, true, true, false);
        Assert.IsFalse(DrawSpinnerFieldFrame(ref value), "Stepping past the range must not report a change.");
        Assert.AreEqual(10f, value, 0.0001f);
    }

    [Test]
    public void TabBarClickSelectsSecondTab()
    {
        var theme = NowTheme.themeAsset;
        float firstWidth = theme.controlRenderer.MeasureTab(theme, TabLabels[0]).x;
        float secondWidth = theme.controlRenderer.MeasureTab(theme, TabLabels[1]).x;
        var secondTabCenter = new Vector2(
            TabsRect.x + firstWidth + theme.controlStyles.tabSpacing + secondWidth * 0.5f,
            TabsRect.y + TabsRect.height * 0.5f);
        int selected = 0;

        _pointer.snapshot = new NowInputSnapshot(secondTabCenter, true, true, false);
        Assert.IsFalse(DrawTabsFrame(ref selected));

        _pointer.snapshot = new NowInputSnapshot(secondTabCenter, false, false, true);
        Assert.IsTrue(DrawTabsFrame(ref selected), "Clicking the second tab must report a change.");
        Assert.AreEqual(1, selected);

        _pointer.snapshot = new NowInputSnapshot(secondTabCenter, false, false, false);
        Assert.IsFalse(DrawTabsFrame(ref selected));
        Assert.AreEqual(1, selected);
    }

    [Test]
    public void SplitViewDividerDragChangesRatio()
    {
        var styles = NowTheme.themeAsset.controlStyles;
        float usable = SplitRect.width - styles.splitterThickness;
        var dividerCenter = new Vector2(
            SplitRect.x + usable * 0.5f + styles.splitterThickness * 0.5f,
            SplitRect.y + SplitRect.height * 0.5f);
        var dragPoint = new Vector2(245f, dividerCenter.y);
        float expected = (dragPoint.x - SplitRect.x - styles.splitterThickness * 0.5f) / usable;
        float ratio = 0.5f;

        _pointer.snapshot = new NowInputSnapshot(dividerCenter, true, true, false);
        ratio = DrawSplitFrame(ratio);

        _pointer.snapshot = new NowInputSnapshot(dragPoint, dragPoint - dividerCenter, true, false, false);
        ratio = DrawSplitFrame(ratio);

        _pointer.snapshot = new NowInputSnapshot(dragPoint, false, false, true);
        ratio = DrawSplitFrame(ratio);

        Assert.AreEqual(expected, ratio, 0.02f);
    }

    [Test]
    public void SplitViewDragClampsToMinPaneSizes()
    {
        var styles = NowTheme.themeAsset.controlStyles;
        float usable = SplitRect.width - styles.splitterThickness;
        var dividerCenter = new Vector2(
            SplitRect.x + usable * 0.5f + styles.splitterThickness * 0.5f,
            SplitRect.y + SplitRect.height * 0.5f);
        var dragPoint = new Vector2(SplitRect.xMax - 10f, dividerCenter.y);
        float maxRatio = 1f - 120f / usable;
        float ratio = 0.5f;

        _pointer.snapshot = new NowInputSnapshot(dividerCenter, true, true, false);
        ratio = DrawSplitFrame(ratio, 120f, 120f);

        _pointer.snapshot = new NowInputSnapshot(dragPoint, dragPoint - dividerCenter, true, false, false);
        ratio = DrawSplitFrame(ratio, 120f, 120f);

        _pointer.snapshot = new NowInputSnapshot(dragPoint, false, false, true);
        ratio = DrawSplitFrame(ratio, 120f, 120f);

        Assert.AreEqual(maxRatio, ratio, 0.01f, "Drag must clamp to the second pane's minimum size.");
    }

    [Test]
    public void TreeViewCollapsedNodeHidesChildren()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var state = new NowTreeViewState();

            using (NowTheme.Scope(theme))
            {
                _pointer.snapshot = default;
                DrawTreeFrame(state, renderer);
                DrawTreeFrame(state, renderer);
            }

            Assert.IsTrue(renderer.rowRects.ContainsKey("Root"));
            Assert.IsFalse(renderer.rowRects.ContainsKey("Leaf"), "Collapsed children must not be drawn.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void TreeViewDisclosureClickExpandsWithoutSelecting()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var state = new NowTreeViewState();

            using (NowTheme.Scope(theme))
            {
                _pointer.snapshot = default;
                DrawTreeFrame(state, renderer);
                DrawTreeFrame(state, renderer);

                Assert.Greater(renderer.rowRects["Root"].width, 0f);
                var disclosureCenter = renderer.disclosureRects["Root"].center;

                _pointer.snapshot = new NowInputSnapshot(disclosureCenter, true, true, false);
                DrawTreeFrame(state, renderer);
                _pointer.snapshot = new NowInputSnapshot(disclosureCenter, false, false, true);
                DrawTreeFrame(state, renderer);

                _pointer.snapshot = default;
                DrawTreeFrame(state, renderer);
            }

            Assert.IsTrue(renderer.rowRects.ContainsKey("Leaf"), "Disclosure click must expand the node.");
            Assert.AreEqual(0, state.selectedId, "Toggling the disclosure must not change the selection.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void TreeViewLeafClickSelectsRow()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var state = new NowTreeViewState();

            using (NowTheme.Scope(theme))
            {
                _pointer.snapshot = default;
                DrawTreeFrame(state, renderer);
                DrawTreeFrame(state, renderer);

                var disclosureCenter = renderer.disclosureRects["Root"].center;
                _pointer.snapshot = new NowInputSnapshot(disclosureCenter, true, true, false);
                DrawTreeFrame(state, renderer);
                _pointer.snapshot = new NowInputSnapshot(disclosureCenter, false, false, true);
                DrawTreeFrame(state, renderer);

                _pointer.snapshot = default;
                DrawTreeFrame(state, renderer);

                Assert.IsTrue(renderer.rowRects.ContainsKey("Leaf"));
                var leafCenter = renderer.rowRects["Leaf"].center;

                _pointer.snapshot = new NowInputSnapshot(leafCenter, true, true, false);
                DrawTreeFrame(state, renderer);
                _pointer.snapshot = new NowInputSnapshot(leafCenter, false, false, true);
                var (activated, selectionChanged) = DrawTreeFrame(state, renderer);

                Assert.IsTrue(activated, "Clicking a leaf must report activation.");
                Assert.IsTrue(selectionChanged);
                Assert.AreNotEqual(0, state.selectedId);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ComboBoxClickOpensPopup()
    {
        int selected = 0;
        var fieldCenter = ComboRect.center;

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, true, true, false);
        Assert.IsFalse(DrawComboFrame(ref selected));

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, false, false, true);
        Assert.IsFalse(DrawComboFrame(ref selected));

        int id;

        using (NowInput.Begin(_pointer, Surface))
            id = NowControls.GetControlId("combo");

        Assert.IsTrue(NowControlState.Get<bool>(id), "Combo box should open after a click.");
    }

    [Test]
    public void ComboBoxPopupItemClickAppliesSelectionNextFrame()
    {
        var styles = NowTheme.themeAsset.controlStyles;
        int selected = 0;
        var fieldCenter = ComboRect.center;

        var popupRect = new NowRect(
            ComboRect.x,
            ComboRect.yMax + styles.dropdownPopupGap,
            ComboRect.width,
            ComboOptions.Count * styles.dropdownItemHeight + styles.popupPadding * 2f);
        var itemArea = popupRect.Inset(styles.popupPadding);
        var mediumCenter = new Vector2(
            itemArea.x + itemArea.width * 0.5f,
            itemArea.y + styles.dropdownItemHeight * 1.5f);

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, true, true, false);
        DrawComboFrame(ref selected);
        _pointer.snapshot = new NowInputSnapshot(fieldCenter, false, false, true);
        DrawComboFrame(ref selected);

        _pointer.snapshot = new NowInputSnapshot(mediumCenter, false, false, false);
        Assert.IsFalse(DrawComboFrame(ref selected));

        _pointer.snapshot = new NowInputSnapshot(mediumCenter, true, true, false);
        Assert.IsFalse(DrawComboFrame(ref selected));

        _pointer.snapshot = new NowInputSnapshot(mediumCenter, false, false, true);
        Assert.IsFalse(DrawComboFrame(ref selected));

        _pointer.snapshot = new NowInputSnapshot(mediumCenter, false, false, false);
        Assert.IsTrue(DrawComboFrame(ref selected), "Pending popup selection must apply on the next Draw.");
        Assert.AreEqual(1, selected);
    }

    [Test]
    public void DatePickerDayClickAppliesDatePreservingTimeOfDay()
    {
        var value = new DateTime(2026, 6, 15, 10, 30, 0);
        var target = new DateTime(2026, 6, 20);
        var fieldCenter = DateRect.center;
        var cellCenter = CalendarDayCellRect(DateRect, 2026, 6, target).center;

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, true, true, false);
        Assert.IsFalse(DrawDateFrame(ref value));
        _pointer.snapshot = new NowInputSnapshot(fieldCenter, false, false, true);
        Assert.IsFalse(DrawDateFrame(ref value));

        _pointer.snapshot = new NowInputSnapshot(cellCenter, true, true, false);
        Assert.IsFalse(DrawDateFrame(ref value));
        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, true);
        Assert.IsFalse(DrawDateFrame(ref value));

        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, false);
        Assert.IsTrue(DrawDateFrame(ref value), "Pending day selection must apply on the next Draw.");
        Assert.AreEqual(target, value.Date);
        Assert.AreEqual(new TimeSpan(10, 30, 0), value.TimeOfDay, "Picking a day must preserve the time of day.");
    }

    [Test]
    public void DatePickerMonthNavigationSelectsDayInPreviousMonth()
    {
        var value = new DateTime(2026, 6, 15, 8, 45, 0);
        var target = new DateTime(2026, 5, 10);
        var fieldCenter = DateRect.center;
        var prevCenter = CalendarPrevButtonRect(DateRect).center;
        var cellCenter = CalendarDayCellRect(DateRect, 2026, 5, target).center;

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, true, true, false);
        DrawDateFrame(ref value);
        _pointer.snapshot = new NowInputSnapshot(fieldCenter, false, false, true);
        DrawDateFrame(ref value);

        _pointer.snapshot = new NowInputSnapshot(prevCenter, true, true, false);
        Assert.IsFalse(DrawDateFrame(ref value));
        _pointer.snapshot = new NowInputSnapshot(prevCenter, false, false, true);
        Assert.IsFalse(DrawDateFrame(ref value));
        Assert.AreEqual(new DateTime(2026, 6, 15), value.Date, "Month navigation must not change the value.");

        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, false);
        DrawDateFrame(ref value);

        _pointer.snapshot = new NowInputSnapshot(cellCenter, true, true, false);
        DrawDateFrame(ref value);
        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, true);
        DrawDateFrame(ref value);

        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, false);
        Assert.IsTrue(DrawDateFrame(ref value));
        Assert.AreEqual(target, value.Date);
        Assert.AreEqual(new TimeSpan(8, 45, 0), value.TimeOfDay);
    }

    [Test]
    public void DatePickerRangeIgnoresClicksOnDisabledDays()
    {
        var value = new DateTime(2026, 6, 15);
        var min = new DateTime(2026, 6, 10);
        var max = new DateTime(2026, 6, 20);
        var disabledDay = new DateTime(2026, 6, 25);
        var fieldCenter = DateRect.center;
        var cellCenter = CalendarDayCellRect(DateRect, 2026, 6, disabledDay).center;

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, true, true, false);
        DrawDateFrame(ref value, min, max);
        _pointer.snapshot = new NowInputSnapshot(fieldCenter, false, false, true);
        DrawDateFrame(ref value, min, max);

        _pointer.snapshot = new NowInputSnapshot(cellCenter, true, true, false);
        Assert.IsFalse(DrawDateFrame(ref value, min, max));
        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, true);
        Assert.IsFalse(DrawDateFrame(ref value, min, max));

        _pointer.snapshot = new NowInputSnapshot(cellCenter, false, false, false);
        Assert.IsFalse(DrawDateFrame(ref value, min, max), "Disabled days must not apply a selection.");
        Assert.AreEqual(new DateTime(2026, 6, 15), value.Date);
    }

    [Test]
    public void TimePickerHourSpinnerEditsValue()
    {
        var value = new TimeSpan(9, 30, 0);
        var fieldCenter = TimeRect.center;
        var upCenter = TimePickerHourUpRect(TimeRect).center;

        _pointer.snapshot = new NowInputSnapshot(fieldCenter, true, true, false);
        Assert.IsFalse(DrawTimeFrame(ref value));
        _pointer.snapshot = new NowInputSnapshot(fieldCenter, false, false, true);
        Assert.IsFalse(DrawTimeFrame(ref value));

        _pointer.snapshot = new NowInputSnapshot(upCenter, true, true, false);
        Assert.IsFalse(DrawTimeFrame(ref value));

        _pointer.snapshot = new NowInputSnapshot(upCenter, false, false, true);
        Assert.IsTrue(DrawTimeFrame(ref value), "Popup spinner edits must reflect in the caller's value.");
        Assert.AreEqual(new TimeSpan(10, 30, 0), value);
    }
}
