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
    /// <c>SetId</c> (or the string-id overloads) when one logical control draws
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

        public static NowTextField TextField(NowRect rect, string id = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(rect, id, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(NowRect rect, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(rect, null, options, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(NowRect rect, string id, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(rect, id, options, NowControls.SiteId(file, line));
        }

        public static NowScrollView ScrollView(NowRect rect, string id = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowScrollView(rect, id, NowControls.SiteId(file, line));
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

        public static NowTextField TextField(string id = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowTextField(id, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(null, options, NowControls.SiteId(file, line));
        }

        public static NowDropdown Dropdown(string id, IReadOnlyList<string> options, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowDropdown(id, options, NowControls.SiteId(file, line));
        }

        public static NowScrollView ScrollView(string id = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowScrollView(id, NowControls.SiteId(file, line));
        }
    }
}
