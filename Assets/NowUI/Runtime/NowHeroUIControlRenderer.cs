using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/HeroUI Control Renderer", fileName = "NowHeroUIControlRenderer")]
    public sealed class NowHeroUIControlRenderer : NowControlRenderer
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
            return new Vector2(220f, Mathf.Max(themeAsset.controlStyles.textFieldMinHeight, lineHeight + padding.y + padding.w));
        }

        public override Vector2 MeasureDropdownField(NowThemeAsset themeAsset, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.dropdownFieldPadding;
            return new Vector2(220f, Mathf.Max(themeAsset.controlStyles.dropdownFieldMinHeight, lineHeight + padding.y + padding.w));
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
            NowRect visualRect = context.interaction.held ? ScaleRect(context.rect, 0.98f) : context.rect;
            var rectangle = ButtonRectangle(context.themeAsset, visualRect, context.rectangleStyle);
            rectangle.radius = Radius(context.themeAsset, context.themeAsset.controlStyles.buttonRadius, visualRect, 12f);

            rectangle.Draw();
            DrawStateLayer(context.themeAsset, visualRect, rectangle.radius, ButtonStateLayerColor(context.themeAsset, context.rectangleStyle), context.hoverT, context.interaction.held);

            if (context.focused)
                DrawFocusRing(context.themeAsset, visualRect, rectangle.radius, field: false);

            if (!string.IsNullOrEmpty(context.label))
                DrawCenteredButtonLabel(context, visualRect);
        }

        public override void DrawCheckbox(in NowToggleRenderContext context)
        {
            NowRect stateRect = StateLayerRect(context.glyphRect, context.themeAsset.controlStyles.toggleStateLayerSize);
            DrawStateLayer(context.themeAsset, stateRect, Circle(stateRect), context.themeAsset.GetColor(NowColorToken.Accent, Color.blue), context.hoverT, context.interaction.held);

            float radius = context.themeAsset.controlStyles.checkboxMarkRadius;
            var box = Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(context.value
                    ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                    : context.themeAsset.GetColor(NowColorToken.Surface, Color.white))
                .SetOutline(context.value ? 0f : 1f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Border, Color.gray));

            box.Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.glyphRect, new Vector4(radius, radius, radius, radius), field: false);

            if (!context.value)
                return;

            DrawCheckMark(context.themeAsset, context.glyphRect);
        }

        public override void DrawRadio(in NowToggleRenderContext context)
        {
            NowRect stateRect = StateLayerRect(context.glyphRect, context.themeAsset.controlStyles.toggleStateLayerSize);
            DrawStateLayer(context.themeAsset, stateRect, Circle(stateRect), context.themeAsset.GetColor(NowColorToken.Accent, Color.blue), context.hoverT, context.interaction.held);

            float radius = Mathf.Min(context.glyphRect.width, context.glyphRect.height) * 0.5f;
            Color accent = context.themeAsset.GetColor(NowColorToken.Accent, Color.blue);

            var frame = Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Surface, Color.white))
                .SetOutline(2f)
                .SetOutlineColor(context.value ? accent : context.themeAsset.GetColor(NowColorToken.Border, Color.gray));

            frame.Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.glyphRect, Circle(context.glyphRect), field: false);

            if (!context.value)
                return;

            float dot = context.glyphRect.width * 0.5f;
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
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.92f, 0.92f, 0.93f, 1f)));
            track.Draw();

            var fill = Now.Rectangle(context.metrics.fill)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue));
            fill.Draw();

            float knobRadius = context.metrics.knob.width * 0.5f;
            DrawSoftShadow(context.themeAsset, context.metrics.knob, new Vector4(knobRadius, knobRadius, knobRadius, knobRadius), 0.08f);

            var knob = Now.Rectangle(context.metrics.knob)
                .SetRadius(knobRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue));
            knob.Draw();

            DrawStateLayer(context.themeAsset, StateLayerRect(context.metrics.knob, context.themeAsset.controlStyles.sliderStateLayerSize), Circle(StateLayerRect(context.metrics.knob, context.themeAsset.controlStyles.sliderStateLayerSize)), context.themeAsset.GetColor(NowColorToken.Accent, Color.blue), context.hoverT, context.interaction.held);

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.metrics.knob, new Vector4(knobRadius, knobRadius, knobRadius, knobRadius), field: false);
        }

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            Vector4 radius = Radius(context.themeAsset, context.themeAsset.controlStyles.fieldRadius, context.rect, 12f);

            var box = Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.white))
                .SetOutline(context.focused ? 1f : 0f)
                .SetOutlineColor(context.focused
                    ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                    : context.themeAsset.GetColor(NowColorToken.Border, Color.gray));
            box.Draw();

            if (context.focused)
                DrawFocusRing(context.themeAsset, context.rect, radius, field: true);
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
            Vector4 radius = Radius(themeAsset, themeAsset.controlStyles.popupRadius, rect, 12f);
            DrawSoftShadow(themeAsset, rect, radius, 0.10f);

            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(themeAsset.GetColor(NowColorToken.Surface, Color.white))
                .SetOutline(1f)
                .SetOutlineColor(themeAsset.GetColor(NowColorToken.Border, Color.gray))
                .Draw();
        }

        public override void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            if (context.selected || context.interaction.hovered)
            {
                Color color = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                    : context.themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.gray);
                color.a = context.selected ? 0.14f : 1f;

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
            if (context.interaction.hovered || context.selected)
            {
                Color color = context.themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.gray);
                if (context.interaction.held)
                    color = Mix(color, context.themeAsset.GetColor(NowColorToken.Text, Color.black), 0.04f);

                Now.Rectangle(context.rect)
                    .SetRadius(context.themeAsset.controlStyles.popupItemRadius)
                    .SetColor(color)
                    .Draw();
            }

            DrawItemLabel(
                context.themeAsset,
                context.rect,
                context.label,
                context.themeAsset.GetColor(NowColorToken.Text, Color.black),
                context.hasSubmenu);
        }

        public override void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            if (!context.metrics.visible)
                return;

            float radius = context.axis == NowScrollbarAxis.Vertical
                ? context.metrics.thumb.width * 0.5f
                : context.metrics.thumb.height * 0.5f;

            Color thumb = context.themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
            thumb.a = IsDark(context.themeAsset) ? 0.42f : 0.32f;

            Now.Rectangle(context.metrics.thumb)
                .SetRadius(radius)
                .SetColor(thumb)
                .Draw();
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

        static NowRect ScaleRect(NowRect rect, float scale)
        {
            float width = rect.width * scale;
            float height = rect.height * scale;
            return new NowRect(
                rect.x + (rect.width - width) * 0.5f,
                rect.y + (rect.height - height) * 0.5f,
                width,
                height);
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

        static Color ButtonStateLayerColor(NowThemeAsset themeAsset, NowRectangleStyle rectangleStyle)
        {
            return rectangleStyle == NowRectangleStyle.Accent
                ? themeAsset.GetColor(NowColorToken.AccentText, Color.white)
                : themeAsset.GetColor(NowColorToken.Accent, Color.blue);
        }

        static void DrawFocusRing(NowThemeAsset themeAsset, NowRect rect, Vector4 radius, bool field)
        {
            var styles = themeAsset.controlStyles;
            Color color = field
                ? styles.fieldFocusColor.Resolve(themeAsset)
                : styles.focusColor.Resolve(themeAsset);
            color.a = 0.82f;
            Vector2 transformScale = Now.currentTransform.scale;
            float scale = Mathf.Max(Mathf.Abs(transformScale.x), Mathf.Abs(transformScale.y));

            Now.Rectangle(rect.Outset(2f * scale))
                .SetRadius(radius + new Vector4(2f * scale, 2f * scale, 2f * scale, 2f * scale))
                .SetColor(Color.clear)
                .SetOutline(styles.focusOutline)
                .SetOutlineColor(color)
                .Draw();
        }

        static void DrawSoftShadow(NowThemeAsset themeAsset, NowRect rect, Vector4 radius, float alpha)
        {
            if (IsDark(themeAsset))
                return;

            Now.Rectangle(new NowRect(rect.x, rect.y + 3f, rect.width, rect.height).Outset(1f))
                .SetRadius(radius + new Vector4(1f, 1f, 1f, 1f))
                .SetBlur(5f)
                .SetColor(new Color(0f, 0f, 0f, alpha))
                .Draw();
        }

        static bool IsDark(NowThemeAsset themeAsset)
        {
            Color background = themeAsset.GetColor(NowColorToken.Background, Color.white);
            float luminance = background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f;
            return luminance < 0.35f;
        }

        static Color Mix(Color a, Color b, float t)
        {
            return new Color(
                Mathf.Lerp(a.r, b.r, t),
                Mathf.Lerp(a.g, b.g, t),
                Mathf.Lerp(a.b, b.b, t),
                Mathf.Lerp(a.a, b.a, t));
        }

        static void DrawCheckMark(NowThemeAsset themeAsset, NowRect rect)
        {
            Color check = themeAsset.GetColor(NowColorToken.AccentText, Color.white);
            float x = rect.x;
            float y = rect.y;
            float w = rect.width;
            float h = rect.height;
            Vector2 a = new Vector2(x + w * 0.28f, y + h * 0.53f);
            Vector2 b = new Vector2(x + w * 0.43f, y + h * 0.68f);
            Vector2 c = new Vector2(x + w * 0.74f, y + h * 0.32f);

            Now.Line(a, b).SetColor(check).SetWidth(2f).SetCap(NowLineCap.Round).Draw();
            Now.Line(b, c).SetColor(check).SetWidth(2f).SetCap(NowLineCap.Round).Draw();
        }

        static void DrawItemLabel(NowThemeAsset themeAsset, NowRect rect, string label, Color color, bool hasSubmenu = false)
        {
            float left = themeAsset.controlStyles.contextMenuPaddingX;
            float right = hasSubmenu ? 28f : 8f;
            NowControls.DrawLeftLabel(themeAsset, rect.Inset(left, 0f, right, 0f), label, NowTextStyle.Body, color);
        }

        static void DrawCenteredButtonLabel(in NowButtonRenderContext context, NowRect rect)
        {
            var text = NowControls.Text(context.themeAsset, context.textStyle)
                .SetColor(ButtonTextColor(context.themeAsset, context.rectangleStyle));
            Vector2 size = text.Measure(context.label);
            float pad = 1f;

            text.rect = new NowRect(
                rect.x + (rect.width - size.x) * 0.5f,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + pad,
                size.y + pad);
            text.SetMask(context.rect).Draw(context.label);
        }

        static Color ButtonTextColor(NowThemeAsset themeAsset, NowRectangleStyle rectangleStyle)
        {
            switch (rectangleStyle)
            {
                case NowRectangleStyle.Accent:
                    return themeAsset.GetColor(NowColorToken.AccentText, Color.white);
                case NowRectangleStyle.Outline:
                    return themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                default:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
            }
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
    }
}
