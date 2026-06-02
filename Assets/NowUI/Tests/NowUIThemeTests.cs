using NUnit.Framework;
using UnityEngine;

public class NowUIThemeTests
{
    [Test]
    public void ThemeDefaultsResolvePaletteTokensCaseInsensitively()
    {
        var theme = ScriptableObject.CreateInstance<NowUITheme>();

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
        var theme = ScriptableObject.CreateInstance<NowUITheme>();

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
        var theme = ScriptableObject.CreateInstance<NowUITheme>();

        try
        {
            NowUIRectangle rectangle = theme.Rectangle(new Vector4(4, 8, 100, 40), "accent");

            Assert.AreEqual(new Vector4(4, 8, 100, 40), rectangle.rect);
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
        var theme = ScriptableObject.CreateInstance<NowUITheme>();
        var font = ScriptableObject.CreateInstance<NowFont>();

        try
        {
            NowUIText text = theme.Text(new Vector4(0, 0, 100, 24), font, "button");

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
}
