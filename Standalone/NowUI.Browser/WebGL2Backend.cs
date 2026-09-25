// WebGL2 implementation of the shared INowRenderBackend. C# owns rendering decisions;
// wwwroot/nowui-gl.js contains only browser graphics interop. Shader source is shared with Desktop.
using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using NowUI;
using NowUI.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI.Browser
{
    public sealed partial class WebGL2Backend : INowRenderBackend
    {

        const string RectangleShader = "NowUI/UI Rectangle";
        const string TextShader = "NowUI/Text Renderer";
        const string GradientShader = "NowUI/UI Gradient";
        const string RippleShader = "NowUI/UI Ripple";
        const string GlassShader = "NowUI/UI Glass";
        const string ColorPickerShader = "NowUI/Color Picker";
        const string BezierShader = "NowUI/UI Bezier";
        const string GlassBlurShader = "Hidden/NowUI/GlassBlur";
        const string SdfShader = "NowUI/SDF Scene";
        const string SdfImageFieldShader = "Hidden/NowUI/SDF Image Field";
        private bool m_ReportedGlassBackdropFlag;
        internal static int traceFrames;

        private const string NewLine = "\n";

        private static readonly System.Text.StringBuilder s_Trace = new System.Text.StringBuilder(4096);

        private static void Trace(string line)
        {
            if (traceFrames <= 0)
                return;

            s_Trace.Append(line).Append(NewLine);
        }
        const string PortedShaderList =
            "'" + RectangleShader + "', '" + TextShader + "', '" + GradientShader + "', '" + RippleShader +
            "', '" + GlassShader + "', '" + GlassBlurShader + "', '" + ColorPickerShader + "', '" +
            BezierShader + "', '" + SdfShader + "' and '" + SdfImageFieldShader + "'";

        static bool IsPortedShader(string name)
        {
            return name == RectangleShader
                || name == TextShader
                || name == GradientShader
                || name == RippleShader
                || name == GlassShader
                || name == ColorPickerShader
                || name == BezierShader
                || name == GlassBlurShader
                || name == SdfShader
                || name == SdfImageFieldShader;
        }

        internal static class UniformSlots
        {
            public const int Mvp = 0;                     // mat4, column major
            public const int MainTexST = 16;              // vec4
            public const int PremultipliedTexture = 20;   // float
            public const int TextSdfEncoding = 21;        // float
            public const int MaskCount = 22;              // float
            public const int TextureMaskCount = 23;       // float
            public const int MaskRects = 24;              // vec4[8]
            public const int MaskData = 56;               // vec4[8]
            public const int MaskParams = 88;             // vec4[8]
            public const int MaskTransforms = 120;        // vec4[8]
            public const int TextureMaskRects = 152;      // vec4[2]
            public const int TextureMaskParams = 160;     // vec4[2]
            public const int TextureMaskTransforms = 168; // vec4[2]
            public const int GradientRampTexelSize = 176; // vec4, NowUI/UI Gradient only
            public const int ColorPickerMode = 180;         // float, NowUI/Color Picker only
            public const int GlassUseBackdrop = 181;        // float, NowUI/UI Glass
            public const int GlassUseStereoBackdrop = 182;  // float, NowUI/UI Glass
            public const int GlassMaterialMode = 183;       // float, NowUI/UI Glass
            public const int BackdropUvTransform = 184;     // vec4,  NowUI/UI Glass
            public const int BlurTexelSize = 188;           // vec4,  Hidden/NowUI/GlassBlur
            public const int BlurSourceScaleOffset = 192;   // vec4,  Hidden/NowUI/GlassBlur
            public const int BlurDirection = 196;           // vec2,  Hidden/NowUI/GlassBlur
            public const int SourceUv = 198;                // vec4,  Hidden/NowUI/SDF Image Field
            public const int FieldParams = 202;             // vec4,  Hidden/NowUI/SDF Image Field
            public const int FieldTexels = 206;             // vec4,  Hidden/NowUI/SDF Image Field
            public const int StampRect = 210;               // vec4,  Hidden/NowUI/SDF Image Field
            public const int Step = 214;                    // float, Hidden/NowUI/SDF Image Field
            public const int Count = 215;
        }
        internal static class SdfUniformSlots
        {
            public const int Data0 = 0;                  // vec4[64]
            public const int Data1 = 256;                // vec4[64]
            public const int Data2 = 512;                // vec4[64]
            public const int ShapeMeta = 768;            // vec4[64]
            public const int Colors = 1024;              // vec4[64]
            public const int Uvs = 1280;                 // vec4[64]
            public const int ImageUvs = 1536;            // vec4[64]
            public const int LayerData0 = 1792;          // vec4[16]
            public const int LayerData1 = 1856;          // vec4[16]
            public const int ImageAtlasSize = 1920;      // vec4
            public const int Outline = 1924;             // vec4
            public const int OutlineColor = 1928;        // vec4
            public const int Glow = 1932;                // vec4
            public const int GlowColor = 1936;           // vec4
            public const int Shadow = 1940;              // vec4
            public const int ShadowColor = 1944;         // vec4
            public const int InnerShadow = 1948;         // vec4
            public const int InnerShadowColor = 1952;    // vec4
            public const int Emboss = 1956;              // vec4
            public const int Contour = 1960;             // vec4
            public const int ContourColor = 1964;        // vec4
            public const int ContourMask = 1968;         // vec4
            public const int Warp = 1972;                // vec4
            public const int ShapeCount = 1976;          // float
            public const int LayerCount = 1977;          // float
            public const int Feather = 1978;             // float
            public const int TextEffectLimit = 1979;     // float
            public const int MaskOutput = 1980;          // float
            public const int CanvasLayout = 1981;        // float
            public const int Time = 1982;                // float
            public const int Shadow2 = 1983;             // vec4
            public const int Shadow2Color = 1987;        // vec4
            public const int Count = 1991;
        }
        const int SdfShapeCapacity = 64;
        const int SdfLayerCapacity = 16;
        const float SdfPortedAbiVersion = 2f;
        const int AnalyticMaskCapacity = 8;
        const int TextureMaskCapacity = 2;

        static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
        static readonly int IdPremultipliedTexture = Shader.PropertyToID("_NowPremultipliedTexture");
        static readonly int IdTextSdfEncoding = Shader.PropertyToID("_NowUITextSdfEncoding");
        static readonly int IdGradientRampTexelSize = Shader.PropertyToID("_NowGradientRampTexelSize");
        static readonly int IdGradientRampTexture = Shader.PropertyToID("_NowGradientRampTexture");
        static readonly int IdColorPickerMode = Shader.PropertyToID("_Mode");
        static readonly int IdGlassUseBackdrop = Shader.PropertyToID("_NowGlassUseBackdrop");
        static readonly int IdGlassUseStereoBackdrop = Shader.PropertyToID("_NowGlassUseStereoBackdrop");
        static readonly int IdGlassMaterialMode = Shader.PropertyToID("_NowMaterialGlassMode");
        static readonly int IdBackdropUvTransform = Shader.PropertyToID("_NowBackdropUVTransform");
        static readonly int IdBlurTexelSize = Shader.PropertyToID("_NowBlurTexelSize");
        static readonly int IdBlurSourceScaleOffset = Shader.PropertyToID("_NowBlurSourceScaleOffset");
        static readonly int IdBlurDirection = Shader.PropertyToID("_NowBlurDirection");
        static readonly int IdMaskCount = Shader.PropertyToID("_NowUIMaskCount");
        static readonly int IdMaskRects = Shader.PropertyToID("_NowUIMaskRects");
        static readonly int IdMaskData = Shader.PropertyToID("_NowUIMaskData");
        static readonly int IdMaskParams = Shader.PropertyToID("_NowUIMaskParams");
        static readonly int IdMaskTransforms = Shader.PropertyToID("_NowUIMaskTransforms");
        static readonly int IdTextureMaskCount = Shader.PropertyToID("_NowUITextureMaskCount");
        static readonly int IdTextureMask0 = Shader.PropertyToID("_NowUITextureMask0");
        static readonly int IdTextureMask1 = Shader.PropertyToID("_NowUITextureMask1");
        static readonly int IdTextureMaskRects = Shader.PropertyToID("_NowUITextureMaskRects");
        static readonly int IdTextureMaskParams = Shader.PropertyToID("_NowUITextureMaskParams");
        static readonly int IdTextureMaskTransforms = Shader.PropertyToID("_NowUITextureMaskTransforms");
        static readonly int IdSdfAbiVersion = Shader.PropertyToID("_NowSdfAbiVersion");
        static readonly int IdSdfCanvasLayout = Shader.PropertyToID("_NowCanvasLayout");
        static readonly int IdSdfShapeCount = Shader.PropertyToID("_SdfShapeCount");
        static readonly int IdSdfLayerCount = Shader.PropertyToID("_SdfLayerCount");
        static readonly int IdSdfFeather = Shader.PropertyToID("_SdfFeather");
        static readonly int IdSdfTextEffectLimit = Shader.PropertyToID("_SdfTextEffectLimit");
        static readonly int IdSdfMaskOutput = Shader.PropertyToID("_SdfMaskOutput");
        static readonly int IdSdfImageField = Shader.PropertyToID("_SdfImageField");
        static readonly int IdSdfImageColor = Shader.PropertyToID("_SdfImageColor");
        static readonly int IdSdfImageAtlasSize = Shader.PropertyToID("_SdfImageAtlasSize");
        static readonly int IdSdfData0 = Shader.PropertyToID("_SdfData0");
        static readonly int IdSdfData1 = Shader.PropertyToID("_SdfData1");
        static readonly int IdSdfData2 = Shader.PropertyToID("_SdfData2");
        static readonly int IdSdfShapeMeta = Shader.PropertyToID("_SdfShapeMeta");
        static readonly int IdSdfColors = Shader.PropertyToID("_SdfColors");
        static readonly int IdSdfUvs = Shader.PropertyToID("_SdfUvs");
        static readonly int IdSdfImageUvs = Shader.PropertyToID("_SdfImageUvs");
        static readonly int IdSdfLayerData0 = Shader.PropertyToID("_SdfLayerData0");
        static readonly int IdSdfLayerData1 = Shader.PropertyToID("_SdfLayerData1");
        static readonly int IdSdfOutline = Shader.PropertyToID("_SdfOutline");
        static readonly int IdSdfOutlineColor = Shader.PropertyToID("_SdfOutlineColor");
        static readonly int IdSdfGlow = Shader.PropertyToID("_SdfGlow");
        static readonly int IdSdfGlowColor = Shader.PropertyToID("_SdfGlowColor");
        static readonly int IdSdfShadow = Shader.PropertyToID("_SdfShadow");
        static readonly int IdSdfShadowColor = Shader.PropertyToID("_SdfShadowColor");
        static readonly int IdSdfShadow2 = Shader.PropertyToID("_SdfShadow2");
        static readonly int IdSdfShadow2Color = Shader.PropertyToID("_SdfShadow2Color");
        static readonly int IdSdfInnerShadow = Shader.PropertyToID("_SdfInnerShadow");
        static readonly int IdSdfInnerShadowColor = Shader.PropertyToID("_SdfInnerShadowColor");
        static readonly int IdSdfEmboss = Shader.PropertyToID("_SdfEmboss");
        static readonly int IdSdfContour = Shader.PropertyToID("_SdfContour");
        static readonly int IdSdfContourColor = Shader.PropertyToID("_SdfContourColor");
        static readonly int IdSdfContourMask = Shader.PropertyToID("_SdfContourMask");
        static readonly int IdSdfWarp = Shader.PropertyToID("_SdfWarp");
        static readonly int IdSourceTex = Shader.PropertyToID("_SourceTex");
        static readonly int IdSourceUv = Shader.PropertyToID("_SourceUv");
        static readonly int IdFieldParams = Shader.PropertyToID("_FieldParams");
        static readonly int IdFieldTexels = Shader.PropertyToID("_FieldTexels");
        static readonly int IdStampRect = Shader.PropertyToID("_StampRect");
        static readonly int IdStep = Shader.PropertyToID("_Step");

        NowRenderCaps m_Caps;
        bool m_Initialized;
        bool m_InFrame;

        Matrix4x4 m_View = Matrix4x4.identity;
        Matrix4x4 m_Projection = Matrix4x4.identity;
        bool m_ViewProjectionSet;

        readonly HashSet<string> m_ResolvedShaders = new HashSet<string>();
        readonly HashSet<string> m_WarnedOnce = new HashSet<string>();
        readonly List<Vector3> m_Positions = new List<Vector3>();
        readonly List<Vector2> m_Uv0 = new List<Vector2>();
        readonly List<Vector4>[] m_UvN = new List<Vector4>[8];
        readonly List<int> m_Indices = new List<int>();
        readonly List<Vector4> m_VectorArray = new List<Vector4>();
        readonly float[] m_Uniforms = new float[UniformSlots.Count];
        readonly float[] m_SdfUniforms = new float[SdfUniformSlots.Count];
        readonly int[] m_TextureInfo = new int[11];
        readonly int[] m_SamplerInfo = new int[5];
        // Vertex bytes, index count, nine attribute offsets and nine strides.
        readonly int[] m_MeshHeader = new int[20];
        readonly int[] m_DrawInfo = new int[8];
        readonly int[] m_TargetInfo = new int[13];
        readonly int[] m_BlitInfo = new int[9];
        static readonly int[] s_SdfImageFieldPassBlits = new int[5];
        internal static string SdfImageFieldPassReport()
        {
            return s_SdfImageFieldPassBlits[0] + "/" + s_SdfImageFieldPassBlits[1] + "/" +
                   s_SdfImageFieldPassBlits[2] + "/" + s_SdfImageFieldPassBlits[3] + "/" +
                   s_SdfImageFieldPassBlits[4];
        }
        readonly int[] m_ProceduralInfo = new int[7];
        byte[] m_Payload = new byte[64 * 1024];
        static readonly int[] StreamElementSize = { 12, 8, 16, 16, 16, 16, 16, 16, 16 };

        WebGL2Backend()
        {
            for (int i = 0; i < m_UvN.Length; ++i)
                m_UvN[i] = new List<Vector4>();
        }
        public const string DefaultModulePath = "../nowui-gl.js";
        public static async Task<WebGL2Backend> CreateAsync(string canvasSelector, string modulePath = DefaultModulePath,
            ColorSpace colorSpace = ColorSpace.Gamma)
        {
            if (string.IsNullOrEmpty(canvasSelector))
                throw new ArgumentException("A CSS selector for the canvas is required.", nameof(canvasSelector));
            if (colorSpace != ColorSpace.Gamma)
                throw new NotSupportedException("The browser renderer currently supports Gamma color space.");

            var backend = new WebGL2Backend();
            await JSHost.ImportAsync(Interop.ModuleName, modulePath).ConfigureAwait(false);
            RegisterShaders();
            string report = Interop.Init(canvasSelector);
            backend.m_Caps = ParseCaps(report);
            backend.m_Initialized = true;
            return backend;
        }

        static void RegisterShaders()
        {
            RegisterShader(RectangleShader, "rectangle");
            RegisterShader(TextShader, "text");
            RegisterShader(GradientShader, "gradient");
            RegisterShader(RippleShader, "ripple");
            RegisterShader(GlassShader, "glass");
            RegisterShader(ColorPickerShader, "colorpicker");
            RegisterShader(BezierShader, "bezier");
            RegisterShader(SdfShader, "sdf");
            RegisterShader(GlassBlurShader, "glassblur");
            string[] passes = { "seed", "flood", "resolve", "stamp", "dilate" };
            string vertex = ReadShader("nowui-sdf-image.vert");
            for (int i = 0; i < passes.Length; i++)
                Interop.RegisterShader(SdfImageFieldShader, i, vertex, ReadShader("nowui-sdf-image-" + passes[i] + ".frag"));
        }

        static void RegisterShader(string name, string file)
            => Interop.RegisterShader(name, 0, ReadShader("nowui-" + file + ".vert"), ReadShader("nowui-" + file + ".frag"));

        // Embed the same canonical GLSL files as Desktop; only include expansion
        // happens here. This avoids a second manually maintained shader copy in JS.
        static string ReadShader(string name)
        {
            using var stream = typeof(WebGL2Backend).Assembly.GetManifestResourceStream("NowUI.Browser.Shaders." + name)
                ?? throw new InvalidOperationException("Missing embedded browser shader: " + name);
            using var reader = new StreamReader(stream);
            var result = new StringBuilder();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("//#include \"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
                    result.AppendLine(ReadShader(trimmed.Substring(12, trimmed.Length - 13)));
                else result.AppendLine(line);
            }
            return result.ToString();
        }
        static NowRenderCaps ParseCaps(string report)
        {
            string[] parts = (report ?? string.Empty).Split('|');

            int maxTextureSize = parts.Length > 0 && int.TryParse(parts[0], out int t) ? t : 2048;
            bool floatRenderable = parts.Length > 2 && parts[2] == "1";
            string renderer = parts.Length > 3 ? parts[3] : "WebGL2";
            bool halfRenderable = parts.Length > 4 ? parts[4] == "1" : floatRenderable;

            return new NowRenderCaps(
                maxTextureSize: maxTextureSize,
                maxMsaaSamples: 1,
                supportsMultisampledTextures: false,
                supportsTextureArrays: false,
                supportsInstancing: false,
                supportsR8: true,
                supportsRHalf: halfRenderable,
                supportsRFloat: floatRenderable,
                supportsRGHalf: halfRenderable,
                supportsRGFloat: floatRenderable,
                supportsARGBHalf: halfRenderable,
                supportsARGBFloat: floatRenderable,
                supportsARGB32: true,
                supportsDepth: true,
                renderTargetsAreBottomUp: true,
                colorSpace: ColorSpace.Gamma,
                deviceName: renderer,
                deviceType: GraphicsDeviceType.OpenGLES3,
                graphicsMemorySizeMb: 0);
        }
        public NowRenderCaps caps
        {
            get
            {
                RequireInitialized();
                return m_Caps;
            }
        }
        public void BeginFrame(int frameCount)
        {
            RequireInitialized();

            if (m_InFrame)
                throw new InvalidOperationException("WebGL2Backend.BeginFrame was called twice without an EndFrame.");

            m_InFrame = true;
            m_ViewProjectionSet = false;
            Trace("BeginFrame(" + frameCount + ")");
            Interop.BeginFrame(frameCount);
        }
        public void EndFrame()
        {
            RequireInitialized();

            if (!m_InFrame)
                throw new InvalidOperationException("WebGL2Backend.EndFrame was called without a BeginFrame.");

            m_InFrame = false;
            Trace("EndFrame()");
            Interop.EndFrame();

            if (traceFrames > 0)
            {
                traceFrames--;
                Console.WriteLine("[NowUI][trace]" + NewLine + s_Trace.ToString());
                s_Trace.Clear();
            }
        }
        public void UploadTexture2D(Texture2D texture, ReadOnlySpan<byte> pixels, RectInt dirtyRect, bool generateMips)
        {
            RequireInitialized();

            if (ReferenceEquals(texture, null))
                throw new ArgumentNullException(nameof(texture));
            if (texture.format != TextureFormat.RGBA32)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.UploadTexture2D: texture '{texture.name}' is {texture.format}. Slice 1 uploads " +
                    "RGBA32 only; other texture formats belong to a later slice.");
            }

            int width = texture.width;
            int height = texture.height;

            if (pixels.Length < width * height * 4)
            {
                throw new ArgumentException(
                    $"WebGL2Backend.UploadTexture2D: texture '{texture.name}' is {width}x{height} RGBA32, which needs " +
                    $"{width * height * 4} bytes, but {pixels.Length} arrived.", nameof(pixels));
            }
            m_TextureInfo[0] = width;
            m_TextureInfo[1] = height;
            m_TextureInfo[2] = dirtyRect.x;
            m_TextureInfo[3] = dirtyRect.y;
            m_TextureInfo[4] = dirtyRect.width;
            m_TextureInfo[5] = dirtyRect.height;
            m_TextureInfo[6] = (int)texture.filterMode;
            m_TextureInfo[7] = (int)texture.wrapModeU;
            m_TextureInfo[8] = (int)texture.wrapModeV;
            m_TextureInfo[9] = texture.mipmapCount;
            m_TextureInfo[10] = generateMips ? 1 : 0;
            Interop.UploadTexture(
                texture.GetInstanceID(),
                m_TextureInfo,
                MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(pixels), pixels.Length));
        }
        public void UpdateSampler(Texture texture)
        {
            RequireInitialized();

            if (ReferenceEquals(texture, null))
                throw new ArgumentNullException(nameof(texture));

            m_SamplerInfo[0] = (int)texture.filterMode;
            m_SamplerInfo[1] = (int)texture.wrapModeU;
            m_SamplerInfo[2] = (int)texture.wrapModeV;
            m_SamplerInfo[3] = 0;

            m_SamplerInfo[4] = texture.mipmapCount;

            Interop.UpdateSampler(texture.GetInstanceID(), m_SamplerInfo);
        }
        public void ReleaseTexture(Texture texture)
        {
            if (!m_Initialized || ReferenceEquals(texture, null))
                return;

            Interop.ReleaseTexture(texture.GetInstanceID());
        }
        public void ReleaseMesh(Mesh mesh)
        {
            if (!m_Initialized || ReferenceEquals(mesh, null))
                return;

            Interop.ReleaseMesh(mesh.GetInstanceID());
        }
        public void ReleaseMaterial(Material material)
        {
        }
        public bool ResolveShader(Shader shader)
        {
            RequireInitialized();

            if (ReferenceEquals(shader, null))
                return false;

            string name = shader.name;

            if (m_ResolvedShaders.Contains(name))
                return true;
            if (Interop.ResolveShader(name) == 0)
                return false;

            m_ResolvedShaders.Add(name);
            return true;
        }
        public void SetRenderTarget(in NowRenderTarget target)
        {
            RequireInitialized();
            if (target.isBackBuffer)
            {
                Trace("SetRenderTarget(backbuffer)");
                Interop.SetRenderTarget(BackBufferTarget, 0, 0);
                return;
            }

            if (target.face != CubemapFace.Unknown)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.SetRenderTarget: cubemap face {target.face}. Only 2D targets are allocatable " +
                    "by this backend, so a cube face cannot be reached.");
            }
            int slice = target.depthSlice == NowRenderTarget.AllDepthSlices ? 0 : target.depthSlice;

            if (slice != 0)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.SetRenderTarget: depth slice {slice}. Array render targets are not ported; " +
                    "the only NowUI code that asks for one is NowGlassRenderer's single-pass-instanced stereo " +
                    "path, which no browser host reaches.");
            }

            Trace("SetRenderTarget(rt " + target.texture.GetInstanceID() + " " +
                  target.texture.width + "x" + target.texture.height + " mip " + target.mipLevel + ")");
            Interop.SetRenderTarget(target.texture.GetInstanceID(), target.mipLevel, slice);
        }
        const int BackBufferTarget = 0;
        public void SetViewport(in Rect pixelRect)
        {
            RequireInitialized();

            Trace("SetViewport(" + (int)pixelRect.x + "," + (int)pixelRect.y + " " +
                  (int)pixelRect.width + "x" + (int)pixelRect.height + ")");
            Interop.SetViewport(
                (int)System.Math.Round(pixelRect.x),
                (int)System.Math.Round(pixelRect.y),
                (int)System.Math.Round(pixelRect.width),
                (int)System.Math.Round(pixelRect.height));
        }
        public void SetViewProjection(in Matrix4x4 view, in Matrix4x4 projection)
        {
            RequireInitialized();

            m_View = view;
            m_Projection = projection;
            m_ViewProjectionSet = true;
        }
        public void ClearRenderTarget(bool clearDepth, bool clearColor, in Color color, float depth)
        {
            RequireInitialized();
            Trace("  ClearRenderTarget");
            Interop.ClearTarget(clearColor, color.r, color.g, color.b, color.a);
        }
        public void DrawMesh(Mesh mesh, int subMesh, in Matrix4x4 model, Material material, int pass,
                             MaterialPropertyBlock properties)
        {
            RequireInitialized();

            if (ReferenceEquals(mesh, null))
                throw new ArgumentNullException(nameof(mesh));
            if (ReferenceEquals(material, null))
                throw new ArgumentNullException(nameof(material));
            if (!m_ViewProjectionSet)
                throw new InvalidOperationException("WebGL2Backend.DrawMesh: no SetViewProjection preceded this draw.");

            Shader shader = material.shader;

            if (ReferenceEquals(shader, null))
                throw new InvalidOperationException($"WebGL2Backend.DrawMesh: material '{material.name}' has no shader.");

            string shaderName = shader.name;

            if (!IsPortedShader(shaderName))
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.DrawMesh: shader '{shaderName}' is not ported. This backend implements " +
                    PortedShaderList + "; every other program belongs to a later slice.");
            }

            Trace("  DrawMesh(" + (string.IsNullOrEmpty(mesh.name) ? "(unnamed)" : mesh.name) + ", '" +
                  shaderName + "', pass " + pass + ")");
            pass = NormalizePass(pass);

            if (!m_ResolvedShaders.Contains(shaderName) && !ResolveShader(shader))
                throw new InvalidOperationException($"WebGL2Backend.DrawMesh: '{shaderName}' failed to resolve.");

            int indexCount = EnsureMeshUploaded(mesh, subMesh);

            if (indexCount == 0)
                return;

            BuildUniformBlock(material, properties, model, shaderName);

            m_DrawInfo[0] = indexCount;
            m_DrawInfo[1] = TextureId(material, properties, IdMainTex);
            if (shaderName == GradientShader && m_DrawInfo[1] == NoTexture && m_WarnedOnce.Add("gradient-ramp"))
            {
                Debug.LogError(
                    "WebGL2Backend: a NowUI/UI Gradient draw has no _MainTex, so the ramp atlas is missing and " +
                    "every gradient will render flat white. NowGradientMaterials assigns the atlas as the " +
                    "material's mainTexture; check that NowGradientRampCache published one. This message " +
                    "appears once.");
            }
            m_DrawInfo[2] = TextureId(material, properties, IdTextureMask0);
            m_DrawInfo[3] = TextureId(material, properties, IdTextureMask1);
            m_DrawInfo[4] = pass;
            m_DrawInfo[5] = NoTexture;
            m_DrawInfo[6] = NoTexture;
            m_DrawInfo[7] = NoTexture;

            if (shaderName == TextShader)
            {
                m_DrawInfo[7] = TextureId(material, properties, IdGradientRampTexture);
                if (m_DrawInfo[7] == NoTexture)
                    WarnOnMissingTextGradientRamp(mesh);
            }

            if (shaderName == SdfShader)
            {
                m_DrawInfo[5] = TextureId(material, properties, IdSdfImageField);
                m_DrawInfo[6] = TextureId(material, properties, IdSdfImageColor);
                // Gradient fills sample the shared ramp atlas; nowui-gl.js binds
                // the unit for any program that declares the sampler.
                m_DrawInfo[7] = TextureId(material, properties, IdGradientRampTexture);
                BuildSdfUniformBlock(material, properties);
                Interop.SetSdfUniforms(MemoryMarshal.AsBytes(new Span<float>(m_SdfUniforms)));
            }

            Interop.Draw(
                mesh.GetInstanceID(),
                shaderName,
                m_DrawInfo,
                MemoryMarshal.AsBytes(new Span<float>(m_Uniforms)));
        }
        int EnsureMeshUploaded(Mesh mesh, int subMesh)
        {
            if (subMesh < 0 || subMesh >= mesh.subMeshCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(subMesh), $"WebGL2Backend.DrawMesh: sub-mesh {subMesh} of a mesh with {mesh.subMeshCount}.");
            }

            SubMeshDescriptor descriptor = mesh.GetSubMesh(subMesh);

            if (descriptor.topology != MeshTopology.Triangles)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.DrawMesh: sub-mesh topology {descriptor.topology}. NowUI's render meshes are " +
                    "triangle lists; other topologies belong to a later slice.");
            }

            int vertexCount = mesh.vertexCount;
            mesh.GetTriangles(m_Indices, subMesh);

            if (vertexCount == 0 || m_Indices.Count == 0)
                return 0;

            Array.Clear(m_MeshHeader, 0, m_MeshHeader.Length);
            if (TryUploadRawMesh(mesh, vertexCount)) return m_Indices.Count;
            Array.Clear(m_MeshHeader, 0, m_MeshHeader.Length);

            mesh.GetVertices(m_Positions);
            mesh.GetUVs(0, m_Uv0);

            for (int channel = 1; channel <= 7; ++channel)
                mesh.GetUVs(channel, m_UvN[channel]);

            RequireStream(mesh, "POSITION", m_Positions.Count, vertexCount);
            RequireStream(mesh, "TEXCOORD0", m_Uv0.Count, vertexCount);

            for (int channel = 1; channel <= 7; ++channel)
                RequireStream(mesh, "TEXCOORD" + channel, m_UvN[channel].Count, vertexCount);

            int vertexBytes = 0;
            for (int a = 0; a < StreamElementSize.Length; ++a)
                vertexBytes += StreamElementSize[a] * vertexCount;

            int indexBytes = m_Indices.Count * 4;
            EnsurePayload(vertexBytes + indexBytes);

            var payload = new Span<byte>(m_Payload);
            int cursor = 0;

            m_MeshHeader[0] = vertexBytes;
            m_MeshHeader[1] = m_Indices.Count;

            m_MeshHeader[2] = cursor;
            for (int v = 0; v < vertexCount; ++v)
            {
                Vector3 p = m_Positions[v];
                WriteFloat(payload, ref cursor, p.x);
                WriteFloat(payload, ref cursor, p.y);
                WriteFloat(payload, ref cursor, p.z);
            }

            m_MeshHeader[3] = cursor;
            for (int v = 0; v < vertexCount; ++v)
            {
                Vector2 uv = m_Uv0[v];
                WriteFloat(payload, ref cursor, uv.x);
                WriteFloat(payload, ref cursor, uv.y);
            }

            for (int channel = 1; channel <= 7; ++channel)
            {
                m_MeshHeader[3 + channel] = cursor;
                List<Vector4> stream = m_UvN[channel];

                for (int v = 0; v < vertexCount; ++v)
                {
                    Vector4 value = stream[v];
                    WriteFloat(payload, ref cursor, value.x);
                    WriteFloat(payload, ref cursor, value.y);
                    WriteFloat(payload, ref cursor, value.z);
                    WriteFloat(payload, ref cursor, value.w);
                }
            }
            for (int i = 0; i < m_Indices.Count; ++i)
            {
                int index = m_Indices[i];

                if ((uint)index >= (uint)vertexCount)
                {
                    throw new InvalidOperationException(
                        $"WebGL2Backend.DrawMesh: index {index} of sub-mesh {subMesh} addresses vertex {index} of " +
                        $"{vertexCount}. The mesh's index buffer and vertex streams disagree.");
                }

                WriteInt(payload, ref cursor, index);
            }

            Interop.UploadMesh(mesh.GetInstanceID(), vertexCount, m_MeshHeader, payload.Slice(0, cursor));
            return m_Indices.Count;
        }

        bool TryUploadRawMesh(Mesh mesh, int vertexCount)
        {
            var data = mesh.data;
            int vertexBytes = 0;
            for (int a = 0; a < 9; a++)
            {
                var attribute = a == 0 ? VertexAttribute.Position : (VertexAttribute)((int)VertexAttribute.TexCoord0 + a - 1);
                int expected = a == 0 ? 3 : a == 1 ? 2 : 4;
                if (data.interleaved)
                {
                    if (!data.TryGetAttribute(attribute, out int offset, out var format, out int dimension)
                        || format != VertexAttributeFormat.Float32 || dimension < expected) return false;
                    m_MeshHeader[2 + a] = offset;
                    m_MeshHeader[11 + a] = data.vertexStride;
                }
                else
                {
                    var stream = data.streams[(int)attribute];
                    if (stream.count != vertexCount || stream.elementSize < expected * 4 || stream.elementSize > 16
                        || stream.bytes == null || stream.bytes.Length < vertexCount * stream.elementSize) return false;
                    m_MeshHeader[2 + a] = vertexBytes;
                    m_MeshHeader[11 + a] = stream.elementSize;
                    vertexBytes = checked(vertexBytes + vertexCount * stream.elementSize);
                }
            }
            if (data.interleaved)
            {
                vertexBytes = checked(vertexCount * data.vertexStride);
                if (data.vertexBytes == null || data.vertexBytes.Length < vertexBytes) return false;
            }
            int indexBytes = checked(m_Indices.Count * 4);
            EnsurePayload(checked(vertexBytes + indexBytes));
            var payload = m_Payload.AsSpan(0, vertexBytes + indexBytes);
            if (data.interleaved) data.vertexBytes.AsSpan(0, vertexBytes).CopyTo(payload);
            else
            {
                for (int a = 0; a < 9; a++)
                {
                    var attribute = a == 0 ? VertexAttribute.Position : (VertexAttribute)((int)VertexAttribute.TexCoord0 + a - 1);
                    var stream = data.streams[(int)attribute];
                    stream.bytes.AsSpan(0, vertexCount * stream.elementSize).CopyTo(payload.Slice(m_MeshHeader[2 + a]));
                }
            }
            var indices = CollectionsMarshal.AsSpan(m_Indices);
            for (int i = 0; i < indices.Length; i++)
                if ((uint)indices[i] >= (uint)vertexCount)
                    throw new InvalidOperationException("Browser mesh index lies outside its vertex buffer.");
            MemoryMarshal.AsBytes(indices).CopyTo(payload.Slice(vertexBytes));
            m_MeshHeader[0] = vertexBytes;
            m_MeshHeader[1] = indices.Length;
            Interop.UploadMesh(mesh.GetInstanceID(), vertexCount, m_MeshHeader, payload);
            return true;
        }

        static void RequireStream(Mesh mesh, string semantic, int actual, int expected)
        {
            if (actual == expected)
                return;

            throw new InvalidOperationException(
                $"WebGL2Backend.DrawMesh: mesh '{mesh.name}' has {actual} {semantic} elements for {expected} " +
                "vertices. NowUI's render layout writes all nine streams at full length.");
        }
        void WarnOnMissingTextGradientRamp(Mesh mesh)
        {
            if (m_WarnedOnce.Contains("text-gradient-ramp"))
                return;

            mesh.GetUVs(5, m_UvN[5]);
            List<Vector4> extras = m_UvN[5];

            for (int v = 0; v < extras.Count; ++v)
            {
                if (extras[v].w == 0f)
                    continue;

                m_WarnedOnce.Add("text-gradient-ramp");
                Debug.LogError(
                    $"WebGL2Backend: mesh '{mesh.name}' carries a text vertex with extras.w = {extras[v].w}, which " +
                    "asks NowUITextGradientSample for row " + Mathf.Floor(extras[v].w) + " of the gradient ramp " +
                    "atlas — but _NowGradientRampTexture is not set, on the material, in the property block or " +
                    "in NowRuntime.globals. The sampler falls back to 1x1 white, so this text renders with its " +
                    "flat colour. NowGradientRampCache publishes the atlas with Shader.SetGlobalTexture " +
                    "(NowGradient.cs:580); check that the global survived the trip through the shim. This " +
                    "message appears once.");
                return;
            }
        }

        void EnsurePayload(int bytes)
        {
            if (m_Payload.Length >= bytes)
                return;

            int size = m_Payload.Length;
            while (size < bytes)
                size *= 2;

            m_Payload = new byte[size];
        }

        static void WriteFloat(Span<byte> destination, ref int cursor, float value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(cursor, 4), value);
            cursor += 4;
        }

        static void WriteInt(Span<byte> destination, ref int cursor, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(cursor, 4), value);
            cursor += 4;
        }
        void BuildUniformBlock(Material material, MaterialPropertyBlock properties, in Matrix4x4 model,
                               string shaderName)
        {
            Array.Clear(m_Uniforms, 0, m_Uniforms.Length);
            Matrix4x4 mvp = m_Projection * m_View * model;
            WriteMatrix(m_Uniforms, UniformSlots.Mvp, mvp);
            Vector2 scale = material.mainTextureScale;
            Vector2 offset = material.mainTextureOffset;
            m_Uniforms[UniformSlots.MainTexST + 0] = scale.x;
            m_Uniforms[UniformSlots.MainTexST + 1] = scale.y;
            m_Uniforms[UniformSlots.MainTexST + 2] = offset.x;
            m_Uniforms[UniformSlots.MainTexST + 3] = offset.y;

            if (shaderName == RectangleShader)
            {
                m_Uniforms[UniformSlots.PremultipliedTexture] =
                    ResolveFloat(material, properties, IdPremultipliedTexture);
            }
            else if (shaderName == GradientShader)
            {
                Vector4 texelSize = ResolveVector(material, properties, IdGradientRampTexelSize);

                if (texelSize.z < 1f || texelSize.w < 1f || texelSize.x <= 0f || texelSize.y <= 0f)
                {
                    if (m_WarnedOnce.Add("gradient-texel-size"))
                    {
                        Debug.LogWarning(
                            $"WebGL2Backend: _NowGradientRampTexelSize resolved to {texelSize}, which cannot " +
                            "describe a ramp atlas. Falling back to the shader's 256x256 default. Expect this " +
                            "when NowGradientMaterials could not publish the atlas. This message appears once.");
                    }

                    texelSize = DefaultRampTexelSize;
                }

                m_Uniforms[UniformSlots.GradientRampTexelSize + 0] = texelSize.x;
                m_Uniforms[UniformSlots.GradientRampTexelSize + 1] = texelSize.y;
                m_Uniforms[UniformSlots.GradientRampTexelSize + 2] = texelSize.z;
                m_Uniforms[UniformSlots.GradientRampTexelSize + 3] = texelSize.w;
            }
            else if (shaderName == RippleShader || shaderName == BezierShader)
            {
            }
            else if (shaderName == ColorPickerShader)
            {
                m_Uniforms[UniformSlots.ColorPickerMode] = ResolveFloat(material, properties, IdColorPickerMode);
            }
            else if (shaderName == GlassShader)
            {
                m_Uniforms[UniformSlots.GlassUseBackdrop] =
                    ResolveFloat(material, properties, IdGlassUseBackdrop);
                if (!m_ReportedGlassBackdropFlag)
                {
                    m_ReportedGlassBackdropFlag = true;
                    Console.WriteLine(
                        "[NowUI] WebGL2Backend: first 'NowUI/UI Glass' draw resolved _NowGlassUseBackdrop = " +
                        m_Uniforms[UniformSlots.GlassUseBackdrop].ToString("0.###") +
                        ". Zero selects the shader's no-backdrop branch, so the pane is a translucent tinted " +
                        "rounded rect and the backdrop behind it is NOT blurred, whatever the blur pipeline did.");
                }
                m_Uniforms[UniformSlots.GlassUseStereoBackdrop] =
                    ResolveFloat(material, properties, IdGlassUseStereoBackdrop);
                m_Uniforms[UniformSlots.GlassMaterialMode] =
                    ResolveFloat(material, properties, IdGlassMaterialMode);
                Vector4 backdropUv = ResolveVector(material, properties, IdBackdropUvTransform);

                if (backdropUv == Vector4.zero)
                    backdropUv = IdentityUvTransform;

                m_Uniforms[UniformSlots.BackdropUvTransform + 0] = backdropUv.x;
                m_Uniforms[UniformSlots.BackdropUvTransform + 1] = backdropUv.y;
                m_Uniforms[UniformSlots.BackdropUvTransform + 2] = backdropUv.z;
                m_Uniforms[UniformSlots.BackdropUvTransform + 3] = backdropUv.w;
            }
            else if (shaderName == GlassBlurShader)
            {
                Vector4 texelSize = ResolveVector(material, properties, IdBlurTexelSize);
                Vector4 sourceScaleOffset = ResolveVector(material, properties, IdBlurSourceScaleOffset);
                Vector4 direction = ResolveVector(material, properties, IdBlurDirection);

                if (sourceScaleOffset == Vector4.zero)
                    sourceScaleOffset = IdentityUvTransform;

                if ((texelSize.x <= 0f || texelSize.y <= 0f) && m_WarnedOnce.Add("blur-texel-size"))
                {
                    Debug.LogWarning(
                        $"WebGL2Backend: _NowBlurTexelSize resolved to {texelSize}, which cannot describe a " +
                        "blur source. Every tap will land on the same texel, so the pass will be a " +
                        "pixel-perfect copy and the glass behind it will look sharp rather than blurred. " +
                        "Expect this if the blur was invoked without NowGlassRenderer setting its globals. " +
                        "This message appears once.");
                }

                m_Uniforms[UniformSlots.BlurTexelSize + 0] = texelSize.x;
                m_Uniforms[UniformSlots.BlurTexelSize + 1] = texelSize.y;
                m_Uniforms[UniformSlots.BlurTexelSize + 2] = texelSize.z;
                m_Uniforms[UniformSlots.BlurTexelSize + 3] = texelSize.w;

                m_Uniforms[UniformSlots.BlurSourceScaleOffset + 0] = sourceScaleOffset.x;
                m_Uniforms[UniformSlots.BlurSourceScaleOffset + 1] = sourceScaleOffset.y;
                m_Uniforms[UniformSlots.BlurSourceScaleOffset + 2] = sourceScaleOffset.z;
                m_Uniforms[UniformSlots.BlurSourceScaleOffset + 3] = sourceScaleOffset.w;
                m_Uniforms[UniformSlots.BlurDirection + 0] = direction.x;
                m_Uniforms[UniformSlots.BlurDirection + 1] = direction.y;
            }
            else if (shaderName == SdfImageFieldShader)
            {
                WriteVector(m_Uniforms, UniformSlots.SourceUv, ResolveVector(material, properties, IdSourceUv));
                WriteVector(m_Uniforms, UniformSlots.FieldParams,
                            ResolveVector(material, properties, IdFieldParams));
                WriteVector(m_Uniforms, UniformSlots.FieldTexels,
                            ResolveVector(material, properties, IdFieldTexels));
                WriteVector(m_Uniforms, UniformSlots.StampRect,
                            ResolveVector(material, properties, IdStampRect));
                m_Uniforms[UniformSlots.Step] = ResolveFloat(material, properties, IdStep);
            }
            else if (shaderName == SdfShader)
            {
            }
            else
            {
                float encoding = ResolveFloat(material, properties, IdTextSdfEncoding);
                m_Uniforms[UniformSlots.TextSdfEncoding] = encoding;

                if (encoding <= 0.5f && NowFontCompiler.forceManagedCompiler && m_WarnedOnce.Add("sdf-encoding"))
                {
                    Debug.LogWarning(
                        "WebGL2Backend: _NowUITextSdfEncoding resolved to 0 while the managed baker is forced, so " +
                        "the text shader will take the median-RGB branch on a page that packs SDF16; text will " +
                        "render blurry and subtly wrong until that is understood. This message appears once.");
                }
            }

            float maskCount = ResolveFloat(material, properties, IdMaskCount);
            float textureMaskCount = ResolveFloat(material, properties, IdTextureMaskCount);
            m_Uniforms[UniformSlots.MaskCount] = maskCount;
            m_Uniforms[UniformSlots.TextureMaskCount] = textureMaskCount;
            if (maskCount >= 0.5f)
            {
                CopyVectorArray(material, properties, IdMaskRects, UniformSlots.MaskRects, AnalyticMaskCapacity);
                CopyVectorArray(material, properties, IdMaskData, UniformSlots.MaskData, AnalyticMaskCapacity);
                CopyVectorArray(material, properties, IdMaskParams, UniformSlots.MaskParams, AnalyticMaskCapacity);
                CopyVectorArray(material, properties, IdMaskTransforms, UniformSlots.MaskTransforms,
                                AnalyticMaskCapacity);
            }

            if (textureMaskCount >= 0.5f)
            {
                CopyVectorArray(material, properties, IdTextureMaskRects, UniformSlots.TextureMaskRects,
                                TextureMaskCapacity);
                CopyVectorArray(material, properties, IdTextureMaskParams, UniformSlots.TextureMaskParams,
                                TextureMaskCapacity);
                CopyVectorArray(material, properties, IdTextureMaskTransforms, UniformSlots.TextureMaskTransforms,
                                TextureMaskCapacity);
            }
        }
        void BuildSdfUniformBlock(Material material, MaterialPropertyBlock properties)
        {
            Array.Clear(m_SdfUniforms, 0, m_SdfUniforms.Length);
            float abi = ResolveFloat(material, properties, IdSdfAbiVersion);

            if (abi != SdfPortedAbiVersion)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend: SDF material '{material.name}' declares _NowSdfAbiVersion = {abi}. This " +
                    $"backend ports NowSdfShaderV{(int)SdfPortedAbiVersion}.cginc only. A material on a " +
                    "different ABI packs its uniform arrays under a different contract, so drawing it through " +
                    "this program would render a plausible wrong scene rather than fail.");
            }

            CopyVectorArray(material, properties, IdSdfData0, m_SdfUniforms, SdfUniformSlots.Data0,
                            SdfShapeCapacity, "_SdfData0");
            CopyVectorArray(material, properties, IdSdfData1, m_SdfUniforms, SdfUniformSlots.Data1,
                            SdfShapeCapacity, "_SdfData1");
            CopyVectorArray(material, properties, IdSdfData2, m_SdfUniforms, SdfUniformSlots.Data2,
                            SdfShapeCapacity, "_SdfData2");
            CopyVectorArray(material, properties, IdSdfShapeMeta, m_SdfUniforms, SdfUniformSlots.ShapeMeta,
                            SdfShapeCapacity, "_SdfShapeMeta");
            CopyVectorArray(material, properties, IdSdfColors, m_SdfUniforms, SdfUniformSlots.Colors,
                            SdfShapeCapacity, "_SdfColors");
            CopyVectorArray(material, properties, IdSdfUvs, m_SdfUniforms, SdfUniformSlots.Uvs,
                            SdfShapeCapacity, "_SdfUvs");
            CopyVectorArray(material, properties, IdSdfImageUvs, m_SdfUniforms, SdfUniformSlots.ImageUvs,
                            SdfShapeCapacity, "_SdfImageUvs");
            CopyVectorArray(material, properties, IdSdfLayerData0, m_SdfUniforms, SdfUniformSlots.LayerData0,
                            SdfLayerCapacity, "_SdfLayerData0");
            CopyVectorArray(material, properties, IdSdfLayerData1, m_SdfUniforms, SdfUniformSlots.LayerData1,
                            SdfLayerCapacity, "_SdfLayerData1");
            Vector4 atlasSize = ResolveVector(material, properties, IdSdfImageAtlasSize);
            WriteVector(m_SdfUniforms, SdfUniformSlots.ImageAtlasSize,
                        atlasSize == Vector4.zero ? Vector4.one : atlasSize);

            WriteVector(m_SdfUniforms, SdfUniformSlots.Outline, ResolveVector(material, properties, IdSdfOutline));
            WriteVector(m_SdfUniforms, SdfUniformSlots.OutlineColor,
                        ResolveVector(material, properties, IdSdfOutlineColor));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Glow, ResolveVector(material, properties, IdSdfGlow));
            WriteVector(m_SdfUniforms, SdfUniformSlots.GlowColor,
                        ResolveVector(material, properties, IdSdfGlowColor));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Shadow, ResolveVector(material, properties, IdSdfShadow));
            WriteVector(m_SdfUniforms, SdfUniformSlots.ShadowColor,
                        ResolveVector(material, properties, IdSdfShadowColor));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Shadow2, ResolveVector(material, properties, IdSdfShadow2));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Shadow2Color,
                        ResolveVector(material, properties, IdSdfShadow2Color));
            WriteVector(m_SdfUniforms, SdfUniformSlots.InnerShadow,
                        ResolveVector(material, properties, IdSdfInnerShadow));
            WriteVector(m_SdfUniforms, SdfUniformSlots.InnerShadowColor,
                        ResolveVector(material, properties, IdSdfInnerShadowColor));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Emboss, ResolveVector(material, properties, IdSdfEmboss));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Contour, ResolveVector(material, properties, IdSdfContour));
            WriteVector(m_SdfUniforms, SdfUniformSlots.ContourColor,
                        ResolveVector(material, properties, IdSdfContourColor));
            WriteVector(m_SdfUniforms, SdfUniformSlots.ContourMask,
                        ResolveVector(material, properties, IdSdfContourMask));
            WriteVector(m_SdfUniforms, SdfUniformSlots.Warp, ResolveVector(material, properties, IdSdfWarp));

            m_SdfUniforms[SdfUniformSlots.ShapeCount] = ResolveFloat(material, properties, IdSdfShapeCount);
            m_SdfUniforms[SdfUniformSlots.LayerCount] = ResolveFloat(material, properties, IdSdfLayerCount);
            m_SdfUniforms[SdfUniformSlots.Feather] = ResolveFloat(material, properties, IdSdfFeather);
            m_SdfUniforms[SdfUniformSlots.MaskOutput] = ResolveFloat(material, properties, IdSdfMaskOutput);
            m_SdfUniforms[SdfUniformSlots.CanvasLayout] = ResolveFloat(material, properties, IdSdfCanvasLayout);
            float textEffectLimit = ResolveFloat(material, properties, IdSdfTextEffectLimit);

            if (textEffectLimit <= 0f)
            {
                if (m_WarnedOnce.Add("sdf-text-effect-limit"))
                {
                    Debug.LogWarning(
                        $"WebGL2Backend: _SdfTextEffectLimit resolved to {textEffectLimit} on SDF material " +
                        $"'{material.name}'. NowSdfCache.Upload always writes it, so this material was never " +
                        "uploaded. Falling back to 100000 (the analytic-only value) so outline, glow, shadow " +
                        "and contours are not silently erased. This message appears once.");
                }

                textEffectLimit = 100000f;
            }

            m_SdfUniforms[SdfUniformSlots.TextEffectLimit] = textEffectLimit;
            m_SdfUniforms[SdfUniformSlots.Time] = Time.unscaledTime;
            if (m_DrawInfo[5] == NoTexture && m_DrawInfo[6] == NoTexture &&
                m_SdfUniforms[SdfUniformSlots.ShapeCount] > 0f &&
                HasImageNode() && m_WarnedOnce.Add("sdf-image-node"))
            {
                Debug.LogWarning(
                    "WebGL2Backend: this SDF scene contains an Image or Sprite node, but no image atlas is " +
                    "bound because 'Hidden/NowUI/SDF Image Field' — the jump-flood program that builds one — " +
                    "is not ported. _SdfImageField falls back to 1x1 black, so the node renders as a filled " +
                    "rectangle rather than as its silhouette. This message appears once.");
            }
        }
        bool HasImageNode()
        {
            int count = Math.Min((int)m_SdfUniforms[SdfUniformSlots.ShapeCount], SdfShapeCapacity);

            for (int i = 0; i < count; ++i)
            {
                float type = m_SdfUniforms[SdfUniformSlots.Data0 + i * 4];

                if (type > 9.5f && type < 10.5f)
                    return true;
            }

            return false;
        }

        static void WriteVector(float[] destination, int slot, in Vector4 value)
        {
            destination[slot + 0] = value.x;
            destination[slot + 1] = value.y;
            destination[slot + 2] = value.z;
            destination[slot + 3] = value.w;
        }

        static void WriteMatrix(float[] destination, int slot, in Matrix4x4 m)
        {
            destination[slot + 0] = m.m00; destination[slot + 1] = m.m10;
            destination[slot + 2] = m.m20; destination[slot + 3] = m.m30;
            destination[slot + 4] = m.m01; destination[slot + 5] = m.m11;
            destination[slot + 6] = m.m21; destination[slot + 7] = m.m31;
            destination[slot + 8] = m.m02; destination[slot + 9] = m.m12;
            destination[slot + 10] = m.m22; destination[slot + 11] = m.m32;
            destination[slot + 12] = m.m03; destination[slot + 13] = m.m13;
            destination[slot + 14] = m.m23; destination[slot + 15] = m.m33;
        }
        static float ResolveFloat(Material material, MaterialPropertyBlock properties, int id)
        {
            if (properties != null && properties.HasProperty(id))
                return properties.GetFloat(id);

            float value = material.GetFloat(id);

            if (value != 0f)
                return value;

            return NowRuntime.globals.GetFloat(id);
        }
        static readonly Vector4 DefaultRampTexelSize = new Vector4(1f / 256f, 1f / 256f, 256f, 256f);
        static readonly Vector4 IdentityUvTransform = new Vector4(1f, 1f, 0f, 0f);
        static Vector4 ResolveVector(Material material, MaterialPropertyBlock properties, int id)
        {
            if (properties != null && properties.HasProperty(id))
                return properties.GetVector(id);

            Vector4 value = material.GetVector(id);

            if (value != Vector4.zero)
                return value;

            return NowRuntime.globals.GetVector(id);
        }
        const int NoTexture = 0;
        static int TextureId(Material material, MaterialPropertyBlock properties, int id)
        {
            Texture texture = null;

            if (properties != null && properties.HasProperty(id))
                texture = properties.GetTexture(id);

            if (ReferenceEquals(texture, null))
                texture = material.GetTexture(id);
            if (ReferenceEquals(texture, null))
                texture = NowRuntime.globals.GetTexture(id);

            return ReferenceEquals(texture, null) ? NoTexture : texture.GetInstanceID();
        }
        void CopyVectorArray(Material material, MaterialPropertyBlock properties, int id, int slot, int capacity)
        {
            CopyVectorArray(material, properties, id, m_Uniforms, slot, capacity, "a mask vector array");
        }
        void CopyVectorArray(Material material, MaterialPropertyBlock properties, int id, float[] destination,
                             int slot, int capacity, string name)
        {
            m_VectorArray.Clear();

            if (properties != null && properties.HasProperty(id))
                properties.GetVectorArray(id, m_VectorArray);

            if (m_VectorArray.Count == 0)
                material.GetVectorArray(id, m_VectorArray);

            if (m_VectorArray.Count == 0)
                return;

            if (m_VectorArray.Count != capacity)
            {
                throw new InvalidOperationException(
                    $"WebGL2Backend: {name} arrived with {m_VectorArray.Count} entries; the shader " +
                    $"declares exactly {capacity}. Uploading it truncated or padded would render a plausible " +
                    "wrong result, so it is refused.");
            }

            for (int i = 0; i < capacity; ++i)
            {
                Vector4 v = m_VectorArray[i];
                destination[slot + i * 4 + 0] = v.x;
                destination[slot + i * 4 + 1] = v.y;
                destination[slot + i * 4 + 2] = v.z;
                destination[slot + i * 4 + 3] = v.w;
            }
        }
        public bool CreateRenderTexture(RenderTexture texture, in NowRenderTextureRequest request)
        {
            RequireInitialized();

            if (ReferenceEquals(texture, null))
                throw new ArgumentNullException(nameof(texture));

            if (request.enableRandomWrite)
            {
                Debug.LogError(
                    $"WebGL2Backend.CreateRenderTexture: target '{texture.name}' asks for enableRandomWrite. " +
                    "WebGL2 has no compute and no image load/store, so there is no UAV to bind it as.");
                return false;
            }

            if (request.bindMS)
            {
                Debug.LogError(
                    $"WebGL2Backend.CreateRenderTexture: target '{texture.name}' asks for bindTextureMS. " +
                    "WebGL2 has no sampleable multisampled texture; caps.supportsMultisampledTextures is false " +
                    "for exactly this reason.");
                return false;
            }

            m_TargetInfo[0] = request.width;
            m_TargetInfo[1] = request.height;
            m_TargetInfo[2] = request.depthBits;
            m_TargetInfo[3] = request.volumeDepth;
            m_TargetInfo[4] = request.mipCount;
            m_TargetInfo[5] = request.msaaSamples;
            m_TargetInfo[6] = (int)request.format;
            m_TargetInfo[7] = (int)request.dimension;
            m_TargetInfo[8] = (int)texture.filterMode;
            m_TargetInfo[9] = (int)texture.wrapModeU;
            m_TargetInfo[10] = (int)texture.wrapModeV;
            m_TargetInfo[11] = request.useMipMap ? 1 : 0;
            m_TargetInfo[12] = request.autoGenerateMips ? 1 : 0;
            return Interop.CreateRenderTexture(texture.GetInstanceID(), m_TargetInfo) != 0;
        }
        public bool IsRenderTextureLost(RenderTexture texture)
        {
            if (ReferenceEquals(texture, null))
                throw new ArgumentNullException(nameof(texture));
            if (!m_Initialized)
                return true;

            return Interop.IsRenderTextureLost(texture.GetInstanceID()) != 0;
        }
        public void ReleaseRenderTexture(RenderTexture texture)
        {
            if (!m_Initialized || ReferenceEquals(texture, null))
                return;

            Interop.ReleaseRenderTexture(texture.GetInstanceID());
        }
        static readonly Matrix4x4 BlitProjection = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 100f);
        public void Blit(Texture source, in NowRenderTarget destination, Material material, int pass,
                         in Vector2 scale, in Vector2 offset, int sourceDepthSlice, int destinationDepthSlice)
        {
            RequireInitialized();
            Trace("  Blit");
            if (ReferenceEquals(source, null) && ReferenceEquals(material, null))
                throw new ArgumentNullException(nameof(source), "A blit needs either a source texture or a material.");

            if (sourceDepthSlice != 0 || destinationDepthSlice != 0)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.Blit: depth slices ({sourceDepthSlice} -> {destinationDepthSlice}). Array " +
                    "render targets are not ported.");
            }

            if (!destination.isBackBuffer && destination.face != CubemapFace.Unknown)
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.Blit: cubemap face {destination.face}. Only 2D targets are allocatable.");
            }

            string shaderName = string.Empty;
            pass = NormalizePass(pass);

            if (!ReferenceEquals(material, null))
            {
                Shader shader = material.shader;

                if (ReferenceEquals(shader, null))
                    throw new InvalidOperationException($"WebGL2Backend.Blit: material '{material.name}' has no shader.");

                shaderName = shader.name;

                if (!IsPortedShader(shaderName))
                {
                    throw new NotSupportedException(
                        $"WebGL2Backend.Blit: shader '{shaderName}' is not ported. The blit machinery itself is " +
                        "live — target binding, the full-screen quad, the pass index, scale/offset — but the " +
                        "program is not. Note for whoever ports it: this backend's uniform bridge is the fixed " +
                        "slot map in UniformSlots, so a blit program's own uniforms need slots there and in " +
                        "nowui-gl.js's U before the pass can be fed. Hidden/NowUI/GlassBlur pass 0 is done and " +
                        "is the worked example: _NowBlurSourceTex, _NowBlurTexelSize, " +
                        "_NowBlurSourceScaleOffset and _NowBlurDirection, all resolved from " +
                        "NowRuntime.globals.");
                }

                if (!m_ResolvedShaders.Contains(shaderName) && !ResolveShader(shader))
                    throw new InvalidOperationException($"WebGL2Backend.Blit: '{shaderName}' failed to resolve.");
                BuildUniformBlock(material, null, Matrix4x4.identity, shaderName);
            }
            else
            {
                Array.Clear(m_Uniforms, 0, m_Uniforms.Length);
            }

            WriteMatrix(m_Uniforms, UniformSlots.Mvp, BlitProjection);
            m_Uniforms[UniformSlots.MainTexST + 0] = scale.x;
            m_Uniforms[UniformSlots.MainTexST + 1] = scale.y;
            m_Uniforms[UniformSlots.MainTexST + 2] = offset.x;
            m_Uniforms[UniformSlots.MainTexST + 3] = offset.y;

            m_BlitInfo[0] = ReferenceEquals(source, null) ? NoTexture : source.GetInstanceID();
            m_BlitInfo[1] = destination.isBackBuffer ? BackBufferTarget : destination.texture.GetInstanceID();
            m_BlitInfo[2] = destination.mipLevel;
            m_BlitInfo[3] = destination.width;
            m_BlitInfo[4] = destination.height;
            m_BlitInfo[5] = ReferenceEquals(material, null) ? NoTexture : TextureId(material, null, IdTextureMask0);
            m_BlitInfo[6] = ReferenceEquals(material, null) ? NoTexture : TextureId(material, null, IdTextureMask1);
            m_BlitInfo[7] = pass;
            if (shaderName == SdfImageFieldShader && pass >= 0 && pass < s_SdfImageFieldPassBlits.Length)
                ++s_SdfImageFieldPassBlits[pass];
            m_BlitInfo[8] = ReferenceEquals(material, null) ? NoTexture : TextureId(material, null, IdSourceTex);

            Interop.Blit(shaderName, m_BlitInfo, MemoryMarshal.AsBytes(new Span<float>(m_Uniforms)));
        }
        public void DrawProcedural(in Matrix4x4 model, Material material, int pass, MeshTopology topology,
                                   int vertexCount, int instanceCount, MaterialPropertyBlock properties)
        {
            RequireInitialized();
            Trace("  DrawProcedural");

            if (ReferenceEquals(material, null))
                throw new ArgumentNullException(nameof(material));
            if (vertexCount < 0)
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            if (instanceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(instanceCount));
            if (!m_ViewProjectionSet)
                throw new InvalidOperationException("WebGL2Backend.DrawProcedural: no SetViewProjection preceded this draw.");

            Shader shader = material.shader;

            if (ReferenceEquals(shader, null))
                throw new InvalidOperationException($"WebGL2Backend.DrawProcedural: material '{material.name}' has no shader.");

            string shaderName = shader.name;

            if (!IsPortedShader(shaderName))
            {
                throw new NotSupportedException(
                    $"WebGL2Backend.DrawProcedural: shader '{shaderName}' is not ported. The procedural draw " +
                    "path itself is live — vertex-less draw over gl_VertexID, pass selection, instancing — but " +
                    "the program is not, and its uniforms need slots in UniformSlots (see the note on Blit).");
            }

            pass = NormalizePass(pass);

            if (!m_ResolvedShaders.Contains(shaderName) && !ResolveShader(shader))
                throw new InvalidOperationException($"WebGL2Backend.DrawProcedural: '{shaderName}' failed to resolve.");

            if (vertexCount == 0 || instanceCount == 0)
                return;

            BuildUniformBlock(material, properties, model, shaderName);

            m_ProceduralInfo[0] = (int)topology;
            m_ProceduralInfo[1] = vertexCount;
            m_ProceduralInfo[2] = instanceCount;
            m_ProceduralInfo[3] = TextureId(material, properties, IdMainTex);
            m_ProceduralInfo[4] = TextureId(material, properties, IdTextureMask0);
            m_ProceduralInfo[5] = TextureId(material, properties, IdTextureMask1);
            m_ProceduralInfo[6] = pass;

            Interop.DrawProcedural(shaderName, m_ProceduralInfo, MemoryMarshal.AsBytes(new Span<float>(m_Uniforms)));
        }
        public void CopyTexture(Texture source, Texture destination)
        {
            RequireInitialized();

            if (ReferenceEquals(source, null))
                throw new ArgumentNullException(nameof(source));
            if (ReferenceEquals(destination, null))
                throw new ArgumentNullException(nameof(destination));

            Interop.CopyTexture(source.GetInstanceID(), destination.GetInstanceID());
        }
        static int NormalizePass(int pass)
        {
            return pass < 0 ? 0 : pass;
        }

        void RequireInitialized()
        {
            if (!m_Initialized)
            {
                throw new InvalidOperationException(
                    "WebGL2Backend was used before CreateAsync completed. The JavaScript module has to be imported " +
                    "and the WebGL2 context created before any backend call.");
            }
        }
        internal static partial class Interop
        {
            public const string ModuleName = "nowui-gl";

            [JSImport("registerShader", ModuleName)]
            public static partial void RegisterShader(string name, int pass, string vertex, string fragment);

            [JSImport("init", ModuleName)]
            public static partial string Init(string canvasSelector);

            [JSImport("resolveShader", ModuleName)]
            public static partial int ResolveShader(string name);

            [JSImport("beginFrame", ModuleName)]
            public static partial void BeginFrame(int frameCount);

            [JSImport("endFrame", ModuleName)]
            public static partial void EndFrame();

            [JSImport("setViewport", ModuleName)]
            public static partial void SetViewport(int x, int y, int width, int height);

            [JSImport("clearTarget", ModuleName)]
            public static partial void ClearTarget(bool clearColor, float r, float g, float b, float a);

            [JSImport("uploadTexture", ModuleName)]
            public static partial void UploadTexture(
                int id,
                [JSMarshalAs<JSType.MemoryView>] Span<int> info,
                [JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

            [JSImport("updateSampler", ModuleName)]
            public static partial void UpdateSampler(int id, [JSMarshalAs<JSType.MemoryView>] Span<int> info);

            [JSImport("releaseTexture", ModuleName)]
            public static partial void ReleaseTexture(int id);

            [JSImport("uploadMesh", ModuleName)]
            public static partial void UploadMesh(
                int id,
                int vertexCount,
                [JSMarshalAs<JSType.MemoryView>] Span<int> header,
                [JSMarshalAs<JSType.MemoryView>] Span<byte> payload);

            [JSImport("releaseMesh", ModuleName)]
            public static partial void ReleaseMesh(int id);

            [JSImport("draw", ModuleName)]
            public static partial void Draw(
                int meshId,
                string shaderName,
                [JSMarshalAs<JSType.MemoryView>] Span<int> info,
                [JSMarshalAs<JSType.MemoryView>] Span<byte> uniforms);
            [JSImport("setSdfUniforms", ModuleName)]
            public static partial void SetSdfUniforms([JSMarshalAs<JSType.MemoryView>] Span<byte> uniforms);

            [JSImport("createRenderTexture", ModuleName)]
            public static partial int CreateRenderTexture(int id, [JSMarshalAs<JSType.MemoryView>] Span<int> info);

            [JSImport("isRenderTextureLost", ModuleName)]
            public static partial int IsRenderTextureLost(int id);

            [JSImport("releaseRenderTexture", ModuleName)]
            public static partial void ReleaseRenderTexture(int id);

            [JSImport("setRenderTarget", ModuleName)]
            public static partial void SetRenderTarget(int id, int mipLevel, int depthSlice);

            [JSImport("blit", ModuleName)]
            public static partial void Blit(
                string shaderName,
                [JSMarshalAs<JSType.MemoryView>] Span<int> info,
                [JSMarshalAs<JSType.MemoryView>] Span<byte> uniforms);

            [JSImport("drawProcedural", ModuleName)]
            public static partial void DrawProcedural(
                string shaderName,
                [JSMarshalAs<JSType.MemoryView>] Span<int> info,
                [JSMarshalAs<JSType.MemoryView>] Span<byte> uniforms);

            [JSImport("copyTexture", ModuleName)]
            public static partial void CopyTexture(int sourceId, int destinationId);

            [JSImport("getError", ModuleName)]
            public static partial int GetError();
        }
    }
}
