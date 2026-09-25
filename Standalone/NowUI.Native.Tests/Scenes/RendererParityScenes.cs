using System;
using NowUI.Hosting;
using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Native.Tests.Scenes;

public sealed class ColorSpaceScene : INowScene, IDisposable
{
    readonly Texture2D color = new(1, 1, TextureFormat.RGBA32, false, false);
    readonly Texture2D data = new(1, 1, TextureFormat.RGBA32, false, true);

    public ColorSpaceScene()
    {
        color.SetPixels32(new[] { new Color32(128, 128, 128, 255) }); color.Apply();
        data.SetPixels32(new[] { new Color32(128, 128, 128, 255) }); data.Apply();
    }

    public void Draw(NowRect view)
    {
        Now.Rectangle(new NowRect(0, 0, 128, 64)).SetColor(Color.black).Draw();
        Now.Rectangle(new NowRect(0, 0, 32, 32)).SetColor(new Color(.5f, .5f, .5f, 1)).Draw();
        Now.Rectangle(new NowRect(32, 0, 32, 32)).SetTexture(color).SetColor(Color.white).Draw();
        Now.Rectangle(new NowRect(64, 0, 32, 32)).SetTexture(data).SetColor(Color.white).Draw();
        Now.Rectangle(new NowRect(96, 0, 32, 32)).SetColor(new Color(1, 1, 1, .5f)).Draw();
        Now.Text(new NowRect(0, 32, 128, 32)).SetFontSize(22).SetColor(new Color(.5f, .5f, .5f, 1)).Draw("Color");
        Now.Rectangle(new NowRect(0, 64, 32, 32)).SetColor(new Color(.5f, .25f, .75f, .5f)).Draw();
    }

    public void Dispose() { UnityEngine.Object.Destroy(color); UnityEngine.Object.Destroy(data); }
}

public sealed class GlassParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        for (int x = 0; x < 128; x += 8)
            Now.Rectangle(new NowRect(x, 0, 8, 96)).SetColor(x % 16 == 0 ? Color.white : Color.black).Draw();
        Now.Glass(new NowRect(24, 16, 80, 64)).SetBlurRadius(12).SetBlurQuality(NowGlassBlurQuality.High)
            .SetTint(Color.clear).SetVibrancy(1, 1).SetRadius(0).Draw();
        // Content after the glass must remain sharp.
        Now.Rectangle(new NowRect(56, 40, 16, 16)).SetColor(Color.red).Draw();
    }
}

public sealed class SdfParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        NowSdf.Scene(new NowRect(0, 0, 128, 96), "native-parity")
            .SetOutline(4, Color.green).SetShadow(new Vector2(8, 4), 2, Color.blue, 2)
            .SetColor(Color.red).Circle(new Vector2(40, 48), 20)
            .Subtract().Circle(new Vector2(40, 48), 8).Draw();
        using (NowSdf.Scene(new NowRect(80, 16, 40, 64), "native-mask").Circle(new Vector2(20, 32), 16).BeginMask())
            Now.Rectangle(new NowRect(80, 16, 40, 64)).SetColor(Color.cyan).Draw();
    }
}

// A warped SDF fill (left) and a warped SDF mask (right). Masks exercise
// BeginMask coverage caching: a stale cache would keep an earlier warp phase.
static class SdfWarpParity
{
    internal static readonly float Speed = 1.3f, Seed = 2f, FixedTime = 0.5f;

    internal static void Draw(NowRect view, float speed, float seed, float? time)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        var fill = NowSdf.Scene(new NowRect(0, 0, 64, 64), "native-warp").SetColor(Color.white).SetWarp(6f, 14f, speed, seed);
        if (time.HasValue) fill = fill.SetTime(time.Value);
        fill.Circle(new Vector2(32, 32), 22).Draw();
        var mask = NowSdf.Scene(new NowRect(64, 0, 64, 64), "native-warp-mask").SetWarp(6f, 14f, speed, seed);
        if (time.HasValue) mask = mask.SetTime(time.Value);
        using (mask.Circle(new Vector2(32, 32), 22).BeginMask())
            Now.Rectangle(new NowRect(64, 0, 64, 64)).SetColor(Color.yellow).Draw();
    }
}

public sealed class SdfWarpCallerClockScene : INowScene
{
    public void Draw(NowRect view) => SdfWarpParity.Draw(view, SdfWarpParity.Speed, SdfWarpParity.Seed, Time.time);
}

public sealed class SdfWarpHostClockScene : INowScene
{
    public void Draw(NowRect view) => SdfWarpParity.Draw(view, SdfWarpParity.Speed, SdfWarpParity.Seed, null);
}

public sealed class SdfWarpFixedTimeScene : INowScene
{
    public void Draw(NowRect view) => SdfWarpParity.Draw(view, SdfWarpParity.Speed, SdfWarpParity.Seed, SdfWarpParity.FixedTime);
}

public sealed class SdfWarpSeedPhaseScene : INowScene
{
    public void Draw(NowRect view) => SdfWarpParity.Draw(view, 0f,
        SdfWarpParity.Seed + SdfWarpParity.FixedTime * SdfWarpParity.Speed, null);
}

