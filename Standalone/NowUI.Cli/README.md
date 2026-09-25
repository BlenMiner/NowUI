# NowUI native CLI

Build and render actual C# NowUI scenes, with direct access to supported Unity
project assets. Includes the NowUI runtime and extensions, a native OpenGL host,
and bundled font/shader resources.

```text
nowui init MyPreview
nowui preview MyPreview/Preview.csproj
nowui render MyPreview/Preview.csproj --output preview.png
nowui animate MyPreview/Preview.csproj --output frames --duration 3 --fps 30
nowui publish MyPreview/Preview.csproj --target web --output site
nowui serve site
```

Preview watches saved code and assets. Render and animation capture use a
deterministic clock and accept JSON interaction replay. Captures and the preview
window default to 960 × 540 (16:9); set `--width` and `--height` to change it. `nowui --help` lists
options, including Gamma/Linear color space and explicit Unity project selection.

Native commands require the .NET 9 SDK and a desktop session with OpenGL 3.3. Unity-specific scenes,
components, custom importers and arbitrary Unity shaders are not loaded by this
host. Source projects remain ordinary C# and use the existing NowUI API.

The optional web target deploys the same scene to a static WebGL2 canvas. It
automatically includes supported asset paths and known GUID dependencies;
`--all-assets` handles fully computed paths. Web builds additionally require
the .NET 9 `wasm-tools` workload. Use `preview --target web` for a local browser
server. Native stays the default and retains live reload and capture commands.
