using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NowUI.Engine;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using GLFWBindingsContext = OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext;
using UnityEngine;
using UnityEngine.Rendering;
using GL = OpenTK.Graphics.OpenGL4.GL;
using Shader = UnityEngine.Shader;

namespace NowUI.Desktop
{
    /// <summary>
    /// Native OpenGL preview renderer for the engine-free C# runtime. Owns a context and an RGBA8 framebuffer.
    /// The default constructor keeps its window hidden; interactive hosts can explicitly request a visible window.
    /// Stock 2D shaders implement the shared NowUI material contracts, including glass and SDF scenes.
    /// Construct, draw, read back and dispose on the same thread. Requires a desktop graphics session/driver.
    /// </summary>
    public sealed class DesktopRenderBackend : INowRenderBackend, IDisposable
    {
        static readonly Dictionary<string, string> ShaderFiles = new(StringComparer.Ordinal)
        {
            ["NowUI/UI Rectangle"] = "nowui-rectangle",
            ["NowUI/Text Renderer"] = "nowui-text",
            ["NowUI/UI Gradient"] = "nowui-gradient",
            ["NowUI/UI Ripple"] = "nowui-ripple",
            ["NowUI/UI Bezier"] = "nowui-bezier",
            ["NowUI/Color Picker"] = "nowui-colorpicker",
            ["NowUI/UI Glass"] = "nowui-glass",
            ["Hidden/NowUI/GlassBlur"] = "nowui-glassblur",
            ["NowUI/SDF Scene"] = "nowui-sdf",
            ["Hidden/NowUI/SDF Image Field"] = "nowui-sdf-image",
            ["Hidden/NowUI/Desktop Copy"] = "nowui-copy",
        };

        sealed class GpuMesh
        {
            public int vao, vertices, indices;
            public uint version;
            public bool uploaded;
            public int vertexStride;
            public readonly int[] attributes = new int[18];
        }

        sealed class GpuTexture
        {
            public int handle, framebuffer;
            public int width, height;
            public uint version;
            public bool hasMips;
        }

        sealed class Uniform
        {
            public string name;
            public int id, location, size;
            public ActiveUniformType type;
            public float[] scratch;
        }

        sealed class GpuProgram
        {
            public int handle;
            public bool straightAlpha, replace;
            public readonly List<Uniform> uniforms = new();
        }

        readonly int ownerThread = Environment.CurrentManagedThreadId;
        const long MaximumOutputPixels = 16_777_216;
        readonly bool visible;
        readonly ColorSpace colorSpace;
        int width, height;
        readonly Dictionary<int, GpuMesh> meshes = new();
        readonly Dictionary<int, GpuTexture> textures = new();
        readonly Dictionary<(string shader, int pass), GpuProgram> programs = new();
        readonly List<Vector3> positions = new();
        readonly List<Vector4>[] uvs = new List<Vector4>[8];
        readonly float[] matrixScratch = new float[16];
        readonly NowMaterialBag emptyMaterial = new();
        float[] vertexScratch = Array.Empty<float>();
        NativeWindow window;
        GpuTexture output;
        int whiteTexture, boundFramebuffer, blitVao, blitVertices;
        Rect viewport;
        bool disposed;
        Matrix4x4 view = Matrix4x4.identity, projection = Matrix4x4.identity;

        public NowRenderCaps caps { get; private set; }
        public string RendererDescription { get; private set; }

        /// <summary>The native window, for input subscriptions. Access it only on the renderer's creating thread.</summary>
        public NativeWindow Window
        {
            get
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return window;
            }
        }

        /// <summary>Current output width in framebuffer pixels, which may differ from the window's client width.</summary>
        public int Width => width;

        /// <summary>Current output height in framebuffer pixels, which may differ from the window's client height.</summary>
        public int Height => height;

        public bool IsClosing => disposed || window == null || !window.Exists || window.IsExiting;

        /// <summary>A minimized or zero-sized visible window should be skipped by the host's rendering loop.</summary>
        public bool IsMinimized => !IsClosing && visible && (window.WindowState == WindowState.Minimized
            || window.FramebufferSize.X <= 0 || window.FramebufferSize.Y <= 0);

        /// <summary>Creates the original hidden PNG-capture host.</summary>
        public DesktopRenderBackend(int width, int height) : this(width, height, false)
        {
        }

