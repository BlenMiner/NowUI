using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/Material Control Renderer", fileName = "NowMaterialControlRenderer")]
    public sealed class NowMaterialControlRenderer : NowControlRenderer
    {
        public override Vector2 MeasureButton(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            var text = NowControls.Text(themeAsset, textStyle);
            Vector2 labelSize = text.Measure(label ?? string.Empty);
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            return new Vector2(
                labelSize.x + padding.x + padding.z,
                Mathf.Max(themeAsset.controlStyles.buttonMinHeight, labelSize.y + padding.y + padding.w));
        }

        public override Vector2 MeasureButtonContent(NowThemeAsset themeAsset, Vector2 cachedContentSize)
        {
            Vector4 padding = themeAsset.controlStyles.buttonPadding;
            Vector2 fallback = new Vector2(themeAsset.controlStyles.buttonFallbackContentWidth, themeAsset.controlStyles.buttonFallbackContentHeight);
            Vector2 contentSize = cachedContentSize.x > 0f ? cachedContentSize : fallback;
            return new Vector2(
                contentSize.x + padding.x + padding.z,
                Mathf.Max(themeAsset.controlStyles.buttonMinHeight, contentSize.y + padding.y + padding.w));
        }

        public override Vector2 MeasureTextField(NowThemeAsset themeAsset, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.textFieldPadding;
            return new Vector2(240f, Mathf.Max(themeAsset.controlStyles.textFieldMinHeight, lineHeight + padding.y + padding.w));
        }

        public override Vector2 MeasureDropdownField(NowThemeAsset themeAsset, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.dropdownFieldPadding;
            return new Vector2(240f, Mathf.Max(themeAsset.controlStyles.dropdownFieldMinHeight, lineHeight + padding.y + padding.w));
        }

        public override NowRect TextFieldInnerRect(NowThemeAsset themeAsset, NowRect rect, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.textFieldPadding;
            float top = (rect.height - lineHeight) * 0.5f;
            return rect.Inset(padding.x, top, padding.z, top);
        }

        public override NowRect DropdownFieldInnerRect(NowThemeAsset themeAsset, NowRect rect, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.dropdownFieldPadding;
            float top = (rect.height - lineHeight) * 0.5f;
            return rect.Inset(padding.x, top, Mathf.Max(padding.z, 32f), top);
        }

        public override void DrawButton(in NowButtonRenderContext context)
        {
            var rectangle = ButtonRectangle(context.themeAsset, context.rect, context.rectangleStyle);
            rectangle.radius = Radius(context.themeAsset, context.themeAsset.controlStyles.buttonRadius, context.rect, Mathf.Min(context.rect.width, context.rect.height) * 0.5f);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref rectangle, field: false);

            rectangle.Draw();

            bool rippleEnabled = context.themeAsset.controlStyles.rippleDuration > 0f &&
                context.themeAsset.controlStyles.rippleOpacity > 0f;
            DrawStateLayer(
                context.themeAsset,
                context.rect,
                rectangle.radius,
                ButtonStateLayerColor(context.themeAsset, context.rectangleStyle),
                context.hoverT,
                context.interaction.held && !rippleEnabled);
            DrawButtonRipple(context, rectangle.radius);

            if (!string.IsNullOrEmpty(context.label))
                DrawButtonLabel(context);
        }

        protected override Color ResolveDefaultButtonTextColor(NowThemeAsset themeAsset, NowRectangleStyle rectangleStyle)
        {
            switch (rectangleStyle)
            {
                case NowRectangleStyle.Accent:
                    return themeAsset.GetColor(NowColorToken.AccentText, Color.white);
                case NowRectangleStyle.Outline:
                case NowRectangleStyle.Surface:
                    return themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                default:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
            }
        }

        public override void DrawCheckbox(in NowToggleRenderContext context)
        {
            DrawStateLayer(
                context.themeAsset,
                StateLayerRect(context.glyphRect, context.themeAsset.controlStyles.toggleStateLayerSize),
                Circle(StateLayerRect(context.glyphRect, context.themeAsset.controlStyles.toggleStateLayerSize)),
                context.themeAsset.GetColor(NowColorToken.Accent, Color.blue),
                context.hoverT,
                context.interaction.held);

            var box = Now.Rectangle(context.glyphRect)
                .SetRadius(context.themeAsset.controlStyles.checkboxMarkRadius)
                .SetColor(context.value ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue) : Color.clear);

            box.outline = context.value ? 0f : 2f;
            box.outlineColor = context.themeAsset.GetColor(NowColorToken.Border, Color.gray);

            if (context.focused)
                ApplyFocus(context.themeAsset, ref box, field: false);

            box.Draw();

            if (!context.value)
                return;

            Color check = context.themeAsset.GetColor(NowColorToken.AccentText, Color.white);
            float x = context.glyphRect.x;
            float y = context.glyphRect.y;
            float w = context.glyphRect.width;
            float h = context.glyphRect.height;
            Vector2 a = new Vector2(x + w * 0.28f, y + h * 0.53f);
            Vector2 b = new Vector2(x + w * 0.43f, y + h * 0.68f);
            Vector2 c = new Vector2(x + w * 0.74f, y + h * 0.32f);

            Now.Line(a, b).SetColor(check).SetWidth(2f).SetCap(NowLineCap.Round).Draw();
            Now.Line(b, c).SetColor(check).SetWidth(2f).SetCap(NowLineCap.Round).Draw();
        }

        public override void DrawRadio(in NowToggleRenderContext context)
        {
            DrawStateLayer(
                context.themeAsset,
                StateLayerRect(context.glyphRect, context.themeAsset.controlStyles.toggleStateLayerSize),
                Circle(StateLayerRect(context.glyphRect, context.themeAsset.controlStyles.toggleStateLayerSize)),
                context.themeAsset.GetColor(NowColorToken.Accent, Color.blue),
                context.hoverT,
                context.interaction.held);

            float radius = Mathf.Min(context.glyphRect.width, context.glyphRect.height) * 0.5f;
            Color accent = context.themeAsset.GetColor(NowColorToken.Accent, Color.blue);

            var frame = Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(Color.clear)
                .SetOutline(2f)
                .SetOutlineColor(context.value ? accent : context.themeAsset.GetColor(NowColorToken.Border, Color.gray));

            if (context.focused)
                ApplyFocus(context.themeAsset, ref frame, field: false);

            frame.Draw();

            if (!context.value)
                return;

            float dot = context.glyphRect.width * 0.48f;
            Now.Rectangle(new NowRect(
                    context.glyphRect.x + (context.glyphRect.width - dot) * 0.5f,
                    context.glyphRect.y + (context.glyphRect.height - dot) * 0.5f,
                    dot,
                    dot))
                .SetRadius(dot * 0.5f)
                .SetColor(accent)
                .Draw();
        }

        public override void DrawSlider(in NowSliderRenderContext context)
        {
            float trackRadius = context.metrics.track.height * 0.5f;
            var track = Now.Rectangle(context.metrics.track)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.9f, 0.9f, 0.9f, 1f)));
            track.Draw();

            var fill = Now.Rectangle(context.metrics.fill)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue));
            fill.Draw();

            float knobRadius = context.metrics.knob.width * 0.5f;
            var knob = Now.Rectangle(context.metrics.knob)
                .SetRadius(knobRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue));

            if (context.focused)
                ApplyFocus(context.themeAsset, ref knob, field: false);

            knob.Draw();
            DrawStateLayer(
                context.themeAsset,
                StateLayerRect(context.metrics.knob, context.themeAsset.controlStyles.sliderStateLayerSize),
                Circle(StateLayerRect(context.metrics.knob, context.themeAsset.controlStyles.sliderStateLayerSize)),
                context.themeAsset.GetColor(NowColorToken.Accent, Color.blue),
                context.hoverT,
                context.interaction.held);
        }

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            var box = Now.Rectangle(context.rect)
                .SetRadius(Radius(context.themeAsset, context.themeAsset.controlStyles.fieldRadius, context.rect, 4f))
                .SetColor(context.themeAsset.GetColor(NowColorToken.Surface, Color.white))
                .SetOutline(context.focused ? context.themeAsset.controlStyles.focusOutline : 1f)
                .SetOutlineColor(context.focused
                    ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                    : context.themeAsset.GetColor(NowColorToken.Border, Color.gray));
            box.Draw();
        }

        public override void DrawDropdownField(in NowDropdownFieldRenderContext context)
        {
            DrawTextInputFrame(new NowControlFrameRenderContext(context.themeAsset, context.rect, context.focused || context.open));

            NowRect inner = DropdownFieldInnerRect(context.themeAsset, context.rect, LabelHeight(context.themeAsset));
            NowControls.DrawLeftLabel(context.themeAsset, inner, context.current, NowTextStyle.Body);
            DrawChevron(context.themeAsset, new NowRect(context.rect.xMax - 24f, context.rect.y, 16f, context.rect.height), context.open);
        }

        public override void DrawPopupBackground(NowThemeAsset themeAsset, NowRect rect, bool menu)
        {
            var background = Now.Rectangle(rect)
                .SetRadius(Radius(themeAsset, themeAsset.controlStyles.popupRadius, rect, 4f))
                .SetColor(themeAsset.GetColor(NowColorToken.Surface, Color.white))
                .SetOutline(1f)
                .SetOutlineColor(themeAsset.GetColor(NowColorToken.Border, Color.gray));
            background.Draw();
        }

        public override void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            bool highlighted = context.selected || context.interaction.hovered;

            if (highlighted)
            {
                Color color = context.themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                color.a = context.selected ? 0.16f : context.themeAsset.controlStyles.hoverStateOpacity;

                Now.Rectangle(context.rect)
                    .SetRadius(context.themeAsset.controlStyles.popupItemRadius)
                    .SetColor(color)
                    .Draw();
            }

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                : context.themeAsset.GetColor(NowColorToken.Text, Color.black);
            DrawItemLabel(context.themeAsset, context.rect, context.label, textColor);
        }

        public override void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            if (context.interaction.hovered)
            {
                Color color = context.themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                color.a = context.interaction.held
                    ? context.themeAsset.controlStyles.pressedStateOpacity
                    : context.themeAsset.controlStyles.hoverStateOpacity;

                Now.Rectangle(context.rect)
                    .SetRadius(context.themeAsset.controlStyles.popupItemRadius)
                    .SetColor(color)
                    .Draw();
            }

            DrawItemLabel(context.themeAsset, context.rect, context.label, context.themeAsset.GetColor(NowColorToken.Text, Color.black));
        }

        public override void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            if (!context.metrics.visible)
                return;

            float radius = context.axis == NowScrollbarAxis.Vertical
                ? context.metrics.track.width * 0.5f
                : context.metrics.track.height * 0.5f;

            Now.Rectangle(context.metrics.track)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.gray))
                .Draw();

            Now.Rectangle(context.metrics.thumb)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw();
        }

        static NowRectangle ButtonRectangle(NowThemeAsset themeAsset, NowRect rect, NowRectangleStyle rectangleStyle)
        {
            var rectangle = themeAsset.Rectangle(rect, rectangleStyle);

            switch (rectangleStyle)
            {
                case NowRectangleStyle.Surface:
                    rectangle.color = Color.clear;
                    rectangle.outline = 0f;
                    break;
                case NowRectangleStyle.Outline:
                    rectangle.color = Color.clear;
                    rectangle.outline = 1f;
                    rectangle.outlineColor = themeAsset.GetColor(NowColorToken.Border, Color.gray);
                    break;
            }

            return rectangle;
        }

        static Vector4 Radius(NowThemeAsset themeAsset, NowThemeRadiusReference reference, NowRect rect, float fallback)
        {
            Vector4 radius = reference.Resolve(themeAsset);
            if (radius.x >= 999f || radius.y >= 999f || radius.z >= 999f || radius.w >= 999f)
                return Circle(rect);

            if (radius == default)
                return new Vector4(fallback, fallback, fallback, fallback);

            return radius;
        }

        static Vector4 Circle(NowRect rect)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            return new Vector4(radius, radius, radius, radius);
        }

        static NowRect StateLayerRect(NowRect contentRect, float size)
        {
            if (size <= 0f)
                return contentRect;

            float x = contentRect.x + (contentRect.width - size) * 0.5f;
            float y = contentRect.y + (contentRect.height - size) * 0.5f;
            return new NowRect(x, y, size, size);
        }

        static void DrawStateLayer(NowThemeAsset themeAsset, NowRect rect, Vector4 radius, Color color, float hoverT, bool pressed)
        {
            var styles = themeAsset.controlStyles;
            float opacity = pressed ? styles.pressedStateOpacity : styles.hoverStateOpacity * Mathf.Clamp01(hoverT);
            if (opacity <= 0f)
                return;

            color.a *= opacity;
            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(color)
                .Draw();
        }

        static void DrawButtonRipple(in NowButtonRenderContext context, Vector4 radius)
        {
            var styles = context.themeAsset.controlStyles;
            if (styles.rippleDuration <= 0f || styles.rippleOpacity <= 0f)
                return;

            bool trigger = context.interaction.pressed || context.submitted;
            Vector2 origin = context.interaction.pressed && context.interaction.hasPointer
                ? context.interaction.pointerPosition
                : context.rect.center;
            var ripple = NowControlState.PressAnimation(
                context.interaction,
                "button-ripple",
                trigger,
                origin,
                styles.rippleDuration);

            if (!ripple.active)
                return;

            float eased = EaseOutCubic(ripple.progress);
            float maxRadius = MaxDistanceToCorner(context.rect, ripple.origin);
            float rippleRadius = Mathf.Lerp(Mathf.Min(context.rect.width, context.rect.height) * 0.18f, maxRadius, eased);
            float fade = 1f - Mathf.SmoothStep(0.65f, 1f, ripple.progress);
            Color color = ButtonStateLayerColor(context.themeAsset, context.rectangleStyle);
            color.a *= styles.rippleOpacity * fade;

            Now.Ripple(context.rect)
                .SetRadius(radius)
                .SetOrigin(ripple.origin)
                .SetCircleRadius(rippleRadius)
                .SetColor(color)
                .Draw();
        }

        static float EaseOutCubic(float value)
        {
            value = 1f - Mathf.Clamp01(value);
            return 1f - value * value * value;
        }

        static float MaxDistanceToCorner(NowRect rect, Vector2 point)
        {
            float left = Mathf.Abs(point.x - rect.x);
            float right = Mathf.Abs(point.x - rect.xMax);
            float top = Mathf.Abs(point.y - rect.y);
            float bottom = Mathf.Abs(point.y - rect.yMax);
            float dx = Mathf.Max(left, right);
            float dy = Mathf.Max(top, bottom);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static Color ButtonStateLayerColor(NowThemeAsset themeAsset, NowRectangleStyle rectangleStyle)
        {
            return rectangleStyle == NowRectangleStyle.Accent
                ? themeAsset.GetColor(NowColorToken.AccentText, Color.white)
                : themeAsset.GetColor(NowColorToken.Accent, Color.blue);
        }

        static void DrawChevron(NowThemeAsset themeAsset, NowRect rect, bool up)
        {
            Color color = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
            float cx = rect.center.x;
            float cy = rect.center.y;
            float w = Mathf.Min(rect.width, rect.height) * 0.32f;
            float h = w * 0.7f;

            Vector2 left = new Vector2(cx - w, up ? cy + h * 0.5f : cy - h * 0.5f);
            Vector2 mid = new Vector2(cx, up ? cy - h * 0.5f : cy + h * 0.5f);
            Vector2 right = new Vector2(cx + w, up ? cy + h * 0.5f : cy - h * 0.5f);

            Now.Line(left, mid).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
            Now.Line(mid, right).SetColor(color).SetWidth(1.6f).SetCap(NowLineCap.Round).Draw();
        }

        static void ApplyFocus(NowThemeAsset themeAsset, ref NowRectangle rectangle, bool field)
        {
            var styles = themeAsset.controlStyles;
            rectangle.outline = Mathf.Max(rectangle.outline, styles.focusOutline);
            rectangle.outlineColor = field
                ? styles.fieldFocusColor.Resolve(themeAsset)
                : styles.focusColor.Resolve(themeAsset);
        }

        static void DrawItemLabel(NowThemeAsset themeAsset, NowRect rect, string label, Color color)
        {
            float left = themeAsset.controlStyles.contextMenuPaddingX;
            NowControls.DrawLeftLabel(themeAsset, rect.Inset(left, 0f, 8f, 0f), label, NowTextStyle.Body, color);
        }
    }
}
