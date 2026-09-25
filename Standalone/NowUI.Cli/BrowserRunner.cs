using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;
using NowUI.Hosting;

namespace NowUI.Cli;

internal static class BrowserRunner
{
    internal static void Serve(string[] args)
    {
        if (args.Length < 2 || args[1].StartsWith('-')) throw new ArgumentException("Expected: nowui serve <site-directory> [--port <number>] [--no-open].");
        int port = 0;
        bool open = true, suppliedPort = false;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--no-open" && open) { open = false; continue; }
            if (args[i] == "--port" && !suppliedPort && ++i < args.Length && int.TryParse(args[i], out port) && port is >= 0 and <= 65535)
            { suppliedPort = true; continue; }
            throw new ArgumentException("Expected --port <0..65535> and/or --no-open, once each.");
        }
        string directory = Path.GetFullPath(args[1]);
        if (!File.Exists(Path.Combine(directory, "index.html"))) throw new ArgumentException("The site directory must contain index.html.");
        BrowserServer.Run(directory, port, open);
    }

    internal static void Run(BrowserOptions options)
    {
        string kit = Path.Combine(AppContext.BaseDirectory, "BrowserKit");
        if (!File.Exists(Path.Combine(kit, "NowUI.Browser.dll")))
            throw new InvalidOperationException("The optional browser kit is missing. Install a complete NowUI CLI bundle, or run Tools/Build-NowUIBrowserKit.ps1 and rebuild the CLI.");
        string assembly = ProjectBuilder.Build(new RenderOptions(options.Project, "", options.Scene, RenderOptions.DefaultWidth, RenderOptions.DefaultHeight, 0,
            options.Configuration, options.NoBuild, UnityProject: options.UnityProject));
        SceneFactoryOptions sceneFactory;
        using (var loaded = LoadedScene.Create(assembly, options.Scene))
        {
            sceneFactory = SceneFactoryOptions.FromType(loaded.SceneType);
        }
        string sceneName = sceneFactory.SceneName, sceneAssemblyName = sceneFactory.AssemblyName;
        var references = Dependencies(assembly, kit);
        string? unity = options.UnityProject ?? NowProjectAssets.FindProjectRoot(options.Project)
            ?? NowProjectAssets.FindProjectRoot(Environment.CurrentDirectory);
        string work = Path.Combine(Path.GetTempPath(), "nowui-web-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        PinNet9Sdk(work);
        // Keep a failed build's concrete project and diagnostics available for troubleshooting.
        Console.Error.WriteLine("Browser build: " + work);
        string site = Path.Combine(work, "site");
        var assets = BrowserAssets.Stage(unity, Path.Combine(AppContext.BaseDirectory, "NowUI", "Resources"),
            Path.Combine(work, "assets"), roots: options.AllAssets ? null : BrowserAssetSelection.FromAssemblies(references.Values));
        Console.Error.WriteLine($"Browser assets: {assets.ProjectFiles + assets.HostFiles} files, {assets.Bytes:N0} bytes.");
        string setup = BrowserEntrySource.ReflectionRegistration(references.Keys);
        string entrySource = Path.Combine(work, "BrowserEntry.cs");
        File.WriteAllText(entrySource, BrowserEntrySource.Create(File.ReadAllText(Path.Combine(kit, "App", "BrowserEntry.cs")), sceneFactory,
            setup));
        string webRoot = Path.Combine(work, "wwwroot");
        CopyTree(Path.Combine(kit, "wwwroot"), webRoot);
        string html = Path.Combine(webRoot, "index.html");
        File.WriteAllText(html, File.ReadAllText(html).Replace("<title>NowUI</title>", "<title>" +
            System.Net.WebUtility.HtmlEncode(options.Title) + "</title>", StringComparison.Ordinal));
        var properties = new XElement("PropertyGroup",
            P("TargetFramework", "net9.0-browser"), P("RuntimeIdentifier", "browser-wasm"), P("OutputType", "Exe"), P("AssemblyName", "NowUI.WebApp"),
            P("AllowUnsafeBlocks", "true"), P("WasmBuildNative", "true"), P("PublishTrimmed", "true"),
            P("JsonSerializerIsReflectionEnabledByDefault", "true"), P("EnableDefaultCompileItems", "false"),
            P("RunAOTCompilation", options.Aot ? "true" : "false"), P("WasmAppDir", site),
            P("WasmMainJSPath", Path.Combine(webRoot, "main.js")), P("WasmMainHTMLPath", html),
            P("WasmEmitSymbolMap", options.NativeSymbols ? "true" : "false"),
            P("WasmEmitSourceMap", "false"), P("DebugType", "none"), P("WasmDebugLevel", "0"));
        var items = new XElement("ItemGroup", new XElement("Compile", A("Include", entrySource)));
        string reflectionRoots = BrowserReflectionRoots.Write(references, Path.Combine(work, "NowUI.ReflectionRoots.xml"));
        items.Add(new XElement("TrimmerRootDescriptor", A("Include", reflectionRoots)));
        var preserved = BrowserTrimming.PreservedAssemblies(references, trimNowUi: true);
        bool trimScene = BrowserSceneTrimming.CanTrimSceneAssembly(references, sceneFactory);
        if (trimScene) preserved.Remove(sceneAssemblyName);
        foreach (var pair in references)
        {
            items.Add(new XElement("Reference", A("Include", pair.Key), P("HintPath", pair.Value),
                pair.Key == sceneAssemblyName ? P("Aliases", "global," + BrowserEntrySource.SceneAssemblyAlias) : null));
            // The descriptor covers known host reflection; consumer reflection retains full roots.
            if (preserved.Contains(pair.Key))
                items.Add(new XElement("TrimmerRootAssembly", A("Include", pair.Key)));
        }
        string project = Path.Combine(work, "Browser.csproj");
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), properties, items,
            new XElement("Import", A("Project", assets.PropsPath)),
            new XElement("Import", A("Project", Path.Combine(kit, "Browser.Native.targets"))))).Save(project);
        ProjectBuilder.RunDotnet(work, ["publish", project, "--configuration", "Release", "--nologo", "--verbosity", "minimal"], true);
        if (!File.Exists(Path.Combine(site, "_framework", "dotnet.js")))
            throw new InvalidOperationException("The WebAssembly SDK did not produce the expected browser runtime in " + site);
        CopyTree(webRoot, site);
        File.Copy(assets.ReportPath, Path.Combine(site, "nowui-assets.json"));
        File.WriteAllText(Path.Combine(site, "nowui-build.json"), JsonSerializer.Serialize(new { target = "web", scene = sceneName,
            aot = options.Aot, allAssets = options.AllAssets, nativeSymbols = options.NativeSymbols,
            trimmedSceneAssembly = trimScene,
            assetBytes = assets.Bytes }, new JsonSerializerOptions { WriteIndented = true }));
        CopyTree(Path.Combine(AppContext.BaseDirectory, "ThirdPartyLicenses"), Path.Combine(site, "ThirdPartyLicenses"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.md"), Path.Combine(site, "THIRD_PARTY_NOTICES.md"));
        string notices = Path.Combine(site, "BrowserNotices");
        Directory.CreateDirectory(Path.Combine(notices, "native"));
        foreach (string name in new[] { "THIRD_PARTY_NOTICES.md", "NowUI-LIBRARY-NOTICES.md", "NowUI-LICENSE.md" })
            File.Copy(Path.Combine(kit, name), Path.Combine(notices, name));
        foreach (string name in new[] { "HarfBuzz-LICENSE.txt", "FreeType-LICENSE.txt", "harfbuzz.json", "nowui-browser.json" })
            File.Copy(Path.Combine(kit, "native", name), Path.Combine(notices, "native", name));
        BrowserCompression.Compress(site, optimizeSize: !options.Preview);
        var sizes = BrowserSizeReport.Write(site);
        Console.Error.WriteLine($"Browser site: {sizes.OriginalBytes:N0} original bytes; {sizes.BrotliBytes:N0} bytes with Brotli; {sizes.StoredBytes:N0} bytes stored including all encodings.");
        Console.Error.WriteLine("Size totals include all assets and locale variants, not just initial downloads. Details: nowui-size.json.");
        if (options.Preview)
        {
            try { BrowserServer.Run(site, options.Port, !options.NoOpen); }
            finally { RemoveBuildDirectory(work); }
            return;
        }
        string output = options.Output!;
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        // Cross-volume publishing also works. Only the final move makes a complete site visible.
        string staging = Path.Combine(Path.GetDirectoryName(output)!, ".nowui-site-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyTree(site, staging);
            Directory.Move(staging, output);
        }
        finally { if (Directory.Exists(staging)) RemoveBuildDirectory(staging); }
        Console.WriteLine("Published browser site: " + output);
        Console.WriteLine("Serve this directory over HTTP(S), with application/wasm for .wasm files.");
        RemoveBuildDirectory(work);
    }

    static void RemoveBuildDirectory(string directory)
    {
        // Only fresh directories allocated by this command reach this helper.
        try { Directory.Delete(directory, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Could not remove browser build directory: " + directory); }
    }

    internal static void PinNet9Sdk(string directory)
    {
        string installed = ProjectBuilder.RunDotnet(directory, ["--list-sdks"], echo: false);
        string version = SelectNet9Sdk(installed);
        File.WriteAllText(Path.Combine(directory, "global.json"), JsonSerializer.Serialize(new
        {
            sdk = new { version, rollForward = "disable", allowPrerelease = false }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static string SelectNet9Sdk(string installed)
    {
        var versions = installed.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => line.Length > 0)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            .Select(value => Version.TryParse(value, out var version) && version.Major == 9 ? version : null)
            .Where(version => version != null).OrderByDescending(version => version).ToArray();
        if (versions.Length == 0)
            throw new InvalidOperationException("Browser publishing requires a released .NET 9 SDK and its wasm-tools workload. No .NET 9 SDK was found by dotnet --list-sdks.");
        return versions[0]!.ToString();
    }

    static XElement P(string name, string value) => new(name, BrowserAssets.MsBuildLiteral(value));
    static XAttribute A(string name, string value) => new(name, BrowserAssets.MsBuildLiteral(value));

    internal static SortedDictionary<string, string> Dependencies(string scene, string kit)
    {
        var result = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>([scene, Path.Combine(kit, "NowUI.Browser.dll")]);
        string[] directories = [Path.GetDirectoryName(scene)!, kit, AppContext.BaseDirectory];
        while (pending.TryDequeue(out string? path))
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            string name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            if (name is "NowUI.Desktop" || name.StartsWith("OpenTK", StringComparison.Ordinal))
                throw new InvalidOperationException("Browser scenes must use portable C# drawing code; this scene depends on " + name + ".");
            if (!result.TryAdd(name, path)) continue;
            foreach (var handle in metadata.AssemblyReferences)
            {
                string dependency = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                if (result.ContainsKey(dependency)) continue;
                var searchDirectories = BrowserAssetSelection.HostAssemblies.Contains(dependency)
                    ? new[] { AppContext.BaseDirectory, kit } : directories;
                string? file = searchDirectories.Select(directory => Path.Combine(directory, dependency + ".dll")).FirstOrDefault(File.Exists);
                if (file != null) pending.Enqueue(file);
                else if (dependency is not ("mscorlib" or "netstandard" or "System" or "Microsoft.CSharp") && !dependency.StartsWith("System.", StringComparison.Ordinal))
                    throw new FileNotFoundException($"Browser scene dependency '{dependency}' is missing beside the scene assembly.");
            }
        }
        return result;
    }

    static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target, true);
        }
    }
}