public sealed class SdfImageParityScene : INowScene, IDisposable
{
    readonly Texture2D image = new(8, 8, TextureFormat.RGBA32, false, false);
    public SdfImageParityScene()
    {
        var pixels = new Color32[64];
        for (int y = 2; y < 6; y++) for (int x = 2; x < 6; x++) pixels[y * 8 + x] = new Color32(255, 0, 0, 255);
        image.SetPixels32(pixels); image.Apply();
    }
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        NowSdf.Scene(new NowRect(0, 0, 96, 96), "native-image").SetColor(Color.white)
            .SetOutline(4, Color.green).Image(new NowRect(16, 16, 64, 64), image).Draw();
    }
    public void Dispose() { NowSdf.Reset(); UnityEngine.Object.Destroy(image); }
}

public sealed class DataTextureParityScene : INowScene, IDisposable
{
    readonly Texture2D[] textures = new Texture2D[7];
    readonly RenderTexture[] targets = new RenderTexture[7];

    public DataTextureParityScene()
    {
        TextureFormat[] formats = { TextureFormat.R8, TextureFormat.RHalf, TextureFormat.RFloat,
            TextureFormat.RGHalf, TextureFormat.RGFloat, TextureFormat.RGBAHalf, TextureFormat.RGBAFloat };
        RenderTextureFormat[] renderFormats = { RenderTextureFormat.R8, RenderTextureFormat.RHalf, RenderTextureFormat.RFloat,
            RenderTextureFormat.RGHalf, RenderTextureFormat.RGFloat, RenderTextureFormat.ARGBHalf, RenderTextureFormat.ARGBFloat };
        var previous = RenderTexture.active;
        try
        {
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = new Texture2D(1, 1, formats[i], false, true);
                textures[i].SetPixel(0, 0, new Color(.5f, .25f, .75f, 1)); textures[i].Apply();
                targets[i] = new RenderTexture(1, 1, 0, renderFormats[i], RenderTextureReadWrite.Linear);
                targets[i].Create(); Graphics.Blit(textures[i], targets[i]);
            }
        }
        finally { RenderTexture.active = previous; }
    }

    public void Draw(NowRect view)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            Now.Rectangle(new NowRect(i * 32, 0, 32, 32)).SetTexture(textures[i]).SetColor(Color.white).Draw();
            Now.Rectangle(new NowRect(i * 32, 32, 32, 32)).SetTexture(targets[i]).SetColor(Color.white).Draw();
        }
    }

    public void Dispose()
    {
        foreach (var texture in textures) if (texture != null) UnityEngine.Object.Destroy(texture);
        foreach (var target in targets) if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
    }
}

public sealed class GradientParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        // Left to right: red -> blue.
        Now.Gradient(new NowRect(0, 0, 64, 32), Color.red, Color.blue).SetLinear(90f).Draw();
        // A 16 px circle of green fading to blue, clamped to blue outside it.
        Now.Gradient(new NowRect(64, 0, 64, 32), Color.green, Color.blue).SetRadial(new Vector2(0.5f, 0.5f), 0.5f).Draw();
        // Clockwise from straight up: red just right of 12 o'clock, green just left of it.
        Now.Gradient(new NowRect(0, 32, 64, 64), Color.red, Color.green).SetConic(new Vector2(0.5f, 0.5f), 0f).Draw();
        Now.Text(new NowRect(64, 48, 64, 48)).SetFontSize(30f).SetBold()
            .SetGradient(Color.red, Color.blue).SetGradientLinear(90f).Draw("WW");
    }
}

public sealed class TransformScopesParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        // Half opacity over black.
        using (Now.Opacity(0.5f))
            Now.Rectangle(new NowRect(0, 0, 32, 32)).SetColor(Color.white).Draw();
        // A horizontal bar turned a quarter turn around its center becomes vertical.
        using (Now.Rotate(90f, new Vector2(68, 16)))
            Now.Rectangle(new NowRect(48, 12, 40, 8)).SetColor(Color.red).Draw();
        // Fully faded glass leaves the stripes beneath it untouched.
        for (int x = 96; x < 128; x += 8)
            Now.Rectangle(new NowRect(x, 0, 8, 32)).SetColor(x % 16 == 0 ? Color.white : Color.black).Draw();
        using (Now.Opacity(0f))
            Now.Glass(new NowRect(96, 0, 32, 32)).SetBlurRadius(10).SetTint(new Color(0, 0, 1, 0.8f)).Draw();
        // Coincident SDF shapes resolve in draw order instead of per-pixel noise.
        NowSdf.Scene(new NowRect(0, 40, 128, 56), "parity-coincident")
            .SetColor(Color.red).Arc(new Vector2(28, 28), 20, 5, 0f, Mathf.PI * 2f)
            .SetColor(Color.green).Arc(new Vector2(28, 28), 20, 5, 0f, Mathf.PI * 2f)
            .SetColor(Color.red).Circle(new Vector2(92, 28), 18)
            .SetColor(Color.green).Circle(new Vector2(92, 28), 18)
            .Draw();
    }
}

public sealed class AlignedTextParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        Now.Text(new NowRect(0, 0, 128, 48)).SetFontSize(28).SetBold().SetColor(Color.white)
            .SetAlign(NowTextAlign.Center, NowTextVerticalAlign.CapMiddle).Draw("HI");
        NowSdf.Scene(new NowRect(0, 48, 128, 48), "parity-aligned-text")
            .SetColor(Color.white)
            .Text(new NowRect(0, 0, 128, 48), "HI", 28f, NowFontStyle.Bold, NowTextAlign.Center, NowTextVerticalAlign.CapMiddle)
            .Draw();
    }
}
