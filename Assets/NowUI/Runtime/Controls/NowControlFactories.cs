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
        public static NowButton Button(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowButton(rect, label, NowControls.SiteId(file, line));
        }

        public static NowCheckbox Checkbox(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCheckbox(rect, label, NowControls.SiteId(file, line));
        }

        public static NowRadio Radio(NowRect rect, string label, bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(rect, label, isOn, NowControls.SiteId(file, line));
        }

        public static NowRadio Radio(NowRect rect, bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(rect, string.Empty, isOn, NowControls.SiteId(file, line));
        }

        public static NowSlider Slider(NowRect rect, float min, float max, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSlider(rect, min, max, NowControls.SiteId(file, line));
        }

        public static NowTextField TextField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowTextArea TextArea(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextArea(rect, id, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(NowRect rect, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(rect, default, options, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(NowRect rect, NowId id, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(rect, id, options, NowControls.SiteId(file, line));
        }

        public static NowScrollView ScrollView(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowScrollView(rect, id, NowControls.SiteId(file, line));
        }

        public static NowSwitch Switch(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSwitch(rect, label, NowControls.SiteId(file, line));
        }

        public static NowProgressBar ProgressBar(NowRect rect, float value01 = 0f, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowProgressBar(rect, value01, NowControls.SiteId(file, line));
        }

        public static NowBadge Badge(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowBadge(rect, label, NowControls.SiteId(file, line));
        }

        public static NowChip Chip(NowRect rect, string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowChip(rect, label, NowControls.SiteId(file, line));
        }

        public static NowTabBar TabBar(NowRect rect, IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabBar(rect, labels, NowControls.SiteId(file, line));
        }

        public static NowTabView TabView(NowRect rect, IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabView(rect, labels, NowControls.SiteId(file, line));
        }

        public static NowSplitView SplitView(NowRect rect, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSplitView(rect, NowControls.SiteId(file, line));
        }

        public static NowComboBox ComboBox(NowRect rect, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowComboBox(rect, default, options, NowControls.SiteId(file, line));
        }

        public static NowDatePicker DatePicker(NowRect rect, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDatePicker(rect, default, NowControls.SiteId(file, line));
        }

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
        public static NowButton Button(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowButton(label, NowControls.SiteId(file, line));
        }

        public static NowCheckbox Checkbox(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCheckbox(label, NowControls.SiteId(file, line));
        }

        public static NowRadio Radio(string label, bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(label, isOn, NowControls.SiteId(file, line));
        }

        public static NowRadio Radio(bool isOn, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowRadio(string.Empty, isOn, NowControls.SiteId(file, line));
        }

        public static NowSlider Slider(float min, float max, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSlider(min, max, NowControls.SiteId(file, line));
        }

        public static NowTextField TextField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(id, NowControls.SiteId(file, line));
        }

        public static NowTextArea TextArea(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextArea(id, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(default, options, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(NowId id, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(id, options, NowControls.SiteId(file, line));
        }

        public static NowScrollView ScrollView(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowScrollView(id, NowControls.SiteId(file, line));
        }

        public static NowSwitch Switch(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSwitch(label, NowControls.SiteId(file, line));
        }

        public static NowProgressBar ProgressBar(float value01 = 0f, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowProgressBar(value01, NowControls.SiteId(file, line));
        }

        public static NowBadge Badge(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowBadge(label, NowControls.SiteId(file, line));
        }

        public static NowChip Chip(string label = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowChip(label, NowControls.SiteId(file, line));
        }

        public static NowTabBar TabBar(IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabBar(labels, NowControls.SiteId(file, line));
        }

        public static NowTabView TabView(IReadOnlyList<string> labels, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTabView(labels, NowControls.SiteId(file, line));
        }

        public static NowSplitView SplitView(NowSplitAxis axis = NowSplitAxis.Horizontal, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowSplitView(NowControls.SiteId(file, line)).SetAxis(axis);
        }

        public static NowTreeView TreeView(NowTreeViewState state, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTreeView(state, NowControls.SiteId(file, line));
        }

        public static NowComboBox ComboBox(IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowComboBox(default, options, NowControls.SiteId(file, line));
        }

        public static NowDatePicker DatePicker([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDatePicker(default(NowId), NowControls.SiteId(file, line));
        }

        public static NowTimePicker TimePicker([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTimePicker(default(NowId), NowControls.SiteId(file, line));
        }
    }
}
