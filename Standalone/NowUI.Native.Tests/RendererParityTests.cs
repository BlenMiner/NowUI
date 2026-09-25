using System.Diagnostics;
using NowUI.Hosting;
using NUnit.Framework;

namespace NowUI.Native.Tests;

[Category("NativeGraphics")]
public sealed class RendererParityTests
{
    [TestCase("gamma", 128), TestCase("linear", 188)]
    public void ColorSpaceConvertsColorsTexturesBlendingAndStraightAlpha(string colorSpace, int workingGray)
    {
        var pixels = Render("ColorSpaceScene", 128, 96, colorSpace);
        AssertPixel(pixels, 128, 96, 16, 16, 128, 128, 128, 255, 2);
        AssertPixel(pixels, 128, 96, 48, 16, 128, 128, 128, 255, 1);
        AssertPixel(pixels, 128, 96, 80, 16, workingGray, workingGray, workingGray, 255, 1);
        AssertPixel(pixels, 128, 96, 112, 16, workingGray, workingGray, workingGray, 255, 1);
        AssertPixel(pixels, 128, 96, 16, 80, 128, 64, 191, 128, 3);
        int solidText = 0;
        for (int y = 32; y < 64; y++) for (int x = 0; x < 100; x++)
        {
            int i = ((95 - y) * 128 + x) * 4;
            if (pixels[i] is >= 123 and <= 130) solidText++;
        }
        Assert.That(solidText, Is.GreaterThan(20), "The text must remain in its authored display color.");
    }

    [TestCase("gamma", 128), TestCase("linear", 188)]
    public void GlassBlursEarlierContentAndKeepsLaterContentSharp(string colorSpace, int gray)
    {
        var pixels = Render("GlassParityScene", 128, 96, colorSpace);
        AssertPixel(pixels, 128, 96, 4, 48, 255, 255, 255, 255, 0);
        AssertPixel(pixels, 128, 96, 12, 48, 0, 0, 0, 255, 0);
        AssertPixel(pixels, 128, 96, 48, 32, gray, gray, gray, 255, 12);
        AssertPixel(pixels, 128, 96, 64, 48, 255, 0, 0, 255, 0);
    }

    [TestCase("gamma"), TestCase("linear")]
    public void SdfBooleanOutlineShadowAndMaskRender(string colorSpace)
    {
        var pixels = Render("SdfParityScene", 128, 96, colorSpace);
        AssertPixel(pixels, 128, 96, 40, 30, 255, 0, 0, 255, 2);
        AssertPixel(pixels, 128, 96, 40, 25, 0, 255, 0, 255, 2);
        // The subtracted hole exposes the displaced blue shadow instead of the red shape fill.
        AssertPixel(pixels, 128, 96, 40, 48, 0, 0, 255, 255, 2);
        AssertPixel(pixels, 128, 96, 66, 51, 0, 0, 255, 255, 20);
        AssertPixel(pixels, 128, 96, 100, 48, 0, 255, 255, 255, 2);
        AssertPixel(pixels, 128, 96, 82, 18, 0, 0, 0, 255, 2);
    }

    [TestCase("gamma"), TestCase("linear")]
    public void SdfSpriteSilhouetteBakesThroughAllFiveImagePasses(string colorSpace)
    {
        var pixels = Render("SdfImageParityScene", 96, 96, colorSpace);
        AssertPixel(pixels, 96, 96, 48, 48, 255, 0, 0, 255, 2);
        AssertPixel(pixels, 96, 96, 30, 48, 0, 255, 0, 255, 2);
        AssertPixel(pixels, 96, 96, 18, 18, 0, 0, 0, 255, 0);
    }

    [TestCase("gamma"), TestCase("linear")]
    public void AdvertisedDataFormatsUploadBlitAndSampleWithoutColorDecoding(string colorSpace)
    {
        var pixels = Render("DataTextureParityScene", 224, 64, colorSpace);
        int red = colorSpace == "linear" ? 188 : 128;
        int green = colorSpace == "linear" ? 137 : 64;
        int blue = colorSpace == "linear" ? 225 : 191;
        for (int column = 0; column < 7; column++) for (int row = 0; row < 2; row++)
            AssertPixel(pixels, 224, 64, column * 32 + 16, row * 32 + 16,
                red, column >= 3 ? green : 0, column >= 5 ? blue : 0, 255, 1);
    }

