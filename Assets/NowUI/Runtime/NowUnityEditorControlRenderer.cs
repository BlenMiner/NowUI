using UnityEngine;

namespace NowUI
{
    [CreateAssetMenu(menuName = "NowUI/Unity Editor Control Renderer", fileName = "NowUnityEditorControlRenderer")]
    public sealed class NowUnityEditorControlRenderer : NowControlRenderer
    {
        public override Vector2 MeasureButton(NowThemeAsset themeAsset, string label, NowTextStyle textStyle)
        {
            var text = themeAsset.Text(default, textStyle);
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
            return new Vector2(180f, Mathf.Max(themeAsset.controlStyles.textFieldMinHeight, lineHeight + padding.y + padding.w));
        }

        public override Vector2 MeasureDropdownField(NowThemeAsset themeAsset, float lineHeight)
        {
            Vector4 padding = themeAsset.controlStyles.dropdownFieldPadding;
            return new Vector2(180f, Mathf.Max(themeAsset.controlStyles.dropdownFieldMinHeight, lineHeight + padding.y + padding.w));
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
            return rect.Inset(padding.x, top, Mathf.Max(padding.z, 20f), top);
        }

        public override void DrawButton(in NowButtonRenderContext context)
        {
            Vector4 radius = Radius(context.themeAsset, context.themeAsset.controlStyles.buttonRadius, context.rect, 2f);
            Color fill = ButtonFill(context.themeAsset, context.rectangleStyle);

            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(fill)
                .SetOutline(1f)
                .SetOutlineColor(ButtonBorder(context.themeAsset, context.focused, context.rectangleStyle))
                .Draw();

            DrawInteractionOverlay(context.themeAsset, context.rect, radius, context.hoverT, context.interaction.held);

            if (!string.IsNullOrEmpty(context.label))
                DrawButtonLabel(context);
        }

        protected override Color ResolveDefaultButtonTextColor(NowThemeAsset themeAsset, NowRectangleStyle rectangleStyle)
        {
            return themeAsset.GetColor(NowColorToken.Text, Color.black);
        }

        public override void DrawCheckbox(in NowToggleRenderContext context)
        {
            float radius = context.themeAsset.controlStyles.checkboxMarkRadius;
            Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Surface, Color.gray))
                .SetOutline(1f)
                .SetOutlineColor(ButtonBorder(context.themeAsset, context.focused))
                .Draw();

            if (!context.value)
                return;

            Color check = context.focused
                ? context.themeAsset.GetColor(NowColorToken.AccentText, Color.white)
                : context.themeAsset.GetColor(NowColorToken.Text, Color.black);

