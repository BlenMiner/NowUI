using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NowUI
{
    /// <summary>
    /// Free-form controls at explicit rects, mirroring <see cref="Now.Rectangle(NowRect)"/>
    /// and <see cref="Now.Text(NowRect)"/>:
    /// <code>if (Now.Button(rect, "Save").Draw()) Save();</code>
    ///
    /// Identity comes from the call site (captured via caller-info attributes):
    /// every textual call site is its own control, labels are purely visual, and
    /// loop iterations over one site are salted by occurrence. Use
    /// <c>SetId</c> or an explicit <see cref="NowId"/> when one logical control draws
    /// from several places or when looped items can reorder.
    /// </summary>
    public static partial class Now
    {
        /// <summary>Push button at an explicit rect.</summary>
        public static NowButton Button(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowButton(rect, label, NowControls.SiteId(file, line));
        }

        /// <summary>Labeled checkbox at an explicit rect.</summary>
        public static NowCheckbox Checkbox(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCheckbox(rect, label, NowControls.SiteId(file, line));
        }

        /// <summary>Labeled radio option at an explicit rect.</summary>
        public static NowRadio Radio(NowRect rect, string label, bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(rect, label, isOn, NowControls.SiteId(file, line));
        }

        /// <summary>Unlabeled radio option at an explicit rect.</summary>
        public static NowRadio Radio(NowRect rect, bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(rect, string.Empty, isOn, NowControls.SiteId(file, line));
        }

        /// <summary>Horizontal slider over a min/max range at an explicit rect.</summary>
        public static NowSlider Slider(NowRect rect, float min, float max, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSlider(rect, min, max, NowControls.SiteId(file, line));
        }

        /// <summary>Single-line text field at an explicit rect.</summary>
        public static NowTextField TextField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(rect, id, NowControls.SiteId(file, line));
        }

        /// <summary>Multi-line text area at an explicit rect.</summary>
        public static NowTextArea TextArea(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextArea(rect, id, NowControls.SiteId(file, line));
        }

        /// <summary>Single-selection dropdown over the given options at an explicit rect.</summary>
        public static NowDropdown Dropdown(NowRect rect, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(rect, default, options, NowControls.SiteId(file, line));
        }

        /// <summary>Single-selection dropdown with an explicit id at an explicit rect.</summary>
        public static NowDropdown Dropdown(NowRect rect, NowId id, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(rect, id, options, NowControls.SiteId(file, line));
        }

        /// <summary>Scrollable content container at an explicit rect.</summary>
        public static NowScrollView ScrollView(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowScrollView(rect, id, NowControls.SiteId(file, line));
        }

        /// <summary>Toggle switch with a sliding knob at an explicit rect.</summary>
        public static NowSwitch Switch(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSwitch(rect, label, NowControls.SiteId(file, line));
        }

        /// <summary>Draggable divider between two panes at an explicit rect.</summary>
        public static NowSplitter Splitter(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSplitter(rect, id, NowControls.SiteId(file, line));
        }

        /// <summary>Determinate or indeterminate progress bar at an explicit rect.</summary>
        public static NowProgressBar ProgressBar(NowRect rect, float value01 = 0f, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowProgressBar(rect, value01, NowControls.SiteId(file, line));
        }

        /// <summary>Non-interactive pill label at an explicit rect.</summary>
        public static NowBadge Badge(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowBadge(rect, label, NowControls.SiteId(file, line));
        }

        /// <summary>Selectable, optionally removable pill at an explicit rect.</summary>
        public static NowChip Chip(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowChip(rect, label, NowControls.SiteId(file, line));
        }

        /// <summary>Caller-owned tab strip at an explicit rect.</summary>
        public static NowTabBar TabBar(NowRect rect, IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabBar(rect, labels, NowControls.SiteId(file, line));
        }

        /// <summary>Tab strip with a masked page area below it at an explicit rect.</summary>
        public static NowTabView TabView(NowRect rect, IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabView(rect, labels, NowControls.SiteId(file, line));
        }

        /// <summary>Two resizable panes split by a draggable divider at an explicit rect.</summary>
        public static NowSplitView SplitView(NowRect rect, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSplitView(rect, NowControls.SiteId(file, line));
        }

        /// <summary>Editable dropdown that filters the given options at an explicit rect.</summary>
        public static NowComboBox ComboBox(NowRect rect, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowComboBox(rect, default, options, NowControls.SiteId(file, line));
        }

        /// <summary>Date field with a calendar popup at an explicit rect.</summary>
        public static NowDatePicker DatePicker(NowRect rect, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDatePicker(rect, default, NowControls.SiteId(file, line));
        }

        /// <summary>Time-of-day field with a spinner popup at an explicit rect.</summary>
        public static NowTimePicker TimePicker(NowRect rect, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTimePicker(rect, default, NowControls.SiteId(file, line));
        }
    }

    /// <summary>
    /// Layout-flowing controls, mirroring <see cref="NowLayout.Label(string)"/>:
    /// they reserve space in the active group, sized from their themed content.
    /// <code>
    /// if (NowLayout.Button("Save").Draw()) Save();
    /// NowLayout.Checkbox("Shadows").Draw(ref shadows);
    /// </code>
    ///
    /// Identity comes from the call site — two <c>Button("Save")</c> on different
    /// lines are different controls, and the label is never part of the id. With
    /// <see cref="NowButton.Begin"/> the label can simply be omitted
    /// (<c>NowLayout.Button().Begin()</c>) since the content draws inside the
    /// scope. Loops share a site and are salted by draw-order occurrence; use
    /// <c>SetId</c> or <see cref="NowControls.IdScope"/> keyed by your data when
    /// looped items can reorder.
    /// </summary>
    public static partial class NowLayout
    {
        /// <summary>Push button in layout flow.</summary>
        public static NowButton Button(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowButton(label, NowControls.SiteId(file, line));
        }

        /// <summary>Labeled checkbox in layout flow.</summary>
        public static NowCheckbox Checkbox(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCheckbox(label, NowControls.SiteId(file, line));
        }

        /// <summary>Labeled radio option in layout flow.</summary>
        public static NowRadio Radio(string label, bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(label, isOn, NowControls.SiteId(file, line));
        }

        /// <summary>Unlabeled radio option in layout flow.</summary>
        public static NowRadio Radio(bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(string.Empty, isOn, NowControls.SiteId(file, line));
        }

        /// <summary>Horizontal slider over a min/max range in layout flow.</summary>
        public static NowSlider Slider(float min, float max, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSlider(min, max, NowControls.SiteId(file, line));
        }

        /// <summary>Single-line text field in layout flow.</summary>
        public static NowTextField TextField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(id, NowControls.SiteId(file, line));
        }

        /// <summary>Multi-line text area in layout flow.</summary>
        public static NowTextArea TextArea(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextArea(id, NowControls.SiteId(file, line));
        }

        /// <summary>Single-selection dropdown over the given options in layout flow.</summary>
        public static NowDropdown Dropdown(IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(default, options, NowControls.SiteId(file, line));
        }

        /// <summary>Single-selection dropdown with an explicit id in layout flow.</summary>
        public static NowDropdown Dropdown(NowId id, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(id, options, NowControls.SiteId(file, line));
        }

        /// <summary>Scrollable content container in layout flow.</summary>
        public static NowScrollView ScrollView(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowScrollView(id, NowControls.SiteId(file, line));
        }

        /// <summary>Toggle switch with a sliding knob in layout flow.</summary>
        public static NowSwitch Switch(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSwitch(label, NowControls.SiteId(file, line));
        }

        /// <summary>Draggable divider between two panes in layout flow.</summary>
        public static NowSplitter Splitter(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSplitter(id, NowControls.SiteId(file, line));
        }

        /// <summary>Determinate or indeterminate progress bar in layout flow.</summary>
        public static NowProgressBar ProgressBar(float value01 = 0f, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowProgressBar(value01, NowControls.SiteId(file, line));
        }

        /// <summary>Non-interactive pill label in layout flow.</summary>
        public static NowBadge Badge(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowBadge(label, NowControls.SiteId(file, line));
        }

        /// <summary>Selectable, optionally removable pill in layout flow.</summary>
        public static NowChip Chip(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowChip(label, NowControls.SiteId(file, line));
        }

        /// <summary>Caller-owned tab strip in layout flow.</summary>
        public static NowTabBar TabBar(IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabBar(labels, NowControls.SiteId(file, line));
        }

        /// <summary>Tab strip with a masked page area below it in layout flow.</summary>
        public static NowTabView TabView(IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabView(labels, NowControls.SiteId(file, line));
        }

        /// <summary>Two resizable panes split by a draggable divider in layout flow.</summary>
        public static NowSplitView SplitView(NowSplitAxis axis = NowSplitAxis.Horizontal, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSplitView(NowControls.SiteId(file, line)).SetAxis(axis);
        }

        /// <summary>Hierarchical tree of collapsible rows driven by caller-owned state.</summary>
        public static NowTreeView TreeView(NowTreeViewState state, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTreeView(state, NowControls.SiteId(file, line));
        }

        /// <summary>Editable dropdown that filters the given options in layout flow.</summary>
        public static NowComboBox ComboBox(IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowComboBox(default, options, NowControls.SiteId(file, line));
        }

        /// <summary>Date field with a calendar popup in layout flow.</summary>
        public static NowDatePicker DatePicker([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDatePicker(default(NowId), NowControls.SiteId(file, line));
        }

        /// <summary>Time-of-day field with a spinner popup in layout flow.</summary>
        public static NowTimePicker TimePicker([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTimePicker(default(NowId), NowControls.SiteId(file, line));
        }
    }
}