    [Test]
    public void SdfWarpCallerClockIsDeterministicPerTimeAndMatchesTheShaderClock()
    {
        var first = Render("SdfWarpCallerClockScene", 128, 64, "gamma", time: 0.5);
        var repeat = Render("SdfWarpCallerClockScene", 128, 64, "gamma", time: 0.5);
        Assert.That(repeat, Is.EqualTo(first), "The same caller time must render identical pixels.");

        // A different phase moves both the warped fill and the cached warped mask.
        var later = Render("SdfWarpCallerClockScene", 128, 64, "gamma", time: 1.5);
        Assert.That(CountDifferent(first, later, 128, 0, 64, 0), Is.GreaterThan(20), "Warped fill must follow the caller time.");
        Assert.That(CountDifferent(first, later, 128, 64, 128, 0), Is.GreaterThan(20), "Warped mask must re-rasterize for a new caller time.");

        // SetTime(t) folds the phase on the CPU; it must match the shader clock at t.
        var host = Render("SdfWarpHostClockScene", 128, 64, "gamma", time: 0.5);
        Assert.That(CountDifferent(first, host, 128, 0, 128, 8), Is.Zero, "SetTime(Time.time) must match the host shader clock.");

        // SetTime(t) with speed s equals a static warp whose seed is seed + t * s. Captured
        // at host time 0, a fixed SetTime(0.5) still samples the 0.5 phase.
        var fixedTime = Render("SdfWarpFixedTimeScene", 128, 64, "gamma");
        var seedPhase = Render("SdfWarpSeedPhaseScene", 128, 64, "gamma");
        Assert.That(seedPhase, Is.EqualTo(fixedTime), "A caller-clock phase must equal the same seed-only phase.");
        Assert.That(CountDifferent(first, fixedTime, 128, 0, 128, 8), Is.Zero, "SetTime(0.5) must not depend on the host clock.");
    }

    static int CountDifferent(byte[] a, byte[] b, int width, int x0, int x1, int tolerance)
    {
        int height = a.Length / 4 / width, count = 0;
        for (int y = 0; y < height; y++) for (int x = x0; x < x1; x++)
        {
            int i = (y * width + x) * 4;
            for (int c = 0; c < 4; c++)
                if (Math.Abs(a[i + c] - b[i + c]) > tolerance) { count++; break; }
        }
        return count;
    }

    // The source checkout is a Unity project whose Resources folder holds the UGUI
    // gradient .mat; an empty project resolves no template at all. Both must draw.
    [TestCase("gamma", false), TestCase("linear", false), TestCase("gamma", true)]
    public void LinearRadialConicAndTextGradientsRender(string colorSpace, bool sourceProject)
    {
        var pixels = Render("GradientParityScene", 128, 96, colorSpace, sourceProject);
        AssertDominant(pixels, 128, 96, 3, 16, 0);     // linear start: red
        AssertDominant(pixels, 128, 96, 60, 16, 2);    // linear end: blue
        AssertDominant(pixels, 128, 96, 96, 16, 1);    // radial centre: green
        AssertDominant(pixels, 128, 96, 66, 2, 2);     // radial outside the circle: blue
        AssertDominant(pixels, 128, 96, 36, 40, 0);    // conic just after 12 o'clock: red
        AssertDominant(pixels, 128, 96, 28, 40, 1);    // conic just before 12 o'clock: green
        int leftRed = 0, rightBlue = 0, wrong = 0;
        for (int y = 40; y < 96; y++) for (int x = 64; x < 128; x++)
        {
            int i = ((95 - y) * 128 + x) * 4;
            if (pixels[i + 3] < 200 || pixels[i] + pixels[i + 2] < 200) continue;
            bool red = pixels[i] > pixels[i + 2];
            if (x < 80 && red) leftRed++;
            else if (x >= 104 && !red) rightBlue++;
            if (pixels[i + 1] > 60) wrong++;
        }
        Assert.That(leftRed, Is.GreaterThan(20), "Text gradient must start red.");
        Assert.That(rightBlue, Is.GreaterThan(20), "Text gradient must end blue.");
        Assert.That(wrong, Is.Zero, "A red-to-blue text gradient must not produce green.");
    }

    [TestCase("gamma", 128), TestCase("linear", 188)]
    public void OpacityRotationGlassFadeAndCoincidentSdfShapesRender(string colorSpace, int halfGray)
    {
        var pixels = Render("TransformScopesParityScene", 128, 96, colorSpace);
        AssertPixel(pixels, 128, 96, 16, 16, halfGray, halfGray, halfGray, 255, 2);
        AssertPixel(pixels, 128, 96, 68, 28, 255, 0, 0, 255, 1);   // inside the turned bar
        AssertPixel(pixels, 128, 96, 52, 16, 0, 0, 0, 255, 1);     // where the unturned bar was
        AssertPixel(pixels, 128, 96, 100, 16, 255, 255, 255, 255, 0);
        AssertPixel(pixels, 128, 96, 108, 16, 0, 0, 0, 255, 0);
        AssertPixel(pixels, 128, 96, 92, 68, 0, 255, 0, 255, 1);   // coincident discs: later wins

        int red = 0, green = 0;
        for (int y = 40; y < 96; y++) for (int x = 0; x < 64; x++)
        {
            int i = ((95 - y) * 128 + x) * 4;
            if (pixels[i] > 200 && pixels[i + 1] < 60) red++;
            if (pixels[i + 1] > 200 && pixels[i] < 60) green++;
        }
        Assert.That(green, Is.GreaterThan(300), "The later ring must cover the shared band.");
        Assert.That(red, Is.Zero, "Coincident rings must not flicker between fills.");
    }

