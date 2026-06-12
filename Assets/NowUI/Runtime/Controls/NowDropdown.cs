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
    public struct NowDropdown
    {
        readonly string _id;
        readonly IReadOnlyList<string> _options;
        NowLayoutOptions _layoutOptions;
        readonly NowRect _rect;
        readonly bool _hasRect;

        const float ItemHeight = 30f;
        const float MaxPopupHeight = 240f;

        internal NowDropdown(string id, IReadOnlyList<string> options)
        {
            _id = id ?? "dropdown";
            _options = options;
            _layoutOptions = default;
            _rect = default;
            _hasRect = false;
        }

        internal NowDropdown(NowRect rect, string id, IReadOnlyList<string> options) : this(id, options)
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
            int id = NowControls.GetControlId(_id);
            int optionCount = _options?.Count ?? 0;

            // Apply a selection made in last frame's deferred popup.
            ref int pending = ref NowUIControlState.Get<int>(NowUIInput.GetId(id, "pending"));
            bool changed = false;

            if (pending > 0 && pending - 1 < optionCount)
            {
                int next = pending - 1;
                changed = next != selected;
                selected = next;
            }

            pending = 0;

            // Field button.
            var textStyle = theme.Text(default, NowTextStyle.Body);
            float lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight() * textStyle.fontSize : 20f;
            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _layoutOptions, new Vector2(180f, lineHeight + 12f));

            var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
            ref bool open = ref NowUIControlState.Get<bool>(id);

            if (interaction.clicked || submitted)
                open = !open;

            if (open && optionCount == 0)
                open = false;

            float hoverT = NowUIControlState.Transition(id, interaction.hovered || interaction.held);

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

            // Popup, deferred to the overlay layer.
            NowUIControlState.RequestRepaint();

            float popupHeight = Mathf.Min(optionCount * ItemHeight + 8f, MaxPopupHeight);
            var popupRect = new NowRect(rect.x, rect.yMax + 4f, rect.width, popupHeight);
            bool scrolls = optionCount * ItemHeight + 8f > MaxPopupHeight;

            // Captured for the deferred callback (runs after Draw returns).
            var capturedTheme = theme;
            var capturedOptions = _options;
            var capturedField = rect;
            string capturedId = _id;
            int capturedDropdownId = id;
            int capturedSelected = selected;

            NowUIOverlay.Defer(popupRect, () =>
            {
                var background = capturedTheme.Rectangle(popupRect, NowRectangleStyle.Surface);
                background.outline = 1f;
                background.outlineColor = capturedTheme.GetColor(NowColorToken.Border, Color.gray);
                background.Draw();

                var itemArea = popupRect.Inset(4f);
                int pendingId = NowUIInput.GetId(capturedDropdownId, "pending");

                void DrawItems()
                {
                    for (int i = 0; i < capturedOptions.Count; ++i)
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

                        int itemId = NowUIInput.GetId(NowUIInput.GetId(capturedDropdownId, "item"), i.ToString());
                        var itemInteraction = NowUIInput.Interact(itemId, itemRect);

                        if (itemInteraction.hovered || i == capturedSelected)
                        {
                            var highlight = capturedTheme.Rectangle(itemRect, i == capturedSelected ? NowRectangleStyle.Accent : NowRectangleStyle.Muted);

                            if (itemInteraction.hovered && i != capturedSelected)
                                highlight.color = NowControls.StateTint(highlight.color, 1f, itemInteraction.held);

                            highlight.radius = new Vector4(4f, 4f, 4f, 4f);
                            highlight.Draw();
                        }

                        NowTextStyle itemStyle = i == capturedSelected ? NowTextStyle.Button : NowTextStyle.Body;
                        NowControls.DrawLeftLabel(capturedTheme, itemRect.Inset(8f, 0f, 4f, 0f), capturedOptions[i], itemStyle);

                        if (itemInteraction.clicked)
                        {
                            NowUIControlState.Get<int>(pendingId) = i + 1;
                            NowUIControlState.Get<bool>(capturedDropdownId) = false;
                        }
                    }
                }

                if (scrolls)
                {
                    using (Now.ScrollView(itemArea, capturedId).Begin())
                        DrawItems();
                }
                else
                {
                    DrawItems();
                }

                // Close on click-outside or cancel.
                var snapshot = NowUIInput.current;
                bool pressedOutside = snapshot.primaryPressed &&
                    !popupRect.Contains(snapshot.pointerPosition) &&
                    !capturedField.Contains(snapshot.pointerPosition);

                if (pressedOutside || snapshot.cancelPressed)
                    NowUIControlState.Get<bool>(capturedDropdownId) = false;
            });

            return changed;
        }
    }
}
