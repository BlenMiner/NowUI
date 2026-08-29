using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Compact, square-edged control rendering modeled after Unity's dark
    /// editor skin. The renderer is runtime-safe: it deliberately uses only
    /// NowUI theme tokens and never depends on UnityEditor or editor textures.
    /// </summary>
    public sealed class NowUnityEditorControlRenderer : NowControlRenderer
    {
        static readonly List<NowTextRun> TooltipRuns = new List<NowTextRun>(16);

        // Sparse x, top-y, alpha triples sampled from Unity's 1x dark editor
        // glyphs. The menu x values are relative to its already-inset helper rect.
        static readonly byte[] ToggleCheckMask =
        {
            10, 4, 19,
            9, 5, 128, 10, 5, 255, 11, 5, 19,
            8, 6, 128, 9, 6, 255, 10, 6, 128,
            3, 7, 29, 7, 7, 128, 8, 7, 255, 9, 7, 128,
            2, 8, 19, 3, 8, 255, 4, 8, 128, 6, 8, 128, 7, 8, 255, 8, 8, 128,
            3, 9, 128, 4, 9, 255, 5, 9, 193, 6, 9, 255, 7, 9, 128,
            4, 10, 128, 5, 10, 255, 6, 10, 128,
            5, 11, 71
        };
        static readonly byte[] MenuCheckMask =
        {
            9, 4, 131, 10, 4, 255, 11, 4, 199,
            8, 5, 62, 9, 5, 255, 10, 5, 236,
            8, 6, 227, 9, 6, 255, 10, 6, 24,
            5, 7, 27, 7, 7, 60, 8, 7, 255, 9, 7, 134,
            4, 8, 72, 5, 8, 255, 6, 8, 208, 7, 8, 178, 8, 8, 255, 9, 8, 13,
            5, 9, 135, 6, 9, 255, 7, 9, 255, 8, 9, 167,
            6, 10, 212, 7, 10, 255, 8, 10, 47,
            6, 11, 47, 7, 11, 214
        };

        static Texture2D ToggleCheckTexture;
        static Texture2D MenuCheckTexture;

        const float EditorBadgeMinHeight = 16f;
        const float EditorBadgePaddingX = 5f;
        const float EditorChipHeight = 18f;
        const float EditorChipPaddingX = 6f;
        const float EditorChipRemoveSize = 10f;
        const float EditorChipRemoveInset = 4f;
        static readonly Vector4 EditorTagRadius = new Vector4(2f, 2f, 2f, 2f);

        public override Vector2 MeasureButton(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            Vector2 labelSize = NowControls.Text(themeAsset, textStyle).Measure(label ?? string.Empty);
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            return new Vector2(
                labelSize.x + padding.x + padding.z,
                Mathf.Max(themeAsset.controlStyles.buttonMinHeight, labelSize.y));
        }

        public override Vector2 MeasureButtonContent(NowThemeAsset themeAsset, Vector2 cachedContentSize)
        {
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            Vector2 fallback = new Vector2(
                themeAsset.controlStyles.buttonFallbackContentWidth,
                themeAsset.controlStyles.buttonFallbackContentHeight);
            Vector2 contentSize = cachedContentSize.x > 0f ? cachedContentSize : fallback;
            return new Vector2(
                contentSize.x + padding.x + padding.z,
                Mathf.Max(
                    themeAsset.controlStyles.buttonMinHeight,
                    contentSize.y + padding.y + padding.w));
        }

        public override Vector2 MeasureToggle(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            Vector2 labelSize = NowControls.Text(themeAsset, textStyle).Measure(label ?? string.Empty);
            float width = 16f + themeAsset.controlStyles.toggleGap + labelSize.x;
            return new Vector2(width, Mathf.Max(18f, labelSize.y));
        }

        public override Vector2 MeasureToggleContent(NowThemeAsset themeAsset, Vector2 cachedContentSize)
        {
            float width = 16f + themeAsset.controlStyles.toggleGap +
                Mathf.Max(cachedContentSize.x, themeAsset.controlStyles.buttonFallbackContentWidth);
            return new Vector2(width, Mathf.Max(18f, cachedContentSize.y));
        }

        public override float ToggleGlyphSize(NowThemeAsset themeAsset, float labelHeight)
        {
            return 15f;
        }

        public override NowRect ToggleGlyphRect(NowThemeAsset themeAsset, NowRect rect, float glyphSize)
        {
            return new NowRect(rect.x, rect.y + (rect.height - 15f) * 0.5f, 16f, 15f);
        }

        public override NowRect ToggleContentRect(NowThemeAsset themeAsset, NowRect rect, float glyphSize)
        {
            float offset = 16f + themeAsset.controlStyles.toggleGap;
            return new NowRect(rect.x + offset, rect.y, Mathf.Max(0f, rect.width - offset), rect.height);
        }

        public override NowSliderVisualMetrics CalculateSliderMetrics(
            NowThemeAsset themeAsset,
            NowRect rect,
            float normalized)
        {
            const float KnobWidth = 10f;
            const float KnobHeight = 10f;
            float trackHeight = themeAsset.controlStyles.sliderTrackThickness;
            float knobX = rect.x + Mathf.Clamp01(normalized) * Mathf.Max(0f, rect.width - KnobWidth);
            float trackY = rect.y + (rect.height - trackHeight) * 0.5f;
            var track = new NowRect(rect.x, trackY, rect.width, trackHeight);
            var fill = new NowRect(rect.x, trackY, knobX - rect.x + KnobWidth * 0.5f, trackHeight);
            var knob = new NowRect(knobX, rect.y + (rect.height - KnobHeight) * 0.5f, KnobWidth, KnobHeight);
            return new NowSliderVisualMetrics(track, fill, knob);
        }

        public override Vector2 MeasureDropdownField(NowThemeAsset themeAsset, float lineHeight)
        {
            return new Vector2(
                200f,
                Mathf.Max(themeAsset.controlStyles.dropdownFieldMinHeight, lineHeight));
        }

        public override Vector2 MeasureBadge(
            NowThemeAsset themeAsset,
            string label,
            NowTextStyle textStyle)
        {
            Vector2 labelSize = NowControls.Text(themeAsset, textStyle).Measure(label ?? string.Empty);
            float height = Mathf.Max(EditorBadgeMinHeight, Mathf.Ceil(labelSize.y + 2f));
            float width = Mathf.Max(height, Mathf.Ceil(labelSize.x + EditorBadgePaddingX * 2f));
            return new Vector2(width, height);
        }

        public override void DrawBadge(in NowBadgeRenderContext context)
        {
            var rectangle = context.themeAsset.Rectangle(context.rect, context.style);
            rectangle.radius = EditorTagRadius;
            rectangle.outline = 1f;
            rectangle.outlineColor = context.themeAsset.GetColor(NowColorToken.BorderStrong);
            rectangle.Draw();

            if (string.IsNullOrEmpty(context.label))
                return;

            NowControls.DrawCenteredLabel(
                context.themeAsset,
                context.rect,
                context.label,
                context.textStyle,
                context.rect,
                EditorBadgeTextColor(context.themeAsset, context.style));
        }

        public override Vector2 MeasureChip(
            NowThemeAsset themeAsset,
            string label,
            NowTextStyle textStyle,
            bool removable)
        {
            Vector2 labelSize = NowControls.Text(themeAsset, textStyle).Measure(label ?? string.Empty);
            float height = Mathf.Max(EditorChipHeight, Mathf.Ceil(labelSize.y + 1f));
            float width = labelSize.x + EditorChipPaddingX * 2f;

            if (removable)
                width += EditorChipRemoveSize + EditorChipRemoveInset - 1f;

            return new Vector2(Mathf.Max(height, Mathf.Ceil(width)), height);
        }

        public override NowRect ChipRemoveRect(NowThemeAsset themeAsset, NowRect rect)
        {
            float size = Mathf.Min(EditorChipRemoveSize, Mathf.Max(0f, rect.height - 4f));
            return new NowRect(
                rect.xMax - size - EditorChipRemoveInset,
                rect.y + (rect.height - size) * 0.5f,
                size,
                size);
        }

        public override void DrawChip(in NowChipRenderContext context)
        {
            Color fill = context.selected
                ? context.interaction.held
                    ? context.themeAsset.GetColor(NowColorToken.AccentPressed)
                    : context.themeAsset.GetColor(NowColorToken.Accent)
                : StateToken(
                    context.themeAsset,
                    NowColorToken.Surface,
                    NowColorToken.SurfaceHover,
                    NowColorToken.SurfacePressed,
                    context.hoverT,
                    context.interaction.held);
            Color outline = context.focused
                ? context.themeAsset.GetColor(NowColorToken.FocusRing)
                : context.themeAsset.GetColor(NowColorToken.BorderStrong);

            Now.Rectangle(context.rect)
                .SetRadius(EditorTagRadius)
                .SetColor(fill)
                .SetOutline(1f)
                .SetOutlineColor(outline)
                .Draw();

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : context.themeAsset.GetColor(NowColorToken.Text);
            float right = context.removable
                ? Mathf.Max(
                    EditorChipPaddingX,
                    context.rect.xMax - context.removeRect.x + 3f)
                : EditorChipPaddingX;
            NowControls.DrawLeftLabel(
                context.themeAsset,
                context.rect.Inset(EditorChipPaddingX, 0f, right, 0f),
                context.label,
                context.textStyle,
                textColor);

            if (!context.removable)
                return;

            Color crossColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : context.removeHovered
                    ? context.themeAsset.GetColor(NowColorToken.Danger)
                    : context.themeAsset.GetColor(NowColorToken.TextMuted);
            DrawEditorCross(context.removeRect, crossColor);
        }

        public override void DrawButton(in NowButtonRenderContext context)
        {
            Vector4 radius = ResolveRadius(
                context.themeAsset,
                context.themeAsset.controlStyles.buttonRadius,
                context.rect,
                NowRadiusToken.Sm);
            // NowButton's API default is Accent, while Unity's ordinary editor
            // button is neutral. Reinterpret only the button surface here;
            // Accent remains blue for selection, focus, and explicitly themed
            // rectangles.
            NowRectangleStyle surfaceStyle = context.rectangleStyle == NowRectangleStyle.Accent
                ? NowRectangleStyle.Surface
                : context.rectangleStyle;
            var rectangle = ButtonRectangle(
                context.themeAsset,
                context.rect,
                surfaceStyle,
                context.hoverT,
                context.interaction.held);
            rectangle.radius = radius;

            if (context.rectangleStyle == NowRectangleStyle.Accent)
            {
                rectangle.color = context.interaction.held
                    ? context.themeAsset.GetColor(NowColorToken.AccentPressed)
                    : context.themeAsset.GetColor(NowColorToken.Surface);
                rectangle.outlineColor = context.themeAsset.GetColor(NowColorToken.BorderStrong);
            }

            if (context.focused)
            {
                rectangle.outline = Mathf.Max(1f, rectangle.outline);
                rectangle.outlineColor = context.themeAsset.GetColor(NowColorToken.FocusRing);
            }

            rectangle.Draw();

            if (!string.IsNullOrEmpty(context.label))
                DrawButtonLabel(context, context.rect);
        }

        protected override Color ResolveDefaultButtonTextColor(
            NowThemeAsset themeAsset,
            NowRectangleStyle rectangleStyle)
        {
            switch (rectangleStyle)
            {
                case NowRectangleStyle.Accent:
                    return themeAsset.GetColor(NowColorToken.Text);
                case NowRectangleStyle.Danger:
                    return themeAsset.GetColor(NowColorToken.DangerText);
                default:
                    return themeAsset.GetColor(NowColorToken.Text);
            }
        }

        protected override void DrawButtonLabel(in NowButtonRenderContext context, NowRect visualRect)
        {
            Color color = context.rectangleStyle == NowRectangleStyle.Accent && context.interaction.hovered
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : ResolveDefaultButtonTextColor(context.themeAsset, context.rectangleStyle);
            NowControls.DrawCenteredLabel(
                context.themeAsset,
                visualRect,
                context.label,
                context.textStyle,
                context.rect,
                color);
        }

        public override void DrawCheckbox(in NowToggleRenderContext context)
        {
            float radius = context.themeAsset.controlStyles.checkboxMarkRadius;
            Color fill = context.interaction.held
                ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                : context.themeAsset.GetColor(NowColorToken.SurfaceMuted);
            var visualRect = new NowRect(
                context.glyphRect.x,
                context.glyphRect.y + (context.glyphRect.height - 14f) * 0.5f,
                14f,
                14f);

            Now.Rectangle(visualRect)
                .SetRadius(radius)
                .SetColor(fill)
                .SetOutline(1f)
                .SetOutlineColor(context.focused
                    ? context.themeAsset.GetColor(NowColorToken.FocusRing)
                    : context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            if (context.value)
            {
                Color mark = Color.LerpUnclamped(
                    context.themeAsset.GetColor(NowColorToken.Text),
                    context.themeAsset.GetColor(NowColorToken.AccentText),
                    4f / 9f);
                DrawEditorCheckMark(
                    context.glyphRect,
                    mark,
                    false);
            }
        }

        public override void DrawRadio(in NowToggleRenderContext context)
        {
            float radius = Mathf.Min(context.glyphRect.width, context.glyphRect.height) * 0.5f;
            Color mark = context.themeAsset.GetColor(NowColorToken.AccentText);

            Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(context.interaction.held
                    ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                    : context.themeAsset.GetColor(NowColorToken.SurfaceMuted))
                .SetOutline(1f)
                .SetOutlineColor(context.focused
                    ? context.themeAsset.GetColor(NowColorToken.FocusRing)
                    : context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            if (!context.value)
                return;

            float dot = context.glyphRect.width * 0.5f;
            Now.Rectangle(new NowRect(
                    context.glyphRect.x + (context.glyphRect.width - dot) * 0.5f,
                    context.glyphRect.y + (context.glyphRect.height - dot) * 0.5f,
                    dot,
                    dot))
                .SetRadius(dot * 0.5f)
                .SetColor(mark)
                .Draw();
        }

        public override void DrawSlider(in NowSliderRenderContext context)
        {
            float trackRadius = Mathf.Min(2f, context.metrics.track.height * 0.5f);
            Color track = Color.LerpUnclamped(
                context.themeAsset.GetColor(NowColorToken.Surface),
                context.themeAsset.GetColor(NowColorToken.TextMuted),
                0.07f);
            Now.Rectangle(context.metrics.track)
                .SetRadius(trackRadius)
                .SetColor(track)
                .Draw();

            Color knob = context.interaction.held
                ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                : Color.LerpUnclamped(
                    context.themeAsset.GetColor(NowColorToken.Surface),
                    context.themeAsset.GetColor(NowColorToken.TextMuted),
                    0.71f);
            float knobRadius = Mathf.Min(
                context.metrics.knob.width,
                context.metrics.knob.height) * 0.5f;
            NowRectangle knobShape = Now.Rectangle(context.metrics.knob)
                .SetRadius(knobRadius)
                .SetColor(knob);

            if (context.focused)
            {
                knobShape = knobShape
                    .SetOutline(1f)
                    .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.FocusRing));
            }

            knobShape.Draw();
        }

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            var appearance = context.appearance;
            Vector4 defaultRadius = ResolveRadius(
                context.themeAsset,
                context.themeAsset.controlStyles.fieldRadius,
                context.rect,
                NowRadiusToken.Sm);
            Vector4 radius = appearance.ResolveRadius(context.themeAsset, context.rect, defaultRadius);
            Color background = appearance.ResolveBackgroundColor(
                context.themeAsset,
                context.themeAsset.GetColor(NowColorToken.SurfaceMuted));
            Color defaultBorder = Color.LerpUnclamped(
                context.themeAsset.GetColor(NowColorToken.BorderStrong),
                Color.black,
                0.12f);
            Color border = appearance.ResolveBorderColor(context.themeAsset, defaultBorder);
            Color focus = appearance.ResolveFocusColor(
                context.themeAsset,
                context.themeAsset.controlStyles.fieldFocusColor.Resolve(context.themeAsset));
            float outline = context.focused
                ? appearance.hasFocusOutlineWidth ? appearance.focusOutlineWidth : 1f
                : appearance.hasOutlineWidth ? appearance.outlineWidth : 1f;

            if (appearance.hasElevation && appearance.elevation != NowElevationToken.None)
                DrawElevationShadow(context.themeAsset, context.rect, radius, appearance.elevation);

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(background)
                .SetOutline(outline)
                .SetOutlineColor(context.focused ? focus : border)
                .Draw();
        }

        public override void DrawDropdownField(in NowDropdownFieldRenderContext context)
        {
            bool active = context.focused || context.open;
            Vector4 radius = ResolveRadius(
                context.themeAsset,
                context.themeAsset.controlStyles.fieldRadius,
                context.rect,
                NowRadiusToken.Sm);
            Color background = context.interaction.held
                ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                : context.themeAsset.GetColor(NowColorToken.SurfaceHover);

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(background)
                .SetOutline(1f)
                .SetOutlineColor(active
                    ? context.themeAsset.GetColor(NowColorToken.FocusRing)
                    : context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            NowRect inner = DropdownFieldInnerRect(context.themeAsset, context.rect, LabelHeight(context.themeAsset));

            if (context.showsPlaceholder)
                NowControls.DrawLeftPlaceholder(context.themeAsset, inner, context.placeholder);
            else
                NowControls.DrawLeftLabel(context.themeAsset, inner, context.current, NowTextStyle.Body);

            float arrowSize = context.themeAsset.controlStyles.fieldChevronSize;
            DrawDropdownTriangle(
                context.themeAsset,
                new NowRect(
                    context.rect.xMax - arrowSize - 4f,
                    context.rect.y,
                    arrowSize,
                    context.rect.height));
        }

        public override void DrawPopupBackground(NowThemeAsset themeAsset, NowRect rect, bool menu)
        {
            Vector4 radius = ResolveRadius(
                themeAsset,
                themeAsset.controlStyles.popupRadius,
                rect,
                NowRadiusToken.Sm);
            DrawElevationShadow(themeAsset, rect, radius, NowElevationToken.Overlay);
            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(themeAsset.GetColor(NowColorToken.SurfaceElevated))
                .SetOutline(1f)
                .SetOutlineColor(themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();
        }

        public override void DrawSelection(NowThemeAsset themeAsset, NowRect rect)
        {
            Color selection = themeAsset.GetColor(NowColorToken.AccentHover);
            selection.a = themeAsset.controlStyles.selectionAlpha;
            Now.Rectangle(rect).SetColor(selection).Draw();
        }

        public override void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            DrawMenuItem(
                context,
                context.themeAsset.controlStyles.contextMenuPaddingX,
                true);
        }

        public override void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            DrawMenuItem(
                context,
                context.themeAsset.controlStyles.contextMenuPaddingX * 0.7f,
                false);
        }

        public override void DrawContextMenuSelectionIndicator(
            NowThemeAsset themeAsset,
            NowRect rect,
            bool enabled,
            bool highlighted)
        {
            Color color = themeAsset.GetColor(
                highlighted ? NowColorToken.AccentText : NowColorToken.Text);

            if (!enabled)
                color.a *= themeAsset.controlStyles.disabledOpacity;

            float size = Mathf.Min(16f, rect.height);
            DrawEditorCheckMark(
                new NowRect(rect.x + 1f, rect.y + (rect.height - size) * 0.5f, 15f, size),
                color,
                true);
        }

        public override void DrawContextMenuSubmenuIndicator(
            NowThemeAsset themeAsset,
            NowRect rect,
            bool enabled,
            bool open)
        {
            Color color = themeAsset.GetColor(
                open ? NowColorToken.AccentText : NowColorToken.TextMuted);
            if (!enabled)
                color.a *= themeAsset.controlStyles.disabledOpacity;

            float centerX = rect.xMax - 8f;
            float centerY = rect.center.y;
            Now.Triangle(
                    new Vector2(centerX - 2.5f, centerY - 4f),
                    new Vector2(centerX - 2.5f, centerY + 4f),
                    new Vector2(centerX + 2.5f, centerY))
                .SetColor(color)
                .Draw();
        }

        public override void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            if (!context.metrics.visible)
                return;

            NowRect track = InsetScrollbarCrossAxis(context.axis, context.metrics.track, 1f);
            NowRect thumbRect = InsetScrollbarCrossAxis(context.axis, context.metrics.thumb, 1f);
            float trackRadius = Mathf.Min(track.width, track.height) * 0.5f;

            Now.Rectangle(track)
                .SetRadius(trackRadius)
                // Unity's scrollbar trough is a translucent darkening of the
                // pane below it, not an opaque editor-gray strip. Keeping this
                // adaptive matters when a scroll view sits on a darker pane.
                .SetColor(new Color(0f, 0f, 0f, 0.05f))
                .Draw();

            Color thumb = Color.LerpUnclamped(
                context.themeAsset.GetColor(NowColorToken.Surface),
                context.themeAsset.GetColor(NowColorToken.TextMuted),
                0.08f);
            Color thumbOutline = Color.LerpUnclamped(
                context.themeAsset.GetColor(NowColorToken.BorderStrong),
                context.themeAsset.GetColor(NowColorToken.Border),
                0.82f);
            float thumbRadius = Mathf.Min(thumbRect.width, thumbRect.height) * 0.5f;
            Now.Rectangle(thumbRect)
                .SetRadius(thumbRadius)
                .SetColor(thumb)
                .SetOutline(1f)
                .SetOutlineColor(thumbOutline)
                .Draw();
        }

        public override void DrawTooltip(in NowTooltipRenderContext context)
        {
            float padding = context.themeAsset.controlStyles.tooltipPadding;
            Vector4 radius = context.themeAsset.GetRadius(
                NowRadiusToken.Sm,
                new Vector4(2f, 2f, 2f, 2f));
            DrawElevationShadow(context.themeAsset, context.rect, radius, NowElevationToken.Overlay);
            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceElevated))
                .SetOutline(1f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            var text = NowControls.Text(context.themeAsset, NowTextStyle.Caption)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Text));
            text.mask = default;
            string value = context.text ?? string.Empty;
            float width = Mathf.Max(0f, context.rect.width - padding * 2f);
            NowTextWrap.Layout(text, value, width, TooltipRuns);
            NowTextWrap.Draw(text, value, TooltipRuns, new Vector2(context.rect.x + padding, context.rect.y + padding));
        }

        public override void DrawProgressBar(in NowProgressBarRenderContext context)
        {
            Color background = Color.LerpUnclamped(
                context.themeAsset.GetColor(NowColorToken.Background),
                context.themeAsset.GetColor(NowColorToken.BorderStrong),
                0.4f);
            Now.Rectangle(context.rect)
                .SetRadius(1f)
                .SetColor(background)
                .SetOutline(1f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.BorderStrong))
                .Draw();

            NowRect fill;
            if (context.indeterminate)
            {
                float sweep = context.rect.width * context.themeAsset.controlStyles.progressBarSweepRatio;
                float x = context.rect.x - sweep + context.phase01 * (context.rect.width + sweep);
                float left = Mathf.Max(context.rect.x + 1f, x);
                float right = Mathf.Min(context.rect.xMax - 1f, x + sweep);
                fill = new NowRect(left, context.rect.y + 1f, Mathf.Max(0f, right - left), Mathf.Max(0f, context.rect.height - 2f));
            }
            else
            {
                fill = new NowRect(
                    context.rect.x + 1f,
                    context.rect.y + 1f,
                    Mathf.Max(0f, (context.rect.width - 2f) * Mathf.Clamp01(context.value01)),
                    Mathf.Max(0f, context.rect.height - 2f));
            }

            if (fill.width > 0f)
                Now.Rectangle(fill).SetRadius(1f).SetColor(context.themeAsset.GetColor(NowColorToken.AccentMuted)).Draw();
        }

        public override void DrawTab(in NowTabRenderContext context)
        {
            if (context.selected || context.interaction.hovered || context.interaction.held)
            {
                Color fill = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.SurfaceElevated)
                    : context.interaction.held
                        ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                        : context.themeAsset.GetColor(NowColorToken.SurfaceHover);
                Now.Rectangle(context.rect)
                    .SetRadius(2f)
                    .SetColor(fill)
                    .Draw();
            }

            Color text = context.selected
                ? context.themeAsset.GetColor(NowColorToken.Text)
                : context.themeAsset.GetColor(NowColorToken.TextMuted);
            NowControls.DrawCenteredLabel(
                context.themeAsset,
                context.rect,
                context.label,
                context.selected ? NowTextStyle.BodyStrong : NowTextStyle.Body,
                context.rect,
                text);

            if (context.selectedT > 0.01f)
            {
                float thickness = context.themeAsset.controlStyles.tabIndicatorThickness;
                Color indicator = context.themeAsset.GetColor(NowColorToken.Accent);
                indicator.a *= context.selectedT;
                Now.Rectangle(new NowRect(
                        context.rect.x,
                        context.rect.yMax - thickness,
                        context.rect.width,
                        thickness))
                    .SetColor(indicator)
                    .Draw();
            }

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, new Vector4(2f, 2f, 2f, 2f));
        }

        public override void DrawTreeRow(in NowTreeRowRenderContext context)
        {
            if (context.selected || context.interaction.hovered || context.interaction.held)
            {
                Color fill = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.Accent)
                    : context.interaction.held
                        ? context.themeAsset.GetColor(NowColorToken.SurfacePressed)
                        : context.themeAsset.GetColor(NowColorToken.SurfaceHover);
                Now.Rectangle(context.rect).SetRadius(1f).SetColor(fill).Draw();
            }

            if (context.hasChildren)
            {
                Color disclosure = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.AccentText)
                    : Color.LerpUnclamped(
                        context.themeAsset.GetColor(NowColorToken.Surface),
                        context.themeAsset.GetColor(NowColorToken.TextMuted),
                        4f / 23f);
                DrawDisclosureTriangle(
                    context.disclosureRect,
                    disclosure,
                    context.expanded);
            }

            float textLeft = context.disclosureRect.xMax + 3f - context.rect.x;
            Color text = context.selected
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : context.themeAsset.GetColor(NowColorToken.Text);
            NowControls.DrawLeftLabel(
                context.themeAsset,
                context.rect.Inset(textLeft, 0f, 3f, 0f),
                context.label,
                NowTextStyle.Body,
                text);

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, new Vector4(1f, 1f, 1f, 1f));
        }

        public override void DrawSplitDivider(in NowSplitDividerRenderContext context)
        {
            Color color = context.dragging || context.hoverT > 0f
                ? context.themeAsset.GetColor(NowColorToken.Accent)
                : context.themeAsset.GetColor(NowColorToken.BorderStrong);
            NowRect line = context.vertical
                ? new NowRect(context.rect.center.x - 0.5f, context.rect.y, 1f, context.rect.height)
                : new NowRect(context.rect.x, context.rect.center.y - 0.5f, context.rect.width, 1f);
            Now.Rectangle(line).SetColor(color).Draw();
        }

        protected override void DrawFocusRing(NowThemeAsset themeAsset, NowRect rect, Vector4 radius)
        {
            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(Color.clear)
                .SetOutline(1f)
                .SetOutlineColor(themeAsset.GetColor(NowColorToken.FocusRing))
                .Draw();
        }

        protected override void DrawFocusRing(
            NowThemeAsset themeAsset,
            NowRect rect,
            Vector4 radius,
            Color color)
        {
            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(Color.clear)
                .SetOutline(1f)
                .SetOutlineColor(color)
                .Draw();
        }

        static Color EditorBadgeTextColor(
            NowThemeAsset themeAsset,
            NowRectangleStyle style)
        {
            switch (style)
            {
                case NowRectangleStyle.Accent:
                    return themeAsset.GetColor(NowColorToken.AccentText);
                case NowRectangleStyle.Danger:
                    return themeAsset.GetColor(NowColorToken.DangerText);
                default:
                    // AccentSoft and Outline use Unity's deliberately dark
                    // selection blue. Text remains readable over those compact
                    // editor tags instead of inheriting the generic blue
                    // foreground intended for lighter design-system themes.
                    return themeAsset.GetColor(NowColorToken.Text);
            }
        }

        static void DrawEditorCross(NowRect rect, Color color)
        {
            const float Extent = 2.5f;
            Vector2 center = SnapToScreenPixel(rect.center);
            var a = SnapToScreenPixel(new Vector2(center.x - Extent, center.y - Extent));
            var b = SnapToScreenPixel(new Vector2(center.x + Extent, center.y + Extent));
            var c = SnapToScreenPixel(new Vector2(center.x - Extent, center.y + Extent));
            var d = SnapToScreenPixel(new Vector2(center.x + Extent, center.y - Extent));

            Now.Line(a, b).SetColor(color).SetWidth(1f).SetCap(NowLineCap.Butt).Draw();
            Now.Line(c, d).SetColor(color).SetWidth(1f).SetCap(NowLineCap.Butt).Draw();
        }

        static void DrawMenuItem(
            in NowPopupItemRenderContext context,
            float horizontalPadding,
            bool drawCheck)
        {
            bool highlighted = context.selected || context.interaction.hovered;
            if (highlighted)
            {
                Color fill = context.interaction.held
                    ? context.themeAsset.GetColor(NowColorToken.AccentPressed)
                    : context.themeAsset.GetColor(NowColorToken.Accent);
                Now.Rectangle(context.rect).SetRadius(1f).SetColor(fill).Draw();
            }

            Color titleColor = highlighted
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : context.themeAsset.GetColor(NowColorToken.TextMuted);

            if (drawCheck && context.isChecked)
            {
                float size = Mathf.Min(16f, context.rect.height);
                DrawEditorCheckMark(
                    new NowRect(
                        context.rect.x + 1f,
                        context.rect.y + (context.rect.height - size) * 0.5f,
                        15f,
                        size),
                    highlighted
                        ? context.themeAsset.GetColor(NowColorToken.AccentText)
                        : context.themeAsset.GetColor(NowColorToken.Text),
                    true);
            }

            float right = context.hasSubmenu
                ? context.themeAsset.controlStyles.submenuIndicatorInset + 3f
                : horizontalPadding;
            NowRect content = context.rect.Inset(horizontalPadding, 0f, right, 0f);

            if (string.IsNullOrEmpty(context.detail))
            {
                NowControls.DrawLeftLabel(context.themeAsset, content, context.label, NowTextStyle.Body, titleColor);
                return;
            }

            var title = content;
            title.height = Mathf.Max(0f, content.height * 0.5f);
            title.y += 1f;
            var detail = new NowRect(content.x, title.yMax - 1f, content.width, Mathf.Max(0f, content.yMax - title.yMax));
            Color detailColor = highlighted
                ? context.themeAsset.GetColor(NowColorToken.AccentText)
                : context.themeAsset.GetColor(NowColorToken.TextMuted);
            NowControls.DrawLeftLabel(context.themeAsset, title, context.label, NowTextStyle.Body, titleColor);
            NowControls.DrawLeftLabel(context.themeAsset, detail, context.detail, NowTextStyle.Caption, detailColor);
        }

        internal static void DrawDropdownTriangle(NowThemeAsset themeAsset, NowRect rect)
        {
            // EditorStyles.popup uses a fixed filled down-arrow, including
            // while its menu is open. Its calibrated dark-skin glyph is #C4.
            Color color = Color.LerpUnclamped(
                themeAsset.GetColor(NowColorToken.TextMuted),
                themeAsset.GetColor(NowColorToken.Text),
                8f / 15f);
            float centerX = rect.x + 4f;
            float centerY = rect.center.y;
            Now.Triangle(
                    new Vector2(centerX - 3f, centerY - 1.5f),
                    new Vector2(centerX + 3f, centerY - 1.5f),
                    new Vector2(centerX, centerY + 2.5f))
                .SetColor(color)
                .Draw();
        }

        static void DrawEditorCheckMark(NowRect rect, Color color, bool menu)
        {
            float expectedWidth = menu ? 15f : 16f;
            float expectedHeight = menu ? 16f : 15f;
            if (Mathf.Abs(Now.uiScale - 1f) <= 0.0001f &&
                !Now.hasTransform &&
                Mathf.Abs(rect.width - expectedWidth) <= 0.0001f &&
                Mathf.Abs(rect.height - expectedHeight) <= 0.0001f)
            {
                Texture2D texture = GetEditorCheckTexture(menu);
                var pixelRect = new NowRect(
                    Mathf.Round(rect.x),
                    Mathf.Round(rect.y),
                    texture.width,
                    texture.height);
                Now.Rectangle(pixelRect)
                    .SetColor(color)
                    .SetTexture(texture)
                    .Draw();
                return;
            }

            Span<Vector2> points = stackalloc Vector2[3];

            if (menu)
            {
                points[0] = SnapToScreenPixel(new Vector2(rect.x + 5f, rect.center.y));
                points[1] = SnapToScreenPixel(new Vector2(rect.x + 7f, rect.center.y + 2f));
                points[2] = SnapToScreenPixel(new Vector2(rect.x + 10f, rect.center.y - 4f));
            }
            else
            {
                points[0] = SnapToScreenPixel(new Vector2(rect.x + 3f, rect.center.y));
                points[1] = SnapToScreenPixel(new Vector2(rect.x + 5f, rect.center.y + 2f));
                points[2] = SnapToScreenPixel(new Vector2(rect.x + 10f, rect.center.y - 3f));
            }

            float width = menu ? 2f : 1.55f;
            Now.DrawPolyline(points.Slice(0, 2), width, NowLineCap.Butt, color);
            Now.DrawPolyline(points.Slice(1, 2), width, NowLineCap.Butt, color);
        }

        static Texture2D GetEditorCheckTexture(bool menu)
        {
            if (menu)
            {
                if (MenuCheckTexture == null)
                    MenuCheckTexture = CreateEditorCheckTexture("Now Unity Editor Menu Check", 15, 16, MenuCheckMask);

                return MenuCheckTexture;
            }

            if (ToggleCheckTexture == null)
                ToggleCheckTexture = CreateEditorCheckTexture("Now Unity Editor Toggle Check", 16, 15, ToggleCheckMask);

            return ToggleCheckTexture;
        }

        static Texture2D CreateEditorCheckTexture(string name, int width, int height, byte[] mask)
        {
            var pixels = new Color32[width * height];

            for (int i = 0; i < mask.Length; i += 3)
            {
                int x = mask[i];
                int topY = mask[i + 1];
                byte alpha = mask[i + 2];
                pixels[x + (height - 1 - topY) * width] = new Color32(255, 255, 255, alpha);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEditorCheckTextures()
        {
            DestroyEditorCheckTexture(ref ToggleCheckTexture);
            DestroyEditorCheckTexture(ref MenuCheckTexture);
        }

        static void DestroyEditorCheckTexture(ref Texture2D texture)
        {
            if (texture == null)
                return;

            Now.ReleaseTextureMaterials(texture);

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);

            texture = null;
        }

        static Vector2 SnapToScreenPixel(Vector2 point)
        {
            float scale = Mathf.Max(0.0001f, Now.uiScale);
            return new Vector2(
                Mathf.Round(point.x * scale) / scale,
                Mathf.Round(point.y * scale) / scale);
        }

        static void DrawDisclosureTriangle(NowRect rect, Color color, bool expanded)
        {
            float centerX = rect.x + 4.5f;
            float centerY = rect.center.y;

            if (expanded)
            {
                Now.Triangle(
                        new Vector2(centerX - 3.75f, centerY - 2.5f),
                        new Vector2(centerX + 3.75f, centerY - 2.5f),
                        new Vector2(centerX, centerY + 3.65f))
                    .SetColor(color)
                    .Draw();
                return;
            }

            Now.Triangle(
                    new Vector2(centerX - 2.5f, centerY - 3.25f),
                    new Vector2(centerX - 2.5f, centerY + 3.25f),
                    new Vector2(centerX + 3.5f, centerY))
                .SetColor(color)
                .Draw();
        }

        static NowRect InsetScrollbarCrossAxis(
            NowScrollbarAxis axis,
            NowRect rect,
            float amount)
        {
            return axis == NowScrollbarAxis.Horizontal
                ? new NowRect(
                    rect.x,
                    rect.y + amount,
                    rect.width,
                    Mathf.Max(0f, rect.height - amount * 2f))
                : new NowRect(
                    rect.x + amount,
                    rect.y,
                    Mathf.Max(0f, rect.width - amount * 2f),
                    rect.height);
        }
    }
}
