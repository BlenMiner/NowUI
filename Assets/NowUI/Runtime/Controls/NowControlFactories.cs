using System.Collections.Generic;

namespace NowUI
{
    /// <summary>
    /// Free-form controls at explicit rects, mirroring <see cref="Now.Rectangle(NowRect)"/>
    /// and <see cref="Now.Text(NowRect)"/>:
    /// <code>if (Now.Button(rect, "Save").Draw()) Save();</code>
    /// </summary>
    public static partial class Now
    {
        public static NowButton Button(NowRect rect, string label)
        {
            return new NowButton(rect, label);
        }

        public static NowCheckbox Checkbox(NowRect rect, string label)
        {
            return new NowCheckbox(rect, label);
        }

        public static NowRadio Radio(NowRect rect, string label, bool isOn)
        {
            return new NowRadio(rect, label, isOn);
        }

        public static NowSlider Slider(NowRect rect, float min, float max)
        {
            return new NowSlider(rect, min, max);
        }

        public static NowTextField TextField(NowRect rect, string id)
        {
            return new NowTextField(rect, id);
        }

        public static NowDropdown Dropdown(NowRect rect, string id, IReadOnlyList<string> options)
        {
            return new NowDropdown(rect, id, options);
        }

        public static NowScrollView ScrollView(NowRect rect, string id)
        {
            return new NowScrollView(rect, id);
        }
    }

    /// <summary>
    /// Layout-flowing controls, mirroring <see cref="NowLayout.Label(string)"/>:
    /// they reserve space in the active group, sized from their themed content.
    /// <code>
    /// if (NowLayout.Button("Save").Draw()) Save();
    /// NowLayout.Checkbox("Shadows").Draw(ref shadows);
    /// </code>
    /// </summary>
    public static partial class NowLayout
    {
        /// <summary>
        /// The label doubles as the control id and must be non-empty. With
        /// <see cref="NowButton.Draw"/> it is also the rendered text; with
        /// <see cref="NowButton.Begin"/> it is identity only and never rendered —
        /// pass an id like "icon-button" and draw the visible content inside the
        /// scope.
        /// </summary>
        public static NowButton Button(string label)
        {
            return new NowButton(label);
        }

        public static NowCheckbox Checkbox(string label)
        {
            return new NowCheckbox(label);
        }

        public static NowRadio Radio(string label, bool isOn)
        {
            return new NowRadio(label, isOn);
        }

        public static NowSlider Slider(float min, float max)
        {
            return new NowSlider(min, max);
        }

        public static NowTextField TextField(string id)
        {
            return new NowTextField(id);
        }

        public static NowDropdown Dropdown(string id, IReadOnlyList<string> options)
        {
            return new NowDropdown(id, options);
        }

        public static NowScrollView ScrollView(string id)
        {
            return new NowScrollView(id);
        }
    }
}
