using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Editor;
using NowUI.Internal;

public class NowThemeAssetTests
{
    static readonly NowResolvedId TestIdentityRoot =
        NowResolvedId.CreateOwnerRoot(0x5448454D45544553UL);

    static readonly string[] SerializedColorFields =
    {
        "_background", "_surface", "_surfaceMuted", "_text", "_textMuted", "_border", "_accent", "_accentText",
        "_surfaceElevated", "_surfaceHover", "_surfacePressed", "_accentHover", "_accentPressed", "_accentMuted",
        "_borderStrong", "_focusRing", "_success", "_successText", "_successMuted", "_warning", "_warningText",
        "_warningMuted", "_danger", "_dangerText", "_dangerMuted", "_shadow", "_scrim"
    };

    [Test]
    public void ThemeDefaultsResolvePaletteTokens()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
            Assert.AreEqual(0.369f, accent.r, 0.0001f);
            Assert.AreEqual(0.416f, accent.g, 0.0001f);
            Assert.AreEqual(0.824f, accent.b, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ThemeTokenGroupsSerializeAsFixedSlots()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            var serializedTheme = new SerializedObject(theme);
            var palette = serializedTheme.FindProperty("_palette");
            var spacings = serializedTheme.FindProperty("_spacings");
            var radii = serializedTheme.FindProperty("_radii");
            var rectangles = serializedTheme.FindProperty("_rectanglePresets");
            var texts = serializedTheme.FindProperty("_textPresets");

            Assert.IsNotNull(palette);
            Assert.IsNotNull(spacings);
            Assert.IsNotNull(radii);
            Assert.IsNotNull(rectangles);
            Assert.IsNotNull(texts);
            Assert.IsFalse(palette.isArray);
            Assert.IsFalse(spacings.isArray);
            Assert.IsFalse(radii.isArray);
            Assert.IsFalse(rectangles.isArray);
            Assert.IsFalse(texts.isArray);
            Assert.IsNotNull(palette.FindPropertyRelative("_background"));
            Assert.IsNotNull(spacings.FindPropertyRelative("_panel"));
            Assert.IsNotNull(radii.FindPropertyRelative("_pill"));
            Assert.IsNotNull(rectangles.FindPropertyRelative("_accent"));
            Assert.IsNotNull(texts.FindPropertyRelative("_button"));
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ThemeInsetsRectUsingSpacingToken()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            Vector4 rect = theme.Inset(new Vector4(0, 0, 100, 60), NowSpacingToken.Md);

            Assert.AreEqual(12, rect.x, 0.0001f);
            Assert.AreEqual(12, rect.y, 0.0001f);
            Assert.AreEqual(76, rect.z, 0.0001f);
            Assert.AreEqual(36, rect.w, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ThemeAppliesRectanglePreset()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            NowRectangle rectangle = theme.Rectangle(new Vector4(4, 8, 100, 40), NowRectangleStyle.Accent);

            Assert.AreEqual(new NowRect(4, 8, 100, 40), rectangle.rect);
            Assert.AreEqual(0.369f, rectangle.color.x, 0.0001f);
            Assert.AreEqual(0.416f, rectangle.color.y, 0.0001f);
            Assert.AreEqual(0.824f, rectangle.color.z, 0.0001f);
            Assert.AreEqual(new Vector4(10, 10, 10, 10), rectangle.radius);
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ThemeAppliesTextPresetWithoutReplacingProvidedFont()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var font = ScriptableObject.CreateInstance<NowFont>();

        try
        {
            NowText text = theme.Text(new Vector4(0, 0, 100, 24), font, NowTextStyle.Button);

            Assert.AreSame(font, text.font);
            Assert.AreEqual(15, text.fontSize, 0.0001f);
            Assert.AreEqual(Color.white, (Color)text.color);
            Assert.AreEqual(NowFontStyle.Bold, text.fontStyle);
        }
        finally
        {
            Object.DestroyImmediate(font);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void DarkThemeAssetProvidesBuiltInTokens()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/DefaultDark.asset");

        Assert.IsNotNull(theme);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Text, out var text));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.AccentText, out var accentText));

        Assert.Less(background.r, 0.2f);
        Assert.Less(background.g, 0.2f);
        Assert.Less(background.b, 0.2f);
        Assert.Greater(text.r, 0.9f);
        Assert.Greater(text.g, 0.9f);
        Assert.Greater(text.b, 0.9f);
        Assert.Greater(accent.b, accent.r);
        Assert.Greater(accentText.r, 0.9f);
        Assert.Greater(accentText.g, 0.9f);
        Assert.Greater(accentText.b, 0.9f);
    }

    [Test]
    public void MaterialThemeAssetUsesMaterialRendererAndDefaults()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Material.asset");

        Assert.IsNotNull(theme);
        Assert.IsInstanceOf<NowMaterialControlRenderer>(theme.controlRenderer);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.AreEqual(0.40392157f, accent.r, 0.0001f);
        Assert.AreEqual(0.3137255f, accent.g, 0.0001f);
        Assert.AreEqual(0.6431373f, accent.b, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.buttonMinHeight, 0.0001f);
        Assert.AreEqual(56f, theme.controlStyles.textFieldMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.dropdownFieldMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.dropdownItemHeight, 0.0001f);
        Assert.AreEqual(20f, theme.controlStyles.sliderKnobSize, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.toggleStateLayerSize, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.sliderStateLayerSize, 0.0001f);
        Assert.AreEqual(0.08f, theme.controlStyles.hoverStateOpacity, 0.0001f);
        Assert.AreEqual(0.10f, theme.controlStyles.pressedStateOpacity, 0.0001f);
        Assert.AreEqual(new Vector4(999f, 999f, 999f, 999f), theme.controlStyles.buttonRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(4f, 4f, 4f, 4f), theme.controlStyles.fieldRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(4f, 4f, 4f, 4f), theme.controlStyles.popupRadius.Resolve(theme));
        Assert.AreEqual(40f, theme.controlRenderer.MeasureButton(theme, string.Empty, NowTextStyle.Button).y, 0.0001f);
        Assert.AreEqual(56f, theme.controlRenderer.MeasureTextField(theme, 20f).y, 0.0001f);
        Assert.AreEqual(40f, theme.controlRenderer.MeasureDropdownField(theme, 20f).y, 0.0001f);
    }

    [Test]
    public void MaterialDarkThemeAssetUsesMaterialRendererAndDarkRoles()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");

        Assert.IsNotNull(theme);
        Assert.IsInstanceOf<NowMaterialControlRenderer>(theme.controlRenderer);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Text, out var text));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.AccentText, out var accentText));
        Assert.Less(background.r, 0.2f);
        Assert.Less(background.g, 0.2f);
        Assert.Less(background.b, 0.2f);
        Assert.Greater(text.r, 0.85f);
        Assert.Greater(text.g, 0.85f);
        Assert.Greater(text.b, 0.85f);
        Assert.AreEqual(0.8156863f, accent.r, 0.0001f);
        Assert.AreEqual(0.7372549f, accent.g, 0.0001f);
        Assert.AreEqual(1f, accent.b, 0.0001f);
        Assert.Less(accentText.r, 0.3f);
        Assert.AreEqual(40f, theme.controlStyles.buttonMinHeight, 0.0001f);
        Assert.AreEqual(56f, theme.controlStyles.textFieldMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.dropdownFieldMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.dropdownItemHeight, 0.0001f);
        Assert.AreEqual(new Vector4(999f, 999f, 999f, 999f), theme.controlStyles.buttonRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(4f, 4f, 4f, 4f), theme.controlStyles.fieldRadius.Resolve(theme));
        Assert.AreEqual(40f, theme.controlRenderer.MeasureDropdownField(theme, 20f).y, 0.0001f);
    }

    [Test]
    public void ShippedThemeAssetsResolveEverySerializedTokenAndPreset()
    {
        var colorTokens = (NowColorToken[])System.Enum.GetValues(typeof(NowColorToken));
        Assert.AreEqual(NowThemeColorSet.TokenCount, colorTokens.Length, "NowThemeColorSet.TokenCount must track NowColorToken.");
        Assert.AreEqual(colorTokens.Length, SerializedColorFields.Length, "The serialized palette audit must track every color token.");

        string[] guids = AssetDatabase.FindAssets("t:NowThemeAsset", new[] { "Assets/NowUI/Assets/Themes" });
        Assert.IsNotEmpty(guids, "No shipped theme assets were discovered.");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(path);
            Assert.IsNotNull(theme, path);

            var serializedTheme = new SerializedObject(theme);
            SerializedProperty palette = serializedTheme.FindProperty("_palette");
            Assert.IsNotNull(palette, path);

            for (int i = 0; i < colorTokens.Length; ++i)
            {
                NowColorToken token = colorTokens[i];
                SerializedProperty property = palette.FindPropertyRelative(SerializedColorFields[i]);
                Assert.IsNotNull(property, $"{path}: missing serialized {token} slot ({SerializedColorFields[i]}).");

                Color serializedColor = property.colorValue;
                AssertFiniteColor(serializedColor, $"{path}: serialized {token}");
                Assert.Greater(serializedColor.a, 0f, $"{path}: serialized {token} is uninitialized/transparent.");

                Assert.IsTrue(theme.TryGetColor(token, out var resolvedColor), $"{path}: {token}");
                AssertFiniteColor(resolvedColor, $"{path}: resolved {token}");
                Assert.Greater(resolvedColor.a, 0f, $"{path}: resolved {token} is uninitialized/transparent.");
            }

            foreach (NowSpacingToken token in System.Enum.GetValues(typeof(NowSpacingToken)))
            {
                Assert.IsTrue(theme.TryGetSpacing(token, out var spacing), $"{path}: spacing {token}");
                AssertFiniteVector(spacing, $"{path}: spacing {token}");
                AssertNonNegative(spacing, $"{path}: spacing {token}");
            }

            foreach (NowRadiusToken token in System.Enum.GetValues(typeof(NowRadiusToken)))
            {
                Assert.IsTrue(theme.TryGetRadius(token, out var radius), $"{path}: radius {token}");
                AssertFiniteVector(radius, $"{path}: radius {token}");
                AssertNonNegative(radius, $"{path}: radius {token}");
            }

            foreach (NowElevationToken token in System.Enum.GetValues(typeof(NowElevationToken)))
            {
                if (token == NowElevationToken.None)
                    continue;

                Assert.IsTrue(theme.TryGetShadow(token, out var shadow), $"{path}: elevation {token}");
                AssertValidShadowLayer(shadow.key, $"{path}: elevation {token} key");
                AssertValidShadowLayer(shadow.ambient, $"{path}: elevation {token} ambient");
            }

            foreach (NowRectangleStyle style in System.Enum.GetValues(typeof(NowRectangleStyle)))
            {
                Assert.IsTrue(theme.TryGetRectanglePreset(style, out _), $"{path}: rectangle {style}");
                NowRectangle rectangle = theme.Rectangle(new NowRect(0f, 0f, 40f, 24f), style);
                AssertFiniteVector(rectangle.color, $"{path}: rectangle {style} fill");
                AssertFiniteVector(rectangle.radius, $"{path}: rectangle {style} radius");
                AssertFiniteVector(rectangle.padding, $"{path}: rectangle {style} padding");
                AssertFiniteVector(rectangle.outlineColor, $"{path}: rectangle {style} outline");
                AssertFinite(rectangle.blur, $"{path}: rectangle {style} blur");
                AssertFinite(rectangle.outline, $"{path}: rectangle {style} outline width");
                Assert.Greater(MaxComponent(rectangle.radius), 0f, $"{path}: rectangle {style} has no serialized radius.");
            }

            foreach (NowTextStyle style in System.Enum.GetValues(typeof(NowTextStyle)))
            {
                Assert.IsTrue(theme.TryGetTextPreset(style, out var preset), $"{path}: text {style}");
                AssertFinite(preset.fontSize, $"{path}: text {style} font size");
                Assert.Greater(preset.fontSize, 0f, $"{path}: text {style} has no serialized font size.");

                NowText text = theme.Text(new NowRect(0f, 0f, 120f, 32f), style);
                AssertFiniteVector(text.color, $"{path}: text {style} color");
                Assert.Greater(text.color.w, 0f, $"{path}: text {style} has no visible serialized color.");
            }
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/Material.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void MaterialThemeExtendedRolesMeetTextContrastMinimums(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        Assert.IsNotNull(theme);

        AssertThemeContrast(theme, NowColorToken.Text, NowColorToken.Background, 7f);

        var pairs = new[]
        {
            new[] { NowColorToken.Text, NowColorToken.Surface },
            new[] { NowColorToken.Text, NowColorToken.SurfaceMuted },
            new[] { NowColorToken.Text, NowColorToken.SurfaceElevated },
            new[] { NowColorToken.Text, NowColorToken.SurfaceHover },
            new[] { NowColorToken.Text, NowColorToken.SurfacePressed },
            new[] { NowColorToken.Text, NowColorToken.AccentMuted },
            new[] { NowColorToken.Text, NowColorToken.SuccessMuted },
            new[] { NowColorToken.Text, NowColorToken.WarningMuted },
            new[] { NowColorToken.Text, NowColorToken.DangerMuted },
            new[] { NowColorToken.TextMuted, NowColorToken.Surface },
            new[] { NowColorToken.TextMuted, NowColorToken.SurfaceMuted },
            new[] { NowColorToken.TextMuted, NowColorToken.SurfaceElevated },
            new[] { NowColorToken.TextMuted, NowColorToken.SurfaceHover },
            new[] { NowColorToken.TextMuted, NowColorToken.SurfacePressed },
            new[] { NowColorToken.AccentText, NowColorToken.Accent },
            new[] { NowColorToken.AccentText, NowColorToken.AccentHover },
            new[] { NowColorToken.AccentText, NowColorToken.AccentPressed },
            new[] { NowColorToken.Accent, NowColorToken.AccentMuted },
            new[] { NowColorToken.SuccessText, NowColorToken.Success },
            new[] { NowColorToken.WarningText, NowColorToken.Warning },
            new[] { NowColorToken.DangerText, NowColorToken.Danger }
        };

        foreach (NowColorToken[] pair in pairs)
            AssertThemeContrast(theme, pair[0], pair[1], 4.5f);
    }

    [Test]
    public void MaterialThemesUseModeSpecificExtendedRoles()
    {
        var light = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Material.asset");
        var dark = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");

        Assert.IsNotNull(light);
        Assert.IsNotNull(dark);

        foreach (NowColorToken token in new[]
        {
            NowColorToken.SurfaceElevated,
            NowColorToken.SurfaceHover,
            NowColorToken.SurfacePressed,
            NowColorToken.AccentHover,
            NowColorToken.AccentPressed,
            NowColorToken.AccentMuted,
            NowColorToken.BorderStrong,
            NowColorToken.FocusRing,
            NowColorToken.Success,
            NowColorToken.SuccessMuted,
            NowColorToken.Warning,
            NowColorToken.WarningMuted,
            NowColorToken.Danger,
            NowColorToken.DangerMuted,
            NowColorToken.Shadow,
            NowColorToken.Scrim
        })
        {
            Color lightColor = light.GetColor(token);
            Color darkColor = dark.GetColor(token);
            Assert.Greater(ColorDifference(lightColor, darkColor), 0.01f, $"{token} was copied unchanged between Material modes.");
        }

        foreach (NowColorToken token in new[]
        {
            NowColorToken.SurfaceElevated,
            NowColorToken.SurfaceHover,
            NowColorToken.SurfacePressed,
            NowColorToken.AccentMuted,
            NowColorToken.SuccessMuted,
            NowColorToken.WarningMuted,
            NowColorToken.DangerMuted
        })
        {
            Assert.Less(
                RelativeLuminance(dark.GetColor(token)),
                RelativeLuminance(light.GetColor(token)),
                $"{token} must be regenerated as a dark role instead of retaining a light-theme value.");
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/Material.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void MaterialButtonsAndBadgesResolveReadableSemanticForegrounds(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        var previousFont = Now.defaultFont;

        Assert.IsNotNull(theme);
        Assert.IsNotNull(font, "Default font resource missing.");

        var styles = new[]
        {
            NowRectangleStyle.Accent,
            NowRectangleStyle.Muted,
            NowRectangleStyle.Elevated,
            NowRectangleStyle.AccentSoft,
            NowRectangleStyle.Danger
        };
        var expectedForegrounds = new[]
        {
            NowColorToken.AccentText,
            NowColorToken.Text,
            NowColorToken.Accent,
            NowColorToken.Accent,
            NowColorToken.DangerText
        };

        try
        {
            Now.defaultFont = font;

            for (int i = 0; i < styles.Length; ++i)
            {
                NowRectangleStyle style = styles[i];
                Color expected = theme.GetColor(expectedForegrounds[i]);
                Color background = theme.Rectangle(new NowRect(0f, 0f, 160f, 40f), style).color;
                Color buttonForeground = RenderButtonForeground(theme, style);
                Color badgeForeground = RenderBadgeForeground(theme, style);

                AssertColor(expected, buttonForeground, $"{themePath}: {style} button foreground");
                AssertColor(expected, badgeForeground, $"{themePath}: {style} badge foreground");
                Assert.GreaterOrEqual(ContrastRatio(buttonForeground, background), 4.5f, $"{themePath}: {style} button text");
                Assert.GreaterOrEqual(ContrastRatio(badgeForeground, background), 4.5f, $"{themePath}: {style} badge text");
            }
        }
        finally
        {
            Now.defaultFont = previousFont;
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/Material.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void MaterialSurfaceStaysBorderlessAndTextLikeWhileMutedProvidesPanelFill(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        var previousFont = Now.defaultFont;
        var drawList = new NowDrawList();

        try
        {
            Assert.IsNotNull(theme);
            Assert.IsNotNull(font, "Default font resource missing.");

            var rect = new NowRect(8f, 8f, 160f, 40f);
            NowRectangle surface = theme.Rectangle(rect, NowRectangleStyle.Surface);
            NowRectangle muted = theme.Rectangle(rect, NowRectangleStyle.Muted);

            Assert.AreEqual(0f, surface.outline, 0.0001f, $"{themePath}: raw Surface must remain borderless.");
            AssertColor(theme.GetColor(NowColorToken.Surface), surface.color, $"{themePath}: Surface fill");
            AssertColor(theme.GetColor(NowColorToken.SurfaceMuted), muted.color, $"{themePath}: Muted fill");
            Assert.Greater(
                ColorDifference(muted.color, theme.GetColor(NowColorToken.Background)),
                0.04f,
                $"{themePath}: Muted must be visibly distinct from Background for filled review panels.");

            Now.defaultFont = font;
            using (drawList.Begin(new Vector2(192f, 64f)))
            {
                theme.controlRenderer.DrawButton(new NowButtonRenderContext(
                    theme,
                    rect,
                    "Text action",
                    NowRectangleStyle.Surface,
                    NowTextStyle.Button,
                    PassiveInteraction(900, rect),
                    false,
                    0f));
            }

            Color fill = FirstBatchValue(drawList, NowMeshKind.Rectangle, 3);
            Vector4 rectangleParameters = FirstBatchValue(drawList, NowMeshKind.Rectangle, 5);
            Color foreground = FirstBatchValue(drawList, NowMeshKind.Text, 3);

            Assert.AreEqual(0f, fill.a, 0.0001f, $"{themePath}: Surface button should not draw a filled container.");
            Assert.AreEqual(0f, rectangleParameters.y, 0.0001f, $"{themePath}: Surface button should suppress the preset outline.");
            AssertColor(theme.GetColor(NowColorToken.Accent), foreground, $"{themePath}: Surface button foreground");
        }
        finally
        {
            drawList.Dispose();
            Now.defaultFont = previousFont;
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/Material.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void MaterialSwitchKnobsKeepGraphicalContrastAcrossStates(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        Assert.IsNotNull(theme);

        var states = new[] { "idle", "hover", "held" };

        for (int valueIndex = 0; valueIndex < 2; ++valueIndex)
        {
            bool value = valueIndex == 1;
            float onT = value ? 1f : 0f;

            for (int stateIndex = 0; stateIndex < states.Length; ++stateIndex)
            {
                bool hovered = stateIndex > 0;
                bool held = stateIndex == 2;
                float hoverT = hovered ? 1f : 0f;
                var glyphRect = new NowRect(8f, 8f, theme.controlStyles.switchWidth, theme.controlStyles.switchHeight);
                var drawList = new NowDrawList();

                try
                {
                    using (drawList.Begin(new Vector2(96f, 48f)))
                    {
                        theme.controlRenderer.DrawSwitch(new NowSwitchRenderContext(
                            theme,
                            glyphRect,
                            glyphRect,
                            value,
                            onT,
                            PassiveInteraction(1000 + valueIndex * 10 + stateIndex, glyphRect, hovered, held),
                            false,
                            hoverT));
                    }

                    Color track = FirstBatchValue(drawList, NowMeshKind.Rectangle, 3);
                    Color knob = LastBatchValue(drawList, NowMeshKind.Rectangle, 3);
                    float ratio = ContrastRatio(knob, track);
                    Assert.GreaterOrEqual(
                        ratio,
                        3f,
                        $"{themePath}: {(value ? "on" : "off")} {states[stateIndex]} switch knob ({ratio:0.00}:1)");
                }
                finally
                {
                    drawList.Dispose();
                }
            }
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/Material.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void MaterialChipsRenderOutlinedIdleAndReadableSelectedStates(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        var previousFont = Now.defaultFont;
        var rect = new NowRect(8f, 8f, 144f, theme != null ? theme.controlStyles.chipHeight : 32f);

        Assert.IsNotNull(theme);
        Assert.IsNotNull(font, "Default font resource missing.");

        try
        {
            Now.defaultFont = font;

            Color idleFill;
            Color idleOutline;
            Color idleForeground;
            float idleOutlineWidth;

            var idleDrawList = new NowDrawList();
            try
            {
                using (idleDrawList.Begin(new Vector2(176f, 56f)))
                {
                    theme.controlRenderer.DrawChip(new NowChipRenderContext(
                        theme,
                        rect,
                        "Idle chip",
                        false,
                        false,
                        default,
                        false,
                        NowTextStyle.Body,
                        PassiveInteraction(1100, rect),
                        false,
                        0f));
                }

                idleFill = FirstBatchValue(idleDrawList, NowMeshKind.Rectangle, 3);
                idleOutline = FirstBatchValue(idleDrawList, NowMeshKind.Rectangle, 4);
                idleOutlineWidth = FirstBatchValue(idleDrawList, NowMeshKind.Rectangle, 5).y;
                idleForeground = FirstBatchValue(idleDrawList, NowMeshKind.Text, 3);
            }
            finally
            {
                idleDrawList.Dispose();
            }

            Color background = theme.GetColor(NowColorToken.Background);
            bool transparentFill = idleFill.a <= 0.0001f;
            bool surfaceCompatibleFill = ColorDifference(idleFill, theme.GetColor(NowColorToken.Surface)) <= 0.0001f;
            Assert.IsTrue(
                transparentFill || surfaceCompatibleFill,
                $"{themePath}: idle chip fill must be transparent or Surface-compatible.");
            Assert.AreEqual(1f, idleOutlineWidth, 0.0001f, $"{themePath}: idle chip boundary must be 1px.");
            AssertColor(theme.GetColor(NowColorToken.Border), idleOutline, $"{themePath}: idle chip outline");
            Assert.GreaterOrEqual(
                ContrastRatio(CompositeOver(idleOutline, background), background),
                3f,
                $"{themePath}: idle chip outline against Background");
            Assert.GreaterOrEqual(
                ContrastRatio(idleForeground, CompositeOver(idleFill, background)),
                4.5f,
                $"{themePath}: idle chip foreground");

            var selectedDrawList = new NowDrawList();
            try
            {
                using (selectedDrawList.Begin(new Vector2(176f, 56f)))
                {
                    theme.controlRenderer.DrawChip(new NowChipRenderContext(
                        theme,
                        rect,
                        "Selected chip",
                        true,
                        false,
                        default,
                        false,
                        NowTextStyle.Body,
                        PassiveInteraction(1101, rect),
                        false,
                        0f));
                }

                Color selectedFill = FirstBatchValue(selectedDrawList, NowMeshKind.Rectangle, 3);
                Color selectedForeground = FirstBatchValue(selectedDrawList, NowMeshKind.Text, 3);
                AssertColor(theme.GetColor(NowColorToken.AccentMuted), selectedFill, $"{themePath}: selected chip fill");
                Assert.GreaterOrEqual(
                    ContrastRatio(selectedForeground, CompositeOver(selectedFill, background)),
                    4.5f,
                    $"{themePath}: selected chip foreground");
            }
            finally
            {
                selectedDrawList.Dispose();
            }
        }
        finally
        {
            Now.defaultFont = previousFont;
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/DefaultDark.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void SelectedPopupItemKeepsReadableBodyTextColor(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        var previousFont = Now.defaultFont;
        var drawList = new NowDrawList();

        try
        {
            Assert.IsNotNull(theme);
            Assert.IsNotNull(font, "Default font resource missing.");
            Now.defaultFont = font;

            using (drawList.Begin(new Vector2(240f, 80f)))
            {
                theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    theme,
                    new NowRect(8f, 8f, 200f, 40f),
                    "Selected option",
                    true,
                    default));
            }

            Color expected = theme.GetColor(NowColorToken.Text);
            Color actual = FirstBatchValue(drawList, NowMeshKind.Text, 3);
            Color highlight = FirstBatchValue(drawList, NowMeshKind.Rectangle, 3);
            Color composedHighlight = CompositeOver(highlight, theme.GetColor(NowColorToken.Surface));

            AssertColor(expected, actual, $"{themePath}: selected popup body text");
            Assert.GreaterOrEqual(
                ContrastRatio(actual, composedHighlight),
                4.5f,
                $"{themePath}: selected popup text over its composited highlight");
        }
        finally
        {
            drawList.Dispose();
            Now.defaultFont = previousFont;
        }
    }

    [TestCase("Assets/NowUI/Assets/Themes/Material.asset")]
    [TestCase("Assets/NowUI/Assets/Themes/MaterialDark.asset")]
    public void MaterialPopupItemRendersMutedDetailText(string themePath)
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(themePath);
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        var previousFont = Now.defaultFont;
        var drawList = new NowDrawList();

        try
        {
            Assert.IsNotNull(theme);
            Assert.IsNotNull(font, "Default font resource missing.");
            Now.defaultFont = font;

            using (drawList.Begin(new Vector2(240f, 80f)))
            {
                theme.controlRenderer.DrawPopupItem(new NowPopupItemRenderContext(
                    theme,
                    new NowRect(8f, 8f, 200f, 56f),
                    "Option title",
                    "Supporting detail",
                    false,
                    default));
            }

            Assert.IsTrue(
                BatchContainsColor(drawList, NowMeshKind.Text, theme.GetColor(NowColorToken.Text)),
                $"{themePath}: popup title did not use Text.");
            Assert.IsTrue(
                BatchContainsColor(drawList, NowMeshKind.Text, theme.GetColor(NowColorToken.TextMuted)),
                $"{themePath}: popup detail was not rendered with TextMuted.");
        }
        finally
        {
            drawList.Dispose();
            Now.defaultFont = previousFont;
        }
    }

    [Test]
    public void DefaultThemeAssetMatchesCodeDefaultsAndLinksDarkCounterpart()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Default.asset");

        Assert.IsNotNull(theme);
        Assert.IsFalse(theme.isDark);
        Assert.IsNotNull(theme.counterpart);
        Assert.IsTrue(theme.counterpart.isDark);
        Assert.AreSame(theme, theme.counterpart.counterpart);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.AreEqual(0.369f, accent.r, 0.0001f);
        Assert.AreEqual(0.416f, accent.g, 0.0001f);
        Assert.AreEqual(0.824f, accent.b, 0.0001f);
    }

    [Test]
    public void EveryColorTokenResolvesInBothDefaultPalettes()
    {
        foreach (var palette in new[] { NowThemeColorSet.DefaultLight, NowThemeColorSet.DefaultDark })
        {
            for (int i = 0; i < NowThemeColorSet.TokenCount; ++i)
            {
                Assert.IsTrue(palette.TryGet((NowColorToken)i, out var color), ((NowColorToken)i).ToString());
                Assert.Greater(color.a, 0f, ((NowColorToken)i).ToString());
            }
        }
    }

    [Test]
    public void DefaultPalettesMeetContrastMinimums()
    {
        AssertContrast(NowThemeColorSet.DefaultLight, dark: false);
        AssertContrast(NowThemeColorSet.DefaultDark, dark: true);
    }

    static void AssertContrast(NowThemeColorSet palette, bool dark)
    {
        string mode = dark ? "dark" : "light";
        Assert.GreaterOrEqual(ContrastRatio(palette.text, palette.background), 7f, $"{mode}: Text on Background");
        Assert.GreaterOrEqual(ContrastRatio(palette.textMuted, palette.surface), 4.5f, $"{mode}: TextMuted on Surface");
        Assert.GreaterOrEqual(ContrastRatio(palette.accentText, palette.accent), 4.5f, $"{mode}: AccentText on Accent");
        Assert.GreaterOrEqual(ContrastRatio(palette.successText, palette.success), 4.5f, $"{mode}: SuccessText on Success");
        Assert.GreaterOrEqual(ContrastRatio(palette.warningText, palette.warning), 4.5f, $"{mode}: WarningText on Warning");
        Assert.GreaterOrEqual(ContrastRatio(palette.dangerText, palette.danger), 4.5f, $"{mode}: DangerText on Danger");
    }

    static void AssertThemeContrast(NowThemeAsset theme, NowColorToken foreground, NowColorToken background, float minimum)
    {
        float ratio = ContrastRatio(theme.GetColor(foreground), theme.GetColor(background));
        Assert.GreaterOrEqual(ratio, minimum, $"{theme.name}: {foreground} on {background} ({ratio:0.00}:1)");
    }

    static Color RenderButtonForeground(NowThemeAsset theme, NowRectangleStyle style)
    {
        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(192f, 64f)))
            {
                theme.controlRenderer.DrawButton(new NowButtonRenderContext(
                    theme,
                    new NowRect(8f, 8f, 160f, 40f),
                    "Button",
                    style,
                    NowTextStyle.Button,
                    PassiveInteraction(800 + (int)style, new Rect(8f, 8f, 160f, 40f)),
                    false,
                    0f));
            }

            return FirstBatchValue(drawList, NowMeshKind.Text, 3);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    static Color RenderBadgeForeground(NowThemeAsset theme, NowRectangleStyle style)
    {
        var drawList = new NowDrawList();

        try
        {
            using (drawList.Begin(new Vector2(192f, 64f)))
            {
                theme.controlRenderer.DrawBadge(new NowBadgeRenderContext(
                    theme,
                    new NowRect(8f, 8f, 160f, 40f),
                    "Badge",
                    style,
                    NowTextStyle.Button));
            }

            return FirstBatchValue(drawList, NowMeshKind.Text, 3);
        }
        finally
        {
            drawList.Dispose();
        }
    }

    static Vector4 FirstBatchValue(NowDrawList drawList, NowMeshKind kind, int uvChannel)
    {
        var values = new List<Vector4>();
        drawList.mesh.GetUVs(uvChannel, values);

        for (int i = 0; i < drawList.batches.Count; ++i)
        {
            if (drawList.batches[i].kind != kind)
                continue;

            int[] indices = drawList.mesh.GetIndices(i);

            if (indices.Length == 0)
                continue;

            Assert.Less(indices[0], values.Count, $"{kind} batch did not populate UV channel {uvChannel}.");
            return values[indices[0]];
        }

        Assert.Fail($"Draw list did not emit a {kind} batch.");
        return default;
    }

    static Vector4 LastBatchValue(NowDrawList drawList, NowMeshKind kind, int uvChannel)
    {
        var values = new List<Vector4>();
        drawList.mesh.GetUVs(uvChannel, values);

        for (int i = drawList.batches.Count - 1; i >= 0; --i)
        {
            if (drawList.batches[i].kind != kind)
                continue;

            int[] indices = drawList.mesh.GetIndices(i);

            if (indices.Length == 0)
                continue;

            int index = indices[indices.Length - 1];
            Assert.Less(index, values.Count, $"{kind} batch did not populate UV channel {uvChannel}.");
            return values[index];
        }

        Assert.Fail($"Draw list did not emit a {kind} batch.");
        return default;
    }

    static NowInteraction PassiveInteraction(int id, Rect rect, bool hovered = false, bool held = false)
    {
        bool hasPointer = hovered || held;
        return new NowInteraction(
            TestIdentityRoot.Child(id),
            rect,
            NowPointerButton.Primary,
            hasPointer,
            hasPointer ? rect.center : default,
            default,
            default,
            hovered || held,
            false,
            held,
            false,
            false,
            held,
            false,
            false,
            false,
            false,
            false);
    }

    static bool BatchContainsColor(NowDrawList drawList, NowMeshKind kind, Color expected)
    {
        var colors = new List<Vector4>();
        drawList.mesh.GetUVs(3, colors);

        for (int i = 0; i < drawList.batches.Count; ++i)
        {
            if (drawList.batches[i].kind != kind)
                continue;

            foreach (int index in drawList.mesh.GetIndices(i))
            {
                if (index < colors.Count && ColorDifference(colors[index], expected) <= 0.0001f)
                    return true;
            }
        }

        return false;
    }

    static Color CompositeOver(Color foreground, Color background)
    {
        float alpha = foreground.a + background.a * (1f - foreground.a);

        if (alpha <= 0f)
            return Color.clear;

        float backgroundWeight = background.a * (1f - foreground.a);
        return new Color(
            (foreground.r * foreground.a + background.r * backgroundWeight) / alpha,
            (foreground.g * foreground.a + background.g * backgroundWeight) / alpha,
            (foreground.b * foreground.a + background.b * backgroundWeight) / alpha,
            alpha);
    }

    static void AssertColor(Color expected, Color actual, string message)
    {
        Assert.AreEqual(expected.r, actual.r, 0.0001f, message);
        Assert.AreEqual(expected.g, actual.g, 0.0001f, message);
        Assert.AreEqual(expected.b, actual.b, 0.0001f, message);
        Assert.AreEqual(expected.a, actual.a, 0.0001f, message);
    }

    static void AssertValidShadowLayer(NowShadowLayer layer, string message)
    {
        AssertFinite(layer.offsetY, $"{message} offset");
        AssertFinite(layer.blur, $"{message} blur");
        AssertFinite(layer.spread, $"{message} spread");
        AssertFinite(layer.alpha, $"{message} alpha");
        Assert.GreaterOrEqual(layer.blur, 0f, $"{message} blur");
        Assert.Greater(layer.alpha, 0f, $"{message} alpha");
        Assert.LessOrEqual(layer.alpha, 1f, $"{message} alpha");
    }

    static void AssertFiniteColor(Color color, string message)
    {
        AssertFiniteVector(color, message);
        Assert.That(color.r, Is.InRange(0f, 1f), message);
        Assert.That(color.g, Is.InRange(0f, 1f), message);
        Assert.That(color.b, Is.InRange(0f, 1f), message);
        Assert.That(color.a, Is.InRange(0f, 1f), message);
    }

    static void AssertFiniteVector(Vector4 value, string message)
    {
        AssertFinite(value.x, $"{message} x");
        AssertFinite(value.y, $"{message} y");
        AssertFinite(value.z, $"{message} z");
        AssertFinite(value.w, $"{message} w");
    }

    static void AssertNonNegative(Vector4 value, string message)
    {
        Assert.GreaterOrEqual(value.x, 0f, $"{message} x");
        Assert.GreaterOrEqual(value.y, 0f, $"{message} y");
        Assert.GreaterOrEqual(value.z, 0f, $"{message} z");
        Assert.GreaterOrEqual(value.w, 0f, $"{message} w");
    }

    static void AssertFinite(float value, string message)
    {
        Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), message);
    }

    static float MaxComponent(Vector4 value)
    {
        return Mathf.Max(Mathf.Max(value.x, value.y), Mathf.Max(value.z, value.w));
    }

    static float ColorDifference(Color a, Color b)
    {
        return Mathf.Max(
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g)),
            Mathf.Max(Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a)));
    }

    static float ContrastRatio(Color a, Color b)
    {
        float lighter = Mathf.Max(RelativeLuminance(a), RelativeLuminance(b));
        float darker = Mathf.Min(RelativeLuminance(a), RelativeLuminance(b));
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    static float RelativeLuminance(Color color)
    {
        return LinearChannel(color.r) * 0.2126f + LinearChannel(color.g) * 0.7152f + LinearChannel(color.b) * 0.0722f;
    }

    static float LinearChannel(float value)
    {
        return value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    [Test]
    public void LegacyPaletteDerivesExtendedRoles()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            var serialized = new SerializedObject(theme);
            var palette = serialized.FindProperty("_palette");

            foreach (string field in new[]
            {
                "_surfaceElevated", "_surfaceHover", "_surfacePressed", "_accentHover", "_accentPressed",
                "_accentMuted", "_borderStrong", "_focusRing", "_success", "_successText", "_successMuted",
                "_warning", "_warningText", "_warningMuted", "_danger", "_dangerText", "_dangerMuted",
                "_shadow", "_scrim"
            })
            {
                palette.FindPropertyRelative(field).colorValue = default;
            }

            serialized.ApplyModifiedProperties();
            theme.MigrateDerivedRoles();

            for (int i = 0; i < NowThemeColorSet.TokenCount; ++i)
            {
                Assert.IsTrue(theme.TryGetColor((NowColorToken)i, out var color), ((NowColorToken)i).ToString());
                Assert.Greater(color.a, 0f, ((NowColorToken)i).ToString());
            }
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void PreferDarkSwapsToLinkedCounterpart()
    {
        var light = ScriptableObject.CreateInstance<NowThemeAsset>();
        var dark = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            dark.ResetToDefaults(dark: true);
            light.SetCounterpart(dark);
            dark.SetCounterpart(light);

            using (NowTheme.Scope(light))
            {
                Assert.AreSame(light, NowTheme.themeAsset);
                NowTheme.preferDark = true;
                Assert.AreSame(dark, NowTheme.themeAsset);
                NowTheme.preferDark = false;
                Assert.AreSame(light, NowTheme.themeAsset);
                NowTheme.preferDark = null;
                Assert.AreSame(light, NowTheme.themeAsset);
            }

            using (NowTheme.Scope(dark))
            {
                Assert.AreSame(dark, NowTheme.themeAsset, "Unset preferDark must respect an explicitly scoped dark theme.");
                NowTheme.preferDark = false;
                Assert.AreSame(light, NowTheme.themeAsset);
                NowTheme.preferDark = null;
            }
        }
        finally
        {
            NowTheme.Reset();
            Object.DestroyImmediate(light);
            Object.DestroyImmediate(dark);
        }
    }

    [Test]
    public void ThemeGeneratorAppliesDerivedPaletteToTheme()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            var palette = NowThemePaletteGenerator.FromKeyColors(
                new Color(0.32f, 0.20f, 0.48f, 1f),
                new Color(0.88f, 0.38f, 0.12f, 1f),
                true);

            var serializedTheme = new SerializedObject(theme);
            NowThemePaletteGenerator.WriteToSerializedTheme(serializedTheme, palette);
            serializedTheme.ApplyModifiedProperties();

            Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
            Assert.IsTrue(theme.TryGetColor(NowColorToken.Text, out var text));

            NowRectangle accent = theme.Rectangle(new Vector4(0, 0, 10, 10), NowRectangleStyle.Accent);
            NowText button = theme.Text(new Vector4(0, 0, 10, 10), NowTextStyle.Button);

            Assert.AreEqual(palette.background.r, background.r, 0.0001f);
            Assert.AreEqual(palette.background.g, background.g, 0.0001f);
            Assert.AreEqual(palette.background.b, background.b, 0.0001f);
            Assert.AreEqual(palette.text.r, text.r, 0.0001f);
            Assert.AreEqual(palette.accent.r, accent.color.x, 0.0001f);
            Assert.AreEqual(palette.accent.g, accent.color.y, 0.0001f);
            Assert.AreEqual(palette.accentText.b, button.color.z, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ThemeGeneratorRandomPalettesAreDeterministic()
    {
        var first = NowThemePaletteGenerator.RandomPalette(1234, true);
        var second = NowThemePaletteGenerator.RandomPalette(1234, true);

        Assert.AreEqual(first.background.r, second.background.r, 0.0001f);
        Assert.AreEqual(first.surface.g, second.surface.g, 0.0001f);
        Assert.AreEqual(first.accent.b, second.accent.b, 0.0001f);
        Assert.AreEqual(first.accentText.r, second.accentText.r, 0.0001f);
    }

    [Test]
    public void DefaultControlStylesMatchRedesignMetrics()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            var styles = theme.controlStyles;

            Assert.AreEqual(new Vector4(14f, 10f, 14f, 10f), styles.buttonPadding);
            Assert.AreEqual(18f, styles.toggleSize, 0.0001f);
            Assert.AreEqual(8f, styles.toggleGap, 0.0001f);
            Assert.AreEqual(20f, styles.sliderHeight, 0.0001f);
            Assert.AreEqual(18f, styles.sliderKnobSize, 0.0001f);
            Assert.AreEqual(6f, styles.sliderTrackThickness, 0.0001f);
            Assert.AreEqual(36f, styles.dropdownFieldMinHeight, 0.0001f);
            Assert.AreEqual(32f, styles.dropdownItemHeight, 0.0001f);
            Assert.AreEqual(28f, styles.contextMenuItemHeight, 0.0001f);
            Assert.AreEqual(8f, styles.scrollbarWidth, 0.0001f);
            Assert.AreEqual(44f, styles.controlMinTouchTarget, 0.0001f);
            Assert.AreEqual(0.45f, styles.disabledOpacity, 0.0001f);

            var renderer = theme.controlRenderer;
            Assert.AreEqual(new Vector2(28f, 36f), renderer.MeasureButton(theme, string.Empty, NowTextStyle.Button));
            Assert.AreEqual(new Vector2(26f, 18f), renderer.MeasureToggle(theme, string.Empty, NowTextStyle.Body));
            Assert.AreEqual(new Vector2(160f, 20f), renderer.MeasureSlider(theme));
            Assert.AreEqual(new Vector2(200f, 36f), renderer.MeasureTextField(theme, 20f));
        }
        finally
        {
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ThemeGeneratorSettingsAreSerializedAndCopyable()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        NowThemeAsset copy = null;

        try
        {
            var serializedTheme = new SerializedObject(theme);
            var dark = serializedTheme.FindProperty("_generatorDark");
            var key = serializedTheme.FindProperty("_generatorKeyColor");
            var accent = serializedTheme.FindProperty("_generatorAccentColor");
            var seed = serializedTheme.FindProperty("_generatorSeed");

            Assert.IsNotNull(dark);
            Assert.IsNotNull(key);
            Assert.IsNotNull(accent);
            Assert.IsNotNull(seed);

            dark.boolValue = true;
            key.colorValue = new Color(0.25f, 0.35f, 0.45f, 1f);
            accent.colorValue = new Color(0.85f, 0.30f, 0.20f, 1f);
            seed.intValue = 9876;
            serializedTheme.ApplyModifiedProperties();

            copy = Object.Instantiate(theme);
            var serializedCopy = new SerializedObject(copy);

            Assert.IsTrue(serializedCopy.FindProperty("_generatorDark").boolValue);
            Assert.AreEqual(0.25f, serializedCopy.FindProperty("_generatorKeyColor").colorValue.r, 0.0001f);
            Assert.AreEqual(0.30f, serializedCopy.FindProperty("_generatorAccentColor").colorValue.g, 0.0001f);
            Assert.AreEqual(9876, serializedCopy.FindProperty("_generatorSeed").intValue);
        }
        finally
        {
            if (copy != null)
                Object.DestroyImmediate(copy);

            Object.DestroyImmediate(theme);
        }
    }
}
