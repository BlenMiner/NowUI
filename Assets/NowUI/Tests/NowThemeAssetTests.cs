using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Editor;

public class NowThemeAssetTests
{
    [Test]
    public void ThemeDefaultsResolvePaletteTokens()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
            Assert.AreEqual(0.145f, accent.r, 0.0001f);
            Assert.AreEqual(0.388f, accent.g, 0.0001f);
            Assert.AreEqual(0.922f, accent.b, 0.0001f);
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
            Assert.AreEqual(0.145f, rectangle.color.x, 0.0001f);
            Assert.AreEqual(0.388f, rectangle.color.y, 0.0001f);
            Assert.AreEqual(0.922f, rectangle.color.z, 0.0001f);
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
        Assert.Less(accentText.r, 0.2f);
        Assert.Less(accentText.g, 0.2f);
        Assert.Less(accentText.b, 0.2f);
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
    public void DefaultThemeAssetMatchesCodeDefaultsAndLinksDarkCounterpart()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Default.asset");

        Assert.IsNotNull(theme);
        Assert.IsFalse(theme.isDark);
        Assert.IsNotNull(theme.counterpart);
        Assert.IsTrue(theme.counterpart.isDark);
        Assert.AreSame(theme, theme.counterpart.counterpart);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.AreEqual(0.145f, accent.r, 0.0001f);
        Assert.AreEqual(0.388f, accent.g, 0.0001f);
        Assert.AreEqual(0.922f, accent.b, 0.0001f);
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
