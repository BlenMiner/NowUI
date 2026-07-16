using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NowUI
{
    /// <summary>Controls when a model preview refreshes its render texture.</summary>
    public enum NowModelPreviewUpdateMode
    {
        /// <summary>Render when first drawn and after resizing or <see cref="NowModelPreview.MarkDirty"/>; pause visual simulation while dormant.</summary>
        WhenDirty,

        /// <summary>Render and simulate every frame once the preview has been drawn at least once.</summary>
        EveryFrame,

        /// <summary>Render only for explicit <see cref="NowModelPreview.RequestRender"/> or RenderNow calls; pause visual simulation while dormant.</summary>
        Manual
    }

    /// <summary>Controls how a model-preview source reaches the offscreen camera.</summary>
    public enum NowModelPreviewSourceMode
    {
        /// <summary>
        /// Submit supported meshes directly into NowUI's private preview scene.
        /// This is the default and does not instantiate or mutate the source.
        /// </summary>
        Isolated,

        /// <summary>
        /// Let the preview camera render a caller-owned object in a loaded scene.
        /// The caller owns placement, layers, visibility, animation, and dressing.
        /// </summary>
        SceneObject
    }

#if UNITY_INCLUDE_TESTS
    // Retained only as an internal benchmark baseline. Public previews use
    // RenderMesh or a caller-owned scene object and never instantiate a clone.
    internal enum NowModelPreviewBackend
    {
        RendererClone,
        RenderMesh
    }
#endif

    /// <summary>
    /// A lightweight immediate-mode draw builder for a camera-backed model texture.
    /// The expensive camera, source cache, and render target belong to
    /// <see cref="NowModelPreview"/>; this value only carries rectangle styling.
    /// </summary>
    [NowBuilder]
    public struct NowModel
    {
        public NowModelPreview preview;

        public NowRectangle rectangle;

        public NowModel(NowRect rect, NowModelPreview preview)
        {
            this.preview = preview;
            rectangle = new NowRectangle(rect);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetPosition(NowRect rect)
        {
            rectangle = rectangle.SetPosition(rect);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetMask(NowRect mask)
        {
            rectangle = rectangle.SetMask(mask);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetColor(Color color)
        {
            rectangle = rectangle.SetColor(color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetColor(Color color, float alpha)
        {
            rectangle = rectangle.SetColor(color, alpha);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetColor(Vector4 color)
        {
            rectangle = rectangle.SetColor(color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetRadius(float allRadius)
        {
            rectangle = rectangle.SetRadius(allRadius);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            rectangle = rectangle.SetRadius(topLeft, topRight, bottomRight, bottomLeft);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetRadius(NowCornerRadius radius)
        {
            rectangle = rectangle.SetRadius(radius);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetRadius(Vector4 radius)
        {
            rectangle = rectangle.SetRadius(radius);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetPadding(float all)
        {
            rectangle = rectangle.SetPadding(all);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetPadding(Vector4 padding)
        {
            rectangle = rectangle.SetPadding(padding);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetBlur(float blur)
        {
            rectangle = rectangle.SetBlur(blur);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetOutline(float outline)
        {
            rectangle = rectangle.SetOutline(outline);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetOutline(float outline, Color color)
        {
            rectangle = rectangle.SetOutline(outline, color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetOutline(float outline, Vector4 color)
        {
            rectangle = rectangle.SetOutline(outline, color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetOutlineColor(Color color)
        {
            rectangle = rectangle.SetOutlineColor(color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetOutlineColor(Vector4 color)
        {
            rectangle = rectangle.SetOutlineColor(color);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetPreserveAspect(bool preserve = true)
        {
            rectangle = rectangle.SetPreserveAspect(preserve);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetUV(Vector4 uvRect)
        {
            rectangle = rectangle.SetUV(uvRect);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetMaterial(Material material)
        {
            rectangle = rectangle.SetMaterial(material);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetMaterial(Material material, bool syncPerFrame)
        {
            rectangle = rectangle.SetMaterial(material, syncPerFrame);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetMaterial(Material material, Material canvasMaterial)
        {
            rectangle = rectangle.SetMaterial(material, canvasMaterial);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowModel SetCanvasMaterial(Material canvasMaterial)
        {
            rectangle = rectangle.SetCanvasMaterial(canvasMaterial);
            return this;
        }

        [NowConsumer]
        public NowModel Draw()
        {
            Now.DrawModel(this);
            return this;
        }
    }

    public static partial class Now
    {
        /// <summary>
        /// Draws a reusable 3D preview through the normal textured-rectangle
        /// path, so masks, corners, tint, outlines, custom materials, and effects
        /// compose exactly as they do for images.
        /// </summary>
        public static NowModel Model(NowRect rect, NowModelPreview preview)
        {
            return new NowModel(rect, preview);
        }

        internal static void DrawModel(in NowModel model)
        {
            if (model.preview == null)
                return;

            if (!CanDrawModel(model.rectangle, out var displayRect))
                return;

            float pixelWidth = UiUnitsToScreenPixels(displayRect.width);
            float pixelHeight = UiUnitsToScreenPixels(displayRect.height);
            var target = model.preview.Prepare(pixelWidth, pixelHeight);

            // Texture-backed effects copy their captured draw list immediately.
            // Keep retained hosts rebuilding until the deferred camera result has
            // reached that snapshot. Direct draws keep the live RT binding and do
            // not need a geometry rebuild.
            if (NowEffects.isCapturingToTexture)
            {
                model.preview.RegisterCaptureHost(NowFrame.dynamicTextureHost);

                if (model.preview.requiresDeferredTick)
                    NowControlState.RequestRepaint();
            }

            if (target == null)
                return;

            var rectangle = model.rectangle;
            rectangle.texture = target;
            rectangle.premultipliedTexture = true;
            DrawRect(rectangle);
        }

        static bool CanDrawModel(in NowRectangle rectangle, out NowRect displayRect)
        {
            displayRect = rectangle.rect;

            if (_suppressDrawDepth > 0 || _defaultMaterial == null)
                return false;

            var padding = rectangle.padding;
            displayRect.x -= padding.x;
            displayRect.y -= padding.y;
            displayRect.width += padding.x + padding.z;
            displayRect.height += padding.y + padding.w;

            if (displayRect.width <= 0f || displayRect.height <= 0f)
                return false;

            var mask = rectangle.mask;
            bool defaultMask = !mask.isEmpty && mask == rectangle.rect;

            if (!mask.isEmpty)
            {
                mask.x -= padding.x;
                mask.y -= padding.y;
                mask.width += padding.x + padding.z;
                mask.height += padding.y + padding.w;
            }

            float visualPadding = RectangleVisualPadding(rectangle.blur, rectangle.outline);

            if (defaultMask)
                mask = mask.Outset(visualPadding);

            if (_transformStack.Count > 0)
            {
                displayRect = ApplyTransformRect(displayRect);

                if (!mask.isEmpty)
                    mask = ApplyTransformRect(mask);

                visualPadding = RectangleVisualPadding(
                    ApplyTransformScalar(rectangle.blur),
                    ApplyTransformScalar(rectangle.outline));
            }

            var visibleBounds = displayRect.Outset(visualPadding);
            var effectiveMask = ApplyAmbientMask(mask);
            return !visibleBounds.Intersect(effectiveMask).isEmpty;
        }
    }

    /// <summary>
    /// Owns a disabled preview camera, source cache, and persistent render
    /// texture. Create it outside the draw callback, reuse it, and dispose it
    /// with the screen or inventory item that owns it. The source remains
    /// caller-owned and is never instantiated, mutated, or destroyed.
    /// </summary>
    public sealed class NowModelPreview : IDisposable
    {
        struct RawMeshSource
        {
            public Mesh mesh;
            public Mesh ownedBakedMesh;
            public SkinnedMeshRenderer skinnedRenderer;
            public Matrix4x4 localToRoot;
            public Matrix4x4 objectToWorld;
            public Bounds worldBounds;
        }

        struct RawMeshDraw
        {
            public int sourceIndex;
            public int submeshIndex;
            public Material material;
            public MaterialPropertyBlock properties;
            public ShadowCastingMode shadowCastingMode;
            public bool receiveShadows;
            public int rendererPriority;
        }

        internal struct SharedLightSettings
        {
            public Quaternion rotation;
            public Color color;
            public float intensity;
            public int cullingMask;
            public int renderingLayerMask;
            public RenderPipelineAsset pipeline;

            public bool Matches(in SharedLightSettings other)
            {
                return rotation == other.rotation && color == other.color &&
                    intensity == other.intensity && cullingMask == other.cullingMask &&
                    renderingLayerMask == other.renderingLayerMask &&
                    ReferenceEquals(pipeline, other.pipeline);
            }
        }

        struct CaptureHostDependency
        {
            public INowDynamicTextureHost host;
            public int buildVersion;
        }

        struct MutedDirectionalLight
        {
            public Light light;
            public int cullingMask;
        }

        const int DefaultPreviewLayer = 31;
        const int DefaultMaxResolution = 1024;
        const int AutomaticResolutionQuantum = 8;
        const float NormalizedModelSize = 2f;
        const int PreviewRenderingLayerMask = 1 << 7;

        readonly int _previewLayer;
        readonly NowModelPreviewSourceMode _sourceMode;
#if UNITY_INCLUDE_TESTS
        readonly NowModelPreviewBackend _backend;
#endif
        // Preview renders are serialized by NowModelPreviewManager, so these
        // mutable request/scratch objects are safe to share across previews.
        // This avoids two managed allocations for every inventory thumbnail.
        static readonly RenderPipeline.StandardRequest _renderRequest = new RenderPipeline.StandardRequest();
        static readonly List<MutedDirectionalLight> _mutedDirectionalLights = new List<MutedDirectionalLight>(4);

        GameObject _source;
#if UNITY_INCLUDE_TESTS
        GameObject _instance;
#endif
        GameObject _rig;
#if UNITY_INCLUDE_TESTS
        Transform _contentPivot;
        Transform _centeringRoot;
#endif
        Camera _camera;
        Light _keyLight;
        Scene _isolatedScene;
        RenderTexture _target;
        Renderer[] _renderers = Array.Empty<Renderer>();
        RawMeshSource[] _rawMeshSources = Array.Empty<RawMeshSource>();
        RawMeshDraw[] _rawMeshDraws = Array.Empty<RawMeshDraw>();
        int[] _rawSkinnedSourceIndices = Array.Empty<int>();
        int _rawUnsupportedRendererCount;
        bool _rawWorldTransformsDirty = true;
        LayerMask _cameraCullingMask;
        List<CaptureHostDependency> _captureHosts;
#if UNITY_INCLUDE_TESTS
        bool[] _rendererOriginallyForceOff = Array.Empty<bool>();
        SkinnedMeshRenderer[] _skinnedRenderers = Array.Empty<SkinnedMeshRenderer>();
        bool[] _skinnedOriginallyUpdateOffscreen = Array.Empty<bool>();
        Animator[] _animators = Array.Empty<Animator>();
        AnimatorCullingMode[] _animatorCullingModes = Array.Empty<AnimatorCullingMode>();
        bool[] _animatorOriginallyEnabled = Array.Empty<bool>();
        Animation[] _animations = Array.Empty<Animation>();
        AnimationCullingType[] _animationCullingModes = Array.Empty<AnimationCullingType>();
        bool[] _animationOriginallyEnabled = Array.Empty<bool>();
        ParticleSystem[] _particleSystems = Array.Empty<ParticleSystem>();
        bool[] _particlePausedByPolicy = Array.Empty<bool>();
#endif

        NowModelPreviewUpdateMode _updateMode = NowModelPreviewUpdateMode.WhenDirty;
        Vector3 _viewDirection = Vector3.forward;
        Quaternion _contentRotation = Quaternion.identity;
        Vector3 _rawContentCenter;
        float _rawContentScale = 1f;
        Color _backgroundColor = Color.clear;
        Quaternion _lightRotation = Quaternion.Euler(35f, 145f, 0f);
        Color _lightColor = Color.white;
        float _lightIntensity = 1.15f;
        float _fieldOfView = 30f;
        float _framingPadding = 1.12f;
        float _rotationInvariantRadius;
        float _resolutionScale = 1f;
        int _maxResolution = DefaultMaxResolution;
        int _fixedWidth;
        int _fixedHeight;
        float _lastRequestedPixelWidth;
        float _lastRequestedPixelHeight;
        int _pendingTargetWidth;
        int _pendingTargetHeight;
        GameObject _pendingSource;
        bool _hasPendingSource;
        bool _refreshHierarchyPending;
        bool _fixedResolution;
        bool _orthographic;
        bool _renderingEnabled = true;
        bool _sceneLightingEnabled;
#if UNITY_INCLUDE_TESTS
        bool _rendererLightingLayersDirty;
#endif
        bool _postProcessingEnabled;
        LayerMask _postProcessingVolumeMask = ~0;
        bool _postProcessingSettingsDirty = true;
        bool _prepared;
        bool _hasDrawn;
        bool _dirty = true;
        bool _renderQueued;
        bool _frameDirty = true;
        bool _recalculateFramingRadius;
        bool _disposed;
        bool _disposeRequested;
        bool _warnedUnsupportedPipeline;
        int _dirtyVersion;
        int _lastRenderedFrame = -1;
        RenderPipelineAsset _unsupportedPipeline;
        RenderPipelineAsset _supportedPipeline;
        RenderPipelineAsset _configuredCameraPipeline;

        static Type _hdAdditionalLightDataType;
        static Type _universalAdditionalCameraDataType;
        static Type _hdAdditionalCameraDataType;
        static PropertyInfo _universalVolumeLayerMaskProperty;
        static PropertyInfo _universalRenderPostProcessingProperty;
        static PropertyInfo _hdVolumeLayerMaskProperty;
        // GameObject/Renderer discovery is main-thread-only. Reuse its temporary
        // lists so grids do not allocate five collection buffers per preview.
        static readonly List<Renderer> _rawRendererScratch = new List<Renderer>(16);
        static readonly List<RawMeshSource> _rawSourceScratch = new List<RawMeshSource>(16);
        static readonly List<RawMeshDraw> _rawDrawScratch = new List<RawMeshDraw>(16);
        static readonly List<int> _rawSkinnedIndexScratch = new List<int>(2);
        static readonly List<Material> _rawMaterialScratch = new List<Material>(4);

        /// <summary>Creates a preview using layer 31, Unity's conventional preview layer.</summary>
        public NowModelPreview(GameObject source = null)
            : this(source, DefaultPreviewLayer)
        {
        }

        /// <summary>
        /// Creates an isolated raw-mesh preview on an explicit submission layer.
        /// The source is caller-owned and is never instantiated or mutated.
        /// </summary>
        public NowModelPreview(GameObject source, int previewLayer)
            : this(source, previewLayer, NowModelPreviewSourceMode.Isolated)
        {
        }

        NowModelPreview(
            GameObject source,
            int previewLayer,
            NowModelPreviewSourceMode sourceMode)
#if UNITY_INCLUDE_TESTS
            : this(source, previewLayer, sourceMode, NowModelPreviewBackend.RenderMesh)
#endif
        {
#if !UNITY_INCLUDE_TESTS
            if (previewLayer < 0 || previewLayer > 31)
                throw new ArgumentOutOfRangeException(nameof(previewLayer), "Preview layer must be between 0 and 31.");

            if (sourceMode < NowModelPreviewSourceMode.Isolated ||
                sourceMode > NowModelPreviewSourceMode.SceneObject)
                throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, "Unknown model-preview source mode.");

            _previewLayer = previewLayer;
            _sourceMode = sourceMode;
            _cameraCullingMask = 1 << previewLayer;
            _sceneLightingEnabled = sourceMode == NowModelPreviewSourceMode.SceneObject;

            try
            {
                _isolatedScene = NowModelPreviewManager.AcquirePreviewScene();
                CreateRig(NowModelPreviewManager.AllocateStageOrigin(usesPresentationClone), _isolatedScene);
                SetSourceInternal(source);
                NowModelPreviewManager.Register(this, _camera);
            }
            catch
            {
                Dispose();
                throw;
            }
#endif
        }

#if UNITY_INCLUDE_TESTS
        internal NowModelPreview(
            GameObject source,
            int previewLayer,
            NowModelPreviewBackend backend)
            : this(source, previewLayer, NowModelPreviewSourceMode.Isolated, backend)
        {
        }

        NowModelPreview(
            GameObject source,
            int previewLayer,
            NowModelPreviewSourceMode sourceMode,
            NowModelPreviewBackend backend)
        {
            if (previewLayer < 0 || previewLayer > 31)
                throw new ArgumentOutOfRangeException(nameof(previewLayer), "Preview layer must be between 0 and 31.");

            if (backend < NowModelPreviewBackend.RendererClone ||
                backend > NowModelPreviewBackend.RenderMesh)
                throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown model-preview backend.");

            if (sourceMode < NowModelPreviewSourceMode.Isolated ||
                sourceMode > NowModelPreviewSourceMode.SceneObject)
                throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, "Unknown model-preview source mode.");

            _previewLayer = previewLayer;
            _sourceMode = sourceMode;
            _backend = backend;
            _cameraCullingMask = 1 << previewLayer;
            _sceneLightingEnabled = sourceMode == NowModelPreviewSourceMode.SceneObject;

            try
            {
                _isolatedScene = NowModelPreviewManager.AcquirePreviewScene();
                CreateRig(NowModelPreviewManager.AllocateStageOrigin(usesPresentationClone), _isolatedScene);
                SetSourceInternal(source);
                NowModelPreviewManager.Register(this, _camera);
            }
            catch
            {
                Dispose();
                throw;
            }
        }
#endif

        /// <summary>
        /// Creates a preview of an existing caller-owned scene object. No
        /// hierarchy is cloned or modified. The initial camera mask includes
        /// every renderer layer currently present below <paramref name="sceneObject"/>.
        /// Prefer a dedicated layer because every object on the mask can be
        /// visible to the preview camera.
        /// </summary>
        public static NowModelPreview FromSceneObject(GameObject sceneObject)
        {
            RequireLoadedSceneObject(sceneObject);

            var preview = new NowModelPreview(
                sceneObject,
                sceneObject.layer,
                NowModelPreviewSourceMode.SceneObject);
            return preview.SetSceneCullingMask(preview.CalculateCachedSourceLayerMask());
        }

        /// <summary>
        /// Creates a caller-owned scene-object preview using an explicit camera
        /// culling mask. NowUI does not change the object's layers or visibility.
        /// </summary>
        public static NowModelPreview FromSceneObject(
            GameObject sceneObject,
            LayerMask cullingMask)
        {
            RequireLoadedSceneObject(sceneObject);

            return new NowModelPreview(
                    sceneObject,
                    sceneObject.layer,
                    NowModelPreviewSourceMode.SceneObject)
                .SetSceneCullingMask(cullingMask);
        }

        static void RequireLoadedSceneObject(GameObject sceneObject)
        {
            if (sceneObject == null)
                throw new ArgumentNullException(nameof(sceneObject));

            if (!sceneObject.scene.IsValid() || !sceneObject.scene.isLoaded)
                throw new ArgumentException(
                    "FromSceneObject requires an instantiated object in a loaded scene. Use new NowModelPreview(source) for prefab assets.",
                    nameof(sceneObject));
        }

        /// <summary>The caller-owned source read by this preview.</summary>
        public GameObject source => _source;

        /// <summary>Whether the source is submitted in isolation or rendered in its loaded scene.</summary>
        public NowModelPreviewSourceMode sourceMode => _sourceMode;

        /// <summary>The camera culling mask. Scene-object previews leave matching object visibility to the caller.</summary>
        public LayerMask cullingMask => _cameraCullingMask;

        /// <summary>
        /// The disabled preview camera used for deferred offscreen renders.
        /// </summary>
        public Camera camera => _camera;

        /// <summary>The stable texture handle, null until a draw or fixed resolution prepares it.</summary>
        public RenderTexture texture => _target;

        public NowModelPreviewUpdateMode updateMode => _updateMode;

        public bool isDisposed => _disposed || _disposeRequested;

        public bool renderingEnabled => _renderingEnabled;

        public int previewLayer => _previewLayer;

        /// <summary>
        /// Number of active source renderers the isolated raw path could not
        /// represent. MeshRenderer and SkinnedMeshRenderer are supported; use a
        /// scene-object preview for renderer-specific systems such as particles.
        /// </summary>
        public int unsupportedRendererCount => _rawUnsupportedRendererCount;

#if UNITY_INCLUDE_TESTS
        internal NowModelPreviewBackend backend => _backend;

        internal GameObject presentationInstance => _instance;

        internal int renderMeshSourceCount => _rawMeshSources.Length;

        internal int renderMeshDrawCount => _rawMeshDraws.Length;

        internal int presentationCloneGameObjectCount => CountHierarchyObjects(_instance != null
            ? _instance.transform
            : null);

        internal int stagingRigGameObjectCount => CountHierarchyObjects(_rig != null
            ? _rig.transform
            : null);

        internal float normalizedContentScale => usesRawSubmission
            ? _rawContentScale
            : _contentPivot != null
                ? _contentPivot.localScale.x
                : 1f;

        internal Light sharedKeyLight => _keyLight;

        internal int ownedBakedMeshCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _rawMeshSources.Length; ++i)
                {
                    if (_rawMeshSources[i].ownedBakedMesh != null)
                        ++count;
                }

                return count;
            }
        }
#endif

        /// <summary>
        /// Whether this preview opts into lights and environment settings from
        /// loaded game scenes. The default is false: the camera renders only
        /// NowUI's shared private preview scene. Scene-object previews are
        /// inherently loaded-scene renders and therefore always report true.
        /// </summary>
        public bool sceneLightingEnabled => _sceneLightingEnabled;

        /// <summary>Whether URP/HDRP volume post-processing is requested for this preview.</summary>
        public bool postProcessingEnabled => _postProcessingEnabled;

        /// <summary>The volume layers used when post-processing is enabled.</summary>
        public LayerMask postProcessingVolumeMask => _postProcessingVolumeMask;

        bool usesPresentationClone
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                return _backend == NowModelPreviewBackend.RendererClone;
#else
                return false;
#endif
            }
        }

        bool usesRawSubmission =>
            _sourceMode == NowModelPreviewSourceMode.Isolated && !usesPresentationClone;

        /// <summary>Replaces the caller-owned source without changing the source mode.</summary>
        public NowModelPreview SetSource(GameObject source)
        {
            ThrowIfDisposed();

            if (_sourceMode == NowModelPreviewSourceMode.SceneObject && source != null)
                RequireLoadedSceneObject(source);

            if (_hasPendingSource)
            {
                if (ReferenceEquals(_pendingSource, source))
                    return this;

                if (ReferenceEquals(_source, source))
                {
                    _pendingSource = null;
                    _hasPendingSource = false;
                    return this;
                }
            }

            if (ReferenceEquals(_source, source))
                return this;

            if (IsUnsafeToMutateResources())
            {
                _pendingSource = source;
                _hasPendingSource = true;
                InvalidatePixels(reframe: true);
                return this;
            }

            SetSourceInternal(source);
            NowModelPreviewManager.Wake();
            return this;
        }

        /// <summary>
        /// Sets the camera mask used by a scene-object preview. NowUI never
        /// changes the source hierarchy's layers, so the caller must keep the
        /// object on this mask and exclude it from game cameras when required.
        /// </summary>
        public NowModelPreview SetSceneCullingMask(LayerMask cullingMask)
        {
            ThrowIfDisposed();

            if (_sourceMode != NowModelPreviewSourceMode.SceneObject)
                throw new InvalidOperationException(
                    "A scene culling mask applies only to a preview created with FromSceneObject(...).");

            if (cullingMask.value == 0)
                throw new ArgumentOutOfRangeException(nameof(cullingMask), "Scene culling mask must include at least one layer.");

            if (_cameraCullingMask == cullingMask)
                return this;

            _cameraCullingMask = cullingMask;

            if (_camera != null)
                _camera.cullingMask = cullingMask;

            ApplyLightSettings();
            InvalidatePixels();
            return this;
        }

        public NowModelPreview SetUpdateMode(NowModelPreviewUpdateMode mode)
        {
            ThrowIfDisposed();

            if (mode < NowModelPreviewUpdateMode.WhenDirty || mode > NowModelPreviewUpdateMode.Manual)
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown model-preview update mode.");

            if (_updateMode == mode)
                return this;

            _updateMode = mode;
            ApplyAnimationPolicy();
            NowModelPreviewManager.Wake();
            return this;
        }

        /// <summary>Scales automatic draw-rect resolution before the maximum cap is applied.</summary>
        public NowModelPreview SetResolutionScale(float scale)
        {
            ThrowIfDisposed();
            RequirePositiveFinite(scale, nameof(scale));

            if (Mathf.Approximately(_resolutionScale, scale))
                return this;

            _resolutionScale = scale;

            if (ResizePreparedTarget())
                NowModelPreviewManager.Wake();

            return this;
        }

        /// <summary>Caps the longest target edge. The default is 1024 pixels.</summary>
        public NowModelPreview SetMaxResolution(int maxPixels)
        {
            ThrowIfDisposed();

            if (maxPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPixels), "Maximum resolution must be positive.");

            if (_maxResolution == maxPixels)
                return this;

            _maxResolution = maxPixels;

            if (ResizePreparedTarget())
                NowModelPreviewManager.Wake();

            return this;
        }

        /// <summary>
        /// Requests a stable texture size instead of matching the draw rect.
        /// The maximum resolution and device texture-size limit can cap it
        /// proportionally.
        /// </summary>
        public NowModelPreview SetFixedResolution(int width, int height)
        {
            ThrowIfDisposed();

            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

            if (_fixedResolution && _fixedWidth == width && _fixedHeight == height)
                return this;

            _fixedResolution = true;
            _fixedWidth = width;
            _fixedHeight = height;
            EnsureTarget(width, height);
            _prepared = true;
            NowModelPreviewManager.Wake();
            return this;
        }

        /// <summary>Returns to matching the physical pixel size of the next Now.Model draw.</summary>
        public NowModelPreview SetAutomaticResolution()
        {
            ThrowIfDisposed();

            if (!_fixedResolution)
                return this;

            _fixedResolution = false;
            _prepared = _lastRequestedPixelWidth > 0f && _lastRequestedPixelHeight > 0f;

            if (_prepared && ResizePreparedTarget())
                NowModelPreviewManager.Wake();

            return this;
        }

        public NowModelPreview SetBackground(Color color)
        {
            ThrowIfDisposed();

            if (!IsFinite(color))
                throw new ArgumentOutOfRangeException(nameof(color), "Background color must be finite.");

            if (_backgroundColor == color)
                return this;

            _backgroundColor = color;
            ApplyCameraBackground();
            InvalidatePixels();
            return this;
        }

        /// <summary>Sets the normalized direction from the model toward the camera.</summary>
        public NowModelPreview SetViewDirection(Vector3 direction)
        {
            ThrowIfDisposed();

            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
                throw new ArgumentOutOfRangeException(nameof(direction), "View direction must be finite and non-zero.");

            direction.Normalize();

            if ((_viewDirection - direction).sqrMagnitude <= 0.000001f)
                return this;

            _viewDirection = direction;
            InvalidatePixels(reframe: true);
            return this;
        }

        /// <summary>Rotates an isolated model around its framed center.</summary>
        public NowModelPreview SetRotation(Quaternion rotation)
        {
            ThrowIfDisposed();

            if (_sourceMode == NowModelPreviewSourceMode.SceneObject)
                throw new InvalidOperationException(
                    "SetRotation does not mutate a caller-owned scene object. Rotate the scene object directly and call Reframe().");

            if (!IsFinite(rotation) || QuaternionMagnitudeSquared(rotation) <= 0.000001f)
                throw new ArgumentOutOfRangeException(nameof(rotation), "Rotation must be finite and non-zero.");

            rotation = Normalize(rotation);

            if (Mathf.Abs(Quaternion.Dot(_contentRotation, rotation)) >= 0.999999f)
                return this;

            _contentRotation = rotation;

#if UNITY_INCLUDE_TESTS
            if (_contentPivot != null)
                _contentPivot.localRotation = rotation;
#endif

            if (usesRawSubmission)
                _rawWorldTransformsDirty = true;

            // Rotation changes pixels but keeps the sphere-framed camera stable.
            // Call Reframe explicitly when a new pose changes the required sphere.
            InvalidatePixels();
            return this;
        }

        public NowModelPreview SetFieldOfView(float degrees)
        {
            ThrowIfDisposed();

            if (!IsFinite(degrees) || degrees <= 1f || degrees >= 179f)
                throw new ArgumentOutOfRangeException(nameof(degrees), "Field of view must be finite and between 1 and 179 degrees.");

            if (Mathf.Approximately(_fieldOfView, degrees))
                return this;

            _fieldOfView = degrees;
            InvalidatePixels(reframe: true);
            return this;
        }

        public NowModelPreview SetOrthographic(bool orthographic = true)
        {
            ThrowIfDisposed();

            if (_orthographic == orthographic)
                return this;

            _orthographic = orthographic;
            InvalidatePixels(reframe: true);
            return this;
        }

        public NowModelPreview SetFramingPadding(float multiplier)
        {
            ThrowIfDisposed();
            RequirePositiveFinite(multiplier, nameof(multiplier));

            if (Mathf.Approximately(_framingPadding, multiplier))
                return this;

            _framingPadding = multiplier;
            InvalidatePixels(reframe: true);
            return this;
        }

        public NowModelPreview SetLight(Quaternion rotation, float intensity, Color color)
        {
            ThrowIfDisposed();

            if (!IsFinite(rotation) || QuaternionMagnitudeSquared(rotation) <= 0.000001f)
                throw new ArgumentOutOfRangeException(nameof(rotation), "Light rotation must be finite and non-zero.");

            if (!IsFinite(intensity) || intensity < 0f)
                throw new ArgumentOutOfRangeException(nameof(intensity), "Light intensity must be non-negative and finite.");

            if (!IsFinite(color))
                throw new ArgumentOutOfRangeException(nameof(color), "Light color must be finite.");

            rotation = Normalize(rotation);

            if (Mathf.Abs(Quaternion.Dot(_lightRotation, rotation)) >= 0.999999f &&
                Mathf.Approximately(_lightIntensity, intensity) && _lightColor == color)
                return this;

            _lightRotation = rotation;
            _lightIntensity = intensity;
            _lightColor = color;
            ApplyLightSettings();
            InvalidatePixels();
            return this;
        }

        /// <summary>
        /// Opts an isolated preview into lights and environment settings from
        /// loaded game scenes. Disabled by default so inventory and portrait
        /// lighting is stable across menus, levels, and camera stacks. A
        /// scene-object preview is already opted in and cannot disable it.
        /// </summary>
        public NowModelPreview SetSceneLightingEnabled(bool enabled = true)
        {
            ThrowIfDisposed();

            if (_sourceMode == NowModelPreviewSourceMode.SceneObject && !enabled)
                throw new InvalidOperationException(
                    "A scene-object preview necessarily uses its loaded scene. Use the default isolated source mode when scene lighting must be excluded.");

            if (_sceneLightingEnabled == enabled)
                return this;

            _sceneLightingEnabled = enabled;
#if UNITY_INCLUDE_TESTS
            _rendererLightingLayersDirty = true;
#endif

            if (!IsUnsafeToMutateResources())
            {
                ApplyCameraScene();
#if UNITY_INCLUDE_TESTS
                if (usesPresentationClone)
                    ApplyRendererLightingLayers();
#endif
            }

            InvalidatePixels();
            return this;
        }

        /// <summary>
        /// Refreshes the cached Built-in directional-light list. Scene loads
        /// are tracked automatically; call this after creating or destroying a
        /// directional light at runtime while previews are alive.
        /// </summary>
        public void RefreshSceneLighting()
        {
            ThrowIfDisposed();
            NowModelPreviewManager.InvalidateSceneDirectionalLights();
            InvalidatePixels();
        }

        /// <summary>
        /// Enables URP/HDRP volume post-processing using all volume layers.
        /// Post-processing is disabled by default and therefore has no preview
        /// render cost until explicitly enabled.
        /// </summary>
        public NowModelPreview SetPostProcessingEnabled(bool enabled = true)
        {
            return SetPostProcessingEnabled(enabled, _postProcessingVolumeMask);
        }

        /// <summary>
        /// Enables URP/HDRP volume post-processing using an explicit volume
        /// layer mask. Built-in or third-party image effects can still be
        /// configured directly on <see cref="camera"/>.
        /// </summary>
        public NowModelPreview SetPostProcessingEnabled(bool enabled, LayerMask volumeLayerMask)
        {
            ThrowIfDisposed();

            if (_postProcessingEnabled == enabled &&
                _postProcessingVolumeMask == volumeLayerMask)
                return this;

            _postProcessingEnabled = enabled;
            _postProcessingVolumeMask = volumeLayerMask;
            _postProcessingSettingsDirty = true;

            if (!IsUnsafeToMutateResources())
                ApplyPostProcessingSettings(GraphicsSettings.currentRenderPipeline);

            InvalidatePixels();
            return this;
        }

        /// <summary>
        /// Pauses or resumes preview refreshes while retaining the target. This
        /// never pauses or resumes simulation on a caller-owned source. A target
        /// resize still clears its contents to transparent.
        /// </summary>
        public NowModelPreview SetRenderingEnabled(bool enabled)
        {
            ThrowIfDisposed();

            if (_renderingEnabled == enabled)
                return this;

            _renderingEnabled = enabled;
            ApplyAnimationPolicy();

            if (enabled)
                NowModelPreviewManager.Wake();

            return this;
        }

        /// <summary>
        /// Marks pixels stale without moving the camera. Use <see cref="Reframe"/>
        /// when renderer bounds or the desired fit changed.
        /// </summary>
        public void MarkDirty()
        {
            ThrowIfDisposed();
            ApplyAnimationPolicy();
            InvalidatePixels();
        }

        /// <summary>Queues a render even when the update mode is Manual.</summary>
        public void RequestRender()
        {
            ThrowIfDisposed();
            ApplyAnimationPolicy();
            _unsupportedPipeline = null;
            _supportedPipeline = null;
            _renderQueued = true;
            InvalidatePixels();
        }

        /// <summary>
        /// Rediscovers the source hierarchy, recomputes framing from current
        /// renderer bounds, and queues a refresh. Call this after attaching or
        /// removing equipment or changing rigid hierarchy transforms.
        /// </summary>
        public void Reframe()
        {
            ThrowIfDisposed();

            if (_source != null)
            {
                if (IsUnsafeToMutateResources())
                    _refreshHierarchyPending = true;
                else
                    RefreshHierarchyInternal();
            }

            _recalculateFramingRadius = true;
            InvalidatePixels(reframe: true);
        }

        /// <summary>
        /// Attempts to render synchronously into a previously prepared target.
        /// If rendering cannot safely begin, the request is deferred. An active
        /// render pipeline that does not support standard offscreen requests
        /// preserves the previous texture instead.
        /// </summary>
        /// <returns>True when the camera render completed; otherwise false.</returns>
        public bool RenderNow()
        {
            ThrowIfDisposed();

            if (!_renderingEnabled)
            {
                RequestRender();
                return false;
            }

            if (_target == null || IsUnsafeToMutateResources() ||
                !NowModelPreviewManager.TryBeginRender(this))
            {
                RequestRender();
                return false;
            }

            try
            {
                return RenderInternal();
            }
            finally
            {
                NowModelPreviewManager.EndRender(this);
            }
        }

        /// <summary>
        /// Records a physical-pixel draw size, prepares a target using the
        /// current resolution policy, then renders synchronously. This is the
        /// initialization/tooling overload to use before the preview has
        /// appeared in a Now.Model draw.
        /// </summary>
        public bool RenderNow(int pixelWidth, int pixelHeight)
        {
            ThrowIfDisposed();

            if (pixelWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Width must be positive.");

            if (pixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Height must be positive.");

            PrepareResolution(pixelWidth, pixelHeight, reportVisible: false);
            return RenderNow();
        }

        internal RenderTexture Prepare(float pixelWidth, float pixelHeight)
        {
            ThrowIfDisposed();
            PrepareResolution(pixelWidth, pixelHeight, reportVisible: true);
            return _target;
        }

        internal bool requiresDeferredTick
        {
            get
            {
                if (_disposed)
                    return false;

                if (_disposeRequested || _hasPendingSource || _refreshHierarchyPending ||
                    _pendingTargetWidth > 0)
                    return true;

                if (!_renderingEnabled || !_prepared || _target == null)
                    return false;

                if (_unsupportedPipeline != null &&
                    ReferenceEquals(_unsupportedPipeline, GraphicsSettings.currentRenderPipeline))
                    return false;

                if (_renderQueued)
                    return true;

                if (!_hasDrawn)
                    return false;

                return _updateMode == NowModelPreviewUpdateMode.EveryFrame ||
                    (_updateMode == NowModelPreviewUpdateMode.WhenDirty && _dirty);
            }
        }

        void PrepareResolution(float pixelWidth, float pixelHeight, bool reportVisible)
        {
            if (!IsFinite(pixelWidth) || pixelWidth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Width must be positive and finite.");

            if (!IsFinite(pixelHeight) || pixelHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Height must be positive and finite.");

            _lastRequestedPixelWidth = pixelWidth;
            _lastRequestedPixelHeight = pixelHeight;

            if (_fixedResolution)
                EnsureTarget(_fixedWidth, _fixedHeight);
            else
                EnsureAutomaticTarget();

            _prepared = true;

            if (reportVisible && !_hasDrawn)
            {
                _hasDrawn = true;
                ApplyAnimationPolicy();
            }

            if (requiresDeferredTick)
                NowModelPreviewManager.Wake();
        }

        internal void RenderDeferred()
        {
            if (_disposed)
                return;

            ApplyDeferredMutations();

            if (_disposed || !_renderingEnabled || !_prepared || _target == null)
                return;

            if (!requiresDeferredTick || _lastRenderedFrame == Time.frameCount)
                return;

            if (!NowModelPreviewManager.TryBeginRender(this))
                return;

            try
            {
                RenderInternal();
            }
            finally
            {
                NowModelPreviewManager.EndRender(this);
            }
        }

        /// <summary>
        /// Returns whether a camera belongs to a live NowUI model preview.
        /// </summary>
        public static bool IsPreviewCamera(Camera candidate)
        {
            return NowModelPreviewManager.IsPreviewCamera(candidate);
        }

        bool RenderInternal()
        {
            if (_camera == null || _target == null)
                return false;

            if (_frameDirty)
                ReframeInternal();

            var camera = _camera;
            var keyLight = _keyLight;
            var target = _target;
#if UNITY_INCLUDE_TESTS
            var renderers = _renderers;
            var rendererForceOff = _rendererOriginallyForceOff;
#endif
            var pipeline = GraphicsSettings.currentRenderPipeline;
            int renderVersion = _dirtyVersion;

            if (pipeline != null && ReferenceEquals(_unsupportedPipeline, pipeline))
                return false;

            var previousActive = RenderTexture.active;
            var previousTarget = pipeline == null ? camera.targetTexture : null;
            bool success = false;
            bool enabledKeyLight = false;

            try
            {
                ApplyCameraScene();

#if UNITY_INCLUDE_TESTS
                if (_rendererLightingLayersDirty && usesPresentationClone)
                    ApplyRendererLightingLayers();
#endif

                if (_postProcessingSettingsDirty ||
                    !ReferenceEquals(_configuredCameraPipeline, pipeline))
                    ApplyPostProcessingSettings(pipeline);

                if (!_sceneLightingEnabled && pipeline == null)
                    MuteBuiltInSceneDirectionals();

#if UNITY_INCLUDE_TESTS
                if (usesPresentationClone)
                    SetRenderersVisible(renderers, rendererForceOff, true);
#endif
                EnsureLightSettings(pipeline);

                if (keyLight != null && _lightIntensity > 0f)
                {
                    keyLight.enabled = true;
                    enabledKeyLight = true;
                }

                using var profile = NowProfiler.ModelPreviewRender.Auto();

                if (usesRawSubmission)
                    SubmitRawMeshes(camera);

                if (pipeline == null)
                {
                    camera.targetTexture = target;
                    camera.Render();
                    success = true;
                }
                else
                {
                    _renderRequest.destination = target;
                    _renderRequest.mipLevel = 0;
                    _renderRequest.slice = 0;
                    _renderRequest.face = CubemapFace.Unknown;

                    if (ReferenceEquals(_supportedPipeline, pipeline) ||
                        RenderPipeline.SupportsRenderRequest(camera, _renderRequest))
                    {
                        _supportedPipeline = pipeline;
                        RenderPipeline.SubmitRenderRequest(camera, _renderRequest);
                        success = true;
                    }
                    else
                    {
                        _unsupportedPipeline = pipeline;

                        if (!_warnedUnsupportedPipeline)
                        {
                            _warnedUnsupportedPipeline = true;
                            Debug.LogWarning(
                                "NowUI: the active render pipeline does not support standard offscreen camera requests; " +
                                "this model preview will keep its previous texture. Call RequestRender after changing pipelines.",
                                _rig);
                        }
                    }
                }
            }
            finally
            {
                if (pipeline == null && camera != null)
                    camera.targetTexture = previousTarget;

                // The request object is shared; do not let it retain the last
                // preview's RenderTexture after that preview is disposed.
                _renderRequest.destination = null;
                RenderTexture.active = previousActive;

                if (enabledKeyLight && keyLight != null)
                    keyLight.enabled = false;

#if UNITY_INCLUDE_TESTS
                if (usesPresentationClone)
                    SetRenderersVisible(renderers, rendererForceOff, false);
#endif
                RestoreBuiltInSceneDirectionals();
            }

            if (success)
            {
                _unsupportedPipeline = null;

                // Camera callbacks can change the source/settings or even
                // dispose this preview. Do not clear a newer invalidation.
                if (!_disposed && _dirtyVersion == renderVersion && ReferenceEquals(_target, target))
                {
                    _dirty = false;
                    _renderQueued = false;
                }

                _lastRenderedFrame = Time.frameCount;
            }

            return success;
        }

        void CreateRig(Vector3 stageOrigin, Scene isolatedScene)
        {
            _rig = CreateHiddenObject("Now Model Preview");
            _rig.SetActive(false);
            _rig.transform.position = stageOrigin;

            if (isolatedScene.IsValid())
                SceneManager.MoveGameObjectToScene(_rig, isolatedScene);

#if UNITY_INCLUDE_TESTS
            if (usesPresentationClone)
            {
                var pivotObject = CreateHiddenObject("Content", _rig.transform);
                _contentPivot = pivotObject.transform;

                var centeringObject = CreateHiddenObject("Centered Model", _contentPivot);
                _centeringRoot = centeringObject.transform;
            }
#endif

            var cameraObject = usesPresentationClone
                ? CreateHiddenObject("Camera", _rig.transform)
                : _rig;
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            // Camera-type-aware pipeline features can cheaply reject previews.
            // Project-specific features still need to apply that filter.
            _camera.cameraType = CameraType.Preview;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            ApplyCameraBackground();
            _camera.cullingMask = _cameraCullingMask;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            _camera.depth = -1000f;
            ApplyCameraScene();
            ApplyPostProcessingSettings(GraphicsSettings.currentRenderPipeline);

            _keyLight = NowModelPreviewManager.AcquireSharedKeyLight(isolatedScene);
            ApplyLightSettings();

            SetLayerRecursively(_rig, _previewLayer);

            _rig.SetActive(true);
        }

        void SetSourceInternal(GameObject source)
        {
            bool rigWasActive = _rig != null && _rig.activeSelf;

            if (_rig != null)
                _rig.SetActive(false);

#if UNITY_INCLUDE_TESTS
            if (usesPresentationClone)
                SetRenderersVisible(false);

            if (_instance != null)
                DestroyObject(_instance);
#endif

            ReleaseRawMeshes();

            _source = source;
#if UNITY_INCLUDE_TESTS
            _instance = null;
#endif
            _renderers = Array.Empty<Renderer>();
            _rawMeshSources = Array.Empty<RawMeshSource>();
            _rawMeshDraws = Array.Empty<RawMeshDraw>();
            _rawSkinnedSourceIndices = Array.Empty<int>();
            _rawUnsupportedRendererCount = 0;
#if UNITY_INCLUDE_TESTS
            _rendererOriginallyForceOff = Array.Empty<bool>();
            _skinnedRenderers = Array.Empty<SkinnedMeshRenderer>();
            _skinnedOriginallyUpdateOffscreen = Array.Empty<bool>();
            _animators = Array.Empty<Animator>();
            _animatorCullingModes = Array.Empty<AnimatorCullingMode>();
            _animatorOriginallyEnabled = Array.Empty<bool>();
            _animations = Array.Empty<Animation>();
            _animationCullingModes = Array.Empty<AnimationCullingType>();
            _animationOriginallyEnabled = Array.Empty<bool>();
            _particleSystems = Array.Empty<ParticleSystem>();
            _particlePausedByPolicy = Array.Empty<bool>();
#endif
            _rotationInvariantRadius = 0f;
            _rawContentCenter = Vector3.zero;
            _rawContentScale = 1f;
            _rawWorldTransformsDirty = true;
            _recalculateFramingRadius = true;
            _refreshHierarchyPending = false;
#if UNITY_INCLUDE_TESTS
            if (_contentPivot != null)
            {
                _contentPivot.localPosition = Vector3.zero;
                _contentPivot.localRotation = Quaternion.identity;
                _contentPivot.localScale = Vector3.one;
            }

            if (_centeringRoot != null)
            {
                _centeringRoot.localPosition = Vector3.zero;
                _centeringRoot.localRotation = Quaternion.identity;
                _centeringRoot.localScale = Vector3.one;
            }
#endif

            if (source != null)
            {
#if UNITY_INCLUDE_TESTS
                if (usesPresentationClone)
                {
                    _instance = UnityEngine.Object.Instantiate(source, _centeringRoot, false);
                    _instance.name = source.name + " (Preview)";
                    SanitizeClone(_instance);
                    _instance.SetActive(true);
                    CacheRenderers();
                    CacheAnimationState();
                    NormalizeContent();
                }
                else
#endif
                if (usesRawSubmission)
                {
                    CacheRawMeshes(source);
                    NormalizeContent();
                }
                else
                {
                    CacheSceneRenderers();
                }
            }

#if UNITY_INCLUDE_TESTS
            if (_contentPivot != null)
                _contentPivot.localRotation = _contentRotation;
#endif

            if (usesRawSubmission)
                _rawWorldTransformsDirty = true;

            if (_rig != null)
                _rig.SetActive(rigWasActive);

#if UNITY_INCLUDE_TESTS
            if (usesPresentationClone)
                SetRenderersVisible(false);
#endif
            ApplyAnimationPolicy();
            InvalidatePixels(reframe: true, wake: false);
        }

        void RefreshHierarchyInternal()
        {
            if (usesRawSubmission)
            {
                // Normalize from the source's unmodified hierarchy every time.
                // Reusing the previous data transform here would compound the
                // normalization across repeated Reframe() calls.
                _rawContentCenter = Vector3.zero;
                _rawContentScale = 1f;
                _rawWorldTransformsDirty = true;
                ReleaseRawMeshes();
                CacheRawMeshes(_source);
                NormalizeContent();
                _rawWorldTransformsDirty = true;
                _recalculateFramingRadius = true;
                return;
            }

            if (_sourceMode == NowModelPreviewSourceMode.SceneObject)
            {
                CacheSceneRenderers();
                _rotationInvariantRadius = 0f;
                _recalculateFramingRadius = false;
                return;
            }

#if UNITY_INCLUDE_TESTS
            if (_instance == null)
                return;

            bool rigWasActive = _rig != null && _rig.activeSelf;

            if (_rig != null)
                _rig.SetActive(false);

            var previousParticles = _particleSystems;
            var previousParticlePolicy = _particlePausedByPolicy;
            RestoreCachedPresentationState();
            SanitizeClone(_instance);
            CacheRenderers();
            CacheAnimationState(previousParticles, previousParticlePolicy);

            if (_rig != null)
                _rig.SetActive(rigWasActive);

            SetRenderersVisible(false);
            ApplyAnimationPolicy();
            _recalculateFramingRadius = true;
#endif
        }

#if UNITY_INCLUDE_TESTS
        void RestoreCachedPresentationState()
        {
            SetRenderersVisible(_renderers, _rendererOriginallyForceOff, true);

            for (int i = 0; i < _animators.Length; ++i)
            {
                var animator = _animators[i];

                if (animator == null)
                    continue;

                animator.cullingMode = _animatorCullingModes[i];
                animator.enabled = _animatorOriginallyEnabled[i];
            }

            for (int i = 0; i < _animations.Length; ++i)
            {
                var animation = _animations[i];

                if (animation == null)
                    continue;

                animation.cullingType = _animationCullingModes[i];
                animation.enabled = _animationOriginallyEnabled[i];
            }

            for (int i = 0; i < _skinnedRenderers.Length; ++i)
            {
                if (_skinnedRenderers[i] != null)
                    _skinnedRenderers[i].updateWhenOffscreen =
                        _skinnedOriginallyUpdateOffscreen[i];
            }

            for (int i = 0; i < _particleSystems.Length; ++i)
            {
                if (_particleSystems[i] != null && _particlePausedByPolicy[i])
                    _particleSystems[i].Play(false);
            }
        }

        void SanitizeClone(GameObject clone)
        {
            SetLayerRecursively(clone, _previewLayer);

            // Keep animation components available for presentation, but stop
            // every other Behaviour before the inactive staging rig is shown.
            var behaviours = clone.GetComponentsInChildren<Behaviour>(true);

            for (int i = 0; i < behaviours.Length; ++i)
            {
                var behaviour = behaviours[i];

                if (behaviour == null)
                    continue;

                if (behaviour is Animator)
                    continue;

                if (behaviour is Animation)
                    continue;

                behaviour.enabled = false;
            }

            var rigidbodies = clone.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < rigidbodies.Length; ++i)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }

            var rigidbodies2D = clone.GetComponentsInChildren<Rigidbody2D>(true);

            for (int i = 0; i < rigidbodies2D.Length; ++i)
                rigidbodies2D[i].simulated = false;

            var colliders = clone.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; ++i)
                colliders[i].enabled = false;

            var colliders2D = clone.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < colliders2D.Length; ++i)
                colliders2D[i].enabled = false;
        }

        void CacheRenderers()
        {
            _renderers = _instance != null
                ? _instance.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            _rendererOriginallyForceOff = new bool[_renderers.Length];

            for (int i = 0; i < _renderers.Length; ++i)
            {
                var renderer = _renderers[i];

                if (renderer == null)
                    continue;

                _rendererOriginallyForceOff[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
                renderer.renderingLayerMask = _sceneLightingEnabled
                    ? uint.MaxValue
                    : PreviewRenderingLayerMask;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            }

            _rendererLightingLayersDirty = false;
        }
#endif

        void CacheSceneRenderers()
        {
            _renderers = _source != null
                ? _source.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
        }

        void CacheRawMeshes(GameObject source)
        {
            if (source == null)
                return;

            var renderers = _rawRendererScratch;
            var sources = _rawSourceScratch;
            var draws = _rawDrawScratch;
            var skinnedSourceIndices = _rawSkinnedIndexScratch;
            var materials = _rawMaterialScratch;
            renderers.Clear();
            sources.Clear();
            draws.Clear();
            skinnedSourceIndices.Clear();
            materials.Clear();
            source.GetComponentsInChildren(true, renderers);
            Matrix4x4 worldToSourceParent = Matrix4x4.TRS(
                source.transform.localPosition,
                source.transform.localRotation,
                source.transform.localScale) * source.transform.worldToLocalMatrix;
            Dictionary<LODGroup, HashSet<Renderer>> highestLodRenderers = null;

            for (int rendererIndex = 0; rendererIndex < renderers.Count; ++rendererIndex)
            {
                var renderer = renderers[rendererIndex];

                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                    !IsActiveInsideSource(renderer.transform, source.transform))
                    continue;

                // Raw submission has no camera-driven LOD selection. Use the
                // highest-detail LOD deterministically and ignore lower levels.
                var lodGroup = renderer.LODGroup;

                if (lodGroup != null)
                {
                    highestLodRenderers ??= new Dictionary<LODGroup, HashSet<Renderer>>();

                    if (!highestLodRenderers.TryGetValue(lodGroup, out var highestDetail))
                    {
                        highestDetail = new HashSet<Renderer>();
                        highestLodRenderers.Add(lodGroup, highestDetail);
                        var lods = lodGroup.GetLODs();

                        if (lods.Length > 0 && lods[0].renderers != null)
                        {
                            for (int lodRendererIndex = 0;
                                lodRendererIndex < lods[0].renderers.Length;
                                ++lodRendererIndex)
                            {
                                if (lods[0].renderers[lodRendererIndex] != null)
                                    highestDetail.Add(lods[0].renderers[lodRendererIndex]);
                            }
                        }
                    }

                    if (!highestDetail.Contains(renderer))
                        continue;
                }

                Mesh mesh = null;
                Mesh ownedBakedMesh = null;
                SkinnedMeshRenderer skinnedRenderer = null;

                if (renderer is MeshRenderer meshRenderer)
                {
                    var filter = meshRenderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }
                else if (renderer is SkinnedMeshRenderer candidate && candidate.sharedMesh != null)
                {
                    skinnedRenderer = candidate;
                    ownedBakedMesh = new Mesh
                    {
                        name = candidate.sharedMesh.name + " (Now Preview Snapshot)",
                        hideFlags = HideFlags.HideAndDontSave
                    };

                    try
                    {
                        candidate.BakeMesh(ownedBakedMesh, true);
                        mesh = ownedBakedMesh;
                    }
                    catch (UnityException)
                    {
                        DestroyObject(ownedBakedMesh);
                        ownedBakedMesh = null;
                    }
                }

                if (mesh == null)
                {
                    ++_rawUnsupportedRendererCount;
                    continue;
                }

                int sourceIndex = sources.Count;
                sources.Add(new RawMeshSource
                {
                    mesh = mesh,
                    ownedBakedMesh = ownedBakedMesh,
                    skinnedRenderer = skinnedRenderer,
                    localToRoot = worldToSourceParent * renderer.transform.localToWorldMatrix
                });

                if (skinnedRenderer != null)
                    skinnedSourceIndices.Add(sourceIndex);

                int subMeshCount = mesh.subMeshCount;
                materials.Clear();
                renderer.GetSharedMaterials(materials);

                if (subMeshCount <= 0 || materials.Count <= 0)
                    continue;

                bool hasPropertyBlock = renderer.HasPropertyBlock();

                for (int materialIndex = 0; materialIndex < materials.Count; ++materialIndex)
                {
                    var material = materials[materialIndex];

                    if (material == null)
                        continue;

                    MaterialPropertyBlock properties = null;

                    if (hasPropertyBlock)
                    {
                        properties = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(properties, materialIndex);

                        if (properties.isEmpty)
                            properties = null;
                    }

                    // Unity applies surplus material slots to the final
                    // submesh as additional passes; preserve that behavior.
                    draws.Add(new RawMeshDraw
                    {
                        sourceIndex = sourceIndex,
                        submeshIndex = Mathf.Min(materialIndex, subMeshCount - 1),
                        material = material,
                        properties = properties,
                        shadowCastingMode = renderer.shadowCastingMode,
                        receiveShadows = renderer.receiveShadows,
                        rendererPriority = renderer.rendererPriority
                    });
                }
            }

            _rawMeshSources = sources.ToArray();
            _rawMeshDraws = draws.ToArray();
            _rawSkinnedSourceIndices = skinnedSourceIndices.Count > 0
                ? skinnedSourceIndices.ToArray()
                : Array.Empty<int>();
            _rawWorldTransformsDirty = true;
            renderers.Clear();
            sources.Clear();
            draws.Clear();
            skinnedSourceIndices.Clear();
            materials.Clear();
        }

        void SubmitRawMeshes(Camera camera)
        {
            if (camera == null)
                return;

            BakeRawSkinnedMeshes();
            EnsureRawWorldTransforms();
            uint renderingLayerMask = _sceneLightingEnabled
                ? uint.MaxValue
                : PreviewRenderingLayerMask;

            for (int i = 0; i < _rawMeshDraws.Length; ++i)
            {
                ref var draw = ref _rawMeshDraws[i];

                if (draw.material == null)
                    continue;

                ref var source = ref _rawMeshSources[draw.sourceIndex];
                var mesh = source.mesh;

                // Source/submesh indices are validated while caching. Structural
                // mesh changes require Reframe(), matching the material cache.
                if (mesh == null)
                    continue;

                var parameters = new RenderParams(draw.material)
                {
                    camera = camera,
                    layer = _previewLayer,
                    renderingLayerMask = renderingLayerMask,
                    worldBounds = source.worldBounds,
                    matProps = draw.properties,
                    shadowCastingMode = draw.shadowCastingMode,
                    receiveShadows = draw.receiveShadows,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                    rendererPriority = draw.rendererPriority
                };

                Graphics.RenderMesh(parameters, mesh, draw.submeshIndex, source.objectToWorld);
            }
        }

        void BakeRawSkinnedMeshes()
        {
            for (int i = 0; i < _rawSkinnedSourceIndices.Length; ++i)
            {
                int sourceIndex = _rawSkinnedSourceIndices[i];

                if (sourceIndex < 0 || sourceIndex >= _rawMeshSources.Length)
                    continue;

                ref var source = ref _rawMeshSources[sourceIndex];

                if (source.skinnedRenderer == null || source.ownedBakedMesh == null)
                    continue;

                try
                {
                    source.skinnedRenderer.BakeMesh(source.ownedBakedMesh, true);
                    source.mesh = source.ownedBakedMesh;

                    if (!_rawWorldTransformsDirty)
                    {
                        source.worldBounds = TransformBounds(
                            source.mesh.bounds,
                            source.objectToWorld);
                    }
                }
                catch (UnityException)
                {
                    source.mesh = null;
                }

            }
        }

        void EnsureRawWorldTransforms()
        {
            if (!_rawWorldTransformsDirty)
                return;

            Matrix4x4 rootMatrix = Matrix4x4.TRS(
                NowModelPreviewManager.sharedStageOrigin,
                _contentRotation,
                Vector3.one * _rawContentScale) *
                Matrix4x4.Translate(-_rawContentCenter);

            for (int i = 0; i < _rawMeshSources.Length; ++i)
            {
                ref var source = ref _rawMeshSources[i];
                source.objectToWorld = rootMatrix * source.localToRoot;
                source.worldBounds = source.mesh != null
                    ? TransformBounds(source.mesh.bounds, source.objectToWorld)
                    : default;
            }

            _rawWorldTransformsDirty = false;
        }

        void ReleaseRawMeshes()
        {
            for (int i = 0; i < _rawMeshSources.Length; ++i)
            {
                if (_rawMeshSources[i].ownedBakedMesh != null)
                    DestroyObject(_rawMeshSources[i].ownedBakedMesh);
            }

            _rawMeshSources = Array.Empty<RawMeshSource>();
            _rawMeshDraws = Array.Empty<RawMeshDraw>();
            _rawSkinnedSourceIndices = Array.Empty<int>();
            _rawUnsupportedRendererCount = 0;
            _rawWorldTransformsDirty = true;
        }

        static bool IsActiveInsideSource(Transform candidate, Transform root)
        {
            while (candidate != null && candidate != root)
            {
                if (!candidate.gameObject.activeSelf)
                    return false;

                candidate = candidate.parent;
            }

            return candidate == root;
        }

        static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center), extents * 2f);
        }
#if UNITY_INCLUDE_TESTS
        void CacheAnimationState(
            ParticleSystem[] previousParticles = null,
            bool[] previousParticlePolicy = null)
        {
            _animators = _instance != null
                ? _instance.GetComponentsInChildren<Animator>(true)
                : Array.Empty<Animator>();
            _animatorCullingModes = new AnimatorCullingMode[_animators.Length];
            _animatorOriginallyEnabled = new bool[_animators.Length];

            for (int i = 0; i < _animators.Length; ++i)
            {
                if (_animators[i] != null)
                {
                    _animatorCullingModes[i] = _animators[i].cullingMode;
                    _animatorOriginallyEnabled[i] = _animators[i].enabled;
                }
            }

            _animations = _instance != null
                ? _instance.GetComponentsInChildren<Animation>(true)
                : Array.Empty<Animation>();
            _animationCullingModes = new AnimationCullingType[_animations.Length];
            _animationOriginallyEnabled = new bool[_animations.Length];

            for (int i = 0; i < _animations.Length; ++i)
            {
                if (_animations[i] != null)
                {
                    _animationCullingModes[i] = _animations[i].cullingType;
                    _animationOriginallyEnabled[i] = _animations[i].enabled;
                }
            }

            _skinnedRenderers = _instance != null
                ? _instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                : Array.Empty<SkinnedMeshRenderer>();
            _skinnedOriginallyUpdateOffscreen = new bool[_skinnedRenderers.Length];

            for (int i = 0; i < _skinnedRenderers.Length; ++i)
            {
                if (_skinnedRenderers[i] != null)
                    _skinnedOriginallyUpdateOffscreen[i] = _skinnedRenderers[i].updateWhenOffscreen;
            }

            _particleSystems = _instance != null
                ? _instance.GetComponentsInChildren<ParticleSystem>(true)
                : Array.Empty<ParticleSystem>();
            _particlePausedByPolicy = new bool[_particleSystems.Length];

            if (previousParticles != null && previousParticlePolicy != null)
            {
                for (int i = 0; i < _particleSystems.Length; ++i)
                {
                    for (int j = 0; j < previousParticles.Length &&
                        j < previousParticlePolicy.Length; ++j)
                    {
                        if (!ReferenceEquals(_particleSystems[i], previousParticles[j]))
                            continue;

                        _particlePausedByPolicy[i] = previousParticlePolicy[j];
                        break;
                    }
                }
            }

            ApplyAnimationPolicy();
        }
#endif

        void ApplyAnimationPolicy()
        {
#if UNITY_INCLUDE_TESTS
            bool continuous = _updateMode == NowModelPreviewUpdateMode.EveryFrame &&
                _renderingEnabled && _hasDrawn;

            for (int i = 0; i < _animators.Length; ++i)
            {
                var animator = _animators[i];

                if (animator == null)
                    continue;

                animator.enabled = continuous && _animatorOriginallyEnabled[i];
                animator.cullingMode = continuous
                    ? AnimatorCullingMode.AlwaysAnimate
                    : _animatorCullingModes[i];
            }

            for (int i = 0; i < _animations.Length; ++i)
            {
                var animation = _animations[i];

                if (animation == null)
                    continue;

                animation.enabled = continuous && _animationOriginallyEnabled[i];
                animation.cullingType = continuous
                    ? AnimationCullingType.AlwaysAnimate
                    : _animationCullingModes[i];
            }

            for (int i = 0; i < _skinnedRenderers.Length; ++i)
            {
                if (_skinnedRenderers[i] != null)
                    _skinnedRenderers[i].updateWhenOffscreen = continuous;
            }

            for (int i = 0; i < _particleSystems.Length; ++i)
            {
                var particles = _particleSystems[i];

                if (particles == null)
                    continue;

                if (continuous)
                {
                    if (_particlePausedByPolicy[i])
                    {
                        particles.Play(false);
                        _particlePausedByPolicy[i] = false;
                    }
                }
                else if (particles.isPlaying)
                {
                    particles.Pause(false);
                    _particlePausedByPolicy[i] = true;
                }
            }
#endif
        }

        void NormalizeContent()
        {
            if (usesRawSubmission)
            {
                if (!TryGetRawLocalBounds(out var rawBounds))
                    return;

                _rawContentCenter = rawBounds.center;
                float rawLargest = Mathf.Max(
                    rawBounds.size.x,
                    Mathf.Max(rawBounds.size.y, rawBounds.size.z));
                _rawContentScale = IsFinite(rawLargest) && rawLargest > 0.000001f
                    ? NormalizedModelSize / rawLargest
                    : 1f;
                _rawWorldTransformsDirty = true;
                UpdateFramingRadius();
                return;
            }

#if UNITY_INCLUDE_TESTS
            if (!TryGetRendererBounds(out var bounds))
                return;

            Vector3 localCenter = _contentPivot.InverseTransformPoint(bounds.center);
            _centeringRoot.localPosition = -localCenter;

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

            if (IsFinite(largest) && largest > 0.000001f)
                _contentPivot.localScale = Vector3.one * (NormalizedModelSize / largest);

            UpdateFramingRadius();
#endif
        }

        void ReframeInternal()
        {
            if (_camera == null)
                return;

            if (_recalculateFramingRadius)
                UpdateFramingRadius();

            Bounds bounds;

            if (!TryGetRendererBounds(out bounds))
                bounds = new Bounds(GetPresentationCenter(), Vector3.one);

            float aspect = _target != null && _target.height > 0
                ? (float)_target.width / _target.height
                : 1f;

            Vector3 position;
            Quaternion rotation;
            float orthographicSize;
            float nearClip;
            float farClip;

            if (_rotationInvariantRadius > 0.0001f)
            {
                CalculateSphereFraming(
                    GetPresentationCenter(),
                    _rotationInvariantRadius,
                    _viewDirection,
                    aspect,
                    _fieldOfView,
                    _framingPadding,
                    _orthographic,
                    out position,
                    out rotation,
                    out orthographicSize,
                    out nearClip,
                    out farClip);
            }
            else
            {
                CalculateFraming(
                    bounds,
                    _viewDirection,
                    aspect,
                    _fieldOfView,
                    _framingPadding,
                    _orthographic,
                    out position,
                    out rotation,
                    out orthographicSize,
                    out nearClip,
                    out farClip);
            }

            _camera.transform.SetPositionAndRotation(position, rotation);
            _camera.aspect = aspect;
            _camera.fieldOfView = _fieldOfView;
            _camera.orthographic = _orthographic;
            _camera.orthographicSize = orthographicSize;
            _camera.nearClipPlane = nearClip;
            _camera.farClipPlane = farClip;
            _frameDirty = false;
        }

        void UpdateFramingRadius()
        {
            _rotationInvariantRadius = 0f;
            _recalculateFramingRadius = false;

            if (_sourceMode == NowModelPreviewSourceMode.SceneObject)
                return;

            if (!TryGetRendererBounds(out var bounds))
                return;

            Vector3 center = GetPresentationCenter();

            for (int i = 0; i < 8; ++i)
            {
                Vector3 corner = bounds.center + new Vector3(
                    (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                _rotationInvariantRadius = Mathf.Max(
                    _rotationInvariantRadius,
                    Vector3.Distance(center, corner));
            }
        }

        Vector3 GetPresentationCenter()
        {
            if (usesRawSubmission)
                return NowModelPreviewManager.sharedStageOrigin;

#if UNITY_INCLUDE_TESTS
            return _contentPivot != null ? _contentPivot.position : Vector3.zero;
#else
            return Vector3.zero;
#endif
        }

        bool TryGetRendererBounds(out Bounds bounds)
        {
            if (usesRawSubmission)
                return TryGetRawMeshBounds(out bounds);

            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _renderers.Length; ++i)
            {
                var renderer = _renderers[i];

                if (renderer == null || !renderer.enabled)
                    continue;

                bool visible = renderer.gameObject.activeInHierarchy &&
                    !renderer.forceRenderingOff &&
                    IsInsideHierarchy(renderer.transform, _source != null ? _source.transform : null);
#if UNITY_INCLUDE_TESTS
                if (_sourceMode != NowModelPreviewSourceMode.SceneObject)
                {
                    visible = IsActiveInsideClone(renderer.transform) &&
                        !(i < _rendererOriginallyForceOff.Length && _rendererOriginallyForceOff[i]);
                }
#endif

                if (!visible)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds && IsFinite(bounds.center) && IsFinite(bounds.size);
        }

        bool TryGetRawLocalBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _rawMeshSources.Length; ++i)
            {
                ref var source = ref _rawMeshSources[i];

                if (source.mesh == null)
                    continue;

                Bounds candidate = TransformBounds(source.mesh.bounds, source.localToRoot);

                if (!IsFinite(candidate.center) || !IsFinite(candidate.size))
                    continue;

                if (!hasBounds)
                {
                    bounds = candidate;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            return hasBounds;
        }

        bool TryGetRawMeshBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            EnsureRawWorldTransforms();

            for (int i = 0; i < _rawMeshSources.Length; ++i)
            {
                ref var source = ref _rawMeshSources[i];

                if (source.mesh == null)
                    continue;

                Bounds candidate = source.worldBounds;

                if (!IsFinite(candidate.center) || !IsFinite(candidate.size))
                    continue;

                if (!hasBounds)
                {
                    bounds = candidate;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            return hasBounds;
        }
#if UNITY_INCLUDE_TESTS
        bool IsActiveInsideClone(Transform candidate)
        {
            var root = _instance != null ? _instance.transform : null;

            while (candidate != null)
            {
                if (!candidate.gameObject.activeSelf)
                    return false;

                if (candidate == root)
                    return true;

                candidate = candidate.parent;
            }

            return false;
        }
#endif

        static bool IsInsideHierarchy(Transform candidate, Transform root)
        {
            while (candidate != null)
            {
                if (candidate == root)
                    return true;

                candidate = candidate.parent;
            }

            return false;
        }

        bool ResizePreparedTarget()
        {
            if (_fixedResolution && _prepared)
                return EnsureTarget(_fixedWidth, _fixedHeight);

            return !_fixedResolution && _lastRequestedPixelWidth > 0f &&
                _lastRequestedPixelHeight > 0f && EnsureAutomaticTarget();
        }

        bool EnsureAutomaticTarget()
        {
            QuantizeAutomaticResolution(
                _lastRequestedPixelWidth * _resolutionScale,
                _lastRequestedPixelHeight * _resolutionScale,
                out int width,
                out int height);
            return EnsureTarget(width, height);
        }

        bool EnsureTarget(int requestedWidth, int requestedHeight)
        {
            int systemLimit = Mathf.Max(1, SystemInfo.maxTextureSize);
            int limit = Mathf.Max(1, Mathf.Min(_maxResolution, systemLimit));
            float longest = Mathf.Max(requestedWidth, requestedHeight);

            if (longest > limit)
            {
                float scale = limit / longest;
                requestedWidth = Mathf.Max(1, Mathf.RoundToInt(requestedWidth * scale));
                requestedHeight = Mathf.Max(1, Mathf.RoundToInt(requestedHeight * scale));
            }

            int width = Mathf.Clamp(requestedWidth, 1, limit);
            int height = Mathf.Clamp(requestedHeight, 1, limit);

            if (IsUnsafeToMutateResources())
            {
                if (_pendingTargetWidth == width && _pendingTargetHeight == height)
                    return false;

                if (_pendingTargetWidth == 0 && _target != null && _target.IsCreated() &&
                    _target.width == width && _target.height == height)
                    return false;

                _pendingTargetWidth = width;
                _pendingTargetHeight = height;
                InvalidatePixels(reframe: true, wake: false);
                NowModelPreviewManager.Wake();
                return true;
            }

            if (_target == null)
            {
                _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Now Model Preview",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1
                };
                _target.Create();
                ClearTarget(_target);
                _lastRenderedFrame = -1;
                InvalidatePixels(reframe: true, wake: false);
                return true;
            }

            if (_target.width == width && _target.height == height)
            {
                if (!_target.IsCreated())
                {
                    _target.Create();
                    ClearTarget(_target);
                    _lastRenderedFrame = -1;
                    InvalidatePixels(reframe: true, wake: false);
                    return true;
                }

                return false;
            }

            // Keep the Texture object stable: NowUI batches/materials cache by
            // texture identity, so replacing it on responsive layout changes
            // would grow caches and invalidate retained hosts.
            _target.Release();
            _target.width = width;
            _target.height = height;
            _target.Create();
            ClearTarget(_target);
            _lastRenderedFrame = -1;
            InvalidatePixels(reframe: true, wake: false);
            return true;
        }

#if UNITY_INCLUDE_TESTS
        internal void SetRenderersVisible(bool visible)
        {
            SetRenderersVisible(_renderers, _rendererOriginallyForceOff, visible);
        }

        static void SetRenderersVisible(Renderer[] renderers, bool[] originallyForceOff, bool visible)
        {
            int count = Mathf.Min(renderers?.Length ?? 0, originallyForceOff?.Length ?? 0);

            for (int i = 0; i < count; ++i)
            {
                var renderer = renderers[i];

                if (renderer != null)
                    renderer.forceRenderingOff = !visible || originallyForceOff[i];
            }
        }
#endif

        void InvalidatePixels(bool reframe = false, bool wake = true)
        {
            _dirty = true;

            if (reframe)
                _frameDirty = true;

            unchecked
            {
                ++_dirtyVersion;
            }

            RequestCaptureHostRebuilds();

            if (wake)
                NowModelPreviewManager.Wake();
        }

        internal void RegisterCaptureHost(INowDynamicTextureHost host)
        {
            if (host == null || !host.isDynamicTextureHostValid)
                return;

            int version = host.dynamicTextureBuildVersion;

            if (_captureHosts != null)
            {
                for (int i = 0; i < _captureHosts.Count; ++i)
                {
                    if (!ReferenceEquals(_captureHosts[i].host, host))
                        continue;

                    _captureHosts[i] = new CaptureHostDependency
                    {
                        host = host,
                        buildVersion = version
                    };
                    return;
                }
            }
            else
            {
                _captureHosts = new List<CaptureHostDependency>(1);
            }

            _captureHosts.Add(new CaptureHostDependency
            {
                host = host,
                buildVersion = version
            });
        }

        void RequestCaptureHostRebuilds()
        {
            if (_captureHosts == null)
                return;

            for (int i = _captureHosts.Count - 1; i >= 0; --i)
            {
                var dependency = _captureHosts[i];
                var host = dependency.host;

                if (host == null || !host.isDynamicTextureHostValid ||
                    host.dynamicTextureBuildVersion != dependency.buildVersion)
                {
                    _captureHosts.RemoveAt(i);
                    continue;
                }

                host.RequestDynamicTextureRebuild();
            }
        }

        internal void ApplyDeferredMutations()
        {
            if (_disposeRequested)
            {
                CompleteDeferredDispose();
                return;
            }

            bool changed = false;

            if (_hasPendingSource)
            {
                var source = _pendingSource;
                _pendingSource = null;
                _hasPendingSource = false;
                SetSourceInternal(source);
                changed = true;
            }

            if (_refreshHierarchyPending)
            {
                _refreshHierarchyPending = false;
                RefreshHierarchyInternal();
                changed = true;
            }

            if (_pendingTargetWidth > 0)
            {
                int width = _pendingTargetWidth;
                int height = _pendingTargetHeight;
                _pendingTargetWidth = 0;
                _pendingTargetHeight = 0;
                changed |= EnsureTarget(width, height);
            }

            if (changed)
                NowModelPreviewManager.Wake();
        }

        void ApplyCameraBackground()
        {
            if (_camera == null)
                return;

            // Camera clears write raw channels rather than blending. Store the
            // clear color premultiplied so the whole render target keeps one
            // alpha contract, including translucent backgrounds.
            Color stored;

            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                stored = _backgroundColor.linear;
                stored.r *= stored.a;
                stored.g *= stored.a;
                stored.b *= stored.a;
                float alpha = stored.a;
                stored = stored.gamma;
                stored.a = alpha;
            }
            else
            {
                stored = new Color(
                    _backgroundColor.r * _backgroundColor.a,
                    _backgroundColor.g * _backgroundColor.a,
                    _backgroundColor.b * _backgroundColor.a,
                    _backgroundColor.a);
            }

            _camera.backgroundColor = stored;
        }

        void ApplyCameraScene()
        {
            if (_camera == null)
                return;

            Scene desiredScene = _sourceMode == NowModelPreviewSourceMode.SceneObject ||
                _sceneLightingEnabled
                ? default
                : _isolatedScene;

            if (_camera.scene != desiredScene)
                _camera.scene = desiredScene;
        }

#if UNITY_INCLUDE_TESTS
        void ApplyRendererLightingLayers()
        {
            uint mask = _sceneLightingEnabled ? uint.MaxValue : PreviewRenderingLayerMask;

            for (int i = 0; i < _renderers.Length; ++i)
            {
                if (_renderers[i] != null)
                    _renderers[i].renderingLayerMask = mask;
            }

            _rendererLightingLayersDirty = false;
        }
#endif

        void MuteBuiltInSceneDirectionals()
        {
            _mutedDirectionalLights.Clear();
            var directionals = NowModelPreviewManager.GetSceneDirectionalLights();
            int previewMask = 1 << _previewLayer;

            for (int i = 0; i < directionals.Count; ++i)
            {
                var light = directionals[i];

                if (light == null || (light.cullingMask & previewMask) == 0)
                    continue;

                _mutedDirectionalLights.Add(new MutedDirectionalLight
                {
                    light = light,
                    cullingMask = light.cullingMask
                });
                light.cullingMask &= ~previewMask;
            }
        }

        void RestoreBuiltInSceneDirectionals()
        {
            for (int i = _mutedDirectionalLights.Count - 1; i >= 0; --i)
            {
                var state = _mutedDirectionalLights[i];

                if (state.light != null)
                    state.light.cullingMask = state.cullingMask;
            }

            _mutedDirectionalLights.Clear();
        }

        void ApplyPostProcessingSettings(RenderPipelineAsset pipeline)
        {
            _configuredCameraPipeline = pipeline;
            _postProcessingSettingsDirty = false;
            _supportedPipeline = null;
            _unsupportedPipeline = null;

            if (_camera == null)
                return;

            _camera.allowHDR = _postProcessingEnabled;
            // HDRP intentionally sanitizes post-processing off for Preview
            // cameras. Opt-in therefore uses Game camera semantics there;
            // registry-based preview filters remain authoritative.
            _camera.cameraType = _postProcessingEnabled && IsHighDefinitionPipeline(pipeline)
                ? CameraType.Game
                : CameraType.Preview;

            Type additionalDataType = null;
            bool universal = IsUniversalPipeline(pipeline);

            if (universal)
            {
                _universalAdditionalCameraDataType ??= Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime",
                    throwOnError: false);
                additionalDataType = _universalAdditionalCameraDataType;
            }
            else if (IsHighDefinitionPipeline(pipeline))
            {
                _hdAdditionalCameraDataType ??= Type.GetType(
                    "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime",
                    throwOnError: false);
                additionalDataType = _hdAdditionalCameraDataType;
            }

            if (additionalDataType == null)
                return;

            var additionalData = _camera.GetComponent(additionalDataType) ??
                _camera.gameObject.AddComponent(additionalDataType);
            var volumeMask = _postProcessingEnabled
                ? _postProcessingVolumeMask
                : (LayerMask)0;

            if (universal)
            {
                TrySetReflectedProperty(
                    additionalData,
                    "volumeLayerMask",
                    volumeMask,
                    ref _universalVolumeLayerMaskProperty);
                TrySetReflectedProperty(
                    additionalData,
                    "renderPostProcessing",
                    _postProcessingEnabled,
                    ref _universalRenderPostProcessingProperty);
            }
            else
            {
                TrySetReflectedProperty(
                    additionalData,
                    "volumeLayerMask",
                    volumeMask,
                    ref _hdVolumeLayerMaskProperty);
            }
        }

        static void TrySetReflectedProperty(
            Component component,
            string name,
            object value,
            ref PropertyInfo property)
        {
            if (component == null)
                return;

            property ??= component.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);

            if (property == null || !property.CanWrite)
                return;

            if (value is LayerMask layerMask && property.PropertyType == typeof(int))
                value = layerMask.value;

            if (value == null || property.PropertyType.IsInstanceOfType(value))
                property.SetValue(component, value);
        }

        void ApplyLightSettings()
        {
            // Camera callbacks may create or configure another preview while a
            // render is in flight. Do not let that reconfigure the shared light
            // until the new preview owns a render request.
            if (NowModelPreviewManager.isRendering)
                return;

            EnsureLightSettings(GraphicsSettings.currentRenderPipeline);
        }

        void EnsureLightSettings(RenderPipelineAsset pipeline)
        {
            var settings = CreateSharedLightSettings(pipeline);

            if (NowModelPreviewManager.IsSharedKeyLightConfigured(settings))
                return;

            PreparePipelineLight(_keyLight, pipeline);
            ApplyLightSettings(_keyLight, settings);
            NowModelPreviewManager.MarkSharedKeyLightConfigured(settings);
        }

        SharedLightSettings CreateSharedLightSettings(RenderPipelineAsset pipeline)
        {
            return new SharedLightSettings
            {
                rotation = _lightRotation,
                color = _lightColor,
                intensity = IsHighDefinitionPipeline(pipeline)
                    ? _lightIntensity * 100000f
                    : _lightIntensity,
                cullingMask = _cameraCullingMask,
                renderingLayerMask = _sourceMode == NowModelPreviewSourceMode.SceneObject
                    ? -1
                    : PreviewRenderingLayerMask,
                pipeline = pipeline
            };
        }

        static void ApplyLightSettings(Light light, in SharedLightSettings settings)
        {
            if (light == null)
                return;

            light.transform.localRotation = settings.rotation;
            light.color = settings.color;
            light.intensity = settings.intensity;
            light.cullingMask = settings.cullingMask;
            light.renderingLayerMask = settings.renderingLayerMask;
            light.shadows = LightShadows.None;
        }

        static void PreparePipelineLight(Light light, RenderPipelineAsset pipeline)
        {
            if (light == null || !IsHighDefinitionPipeline(pipeline))
                return;

            _hdAdditionalLightDataType ??= Type.GetType(
                "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, Unity.RenderPipelines.HighDefinition.Runtime",
                throwOnError: false);

            if (_hdAdditionalLightDataType != null && light.GetComponent(_hdAdditionalLightDataType) == null)
                light.gameObject.AddComponent(_hdAdditionalLightDataType);
        }

        static bool IsHighDefinitionPipeline(RenderPipelineAsset pipeline)
        {
            return pipeline != null && pipeline.GetType().FullName?.IndexOf(
                "HighDefinition",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsUniversalPipeline(RenderPipelineAsset pipeline)
        {
            return pipeline != null && pipeline.GetType().FullName?.IndexOf(
                "Universal",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void QuantizeAutomaticResolution(
            float requestedWidth,
            float requestedHeight,
            out int width,
            out int height)
        {
            requestedWidth = Mathf.Max(1f, requestedWidth);
            requestedHeight = Mathf.Max(1f, requestedHeight);
            bool widthIsLongest = requestedWidth >= requestedHeight;
            float longest = widthIsLongest ? requestedWidth : requestedHeight;
            int rounded = Mathf.Max(1, Mathf.CeilToInt(longest));

            if (rounded <= int.MaxValue - (AutomaticResolutionQuantum - 1))
                rounded = ((rounded + AutomaticResolutionQuantum - 1) / AutomaticResolutionQuantum) *
                    AutomaticResolutionQuantum;

            float scale = rounded / longest;
            width = widthIsLongest
                ? rounded
                : Mathf.Max(1, Mathf.RoundToInt(requestedWidth * scale));
            height = widthIsLongest
                ? Mathf.Max(1, Mathf.RoundToInt(requestedHeight * scale))
                : rounded;
        }

        static void ClearTarget(RenderTexture target)
        {
            var previous = RenderTexture.active;

            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        public void Dispose()
        {
            if (_disposed || _disposeRequested)
                return;

            if (IsUnsafeToMutateResources())
            {
                _disposeRequested = true;
                _renderingEnabled = false;
                ApplyAnimationPolicy();
                NowModelPreviewManager.Wake();
                return;
            }

            DisposeImmediately();
        }

        internal void CompleteDeferredDispose()
        {
            if (!_disposeRequested || _disposed)
                return;

            _disposeRequested = false;
            DisposeImmediately();
        }

        void DisposeImmediately()
        {
            if (_disposed)
                return;

            _disposed = true;
            RequestCaptureHostRebuilds();
            NowModelPreviewManager.Unregister(this, _camera);

#if UNITY_INCLUDE_TESTS
            if (usesPresentationClone)
                SetRenderersVisible(false);
#endif

            ReleaseRawMeshes();

            if (_target != null)
            {
                Now.ReleaseTextureMaterials(_target);
                _target.Release();
                DestroyObject(_target);
                _target = null;
            }

            if (_rig != null)
            {
                DestroyObject(_rig);
                _rig = null;
            }

            if (_isolatedScene.IsValid())
            {
                var isolatedScene = _isolatedScene;
                _isolatedScene = default;
                NowModelPreviewManager.ReleasePreviewScene(isolatedScene);
            }

            _source = null;
#if UNITY_INCLUDE_TESTS
            _instance = null;
#endif
            _camera = null;
            _keyLight = null;
#if UNITY_INCLUDE_TESTS
            _contentPivot = null;
            _centeringRoot = null;
#endif
            _renderers = Array.Empty<Renderer>();
            _rawMeshSources = Array.Empty<RawMeshSource>();
            _rawMeshDraws = Array.Empty<RawMeshDraw>();
            _rawSkinnedSourceIndices = Array.Empty<int>();
            _captureHosts?.Clear();
            _captureHosts = null;
#if UNITY_INCLUDE_TESTS
            _rendererOriginallyForceOff = Array.Empty<bool>();
            _skinnedRenderers = Array.Empty<SkinnedMeshRenderer>();
            _skinnedOriginallyUpdateOffscreen = Array.Empty<bool>();
            _animators = Array.Empty<Animator>();
            _animatorCullingModes = Array.Empty<AnimatorCullingMode>();
            _animatorOriginallyEnabled = Array.Empty<bool>();
            _animations = Array.Empty<Animation>();
            _animationCullingModes = Array.Empty<AnimationCullingType>();
            _animationOriginallyEnabled = Array.Empty<bool>();
            _particleSystems = Array.Empty<ParticleSystem>();
            _particlePausedByPolicy = Array.Empty<bool>();
#endif
            _pendingSource = null;
            _hasPendingSource = false;
            _refreshHierarchyPending = false;
            _pendingTargetWidth = 0;
            _pendingTargetHeight = 0;
            _unsupportedPipeline = null;
            _supportedPipeline = null;
            _configuredCameraPipeline = null;
        }

        bool IsUnsafeToMutateResources()
        {
            return Camera.current != null || NowModelPreviewManager.isInsidePipelineBuild ||
                NowModelPreviewManager.IsRendering(this);
        }

        internal static void CalculateFraming(
            Bounds bounds,
            Vector3 viewDirection,
            float aspect,
            float fieldOfView,
            float padding,
            bool orthographic,
            out Vector3 position,
            out Quaternion rotation,
            out float orthographicSize,
            out float nearClip,
            out float farClip)
        {
            viewDirection = viewDirection.sqrMagnitude > 0.000001f
                ? viewDirection.normalized
                : Vector3.forward;
            aspect = Mathf.Max(0.0001f, aspect);
            fieldOfView = Mathf.Clamp(fieldOfView, 1.01f, 178.99f);
            padding = Mathf.Max(0.0001f, padding);

            Vector3 forward = -viewDirection;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                ? Vector3.forward
                : Vector3.up;
            rotation = Quaternion.LookRotation(forward, up);
            Vector3 right = rotation * Vector3.right;
            Vector3 cameraUp = rotation * Vector3.up;
            float maxX = 0f;
            float maxY = 0f;
            float minDepth = float.PositiveInfinity;
            float maxDepth = float.NegativeInfinity;

            for (int i = 0; i < 8; ++i)
            {
                Vector3 corner = bounds.center + new Vector3(
                    (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                Vector3 relative = corner - bounds.center;
                maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(relative, right)));
                maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(relative, cameraUp)));
                float depth = Vector3.Dot(relative, forward);
                minDepth = Mathf.Min(minDepth, depth);
                maxDepth = Mathf.Max(maxDepth, depth);
            }

            float extent = Mathf.Max(0.001f, bounds.extents.magnitude);
            float margin = Mathf.Max(0.01f, extent * 0.05f);

            if (orthographic)
            {
                orthographicSize = Mathf.Max(0.01f, Mathf.Max(maxY, maxX / aspect) * padding);
                float distance = extent + 1f;
                position = bounds.center - forward * distance;
                nearClip = Mathf.Max(0.001f, distance + minDepth - margin);
                farClip = Mathf.Max(nearClip + 0.01f, distance + maxDepth + margin);
                return;
            }

            float tanVertical = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * aspect;
            float distanceRequired = 0.01f;

            for (int i = 0; i < 8; ++i)
            {
                Vector3 corner = bounds.center + new Vector3(
                    (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                Vector3 relative = corner - bounds.center;
                float x = Mathf.Abs(Vector3.Dot(relative, right)) * padding;
                float y = Mathf.Abs(Vector3.Dot(relative, cameraUp)) * padding;
                float depth = Vector3.Dot(relative, forward);
                distanceRequired = Mathf.Max(distanceRequired, x / tanHorizontal - depth);
                distanceRequired = Mathf.Max(distanceRequired, y / tanVertical - depth);
                distanceRequired = Mathf.Max(distanceRequired, -depth + 0.01f);
            }

            orthographicSize = 1f;
            position = bounds.center - forward * distanceRequired;
            nearClip = Mathf.Max(0.001f, distanceRequired + minDepth - margin);
            farClip = Mathf.Max(nearClip + 0.01f, distanceRequired + maxDepth + margin);
        }

        internal static void CalculateSphereFraming(
            Vector3 center,
            float radius,
            Vector3 viewDirection,
            float aspect,
            float fieldOfView,
            float padding,
            bool orthographic,
            out Vector3 position,
            out Quaternion rotation,
            out float orthographicSize,
            out float nearClip,
            out float farClip)
        {
            viewDirection = viewDirection.sqrMagnitude > 0.000001f
                ? viewDirection.normalized
                : Vector3.forward;
            aspect = Mathf.Max(0.0001f, aspect);
            fieldOfView = Mathf.Clamp(fieldOfView, 1.01f, 178.99f);
            radius = Mathf.Max(0.001f, radius);
            float paddedRadius = radius * Mathf.Max(0.0001f, padding);
            Vector3 forward = -viewDirection;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                ? Vector3.forward
                : Vector3.up;
            rotation = Quaternion.LookRotation(forward, up);

            if (orthographic)
            {
                orthographicSize = Mathf.Max(paddedRadius, paddedRadius / aspect);
                float distance = radius + 1f;
                position = center - forward * distance;
                nearClip = Mathf.Max(0.001f, distance - radius - 0.01f);
                farClip = distance + radius + 0.01f;
                return;
            }

            float verticalHalfAngle = fieldOfView * 0.5f * Mathf.Deg2Rad;
            float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * aspect);
            float limitingHalfAngle = Mathf.Max(
                0.0001f,
                Mathf.Min(verticalHalfAngle, horizontalHalfAngle));
            float distanceRequired = paddedRadius / Mathf.Sin(limitingHalfAngle);
            distanceRequired = Mathf.Max(distanceRequired, radius + 0.01f);
            orthographicSize = 1f;
            position = center - forward * distanceRequired;
            nearClip = Mathf.Max(0.001f, distanceRequired - radius - 0.01f);
            farClip = distanceRequired + radius + 0.01f;
        }

        static GameObject CreateHiddenObject(string name, Transform parent = null)
        {
            var result = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (parent != null)
                result.transform.SetParent(parent, false);

            return result;
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            root.hideFlags = HideFlags.HideAndDontSave;

            var transform = root.transform;

            for (int i = 0; i < transform.childCount; ++i)
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }

        LayerMask CalculateCachedSourceLayerMask()
        {
            int mask = _source != null ? 1 << _source.layer : 0;

            for (int i = 0; i < _renderers.Length; ++i)
            {
                if (_renderers[i] != null)
                    mask |= 1 << _renderers[i].gameObject.layer;
            }

            return mask;
        }

#if UNITY_INCLUDE_TESTS
        static int CountHierarchyObjects(Transform root)
        {
            if (root == null)
                return 0;

            int count = 1;

            for (int i = 0; i < root.childCount; ++i)
                count += CountHierarchyObjects(root.GetChild(i));

            return count;
        }
#endif

        static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        static void RequirePositiveFinite(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        static bool IsFinite(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
        }

        static float QuaternionMagnitudeSquared(Quaternion value)
        {
            return value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w;
        }

        static Quaternion Normalize(Quaternion value)
        {
            float inverseMagnitude = 1f / Mathf.Sqrt(QuaternionMagnitudeSquared(value));
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }

        void ThrowIfDisposed()
        {
            if (_disposed || _disposeRequested)
                throw new ObjectDisposedException(nameof(NowModelPreview));
        }
    }

    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    internal sealed class NowModelPreviewDriver : MonoBehaviour
    {
        void LateUpdate()
        {
            NowModelPreviewManager.RenderDeferred();
        }
    }

    internal static class NowModelPreviewManager
    {
        static readonly List<NowModelPreview> _previews = new List<NowModelPreview>(8);
        static readonly HashSet<Camera> _cameras = new HashSet<Camera>();
        static readonly List<Light> _sceneDirectionalLights = new List<Light>(4);
        static NowModelPreviewDriver _driver;
        static int _stageSerial;
        static int _sceneSerial;
        static int _previewSceneLeases;
        static Scene _previewScene;
        static Light _sharedKeyLight;
        static NowModelPreview.SharedLightSettings _sharedKeyLightSettings;
        static bool _sharedKeyLightSettingsValid;
        static bool _sceneDirectionalLightsDirty = true;
        static int _sceneDirectionalRefreshCount;
        static bool _sceneEventsRegistered;
        static bool _rendering;
        static NowModelPreview _renderingPreview;
        static int _pipelineBuildDepth;

        internal static bool isInsidePipelineBuild => _pipelineBuildDepth > 0;
        internal static bool isRendering => _rendering;
        internal static int sceneDirectionalRefreshCount => _sceneDirectionalRefreshCount;
        internal static readonly Vector3 sharedStageOrigin = new Vector3(0f, -16384f, 0f);

        public static Scene AcquirePreviewScene()
        {
            RegisterSceneEvents();

            if (!_previewScene.IsValid() || !_previewScene.isLoaded)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    _previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                else
#endif
                    _previewScene = SceneManager.CreateScene(
                        $"NowUI Model Previews {_sceneSerial++}",
                        new CreateSceneParameters(LocalPhysicsMode.None));
            }

            ++_previewSceneLeases;
            return _previewScene;
        }

        public static Light AcquireSharedKeyLight(Scene scene)
        {
            if (_sharedKeyLight != null)
                return _sharedKeyLight;

            var lightObject = new GameObject("Now Model Preview Key Light")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (scene.IsValid())
                SceneManager.MoveGameObjectToScene(lightObject, scene);

            _sharedKeyLight = lightObject.AddComponent<Light>();
            _sharedKeyLight.enabled = false;
            _sharedKeyLight.type = LightType.Directional;
            _sharedKeyLight.shadows = LightShadows.None;
            _sharedKeyLightSettingsValid = false;
            return _sharedKeyLight;
        }

        internal static bool IsSharedKeyLightConfigured(
            in NowModelPreview.SharedLightSettings settings)
        {
            return _sharedKeyLightSettingsValid && _sharedKeyLightSettings.Matches(settings);
        }

        internal static void MarkSharedKeyLightConfigured(
            in NowModelPreview.SharedLightSettings settings)
        {
            _sharedKeyLightSettings = settings;
            _sharedKeyLightSettingsValid = true;
        }

        internal static List<Light> GetSceneDirectionalLights()
        {
            if (_sceneDirectionalLightsDirty)
                RefreshSceneDirectionalLights();

            return _sceneDirectionalLights;
        }

        internal static void InvalidateSceneDirectionalLights()
        {
            _sceneDirectionalLightsDirty = true;
        }

        public static void ReleasePreviewScene(Scene scene)
        {
            if (!scene.IsValid() || scene != _previewScene)
                return;

            _previewSceneLeases = Mathf.Max(0, _previewSceneLeases - 1);

            if (_previewSceneLeases > 0)
                return;

            UnregisterSceneEvents();
            _sceneDirectionalLights.Clear();
            _sceneDirectionalLightsDirty = true;
            ClosePreviewScene();
        }

        public static Vector3 AllocateStageOrigin(bool unique)
        {
            if (!unique)
                return sharedStageOrigin;

            int slot = _stageSerial++;
            return new Vector3((slot & 63) * 16f, -16384f - (slot >> 6) * 16f, 0f);
        }

        public static void Register(NowModelPreview preview, Camera camera)
        {
            if (!_previews.Contains(preview))
                _previews.Add(preview);

            if (camera != null)
                _cameras.Add(camera);

            EnsureDriver();
            Wake();
        }

        public static void Unregister(NowModelPreview preview, Camera camera)
        {
            _previews.Remove(preview);

            if (camera != null)
                _cameras.Remove(camera);

            if (_previews.Count == 0 && _driver != null)
            {
                var driverObject = _driver.gameObject;
                _driver = null;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(driverObject);
                else
                    UnityEngine.Object.DestroyImmediate(driverObject);
            }
        }

        public static bool IsPreviewCamera(Camera camera)
        {
            return camera != null && _cameras.Contains(camera);
        }

        public static void RenderDeferred()
        {
            for (int i = _previews.Count - 1; i >= 0; --i)
                _previews[i]?.RenderDeferred();

            if (_driver == null)
                return;

            bool needsAnotherTick = false;

            for (int i = 0; i < _previews.Count; ++i)
            {
                if (_previews[i] != null && _previews[i].requiresDeferredTick)
                {
                    needsAnotherTick = true;
                    break;
                }
            }

            // Clean WhenDirty/Manual previews have zero per-frame manager cost.
            // Wake re-enables the driver when their state changes.
            _driver.enabled = needsAnotherTick;
        }

        public static bool TryBeginRender(NowModelPreview preview)
        {
            if (_rendering)
                return false;

            _rendering = true;
            _renderingPreview = preview;
            return true;
        }

        public static void EndRender(NowModelPreview preview)
        {
            _rendering = false;
            _renderingPreview = null;
            preview?.ApplyDeferredMutations();
        }

        public static bool IsRendering(NowModelPreview preview)
        {
            return _rendering && ReferenceEquals(_renderingPreview, preview);
        }

        internal static void BeginPipelineBuild()
        {
            ++_pipelineBuildDepth;
        }

        internal static void EndPipelineBuild()
        {
            _pipelineBuildDepth = Mathf.Max(0, _pipelineBuildDepth - 1);
        }

        public static void Wake()
        {
            EnsureDriver();

            if (_driver != null)
                _driver.enabled = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        static void EnsureDriver()
        {
            if (_driver != null || _previews.Count == 0)
                return;

            var driverObject = new GameObject("Now Model Preview Driver")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _driver = driverObject.AddComponent<NowModelPreviewDriver>();

            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(driverObject);
        }

        static void RegisterSceneEvents()
        {
            if (_sceneEventsRegistered)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _sceneEventsRegistered = true;
        }

        static void UnregisterSceneEvents()
        {
            if (!_sceneEventsRegistered)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _sceneEventsRegistered = false;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sceneDirectionalLightsDirty = true;
        }

        static void OnSceneUnloaded(Scene scene)
        {
            _sceneDirectionalLightsDirty = true;
        }

        static void RefreshSceneDirectionalLights()
        {
            ++_sceneDirectionalRefreshCount;
            _sceneDirectionalLights.Clear();
            var lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include);

            for (int i = 0; i < lights.Length; ++i)
            {
                var light = lights[i];

                if (light == null || light.type != LightType.Directional ||
                    light.gameObject.scene == _previewScene)
                    continue;

                _sceneDirectionalLights.Add(light);
            }

            _sceneDirectionalLightsDirty = false;
        }

        static void ClosePreviewScene()
        {
            var scene = _previewScene;
            _previewScene = default;
            _previewSceneLeases = 0;
            _sharedKeyLight = null;
            _sharedKeyLightSettings = default;
            _sharedKeyLightSettingsValid = false;

            if (!scene.IsValid() || !scene.isLoaded)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                return;
            }
#endif

            SceneManager.UnloadSceneAsync(scene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            while (_previews.Count > 0)
                _previews[_previews.Count - 1]?.Dispose();

            _previews.Clear();
            _cameras.Clear();
            UnregisterSceneEvents();
            _sceneDirectionalLights.Clear();
            _sceneDirectionalLightsDirty = true;
            _sceneDirectionalRefreshCount = 0;
            _driver = null;
            _stageSerial = 0;
            ClosePreviewScene();
            _sceneSerial = 0;
            _rendering = false;
            _renderingPreview = null;
            _pipelineBuildDepth = 0;
        }
    }
}