            if (context.focused)
            {
                Now.Rectangle(context.glyphRect.Inset(2f))
                    .SetRadius(Mathf.Max(0f, radius - 1f))
                    .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue))
                    .Draw();
            }

            DrawCheckMark(context.glyphRect, check);
        }

        public override void DrawRadio(in NowToggleRenderContext context)
        {
            float radius = Mathf.Min(context.glyphRect.width, context.glyphRect.height) * 0.5f;
            Now.Rectangle(context.glyphRect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Surface, Color.gray))
                .SetOutline(1f)
                .SetOutlineColor(ButtonBorder(context.themeAsset, context.focused))
                .Draw();

            if (!context.value)
                return;

            Color accent = context.themeAsset.GetColor(NowColorToken.Accent, Color.blue);
            float dot = context.glyphRect.width * 0.45f;
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
            Now.Rectangle(context.metrics.track)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Border, Color.gray))
                .Draw();

            Now.Rectangle(context.metrics.fill)
                .SetRadius(trackRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue))
                .Draw();

            float knobRadius = Mathf.Min(context.metrics.knob.width, context.metrics.knob.height) * 0.2f;
            Now.Rectangle(context.metrics.knob)
                .SetRadius(knobRadius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.gray))
                .SetOutline(1f)
                .SetOutlineColor(ButtonBorder(context.themeAsset, context.focused))
                .Draw();
        }

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            Vector4 radius = Radius(context.themeAsset, context.themeAsset.controlStyles.fieldRadius, context.rect, 2f);
            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Background, Color.gray))
                .SetOutline(1f)
                .SetOutlineColor(ButtonBorder(context.themeAsset, context.focused))
                .Draw();
        }

        public override void DrawDropdownField(in NowDropdownFieldRenderContext context)
        {
            Vector4 radius = Radius(context.themeAsset, context.themeAsset.controlStyles.fieldRadius, context.rect, 2f);
            Now.Rectangle(context.rect)
                .SetRadius(radius)
                .SetColor(ButtonFill(context.themeAsset, NowRectangleStyle.Accent))
                .SetOutline(1f)
                .SetOutlineColor(ButtonBorder(context.themeAsset, context.focused || context.open))
                .Draw();

            DrawInteractionOverlay(context.themeAsset, context.rect, radius, context.hoverT, context.interaction.held);

            NowRect inner = DropdownFieldInnerRect(context.themeAsset, context.rect, LabelHeight(context.themeAsset));
            NowControls.DrawLeftLabel(context.themeAsset, inner, context.current, NowTextStyle.Body);
            DrawEditorChevron(context.themeAsset, new NowRect(context.rect.xMax - 16f, context.rect.y, 12f, context.rect.height), context.open);
        }

        public override void DrawPopupBackground(NowThemeAsset themeAsset, NowRect rect, bool menu)
        {
            Vector4 radius = Radius(themeAsset, themeAsset.controlStyles.popupRadius, rect, 2f);
            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(themeAsset.GetColor(NowColorToken.Surface, Color.gray))
                .SetOutline(1f)
                .SetOutlineColor(themeAsset.GetColor(NowColorToken.Border, Color.black))
                .Draw();
        }

        public override void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            if (context.selected || context.interaction.hovered)
            {
                Color color = context.selected
                    ? context.themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                    : HoverColor(context.themeAsset);

                Now.Rectangle(context.rect)
                    .SetRadius(context.themeAsset.controlStyles.popupItemRadius)
                    .SetColor(color)
                    .Draw();
            }

            Color textColor = context.selected
                ? context.themeAsset.GetColor(NowColorToken.AccentText, Color.white)
                : context.themeAsset.GetColor(NowColorToken.Text, Color.black);
            DrawItemLabel(context.themeAsset, context.rect, context.label, textColor);
        }

        public override void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            if (context.interaction.hovered)
            {
                Now.Rectangle(context.rect)
                    .SetRadius(context.themeAsset.controlStyles.popupItemRadius)
                    .SetColor(HoverColor(context.themeAsset))
                    .Draw();
            }

            DrawItemLabel(context.themeAsset, context.rect, context.label, context.themeAsset.GetColor(NowColorToken.Text, Color.black));
        }

        public override void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            if (!context.metrics.visible)
                return;

            float radius = context.axis == NowScrollbarAxis.Vertical
                ? context.metrics.thumb.width * 0.15f
                : context.metrics.thumb.height * 0.15f;

            Now.Rectangle(context.metrics.track)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.Background, Color.gray))
                .Draw();

            Now.Rectangle(context.metrics.thumb)
                .SetRadius(radius)
                .SetColor(context.themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.gray))
                .SetOutline(1f)
                .SetOutlineColor(context.themeAsset.GetColor(NowColorToken.Border, Color.black))
                .Draw();
        }

        public override void DrawSelection(NowThemeAsset themeAsset, NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(themeAsset.GetColor(NowColorToken.Accent, Color.blue))
                .Draw();
        }

        static Color ButtonFill(NowThemeAsset themeAsset, NowRectangleStyle style)
        {
            switch (style)
            {
                case NowRectangleStyle.Outline:
                    return Color.clear;
                case NowRectangleStyle.Surface:
                    return themeAsset.GetColor(NowColorToken.Surface, Color.gray);
                default:
                    return themeAsset.GetColor(NowColorToken.SurfaceMuted, Color.gray);
            }
        }

        static Color ButtonBorder(NowThemeAsset themeAsset, bool focused)
        {
            return ButtonBorder(themeAsset, focused, NowRectangleStyle.Surface);
        }

        static Color ButtonBorder(NowThemeAsset themeAsset, bool focused, NowRectangleStyle style)
        {
            return focused
                || style == NowRectangleStyle.Accent
                ? themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                : themeAsset.GetColor(NowColorToken.Border, Color.black);
        }

        static Color HoverColor(NowThemeAsset themeAsset)
        {
            Color text = themeAsset.GetColor(NowColorToken.Text, Color.black);
            text.a = IsDark(themeAsset) ? 0.10f : 0.08f;
            return text;
        }

        static void DrawInteractionOverlay(NowThemeAsset themeAsset, NowRect rect, Vector4 radius, float hoverT, bool held)
        {
            float alpha = held ? 0.14f : 0.07f * Mathf.Clamp01(hoverT);
            if (alpha <= 0f)
                return;

            Color color = themeAsset.GetColor(NowColorToken.Text, Color.black);
            color.a = alpha;
            Now.Rectangle(rect)
                .SetRadius(radius)
                .SetColor(color)
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

        static Vector4 Circle(NowRect rect)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            return new Vector4(radius, radius, radius, radius);
        }

        static bool IsDark(NowThemeAsset themeAsset)
        {
            Color background = themeAsset.GetColor(NowColorToken.Background, Color.gray);
            float luminance = background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f;
            return luminance < 0.35f;
        }

        static void DrawCheckMark(NowRect rect, Color color)
        {
            float x = rect.x;
            float y = rect.y;
            float w = rect.width;
            float h = rect.height;
            Vector2 a = new Vector2(x + w * 0.25f, y + h * 0.52f);
            Vector2 b = new Vector2(x + w * 0.43f, y + h * 0.70f);
            Vector2 c = new Vector2(x + w * 0.76f, y + h * 0.30f);

            Now.Line(a, b).SetColor(color).SetWidth(1.5f).SetCap(NowLineCap.Round).Draw();
            Now.Line(b, c).SetColor(color).SetWidth(1.5f).SetCap(NowLineCap.Round).Draw();
        }

        static void DrawItemLabel(NowThemeAsset themeAsset, NowRect rect, string label, Color color)
        {
            float left = themeAsset.controlStyles.contextMenuPaddingX;
            NowControls.DrawLeftLabel(themeAsset, rect.Inset(left, 0f, 4f, 0f), label, NowTextStyle.Body, color);
        }

        static void DrawEditorChevron(NowThemeAsset themeAsset, NowRect rect, bool up)
        {
            Color color = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
            float cx = rect.center.x;
            float cy = rect.center.y;
            float w = Mathf.Min(rect.width, rect.height) * 0.28f;
            float h = w * 0.8f;

            if (up)
            {
                Now.Triangle(
                        new Vector2(cx, cy - h * 0.5f),
                        new Vector2(cx - w, cy + h * 0.5f),
                        new Vector2(cx + w, cy + h * 0.5f))
                    .SetColor(color)
                    .Draw();
                return;
            }

            Now.Triangle(
                    new Vector2(cx - w, cy - h * 0.5f),
                    new Vector2(cx + w, cy - h * 0.5f),
                    new Vector2(cx, cy + h * 0.5f))
                .SetColor(color)
                .Draw();
        }
    }
}
