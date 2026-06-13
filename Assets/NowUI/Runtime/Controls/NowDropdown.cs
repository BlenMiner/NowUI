using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Dropdown selector:
    /// <code>NowLayout.Dropdown("quality", qualityNames).Draw(ref qualityIndex);</code>
    /// The popup draws through <see cref="NowUIOverlay"/> — above everything, with
    /// the controls underneath pointer-blocked — and closes on selection, on a
    /// click outside, or on cancel. Long lists scroll. Selection from the popup
    /// applies on the next frame's Draw (deferred draws run after Draw returns).
    /// </summary>
    [NowBuilder]
    public struct NowDropdown
    {
        readonly string _id;
        readonly int _site;
        readonly IReadOnlyList<string> _options;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;

        const float ItemHeight = 30f;
        const float MaxPopupHeight = 240f;

        internal NowDropdown(string id, IReadOnlyList<string> options, int site)
        {
            _id = id;
            _site = site;
            _options = options;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
        }

        internal NowDropdown(NowRect rect, string id, IReadOnlyList<string> options, int site) : this(id, options, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowDropdown SetOptions(NowLayoutOptions options) { _layoutOptions = options; return this; }

        public NowDropdown SetWidth(float width) { _layoutOptions = _layoutOptions.SetWidth(width); return this; }

        public NowDropdown SetStretchWidth(float weight = 1f) { _layoutOptions = _layoutOptions.SetStretchWidth(weight); return this; }

        public bool Draw(ref int selected)
        {
            var theme = NowControls.theme;
            int id = _id != null ? NowControls.GetControlId(_id) : NowControls.GetControlId(_site);
            int optionCount = _options?.Count ?? 0;

            ref int pending = ref NowControlState.Get<int>(NowInput.GetId(id, "pending"));
            bool changed = false;

            if (pending > 0 && pending - 1 < optionCount)
            {
                int next = pending - 1;
                changed = next != selected;
                selected = next;
            }

            pending = 0;

            var textStyle = theme.Text(default, NowTextStyle.Body);
            float lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;
            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, new Vector2(180f, lineHeight + 12f));

            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
            ref bool open = ref NowControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
                open = !open;

            if (open && optionCount == 0)
                open = false;

            float hoverT = NowControlState.Transition(id, interaction.hovered || interaction.held);

            var box = theme.Rectangle(rect, NowRectangleStyle.Outline);
            box.color = NowControls.StateTint(box.color, hoverT, interaction.held);

            if (focused || open)
            {
                box.outline = 2f;
                box.outlineColor = theme.GetColor(NowColorToken.Accent, Color.blue);
            }

            box.Draw();

            string current = selected >= 0 && selected < optionCount ? _options[selected] : string.Empty;
            var inner = rect.Inset(10f, 0f, 24f, 0f);
            NowControls.DrawLeftLabel(theme, inner, current, NowTextStyle.Body);
            NowControls.DrawLeftLabel(theme, new NowRect(rect.xMax - 20f, rect.y, 16f, rect.height), open ? "^" : "v", NowTextStyle.Muted);

            if (!open)
                return changed;

            NowControlState.RequestRepaint();
            DeferPopup(theme, _options, id, rect, selected, optionCount);
            return changed;
        }

        /// <summary>
        /// The popup closure lives here so its display class only allocates while
        /// the popup is open — captured locals in Draw would otherwise allocate at
        /// method entry on every frame, even with the popup closed.
        /// </summary>
        static void DeferPopup(NowTheme theme, IReadOnlyList<string> options, int id, NowRect field, int selected, int optionCount)
        {
            float popupHeight = Mathf.Min(optionCount * ItemHeight + 8f, MaxPopupHeight);
            var popupRect = new NowRect(field.x, field.yMax + 4f, field.width, popupHeight);
            bool scrolls = optionCount * ItemHeight + 8f > MaxPopupHeight;
            int scrollId = NowInput.GetId(id, "popup-scroll");

            NowUIOverlay.Defer(popupRect, () =>
            {
                var background = theme.Rectangle(popupRect, NowRectangleStyle.Surface);
                background.outline = 1f;
                background.outlineColor = theme.GetColor(NowColorToken.Border, Color.gray);
                background.Draw();

                var itemArea = popupRect.Inset(4f);
                int pendingId = NowInput.GetId(id, "pending");
                int itemSeed = NowInput.GetId(id, "item");

                void DrawItems()
                {
                    for (int i = 0; i < options.Count; ++i)
                    {
                        NowRect itemRect;

                        if (scrolls)
                        {
                            itemRect = NowLayout.Rect(new NowLayoutOptions().SetHeight(ItemHeight).SetStretchWidth());
                        }
                        else
                        {
                            itemRect = new NowRect(itemArea.x, itemArea.y + i * ItemHeight, itemArea.width, ItemHeight);
                        }

                        var itemInteraction = NowInput.Interact(NowInput.CombineId(itemSeed, i + 1), itemRect);

                        if (itemInteraction.hovered || i == selected)
                        {
                            var highlight = theme.Rectangle(itemRect, i == selected ? NowRectangleStyle.Accent : NowRectangleStyle.Muted);

                            if (itemInteraction.hovered && i != selected)
                                highlight.color = NowControls.StateTint(highlight.color, 1f, itemInteraction.held);

                            highlight.radius = new Vector4(4f, 4f, 4f, 4f);
                            highlight.Draw();
                        }

                        NowTextStyle itemStyle = i == selected ? NowTextStyle.Button : NowTextStyle.Body;
                        NowControls.DrawLeftLabel(theme, itemRect.Inset(8f, 0f, 4f, 0f), options[i], itemStyle);

                        if (itemInteraction.clicked)
                        {
                            NowControlState.Get<int>(pendingId) = i + 1;
                            NowControlState.Get<bool>(id) = false;
                        }
                    }
                }

                if (scrolls)
                {
                    using (new NowScrollView(itemArea, scrollId).Begin())
                        DrawItems();
                }
                else
                {
                    DrawItems();
                }

                var snapshot = NowInput.current;
                bool pressedOutside = snapshot.primaryPressed &&
                    !popupRect.Contains(snapshot.pointerPosition) &&
                    !field.Contains(snapshot.pointerPosition);

                if (pressedOutside || snapshot.cancelPressed)
                    NowControlState.Get<bool>(id) = false;
            });
        }
    }
}
