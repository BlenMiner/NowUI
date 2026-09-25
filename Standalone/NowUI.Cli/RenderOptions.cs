using System.Globalization;

namespace NowUI.Cli;

internal sealed record RenderOptions(string Project, string Output, string? Scene,
    int Width, int Height, double Time, string Configuration, bool NoBuild, bool Preview = false, int? Frames = null,
    string? UnityProject = null, bool Animate = false, double Duration = 2, int Fps = 60,
    string? Input = null, bool Watch = true, UnityEngine.ColorSpace ColorSpace = UnityEngine.ColorSpace.Gamma,
    double LoadTimeout = 30)
{
    /// <summary>Default capture and preview-window width in pixels (16:9 with <see cref="DefaultHeight"/>).</summary>
    internal const int DefaultWidth = 960;
    /// <summary>Default capture and preview-window height in pixels.</summary>
    internal const int DefaultHeight = 540;

    internal const string Help = """
        NowUI native C# renderer

        nowui render <project.csproj> --output <image.png> [options]
        nowui preview <project.csproj> [options]
        nowui animate <project.csproj> --output <new-frame-directory> [options]
        nowui init <directory>

          --scene <type>             Public INowScene class (required if there is more than one)
          --width <pixels>           Image width, 1..8192 (default 960)
          --height <pixels>          Image height, 1..8192 (default 540, 16:9)
          --time <seconds>           Advance a fixed clock to this time, 0..60 (default 0)
          --configuration <name>     Debug or Release (default Release)
          --no-build                 Use the project's existing build output
          --frames <count>           Preview only: close after this many frames (for smoke tests)
          --unity-project <dir>      Asset source when outside a Unity project (otherwise auto-detected)
          --color-space <mode>       gamma (default) or linear
          --watch / --no-watch       Preview: reload saved code/assets (default enabled)
          --duration <seconds>       Animation duration, >0..60 (default 2)
          --fps <frames/second>      Animation and replay rate, 1..120 (default 60)
          --input <events.json>      Deterministic input events for render/animate
          --load-timeout <seconds>   Capture wait for remote resources, 1..600 (default 30)

        preview opens a resizable native window with mouse, keyboard and text input.
        Its optional --output saves the last frame when the window closes.

        Builds a normal .NET 9 C# project and renders with native OpenGL 3.3.
        Requires a graphics-capable desktop session. PNG capture uses no visible window.
        The scene implements NowUI.Hosting.INowScene.Draw(NowRect view).
        """;

    internal static RenderOptions Parse(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("render" or "preview" or "animate") || args[1].StartsWith('-'))
            throw new ArgumentException("Expected: nowui render|preview|animate <project.csproj>. Use --help for options.");
        bool preview = args[0] == "preview";
        bool animate = args[0] == "animate";
        var project = Path.GetFullPath(args[1]);
        if (!string.Equals(Path.GetExtension(project), ".csproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The input must be a C# .csproj file.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool noBuild = false;
        bool? watch = null;
        for (int i = 2; i < args.Length; i++)
        {
            string option = args[i];
            if (option is "--watch" or "--no-watch")
            {
                if (!preview || watch.HasValue) throw new ArgumentException("Supply --watch or --no-watch once, for preview only.");
                watch = option == "--watch";
                continue;
            }
            if (option == "--no-build")
            {
                if (noBuild) throw new ArgumentException("--no-build was supplied twice.");
                noBuild = true;
                continue;
            }
            if (option is not ("--output" or "--scene" or "--width" or "--height" or "--time" or "--configuration" or "--frames" or "--unity-project" or "--duration" or "--fps" or "--input" or "--color-space" or "--load-timeout"))
                throw new ArgumentException($"Unknown option '{option}'. Use --help for options.");
            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for {option}.");
            if (!values.TryAdd(option, args[i]))
                throw new ArgumentException($"{option} was supplied twice.");
        }

        values.TryGetValue("--output", out string? output);
        if (!preview && string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("--output is required (a PNG file for render, a new directory for animate).");
        output = string.IsNullOrWhiteSpace(output) ? "" : Path.GetFullPath(output);
        if (!animate && output.Length > 0 && !string.Equals(Path.GetExtension(output), ".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--output must end in .png for render/preview.");
        if (animate && (Directory.Exists(output) || File.Exists(output)))
            throw new ArgumentException("Animation --output must name a new directory; an existing output is never replaced.");
        int width = Dimension("--width", DefaultWidth);
        int height = Dimension("--height", DefaultHeight);
        // Bound both staging memory and readback, independently of driver limits.
        if ((long)width * height > 16_777_216)
            throw new ArgumentException("The image may contain at most 16,777,216 pixels.");
        string configuration = values.GetValueOrDefault("--configuration", "Release");
        if (configuration is not ("Debug" or "Release"))
            throw new ArgumentException("--configuration must be Debug or Release.");
        if (!double.TryParse(values.GetValueOrDefault("--time", "0"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double time) || !double.IsFinite(time) || time < 0 || time > 60)
            throw new ArgumentException("--time must be a finite number from 0 to 60 seconds.");
        if (preview && values.ContainsKey("--time"))
            throw new ArgumentException("preview uses the live clock; --time is available for render.");
        if (animate && values.ContainsKey("--time")) throw new ArgumentException("animate uses --duration, not --time.");
        if (!animate && values.ContainsKey("--duration")) throw new ArgumentException("--duration is for animate.");
        if (!double.TryParse(values.GetValueOrDefault("--duration", "2"), NumberStyles.Float, CultureInfo.InvariantCulture,
                out double duration) || !double.IsFinite(duration) || duration <= 0 || duration > 60)
            throw new ArgumentException("--duration must be finite, greater than zero, and at most 60 seconds.");
        if (!int.TryParse(values.GetValueOrDefault("--fps", "60"), NumberStyles.None, CultureInfo.InvariantCulture,
                out int fps) || fps < 1 || fps > 120) throw new ArgumentException("--fps must be an integer from 1 to 120.");
        if (preview && (values.ContainsKey("--fps") || values.ContainsKey("--input")))
            throw new ArgumentException("--fps and --input are for deterministic render/animate.");
        if (!double.TryParse(values.GetValueOrDefault("--load-timeout", "30"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double loadTimeout) || !double.IsFinite(loadTimeout) || loadTimeout < 1 || loadTimeout > 600)
            throw new ArgumentException("--load-timeout must be finite and between 1 and 600 seconds.");
        if (preview && values.ContainsKey("--load-timeout"))
            throw new ArgumentException("--load-timeout is for render/animate; preview loads remote resources asynchronously.");
        var colorSpace = values.GetValueOrDefault("--color-space", "gamma").ToLowerInvariant() switch
        {
            "gamma" => UnityEngine.ColorSpace.Gamma, "linear" => UnityEngine.ColorSpace.Linear,
            _ => throw new ArgumentException("--color-space must be gamma or linear.")
        };
        int? frames = null;
        if (values.TryGetValue("--frames", out string? count))
        {
            if (!preview || !int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < 1 || parsed > 100_000)
                throw new ArgumentException("--frames is for preview and must be an integer from 1 to 100000.");
            frames = parsed;
        }
        string? unityProject = values.TryGetValue("--unity-project", out var unityPath) ? Path.GetFullPath(unityPath) : null;
        if (unityProject != null && !Directory.Exists(Path.Combine(unityProject, "Assets")))
            throw new ArgumentException("--unity-project must name a Unity project directory containing Assets.");
        string? input = values.TryGetValue("--input", out var inputPath) ? Path.GetFullPath(inputPath) : null;
        return new(project, output, values.GetValueOrDefault("--scene"), width, height, time, configuration, noBuild,
            preview, frames, unityProject, animate, duration, fps, input, watch ?? true, colorSpace, loadTimeout);

        int Dimension(string key, int fallback)
        {
            if (!values.TryGetValue(key, out string? value)) return fallback;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int dimension) || dimension < 1 || dimension > 8192)
                throw new ArgumentException($"{key} must be an integer from 1 to 8192.");
            return dimension;
        }
    }
}
