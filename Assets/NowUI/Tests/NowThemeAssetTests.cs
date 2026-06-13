using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Editor;

public class NowThemeAssetTests
{
    [Test]
    public void ThemeDefaultsResolvePaletteTokensCaseInsensitively()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            Assert.IsTrue(theme.TryGetColor("ACCENT", out var accent));
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
    public void ThemeInsetsRectUsingNamedSpacing()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();

        try
        {
            Vector4 rect = theme.Inset(new Vector4(0, 0, 100, 60), "md");

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
            NowRectangle rectangle = theme.Rectangle(new Vector4(4, 8, 100, 40), "accent");

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
            NowText text = theme.Text(new Vector4(0, 0, 100, 24), font, "button");

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
        Assert.IsTrue(theme.TryGetColor("background", out var background));
        Assert.IsTrue(theme.TryGetColor("text", out var text));
        Assert.IsTrue(theme.TryGetColor("accent", out var accent));
        Assert.IsTrue(theme.TryGetColor("accent-text", out var accentText));

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

            Assert.IsTrue(theme.TryGetColor("background", out var background));
            Assert.IsTrue(theme.TryGetColor("text", out var text));

            NowRectangle accent = theme.Rectangle(new Vector4(0, 0, 10, 10), "accent");
            NowText button = theme.Text(new Vector4(0, 0, 10, 10), "button");

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
