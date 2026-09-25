// Exports the NowUI assets the engine-free test run needs as plain data.
//
// WHY THIS EXISTS. Standalone/Tests compiles NowUI's own Unity test suite against the NowUI.Engine shim, with no Unity
// process anywhere. Thirty-one of the thirty-six gate files reach Now.defaultFont, and every one of them expects the
// real NotoSans metrics: label widths decide hit rectangles, so a synthetic font would move every pointer position in
// NowDockingTests and every extras-channel assertion in NowTextStylingTests. The glyph table, the material templates
// and the shader property declarations come from Unity's serialized assets (NowFont is
// [PreferBinarySerialization], the materials are YAML, the shaders are source). This Editor pass preserves the
// checked-in test fixtures and native host's built-in templates. Direct project assets now load through
// NowProjectAssets; users do not run this exporter to preview their assets.
//
// It is EDITOR-ONLY on purpose (design H.7 / work unit U24): it lives in the NowUI.Editor asmdef, so it cannot change
// the runtime assembly, its public API, or anything a player ships. It writes OUTSIDE Assets/ - into
// Standalone/Tests/Fixtures/ - so the export adds no Unity assets either.
//
// WHAT IT WRITES (design section 7.2 "Support/NowStandaloneTestResources.cs", test plan section 3.3):
//   Fixtures/materials.json                       every non-UGUI material template under a Resources folder, with the
//                                                 shader it uses and its float/vector/colour/texture/keyword values
//   Fixtures/shaders.json                         every NowUI shader program: name, pass count, declared properties
//                                                 (type + default) and keywords - this is what makes the shim's
//                                                 Material.HasProperty answer for a property nobody has assigned
//   Fixtures/NowUI/NotoSans.family.json           the family: which face file fills each of the four slots
//   Fixtures/NowUI/NotoSans-<Face>.font.json      per face: atlasInfo VERBATIM, the dynamic-atlas settings, the
//                                                 material template name and the face material's own property values
//   Fixtures/NowUI/NotoSans-<Face>.ttf            per face: the source font, raw bytes
//
// ONE THING THE PLAN ASSUMED IS NOT TRUE OF THESE ASSETS, and the shape of the font fixture follows from it. Design
// H.7 and test plan section 3.3 describe exporting each face's PREBAKED atlasInfo - "atlas/metrics/glyphs verbatim" -
// and omitting the embedded TTF as unnecessary weight. There is no prebaked atlas in this project. Every NowFont asset
// under Assets/NowUI/Assets/Fonts carries an empty atlasInfo (0 glyphs, no atlas texture, zero metrics) and nothing
// but its source TTF in _fontBytes; the run logs that inventory every time, so the claim is checkable and not taken on
// faith. NowUI's fonts are dynamic: NowFontCompiler bakes glyphs on demand from those bytes, preferring its MANAGED
// compiler (NowManagedFontSession) and dropping to the native plugin only when the managed path declines. So the bytes
// are not optional weight - they are the entire payload, and a standalone run that bakes from them measures text with
// the same code and the same input Unity uses. That is why every face here ships its TTF and why the exported
// atlasInfo is (correctly) empty; if a prebaked asset ever lands, the same fields carry it with no format change.
//
// HISTORICAL BROWSER MEASUREMENT: this pass once baked printable ASCII into a .page<N>.bin sidecar per face and
// declared it under a "bakedPages" member. The standalone browser bundle is now retired. That export step was removed
// earlier, and the reason is worth
// keeping because the numbers point the other way round from the intuition that put it here.
//
// Measured in a real browser, on the shipped bundle, with the frame pump blocked so nothing consumed the first frame
// before it was timed. Installing the four pages cost 311 ms of that frame - 251 ms of it inside the MANAGED PNG
// decoder (NowWebPng, ~63 ms per 1024 px page in interpreted WebAssembly) and the rest in the channel swizzle - and
// bought back 456 ms of rasterisation. A net 145 ms, for 875 KB, which was a quarter of the whole bundle. Halving the
// atlas cell to 32/8 (NowFont.DEFAULT_DYNAMIC_ATLAS_SIZE) then made rasterisation 2.8x cheaper, so what the pages
// bought fell to about 160 ms while what they cost stayed at 311 ms - they had become a net LOSS as well as the
// single largest thing in the bundle. Deleting them took ?area=text's first frame from 600 ms to 434 ms AND removed
// 875 KB. Probe apps drawing 0 / 1 / 95 glyphs agree.
//
// The MECHANISM is untouched and still supported: NowFont.BakedPage / SetBakedPages / EnsureBakedPagesLoaded, the
// Editor's own bake buttons (NowFontEditor), and NowFileResources, which reads a
// "bakedPages" member if a fixture carries one. Only this export stopped writing one. Anyone re-enabling it should
// re-measure the decode first: at 32/8 a page is a quarter of the pixels, so the cost is roughly a quarter too, and
// the trade may swing back.
//
// Still deliberately NOT exported: a prebaked atlasInfo ("atlasPng", reserved) - a baked page is not one of those, and
// no test in the subset samples one. The family's fallback families (CJK, Arabic, emoji, Material Design icons) are omitted
// too: fallback traversal only happens on a glyph miss and no subset test misses, and their TTFs run to 5-10 MB each.
// Their names go out under "omittedFallbacks" so the omission is visible rather than implied.
//
// Output is deterministic: everything is sorted by ordinal name, floats are written with the round-trip "G9" format,
// and there is no timestamp anywhere. Re-running the export on an unchanged project produces byte-identical files, so
// a diff in the checked-in fixtures always means an asset actually changed.
//
// Invoke it from the menu, or headless with the project closed:
//   Unity.exe -batchmode -quit -projectPath <root> \
//             -executeMethod NowUI.Editor.NowStandaloneAssetExport.Export \
//             -logFile <root>/artifacts/local/export.log
// On failure it logs an error and exits 1 so a batch run cannot report success with a half-written fixture.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI.Editor
{
    /// <summary>
    /// Exports NowUI's fonts, material templates and shader declarations to JSON under
    /// <c>Standalone/Tests/Fixtures/</c> for the engine-free test run. Editor-only; never referenced by runtime code.
    /// </summary>
    public static class NowStandaloneAssetExport
    {
        /// <summary>Menu entry point; identical to the <c>-executeMethod</c> one.</summary>
        public const string MENU_PATH = "Tools/NowUI/Export Standalone Test Fixtures";

        /// <summary>Schema tags. Bump the version when a consumer would have to change to read the file.</summary>
        const string MATERIALS_SCHEMA = "nowui.standalone.materials/1";
        const string SHADERS_SCHEMA = "nowui.standalone.shaders/1";
        const string FONT_FAMILY_SCHEMA = "nowui.standalone.fontfamily/1";
        const string FONT_FACE_SCHEMA = "nowui.standalone.font/1";

        const string SEARCH_ROOT = "Assets/NowUI";
        const string FONT_FAMILY_ASSET_PATH = "Assets/NowUI/Assets/Resources/NowUI/NotoSans.asset";
        const string FONT_FAMILY_RESOURCE_PATH = "NowUI/NotoSans";
        const string FONT_OUTPUT_SUBDIRECTORY = "NowUI";
        const string OUTPUT_ENVIRONMENT_VARIABLE = "NOWUI_STANDALONE_FIXTURES";
        const string DEFAULT_OUTPUT_RELATIVE_PATH = "Standalone/Tests/Fixtures";

        /// <summary>
        /// The material templates the standalone resource provider must be able to serve. Listed so a missing or
        /// renamed asset fails the export loudly instead of surfacing later as a null <c>Resources.Load</c> - which,
        /// under the Unity-parity log policy, would fail an unrelated test with an unrelated message.
        /// </summary>
        static readonly string[] REQUIRED_MATERIAL_RESOURCE_PATHS =
        {
            "NowUI/BezierMaterial",
            "NowUI/GlassBlurMaterial",
            "NowUI/GlassMaterial",
            "NowUI/GradientMaterial",
            "NowUI/RippleMaterial",
            "NowUI/SdfMaterial",
            "NowUI/TxtMaterial",
            "NowUI/TxtMaterialRGBA",
            "NowUI/UIMaterial",
        };

        /// <summary>Shader names the core and the extensions reach through <c>Shader.Find</c>.</summary>
        static readonly string[] REQUIRED_SHADER_NAMES =
        {
            "Hidden/NowUI/SDF Image Field",
            "NowUI/Color Picker",
            "NowUI/SDF Scene",
            "NowUI/UI Bezier",
            "NowUI/UI Ripple",
        };

        [MenuItem(MENU_PATH)]
        public static void Export()
        {
            try
            {
                string outputRoot = ResolveOutputRoot();
                List<string> written = ExportTo(outputRoot);

                var summary = new StringBuilder();
                summary.Append("Standalone asset export: ")
                       .Append(written.Count)
                       .Append(" file(s) under ")
                       .Append(outputRoot.Replace('\\', '/'))
                       .Append('.');

                for (int i = 0; i < written.Count; i++)
                {
                    var info = new FileInfo(written[i]);
                    summary.Append("\n  ")
                           .Append(Relative(outputRoot, written[i]))
                           .Append("  ")
                           .Append(info.Length.ToString(CultureInfo.InvariantCulture))
                           .Append(" bytes");
                }

                Debug.Log(summary.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError($"Standalone asset export failed: {exception}");

                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        /// <summary>Writes every fixture into <paramref name="outputRoot"/> and returns the absolute paths written.</summary>
        public static List<string> ExportTo(string outputRoot)
        {
            if (string.IsNullOrEmpty(outputRoot))
                throw new ArgumentException("An output directory is required.", nameof(outputRoot));

            Directory.CreateDirectory(outputRoot);

            var written = new List<string>();
            written.Add(ExportMaterials(outputRoot));
            written.Add(ExportShaders(outputRoot));
            LogFontInventory();
            written.AddRange(ExportFontFamily(outputRoot));
            return written;
        }

        /// <summary>
        /// <c>Standalone/Tests/Fixtures</c> beside the Unity project, overridable through
        /// <c>NOWUI_STANDALONE_FIXTURES</c> so a CI job can stage the export somewhere else.
        /// </summary>
        static string ResolveOutputRoot()
        {
            string configured = Environment.GetEnvironmentVariable(OUTPUT_ENVIRONMENT_VARIABLE);

            if (!string.IsNullOrEmpty(configured))
                return Path.GetFullPath(configured);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, DEFAULT_OUTPUT_RELATIVE_PATH));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Materials
        // ---------------------------------------------------------------------------------------------------------

        static string ExportMaterials(string outputRoot)
        {
            List<string> paths = new List<string>();
            List<Material> materials = new List<Material>();

            foreach (string assetPath in FindAssetPaths("t:Material"))
            {
                string resourcePath = ToResourcePath(assetPath);

                // Only Resources assets can be reached by Resources.Load, and only those are templates.
                if (resourcePath == null)
                    continue;

                // The UGUI variants are excluded by design: NOWUI_UGUI is compiled out of the standalone build, so a
                // request for one is a bug the provider should show as a null rather than satisfy.
                if (resourcePath.EndsWith("UGUI", StringComparison.Ordinal))
                    continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                // FindAssets also reports assets that merely CONTAIN a material sub-asset (every .ttf.asset does);
                // those load as null at the main-asset path and are not templates.
                if (material == null)
                    continue;

                paths.Add(resourcePath);
                materials.Add(material);
            }

            SortByKey(paths, materials);
            RequireAll("material template", REQUIRED_MATERIAL_RESOURCE_PATHS, paths);

            var json = new JsonWriter();
            json.BeginObject();
            json.String("schema", MATERIALS_SCHEMA);
            json.String("unityVersion", Application.unityVersion);

            // Colour properties are stored in the project's colour space; the loader has to know which one produced
            // these numbers before it can compare them with anything.
            json.String("colorSpace", PlayerSettings.colorSpace.ToString());
            json.BeginArray("materials");

            for (int i = 0; i < materials.Count; i++)
                WriteMaterial(json, null, paths[i], materials[i]);

            json.EndArray();
            json.EndObject();

            return WriteJson(Path.Combine(outputRoot, "materials.json"), json);
        }

        /// <summary>
        /// Writes one material as an object. <paramref name="memberName"/> is the JSON key (null inside an array);
        /// <paramref name="resourcePath"/> is omitted for materials that are not loadable through Resources - a font
        /// face's own material, for instance.
        /// </summary>
        static void WriteMaterial(JsonWriter json, string memberName, string resourcePath, Material material)
        {
            json.BeginObject(memberName);

            if (resourcePath != null)
                json.String("resourcePath", resourcePath);

            json.String("assetPath", NullIfEmpty(AssetDatabase.GetAssetPath(material)));
            json.String("name", material.name);

            Shader shader = material.shader;
            json.String("shader", shader != null ? shader.name : null);
            json.Number("renderQueue", material.renderQueue);

            string[] keywords = material.shaderKeywords ?? Array.Empty<string>();
            Array.Sort(keywords, StringComparer.Ordinal);
            json.BeginArray("keywords");

            for (int i = 0; i < keywords.Length; i++)
                json.String(null, keywords[i]);

            json.EndArray();

            var floats = new List<int>();
            var vectors = new List<int>();
            var colors = new List<int>();
            var textures = new List<int>();
            CollectProperties(shader, floats, vectors, colors, textures);

            json.BeginArray("floats");

            for (int i = 0; i < floats.Count; i++)
            {
                string name = shader.GetPropertyName(floats[i]);
                json.BeginObject(null, true);
                json.String("name", name);
                json.Number("value", material.GetFloat(name));
                json.EndObject();
            }

            json.EndArray();

            json.BeginArray("vectors");

            for (int i = 0; i < vectors.Count; i++)
            {
                string name = shader.GetPropertyName(vectors[i]);
                json.BeginObject(null, true);
                json.String("name", name);
                json.Vector("value", material.GetVector(name));
                json.EndObject();
            }

            json.EndArray();

            json.BeginArray("colors");

            for (int i = 0; i < colors.Count; i++)
            {
                string name = shader.GetPropertyName(colors[i]);
                Color color = material.GetColor(name);
                json.BeginObject(null, true);
                json.String("name", name);
                json.Vector("value", new Vector4(color.r, color.g, color.b, color.a));
                json.EndObject();
            }

            json.EndArray();

            json.BeginArray("textures");

            for (int i = 0; i < textures.Count; i++)
            {
                string name = shader.GetPropertyName(textures[i]);
                Texture texture = material.GetTexture(name);
                json.BeginObject(null, true);
                json.String("name", name);

                // Templates leave their samplers unassigned; when one is not, record what it points at so the
                // difference is visible rather than silently dropped.
                json.String("texture", texture != null ? NullIfEmpty(AssetDatabase.GetAssetPath(texture)) : null);
                json.String("textureName", texture != null ? texture.name : null);
                json.Vector("scale", material.GetTextureScale(name));
                json.Vector("offset", material.GetTextureOffset(name));
                json.EndObject();
            }

            json.EndArray();
            json.EndObject();
        }

        /// <summary>Splits a shader's declared properties into the four buckets the shim's material model uses.</summary>
        static void CollectProperties(
            Shader shader,
            List<int> floats,
            List<int> vectors,
            List<int> colors,
            List<int> textures)
        {
            if (shader == null)
                return;

            int count = shader.GetPropertyCount();

            for (int i = 0; i < count; i++)
            {
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                    case ShaderPropertyType.Int:
                        floats.Add(i);
                        break;

                    case ShaderPropertyType.Vector:
                        vectors.Add(i);
                        break;

                    case ShaderPropertyType.Color:
                        colors.Add(i);
                        break;

                    case ShaderPropertyType.Texture:
                        textures.Add(i);
                        break;
                }
            }

            SortIndicesByName(shader, floats);
            SortIndicesByName(shader, vectors);
            SortIndicesByName(shader, colors);
            SortIndicesByName(shader, textures);
        }

        static void SortIndicesByName(Shader shader, List<int> indices)
        {
            indices.Sort((a, b) => string.CompareOrdinal(shader.GetPropertyName(a), shader.GetPropertyName(b)));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Shaders
        // ---------------------------------------------------------------------------------------------------------

        static string ExportShaders(string outputRoot)
        {
            List<string> names = new List<string>();
            List<Shader> shaders = new List<Shader>();
            List<string> assetPaths = new List<string>();

            foreach (string assetPath in FindAssetPaths("t:Shader"))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);

                if (shader == null)
                    continue;

                names.Add(shader.name);
                shaders.Add(shader);
                assetPaths.Add(assetPath);
            }

            SortByKey(names, shaders, assetPaths);
            RequireAll("shader", REQUIRED_SHADER_NAMES, names);

            var json = new JsonWriter();
            json.BeginObject();
            json.String("schema", SHADERS_SCHEMA);
            json.String("unityVersion", Application.unityVersion);
            json.BeginArray("shaders");

            for (int i = 0; i < shaders.Count; i++)
                WriteShader(json, shaders[i], assetPaths[i]);

            json.EndArray();
            json.EndObject();

            return WriteJson(Path.Combine(outputRoot, "shaders.json"), json);
        }

        static void WriteShader(JsonWriter json, Shader shader, string assetPath)
        {
            json.BeginObject();
            json.String("name", shader.name);
            json.String("assetPath", assetPath);

            // Two independent readings of the same number. shader.passCount is the compiled truth and is what we
            // publish; the source parse is the fallback for a headless run where the program did not compile, and a
            // cross-check the rest of the time. A disagreement is logged, never silently resolved.
            int parsed = ParsePassCount(ReadTextOrEmpty(assetPath));
            int compiled = ReadCompiledPassCount(shader);
            int passCount = compiled >= 1 ? compiled : parsed;

            if (compiled >= 1 && parsed >= 1 && compiled != parsed)
            {
                Debug.LogWarning(
                    $"Standalone asset export: '{shader.name}' reports {compiled} pass(es) but its source parses as " +
                    $"{parsed}. Publishing {compiled}.");
            }

            json.Number("passCount", passCount < 1 ? 1 : passCount);
            json.Number("passCountParsed", parsed);
            json.Number("renderQueue", ReadRenderQueue(shader));
            json.Bool("isSupported", shader.isSupported);

            json.BeginArray("properties");
            int count = shader.GetPropertyCount();
            var indices = new List<int>(count);

            for (int i = 0; i < count; i++)
                indices.Add(i);

            SortIndicesByName(shader, indices);

            for (int i = 0; i < indices.Count; i++)
                WriteShaderProperty(json, shader, indices[i]);

            json.EndArray();

            string[] keywords = ReadKeywords(shader);
            json.BeginArray("keywords");

            for (int i = 0; i < keywords.Length; i++)
                json.String(null, keywords[i]);

            json.EndArray();
            json.EndObject();
        }

        static void WriteShaderProperty(JsonWriter json, Shader shader, int index)
        {
            ShaderPropertyType type = shader.GetPropertyType(index);

            json.BeginObject();
            json.String("name", shader.GetPropertyName(index));
            json.String("type", type.ToString());
            json.String("description", shader.GetPropertyDescription(index));

            ShaderPropertyFlags flags = shader.GetPropertyFlags(index);
            json.BeginArray("flags");

            if (flags != ShaderPropertyFlags.None)
            {
                string[] names = flags.ToString().Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < names.Length; i++)
                    json.String(null, names[i]);
            }

            json.EndArray();

            string[] attributes = shader.GetPropertyAttributes(index) ?? Array.Empty<string>();
            json.BeginArray("attributes");

            for (int i = 0; i < attributes.Length; i++)
                json.String(null, attributes[i]);

            json.EndArray();

            switch (type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                case ShaderPropertyType.Int:
                    json.Number("defaultFloat", shader.GetPropertyDefaultFloatValue(index));

                    if (type == ShaderPropertyType.Range)
                    {
                        Vector2 limits = shader.GetPropertyRangeLimits(index);
                        json.Number("rangeMin", limits.x);
                        json.Number("rangeMax", limits.y);
                    }

                    break;

                case ShaderPropertyType.Vector:
                case ShaderPropertyType.Color:
                    json.Vector("defaultVector", shader.GetPropertyDefaultVectorValue(index));
                    break;

                case ShaderPropertyType.Texture:
                    // "white", "black", "bump", ... - the built-in texture a fresh material samples.
                    json.String("defaultTexture", NullIfEmpty(shader.GetPropertyTextureDefaultName(index)));
                    break;
            }

            json.EndObject();
        }

        static int ReadCompiledPassCount(Shader shader)
        {
            try
            {
                return shader.passCount;
            }
            catch (Exception)
            {
                // A headless editor can refuse to compile a program; the source parse covers that case.
                return 0;
            }
        }

        static int ReadRenderQueue(Shader shader)
        {
            try
            {
                return shader.renderQueue;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        static string[] ReadKeywords(Shader shader)
        {
            try
            {
                string[] keywords = shader.keywordSpace.keywordNames ?? Array.Empty<string>();
                var copy = new string[keywords.Length];
                Array.Copy(keywords, copy, keywords.Length);
                Array.Sort(copy, StringComparer.Ordinal);
                return copy;
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Counts the passes declared in the first SubShader of a .shader source. Comments are stripped first, and
        /// <c>Pass</c> is matched as a whole word at brace depth one, so <c>UsePass</c>, <c>GrabPass</c> and any
        /// identifier that merely ends in "Pass" do not count.
        /// </summary>
        static int ParsePassCount(string source)
        {
            if (string.IsNullOrEmpty(source))
                return 0;

            string text = StripComments(source);
            int subShader = IndexOfWord(text, "SubShader", 0);

            if (subShader < 0)
                return 0;

            int open = text.IndexOf('{', subShader);

            if (open < 0)
                return 0;

            int depth = 0;
            int passes = 0;

            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;

                    if (depth == 0)
                        break;

                    continue;
                }

                if (depth == 1 && IsWordAt(text, i, "Pass"))
                {
                    passes++;
                    i += "Pass".Length - 1;
                }
            }

            return passes;
        }

        static string StripComments(string source)
        {
            var result = new StringBuilder(source.Length);

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n')
                        i++;

                    result.Append('\n');
                    continue;
                }

                if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;

                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                        i++;

                    i++;
                    result.Append(' ');
                    continue;
                }

                result.Append(source[i]);
            }

            return result.ToString();
        }

        static int IndexOfWord(string text, string word, int start)
        {
            for (int i = start; i + word.Length <= text.Length; i++)
            {
                if (IsWordAt(text, i, word))
                    return i;
            }

            return -1;
        }

        static bool IsWordAt(string text, int index, string word)
        {
            if (index + word.Length > text.Length)
                return false;

            if (string.CompareOrdinal(text, index, word, 0, word.Length) != 0)
                return false;

            if (index > 0 && IsWordCharacter(text[index - 1]))
                return false;

            int after = index + word.Length;
            return after >= text.Length || !IsWordCharacter(text[after]);
        }

        static bool IsWordCharacter(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Fonts
        // ---------------------------------------------------------------------------------------------------------

        sealed class Face
        {
            public string slot;
            public NowFont font;
            public string fileName;
        }

        static List<string> ExportFontFamily(string outputRoot)
        {
            var family = AssetDatabase.LoadAssetAtPath<NowFontFamily>(FONT_FAMILY_ASSET_PATH);

            if (family == null)
            {
                throw new InvalidOperationException(
                    $"The NotoSans family was not found at '{FONT_FAMILY_ASSET_PATH}'. Thirty-one of the thirty-six " +
                    "gate files need it; the export cannot produce a usable fixture without it.");
            }

            var faces = new List<Face>
            {
                new Face { slot = "regular", font = family.regular },
                new Face { slot = "bold", font = family.bold },
                new Face { slot = "italic", font = family.italic },
                new Face { slot = "boldItalic", font = family.boldItalic },
            };

            if (faces[0].font == null)
                throw new InvalidOperationException("The NotoSans family has no regular face; every text draw needs it.");

            string fontDirectory = Path.Combine(outputRoot, FONT_OUTPUT_SUBDIRECTORY);
            Directory.CreateDirectory(fontDirectory);

            var written = new List<string>();
            var byFont = new Dictionary<NowFont, string>();

            for (int i = 0; i < faces.Count; i++)
            {
                Face face = faces[i];

                if (face.font == null)
                    continue;

                // Two slots can point at the same NowFont; write it once and let both slots name the same file.
                string fileName;

                if (!byFont.TryGetValue(face.font, out fileName))
                {
                    fileName = FaceFileName(face.font);
                    ExportFace(fontDirectory, fileName, face.font, written);
                    byFont.Add(face.font, fileName);
                }

                face.fileName = fileName;
            }

            // Checked after every face has been written and described, so the log carries the whole picture rather
            // than stopping at the first face. A face is usable if it can measure text at all: either it carries a
            // prebaked glyph table, or it carries the source TTF the compiler bakes from. Neither means no fixture.
            NowFont regular = faces[0].font;

            if (GlyphCount(regular) == 0 && regular.GetSourceByteCount() == 0)
            {
                throw new InvalidOperationException(
                    $"The NotoSans regular face '{regular.name}' has neither a prebaked glyph table nor source font " +
                    "bytes, so nothing the standalone run could measure text with exists to export.");
            }

            var json = new JsonWriter();
            json.BeginObject();
            json.String("schema", FONT_FAMILY_SCHEMA);
            json.String("unityVersion", Application.unityVersion);
            json.String("resourcePath", FONT_FAMILY_RESOURCE_PATH);
            json.String("name", family.name);
            json.String("assetPath", FONT_FAMILY_ASSET_PATH);

            json.BeginObject("faces");

            for (int i = 0; i < faces.Count; i++)
                json.String(faces[i].slot, faces[i].fileName);

            json.EndObject();

            // Empty in M1 by design (test plan section 3.3): fallback traversal only runs on a glyph miss and no
            // subset test misses. The names go out anyway so the omission is a recorded decision, not a gap.
            json.BeginArray("fallbacks");
            json.EndArray();

            json.BeginArray("omittedFallbacks");
            var fallbacks = family.fallbacks;

            if (fallbacks != null)
            {
                var names = new List<string>(fallbacks.Count);

                for (int i = 0; i < fallbacks.Count; i++)
                {
                    if (fallbacks[i] != null)
                        names.Add(fallbacks[i].name);
                }

                names.Sort(StringComparer.Ordinal);

                for (int i = 0; i < names.Count; i++)
                    json.String(null, names[i]);
            }

            json.EndArray();
            json.EndObject();

            written.Add(WriteJson(Path.Combine(fontDirectory, "NotoSans.family.json"), json));
            return written;
        }

        /// <summary>
        /// Writes one face: its JSON description, and its source TTF beside it. Both paths are appended to
        /// <paramref name="written"/>.
        /// </summary>
        static void ExportFace(string fontDirectory, string fileName, NowFont font, List<string> written)
        {
            NowFontAtlasInfo info = font.atlasInfo;
            int glyphCount = info.glyphs != null ? info.glyphs.Length : 0;

            // Say what each face actually holds, every run. Whether the checked-in asset carries a prebaked glyph
            // table or only the source TTF decides whether the standalone run can measure text at all, and that is
            // worth stating in the log rather than inferring from a failure later.
            Debug.Log(
                $"Standalone asset export: face '{font.name}' - {glyphCount} glyph(s), " +
                $"atlas type '{info.atlas.type ?? "(none)"}', atlas {info.atlas.width}x{info.atlas.height}, " +
                $"size {info.atlas.size}, distanceRange {info.atlas.distanceRange}, " +
                $"em {info.metrics.emSize.ToString(CultureInfo.InvariantCulture)}, " +
                $"lineHeight {info.metrics.lineHeight.ToString(CultureInfo.InvariantCulture)}, " +
                $"texture {(font.atlas != null ? font.atlas.width + "x" + font.atlas.height : "(null)")}, " +
                $"embedded font bytes {ReadFontByteCount(font)}.");

            // The source TTF goes out as raw bytes, not base64 in the JSON: a third smaller, and it is exactly what
            // the compiler's byte[] parameter wants, with no decode step between the fixture and the bake.
            string fontBytesFile = null;
            byte[] fontBytes;

            if (font.TryGetSourceBytes(out fontBytes) && fontBytes.Length > 0)
            {
                fontBytesFile = StripFaceSuffix(fileName) + ".ttf";
                string fontBytesPath = Path.GetFullPath(Path.Combine(fontDirectory, fontBytesFile));
                File.WriteAllBytes(fontBytesPath, fontBytes);
                written.Add(fontBytesPath);
            }

            var json = new JsonWriter();
            json.BeginObject();
            json.String("schema", FONT_FACE_SCHEMA);
            json.String("unityVersion", Application.unityVersion);
            json.String("name", font.name);
            json.String("assetPath", NullIfEmpty(AssetDatabase.GetAssetPath(font)));
            json.String("fileName", fileName);

            // The one field a loader has to branch on. "dynamic": no glyph table, bake from fontBytesFile - what every
            // NowUI font asset is today. "prebaked": atlasInfo carries the glyphs and can be used as-is. Stated rather
            // than inferred from an empty array, so a loader cannot mistake a failed read for a dynamic font.
            json.String("kind", glyphCount > 0 ? "prebaked" : "dynamic");

            Texture2D atlas = font.atlas;
            json.Number("atlasWidth", atlas != null ? atlas.width : info.atlas.width);
            json.Number("atlasHeight", atlas != null ? atlas.height : info.atlas.height);
            json.String("atlasTextureFormat", atlas != null ? atlas.format.ToString() : null);
            json.String("atlasTextureName", atlas != null ? atlas.name : null);
            json.Bool("isColor", font.isColor);

            // The template a fresh face material is cloned from, chosen exactly as NowFontCompiler chooses it.
            string template = font.isColor ? "NowUI/TxtMaterialRGBA" : "NowUI/TxtMaterial";
            json.String("materialTemplate", template);
            WarnOnTemplateMismatch(font, template);

            json.Number("dynamicAtlasSize", font.dynamicAtlasSize);
            json.Number("dynamicPixelRange", font.dynamicPixelRange);
            json.Number("dynamicPageSize", font.dynamicPageSize);
            json.Number("dynamicMaxAtlasSize", font.dynamicMaxAtlasSize);
            json.Number("dynamicMaxGlyphSize", font.dynamicMaxGlyphSize);
            json.Number("dynamicMaxAtlasBytes", font.dynamicMaxAtlasBytes);

            // A face carries no fallbacks of its own today; exported so that stays a fact and not an assumption.
            json.BeginArray("fallbacks");
            var fallbacks = font.fallbacks;

            if (fallbacks != null)
            {
                for (int i = 0; i < fallbacks.Count; i++)
                {
                    if (fallbacks[i] != null)
                        json.String(null, fallbacks[i].name);
                }
            }

            json.EndArray();

            // The face's source TTF, written beside this file as raw bytes. This is the whole payload for a dynamic
            // font: with no prebaked atlas in the asset, the glyph table the standalone run measures with is the one
            // NowUI's own compiler bakes from these bytes - the same code and the same input Unity uses.
            json.String("fontBytesFile", fontBytesFile);
            json.Number("fontByteCount", font.GetSourceByteCount());

            // Reserved: an atlas image, and the bytes inline, for a consumer that cannot read the sidecar.
            json.Null("atlasPng");
            json.Null("fontBytesBase64");

            if (font.material != null)
                WriteMaterial(json, "material", null, font.material);
            else
                json.Null("material");

            WriteAtlasInfo(json, "atlasInfo", info);
            json.EndObject();

            written.Add(WriteJson(Path.Combine(fontDirectory, fileName), json));
        }

        /// <summary>
        /// The template is chosen from <c>isColor</c>, the way the compiler chooses it. If the face's own material
        /// runs a different program than that template does, the choice is wrong for this asset and has to be seen.
        /// </summary>
        static void WarnOnTemplateMismatch(NowFont font, string template)
        {
            if (font.material == null || font.material.shader == null)
                return;

            var templateMaterial = Resources.Load<Material>(template);

            if (templateMaterial == null || templateMaterial.shader == null)
            {
                Debug.LogWarning($"Standalone asset export: material template '{template}' did not load.");
                return;
            }

            if (!string.Equals(templateMaterial.shader.name, font.material.shader.name, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"Standalone asset export: font '{font.name}' uses shader '{font.material.shader.name}' but the " +
                    $"template '{template}' it is mapped to uses '{templateMaterial.shader.name}'.");
            }
        }

        /// <summary>
        /// Writes <see cref="NowFontAtlasInfo"/> verbatim. The member names here are the struct's own field names on
        /// purpose: the loader deserializes this object straight into the struct with
        /// <c>System.Text.Json</c> + <c>IncludeFields</c>, so renaming anything below silently produces zeroed metrics.
        /// </summary>
        static void WriteAtlasInfo(JsonWriter json, string memberName, NowFontAtlasInfo info)
        {
            json.BeginObject(memberName);

            json.BeginObject("atlas");
            json.String("type", info.atlas.type);
            json.Number("distanceRange", info.atlas.distanceRange);
            json.Number("size", info.atlas.size);
            json.Number("width", info.atlas.width);
            json.Number("height", info.atlas.height);
            json.String("yOrigin", info.atlas.yOrigin);
            json.EndObject();

            WriteMetrics(json, "metrics", info.metrics);
            WriteGlyphs(json, "glyphs", info.glyphs);
            json.EndObject();
        }

        static void WriteMetrics(JsonWriter json, string memberName, NowFontAtlasInfo.Metrics metrics)
        {
            json.BeginObject(memberName);
            json.Number("emSize", metrics.emSize);
            json.Number("lineHeight", metrics.lineHeight);
            json.Number("ascender", metrics.ascender);
            json.Number("descender", metrics.descender);
            json.Number("underlineY", metrics.underlineY);
            json.Number("underlineThickness", metrics.underlineThickness);
            json.EndObject();
        }

        /// <summary>
        /// Glyph records verbatim - <c>atlasBounds</c> in PIXELS, the way <see cref="NowFontAtlasInfo"/> stores them.
        /// NowFont.BuildGlyphCache divides by the atlas size when it builds its lookup, so anything that normalized
        /// here would be divided twice and every glyph would sample a sliver of the atlas corner.
        /// </summary>
        static void WriteGlyphs(JsonWriter json, string memberName, NowFontAtlasInfo.Glyph[] glyphs)
        {
            json.BeginArray(memberName);
            glyphs = glyphs ?? Array.Empty<NowFontAtlasInfo.Glyph>();

            for (int i = 0; i < glyphs.Length; i++)
            {
                // One glyph per line: a few thousand of these, and a fully expanded form would make the file
                // unreadable and its diffs useless.
                json.BeginObject(null, true);
                json.Number("unicode", glyphs[i].unicode);
                json.Number("advance", glyphs[i].advance);
                WriteBounds(json, "planeBounds", glyphs[i].planeBounds);
                WriteBounds(json, "atlasBounds", glyphs[i].atlasBounds);
                json.EndObject();
            }

            json.EndArray();
        }

        static void WriteBounds(JsonWriter json, string memberName, NowFontAtlasInfo.Bounds bounds)
        {
            json.BeginObject(memberName, true);
            json.Number("left", bounds.left);
            json.Number("bottom", bounds.bottom);
            json.Number("right", bounds.right);
            json.Number("top", bounds.top);
            json.EndObject();
        }

        /// <summary>
        /// One line naming every NowFont in the project and how much baked data it carries. The standalone run can
        /// only measure text from a prebaked glyph table, so which assets have one - and which only carry a TTF to be
        /// baked at runtime - is the single fact that decides whether the font fixture is possible as designed.
        /// </summary>
        static void LogFontInventory()
        {
            var report = new StringBuilder("Standalone asset export: NowFont inventory (glyphs / atlas / font bytes)");

            foreach (string assetPath in FindAssetPaths("t:NowFont"))
            {
                var font = AssetDatabase.LoadAssetAtPath<NowFont>(assetPath);

                if (font == null)
                    continue;

                report.Append("\n  ")
                      .Append(assetPath)
                      .Append("  glyphs=")
                      .Append(GlyphCount(font).ToString(CultureInfo.InvariantCulture))
                      .Append("  atlasType=")
                      .Append(font.atlasInfo.atlas.type ?? "(none)")
                      .Append("  atlasInfo=")
                      .Append(font.atlasInfo.atlas.width.ToString(CultureInfo.InvariantCulture))
                      .Append('x')
                      .Append(font.atlasInfo.atlas.height.ToString(CultureInfo.InvariantCulture))
                      .Append("  texture=")
                      .Append(font.atlas != null
                          ? font.atlas.width + "x" + font.atlas.height
                          : "(null)")
                      .Append("  fontBytes=")
                      .Append(ReadFontByteCount(font).ToString(CultureInfo.InvariantCulture));
            }

            Debug.Log(report.ToString());
        }

        static int GlyphCount(NowFont font)
        {
            NowFontAtlasInfo.Glyph[] glyphs = font.atlasInfo.glyphs;
            return glyphs != null ? glyphs.Length : 0;
        }

        /// <summary>
        /// Size of the face's embedded TTF, read through SerializedObject because the field is private. Only used to
        /// describe the asset in the log - the bytes themselves are not exported in M1.
        /// </summary>
        static int ReadFontByteCount(NowFont font)
        {
            try
            {
                using (var serialized = new SerializedObject(font))
                {
                    SerializedProperty bytes = serialized.FindProperty("_fontBytes");
                    return bytes != null && bytes.isArray ? bytes.arraySize : -1;
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        const string FACE_FILE_SUFFIX = ".font.json";

        /// <summary>Turns "NotoSans-Regular.font.json" back into "NotoSans-Regular".</summary>
        static string StripFaceSuffix(string fileName)
        {
            return fileName.EndsWith(FACE_FILE_SUFFIX, StringComparison.Ordinal)
                ? fileName.Substring(0, fileName.Length - FACE_FILE_SUFFIX.Length)
                : Path.GetFileNameWithoutExtension(fileName);
        }

        static string FaceFileName(NowFont font)
        {
            string assetPath = AssetDatabase.GetAssetPath(font);
            string baseName = string.IsNullOrEmpty(assetPath)
                ? font.name
                : Path.GetFileNameWithoutExtension(assetPath);

            if (string.IsNullOrEmpty(baseName))
                baseName = "Font";

            // The assets are named "NotoSans-Regular.ttf.asset", so one extension is left after the strip above.
            string[] fontExtensions = { ".ttf", ".otf", ".ttc", ".woff", ".woff2" };

            for (int i = 0; i < fontExtensions.Length; i++)
            {
                if (baseName.EndsWith(fontExtensions[i], StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - fontExtensions[i].Length);
                    break;
                }
            }

            return baseName + FACE_FILE_SUFFIX;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------------------------------------------------

        static List<string> FindAssetPaths(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { SEARCH_ROOT });
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var paths = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (!string.IsNullOrEmpty(path) && seen.Add(path))
                    paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        /// <summary>Maps an asset path under a Resources folder to the path <c>Resources.Load</c> takes.</summary>
        static string ToResourcePath(string assetPath)
        {
            const string marker = "/Resources/";
            int index = assetPath.LastIndexOf(marker, StringComparison.Ordinal);

            if (index < 0)
                return null;

            string relative = assetPath.Substring(index + marker.Length);
            int dot = relative.LastIndexOf('.');
            return dot > 0 ? relative.Substring(0, dot) : relative;
        }

        static void RequireAll(string what, string[] required, List<string> found)
        {
            var missing = new List<string>();

            for (int i = 0; i < required.Length; i++)
            {
                if (!found.Contains(required[i]))
                    missing.Add(required[i]);
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The export found no {what} for: {string.Join(", ", missing)}. The standalone resource provider " +
                    "serves these by name, and a missing one turns into a null Resources.Load and a failed test with " +
                    "an unrelated message.");
            }
        }

        static void SortByKey<T>(List<string> keys, List<T> values)
        {
            SortByKey(keys, values, null);
        }

        /// <summary>Sorts up to three parallel lists by the ordinal order of <paramref name="keys"/>.</summary>
        static void SortByKey<T>(List<string> keys, List<T> values, List<string> extra)
        {
            var order = new List<int>(keys.Count);

            for (int i = 0; i < keys.Count; i++)
                order.Add(i);

            order.Sort((a, b) =>
            {
                int comparison = string.CompareOrdinal(keys[a], keys[b]);
                return comparison != 0 ? comparison : a.CompareTo(b);
            });

            var sortedKeys = new List<string>(keys.Count);
            var sortedValues = new List<T>(values.Count);
            var sortedExtra = extra != null ? new List<string>(extra.Count) : null;

            for (int i = 0; i < order.Count; i++)
            {
                sortedKeys.Add(keys[order[i]]);
                sortedValues.Add(values[order[i]]);

                if (sortedExtra != null)
                    sortedExtra.Add(extra[order[i]]);
            }

            keys.Clear();
            keys.AddRange(sortedKeys);
            values.Clear();
            values.AddRange(sortedValues);

            if (extra != null)
            {
                extra.Clear();
                extra.AddRange(sortedExtra);
            }
        }

        static string ReadTextOrEmpty(string assetPath)
        {
            try
            {
                string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
                return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        static string Relative(string root, string path)
        {
            string normalizedRoot = root.Replace('\\', '/').TrimEnd('/') + "/";
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(normalizedRoot.Length)
                : normalized;
        }

        /// <summary>
        /// Writes one document. NaN and infinity have no JSON spelling, so the writer substitutes 0 for them; a
        /// non-zero count means an asset holds a float no fixture should carry, and it is reported rather than shipped
        /// as a plausible-looking zero.
        /// </summary>
        static string WriteJson(string path, JsonWriter json)
        {
            if (json.nonFiniteCount > 0)
            {
                Debug.LogWarning(
                    $"Standalone asset export: {json.nonFiniteCount} non-finite float(s) in " +
                    $"'{Path.GetFileName(path)}' were written as 0. The source asset holds NaN or infinity.");
            }

            return WriteFile(path, json.ToString());
        }

        /// <summary>Writes UTF-8 without a BOM and with LF endings, so the checked-in fixtures are platform-neutral.</summary>
        static string WriteFile(string path, string text)
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text, new UTF8Encoding(false));
            return full;
        }

        // ---------------------------------------------------------------------------------------------------------
        // A small JSON writer
        //
        // Hand-rolled rather than JsonUtility because two things matter here that JsonUtility does not give: exact
        // control over float formatting (round-trip "G9", so a glyph advance that reaches the standalone run is the
        // same float Unity measured with) and a compact mode, without which a few thousand glyph records would expand
        // into a file nobody can read or diff.
        // ---------------------------------------------------------------------------------------------------------

        sealed class JsonWriter
        {
            readonly StringBuilder _text = new StringBuilder(1 << 16);
            readonly List<bool> _empty = new List<bool>();
            readonly List<bool> _compact = new List<bool>();

            int _nonFinite;

            public JsonWriter()
            {
                // A synthetic root scope, so the first value needs no special case.
                _empty.Add(true);
                _compact.Add(false);
            }

            /// <summary>Number of NaN or infinite floats written as 0. Non-zero means the source data is suspect.</summary>
            public int nonFiniteCount => _nonFinite;

            public void BeginObject(string name = null, bool compact = false)
            {
                Open(name, '{', compact);
            }

            public void EndObject()
            {
                Close('}');
            }

            public void BeginArray(string name = null, bool compact = false)
            {
                Open(name, '[', compact);
            }

            public void EndArray()
            {
                Close(']');
            }

            public void String(string name, string value)
            {
                Prefix(name);

                if (value == null)
                    _text.Append("null");
                else
                    AppendString(value);
            }

            public void Number(string name, int value)
            {
                Prefix(name);
                _text.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            public void Number(string name, float value)
            {
                Prefix(name);

                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    _nonFinite++;
                    _text.Append('0');
                    return;
                }

                _text.Append(value.ToString("G9", CultureInfo.InvariantCulture));
            }

            public void Bool(string name, bool value)
            {
                Prefix(name);
                _text.Append(value ? "true" : "false");
            }

            public void Null(string name)
            {
                Prefix(name);
                _text.Append("null");
            }

            public void Vector(string name, Vector4 value)
            {
                BeginArray(name, true);
                Number(null, value.x);
                Number(null, value.y);
                Number(null, value.z);
                Number(null, value.w);
                EndArray();
            }

            public void Vector(string name, Vector2 value)
            {
                BeginArray(name, true);
                Number(null, value.x);
                Number(null, value.y);
                EndArray();
            }

            public override string ToString()
            {
                // A trailing newline, without mutating the buffer: ToString stays repeatable.
                return _text.ToString() + "\n";
            }

            void Open(string name, char brace, bool compact)
            {
                bool inherited = _compact[_compact.Count - 1];
                Prefix(name);
                _text.Append(brace);
                _empty.Add(true);
                _compact.Add(compact || inherited);
            }

            void Close(char brace)
            {
                int top = _empty.Count - 1;
                bool wasEmpty = _empty[top];
                bool wasCompact = _compact[top];
                _empty.RemoveAt(top);
                _compact.RemoveAt(top);

                if (!wasEmpty && !wasCompact)
                {
                    _text.Append('\n');
                    AppendIndent();
                }

                _text.Append(brace);
            }

            void Prefix(string name)
            {
                int top = _empty.Count - 1;

                if (!_empty[top])
                    _text.Append(',');

                bool wasEmpty = _empty[top];
                _empty[top] = false;

                if (_compact[top])
                {
                    if (!wasEmpty)
                        _text.Append(' ');
                }
                else if (_text.Length > 0)
                {
                    _text.Append('\n');
                    AppendIndent();
                }

                if (name != null)
                {
                    AppendString(name);
                    _text.Append(": ");
                }
            }

            void AppendIndent()
            {
                _text.Append(' ', 2 * (_empty.Count - 1));
            }

            void AppendString(string value)
            {
                _text.Append('"');

                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];

                    switch (c)
                    {
                        case '"':
                            _text.Append("\\\"");
                            break;

                        case '\\':
                            _text.Append("\\\\");
                            break;

                        case '\b':
                            _text.Append("\\b");
                            break;

                        case '\f':
                            _text.Append("\\f");
                            break;

                        case '\n':
                            _text.Append("\\n");
                            break;

                        case '\r':
                            _text.Append("\\r");
                            break;

                        case '\t':
                            _text.Append("\\t");
                            break;

                        default:
                            if (c < ' ')
                                _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                _text.Append(c);

                            break;
                    }
                }

                _text.Append('"');
            }
        }
    }
}