    [TestCase("gamma"), TestCase("linear")]
    public void CenteredTextAndAlignedSdfTextShareOneLayout(string colorSpace)
    {
        var pixels = Render("AlignedTextParityScene", 128, 96, colorSpace);
        var top = InkCentroid(pixels, 0, 48);
        var bottom = InkCentroid(pixels, 48, 96);
        Assert.That(top.x, Is.EqualTo(64f).Within(1.5f), "Centered text");
        Assert.That(top.y, Is.EqualTo(24f).Within(1.5f), "Cap-centered text");
        Assert.That(bottom.x, Is.EqualTo(top.x).Within(1f), "SDF text uses the same horizontal layout");
        Assert.That(bottom.y - 48f, Is.EqualTo(top.y).Within(1f), "SDF text uses the same vertical layout");
    }

    static (float x, float y) InkCentroid(byte[] pixels, int yMin, int yMax)
    {
        double sx = 0, sy = 0, w = 0;
        for (int y = yMin; y < yMax; y++) for (int x = 0; x < 128; x++)
        {
            int i = ((95 - y) * 128 + x) * 4;
            double ink = pixels[i] / 255.0;
            sx += (x + 0.5) * ink; sy += (y + 0.5) * ink; w += ink;
        }
        Assert.That(w, Is.GreaterThan(20), "Expected rendered text.");
        return ((float)(sx / w), (float)(sy / w));
    }

    static byte[] Render(string scene, int width, int height, string colorSpace, bool sourceProject = false, double? time = null)
    {
        if (Environment.GetEnvironmentVariable("NOWUI_TEST_NATIVE_GRAPHICS") != "1")
            Assert.Ignore("Set NOWUI_TEST_NATIVE_GRAPHICS=1 in a desktop graphics session.");
        string standalone = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
        string configuration = new DirectoryInfo(TestContext.CurrentContext.TestDirectory).Parent!.Name;
        string directory = Path.Combine(Path.GetTempPath(), "nowui-renderer-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "Assets"));
        try
        {
            string output = Path.Combine(directory, "frame.png");
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { Path.Combine(standalone, "NowUI.Cli/bin", configuration, "net9.0/nowui.dll"),
                "render", Path.Combine(standalone, "NowUI.Native.Tests/Scenes/Scenes.csproj"), "--scene", scene,
                "--output", output, "--width", width.ToString(), "--height", height.ToString(),
                "--color-space", colorSpace, "--unity-project", sourceProject ? Path.GetDirectoryName(standalone)! : directory,
                "--configuration", configuration, "--no-build" }) start.ArgumentList.Add(argument);
            if (time.HasValue) { start.ArgumentList.Add("--time"); start.ArgumentList.Add(time.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000)) { process.Kill(entireProcessTree: true); Assert.Fail("Renderer parity capture exceeded 60 seconds."); }
            Assert.That(process.ExitCode, Is.Zero, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
            Assert.That(new NowPngImageDecoder().TryDecode(File.ReadAllBytes(output), out int actualWidth, out int actualHeight,
                out byte[] pixels, out string error), Is.True, error);
            Assert.That((actualWidth, actualHeight), Is.EqualTo((width, height)));
            string? artifacts = Environment.GetEnvironmentVariable("NOWUI_RENDER_ARTIFACT_DIR");
            if (!string.IsNullOrEmpty(artifacts)) { Directory.CreateDirectory(artifacts); File.Copy(output, Path.Combine(artifacts, scene + "-" + colorSpace + ".png"), true); }
            return pixels;
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    static void AssertDominant(byte[] pixels, int width, int height, int x, int y, int channel)
    {
        int i = ((height - 1 - y) * width + x) * 4;
        Assert.That((int)pixels[i + 3], Is.EqualTo(255), $"Pixel ({x},{y}) alpha");
        for (int c = 0; c < 3; c++)
            if (c != channel)
                Assert.That((int)pixels[i + channel], Is.GreaterThan(pixels[i + c] + 100), $"Pixel ({x},{y}) channel {channel} must dominate channel {c}");
    }

    static void AssertPixel(byte[] pixels, int width, int height, int x, int y, int r, int g, int b, int a, int tolerance)
    {
        int i = ((height - 1 - y) * width + x) * 4;
        int[] expected = [r, g, b, a];
        for (int c = 0; c < 4; c++) Assert.That((int)pixels[i + c], Is.EqualTo(expected[c]).Within(tolerance), $"Pixel ({x},{y}) channel {c}");
    }
}
