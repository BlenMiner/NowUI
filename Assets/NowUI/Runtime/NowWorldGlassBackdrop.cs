using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if NOWUI_XR
using UnityEngine.XR;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NowUI
{
    public enum NowWorldGlassBackdropMode
    {
        TintOnly = 0,
        Camera = 1,
        CameraAndWorld = 4
    }

    public static class NowWorldGlassBackdrop
    {
        static readonly int _useBackdropId = Shader.PropertyToID("_NowGlassUseBackdrop");

        static readonly Dictionary<Camera, CameraState> _states = new Dictionary<Camera, CameraState>();

        static readonly List<Camera> _staleCameras = new List<Camera>(4);

        static readonly List<NowWorldGraphic> _worldContributors = new List<NowWorldGraphic>(16);

#if NOWUI_XR
        static readonly List<XRDisplaySubsystem> _xrDisplays = new List<XRDisplaySubsystem>(2);
#endif

        static bool _callbacksRegistered;

        static int _lastCleanupFrame = int.MinValue;

        sealed class CameraState
        {
            public Camera camera;

            public CommandBuffer builtInBuffer;

            public int lastUsedFrame = -1;

            public int lastSceneDepthFrame = -1;

            public long populateSequence;

            public readonly List<RequestState> requests = new List<RequestState>(4);

            public readonly List<SharedBackdropState> sharedBackdrops = new List<SharedBackdropState>(4);

            public bool builtInBufferAttached;
        }

        sealed class SharedBackdropState
        {
            public RenderTexture texture;

            public RenderTexture sharpTexture;

            // A material may still sample the previous allocation until the
            // replacement capture has executed and ApplyReadyTextures binds it.
            public RenderTexture retiredTexture;

            public RenderTexture retiredSharpTexture;

            public bool textureReady;

            public bool sharpTextureReady;

            public int textureReadyFrame = -1;

            public int sharpTextureReadyFrame = -1;

            public int texturePendingFrame = -1;

            public int sharpTexturePendingFrame = -1;

            public int width;

            public int height;

            public int lastUsedFrame = -1;

            public long lastPopulatedSequence = -1;

            public long lastSharpPopulatedSequence = -1;

            public NowWorldGlassBackdropMode mode;

            public float blurRadius;

            public NowGlassBlurQuality quality;
        }

        sealed class RequestState
        {
            public NowWorldGraphic requester;

            public RenderTexture backdrop;

            public RenderTexture source;

            public RenderTexture retiredBackdrop;

            public RenderTexture retiredSource;

            public bool backdropReady;

            public bool sourceReady;

            public int backdropReadyFrame = -1;

            public int sourceReadyFrame = -1;

            public int backdropPendingFrame = -1;

            public int sourcePendingFrame = -1;

            public int width;

            public int height;

            public int lastUsedFrame = -1;

            public NowWorldGlassBackdropMode mode;

            public float blurRadius;

            public NowGlassBlurQuality quality = NowGlassBlurQuality.Balanced;

            public float requesterDepth;

            public bool requiresSceneDepth;
        }

        public static void Register(Camera camera, NowWorldGraphic requester, NowWorldGlassBackdropMode mode, float blurRadius)
        {
            Register(camera, requester, mode, blurRadius, NowGlassBlurQuality.Auto, false);
        }

        public static void Register(
            Camera camera,
            NowWorldGraphic requester,
            NowWorldGlassBackdropMode mode,
            float blurRadius,
            bool requiresSceneDepth)
        {
            Register(camera, requester, mode, blurRadius, NowGlassBlurQuality.Auto, requiresSceneDepth);
        }

        public static void Register(
            Camera camera,
            NowWorldGraphic requester,
            NowWorldGlassBackdropMode mode,
            float blurRadius,
            NowGlassBlurQuality quality,
            bool requiresSceneDepth)
        {
            if (camera == null || requester == null)
                return;

            EnsureCallbacks();

            mode = NormalizeMode(mode);

            if (mode == NowWorldGlassBackdropMode.TintOnly)
                return;

            if (requiresSceneDepth)
                RequestSceneDepth(camera);

            if (!_states.TryGetValue(camera, out var state))
            {
                _lastCleanupFrame = int.MinValue;
                CleanupStaleStates();

                state = new CameraState
                {
                    camera = camera
                };
                _states.Add(camera, state);
            }

            int frame = Time.frameCount;

            state.lastUsedFrame = frame;

            var request = GetRequestState(state, requester);

            if (request.lastUsedFrame != frame)
            {
                request.lastUsedFrame = frame;
                request.mode = mode;
                request.blurRadius = Mathf.Max(0f, blurRadius);
                request.quality = NowGlassSettings.Resolve(quality);
                request.requesterDepth = requester.GetCameraDepth(camera);
                request.requiresSceneDepth = requiresSceneDepth;
                return;
            }

            request.mode = CombineModes(request.mode, mode);
            request.blurRadius = Mathf.Max(request.blurRadius, blurRadius);
            request.quality = MaxQuality(request.quality, NowGlassSettings.Resolve(quality));
            request.requesterDepth = requester.GetCameraDepth(camera);
            request.requiresSceneDepth |= requiresSceneDepth;
        }

        public static bool HasRequest(Camera camera)
        {
            CleanupStaleStates();

            return camera != null &&
                _states.TryGetValue(camera, out var state) &&
                IsActiveFrame(state.lastUsedFrame, Time.frameCount) &&
                HasActiveRequest(state, Time.frameCount);
        }

        public static bool RequiresSceneDepth(Camera camera)
        {
            CleanupStaleStates();

            return camera != null &&
                _states.TryGetValue(camera, out var state) &&
                (IsActiveFrame(state.lastSceneDepthFrame, Time.frameCount) ||
                 (IsActiveFrame(state.lastUsedFrame, Time.frameCount) && HasSceneDepthRequest(state, Time.frameCount)));
        }

        public static void RequestSceneDepth(Camera camera)
        {
            if (camera == null)
                return;

            EnsureCallbacks();

            if (!_states.TryGetValue(camera, out var state))
            {
                _lastCleanupFrame = int.MinValue;
                CleanupStaleStates();

                state = new CameraState
                {
                    camera = camera
                };
                _states.Add(camera, state);
            }

            state.lastSceneDepthFrame = Time.frameCount;
            camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        public static bool PopulateCommandBuffer(
            CommandBuffer commandBuffer,
            Camera camera,
            RenderTargetIdentifier source,
            int width,
            int height)
        {
            var sourceDescriptor = GetCameraSourceDescriptor(camera, width, height);

#if NOWUI_XR
            // MultiPass exposes a flat bound-MS intermediate even when the XR
            // provider target is already single-sampled. In SPI, however, the
            // logical CameraTarget is the provider's sampled texture array; keep
            // that descriptor unless the provider itself explicitly sets bindMS.
            bool requiresXrMsaaFallback = TryGetBuiltinXrMsaaSourceSamples(
                camera,
                sourceDescriptor,
                out int sourceMsaaSamples);

            if (RequiresBuiltinXrColorMsaaOverride(
                    requiresXrMsaaFallback,
                    sourceDescriptor.dimension))
            {
                sourceDescriptor.msaaSamples = sourceMsaaSamples;
                sourceDescriptor.bindMS = true;
            }
#endif

            return PopulateCommandBuffer(commandBuffer, camera, source, sourceDescriptor);
        }

        public static bool PopulateCommandBuffer(
            CommandBuffer commandBuffer,
            Camera camera,
            RenderTargetIdentifier source,
            in RenderTextureDescriptor sourceDescriptor)
        {
            return PopulateCommandBuffer(
                commandBuffer,
                camera,
                source,
                sourceDescriptor,
                new Vector4(1f, 1f, 0f, 0f));
        }

        internal static bool PopulateCommandBuffer(
            CommandBuffer commandBuffer,
            Camera camera,
            RenderTargetIdentifier source,
            in RenderTextureDescriptor sourceDescriptor,
            Vector4 sourceScaleOffset)
        {
            if (commandBuffer == null ||
                camera == null ||
                !_states.TryGetValue(camera, out var state) ||
                !IsActiveFrame(state.lastUsedFrame, Time.frameCount))
            {
                return false;
            }

            int width = Mathf.Max(1, sourceDescriptor.width);
            int height = Mathf.Max(1, sourceDescriptor.height);
            var layout = NowGlassTextureLayout.FromDescriptor(sourceDescriptor);
            int frame = Time.frameCount;
            long populateSequence = ++state.populateSequence;
            bool populated = false;

            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (!IsActiveFrame(request.lastUsedFrame, frame) ||
                    request.requester == null ||
                    request.mode == NowWorldGlassBackdropMode.TintOnly)
                {
                    continue;
                }

                bool includeWorld = IncludesWorld(request.mode);
                bool blur = ShouldBlur(request);
                var backdropSource = source;

                if (!includeWorld &&
                    TryGetSharedBackdrop(
                        state,
                        commandBuffer,
                        source,
                        width,
                        height,
                        layout,
                        sourceScaleOffset,
                        populateSequence,
                        request,
                        out var sharedBackdrop,
                        out var sharedSharpBackdrop,
                        out bool sharedBackdropReady))
                {
                    if (sharedBackdropReady)
                        request.requester.ApplyGlassBackdropTexture(sharedBackdrop, sharedSharpBackdrop);

                    populated = true;
                    continue;
                }

                EnsureBackdropTexture(request, width, height, layout);
                bool needsSharpSource = request.requiresSceneDepth && blur;
                var backdropSourceLayout = layout;
                var backdropSourceScaleOffset = sourceScaleOffset;

                if (includeWorld || needsSharpSource)
                {
                    EnsureSourceTexture(request, width, height, layout);
                    NowGlassRenderer.CopyBackdropRegion(
                        commandBuffer,
                        source,
                        request.source,
                        width,
                        height,
                        sourceScaleOffset,
                        layout);

                    if (includeWorld)
                        RenderWorldContributors(commandBuffer, camera, request);

                    backdropSource = request.source;
                    backdropSourceLayout = layout.AsSingleSampled();
                    backdropSourceScaleOffset = new Vector4(1f, 1f, 0f, 0f);
                }

                bool requestBackdropReady =
                    IsTextureReady(request.backdropReady, request.backdropReadyFrame, frame) &&
                    (!needsSharpSource || IsTextureReady(request.sourceReady, request.sourceReadyFrame, frame));

                if (!blur)
                    NowGlassRenderer.CopyBackdropRegion(
                        commandBuffer,
                        backdropSource,
                        request.backdrop,
                        width,
                        height,
                        backdropSourceScaleOffset,
                        backdropSourceLayout);
                else
                    NowGlassRenderer.CopyAndBlurBackdrop(
                        commandBuffer,
                        backdropSource,
                        request.backdrop,
                        width,
                        height,
                        request.blurRadius,
                        request.quality,
                        "World",
                        new NowRect(0f, 0f, width, height),
                        backdropSourceLayout,
                        backdropSourceScaleOffset,
                        out _);

                if (requestBackdropReady)
                {
                    request.requester.ApplyGlassBackdropTexture(
                        request.backdrop,
                        needsSharpSource ? request.source : request.backdrop);
                }

                request.backdropPendingFrame = frame;
                if (includeWorld || needsSharpSource)
                    request.sourcePendingFrame = frame;

                populated = true;
            }

            if (!populated)
                return false;

            return true;
        }

        static bool TryGetSharedBackdrop(
            CameraState state,
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            int width,
            int height,
            in NowGlassTextureLayout layout,
            Vector4 sourceScaleOffset,
            long populateSequence,
            RequestState request,
            out RenderTexture texture,
            out RenderTexture sharpTexture,
            out bool textureReady)
        {
            texture = null;
            sharpTexture = null;
            textureReady = false;

            if (state == null || commandBuffer == null || request == null)
                return false;

            var shared = GetSharedBackdropState(state, request.mode, request.blurRadius, request.quality);
            bool needsSharpTexture = request.requiresSceneDepth && ShouldBlur(request);
            EnsureSharedTexture(shared, width, height, layout, needsSharpTexture);
            bool canApplyTexture =
                IsTextureReady(shared.textureReady, shared.textureReadyFrame, Time.frameCount) &&
                (!needsSharpTexture || IsTextureReady(shared.sharpTextureReady, shared.sharpTextureReadyFrame, Time.frameCount));

            if (shared.lastPopulatedSequence != populateSequence)
            {
                if (!ShouldBlur(request))
                {
                    NowGlassRenderer.CopyBackdropRegion(
                        commandBuffer,
                        source,
                        shared.texture,
                        width,
                        height,
                        sourceScaleOffset,
                        layout);
                }
                else
                {
                    NowGlassRenderer.CopyAndBlurBackdrop(
                        commandBuffer,
                        source,
                        shared.texture,
                        width,
                        height,
                        request.blurRadius,
                        request.quality,
                        "World",
                        new NowRect(0f, 0f, width, height),
                        layout,
                        sourceScaleOffset,
                        out _);
                }

                shared.lastPopulatedSequence = populateSequence;
            }

            shared.lastUsedFrame = Time.frameCount;
            if (needsSharpTexture && shared.lastSharpPopulatedSequence != populateSequence)
            {
                NowGlassRenderer.CopyBackdropRegion(
                    commandBuffer,
                    source,
                    shared.sharpTexture,
                    width,
                    height,
                    sourceScaleOffset,
                    layout);
                shared.lastSharpPopulatedSequence = populateSequence;
            }

            texture = shared.texture;
            sharpTexture = needsSharpTexture ? shared.sharpTexture : shared.texture;
            textureReady = canApplyTexture;
            shared.texturePendingFrame = Time.frameCount;
            if (needsSharpTexture)
                shared.sharpTexturePendingFrame = Time.frameCount;

            return texture != null;
        }

        static SharedBackdropState GetSharedBackdropState(
            CameraState state,
            NowWorldGlassBackdropMode mode,
            float blurRadius,
            NowGlassBlurQuality quality)
        {
            blurRadius = QuantizeSharedBlurRadius(blurRadius);

            for (int i = 0; i < state.sharedBackdrops.Count; ++i)
            {
                var shared = state.sharedBackdrops[i];

                if (shared.mode == mode &&
                    Mathf.Approximately(shared.blurRadius, blurRadius) &&
                    shared.quality == quality)
                {
                    return shared;
                }
            }

            var created = new SharedBackdropState
            {
                mode = mode,
                blurRadius = blurRadius,
                quality = quality
            };
            state.sharedBackdrops.Add(created);
            return created;
        }

        /// <summary>
        /// Shared backdrops are keyed on blur radius; quantizing to quarter-pixel
        /// steps (with all unblurred radii collapsing to zero, matching the
        /// <see cref="ShouldBlur"/> threshold) lets animated radii reuse one
        /// capture instead of re-allocating full-resolution textures every frame.
        /// </summary>
        static float QuantizeSharedBlurRadius(float blurRadius)
        {
            return blurRadius < 0.25f ? 0f : Mathf.Round(blurRadius * 4f) * 0.25f;
        }

        static RequestState GetRequestState(CameraState state, NowWorldGraphic requester)
        {
            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (ReferenceEquals(request.requester, requester))
                    return request;
            }

            var created = new RequestState
            {
                requester = requester
            };
            state.requests.Add(created);
            return created;
        }

        static bool HasActiveRequest(CameraState state, int frame)
        {
            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (IsActiveFrame(request.lastUsedFrame, frame) &&
                    request.requester != null &&
                    request.mode != NowWorldGlassBackdropMode.TintOnly)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasSceneDepthRequest(CameraState state, int frame)
        {
            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (IsActiveFrame(request.lastUsedFrame, frame) &&
                    request.requester != null &&
                    request.requiresSceneDepth &&
                    request.mode != NowWorldGlassBackdropMode.TintOnly)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsActiveFrame(int lastUsedFrame, int frame)
        {
            return lastUsedFrame == frame ||
                (!Application.isPlaying && lastUsedFrame >= frame - 1);
        }

        static bool IsTextureReady(bool ready, int readyFrame, int frame)
        {
            return ready &&
                (Application.isPlaying || readyFrame >= 0 && readyFrame < frame);
        }

        static bool IncludesWorld(NowWorldGlassBackdropMode mode)
        {
            return NormalizeMode(mode) == NowWorldGlassBackdropMode.CameraAndWorld;
        }

        static bool ShouldBlur(RequestState request)
        {
            return request != null && request.blurRadius >= 0.25f;
        }

        static NowWorldGlassBackdropMode CombineModes(NowWorldGlassBackdropMode current, NowWorldGlassBackdropMode requested)
        {
            bool includeWorld = IncludesWorld(current) || IncludesWorld(requested);
            return includeWorld ? NowWorldGlassBackdropMode.CameraAndWorld : NowWorldGlassBackdropMode.Camera;
        }

        internal static NowWorldGlassBackdropMode NormalizeMode(NowWorldGlassBackdropMode mode)
        {
            return (int)mode switch
            {
                0 => NowWorldGlassBackdropMode.TintOnly,
                1 or 2 => NowWorldGlassBackdropMode.Camera,
                3 or 4 => NowWorldGlassBackdropMode.CameraAndWorld,
                _ => NowWorldGlassBackdropMode.CameraAndWorld
            };
        }

        static NowGlassBlurQuality MaxQuality(NowGlassBlurQuality lhs, NowGlassBlurQuality rhs)
        {
            return QualityRank(rhs) > QualityRank(lhs) ? rhs : lhs;
        }

        static int QualityRank(NowGlassBlurQuality quality)
        {
            return quality switch
            {
                NowGlassBlurQuality.Fast => 1,
                NowGlassBlurQuality.Balanced => 2,
                NowGlassBlurQuality.High => 3,
                NowGlassBlurQuality.Ultra => 4,
                _ => 2
            };
        }

        static void RenderWorldContributors(CommandBuffer commandBuffer, Camera camera, RequestState request)
        {
            NowWorldGraphic.CollectBackdropContributors(
                camera,
                request.requester,
                request.requesterDepth,
                _worldContributors);

            if (_worldContributors.Count == 0)
                return;

            if (request.source.dimension == TextureDimension.Tex2DArray)
            {
                commandBuffer.SetRenderTarget(
                    request.source,
                    0,
                    CubemapFace.Unknown,
                    RenderTargetIdentifier.AllDepthSlices);
            }
            else
            {
                commandBuffer.SetRenderTarget(request.source);
            }

            for (int i = 0; i < _worldContributors.Count; ++i)
                _worldContributors[i].DrawBackdropContribution(commandBuffer);

            _worldContributors.Clear();
        }

        public static RenderTextureDescriptor GetCameraSourceDescriptor(
            Camera camera,
            int fallbackWidth,
            int fallbackHeight)
        {
            if (camera != null && camera.targetTexture != null)
                return camera.targetTexture.descriptor;

#if NOWUI_XR
            if (TryGetLiveXrSourceDescriptor(camera, out var xrDescriptor))
            {
                return xrDescriptor;
            }
#endif

            int msaaSamples = CameraCanUseMsaa(camera, SystemInfo.supportsMultisampledTextures)
                ? Mathf.Max(1, QualitySettings.antiAliasing)
                : 1;
            return new RenderTextureDescriptor(
                Mathf.Max(1, fallbackWidth),
                Mathf.Max(1, fallbackHeight),
                RenderTextureFormat.ARGB32,
                0)
            {
                msaaSamples = msaaSamples,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                vrUsage = VRTextureUsage.None
            };
        }

        /// <summary>
        /// Built-in XR can bind an unresolved multisampled depth surface to
        /// _CameraDepthTexture. That resource cannot be declared safely from its
        /// allocation descriptor, so the world-glass shader uses fixed-function
        /// depth testing instead while this exact target is live.
        /// </summary>
        internal static bool SupportsSceneDepthSampling(Camera camera)
        {
#if NOWUI_XR
            var sourceDescriptor = GetCameraSourceDescriptor(
                camera,
                camera != null ? camera.pixelWidth : 1,
                camera != null ? camera.pixelHeight : 1);
            return !TryGetBuiltinXrMsaaSourceSamples(camera, sourceDescriptor, out _);
#else
            return true;
#endif
        }

        internal static bool RequiresBuiltinXrMsaaFallback(
            bool isBuiltInPipeline,
            bool hasExplicitTargetTexture,
            bool stereoEnabled,
            bool xrDisplayActive,
            int msaaSamples)
        {
            return isBuiltInPipeline &&
                !hasExplicitTargetTexture &&
                stereoEnabled &&
                xrDisplayActive &&
                msaaSamples > 1;
        }

        internal static bool RequiresBuiltinXrColorMsaaOverride(
            bool requiresXrMsaaFallback,
            TextureDimension sourceDimension)
        {
            return requiresXrMsaaFallback && sourceDimension != TextureDimension.Tex2DArray;
        }

        internal static bool RenderingPathSupportsMsaa(
            bool allowMsaa,
            RenderingPath actualRenderingPath,
            int supportedMultisampledTextureCount)
        {
            return allowMsaa &&
                supportedMultisampledTextureCount > 0 &&
                !IsDeferredRenderingPath(actualRenderingPath);
        }

        static bool IsDeferredRenderingPath(RenderingPath renderingPath)
        {
#pragma warning disable 618
            return renderingPath == RenderingPath.DeferredLighting ||
                renderingPath == RenderingPath.DeferredShading;
#pragma warning restore 618
        }

        static bool CameraCanUseMsaa(Camera camera, int supportedMultisampledTextureCount)
        {
            return camera != null &&
                RenderingPathSupportsMsaa(
                    camera.allowMSAA,
                    camera.actualRenderingPath,
                    supportedMultisampledTextureCount);
        }

#if NOWUI_XR
        static bool TryGetLiveXrSourceDescriptor(
            Camera camera,
            out RenderTextureDescriptor descriptor)
        {
            if (camera != null &&
                camera.targetTexture == null &&
                IsStereoRenderingRequested(camera) &&
                TryGetXrSourceDescriptor(out descriptor))
            {
                return true;
            }

            descriptor = default;
            return false;
        }

        static bool TryGetBuiltinXrMsaaSourceSamples(
            Camera camera,
            in RenderTextureDescriptor sourceDescriptor,
            out int msaaSamples)
        {
            int requestedSamples = CameraCanUseMsaa(camera, SystemInfo.supportsMultisampledTextures)
                ? Mathf.Max(1, QualitySettings.antiAliasing)
                : 1;
            msaaSamples = GetSupportedMsaaSamples(sourceDescriptor, requestedSamples);

            if (RequiresBuiltinXrMsaaFallback(
                    GraphicsSettings.currentRenderPipeline == null,
                    camera != null && camera.targetTexture != null,
                    IsStereoRenderingRequested(camera),
                    HasRunningXrDisplay(),
                    msaaSamples))
            {
                return true;
            }

            msaaSamples = 1;
            return false;
        }

        static int GetSupportedMsaaSamples(
            in RenderTextureDescriptor sourceDescriptor,
            int requestedSamples)
        {
            if (requestedSamples <= 1)
                return 1;

            var candidate = sourceDescriptor;
            candidate.msaaSamples = requestedSamples;
            candidate.bindMS = true;
            candidate.useMipMap = false;
            candidate.autoGenerateMips = false;
            candidate.enableRandomWrite = false;
            int supportedSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(candidate);
            return Mathf.Min(requestedSamples, Mathf.Max(1, supportedSamples));
        }

        static bool IsStereoRenderingRequested(Camera camera)
        {
            return camera != null &&
                (camera.stereoEnabled || camera.stereoTargetEye != StereoTargetEyeMask.None);
        }

        static bool HasRunningXrDisplay()
        {
            _xrDisplays.Clear();
            SubsystemManager.GetSubsystems(_xrDisplays);

            for (int displayIndex = 0; displayIndex < _xrDisplays.Count; ++displayIndex)
            {
                var display = _xrDisplays[displayIndex];

                if (display != null && display.running)
                    return true;
            }

            return false;
        }

        static bool TryGetXrSourceDescriptor(out RenderTextureDescriptor descriptor)
        {
            _xrDisplays.Clear();
            SubsystemManager.GetSubsystems(_xrDisplays);

            for (int displayIndex = 0; displayIndex < _xrDisplays.Count; ++displayIndex)
            {
                var display = _xrDisplays[displayIndex];

                if (display == null || !display.running)
                    continue;

                int renderPassCount = display.GetRenderPassCount();

                for (int passIndex = 0; passIndex < renderPassCount; ++passIndex)
                {
                    display.GetRenderPass(passIndex, out var renderPass);
                    descriptor = renderPass.renderTargetDesc;

                    if (descriptor.width > 0 && descriptor.height > 0)
                        return true;
                }
            }

            descriptor = default;
            return false;
        }
#endif

        static void EnsureCallbacks()
        {
            if (_callbacksRegistered)
                return;

            _callbacksRegistered = true;
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
#endif
        }

        static void OnCameraPreCull(Camera camera)
        {
            if (camera == null || GraphicsSettings.currentRenderPipeline != null)
                return;

            if (!_states.TryGetValue(camera, out var state) ||
                !IsActiveFrame(state.lastUsedFrame, Time.frameCount) ||
                !HasActiveRequest(state, Time.frameCount))
            {
                RemoveBuiltInBuffer(camera);
                CleanupStaleStates();
                return;
            }

            state.builtInBuffer ??= new CommandBuffer
            {
                name = "Now World Glass Backdrop"
            };

            state.builtInBuffer.Clear();
            PopulateCommandBuffer(
                state.builtInBuffer,
                camera,
                BuiltinRenderTextureType.CameraTarget,
                camera.pixelWidth,
                camera.pixelHeight);

            if (!state.builtInBufferAttached)
            {
                camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, state.builtInBuffer);
                state.builtInBufferAttached = true;
            }

            CleanupStaleStates();
        }

        static void OnCameraPostRender(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                return;

            MarkCameraCapturesReady(camera);
        }

        static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                return;

            MarkCameraCapturesReady(camera);
        }

        static void MarkCameraCapturesReady(Camera camera)
        {
            if (camera == null || !_states.TryGetValue(camera, out var state))
                return;

            int frame = Time.frameCount;

            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (request == null || !IsActiveFrame(request.lastUsedFrame, frame))
                    continue;

                if (request.backdropPendingFrame == frame)
                {
                    request.backdropReady = true;
                    request.backdropReadyFrame = frame;
                    request.backdropPendingFrame = -1;
                }

                if (request.sourcePendingFrame == frame)
                {
                    request.sourceReady = true;
                    request.sourceReadyFrame = frame;
                    request.sourcePendingFrame = -1;
                }
            }

            for (int i = 0; i < state.sharedBackdrops.Count; ++i)
            {
                var shared = state.sharedBackdrops[i];

                if (shared == null)
                    continue;

                if (shared.texturePendingFrame == frame)
                {
                    shared.textureReady = true;
                    shared.textureReadyFrame = frame;
                    shared.texturePendingFrame = -1;
                }

                if (shared.sharpTexturePendingFrame == frame)
                {
                    shared.sharpTextureReady = true;
                    shared.sharpTextureReadyFrame = frame;
                    shared.sharpTexturePendingFrame = -1;
                }
            }

            ApplyReadyTextures(state, frame);
        }

        static void ApplyReadyTextures(CameraState state, int frame)
        {
            if (state == null)
                return;

            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (request == null ||
                    request.requester == null ||
                    !IsActiveFrame(request.lastUsedFrame, frame) ||
                    request.mode == NowWorldGlassBackdropMode.TintOnly)
                {
                    continue;
                }

                bool includeWorld = IncludesWorld(request.mode);
                bool needsSharpTexture = request.requiresSceneDepth && ShouldBlur(request);

                if (!includeWorld)
                {
                    var shared = GetSharedBackdropState(state, request.mode, request.blurRadius, request.quality);
                    if (IsTextureReady(shared.textureReady, shared.textureReadyFrame, frame) &&
                        (!needsSharpTexture || IsTextureReady(shared.sharpTextureReady, shared.sharpTextureReadyFrame, frame)))
                    {
                        request.requester.ApplyGlassBackdropTexture(
                            shared.texture,
                            needsSharpTexture ? shared.sharpTexture : shared.texture);
                        ReleaseRetiredTextures(request);
                    }

                    continue;
                }

                if (IsTextureReady(request.backdropReady, request.backdropReadyFrame, frame) &&
                    (!needsSharpTexture || IsTextureReady(request.sourceReady, request.sourceReadyFrame, frame)))
                {
                    request.requester.ApplyGlassBackdropTexture(
                        request.backdrop,
                        needsSharpTexture ? request.source : request.backdrop);
                    ReleaseRetiredTextures(request);
                }
            }

            // Every active requester has now switched to the ready replacement.
            // Only then is it safe to destroy the allocation that was bound
            // during the just-finished camera render.
            for (int i = 0; i < state.sharedBackdrops.Count; ++i)
            {
                var shared = state.sharedBackdrops[i];

                if (shared == null ||
                    !IsTextureReady(shared.textureReady, shared.textureReadyFrame, frame) ||
                    (shared.sharpTexture != null &&
                     !IsTextureReady(shared.sharpTextureReady, shared.sharpTextureReadyFrame, frame)))
                {
                    continue;
                }

                ReleaseRetiredTextures(shared);
            }
        }

        static void EnsureBackdropTexture(
            RequestState request,
            int width,
            int height,
            in NowGlassTextureLayout layout)
        {
            if (NowGlassBackdropSurface.Matches(request.backdrop, width, height, layout))
                return;

            RetireBackdropTexture(request);
            request.width = width;
            request.height = height;
            request.backdrop = CreateTexture(width, height, "Now World Glass Backdrop", layout);
            request.backdropReady = false;
            request.backdropReadyFrame = -1;
            request.backdropPendingFrame = -1;
        }

        static void EnsureSourceTexture(
            RequestState request,
            int width,
            int height,
            in NowGlassTextureLayout layout)
        {
            if (NowGlassBackdropSurface.Matches(request.source, width, height, layout))
                return;

            RetireSourceTexture(request);
            request.source = CreateTexture(width, height, "Now World Glass Source", layout);
            request.sourceReady = false;
            request.sourceReadyFrame = -1;
            request.sourcePendingFrame = -1;
        }

        static void EnsureSharedTexture(
            SharedBackdropState shared,
            int width,
            int height,
            in NowGlassTextureLayout layout,
            bool needsSharpTexture = false)
        {
            if (shared == null)
                return;

            if (NowGlassBackdropSurface.Matches(shared.texture, width, height, layout))
            {
                if (needsSharpTexture &&
                    !NowGlassBackdropSurface.Matches(shared.sharpTexture, width, height, layout))
                {
                    RetireTexture(ref shared.sharpTexture, ref shared.retiredSharpTexture);
                    shared.sharpTexture = CreateTexture(
                        width,
                        height,
                        "Now World Shared Glass Sharp Backdrop",
                        layout);
                    shared.sharpTextureReady = false;
                    shared.sharpTextureReadyFrame = -1;
                    shared.sharpTexturePendingFrame = -1;
                }

                return;
            }

            RetireTexture(ref shared.texture, ref shared.retiredTexture);
            RetireTexture(ref shared.sharpTexture, ref shared.retiredSharpTexture);
            shared.lastPopulatedSequence = -1;
            shared.lastSharpPopulatedSequence = -1;
            shared.width = width;
            shared.height = height;
            shared.texture = CreateTexture(width, height, "Now World Shared Glass Backdrop", layout);
            shared.textureReady = false;
            shared.textureReadyFrame = -1;
            shared.texturePendingFrame = -1;

            if (needsSharpTexture)
            {
                shared.sharpTexture = CreateTexture(
                    width,
                    height,
                    "Now World Shared Glass Sharp Backdrop",
                    layout);
                shared.sharpTextureReady = false;
                shared.sharpTextureReadyFrame = -1;
                shared.sharpTexturePendingFrame = -1;
            }
        }

        static RenderTexture CreateTexture(
            int width,
            int height,
            string name,
            in NowGlassTextureLayout layout)
        {
            return NowGlassBackdropSurface.CreateTexture(width, height, name, layout);
        }

        static void RemoveBuiltInBuffer(Camera camera)
        {
            if (camera == null || !_states.TryGetValue(camera, out var state))
                return;

            if (state.builtInBufferAttached && state.builtInBuffer != null)
                camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, state.builtInBuffer);

            state.builtInBufferAttached = false;
        }

        /// <summary>
        /// Staleness is frame-granular (lastUsedFrame &lt; frame - 1), so the full
        /// walk runs at most once per frame even though registration, request
        /// queries, and camera callbacks all trigger it. Explicit cleanup entry
        /// points reset the gate first so they always sweep.
        /// </summary>
        static void CleanupStaleStates()
        {
            int frame = Time.frameCount;

            if (frame == _lastCleanupFrame)
                return;

            _lastCleanupFrame = frame;
            _staleCameras.Clear();

            foreach (var pair in _states)
            {
                bool hasLiveEditModeRequest = HasLiveEditModeRequest(pair.Value);
                CleanupStaleRequests(pair.Value, frame);
                CleanupStaleSharedBackdrops(pair.Value, frame);

                if (pair.Key == null ||
                    (!hasLiveEditModeRequest &&
                     pair.Value.lastUsedFrame < frame - 1 &&
                     pair.Value.lastSceneDepthFrame < frame - 1))
                {
                    _staleCameras.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleCameras.Count; ++i)
            {
                var camera = _staleCameras[i];

                if (camera != null)
                    RemoveBuiltInBuffer(camera);

                if (_states.TryGetValue(camera, out var state))
                    ReleaseState(state);

                _states.Remove(camera);
            }
        }

        static void ReleaseState(CameraState state)
        {
            if (state == null)
                return;

            if (state.builtInBuffer != null)
            {
                state.builtInBuffer.Release();
                state.builtInBuffer = null;
            }

            for (int i = 0; i < state.requests.Count; ++i)
                ReleaseRequest(state.requests[i]);

            state.requests.Clear();

            for (int i = 0; i < state.sharedBackdrops.Count; ++i)
                ReleaseSharedTexture(state.sharedBackdrops[i]);

            state.sharedBackdrops.Clear();
        }

        static void CleanupStaleRequests(CameraState state, int frame)
        {
            if (state == null)
                return;

            for (int i = state.requests.Count - 1; i >= 0; --i)
            {
                var request = state.requests[i];

                if (request.requester != null && request.lastUsedFrame >= frame - 1)
                    continue;

                if (IsLiveEditModeRequester(request.requester))
                    continue;

                ReleaseRequest(request);
                state.requests.RemoveAt(i);
            }
        }

        static void CleanupStaleSharedBackdrops(CameraState state, int frame)
        {
            if (state == null)
                return;

            if (HasLiveEditModeRequest(state))
                return;

            for (int i = state.sharedBackdrops.Count - 1; i >= 0; --i)
            {
                var shared = state.sharedBackdrops[i];

                if (shared.lastUsedFrame >= frame - 1)
                    continue;

                ReleaseSharedTexture(shared);
                state.sharedBackdrops.RemoveAt(i);
            }
        }

        static bool HasLiveEditModeRequest(CameraState state)
        {
            if (Application.isPlaying || state == null)
                return false;

            for (int i = 0; i < state.requests.Count; ++i)
            {
                if (IsLiveEditModeRequester(state.requests[i].requester))
                    return true;
            }

            return false;
        }

        static bool IsLiveEditModeRequester(NowWorldGraphic requester)
        {
            return !Application.isPlaying &&
                requester != null &&
                requester.isActiveAndEnabled &&
                requester.gameObject.activeInHierarchy;
        }

        static void ReleaseRequest(RequestState request)
        {
            if (request == null)
                return;

            if (request.requester != null)
                request.requester.ApplyGlassBackdropTexture(null);

            ReleaseBackdropTexture(request);
            ReleaseSourceTexture(request);
        }

        static void ReleaseBackdropTexture(RequestState request)
        {
            if (request == null)
                return;

            NowGlassBackdropSurface.ReleaseTexture(ref request.backdrop);
            NowGlassBackdropSurface.ReleaseTexture(ref request.retiredBackdrop);
            request.backdropReady = false;
            request.backdropReadyFrame = -1;
            request.backdropPendingFrame = -1;
            request.width = 0;
            request.height = 0;
        }

        static void ReleaseSourceTexture(RequestState request)
        {
            if (request == null)
                return;

            NowGlassBackdropSurface.ReleaseTexture(ref request.source);
            NowGlassBackdropSurface.ReleaseTexture(ref request.retiredSource);
            request.sourceReady = false;
            request.sourceReadyFrame = -1;
            request.sourcePendingFrame = -1;
        }

        static void ReleaseSharedTexture(SharedBackdropState shared)
        {
            if (shared == null)
                return;

            ReleaseTexture(ref shared.texture);
            ReleaseTexture(ref shared.sharpTexture);
            ReleaseTexture(ref shared.retiredTexture);
            ReleaseTexture(ref shared.retiredSharpTexture);
            shared.width = 0;
            shared.height = 0;
            shared.lastUsedFrame = -1;
            shared.lastPopulatedSequence = -1;
            shared.lastSharpPopulatedSequence = -1;
            shared.textureReady = false;
            shared.sharpTextureReady = false;
            shared.textureReadyFrame = -1;
            shared.sharpTextureReadyFrame = -1;
            shared.texturePendingFrame = -1;
            shared.sharpTexturePendingFrame = -1;
        }

        static void ReleaseTexture(ref RenderTexture texture)
        {
            NowGlassBackdropSurface.ReleaseTexture(ref texture);
        }

        static void RetireBackdropTexture(RequestState request)
        {
            if (request == null)
                return;

            RetireTexture(ref request.backdrop, ref request.retiredBackdrop);
            request.backdropReady = false;
            request.backdropReadyFrame = -1;
            request.backdropPendingFrame = -1;
        }

        static void RetireSourceTexture(RequestState request)
        {
            if (request == null)
                return;

            RetireTexture(ref request.source, ref request.retiredSource);
            request.sourceReady = false;
            request.sourceReadyFrame = -1;
            request.sourcePendingFrame = -1;
        }

        internal static void RetireTexture(ref RenderTexture current, ref RenderTexture retired)
        {
            if (current == null)
                return;

            if (retired == null)
            {
                retired = current;
                current = null;
                return;
            }

            // A second resize before the first replacement is applied must keep
            // the original, still-bound allocation. The intermediate current
            // allocation was never exposed to a material and can be discarded.
            ReleaseTexture(ref current);
        }

        static void ReleaseRetiredTextures(RequestState request)
        {
            if (request == null)
                return;

            ReleaseTexture(ref request.retiredBackdrop);
            ReleaseTexture(ref request.retiredSource);
        }

        static void ReleaseRetiredTextures(SharedBackdropState shared)
        {
            if (shared == null)
                return;

            ReleaseTexture(ref shared.retiredTexture);
            ReleaseTexture(ref shared.retiredSharpTexture);
        }

        public static void ResetEditorPreviewState()
        {
            _lastCleanupFrame = int.MinValue;

            for (int i = 0; i < _staleCameras.Count; ++i)
                _staleCameras[i] = null;

            foreach (var pair in _states)
            {
                if (pair.Key != null)
                    RemoveBuiltInBuffer(pair.Key);

                ReleaseState(pair.Value);
            }

            _states.Clear();
            _staleCameras.Clear();
            _worldContributors.Clear();
            Shader.SetGlobalFloat(_useBackdropId, 0f);
        }

#if UNITY_EDITOR
        static void OnEditorPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                ResetEditorPreviewState();
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            if (_callbacksRegistered)
            {
                Camera.onPreCull -= OnCameraPreCull;
                Camera.onPostRender -= OnCameraPostRender;
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#if UNITY_EDITOR
                EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
#endif
            }

            _callbacksRegistered = false;

            ResetEditorPreviewState();
        }

        /// <summary>
        /// Always sweeps, bypassing the once-per-frame gate, so explicit
        /// end-of-frame cleanup keeps releasing destroyed cameras even in
        /// batch runs where <see cref="Time.frameCount"/> is frozen.
        /// </summary>
        public static void EndFrameCleanup()
        {
            _lastCleanupFrame = int.MinValue;
            CleanupStaleStates();
        }
    }
}
