using System.Diagnostics;
using NowUI.Cli;
using NowUI.Hosting;
using NUnit.Framework;

namespace NowUI.Native.Tests;

public class NativeCaptureTests
{
    private string scratch = null!;
    private string standalone = null!;
    private string configuration = null!;

    [SetUp]
    public void SetUp()
    {
        standalone = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
        configuration = new DirectoryInfo(TestContext.CurrentContext.TestDirectory).Parent!.Name;
        scratch = Path.Combine(Path.GetTempPath(), "nowui-native-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
    }

    [TearDown]
    public void TearDown() { Directory.Delete(scratch, recursive: true); }

    [TestCase("--width", "0")]
    [TestCase("--height", "8193")]
    [TestCase("--time", "NaN")]
    [TestCase("--time", "Infinity")]
    [TestCase("--time", "-1")]
    [TestCase("--time", "60.1")]
    public void RejectsInvalidCaptureBounds(string option, string value)
    {
        Assert.Throws<ArgumentException>(() => RenderOptions.Parse(
            ["render", "Scene.csproj", "--output", "preview.png", option, value]));
    }

    [Test]
    public void RejectsExcessivePixelArea()
    {
        Assert.Throws<ArgumentException>(() => RenderOptions.Parse(
            ["render", "Scene.csproj", "--output", "preview.png", "--width", "8192", "--height", "8192"]));
    }

    [Test]
    public void CaptureAndPreviewDefaultToSixteenByNine()
    {
        var render = RenderOptions.Parse(["render", "Scene.csproj", "--output", "frame.png"]);
        var animate = RenderOptions.Parse(["animate", "Scene.csproj", "--output", Path.Combine(scratch, "frames")]);
        var preview = RenderOptions.Parse(["preview", "Scene.csproj"]);
        foreach (var options in new[] { render, animate, preview })
            Assert.That((options.Width, options.Height), Is.EqualTo((960, 540)));
        var custom = RenderOptions.Parse(["render", "Scene.csproj", "--output", "frame.png", "--width", "640", "--height", "480"]);
        Assert.That((custom.Width, custom.Height), Is.EqualTo((640, 480)));
    }

    [Test]
    public void PreviewAcceptsLiveWindowWithoutCaptureOutput()
    {
        var options = RenderOptions.Parse(["preview", "Scene.csproj", "--scene", "Demo"]);
        Assert.That(options.Preview, Is.True);
        Assert.That(options.Output, Is.Empty);
        Assert.That(options.Frames, Is.Null);
        Assert.Throws<ArgumentException>(() => RenderOptions.Parse(["preview", "Scene.csproj", "--time", "1"]));
        Assert.Throws<ArgumentException>(() => RenderOptions.Parse(["render", "Scene.csproj", "--output", "frame.png", "--frames", "2"]));
        Assert.Throws<ArgumentException>(() => RenderOptions.Parse(["preview", "Scene.csproj", "--frames", "0"]));
    }

    [Test]
    public void ResolvesSceneWithSharedRuntimeIdentity()
    {
        string path = Path.Combine(standalone, "Samples/NativePreview/bin", configuration, "net9.0/NativePreview.dll");
        var loader = new SceneAssembly(path);
        var scene = loader.FindScene(path, "PreviewScene");
        Assert.That(typeof(INowScene).IsAssignableFrom(scene), Is.True);
        Assert.Throws<InvalidOperationException>(() => loader.FindScene(path, null));
        Assert.Throws<InvalidOperationException>(() => loader.FindScene(path, "NoSuchScene"));
    }

    [Test]
    public void PngRoundTripPreservesBottomUpPixelsAndAlpha()
    {
        byte[] rgba = [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 128, 255, 255, 255, 0];
        byte[] png = NowPng.EncodeRgba(2, 2, rgba);
        Assert.That(new NowPngImageDecoder().TryDecode(png, out int width, out int height, out byte[] actual, out string error), Is.True, error);
        Assert.That((width, height), Is.EqualTo((2, 2)));
        Assert.That(actual, Is.EqualTo(rgba));
    }

    [Test, Category("NativeGraphics")]
    public void RendersActualCSharpWithCorrectOrientationBlendingMasksAndText()
    {
        RequireGraphics();
        string output = Path.Combine(scratch, "probe.png");
        var result = Run("Samples/NativePreview/NativePreview.csproj", "PixelProbeScene", output, 320, 240);
        Assert.That(result.Code, Is.Zero, result.Log);
        var pixels = Decode(output, 320, 240);
        Pixel(pixels, 320, 240, 112, 84, 255, 0, 0, 255);
        Pixel(pixels, 320, 240, 208, 84, 0, 255, 0, 255);
        Pixel(pixels, 320, 240, 112, 216, 0, 0, 255, 255);
        Pixel(pixels, 320, 240, 208, 216, 255, 255, 0, 255);
        Pixel(pixels, 320, 240, 48, 36, 255, 128, 128, 255);
        Pixel(pixels, 320, 240, 240, 180, 255, 255, 255, 255);
        Pixel(pixels, 320, 240, 208, 180, 255, 255, 0, 255);
        Pixel(pixels, 320, 240, 2, 2, 255, 255, 255, 255);
        Pixel(pixels, 320, 240, 317, 2, 0, 0, 0, 255);
        Pixel(pixels, 320, 240, 2, 237, 255, 0, 255, 255);
        Pixel(pixels, 320, 240, 317, 237, 0, 255, 255, 255);
        // A blank atlas/incorrect text shader still leaves all quadrant checks green.
        int white = 0;
        for (int y = 132; y < 191; y++)
        for (int x = 16; x < 145; x++)
        {
            int i = ((239 - y) * 320 + x) * 4;
            if (pixels[i] > 200 && pixels[i + 1] > 200 && pixels[i + 2] > 200) white++;
        }
        Assert.That(white, Is.GreaterThan(30), "The text region contains no readable glyph pixels.");
    }

    [Test, Category("NativeGraphics")]
    public void HalfPixelPlacedOnePixelBarsKeepTheirWidth()
    {
        RequireGraphics();
        string output = Path.Combine(scratch, "alignment.png");
        var result = Run("Samples/NativePreview/NativePreview.csproj", "PixelAlignmentScene", output, 960, 640);
        Assert.That(result.Code, Is.Zero, result.Log);
        var pixels = Decode(output, 960, 640);
        for (int column = 0; column < 4; column++)
        for (int bar = 0; bar < 7; bar++)
        {
            int x = 38 + column * 228 + ((column & 1) != 0 ? 1 : 0) + bar * 7;
            Pixel(pixels, 960, 640, x, 418, 242, 242, 237, 255);
            Pixel(pixels, 960, 640, x - 1, 418, 25, 29, 35, 255);
            Pixel(pixels, 960, 640, x + 1, 418, 25, 29, 35, 255);
        }
    }

    [Test, Category("NativeGraphics")]
    public void PreservesTransparencyAndExportsStraightAlpha()
    {
        RequireGraphics();
        string output = Path.Combine(scratch, "alpha.png");
        var result = Run("NowUI.Native.Tests/Scenes/Scenes.csproj", "TransparentScene", output, 64, 64);
        Assert.That(result.Code, Is.Zero, result.Log);
        var pixels = Decode(output, 64, 64);
        Pixel(pixels, 64, 64, 8, 8, 255, 0, 0, 128);
        Pixel(pixels, 64, 64, 56, 56, 0, 0, 0, 0);
    }

    [Test, Category("NativeGraphics")]
    public void AdvancesClockDeterministically()
    {
        RequireGraphics();
        string output = Path.Combine(scratch, "clock.png");
        var result = Run("NowUI.Native.Tests/Scenes/Scenes.csproj", "ClockScene", output, 32, 32, "--time", "0.5");
        Assert.That(result.Code, Is.Zero, result.Log);
        Pixel(Decode(output, 32, 32), 32, 32, 16, 16, 128, 0, 0, 255);
        byte[] first = File.ReadAllBytes(output);
        result = Run("NowUI.Native.Tests/Scenes/Scenes.csproj", "ClockScene", output, 32, 32, "--time", "0.5");
        Assert.That(result.Code, Is.Zero, result.Log);
        Assert.That(File.ReadAllBytes(output), Is.EqualTo(first));
    }

    [Test, Category("NativeGraphics")]
    public void CopiesActiveRenderTargetAndRestoresItsBinding()
    {
        RequireGraphics();
        string output = Path.Combine(scratch, "copy.png");
        var result = Run("NowUI.Native.Tests/Scenes/Scenes.csproj", "CopyScene", output, 32, 32);
        Assert.That(result.Code, Is.Zero, result.Log);
        Pixel(Decode(output, 32, 32), 32, 32, 16, 16, 255, 0, 0, 255);
    }

    [TestCase("ErrorScene"), TestCase("ThrowScene"), TestCase("DisposeScene"), TestCase("DisposeErrorScene"), Category("NativeGraphics")]
    public void FailedCaptureDoesNotReplaceExistingOutput(string scene)
    {
        RequireGraphics();
        string output = Path.Combine(scratch, "existing.png");
        byte[] original = [1, 2, 3, 4];
        File.WriteAllBytes(output, original);
        var result = Run("NowUI.Native.Tests/Scenes/Scenes.csproj", scene, output, 32, 32);
        Assert.That(result.Code, Is.EqualTo(1), result.Log);
        Assert.That(result.Log, Does.Contain("Deliberate"));
        Assert.That(File.ReadAllBytes(output), Is.EqualTo(original));
    }

    [Test, Category("NativeGraphics")]
    public void RendersProjectTextureAndNamedSpriteDirectlyFromUnityFiles()
    {
        RequireGraphics();
        string assetDirectory = Path.Combine(scratch, "Assets/UI");
        Directory.CreateDirectory(assetDirectory);
        File.WriteAllBytes(Path.Combine(assetDirectory, "tiles.png"), NowPng.EncodeRgba(2, 2,
            [0, 0, 255, 255, 255, 255, 0, 255, 255, 0, 0, 255, 0, 255, 0, 255]));
        File.WriteAllText(Path.Combine(assetDirectory, "tiles.png.meta"), """
            fileFormatVersion: 2
            guid: 0123456789abcdef0123456789abcdef
            TextureImporter:
              textureType: 8
              spriteMode: 2
              spritePixelsToUnits: 100
              nPOTScale: 0
              textureSettings:
                filterMode: 0
                wrapU: 1
                wrapV: 1
              spriteSheet:
                sprites:
                - name: green
                  rect: {x: 1, y: 1, width: 1, height: 1}
                  pivot: {x: 0.5, y: 0.5}
                  alignment: 9
                  border: {x: 0, y: 0, z: 0, w: 0}
                  internalID: 21300002
            """);
        string output = Path.Combine(scratch, "project-assets.png");
        var result = Run("NowUI.Native.Tests/Scenes/Scenes.csproj", "ProjectAssetsScene", output, 64, 32,
            "--unity-project", scratch);
        Assert.That(result.Code, Is.Zero, result.Log);
        var pixels = Decode(output, 64, 32);
        Pixel(pixels, 64, 32, 8, 8, 255, 0, 0, 255);
        Pixel(pixels, 64, 32, 24, 8, 0, 255, 0, 255);
        Pixel(pixels, 64, 32, 8, 24, 0, 0, 255, 255);
        Pixel(pixels, 64, 32, 24, 24, 255, 255, 0, 255);
        Pixel(pixels, 64, 32, 48, 16, 0, 255, 0, 255);
    }

    private (int Code, string Log) Run(string project, string scene, string output, int width, int height, params string[] extra)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = scratch, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { Path.Combine(standalone, "NowUI.Cli/bin", configuration, "net9.0/nowui.dll"),
            "render", Path.Combine(standalone, project), "--scene", scene, "--output", output,
            "--width", width.ToString(), "--height", height.ToString(), "--configuration", configuration, "--no-build" }.Concat(extra))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000)) { process.Kill(entireProcessTree: true); Assert.Fail("Native capture exceeded 60 seconds."); }
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static byte[] Decode(string path, int expectedWidth, int expectedHeight)
    {
        Assert.That(new NowPngImageDecoder().TryDecode(File.ReadAllBytes(path), out int width, out int height,
            out byte[] pixels, out string error), Is.True, error);
        Assert.That((width, height), Is.EqualTo((expectedWidth, expectedHeight)));
        return pixels;
    }

    private static void Pixel(byte[] pixels, int width, int height, int x, int y, params byte[] expected)
    {
        int offset = ((height - 1 - y) * width + x) * 4;
        for (int c = 0; c < 4; c++)
            Assert.That((int)pixels[offset + c], Is.EqualTo((int)expected[c]).Within(1), $"Pixel ({x},{y}), channel {c}");
    }

    private static void RequireGraphics()
    {
        if (Environment.GetEnvironmentVariable("NOWUI_TEST_NATIVE_GRAPHICS") != "1")
            Assert.Ignore("Set NOWUI_TEST_NATIVE_GRAPHICS=1 in a graphics-capable session to run native captures.");
    }
}
