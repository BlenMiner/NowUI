using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        static readonly int _backdropTexId = Shader.PropertyToID("_NowBackdropTex");

        static readonly int _useBackdropId = Shader.PropertyToID("_NowGlassUseBackdrop");

        static readonly Dictionary<Camera, CameraState> _states = new Dictionary<Camera, CameraState>();

        static readonly List<Camera> _staleCameras = new List<Camera>(4);

        static readonly List<NowWorldGraphic> _worldContributors = new List<NowWorldGraphic>(16);

        static bool _callbacksRegistered;

        sealed class CameraState
        {
            public Camera camera;

            public CommandBuffer builtInBuffer;

            public int lastUsedFrame = -1;

            public int lastSceneDepthFrame = -1;

            public readonly List<RequestState> requests = new List<RequestState>(4);

            public readonly List<SharedBackdropState> sharedBackdrops = new List<SharedBackdropState>(4);

            public bool builtInBufferAttached;
        }

        sealed class SharedBackdropState
        {
            public RenderTexture texture;

            public RenderTexture sharpTexture;

            public int width;

            public int height;

            public int lastUsedFrame = -1;

            public int lastSharpUsedFrame = -1;

            public NowWorldGlassBackdropMode mode;

            public float blurRadius;

            public NowGlassBlurQuality quality;
        }

        sealed class RequestState
        {
            public NowWorldGraphic requester;

            public RenderTexture backdrop;

            public RenderTexture source;

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
                state.lastUsedFrame == Time.frameCount &&
                HasActiveRequest(state, Time.frameCount);
        }

        public static bool RequiresSceneDepth(Camera camera)
        {
            CleanupStaleStates();

            return camera != null &&
                _states.TryGetValue(camera, out var state) &&
                (state.lastSceneDepthFrame == Time.frameCount ||
                 (state.lastUsedFrame == Time.frameCount && HasSceneDepthRequest(state, Time.frameCount)));
        }

        public static void RequestSceneDepth(Camera camera)
        {
            if (camera == null)
                return;

            EnsureCallbacks();

            if (!_states.TryGetValue(camera, out var state))
            {
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
            if (commandBuffer == null ||
                camera == null ||
                !_states.TryGetValue(camera, out var state) ||
                state.lastUsedFrame != Time.frameCount)
            {
                return false;
            }

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            int frame = Time.frameCount;
            bool populated = false;
            RenderTexture lastBackdrop = null;

            for (int i = 0; i < state.requests.Count; ++i)
            {
                var request = state.requests[i];

                if (request.lastUsedFrame != frame ||
                    request.requester == null ||
                    request.mode == NowWorldGlassBackdropMode.TintOnly)
                {
                    continue;
                }

                bool includeWorld = IncludesWorld(request.mode);
                bool blur = ShouldBlur(request);
                var backdropSource = source;

                if (!includeWorld &&
                    TryGetSharedBackdrop(state, commandBuffer, source, width, height, request, out var sharedBackdrop, out var sharedSharpBackdrop))
                {
                    request.requester.ApplyGlassBackdropTexture(sharedBackdrop, sharedSharpBackdrop);
                    lastBackdrop = sharedBackdrop;
                    populated = true;
                    continue;
                }

                EnsureBackdropTexture(request, width, height);
                bool needsSharpSource = request.requiresSceneDepth && blur;

                if (includeWorld || needsSharpSource)
                {
                    EnsureSourceTexture(request, width, height);
                    commandBuffer.Blit(source, request.source);

                    if (includeWorld)
                        RenderWorldContributors(commandBuffer, camera, request);

                    backdropSource = request.source;
                }

                if (!blur)
                    commandBuffer.Blit(backdropSource, request.backdrop);
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
                        out _);

                request.requester.ApplyGlassBackdropTexture(
                    request.backdrop,
                    needsSharpSource ? request.source : request.backdrop);
                lastBackdrop = request.backdrop;
                populated = true;
            }

            if (!populated)
                return false;

            commandBuffer.SetGlobalTexture(_backdropTexId, lastBackdrop);
            commandBuffer.SetGlobalFloat(_useBackdropId, 1f);
            return true;
        }

        static bool TryGetSharedBackdrop(
            CameraState state,
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            int width,
            int height,
            RequestState request,
            out RenderTexture texture,
            out RenderTexture sharpTexture)
        {
            texture = null;
            sharpTexture = null;

            if (state == null || commandBuffer == null || request == null)
                return false;

            var shared = GetSharedBackdropState(state, request.mode, request.blurRadius, request.quality);
            bool needsSharpTexture = request.requiresSceneDepth && ShouldBlur(request);
            EnsureSharedTexture(shared, width, height, needsSharpTexture);

            if (shared.lastUsedFrame != Time.frameCount)
            {
                if (!ShouldBlur(request))
                {
                    commandBuffer.Blit(source, shared.texture);
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
                        out _);
                }
            }

            shared.lastUsedFrame = Time.frameCount;
            if (needsSharpTexture && shared.lastSharpUsedFrame != Time.frameCount)
            {
                commandBuffer.Blit(source, shared.sharpTexture);
                shared.lastSharpUsedFrame = Time.frameCount;
            }

            texture = shared.texture;
            sharpTexture = needsSharpTexture ? shared.sharpTexture : shared.texture;
            return texture != null;
        }

        static SharedBackdropState GetSharedBackdropState(
            CameraState state,
            NowWorldGlassBackdropMode mode,
            float blurRadius,
            NowGlassBlurQuality quality)
        {
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

                if (request.lastUsedFrame == frame &&
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

                if (request.lastUsedFrame == frame &&
                    request.requester != null &&
                    request.requiresSceneDepth &&
                    request.mode != NowWorldGlassBackdropMode.TintOnly)
                {
                    return true;
                }
            }

            return false;
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

            commandBuffer.SetRenderTarget(request.source);

            for (int i = 0; i < _worldContributors.Count; ++i)
                _worldContributors[i].DrawBackdropContribution(commandBuffer);

            _worldContributors.Clear();
        }

        static void EnsureCallbacks()
        {
            if (_callbacksRegistered)
                return;

            _callbacksRegistered = true;
            Camera.onPreCull += OnCameraPreCull;
        }

        static void OnCameraPreCull(Camera camera)
        {
            if (camera == null || GraphicsSettings.currentRenderPipeline != null)
                return;

            if (!_states.TryGetValue(camera, out var state) ||
                state.lastUsedFrame != Time.frameCount ||
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

        static void EnsureBackdropTexture(RequestState request, int width, int height)
        {
            if (request.backdrop != null &&
                request.width == width &&
                request.height == height)
            {
                return;
            }

            ReleaseBackdropTexture(request);
            request.width = width;
            request.height = height;
            request.backdrop = CreateTexture(width, height, "Now World Glass Backdrop");
            request.backdrop.Create();
        }

        static void EnsureSourceTexture(RequestState request, int width, int height)
        {
            if (request.source != null &&
                request.source.width == width &&
                request.source.height == height)
            {
                return;
            }

            ReleaseSourceTexture(request);
            request.source = CreateTexture(width, height, "Now World Glass Source");
            request.source.Create();
        }

        static void EnsureSharedTexture(SharedBackdropState shared, int width, int height, bool needsSharpTexture = false)
        {
            if (shared == null)
                return;

            if (shared.texture != null &&
                shared.width == width &&
                shared.height == height)
            {
                if (needsSharpTexture && shared.sharpTexture == null)
                {
                    shared.sharpTexture = CreateTexture(width, height, "Now World Shared Glass Sharp Backdrop");
                    shared.sharpTexture.Create();
                }

                return;
            }

            ReleaseSharedTexture(shared);
            shared.width = width;
            shared.height = height;
            shared.texture = CreateTexture(width, height, "Now World Shared Glass Backdrop");
            shared.texture.Create();

            if (needsSharpTexture)
            {
                shared.sharpTexture = CreateTexture(width, height, "Now World Shared Glass Sharp Backdrop");
                shared.sharpTexture.Create();
            }
        }

        static RenderTexture CreateTexture(int width, int height, string name)
        {
            var descriptor = new RenderTextureDescriptor(
                Mathf.Max(1, width),
                Mathf.Max(1, height),
                RenderTextureFormat.ARGB32,
                0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            return new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        static void RemoveBuiltInBuffer(Camera camera)
        {
            if (camera == null || !_states.TryGetValue(camera, out var state))
                return;

            if (state.builtInBufferAttached && state.builtInBuffer != null)
                camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, state.builtInBuffer);

            state.builtInBufferAttached = false;
        }

        static void CleanupStaleStates()
        {
            int frame = Time.frameCount;
            _staleCameras.Clear();

            foreach (var pair in _states)
            {
                CleanupStaleRequests(pair.Value, frame);
                CleanupStaleSharedBackdrops(pair.Value, frame);

                if (pair.Key == null ||
                    (pair.Value.lastUsedFrame < frame - 1 &&
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

                ReleaseRequest(request);
                state.requests.RemoveAt(i);
            }
        }

        static void CleanupStaleSharedBackdrops(CameraState state, int frame)
        {
            if (state == null)
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
            if (request?.backdrop == null)
                return;

            request.backdrop.Release();

            if (Application.isPlaying)
                Object.Destroy(request.backdrop);
            else
                Object.DestroyImmediate(request.backdrop);

            request.backdrop = null;
            request.width = 0;
            request.height = 0;
        }

        static void ReleaseSourceTexture(RequestState request)
        {
            if (request?.source == null)
                return;

            request.source.Release();

            if (Application.isPlaying)
                Object.Destroy(request.source);
            else
                Object.DestroyImmediate(request.source);

            request.source = null;
        }

        static void ReleaseSharedTexture(SharedBackdropState shared)
        {
            if (shared == null)
                return;

            ReleaseTexture(ref shared.texture);
            ReleaseTexture(ref shared.sharpTexture);
            shared.width = 0;
            shared.height = 0;
            shared.lastUsedFrame = -1;
            shared.lastSharpUsedFrame = -1;
        }

        static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();

            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);

            texture = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            if (_callbacksRegistered)
                Camera.onPreCull -= OnCameraPreCull;

            _callbacksRegistered = false;

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

        public static void EndFrameCleanup()
        {
            CleanupStaleStates();
        }
    }
}
