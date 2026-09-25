using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NowUI;
using NowUI.Engine;
using UnityEngine;

namespace NowUI.Hosting
{
    /// <summary>Loads NowUI's built-in fonts, materials and shader descriptions from an exported resource directory.</summary>
    public class NowFileResources : INowResourceProvider, IDisposable
    {
        /// <summary>The resource path <c>Now.defaultFont</c> loads. Public so a self-test can name it once.</summary>
        public const string fontFamilyPath = "NowUI/NotoSans";

        /// <summary>
        /// The material templates the core reaches through <c>Resources.Load</c> outside the UGUI paths, which are
        /// compiled out of the standalone build. Every one of these must resolve or a draw that needs it logs an error
        /// and fails its test; the constructor checks the list rather than letting that surface later, somewhere else.
        /// </summary>
        private static readonly string[] k_RequiredMaterialPaths =
        {
            "NowUI/UIMaterial",         // Now._defaultMaterial
            "NowUI/TxtMaterial",        // text, and every dynamic font page
            "NowUI/TxtMaterialRGBA",    // colour (bitmap) faces
            "NowUI/GradientMaterial",   // NowGradient
            "NowUI/GlassMaterial",      // NowGlass
            "NowUI/GlassBlurMaterial",  // NowGlass blur pass
            "NowUI/RippleMaterial",     // NowRipple
            "NowUI/BezierMaterial",     // NowLine, node-graph links
            "NowUI/SdfMaterial",        // NowSdf
        };

        /// <summary>
        /// Shader names the core and the extensions reach through <c>Shader.Find</c>. Checked for the same reason as
        /// the materials above: a null here is a silently wrong branch, not an exception.
        /// </summary>
        private static readonly string[] k_RequiredShaderNames =
        {
            "NowUI/UI Bezier",              // NowLine
            "NowUI/UI Ripple",              // NowRipple
            "NowUI/Color Picker",           // NowValueControls
            "NowUI/SDF Scene",              // NowSdf
            "Hidden/NowUI/SDF Image Field", // NowSdfImageField
        };

        private readonly string m_ResourceRoot;

        private readonly Dictionary<string, UnityEngine.Object> m_Objects =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

        private readonly Dictionary<string, Shader> m_Shaders =
            new Dictionary<string, Shader>(StringComparer.Ordinal);

        private readonly List<UnityEngine.Object> m_Owned = new List<UnityEngine.Object>();
        private bool m_Disposed;

        /// <summary>The resource directory copied alongside applications that reference this project.</summary>
        public static string bundledRoot => Path.Combine(AppContext.BaseDirectory, "NowUI", "Resources");

        /// <summary>Loads the bundled resources independently of the current working directory.</summary>
        public NowFileResources() : this(bundledRoot)
        {
        }

        /// <summary>Loads the exported shader, material and font files under <paramref name="root"/>.</summary>
        public NowFileResources(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("A resource root is required.", nameof(root));

            resourceRoot = Path.GetFullPath(root);
            m_ResourceRoot = resourceRoot;
            try
            {
                LoadShaders();
                LoadMaterials();
                RequireResources();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>The absolute directory containing the exported resource files.</summary>
        public string resourceRoot { get; }

        /// <summary>The material template paths a test can expect to resolve, in fixture order.</summary>
        public static IReadOnlyList<string> requiredMaterialPaths
        {
            get { return k_RequiredMaterialPaths; }
        }

        /// <summary>The shader names a test can expect <see cref="FindShader"/> to answer, in fixture order.</summary>
        public static IReadOnlyList<string> requiredShaderNames
        {
            get { return k_RequiredShaderNames; }
        }

        /// <inheritdoc />
        public UnityEngine.Object Load(string path, Type type)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(path))
                return null;

            // Glass prepares its optional Canvas material even for a native mesh.
            // Native hosts have no Canvas/stencil path, so both slots use the stock glass program.
            if (string.Equals(path, "NowUI/GlassMaterialUGUI", StringComparison.Ordinal))
                path = "NowUI/GlassMaterial";

            UnityEngine.Object result;
            bool known = m_Objects.TryGetValue(path, out result);

            // `result == null` is UnityEngine.Object's fake-null: it is also true for an instance a test destroyed.
            // Unity would reload such an asset from disk on the next Resources.Load, and the font family is the one
            // asset here a test can plausibly reach (Now.defaultFont is a global), so it is rebuilt on demand. The
            // materials are not: nothing in the subset destroys a template, and returning the destroyed instance makes
            // that (impossible) case fail visibly instead of being papered over.
            if (!known || result == null)
            {
                if (!string.Equals(path, fontFamilyPath, StringComparison.Ordinal))
                    return known ? result : null;

                result = BuildFontFamily();
                m_Objects[path] = result;
            }

            // Unity's Resources.Load<T> returns null when the asset is not of the requested type; matching that keeps
            // a mis-typed load looking the same in both runs.
            if (type != null && !type.IsInstanceOfType(result))
                return null;

            return result;
        }