        public DesktopRenderBackend(int width, int height, bool visible, string title = "NowUI preview", ColorSpace colorSpace = ColorSpace.Gamma)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Capture dimensions must be positive.");
            if ((long)width * height > MaximumOutputPixels)
                throw new ArgumentOutOfRangeException(nameof(width), $"Output may contain at most {MaximumOutputPixels:N0} pixels.");
            this.visible = visible;
            if (colorSpace != ColorSpace.Gamma && colorSpace != ColorSpace.Linear)
                throw new ArgumentOutOfRangeException(nameof(colorSpace));
            this.colorSpace = colorSpace;
            this.width = width;
            this.height = height;
            for (int i = 0; i < uvs.Length; ++i) uvs[i] = new List<Vector4>();
            try
            {
                window = new NativeWindow(new NativeWindowSettings
                {
                    Title = visible ? title ?? "NowUI preview" : "NowUI offscreen renderer",
                    StartVisible = visible,
                    StartFocused = visible,
                    ClientSize = visible ? new OpenTK.Mathematics.Vector2i(width, height) : new OpenTK.Mathematics.Vector2i(1, 1),
                    Vsync = visible ? VSyncMode.On : VSyncMode.Off,
                    API = ContextAPI.OpenGL,
                    APIVersion = new Version(3, 3),
                    Profile = ContextProfile.Core,
                    Flags = ContextFlags.ForwardCompatible,
                });
                window.Context.MakeCurrent();
                GL.LoadBindings(new GLFWBindingsContext());
                RendererDescription = $"{GL.GetString(StringName.Renderer)} / OpenGL {GL.GetString(StringName.Version)}";
                int maximum = GL.GetInteger(GetPName.MaxTextureSize);
                if (width > maximum || height > maximum)
                    throw new ArgumentOutOfRangeException(nameof(width), $"Capture exceeds GPU texture limit {maximum}.");
                caps = new NowRenderCaps(maximum, 1, false, false, false,
                    true, true, true, true, true, true, true, true, false, true,
                    colorSpace, RendererDescription, GraphicsDeviceType.OpenGLCore, 0);
                if (visible && window.FramebufferSize.X > 0 && window.FramebufferSize.Y > 0)
                {
                    // A HiDPI window's requested client dimensions are not its rendering dimensions.
                    this.width = window.FramebufferSize.X;
                    this.height = window.FramebufferSize.Y;
                }
                if ((long)this.width * this.height > MaximumOutputPixels)
                    throw new ArgumentOutOfRangeException(nameof(width), $"Output may contain at most {MaximumOutputPixels:N0} pixels.");
                output = CreateTarget(this.width, this.height, colorSpace == ColorSpace.Linear ? PixelInternalFormat.Srgb8Alpha8 : PixelInternalFormat.Rgba8);
                whiteTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, whiteTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 1, 1, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, new byte[] { 255, 255, 255, 255 });
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                BindFramebuffer(output.framebuffer);
                SetColorWrite();
                GL.Disable(EnableCap.DepthTest);
                GL.Disable(EnableCap.CullFace);
                GL.Disable(EnableCap.ScissorTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendEquation(BlendEquationMode.FuncAdd);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
                CreateBlitQuad();
                CheckError("creating the offscreen renderer");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Starts a native input frame and polls pending events without blocking. Visible output is resized after
        /// the callbacks, so hosts can then update their screen/input geometry from Width and Height.
        /// </summary>
        public void ProcessEvents()
        {
            RequireContext();
            if (IsClosing) return;
            window.NewInputFrame();
            NativeWindow.ProcessWindowEvents(waitForEvents: false);
            if (visible && !IsClosing && !IsMinimized)
            {
                var size = window.FramebufferSize;
                ResizeOutput(size.X, size.Y);
            }
        }

        /// <summary>
        /// Presents the current offscreen frame in a visible window. Hidden capture hosts need not call this.
        /// VSync limits the visible host's swap rate; minimize/zero-size leaves the previous output intact.
        /// </summary>
        public void Present()
        {
            RequireContext();
            if (!visible || IsClosing || IsMinimized) return;
            var size = window.FramebufferSize;
            int previous = boundFramebuffer;
            try
            {
                // The output attachment already stores display-encoded bytes. Window presentation copies them.
                GL.Disable(EnableCap.FramebufferSrgb);
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, output.framebuffer);
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                GL.DrawBuffer(DrawBufferMode.Back);
                GL.BlitFramebuffer(0, 0, width, height, 0, 0, size.X, size.Y,
                    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
                CheckError("presenting the native preview");
                window.Context.SwapBuffers();
            }
            finally { BindFramebuffer(previous); SetColorWrite(); }
        }

        /// <summary>Requests normal window closure; the graphics context stays alive until Dispose.</summary>
        public void Close()
        {
            RequireContext();
            if (!IsClosing) window.Close();
        }

        // Replace atomically, preserving a bound offscreen target and leaving the previous output alive if
        // allocation fails. Zero framebuffer dimensions during minimize never reach this method.
        internal void ResizeOutput(int newWidth, int newHeight)
        {
            RequireContext();
            if (newWidth <= 0 || newHeight <= 0 || (long)newWidth * newHeight > MaximumOutputPixels)
                throw new ArgumentOutOfRangeException(nameof(newWidth), $"Output dimensions must be positive and contain at most {MaximumOutputPixels:N0} pixels.");
            if (width == newWidth && height == newHeight) return;
            var replacement = CreateTarget(newWidth, newHeight, colorSpace == ColorSpace.Linear ? PixelInternalFormat.Srgb8Alpha8 : PixelInternalFormat.Rgba8);
            var previous = output;
            bool outputWasBound = boundFramebuffer == previous.framebuffer;
            output = replacement;
            width = newWidth;
            height = newHeight;
            if (outputWasBound) BindFramebuffer(replacement.framebuffer);
            DeleteTexture(previous);
        }

        public void BeginFrame(int frameCount)
        {
            RequireContext();
            if (NowRuntime.colorSpace != colorSpace)
                throw new InvalidOperationException("The native renderer and NowRuntime must use the same color space.");
            BindFramebuffer(output.framebuffer);
            SetColorWrite();
            SetViewport(new Rect(0, 0, width, height));
        }

        public void EndFrame()
        {
            RequireContext();
            GL.Flush();
            CheckError("ending the frame");
        }

        /// <summary>
        /// Reads tightly packed, straight-alpha RGBA pixels suitable for PNG encoding. Row zero is the bottom
        /// row. The GPU's premultiplied RGB is unpremultiplied; fully transparent pixels have zero RGB.
        /// </summary>
        public byte[] ReadPixelsRgbaBottomUp()
        {
            RequireContext();
            int previous = boundFramebuffer;
            try
            {
                BindFramebuffer(output.framebuffer);
                var pixels = new byte[checked(width * height * 4)];
                GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                CheckError("reading capture pixels");
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int alpha = pixels[i + 3];
                    if (alpha == 255) continue;
                    for (int channel = 0; channel < 3; ++channel)
                    {
                        if (alpha == 0) pixels[i + channel] = 0;
                        else if (colorSpace == ColorSpace.Linear)
                        {
                            // sRGB attachments encode premultiplied LINEAR RGB. Divide in that working space.
                            float encoded = pixels[i + channel] / 255f;
                            float linear = encoded <= .04045f ? encoded / 12.92f : MathF.Pow((encoded + .055f) / 1.055f, 2.4f);
                            linear = Math.Clamp(linear * 255f / alpha, 0f, 1f);
                            encoded = linear <= .0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1f / 2.4f) - .055f;
                            pixels[i + channel] = (byte)Math.Clamp((int)MathF.Round(encoded * 255f), 0, 255);
                        }
                        else pixels[i + channel] = (byte)Math.Min(255, (pixels[i + channel] * 255 + alpha / 2) / alpha);
                    }
                }
                return pixels;
            }
            finally { BindFramebuffer(previous); }
        }

        public unsafe void UploadTexture2D(Texture2D texture, ReadOnlySpan<byte> pixels, RectInt dirtyRect, bool generateMips)
        {
            RequireContext();
            var (internalFormat, pixelFormat, pixelType) = texture.format switch
            {
                TextureFormat.RGBA32 => (colorSpace == ColorSpace.Linear && !texture.isLinear ? PixelInternalFormat.Srgb8Alpha8 : PixelInternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte),
                TextureFormat.R8 => (PixelInternalFormat.R8, PixelFormat.Red, PixelType.UnsignedByte),
                TextureFormat.RHalf => (PixelInternalFormat.R16f, PixelFormat.Red, PixelType.HalfFloat),
                TextureFormat.RFloat => (PixelInternalFormat.R32f, PixelFormat.Red, PixelType.Float),
                TextureFormat.RGHalf => (PixelInternalFormat.Rg16f, PixelFormat.Rg, PixelType.HalfFloat),
                TextureFormat.RGFloat => (PixelInternalFormat.Rg32f, PixelFormat.Rg, PixelType.Float),
                TextureFormat.RGBAHalf => (PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat),
                TextureFormat.RGBAFloat => (PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float),
                _ => throw Unsupported($"texture format {texture.format} on '{texture.name}'", "RGBA32, R8, and one-, two-, or four-channel floating-point textures"),
            };
            int needed = checked(texture.width * texture.height * Texture2D.BytesPerPixel(texture.format));
            if (pixels.Length < needed) throw new ArgumentException("Texture pixel buffer is shorter than its dimensions.");
            if (!textures.TryGetValue(texture.GetInstanceID(), out var gpu))
            {
                gpu = new GpuTexture { handle = GL.GenTexture() };
                textures.Add(texture.GetInstanceID(), gpu);
            }
            GL.BindTexture(TextureTarget.Texture2D, gpu.handle);
            bool allocated = gpu.width == texture.width && gpu.height == texture.height;
            bool fullRect = dirtyRect.x <= 0 && dirtyRect.y <= 0 &&
                dirtyRect.width >= texture.width && dirtyRect.height >= texture.height;
            bool partial = !fullRect && dirtyRect.width > 0 && dirtyRect.height > 0;
            // Upload only the base level; GenerateMipmap produces the remaining levels when requested.
            fixed (byte* pointer = pixels)
            {
                if (!partial)
                {
                    GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, texture.width, texture.height,
                        0, pixelFormat, pixelType, (IntPtr)pointer);
                }
                else
                {
                    // A dirty region (a font page gaining a band of glyphs) sends only
                    // those texels. A texture seen for the first time is allocated
                    // without data: texels outside the regions written are never sampled.
                    if (!allocated)
                        GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, texture.width, texture.height,
                            0, pixelFormat, pixelType, IntPtr.Zero);

                    GL.PixelStore(PixelStoreParameter.UnpackRowLength, texture.width);
                    GL.PixelStore(PixelStoreParameter.UnpackSkipPixels, dirtyRect.x);
                    GL.PixelStore(PixelStoreParameter.UnpackSkipRows, dirtyRect.y);
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, dirtyRect.x, dirtyRect.y, dirtyRect.width, dirtyRect.height,
                        pixelFormat, pixelType, (IntPtr)pointer);
                    GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
                    GL.PixelStore(PixelStoreParameter.UnpackSkipPixels, 0);
                    GL.PixelStore(PixelStoreParameter.UnpackSkipRows, 0);
                }
            }
            gpu.width = texture.width;
            gpu.height = texture.height;
            gpu.version = texture.version;
            gpu.hasMips = generateMips && texture.mipmapCount > 1;
            if (gpu.hasMips) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            SetSampler(texture, gpu);
            CheckError($"uploading texture '{texture.name}'");
        }

        public void UpdateSampler(Texture texture)
        {
            RequireContext();
            if (textures.TryGetValue(texture.GetInstanceID(), out var gpu)) SetSampler(texture, gpu);
        }

        static void SetSampler(Texture texture, GpuTexture gpu)
        {
            GL.BindTexture(TextureTarget.Texture2D, gpu.handle);
            var min = texture.filterMode == FilterMode.Point ? TextureMinFilter.Nearest : TextureMinFilter.Linear;
            if (gpu.hasMips)
                min = texture.filterMode == FilterMode.Point ? TextureMinFilter.NearestMipmapNearest
                    : texture.filterMode == FilterMode.Trilinear ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.LinearMipmapNearest;
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)(texture.filterMode == FilterMode.Point ? TextureMagFilter.Nearest : TextureMagFilter.Linear));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)Wrap(texture.wrapModeU));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)Wrap(texture.wrapModeV));
        }

        static OpenTK.Graphics.OpenGL4.TextureWrapMode Wrap(UnityEngine.TextureWrapMode mode) => mode switch
        {
            UnityEngine.TextureWrapMode.Clamp => OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge,
            UnityEngine.TextureWrapMode.Mirror => OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
            UnityEngine.TextureWrapMode.Repeat => OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat,
            _ => throw Unsupported($"texture wrapping {mode}", "Clamp, Mirror and Repeat"),
        };

        public bool CreateRenderTexture(RenderTexture texture, in NowRenderTextureRequest request)
        {
            RequireContext();
            if (request.dimension != TextureDimension.Tex2D || request.msaaSamples != 1 || request.volumeDepth != 1
                || request.useMipMap || request.enableRandomWrite || request.bindMS || request.depthBits != 0)
                throw Unsupported($"render target {request.format}, {request.dimension}, MSAA {request.msaaSamples}, depth {request.depthBits}",
                    "single-sample color 2D targets without depth or mipmaps");
            ReleaseRenderTexture(texture);
            var format = request.format switch
            {
                RenderTextureFormat.ARGB32 or RenderTextureFormat.Default =>
                    request.readWrite == RenderTextureReadWrite.sRGB || request.readWrite == RenderTextureReadWrite.Default && colorSpace == ColorSpace.Linear
                    ? PixelInternalFormat.Srgb8Alpha8 : PixelInternalFormat.Rgba8,
                RenderTextureFormat.R8 => PixelInternalFormat.R8,
                RenderTextureFormat.RHalf => PixelInternalFormat.R16f,
                RenderTextureFormat.RFloat => PixelInternalFormat.R32f,
                RenderTextureFormat.RGHalf => PixelInternalFormat.Rg16f,
                RenderTextureFormat.RGFloat => PixelInternalFormat.Rg32f,
                RenderTextureFormat.ARGBHalf or RenderTextureFormat.DefaultHDR => PixelInternalFormat.Rgba16f,
                RenderTextureFormat.ARGBFloat => PixelInternalFormat.Rgba32f,
                _ => throw Unsupported($"render-target format {request.format}", "R8 and one-, two-, or four-channel floating-point color targets"),
            };
            var gpu = CreateTarget(request.width, request.height, format);
            textures.Add(texture.GetInstanceID(), gpu);
            SetSampler(texture, gpu);
            return true;
        }

        GpuTexture CreateTarget(int targetWidth, int targetHeight, PixelInternalFormat format = PixelInternalFormat.Rgba8)
        {
            if (targetWidth <= 0 || targetHeight <= 0 || targetWidth > caps.maxTextureSize || targetHeight > caps.maxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(targetWidth), $"Render-target dimensions must be between 1 and {caps.maxTextureSize}.");
            int previous = boundFramebuffer;
            var gpu = new GpuTexture { width = targetWidth, height = targetHeight };
            try
            {
                gpu.handle = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, gpu.handle);
                GL.TexImage2D(TextureTarget.Texture2D, 0, format, targetWidth, targetHeight, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                gpu.framebuffer = GL.GenFramebuffer();
                BindFramebuffer(gpu.framebuffer);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                    TextureTarget.Texture2D, gpu.handle, 0);
                if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                    throw new InvalidOperationException("NowUI offscreen framebuffer is incomplete.");
                return gpu;
            }
            catch { DeleteTexture(gpu); throw; }
            finally { BindFramebuffer(previous); }
        }

        public bool IsRenderTextureLost(RenderTexture texture) => !textures.ContainsKey(texture.GetInstanceID());

        public void ReleaseTexture(Texture texture)
        {
            RequireContext();
            if (textures.Remove(texture.GetInstanceID(), out var gpu))
            {
                if (gpu.framebuffer == boundFramebuffer) BindFramebuffer(output.framebuffer);
                DeleteTexture(gpu);
            }
        }

        public void ReleaseRenderTexture(RenderTexture texture) => ReleaseTexture(texture);

        public void ReleaseMesh(Mesh mesh)
        {
            RequireContext();
            if (meshes.Remove(mesh.GetInstanceID(), out var gpu)) DeleteMesh(gpu);
        }

        // Uniforms are resolved on every draw; there is no material-owned GPU object to release.
        public void ReleaseMaterial(Material material) { RequireContext(); }

        public bool ResolveShader(Shader shader)
        {
            RequireContext();
            GetProgram(shader.name);
            return true;
        }

        public void SetRenderTarget(in NowRenderTarget target)
        {
            RequireContext();
            if (target.mipLevel != 0 || target.face != CubemapFace.Unknown || target.depthSlice > 0)
                throw Unsupported("render-target mip/face/slice selection", "base-level 2D render targets");
            if (target.isBackBuffer) BindFramebuffer(output.framebuffer);
            else if (textures.TryGetValue(target.texture.GetInstanceID(), out var gpu) && gpu.framebuffer != 0)
                BindFramebuffer(gpu.framebuffer);
            else throw new InvalidOperationException($"Render target '{target.texture.name}' has not been created.");
        }

        public void SetViewport(in Rect pixelRect)
        {
            RequireContext();
            viewport = pixelRect;
            GL.Viewport((int)pixelRect.x, (int)pixelRect.y, (int)pixelRect.width, (int)pixelRect.height);
        }

        public void SetViewProjection(in Matrix4x4 view, in Matrix4x4 projection)
        {
            RequireContext();
            this.view = view;
            this.projection = projection;
        }

        public void ClearRenderTarget(bool clearDepth, bool clearColor, in Color color, float depth)
        {
            RequireContext();
            if (clearColor)
            {
                GL.ClearColor(color.r, color.g, color.b, color.a);
                GL.Clear(ClearBufferMask.ColorBufferBit);
            }
            // There is no depth attachment and depth testing is disabled for the supported 2D shaders.
        }

        public void DrawMesh(Mesh mesh, int subMesh, in Matrix4x4 model, Material material, int pass, MaterialPropertyBlock properties)
        {
            RequireContext();
            if (ReferenceEquals(mesh, null) || ReferenceEquals(material, null) || ReferenceEquals(material.shader, null))
                throw new ArgumentException("A draw requires a mesh and a material with a shader.");
            if (pass != 0) throw Unsupported($"shader pass {pass}", "pass zero");
            if ((uint)subMesh >= (uint)mesh.subMeshCount) throw new ArgumentOutOfRangeException(nameof(subMesh));
            var descriptor = mesh.GetSubMesh(subMesh);
            if (descriptor.topology != MeshTopology.Triangles) throw Unsupported($"mesh topology {descriptor.topology}", "triangles");
            var program = GetProgram(material.shader.name, pass);
            var gpu = UploadMesh(mesh);
            GL.UseProgram(program.handle);
            SetBlend(program);
            BindUniforms(program, material, properties, projection * view * model);
            GL.BindVertexArray(gpu.vao);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, descriptor.indexCount,
                mesh.indexFormat == IndexFormat.UInt32 ? DrawElementsType.UnsignedInt : DrawElementsType.UnsignedShort,
                (IntPtr)(descriptor.indexStart * NowMeshData.IndexSize(mesh.indexFormat)), descriptor.baseVertex);
            CheckError("drawing a NowUI mesh");
        }

        GpuMesh UploadMesh(Mesh mesh)
        {
            if (!meshes.TryGetValue(mesh.GetInstanceID(), out var gpu))
            {
                gpu = new GpuMesh { vao = GL.GenVertexArray(), vertices = GL.GenBuffer(), indices = GL.GenBuffer() };
                meshes.Add(mesh.GetInstanceID(), gpu);
            }
            if (gpu.uploaded && gpu.version == mesh.data.version) return gpu;
            Span<int> attributes = stackalloc int[18];
            GL.BindVertexArray(gpu.vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, gpu.vertices);
            if (TryGetInterleavedLayout(mesh.data, attributes))
            {
                // The normal NowUI upload already owns packed vertex bytes. Bind that exact
                // layout instead of de-interleaving nine lists and packing them again.
                GL.BufferData(BufferTarget.ArrayBuffer, checked(mesh.vertexCount * mesh.data.vertexStride),
                    mesh.data.vertexBytes, BufferUsageHint.DynamicDraw);
                ConfigureMeshLayout(gpu, mesh.data.vertexStride, attributes);
            }
            else if (TryGetSeparateLayout(mesh.data, attributes, out int byteCount))
            {
                // Legacy/effect/Lottie meshes already hold contiguous float channels.
                // Put those channels next to one another in the VBO and bind each range
                // with tightly packed stride, avoiding another per-vertex conversion.
                GL.BufferData(BufferTarget.ArrayBuffer, byteCount, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                for (int i = 0; i < 9; ++i)
                {
                    var channel = i == 0 ? VertexAttribute.Position : VertexAttribute.TexCoord0 + i - 1;
                    ref var stream = ref mesh.data.streams[(int)channel];
                    GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)attributes[i * 2],
                        checked(mesh.vertexCount * stream.elementSize), stream.bytes);
                }
                ConfigureMeshLayout(gpu, 0, attributes);
            }
            else UploadConvertedVertices(mesh, gpu, attributes);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, gpu.indices);
            GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.data.indexCount * NowMeshData.IndexSize(mesh.indexFormat),
                mesh.data.indexBytes, BufferUsageHint.DynamicDraw);
            gpu.version = mesh.data.version;
            gpu.uploaded = true;
            return gpu;
        }

        static bool TryGetInterleavedLayout(NowMeshData data, Span<int> attributes)
        {
            if (!data.interleaved || data.vertexBytes == null) return false;
            for (int i = 0; i < 9; ++i)
            {
                var channel = i == 0 ? VertexAttribute.Position : VertexAttribute.TexCoord0 + i - 1;
                if (!data.TryGetAttribute(channel, out int offset, out var format, out int dimension)
                    || format != VertexAttributeFormat.Float32) return false;
                // Stock shaders consume Position.xyz, UV0.xy and full vectors for UV1..7.
                // Other layouts retain the existing readback conversion and zero-extension rules.
                if (i == 0 ? dimension != 3 : i == 1 ? dimension < 2 || dimension > 4 : dimension != 4)
                    return false;
                attributes[i * 2] = offset;
                attributes[i * 2 + 1] = dimension;
            }
            return true;
        }

        static bool TryGetSeparateLayout(NowMeshData data, Span<int> attributes, out int byteCount)
        {
            byteCount = 0;
            if (data.interleaved) return false;
            for (int i = 0; i < 9; ++i)
            {
                var channel = i == 0 ? VertexAttribute.Position : VertexAttribute.TexCoord0 + i - 1;
                ref var stream = ref data.streams[(int)channel];
                int dimension = stream.elementSize / sizeof(float);
                if (stream.bytes == null || stream.count != data.vertexCount || stream.elementSize % sizeof(float) != 0
                    || (i == 0 ? dimension != 3 : i == 1 ? dimension < 2 || dimension > 4 : dimension != 4))
                    return false;
                attributes[i * 2] = byteCount;
                attributes[i * 2 + 1] = dimension;
                byteCount = checked(byteCount + data.vertexCount * stream.elementSize);
            }
            return true;
        }

        static void ConfigureMeshLayout(GpuMesh gpu, int stride, ReadOnlySpan<int> attributes)
        {
            if (gpu.vertexStride == stride && attributes.SequenceEqual(gpu.attributes)) return;
            for (int i = 0; i < 9; ++i)
            {
                GL.EnableVertexAttribArray(i);
                GL.VertexAttribPointer(i, attributes[i * 2 + 1], VertexAttribPointerType.Float,
                    false, stride, attributes[i * 2]);
            }
            attributes.CopyTo(gpu.attributes);
            gpu.vertexStride = stride;
        }

        void UploadConvertedVertices(Mesh mesh, GpuMesh gpu, Span<int> attributes)
        {
            mesh.GetVertices(positions);
            for (int i = 0; i < uvs.Length; ++i)
            {
                mesh.GetUVs(i, uvs[i]);
                if (uvs[i].Count != mesh.vertexCount)
                    throw new InvalidOperationException($"Mesh '{mesh.name}' is missing the full NowUI UV{i} stream.");
            }
            if (positions.Count != mesh.vertexCount) throw new InvalidOperationException("Mesh has an incomplete position stream.");
            const int floatsPerVertex = 35;
            int floatCount = checked(mesh.vertexCount * floatsPerVertex);
            if (vertexScratch.Length < floatCount) Array.Resize(ref vertexScratch, Math.Max(floatCount, vertexScratch.Length * 2));
            int cursor = 0;
            for (int vertex = 0; vertex < mesh.vertexCount; ++vertex)
            {
                Vector3 p = positions[vertex];
                vertexScratch[cursor++] = p.x; vertexScratch[cursor++] = p.y; vertexScratch[cursor++] = p.z;
                for (int stream = 0; stream < uvs.Length; ++stream)
                {
                    Vector4 v = uvs[stream][vertex];
                    vertexScratch[cursor++] = v.x; vertexScratch[cursor++] = v.y;
                    vertexScratch[cursor++] = v.z; vertexScratch[cursor++] = v.w;
                }
            }
            GL.BufferData(BufferTarget.ArrayBuffer, floatCount * sizeof(float), vertexScratch, BufferUsageHint.DynamicDraw);
            for (int i = 0; i < 9; ++i)
            {
                attributes[i * 2] = (i == 0 ? 0 : 3 + (i - 1) * 4) * sizeof(float);
                attributes[i * 2 + 1] = i == 0 ? 3 : 4;
            }
            ConfigureMeshLayout(gpu, floatsPerVertex * sizeof(float), attributes);
        }

        void BindUniforms(GpuProgram program, Material material, MaterialPropertyBlock properties, Matrix4x4 mvp,
            Texture blitSource = null, Vector4? blitTransform = null)
        {
            var bag = material?.bag ?? emptyMaterial;
            var block = properties?.bag;
            var globals = NowRuntime.globals;
            int unit = 0;
            foreach (var uniform in program.uniforms)
            {
                int id = uniform.id;
                switch (uniform.type)
                {
                    case ActiveUniformType.FloatMat4:
                        WriteMatrix(uniform.name == "nowui_MatrixMVP" ? mvp
                            : Resolve(id, block?.matrices, bag.matrices, globals.matrices), matrixScratch);
                        GL.UniformMatrix4(uniform.location, 1, false, matrixScratch);
                        break;
                    case ActiveUniformType.Float:
                        float number = uniform.name == "nowui_Time" ? Time.time : Resolve(id, block?.floats, bag.floats, globals.floats);
                        GL.Uniform1(uniform.location, number);
                        break;
                    case ActiveUniformType.FloatVec2:
                    case ActiveUniformType.FloatVec3:
                        var vector = Resolve(id, block?.vectors, bag.vectors, globals.vectors);
                        if (uniform.type == ActiveUniformType.FloatVec2) GL.Uniform2(uniform.location, vector.x, vector.y);
                        else GL.Uniform3(uniform.location, vector.x, vector.y, vector.z);
                        break;
                    case ActiveUniformType.FloatVec4:
                        if (uniform.size > 1)
                        {
                            var values = Resolve(id, block?.vectorArrays, bag.vectorArrays, globals.vectorArrays);
                            Array.Clear(uniform.scratch);
                            if (values != null)
                            {
                                if (values.Length > uniform.size) throw new InvalidOperationException($"Uniform {uniform.name} exceeds its shader capacity.");
                                for (int i = 0; i < values.Length; ++i) WriteVector(values[i], uniform.scratch, i * 4);
                            }
                            GL.Uniform4(uniform.location, uniform.size, uniform.scratch);
                        }
                        else
                        {
                            var value = Resolve(id, block?.vectors, bag.vectors, globals.vectors);
                            if (uniform.name == "_MainTex_ST")
                            {
                                if (blitTransform.HasValue) value = blitTransform.Value;
                                else if (!Has(id, block?.vectors, bag.vectors, globals.vectors)) value = new Vector4(1, 1, 0, 0);
                            }
                            else if (uniform.name == "_Time") value = new Vector4(Time.time / 20f, Time.time, Time.time * 2f, Time.time * 3f);
                            GL.Uniform4(uniform.location, value.x, value.y, value.z, value.w);
                        }
                        break;
                    case ActiveUniformType.Sampler2D:
                        Texture texture = uniform.name == "_MainTex" && !ReferenceEquals(blitSource, null)
                            ? blitSource : Resolve(id, block?.textures, bag.textures, globals.textures);
                        GL.ActiveTexture(TextureUnit.Texture0 + unit);
                        int handle = EnsureTexture(texture);
                        GL.BindTexture(TextureTarget.Texture2D, handle);
                        GL.Uniform1(uniform.location, unit++);
                        break;
                    default:
                        throw Unsupported($"uniform '{uniform.name}' of type {uniform.type}", "the stock 2D shader uniform types");
                }
            }
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        static T Resolve<T>(int id, Dictionary<int, T> block, Dictionary<int, T> material, Dictionary<int, T> globals)
        {
            if (block != null && block.TryGetValue(id, out var overridden)) return overridden;
            if (material.TryGetValue(id, out var authored)) return authored;
            return globals.TryGetValue(id, out var global) ? global : default;
        }

        static bool Has<T>(int id, Dictionary<int, T> block, Dictionary<int, T> material, Dictionary<int, T> globals)
            => (block != null && block.ContainsKey(id)) || material.ContainsKey(id) || globals.ContainsKey(id);

        int EnsureTexture(Texture texture, bool allowCurrentTarget = false)
        {
            if (ReferenceEquals(texture, null)) return whiteTexture;
            if (texture is Texture2D cpu && (!textures.TryGetValue(texture.GetInstanceID(), out var cached) || cached.version != texture.version))
            {
                if (cpu.pixels == null) throw new InvalidOperationException($"Texture '{texture.name}' has no retained pixels to upload.");
                UploadTexture2D(cpu, cpu.pixels, new RectInt(0, 0, cpu.width, cpu.height), cpu.mipmapCount > 1);
            }
            if (!textures.TryGetValue(texture.GetInstanceID(), out var gpu))
                throw new InvalidOperationException($"Texture '{texture.name}' has no native GPU resource.");
            if (!allowCurrentTarget && gpu.framebuffer != 0 && gpu.framebuffer == boundFramebuffer)
                throw new InvalidOperationException("A draw cannot sample the framebuffer it is currently writing.");
            return gpu.handle;
        }

        GpuProgram GetProgram(string name, int pass = 0)
        {
            var keyName = (name, pass);
            if (programs.TryGetValue(keyName, out var existing)) return existing;
            if (!ShaderFiles.TryGetValue(name, out var file))
                throw Unsupported($"shader '{name}'", "built-in NowUI 2D materials; custom Unity HLSL shaders require Unity");
            string fragmentFile = file;
            if (name == "Hidden/NowUI/SDF Image Field")
                fragmentFile += pass switch { 0 => "-seed", 1 => "-flood", 2 => "-resolve", 3 => "-stamp", 4 => "-dilate",
                    _ => throw Unsupported($"SDF image-field pass {pass}", "passes zero through four") };
            else if (pass != 0) throw Unsupported($"shader '{name}' pass {pass}", "pass zero for flat 2D rendering");
            int vertex = 0, fragment = 0, handle = 0;
            try
            {
                vertex = Compile(ShaderType.VertexShader, ReadShader(file + ".vert"));
                fragment = Compile(ShaderType.FragmentShader, ReadShader(fragmentFile + ".frag"));
                handle = GL.CreateProgram();
                GL.AttachShader(handle, vertex); GL.AttachShader(handle, fragment); GL.LinkProgram(handle);
                GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out int linked);
                if (linked == 0) throw new InvalidOperationException($"Shader '{name}' link failed: {GL.GetProgramInfoLog(handle)}");
                var result = new GpuProgram { handle = handle,
                    straightAlpha = name is "NowUI/UI Glass" or "NowUI/SDF Scene",
                    replace = name is "Hidden/NowUI/GlassBlur" or "Hidden/NowUI/SDF Image Field" or "Hidden/NowUI/Desktop Copy" };
                GL.GetProgram(handle, GetProgramParameterName.ActiveUniforms, out int count);
                for (int i = 0; i < count; ++i)
                {
                    string uniformName = GL.GetActiveUniform(handle, i, out int size, out ActiveUniformType type);
                    string key = uniformName.EndsWith("[0]", StringComparison.Ordinal) ? uniformName[..^3] : uniformName;
                    result.uniforms.Add(new Uniform { name = key, id = Shader.PropertyToID(key),
                        location = GL.GetUniformLocation(handle, uniformName), type = type, size = size,
                        scratch = size > 1 ? new float[size * 4] : null });
                }
                programs.Add(keyName, result);
                return result;
            }
            catch { if (handle != 0) GL.DeleteProgram(handle); throw; }
            finally { if (vertex != 0) GL.DeleteShader(vertex); if (fragment != 0) GL.DeleteShader(fragment); }
        }

        static int Compile(ShaderType type, string source)
        {
            int handle = GL.CreateShader(type);
            GL.ShaderSource(handle, source); GL.CompileShader(handle);
            GL.GetShader(handle, ShaderParameter.CompileStatus, out int compiled);
            if (compiled != 0) return handle;
            string error = GL.GetShaderInfoLog(handle);
            GL.DeleteShader(handle);
            throw new InvalidOperationException($"Native NowUI {type} compilation failed: {error}");
        }

        string ReadShader(string name)
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "Shaders");
            string Read(string file, HashSet<string> seen)
            {
                if (Path.GetFileName(file) != file) throw new InvalidDataException("Shader include must be a filename.");
                if (!seen.Add(file)) return "";
                string source = File.ReadAllText(Path.Combine(directory, file));
                return Regex.Replace(source, "(?m)^\\s*//#include \\\"([^\\\"]+)\\\"\\s*$", match => Read(match.Groups[1].Value, seen));
            }
            string resolved = Read(name, new HashSet<string>(StringComparer.Ordinal));
            resolved = Regex.Replace(resolved, "(?m)^#version 300 es\\s*$", "#version 330 core");
            if (colorSpace == ColorSpace.Linear)
                resolved = resolved.Replace("#version 330 core", "#version 330 core\n#define NOWUI_COLORSPACE_LINEAR");
            resolved = Regex.Replace(resolved, "(?m)^\\s*precision\\s+\\w+\\s+\\w+\\s*;\\s*$", "");
            return Regex.Replace(resolved, @"\b(highp|mediump|lowp)\b", "");
        }

        public void DrawProcedural(in Matrix4x4 model, Material material, int pass, MeshTopology topology, int vertexCount,
            int instanceCount, MaterialPropertyBlock properties)
            => throw Unsupported($"procedural draw for '{material?.shader?.name}'", "indexed stock 2D meshes");

        public void Blit(Texture source, in NowRenderTarget destination, Material material, int pass, in Vector2 scale,
            in Vector2 offset, int sourceDepthSlice, int destinationDepthSlice)
        {
            RequireContext();
            if (sourceDepthSlice > 0 || destinationDepthSlice > 0)
                throw Unsupported($"material blit '{material?.shader?.name}' or array slice", "plain 2D color blits");
            SetRenderTarget(destination);
            DrawBlit(source, material, pass, destination.width, destination.height, scale, offset);
        }

        public void CopyTexture(Texture source, Texture destination)
        {
            RequireContext();
            if (source.width != destination.width || source.height != destination.height)
                throw new ArgumentException("CopyTexture requires equal dimensions.");
            if (destination is not RenderTexture || !textures.TryGetValue(destination.GetInstanceID(), out var gpu) || gpu.framebuffer == 0)
                throw Unsupported("copying into a CPU Texture2D", "copying into a created RGBA8 render target");
            int previous = boundFramebuffer;
            try
            {
                BindFramebuffer(gpu.framebuffer);
                DrawBlit(source, null, 0, destination.width, destination.height, Vector2.one, Vector2.zero);
            }
            finally { BindFramebuffer(previous); }
        }

        void DrawBlit(Texture source, Material material, int pass, int targetWidth, int targetHeight, Vector2 scale, Vector2 offset)
        {
            if (ReferenceEquals(source, null)) throw new ArgumentNullException(nameof(source));
            EnsureTexture(source);
            var program = GetProgram(material?.shader?.name ?? "Hidden/NowUI/Desktop Copy", pass);
            Rect previousViewport = viewport;
            try
            {
                SetViewport(new Rect(0, 0, targetWidth, targetHeight));
                GL.UseProgram(program.handle);
                SetBlend(program);
                BindUniforms(program, material, null, Matrix4x4.Ortho(0, 1, 0, 1, -1, 100), source,
                    new Vector4(scale.x, scale.y, offset.x, offset.y));
                GL.BindVertexArray(blitVao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                CheckError("blitting a texture");
            }
            finally { SetViewport(previousViewport); }
        }

        void CreateBlitQuad()
        {
            float[] vertices = [0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1,
                0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 1, 0, 0, 1];
            blitVao = GL.GenVertexArray();
            blitVertices = GL.GenBuffer();
            GL.BindVertexArray(blitVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, blitVertices);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        }

        void SetColorWrite()
        {
            if (colorSpace == ColorSpace.Linear) GL.Enable(EnableCap.FramebufferSrgb);
            else GL.Disable(EnableCap.FramebufferSrgb);
        }

        static void SetBlend(GpuProgram program)
        {
            if (program.replace) { GL.Disable(EnableCap.Blend); return; }
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(program.straightAlpha ? BlendingFactorSrc.SrcAlpha : BlendingFactorSrc.One,
                BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
        }

        void BindFramebuffer(int handle)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, handle);
            boundFramebuffer = handle;
        }

        void RequireContext()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException("DesktopRenderBackend must be used and disposed on its creating thread.");
            if (!window.Context.IsCurrent) window.Context.MakeCurrent();
        }

        static void CheckError(string operation)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError) throw new InvalidOperationException($"OpenGL {error} while {operation}.");
        }

        static NotSupportedException Unsupported(string feature, string supported)
            => new($"NowUI.Desktop does not support {feature}. Supported here: {supported}.");

        static void WriteVector(Vector4 v, float[] target, int at)
        { target[at] = v.x; target[at + 1] = v.y; target[at + 2] = v.z; target[at + 3] = v.w; }

        static void WriteMatrix(Matrix4x4 m, float[] target)
        {
            target[0] = m.m00; target[1] = m.m10; target[2] = m.m20; target[3] = m.m30;
            target[4] = m.m01; target[5] = m.m11; target[6] = m.m21; target[7] = m.m31;
            target[8] = m.m02; target[9] = m.m12; target[10] = m.m22; target[11] = m.m32;
            target[12] = m.m03; target[13] = m.m13; target[14] = m.m23; target[15] = m.m33;
        }

        static void DeleteMesh(GpuMesh gpu)
        { GL.DeleteVertexArray(gpu.vao); GL.DeleteBuffer(gpu.vertices); GL.DeleteBuffer(gpu.indices); }

        static void DeleteTexture(GpuTexture gpu)
        {
            if (gpu.framebuffer != 0) GL.DeleteFramebuffer(gpu.framebuffer);
            if (gpu.handle != 0) GL.DeleteTexture(gpu.handle);
        }

        public void Dispose()
        {
            if (disposed) return;
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException("Dispose the native renderer on its creating thread.");
            disposed = true;
            if (window == null) return;
            try
            {
                window.Context.MakeCurrent();
                foreach (var gpu in meshes.Values) DeleteMesh(gpu);
                foreach (var gpu in textures.Values) DeleteTexture(gpu);
                foreach (var program in programs.Values) GL.DeleteProgram(program.handle);
                if (whiteTexture != 0) GL.DeleteTexture(whiteTexture);
                if (blitVao != 0) GL.DeleteVertexArray(blitVao);
                if (blitVertices != 0) GL.DeleteBuffer(blitVertices);
                if (output != null) DeleteTexture(output);
                meshes.Clear(); textures.Clear(); programs.Clear();
            }
            finally { window.Dispose(); window = null; }
        }
    }
}
