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
            Assert.AreEqual(0.102f, accent.r, 0.0001f);
            Assert.AreEqual(0.451f, accent.g, 0.0001f);
            Assert.AreEqual(0.910f, accent.b, 0.0001f);
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
            Assert.AreEqual(0.102f, rectangle.color.x, 0.0001f);
            Assert.AreEqual(0.451f, rectangle.color.y, 0.0001f);
            Assert.AreEqual(0.910f, rectangle.color.z, 0.0001f);
            Assert.AreEqual(new Vector4(8, 8, 8, 8), rectangle.radius);
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
            Assert.AreEqual(14, text.fontSize, 0.0001f);
            Assert.AreEqual(Color.white, (Color)text.color);
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
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/Dark.asset");

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
    public void HeroUIThemeAssetUsesHeroUIRendererAndDefaults()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/HeroUI.asset");

        Assert.IsNotNull(theme);
        Assert.IsInstanceOf<NowHeroUIControlRenderer>(theme.controlRenderer);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Text, out var text));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.AccentText, out var accentText));
        Assert.AreEqual(0.960849f, background.r, 0.0001f);
        Assert.AreEqual(0.094084f, text.r, 0.0001f);
        Assert.AreEqual(0f, accent.r, 0.0001f);
        Assert.AreEqual(0.43529412f, accent.g, 0.0001f);
        Assert.AreEqual(0.93333334f, accent.b, 0.0001f);
        Assert.Greater(accentText.r, 0.98f);
        Assert.AreEqual(40f, theme.controlStyles.buttonMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.textFieldMinHeight, 0.0001f);
        Assert.AreEqual(36f, theme.controlStyles.dropdownFieldMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.dropdownItemHeight, 0.0001f);
        Assert.AreEqual(18f, theme.controlStyles.sliderKnobSize, 0.0001f);
        Assert.AreEqual(36f, theme.controlStyles.toggleStateLayerSize, 0.0001f);
        Assert.AreEqual(36f, theme.controlStyles.sliderStateLayerSize, 0.0001f);
        Assert.AreEqual(new Vector4(12f, 12f, 12f, 12f), theme.controlStyles.buttonRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(12f, 12f, 12f, 12f), theme.controlStyles.fieldRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(12f, 12f, 12f, 12f), theme.controlStyles.popupRadius.Resolve(theme));
        Assert.AreEqual(40f, theme.controlRenderer.MeasureButton(theme, string.Empty, NowTextStyle.Button).y, 0.0001f);
        Assert.AreEqual(40f, theme.controlRenderer.MeasureTextField(theme, 20f).y, 0.0001f);
        Assert.AreEqual(36f, theme.controlRenderer.MeasureDropdownField(theme, 20f).y, 0.0001f);
    }

    [Test]
    public void HeroUIDarkThemeAssetUsesHeroUIRendererAndDarkRoles()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/HeroUIDark.asset");

        Assert.IsNotNull(theme);
        Assert.IsInstanceOf<NowHeroUIControlRenderer>(theme.controlRenderer);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Text, out var text));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.AccentText, out var accentText));
        Assert.Less(background.r, 0.05f);
        Assert.Less(background.g, 0.05f);
        Assert.Less(background.b, 0.05f);
        Assert.Greater(text.r, 0.98f);
        Assert.AreEqual(0.2f, accent.r, 0.0001f);
        Assert.AreEqual(0.5568628f, accent.g, 0.0001f);
        Assert.AreEqual(0.96862745f, accent.b, 0.0001f);
        Assert.Less(accentText.r, 0.1f);
        Assert.AreEqual(40f, theme.controlStyles.buttonMinHeight, 0.0001f);
        Assert.AreEqual(40f, theme.controlStyles.textFieldMinHeight, 0.0001f);
        Assert.AreEqual(36f, theme.controlStyles.dropdownFieldMinHeight, 0.0001f);
        Assert.AreEqual(new Vector4(12f, 12f, 12f, 12f), theme.controlStyles.buttonRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(12f, 12f, 12f, 12f), theme.controlStyles.fieldRadius.Resolve(theme));
        Assert.AreEqual(36f, theme.controlRenderer.MeasureDropdownField(theme, 20f).y, 0.0001f);
    }

    [Test]
    public void UnityEditorThemeAssetUsesEditorRendererAndDefaults()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/UnityEditor.asset");

        Assert.IsNotNull(theme);
        Assert.IsInstanceOf<NowUnityEditorControlRenderer>(theme.controlRenderer);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.SurfaceMuted, out var button));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Border, out var border));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.AreEqual(0.78431374f, background.r, 0.0001f);
        Assert.AreEqual(0.89411765f, button.r, 0.0001f);
        Assert.AreEqual(0.6f, border.r, 0.0001f);
        Assert.AreEqual(0.22745098f, accent.r, 0.0001f);
        Assert.AreEqual(0.44705883f, accent.g, 0.0001f);
        Assert.AreEqual(0.6901961f, accent.b, 0.0001f);
        Assert.AreEqual(22f, theme.controlStyles.buttonMinHeight, 0.0001f);
        Assert.AreEqual(22f, theme.controlStyles.textFieldMinHeight, 0.0001f);
        Assert.AreEqual(22f, theme.controlStyles.dropdownFieldMinHeight, 0.0001f);
        Assert.AreEqual(20f, theme.controlStyles.dropdownItemHeight, 0.0001f);
        Assert.AreEqual(12f, theme.controlStyles.sliderKnobSize, 0.0001f);
        Assert.AreEqual(14f, theme.controlStyles.toggleSize, 0.0001f);
        Assert.AreEqual(13f, theme.controlStyles.scrollbarWidth, 0.0001f);
        Assert.AreEqual(Vector4.zero, theme.controlStyles.buttonRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(2f, 2f, 2f, 2f), theme.controlStyles.fieldRadius.Resolve(theme));
        Assert.AreEqual(22f, theme.controlRenderer.MeasureButton(theme, string.Empty, NowTextStyle.Button).y, 0.0001f);
        Assert.AreEqual(22f, theme.controlRenderer.MeasureTextField(theme, 16f).y, 0.0001f);
        Assert.AreEqual(22f, theme.controlRenderer.MeasureDropdownField(theme, 16f).y, 0.0001f);
    }

    [Test]
    public void UnityEditorDarkThemeAssetUsesEditorRendererAndDarkRoles()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/UnityEditorDark.asset");

        Assert.IsNotNull(theme);
        Assert.IsInstanceOf<NowUnityEditorControlRenderer>(theme.controlRenderer);
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Background, out var background));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.SurfaceMuted, out var button));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Text, out var text));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Border, out var border));
        Assert.IsTrue(theme.TryGetColor(NowColorToken.Accent, out var accent));
        Assert.AreEqual(0.21960784f, background.r, 0.0001f);
        Assert.AreEqual(0.34509805f, button.r, 0.0001f);
        Assert.Greater(text.r, 0.9f);
        Assert.AreEqual(0.13725491f, border.r, 0.0001f);
        Assert.AreEqual(0.17254902f, accent.r, 0.0001f);
        Assert.AreEqual(0.3647059f, accent.g, 0.0001f);
        Assert.AreEqual(0.5294118f, accent.b, 0.0001f);
        Assert.AreEqual(22f, theme.controlStyles.buttonMinHeight, 0.0001f);
        Assert.AreEqual(22f, theme.controlStyles.textFieldMinHeight, 0.0001f);
        Assert.AreEqual(22f, theme.controlStyles.dropdownFieldMinHeight, 0.0001f);
        Assert.AreEqual(Vector4.zero, theme.controlStyles.buttonRadius.Resolve(theme));
        Assert.AreEqual(new Vector4(2f, 2f, 2f, 2f), theme.controlStyles.fieldRadius.Resolve(theme));
        Assert.AreEqual(22f, theme.controlRenderer.MeasureDropdownField(theme, 16f).y, 0.0001f);
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
    public void DefaultControlStylesPreserveLegacyMetrics()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            var styles = theme.controlStyles;

            Assert.AreEqual(new Vector4(12f, 12f, 12f, 12f), styles.buttonPadding);
            Assert.AreEqual(18f, styles.toggleSize, 0.0001f);
            Assert.AreEqual(8f, styles.toggleGap, 0.0001f);
            Assert.AreEqual(20f, styles.sliderHeight, 0.0001f);
            Assert.AreEqual(16f, styles.sliderKnobSize, 0.0001f);
            Assert.AreEqual(6f, styles.sliderTrackThickness, 0.0001f);
            Assert.AreEqual(0f, styles.dropdownFieldMinHeight, 0.0001f);
            Assert.AreEqual(30f, styles.dropdownItemHeight, 0.0001f);
            Assert.AreEqual(26f, styles.contextMenuItemHeight, 0.0001f);
            Assert.AreEqual(8f, styles.scrollbarWidth, 0.0001f);

            var renderer = theme.controlRenderer;
            Assert.AreEqual(new Vector2(24f, 12f), renderer.MeasureButton(theme, string.Empty, NowTextStyle.Button));
            Assert.AreEqual(new Vector2(26f, 18f), renderer.MeasureToggle(theme, string.Empty, NowTextStyle.Body));
            Assert.AreEqual(new Vector2(160f, 20f), renderer.MeasureSlider(theme));
            Assert.AreEqual(new Vector2(200f, 32f), renderer.MeasureTextField(theme, 20f));
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
