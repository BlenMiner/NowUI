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

public sealed class SdfGradientParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();
        NowSdf.Scene(new NowRect(0, 0, 128, 96), "native-sdf-gradient")
            // Left to right across the box: red -> blue.
            .SetGradient(Color.red, Color.blue).SetGradientLinear(90f)
            .Box(new NowRect(4, 4, 56, 24))
            // Green at the circle's centre, blue at its rim.
            .SetGradient(Color.green, Color.blue).SetGradientRadial(NowGradientShape.Circle)
            .Circle(new Vector2(96, 16), 14)
            // A full ring swept clockwise from 12 o'clock: red just after it, green just before.
            .SetGradient(Color.red, Color.green).SetGradientConic()
            .Arc(new Vector2(32, 64), 20, 6, 0f, Mathf.PI * 2f)
            // The same left-to-right ramp on a box turned a quarter: it turns with the box.
            .SetGradient(Color.red, Color.blue).SetGradientLinear(90f)
            .RotateNext(90f)
            .Box(new NowRect(80, 44, 40, 40))
            // UseColor returns to solid fills.
            .SetColor(Color.white).UseColor()
            .Circle(new Vector2(70, 70), 5)
            .Draw();
    }
}

public sealed class SdfShapeFeaturesParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();

        // Round, butt and square caps on the same quarter arc from 12 o'clock.
        var arcs = NowSdf.Scene(new NowRect(0, 0, 128, 48), "native-sdf-caps").SetColor(Color.white);
        arcs.SetArcCap(NowLineCap.Round).Arc(new Vector2(20, 24), 12, 4, NowSweep.Clock(0f, 90f));
        arcs.SetArcCap(NowLineCap.Butt).Arc(new Vector2(60, 24), 12, 4, NowSweep.Clock(0f, 90f));
        arcs.SetArcCap(NowLineCap.Square).Arc(new Vector2(100, 24), 12, 4, NowSweep.Clock(0f, 90f));
        arcs.Draw();

        // A bar pointing right from the local origin, turned a quarter and doubled.
        NowSdf.Scene(new NowRect(0, 48, 64, 48), "native-sdf-transform").SetColor(Color.white)
            .PushTransform(new Vector2(16, 8), 2f, 90f)
            .Box(new NowRect(0f, -2f, 10f, 4f))
            .PopTransform()
            .Draw();

        // Shapes placed in the same UI coordinates as the scene rect.
        NowSdf.Scene(new NowRect(64, 48, 64, 48), "native-sdf-ui-space").SetColor(Color.white)
            .UseUiCoordinates()
            .Circle(new Vector2(76, 60), 6)
            .Draw();

        // Two hard shadows: red to the right, blue beneath.
        NowSdf.Scene(new NowRect(64, 48, 64, 48), "native-sdf-two-shadows").SetColor(Color.white)
            .SetShadow(new Vector2(6, 0), 0.01f, Color.red)
            .AddShadow(new Vector2(0, 6), 0.01f, Color.blue)
            .Box(new NowRect(40, 8, 16, 16))
            .Draw();

        // One build draws the circle and clips the green fill to it.
        using (NowSdf.Scene(new NowRect(128, 0, 32, 48), "native-sdf-draw-mask").SetColor(Color.white)
                   .Circle(new Vector2(16, 16), 10).DrawAndBeginMask())
            Now.Rectangle(new NowRect(128, 0, 32, 48)).SetColor(Color.green).Draw();
    }
}

public sealed class SdfNestedGraphParityScene : INowScene
{
    public void Draw(NowRect view)
    {
        Now.Rectangle(view).SetColor(Color.black).Draw();

        // Two smooth-unioned dots, authored around their own origin.
        var dots = NowSdf.Graph().SetColor(Color.white)
            .Circle(new Vector2(-6f, 0f), 5f).SmoothUnion(3f).Circle(new Vector2(6f, 0f), 5f);

        // A card with the dots cut out, placed and scaled by a transform.
        var card = NowSdf.Graph().SetColor(Color.white)
            .RoundedBox(new NowRect(4f, 4f, 56f, 56f), 8f)
            .Subtract().PushTransform(new Vector2(32f, 32f), 1.5f).Graph(dots).PopTransform();

        // A graph that opens with a morph: a red circle halfway to a blue box.
        var circle = NowSdf.Graph().SetColor(Color.red).Circle(new Vector2(96f, 32f), 12f);
        var box = NowSdf.Graph().SetColor(Color.blue).Box(new NowRect(84f, 20f, 24f, 24f));
        var morph = NowSdf.Graph().Morph(circle, box, 0.5f);

        NowSdf.Scene(new NowRect(0, 0, 128, 64), "native-sdf-nested").Graph(card).Graph(morph).Draw();
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
