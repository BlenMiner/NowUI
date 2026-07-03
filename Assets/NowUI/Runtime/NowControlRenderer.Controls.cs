using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public readonly struct NowSwitchRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly NowRect glyphRect;
        public readonly bool value;
        public readonly float onT;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowSwitchRenderContext(NowThemeAsset themeAsset, NowRect rect, NowRect glyphRect, bool value, float onT, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.glyphRect = glyphRect;
            this.value = value;
            this.onT = onT;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowProgressBarRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly float value01;
        public readonly bool indeterminate;
        public readonly float phase01;

        public NowProgressBarRenderContext(NowThemeAsset themeAsset, NowRect rect, float value01, bool indeterminate, float phase01)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.value01 = value01;
            this.indeterminate = indeterminate;
            this.phase01 = phase01;
        }
    }

    public readonly struct NowBadgeRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly NowRectangleStyle style;
        public readonly NowTextStyle textStyle;

        public NowBadgeRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, NowRectangleStyle style, NowTextStyle textStyle)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.style = style;
            this.textStyle = textStyle;
        }
    }

    public readonly struct NowChipRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly bool selected;
        public readonly bool removable;
        public readonly NowRect removeRect;
        public readonly bool removeHovered;
        public readonly NowTextStyle textStyle;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowChipRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, bool selected, bool removable, NowRect removeRect, bool removeHovered, NowTextStyle textStyle, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.selected = selected;
            this.removable = removable;
            this.removeRect = removeRect;
            this.removeHovered = removeHovered;
            this.textStyle = textStyle;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowTooltipRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string text;

        public NowTooltipRenderContext(NowThemeAsset themeAsset, NowRect rect, string text)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.text = text;
        }
    }

    public readonly struct NowTabRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly bool selected;
        public readonly float selectedT;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowTabRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, bool selected, float selectedT, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.selected = selected;
            this.selectedT = selectedT;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowSplitDividerRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly bool vertical;
        public readonly bool dragging;
        public readonly bool focused;
        public readonly float hoverT;

        public NowSplitDividerRenderContext(NowThemeAsset themeAsset, NowRect rect, bool vertical, bool dragging, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.vertical = vertical;
            this.dragging = dragging;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowTreeRowRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly int depth;
        public readonly bool hasChildren;
        public readonly bool expanded;
        public readonly bool selected;
        public readonly NowRect disclosureRect;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowTreeRowRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, int depth, bool hasChildren, bool expanded, bool selected, NowRect disclosureRect, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.depth = depth;
            this.hasChildren = hasChildren;
            this.expanded = expanded;
            this.selected = selected;
            this.disclosureRect = disclosureRect;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public readonly struct NowSpinnerRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly NowRect upRect;
        public readonly NowRect downRect;
        public readonly bool upHovered;
        public readonly bool upHeld;
        public readonly bool downHovered;
        public readonly bool downHeld;
        public readonly bool focused;

        public NowSpinnerRenderContext(NowThemeAsset themeAsset, NowRect rect, NowRect upRect, NowRect downRect, bool upHovered, bool upHeld, bool downHovered, bool downHeld, bool focused)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.upRect = upRect;
            this.downRect = downRect;
            this.upHovered = upHovered;
            this.upHeld = upHeld;
            this.downHovered = downHovered;
            this.downHeld = downHeld;
            this.focused = focused;
        }
    }

    public readonly struct NowCalendarDayRenderContext
    {
        public readonly NowThemeAsset themeAsset;
        public readonly NowRect rect;
        public readonly string label;
        public readonly bool inMonth;
        public readonly bool isToday;
        public readonly bool selected;
        public readonly bool disabled;
        public readonly NowInteraction interaction;
        public readonly bool focused;
        public readonly float hoverT;

        public NowCalendarDayRenderContext(NowThemeAsset themeAsset, NowRect rect, string label, bool inMonth, bool isToday, bool selected, bool disabled, NowInteraction interaction, bool focused, float hoverT)
        {
            this.themeAsset = themeAsset;
            this.rect = rect;
            this.label = label;
            this.inMonth = inMonth;
            this.isToday = isToday;
            this.selected = selected;
            this.disabled = disabled;
            this.interaction = interaction;
            this.focused = focused;
            this.hoverT = hoverT;
        }
    }

    public partial class NowControlRenderer
    {
        public virtual Vector2 MeasureSwitch(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            var styles = themeAsset.controlStyles;
            var text = NowControls.Text(themeAsset, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            float gap = string.IsNullOrEmpty(label) ? 0f : styles.toggleGap;
            return new Vector2(
                styles.switchWidth + gap + labelSize.x,
                Mathf.Max(styles.switchHeight, labelSize.y));
        }

        public virtual NowRect SwitchGlyphRect(NowThemeAsset themeAsset, NowRect rect)
        {
            var styles = themeAsset.controlStyles;
            return new NowRect(
                rect.x,
                rect.y + (rect.height - styles.switchHeight) * 0.5f,
                styles.switchWidth,
                styles.switchHeight);
        }

        public virtual NowRect SwitchContentRect(NowThemeAsset themeAsset, NowRect rect)
        {
            var styles = themeAsset.controlStyles;
            float offset = styles.switchWidth + styles.toggleGap;
            return new NowRect(rect.x + offset, rect.y, rect.width - offset, rect.height);
        }

        public virtual void DrawSwitch(in NowSwitchRenderContext context)
        {
            var styles = context.themeAsset.controlStyles;
            var track = context.glyphRect;
            float trackRadius = track.height * 0.5f;

            Color offColor = StateToken(context.themeAsset, NowColorToken.SurfaceMuted, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, context.hoverT, context.interaction.held);
            Color onColor = StateToken(context.themeAsset, NowColorToken.Accent, NowColorToken.AccentHover, NowColorToken.AccentPressed, context.hoverT, context.interaction.held);

            Now.Rectangle(track)
                .SetRadius(trackRadius)
                .SetColor(Color.LerpUnclamped(offColor, onColor, context.onT))
                .SetOutline(context.value ? 0f : 1.5f)
                .SetOutlineColor(Color.LerpUnclamped(context.themeAsset.GetColor(NowColorToken.BorderStrong), Color.clear, context.onT))
                .Draw();

            float inset = styles.switchKnobInset;
            float knob = track.height - inset * 2f;
            float knobX = Mathf.LerpUnclamped(track.x + inset, track.xMax - inset - knob, context.onT);
            var knobRect = new NowRect(knobX, track.y + inset, knob, knob);

            DrawElevationShadow(context.themeAsset, knobRect, Circle(knobRect), NowElevationToken.Raised);
            Now.Rectangle(knobRect)
                .SetRadius(knob * 0.5f)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Surface))
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, track, new Vector4(trackRadius, trackRadius, trackRadius, trackRadius));
        }

        public virtual Vector2 MeasureProgressBar(NowThemeAsset themeAsset)
        {
            return new Vector2(160f, themeAsset.controlStyles.progressBarHeight);
        }

        public virtual void DrawProgressBar(in NowProgressBarRenderContext context)
        {
            float radius = context.rect.height * 0.5f;

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted))
                .SetOutline(1f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Border))
                .Draw();

            NowRect fillRect;

            if (context.indeterminate)
            {
                float sweep = context.rect.width * context.themeAsset.controlStyles.progressBarSweepRatio;
                float travel = context.rect.width + sweep;
                float x = context.rect.x - sweep + context.phase01 * travel;
                float clampedX = Mathf.Max(context.rect.x, x);
                float clampedRight = Mathf.Min(context.rect.xMax, x + sweep);

                if (clampedRight <= clampedX)
                    return;

                fillRect = new NowRect(clampedX, context.rect.y, clampedRight - clampedX, context.rect.height);
            }
            else
            {
                float width = context.rect.width * Mathf.Clamp01(context.value01);

                if (width <= 0f)
                    return;

                fillRect = new NowRect(context.rect.x, context.rect.y, width, context.rect.height);
            }

            Now.Rectangle(fillRect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent))
                .Draw();
        }

        public virtual Vector2 MeasureBadge(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            var styles = themeAsset.controlStyles;
            var text = NowControls.Text(themeAsset, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            float height = Mathf.Max(styles.badgeMinSize, labelSize.y + 4f);
            return new Vector2(Mathf.Max(height, labelSize.x + styles.badgePaddingX * 2f), height);
        }

        public virtual void DrawBadge(in NowBadgeRenderContext context)
        {
            var rectangle = context.themeAsset.Rectangle(context.rect, context.style);
            rectangle.radius = Circle(context.rect);
            rectangle.Draw();

            if (!string.IsNullOrEmpty(context.label))
                NowControls.DrawCenteredLabel(
                    context.themeAsset,
                    context.rect,
                    context.label,
                    context.textStyle,
                    context.rect,
                    ResolveDefaultButtonTextColor(context.themeAsset, context.style));
        }

        public virtual Vector2 MeasureChip(NowThemeAsset themeAsset, string label, NowTextStyle textStyle, bool removable)
        {
            var styles = themeAsset.controlStyles;
            var text = NowControls.Text(themeAsset, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            float width = labelSize.x + styles.chipPaddingX * 2f;

            if (removable)
                width += styles.chipRemoveSize + 4f;

            return new Vector2(width, styles.chipHeight);
        }

        public virtual NowRect ChipRemoveRect(NowThemeAsset themeAsset, NowRect rect)
        {
            var styles = themeAsset.controlStyles;
            float size = styles.chipRemoveSize;
            return new NowRect(
                rect.xMax - size - styles.chipPaddingX * 0.5f,
                rect.y + (rect.height - size) * 0.5f,
                size,
                size);
        }

        public virtual void DrawChip(in NowChipRenderContext context)
        {
            Vector4 radius = Circle(context.rect);
            Color fill = context.selected
                ? StateToken(context.themeAsset, NowColorToken.AccentMuted, NowColorToken.AccentMuted, NowColorToken.AccentMuted, context.hoverT, context.interaction.held)
                : StateToken(context.themeAsset, NowColorToken.SurfaceMuted, NowColorToken.SurfaceHover, NowColorToken.SurfacePressed, context.hoverT, context.interaction.held);

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(fill)
                .SetOutline(context.selected ? 1f : 0f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Accent))
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, radius);

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.Accent)
                : context.themeAsset.GetColor(NowColorToken.Text);
            float right = context.removable ? context.themeAsset.controlStyles.chipRemoveSize + 4f : context.themeAsset.controlStyles.chipPaddingX;
            NowControls.DrawLeftLabel(
                context.themeAsset,
                context.rect.Inset(context.themeAsset.controlStyles.chipPaddingX, 0f, right, 0f),
                context.label,
                context.textStyle,
                textColor);

            if (!context.removable)
                return;

            Color crossColor = context.removeHovered
                ? context.themeAsset.GetColor(NowColorToken.Danger)
                : context.themeAsset.GetColor(NowColorToken.TextMuted);
            DrawCross(context.removeRect, crossColor);
        }

        /// <summary>Line-drawn close/remove glyph centered in the rect.</summary>
        protected static void DrawCross(NowRect rect, Color color)
        {
            float extent = Mathf.Min(rect.width, rect.height) * 0.26f;
            Vector2 center = rect.center;
            var a = new Vector2(center.x - extent, center.y - extent);
            var b = new Vector2(center.x + extent, center.y + extent);
            var c = new Vector2(center.x - extent, center.y + extent);
            var d = new Vector2(center.x + extent, center.y - extent);

            Now.Line(a, b).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
            Now.Line(c, d).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
        }

        static readonly List<NowTextRun> s_tooltipRuns = new List<NowTextRun>(16);

        public virtual Vector2 MeasureTooltip(NowThemeAsset themeAsset, string text)
        {
            var styles = themeAsset.controlStyles;
            var body = NowControls.Text(themeAsset, NowTextStyle.Caption);
            body.mask = default;
            Vector2 size = NowTextWrap.Layout(body, text ?? string.Empty, styles.tooltipMaxWidth - styles.tooltipPadding * 2f, s_tooltipRuns);
            return new Vector2(
                Mathf.Min(styles.tooltipMaxWidth, size.x + styles.tooltipPadding * 2f),
                size.y + styles.tooltipPadding * 2f);
        }

        public virtual void DrawTooltip(in NowTooltipRenderContext context)
        {
            var styles = context.themeAsset.controlStyles;
            Vector4 radius = context.themeAsset.GetRadius(NowRadiusToken.Sm, new Vector4(6f, 6f, 6f, 6f));

            DrawElevationShadow(context.themeAsset, context.rect, radius, NowElevationToken.Overlay);

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Text))
                .Draw();

            var text = NowControls.Text(context.themeAsset, NowTextStyle.Caption)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Background));
            text.mask = default;
            float pad = styles.tooltipPadding;
            NowTextWrap.Layout(text, context.text ?? string.Empty, context.rect.width - pad * 2f, s_tooltipRuns);
            NowTextWrap.Draw(text, context.text ?? string.Empty, s_tooltipRuns, new Vector2(context.rect.x + pad, context.rect.y + pad));
        }

        public virtual float TabBarHeight(NowThemeAsset themeAsset)
        {
            return themeAsset.controlStyles.tabHeight;
        }

        public virtual Vector2 MeasureTab(NowThemeAsset themeAsset, string label)
        {
            var styles = themeAsset.controlStyles;
            var text = NowControls.Text(themeAsset, NowTextStyle.Body);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            return new Vector2(labelSize.x + styles.tabPaddingX * 2f, styles.tabHeight);
        }

        public virtual void DrawTabBarBackground(NowThemeAsset themeAsset, NowRect rect)
        {
            Now.Rectangle(new NowRect(rect.x, rect.yMax - 1f, rect.width, 1f))
                .SetColor(themeAsset.GetColor(NowColorToken.Border))
                .Draw();
        }

        public virtual void DrawTab(in NowTabRenderContext context)
        {
            var styles = context.themeAsset.controlStyles;

            if (context.interaction.hovered || context.interaction.held)
            {
                Color hover = context.interaction.held
                    ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                    : context.themeAsset.GetColor(NowColorToken.SurfaceHover);
                Vector4 radius = context.themeAsset.GetRadius(NowRadiusToken.Sm, new Vector4(6f, 6f, 6f, 6f));
                Now.Rectangle(context.rect.Inset(2f, 4f, 2f, 4f))
                    .SetRadius(radius)
                    .SetColor(hover)
                    .Draw();
            }

            Color textColor = Color.LerpUnclamped(
                context.themeAsset.GetColor(NowColorToken.TextMuted),
                context.themeAsset.GetColor(NowColorToken.Accent),
                context.selectedT);
            NowControls.DrawCenteredLabel(context.themeAsset, context.rect, context.label, context.selected ? NowTextStyle.BodyStrong : NowTextStyle.Body, context.rect, textColor);

            if (context.selectedT > 0.01f)
            {
                float thickness = styles.tabIndicatorThickness;
                float width = (context.rect.width - styles.tabPaddingX) * context.selectedT;
                Color indicator = context.themeAsset.GetColor(NowColorToken.Accent);
                indicator.a *= context.selectedT;
                Now.Rectangle(new NowRect(
                        context.rect.x + (context.rect.width - width) * 0.5f,
                        context.rect.yMax - thickness,
                        width,
                        thickness))
                    .SetRadius(thickness * 0.5f)
                    .SetColor(indicator)
                    .Draw();
            }

            if (context.focused)
            {
                Vector4 radius = context.themeAsset.GetRadius(NowRadiusToken.Sm, new Vector4(6f, 6f, 6f, 6f));
                DrawFocusRing(context.themeAsset, context.rect.Inset(2f, 4f, 2f, 4f), radius);
            }
        }

        public virtual void DrawSplitDivider(in NowSplitDividerRenderContext context)
        {
            Color color = context.dragging
                ? context.themeAsset.GetColor(NowColorToken.Accent)
                : Color.LerpUnclamped(
                    context.themeAsset.GetColor(NowColorToken.Border),
                    context.themeAsset.GetColor(NowColorToken.BorderStrong),
                    context.hoverT);

            float radius = (context.vertical ? context.rect.width : context.rect.height) * 0.5f;
            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(color)
                .Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, new Vector4(radius, radius, radius, radius));
        }

        public virtual void DrawTreeRow(in NowTreeRowRenderContext context)
        {
            var styles = context.themeAsset.controlStyles;

            if (context.selected || context.interaction.hovered || context.interaction.held)
            {
                Color fill = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.AccentMuted)
                    : context.interaction.held
                        ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                        : context.themeAsset.GetColor(NowColorToken.SurfaceHover);

                Now.Rectangle(context.rect)
                    .SetRadius(styles.popupItemRadius)
                    .SetColor(fill)
                    .Draw();
            }

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, new Vector4(styles.popupItemRadius, styles.popupItemRadius, styles.popupItemRadius, styles.popupItemRadius));

            if (context.hasChildren)
            {
                Color chevronColor = context.themeAsset.GetColor(NowColorToken.TextMuted);
                DrawChevron(context.disclosureRect, chevronColor, context.expanded ? NowChevronDirection.Down : NowChevronDirection.Right);
            }

            float textLeft = context.disclosureRect.xMax + 4f - context.rect.x;
            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.Accent)
                : context.themeAsset.GetColor(NowColorToken.Text);
            NowControls.DrawLeftLabel(
                context.themeAsset,
                context.rect.Inset(textLeft, 0f, 4f, 0f),
                context.label,
                context.selected ? NowTextStyle.BodyStrong : NowTextStyle.Body,
                textColor);
        }

        public virtual void DrawSpinnerButtons(in NowSpinnerRenderContext context)
        {
            DrawSpinnerButton(context.themeAsset, context.upRect, context.upHovered, context.upHeld, NowChevronDirection.Up);
            DrawSpinnerButton(context.themeAsset, context.downRect, context.downHovered, context.downHeld, NowChevronDirection.Down);
        }

        void DrawSpinnerButton(NowThemeAsset themeAsset, NowRect rect, bool hovered, bool held, NowChevronDirection direction)
        {
            if (hovered || held)
            {
                Now.Rectangle(rect.Inset(1f, 1f, 1f, 1f))
                    .SetRadius(3f)
                    .SetColor(held
                        ? themeAsset.GetColor(NowColorToken.SurfacePressed)
                        : themeAsset.GetColor(NowColorToken.SurfaceHover))
                    .Draw();
            }

            DrawChevron(rect, themeAsset.GetColor(NowColorToken.TextMuted), direction);
        }

        public virtual void DrawCalendarDay(in NowCalendarDayRenderContext context)
        {
            Vector4 radius = Circle(context.rect);

            if (context.selected)
            {
                Now.Rectangle(context.rect.Inset(2f, 2f, 2f, 2f))
                    .SetRadius(radius)
                    .SetColor(StateToken(context.themeAsset, NowColorToken.Accent, NowColorToken.AccentHover, NowColorToken.AccentPressed, context.hoverT, context.interaction.held))
                    .Draw();
            }
            else if (!context.disabled && (context.interaction.hovered || context.interaction.held))
            {
                Now.Rectangle(context.rect.Inset(2f, 2f, 2f, 2f))
                    .SetRadius(radius)
                    .SetColor(context.interaction.held
                        ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                        : context.themeAsset.GetColor(NowColorToken.SurfaceHover))
                    .Draw();
            }

            if (context.isToday && !context.selected)
            {
                Now.Rectangle(context.rect.Inset(2f, 2f, 2f, 2f))
                    .SetRadius(radius)
                    .SetColor(Color.clear)
                    .SetOutline(1f)
                    .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Accent))
                    .Draw();
            }

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : context.inMonth
                    ? context.themeAsset.GetColor(NowColorToken.Text)
                    : context.themeAsset.GetColor(NowColorToken.TextMuted);

            if (context.disabled)
                textColor.a *= context.themeAsset.controlStyles.disabledOpacity;

            NowControls.DrawCenteredLabel(context.themeAsset, context.rect, context.label, NowTextStyle.Body, context.rect, textColor);

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect.Inset(2f, 2f, 2f, 2f), radius);
        }
    }
}