        /// <inheritdoc />
        public Shader FindShader(string name)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(name))
                return null;

            Shader shader;
            return m_Shaders.TryGetValue(name, out shader) ? shader : null;
        }

        /// <summary>Registers a caller-owned object under a resource path, replacing its current lookup value.</summary>
        public void Register(string path, UnityEngine.Object asset)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("A resource path is required.", nameof(path));

            m_Objects[path] = asset;
        }

        /// <summary>Looks up a template material by resource path; null when the fixtures do not carry one.</summary>
        public Material GetMaterial(string path)
        {
            return Load(path, typeof(Material)) as Material;
        }

        /// <summary>
        /// Destroys the shaders, materials and font faces created by this provider. Objects passed to
        /// <see cref="Register"/> remain caller-owned. Dispose before shutting down the rendering backend.
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;

            for (int i = m_Owned.Count - 1; i >= 0; --i)
                if (m_Owned[i] != null)
                    UnityEngine.Object.DestroyImmediate(m_Owned[i]);
            m_Owned.Clear();
            m_Objects.Clear();
            m_Shaders.Clear();
        }

        private T Own<T>(T asset) where T : UnityEngine.Object
        {
            m_Owned.Add(asset);
            return asset;
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
                throw new ObjectDisposedException(nameof(NowFileResources));
        }

        private string FixturePath(string part)
        {
            return Path.Combine(m_ResourceRoot, part);
        }

        private string FixturePath(string folder, string part)
        {
            return Path.Combine(m_ResourceRoot, folder, part);
        }

        private static JsonDocument ReadJson(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Missing standalone resource file '" + path + "'. Re-run NowStandaloneAssetExport.Export.", path);
            }

            return JsonDocument.Parse(File.ReadAllBytes(path));
        }

        // -------------------------------------------------------------------------------------------------------
        // Shaders and materials
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds one shim <see cref="Shader"/> per exported program, carrying its declared uniforms and their
        /// defaults. The declarations are the point: <c>Material.HasProperty</c> answers from them for a property
        /// nobody has assigned, which is what Unity does and what NowUI gates optional features on.
        /// </summary>
        private void LoadShaders()
        {
            using (JsonDocument document = ReadJson(FixturePath("shaders.json")))
            {
                foreach (JsonElement entry in document.RootElement.GetProperty("shaders").EnumerateArray())
                {
                    string name = entry.GetProperty("name").GetString();
                    NowShaderInfo info = new NowShaderInfo(name, entry.GetProperty("passCount").GetInt32());

                    foreach (JsonElement property in entry.GetProperty("properties").EnumerateArray())
                    {
                        string propertyName = property.GetProperty("name").GetString();
                        string propertyType = property.GetProperty("type").GetString();

                        switch (propertyType)
                        {
                            case "Float":
                            case "Range":
                                info.DeclareFloat(propertyName, ReadFloat(property, "defaultFloat"));
                                break;
                            case "Color":
                            case "Vector":
                                info.DeclareVector(propertyName, ReadVector4(property, "defaultVector"));
                                break;
                            case "Texture":
                                info.DeclareTexture(propertyName);
                                break;
                            default:
                                // An unknown property type would silently drop a declaration, and a dropped
                                // declaration is a HasProperty that answers false where Unity answers true.
                                throw new NotSupportedException(
                                    "Shader '" + name + "' declares property '" + propertyName +
                                    "' of unhandled type '" + propertyType + "'.");
                        }
                    }

                    info.keywords = ReadStringArray(entry, "keywords");
                    m_Shaders[name] = Own(new Shader(name, info));
                }
            }
        }

        /// <summary>
        /// Builds one shim <see cref="Material"/> per exported template. <c>new Material(shader)</c> seeds the
        /// shader's declared defaults, exactly as Unity does, and the values the .mat stores are written over them.
        /// </summary>
        private void LoadMaterials()
        {
            using (JsonDocument document = ReadJson(FixturePath("materials.json")))
            {
                foreach (JsonElement entry in document.RootElement.GetProperty("materials").EnumerateArray())
                {
                    string resourcePath = entry.GetProperty("resourcePath").GetString();
                    string shaderName = entry.GetProperty("shader").GetString();

                    Shader shader;
                    if (!m_Shaders.TryGetValue(shaderName, out shader))
                    {
                        throw new InvalidOperationException(
                            "Material '" + resourcePath + "' uses shader '" + shaderName +
                            "', which shaders.json does not declare. The two fixtures are out of step; re-export.");
                    }

                    Material material = Own(new Material(shader));
                    material.name = entry.GetProperty("name").GetString();
                    material.hideFlags = HideFlags.HideAndDontSave;
                    material.renderQueue = entry.GetProperty("renderQueue").GetInt32();

                    foreach (JsonElement value in entry.GetProperty("floats").EnumerateArray())
                        material.SetFloat(value.GetProperty("name").GetString(), ReadFloat(value, "value"));

                    foreach (JsonElement value in entry.GetProperty("vectors").EnumerateArray())
                        material.SetVector(value.GetProperty("name").GetString(), ReadVector4(value, "value"));

                    foreach (JsonElement value in entry.GetProperty("colors").EnumerateArray())
                    {
                        Vector4 rgba = ReadVector4(value, "value");
                        material.SetColor(
                            value.GetProperty("name").GetString(),
                            new Color(rgba.x, rgba.y, rgba.z, rgba.w));
                    }

                    // Texture slots are deliberately left alone: every sampler in every exported template is
                    // unassigned, and an unassigned sampler reads as null here exactly as it does in Unity. A
                    // template that ever ships with a texture bound would need that texture exported too, so it is
                    // refused rather than quietly dropped.
                    foreach (JsonElement value in entry.GetProperty("textures").EnumerateArray())
                    {
                        if (value.GetProperty("texture").ValueKind != JsonValueKind.Null)
                        {
                            throw new NotSupportedException(
                                "Material '" + resourcePath + "' binds a texture to '" +
                                value.GetProperty("name").GetString() +
                                "'. The fixtures carry no texture payloads; see NowStandaloneAssetExport.");
                        }
                    }

                    string[] keywords = ReadStringArray(entry, "keywords");
                    for (int i = 0; i < keywords.Length; i++)
                        material.EnableKeyword(keywords[i]);

                    m_Objects[resourcePath] = material;
                }
            }
        }

        /// <summary>Fails construction when a resource the core requires is not in the fixtures.</summary>
        private void RequireResources()
        {
            for (int i = 0; i < k_RequiredMaterialPaths.Length; i++)
            {
                if (!m_Objects.ContainsKey(k_RequiredMaterialPaths[i]))
                {
                    throw new InvalidOperationException(
                        "materials.json (" + m_ResourceRoot + ") is missing the required template '" +
                        k_RequiredMaterialPaths[i] + "'. Re-run NowStandaloneAssetExport.Export.");
                }
            }

            for (int i = 0; i < k_RequiredShaderNames.Length; i++)
            {
                if (!m_Shaders.ContainsKey(k_RequiredShaderNames[i]))
                {
                    throw new InvalidOperationException(
                        "shaders.json (" + m_ResourceRoot + ") is missing the required program '" +
                        k_RequiredShaderNames[i] + "'. Re-run NowStandaloneAssetExport.Export.");
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // The font family
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds the NotoSans family: four faces from their exported TTFs, bound into a
        /// <see cref="NowFontFamily"/>. Built on first use rather than in the constructor, so it is built after
        /// NowRuntime.Initialize has installed the host and the backend, and so a run that never touches text never
        /// reads 2.5 MB of font.
        /// </summary>
        private NowFontFamily BuildFontFamily()
        {
            using (JsonDocument document = ReadJson(FixturePath("NowUI", "NotoSans.family.json")))
            {
                JsonElement root = document.RootElement;
                JsonElement faces = root.GetProperty("faces");

                NowFontFamily family = Own(ScriptableObject.CreateInstance<NowFontFamily>());
                family.name = root.GetProperty("name").GetString();
                family.hideFlags = HideFlags.HideAndDontSave;

                RegularField(family) = BuildFace(faces, "regular");
                BoldField(family) = BuildFace(faces, "bold");
                ItalicField(family) = BuildFace(faces, "italic");
                BoldItalicField(family) = BuildFace(faces, "boldItalic");

                // The exporter omits the CJK/Arabic/emoji/icon fallback families (they are 5-10 MB each and no gate
                // test misses a glyph, so traversal never reaches them). An empty array, not null, so the family
                // reports the same shape a Unity asset with no fallbacks configured would.
                FallbacksField(family) = RequireNoFallbacks(root, "NotoSans.family.json");

                return family;
            }
        }

        /// <summary>Builds one face from the file named in the family's <c>faces</c> object.</summary>
        private NowFont BuildFace(JsonElement faces, string slot)
        {
            JsonElement fileNameElement;
            if (!faces.TryGetProperty(slot, out fileNameElement) || fileNameElement.ValueKind == JsonValueKind.Null)
                return null;

            string fileName = fileNameElement.GetString();

            using (JsonDocument document = ReadJson(FixturePath("NowUI", fileName)))
            {
                JsonElement face = document.RootElement;

                // Refused, not guessed. Design H.7 and test plan 3.3 describe a prebaked face (atlas pixels plus a
                // glyph table); no asset in this project is one, so there is no exercised code here to build one
                // with. A fixture that ever carries one changes what the tests measure, and that has to be a
                // deliberate change to this method rather than a silent half-build.
                if (!string.Equals(face.GetProperty("kind").GetString(), "dynamic", StringComparison.Ordinal) ||
                    face.GetProperty("atlasWidth").GetInt32() != 0 ||
                    face.GetProperty("atlasInfo").GetProperty("glyphs").GetArrayLength() != 0)
                {
                    throw new NotSupportedException(
                        "Font fixture '" + fileName + "' carries a prebaked atlas. This provider only builds the " +
                        "dynamic faces NowUI actually ships; see NowFileResources.BuildFace.");
                }

                JsonElement bytesElement = face.GetProperty("fontBytesFile");
                if (bytesElement.ValueKind == JsonValueKind.Null)
                {
                    throw new InvalidOperationException(
                        "Font fixture '" + fileName + "' has no source bytes, so nothing can bake from it.");
                }

                byte[] fontBytes = File.ReadAllBytes(FixturePath("NowUI", bytesElement.GetString()));
                int expectedCount = face.GetProperty("fontByteCount").GetInt32();

                if (fontBytes.Length != expectedCount)
                {
                    throw new InvalidOperationException(
                        "Font fixture '" + fileName + "' declares " + expectedCount + " source bytes but the file " +
                        "holds " + fontBytes.Length + ". The fixtures are inconsistent; re-export.");
                }

                // The same call the runtime makes for a .ttf.asset (NowFontCompiler.cs:731), with the same argument
                // shape. The material template is deliberately left null: on a Unity asset _dynamicMaterialTemplate
                // is a non-serialized field, so it is null there too, and the font loads "NowUI/TxtMaterial" through
                // this very provider when it bakes its first page (NowFont.cs:3618). Passing one here would take a
                // branch the editor run does not take.
                NowFont font;
                string error;
                if (!NowFontCompiler.TryCompile(
                        fontBytes,
                        face.GetProperty("dynamicAtlasSize").GetInt32(),
                        face.GetProperty("dynamicPixelRange").GetInt32(),
                        out font,
                        out error))
                {
                    throw new InvalidOperationException(
                        "NowFontCompiler rejected the '" + fileName + "' fixture: " + error);
                }

                Own(font);
                font.name = face.GetProperty("name").GetString();
                font.hideFlags = HideFlags.HideAndDontSave;

                // TryCompile seeds the two page limits with the runtime defaults; the fixture carries what the asset
                // was actually authored with, which is what the editor run used.
                font.dynamicPageSize = face.GetProperty("dynamicPageSize").GetInt32();
                font.dynamicMaxAtlasSize = face.GetProperty("dynamicMaxAtlasSize").GetInt32();

                // Exports that predate resolution tiers keep the runtime default.
                if (face.TryGetProperty("dynamicMaxGlyphSize", out var maxGlyphSize))
                    font.dynamicMaxGlyphSize = maxGlyphSize.GetInt32();
                font.dynamicMaxAtlasBytes = face.GetProperty("dynamicMaxAtlasBytes").GetInt32();

                FallbacksField(font) = RequireNoFallbacks(face, fileName);

                return font;
            }
        }

        /// <summary>
        /// Reads a fixture's <c>fallbacks</c> list, which the exporter writes empty. A populated one would name
        /// families whose TTFs are not in the fixtures, so it is refused rather than half-resolved.
        /// </summary>
        private static NowFontAsset[] RequireNoFallbacks(JsonElement element, string fixtureName)
        {
            if (element.GetProperty("fallbacks").GetArrayLength() != 0)
            {
                throw new NotSupportedException(
                    "Font fixture '" + fixtureName + "' names fallback assets, which the fixtures do not carry. " +
                    "See NowStandaloneAssetExport (\"omittedFallbacks\").");
            }

            return Array.Empty<NowFontAsset>();
        }

        // Unity hydrates these private serialized slots. This .NET-only host uses
        // typed field accessors so trimming/AOT sees each exact field dependency,
        // without retaining runtime reflection for the built-in fixture schema.
        // Binding checks the declaring type, name and field type; a mismatched
        // runtime fails when the accessor is compiled or first called.
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regular")]
        private static extern ref NowFont RegularField(NowFontFamily family);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bold")]
        private static extern ref NowFont BoldField(NowFontFamily family);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_italic")]
        private static extern ref NowFont ItalicField(NowFontFamily family);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_boldItalic")]
        private static extern ref NowFont BoldItalicField(NowFontFamily family);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fallbacks")]
        private static extern ref NowFontAsset[] FallbacksField(NowFontAsset asset);

        // -------------------------------------------------------------------------------------------------------
        // JSON helpers
        // -------------------------------------------------------------------------------------------------------

        private static float ReadFloat(JsonElement element, string name)
        {
            return element.GetProperty(name).GetSingle();
        }

        private static Vector4 ReadVector4(JsonElement element, string name)
        {
            JsonElement array = element.GetProperty(name);

            return new Vector4(
                array[0].GetSingle(),
                array[1].GetSingle(),
                array[2].GetSingle(),
                array[3].GetSingle());
        }

        private static string[] ReadStringArray(JsonElement element, string name)
        {
            JsonElement array = element.GetProperty(name);
            int count = array.GetArrayLength();

            if (count == 0)
                return Array.Empty<string>();

            string[] values = new string[count];
            for (int i = 0; i < count; i++)
                values[i] = array[i].GetString();

            return values;
        }
    }
}
